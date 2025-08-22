using System.Globalization;
using Microsoft.Extensions.Localization;

namespace J18n;

/// <summary>
/// Provides JSON-based string localization functionality, implementing the <see cref="IStringLocalizer"/> interface.
/// This class serves as a drop-in replacement for resx-based localization, using JSON files as the resource store.
/// </summary>
/// <remarks>
/// <para>
/// The JsonStringLocalizer loads localized strings from JSON resource files and provides culture-aware
/// string retrieval with support for parameterized strings and fallback behavior.
/// </para>
/// <para>
/// Key features:
/// <list type="bullet">
/// <item><description>Culture hierarchy fallback (specific → parent → English)</description></item>
/// <item><description>Parameterized string formatting using .NET string format syntax</description></item>
/// <item><description>Caching for optimal performance</description></item>
/// <item><description>Thread-safe operations</description></item>
/// <item><description>Graceful handling of missing keys (returns key name as fallback)</description></item>
/// </list>
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Basic usage
/// var localizer = new JsonStringLocalizer(resourceLoader, "MyResource", CultureInfo.CurrentUICulture);
/// string welcome = localizer["WelcomeMessage"]; // Returns localized welcome message
/// 
/// // Parameterized strings
/// string greeting = localizer["HelloUser", "John"]; // Returns "Hello, John!" (localized)
/// 
/// // Get all strings
/// var allStrings = localizer.GetAllStrings(includeParentCultures: true);
/// </code>
/// </example>
public class JsonStringLocalizer : IStringLocalizer
{
    private readonly JsonResourceLoader _resourceLoader;
    private readonly string _baseName;
    private readonly CultureInfo _culture;
    private readonly Dictionary<string, string> _resources;

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonStringLocalizer"/> class.
    /// </summary>
    /// <param name="resourceLoader">The resource loader used to load JSON resource files.</param>
    /// <param name="baseName">The base name of the resource (typically matches the resource class name).</param>
    /// <param name="culture">The culture for which to load localized resources.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when any of the parameters (<paramref name="resourceLoader"/>, <paramref name="baseName"/>, 
    /// or <paramref name="culture"/>) is null.
    /// </exception>
    /// <remarks>
    /// The constructor immediately loads all resources for the specified culture using the resource loader.
    /// Resources are loaded following the culture hierarchy fallback pattern and are cached for subsequent access.
    /// </remarks>
    public JsonStringLocalizer(JsonResourceLoader resourceLoader, string baseName, CultureInfo culture)
    {
        this._resourceLoader = resourceLoader ?? throw new ArgumentNullException(nameof(resourceLoader));
        this._baseName = baseName ?? throw new ArgumentNullException(nameof(baseName));
        this._culture = culture ?? throw new ArgumentNullException(nameof(culture));
        this._resources = this._resourceLoader.LoadResources(this._baseName, this._culture);
    }

    /// <summary>
    /// Gets the localized string with the specified name.
    /// </summary>
    /// <param name="name">The name/key of the localized string to retrieve.</param>
    /// <returns>
    /// A <see cref="LocalizedString"/> object containing the localized value.
    /// If the key is not found, returns the key name as the value with the ResourceNotFound flag set to true.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="name"/> is null or empty.
    /// </exception>
    /// <remarks>
    /// This indexer provides the primary mechanism for retrieving localized strings.
    /// It follows the culture hierarchy fallback pattern and handles missing keys gracefully
    /// by returning the key name itself as a fallback value.
    /// </remarks>
    /// <example>
    /// <code>
    /// var welcomeMessage = localizer["WelcomeMessage"];
    /// Console.WriteLine(welcomeMessage.Value); // Prints localized welcome message
    /// Console.WriteLine(welcomeMessage.ResourceNotFound); // False if found, true if using fallback
    /// </code>
    /// </example>
    public LocalizedString this[string name]
    {
        get
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentNullException(nameof(name));
            }

            var value = this.GetStringValue(name);
            var resourceNotFound = value == name;
            
