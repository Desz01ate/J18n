// To test how the analyzer works in the actual code.

using System.Globalization;
using J18n;
using J18n.Runner.Localization;
using J18n.Runner.Resources;
using Microsoft.Extensions.FileProviders;

var culture = new CultureInfo("th-TH");

CultureInfo.DefaultThreadCurrentCulture = culture;
CultureInfo.DefaultThreadCurrentUICulture = culture;

var testResourcesPath = Path.Combine(Directory.GetCurrentDirectory());
var fileProvider = new PhysicalFileProvider(testResourcesPath);

var loader = new JsonResourceLoader(fileProvider);
var factory = new JsonStringLocalizerFactory(loader);
var localizer = factory.Create(typeof(MyLocalization));

var programResourceInstance = new ProgramResource();
var myRandomClassInstance = new MyRandomClass();
var myLocalizationInstance = new MyLocalization();

Console.WriteLine(localizer["Hello"]);