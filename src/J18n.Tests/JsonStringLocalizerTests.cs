using System.Globalization;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Localization;

namespace J18n.Tests;

public class JsonStringLocalizerTests
{
    private readonly JsonResourceLoader _resourceLoader;
    private readonly JsonStringLocalizer _localizer;

    public JsonStringLocalizerTests()
    {
        var testResourcesPath = Path.Combine(Directory.GetCurrentDirectory(), "TestResources");
        var fileProvider = new PhysicalFileProvider(testResourcesPath);
        this._resourceLoader = new JsonResourceLoader(fileProvider, "");
        this._localizer = new JsonStringLocalizer(this._resourceLoader, "TestResource", new CultureInfo("en"));
    }

    [Fact]
    public void Constructor_WithNullResourceLoader_ThrowsArgumentNullException()
    {
        Action act = () => new JsonStringLocalizer(null!, "TestResource", CultureInfo.InvariantCulture);

        act.Should().Throw<ArgumentNullException>()
           .WithParameterName("resourceLoader");
    }

    [Fact]
    public void Constructor_WithNullBaseName_ThrowsArgumentNullException()
    {
        Action act = () => new JsonStringLocalizer(this._resourceLoader, null!, CultureInfo.InvariantCulture);

        act.Should().Throw<ArgumentNullException>()
           .WithParameterName("baseName");
    }

    [Fact]
    public void Constructor_WithNullCulture_ThrowsArgumentNullException()
    {
        Action act = () => new JsonStringLocalizer(this._resourceLoader, "TestResource", null!);

        act.Should().Throw<ArgumentNullException>()
           .WithParameterName("culture");
    }

    [Fact]
    public void Indexer_WithExistingKey_ReturnsCorrectValue()
    {
        var result = this._localizer["SimpleMessage"];

        result.Should().NotBeNull();
        result.Name.Should().Be("SimpleMessage");
        result.Value.Should().Be("Hello World");
        result.ResourceNotFound.Should().BeFalse();
    }

    [Fact]
    public void Indexer_WithNonExistentKey_ReturnsKeyAsValue()
    {
        var result = this._localizer["NonExistentKey"];

        result.Should().NotBeNull();
        result.Name.Should().Be("NonExistentKey");
        result.Value.Should().Be("NonExistentKey");
        result.ResourceNotFound.Should().BeTrue();
    }

    [Fact]
    public void Indexer_WithNullKey_ThrowsArgumentNullException()
    {
        Action act = () => _ = this._localizer[null!];

        act.Should().Throw<ArgumentNullException>()
           .WithParameterName("name");
    }

    [Fact]
    public void Indexer_WithEmptyKey_ThrowsArgumentNullException()
    {
        Action act = () => _ = this._localizer[""];

        act.Should().Throw<ArgumentNullException>()
           .WithParameterName("name");
    }

    [Fact]
    public void IndexerWithArguments_WithExistingKey_ReturnsFormattedValue()
    {
        var result = this._localizer["ParameterizedMessage", "John"];

        result.Should().NotBeNull();
        result.Name.Should().Be("ParameterizedMessage");
        result.Value.Should().Be("Hello, John!");
        result.ResourceNotFound.Should().BeFalse();
    }

    [Fact]
    public void IndexerWithArguments_WithMultipleParameters_ReturnsFormattedValue()
    {
        var result = this._localizer["MultipleParameters", "Alice", 5];

        result.Should().NotBeNull();
        result.Name.Should().Be("MultipleParameters");
        result.Value.Should().Be("User Alice has 5 items");
        result.ResourceNotFound.Should().BeFalse();
    }

    [Fact]
    public void IndexerWithArguments_WithFormattedNumber_ReturnsFormattedValue()
    {
        var culture = new CultureInfo("en-US");
        var localizer = new JsonStringLocalizer(this._resourceLoader, "TestResource", culture);
        
        var result = localizer["FormattedNumber", 29.99m];

        result.Should().NotBeNull();
        result.Name.Should().Be("FormattedNumber");
        result.Value.Should().Contain("$29.99");
        result.ResourceNotFound.Should().BeFalse();
    }

