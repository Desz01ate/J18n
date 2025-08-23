using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.FileProviders;

namespace J18n;

/// <summary>
/// Provides functionality to load and cache JSON-based localization resources.
/// This class handles loading JSON resource files, applying culture hierarchy fallback,
/// and caching resources for optimal performance.
/// </summary>
/// <remarks>
/// The JsonResourceLoader supports culture hierarchy fallback, where resources are loaded
/// in the following order: specific culture → parent culture → English fallback.
/// All loaded resources are cached in memory for subsequent requests.
/// 
/// Resource files should be named using the pattern: {ResourceName}.{Culture}.json
/// For example: SharedResource.en.json, SharedResource.es.json, etc.
/// </remarks>
public class JsonResourceLoader
{
    private readonly IFileProvider _fileProvider;
    private readonly string _resourcesPath;
    private readonly ConcurrentDictionary<string, Dictionary<string, string>> _resourceCache;

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonResourceLoader"/> class.
    /// </summary>
    /// <param name="fileProvider">The file provider used to access JSON resource files.</param>
    /// <param name="resourcesPath">The relative path to the directory containing resource files. Defaults to "Resources".</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="fileProvider"/> or <paramref name="resourcesPath"/> is null.
    /// </exception>
    /// <remarks>
    /// The resource path is relative to the file provider's root directory.
    /// Resource files should be organized in the specified directory with the naming convention:
    /// {ResourceName}.{Culture}.json
    /// </remarks>
    public JsonResourceLoader(IFileProvider fileProvider, string resourcesPath = "Resources")
    {
        this._fileProvider = fileProvider ?? throw new ArgumentNullException(nameof(fileProvider));
        this._resourcesPath = resourcesPath ?? throw new ArgumentNullException(nameof(resourcesPath));
        this._resourceCache = new ConcurrentDictionary<string, Dictionary<string, string>>();
    }

    /// <summary>
    /// Loads localization resources for the specified base name and culture.
    /// </summary>
    /// <param name="baseName">The base name of the resource (typically the resource class name).</param>
    /// <param name="culture">The target culture for which to load resources.</param>
    /// <returns>
    /// A dictionary containing all localization key-value pairs for the specified culture,
    /// including fallback values from parent cultures and English default.
    /// Returns an empty dictionary if no resources are found.
    /// </returns>
    /// <remarks>
    /// This method implements culture hierarchy fallback:
    /// 1. Loads resources for the specific culture (e.g., "es-ES")
    /// 2. Falls back to parent culture (e.g., "es")
    /// 3. Falls back to English ("en") as final fallback
    /// 4. Returns an empty dictionary if no resources found
    /// 
    /// Results are cached for subsequent calls with the same parameters.
    /// The cache key is based on both the base name and culture name.
    /// </remarks>
    /// <example>
    /// <code>
    /// var loader = new JsonResourceLoader(fileProvider, "Resources");
    /// var resources = loader.LoadResources("MyResource", new CultureInfo("es-ES"));
    /// // Will look for: MyResource.es-ES.json, MyResource.es.json, MyResource.en.json
    /// </code>
    /// </example>
    public Dictionary<string, string> LoadResources(string baseName, CultureInfo culture)
    {
        var cacheKey = $"{baseName}.{culture.Name}";

        return this._resourceCache.GetOrAdd(cacheKey, _ => this.LoadResourcesInternal(baseName, culture));
    }

    private Dictionary<string, string> LoadResourcesInternal(string baseName, CultureInfo culture)
    {
        var resources = new Dictionary<string, string>();
        var culturesToCheck = GetCultureHierarchy(culture);

        foreach (var cultureToCheck in culturesToCheck.AsEnumerable().Reverse())
        {
            var fileName = $"{baseName}.{cultureToCheck.Name}.json";
            var filePath = string.IsNullOrEmpty(this._resourcesPath) ? fileName : Path.Combine(this._resourcesPath, fileName);

            var fileInfo = this._fileProvider.GetFileInfo(filePath);

            if (!fileInfo.Exists)
            {
                continue;
            }

            var cultureResources = LoadJsonFile(fileInfo);

            foreach (var kvp in cultureResources)
            {
                resources[kvp.Key] = kvp.Value;
            }
        }

        return resources;
    }

    /// <summary>
    /// Gets the culture hierarchy for the specified culture, including fallback cultures.
    /// </summary>
    /// <param name="culture">The target culture.</param>
    /// <returns>
    /// A list of cultures in hierarchy order, from most specific to most general.
    /// Always includes English ("en") as the final fallback culture.
    /// </returns>
    /// <remarks>
    /// The hierarchy follows this pattern:
    /// 1. Specific culture (e.g., "es-ES" for Spanish in Spain)
    /// 2. Parent culture (e.g., "es" for Spanish)
    /// 3. English ("en") as universal fallback (if not already present)
    /// 
    /// This ensures that applications always have a fallback to English resources
    /// when specific or parent culture resources are not available.
    /// </remarks>
    /// <example>
    /// For culture "es-ES":
    /// Returns: ["es-ES", "es", "en"]
    /// 
    /// For culture "en-GB":
    /// Returns: ["en-GB", "en"]
    /// </example>
    private static List<CultureInfo> GetCultureHierarchy(CultureInfo culture)
    {
        var cultures = new List<CultureInfo>();
        var currentCulture = culture;

        while (!Equals(currentCulture, CultureInfo.InvariantCulture))
        {
            cultures.Add(currentCulture);
            currentCulture = currentCulture.Parent;
        }

        // Add English as fallback if it's not already in the hierarchy
        var englishCulture = new CultureInfo("en");

        if (!cultures.Any(c => c.Equals(englishCulture)))
        {
            cultures.Add(englishCulture);
        }

        return cultures;
    }

