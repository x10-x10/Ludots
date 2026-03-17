using System;
using System.IO;
using Arch.Core;
using Ludots.Core.Components;
using Ludots.Core.Config;
using Ludots.Core.Gameplay.GAS;
using Ludots.Core.Gameplay.GAS.Registry;
using Ludots.Core.Modding;
using Ludots.Core.Presentation;
using Ludots.Core.Presentation.Components;
using Ludots.Core.Presentation.Config;
using Ludots.Core.Presentation.Performers;
using Ludots.Core.Presentation.Projectiles;
using Ludots.Core.Presentation.Systems;
using Ludots.Core.Scripting;
using NUnit.Framework;
using static NUnit.Framework.Assert;

namespace Ludots.Tests.GAS
{
    public class ProjectilePresentationBridgeTests
    {
        [Test]
        public void ProjectilePresentationBindingLoader_ResolvesImpactEffectAndStartupPerformers()
        {
            string root = CreateTempRoot();
            try
            {
                Directory.CreateDirectory(Path.Combine(root, "Configs", "Presentation"));
                File.WriteAllText(
                    Path.Combine(root, "Configs", "Presentation", "projectile_cues.json"),
                    """
                    [
                      {
                        "id": "Effect.Test.ProjectileHit",
                        "startupPerformerIds": ["test.projectile.performer"]
                      }
                    ]
                    """);

                EffectTemplateIdRegistry.Clear();
                int impactEffectId = EffectTemplateIdRegistry.Register("Effect.Test.ProjectileHit");

                var performers = new PerformerDefinitionRegistry();
                int performerId = performers.Register("test.projectile.performer", new PerformerDefinition());

                var vfs = new VirtualFileSystem();
                vfs.Mount("Core", root);
                var modLoader = new ModLoader(vfs, new FunctionRegistry(), new TriggerManager());
                var pipeline = new ConfigPipeline(vfs, modLoader);

                var bindings = new ProjectilePresentationBindingRegistry();
                var loader = new ProjectilePresentationBindingConfigLoader(
                    pipeline,
                    bindings,
                    EffectTemplateIdRegistry.GetId,
                    performers.GetId);

                loader.Load(relativePath: "Presentation/projectile_cues.json");

                That(bindings.TryGet(impactEffectId, out var binding), Is.True);
                That(binding.ImpactEffectTemplateId, Is.EqualTo(impactEffectId));
                That(binding.StartupPerformers.Count, Is.EqualTo(1));
                That(binding.StartupPerformers.Get(0), Is.EqualTo(performerId));
            }
            finally
            {
                TryDeleteDirectory(root);
                EffectTemplateIdRegistry.Clear();
            }
        }

        [Test]
        public void ProjectilePresentationBootstrapSystem_AppliesStartupPerformersAndStableId()
        {
            using var world = World.Create();
            int impactEffectId = 42;

            var startupPerformers = default(PresentationStartupPerformers);
            startupPerformers.Count = 1;
            startupPerformers.Set(0, 777);

            var bindings = new ProjectilePresentationBindingRegistry();
            bindings.Register(impactEffectId, new ProjectilePresentationBinding(impactEffectId, in startupPerformers));

            Entity projectile = world.Create(
                new ProjectileState
                {
                    ImpactEffectTemplateId = impactEffectId,
                },
                WorldPositionCm.FromCm(120, 240),
                new PreviousWorldPositionCm { Value = WorldPositionCm.FromCm(120, 240).Value });

            using var system = new ProjectilePresentationBootstrapSystem(
                world,
                bindings,
                new PresentationStableIdAllocator());

            system.Update(0f);

            That(world.Has<ProjectilePresentationBootstrapState>(projectile), Is.True);
            That(world.Has<PresentationStableId>(projectile), Is.True);
            That(world.Get<PresentationStableId>(projectile).Value, Is.GreaterThan(0));
            That(world.Has<VisualTransform>(projectile), Is.True);
            That(world.Has<CullState>(projectile), Is.True);
            That(world.Has<PresentationStartupPerformers>(projectile), Is.True);
            That(world.Has<PresentationStartupState>(projectile), Is.True);

            var applied = world.Get<PresentationStartupPerformers>(projectile);
            That(applied.Count, Is.EqualTo(1));
            That(applied.Get(0), Is.EqualTo(777));
            That(world.Get<PresentationStartupState>(projectile).Initialized, Is.False);
        }

        private static string CreateTempRoot()
        {
            string root = Path.Combine(Path.GetTempPath(), "Ludots_ProjectilePresentationBridgeTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return root;
        }

        private static void TryDeleteDirectory(string path)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, recursive: true);
                }
            }
            catch
            {
            }
        }
    }
}
