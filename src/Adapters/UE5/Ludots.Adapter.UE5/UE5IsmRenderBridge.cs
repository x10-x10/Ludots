// ============================================================
//  UE5IsmRenderBridge.cs
//  Ludots → UE5 ISM 渲染数据桥接层
//
//  职责：
//    - 从 PrimitiveDrawBuffer 读取帧数据
//    - 按 RenderPath 分流：GpuSkinnedInstance → AllegroItems；其余 → HISM 桶
//    - HISM 桶：按 MeshAssetId 分桶，实例携带 StableId 供增量 diff
//    - Allegro 列表：每项携带 StableId、MeshAssetId、UE坐标位置
//    - 坐标系换算：Ludots (RH, Y-Up, 米) → UE5 (LH, Z-Up, 厘米)
//
//  UE5 C# 脚本侧每帧调用 CollectBuckets()，然后逐桶写入 ISM 组件。
//  本文件仅依赖 Ludots.Core，不引用任何 UE5 程序集。
// ============================================================

using System;
using System.Collections.Generic;
using System.Numerics;
using Ludots.Core.Presentation.Components;
using Ludots.Core.Presentation.Rendering;
using Ludots.Core.Scripting;
using Ludots.Core.Engine;

namespace Ludots.Adapter.UE5
{
    /// <summary>
    /// Allegro GPU 蒙皮渲染条目（对应 RenderPath.GpuSkinnedInstance）。
    /// 由 <see cref="UE5IsmRenderBridge.CollectBuckets"/> 填充，
    /// UE5 侧消费方通过 <see cref="StableId"/> 做增量 diff：Create / SetTransform / Destroy。
    /// </summary>
    public readonly struct AllegroDrawItem
    {
        /// <summary>
        /// 实体稳定 ID（Ludots ECS StableId），跨帧唯一，用于增量 diff。
        /// 相同 StableId 意味着同一实体，只需更新变换，无需重建。
        /// </summary>
        public int StableId { get; init; }

        /// <summary>Ludots MeshAssetId，用于在 DT 表中查询 DA 资源路径。</summary>
        public int MeshAssetId { get; init; }

        /// <summary>UE5 坐标系下的世界位置（厘米，左手 Z-Up）。</summary>
        public Vector3 Position { get; init; }

        /// <summary>
        /// 朝向（弧度，Yaw，UE5 Z 轴旋转正方向）。
        /// 暂时固定为 0，后续可由 AnimatorPackedState 或方向向量扩展。
        /// </summary>
        public float RotationYaw { get; init; }
    }

    /// <summary>
    /// 单个 ISM 桶的帧数据快照。
    /// 每个桶对应一种 <see cref="MeshAssetId"/>，即 UE5 侧的一个 InstancedStaticMeshComponent。
    /// </summary>
    public sealed class IsmBucket
    {
        /// <summary>Ludots MeshAssetId，与 UE5 侧 ISM 组件的映射键一致。</summary>
        public int MeshAssetId { get; }

        /// <summary>
        /// UE5 坐标系下的实例变换列表（厘米，左手 Z-Up）。
        /// 每个元素：(StableId, Translation, Scale)；StableId 用于增量 diff。
        /// </summary>
        public List<(int StableId, Vector3 Translation, Vector3 Scale)> Instances { get; } = new();

        public IsmBucket(int meshAssetId) => MeshAssetId = meshAssetId;

        internal void Clear() => Instances.Clear();
    }

    /// <summary>
    /// Ludots → UE5 ISM/Allegro 渲染数据桥接器。
    /// <para>
    /// 用法（UE5 C# 脚本每帧 Tick 中）：
    /// <code>
    ///   _ismBridge.CollectBuckets(_engine);
    ///   // HISM 路径：
    ///   foreach (var bucket in _ismBridge.HismBuckets)
    ///   {
    ///       foreach (var (stableId, t, s) in bucket.Instances) { ... }
    ///   }
    ///   // Allegro 路径：
    ///   foreach (var item in _ismBridge.AllegroItems) { ... }
    /// </code>
    /// </para>
    /// </summary>
    public sealed class UE5IsmRenderBridge
    {
        // ── 坐标系换算常量 ───────────────────────────────────────────────────
        // Ludots：右手坐标 Y-Up，单位 = 米
        // UE5  ：左手坐标 Z-Up，单位 = 厘米
        private const float MetersToUECm = 100f;

        // ── HISM 路径 ────────────────────────────────────────────────────────
        // 桶字典（按帧复用，避免每帧 GC 分配）
        private readonly Dictionary<int, IsmBucket> _buckets = new();

