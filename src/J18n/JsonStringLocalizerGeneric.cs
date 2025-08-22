using System.Globalization;
using Microsoft.Extensions.Localization;

namespace J18n;

/// <summary>
/// Provides strongly-typed JSON-based string localization functionality for a specific resource type.
/// This class extends <see cref="JsonStringLocalizer"/> and implements <see cref="IStringLocalizer{T}"/> 
/// to provide type-safe localization services.
/// </summary>
/// <typeparam name="T">
/// The type representing the resource for which to provide localized strings.
/// The type name is used as the base name for resource file resolution.
/// </typeparam>
/// <remarks>
/// <para>
/// This generic localizer automatically determines the resource base name from the type parameter T,
/// using the type's name to locate the appropriate JSON resource files. This provides compile-time
/// type safety and better IntelliSense support when used with dependency injection.
/// </para>
/// <para>
/// Resource files should be named following the pattern: {TypeName}.{Culture}.json
/// where TypeName matches the name of type T.
/// </para>
/// <para>
/// All functionality from the base <see cref="JsonStringLocalizer"/> class is available,
/// including culture hierarchy fallback, parameterized string formatting, and caching.
/// The localizer automatically uses <see cref="CultureInfo.CurrentUICulture"/> for culture resolution.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // For a resource type named 'HomePageResource'
/// public class HomePageResource { }
/// 
/// // The localizer will look for: HomePageResource.en.json, HomePageResource.es.json, etc.
/// var localizer = new JsonStringLocalizer&lt;HomePageResource&gt;(resourceLoader);
/// string welcome = localizer["WelcomeMessage"]; // Type-safe access to HomePageResource strings
/// 
/// // Use with dependency injection
/// public class HomeController
/// {
///     private readonly IStringLocalizer&lt;HomePageResource&gt; _localizer;
///     
///     public HomeController(IStringLocalizer&lt;HomePageResource&gt; localizer)
///     {
///         _localizer = localizer;
///     }
/// }
/// </code>
/// </example>
public class JsonStringLocalizer<T> : JsonStringLocalizer, IStringLocalizer<T>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JsonStringLocalizer{T}"/> class.
    /// </summary>
    /// <param name="resourceLoader">The resource loader used to load JSON resource files.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="resourceLoader"/> is null.
    /// </exception>
    /// <remarks>
    /// The constructor automatically determines the resource base name from the type parameter T
    /// and uses the current UI culture (<see cref="CultureInfo.CurrentUICulture"/>) for localization.
    /// This makes it suitable for dependency injection scenarios where the culture may change
    /// during the application lifecycle.
    /// </remarks>
    public JsonStringLocalizer(JsonResourceLoader resourceLoader)
        : base(resourceLoader, GetResourceBaseName(typeof(T)), CultureInfo.CurrentUICulture)
    {
    }

    /// <summary>
    /// Gets the resource base name from the specified type.
    /// </summary>
    /// <param name="type">The type for which to determine the resource base name.</param>
    /// <returns>The name of the type, used as the base name for resource file lookup.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="type"/> is null.
    /// </exception>
    /// <remarks>
    /// This method extracts the simple name of the type (without namespace) to use as the
    /// base name for JSON resource files. For example, if the type is "MyApp.Resources.HomePageResource",
    /// this method returns "HomePageResource" which will be used to look for files like
    /// "HomePageResource.en.json", "HomePageResource.es.json", etc.
    /// </remarks>
    private static string GetResourceBaseName(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        return type.Name;
    }
}