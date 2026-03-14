using System;
using System.Numerics;
using Arch.System;
using CameraAcceptanceMod.Runtime;
using Ludots.Core.Engine;
using Ludots.Core.Gameplay.Camera;

namespace CameraAcceptanceMod.Systems
{
    internal sealed class CameraAcceptanceHotpathSweepSystem : ISystem<float>
    {
        private readonly GameEngine _engine;
        private bool _suppressedInput;
        private int _sweepTick;

        public CameraAcceptanceHotpathSweepSystem(GameEngine engine)
        {
            _engine = engine;
        }

        public void Initialize() { }
        public void BeforeUpdate(in float dt) { }
        public void AfterUpdate(in float dt) { }
        public void Dispose() { }

        public void Update(in float dt)
        {
            string? mapId = _engine.CurrentMapSession?.MapId.Value;
            bool hotpathMap = string.Equals(mapId, CameraAcceptanceIds.HotpathMapId, StringComparison.OrdinalIgnoreCase);
            if (!hotpathMap)
            {
                if (_suppressedInput)
                {
                    _engine.GameSession.Camera.SetUserInputSuppressed(false);
                    _suppressedInput = false;
                }

                _sweepTick = 0;
                if (_engine.GetService(CameraAcceptanceServiceKeys.DiagnosticsState) is CameraAcceptanceDiagnosticsState diagnostics)
                {
                    diagnostics.PublishHotpathSweep("inactive", 0, "none");
                }

                return;
            }

            if (!_suppressedInput)
            {
                _engine.GameSession.Camera.SetUserInputSuppressed(true);
                _suppressedInput = true;
            }

            ResolveSweepPose(_sweepTick++, out string phase, out int cycle, out Vector2 targetCm);
            _engine.GameSession.Camera.ApplyPose(new CameraPoseRequest
            {
                TargetCm = targetCm
            });

            if (_engine.GetService(CameraAcceptanceServiceKeys.DiagnosticsState) is CameraAcceptanceDiagnosticsState state)
            {
                state.PublishHotpathSweep(phase, cycle, $"{targetCm.X:0},{targetCm.Y:0}");
            }
        }

        private static void ResolveSweepPose(int sweepTick, out string phase, out int cycle, out Vector2 targetCm)
        {
            int travelFrames = CameraAcceptanceIds.HotpathSweepTravelFrames;
            int holdFrames = CameraAcceptanceIds.HotpathSweepHoldFrames;
            int cycleFrames = (travelFrames * 2) + (holdFrames * 2);
            if (cycleFrames <= 0)
            {
                phase = "inactive";
                cycle = 0;
                targetCm = new Vector2(CameraAcceptanceIds.HotpathSweepLeftX, CameraAcceptanceIds.HotpathSweepCenterY);
                return;
            }

            int localTick = Math.Abs(sweepTick) % cycleFrames;
            cycle = Math.Abs(sweepTick) / cycleFrames;
            float leftX = CameraAcceptanceIds.HotpathSweepLeftX;
            float rightX = CameraAcceptanceIds.HotpathSweepRightX;
            float centerY = CameraAcceptanceIds.HotpathSweepCenterY;
            float amplitudeY = CameraAcceptanceIds.HotpathSweepAmplitudeY;

            if (localTick < travelFrames)
            {
                float t = Normalize(localTick, travelFrames);
                phase = "sweep-right";
                targetCm = new Vector2(
                    Lerp(leftX, rightX, t),
                    centerY + MathF.Sin(t * MathF.PI) * amplitudeY);
                return;
            }

            if (localTick < travelFrames + holdFrames)
            {
                phase = "hold-right";
                targetCm = new Vector2(rightX, centerY);
                return;
            }

            localTick -= travelFrames + holdFrames;
            if (localTick < travelFrames)
            {
                float t = Normalize(localTick, travelFrames);
                phase = "sweep-left";
                targetCm = new Vector2(
                    Lerp(rightX, leftX, t),
                    centerY - MathF.Sin(t * MathF.PI) * amplitudeY);
                return;
            }

            phase = "hold-left";
            targetCm = new Vector2(leftX, centerY);
        }

        private static float Normalize(int tick, int span)
        {
            if (span <= 1)
            {
                return 1f;
            }

            return tick / (float)(span - 1);
        }

        private static float Lerp(float from, float to, float t)
        {
            return from + ((to - from) * Math.Clamp(t, 0f, 1f));
        }
    }
}
