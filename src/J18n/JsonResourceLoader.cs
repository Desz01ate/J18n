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
            var filePath = string.IsNullOrEmpty(this._resourcesPath) ? fileName : $"{this._resourcesPath}/{fileName}";

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
    /// </returns>
    /// <remarks>
    /// The JSON file should contain a single object where each property represents
    /// a localization key-value pair. The method safely handles null values by
    /// converting them to empty strings.
    /// 
    /// Expected JSON format:
    /// {
    ///   "WelcomeMessage": "Welcome!",
    ///   "GoodbyeMessage": "Goodbye!",
    ///   "ParameterizedMessage": "Hello, {0}!"
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

        foreach (var property in jsonDocument.RootElement.EnumerateObject())
        {
            resources[property.Name] = property.Value.GetString() ?? string.Empty;
        }

        return resources;
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