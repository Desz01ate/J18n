using System.Globalization;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Localization;

namespace J18n.Tests;

public class JsonStringLocalizerFactoryTests
{
    private readonly JsonResourceLoader _resourceLoader;
    private readonly JsonStringLocalizerFactory _factory;

    public JsonStringLocalizerFactoryTests()
    {
        var testResourcesPath = Path.Combine(Directory.GetCurrentDirectory(), "TestResources");
        var fileProvider = new PhysicalFileProvider(testResourcesPath);
        this._resourceLoader = new JsonResourceLoader(fileProvider, "");
        this._factory = new JsonStringLocalizerFactory(this._resourceLoader);
    }

    [Fact]
    public void Constructor_WithValidResourceLoader_CreatesInstance()
    {
        var factory = new JsonStringLocalizerFactory(this._resourceLoader);

        factory.Should().NotBeNull();
        factory.Should().BeAssignableTo<IStringLocalizerFactory>();
    }

    [Fact]
    public void Constructor_WithNullResourceLoader_ThrowsArgumentNullException()
    {
        Action act = () => new JsonStringLocalizerFactory(null!);

        act.Should().Throw<ArgumentNullException>()
           .WithParameterName("resourceLoader");
    }

    [Fact]
    public void Create_WithValidResourceSource_ReturnsLocalizer()
    {
        var resourceSource = typeof(TestResource);
        
        var localizer = this._factory.Create(resourceSource);

        localizer.Should().NotBeNull();
        localizer.Should().BeAssignableTo<IStringLocalizer>();
        localizer.Should().BeOfType<JsonStringLocalizer>();
    }

    [Fact]
    public void Create_WithNullResourceSource_ThrowsArgumentNullException()
    {
        Action act = () => this._factory.Create((Type)null!);

        act.Should().Throw<ArgumentNullException>()
           .WithParameterName("resourceSource");
    }

    [Fact]
    public void Create_WithResourceSourceType_UsesTypeNameAsBaseName()
    {
        var resourceSource = typeof(TestResource);
        
        var localizer = this._factory.Create(resourceSource);
        var result = localizer["SimpleMessage"];

        // Should find TestResource.en.json and return the correct value
        result.Value.Should().Be("Hello World");
        result.ResourceNotFound.Should().BeFalse();
    }

    [Fact]
    public void Create_WithBaseNameAndLocation_ReturnsLocalizer()
    {
        var localizer = this._factory.Create("TestResource", "SomeLocation");

        localizer.Should().NotBeNull();
        localizer.Should().BeAssignableTo<IStringLocalizer>();
        localizer.Should().BeOfType<JsonStringLocalizer>();
    }

    [Fact]
    public void Create_WithNullBaseName_ThrowsArgumentNullException()
    {
        Action act = () => this._factory.Create(null!, "SomeLocation");

        act.Should().Throw<ArgumentNullException>()
           .WithParameterName("baseName");
    }

    [Fact]
    public void Create_WithEmptyBaseName_ThrowsArgumentNullException()
    {
        Action act = () => this._factory.Create("", "SomeLocation");

        act.Should().Throw<ArgumentNullException>()
           .WithParameterName("baseName");
    }

    [Fact]
    public void Create_WithBaseNameAndLocation_UsesCorrectBaseName()
    {
        var localizer = this._factory.Create("TestResource", "SomeLocation");
        var result = localizer["SimpleMessage"];

        // Should find TestResource.en.json regardless of location parameter
        result.Value.Should().Be("Hello World");
        result.ResourceNotFound.Should().BeFalse();
    }

