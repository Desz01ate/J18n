using System.Collections.Immutable;
using System.Text.RegularExpressions;
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
            ["build_property.RootNamespace"] = "TestProject",
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
    public void Generator_GeneratesNestedNamespace_ForNestedResource()
    {
        // Arrange
        var generator = new LocalizationMarkerGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            references: [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);

        var additionalTexts = ImmutableArray.Create<AdditionalText>(
            new TestAdditionalText("Resources/Admin/Auth.en-US.json", "{}"));

        var options = new TestAnalyzerConfigOptionsProvider(new Dictionary<string, string>
        {
            ["build_property.RootNamespace"] = "TestProject",
        });

        driver = (CSharpGeneratorDriver)driver.AddAdditionalTexts(additionalTexts)
                                             .WithUpdatedAnalyzerConfigOptions(options);

        // Act
        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        // Assert
        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedText = string.Join("\n\n---\n\n", outputCompilation.SyntaxTrees.Select(t => t.ToString()));
        Assert.Contains("namespace TestProject.Resources.Admin", generatedText);
        Assert.Contains("public sealed partial class Auth", generatedText);
    }

    [Fact]
    public void Generator_Deduplicates_MultipleCultures_EmitsSingleClassAndDiagnostic()
    {
        // Arrange
        var generator = new LocalizationMarkerGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            references: [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);

        var additionalTexts = ImmutableArray.Create<AdditionalText>(
            new TestAdditionalText("Resources/Test.en.json", "{}"),
            new TestAdditionalText("Resources/Test.es.json", "{}"),
            new TestAdditionalText("Resources/Test.en-US.json", "{}"));

        var options = new TestAnalyzerConfigOptionsProvider(new Dictionary<string, string>
        {
            ["build_property.RootNamespace"] = "TestProject",
        });

        driver = (CSharpGeneratorDriver)driver.AddAdditionalTexts(additionalTexts)
                                             .WithUpdatedAnalyzerConfigOptions(options);

        // Act
        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        // Assert - no errors
        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        // Expect at least one duplicate diagnostic (LMG003)
        Assert.Contains(diagnostics, d => d.Id == "LMG003");

        // Only one Test class should be present in generated output
        var generatedText = string.Join("\n\n---\n\n", outputCompilation.SyntaxTrees.Select(t => t.ToString()));
        var count = Regex.Matches(generatedText, @"public\s+sealed\s+partial\s+class\s+Test\b").Count;
        Assert.Equal(1, count);
    }

    [Fact]
    public void ResourceItem_TryCreate_NestedFolder_ReturnsCorrectNamespace()
    {
        // Arrange
        var additionalText = new TestAdditionalText("Resources/Admin/Auth.en-US.json", "{}");
        var options = new TestAnalyzerConfigOptions(new Dictionary<string, string>
        {
            ["build_property.RootNamespace"] = "TestProject",
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
            ["build_property.LocalizationResourceRoot"] = "Resources",
        });

        // Act
        var result = ResourceItem.TryCreate(additionalText, options);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Validation", result.ClassName);
        Assert.Equal("MyApp.Resources", result.Namespace);
    }

    [Fact]
    public void ResourceItem_TryCreate_NonResourcesFolder_UsesFolderAsNamespaceSuffix()
    {
        // Arrange
        var additionalText = new TestAdditionalText("Localization/Auth.en.json", "{}");
        var options = new TestAnalyzerConfigOptions(new Dictionary<string, string>
        {
            ["build_property.RootNamespace"] = "MyApp",
        });

        // Act
        var result = ResourceItem.TryCreate(additionalText, options);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Auth", result.ClassName);
        Assert.Equal("MyApp.Localization", result.Namespace);
        Assert.Equal("LocalizationMarkers/Localization/Auth.g.cs", result.HintName);
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
            ["build_property.LocalizationResourceRoot"] = "Resources",
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
            ["build_property.LocalizationResourceRoot"] = "Resources",
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
            ["build_property.LocalizationResourceRoot"] = "Resources",
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
        var item = new ResourceItem("test", "", "Test", "Test", "MyApp.Resources", "Test.g.cs", null);

        // Act
        var result = SourceEmitter.Emit(item);

        // Assert
        Assert.Contains("namespace MyApp.Resources", result);
        Assert.Contains("public sealed partial class Test", result);
        Assert.Contains("[System.CodeDom.Compiler.GeneratedCode(\"LocalizationMarkerGenerator\"", result);
        Assert.Contains("[System.Diagnostics.DebuggerNonUserCode]", result);
    }

    [Fact]
    public void Generator_GeneratesNamespace_FromNonResourcesFolder()
    {
        // Arrange
        var generator = new LocalizationMarkerGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            references: [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);

        var additionalTexts = ImmutableArray.Create<AdditionalText>(
            new TestAdditionalText("Localization/Authentication.en.json", "{}"));

        var options = new TestAnalyzerConfigOptionsProvider(new Dictionary<string, string>
        {
            ["build_property.RootNamespace"] = "TestProject",
        });

        driver = (CSharpGeneratorDriver)driver.AddAdditionalTexts(additionalTexts)
                                             .WithUpdatedAnalyzerConfigOptions(options);

        // Act
        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        // Assert
        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedText = string.Join("\n\n---\n\n", outputCompilation.SyntaxTrees.Select(t => t.ToString()));
        Assert.Contains("namespace TestProject.Localization", generatedText);
        Assert.Contains("public sealed partial class Authentication", generatedText);
    }

    [Fact]
    public void Generator_RunsWithoutErrors()
    {
        // Arrange
        var generator = new LocalizationMarkerGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            references: [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);

        var additionalTexts = ImmutableArray.Create<AdditionalText>(
            new TestAdditionalText("Resources/Test.en.json", "{\"key\": \"value\"}"));

        var options = new TestAnalyzerConfigOptionsProvider(new Dictionary<string, string>
        {
            ["build_property.RootNamespace"] = "TestProject",
        });

        driver = (CSharpGeneratorDriver)driver.AddAdditionalTexts(additionalTexts)
                                             .WithUpdatedAnalyzerConfigOptions(options);

        // Act
        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        // Assert - no errors
        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        // Assert - generated source exists and matches expectations
        var allGenerated = outputCompilation.SyntaxTrees.ToArray();
        Assert.NotEmpty(allGenerated);

        var generatedText = string.Join("\n\n---\n\n", allGenerated.Select(t => t.ToString()));
        Assert.Contains("namespace TestProject.Resources", generatedText);
        Assert.Contains("public sealed partial class Test", generatedText);
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

    [Fact]
    public void Generator_GeneratesSimpleProperties_FromJsonValues()
    {
        // Arrange
        var generator = new LocalizationMarkerGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            references: [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);

        var jsonContent = """{"name": "test", "message": "Hello World"}""";
        var additionalTexts = ImmutableArray.Create<AdditionalText>(
            new TestAdditionalText("Resources/Test.json", jsonContent));

        var options = new TestAnalyzerConfigOptionsProvider(new Dictionary<string, string>
        {
            ["build_property.RootNamespace"] = "TestProject",
        });

        driver = (CSharpGeneratorDriver)driver.AddAdditionalTexts(additionalTexts)
                                             .WithUpdatedAnalyzerConfigOptions(options);

        // Act
        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        // Assert
        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedText = string.Join("\n\n---\n\n", outputCompilation.SyntaxTrees.Select(t => t.ToString()));
        Assert.Contains("public const string Name = \"name\";", generatedText);
        Assert.Contains("public const string Message = \"message\";", generatedText);
    }

    [Fact]
    public void Generator_GeneratesNestedClasses_FromJsonObjects()
    {
        // Arrange
        var generator = new LocalizationMarkerGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            references: [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);

        var jsonContent = """{"user": {"name": "John", "email": "john@example.com"}, "title": "Welcome"}""";
        var additionalTexts = ImmutableArray.Create<AdditionalText>(
            new TestAdditionalText("Resources/Test.json", jsonContent));

        var options = new TestAnalyzerConfigOptionsProvider(new Dictionary<string, string>
        {
            ["build_property.RootNamespace"] = "TestProject",
        });

        driver = (CSharpGeneratorDriver)driver.AddAdditionalTexts(additionalTexts)
                                             .WithUpdatedAnalyzerConfigOptions(options);

        // Act
        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        // Assert
        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedText = string.Join("\n\n---\n\n", outputCompilation.SyntaxTrees.Select(t => t.ToString()));
        Assert.Contains("public const string Title = \"title\";", generatedText);
        Assert.Contains("public static class User", generatedText);
        Assert.Contains("public const string Name = \"user.name\";", generatedText);
        Assert.Contains("public const string Email = \"user.email\";", generatedText);
    }

    [Fact]
    public void Generator_SanitizesPropertyNames_FromInvalidJsonKeys()
    {
        // Arrange  
        var generator = new LocalizationMarkerGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            references: [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);

        var jsonContent = """{"user-name": "test", "class": "invalid", "123invalid": "number"}""";
        var additionalTexts = ImmutableArray.Create<AdditionalText>(
            new TestAdditionalText("Resources/Test.json", jsonContent));

        var options = new TestAnalyzerConfigOptionsProvider(new Dictionary<string, string>
        {
            ["build_property.RootNamespace"] = "TestProject",
        });

        driver = (CSharpGeneratorDriver)driver.AddAdditionalTexts(additionalTexts)
                                             .WithUpdatedAnalyzerConfigOptions(options);

        // Act
        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        // Assert
        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedText = string.Join("\n\n---\n\n", outputCompilation.SyntaxTrees.Select(t => t.ToString()));
        Assert.Contains("public const string UserName = \"user-name\";", generatedText);
        Assert.Contains("public const string Class_ = \"class\";", generatedText);
        Assert.Contains("public const string _123invalid = \"123invalid\";", generatedText);
    }

    [Fact]
    public void SourceEmitter_GeneratesEmptyClass_WhenNoJsonStructure()
    {
        // Arrange
        var item = new ResourceItem("test", "", "Test", "Test", "MyApp.Resources", "Test.g.cs", null);

        // Act
        var result = SourceEmitter.Emit(item);

        // Assert
        Assert.Contains("namespace MyApp.Resources", result);
        Assert.Contains("public sealed partial class Test", result);
        Assert.DoesNotContain("public const string", result);
        Assert.DoesNotContain("public static class", result);
    }

    [Fact]
    public void Generator_GeneratesArrayProperties_FromJsonArrays()
    {
        // Arrange
        var generator = new LocalizationMarkerGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            references: [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);

        var jsonContent = """{"items": ["item1", "item2"], "title": "Test"}""";
        var additionalTexts = ImmutableArray.Create<AdditionalText>(
            new TestAdditionalText("Resources/Test.json", jsonContent));

        var options = new TestAnalyzerConfigOptionsProvider(new Dictionary<string, string>
        {
            ["build_property.RootNamespace"] = "TestProject",
        });

        driver = (CSharpGeneratorDriver)driver.AddAdditionalTexts(additionalTexts)
                                             .WithUpdatedAnalyzerConfigOptions(options);

        // Act
        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        // Assert
        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedText = string.Join("\n\n---\n\n", outputCompilation.SyntaxTrees.Select(t => t.ToString()));
        Assert.Contains("public const string Title = \"title\";", generatedText);
        Assert.Contains("public static class Items", generatedText);
        // Bare container Key constant must NOT be emitted (runtime never resolves "items", only "items[0]" etc.)
        Assert.DoesNotContain("public const string Key = \"items\";", generatedText);
        Assert.Contains("public const string _Item0 = \"items[0]\";", generatedText);
        Assert.Contains("public const string _Item1 = \"items[1]\";", generatedText);
    }

    [Fact]
    public void Generator_GeneratesNestedArrayProperties_FromJsonArraysWithObjects()
    {
        // Arrange
        var generator = new LocalizationMarkerGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            references: [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);

        var jsonContent = """{"users": [{"name": "John", "age": "30"}, {"name": "Jane", "age": "25"}]}""";
        var additionalTexts = ImmutableArray.Create<AdditionalText>(
            new TestAdditionalText("Resources/Test.json", jsonContent));

        var options = new TestAnalyzerConfigOptionsProvider(new Dictionary<string, string>
        {
            ["build_property.RootNamespace"] = "TestProject",
        });

        driver = (CSharpGeneratorDriver)driver.AddAdditionalTexts(additionalTexts)
                                             .WithUpdatedAnalyzerConfigOptions(options);

        // Act
        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        // Assert
        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedText = string.Join("\n\n---\n\n", outputCompilation.SyntaxTrees.Select(t => t.ToString()));
        Assert.Contains("public static class Users", generatedText);
        // Bare container Key constant must NOT be emitted (runtime never resolves "users", only "users[0].name" etc.)
        Assert.DoesNotContain("public const string Key = \"users\";", generatedText);
        Assert.Contains("public static class Item0", generatedText);
        Assert.Contains("public const string Name = \"users[0].name\";", generatedText);
        Assert.Contains("public const string Age = \"users[0].age\";", generatedText);
        Assert.Contains("public static class Item1", generatedText);
        Assert.Contains("public const string Name = \"users[1].name\";", generatedText);
        Assert.Contains("public const string Age = \"users[1].age\";", generatedText);
    }

    [Fact]
    public void Generator_GeneratesNestedArrays_FromJsonNestedArrays()
    {
        // Arrange
        var generator = new LocalizationMarkerGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            references: [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);

        var jsonContent = """{"matrix": [["a", "b"], ["c", "d"]]}""";
        var additionalTexts = ImmutableArray.Create<AdditionalText>(
            new TestAdditionalText("Resources/Test.json", jsonContent));

        var options = new TestAnalyzerConfigOptionsProvider(new Dictionary<string, string>
        {
            ["build_property.RootNamespace"] = "TestProject",
        });

        driver = (CSharpGeneratorDriver)driver.AddAdditionalTexts(additionalTexts)
                                             .WithUpdatedAnalyzerConfigOptions(options);

        // Act
        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        // Assert
        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedText = string.Join("\n\n---\n\n", outputCompilation.SyntaxTrees.Select(t => t.ToString()));
        Assert.Contains("public static class Matrix", generatedText);
        // Bare container Key constants must NOT be emitted at any level (runtime never resolves "matrix" or "matrix[0]")
        Assert.DoesNotContain("public const string Key = \"matrix\";", generatedText);
        Assert.Contains("public static class Item0", generatedText);
        Assert.DoesNotContain("public const string Key = \"matrix[0]\";", generatedText);
        Assert.Contains("public const string _Item0 = \"matrix[0][0]\";", generatedText);
        Assert.Contains("public const string _Item1 = \"matrix[0][1]\";", generatedText);
        Assert.Contains("public static class Item1", generatedText);
        Assert.DoesNotContain("public const string Key = \"matrix[1]\";", generatedText);
        Assert.Contains("public const string _Item0 = \"matrix[1][0]\";", generatedText);
        Assert.Contains("public const string _Item1 = \"matrix[1][1]\";", generatedText);
    }

    /// <summary>
    /// Regression test for F1: The array-container bare "Key" constant must NOT be emitted.
    /// The runtime only produces items[0], items[1], ... — never the bare "items" key.
    /// Emitting Key = "items" causes LOC001 (Error) because the analyzer catalog no longer
    /// contains the bare container key.
    /// </summary>
    [Fact]
    public void Generator_ArrayContainer_DoesNotEmitBareKeyConstant()
    {
        // Arrange
        var generator = new LocalizationMarkerGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            references: [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);

        var jsonContent = """{"items": ["a", "b"]}""";
        var additionalTexts = ImmutableArray.Create<AdditionalText>(
            new TestAdditionalText("Resources/Test.json", jsonContent));

        var options = new TestAnalyzerConfigOptionsProvider(new Dictionary<string, string>
        {
            ["build_property.RootNamespace"] = "TestProject",
        });

        driver = (CSharpGeneratorDriver)driver.AddAdditionalTexts(additionalTexts)
                                             .WithUpdatedAnalyzerConfigOptions(options);

        // Act
        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        // Assert
        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedText = string.Join("\n\n---\n\n", outputCompilation.SyntaxTrees.Select(t => t.ToString()));

        // Indexed item constants MUST be present
        Assert.Contains("public const string _Item0 = \"items[0]\";", generatedText);
        Assert.Contains("public const string _Item1 = \"items[1]\";", generatedText);
        Assert.Contains("public static class Items", generatedText);

        // Bare container Key constant must NOT be emitted — the runtime never produces
        // "items" as a resolved key; only "items[0]", "items[1]", ... are valid.
        Assert.DoesNotContain("public const string Key = \"items\";", generatedText);
    }

    [Fact]
    public void Generator_PreservesPascalCasePropertyNames_WhenJsonKeysAreAlreadyPascalCase()
    {
        // Arrange
        var generator = new LocalizationMarkerGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            references: [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);

        var jsonContent = """{"Dashboard": "Dashboard", "HelloWorld": "Hello World", "GoodMorning": "Good Morning"}""";
        var additionalTexts = ImmutableArray.Create<AdditionalText>(
            new TestAdditionalText("Resources/Nav.json", jsonContent));

        var options = new TestAnalyzerConfigOptionsProvider(new Dictionary<string, string>
        {
            ["build_property.RootNamespace"] = "TestProject",
        });

        driver = (CSharpGeneratorDriver)driver.AddAdditionalTexts(additionalTexts)
                                             .WithUpdatedAnalyzerConfigOptions(options);

        // Act
        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        // Assert
        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedText = string.Join("\n\n---\n\n", outputCompilation.SyntaxTrees.Select(t => t.ToString()));
        Assert.Contains("public const string Dashboard = \"Dashboard\";", generatedText);
        Assert.Contains("public const string HelloWorld = \"HelloWorld\";", generatedText);
        Assert.Contains("public const string GoodMorning = \"GoodMorning\";", generatedText);
    }
}
