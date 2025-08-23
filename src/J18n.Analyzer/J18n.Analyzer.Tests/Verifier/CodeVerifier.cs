namespace J18n.Analyzer.Tests.Verifier;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;

public sealed class CodeVerifier : CodeVerifierBase
{
    private readonly Project project;

    public CodeVerifier(string source, AdditionalFile[] additionalFiles)
    {
        this.project = CreateProject(source, additionalFiles);
    }

    protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
    {
        return new LocalizationAnalyzer();
    }

    protected override CodeFixProvider? GetCSharpCodeFixProvider() => null;

    protected override Project GetProject()
    {
        return this.project;
    }
}