using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using Arch.Core;
using Arch.System;
using CameraAcceptanceMod.Runtime;
using Ludots.Core.Components;
using Ludots.Core.Engine;
using Ludots.Core.Gameplay.Spawning;
using Ludots.Core.Mathematics;
using Ludots.Core.Mathematics.FixedPoint;
using Ludots.Core.Map;
using Ludots.Core.Presentation.Assets;
using Ludots.Core.Presentation.Components;
using Ludots.Core.Presentation.Hud;
using Ludots.Core.Presentation.Rendering;
using Ludots.Core.Scripting;

namespace CameraAcceptanceMod.Systems
{
    internal sealed class CameraAcceptanceHotpathLaneSystem : ISystem<float>
    {
        private static readonly QueryDescription UntaggedDummyQuery = new QueryDescription()
            .WithAll<Name, MapEntity>()
            .WithNone<CameraAcceptanceHotpathCrowdTag>();

        private static readonly QueryDescription TaggedCrowdQuery = new QueryDescription()
            .WithAll<CameraAcceptanceHotpathCrowdTag, MapEntity>();

        private static readonly QueryDescription TaggedVisibleCrowdQuery = new QueryDescription()
            .WithAll<CameraAcceptanceHotpathCrowdTag, MapEntity, CullState>();

        private static readonly QueryDescription TaggedVisibleCrowdPositionQuery = new QueryDescription()
            .WithAll<CameraAcceptanceHotpathCrowdTag, MapEntity, WorldPositionCm, CullState>();

        private static readonly QueryDescription TaggedVisibleCrowdVisualQuery = new QueryDescription()
            .WithAll<CameraAcceptanceHotpathCrowdTag, MapEntity, VisualTransform, CullState>();

        private static readonly Vector4 BarBackground = new(0.14f, 0.18f, 0.24f, 0.92f);
        private static readonly Vector4 BarForeground = new(0.12f, 0.84f, 0.62f, 0.96f);
        private static readonly Vector4 TextColor = new(0.96f, 0.92f, 0.68f, 1f);

        private readonly GameEngine _engine;
        private readonly List<Entity> _tagBuffer = new(2048);
        private readonly List<Entity> _crowdBuffer = new(CameraAcceptanceIds.HotpathCrowdTargetCount);
        private readonly List<VisibleSampleEntry> _visibleSampleBuffer = new(4096);
        private string[] _frozenVisibleSampleWindow = Array.Empty<string>();
        private string _frozenVisibleSamplePhase = string.Empty;
        private string _frozenVisibleSampleTarget = string.Empty;
        private int _frozenVisibleSampleCycle = -1;
        private int _frozenVisibleSampleStride = 1;
        private int _cubeMeshAssetId;
        private int _sphereMeshAssetId;

        public CameraAcceptanceHotpathLaneSystem(GameEngine engine)
        {
            _engine = engine;
        }

        public void Initialize() { }
        public void BeforeUpdate(in float dt) { }
        public void AfterUpdate(in float dt) { }
        public void Dispose() { }

        public void Update(in float dt)
        {
            MapId currentMapId = _engine.CurrentMapSession?.MapId ?? default;
            if (!string.Equals(currentMapId.Value, CameraAcceptanceIds.HotpathMapId, StringComparison.OrdinalIgnoreCase))
            {
                ResetHotpathState();
                return;
            }

            if (_engine.GetService(CameraAcceptanceServiceKeys.DiagnosticsState) is not CameraAcceptanceDiagnosticsState diagnostics)
            {
                return;
            }

            TagExistingCrowd(currentMapId);

            int crowdCount = CountCrowd(currentMapId);
            if (!diagnostics.HotpathCullCrowdEnabled)
            {
                if (crowdCount > 0)
                {
                    DestroyCrowd(currentMapId);
                }

                ResetFrozenVisibleSample();
                diagnostics.PublishHotpathLaneCounts(0, 0, 0, 0, 0);
                diagnostics.PublishHotpathVisibleSample(Array.Empty<string>(), 1);
                diagnostics.ObserveHotpathBars(0d);
                diagnostics.ObserveHotpathHudText(0d);
                diagnostics.ObserveHotpathPrimitives(0d);
                diagnostics.ObserveHotpathVisibleSample(0d);
                return;
            }

            if (crowdCount > CameraAcceptanceIds.HotpathCrowdTargetCount)
            {
                crowdCount = TrimCrowdToTarget(currentMapId);
            }
            else if (crowdCount < CameraAcceptanceIds.HotpathCrowdTargetCount)
            {
                EnqueueCrowdSpawns(currentMapId, crowdCount);
            }

            int visibleCrowdCount = CountVisibleCrowd(currentMapId);
            int barCount = EmitBars(diagnostics, currentMapId);
            int textCount = EmitHudText(diagnostics, currentMapId);
            int primitiveCount = EmitPrimitives(diagnostics, currentMapId);
            PublishVisibleEntitySample(diagnostics, currentMapId, visibleCrowdCount);
            diagnostics.PublishHotpathLaneCounts(crowdCount, visibleCrowdCount, barCount, textCount, primitiveCount);
        }

