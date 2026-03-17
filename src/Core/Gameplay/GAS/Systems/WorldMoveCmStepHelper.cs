using System;
using Ludots.Core.Mathematics.FixedPoint;

namespace Ludots.Core.Gameplay.GAS.Systems
{
    internal static class WorldMoveCmStepHelper
    {
        public static bool StepTowards(ref Fix64Vec2 current, in Fix64Vec2 target, float stepCm, float stopRadiusCm)
        {
            if (stepCm <= 0f)
            {
                return false;
            }

            var delta = target - current;
            float dx = delta.X.ToFloat();
            float dy = delta.Y.ToFloat();
            float dist = MathF.Sqrt(dx * dx + dy * dy);

            if (dist <= stopRadiusCm || dist <= stepCm || dist <= 1f)
            {
                current = target;
                return true;
            }

            float inv = 1f / dist;
            current += Fix64Vec2.FromFloat(dx * inv * stepCm, dy * inv * stepCm);
            return false;
        }
    }
}
