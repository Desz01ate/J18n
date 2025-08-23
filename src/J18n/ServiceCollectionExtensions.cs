using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Localization;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

using J18n;

/// <summary>
/// Provides extension methods for <see cref="IServiceCollection"/> to register JSON-based localization services.
/// These extensions enable easy integration of J18n into the dependency injection container.
/// </summary>
/// <remarks>
/// <para>
/// The extension methods in this class provide multiple ways to configure JSON-based localization:
/// <list type="bullet">
/// <item><description><see cref="AddJsonLocalization(IServiceCollection)"/> - Simple registration with default options</description></item>
/// <item><description><see cref="AddJsonLocalization(IServiceCollection, Action{JsonLocalizationOptions})"/> - Registration with configuration options</description></item>
/// <item><description><see cref="AddJsonLocalization(IServiceCollection, IFileProvider, string)"/> - Registration with custom file provider</description></item>
/// </list>
/// </para>
/// <para>
/// All registration methods configure the following services:
/// <list type="bullet">
/// <item><description><see cref="IFileProvider"/> - For accessing resource files (singleton)</description></item>
/// <item><description><see cref="JsonResourceLoader"/> - For loading and caching resources (singleton)</description></item>
/// <item><description><see cref="IStringLocalizerFactory"/> - Factory for creating localizers (transient)</description></item>
/// <item><description><see cref="IStringLocalizer{T}"/> - Generic localizer interface (transient)</description></item>
/// </list>
/// </para>
/// </remarks>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds JSON-based localization services to the service collection with default configuration.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <returns>The same service collection for method chaining.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="services"/> is null.
    /// </exception>
    /// <remarks>
    /// <para>
    /// This method registers JSON localization services with default settings:
    /// <list type="bullet">
    /// <item><description>Resources directory: "Resources" (relative to current directory)</description></item>
    /// <item><description>File provider: <see cref="PhysicalFileProvider"/> pointing to current directory</description></item>
    /// <item><description>All core localization services configured for dependency injection</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// This is the simplest way to add JSON localization to your application.
    /// For more control over configuration, use the overload that accepts <see cref="JsonLocalizationOptions"/>.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // In Program.cs or Startup.cs
    /// services.AddJsonLocalization();
    /// 
    /// // Then inject localizers in your services
    /// public class MyService
    /// {
    ///     public MyService(IStringLocalizer&lt;MyResource&gt; localizer) { ... }
    /// }
    /// </code>
    /// </example>
    public static IServiceCollection AddJsonLocalization(this IServiceCollection services)
    {
        return services.AddJsonLocalization(_ => { });
    }

    /// <summary>
    /// Adds JSON-based localization services to the service collection with custom configuration.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="setupAction">An action to configure the localization options.</param>
    /// <returns>The same service collection for method chaining.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="services"/> or <paramref name="setupAction"/> is null.
    /// </exception>
    /// <remarks>
    /// <para>
    /// This method allows you to customize the JSON localization configuration through the
    /// <see cref="JsonLocalizationOptions"/> parameter. You can specify custom resource paths
    /// and other configuration settings.
    /// </para>
    /// <para>
    /// The method registers all necessary services including:
    /// <list type="bullet">
    /// <item><description>A configured <see cref="IFileProvider"/> based on the options</description></item>
    /// <item><description>A <see cref="JsonResourceLoader"/> with custom resource path</description></item>
    /// <item><description>The <see cref="IStringLocalizerFactory"/> implementation</description></item>
    /// <item><description>Generic <see cref="IStringLocalizer{T}"/> services</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// services.AddJsonLocalization(options =>
    /// {
    ///     options.ResourcesPath = "/app/localization";
    ///     options.ResourcesRelativePath = "Messages";
    /// });
    /// 
    /// // Or configure just the relative path
    /// services.AddJsonLocalization(options =>
    /// {
    ///     options.ResourcesRelativePath = "Localization/Resources";
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddJsonLocalization(this IServiceCollection services, Action<JsonLocalizationOptions> setupAction)
    {
        ArgumentNullException.ThrowIfNull(services);

        ArgumentNullException.ThrowIfNull(setupAction);

        var options = new JsonLocalizationOptions();
        setupAction(options);

        services.Configure(setupAction);

        services.TryAddSingleton<IFileProvider>(
            _ =>
                !string.IsNullOrEmpty(options.ResourcesPath)
                    ? new PhysicalFileProvider(options.ResourcesPath)
                    : new PhysicalFileProvider(Directory.GetCurrentDirectory()));

        services.TryAddSingleton<JsonResourceLoader>(provider =>
        {
            var fileProvider = provider.GetRequiredService<IFileProvider>();
            return new JsonResourceLoader(fileProvider, options.ResourcesRelativePath);
        });

        services.TryAddTransient<IStringLocalizerFactory, JsonStringLocalizerFactory>();

        services.Add(ServiceDescriptor.Transient(typeof(IStringLocalizer<>), typeof(JsonStringLocalizer<>)));

        return services;
    }

    /// <summary>
    /// Adds JSON-based localization services to the service collection with a custom file provider.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="fileProvider">The file provider to use for accessing resource files.</param>
    /// <param name="resourcesPath">The relative path within the file provider where resource files are located. Defaults to "Resources".</param>
    /// <returns>The same service collection for method chaining.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="services"/> or <paramref name="fileProvider"/> is null.
    /// </exception>
    /// <remarks>
    /// <para>
    /// This overload is useful when you need to provide a custom <see cref="IFileProvider"/> implementation,
    /// such as an embedded file provider, a custom file system provider, or a provider that accesses
    /// resources from a specific location.
    /// </para>
    /// <para>
    /// The method registers the provided file provider as a singleton and configures all localization
    /// services to use it for resource access. This gives you complete control over how and where
    /// resource files are loaded from.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Use embedded resources
    /// var embeddedProvider = new EmbeddedFileProvider(Assembly.GetExecutingAssembly());
    /// services.AddJsonLocalization(embeddedProvider, "Resources.Localization");
    /// 
    /// // Use a custom directory
    /// var customProvider = new PhysicalFileProvider("/custom/path");
    /// services.AddJsonLocalization(customProvider, "Messages");
    /// 
    /// // Use memory provider (for testing)
    /// var memoryProvider = new InMemoryFileProvider();
    /// services.AddJsonLocalization(memoryProvider);
    /// </code>
    /// </example>
    public static IServiceCollection AddJsonLocalization(this IServiceCollection services, IFileProvider fileProvider, string? resourcesPath = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        ArgumentNullException.ThrowIfNull(fileProvider);

        services.TryAddSingleton(fileProvider);
        services.TryAddSingleton(new JsonResourceLoader(fileProvider, resourcesPath ?? "Resources"));
        services.TryAddTransient<IStringLocalizerFactory, JsonStringLocalizerFactory>();
        services.Add(ServiceDescriptor.Transient(typeof(IStringLocalizer<>), typeof(JsonStringLocalizer<>)));

        return services;
    }
}

public class JsonLocalizationOptions
{
    public string? ResourcesPath { get; set; }

    public string ResourcesRelativePath { get; set; } = "Resources";
}