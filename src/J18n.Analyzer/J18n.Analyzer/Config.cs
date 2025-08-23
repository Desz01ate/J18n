using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace J18n.Analyzer;

public class LocalizationConfig
{
    public string[] JsonPatterns { get; }

    public AccessorKind[] AccessorKinds { get; }

    public string[] Cultures { get; }

    public bool KeyCaseSensitive { get; }

    public bool WarnOnPartialMissing { get; }

    public string[] AllowedDynamicPatterns { get; }

    private LocalizationConfig(
        string[] jsonPatterns,
        AccessorKind[] accessorKinds,
        string[] cultures,
        bool keyCaseSensitive,
        bool warnOnPartialMissing,
        string[] allowedDynamicPatterns)
    {
        this.JsonPatterns = jsonPatterns;
        this.AccessorKinds = accessorKinds;
        this.Cultures = cultures;
        this.KeyCaseSensitive = keyCaseSensitive;
        this.WarnOnPartialMissing = warnOnPartialMissing;
        this.AllowedDynamicPatterns = allowedDynamicPatterns;
    }

    public static LocalizationConfig FromAnalyzerOptions(AnalyzerOptions options, SyntaxTree? syntaxTree = null)
    {
        // FIXME: This method doesn't respect .editorconfig file and need to be fixed.
        var configOptions = syntaxTree != null
            ? options.AnalyzerConfigOptionsProvider.GetOptions(syntaxTree)
            : options.AnalyzerConfigOptionsProvider.GlobalOptions;

        var jsonPatterns =
            GetConfigValue(configOptions,
                    "localization_json_patterns",
                    "**/*.json")
                .Split([','], StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim()).ToArray();

        var accessorKinds =
            ParseAccessorKinds(
                GetConfigValue(configOptions,
                    "localization_accessor_kinds",
                    "Indexer:IStringLocalizer;Method:Localizer.Get,Translate"));

        var cultures =
            GetConfigValue(configOptions, "localization_cultures", "")
                .Split([','], StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim()).ToArray();

        var keyCaseSensitive =
            GetConfigValue(configOptions, "localization_key_case", "sensitive") == "sensitive";

        var warnOnPartialMissing =
            bool.Parse(GetConfigValue(configOptions, "localization_warn_on_partial_missing", "true"));

        var allowedDynamicPatterns =
            GetConfigValue(configOptions, "localization_allowed_dynamic_patterns", "")
                .Split([','], StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim()).ToArray();

        return new LocalizationConfig(
            jsonPatterns,
            accessorKinds,
            cultures,
            keyCaseSensitive,
            warnOnPartialMissing,
            allowedDynamicPatterns);
    }

    private static string GetConfigValue(AnalyzerConfigOptions options, string key, string defaultValue)
    {
        return options.TryGetValue(key, out var value) ? value : defaultValue;
    }

    private static AccessorKind[] ParseAccessorKinds(string accessorConfig)
    {
        var result = new List<AccessorKind>();

        var parts = accessorConfig.Split([';'], StringSplitOptions.RemoveEmptyEntries)
                                  .Select(s => s.Trim()).ToArray();

        foreach (var part in parts)
        {
            var colonIndex = part.IndexOf(':');
            if (colonIndex == -1) continue;

            var kindStr = part.Substring(0, colonIndex).Trim();
            var methodsStr = part.Substring(colonIndex + 1).Trim();

            if (Enum.TryParse<AccessorType>(kindStr, true, out var accessorType))
            {
                var methods = methodsStr.Split([','], StringSplitOptions.RemoveEmptyEntries)
                                        .Select(s => s.Trim()).ToArray();
                result.Add(new AccessorKind(accessorType, methods));
            }
        }

        return result.ToArray();
    }
}

public enum AccessorType
{
    Indexer,
    Method,
}

public class AccessorKind
{
    public AccessorType Type { get; }

    public string[] TypesOrMethods { get; }

    public AccessorKind(AccessorType type, string[] typesOrMethods)
    {
        this.Type = type;
        this.TypesOrMethods = typesOrMethods;
    }
}