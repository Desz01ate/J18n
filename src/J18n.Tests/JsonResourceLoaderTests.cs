using System.Globalization;
using Microsoft.Extensions.FileProviders;

namespace J18n.Tests;

public class JsonResourceLoaderTests
{
    private readonly IFileProvider _fileProvider;
    private readonly JsonResourceLoader _loader;

    public JsonResourceLoaderTests()
    {
        var testResourcesPath = Path.Combine(Directory.GetCurrentDirectory(), "TestResources");
        this._fileProvider = new PhysicalFileProvider(testResourcesPath);
        this._loader = new JsonResourceLoader(this._fileProvider, "");
    }

    [Fact]
    public void Constructor_WithNullFileProvider_ThrowsArgumentNullException()
    {
        Action act = () => new JsonResourceLoader(null!, "Resources");

        act.Should().Throw<ArgumentNullException>()
           .WithParameterName("fileProvider");
    }

    [Fact]
    public void Constructor_WithNullResourcesPath_ThrowsArgumentNullException()
    {
        Action act = () => new JsonResourceLoader(this._fileProvider, null!);

        act.Should().Throw<ArgumentNullException>()
           .WithParameterName("resourcesPath");
    }

    [Fact]
    public void LoadResources_WithExistingEnglishResource_ReturnsCorrectData()
    {
        var culture = new CultureInfo("en");
        
        var resources = this._loader.LoadResources("TestResource", culture);

        resources.Should().NotBeNull();
        resources.Should().ContainKey("SimpleMessage");
        resources["SimpleMessage"].Should().Be("Hello World");
        resources.Should().ContainKey("ParameterizedMessage");
        resources["ParameterizedMessage"].Should().Be("Hello, {0}!");
    }

    [Fact]
    public void LoadResources_WithExistingSpanishResource_ReturnsCorrectData()
    {
        var culture = new CultureInfo("es");
        
        var resources = this._loader.LoadResources("TestResource", culture);

        resources.Should().NotBeNull();
        resources.Should().ContainKey("SimpleMessage");
        resources["SimpleMessage"].Should().Be("Hola Mundo");
        resources.Should().ContainKey("ParameterizedMessage");
        resources["ParameterizedMessage"].Should().Be("¡Hola, {0}!");
    }

    [Fact]
    public void LoadResources_WithFrenchCulture_UsesCultureFallback()
    {
        var culture = new CultureInfo("fr");
        
        var resources = this._loader.LoadResources("TestResource", culture);

        resources.Should().NotBeNull();
        resources.Should().ContainKey("SimpleMessage");
        resources["SimpleMessage"].Should().Be("Bonjour le monde");
        
        // Should have fallback values that don't exist in French but exist in base culture
        resources.Should().ContainKey("ParameterizedMessage");
        resources["ParameterizedMessage"].Should().Be("Bonjour, {0} !");
    }

    [Fact]
    public void LoadResources_WithNonExistentResource_ReturnsEmptyDictionary()
    {
        var culture = new CultureInfo("en");
        
        var resources = this._loader.LoadResources("NonExistentResource", culture);

        resources.Should().NotBeNull();
        resources.Should().BeEmpty();
    }

    [Fact]
    public void LoadResources_WithSpecificCulture_UsesCultureHierarchy()
    {
        var culture = new CultureInfo("es-ES");
        
        var resources = this._loader.LoadResources("TestResource", culture);

        // Should find es.json file since es-ES.json doesn't exist
        resources.Should().NotBeNull();
        resources.Should().ContainKey("SimpleMessage");
        resources["SimpleMessage"].Should().Be("Hola Mundo");
    }

    [Fact]
    public void LoadResources_CalledMultipleTimes_UsesCaching()
    {
        var culture = new CultureInfo("en");
        
        var resources1 = this._loader.LoadResources("TestResource", culture);
        var resources2 = this._loader.LoadResources("TestResource", culture);

        // Should return the same instance due to caching
        resources1.Should().BeSameAs(resources2);
    }

    [Fact]
    public void LoadResources_WithEmptyString_HandlesEmptyValues()
    {
        var culture = new CultureInfo("en");
        
        var resources = this._loader.LoadResources("TestResource", culture);

        resources.Should().ContainKey("EmptyString");
        resources["EmptyString"].Should().Be("");
    }

    [Fact]
    public void LoadResources_WithSpecialCharacters_HandlesUnicodeCorrectly()
    {
        var culture = new CultureInfo("en");
        
        var resources = this._loader.LoadResources("TestResource", culture);

        resources.Should().ContainKey("SpecialCharacters");
        resources["SpecialCharacters"].Should().Be("Special chars: àáâãäåæçèéêë");
    }

    [Fact]
    public void LoadResources_WithLongText_HandlesLongStringsCorrectly()
    {
        var culture = new CultureInfo("en");
        
        var resources = this._loader.LoadResources("TestResource", culture);

        resources.Should().ContainKey("LongText");
        resources["LongText"].Should().StartWith("This is a very long text");
        resources["LongText"].Length.Should().BeGreaterThan(100);
    }

    [Fact]
    public void ClearCache_RemovesCachedEntries()
    {
        var culture = new CultureInfo("en");
        
        // Load resources to populate cache
        var resources1 = this._loader.LoadResources("TestResource", culture);
        
        // Clear cache
        this._loader.ClearCache();
        
        // Load resources again - should be a new instance
        var resources2 = this._loader.LoadResources("TestResource", culture);

        resources1.Should().NotBeSameAs(resources2);
        resources1.Should().BeEquivalentTo(resources2);
    }

    [Theory]
    [InlineData("en")]
    [InlineData("es")]
    [InlineData("fr")]
    [InlineData("en-US")]
    [InlineData("es-ES")]
    public void LoadResources_WithVariousCultures_DoesNotThrow(string cultureName)
    {
        var culture = new CultureInfo(cultureName);
        
        Action act = () => this._loader.LoadResources("TestResource", culture);

        act.Should().NotThrow();
    }
}