    /// <summary>
    /// Loads and parses a JSON resource file into a dictionary of key-value pairs.
    /// </summary>
    /// <param name="fileInfo">The file info object representing the JSON resource file.</param>
    /// <returns>
    /// A dictionary containing all key-value pairs from the JSON file.
    /// Returns an empty dictionary if the file is empty or contains no valid JSON objects.
    /// Nested objects are flattened using dot notation (e.g., "User.Profile.Name").
    /// </returns>
    /// <remarks>
    /// The JSON file can contain both flat key-value pairs and nested objects.
    /// Nested objects are automatically flattened using dot notation for keys.
    /// The method safely handles null values by converting them to empty strings.
    /// 
    /// Supported JSON formats:
    /// Flat structure:
    /// {
    ///   "WelcomeMessage": "Welcome!",
    ///   "GoodbyeMessage": "Goodbye!"
    /// }
    /// 
    /// Nested structure:
    /// {
    ///   "User": {
    ///     "Profile": {
    ///       "Name": "Hello, {0}!"
    ///     }
    ///   }
    /// }
    /// 
    /// Mixed structure:
    /// {
    ///   "WelcomeMessage": "Welcome!",
    ///   "User": {
    ///     "Settings": {
    ///       "Language": "English"
    ///     }
    ///   }
    /// }
    /// </remarks>
    /// <exception cref="JsonException">
    /// Thrown when the JSON content is malformed or cannot be parsed.
    /// </exception>
    private static Dictionary<string, string> LoadJsonFile(IFileInfo fileInfo)
    {
        using var stream = fileInfo.CreateReadStream();
        using var reader = new StreamReader(stream);
        var jsonContent = reader.ReadToEnd();

        var jsonDocument = JsonDocument.Parse(jsonContent);
        var resources = new Dictionary<string, string>();

        FlattenJsonElement(jsonDocument.RootElement, string.Empty, resources);

        return resources;
    }

    /// <summary>
    /// Recursively flattens a JSON element into key-value pairs using dot notation.
    /// </summary>
    /// <param name="element">The JSON element to flatten.</param>
    /// <param name="prefix">The current key prefix for nested elements.</param>
    /// <param name="resources">The dictionary to store the flattened key-value pairs.</param>
    /// <remarks>
    /// This method handles various JSON value types:
    /// - Objects: Recursively flattened with dot notation
    /// - Arrays: Indexed with square brackets (e.g., "Items[0]")
    /// - Strings: Added directly to the resources dictionary
    /// - Numbers, booleans: Converted to strings
    /// - Null values: Converted to empty strings
    /// </remarks>
    private static void FlattenJsonElement(JsonElement element, string prefix, Dictionary<string, string> resources)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    var key = string.IsNullOrEmpty(prefix) ? property.Name : $"{prefix}.{property.Name}";
                    FlattenJsonElement(property.Value, key, resources);
                }
                break;

            case JsonValueKind.Array:
                var arrayIndex = 0;
                foreach (var arrayElement in element.EnumerateArray())
                {
                    var key = $"{prefix}[{arrayIndex}]";
                    FlattenJsonElement(arrayElement, key, resources);
                    arrayIndex++;
                }
                break;

            case JsonValueKind.String:
                resources[prefix] = element.GetString() ?? string.Empty;
                break;

            case JsonValueKind.Number:
                resources[prefix] = element.GetRawText();
                break;

            case JsonValueKind.True:
            case JsonValueKind.False:
                resources[prefix] = element.GetBoolean().ToString().ToLowerInvariant();
                break;

            case JsonValueKind.Null:
                resources[prefix] = string.Empty;
                break;

            default:
                resources[prefix] = element.GetRawText();
                break;
        }
    }

    /// <summary>
    /// Clears all cached resources from memory.
    /// </summary>
    /// <remarks>
    /// This method removes all cached resource dictionaries, forcing subsequent
    /// calls to <see cref="LoadResources"/> to reload resources from disk.
    /// This can be useful during development or when resource files are updated
    /// at runtime and need to be reloaded.
    /// 
    /// Note: This operation is thread-safe but may impact performance temporarily
    /// as resources will need to be reloaded from disk.
    /// </remarks>
    public void ClearCache()
    {
        this._resourceCache.Clear();
    }
}