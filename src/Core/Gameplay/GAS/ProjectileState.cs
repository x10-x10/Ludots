using Arch.Core;
using Ludots.Core.Mathematics.FixedPoint;

namespace Ludots.Core.Gameplay.GAS
{
    public struct ProjectileState
    {
        public Fix64 Speed;
        public int Range;
        public int ArcHeight;
        public int ImpactEffectTemplateId;
        public Entity Source;
        public Entity Target;
        public Fix64Vec2 LaunchOriginCm;
        public byte HasLaunchOrigin;
        public Fix64Vec2 TargetPointCm;
        public byte HasTargetPoint;
        public Fix64 TraveledCm;
    }
}
