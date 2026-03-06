using Arch.Core;
using Ludots.Core.Mathematics.FixedPoint;
using Ludots.Core.Navigation2D.Components;
using Ludots.Core.Navigation2D.Runtime;
using Ludots.Core.Physics;
using Ludots.Core.Physics2D.Components;
using Ludots.Core.Physics2D.Systems;
using NUnit.Framework;

namespace Ludots.Tests.Navigation2D
{
    [TestFixture]
    [NonParallelizable]
    public sealed class Navigation2DSteeringSoATests
    {
        [Test]
        public void SteeringUpdate_PopulatesAgentSoA_FromKinematicsAndGoal()
        {
            using var world = World.Create();
            using var runtime = new Navigation2DRuntime(maxAgents: 16, gridCellSizeCm: 100, loadedChunks: null)
            {
                FlowIterationsPerTick = 0
            };

            var system = new Navigation2DSteeringSystem2D(world, runtime);
            var kinematics = new NavKinematics2D
            {
                MaxSpeedCmPerSec = Fix64.FromInt(250),
                MaxAccelCmPerSec2 = Fix64.FromInt(1200),
                RadiusCm = Fix64.FromInt(35),
                NeighborDistCm = Fix64.FromInt(420),
                TimeHorizonSec = Fix64.FromInt(3),
                MaxNeighbors = 7
            };

            world.Create(
                new NavAgent2D(),
                new NavGoal2D { Kind = NavGoalKind2D.Point, TargetCm = Fix64Vec2.FromInt(1000, 0), RadiusCm = Fix64.Zero },
                kinematics,
                new Position2D { Value = Fix64Vec2.Zero },
                Velocity2D.Zero,
                Mass2D.FromFloat(1f, 1f),
                new ForceInput2D { Force = Fix64Vec2.Zero },
                new NavDesiredVelocity2D { ValueCmPerSec = Fix64Vec2.Zero }
            );

            system.Update(1f / 60f);

            Assert.That(runtime.AgentSoA.Count, Is.EqualTo(1));
            Assert.That(runtime.AgentSoA.MaxAccels[0], Is.EqualTo(kinematics.MaxAccelCmPerSec2.ToFloat()).Within(0.001f));
            Assert.That(runtime.AgentSoA.NeighborDistances[0], Is.EqualTo(kinematics.NeighborDistCm.ToFloat()).Within(0.001f));
            Assert.That(runtime.AgentSoA.TimeHorizons[0], Is.EqualTo(kinematics.TimeHorizonSec.ToFloat()).Within(0.001f));
            Assert.That(runtime.AgentSoA.MaxNeighbors[0], Is.EqualTo(kinematics.MaxNeighbors));
            Assert.That(runtime.AgentSoA.PreferredVelocities[0].X, Is.EqualTo(kinematics.MaxSpeedCmPerSec.ToFloat()).Within(0.001f));
            Assert.That(runtime.AgentSoA.PreferredVelocities[0].Y, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void SteeringUpdate_RespectsPerAgentMaxNeighbors()
        {
            var desiredWithoutNeighbors = RunDesiredVelocity(maxNeighbors: 0);
            var desiredWithNeighbors = RunDesiredVelocity(maxNeighbors: 1);

            Assert.That(desiredWithoutNeighbors.X.ToFloat(), Is.EqualTo(100f).Within(0.01f));
            Assert.That(desiredWithoutNeighbors.Y.ToFloat(), Is.EqualTo(0f).Within(0.01f));

            bool changedByAvoidance =
                System.MathF.Abs(desiredWithNeighbors.X.ToFloat() - desiredWithoutNeighbors.X.ToFloat()) > 0.01f ||
                System.MathF.Abs(desiredWithNeighbors.Y.ToFloat() - desiredWithoutNeighbors.Y.ToFloat()) > 0.01f;

            Assert.That(changedByAvoidance, Is.True, "Neighbor-aware ORCA solve should differ from the zero-neighbor straight steering result.");
        }

        private static Fix64Vec2 RunDesiredVelocity(int maxNeighbors)
        {
            using var world = World.Create();
            using var runtime = new Navigation2DRuntime(maxAgents: 8, gridCellSizeCm: 100, loadedChunks: null)
            {
                FlowIterationsPerTick = 0
            };

            var system = new Navigation2DSteeringSystem2D(world, runtime);
            var actorKin = new NavKinematics2D
            {
                MaxSpeedCmPerSec = Fix64.FromInt(100),
                MaxAccelCmPerSec2 = Fix64.FromInt(5000),
                RadiusCm = Fix64.FromInt(30),
                NeighborDistCm = Fix64.FromInt(200),
                TimeHorizonSec = Fix64.FromInt(2),
                MaxNeighbors = maxNeighbors
            };

            var obstacleKin = new NavKinematics2D
            {
                MaxSpeedCmPerSec = Fix64.Zero,
                MaxAccelCmPerSec2 = Fix64.Zero,
                RadiusCm = Fix64.FromInt(30),
                NeighborDistCm = Fix64.FromInt(200),
                TimeHorizonSec = Fix64.FromInt(2),
                MaxNeighbors = 0
            };

            var actor = world.Create(
                new NavAgent2D(),
                new NavGoal2D { Kind = NavGoalKind2D.Point, TargetCm = Fix64Vec2.FromInt(1000, 0), RadiusCm = Fix64.Zero },
                actorKin,
                new Position2D { Value = Fix64Vec2.Zero },
                Velocity2D.Zero,
                Mass2D.FromFloat(1f, 1f),
                new ForceInput2D { Force = Fix64Vec2.Zero },
                new NavDesiredVelocity2D { ValueCmPerSec = Fix64Vec2.Zero }
            );

            world.Create(
                new NavAgent2D(),
                obstacleKin,
                new Position2D { Value = Fix64Vec2.FromInt(50, 0) },
                Velocity2D.Zero,
                Mass2D.FromFloat(1f, 1f),
                new ForceInput2D { Force = Fix64Vec2.Zero },
                new NavDesiredVelocity2D { ValueCmPerSec = Fix64Vec2.Zero }
            );

            system.Update(1f / 60f);
            return world.Get<NavDesiredVelocity2D>(actor).ValueCmPerSec;
        }
    }
}
