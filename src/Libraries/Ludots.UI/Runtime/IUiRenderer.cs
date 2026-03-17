namespace Ludots.UI.Runtime;

/// <summary>
/// Platform-agnostic UI scene renderer.
/// The concrete implementation (e.g. Skia, GPU, engine-native) lives
/// in a separate adapter assembly — never in Ludots.UI itself.
/// </summary>
public interface IUiRenderer
{
    void Render(UiScene scene, float width, float height);
}