        private void ResetHotpathState()
        {
            ResetFrozenVisibleSample();
            if (_engine.GetService(CameraAcceptanceServiceKeys.DiagnosticsState) is CameraAcceptanceDiagnosticsState diagnostics)
            {
                diagnostics.ResetHotpathState();
            }
        }

        private void TagExistingCrowd(MapId currentMapId)
        {
            _tagBuffer.Clear();
            _engine.World.Query(in UntaggedDummyQuery, (Entity entity, ref Name name, ref MapEntity mapEntity) =>
            {
                if (MatchesMap(mapEntity, currentMapId) &&
                    string.Equals(name.Value, "Dummy", StringComparison.OrdinalIgnoreCase))
                {
                    _tagBuffer.Add(entity);
                }
            });

            for (int i = 0; i < _tagBuffer.Count; i++)
            {
                Entity entity = _tagBuffer[i];
                if (_engine.World.IsAlive(entity) && !_engine.World.Has<CameraAcceptanceHotpathCrowdTag>(entity))
                {
                    _engine.World.Add(entity, new CameraAcceptanceHotpathCrowdTag());
                }
            }
        }

        private int CountCrowd(MapId currentMapId)
        {
            int crowdCount = 0;
            _engine.World.Query(in TaggedCrowdQuery, (ref CameraAcceptanceHotpathCrowdTag _, ref MapEntity mapEntity) =>
            {
                if (MatchesMap(mapEntity, currentMapId))
                {
                    crowdCount++;
                }
            });

            return crowdCount;
        }

        private int CountVisibleCrowd(MapId currentMapId)
        {
            int visibleCount = 0;
            _engine.World.Query(in TaggedVisibleCrowdQuery, (ref CameraAcceptanceHotpathCrowdTag _, ref MapEntity mapEntity, ref CullState cull) =>
            {
                if (MatchesMap(mapEntity, currentMapId) && cull.IsVisible)
                {
                    visibleCount++;
                }
            });

            return visibleCount;
        }

        private void DestroyCrowd(MapId currentMapId)
        {
            _crowdBuffer.Clear();
            _engine.World.Query(in TaggedCrowdQuery, (Entity entity, ref CameraAcceptanceHotpathCrowdTag _, ref MapEntity mapEntity) =>
            {
                if (MatchesMap(mapEntity, currentMapId))
                {
                    _crowdBuffer.Add(entity);
                }
            });

            for (int i = 0; i < _crowdBuffer.Count; i++)
            {
                if (_engine.World.IsAlive(_crowdBuffer[i]))
                {
                    _engine.World.Destroy(_crowdBuffer[i]);
                }
            }
        }

        private int TrimCrowdToTarget(MapId currentMapId)
        {
            _crowdBuffer.Clear();
            _engine.World.Query(in TaggedCrowdQuery, (Entity entity, ref CameraAcceptanceHotpathCrowdTag _, ref MapEntity mapEntity) =>
            {
                if (MatchesMap(mapEntity, currentMapId))
                {
                    _crowdBuffer.Add(entity);
                }
            });

            for (int i = _crowdBuffer.Count - 1; i >= CameraAcceptanceIds.HotpathCrowdTargetCount; i--)
            {
                Entity entity = _crowdBuffer[i];
                if (_engine.World.IsAlive(entity))
                {
                    _engine.World.Destroy(entity);
                }
            }

            return Math.Min(_crowdBuffer.Count, CameraAcceptanceIds.HotpathCrowdTargetCount);
        }

