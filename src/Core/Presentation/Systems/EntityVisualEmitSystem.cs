using Arch.Core;
using Arch.System;
using Ludots.Core.Presentation.Components;
using Ludots.Core.Presentation.Rendering;
using Ludots.Core.Presentation.Utils;

namespace Ludots.Core.Presentation.Systems
{
    public sealed class EntityVisualEmitSystem : BaseSystem<World, float>
    {
        private readonly PrimitiveDrawBuffer _drawBuffer;
        private readonly PrimitiveDrawBuffer? _snapshotBuffer;

        private readonly QueryDescription _withCullQuery = new QueryDescription()
            .WithAll<VisualTransform, VisualRuntimeState, CullState>();

        private readonly QueryDescription _withoutCullQuery = new QueryDescription()
            .WithAll<VisualTransform, VisualRuntimeState>()
            .WithNone<CullState>();

        public EntityVisualEmitSystem(World world, PrimitiveDrawBuffer drawBuffer, PrimitiveDrawBuffer? snapshotBuffer = null)
            : base(world)
        {
            _drawBuffer = drawBuffer;
            _snapshotBuffer = snapshotBuffer;
        }

        public override void Update(in float dt)
        {
            EmitWithCullState();
            EmitWithoutCullState();
        }

        private void EmitWithCullState()
        {
            var query = World.Query(in _withCullQuery);
            foreach (var chunk in query)
            {
                var transforms = chunk.GetArray<VisualTransform>();
                var visuals = chunk.GetArray<VisualRuntimeState>();
                var culls = chunk.GetArray<CullState>();
                for (int i = 0; i < chunk.Count; i++)
                {
                    Emit(chunk.Entity(i), visuals[i], transforms[i], culls[i].IsVisible);
                }
            }
        }

        private void EmitWithoutCullState()
        {
            var query = World.Query(in _withoutCullQuery);
            foreach (var chunk in query)
            {
                var transforms = chunk.GetArray<VisualTransform>();
                var visuals = chunk.GetArray<VisualRuntimeState>();
                for (int i = 0; i < chunk.Count; i++)
                {
                    Emit(chunk.Entity(i), visuals[i], transforms[i], cullVisible: true);
                }
            }
        }

        private void Emit(Entity entity, in VisualRuntimeState visual, in VisualTransform transform, bool cullVisible)
        {
            if (!visual.HasRenderableAsset)
            {
                return;
            }

            float baseScale = visual.BaseScale <= 0f ? 1f : visual.BaseScale;
            var scale = transform.Scale * baseScale;
            int stableId = World.Has<PresentationStableId>(entity) ? World.Get<PresentationStableId>(entity).Value : 0;
            int templateId = World.Has<VisualTemplateRef>(entity) ? World.Get<VisualTemplateRef>(entity).TemplateId : 0;
            AnimatorPackedState animator = World.Has<AnimatorPackedState>(entity) ? World.Get<AnimatorPackedState>(entity) : default;
            VisualVisibility visibility = visual.ResolveVisibility(cullVisible);

            var item = new PrimitiveDrawItem
            {
                MeshAssetId = visual.MeshAssetId,
                Position = transform.Position,
                Rotation = transform.Rotation,
                Scale = scale,
                Color = TeamColorResolver.Resolve(World, entity),
                StableId = stableId,
                MaterialId = visual.MaterialId,
                TemplateId = templateId,
                RenderPath = visual.RenderPath,
                Mobility = visual.Mobility,
                Flags = visual.Flags,
                Animator = animator,
                Visibility = visibility,
            };

            _snapshotBuffer?.TryAdd(item);

            if (visibility == VisualVisibility.Visible)
            {
                _drawBuffer.TryAdd(item);
            }
        }
    }
}
