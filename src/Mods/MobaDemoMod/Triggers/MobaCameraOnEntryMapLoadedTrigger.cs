using System;
using System.Numerics;
using System.Threading.Tasks;
using Arch.Core;
using Ludots.Core.Components;
using Ludots.Core.Config;
using Ludots.Core.Engine;
using Ludots.Core.Gameplay;
using Ludots.Core.Gameplay.Camera;
using Ludots.Core.Gameplay.Components;
using Ludots.Core.Map;
using Ludots.Core.Modding;
using Ludots.Core.Scripting;

namespace MobaDemoMod.Triggers
{
    public sealed class MobaCameraOnEntryMapLoadedTrigger : Trigger
    {
        private readonly IModContext _ctx;

        public MobaCameraOnEntryMapLoadedTrigger(IModContext ctx)
        {
            _ctx = ctx;
            EventKey = GameEvents.MapLoaded;
        }

        public override Task ExecuteAsync(ScriptContext context)
        {
            var mapId = context.Get<MapId>(ContextKeys.MapId);
            
            var engine = context.GetEngine();
            if (engine == null) return Task.CompletedTask;

            var config = (GameConfig)engine.GlobalContext[ContextKeys.GameConfig];
            string startupMapId = config.StartupMapId;
            
            bool isEntry = mapId.Value == startupMapId;
            if (!isEntry) return Task.CompletedTask;

            var session = context.Get<GameSession>(ContextKeys.GameSession);
            if (session == null) return Task.CompletedTask;

            // MobaConfig is guaranteed to be in GlobalContext by InstallMobaDemoOnGameStartTrigger
            var mobaConfig = (MobaConfig)engine.GlobalContext[InstallMobaDemoOnGameStartTrigger.MobaConfigKey];

            session.Camera.State.Yaw = mobaConfig.Camera.InitialYawDegrees;
            session.Camera.State.Pitch = mobaConfig.Camera.InitialPitchDegrees;
            session.Camera.State.DistanceCm = ClampDistance(mobaConfig.Camera.InitialDistanceCm, mobaConfig.Camera.MinDistanceCm, mobaConfig.Camera.MaxDistanceCm);

            if (TryCenterOnLocalPlayerEntity(engine, session))
            {
                _ctx.Log("[MobaDemoMod] Camera centered on local player entity.");
                return Task.CompletedTask;
            }
            if (TryCenterOnOwnerPlayerOne(engine, session))
            {
                _ctx.Log("[MobaDemoMod] Camera centered on fallback owner player=1.");
                return Task.CompletedTask;
            }

            session.Camera.State.TargetCm = Vector2.Zero;
            _ctx.Log("[MobaDemoMod] Camera centered on origin.");
            return Task.CompletedTask;
        }

        private static float ClampDistance(float value, float minDistanceCm, float maxDistanceCm)
        {
            float min = Math.Min(minDistanceCm, maxDistanceCm);
            float max = Math.Max(minDistanceCm, maxDistanceCm);
            return Math.Clamp(value, min, max);
        }

        private static bool TryCenterOnLocalPlayerEntity(GameEngine engine, GameSession session)
        {
            if (!engine.GlobalContext.TryGetValue(ContextKeys.LocalPlayerEntity, out var localObj) || localObj is not Entity localPlayer)
                return false;
            if (!engine.World.IsAlive(localPlayer) || !engine.World.Has<WorldPositionCm>(localPlayer))
                return false;

            session.Camera.State.TargetCm = engine.World.Get<WorldPositionCm>(localPlayer).Value.ToVector2();
            return true;
        }

        private static bool TryCenterOnOwnerPlayerOne(GameEngine engine, GameSession session)
        {
            var world = engine.World;
            var q = new QueryDescription().WithAll<PlayerOwner, WorldPositionCm>();
            var chunks = world.Query(in q);
            foreach (var chunk in chunks)
            {
                var owners = chunk.GetArray<PlayerOwner>();
                var positions = chunk.GetArray<WorldPositionCm>();
                for (int i = 0; i < chunk.Count; i++)
                {
                    if (owners[i].PlayerId != 1) continue;
                    session.Camera.State.TargetCm = positions[i].Value.ToVector2();
                    return true;
                }
            }

            return false;
        }
    }
}

