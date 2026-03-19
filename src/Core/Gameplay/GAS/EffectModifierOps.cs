using System.Runtime.CompilerServices;
using Ludots.Core.Gameplay.GAS.Components;
using Ludots.Core.Gameplay.Components;

namespace Ludots.Core.Gameplay.GAS
{
    /// <summary>
    /// Static utility for applying effect modifiers to attribute buffers.
    /// Extracted from <see cref="EffectModifiers.ApplyTo"/> to follow ECS best practice:
    /// components are data containers, systems/utilities own all behavior.
    /// </summary>
    public static class EffectModifierOps
    {
        /// <summary>
        /// Apply all modifiers in the set to the target AttributeBuffer.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Apply(in EffectModifiers modifiers, ref AttributeBuffer buffer)
        {
            ApplyInternal(in modifiers, ref buffer, clampToCapacity: true);
        }

        /// <summary>
        /// Apply aggregated modifiers while bypassing ClampCurrentToBase.
        /// Used by attribute recomputation to rebuild dynamic caps before persistent current values are restored.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ApplyAggregated(in EffectModifiers modifiers, ref AttributeBuffer buffer)
        {
            ApplyInternal(in modifiers, ref buffer, clampToCapacity: false);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ApplyInternal(in EffectModifiers modifiers, ref AttributeBuffer buffer, bool clampToCapacity)
        {
            for (int i = 0; i < modifiers.Count; i++)
            {
                var mod = modifiers.Get(i);
                float current = buffer.GetCurrent(mod.AttributeId);

                switch (mod.Operation)
                {
                    case ModifierOp.Add:
                        if (clampToCapacity)
                        {
                            buffer.SetCurrent(mod.AttributeId, current + mod.Value);
                        }
                        else
                        {
                            buffer.SetAggregatedCurrent(mod.AttributeId, current + mod.Value);
                        }
                        break;
                    case ModifierOp.Multiply:
                        if (clampToCapacity)
                        {
                            buffer.SetCurrent(mod.AttributeId, current * mod.Value);
                        }
                        else
                        {
                            buffer.SetAggregatedCurrent(mod.AttributeId, current * mod.Value);
                        }
                        break;
                    case ModifierOp.Override:
                        if (clampToCapacity)
                        {
                            buffer.SetCurrent(mod.AttributeId, mod.Value);
                        }
                        else
                        {
                            buffer.SetAggregatedCurrent(mod.AttributeId, mod.Value);
                        }
                        break;
                }
            }
        }
    }
}
