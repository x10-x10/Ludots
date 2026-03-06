using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Ludots.Core.Engine;
using Ludots.Core.Gameplay.Camera;
using Ludots.Core.Scripting;
using NUnit.Framework;

namespace Ludots.Tests.Camera
{
    [TestFixture]
    public class VirtualCameraTests
    {
        private sealed class AddYawController : ICameraController
        {
            private readonly float _deltaYaw;

            public AddYawController(float deltaYaw)
            {
                _deltaYaw = deltaYaw;
            }

            public void Update(CameraState state, float dt)
            {
                state.Yaw += _deltaYaw;
            }
        }

        [Test]
        public void CameraManager_WithActiveVirtualCamera_BlendsTowardDefinition()
        {
            var manager = new CameraManager();
            var registry = new VirtualCameraRegistry();
            var brain = new VirtualCameraBrain(registry);
            manager.SetVirtualCameraBrain(brain);

            manager.State.TargetCm = Vector2.Zero;
            manager.State.Yaw = 0f;
            manager.State.Pitch = 30f;
            manager.State.DistanceCm = 1000f;
            manager.State.FovYDeg = 60f;

            registry.Register(new VirtualCameraDefinition
            {
                Id = "closeup",
                FixedTargetCm = new Vector2(1000f, 500f),
                Yaw = 90f,
                Pitch = 60f,
                DistanceCm = 5000f,
                FovYDeg = 50f,
                DefaultBlendDuration = 1f,
                BlendCurve = CameraBlendCurve.Linear
            });

            brain.Activate("closeup", manager.State);
            manager.Update(0.25f);

            Assert.That(manager.State.TargetCm.X, Is.EqualTo(250f).Within(0.001f));
            Assert.That(manager.State.TargetCm.Y, Is.EqualTo(125f).Within(0.001f));
            Assert.That(manager.State.Yaw, Is.EqualTo(22.5f).Within(0.001f));
            Assert.That(manager.State.Pitch, Is.EqualTo(37.5f).Within(0.001f));
            Assert.That(manager.State.DistanceCm, Is.EqualTo(2000f).Within(0.001f));
            Assert.That(manager.State.FovYDeg, Is.EqualTo(57.5f).Within(0.001f));
        }

        [Test]
        public void CameraManager_WithFollowVirtualCamera_UsesFollowTarget()
        {
            var manager = new CameraManager();
            var registry = new VirtualCameraRegistry();
            var brain = new VirtualCameraBrain(registry);
            manager.SetVirtualCameraBrain(brain);
            manager.FollowTargetPositionCm = new Vector2(640f, 320f);

            registry.Register(new VirtualCameraDefinition
            {
                Id = "follow",
                TargetSource = VirtualCameraTargetSource.FollowTarget,
                Yaw = 180f,
                Pitch = 55f,
                DistanceCm = 9000f,
                DefaultBlendDuration = 0f,
                BlendCurve = CameraBlendCurve.Cut
            });

            brain.Activate("follow", manager.State);
            manager.Update(0.016f);

            Assert.That(manager.State.TargetCm, Is.EqualTo(new Vector2(640f, 320f)));
            Assert.That(manager.State.IsFollowing, Is.True);
            Assert.That(manager.State.DistanceCm, Is.EqualTo(9000f));
        }

        [Test]
        public void CameraManager_WithInputEnabledVirtualCamera_PersistsControllerChanges()
        {
            var manager = new CameraManager();
            var registry = new VirtualCameraRegistry();
            var brain = new VirtualCameraBrain(registry);
            manager.SetVirtualCameraBrain(brain);
            manager.SetController(new AddYawController(5f));

            registry.Register(new VirtualCameraDefinition
            {
                Id = "free_orbit",
                FixedTargetCm = new Vector2(100f, 200f),
                Yaw = 10f,
                Pitch = 45f,
                DistanceCm = 3000f,
                DefaultBlendDuration = 0f,
                BlendCurve = CameraBlendCurve.Cut,
                AllowUserInput = true
            });

            brain.Activate("free_orbit", manager.State);
            manager.Update(0.016f);
            Assert.That(manager.State.Yaw, Is.EqualTo(15f).Within(0.001f));

            manager.Update(0.016f);
            Assert.That(manager.State.Yaw, Is.EqualTo(20f).Within(0.001f));
        }

        [Test]
        public void GameEngine_VirtualCameraRequest_ActivatesRegisteredCamera()
        {
            string repoRoot = FindRepoRoot();
            string assetsRoot = Path.Combine(repoRoot, "assets");
            string modsRoot = Path.Combine(repoRoot, "mods");

            var engine = new GameEngine();
            try
            {
                engine.InitializeWithConfigPipeline(
                    new List<string>
                    {
                        Path.Combine(modsRoot, "LudotsCoreMod"),
                        Path.Combine(modsRoot, "CoreInputMod")
                    },
                    assetsRoot);

                var registry = engine.GetService(CoreServiceKeys.VirtualCameraRegistry);
                Assert.That(registry, Is.Not.Null);

                registry.Register(new VirtualCameraDefinition
                {
                    Id = "test_shot",
                    FixedTargetCm = new Vector2(1200f, 800f),
                    Yaw = 135f,
                    Pitch = 50f,
                    DistanceCm = 7777f,
                    DefaultBlendDuration = 0f,
                    BlendCurve = CameraBlendCurve.Cut
                });

                engine.Start();
                engine.SetService(CoreServiceKeys.VirtualCameraRequest, new VirtualCameraRequest
                {
                    Id = "test_shot",
                    BlendDurationSeconds = 0f
                });

                engine.Tick(1f / 60f);

                Assert.That(engine.GameSession.Camera.VirtualCameraBrain, Is.Not.Null);
                Assert.That(engine.GameSession.Camera.VirtualCameraBrain.HasActiveCamera, Is.True);
                Assert.That(engine.GameSession.Camera.VirtualCameraBrain.ActiveCameraId, Is.EqualTo("test_shot"));
                Assert.That(engine.GameSession.Camera.State.TargetCm, Is.EqualTo(new Vector2(1200f, 800f)));
                Assert.That(engine.GameSession.Camera.State.DistanceCm, Is.EqualTo(7777f));
            }
            finally
            {
                engine.Dispose();
            }
        }

        private static string FindRepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            for (int i = 0; i < 10 && dir != null; i++)
            {
                var srcDir = Path.Combine(dir.FullName, "src");
                var assetsDir = Path.Combine(dir.FullName, "assets");
                if (Directory.Exists(srcDir) && Directory.Exists(assetsDir))
                    return dir.FullName;

                dir = dir.Parent;
            }

            throw new DirectoryNotFoundException("Failed to locate repository root from test output directory.");
        }
    }
}
