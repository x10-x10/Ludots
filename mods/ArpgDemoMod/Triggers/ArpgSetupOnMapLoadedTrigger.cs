using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Arch.Core;
using Ludots.Core.Components;
using Ludots.Core.Engine;
using Ludots.Core.Gameplay;
using Ludots.Core.Gameplay.Camera;
using Ludots.Core.Gameplay.Camera.FollowTargets;
using Ludots.Core.Gameplay.GAS.Components;
using Ludots.Core.Scripting;
using Ludots.Core.Modding;

namespace ArpgDemoMod.Triggers
{
    public sealed class ArpgSetupOnMapLoadedTrigger : Trigger
    {
        private readonly IModContext _ctx;

        public ArpgSetupOnMapLoadedTrigger(IModContext ctx)
        {
            _ctx = ctx;
            EventKey = GameEvents.MapLoaded;
        }

        public override Task ExecuteAsync(ScriptContext context)
        {
            var engine = context.GetEngine();
            if (engine == null) return Task.CompletedTask;

            var mapTags = context.Get(CoreServiceKeys.MapTags) ?? new List<string>();
            if (!HasTag(mapTags, "arpg")) return Task.CompletedTask;

            var world = engine.World;
            var q = new QueryDescription().WithAll<Name>();
            Entity heroEntity = default;
            world.Query(in q, (Entity e, ref Name name) =>
            {
                if (!string.Equals(name.Value, "ArpgHero", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(name.Value, "ArpgEnemy", StringComparison.OrdinalIgnoreCase))
                    return;

                if (!world.Has<GameplayTagContainer>(e)) world.Add(e, new GameplayTagContainer());
                if (!world.Has<TagCountContainer>(e)) world.Add(e, new TagCountContainer());
                if (!world.Has<TimedTagBuffer>(e)) world.Add(e, new TimedTagBuffer());

                if (string.Equals(name.Value, "ArpgHero", StringComparison.OrdinalIgnoreCase))
                    heroEntity = e;
            });

            if (heroEntity != default)
            {
                engine.GlobalContext[CoreServiceKeys.LocalPlayerEntity.Name] = heroEntity;

                var session = context.Get(CoreServiceKeys.GameSession);
                if (session != null)
                {
                    session.Camera.FollowTarget = new EntityFollowTarget(world, heroEntity);
                    _ctx.Log("[ArpgDemoMod] Camera follow target set to hero entity.");
                }
            }

            return Task.CompletedTask;
        }

        private static bool HasTag(List<string> tags, string t)
        {
            for (int i = 0; i < tags.Count; i++)
            {
                if (string.Equals(tags[i], t, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }
    }
}

