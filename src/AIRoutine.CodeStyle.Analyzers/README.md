# AIRoutine.CodeStyle.Analyzers

Roslyn-based C# code analyzers for strict code style enforcement with IDE integration.

## Rules

| Rule ID | Description | Category | Code Fix |
|---------|-------------|----------|----------|
| ACS0001 | No hardcoded strings in ViewModels, Services, Handlers | Maintainability | Extract to constant |
| ACS0002 | No static method calls on non-framework types | Design | Inject via constructor |
| ACS0003 | No hardcoded colors in C# code | Maintainability | Use resource lookup |
| ACS0012 | No ICommand properties in ViewModels/Models | Design | - |
| ACS0013 | No command attributes except [UnoCommand] | Design | - |
| ACS0014 | C# Markup: No inline visual properties (use Style) | Design | - |
| ACS0015 | C# Markup: UI controls must have Style | Maintainability | - |
| ACS0016 | C# Markup: AutomationId must follow naming pattern | Naming | - |
| ACS0017 | C# Markup: StaticResource must use constants from *.Core.Styles | Maintainability | Replace with constant |
| ACS0018 | Use C# 14 extension block syntax instead of 'this' parameter | Style | - |
| ACS0019 | Use Shiny Mediator HTTP instead of direct HttpClient | Design | - |

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

## ACS0012: No ICommand Properties

Forbids direct `ICommand` property declarations in ViewModel/Model classes. Commands must be defined using the `[UnoCommand]` attribute on methods instead.

### Detected Patterns

- `public ICommand MyCommand { get; }`
- `public IRelayCommand Command { get; }`
- `public AsyncRelayCommand SaveCommand { get; }`
- Any property implementing `System.Windows.Input.ICommand`

### Applies To

Classes ending with:
- `*ViewModel` (MVVM pattern)
- `*Model` (MVUX pattern)

### Example

```csharp
// Bad - ACS0012
public class MainViewModel
{
    public ICommand SaveCommand { get; }  // Error
}

// Good
public class MainViewModel
{
    [UnoCommand]
    public void Save() { }  // OK - generates command automatically
}
```

---

## ACS0013: No Forbidden Command Attributes

Forbids command attributes from other frameworks. Only `[UnoCommand]` is allowed.

### Forbidden Attributes

- `[RelayCommand]` (CommunityToolkit.Mvvm)
- `[AsyncRelayCommand]` (CommunityToolkit.Mvvm)
- `[Command]` (various frameworks)
- `[AsyncCommand]`
- `[DelegateCommand]` (Prism)
- `[ReactiveCommand]` (ReactiveUI)

### Example

```csharp
// Bad - ACS0013
public class MainViewModel
{
    [RelayCommand]  // Error - use [UnoCommand] instead
    public void Save() { }
}

// Good
public class MainViewModel
{
    [UnoCommand]  // OK
    public void Save() { }
}
```

---

## ACS0014: No Inline Visual Properties in C# Markup

Forbids setting visual properties inline in C# Markup. These should come from Style instead.

### Forbidden Methods

- **Spacing:** `.Padding()`, `.Margin()`
- **Colors:** `.Background()`, `.Foreground()`, `.BorderBrush()`, `.Fill()`, `.Stroke()`
- **Typography:** `.FontSize()`, `.FontWeight()`, `.FontFamily()`
- **Sizing:** `.CornerRadius()`, `.BorderThickness()`

### Example

```csharp
// Bad - ACS0014
new Grid()
    .Padding(16)                                    // Error - use Style
    .Background(x => x.StaticResource("MyBrush"))   // Error - use Style

// Good - visual properties come from Style
new Grid()
    .Style(x => x.StaticResource("MyGridStyle"))    // Style defines Padding, Background, etc.
```

---

## ACS0017: StaticResource Must Use Constants

Forbids hardcoded strings in `.StaticResource()` and `.ThemeResource()` calls. Resource keys must be constants from a `*.Core.Styles` project.

### Allowed Constant Sources

Classes/namespaces containing: `Styles`, `StyleKeys`, `ResourceKeys`, `ThemeKeys`, `BrushKeys`, `ColorKeys`

### Example

```csharp
// Bad - ACS0017
.Style(x => x.StaticResource("BodyTextBlockStyle"))     // Error - hardcoded string
.Background(x => x.StaticResource("SurfaceBrush"))      // Error - hardcoded string

// Good - constants from Core.Styles project
.Style(x => x.StaticResource(StyleKeys.BodyTextBlock))  // OK
.Background(x => x.StaticResource(BrushKeys.Surface))   // OK
```

### Required Project Structure

```
MyApp.Core.Styles/
├── StyleKeys.cs      // public const string BodyTextBlock = "BodyTextBlockStyle";
├── BrushKeys.cs      // public const string Surface = "SurfaceBrush";
└── ColorKeys.cs      // public const string Primary = "PrimaryColor";
```

---

## ACS0015: C# Markup Style Required

Ensures UI controls in C# Markup have explicit Style assignments for consistent theming.

### Applies To

Common UI controls: Button, TextBlock, TextBox, ListView, ComboBox, Border, etc.

### Example

```csharp
// Bad - ACS0015
new TextBlock()
    .Text("Hello");  // Error - missing Style

// Good
new TextBlock()
    .Style(x => x.StaticResource("BodyTextBlockStyle"))
    .Text("Hello");
```

---

## ACS0016: C# Markup AutomationId Format

Validates that AutomationId values follow a consistent naming pattern for test automation.

### Required Pattern

`PageName.ControlType.Purpose` or `PageName.Purpose`

Examples: `LoginPage.Button.Submit`, `SettingsPage.Username`, `MainPage.Root`

