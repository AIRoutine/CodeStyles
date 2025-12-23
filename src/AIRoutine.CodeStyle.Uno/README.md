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

## Included Rules

All common rules plus Uno Platform specific configurations.