        private void EnqueueCrowdSpawns(MapId currentMapId, int crowdCount)
        {
            if (_engine.GetService(CoreServiceKeys.RuntimeEntitySpawnQueue) is not RuntimeEntitySpawnQueue spawnQueue)
            {
                throw new InvalidOperationException("RuntimeEntitySpawnQueue is required for the camera hotpath acceptance scenario.");
            }

            int remaining = CameraAcceptanceIds.HotpathCrowdTargetCount - crowdCount;
            int available = spawnQueue.Capacity - spawnQueue.Count;
            int toEnqueue = Math.Min(remaining, available);
            for (int i = 0; i < toEnqueue; i++)
            {
                WorldCmInt2 spawnWorldCm = ResolveCrowdPosition(crowdCount + i);
                var request = new RuntimeEntitySpawnRequest
                {
                    Kind = RuntimeEntitySpawnKind.Template,
                    TemplateId = CameraAcceptanceIds.HotpathCrowdTemplateId,
                    WorldPositionCm = Fix64Vec2.FromInt(spawnWorldCm.X, spawnWorldCm.Y),
                    MapId = currentMapId,
                };

                if (!spawnQueue.TryEnqueue(request))
                {
                    break;
                }
            }
        }

        private int EmitBars(CameraAcceptanceDiagnosticsState diagnostics, MapId currentMapId)
        {
            long start = Stopwatch.GetTimestamp();
            int emitted = 0;

            if (diagnostics.HotpathBarsEnabled &&
                _engine.GetService(CoreServiceKeys.PresentationWorldHudBuffer) is WorldHudBatchBuffer worldHud)
            {
                _engine.World.Query(in TaggedVisibleCrowdVisualQuery, (Entity entity, ref CameraAcceptanceHotpathCrowdTag _, ref MapEntity mapEntity, ref VisualTransform transform, ref CullState cull) =>
                {
                    if (!MatchesMap(mapEntity, currentMapId) || !cull.IsVisible)
                    {
                        return;
                    }

                    float fill = 0.28f + ((entity.Id % 9) * 0.07f);
                    if (fill > 0.98f)
                    {
                        fill = 0.98f;
                    }

                    if (worldHud.TryAdd(new WorldHudItem
                    {
                        Kind = WorldHudItemKind.Bar,
                        WorldPosition = transform.Position + new Vector3(0f, 1.65f, 0f),
                        Width = 56f,
                        Height = 7f,
                        Value0 = fill,
                        Color0 = BarBackground,
                        Color1 = BarForeground,
                    }))
                    {
                        emitted++;
                    }
                });
            }

            diagnostics.ObserveHotpathBars((Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency);
            return emitted;
        }

        private int EmitHudText(CameraAcceptanceDiagnosticsState diagnostics, MapId currentMapId)
        {
            long start = Stopwatch.GetTimestamp();
            int emitted = 0;

            if (diagnostics.HotpathHudTextEnabled &&
                _engine.GetService(CoreServiceKeys.PresentationWorldHudBuffer) is WorldHudBatchBuffer worldHud)
            {
                _engine.World.Query(in TaggedVisibleCrowdVisualQuery, (Entity entity, ref CameraAcceptanceHotpathCrowdTag _, ref MapEntity mapEntity, ref VisualTransform transform, ref CullState cull) =>
                {
                    if (!MatchesMap(mapEntity, currentMapId) || !cull.IsVisible)
                    {
                        return;
                    }

                    if (worldHud.TryAdd(new WorldHudItem
                    {
                        Kind = WorldHudItemKind.Text,
                        WorldPosition = transform.Position + new Vector3(0f, 2.15f, 0f),
                        FontSize = 14,
                        Color0 = TextColor,
                        Value0 = 100 + (entity.Id % 900),
                        Id1 = (int)WorldHudValueMode.Constant,
                    }))
                    {
                        emitted++;
                    }
                });
            }

            diagnostics.ObserveHotpathHudText((Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency);
            return emitted;
        }

        private int EmitPrimitives(CameraAcceptanceDiagnosticsState diagnostics, MapId currentMapId)
        {
            long start = Stopwatch.GetTimestamp();
            int emitted = 0;

            if (_engine.GetService(CoreServiceKeys.RenderDebugState) is RenderDebugState renderDebug &&
                renderDebug.DrawPrimitives &&
                _engine.GetService(CoreServiceKeys.PresentationPrimitiveDrawBuffer) is PrimitiveDrawBuffer primitives &&
                _engine.GetService(CoreServiceKeys.PresentationMeshAssetRegistry) is MeshAssetRegistry meshes)
            {
                ResolvePrimitiveMeshIds(meshes);

                _engine.World.Query(in TaggedVisibleCrowdVisualQuery, (Entity entity, ref CameraAcceptanceHotpathCrowdTag _, ref MapEntity mapEntity, ref VisualTransform transform, ref CullState cull) =>
                {
                    if (!MatchesMap(mapEntity, currentMapId) || !cull.IsVisible)
                    {
                        return;
                    }

                    int meshAssetId = (entity.Id & 1) == 0 ? _cubeMeshAssetId : _sphereMeshAssetId;
                    if (meshAssetId == 0)
                    {
                        return;
                    }

                    float scale = 0.26f + ((entity.Id % 4) * 0.04f);
                    if (primitives.TryAdd(new PrimitiveDrawItem
                    {
                        MeshAssetId = meshAssetId,
                        Position = transform.Position + new Vector3(0f, 0.35f + ((entity.Id % 3) * 0.05f), 0f),
                        Scale = new Vector3(scale),
                        Color = ResolvePrimitiveColor(entity.Id),
                        StableId = 500000 + entity.Id,
                        RenderPath = VisualRenderPath.InstancedStaticMesh,
                        Mobility = VisualMobility.Static,
                        Flags = VisualRuntimeFlags.Visible,
                    }))
                    {
                        emitted++;
                    }
                });
            }

            diagnostics.ObserveHotpathPrimitives((Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency);
            return emitted;
        }

        private void PublishVisibleEntitySample(CameraAcceptanceDiagnosticsState diagnostics, MapId currentMapId, int visibleCrowdCount)
        {
            long start = Stopwatch.GetTimestamp();
            bool freezeHoldSample = IsHoldPhase(diagnostics.HotpathSweepPhase);

            if (!freezeHoldSample)
            {
                ResetFrozenVisibleSample();
            }
            else if (HasFrozenVisibleSample(diagnostics))
            {
                diagnostics.PublishHotpathVisibleSample(_frozenVisibleSampleWindow, _frozenVisibleSampleStride);
                diagnostics.ObserveHotpathVisibleSample((Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency);
                return;
            }

            if (_engine.GetService(CoreServiceKeys.RenderDebugState) is not RenderDebugState renderDebug || !renderDebug.DrawSkiaUi || visibleCrowdCount <= 0)
            {
                diagnostics.PublishHotpathVisibleSample(Array.Empty<string>(), 1);
                diagnostics.ObserveHotpathVisibleSample((Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency);
                return;
            }

            int sampleLimit = CameraAcceptanceIds.HotpathVisibleSampleLimit;
            _visibleSampleBuffer.Clear();

            _engine.World.Query(in TaggedVisibleCrowdPositionQuery, (Entity entity, ref CameraAcceptanceHotpathCrowdTag _, ref MapEntity mapEntity, ref WorldPositionCm position, ref CullState cull) =>
            {
                if (!MatchesMap(mapEntity, currentMapId) || !cull.IsVisible)
                {
                    return;
                }

                WorldCmInt2 worldCm = position.Value.ToWorldCmInt2();
                _visibleSampleBuffer.Add(new VisibleSampleEntry(entity.Id, $"#{entity.Id} @ {worldCm.X},{worldCm.Y}"));
            });

            _visibleSampleBuffer.Sort((left, right) => left.Id.CompareTo(right.Id));

            int stableVisibleCount = _visibleSampleBuffer.Count;
            int stride = Math.Max(1, (stableVisibleCount + sampleLimit - 1) / sampleLimit);
            var lines = new List<string>(sampleLimit);
            for (int i = 0; i < stableVisibleCount && lines.Count < sampleLimit; i += stride)
            {
                lines.Add(_visibleSampleBuffer[i].Line);
            }

            string[] sampleLines = lines.ToArray();
            if (freezeHoldSample)
            {
                FreezeVisibleSample(diagnostics, sampleLines, stride);
            }

            diagnostics.PublishHotpathVisibleSample(sampleLines, stride);
            diagnostics.ObserveHotpathVisibleSample((Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency);
        }

        private void ResolvePrimitiveMeshIds(MeshAssetRegistry meshes)
        {
            if (_cubeMeshAssetId == 0)
            {
                _cubeMeshAssetId = meshes.GetId(WellKnownMeshKeys.Cube);
            }

            if (_sphereMeshAssetId == 0)
            {
                _sphereMeshAssetId = meshes.GetId(WellKnownMeshKeys.Sphere);
            }
        }

        private static Vector4 ResolvePrimitiveColor(int entityId)
        {
            float tint = (entityId % 7) / 6f;
            return new Vector4(0.22f + (tint * 0.58f), 0.38f + (tint * 0.27f), 0.86f - (tint * 0.31f), 0.94f);
        }

        private static bool MatchesMap(in MapEntity mapEntity, in MapId currentMapId)
        {
            return string.Equals(mapEntity.MapId.Value, currentMapId.Value, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsHoldPhase(string phase)
        {
            return phase.StartsWith("hold-", StringComparison.Ordinal);
        }

        private bool HasFrozenVisibleSample(CameraAcceptanceDiagnosticsState diagnostics)
        {
            return _frozenVisibleSampleWindow.Length > 0 &&
                   string.Equals(_frozenVisibleSamplePhase, diagnostics.HotpathSweepPhase, StringComparison.Ordinal) &&
                   string.Equals(_frozenVisibleSampleTarget, diagnostics.HotpathSweepTarget, StringComparison.Ordinal) &&
                   _frozenVisibleSampleCycle == diagnostics.HotpathSweepCycle;
        }

        private void FreezeVisibleSample(CameraAcceptanceDiagnosticsState diagnostics, string[] sampleLines, int stride)
        {
            _frozenVisibleSampleWindow = sampleLines;
            _frozenVisibleSamplePhase = diagnostics.HotpathSweepPhase;
            _frozenVisibleSampleTarget = diagnostics.HotpathSweepTarget;
            _frozenVisibleSampleCycle = diagnostics.HotpathSweepCycle;
            _frozenVisibleSampleStride = stride <= 0 ? 1 : stride;
        }

        private void ResetFrozenVisibleSample()
        {
            _frozenVisibleSampleWindow = Array.Empty<string>();
            _frozenVisibleSamplePhase = string.Empty;
            _frozenVisibleSampleTarget = string.Empty;
            _frozenVisibleSampleCycle = -1;
            _frozenVisibleSampleStride = 1;
        }

        internal static WorldCmInt2 ResolveCrowdPosition(int index)
        {
            int row = index / CameraAcceptanceIds.HotpathCrowdColumns;
            int column = index % CameraAcceptanceIds.HotpathCrowdColumns;
            int x = CameraAcceptanceIds.HotpathCrowdBaseX + (column * CameraAcceptanceIds.HotpathCrowdSpacingX);
            int y = CameraAcceptanceIds.HotpathCrowdBaseY + (row * CameraAcceptanceIds.HotpathCrowdSpacingY) +
                    (((column & 1) == 0) ? 0 : CameraAcceptanceIds.HotpathCrowdOddColumnOffsetY);
            return new WorldCmInt2(x, y);
        }

        private readonly record struct VisibleSampleEntry(int Id, string Line);
    }
}
