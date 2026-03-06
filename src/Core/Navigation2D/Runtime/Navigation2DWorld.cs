using System;
using System.Numerics;
using Arch.LowLevel;

namespace Ludots.Core.Navigation2D.Runtime
{
    public sealed unsafe class Navigation2DWorld : IDisposable
    {
        public readonly Navigation2DWorldSettings Settings;

        public UnsafeList<Vector2> Positions;
        public UnsafeList<Vector2> Velocities;
        public UnsafeList<float> Radii;
        public UnsafeList<float> MaxSpeeds;
        public UnsafeList<float> MaxAccels;
        public UnsafeList<float> NeighborDistances;
        public UnsafeList<float> TimeHorizons;
        public UnsafeList<int> MaxNeighbors;
        public UnsafeList<Vector2> PreferredVelocities;
        public UnsafeList<Vector2> OutputForces;
        public UnsafeList<Vector2> OutputDesiredVelocities;

        public int Count => Positions.Count;

        public Navigation2DWorld(Navigation2DWorldSettings settings)
        {
            Settings = settings;

            Positions = new UnsafeList<Vector2>(settings.MaxAgents);
            Velocities = new UnsafeList<Vector2>(settings.MaxAgents);
            Radii = new UnsafeList<float>(settings.MaxAgents);
            MaxSpeeds = new UnsafeList<float>(settings.MaxAgents);
            MaxAccels = new UnsafeList<float>(settings.MaxAgents);
            NeighborDistances = new UnsafeList<float>(settings.MaxAgents);
            TimeHorizons = new UnsafeList<float>(settings.MaxAgents);
            MaxNeighbors = new UnsafeList<int>(settings.MaxAgents);
            PreferredVelocities = new UnsafeList<Vector2>(settings.MaxAgents);
            OutputForces = new UnsafeList<Vector2>(settings.MaxAgents);
            OutputDesiredVelocities = new UnsafeList<Vector2>(settings.MaxAgents);
        }

        public bool TryAdd(
            in Vector2 position,
            in Vector2 velocity,
            float radius,
            float maxSpeed,
            float maxAccel,
            float neighborDistance,
            float timeHorizon,
            int maxNeighbors,
            in Vector2 preferredVelocity)
        {
            if (Count >= Settings.MaxAgents)
            {
                return false;
            }

            Positions.Add(position);
            Velocities.Add(velocity);
            Radii.Add(radius);
            MaxSpeeds.Add(maxSpeed);
            MaxAccels.Add(maxAccel);
            NeighborDistances.Add(neighborDistance);
            TimeHorizons.Add(timeHorizon);
            MaxNeighbors.Add(maxNeighbors);
            PreferredVelocities.Add(preferredVelocity);
            OutputForces.Add(Vector2.Zero);
            OutputDesiredVelocities.Add(Vector2.Zero);
            return true;
        }

        public void Clear()
        {
            Positions.Clear();
            Velocities.Clear();
            Radii.Clear();
            MaxSpeeds.Clear();
            MaxAccels.Clear();
            NeighborDistances.Clear();
            TimeHorizons.Clear();
            MaxNeighbors.Clear();
            PreferredVelocities.Clear();
            OutputForces.Clear();
            OutputDesiredVelocities.Clear();
        }

        public void Dispose()
        {
            Positions.Dispose();
            Velocities.Dispose();
            Radii.Dispose();
            MaxSpeeds.Dispose();
            MaxAccels.Dispose();
            NeighborDistances.Dispose();
            TimeHorizons.Dispose();
            MaxNeighbors.Dispose();
            PreferredVelocities.Dispose();
            OutputForces.Dispose();
            OutputDesiredVelocities.Dispose();
        }
    }
}
