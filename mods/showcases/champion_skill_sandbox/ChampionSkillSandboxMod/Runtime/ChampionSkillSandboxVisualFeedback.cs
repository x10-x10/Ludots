using System;
using System.Collections.Generic;
using System.Numerics;
using Arch.Core;
using Ludots.Core.Engine;
using Ludots.Core.Gameplay.GAS.Presentation;
using Ludots.Core.Gameplay.GAS.Registry;
using Ludots.Core.Presentation.Commands;
using Ludots.Core.Presentation.Hud;
using Ludots.Core.Presentation.Performers;
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

        private static readonly Vector4 DamageTextColor = new(1.0f, 0.82f, 0.46f, 1.0f);
        private static readonly Vector4 HealTextColor = new(0.62f, 1.0f, 0.72f, 1.0f);

        private readonly CombatTextEntry[] _combatTextEntries = new CombatTextEntry[32];
        private readonly Dictionary<int, int> _castCueByAbility = new();
        private readonly Dictionary<int, int> _hitCueByEffect = new();
        private int _combatTextCount;
        private int _nextCombatTextStableId = 1;
        private int _combatDeltaTokenId;
        private bool _cueIdsInitialized;

        public void Update(GameEngine engine, float dt)
        {
            if (!ChampionSkillSandboxIds.IsSandboxMap(engine.CurrentMapSession?.MapId.Value))
            {
                _combatTextCount = 0;
                return;
            }

            GasPresentationEventBuffer? gasEvents = engine.GetService(CoreServiceKeys.GasPresentationEventBuffer);
            WorldHudBatchBuffer? worldHud = engine.GetService(CoreServiceKeys.PresentationWorldHudBuffer);
            PresentationCommandBuffer? commands = engine.GetService(CoreServiceKeys.PresentationCommandBuffer);
            RenderDebugState? renderDebug = engine.GetService(CoreServiceKeys.RenderDebugState);
            if (ChampionSkillSandboxIds.IsStressMap(engine.CurrentMapSession?.MapId.Value) &&
                renderDebug is { DrawCombatText: false })
            {
                _combatTextCount = 0;
            }

            TickCombatText(dt);
            EmitCombatTextEntries(engine.World, worldHud);

            if (gasEvents == null || gasEvents.Count == 0 || commands == null)
            {
                return;
            }

            EnsureIds(engine);
            ReadOnlySpan<GasPresentationEvent> events = gasEvents.Events;
            for (int i = 0; i < events.Length; i++)
            {
                ref readonly var evt = ref events[i];
                switch (evt.Kind)
                {
                    case GasPresentationEventKind.CastCommitted:
                        TryQueueCue(engine.World, commands, evt.Actor, ResolveCastCue(evt.AbilityId));
                        break;
                    case GasPresentationEventKind.EffectApplied:
                        EmitEffectAppliedFeedback(engine.World, worldHud, commands, renderDebug?.DrawCombatText != false, in evt);
                        break;
                }
            }
        }

        private void EmitEffectAppliedFeedback(
            World world,
            WorldHudBatchBuffer? worldHud,
            PresentationCommandBuffer commands,
            bool allowCombatText,
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
            if (allowCombatText)
            {
                QueueCombatText(worldHud, anchor, evt.Delta, isDamage ? DamageTextColor : HealTextColor);
            }
            TryQueueCue(world, commands, anchor, ResolveHitCue(evt.EffectTemplateId));
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

        private void EnsureIds(GameEngine engine)
        {
            if (_combatDeltaTokenId <= 0 &&
                engine.GetService(CoreServiceKeys.PresentationTextCatalog) is PresentationTextCatalog textCatalog)
            {
                _combatDeltaTokenId = textCatalog.GetTokenId(WellKnownHudTextKeys.CombatDelta);
            }

            if (_cueIdsInitialized)
            {
                return;
            }

            if (engine.GetService(CoreServiceKeys.PerformerDefinitionRegistry) is not PerformerDefinitionRegistry performers)
            {
                return;
            }

            _castCueByAbility.Clear();
            _hitCueByEffect.Clear();

            RegisterAbilityCue(performers, "Ability.Champion.Ezreal.ArcaneShift", "champion_skill_sandbox.cue.ezreal_arcane_shift");
            RegisterAbilityCue(performers, "Ability.Champion.Ezreal.EssenceFlux", "champion_skill_sandbox.cue.ezreal_essence_flux_cast");
            RegisterAbilityCue(performers, "Ability.Champion.Ezreal.MysticShot", "champion_skill_sandbox.cue.ezreal_mystic_shot_cast");
            RegisterAbilityCue(performers, "Ability.Champion.Ezreal.TrueshotBarrage", "champion_skill_sandbox.cue.ezreal_trueshot_barrage_cast");

            RegisterAbilityCue(performers, "Ability.Champion.Garen.DecisiveStrike", "champion_skill_sandbox.cue.garen_decisive_strike");
            RegisterAbilityCue(performers, "Ability.Champion.Garen.Courage", "champion_skill_sandbox.cue.garen_courage");
            RegisterAbilityCue(performers, "Ability.Champion.Garen.Judgment", "champion_skill_sandbox.cue.garen_judgment");
            RegisterAbilityCue(performers, "Ability.Champion.Garen.DemacianJustice", "champion_skill_sandbox.cue.garen_demacian_justice_cast");

            RegisterAbilityCue(performers, "Ability.Champion.Jayce.Cannon.AccelerationGate", "champion_skill_sandbox.cue.jayce_acceleration_gate");
            RegisterAbilityCue(performers, "Ability.Champion.Jayce.Cannon.HyperCharge", "champion_skill_sandbox.cue.jayce_hyper_charge");
            RegisterAbilityCue(performers, "Ability.Champion.Jayce.Cannon.ShockBlast", "champion_skill_sandbox.cue.jayce_shock_blast_cast");
            RegisterAbilityCue(performers, "Ability.Champion.Jayce.Hammer.LightningField", "champion_skill_sandbox.cue.jayce_hammer_lightning_field");
            RegisterAbilityCue(performers, "Ability.Champion.Jayce.Hammer.ThunderingBlow", "champion_skill_sandbox.cue.jayce_hammer_thundering_blow_cast");
            RegisterAbilityCue(performers, "Ability.Champion.Jayce.Hammer.ToTheSkies", "champion_skill_sandbox.cue.jayce_hammer_to_the_skies_cast");
            RegisterAbilityCue(performers, "Ability.Champion.Jayce.Transform.Cannon", "champion_skill_sandbox.cue.jayce_transform_cannon");
            RegisterAbilityCue(performers, "Ability.Champion.Jayce.Transform.Hammer", "champion_skill_sandbox.cue.jayce_transform_hammer");
            RegisterAbilityCue(performers, "Ability.ChampionStress.Warrior.Cleave", "champion_skill_sandbox.cue.stress_warrior_cleave");
            RegisterAbilityCue(performers, "Ability.ChampionStress.FireMage.Fireball", "champion_skill_sandbox.cue.stress_fireball_cast");
            RegisterAbilityCue(performers, "Ability.ChampionStress.LaserMage.Laser", "champion_skill_sandbox.cue.stress_laser_cast");
            RegisterAbilityCue(performers, "Ability.ChampionStress.Priest.Heal", "champion_skill_sandbox.cue.stress_priest_heal_cast");

            RegisterEffectCue(performers, "Effect.Champion.Ezreal.EssenceFluxHit", "champion_skill_sandbox.cue.ezreal_essence_flux_hit");
            RegisterEffectCue(performers, "Effect.Champion.Ezreal.MysticShotHit", "champion_skill_sandbox.cue.ezreal_mystic_shot_hit");
            RegisterEffectCue(performers, "Effect.Champion.Ezreal.TrueshotBarrageHit", "champion_skill_sandbox.cue.ezreal_trueshot_barrage_hit");
            RegisterEffectCue(performers, "Effect.Champion.Garen.JudgmentHit", "champion_skill_sandbox.cue.garen_judgment_hit");
            RegisterEffectCue(performers, "Effect.Champion.Garen.DemacianJusticeHit", "champion_skill_sandbox.cue.garen_demacian_justice_hit");
            RegisterEffectCue(performers, "Effect.Champion.Jayce.Cannon.ShockBlastHit", "champion_skill_sandbox.cue.jayce_shock_blast_hit");
            RegisterEffectCue(performers, "Effect.Champion.Jayce.Hammer.LightningFieldHit", "champion_skill_sandbox.cue.jayce_hammer_lightning_field_hit");
            RegisterEffectCue(performers, "Effect.Champion.Jayce.Hammer.ThunderingBlowHit", "champion_skill_sandbox.cue.jayce_hammer_thundering_blow_hit");
            RegisterEffectCue(performers, "Effect.Champion.Jayce.Hammer.ToTheSkiesHit", "champion_skill_sandbox.cue.jayce_hammer_to_the_skies_hit");
            RegisterEffectCue(performers, "Effect.ChampionStress.Warrior.CleaveHit", "champion_skill_sandbox.cue.stress_warrior_cleave_hit");
            RegisterEffectCue(performers, "Effect.ChampionStress.FireMage.FireballHit", "champion_skill_sandbox.cue.stress_fireball_hit");
            RegisterEffectCue(performers, "Effect.ChampionStress.LaserMage.LaserHit", "champion_skill_sandbox.cue.stress_laser_hit");
            RegisterEffectCue(performers, "Effect.ChampionStress.Priest.Heal", "champion_skill_sandbox.cue.stress_priest_heal_hit");

            _cueIdsInitialized = true;
        }

        private void RegisterAbilityCue(PerformerDefinitionRegistry performers, string abilityKey, string performerKey)
        {
            int abilityId = AbilityIdRegistry.GetId(abilityKey);
            if (abilityId <= 0)
            {
                throw new InvalidOperationException($"ChampionSkillSandboxMod requires ability id '{abilityKey}' to be registered.");
            }

            _castCueByAbility[abilityId] = ResolvePerformerId(performers, performerKey);
        }

        private void RegisterEffectCue(PerformerDefinitionRegistry performers, string effectKey, string performerKey)
        {
            int effectId = EffectTemplateIdRegistry.GetId(effectKey);
            if (effectId <= 0)
            {
                throw new InvalidOperationException($"ChampionSkillSandboxMod requires effect id '{effectKey}' to be registered.");
            }

            _hitCueByEffect[effectId] = ResolvePerformerId(performers, performerKey);
        }

        private static int ResolvePerformerId(PerformerDefinitionRegistry performers, string performerKey)
        {
            int performerId = performers.GetId(performerKey);
            if (performerId <= 0)
            {
                throw new InvalidOperationException($"ChampionSkillSandboxMod requires performer '{performerKey}'.");
            }

            return performerId;
        }

        private int ResolveCastCue(int abilityId)
        {
            return _castCueByAbility.TryGetValue(abilityId, out int performerId) ? performerId : 0;
        }

        private int ResolveHitCue(int effectTemplateId)
        {
            return _hitCueByEffect.TryGetValue(effectTemplateId, out int performerId) ? performerId : 0;
        }

        private static void TryQueueCue(World world, PresentationCommandBuffer commands, Entity anchor, int performerId)
        {
            if (performerId <= 0 || anchor == Entity.Null || !world.IsAlive(anchor))
            {
                return;
            }

            commands.TryAdd(new PresentationCommand
            {
                Kind = PresentationCommandKind.CreatePerformer,
                AnchorKind = PresentationAnchorKind.Entity,
                IdA = performerId,
                IdB = 0,
                Source = anchor,
            });
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
    }
}
