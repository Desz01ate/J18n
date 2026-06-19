using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace J18n.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class LocalizationAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
    [
        Diagnostics.MissingKey,
        Diagnostics.UnusedKey,
        Diagnostics.PartialMissingKey,
        Diagnostics.DuplicateKey,
    ];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    private static void OnCompilationStart(CompilationStartAnalysisContext context)
    {
        var additionalFiles =
            context.Options.AdditionalFiles;

        var config = LocalizationConfig.FromAnalyzerOptions(context.Options);
        var catalog = JsonKeyCatalog.FromAdditionalFiles(additionalFiles, config);
        var comparer = config.KeyCaseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;
        var usedKeys = new ConcurrentDictionary<string, byte>(comparer);

        context.RegisterOperationAction(operationContext =>
        {
            AnalyzeOperation(operationContext, config, catalog, usedKeys);
        }, OperationKind.Invocation, OperationKind.PropertyReference);

        context.RegisterCompilationEndAction(compilationContext =>
        {
            ReportUnusedKeys(compilationContext, catalog, usedKeys);
            ReportDuplicateKeys(compilationContext, catalog);
        });
    }

    private static void AnalyzeOperation(
        OperationAnalysisContext context,
        LocalizationConfig config,
        JsonKeyCatalog catalog,
        ConcurrentDictionary<string, byte> usedKeys)
    {
        if (!Utilities.IsLocalizationAccessor(context.Operation, config))
        {
            return;
        }

        var keyArgument = GetKeyArgument(context.Operation);

        if (keyArgument == null)
        {
            return;
        }

        // Roslyn broken as it'd not recognize that the key is non-nullable after the below check
        var key = Utilities.ExtractKeyFromArgument(keyArgument)!;

        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        // Track usage (thread-safe: ConcurrentDictionary.TryAdd is atomic)
        usedKeys.TryAdd(key, 0);

        var comparison = config.KeyCaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

        // Check if key exists in any culture
        if (!catalog.ContainsKey(key, comparison))
        {
            var location = Utilities.GetStringLiteralLocation(keyArgument);

            if (location == null)
            {
                return;
            }

            var diagnostic = Diagnostic.Create(
                Diagnostics.MissingKey,
                location,
                key);

            context.ReportDiagnostic(diagnostic);

            return;
        }

        // Check for partial missing keys across cultures
        // Use discovered cultures if no explicit configuration, and default WarnOnPartialMissing to true
        var effectiveCultures = catalog.GetEffectiveCultures(config.Cultures);
        var shouldWarnOnPartial = config.WarnOnPartialMissing || config.Cultures.Length == 0; // Default to true if no explicit config

        if (!shouldWarnOnPartial || effectiveCultures.Length <= 1)
        {
            return;
        }

        {
            var missingCultures = catalog.GetMissingCultures(key, effectiveCultures, comparison).ToArray();

            if (missingCultures.Length <= 0)
            {
                return;
            }

            var location = Utilities.GetStringLiteralLocation(keyArgument);

            if (location == null)
            {
                return;
            }

            var diagnostic = Diagnostic.Create(
                Diagnostics.PartialMissingKey,
                location,
                key,
                string.Join(", ", missingCultures));

            context.ReportDiagnostic(diagnostic);
        }
    }

    private static IOperation? GetKeyArgument(IOperation operation)
    {
        return operation switch
        {
            IInvocationOperation { Arguments.Length: > 0 } invocation => invocation.Arguments[0].Value,
            IPropertyReferenceOperation { Arguments.Length: > 0 } propertyRef => propertyRef.Arguments[0].Value,
            _ => null,
        };
    }

    private static void ReportUnusedKeys(
        CompilationAnalysisContext context,
        JsonKeyCatalog catalog,
        ConcurrentDictionary<string, byte> usedKeys)
    {
        foreach (var key in catalog.AllKeys)
        {
            // The ConcurrentDictionary is constructed with the correct comparer (Ordinal or OrdinalIgnoreCase),
            // so ContainsKey handles both case-sensitive and case-insensitive matching correctly.
            var isUsed = usedKeys.ContainsKey(key);

            if (isUsed)
            {
                continue;
            }

            // Report unused key at the JSON location if available
            var location = catalog.GetKeyLocation(key, catalog.KeysByCulture.Keys.FirstOrDefault() ?? "default");

            if (location == null)
            {
                continue;
            }

            var diagnostic = Diagnostic.Create(
                Diagnostics.UnusedKey,
                location,
                key);

            context.ReportDiagnostic(diagnostic);
        }
    }

    private static void ReportDuplicateKeys(CompilationAnalysisContext context, JsonKeyCatalog catalog)
    {
        foreach (var (key, culture, location) in catalog.DuplicateKeys)
        {
            var diagnostic = Diagnostic.Create(
                Diagnostics.DuplicateKey,
                location,
                key);

            context.ReportDiagnostic(diagnostic);
        }
    }
}