using Ludots.UI.Runtime;

namespace Ludots.UI.Skia;

public sealed class SkiaImageSizeProvider : IUiImageSizeProvider
{
    public bool TryGetSize(string? source, out float width, out float height)
        => UiImageSourceCache.TryGetSize(source, out width, out height);
}
