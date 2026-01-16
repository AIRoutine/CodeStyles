using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace Uno.HardcodedColors.CSharp;

public sealed class HardcodedColorService
{
    // BAD: Using Colors.* static properties
    public Color GetPrimaryColor() => Colors.Red;

    // BAD: Using Colors.* for brush
    public SolidColorBrush GetAccentBrush() => new SolidColorBrush(Colors.Blue);

    // BAD: Using Color.FromArgb
    public Color GetCustomColor() => Color.FromArgb(255, 128, 64, 32);

    // BAD: Using hex string
    public string GetColorHex() => "#FF5733";

    public void ApplyColors()
    {
        // BAD: Multiple violations in method
        var red = Colors.Red;
        var green = Colors.Green;
        var custom = Color.FromArgb(255, 0, 128, 255);

        // BAD: Creating brush with hardcoded color
        var brush = new SolidColorBrush(Colors.Orange);
    }
}
