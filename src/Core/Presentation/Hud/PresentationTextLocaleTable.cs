using System;

namespace Ludots.Core.Presentation.Hud
{
    public sealed class PresentationTextLocaleTable
    {
        private readonly PresentationTextTemplate[] _templates;

        public PresentationTextLocaleTable(int localeId, string localeKey, PresentationTextTemplate[] templates)
        {
            LocaleId = localeId;
            LocaleKey = localeKey ?? string.Empty;
            _templates = templates ?? Array.Empty<PresentationTextTemplate>();
        }

        public int LocaleId { get; }

        public string LocaleKey { get; }

        public bool TryGetTemplate(int tokenId, out PresentationTextTemplate template)
        {
            if ((uint)tokenId < (uint)_templates.Length && _templates[tokenId] != null)
            {
                template = _templates[tokenId];
                return true;
            }

            template = null!;
            return false;
        }
    }
}
