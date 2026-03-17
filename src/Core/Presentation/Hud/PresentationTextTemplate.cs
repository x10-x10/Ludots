using System;

namespace Ludots.Core.Presentation.Hud
{
    public enum PresentationTextTemplatePartKind : byte
    {
        Literal = 0,
        Argument = 1,
    }

    public readonly struct PresentationTextTemplatePart
    {
        public PresentationTextTemplatePart(PresentationTextTemplatePartKind kind, string literal, int argIndex)
        {
            Kind = kind;
            Literal = literal ?? string.Empty;
            ArgIndex = argIndex;
        }

        public PresentationTextTemplatePartKind Kind { get; }

        public string Literal { get; }

        public int ArgIndex { get; }
    }

    public sealed class PresentationTextTemplate
    {
        private readonly PresentationTextTemplatePart[] _parts;

        public PresentationTextTemplate(string source, PresentationTextTemplatePart[] parts)
        {
            Source = source ?? string.Empty;
            _parts = parts ?? Array.Empty<PresentationTextTemplatePart>();
        }

        public string Source { get; }

        public ReadOnlySpan<PresentationTextTemplatePart> GetParts() => _parts;
    }
}
