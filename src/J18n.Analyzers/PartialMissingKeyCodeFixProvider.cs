using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace J18n.Analyzers;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(PartialMissingKeyCodeFixProvider))]
[Shared]
public class PartialMissingKeyCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
    [
        Diagnostics.PartialMissingKey.Id,
    ];

    public override FixAllProvider GetFixAllProvider()
    {
        return WellKnownFixAllProviders.BatchFixer;
    }

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        try
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            if (root == null)
            {
                return;
            }

            var diagnostic = context.Diagnostics.FirstOrDefault(d => d.Id == Diagnostics.PartialMissingKey.Id);

            if (diagnostic == null)
            {
                return;
            }

            // Find the string literal that contains the missing key
            var node = root.FindNode(diagnostic.Location.SourceSpan);
            var stringLiteral = node as LiteralExpressionSyntax ??
                                node.DescendantNodesAndSelf()
                                    .OfType<LiteralExpressionSyntax>()
                                    .FirstOrDefault(l => l.Token.IsKind(SyntaxKind.StringLiteralToken));

            if (stringLiteral == null)
            {
                return;
            }

            var missingKey = stringLiteral.Token.ValueText;

            if (string.IsNullOrWhiteSpace(missingKey))
            {
                return;
            }

            // Extract missing cultures from diagnostic message
            var missingCultures = ExtractMissingCulturesFromMessage(diagnostic.GetMessage());

            if (missingCultures.Length == 0)
            {
                return;
            }

            // Get the value from existing cultures to copy
            var catalog = GetJsonKeyCatalog(context.Document);

            if (catalog == null)
            {
                return;
            }

            var existingValue = GetExistingKeyValue(catalog, missingKey);

            // Create fix action for adding key to missing cultures
            var fixAction = CodeAction.Create(
                title: $"Add '{missingKey}' to missing cultures: {string.Join(", ", missingCultures)}",
                createChangedSolution: ct => AddKeyToMissingCulturesAsync(
                    context.Document, missingKey, missingCultures, existingValue, ct),
                equivalenceKey: "add_to_missing_cultures");

            context.RegisterCodeFix(fixAction, diagnostic);
        }
        catch (OperationCanceledException)
        {
            // Expected when operation is cancelled
        }
        catch
        {
            // Silently ignore other exceptions to prevent IDE crashes
        }
    }

    private static string[] ExtractMissingCulturesFromMessage(string message)
    {
        // Message format: "Localization key 'user.name' is missing in cultures: pl, th"
        var match = Regex.Match(message, @"missing in cultures:\s*(.+)$");

        if (!match.Success)
        {
            return [];
        }

        return match.Groups[1].Value
                    .Split(',')
                    .Select(c => c.Trim())
                    .Where(c => !string.IsNullOrWhiteSpace(c))
                    .ToArray();
    }

    private static string GetExistingKeyValue(JsonKeyCatalog catalog, string missingKey)
    {
        // Find the first culture that has this key and get its value
        foreach (var kvp in catalog.KeysByCulture)
        {
            if (kvp.Value.Contains(missingKey))
            {
                return $"TODO: Translate from {kvp.Key}";
            }
        }

        return "TODO: Add translation";
    }

    private static async Task<Solution> AddKeyToMissingCulturesAsync(
        Document document,
        string missingKey,
        string[] missingCultures,
        string value,
        CancellationToken cancellationToken)
    {
        return await JsonCodeFixHelper.ModifyJsonFilesAsync(
            document, missingKey, missingCultures, value, cancellationToken);
    }

    private static JsonKeyCatalog? GetJsonKeyCatalog(Document document)
    {
        return JsonCodeFixHelper.GetJsonKeyCatalog(document);
    }
}