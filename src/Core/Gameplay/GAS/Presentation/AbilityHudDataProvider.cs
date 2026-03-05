using System;
using Arch.Core;
using Ludots.Core.Gameplay.GAS.Components;

namespace Ludots.Core.Gameplay.GAS.Presentation
{
    /// <summary>
    /// Per-slot HUD data for rendering ability bars.
    /// </summary>
    public struct AbilitySlotHudData
    {
        public int SlotIndex;
        public int AbilityId;
        public bool IsAvailable;
        public bool IsOnCooldown;
        public int CooldownRemainingTicks;
        public int CooldownTotalTicks;

        public float CooldownFraction =>
            CooldownTotalTicks > 0
                ? (float)CooldownRemainingTicks / CooldownTotalTicks
                : 0f;
    }

    /// <summary>
    /// Reads GAS component data from an entity and produces per-slot HUD data.
    /// Stateless utility — used by presentation-layer skill bar systems.
    /// </summary>
    public static class AbilityHudDataProvider
    {
        public static int GetAllSlots(
            World world, Entity entity,
            AbilityDefinitionRegistry? abilityRegistry, int currentTick,
            Span<AbilitySlotHudData> output)
        {
            if (!world.IsAlive(entity)) return 0;
            if (!world.Has<AbilityStateBuffer>(entity)) return 0;

            ref var abilities = ref world.Get<AbilityStateBuffer>(entity);
            int count = Math.Min(abilities.Count, output.Length);

            for (int i = 0; i < count; i++)
            {
                output[i] = GetSlot(world, entity, i, abilityRegistry, currentTick);
            }
            return count;
        }

        public static AbilitySlotHudData GetSlot(
            World world, Entity entity, int slotIndex,
            AbilityDefinitionRegistry? abilityRegistry, int currentTick)
        {
            var result = new AbilitySlotHudData { SlotIndex = slotIndex };
            if (!world.IsAlive(entity) || !world.Has<AbilityStateBuffer>(entity)) return result;

            ref var abilities = ref world.Get<AbilityStateBuffer>(entity);
            if (slotIndex < 0 || slotIndex >= abilities.Count) return result;

            bool hasGranted = world.Has<GrantedSlotBuffer>(entity);
            GrantedSlotBuffer granted = hasGranted ? world.Get<GrantedSlotBuffer>(entity) : default;
            var slot = AbilitySlotResolver.Resolve(in abilities, in granted, hasGranted, slotIndex);
            result.AbilityId = slot.AbilityId;
            if (slot.AbilityId <= 0) return result;

            bool hasTimedTags = world.Has<TimedTagBuffer>(entity);
            bool hasBlockTags = abilityRegistry != null &&
                                abilityRegistry.TryGet(slot.AbilityId, out var def) &&
                                def.HasActivationBlockTags;

            if (hasTimedTags && hasBlockTags)
            {
                ref var timedTags = ref world.Get<TimedTagBuffer>(entity);
                ref var blockTags = ref def.ActivationBlockTags;
                FindLongestCooldown(ref timedTags, ref blockTags.BlockedAny, currentTick,
                    out result.CooldownRemainingTicks, out result.CooldownTotalTicks);
                result.IsOnCooldown = result.CooldownRemainingTicks > 0;
            }

            result.IsAvailable = !result.IsOnCooldown;
            return result;
        }

        private static unsafe void FindLongestCooldown(
            ref TimedTagBuffer timedTags, ref GameplayTagContainer blockedAny,
            int currentTick, out int remainingTicks, out int totalTicks)
        {
            remainingTicks = 0;
            totalTicks = 0;
            for (int i = 0; i < timedTags.Count; i++)
            {
                int tagId = timedTags.TagIds[i];
                if (!blockedAny.HasTag(tagId)) continue;
                int expireAt = timedTags.ExpireAt[i];
                int remaining = expireAt - currentTick;
                if (remaining <= 0) continue;
                if (remaining > remainingTicks)
                {
                    remainingTicks = remaining;
                    totalTicks = expireAt;
                }
            }
        }
    }
}
