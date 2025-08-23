using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace J18n.Analyzer;

public class JsonKeyCatalog
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

    public static JsonKeyCatalog FromAdditionalFiles(ImmutableArray<AdditionalText> additionalFiles, LocalizationConfig config)
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
            // First check for raw duplicate keys in JSON content (before parsing)
            this.CheckForRawDuplicateKeys(file, content, culture);

            using var document = JsonDocument.Parse(content);
            var keys = ExtractKeysFromJsonElement(document.RootElement, "");

            var duplicateChecker = new HashSet<string>(config.KeyCaseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase);

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
                    this._keysByCulture[culture] = new HashSet<string>(config.KeyCaseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase);
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

    private void CheckForRawDuplicateKeys(AdditionalText file, string content, string culture)
    {
        // This is a simplified check - in a full implementation, we'd need proper JSON parsing
        // to handle complex cases like nested objects with same keys at different levels
        var lines = content.Split('\n');
        var seenKeys = new HashSet<string>();

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();

            if (!line.Contains(':') || !line.Contains('"'))
            {
                continue;
            }

            // Extract potential key from line like: "keyName": "value"
            var colonIndex = line.IndexOf(':');
            var beforeColon = line.Substring(0, colonIndex).Trim();

            if (!beforeColon.StartsWith("\"") || !beforeColon.EndsWith("\""))
            {
                continue;
            }

            var key = beforeColon.Trim('"');

            if (seenKeys.Add(key))
            {
                continue;
            }

            // Found duplicate key at raw JSON level
            var sourceText = file.GetText();

            if (sourceText == null)
            {
                continue;
            }

            var textSpan = TextSpan.FromBounds(0, Math.Min(line.Length, sourceText.Length));
            var location = Location.Create(file.Path, textSpan, sourceText.Lines.GetLinePositionSpan(textSpan));

            this.ReportDuplicateKey(file, key, culture, location);
        }
    }

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
        var fileName = Path.GetFileNameWithoutExtension(filePath);
        var directory = Path.GetDirectoryName(filePath) ?? "";

        // Check if configured cultures are in the path
        foreach (var culture in configuredCultures)
        {
            if (directory.IndexOf(culture, StringComparison.OrdinalIgnoreCase) >= 0 ||
                fileName.IndexOf(culture, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return culture;
            }
        }

        // Extract culture from filename patterns like "Program.en.json", "messages.th.json", etc.
        var parts = fileName.Split('.');

        if (parts.Length > 1)
        {
            // Take the last part before the extension as potential culture code
            var lastPart = parts[parts.Length - 1];

            // Check if it looks like a culture code (2-5 letters, possibly with dash)
            if (lastPart.Length >= 2 && lastPart.Length <= 5 &&
                System.Text.RegularExpressions.Regex.IsMatch(lastPart, @"^[a-zA-Z]{2}(-[a-zA-Z]{2})?$"))
            {
                return lastPart.ToLowerInvariant();
            }
        }

        // Check for common culture patterns in directory or filename
        var commonCultures = new[]
        {
            "en", "en-US", "fr", "de", "es", "it", "ja", "zh", "pt", "ru", "th", "ko", "ar", "hi", "tr", "pl", "nl", "sv", "da", "no", "fi", "cs", "sk", "hu", "ro", "bg", "hr", "sr", "sl", "et",
            "lv", "lt", "uk", "be", "mk", "sq", "az", "ka", "am", "is", "fo", "mt", "cy", "eu", "ca", "gl", "ast", "br", "co", "fur", "rm", "sc", "vec", "lij", "pms", "nap", "scn",
        };

        foreach (var culture in commonCultures)
        {
            if (directory.IndexOf(culture, StringComparison.OrdinalIgnoreCase) >= 0 ||
                fileName.IndexOf(culture, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return culture;
            }
        }

        // Default culture
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

                    if (property.Value.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var nestedKey in ExtractKeysFromJsonElement(property.Value, currentKey))
                        {
                            yield return nestedKey;
                        }
                    }
                    else
                    {
                        yield return currentKey;
                    }
                }

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