using System.Collections.Immutable;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace J18n.Analyzers;

public partial class JsonKeyCatalog
{
    private readonly Dictionary<string, HashSet<string>> _keysByCulture = new();
    private readonly Dictionary<(string key, string culture), List<Location>> _keyToLocations = new();
    private readonly HashSet<string> _allKeys = [];
    private string[] _discoveredCultures = [];

    public IReadOnlyDictionary<string, HashSet<string>> KeysByCulture => this._keysByCulture;

    public IReadOnlyCollection<string> AllKeys => this._allKeys;

    public string[] DiscoveredCultures => this._discoveredCultures;

    public string[] GetEffectiveCultures(string[] configuredCultures)
    {
        // If cultures are explicitly configured, use them
        if (configuredCultures.Length > 0)
        {
            return configuredCultures;
        }

        // Otherwise, use discovered cultures, but filter out "default" if we have real cultures
        var realCultures = this._discoveredCultures.Where(c => c != "default").ToArray();
        return realCultures.Length > 0 ? realCultures : this._discoveredCultures;
    }

    public static JsonKeyCatalog FromAdditionalFiles(ImmutableArray<AdditionalText> additionalFiles,
        LocalizationConfig config)
    {
        var catalog = new JsonKeyCatalog();

        // First pass: discover all cultures from file paths if none configured
        var discoveredCultures = new HashSet<string>();
        var jsonFiles = additionalFiles.Where(file => ShouldProcessFile(file.Path, config.JsonPatterns)).ToList();

        foreach (var file in jsonFiles)
        {
            var culture = InferCultureFromPath(file.Path, config.Cultures);
            discoveredCultures.Add(culture);
        }

        // Update the catalog with discovered cultures
        catalog._discoveredCultures = discoveredCultures.ToArray();

        foreach (var file in jsonFiles)
        {
            catalog.ProcessJsonFile(file, config);
        }

        return catalog;
    }

    private static bool ShouldProcessFile(string filePath, string[] patterns)
    {
        var fileName = Path.GetFileName(filePath);
        var extension = Path.GetExtension(filePath);

        if (!string.Equals(extension, ".json", StringComparison.OrdinalIgnoreCase))
            return false;

        // For now, simple pattern matching - in a full implementation would use proper glob matching
        foreach (var pattern in patterns)
        {
            // Handle common patterns
            if (pattern == "**/*.json") // Match all JSON files
                return true;
            if (pattern.Contains("**") && fileName.EndsWith(".json"))
                return true;
            if (pattern.Contains("*.json") && fileName.EndsWith(".json"))
                return true;
            if (pattern.Contains("Resources") && filePath.Contains("Resources") && fileName.EndsWith(".json"))
                return true;
        }

        return false;
    }

    private void ProcessJsonFile(AdditionalText file, LocalizationConfig config)
    {
        var sourceText = file.GetText();
        if (sourceText == null) return;

        var culture = InferCultureFromPath(file.Path, config.Cultures);
        var content = sourceText.ToString();

        try
        {
            using var document = JsonDocument.Parse(content);
            var keys = ExtractKeysFromJsonElement(document.RootElement, "");

            var duplicateChecker =
                new HashSet<string>(config.KeyCaseSensitive
                    ? StringComparer.Ordinal
                    : StringComparer.OrdinalIgnoreCase);

            foreach (var key in keys)
            {
                if (!duplicateChecker.Add(key))
                {
                    // This means we have a duplicate flattened key (e.g., "user.name" appears twice)
                    this.ReportDuplicateKey(file, key, culture);
                    continue;
                }

                this._allKeys.Add(key);

                if (!this._keysByCulture.ContainsKey(culture))
                {
                    this._keysByCulture[culture] = new HashSet<string>(config.KeyCaseSensitive
                        ? StringComparer.Ordinal
                        : StringComparer.OrdinalIgnoreCase);
                }

                this._keysByCulture[culture].Add(key);

                // For location tracking, create a location pointing to the file
                // Use the first line of the file as the location for unused key reporting
                var textSpan = new TextSpan(0, Math.Min(100, sourceText.Length));
                var location = Location.Create(file.Path, textSpan, sourceText.Lines.GetLinePositionSpan(textSpan));

                var locationKey = (key, culture);

                if (!this._keyToLocations.ContainsKey(locationKey))
                {
                    this._keyToLocations[locationKey] = [];
                }

                this._keyToLocations[locationKey].Add(location);
            }
        }
        catch (JsonException)
        {
            // Invalid JSON - skip this file
        }
    }

    private readonly List<(string key, string culture, Location location)> _duplicateKeys = [];

    public IReadOnlyCollection<(string key, string culture, Location location)> DuplicateKeys => this._duplicateKeys;


    private void ReportDuplicateKey(AdditionalText file, string key, string culture, Location? location = null)
    {
        if (location == null)
        {
            var sourceText = file.GetText();

            if (sourceText != null)
            {
                var textSpan = new TextSpan(0, Math.Min(100, sourceText.Length));
                location = Location.Create(file.Path, textSpan, sourceText.Lines.GetLinePositionSpan(textSpan));
            }
        }

        if (location != null)
        {
            this._duplicateKeys.Add((key, culture, location));
        }
    }

