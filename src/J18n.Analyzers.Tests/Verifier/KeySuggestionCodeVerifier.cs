namespace J18n.Analyzers.Tests.Verifier;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;

public sealed class KeySuggestionCodeVerifier : CodeVerifierBase
{
    private readonly Project project;

    public KeySuggestionCodeVerifier(string source, AdditionalFile[] additionalFiles)
    {
        this.project = CreateProject(source, additionalFiles);
    }

    protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
    {
        return new LocalizationAnalyzer();
    }

    protected override CodeFixProvider GetCSharpCodeFixProvider()
    {
        return new KeySuggestionCodeFixProvider();
    }

    protected override Project GetProject()
    {
        return this.project;
    }
}