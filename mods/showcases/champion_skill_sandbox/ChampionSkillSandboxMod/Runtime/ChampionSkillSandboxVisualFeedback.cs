using System;
using System.Numerics;
using Arch.Core;
using Ludots.Core.Engine;
using Ludots.Core.Gameplay.GAS.Presentation;
using Ludots.Core.Presentation.Assets;
using Ludots.Core.Presentation.Hud;
using Ludots.Core.Presentation.Rendering;
using Ludots.Core.Scripting;

namespace ChampionSkillSandboxMod.Runtime
{
    internal sealed class ChampionSkillSandboxVisualFeedback
    {
        private struct CombatTextEntry
        {
            public int StableId;
            public Entity Anchor;
            public int RoundedDelta;
            public float Lifetime;
            public float TimeLeft;
            public Vector4 Color;
        }

        private static readonly Vector3 CastPulseScale = new(0.22f, 0.22f, 0.22f);
        private static readonly Vector4 CastPulseColor = new(0.34f, 0.89f, 1.0f, 0.92f);
        private static readonly Vector3 DamagePulseScale = new(0.34f, 0.16f, 0.34f);
        private static readonly Vector4 DamagePulseColor = new(1.0f, 0.54f, 0.31f, 0.96f);
        private static readonly Vector3 HealPulseScale = new(0.28f, 0.28f, 0.28f);
        private static readonly Vector4 HealPulseColor = new(0.42f, 1.0f, 0.58f, 0.96f);
        private static readonly Vector4 DamageTextColor = new(1.0f, 0.82f, 0.46f, 1.0f);
        private static readonly Vector4 HealTextColor = new(0.62f, 1.0f, 0.72f, 1.0f);

        private readonly CombatTextEntry[] _combatTextEntries = new CombatTextEntry[32];
        private int _combatTextCount;
        private int _nextCombatTextStableId = 1;
        private int _sphereMeshId;
        private int _combatDeltaTokenId;

        public void Update(GameEngine engine, float dt)
        {
            if (!ChampionSkillSandboxIds.IsSandboxMap(engine.CurrentMapSession?.MapId.Value))
            {
                _combatTextCount = 0;
                return;
            }

            GasPresentationEventBuffer? gasEvents = engine.GetService(CoreServiceKeys.GasPresentationEventBuffer);
            TransientMarkerBuffer? markers = engine.GetService(CoreServiceKeys.TransientMarkerBuffer);
            WorldHudBatchBuffer? worldHud = engine.GetService(CoreServiceKeys.PresentationWorldHudBuffer);
            TickCombatText(dt);
            EmitCombatTextEntries(engine.World, worldHud);

            if (gasEvents == null || gasEvents.Count == 0 || markers == null)
            {
                return;
            }

            EnsureIds(engine);
            ReadOnlySpan<GasPresentationEvent> events = gasEvents.Events;
            for (int i = 0; i < events.Length; i++)
            {
                ref readonly GasPresentationEvent evt = ref events[i];
                switch (evt.Kind)
                {
                    case GasPresentationEventKind.CastCommitted:
                        EmitAnchoredPulse(engine.World, markers, evt.Actor, CastPulseScale, CastPulseColor, 0.24f, 0.95f);
                        break;
                    case GasPresentationEventKind.EffectApplied:
                        EmitEffectAppliedFeedback(engine.World, markers, worldHud, in evt);
                        break;
                }
            }
        }

        private void EmitEffectAppliedFeedback(
            World world,
            TransientMarkerBuffer markers,
            WorldHudBatchBuffer? worldHud,
            in GasPresentationEvent evt)
        {
            if (evt.Delta == 0f)
            {
                return;
            }

            Entity anchor = ResolveFeedbackAnchor(world, evt);
            if (anchor == Entity.Null)
            {
                return;
            }

            bool isDamage = evt.Delta < 0f;
            Vector3 pulseScale = isDamage ? DamagePulseScale : HealPulseScale;
            Vector4 pulseColor = isDamage ? DamagePulseColor : HealPulseColor;
            Vector4 textColor = isDamage ? DamageTextColor : HealTextColor;
            EmitAnchoredPulse(world, markers, anchor, pulseScale, pulseColor, 0.32f, 0.86f);
            QueueCombatText(worldHud, anchor, evt.Delta, textColor);
        }

