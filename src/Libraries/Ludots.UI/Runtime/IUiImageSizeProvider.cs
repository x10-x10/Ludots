namespace Ludots.UI.Runtime;

/// <summary>
/// Platform-agnostic image size provider for layout.
/// Injected into <see cref="UiLayoutEngine"/> so the layout layer
/// never touches a concrete image loading backend.
/// </summary>
public interface IUiImageSizeProvider
{
    bool TryGetSize(string? source, out float width, out float height);
}