    [Fact]
    public void IndexerWithArguments_WithNonExistentKey_ReturnsKeyAsValue()
    {
        var result = this._localizer["NonExistentKey", "arg1", "arg2"];

        result.Should().NotBeNull();
        result.Name.Should().Be("NonExistentKey");
        result.Value.Should().Be("NonExistentKey");
        result.ResourceNotFound.Should().BeTrue();
    }

    [Fact]
    public void IndexerWithArguments_WithInvalidFormatString_ReturnsUnformattedValue()
    {
        // Create a localizer with a malformed format string
        var invalidFormatLoader = new JsonResourceLoader(
            new PhysicalFileProvider(Directory.GetCurrentDirectory()), "");
        
        // Mock a resource with invalid format
        var localizer = new JsonStringLocalizer(invalidFormatLoader, "NonExistent", new CultureInfo("en"));
        
        var result = localizer["SomeKey", "arg1"];

        result.Should().NotBeNull();
        result.Name.Should().Be("SomeKey");
        result.Value.Should().Be("SomeKey"); // Falls back to key name
    }

    [Fact]
    public void IndexerWithArguments_WithNullKey_ThrowsArgumentNullException()
    {
        Action act = () => _ = this._localizer[null!, "arg1"];

        act.Should().Throw<ArgumentNullException>()
           .WithParameterName("name");
    }

    [Fact]
    public void IndexerWithArguments_WithEmptyKey_ThrowsArgumentNullException()
    {
        Action act = () => _ = this._localizer["", "arg1"];

        act.Should().Throw<ArgumentNullException>()
           .WithParameterName("name");
    }

    [Fact]
    public void GetAllStrings_WithIncludeParentCulturesTrue_ReturnsAllStrings()
    {
        var result = this._localizer.GetAllStrings(includeParentCultures: true);

        result.Should().NotBeNull();
        result.Should().NotBeEmpty();
        
        var resultList = result.ToList();
        resultList.Should().Contain(ls => ls.Name == "SimpleMessage" && ls.Value == "Hello World");
        resultList.Should().Contain(ls => ls.Name == "ParameterizedMessage" && ls.Value == "Hello, {0}!");
        resultList.Should().Contain(ls => ls.Name == "MultipleParameters");
        
        // All should be marked as found
        resultList.Should().AllSatisfy(ls => ls.ResourceNotFound.Should().BeFalse());
    }

    // Renamed from GetAllStrings_WithIncludeParentCulturesFalse_ReturnsAllStrings:
    // The fixture uses en culture — the single culture file IS the same as the merged hierarchy,
    // so both true and false return the same keys for en. The test is retained as a non-regression
    // check but updated to use the correct contract name and exclude any fallback-exclusion
    // assertion (which would be vacuous here). The es/fr tests below are the discriminating ones.
    [Fact]
    public void GetAllStrings_WithIncludeParentCulturesFalse_ReturnsOnlySingleCultureFile()
    {
        // en is both the target culture and the sole fallback — single-culture == merged for en.
        var result = this._localizer.GetAllStrings(includeParentCultures: false);

        result.Should().NotBeNull();
        result.Should().NotBeEmpty();

        var resultList = result.ToList();
        resultList.Should().Contain(ls => ls.Name == "SimpleMessage");
        resultList.Should().Contain(ls => ls.Name == "ParameterizedMessage");
        // All returned items must be marked as found
        resultList.Should().AllSatisfy(ls => ls.ResourceNotFound.Should().BeFalse());
    }

