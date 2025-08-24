// To test how the analyzer works in the actual code.

using System.Globalization;
using J18n;
using J18n.Runner.Resources;
using Microsoft.Extensions.FileProviders;

var culture = new CultureInfo("th-TH");

CultureInfo.DefaultThreadCurrentCulture = culture;
CultureInfo.DefaultThreadCurrentUICulture = culture;

var testResourcesPath = Path.Combine(Directory.GetCurrentDirectory());
var fileProvider = new PhysicalFileProvider(testResourcesPath);

var loader = new JsonResourceLoader(fileProvider);
var factory = new JsonStringLocalizerFactory(loader);
var localizer = factory.Create(typeof(MyRandomClass));

Console.WriteLine(localizer[MyRandomClass.Rolling.In_.The.Deep.Like]);