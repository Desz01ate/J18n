using System.Globalization;
using Microsoft.Extensions.Localization;

namespace J18n;

/// <summary>
/// Factory for creating JSON-based string localizers that implement the <see cref="IStringLocalizerFactory"/> interface.
/// This factory provides a centralized way to create localizer instances for different resource types and cultures.
/// </summary>
/// <remarks>
/// <para>
/// The JsonStringLocalizerFactory creates localizer instances that use JSON files as the resource store,
/// providing a modern alternative to traditional resx-based localization. The factory automatically
/// handles resource loading, culture resolution, and caching through the underlying resource loader.
/// </para>
/// <para>
/// The factory supports two primary creation methods:
/// <list type="bullet">
/// <item><description>Type-based creation using <see cref="Create(Type)"/> - determines resource base name from the type</description></item>
/// <item><description>Name-based creation using <see cref="Create(string, string)"/> - uses explicit resource name</description></item>
/// </list>
/// </para>
/// <para>
/// All created localizers share the same underlying <see cref="JsonResourceLoader"/> instance,
/// ensuring efficient resource caching and consistent behavior across the application.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Create factory with resource loader
/// var factory = new JsonStringLocalizerFactory(resourceLoader);
/// 
/// // Create localizer for specific resource type
/// var homeLocalizer = factory.Create(typeof(HomePageResource));
/// var loginLocalizer = factory.Create&lt;LoginResource&gt;();
/// 
/// // Create localizer with explicit name
/// var customLocalizer = factory.Create("CustomMessages", "Location");
/// 
/// // Use with dependency injection
/// services.AddSingleton&lt;IStringLocalizerFactory, JsonStringLocalizerFactory&gt;();
/// </code>
/// </example>
public class JsonStringLocalizerFactory : IStringLocalizerFactory
{
    private readonly JsonResourceLoader _resourceLoader;

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonStringLocalizerFactory"/> class.
    /// </summary>
    /// <param name="resourceLoader">The resource loader to use for loading JSON resource files.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="resourceLoader"/> is null.
    /// </exception>
    /// <remarks>
    /// The factory will use the provided resource loader for all localizer instances it creates.
    /// The resource loader handles the actual loading, parsing, and caching of JSON resource files.
    /// </remarks>
    public JsonStringLocalizerFactory(JsonResourceLoader resourceLoader)
    {
        this._resourceLoader = resourceLoader ?? throw new ArgumentNullException(nameof(resourceLoader));
    }

    /// <summary>
    /// Creates a string localizer for the specified resource source type.
    /// </summary>
    /// <param name="resourceSource">The type representing the resource for which to create a localizer.</param>
    /// <returns>
    /// An <see cref="IStringLocalizer"/> instance configured for the specified resource type.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="resourceSource"/> is null.
    /// </exception>
    /// <remarks>
    /// <para>
    /// This method creates a localizer using the type name as the resource base name.
    /// For example, if the resource source is of type "HomePageResource", the localizer
    /// will look for JSON files named "HomePageResource.{culture}.json".
    /// </para>
    /// <para>
    /// The localizer is created using the current UI culture (<see cref="CultureInfo.CurrentUICulture"/>),
    /// making it suitable for applications where the culture may change during execution.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Creates localizer for HomePageResource type
    /// var localizer = factory.Create(typeof(HomePageResource));
    /// // Will look for: HomePageResource.en.json, HomePageResource.es.json, etc.
    /// 
    /// string welcome = localizer["WelcomeMessage"];
    /// </code>
    /// </example>
    public IStringLocalizer Create(Type resourceSource)
    {
        ArgumentNullException.ThrowIfNull(resourceSource);

        var baseName = resourceSource.Name;
        
        var culture = CultureInfo.CurrentUICulture;

        return new JsonStringLocalizer(this._resourceLoader, baseName, culture);
    }

    /// <summary>
    /// Creates a string localizer using the specified base name and location.
    /// </summary>
    /// <param name="baseName">The base name to use for resource file lookup.</param>
    /// <param name="location">The location parameter (currently not used in the implementation, maintained for interface compatibility).</param>
    /// <returns>
    /// An <see cref="IStringLocalizer"/> instance configured with the specified base name.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="baseName"/> is null or empty.
    /// </exception>
    /// <remarks>
    /// <para>
    /// This method creates a localizer using the explicitly provided base name for resource file lookup.
    /// The localizer will look for JSON files named "{baseName}.{culture}.json".
    /// </para>
    /// <para>
    /// The <paramref name="location"/> parameter is maintained for compatibility with the
    /// <see cref="IStringLocalizerFactory"/> interface but is not used in the current implementation.
    /// Resource file location is determined by the underlying <see cref="JsonResourceLoader"/> configuration.
    /// </para>
    /// <para>
    /// Like the type-based creation method, this uses the current UI culture for localization.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Creates localizer with explicit base name
    /// var localizer = factory.Create("ErrorMessages", "App.Resources");
    /// // Will look for: ErrorMessages.en.json, ErrorMessages.es.json, etc.
    /// 
    /// string error = localizer["ValidationError"];
    /// </code>
    /// </example>
    public IStringLocalizer Create(string baseName, string location)
    {
        if (string.IsNullOrEmpty(baseName))
        {
            throw new ArgumentNullException(nameof(baseName));
        }
        
        var culture = CultureInfo.CurrentUICulture;

        return new JsonStringLocalizer(this._resourceLoader, baseName, culture);
    }
}