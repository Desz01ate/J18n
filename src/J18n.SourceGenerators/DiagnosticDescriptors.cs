using Microsoft.CodeAnalysis;

namespace J18n.SourceGenerators;

internal static class DiagnosticDescriptors
{
    public static readonly DiagnosticDescriptor MissingRootNamespace = new(
        id: "LMG001",
        title: "Missing RootNamespace property",
        messageFormat: "RootNamespace build property is not available, using 'Resources' as root namespace",
        category: "LocalizationMarkerGenerator",
        DiagnosticSeverity.Info,
        isEnabledByDefault: false,
        description: "The RootNamespace build property was not found, generated classes will be in the 'Resources' namespace.");

    public static readonly DiagnosticDescriptor InvalidBaseName = new(
        id: "LMG002", 
        title: "Invalid or empty base name",
        messageFormat: "Resource file '{0}' has invalid or empty base name after culture suffix removal, skipping",
        category: "LocalizationMarkerGenerator",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Resource files must have valid base names after removing culture suffixes to generate marker classes.");

    public static readonly DiagnosticDescriptor DuplicateClassGenerated = new(
        id: "LMG003",
        title: "Duplicate localization marker class",
        messageFormat: "Multiple resource files map to the same marker class '{0}' in namespace '{1}'. Duplicates: {2}",
        category: "LocalizationMarkerGenerator", 
        DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "Multiple resource files with different culture suffixes map to the same marker class. Only the first occurrence will be generated.");

    public static readonly DiagnosticDescriptor ResourceProcessingSummary = new(
        id: "LMG004",
        title: "Resource processing summary",
        messageFormat: "Processed {0} resource files, generated {1} localization marker classes",
        category: "LocalizationMarkerGenerator",
        DiagnosticSeverity.Info,
        isEnabledByDefault: false,
        description: "Summary of localization resource processing for troubleshooting.");
}