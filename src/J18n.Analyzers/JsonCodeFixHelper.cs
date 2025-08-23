using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace J18n.Analyzers;

public static class JsonCodeFixHelper
{
    public static JsonKeyCatalog? GetJsonKeyCatalog(Document document)
    {
        var project = document.Project;
        var additionalFiles = project.AdditionalDocuments
                                     .Select(doc => new AdditionalTextWrapper(doc))
                                     .ToImmutableArray<AdditionalText>();

        if (additionalFiles.IsEmpty)
        {
            return null;
        }

        var config = GetLocalizationConfig(document);
        var catalog = JsonKeyCatalog.FromAdditionalFiles(additionalFiles, config);
        return catalog;
    }

    public static LocalizationConfig GetLocalizationConfig(Document document)
    {
        var analyzerOptions = document.Project.AnalyzerOptions;
        return LocalizationConfig.FromAnalyzerOptions(analyzerOptions);
    }

    public static bool ShouldProcessFile(string filePath, string[] patterns)
    {
        var fileName = Path.GetFileName(filePath);
        var extension = Path.GetExtension(filePath);

        if (!string.Equals(extension, ".json", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Simple pattern matching
        foreach (var pattern in patterns)
        {
            if (pattern == "**/*.json") // Match all JSON files
            {
                return true;
            }

            if (pattern.Contains("**") && fileName.EndsWith(".json"))
            {
                return true;
            }

            if (pattern.Contains("*.json") && fileName.EndsWith(".json"))
            {
                return true;
            }

            if (pattern.Contains("Resources") && filePath.Contains("Resources") && fileName.EndsWith(".json"))
            {
                return true;
            }
        }

        return false;
    }

    public static string InferCultureFromPath(string filePath, string[] configuredCultures)
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
            if (lastPart.Length is >= 2 and <= 5 &&
                Regex.IsMatch(lastPart, @"^[a-zA-Z]{2}(-[a-zA-Z]{2})?$"))
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

    public static string AddKeyToJsonContent(string jsonContent, string keyPath, string value = "TODO: Add translation")
    {
        try
        {
            using var document = JsonDocument.Parse(jsonContent);
            var rootObject = document.RootElement.Clone();

            // Convert to mutable dictionary for easier manipulation
            var jsonDict = JsonElementToDictionary(rootObject);

            // Add the key with the specified value
            AddNestedKey(jsonDict, keyPath, value);

            // Convert back to JSON string with proper formatting
            return JsonSerializer.Serialize(jsonDict, new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            });
        }
        catch
        {
            // If parsing fails, append to the end (simple fallback)
            var trimmed = jsonContent.TrimEnd();

            // Return unchanged if we can't parse
            if (!trimmed.EndsWith("}"))
            {
                return jsonContent;
            }

            var lastBrace = trimmed.LastIndexOf('}');
            var beforeBrace = trimmed.Substring(0, lastBrace);
            var afterBrace = trimmed.Substring(lastBrace);

            // Add comma if there's existing content
            var comma = beforeBrace.TrimEnd().EndsWith("{") ? "" : ",";
            return $"{beforeBrace}{comma}\n  \"{keyPath}\": \"{value}\"\n{afterBrace}";
        }
    }

    public static Dictionary<string, object?> JsonElementToDictionary(JsonElement element)
    {
        var dict = new Dictionary<string, object?>();

        foreach (var property in element.EnumerateObject())
        {
            dict[property.Name] = property.Value.ValueKind switch
            {
                JsonValueKind.Object => JsonElementToDictionary(property.Value),
                JsonValueKind.Array => property.Value.EnumerateArray().Select(JsonElementToObject).ToArray(),
                JsonValueKind.String => property.Value.GetString() ?? "",
                JsonValueKind.Number => property.Value.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null,
                _ => property.Value.ToString(),
            };
        }

        return dict;
    }

    public static object? JsonElementToObject(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object => JsonElementToDictionary(element),
            JsonValueKind.Array => element.EnumerateArray().Select(JsonElementToObject).ToArray(),
            JsonValueKind.String => element.GetString() ?? "",
            JsonValueKind.Number => element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => element.ToString(),
        };
    }

    public static void AddNestedKey(Dictionary<string, object?> dict, string keyPath, string value)
    {
        var parts = keyPath.Split('.');
        var current = dict;

        // Navigate/create the nested structure
        for (var i = 0; i < parts.Length - 1; i++)
        {
            var part = parts[i];

            if (!current.ContainsKey(part))
            {
                current[part] = new Dictionary<string, object?>();
            }

            if (current[part] is Dictionary<string, object?> nestedDict)
            {
                current = nestedDict;
            }
            else
            {
                // If the path conflicts with existing non-object value, 
                // we'll overwrite it with a nested object
                var temp = new Dictionary<string, object?>();
                current[part] = temp;
                current = temp;
            }
        }

        // Set the final value
        current[parts[parts.Length - 1]] = value;
    }

    public static async Task<Solution> ModifyJsonFilesAsync(
        Document document,
        string missingKey,
        string[] targetCultures,
        string value,
        CancellationToken cancellationToken)
    {
        var solution = document.Project.Solution;
        var config = GetLocalizationConfig(document);

        // Get all additional JSON files
        var jsonFiles = document.Project.AdditionalDocuments
                                .Where(doc => ShouldProcessFile(doc.FilePath ?? doc.Name, config.JsonPatterns))
                                .ToList();

        foreach (var jsonFile in jsonFiles)
        {
            try
            {
                var sourceText = await jsonFile.GetTextAsync(cancellationToken).ConfigureAwait(false);

                if (sourceText == null)
                {
                    continue;
                }

                // If targetCultures is provided, only modify files for those cultures
                if (targetCultures.Length > 0)
                {
                    var fileCulture = InferCultureFromPath(jsonFile.FilePath ?? jsonFile.Name, config.Cultures);

                    if (!targetCultures.Contains(fileCulture))
                    {
                        continue;
                    }
                }

                var updatedText = AddKeyToJsonContent(sourceText.ToString(), missingKey, value);
                var newSourceText = SourceText.From(updatedText);

                solution = solution.WithAdditionalDocumentText(jsonFile.Id, newSourceText);
            }
            catch
            {
                // Skip files that can't be processed
            }
        }

        return solution;
    }

    private class AdditionalTextWrapper(TextDocument document) : AdditionalText
    {
        public override string Path => document.FilePath ?? document.Name;

        public override SourceText? GetText(CancellationToken cancellationToken = default)
        {
            return document.GetTextAsync(cancellationToken).Result;
        }
    }
}