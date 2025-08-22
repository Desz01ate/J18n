using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Localization;

namespace J18n.Tests;

public class IntegrationTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;

    public IntegrationTests()
    {
        var services = new ServiceCollection();
        var testResourcesPath = Path.Combine(Directory.GetCurrentDirectory(), "TestResources");
        var fileProvider = new PhysicalFileProvider(testResourcesPath);

        services.AddJsonLocalization(fileProvider, "");
        this._serviceProvider = services.BuildServiceProvider();
    }

    public void Dispose()
    {
        this._serviceProvider?.Dispose();
    }

    [Fact]
    public void JsonLocalization_FullWorkflow_WorksEndToEnd()
    {
        // Get services
        var factory = this._serviceProvider.GetRequiredService<IStringLocalizerFactory>();
        var typedLocalizer = this._serviceProvider.GetRequiredService<IStringLocalizer<TestResource>>();

        // Test factory-created localizer
        var localizer = factory.Create(typeof(TestResource));

        // Test simple localization
        var simple = localizer["SimpleMessage"];
        simple.Value.Should().Be("Hello World");
        simple.ResourceNotFound.Should().BeFalse();

        // Test parameterized localization
        var parameterized = localizer["ParameterizedMessage", "Integration"];
        parameterized.Value.Should().Be("Hello, Integration!");
        parameterized.ResourceNotFound.Should().BeFalse();

        // Test typed localizer
        var typedSimple = typedLocalizer["SimpleMessage"];
        typedSimple.Value.Should().Be("Hello World");
        typedSimple.ResourceNotFound.Should().BeFalse();

        // Test missing key fallback
        var missing = localizer["NonExistentKey"];
        missing.Value.Should().Be("NonExistentKey");
        missing.ResourceNotFound.Should().BeTrue();

        // Test GetAllStrings
        var allStrings = localizer.GetAllStrings(includeParentCultures: true);
        allStrings.Should().NotBeEmpty();
        allStrings.Should().Contain(ls => ls.Name == "SimpleMessage");
        allStrings.Should().Contain(ls => ls.Name == "ParameterizedMessage");
    }

    [Fact]
    public void JsonLocalization_MultiCultureSupport_WorksCorrectly()
    {
        var originalCulture = CultureInfo.CurrentUICulture;

        try
        {
            // Test English
            CultureInfo.CurrentUICulture = new CultureInfo("en");
            var englishFactory = this._serviceProvider.GetRequiredService<IStringLocalizerFactory>();
            var englishLocalizer = englishFactory.Create(typeof(TestResource));
            var englishResult = englishLocalizer["SimpleMessage"];

            // Test Spanish
            CultureInfo.CurrentUICulture = new CultureInfo("es");
            var spanishFactory = this._serviceProvider.GetRequiredService<IStringLocalizerFactory>();
            var spanishLocalizer = spanishFactory.Create(typeof(TestResource));
            var spanishResult = spanishLocalizer["SimpleMessage"];

            // Test French (with fallback)
            CultureInfo.CurrentUICulture = new CultureInfo("fr");
            var frenchFactory = this._serviceProvider.GetRequiredService<IStringLocalizerFactory>();
            var frenchLocalizer = frenchFactory.Create(typeof(TestResource));
            var frenchResult = frenchLocalizer["SimpleMessage"];

            englishResult.Value.Should().Be("Hello World");
            spanishResult.Value.Should().Be("Hola Mundo");
            frenchResult.Value.Should().Be("Bonjour le monde");

            // Test culture fallback - French should fallback to English for keys not in French
            var frenchFallback = frenchLocalizer["FormattedNumber", 19.99m];
            frenchFallback.ResourceNotFound.Should().BeFalse();
            frenchFallback.Value.Should().StartWith("Price:");
        }
        finally
        {
            CultureInfo.CurrentUICulture = originalCulture;
        }
    }

    [Fact]
    public void JsonLocalization_CultureHierarchy_WorksCorrectly()
    {
        var originalCulture = CultureInfo.CurrentUICulture;

        try
        {
            // Test with specific culture that should fall back to parent
            CultureInfo.CurrentUICulture = new CultureInfo("es-MX"); // Mexican Spanish

            var factory = this._serviceProvider.GetRequiredService<IStringLocalizerFactory>();
            var localizer = factory.Create(typeof(TestResource));
            var result = localizer["SimpleMessage"];

            // Should fallback to es.json since es-MX.json doesn't exist
            result.Value.Should().Be("Hola Mundo");
            result.ResourceNotFound.Should().BeFalse();
        }
        finally
        {
            CultureInfo.CurrentUICulture = originalCulture;
        }
    }

    [Fact]
    public void JsonLocalization_ComplexFormatting_WorksCorrectly()
    {
        var originalCulture = CultureInfo.CurrentUICulture;

        try
        {
            CultureInfo.CurrentUICulture = new CultureInfo("en-US");

            var factory = this._serviceProvider.GetRequiredService<IStringLocalizerFactory>();
            var localizer = factory.Create(typeof(TestResource));

            // Test multiple parameters
            var multipleParams = localizer["MultipleParameters", "Alice", 42];
            multipleParams.Value.Should().Be("User Alice has 42 items");

            // Test formatted numbers
            var formattedNumber = localizer["FormattedNumber", 123.45m];
            formattedNumber.Value.Should().Contain("$123.45");
        }
        finally
        {
            CultureInfo.CurrentUICulture = originalCulture;
        }
    }

    [Fact]
    public void JsonLocalization_EmptyAndSpecialValues_HandledCorrectly()
    {
        var factory = this._serviceProvider.GetRequiredService<IStringLocalizerFactory>();
        var localizer = factory.Create(typeof(TestResource));

        // Test empty string
        var empty = localizer["EmptyString"];
        empty.Value.Should().Be("");
        empty.ResourceNotFound.Should().BeFalse();

        // Test special characters
        var special = localizer["SpecialCharacters"];
        special.Value.Should().Be("Special chars: àáâãäåæçèéêë");
        special.ResourceNotFound.Should().BeFalse();

        // Test long text
        var longText = localizer["LongText"];
        longText.Value.Should().StartWith("This is a very long text");
        longText.Value.Length.Should().BeGreaterThan(100);
        longText.ResourceNotFound.Should().BeFalse();
    }

    [Fact]
    public void JsonLocalization_TypedLocalizer_ConsistentWithFactory()
    {
        var factory = this._serviceProvider.GetRequiredService<IStringLocalizerFactory>();
        var factoryLocalizer = factory.Create(typeof(TestResource));
        var typedLocalizer = this._serviceProvider.GetRequiredService<IStringLocalizer<TestResource>>();

        var factoryResult = factoryLocalizer["SimpleMessage"];
        var typedResult = typedLocalizer["SimpleMessage"];

        factoryResult.Value.Should().Be(typedResult.Value);
        factoryResult.Name.Should().Be(typedResult.Name);
        factoryResult.ResourceNotFound.Should().Be(typedResult.ResourceNotFound);
    }

    [Fact]
    public void JsonLocalization_DifferentResourceTypes_IsolatedCorrectly()
    {
        var factory = this._serviceProvider.GetRequiredService<IStringLocalizerFactory>();

        var testResourceLocalizer = factory.Create(typeof(TestResource));
        var anotherResourceLocalizer = factory.Create(typeof(AnotherTestResource));

        // TestResource should find the resource file
        var testResult = testResourceLocalizer["SimpleMessage"];
        testResult.ResourceNotFound.Should().BeFalse();
        testResult.Value.Should().Be("Hello World");

        // AnotherTestResource should not find a resource file
        var anotherResult = anotherResourceLocalizer["SimpleMessage"];
        anotherResult.ResourceNotFound.Should().BeTrue();
        anotherResult.Value.Should().Be("SimpleMessage");
    }

    [Fact]
    public void JsonLocalization_Performance_CachingWorks()
    {
        var factory = this._serviceProvider.GetRequiredService<IStringLocalizerFactory>();
        var localizer = factory.Create(typeof(TestResource));

        // First access
        var startTime = DateTime.UtcNow;
        var result1 = localizer["SimpleMessage"];
        var firstAccessTime = DateTime.UtcNow - startTime;

        // Second access (should be faster due to caching)
        startTime = DateTime.UtcNow;
        var result2 = localizer["SimpleMessage"];
        var secondAccessTime = DateTime.UtcNow - startTime;

        result1.Value.Should().Be(result2.Value);

        // While we can't guarantee exact timing, the second access should generally be faster
        // This is more of a smoke test to ensure caching doesn't break functionality
        result1.Should().NotBeSameAs(result2); // Different LocalizedString instances
        result1.Value.Should().Be(result2.Value); // But same value
    }

    [Fact]
    public void JsonLocalization_ErrorHandling_GracefulDegradation()
    {
        var factory = this._serviceProvider.GetRequiredService<IStringLocalizerFactory>();
        var localizer = factory.Create("NonExistentResource", "SomeLocation");

        // Should not throw, should return key as value
        var result = localizer["AnyKey"];
        result.Value.Should().Be("AnyKey");
        result.ResourceNotFound.Should().BeTrue();

        // GetAllStrings should return empty collection
        var allStrings = localizer.GetAllStrings(includeParentCultures: true);
        allStrings.Should().NotBeNull();
        allStrings.Should().BeEmpty();
    }

    [Fact]
    public void JsonLocalization_ServiceLifetime_BehavesCorrectly()
    {
        // Factory should be transient
        var factory1 = this._serviceProvider.GetService<IStringLocalizerFactory>();
        var factory2 = this._serviceProvider.GetService<IStringLocalizerFactory>();
        factory1.Should().NotBeSameAs(factory2);

        // ResourceLoader should be singleton
        var loader1 = this._serviceProvider.GetService<JsonResourceLoader>();
        var loader2 = this._serviceProvider.GetService<JsonResourceLoader>();
        loader1.Should().BeSameAs(loader2);

        // Typed localizers should be transient
        var typed1 = this._serviceProvider.GetService<IStringLocalizer<TestResource>>();
        var typed2 = this._serviceProvider.GetService<IStringLocalizer<TestResource>>();
        typed1.Should().NotBeSameAs(typed2);
    }

    [Theory]
    [InlineData("en", "Hello World")]
    [InlineData("es", "Hola Mundo")]
    [InlineData("fr", "Bonjour le monde")]
    [InlineData("de", "Hello World")] // Fallback to English when German not available
    public void JsonLocalization_VariousCultures_ProduceExpectedResults(string cultureName, string expectedValue)
    {
        var originalCulture = CultureInfo.CurrentUICulture;

        try
        {
            CultureInfo.CurrentUICulture = new CultureInfo(cultureName);

            var factory = this._serviceProvider.GetRequiredService<IStringLocalizerFactory>();
            var localizer = factory.Create(typeof(TestResource));
            var result = localizer["SimpleMessage"];

            result.Value.Should().Be(expectedValue);
        }
        finally
        {
            CultureInfo.CurrentUICulture = originalCulture;
        }
    }
}