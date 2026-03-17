using System;

namespace Ludots.Core.Presentation.Hud
{
    public sealed class PresentationTextLocaleSelection
    {
        private readonly PresentationTextCatalog _catalog;

        public PresentationTextLocaleSelection(PresentationTextCatalog catalog, int initialLocaleId = 0)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            ActiveLocaleId = initialLocaleId > 0 ? initialLocaleId : catalog.DefaultLocaleId;
        }

        public int ActiveLocaleId { get; private set; }

        public string ActiveLocaleKey => _catalog.GetLocaleKey(ActiveLocaleId);

        public bool TrySetActiveLocale(string localeKey)
        {
            int localeId = _catalog.GetLocaleId(localeKey);
            if (localeId <= 0)
            {
                return false;
            }

            ActiveLocaleId = localeId;
            return true;
        }

        public void SetActiveLocale(string localeKey)
        {
            if (!TrySetActiveLocale(localeKey))
            {
                throw new InvalidOperationException($"Presentation locale '{localeKey}' is not registered.");
            }
        }
    }
}
