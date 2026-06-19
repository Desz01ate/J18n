using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Localization;

namespace J18n.Tests;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddJsonLocalization_WithoutOptions_RegistersServices()
    {
        var services = new ServiceCollection();

        services.AddJsonLocalization();

        var serviceProvider = services.BuildServiceProvider();
        
        // Verify all required services are registered
        serviceProvider.GetService<IStringLocalizerFactory>().Should().NotBeNull();
        serviceProvider.GetService<JsonResourceLoader>().Should().NotBeNull();
        serviceProvider.GetService<IFileProvider>().Should().NotBeNull();
    }

    [Fact]
    public void AddJsonLocalization_WithOptions_RegistersServicesWithConfiguration()
    {
        var services = new ServiceCollection();

        services.AddJsonLocalization(options =>
        {
            options.ResourcesRelativePath = "CustomResources";
        });

        var serviceProvider = services.BuildServiceProvider();
        
        serviceProvider.GetService<IStringLocalizerFactory>().Should().NotBeNull();
        serviceProvider.GetService<JsonResourceLoader>().Should().NotBeNull();
    }

    [Fact]
    public void AddJsonLocalization_WithNullServices_ThrowsArgumentNullException()
    {
        IServiceCollection services = null!;

        Action act = () => services.AddJsonLocalization();

        act.Should().Throw<ArgumentNullException>()
           .WithParameterName("services");
    }

    [Fact]
    public void AddJsonLocalization_WithNullSetupAction_ThrowsArgumentNullException()
    {
        var services = new ServiceCollection();

        Action act = () => services.AddJsonLocalization((Action<JsonLocalizationOptions>)null!);

        act.Should().Throw<ArgumentNullException>()
           .WithParameterName("setupAction");
    }

    [Fact]
    public void AddJsonLocalization_WithFileProvider_RegistersServices()
    {
        var services = new ServiceCollection();
        var testResourcesPath = Path.Combine(Directory.GetCurrentDirectory(), "TestResources");
        var fileProvider = new PhysicalFileProvider(testResourcesPath);

        services.AddJsonLocalization(fileProvider, "");

        var serviceProvider = services.BuildServiceProvider();
        
        serviceProvider.GetService<IStringLocalizerFactory>().Should().NotBeNull();
        serviceProvider.GetService<JsonResourceLoader>().Should().NotBeNull();
        serviceProvider.GetRequiredService<IFileProvider>().Should().BeSameAs(fileProvider);
    }

    [Fact]
    public void AddJsonLocalization_WithFileProvider_AndNullServices_ThrowsArgumentNullException()
    {
        IServiceCollection services = null!;
        var fileProvider = new PhysicalFileProvider(Directory.GetCurrentDirectory());

        Action act = () => services.AddJsonLocalization(fileProvider);

        act.Should().Throw<ArgumentNullException>()
           .WithParameterName("services");
    }

    [Fact]
    public void AddJsonLocalization_WithNullFileProvider_ThrowsArgumentNullException()
    {
        var services = new ServiceCollection();

        Action act = () => services.AddJsonLocalization((IFileProvider)null!);

        act.Should().Throw<ArgumentNullException>()
           .WithParameterName("fileProvider");
    }

    [Fact]
    public void AddJsonLocalization_RegistersIStringLocalizerFactory()
    {
        var services = new ServiceCollection();

        services.AddJsonLocalization();

        var serviceProvider = services.BuildServiceProvider();
        var factory = serviceProvider.GetService<IStringLocalizerFactory>();
        
        factory.Should().NotBeNull();
        factory.Should().BeOfType<JsonStringLocalizerFactory>();
    }

    [Fact]
    public void AddJsonLocalization_RegistersTypedLocalizer()
    {
        var services = new ServiceCollection();
        var testResourcesPath = Path.Combine(Directory.GetCurrentDirectory(), "TestResources");
        var fileProvider = new PhysicalFileProvider(testResourcesPath);

        services.AddJsonLocalization(fileProvider, "");

        var serviceProvider = services.BuildServiceProvider();
        var typedLocalizer = serviceProvider.GetService<IStringLocalizer<TestResource>>();
        
        typedLocalizer.Should().NotBeNull();
        typedLocalizer.Should().BeAssignableTo<IStringLocalizer<TestResource>>();
    }

    [Fact]
    public void AddJsonLocalization_TypedLocalizer_WorksCorrectly()
    {
        var services = new ServiceCollection();
        var testResourcesPath = Path.Combine(Directory.GetCurrentDirectory(), "TestResources");
        var fileProvider = new PhysicalFileProvider(testResourcesPath);

        services.AddJsonLocalization(fileProvider, "");

        var serviceProvider = services.BuildServiceProvider();
        var typedLocalizer = serviceProvider.GetRequiredService<IStringLocalizer<TestResource>>();
        
        var result = typedLocalizer["SimpleMessage"];
        result.Value.Should().Be("Hello World");
        result.ResourceNotFound.Should().BeFalse();
    }

    [Fact]
    public void AddJsonLocalization_Factory_WorksCorrectly()
    {
        var services = new ServiceCollection();
        var testResourcesPath = Path.Combine(Directory.GetCurrentDirectory(), "TestResources");
        var fileProvider = new PhysicalFileProvider(testResourcesPath);

        services.AddJsonLocalization(fileProvider, "");

        var serviceProvider = services.BuildServiceProvider();
        var factory = serviceProvider.GetRequiredService<IStringLocalizerFactory>();
        var localizer = factory.Create(typeof(TestResource));
        
        var result = localizer["SimpleMessage"];
        result.Value.Should().Be("Hello World");
        result.ResourceNotFound.Should().BeFalse();
    }

    [Fact]
    public void AddJsonLocalization_RegistersServicesAsSingleton()
    {
        var services = new ServiceCollection();

        services.AddJsonLocalization();

        var serviceProvider = services.BuildServiceProvider();
        
        // JsonResourceLoader should be singleton
        var loader1 = serviceProvider.GetService<JsonResourceLoader>();
        var loader2 = serviceProvider.GetService<JsonResourceLoader>();
        
        loader1.Should().BeSameAs(loader2);
    }

    [Fact]
    public void AddJsonLocalization_RegistersFactoryAsTransient()
    {
        var services = new ServiceCollection();

        services.AddJsonLocalization();

        var serviceProvider = services.BuildServiceProvider();
        
        // Factory should be transient (new instance each time)
        var factory1 = serviceProvider.GetService<IStringLocalizerFactory>();
        var factory2 = serviceProvider.GetService<IStringLocalizerFactory>();
        
        factory1.Should().NotBeSameAs(factory2);
        factory1.Should().BeOfType<JsonStringLocalizerFactory>();
        factory2.Should().BeOfType<JsonStringLocalizerFactory>();
    }

    [Fact]
    public void AddJsonLocalization_WithCustomResourcesPath_ConfiguresCorrectly()
    {
        var services = new ServiceCollection();

        services.AddJsonLocalization(options =>
        {
            options.ResourcesRelativePath = "MyCustomResources";
        });

        // Service registration should complete without error
        var serviceProvider = services.BuildServiceProvider();
        serviceProvider.GetService<IStringLocalizerFactory>().Should().NotBeNull();
    }

    [Fact]
    public void AddJsonLocalization_MultipleRegistrations_DoesNotDuplicate()
    {
        var services = new ServiceCollection();

        services.AddJsonLocalization();
        services.AddJsonLocalization(); // Second registration

        var serviceProvider = services.BuildServiceProvider();
        
        // Should still work correctly
        var factory = serviceProvider.GetService<IStringLocalizerFactory>();
        factory.Should().NotBeNull();
        factory.Should().BeOfType<JsonStringLocalizerFactory>();
    }

    [Fact]
    public void AddJsonLocalization_WithFileProviderAndResourcesPath_ConfiguresCorrectly()
    {
        var services = new ServiceCollection();
        var testResourcesPath = Path.Combine(Directory.GetCurrentDirectory(), "TestResources");
        var fileProvider = new PhysicalFileProvider(testResourcesPath);

        services.AddJsonLocalization(fileProvider, "CustomPath");

        var serviceProvider = services.BuildServiceProvider();
        var resourceLoader = serviceProvider.GetService<JsonResourceLoader>();
        
        resourceLoader.Should().NotBeNull();
        serviceProvider.GetRequiredService<IFileProvider>().Should().BeSameAs(fileProvider);
    }

    [Fact]
    public void JsonLocalizationOptions_HasCorrectDefaults()
    {
        var options = new JsonLocalizationOptions();

        options.ResourcesPath.Should().BeNull();
        options.ResourcesRelativePath.Should().Be("Resources");
    }

    [Fact]
    public void JsonLocalizationOptions_CanBeConfigured()
    {
        var options = new JsonLocalizationOptions
        {
            ResourcesPath = "/custom/path",
            ResourcesRelativePath = "MyResources",
        };

        options.ResourcesPath.Should().Be("/custom/path");
        options.ResourcesRelativePath.Should().Be("MyResources");
    }

    [Fact]
    public void AddJsonLocalization_IntegrationTest_EndToEnd()
    {
        var services = new ServiceCollection();
        var testResourcesPath = Path.Combine(Directory.GetCurrentDirectory(), "TestResources");
        var fileProvider = new PhysicalFileProvider(testResourcesPath);

        services.AddJsonLocalization(fileProvider, "");

        var serviceProvider = services.BuildServiceProvider();
        
        // Test factory creation
        var factory = serviceProvider.GetRequiredService<IStringLocalizerFactory>();
        var localizer = factory.Create(typeof(TestResource));
        
        // Test basic localization
        var simpleResult = localizer["SimpleMessage"];
        simpleResult.Value.Should().Be("Hello World");
        
        // Test parameterized localization
        var paramResult = localizer["ParameterizedMessage", "World"];
        paramResult.Value.Should().Be("Hello, World!");
        
        // Test typed localizer
        var typedLocalizer = serviceProvider.GetRequiredService<IStringLocalizer<TestResource>>();
        var typedResult = typedLocalizer["SimpleMessage"];
        typedResult.Value.Should().Be("Hello World");
        
        // Test GetAllStrings
        var allStrings = localizer.GetAllStrings(includeParentCultures: true);
        allStrings.Should().NotBeEmpty();
        allStrings.Should().Contain(ls => ls.Name == "SimpleMessage");
    }

    #region Task 5 — Configurable fallback culture DI Tests

    [Fact]
    public void AddJsonLocalization_WithOptions_FallbackCulturePassedToLoader()
    {
        // Wire up DI with "de" fallback via options path; verify loader uses "de", not "en"
        var testResourcesPath = Path.Combine(Directory.GetCurrentDirectory(), "TestResources");
        var services = new ServiceCollection();

        services.AddJsonLocalization(options =>
        {
            options.ResourcesPath = testResourcesPath;
            options.ResourcesRelativePath = "";
            options.FallbackCulture = "de";
        });

        var serviceProvider = services.BuildServiceProvider();
        var loader = serviceProvider.GetRequiredService<JsonResourceLoader>();

        // FallbackTest.de.json = { "K": "de" }, FallbackTest.fr.json = { "Other": "fr" }
        // No FallbackTest.en.json — with "de" fallback, "K" must appear in fr result
        var result = loader.LoadResources("FallbackTest", new System.Globalization.CultureInfo("fr"));
        result.Should().ContainKey("K");
        result["K"].Should().Be("de");
    }

    [Fact]
    public void AddJsonLocalization_WithFileProvider_DefaultFallbackCulture_IsEn()
    {
        var testResourcesPath = Path.Combine(Directory.GetCurrentDirectory(), "TestResources");
        var services = new ServiceCollection();
        var fileProvider = new PhysicalFileProvider(testResourcesPath);

        // File-provider overload without fallbackCulture arg — defaults to "en"
        services.AddJsonLocalization(fileProvider, "");

        var serviceProvider = services.BuildServiceProvider();
        var loader = serviceProvider.GetRequiredService<JsonResourceLoader>();

        // FallbackEnTest.en.json = { "E": "en" }, FallbackEnTest.fr.json = { "F": "fr" }
        var result = loader.LoadResources("FallbackEnTest", new System.Globalization.CultureInfo("fr"));
        result.Should().ContainKey("E");
        result.Should().ContainKey("F");
    }

    [Fact]
    public void AddJsonLocalization_WithFileProvider_CustomFallbackCulture_IsUsed()
    {
        var testResourcesPath = Path.Combine(Directory.GetCurrentDirectory(), "TestResources");
        var services = new ServiceCollection();
        var fileProvider = new PhysicalFileProvider(testResourcesPath);

        services.AddJsonLocalization(fileProvider, "", "de");

        var serviceProvider = services.BuildServiceProvider();
        var loader = serviceProvider.GetRequiredService<JsonResourceLoader>();

        // FallbackTest.de.json = { "K": "de" }; with "de" fallback, fr request gets "K"
        var result = loader.LoadResources("FallbackTest", new System.Globalization.CultureInfo("fr"));
        result.Should().ContainKey("K");
        result["K"].Should().Be("de");
    }

    [Fact]
    public void AddJsonLocalization_WithOptions_NullFallbackCulture_DisablesFallback()
    {
        var testResourcesPath = Path.Combine(Directory.GetCurrentDirectory(), "TestResources");
        var services = new ServiceCollection();

        services.AddJsonLocalization(options =>
        {
            options.ResourcesPath = testResourcesPath;
            options.ResourcesRelativePath = "";
            options.FallbackCulture = null;
        });

        var serviceProvider = services.BuildServiceProvider();
        var loader = serviceProvider.GetRequiredService<JsonResourceLoader>();

        // FallbackEnTest.en.json = { "E": "en" }, FallbackEnTest.fr.json = { "F": "fr" }
        // With null fallback, E must NOT appear when requesting fr
        var result = loader.LoadResources("FallbackEnTest", new System.Globalization.CultureInfo("fr"));
        result.Should().NotContainKey("E");
        result.Should().ContainKey("F");
    }

    #endregion
}