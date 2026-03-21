using System;
using Arch.Core;
using Arch.System;
using Ludots.Core.Components;
using Ludots.Core.Gameplay.GAS;
using Ludots.Core.Presentation.Components;
using Ludots.Core.Presentation.Projectiles;

namespace Ludots.Core.Presentation.Systems
{
    public sealed class ProjectilePresentationBootstrapSystem : BaseSystem<World, float>
    {
        private static readonly QueryDescription Query = new QueryDescription()
            .WithAll<ProjectileState, WorldPositionCm>()
            .WithNone<ProjectilePresentationBootstrapState>();

        private readonly ProjectilePresentationBindingRegistry _bindings;
        private readonly PresentationStableIdAllocator _stableIds;

        public ProjectilePresentationBootstrapSystem(
            World world,
            ProjectilePresentationBindingRegistry bindings,
            PresentationStableIdAllocator stableIds)
            : base(world)
        {
            _bindings = bindings ?? throw new ArgumentNullException(nameof(bindings));
            _stableIds = stableIds ?? throw new ArgumentNullException(nameof(stableIds));
        }

        public override void Update(in float dt)
        {
            var query = World.Query(in Query);
            foreach (var chunk in query)
            {
                var projectiles = chunk.GetArray<ProjectileState>();
                for (int i = 0; i < chunk.Count; i++)
                {
                    Entity entity = chunk.Entity(i);
                    World.Add(entity, new ProjectilePresentationBootstrapState());

                    ref readonly var projectile = ref projectiles[i];
                    int bindingEffectId = projectile.PresentationEffectTemplateId > 0
                        ? projectile.PresentationEffectTemplateId
                        : projectile.ImpactEffectTemplateId;
                    if (!_bindings.TryGet(bindingEffectId, out var binding))
                    {
                        continue;
                    }

                    var startupPerformers = binding.StartupPerformers;
                    EnsurePresentationBootstrap(entity, in startupPerformers);
                }
            }
        }

        private void EnsurePresentationBootstrap(Entity entity, in PresentationStartupPerformers startupPerformers)
        {
            if (!World.Has<VisualTransform>(entity))
            {
                World.Add(entity, VisualTransform.Default);
            }

            if (!World.Has<CullState>(entity))
            {
                World.Add(entity, new CullState { IsVisible = true, LOD = LODLevel.High });
            }

            if (World.Has<PresentationStartupPerformers>(entity))
            {
                World.Set(entity, startupPerformers);
            }
            else
            {
                World.Add(entity, startupPerformers);
            }

            if (World.Has<PresentationStartupState>(entity))
            {
                World.Set(entity, new PresentationStartupState { Initialized = false });
            }
            else
            {
                World.Add(entity, new PresentationStartupState { Initialized = false });
            }

            if (!World.Has<PresentationStableId>(entity))
            {
                World.Add(entity, new PresentationStableId { Value = _stableIds.Allocate() });
            }
        }
    }
}