        // 向调用方暴露的只读视图（避免装箱）
        private readonly List<IsmBucket> _bucketList = new();

        // ── Allegro 路径 ─────────────────────────────────────────────────────
        private readonly List<AllegroDrawItem> _allegroItems = new();

        // ── 公共属性 ─────────────────────────────────────────────────────────

        /// <summary>本帧 HISM 桶列表（非 GpuSkinnedInstance 条目）。</summary>
        public IReadOnlyList<IsmBucket> HismBuckets => _bucketList;

        /// <summary>本帧 Allegro 渲染条目列表（RenderPath == GpuSkinnedInstance）。</summary>
        public IReadOnlyList<AllegroDrawItem> AllegroItems => _allegroItems;

        // ── 公共 API ─────────────────────────────────────────────────────────

        /// <summary>
        /// 从 <paramref name="engine"/> 的 GlobalContext 读取本帧 PrimitiveDrawBuffer，
        /// 按 RenderPath 分流：GpuSkinnedInstance → AllegroItems；其余 → HismBuckets。
        /// </summary>
        /// <remarks>
        /// 返回的列表在下一次 CollectBuckets 调用时会被重置，调用方不应跨帧持有。
        /// </remarks>
        public void CollectBuckets(GameEngine engine)
        {
            // 重置上一帧数据（复用桶对象，不重新分配）
            foreach (var b in _buckets.Values) b.Clear();
            _bucketList.Clear();
            _allegroItems.Clear();

            if (!engine.GlobalContext.TryGetValue(
                    CoreServiceKeys.PresentationPrimitiveDrawBuffer.Name,
                    out var rawBuf) || rawBuf is not PrimitiveDrawBuffer buf)
                return;

            ReadOnlySpan<PrimitiveDrawItem> span = buf.GetSpan();
            for (int i = 0; i < span.Length; i++)
            {
                ref readonly var item = ref span[i];

                // ── 按 RenderPath 分流 ────────────────────────────────────────
                if (item.RenderPath == VisualRenderPath.GpuSkinnedInstance)
                {
                    // → Allegro GPU 蒙皮渲染路径
                    _allegroItems.Add(new AllegroDrawItem
                    {
                        StableId    = item.StableId,
                        MeshAssetId = item.MeshAssetId,
                        Position    = ToUEPosition(item.Position),
                        RotationYaw = 0f,   // 暂无旋转字段，后续可从 AnimatorPackedState 提取
                    });
                    continue;
                }

                // → HISM 桶（StaticMesh / HISM / 其他路径）
                if (!_buckets.TryGetValue(item.MeshAssetId, out var bucket))
                {
                    bucket = new IsmBucket(item.MeshAssetId);
                    _buckets[item.MeshAssetId] = bucket;
                }

                // 仅在桶首次被本帧使用时（实例列表为空）加入 _bucketList，
                // 避免同一个桶被重复添加。
                // 注意：Clear() 已在帧头执行，Instances.Count==0 等价于"本帧尚未加入"。
                if (bucket.Instances.Count == 0)
                    _bucketList.Add(bucket);

                bucket.Instances.Add((item.StableId, ToUEPosition(item.Position), ToUEScale(item.Scale)));
            }
        }

        // ── 坐标换算 ─────────────────────────────────────────────────────────

        /// <summary>
        /// Ludots 世界坐标 (Y-Up, 米) → UE5 世界坐标 (Z-Up, 厘米)。
        /// 映射规则（与 LudotsCameraOutputSystem 一致）：
        ///   UE.X =  Ludots.X * 100
        ///   UE.Y =  Ludots.Z * 100
        ///   UE.Z =  Ludots.Y * 100   （Y-Up → Z-Up）
        /// </summary>
        public static Vector3 ToUEPosition(Vector3 ludots)
            => new Vector3(
                 ludots.X * MetersToUECm,
                 ludots.Z * MetersToUECm,
                 ludots.Y * MetersToUECm);

        /// <summary>
        /// Scale 不涉及手系翻转，但需要将 Y-Up 的轴对应到 Z-Up 轴：
        ///   UE.X = Ludots.X
        ///   UE.Y = Ludots.Z
        ///   UE.Z = Ludots.Y
        /// 单位保持无量纲（Scale 本身不需要乘以 100）。
        /// </summary>
        public static Vector3 ToUEScale(Vector3 ludotsScale)
            => new Vector3(ludotsScale.X, ludotsScale.Z, ludotsScale.Y);
    }
}
