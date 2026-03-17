using System.Collections.Generic;
using Arch.Core;
using Arch.System;
using Ludots.Core.Components;
using Ludots.Core.Engine;
using Ludots.Core.Gameplay.GAS.Components;
using Ludots.Core.Mathematics.FixedPoint;

namespace Ludots.Core.Gameplay.GAS.Systems
{
    public sealed class ProjectileRuntimeSystem : BaseSystem<World, float>
    {
        private static readonly QueryDescription _query = new QueryDescription().WithAll<ProjectileState, WorldPositionCm>();
        private readonly EffectRequestQueue _effectRequests;
        private readonly List<Entity> _toDestroy = new();

        public ProjectileRuntimeSystem(World world, IClock clock, EffectRequestQueue effectRequests) : base(world)
        {
            _effectRequests = effectRequests;
        }

        public override void Update(in float dt)
        {
            if (_effectRequests == null) return;

            _toDestroy.Clear();
            Fix64 deltaTime = Fix64.FromFloat(dt);

            World.Query(in _query, (Entity e, ref ProjectileState ps, ref WorldPositionCm pos) =>
            {
                if (!World.IsAlive(ps.Source))
                {
                    _toDestroy.Add(e);
                    return;
                }

                if (ps.Speed <= Fix64.Zero || ps.Range <= 0)

                {
                    _toDestroy.Add(e);
                    return;
                }

                Fix64 stepCm = ps.Speed * deltaTime;
                if (stepCm <= Fix64.Zero) return;

                Fix64Vec2 current = pos.Value;
                Fix64Vec2 next;
                bool reachedImpact = false;

                if (World.IsAlive(ps.Target) && World.Has<WorldPositionCm>(ps.Target))
                {
                    var targetPos = World.Get<WorldPositionCm>(ps.Target).Value;
                    var delta = targetPos - current;
                    Fix64 dist = delta.Length();

                    if (dist <= stepCm || dist <= Fix64.OneValue)
                    {
                        pos.Value = targetPos;
                        reachedImpact = true;
                        next = targetPos;
                    }
                    else
                    {
                        next = current + delta.Normalized() * stepCm;
                    }
                }
                else if (ps.HasTargetPoint != 0)
                {
                    var delta = ps.TargetPointCm - current;
                    Fix64 dist = delta.Length();
                    if (dist <= stepCm || dist <= Fix64.OneValue)
                    {
                        next = ps.TargetPointCm;
                        reachedImpact = true;
                    }
                    else
                    {
                        next = current + delta.Normalized() * stepCm;
                    }
                }
                else
                {
                    next = current + new Fix64Vec2(stepCm, Fix64.Zero);
                }

                ps.TraveledCm += stepCm;
                pos.Value = next;

                if (reachedImpact || ps.TraveledCm >= Fix64.FromInt(ps.Range))
                {
                    PublishImpact(ref ps);
                    _toDestroy.Add(e);
                }
            });

            for (int i = 0; i < _toDestroy.Count; i++)
            {
                if (World.IsAlive(_toDestroy[i]))
                    World.Destroy(_toDestroy[i]);
            }
        }

        private void PublishImpact(ref ProjectileState ps)
        {
            if (ps.ImpactEffectTemplateId <= 0)
            {
                return;
            }

            var request = new EffectRequest
            {
                RootId = 0,
                Source = ps.Source,
                Target = World.IsAlive(ps.Target) ? ps.Target : Entity.Null,
                TargetContext = default,
                TemplateId = ps.ImpactEffectTemplateId,
            };

            var callerParams = new EffectConfigParams();
            bool hasCallerParams = false;

            if (ps.HasLaunchOrigin != 0)
            {
                hasCallerParams |= callerParams.TryAddFloat(EffectParamKeys.TargetOriginX, ps.LaunchOriginCm.X.ToFloat());
                hasCallerParams |= callerParams.TryAddFloat(EffectParamKeys.TargetOriginY, ps.LaunchOriginCm.Y.ToFloat());
            }

            if (ps.HasTargetPoint != 0)
            {
                hasCallerParams |= callerParams.TryAddFloat(EffectParamKeys.TargetPosX, ps.TargetPointCm.X.ToFloat());
                hasCallerParams |= callerParams.TryAddFloat(EffectParamKeys.TargetPosY, ps.TargetPointCm.Y.ToFloat());
            }
            else if (World.IsAlive(ps.Target) && World.Has<WorldPositionCm>(ps.Target))
            {
                var targetPos = World.Get<WorldPositionCm>(ps.Target).Value;
                hasCallerParams |= callerParams.TryAddFloat(EffectParamKeys.TargetPosX, targetPos.X.ToFloat());
                hasCallerParams |= callerParams.TryAddFloat(EffectParamKeys.TargetPosY, targetPos.Y.ToFloat());
            }

            request.CallerParams = callerParams;
            request.HasCallerParams = hasCallerParams;
            _effectRequests.Publish(request);
        }
    }
}