    private static string InferCultureFromPath(string filePath, string[] configuredCultures)
    {
        // Step 1: Extract the filename culture suffix (last dot-segment of the name without extension).
        var fileName = Path.GetFileNameWithoutExtension(filePath);
        var parts = fileName.Split('.');

        if (parts.Length > 1)
        {
            var lastPart = parts[parts.Length - 1];

            // Valid culture token: 2 letters or 2-letter-dash-2-letter (e.g. "en", "en-US").
            if (Regex.IsMatch(lastPart, "^[a-zA-Z]{2}(-[a-zA-Z]{2})?$", RegexOptions.Compiled))
            {
                if (configuredCultures.Length > 0)
                {
                    // Configured cultures: return only if the suffix exactly matches one.
                    foreach (var culture in configuredCultures)
                    {
                        if (string.Equals(lastPart, culture, StringComparison.OrdinalIgnoreCase))
                        {
                            return culture;
                        }
                    }
                    // Suffix doesn't match any configured culture — fall through to directory check.
                }
                else
                {
                    // No configured cultures: return the candidate lowercased.
                    return lastPart.ToLowerInvariant();
                }
            }
        }

        // Step 2: Check directory segments for an exact culture token match.
        var directory = Path.GetDirectoryName(filePath) ?? "";
        var segments = directory.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);

        if (configuredCultures.Length > 0)
        {
            // When configured: exact-match any segment against the configured list.
            foreach (var segment in segments)
            {
                foreach (var culture in configuredCultures)
                {
                    if (string.Equals(segment, culture, StringComparison.OrdinalIgnoreCase))
                    {
                        return culture;
                    }
                }
            }
        }
        else
        {
            // When not configured: exact-match against known common culture tokens only.
            var commonCultures = new[]
            {
                "en", "en-US", "fr", "de", "es", "it", "ja", "zh", "pt", "ru", "th", "ko", "ar", "hi", "tr", "pl", "nl",
                "sv", "da", "no", "fi", "cs", "sk", "hu", "ro", "bg", "hr", "sr", "sl", "et",
                "lv", "lt", "uk", "be", "mk", "sq", "az", "ka", "am", "is", "fo", "mt", "cy", "eu", "ca", "gl", "ast",
                "br", "co", "fur", "rm", "sc", "vec", "lij", "pms", "nap", "scn",
            };

            foreach (var segment in segments)
            {
                foreach (var culture in commonCultures)
                {
                    if (string.Equals(segment, culture, StringComparison.OrdinalIgnoreCase))
                    {
                        return culture;
                    }
                }
            }
        }

        // Step 3: No culture could be determined.
        return "default";
    }

    private static IEnumerable<string> ExtractKeysFromJsonElement(JsonElement element, string prefix)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    var currentKey = string.IsNullOrEmpty(prefix) ? property.Name : $"{prefix}.{property.Name}";

                    foreach (var nestedKey in ExtractKeysFromJsonElement(property.Value, currentKey))
                    {
                        yield return nestedKey;
                    }
                }

                break;

            case JsonValueKind.Array:
                var index = 0;
                foreach (var arrayElement in element.EnumerateArray())
                {
                    var currentKey = $"{prefix}[{index}]";

                    foreach (var nestedKey in ExtractKeysFromJsonElement(arrayElement, currentKey))
                    {
                        yield return nestedKey;
                    }

                    index++;
                }

                break;

            default:
                // String, Number, True, False, Null — yield the current path as a leaf key.
                yield return prefix;
                break;
        }
    }

    public bool ContainsKey(string key, StringComparison comparison = StringComparison.Ordinal)
    {
        if (comparison == StringComparison.OrdinalIgnoreCase)
        {
            return this._allKeys.Any(k => string.Equals(k, key, comparison));
        }

        return this._allKeys.Contains(key);
    }

    public IEnumerable<string> GetMissingCultures(string key, string[] requiredCultures, StringComparison comparison)
    {
        // A key present in the culture-agnostic neutral ("default") file is merged by the
        // runtime into every culture's resolution as a shared base layer (C2 change). Such a
        // key resolves in ALL cultures at runtime, so it must not be flagged as partially
        // missing in any of them. Return an empty list to suppress LOC003.
        //
        // IMPORTANT: This only applies to the culture-agnostic neutral set ("default").
        // The "en" fallback (or any other per-culture file) does NOT suppress LOC003 —
        // a key in "en" but not "fr" is a genuine missing "fr" translation.
        if (this._keysByCulture.TryGetValue("default", out var defaultKeys))
        {
            var inDefault = comparison == StringComparison.OrdinalIgnoreCase
                ? defaultKeys.Any(k => string.Equals(k, key, comparison))
                : defaultKeys.Contains(key);

            if (inDefault)
            {
                return [];
            }
        }

        var missingCultures = new List<string>();

        foreach (var culture in requiredCultures)
        {
            if (!this._keysByCulture.ContainsKey(culture))
            {
                missingCultures.Add(culture);
                continue;
            }

            var found = comparison == StringComparison.OrdinalIgnoreCase
                ? this._keysByCulture[culture].Any(k => string.Equals(k, key, comparison))
                : this._keysByCulture[culture].Contains(key);

            if (!found)
            {
                missingCultures.Add(culture);
            }
        }

        return missingCultures;
    }

    public Location? GetKeyLocation(string key, string culture)
    {
        var locationKey = (key, culture);

        if (this._keyToLocations.TryGetValue(locationKey, out var locations))
        {
            return locations.FirstOrDefault();
        }

        // If not found for specific culture, try any culture for this key
        foreach (var kvp in this._keyToLocations)
        {
            if (kvp.Key.key.Equals(key, StringComparison.OrdinalIgnoreCase))
            {
                return kvp.Value.FirstOrDefault();
            }
        }

        return null;
    }
}