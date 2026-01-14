# AIRoutine.CodeStyle.Uno

Strict C# code style rules for Uno Platform projects.

## Installation

```xml
<PackageReference Include="AIRoutine.CodeStyle.Uno" Version="1.0.0" PrivateAssets="all" />
```

## Features

Includes all rules from `AIRoutine.CodeStyle.Common` plus:

- **XAML code-behind relaxations**: Partial classes and event handlers
- **IDE0051 relaxed**: Unused private members common in XAML bindings
- **Primary constructors optional**: Traditional constructors preferred for ViewModels
- **Top-level statements disabled**: App.xaml.cs requires Program class structure
- **No hardcoded colors**: Validates that all colors are defined in ResourceDictionary

## No Hardcoded Colors Rule

Following [Uno Platform best practices](https://platform.uno/docs/articles/external/uno.themes/doc/lightweight-styling.html), this package enforces that all colors must be defined in ResourceDictionary and referenced via `StaticResource` or `ThemeResource`.

### What is detected

**In XAML files:**
- Hex colors: `Background="#FF0000"`, `Color="#AARRGGBB"`
- Named colors: `Foreground="Red"`, `Fill="Blue"`

**In C# files:**
- `Colors.*` usage (e.g., `Colors.Red`)
- `Color.FromArgb()` / `Color.FromRgb()` calls
- `new SolidColorBrush(Colors.*)` instantiations
- Hex color strings: `Color.Parse("#FF0000")`

### What is allowed

- Colors defined in ResourceDictionary files (files containing `<ResourceDictionary>` or named with "Resources", "Theme", "Colors", "Brushes")
- Usage via `{StaticResource MyColor}` or `{ThemeResource MyColor}`
- Resource lookups in code: `Application.Current.Resources["MyColorBrush"]`

### Correct usage example

```xml
<!-- In Resources/Colors.xaml (ResourceDictionary) -->
<Color x:Key="PrimaryColor">#FF6200EE</Color>
<SolidColorBrush x:Key="PrimaryBrush" Color="{StaticResource PrimaryColor}" />

<!-- In MainPage.xaml -->
<Button Background="{StaticResource PrimaryBrush}" />
```

```csharp
// In code-behind
var brush = (SolidColorBrush)Application.Current.Resources["PrimaryBrush"];
```

### Configuration

You can configure the color validation in your `.csproj`:

```xml
<PropertyGroup>
  <!-- Disable color validation (default: true) -->
  <UnoCodeStyleValidateColors>false</UnoCodeStyleValidateColors>

  <!-- Set severity to warning instead of error (default: error) -->
  <UnoCodeStyleColorValidationSeverity>warning</UnoCodeStyleColorValidationSeverity>
</PropertyGroup>
```

## Included Rules

All common rules plus Uno Platform specific configurations.
