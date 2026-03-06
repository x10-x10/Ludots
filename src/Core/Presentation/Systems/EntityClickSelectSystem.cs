using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Arch.Core;
using Arch.System;
using Ludots.Core.Components;
using Ludots.Core.Input.Runtime;
using Ludots.Core.Mathematics;
using Ludots.Core.Presentation.Utils;
using Ludots.Core.Scripting;
using Ludots.Core.Spatial;
using Ludots.Platform.Abstractions;

namespace Ludots.Core.Presentation.Systems
{
    /// <summary>
    /// 通用的鼠标点选实体系统。
    /// 
    /// 鼠标点击 "Select" → 屏幕射线打地面 → 空间查询最近实体 → 更新 SelectedEntity。
    /// 如果当前无选中实体或选中实体已死亡，自动回退到 LocalPlayerEntity。
    /// 
    /// 不包含任何游戏特定逻辑。视觉反馈（标记等）由 Mod 层通过 OnEntitySelected 回调处理。
    /// </summary>
    public sealed class EntityClickSelectSystem : ISystem<float>
    {
        private readonly World _world;
        private readonly Dictionary<string, object> _globals;
        private readonly ISpatialQueryService _spatial;
        private int _lastHoveredDebugEntityId = int.MinValue;

        /// <summary>拾取半径（厘米）。</summary>
        public int PickRadiusCm { get; set; } = 120;

        /// <summary>
        /// 选中实体后的回调。Mod 可注册此回调来添加视觉反馈。
        /// 参数：(WorldCmInt2 clickPoint, Entity selectedEntity)
        /// </summary>
        public Action<WorldCmInt2, Entity>? OnEntitySelected { get; set; }

        public EntityClickSelectSystem(World world, Dictionary<string, object> globals, ISpatialQueryService spatial)
        {
            _world = world;
            _globals = globals;
            _spatial = spatial;
        }

        public void Initialize() { }

        public void Update(in float dt)
        {
            if (!_globals.TryGetValue(CoreServiceKeys.InputHandler.Name, out var inputObj) || inputObj is not PlayerInputHandler input) return;
            if (!_globals.TryGetValue(CoreServiceKeys.ScreenRayProvider.Name, out var rayObj) || rayObj is not IScreenRayProvider rayProvider) return;

            var mouse = input.ReadAction<System.Numerics.Vector2>("PointerPos");
            var ray = rayProvider.GetRay(mouse);
            if (GroundRaycastUtil.TryGetGroundWorldCm(in ray, out var hoveredWorldCm))
            {
                var hovered = FindNearestEntity(hoveredWorldCm, PickRadiusCm);
                if (_world.IsAlive(hovered))
                {
                    _globals[CoreServiceKeys.HoveredEntity.Name] = hovered;
                }
                else
                {
                    _globals.Remove(CoreServiceKeys.HoveredEntity.Name);
                }

                int hoveredDebugId = _world.IsAlive(hovered) ? hovered.Id : 0;
                if (hoveredDebugId != _lastHoveredDebugEntityId)
                {
                    #region agent log
                    DebugLog("H1", "EntityClickSelectSystem.Update:hover", "Hovered entity changed", new
                    {
                        mouseX = (int)mouse.X,
                        mouseY = (int)mouse.Y,
                        worldX = hoveredWorldCm.X,
                        worldY = hoveredWorldCm.Y,
                        hoveredId = hoveredDebugId,
                        pickRadiusCm = PickRadiusCm
                    });
                    #endregion
                    _lastHoveredDebugEntityId = hoveredDebugId;
                }
            }
            else
            {
                _globals.Remove(CoreServiceKeys.HoveredEntity.Name);
                if (_lastHoveredDebugEntityId != 0)
                {
                    #region agent log
                    DebugLog("H1", "EntityClickSelectSystem.Update:hover", "Hovered entity cleared", new
                    {
                        mouseX = (int)mouse.X,
                        mouseY = (int)mouse.Y
                    });
                    #endregion
                    _lastHoveredDebugEntityId = 0;
                }
            }

            // 如果当前无选中实体或选中实体已死亡，自动回退到 LocalPlayerEntity
            if (!_globals.TryGetValue(CoreServiceKeys.SelectedEntity.Name, out var existingSelObj) 
                || existingSelObj is not Entity existingSel 
                || !_world.IsAlive(existingSel))
            {
                if (_globals.TryGetValue(CoreServiceKeys.LocalPlayerEntity.Name, out var localObj) && localObj is Entity local && _world.IsAlive(local))
                {
                    _globals[CoreServiceKeys.SelectedEntity.Name] = local;
                }
            }

            if (!input.PressedThisFrame("Select")) return;

            if (!GroundRaycastUtil.TryGetGroundWorldCm(in ray, out var worldCm)) return;

            var selected = FindNearestEntity(worldCm, PickRadiusCm);
            _globals[CoreServiceKeys.SelectedEntity.Name] = selected;

            #region agent log
            DebugLog("H2", "EntityClickSelectSystem.Update:select", "Select click resolved entity", new
            {
                mouseX = (int)mouse.X,
                mouseY = (int)mouse.Y,
                worldX = worldCm.X,
                worldY = worldCm.Y,
                selectedId = selected.Id
            });
            #endregion

            OnEntitySelected?.Invoke(worldCm, selected);
        }

        private Entity FindNearestEntity(in WorldCmInt2 worldCm, int radiusCm)
        {
            Span<Entity> buffer = stackalloc Entity[256];
            var result = _spatial.QueryRadius(worldCm, radiusCm, buffer);
            int count = result.Count;
            if (count <= 0) return default;

            Entity best = default;
            long bestD2 = long.MaxValue;

            for (int i = 0; i < count; i++)
            {
                var e = buffer[i];
                if (!_world.IsAlive(e)) continue;
                ref var pos = ref _world.TryGetRef<WorldPositionCm>(e, out bool hasPos);
                if (!hasPos) continue;

                var cmPos = pos.Value.ToWorldCmInt2();
                long dx = cmPos.X - worldCm.X;
                long dy = cmPos.Y - worldCm.Y;
                long d2 = dx * dx + dy * dy;
                if (d2 < bestD2)
                {
                    bestD2 = d2;
                    best = e;
                }
            }

            return best;
        }

        public void BeforeUpdate(in float dt) { }
        public void AfterUpdate(in float dt) { }
        public void Dispose() { }

        private static void DebugLog(string hypothesisId, string location, string message, object data)
        {
            File.AppendAllText("/opt/cursor/logs/debug.log",
                JsonSerializer.Serialize(new { hypothesisId, location, message, data, timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }) +
                Environment.NewLine);
        }
    }
}
