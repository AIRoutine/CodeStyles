namespace Uno.ValidColors;

public sealed class ValidColorService
{
    // GOOD: Getting color from resources (conceptual - actual implementation depends on app context)
    public object? GetPrimaryBrush()
    {
        // In real Uno app: Application.Current.Resources["PrimaryBrush"]
        return null;
    }

    // GOOD: No hardcoded colors
    public string GetColorResourceKey() => "PrimaryColor";

    // GOOD: Working with resource keys instead of direct colors
    public void ApplyTheme(string themeKey)
    {
        var resourceKey = themeKey switch
        {
            "dark" => "DarkThemeBrush",
            "light" => "LightThemeBrush",
            _ => "DefaultBrush"
        };

        // Use resourceKey to lookup actual color from resources
    }
}
