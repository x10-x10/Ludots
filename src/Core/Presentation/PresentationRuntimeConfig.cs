namespace Ludots.Core.Presentation
{
    /// <summary>
    /// Engine-level presentation runtime capacity knobs merged from game.json.
    /// Defaults are sized for playable showcase scenes rather than tiny unit-test maps.
    /// </summary>
    public sealed class PresentationRuntimeConfig
    {
        public int PerformerInstanceCapacity { get; set; } = 2048;

        public int GetEffectivePerformerInstanceCapacity()
        {
            return PerformerInstanceCapacity > 0 ? PerformerInstanceCapacity : 2048;
        }
    }
}
