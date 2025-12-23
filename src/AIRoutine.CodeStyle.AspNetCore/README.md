# AIRoutine.CodeStyle.AspNetCore

Strict C# code style rules for ASP.NET Core backend projects.

## Installation

```xml
<PackageReference Include="AIRoutine.CodeStyle.AspNetCore" Version="1.0.0" PrivateAssets="all" />
```

## Features

Includes all rules from `AIRoutine.CodeStyle.Common` plus:

- **ASP.NET Core specific relaxations**: ConfigureAwait not required (ASP.NET Core has no synchronization context)
- **CA1062 relaxed**: Nullable reference types handle null validation
- **CA5394 relaxed**: Random is acceptable for non-security scenarios

## Included Rules

All common rules plus ASP.NET Core specific configurations.
