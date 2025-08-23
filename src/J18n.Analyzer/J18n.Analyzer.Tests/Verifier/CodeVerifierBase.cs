namespace J18n.Analyzer.Tests.Verifier;

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using NUnit.Framework;

public record struct AdditionalFile(string Path, string Content);

/// <summary>
/// Base class for testing code fix providers
/// Provides utilities for creating projects, running analyzers, and verifying code fixes.
/// </summary>
public abstract class CodeVerifierBase
{
    private readonly static MetadataReference CorlibReference = MetadataReference.CreateFromFile(typeof(object).Assembly.Location);
    private readonly static MetadataReference SystemCoreReference = MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location);
    private readonly static MetadataReference CSharpSymbolsReference = MetadataReference.CreateFromFile(typeof(CSharpCompilation).Assembly.Location);
    private readonly static MetadataReference CodeAnalysisReference = MetadataReference.CreateFromFile(typeof(Compilation).Assembly.Location);

    private readonly static MetadataReference LocalizationAbstractionsReference =
        MetadataReference.CreateFromFile(typeof(Microsoft.Extensions.Localization.IStringLocalizer).Assembly.Location);

    /// <summary>
    /// Get the analyzer to be tested.
    /// </summary>
    protected abstract DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer();

    /// <summary>
    /// Get the code fix provider to be tested.
    /// </summary>
    protected abstract CodeFixProvider? GetCSharpCodeFixProvider();

    /// <summary>
    /// Get the project to be tested.
    /// </summary>
    /// <returns></returns>
    protected abstract Project GetProject();

    /// <summary>
    /// Create a project using the inputted string as a source file.
    /// </summary>
    protected static Project CreateProject(string source, params AdditionalFile[] additionalFiles)
    {
        var projectId = ProjectId.CreateNewId(debugName: "TestProject");

        var solution = new AdhocWorkspace()
                       .CurrentSolution
                       .AddProject(projectId, "TestProject", "TestProject", LanguageNames.CSharp)
                       .AddMetadataReference(projectId, CorlibReference)
                       .AddMetadataReference(projectId, SystemCoreReference)
                       .AddMetadataReference(projectId, CSharpSymbolsReference)
                       .AddMetadataReference(projectId, CodeAnalysisReference)
                       .AddMetadataReference(projectId, LocalizationAbstractionsReference);

        var documentId = DocumentId.CreateNewId(projectId);
        solution = solution.AddDocument(documentId, "Test0.cs", SourceText.From(source));

        // Add additional files (e.g., JSON localization files).
        // These are added to the project, and later explicitly forwarded to the analyzer via AnalyzerOptions.
        foreach (var (filename, content) in additionalFiles)
        {
            var additionalDocumentId = DocumentId.CreateNewId(projectId);
            solution = solution.AddAdditionalDocument(additionalDocumentId, filename, SourceText.From(content));
        }

        return solution.GetProject(projectId)!;
    }

    private sealed class InMemoryAdditionalText : AdditionalText
    {
        private readonly SourceText _text;

        public InMemoryAdditionalText(string path, SourceText text)
        {
            this.Path = path;
            this._text = text;
        }

        public override string Path { get; }

        public override SourceText GetText(CancellationToken cancellationToken = default)
        {
            return this._text;
        }
    }

    private static async Task<CompilationWithAnalyzers> CreateCompilationWithAnalyzersAsync(Project project, DiagnosticAnalyzer analyzer)
    {
        var compilation = await project.GetCompilationAsync();

        // Bridge project.AdditionalDocuments -> AnalyzerOptions.AdditionalFiles so the analyzer can see them.
        var additionalFiles = new List<AdditionalText>();

        foreach (var doc in project.AdditionalDocuments)
        {
            var text = await doc.GetTextAsync();
            additionalFiles.Add(new InMemoryAdditionalText(doc.Name, text));
        }

        var analyzerOptions = new AnalyzerOptions([..additionalFiles]);

        var cwaOptions = new CompilationWithAnalyzersOptions(
            analyzerOptions,
            onAnalyzerException: null,
            concurrentAnalysis: true,
            logAnalyzerExecutionTime: false,
            reportSuppressedDiagnostics: false);

        return compilation!.WithAnalyzers([analyzer], cwaOptions);
    }

    /// <summary>
    /// Called to test a C# DiagnosticAnalyzer when applied on the single inputted string as a source.
    /// </summary>
    private async Task<Diagnostic[]> GetSortedDiagnosticsFromDocuments()
    {
        var project = this.GetProject();
        var analyzer = this.GetCSharpDiagnosticAnalyzer();
        var compilationWithAnalyzers = await CreateCompilationWithAnalyzersAsync(project, analyzer);
        var diagnostics = await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync();

        return diagnostics.OrderBy(d => d.Location.SourceSpan.Start).ToArray();
    }

    /// <summary>
    /// Helper method to verify the expected diagnostic results.
    /// </summary>
    private static void VerifyDiagnosticResults(Diagnostic[] actualResults, params DiagnosticResult[] expectedResults)
    {
        foreach (var expected in expectedResults)
        {
            var actuals = actualResults.Where(d => d.Id == expected.Id);

            foreach (var actual in actuals)
            {
                var actualSpan = actual.Location.GetLineSpan();
                var actualLine = actualSpan.StartLinePosition.Line + 1;
                var actualColumn = actualSpan.StartLinePosition.Character + 1;

                Assert.Multiple(() =>
                {
                    Assert.That(actualLine, Is.EqualTo(expected.Line),
                        $"Expected diagnostic to be on line \"{expected.Line}\" was actually on line \"{actualLine}\"");

                    Assert.That(actualColumn, Is.EqualTo(expected.Column),
                        $"Expected diagnostic to start at column \"{expected.Column}\" was actually at column \"{actualColumn}\"");
                });

                // If the assertion above completes successfully, we don't need to continue checking the rest of the diagnostics
                break;
            }
        }
    }

    /// <summary>
    /// Test a C# code fix by running the analyzer and code fix provider.
    /// </summary>
    public async Task VerifyCodeFix(string fixedSource, int? codeFixIndex = null)
    {
        await this.VerifyCodeFixCore(fixedSource, codeFixIndex, null);
    }

    /// <summary>
    /// Test a code fix that modifies additional files (like JSON files).
    /// </summary>
    public async Task VerifyAdditionalFileCodeFix(string fileName, string expectedContent, int? codeFixIndex = null)
    {
        await this.VerifyCodeFixCore(expectedContent, codeFixIndex, fileName);
    }

    private async Task VerifyCodeFixCore(string expectedContent, int? codeFixIndex, string? fileNameToVerify)
    {
        var project = this.GetProject();
        var document = project.Documents.First();

        var analyzer = this.GetCSharpDiagnosticAnalyzer();
        var codeFixProvider = this.GetCSharpCodeFixProvider();

        if (codeFixProvider is null)
        {
            Assert.Inconclusive("No code fix provider was provided");

            return;
        }

        // Get diagnostics
        var compilationWithAnalyzers = await CreateCompilationWithAnalyzersAsync(project, analyzer);
        var diagnostics = await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync();

        Assert.That(diagnostics, Is.Not.Empty, "No diagnostics found");

        // Get code fixes
        var actions = new List<CodeAction>();
        var context = new CodeFixContext(document, diagnostics.First(), (a, d) => actions.Add(a), CancellationToken.None);
        await codeFixProvider.RegisterCodeFixesAsync(context);

        Assert.That(actions, Is.Not.Empty, "No code fixes provided");

        // Apply the code fix
        var actionToApply = actions[codeFixIndex ?? 0];
        var operations = await actionToApply.GetOperationsAsync(CancellationToken.None);
        var solution = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution;

        string actualContent;

        if (fileNameToVerify != null)
        {
            // Verify additional file content
            var additionalDoc = solution.Projects.SelectMany(p => p.AdditionalDocuments).FirstOrDefault(d => d.Name == fileNameToVerify);

            Assert.That(additionalDoc, Is.Not.Null, $"Additional file '{fileNameToVerify}' not found after code fix");

            var additionalText = await additionalDoc!.GetTextAsync();

            actualContent = additionalText.ToString();
        }
        else
        {
            // Verify C# source file content
            var newDocument = solution.GetDocument(document.Id);

            var newSource = await newDocument!.GetTextAsync();

            actualContent = newSource.ToString();
        }

        Assert.That(actualContent, Is.EqualTo(expectedContent),
            fileNameToVerify != null
                ? $"Code fix did not produce the expected result in file '{fileNameToVerify}'"
                : "Code fix did not produce the expected result");
    }

    /// <summary>
    /// Test that a diagnostic is produced for the given source.
    /// </summary>
    public async Task VerifyDiagnostic(params DiagnosticResult[] expectedDiagnostics)
    {
        var actualDiagnostics = await this.GetSortedDiagnosticsFromDocuments();

        VerifyDiagnosticResults(actualDiagnostics, expectedDiagnostics);
    }

    public Task<Diagnostic[]> GetDiagnosticsAsync()
    {
        return this.GetSortedDiagnosticsFromDocuments();
    }
}

/// <summary>
/// Represents an expected diagnostic result for testing.
/// </summary>
public struct DiagnosticResult
{
    public string Id { get; set; }

    public DiagnosticSeverity Severity { get; set; }

    public int Line { get; set; }

    public int Column { get; set; }

    public string? Message { get; set; }

    public static DiagnosticResult Create(string id, DiagnosticSeverity severity, int line, int column, string? message = null)
    {
        return new DiagnosticResult
        {
            Id = id,
            Severity = severity,
            Line = line,
            Column = column,
            Message = message,
        };
    }
}