        private void EmitAnchoredPulse(
            World world,
            TransientMarkerBuffer markers,
            Entity anchor,
            Vector3 scale,
            Vector4 color,
            float lifetimeSeconds,
            float yOffsetMeters)
        {
            if (_sphereMeshId <= 0 || !world.IsAlive(anchor) || !world.Has<Ludots.Core.Presentation.Components.VisualTransform>(anchor))
            {
                return;
            }

            markers.TryAddAnchored(
                _sphereMeshId,
                scale,
                color,
                lifetimeSeconds,
                anchor,
                new Vector3(0f, yOffsetMeters, 0f));
        }

        private void QueueCombatText(
            WorldHudBatchBuffer? worldHud,
            Entity anchor,
            float delta,
            in Vector4 color)
        {
            if (worldHud == null || _combatDeltaTokenId <= 0)
            {
                return;
            }

            int roundedDelta = (int)MathF.Round(delta);
            if (roundedDelta == 0 || _combatTextCount >= _combatTextEntries.Length)
            {
                return;
            }

            _combatTextEntries[_combatTextCount++] = new CombatTextEntry
            {
                StableId = _nextCombatTextStableId++,
                Anchor = anchor,
                RoundedDelta = roundedDelta,
                Lifetime = 0.72f,
                TimeLeft = 0.72f,
                Color = color,
            };
        }

        private void TickCombatText(float dt)
        {
            float delta = dt <= 0f ? (1f / 60f) : dt;
            for (int i = 0; i < _combatTextCount;)
            {
                _combatTextEntries[i].TimeLeft -= delta;
                if (_combatTextEntries[i].TimeLeft <= 0f)
                {
                    _combatTextCount--;
                    if (i < _combatTextCount)
                    {
                        _combatTextEntries[i] = _combatTextEntries[_combatTextCount];
                    }

                    continue;
                }

                i++;
            }
        }

        private void EmitCombatTextEntries(World world, WorldHudBatchBuffer? worldHud)
        {
            if (worldHud == null || _combatDeltaTokenId <= 0)
            {
                return;
            }

            for (int i = 0; i < _combatTextCount; i++)
            {
                ref CombatTextEntry entry = ref _combatTextEntries[i];
                if (!world.IsAlive(entry.Anchor) || !world.Has<Ludots.Core.Presentation.Components.VisualTransform>(entry.Anchor))
                {
                    continue;
                }

                float progress = 1f - (entry.TimeLeft / entry.Lifetime);
                Vector4 color = entry.Color;
                color.W *= 1f - progress;

                Vector3 worldPosition = world.Get<Ludots.Core.Presentation.Components.VisualTransform>(entry.Anchor).Position
                    + new Vector3(0f, 1.42f + progress * 0.42f, 0f);
                var packet = PresentationTextPacket.FromToken(_combatDeltaTokenId);
                packet.SetArg(0, PresentationTextArg.FromInt32(entry.RoundedDelta));

                worldHud.TryAdd(new WorldHudItem
                {
                    StableId = entry.StableId,
                    DirtySerial = HudItemIdentity.ComposeTextDirtySerial(
                        fontSize: 22,
                        legacyStringId: 0,
                        legacyModeId: 0,
                        value0: 0f,
                        value1: 0f,
                        color,
                        packet),
                    Kind = WorldHudItemKind.Text,
                    WorldPosition = worldPosition,
                    Width = 72f,
                    FontSize = 22,
                    Color0 = color,
                    Text = packet,
                });
            }
        }

        private static Entity ResolveFeedbackAnchor(World world, in GasPresentationEvent evt)
        {
            if (world.IsAlive(evt.Target))
            {
                return evt.Target;
            }

            if (world.IsAlive(evt.Actor))
            {
                return evt.Actor;
            }

            return Entity.Null;
        }

        private void EnsureIds(GameEngine engine)
        {
            if (_sphereMeshId <= 0 &&
                engine.GetService(CoreServiceKeys.PresentationMeshAssetRegistry) is MeshAssetRegistry meshes)
            {
                _sphereMeshId = meshes.GetId(WellKnownMeshKeys.Sphere);
            }

            if (_combatDeltaTokenId <= 0 &&
                engine.GetService(CoreServiceKeys.PresentationTextCatalog) is PresentationTextCatalog textCatalog)
            {
                _combatDeltaTokenId = textCatalog.GetTokenId(WellKnownHudTextKeys.CombatDelta);
            }
        }
    }
}