    [Fact]
    public void Create_UsesCurrentUICulture()
    {
        var originalCulture = CultureInfo.CurrentUICulture;
        try
        {
            // Set culture to Spanish
            CultureInfo.CurrentUICulture = new CultureInfo("es");
            
            var localizer = this._factory.Create(typeof(TestResource));
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
    public void Create_WithDifferentTypes_CreatesDistinctLocalizers()
    {
        var localizer1 = this._factory.Create(typeof(TestResource));
        var localizer2 = this._factory.Create(typeof(AnotherTestResource));

        localizer1.Should().NotBeSameAs(localizer2);

        // TestResource should find resources
        var result1 = localizer1["SimpleMessage"];
        result1.ResourceNotFound.Should().BeFalse();

        // AnotherTestResource should not find resources (no AnotherTestResource.json)
        var result2 = localizer2["SimpleMessage"];
        result2.ResourceNotFound.Should().BeTrue();
    }

    [Fact]
    public void Create_MultipleCalls_WithSameParameters_CreatesNewInstances()
    {
        var localizer1 = this._factory.Create(typeof(TestResource));
        var localizer2 = this._factory.Create(typeof(TestResource));

        // Factory should create new instances each time
        localizer1.Should().NotBeSameAs(localizer2);
        
        // But they should behave the same way
        var result1 = localizer1["SimpleMessage"];
        var result2 = localizer2["SimpleMessage"];
        
        result1.Value.Should().Be(result2.Value);
        result1.ResourceNotFound.Should().Be(result2.ResourceNotFound);
    }

    [Fact]
    public void Create_WithDifferentCultures_CreatesAppropriateLocalizers()
    {
        var originalCulture = CultureInfo.CurrentUICulture;
        try
        {
            // Create localizer with English culture
            CultureInfo.CurrentUICulture = new CultureInfo("en");
            var englishLocalizer = this._factory.Create(typeof(TestResource));
            
            // Create localizer with Spanish culture
            CultureInfo.CurrentUICulture = new CultureInfo("es");
            var spanishLocalizer = this._factory.Create(typeof(TestResource));

            // Reset to English to test
            CultureInfo.CurrentUICulture = new CultureInfo("en");
            var englishResult = englishLocalizer["SimpleMessage"];
            
            // Test Spanish localizer
            CultureInfo.CurrentUICulture = new CultureInfo("es");
            var spanishResult = spanishLocalizer["SimpleMessage"];

            englishResult.Value.Should().Be("Hello World");
            spanishResult.Value.Should().Be("Hola Mundo");
        }
        finally
        {
            CultureInfo.CurrentUICulture = originalCulture;
        }
    }

    [Theory]
    [InlineData(typeof(TestResource))]
    [InlineData(typeof(AnotherTestResource))]
    [InlineData(typeof(JsonStringLocalizerFactoryTests))]
    public void Create_WithVariousTypes_CreatesLocalizers(Type resourceType)
    {
        var localizer = this._factory.Create(resourceType);

        // We can't directly test the base name, but we can test that different types
        // produce different behavior (some will find resources, others won't)
        localizer.Should().NotBeNull();
        localizer.Should().BeOfType<JsonStringLocalizer>();
    }

    [Theory]
    [InlineData("TestResource")]
    [InlineData("SomeResource")]
    [InlineData("MyCustomResource")]
    public void Create_WithVariousBaseNames_CreatesLocalizers(string baseName)
    {
        var localizer = this._factory.Create(baseName, "SomeLocation");

        localizer.Should().NotBeNull();
        localizer.Should().BeOfType<JsonStringLocalizer>();
    }

    [Fact]
    public void Factory_ImplementsIStringLocalizerFactory()
    {
        IStringLocalizerFactory factory = this._factory;

        factory.Should().NotBeNull();
        
        var localizer1 = factory.Create(typeof(TestResource));
        var localizer2 = factory.Create("TestResource", "Location");

        localizer1.Should().BeOfType<JsonStringLocalizer>();
        localizer2.Should().BeOfType<JsonStringLocalizer>();
    }

    [Fact]
    public void Create_WithNonExistentResource_StillCreatesLocalizer()
    {
        var localizer = this._factory.Create("NonExistentResource", "SomeLocation");

        localizer.Should().NotBeNull();
        
        // Should return key as value for non-existent resources
        var result = localizer["AnyKey"];
        result.Value.Should().Be("AnyKey");
        result.ResourceNotFound.Should().BeTrue();
    }
}