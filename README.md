# J18n

A JSON-based localization library for .NET that provides a drop-in replacement for resx-based resources, fully compatible with `IStringLocalizer<T>` and the Microsoft.Extensions.Localization ecosystem.

## Features

- **Drop-in replacement** for resx resources using JSON files
- **Full compatibility** with `IStringLocalizer<T>` and Microsoft.Extensions.Localization
- **Nested JSON support** with automatic flattening using dot notation (e.g., `"User.Profile.Name"`)
- **Array handling** with indexed access (e.g., `"Messages[0]"`, `"Items[1].Name"`)
- **Culture hierarchy fallback** (specific → parent → English)
- **Parameterized string formatting** using .NET composite format syntax
- **Thread-safe operations** with built-in caching for optimal performance
- **Graceful fallback** for missing keys (returns key name)
- **Easy dependency injection** integration
- **Roslyn analyzers** for compile-time validation and IntelliSense support
- **Comprehensive XML documentation**
- **Extensive test coverage**

## Installation

Install the main package via NuGet Package Manager:

```bash
dotnet add package J18n
```

Or via Package Manager Console:

```powershell
Install-Package J18n
```

### Optional: Roslyn Analyzers

For compile-time validation and IntelliSense support, also install the analyzers package:

```bash
dotnet add package J18n.Analyzers
```

Or via Package Manager Console:

```powershell
Install-Package J18n.Analyzers
```

## Quick Start

### 1. Set up JSON resource files

Create JSON files in your Resources directory following the naming convention `{ResourceName}.{Culture}.json`:

```
Resources/
├── Messages.en.json
├── Messages.es.json
├── Messages.fr.json
└── Errors.en.json
```

**Note:** Unlike resx files, JSON resource files only need to be set as "Copy to Output Directory" in their build action. They do not need to be embedded resources.

Example `Messages.en.json`:
```json
{
  "WelcomeMessage": "Welcome to our application!",
  "HelloUser": "Hello, {0}!",
  "ItemCount": "You have {0:N0} items",
  "ValidationError": "The field {0} is required"
}
```

### 2. Register services

In your `Program.cs` or `Startup.cs`:

```csharp
var builder = WebApplication.CreateBuilder(args);

// Simple registration with default options
builder.Services.AddJsonLocalization();

// Or with custom configuration
builder.Services.AddJsonLocalization(options =>
{
    options.ResourcesRelativePath = "Localization/Resources";
});

var app = builder.Build();
```

### 3. Use in your application

```csharp
public class HomeController : ControllerBase
{
    private readonly IStringLocalizer<Messages> _localizer;

    public HomeController(IStringLocalizer<Messages> localizer)
    {
        _localizer = localizer;
    }

    public IActionResult Index()
    {
        // Simple string
        var welcome = _localizer["WelcomeMessage"];
        
        // Parameterized string
        var greeting = _localizer["HelloUser", User.Identity.Name];
        
        // Formatted numbers
        var itemCount = _localizer["ItemCount", 1234];
        
        return View(new { welcome, greeting, itemCount });
    }
}
```

## Configuration Options

### Default Configuration

```csharp
services.AddJsonLocalization();
```

This uses:
- Resources directory: `Resources/` (relative to current directory)
- Default fallback behavior

### Custom Resource Path

```csharp
services.AddJsonLocalization(options =>
{
    options.ResourcesRelativePath = "Localization/Messages";
    // or
    options.ResourcesPath = "/absolute/path/to/resources";
});
```

### Custom File Provider

```csharp
var customFileProvider = new PhysicalFileProvider("/custom/path");
services.AddJsonLocalization(customFileProvider, "Resources");
```

## Resource File Format

JSON resource files support both flat key-value pairs and nested object structures. Nested objects are automatically flattened using dot notation for easy access.

```json
{
  "WelcomeMessage": "Welcome to our app!",
  "User": {
    "Profile": {
      "Greeting": "Hello, {0}!",
      "Settings": {
        "Language": "English"
      }
    }
  },
  "GoodbyeMessage": "See you later!"
}
```

### Accessing Nested Values

Nested values are accessed using dot notation:

```csharp
var welcome = localizer["WelcomeMessage"];

// Nested object access
var greeting = localizer["User.Profile.Greeting", "John"];
var language = localizer["User.Profile.Settings.Language"];
var status = localizer["User.Account.Status"];

// Array access with indexes
var firstMessage = localizer["Messages[0]"];
var secondMessage = localizer["Messages[1]"];
```

## Culture Fallback

The library follows standard .NET culture fallback patterns:

1. **Specific culture** (e.g., `en-US`)
2. **Parent culture** (e.g., `en`)
3. **English** as final fallback
4. **Key name** if no translation found

Example with `CultureInfo.CurrentUICulture = new CultureInfo("es-MX")`:

```
Resources/
├── Messages.es-MX.json  ← First choice
├── Messages.es.json     ← Second choice
├── Messages.en.json     ← Third choice
└── Messages.json        ← Final fallback
```

## Advanced Usage

### Getting All Strings

```csharp
var allStrings = localizer.GetAllStrings(includeParentCultures: true);
foreach (var localizedString in allStrings)
{
    Console.WriteLine($"{localizedString.Name}: {localizedString.Value}");
}
```

### Checking if Resource was Found

```csharp
var localized = localizer["SomeKey"];
if (localized.ResourceNotFound)
{
    // Handle missing translation
    Console.WriteLine($"Missing translation for key: {localized.Name}");
}
```

### Using with Dependency Injection

```csharp
public class EmailService
{
    private readonly IStringLocalizer<EmailService> _localizer;

    public EmailService(IStringLocalizer<EmailService> localizer)
    {
        _localizer = localizer;
    }

    public async Task SendWelcomeEmailAsync(string email, string name)
    {
        var subject = _localizer["WelcomeEmailSubject"];
        var body = _localizer["WelcomeEmailBody", name];
        
        await SendEmailAsync(email, subject, body);
    }
}
```

## Performance Considerations

- Resources are **cached after first load** for optimal performance
- **Thread-safe** operations allow concurrent access
- **Lazy loading** - resources loaded only when first accessed
- **Memory efficient** - shares resources across localizer instances

## Compatibility

- **.NET 9.0** and later
- **Microsoft.Extensions.Localization** 9.0.8
- **System.Text.Json** for JSON parsing
- **Full compatibility** with existing `IStringLocalizer<T>` implementations

## Contributing

1. Fork the repository
2. Create a feature branch
3. Add tests for new functionality
4. Ensure all tests pass
5. Submit a pull request

## Testing

Run the test suite:

```bash
dotnet test
```

The project includes comprehensive tests covering:
- Basic localization functionality
- Culture fallback scenarios
- Parameterized strings
- Error handling
- Dependency injection integration

## License

This project is licensed under the GNU General Public License v3.0 - see the [LICENSE](LICENSE) file for details.