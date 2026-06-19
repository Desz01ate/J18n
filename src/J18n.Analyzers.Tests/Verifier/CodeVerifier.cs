namespace J18n.Analyzers.Tests.Verifier;

using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;

public sealed class CodeVerifier : CodeVerifierBase
{
    private readonly Project project;
    private readonly AnalyzerConfigOptionsProvider? configOptionsProvider;

    public CodeVerifier(string source, AdditionalFile[] additionalFiles)
    {
        this.project = CreateProject(source, additionalFiles);
    }

    public CodeVerifier(string source, AdditionalFile[] additionalFiles, Dictionary<string, string> analyzerConfigOptions)
    {
        this.project = CreateProject(source, additionalFiles);
        this.configOptionsProvider = new TestAnalyzerConfigOptionsProvider(analyzerConfigOptions);
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

    protected override AnalyzerConfigOptionsProvider? GetConfigOptionsProvider()
    {
        return this.configOptionsProvider;
    }
}