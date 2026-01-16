#if WINDOWS || HAS_UNO
using Windows.UI;

namespace Analyzers.HardcodedStrings;

/// <summary>
/// This file contains hardcoded colors that should trigger ACS0003.
/// Note: Only compiles on platforms with Windows.UI.
/// </summary>
public class ColorTestService
{
    // ACS0003: Direct Colors.* access
    public Color GetPrimaryColor()
    {
        return Colors.Red;
    }

    // ACS0003: Colors.* in variable
    public void SetBackground()
    {
        var color = Colors.Blue;
        _ = color;
    }

    // ACS0003: Color.FromArgb call
    public Color CreateCustomColor()
    {
        return Color.FromArgb(255, 100, 150, 200);
    }
}
#endif
