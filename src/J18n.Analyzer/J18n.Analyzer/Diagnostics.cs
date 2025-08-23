using Microsoft.CodeAnalysis;

namespace J18n.Analyzer;

public static class Diagnostics
{
    private const string Category = "J18n.Localization";

    public readonly static DiagnosticDescriptor MissingKey = new(
        id: "LOC001",
        title: "Missing localization key",
        messageFormat: "Localization key '{0}' is not found in any configured culture",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The referenced localization key does not exist in any of the configured JSON localization files.");

    public readonly static DiagnosticDescriptor UnusedKey = new(
        id: "LOC002", 
        title: "Unused localization key",
        messageFormat: "Localization key '{0}' is defined but never used",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "The localization key is defined in JSON files but is not referenced anywhere in the code.",
        customTags: [WellKnownDiagnosticTags.CompilationEnd]);

    public readonly static DiagnosticDescriptor PartialMissingKey = new(
        id: "LOC003",
        title: "Localization key missing in some cultures", 
        messageFormat: "Localization key '{0}' is missing in cultures: {1}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "The localization key exists in some cultures but is missing in others.");

    public readonly static DiagnosticDescriptor DuplicateKey = new(
        id: "LOC004",
        title: "Duplicate localization key",
        messageFormat: "Localization key '{0}' is defined multiple times in the same file",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The same localization key appears multiple times within a single JSON file.",
        customTags: [WellKnownDiagnosticTags.CompilationEnd]);
}