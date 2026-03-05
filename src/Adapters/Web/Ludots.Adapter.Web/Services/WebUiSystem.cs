using System.Threading;
using Ludots.Core.UI;

namespace Ludots.Adapter.Web.Services
{
    public sealed class WebUiSystem : IUiSystem
    {
        private readonly object _lock = new();
        private string? _html;
        private string? _css;
        private bool _dirty;

        public void SetHtml(string html, string css)
        {
            lock (_lock)
            {
                _html = html;
                _css = css;
                _dirty = true;
            }
        }

        /// <summary>
        /// Consumes pending UI HTML/CSS if dirty. Returns false if nothing changed.
        /// Called from the frame encoding path each tick.
        /// </summary>
        public bool TryConsume(out string? html, out string? css)
        {
            lock (_lock)
            {
                if (!_dirty)
                {
                    html = null;
                    css = null;
                    return false;
                }
                html = _html;
                css = _css;
                _dirty = false;
                return true;
            }
        }
    }
}
