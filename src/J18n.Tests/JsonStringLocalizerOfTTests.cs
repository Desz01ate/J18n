using System.Globalization;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Localization;

namespace J18n.Tests;

public class TestResource
{
    // Test resource class for generic localizer testing
}

public class AnotherTestResource
{
    // Another test resource class
}

public class JsonStringLocalizerOfTTests
{
    private readonly JsonResourceLoader _resourceLoader;

    public JsonStringLocalizerOfTTests()
    {
        var testResourcesPath = Path.Combine(Directory.GetCurrentDirectory(), "TestResources");
        var fileProvider = new PhysicalFileProvider(testResourcesPath);
        this._resourceLoader = new JsonResourceLoader(fileProvider, "");
    }

    [Fact]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        var localizer = new JsonStringLocalizer<TestResource>(this._resourceLoader);

        localizer.Should().NotBeNull();
        localizer.Should().BeAssignableTo<IStringLocalizer<TestResource>>();
        localizer.Should().BeAssignableTo<IStringLocalizer>();
    }

    [Fact]
    public void Constructor_WithNullResourceLoader_ThrowsArgumentNullException()
    {
        Action act = () => new JsonStringLocalizer<TestResource>(null!);

        act.Should().Throw<ArgumentNullException>()
           .WithParameterName("resourceLoader");
    }

    [Fact]
    public void Indexer_WithExistingKey_ReturnsCorrectValue()
    {
        var localizer = new JsonStringLocalizer<TestResource>(this._resourceLoader);
        
        var result = localizer["SimpleMessage"];

        result.Should().NotBeNull();
        result.Name.Should().Be("SimpleMessage");
        result.Value.Should().Be("Hello World");
        result.ResourceNotFound.Should().BeFalse();
    }

    [Fact]
    public void Indexer_WithParameterizedMessage_ReturnsFormattedValue()
    {
        var localizer = new JsonStringLocalizer<TestResource>(this._resourceLoader);
        
        var result = localizer["ParameterizedMessage", "Alice"];

        result.Should().NotBeNull();
        result.Name.Should().Be("ParameterizedMessage");
        result.Value.Should().Be("Hello, Alice!");
        result.ResourceNotFound.Should().BeFalse();
    }

    [Fact]
    public void Indexer_WithNonExistentKey_ReturnsKeyAsValue()
    {
        var localizer = new JsonStringLocalizer<TestResource>(this._resourceLoader);
        
        var result = localizer["NonExistentKey"];

        result.Should().NotBeNull();
        result.Name.Should().Be("NonExistentKey");
        result.Value.Should().Be("NonExistentKey");
        result.ResourceNotFound.Should().BeTrue();
    }

    [Fact]
    public void GetAllStrings_ReturnsAllResourceStrings()
    {
        var localizer = new JsonStringLocalizer<TestResource>(this._resourceLoader);
        
        var result = localizer.GetAllStrings(includeParentCultures: true);

        result.Should().NotBeNull();
        result.Should().NotBeEmpty();
        
        var resultList = result.ToList();
        resultList.Should().Contain(ls => ls.Name == "SimpleMessage" && ls.Value == "Hello World");
        resultList.Should().Contain(ls => ls.Name == "ParameterizedMessage" && ls.Value == "Hello, {0}!");
    }

    [Fact]
    public void TypedLocalizer_UsesCorrectResourceBaseName()
    {
        // The JsonStringLocalizer<TestResource> should look for "TestResource.{culture}.json"
        var localizer = new JsonStringLocalizer<TestResource>(this._resourceLoader);
        
        var result = localizer["SimpleMessage"];

        // This should find the TestResource.en.json file
        result.Value.Should().Be("Hello World");
        result.ResourceNotFound.Should().BeFalse();
    }

    [Fact]
    public void TypedLocalizer_WithDifferentResourceType_UsesCorrectBaseName()
    {
        // Test that different types use different base names
        var testResourceLocalizer = new JsonStringLocalizer<TestResource>(this._resourceLoader);
        var anotherTestResourceLocalizer = new JsonStringLocalizer<AnotherTestResource>(this._resourceLoader);

        // TestResource should find the file
        var testResult = testResourceLocalizer["SimpleMessage"];
        testResult.ResourceNotFound.Should().BeFalse();
        testResult.Value.Should().Be("Hello World");

        // AnotherTestResource should NOT find the file (since AnotherTestResource.en.json doesn't exist)
        var anotherResult = anotherTestResourceLocalizer["SimpleMessage"];
        anotherResult.ResourceNotFound.Should().BeTrue();
        anotherResult.Value.Should().Be("SimpleMessage");
    }

    [Fact]
    public void TypedLocalizer_InheritsFromBaseLocalizer()
    {
        var localizer = new JsonStringLocalizer<TestResource>(this._resourceLoader);

        // Should have all the functionality of the base class
        localizer.Should().BeAssignableTo<JsonStringLocalizer>();
        localizer.Should().BeAssignableTo<IStringLocalizer>();
    }

    [Fact]
    public void TypedLocalizer_WithCurrentUICulture_UsesCorrectCulture()
    {
        var originalCulture = CultureInfo.CurrentUICulture;
        try
        {
            // Set culture to Spanish
            CultureInfo.CurrentUICulture = new CultureInfo("es");
            
            var localizer = new JsonStringLocalizer<TestResource>(this._resourceLoader);
            var result = localizer["SimpleMessage"];

            // Should use Spanish resources
            result.Value.Should().Be("Hola Mundo");
            result.ResourceNotFound.Should().BeFalse();
        }
        finally
        {
            CultureInfo.CurrentUICulture = originalCulture;
        }
    }

    [Fact]
    public void TypedLocalizer_WithDifferentCultures_ReturnsDifferentValues()
    {
        var originalCulture = CultureInfo.CurrentUICulture;
        try
        {
            // Test English
            CultureInfo.CurrentUICulture = new CultureInfo("en");
            var englishLocalizer = new JsonStringLocalizer<TestResource>(this._resourceLoader);
            var englishResult = englishLocalizer["SimpleMessage"];

            // Test Spanish
            CultureInfo.CurrentUICulture = new CultureInfo("es");
            var spanishLocalizer = new JsonStringLocalizer<TestResource>(this._resourceLoader);
            var spanishResult = spanishLocalizer["SimpleMessage"];

            englishResult.Value.Should().Be("Hello World");
            spanishResult.Value.Should().Be("Hola Mundo");
        }
        finally
        {
            CultureInfo.CurrentUICulture = originalCulture;
        }
    }

    [Fact]
    public void TypedLocalizer_ImplementsIStringLocalizerT()
    {
        var localizer = new JsonStringLocalizer<TestResource>(this._resourceLoader);

        // Verify it implements the correct interface
        localizer.Should().BeAssignableTo<IStringLocalizer<TestResource>>();
        
        // Verify interface methods work
        IStringLocalizer<TestResource> typedInterface = localizer;
        var result = typedInterface["SimpleMessage"];
        result.Value.Should().Be("Hello World");
    }

    [Theory]
    [InlineData("SimpleMessage")]
    [InlineData("ParameterizedMessage")]
    [InlineData("MultipleParameters")]
    [InlineData("EmptyString")]
    [InlineData("SpecialCharacters")]
    public void TypedLocalizer_WithVariousKeys_WorksCorrectly(string key)
    {
        var localizer = new JsonStringLocalizer<TestResource>(this._resourceLoader);
        
        Action act = () => _ = localizer[key];

        act.Should().NotThrow();
    }

    [Fact]
    public void TypedLocalizer_CastToIStringLocalizer_WorksCorrectly()
    {
        var typedLocalizer = new JsonStringLocalizer<TestResource>(this._resourceLoader);
        IStringLocalizer baseLocalizer = typedLocalizer;

        var typedResult = typedLocalizer["SimpleMessage"];
        var baseResult = baseLocalizer["SimpleMessage"];

        typedResult.Value.Should().Be(baseResult.Value);
        typedResult.Name.Should().Be(baseResult.Name);
        typedResult.ResourceNotFound.Should().Be(baseResult.ResourceNotFound);
    }
}