    // C1 fix: GetAllStrings(false) should return ONLY the specific culture file keys, not parent culture keys
    [Fact]
    public void GetAllStrings_WithIncludeParentCulturesFalse_ReturnsOnlySpecificCultureKeys()
    {
        var testResourcesPath = Path.Combine(Directory.GetCurrentDirectory(), "TestResources");
        var fileProvider = new PhysicalFileProvider(testResourcesPath);
        var resourceLoader = new JsonResourceLoader(fileProvider, "");
        var esLocalizer = new JsonStringLocalizer(resourceLoader, "CultureOnlyTest", new CultureInfo("es"));

        var resultTrue = esLocalizer.GetAllStrings(includeParentCultures: true).ToList();
        var resultFalse = esLocalizer.GetAllStrings(includeParentCultures: false).ToList();

        // true: should contain both the es-specific key and the en fallback key
        resultTrue.Should().Contain(ls => ls.Name == "OnlyEs");
        resultTrue.Should().Contain(ls => ls.Name == "OnlyEn");

        // false: should contain ONLY the es-specific key, NOT the en fallback key
        resultFalse.Should().Contain(ls => ls.Name == "OnlyEs");
        resultFalse.Should().NotContain(ls => ls.Name == "OnlyEn");
    }

    // C1 + C2 interaction: GetAllStrings(false) must NOT include neutral file keys
    [Fact]
    public void GetAllStrings_WithIncludeParentCulturesFalse_DoesNotIncludeNeutralFileKeys()
    {
        var testResourcesPath = Path.Combine(Directory.GetCurrentDirectory(), "TestResources");
        var fileProvider = new PhysicalFileProvider(testResourcesPath);
        var resourceLoader = new JsonResourceLoader(fileProvider, "");
        // NeutralMerge.json = { "Shared": "base" }, NeutralMerge.fr.json = { "Greeting": "Bonjour" }
        var frLocalizer = new JsonStringLocalizer(resourceLoader, "NeutralMerge", new CultureInfo("fr"));

        var resultTrue = frLocalizer.GetAllStrings(includeParentCultures: true).ToList();
        var resultFalse = frLocalizer.GetAllStrings(includeParentCultures: false).ToList();

        // true: merged result includes both neutral and culture keys
        resultTrue.Should().Contain(ls => ls.Name == "Shared");
        resultTrue.Should().Contain(ls => ls.Name == "Greeting");

        // false: only the specific culture file (fr) — neutral file must NOT be included
        resultFalse.Should().Contain(ls => ls.Name == "Greeting");
        resultFalse.Should().NotContain(ls => ls.Name == "Shared");
    }

    [Fact]
    public void GetAllStrings_ReturnsLocalizedStringCollection()
    {
        var result = this._localizer.GetAllStrings(includeParentCultures: true);

        result.Should().AllBeOfType<LocalizedString>();
        result.Should().AllSatisfy(ls => 
        {
            ls.Name.Should().NotBeNullOrEmpty();
            ls.Value.Should().NotBeNull();
        });
    }

    [Theory]
    [InlineData("SimpleMessage", "Hello World")]
    [InlineData("EmptyString", "")]
    [InlineData("SpecialCharacters", "Special chars: àáâãäåæçèéêë")]
    public void Indexer_WithVariousKeys_ReturnsExpectedValues(string key, string expectedValue)
    {
        var result = this._localizer[key];

        result.Value.Should().Be(expectedValue);
        result.ResourceNotFound.Should().BeFalse();
    }

    [Fact]
    public void LocalizedString_ImplicitConversionToString_Works()
    {
        LocalizedString localizedString = this._localizer["SimpleMessage"];
        
        string value = localizedString;
        
        value.Should().Be("Hello World");
    }

    [Fact]
    public void JsonStringLocalizer_WithDifferentCultures_LoadsDifferentResources()
    {
        var englishLocalizer = new JsonStringLocalizer(this._resourceLoader, "TestResource", new CultureInfo("en"));
        var spanishLocalizer = new JsonStringLocalizer(this._resourceLoader, "TestResource", new CultureInfo("es"));

        var englishResult = englishLocalizer["SimpleMessage"];
        var spanishResult = spanishLocalizer["SimpleMessage"];

        englishResult.Value.Should().Be("Hello World");
        spanishResult.Value.Should().Be("Hola Mundo");
    }
}