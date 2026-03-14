namespace Ludots.UI.Runtime;

/// <summary>
/// Platform-agnostic text measurement strategy.
/// Injected into <see cref="UiLayoutEngine"/> so the layout layer
/// never touches a concrete rendering backend.
/// </summary>
public interface IUiTextMeasurer
{
    UiTextLayoutResult Measure(string? text, UiStyle style, float availableWidth, bool constrainWidth);

    float MeasureWidth(string? text, UiStyle style);
}