### Example

```csharp
// Bad - ACS0016
.AutomationProperties(ap => ap.AutomationId("submit"))  // Error
.AutomationProperties(ap => ap.AutomationId("btn_submit"))  // Error

// Good
.AutomationProperties(ap => ap.AutomationId("LoginPage.Button.Submit"))  // OK
.AutomationProperties(ap => ap.AutomationId("FooterPage.Root"))  // OK
```

---

## Installation

```xml
<PackageReference Include="AIRoutine.CodeStyle.Analyzers" Version="1.1.0" PrivateAssets="all" />
```

## IDE Integration

These analyzers provide real-time feedback in Visual Studio, VS Code, and Rider:
- Squiggly underlines for violations
- Error messages in Error List / Problems panel
- Build failures for violations (Severity: Error)

## Code Fixes

All rules include automatic code fixes accessible via the light bulb menu (Ctrl+.):

| Rule | Code Fix Action |
|------|-----------------|
| ACS0001 | Extracts hardcoded string to a `private const` field |
| ACS0002 | Creates interface field, adds constructor parameter, updates call |
| ACS0003 | Replaces with `Application.Current.Resources["..."]` lookup |

---

## ACS0018: Use Extension Block Syntax

Detects classic extension methods using the `this` parameter modifier and suggests using the new C# 14 extension block syntax instead.

### Why

C# 14 introduces a new `extension` block syntax that:
- Provides clearer semantics
- Supports extension properties (not possible with classic syntax)
- Supports extension operators
- Supports static extensions on the type itself
- Groups related extension members together

### Detected Patterns

```csharp
public static class StringExtensions
{
    public static int WordCount(this string str) => str.Split(' ').Length;
}
```

### Example

```csharp
// Bad - ACS0018 (classic 'this' parameter syntax)
public static class StringExtensions
{
    public static int WordCount(this string str)
        => str.Split(' ').Length;

    public static string Truncate(this string str, int maxLength)
        => str.Length <= maxLength ? str : str[..maxLength] + "...";
}

// Good - C# 14 extension block syntax
public static class StringExtensions
{
    extension(string str)
    {
        public int WordCount()
            => str.Split(' ').Length;

        public string Truncate(int maxLength)
            => str.Length <= maxLength ? str : str[..maxLength] + "...";

        // Extension property - only possible with new syntax!
        public bool IsNullOrEmpty => string.IsNullOrEmpty(str);
    }
}
```

### Configuration

The analyzer produces warnings by default. To treat as error, add to `.editorconfig`:

```ini
[*.cs]
dotnet_diagnostic.ACS0018.severity = error
```

### Reference

- [Microsoft Docs: Extension Members](https://learn.microsoft.com/dotnet/csharp/programming-guide/classes-and-structs/extension-methods)
- [C# 14 What's New](https://learn.microsoft.com/dotnet/csharp/whats-new/csharp-14)

---

## ACS0019: Use Shiny Mediator HTTP

Enforces the use of Shiny Mediator HTTP extension instead of direct HttpClient usage. All HTTP requests should go through the Mediator pattern using `IHttpRequest<T>` contracts.

### Why

Using Shiny Mediator HTTP provides:
- Consistent configuration via appsettings.json
- Centralized authentication via `IHttpRequestDecorator`
- Automatic error handling and retry policies
- Better testability (mock IMediator instead of HttpClient)
- Declarative HTTP contracts with attributes
- Support for OpenAPI code generation

### Forbidden Patterns

```csharp
// BAD - All of these trigger ACS0019

// Direct HttpClient instantiation
var client = new HttpClient();

// HttpClient as constructor parameter
public class MyService(HttpClient client) { }

// IHttpClientFactory injection
public class MyService(IHttpClientFactory factory) { }

// HttpClient method calls
await httpClient.GetAsync("https://api.example.com");
await httpClient.PostAsync("/data", content);
await httpClient.GetStringAsync("/endpoint");

// DI registration
services.AddHttpClient<MyService>();
```

### Correct Pattern

```csharp
// GOOD - Use Shiny Mediator HTTP contracts

// Define HTTP request contract
[Http(HttpVerb.Get, "/users/{UserId}")]
public class GetUserRequest : IHttpRequest<UserResponse>
{
    [HttpParameter(HttpParameterType.Path)]
    public string UserId { get; set; }
}

[Http(HttpVerb.Post, "/users")]
public class CreateUserRequest : IHttpRequest<UserResponse>
{
    [HttpParameter(HttpParameterType.Query)]
    public int? TenantId { get; set; }

    [HttpParameter(HttpParameterType.Header)]
    public string Authorization { get; set; }

    [HttpBody]
    public CreateUserPayload Body { get; set; }
}

// Use in service
public class UserService
{
    private readonly IMediator _mediator;

    public UserService(IMediator mediator) => _mediator = mediator;

    public Task<UserResponse> GetUserAsync(string id)
        => _mediator.Request(new GetUserRequest { UserId = id });
}

// Configure base URL in appsettings.json
{
    "Mediator": {
        "Http": {
            "MyApp.Api.*": "https://api.myapp.com"
        }
    }
}
```

### Allowed Exceptions

- `IHttpRequestDecorator` implementations are allowed to use `HttpRequestMessage` for adding headers, authentication, etc.

```csharp
// GOOD - Decorators are allowed
public class AuthDecorator : IHttpRequestDecorator
{
    public Task Decorate(HttpRequestMessage message, IMediatorContext context)
    {
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return Task.CompletedTask;
    }
}
```

### Reference

- [Shiny Mediator HTTP Documentation](https://shinylib.net/mediator/extensions/http/)
