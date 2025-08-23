using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace J18n.Analyzers;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(KeySuggestionCodeFixProvider))]
[Shared]
public class KeySuggestionCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
    [
        Diagnostics.MissingKey.Id,
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
            if (root == null) return;

            var diagnostic = context.Diagnostics.FirstOrDefault(d => d.Id == Diagnostics.MissingKey.Id);
            if (diagnostic == null) return;

            // Find the string literal that contains the missing key
            var node = root.FindNode(diagnostic.Location.SourceSpan);

            // Try to find a string literal in the node or its descendants
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

            // Get available keys from additional files
            var catalog = GetJsonKeyCatalog(context.Document);

            if (catalog == null)
            {
                return;
            }

            var config = GetLocalizationConfig(context.Document);
            var suggestedKeys = GetSimilarKeys(catalog, missingKey, config).Take(5).ToList();

            if (suggestedKeys.Count == 0)
            {
                foreach (var key in catalog.AllKeys.Take(3))
                {
                    var fallbackAction = CodeAction.Create(
                        title: $"Replace with '{key}' (existing key)",
                        createChangedDocument: ct => ReplaceKeyAsync(context.Document, stringLiteral, key, ct),
                        equivalenceKey: $"fallback_{key}");
                    context.RegisterCodeFix(fallbackAction, diagnostic);
                }
            }
            else
            {
                // Create code fix actions for each suggested key
                foreach (var suggestedKey in suggestedKeys)
                {
                    var action = CodeAction.Create(
                        title: $"Replace with '{suggestedKey}'",
                        createChangedDocument: ct => ReplaceKeyAsync(context.Document, stringLiteral, suggestedKey, ct),
                        equivalenceKey: suggestedKey);

                    context.RegisterCodeFix(action, diagnostic);
                }
            }

            // Offer to create the missing key in all JSON files
            var createKeyAction = CodeAction.Create(
                title: $"Create key '{missingKey}' in all localization files",
                createChangedSolution: ct => CreateKeyInAllFilesAsync(context.Document, missingKey, ct),
                equivalenceKey: "create_key");

            context.RegisterCodeFix(createKeyAction, diagnostic);
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

    private static async Task<Document> ReplaceKeyAsync(
        Document document,
        LiteralExpressionSyntax originalLiteral,
        string newKey,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root == null) return document;

        // Create new string literal with the suggested key
        var newLiteral = SyntaxFactory.LiteralExpression(
                                          SyntaxKind.StringLiteralExpression,
                                          SyntaxFactory.Literal(newKey))
                                      .WithTriviaFrom(originalLiteral);

        var newRoot = root.ReplaceNode(originalLiteral, newLiteral);
        return document.WithSyntaxRoot(newRoot);
    }

    private static async Task<Solution> CreateKeyInAllFilesAsync(
        Document document,
        string missingKey,
        CancellationToken cancellationToken)
    {
        return await JsonCodeFixHelper.ModifyJsonFilesAsync(
            document, missingKey, [], "TODO: Add translation", cancellationToken);
    }

    private static List<string> GetSimilarKeys(JsonKeyCatalog catalog, string missingKey, LocalizationConfig config)
    {
        var allKeys = catalog.AllKeys.ToList();

        var similarKeys = new List<(string key, int score)>();

        foreach (var key in allKeys)
        {
            var score = CalculateSimilarityScore(missingKey, key, config.KeyCaseSensitive);

            if (score > 0)
            {
                similarKeys.Add((key, score));
            }
        }

        return similarKeys
               .OrderByDescending(x => x.score)
               .Select(x => x.key)
               .ToList();
    }

    private static int CalculateSimilarityScore(string input, string candidate, bool caseSensitive)
    {
        var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

        // Exact match (shouldn't happen for missing keys, but just in case)
        if (string.Equals(input, candidate, comparison))
        {
            return 1000;
        }

        // Contains the input
        if (candidate.IndexOf(input, comparison) >= 0)
        {
            return 800;
        }

        // Input contains the candidate
        if (input.IndexOf(candidate, comparison) >= 0)
        {
            return 700;
        }

        // Starts with same prefix
        var commonPrefix = GetCommonPrefixLength(input, candidate, comparison);

        if (commonPrefix > 0)
        {
            return 500 + commonPrefix * 10;
        }

        // Levenshtein distance for typos
        var distance = Utilities.LevenshteinDistance(
            caseSensitive ? input : input.ToLowerInvariant(),
            caseSensitive ? candidate : candidate.ToLowerInvariant());

        if (distance <= Math.Max(input.Length, candidate.Length) / 3) // Allow up to 1/3 character differences
        {
            return 300 - distance * 10;
        }

        return 0;
    }

    private static int GetCommonPrefixLength(string a, string b, StringComparison comparison)
    {
        var length = Math.Min(a.Length, b.Length);

        for (var i = 0; i < length; i++)
        {
            if (!string.Equals(a[i].ToString(), b[i].ToString(), comparison))
            {
                return i;
            }
        }

        return length;
    }

    private static JsonKeyCatalog? GetJsonKeyCatalog(Document document)
    {
        return JsonCodeFixHelper.GetJsonKeyCatalog(document);
    }

    private static LocalizationConfig GetLocalizationConfig(Document document)
    {
        return JsonCodeFixHelper.GetLocalizationConfig(document);
    }
}