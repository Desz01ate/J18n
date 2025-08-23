namespace J18n.Analyzer.Tests.Verifier;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;

public sealed class PartialMissingKeyCodeVerifier : CodeVerifierBase
{
    private readonly Project project;

    public PartialMissingKeyCodeVerifier(string source, AdditionalFile[] additionalFiles)
    {
        this.project = CreateProject(source, additionalFiles);
    }

    protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
    {
        return new LocalizationAnalyzer();
    }

    protected override CodeFixProvider GetCSharpCodeFixProvider()
    {
        return new PartialMissingKeyCodeFixProvider();
    }

    protected override Project GetProject()
    {
        return this.project;
    }
}