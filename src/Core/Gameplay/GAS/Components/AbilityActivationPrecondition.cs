namespace Ludots.Core.Gameplay.GAS.Components
{
    /// <summary>
    /// Optional ability-level activation validation hook.
    /// Uses an existing GAS graph program instead of introducing a parallel condition system.
    /// </summary>
    public struct AbilityActivationPrecondition
    {
        public int ValidationGraphId;
    }
}
