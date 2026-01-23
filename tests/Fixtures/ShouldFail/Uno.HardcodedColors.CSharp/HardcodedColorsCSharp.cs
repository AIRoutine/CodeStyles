// Minimal stubs to simulate UI color APIs without external dependencies.
namespace Microsoft.UI
{
    public readonly struct Color
    {
        public static Color FromArgb(byte a, byte r, byte g, byte b) => default;
        public static Color FromRgb(byte r, byte g, byte b) => default;
        public static Color Parse(string value) => default;
    }

    public static class Colors
    {
        public static Color Red => default;
        public static Color Blue => default;
    }

    public sealed class SolidColorBrush
    {
        public SolidColorBrush(Color color) { }
    }
}

namespace Uno.HardcodedColors.CSharp
{
    public sealed class HardcodedColorsCSharp
    {
        private readonly Microsoft.UI.Color _red = Microsoft.UI.Colors.Red;
        private readonly Microsoft.UI.Color _fromArgb = Microsoft.UI.Color.FromArgb(255, 255, 0, 0);
        private readonly Microsoft.UI.Color _fromRgb = Microsoft.UI.Color.FromRgb(0, 255, 0);
        private readonly Microsoft.UI.Color _parsed = Microsoft.UI.Color.Parse("#FF00FF");
        private readonly Microsoft.UI.SolidColorBrush _brush = new Microsoft.UI.SolidColorBrush(Microsoft.UI.Colors.Blue);

        public void UseHexString()
        {
            // Should be flagged as a hardcoded color string in a color context.
            var color = Microsoft.UI.Color.Parse("#00FF00");
        }
    }
}
