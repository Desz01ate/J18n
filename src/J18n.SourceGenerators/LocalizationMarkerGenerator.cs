using System.Linq;
using Microsoft.CodeAnalysis;

namespace J18n.SourceGenerators;

[Generator(LanguageNames.CSharp)]
public sealed class LocalizationMarkerGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Filter JSON files from additional texts
        var additionalJson =
            context.AdditionalTextsProvider
                   .Where(a => a.Path.EndsWith(".json", System.StringComparison.OrdinalIgnoreCase));

        // Get global analyzer config options  
        var globalOptions =
            context.AnalyzerConfigOptionsProvider
                   .Select((provider, _) => provider.GlobalOptions);

        // Combine additional texts with global options to create resource items
        var resourceItems = additionalJson
                            .Combine(globalOptions)
                            .Select((pair, _) => ResourceItem.TryCreate(pair.Left, pair.Right))
                            .Where(item => item is not null)
                            .Select((item, _) => item!);

        // Collect all items for processing
        var allItems = resourceItems.Collect();

        // Register source output for processing all items together (for deduplication and diagnostics)
        context.RegisterSourceOutput(allItems, static (context, items) =>
        {
            if (items.IsDefaultOrEmpty)
                return;

            // Group by key for deduplication
            var groups = items.GroupBy(item => new { item.Namespace, item.ClassName }).ToArray();
            var uniqueItems = groups.Select(g => g.First()).ToArray();

            foreach (var group in groups.Where(g => g.Count() > 1))
            {
                var duplicates = group.Skip(1).Select(item => item.AbsolutePath);
                var diagnostic = Diagnostic.Create(
                    DiagnosticDescriptors.DuplicateClassGenerated,
                    Location.None,
                    group.Key.ClassName,
                    group.Key.Namespace,
                    string.Join(", ", duplicates));

                context.ReportDiagnostic(diagnostic);
            }

            var summaryDiagnostic = Diagnostic.Create(
                DiagnosticDescriptors.ResourceProcessingSummary,
                Location.None,
                items.Length,
                uniqueItems.Length);

            context.ReportDiagnostic(summaryDiagnostic);

            // Generate source for unique items
            foreach (var item in uniqueItems)
            {
                var source = SourceEmitter.Emit(item);

                context.AddSource(item.HintName, source);
            }
        });
    }
}