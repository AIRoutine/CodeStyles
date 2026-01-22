# CLAUDE.md

This file contains project-specific instructions for AI assistants working on this codebase.

## Project Overview

AIRoutine.CodeStyle is a NuGet package providing strict C# and XAML code style rules for .NET projects, with specialized support for Uno Platform.

## Key Principles

### No Configuration Options for Validation Rules

**IMPORTANT:** When creating or modifying validation rules in this project:

- **DO NOT** create MSBuild properties to enable/disable individual rules
- **DO NOT** create configuration options for severity levels
- **ALL** validation rules must be enforced as **errors** that fail the build
- The only exception is `UnoCodeStyleAllowedSpacingValues` which configures allowed spacing values (not enable/disable)

The philosophy of this package is: install it to enforce ALL rules. If a project doesn't want a rule, they should not use this package.

### Rule Implementation Pattern

All XAML validation rules follow this pattern:

```xml
<Target Name="ValidateXxx"
        BeforeTargets="BeforeBuild"
        Condition="'$(DesignTimeBuild)' != 'true'">
  <!-- ... -->
  <ValidateXamlXxx XamlFiles="@(_XamlFiles)" Severity="error" />
</Target>
```

- Always use `Severity="error"`
- Only condition is `'$(DesignTimeBuild)' != 'true'`
- No additional enable/disable conditions

## Project Structure

- `src/AIRoutine.CodeStyle.Common/` - Base C# rules for all .NET projects
- `src/AIRoutine.CodeStyle.Uno/` - Uno Platform + XAML specific rules
- `src/AIRoutine.CodeStyle.AspNetCore/` - ASP.NET Core relaxations
- `tests/` - Integration tests with fixture projects

## Build & Test

```bash
# Build all packages
dotnet build -c Release

# Run tests
cd tests && dotnet test
```
