# AIRoutine.CodeStyle.Analyzers

Roslyn-based C# code analyzers for strict code style enforcement with IDE integration.

## Rules

| Rule ID | Description | Category |
|---------|-------------|----------|
| ACS0001 | No hardcoded strings in ViewModels, Services, Handlers | Maintainability |
| ACS0002 | No static method calls on non-framework types | Design |
| ACS0003 | No hardcoded colors in C# code | Maintainability |

---

## ACS0001: No Hardcoded Strings

Detects hardcoded strings in files matching these patterns:
- `*ViewModel.cs`
- `*Service.cs`
- `*Handler.cs`

### Allowed Exceptions

| Pattern | Example |
|---------|---------|
| Empty strings | `""` |
| Single characters | `","` |
| Constants | `const string X = "..."` |
| Attributes | `[Route("api/users")]` |
| nameof() | `nameof(Property)` |
| Logging format strings | `"{User} logged in"` |
| Technical strings | URLs, paths, MIME types |
| Simple identifiers | `"PropertyName"` |

### Example

```csharp
// Bad - ACS0001
public class UserViewModel
{
    public string Title => "Welcome";  // Error
}

// Good
public class UserViewModel
{
    private const string DefaultTitle = "Welcome";  // OK
    public string Title => _localizer["WelcomeTitle"];  // OK
}
```

---

## ACS0002: No Static Calls

Enforces dependency injection by forbidding static method calls on non-framework types.

### Allowed

- `System.*` namespace (Console, Math, Path, etc.)
- `Microsoft.*` namespace
- `Windows.*` namespace
- Testing frameworks (Xunit, NUnit, Moq, etc.)
- Extension methods
- Enum values
- Calls to same type (factory methods)

### Example

```csharp
// Bad - ACS0002
public class OrderService
{
    public string Process() => MyHelper.Calculate();  // Error
}

// Good
public class OrderService
{
    private readonly IHelper _helper;
    public OrderService(IHelper helper) => _helper = helper;
    public string Process() => _helper.Calculate();  // OK (DI)
    public string GetPath() => Path.Combine("a", "b");  // OK (System)
}
```

---

## ACS0003: No Hardcoded Colors

Detects hardcoded colors in C# code. Colors should be defined in ResourceDictionary.

### Detected Patterns

- `Colors.Red`, `Colors.Blue`, etc.
- `Color.FromArgb(...)`, `Color.FromRgb(...)`
- `new SolidColorBrush(Colors.*)`
- `Color.Parse("#FFFFFF")`
- `ColorHelper.FromArgb(...)`

### Example

```csharp
// Bad - ACS0003
public Color GetColor() => Colors.Red;  // Error
public Color Custom() => Color.FromArgb(255, 100, 150, 200);  // Error

// Good
public Color GetColor() => (Color)Application.Current.Resources["PrimaryColor"];
```

---

## Installation

```xml
<PackageReference Include="AIRoutine.CodeStyle.Analyzers" Version="1.0.0" PrivateAssets="all" />
```

## IDE Integration

These analyzers provide real-time feedback in Visual Studio, VS Code, and Rider:
- Squiggly underlines for violations
- Error messages in Error List / Problems panel
- Build failures for violations (Severity: Error)
