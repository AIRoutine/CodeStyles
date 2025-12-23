# AIRoutine.CodeStyle.Common

Strict C# code style rules for all .NET projects.

## Installation

```xml
<PackageReference Include="AIRoutine.CodeStyle.Common" Version="1.0.0" PrivateAssets="all" />
```

## Features

- **Build-time enforcement**: Code style violations cause build errors
- **Strict naming conventions**: Interfaces (I prefix), private fields (_ prefix), static fields (s_ prefix), async methods (Async suffix)
- **Modern C# preferences**: File-scoped namespaces, pattern matching, collection expressions
- **Code quality**: Removes unused code, enforces accessibility modifiers, readonly fields

## Included Rules

All IDE rules (IDE0001-IDE0320) set to `error` severity including:
- Language preferences (var, expression bodies, pattern matching)
- Naming conventions (PascalCase, camelCase, prefixes/suffixes)
- Code quality (unused members, unnecessary code)
