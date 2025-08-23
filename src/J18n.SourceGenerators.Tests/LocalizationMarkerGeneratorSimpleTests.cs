using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace J18n.SourceGenerators.Tests;

public class LocalizationMarkerGeneratorSimpleTests
{
    [Fact]
    public void ResourceItem_TryCreate_SingleFile_ReturnsValidItem()
    {
        // Arrange
        var additionalText = new TestAdditionalText("Resources/Authentication.en.json", "{}");
        var options = new TestAnalyzerConfigOptions(new Dictionary<string, string>
        {
            ["build_property.RootNamespace"] = "TestProject"
        });

        // Act
        var result = ResourceItem.TryCreate(additionalText, options);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Authentication", result.ClassName);
        Assert.Equal("TestProject.Resources", result.Namespace);
        Assert.Equal("LocalizationMarkers/Authentication.g.cs", result.HintName);
    }

    [Fact]
    public void ResourceItem_TryCreate_NestedFolder_ReturnsCorrectNamespace()
    {
        // Arrange
        var additionalText = new TestAdditionalText("Resources/Admin/Auth.en-US.json", "{}");
        var options = new TestAnalyzerConfigOptions(new Dictionary<string, string>
        {
            ["build_property.RootNamespace"] = "TestProject"
        });

        // Act
        var result = ResourceItem.TryCreate(additionalText, options);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Auth", result.ClassName);
        Assert.Equal("TestProject.Resources.Admin", result.Namespace);
        Assert.Equal("LocalizationMarkers/Admin/Auth.g.cs", result.HintName);
    }

    [Fact]
    public void ResourceItem_TryCreate_CultureSuffixRemoval_RemovesCulture()
    {
        // Arrange
        var additionalText = new TestAdditionalText("Resources/Validation.es-ES.json", "{}");
        var options = new TestAnalyzerConfigOptions(new Dictionary<string, string>
        {
            ["build_property.RootNamespace"] = "MyApp",
            ["build_property.ProjectDir"] = "/test/project/",
            ["build_property.LocalizationResourceRoot"] = "Resources"
        });

        // Act
        var result = ResourceItem.TryCreate(additionalText, options);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Validation", result.ClassName);
        Assert.Equal("MyApp.Resources", result.Namespace);
    }

    [Fact]
    public void ResourceItem_TryCreate_NoCultureSuffix_KeepsOriginalName()
    {
        // Arrange
        var additionalText = new TestAdditionalText("Resources/Common.json", "{}");
        var options = new TestAnalyzerConfigOptions(new Dictionary<string, string>
        {
            ["build_property.RootNamespace"] = "MyApp",
            ["build_property.ProjectDir"] = "/test/project/",
            ["build_property.LocalizationResourceRoot"] = "Resources"
        });

        // Act
        var result = ResourceItem.TryCreate(additionalText, options);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Common", result.ClassName);
        Assert.Equal("MyApp.Resources", result.Namespace);
    }

    [Fact]
    public void ResourceItem_TryCreate_InvalidName_SanitizesName()
    {
        // Arrange
        var additionalText = new TestAdditionalText("Resources/my-special-resource.en.json", "{}");
        var options = new TestAnalyzerConfigOptions(new Dictionary<string, string>
        {
            ["build_property.RootNamespace"] = "MyApp",
            ["build_property.ProjectDir"] = "/test/project/",
            ["build_property.LocalizationResourceRoot"] = "Resources"
        });

        // Act
        var result = ResourceItem.TryCreate(additionalText, options);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("MySpecialResource", result.ClassName); // PascalCase applied
        Assert.Equal("MyApp.Resources", result.Namespace);
    }

    [Fact]
    public void ResourceItem_TryCreate_NonJsonFile_ReturnsNull()
    {
        // Arrange
        var additionalText = new TestAdditionalText("Resources/test.txt", "content");
        var options = new TestAnalyzerConfigOptions(new Dictionary<string, string>
        {
            ["build_property.RootNamespace"] = "MyApp",
            ["build_property.ProjectDir"] = "/test/project/",
            ["build_property.LocalizationResourceRoot"] = "Resources"
        });

        // Act
        var result = ResourceItem.TryCreate(additionalText, options);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void SourceEmitter_Emit_GeneratesCorrectCode()
    {
        // Arrange
        var item = new ResourceItem("test", "", "Test", "Test", "MyApp.Resources", "Test.g.cs");

        // Act
        var result = SourceEmitter.Emit(item);

        // Assert
        Assert.Contains("namespace MyApp.Resources", result);
        Assert.Contains("public sealed partial class Test", result);
        Assert.Contains("[System.CodeDom.Compiler.GeneratedCode(\"LocalizationMarkerGenerator\"", result);
        Assert.Contains("[System.Diagnostics.DebuggerNonUserCode]", result);
    }

    [Fact]
    public void Generator_RunsWithoutErrors()
    {
        // Arrange
        var generator = new LocalizationMarkerGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            references: new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) });

        var additionalTexts = ImmutableArray.Create<AdditionalText>(
            new TestAdditionalText("Resources/Test.en.json", "{\"key\": \"value\"}"));

        var options = new TestAnalyzerConfigOptionsProvider(new Dictionary<string, string>
        {
            ["build_property.RootNamespace"] = "TestProject"
        });

        driver = (CSharpGeneratorDriver)driver.AddAdditionalTexts(additionalTexts)
                                             .WithUpdatedAnalyzerConfigOptions(options);

        // Act
        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        // Assert
        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
        var generatedSources = outputCompilation.SyntaxTrees.Where(t => t.FilePath.Contains(".g.cs"));
        Assert.Single(generatedSources);
    }

    // Helper classes for testing
    private class TestAdditionalText : AdditionalText
    {
        private readonly string _content;

        public TestAdditionalText(string path, string content)
        {
            Path = path;
            _content = content;
        }

        public override string Path { get; }

        public override SourceText GetText(CancellationToken cancellationToken = default)
        {
            return SourceText.From(_content);
        }
    }

    private class TestAnalyzerConfigOptions : AnalyzerConfigOptions
    {
        private readonly Dictionary<string, string> _options;

        public TestAnalyzerConfigOptions(Dictionary<string, string> options)
        {
            _options = options;
        }

        public override bool TryGetValue(string key, out string value)
        {
            return _options.TryGetValue(key, out value!);
        }
    }

    private class TestAnalyzerConfigOptionsProvider : AnalyzerConfigOptionsProvider
    {
        public TestAnalyzerConfigOptionsProvider(Dictionary<string, string> globalOptions)
        {
            GlobalOptions = new TestAnalyzerConfigOptions(globalOptions);
        }

        public override AnalyzerConfigOptions GlobalOptions { get; }

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => new TestAnalyzerConfigOptions(new Dictionary<string, string>());
        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => new TestAnalyzerConfigOptions(new Dictionary<string, string>());
    }
}