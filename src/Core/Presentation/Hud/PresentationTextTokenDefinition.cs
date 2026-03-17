namespace Ludots.Core.Presentation.Hud
{
    public sealed class PresentationTextTokenDefinition
    {
        public int TokenId { get; init; }

        public string Key { get; init; } = string.Empty;

        public byte ArgCount { get; init; }
    }
}
