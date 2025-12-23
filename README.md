# AIRoutine.CodeStyle

Strict C# code style rules distributed as NuGet packages. All rules are enforced at build time - violations cause build errors.

## Packages

| Package | Description |
|---------|-------------|
| `AIRoutine.CodeStyle.Common` | Base rules for all .NET projects |
| `AIRoutine.CodeStyle.AspNetCore` | ASP.NET Core backend projects (includes Common) |
| `AIRoutine.CodeStyle.Uno` | Uno Platform projects (includes Common) |

## Installation

Add the appropriate package to your project:

```xml
<!-- For ASP.NET Core projects -->
<PackageReference Include="AIRoutine.CodeStyle.AspNetCore" Version="1.0.0" PrivateAssets="all" />

<!-- For Uno Platform projects -->
<PackageReference Include="AIRoutine.CodeStyle.Uno" Version="1.0.0" PrivateAssets="all" />

<!-- For other .NET projects -->
<PackageReference Include="AIRoutine.CodeStyle.Common" Version="1.0.0" PrivateAssets="all" />
```

## What's Enforced

### Naming Conventions (error severity)
- **Interfaces**: Must start with `I` (e.g., `IService`)
- **Types**: PascalCase (e.g., `MyClass`)
- **Private fields**: Must start with `_` (e.g., `_myField`)
- **Static fields**: Must start with `s_` (e.g., `s_instance`)
- **Constants**: PascalCase (e.g., `MaxValue`)
- **Parameters/Locals**: camelCase (e.g., `myParameter`)
- **Type parameters**: Must start with `T` (e.g., `TResult`)
- **Async methods**: Must end with `Async` (e.g., `GetDataAsync`)

### Language Preferences (error severity)
- File-scoped namespaces
- `var` usage when type is apparent
- Expression-bodied members
- Pattern matching
- Collection expressions
- Null propagation and coalescing
- Primary constructors (Common only)

### Code Quality (error severity)
- Remove unused private members
- Remove unnecessary usings
- Add accessibility modifiers
- Add readonly modifier to fields
- Simplify LINQ expressions

## Building Packages

```bash
cd src/AIRoutine.CodeStyle.Common
dotnet pack -c Release

cd ../AIRoutine.CodeStyle.AspNetCore
dotnet pack -c Release

cd ../AIRoutine.CodeStyle.Uno
dotnet pack -c Release
```

## Local Testing

To test locally, add a local NuGet source:

```bash
dotnet nuget add source C:\Users\Daniel\source\repos\ai\codestyle\packages -n LocalCodeStyle
```

## License

MIT
