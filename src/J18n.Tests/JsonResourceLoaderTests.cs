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

    #region Nested JSON Tests

    [Fact]
    public void LoadResources_WithNestedJson_FlattensCorrectly()
    {
        var culture = new CultureInfo("en");
        
        var resources = this._loader.LoadResources("NestedTestResource", culture);

        resources.Should().NotBeNull();
        resources.Should().ContainKey("SimpleMessage");
        resources["SimpleMessage"].Should().Be("Hello World");
        
        // Test nested object flattening
        resources.Should().ContainKey("User.Profile.Greeting");
        resources["User.Profile.Greeting"].Should().Be("Welcome, {0}!");
        
        resources.Should().ContainKey("User.Profile.Settings.Language");
        resources["User.Profile.Settings.Language"].Should().Be("English");
        
        resources.Should().ContainKey("User.Account.Status");
        resources["User.Account.Status"].Should().Be("Active");
        
        resources.Should().ContainKey("Navigation.Contact.Email");
        resources["Navigation.Contact.Email"].Should().Be("Contact Email");
    }

    [Fact]
    public void LoadResources_WithNestedArrays_FlattensWithIndexes()
    {
        var culture = new CultureInfo("en");
        
        var resources = this._loader.LoadResources("NestedTestResource", culture);

        resources.Should().ContainKey("Messages[0]");
        resources["Messages[0]"].Should().Be("First message");
        
        resources.Should().ContainKey("Messages[1]");
        resources["Messages[1]"].Should().Be("Second message");
        
        resources.Should().ContainKey("Messages[2]");
        resources["Messages[2]"].Should().Be("Third message");
    }

    [Fact]
    public void LoadResources_WithComplexNestedArrays_FlattensCorrectly()
    {
        var culture = new CultureInfo("en");
        
        var resources = this._loader.LoadResources("NestedTestResource", culture);

        // Test nested arrays with objects
        resources.Should().ContainKey("Complex.Items[0].Name");
        resources["Complex.Items[0].Name"].Should().Be("Item 1");
        
        resources.Should().ContainKey("Complex.Items[0].Description");
        resources["Complex.Items[0].Description"].Should().Be("First item description");
        
        resources.Should().ContainKey("Complex.Items[1].Name");
        resources["Complex.Items[1].Name"].Should().Be("Item 2");
        
        resources.Should().ContainKey("Complex.Config.Settings.AutoSave");
        resources["Complex.Config.Settings.AutoSave"].Should().Be("true");
    }

    [Fact]
    public void LoadResources_WithDifferentValueTypes_HandlesAllTypes()
    {
        var culture = new CultureInfo("en");
        
        var resources = this._loader.LoadResources("NestedTestResource", culture);

        // Test string values
        resources.Should().ContainKey("User.Profile.Title");
        resources["User.Profile.Title"].Should().Be("Mr./Ms.");
        
        // Test number values
        resources.Should().ContainKey("Numbers.Count");
        resources["Numbers.Count"].Should().Be("42");
        
        resources.Should().ContainKey("Numbers.Price");
        resources["Numbers.Price"].Should().Be("29.99");
        
        // Test boolean values
        resources.Should().ContainKey("Numbers.IsEnabled");
        resources["Numbers.IsEnabled"].Should().Be("true");
        
        resources.Should().ContainKey("Numbers.IsDisabled");
        resources["Numbers.IsDisabled"].Should().Be("false");
        
        // Test null values
        resources.Should().ContainKey("Numbers.NullValue");
        resources["Numbers.NullValue"].Should().Be("");
    }

    [Fact]
    public void LoadResources_WithNestedJsonAndCultureFallback_Works()
    {
        var culture = new CultureInfo("es");
        
        var resources = this._loader.LoadResources("NestedTestResource", culture);

        resources.Should().NotBeNull();
        
        // Test Spanish translations
        resources.Should().ContainKey("SimpleMessage");
        resources["SimpleMessage"].Should().Be("Hola Mundo");
        
        resources.Should().ContainKey("User.Profile.Greeting");
        resources["User.Profile.Greeting"].Should().Be("Bienvenido, {0}!");
        
        resources.Should().ContainKey("User.Profile.Settings.Language");
        resources["User.Profile.Settings.Language"].Should().Be("Español");
        
        // Test fallback to English for keys not in Spanish file
        resources.Should().ContainKey("Complex.Items[0].Name");
        resources["Complex.Items[0].Name"].Should().Be("Item 1");
    }

    [Fact]
    public void LoadResources_WithMixedFlatAndNestedJson_HandlesBothCorrectly()
    {
        var culture = new CultureInfo("en");
        
        var resources = this._loader.LoadResources("MixedTestResource", culture);

        resources.Should().NotBeNull();
        
        // Test flat keys
        resources.Should().ContainKey("FlatMessage");
        resources["FlatMessage"].Should().Be("This is a flat message");
        
        resources.Should().ContainKey("AnotherFlat");
        resources["AnotherFlat"].Should().Be("Another flat message with {0} parameter");
        
        resources.Should().ContainKey("FinalFlat");
        resources["FinalFlat"].Should().Be("Final flat message");
        
        // Test nested keys
        resources.Should().ContainKey("Nested.Level1");
        resources["Nested.Level1"].Should().Be("First level nested");
        
        resources.Should().ContainKey("Nested.Deep.Level2");
        resources["Nested.Deep.Level2"].Should().Be("Second level nested");
    }

    [Fact]
    public void LoadResources_WithDeepNesting_FlattensAllLevels()
    {
        var culture = new CultureInfo("en");
        
        var resources = this._loader.LoadResources("NestedTestResource", culture);

        // Test deeply nested keys
        resources.Should().ContainKey("User.Profile.Settings.Theme");
        resources["User.Profile.Settings.Theme"].Should().Be("Light");
        
        resources.Should().ContainKey("Complex.Config.Settings.Timeout");
        resources["Complex.Config.Settings.Timeout"].Should().Be("30");
    }

    [Fact]
    public void LoadResources_WithNestedJson_CachesCorrectly()
    {
        var culture = new CultureInfo("en");
        
        var resources1 = this._loader.LoadResources("NestedTestResource", culture);
        var resources2 = this._loader.LoadResources("NestedTestResource", culture);

        // Should return the same instance due to caching
        resources1.Should().BeSameAs(resources2);
        
        // Verify some nested keys are cached correctly
        resources1.Should().ContainKey("User.Profile.Greeting");
        resources2.Should().ContainKey("User.Profile.Greeting");
        resources1["User.Profile.Greeting"].Should().Be(resources2["User.Profile.Greeting"]);
    }

    [Fact]
    public void LoadResources_WithEmptyNestedObjects_HandlesGracefully()
    {
        // Create a test file with empty nested objects
        var testContent = @"{
            ""Message"": ""Hello"",
            ""Empty"": {},
            ""EmptyArray"": []
        }";
        
        var tempDir = Path.GetTempPath();
        var baseName = "EmptyTestResource";
        var tempFile = Path.Combine(tempDir, $"{baseName}.en.json");
        File.WriteAllText(tempFile, testContent);
        
        try
        {
            var fileProvider = new PhysicalFileProvider(tempDir);
            var loader = new JsonResourceLoader(fileProvider, "");
            
            var culture = new CultureInfo("en");
            var resources = loader.LoadResources(baseName, culture);
            
            resources.Should().ContainKey("Message");
            resources["Message"].Should().Be("Hello");
            
            // Empty objects and arrays should not create any keys
            resources.Keys.Should().NotContain(k => k.StartsWith("Empty"));
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    #endregion
}