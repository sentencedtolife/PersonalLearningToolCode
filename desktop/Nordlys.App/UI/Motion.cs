using System.Windows;

namespace Nordlys.App.UI;

/// <summary>
/// Inherited flag the control templates read: true = hover fades play, false = states change instantly.
/// Set on the main window from the Reduce motion setting and Windows' animation effects.
/// </summary>
public static class Motion
{
    public static readonly DependencyProperty EnabledProperty = DependencyProperty.RegisterAttached(
        "Enabled", typeof(bool), typeof(Motion), new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.Inherits));

    public static bool GetEnabled(DependencyObject d) => (bool)d.GetValue(EnabledProperty);
    public static void SetEnabled(DependencyObject d, bool value) => d.SetValue(EnabledProperty, value);
}