            return new LocalizedString(name, value, resourceNotFound);
        }
    }

    /// <summary>
    /// Gets the localized string with the specified name and formats it with the provided arguments.
    /// </summary>
    /// <param name="name">The name/key of the localized string to retrieve.</param>
    /// <param name="arguments">The arguments to use for formatting the localized string.</param>
    /// <returns>
    /// A <see cref="LocalizedString"/> object containing the formatted localized value.
    /// If the key is not found, returns the key name as the value with the ResourceNotFound flag set to true.
    /// If formatting fails, returns the unformatted string.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="name"/> is null or empty.
    /// </exception>
    /// <remarks>
    /// <para>
    /// This indexer supports .NET composite formatting syntax (e.g., "Hello, {0}!" or "You have {0:N0} items").
    /// The formatting is culture-aware and uses the culture specified during localizer construction.
    /// </para>
    /// <para>
    /// If a <see cref="FormatException"/> occurs during string formatting, the method gracefully
    /// returns the unformatted string instead of throwing an exception.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // JSON: { "WelcomeUser": "Welcome, {0}!" }
    /// var greeting = localizer["WelcomeUser", "John"];
    /// Console.WriteLine(greeting.Value); // Prints: "Welcome, John!"
    /// 
    /// // JSON: { "ItemCount": "You have {0:N0} items" }
    /// var itemMessage = localizer["ItemCount", 1234];
    /// Console.WriteLine(itemMessage.Value); // Prints: "You have 1,234 items" (culture-dependent)
    /// </code>
    /// </example>
    public LocalizedString this[string name, params object[] arguments]
    {
        get
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentNullException(nameof(name));
            }

            var format = this.GetStringValue(name);
            var resourceNotFound = format == name;
            
            string value;
            try
            {
                value = string.Format(this._culture, format, arguments);
            }
            catch (FormatException)
            {
                value = format;
            }

            return new LocalizedString(name, value, resourceNotFound);
        }
    }

    /// <summary>
    /// Gets all localized strings available for this localizer's culture.
    /// </summary>
    /// <param name="includeParentCultures">
    /// When true, includes strings from parent cultures and fallback cultures in addition to the specific culture.
    /// When false, returns only strings that would be loaded for the current culture (which may include parent culture strings due to the loading strategy).
    /// </param>
    /// <returns>
    /// An enumerable collection of <see cref="LocalizedString"/> objects representing all available localized strings.
    /// All returned strings have their ResourceNotFound property set to false since they exist in the resource collection.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This method provides access to all localized strings that would be available through the indexer properties.
    /// The actual behavior of the <paramref name="includeParentCultures"/> parameter depends on the current
    /// implementation, where resources are pre-loaded including parent culture fallbacks.
    /// </para>
    /// <para>
    /// The method is useful for scenarios like:
    /// <list type="bullet">
    /// <item><description>Enumerating all available localization keys</description></item>
    /// <item><description>Building localization management tools</description></item>
    /// <item><description>Validating resource completeness</description></item>
    /// <item><description>Debugging localization issues</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var allStrings = localizer.GetAllStrings(includeParentCultures: true);
    /// foreach (var localizedString in allStrings)
    /// {
    ///     Console.WriteLine($"{localizedString.Name}: {localizedString.Value}");
    /// }
    /// </code>
    /// </example>
    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
    {
        var resources = includeParentCultures 
            ? this._resources 
            : this._resourceLoader.LoadResources(this._baseName, this._culture);

        return resources.Select(kvp => new LocalizedString(kvp.Key, kvp.Value, resourceNotFound: false));
    }


    /// <summary>
    /// Gets the localized string value for the specified key, with fallback to the key name if not found.
    /// </summary>
    /// <param name="name">The localization key to look up.</param>
    /// <returns>
    /// The localized string value if found in the resources, otherwise returns the key name as fallback.
    /// </returns>
    /// <remarks>
    /// This private helper method provides consistent fallback behavior throughout the localizer.
    /// When a key is not found in the loaded resources, it returns the key name itself,
    /// which allows applications to continue functioning even with missing translations.
    /// </remarks>
    private string GetStringValue(string name)
    {
        return this._resources.GetValueOrDefault(name, name);
    }
}