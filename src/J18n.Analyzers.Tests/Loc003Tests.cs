namespace J18n.Analyzers.Tests;

using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using NUnit.Framework;
using Verifier;

[TestFixture]
public class Loc003Tests
{
    private const string SourceTemplate =
        """
        using Microsoft.Extensions.Localization;

        public class TestClass
        {
            private readonly IStringLocalizer<TestClass> _localizer;

            public TestClass(IStringLocalizer<TestClass> localizer)
            {
                _localizer = localizer;
            }

            public void TestMethod()
            {
                var value = _localizer["$key"];
            }
        }
        """;

    private static string GetFormattedSource(string key)
    {
        return SourceTemplate.Replace("$key", key);
    }

    [Test]
    public async Task PartialMissingKey_Produces_LOC003_Warning()
    {
        var source = GetFormattedSource("user.name");

        var additionalFiles = new AdditionalFile[]
        {
            new("TestClass.en.json", "{ \"user\": { \"name\": \"Name\" } }"),
            new("TestClass.fr.json", "{ \"user\": { \"email\": \"Email\" } }"),
        };

        var verifier = new CodeVerifier(source, additionalFiles);

        await verifier.VerifyDiagnostic(
            DiagnosticResult.Create(
                Diagnostics.PartialMissingKey.Id,
                DiagnosticSeverity.Warning,
                14,
                32,
                "Localization key 'user.name' is missing in cultures: fr"));
    }

    [Test]
    public async Task MultipleKeys_MissingInDifferentCultures_ProducesLOC003_ForEach()
    {
        const string source =
            """
            using Microsoft.Extensions.Localization;

            public class TestClass
            {
                private readonly IStringLocalizer<TestClass> _localizer;

                public TestClass(IStringLocalizer<TestClass> localizer)
                {
                    _localizer = localizer;
                }

                public void TestMethod()
                {
                    var userName = _localizer["user.name"];
                    var userEmail = _localizer["user.email"];
                    var settings = _localizer["app.settings.title"];
                }
            }
            """;

        var additionalFiles = new AdditionalFile[]
        {
            new("TestClass.en.json",
                "{ \"user\": { \"name\": \"Name\", \"email\": \"Email\" }, \"app\": { \"settings\": { \"title\": \"Settings\" } } }"),
            new("TestClass.fr.json",
                "{ \"user\": { \"name\": \"Nom\" }, \"app\": { \"settings\": { \"title\": \"Paramètres\" } } }"),
            new("TestClass.de.json",
                "{ \"user\": { \"email\": \"E-Mail\" }, \"app\": { \"settings\": { \"title\": \"Einstellungen\" } } }"),
        };

        var verifier = new CodeVerifier(source, additionalFiles);
        var diagnostics = await verifier.GetDiagnosticsAsync();

        var partialMissingDiagnostics = diagnostics.Where(d => d.Id == Diagnostics.PartialMissingKey.Id).ToList();
        Assert.That(partialMissingDiagnostics.Count, Is.EqualTo(2),
            "Expected 2 LOC003 warnings: user.email missing in fr, user.name missing in de");
    }

    [Test]
    public async Task KeyExists_InMajority_MissingInMinority_ProducesLOC003()
    {
        var source = GetFormattedSource("common.button.save");

        var additionalFiles = new AdditionalFile[]
        {
            new("TestClass.en.json", "{ \"common\": { \"button\": { \"save\": \"Save\" } } }"),
            new("TestClass.fr.json", "{ \"common\": { \"button\": { \"save\": \"Sauvegarder\" } } }"),
            new("TestClass.de.json", "{ \"common\": { \"button\": { \"save\": \"Speichern\" } } }"),
            new("TestClass.es.json", "{ \"common\": { \"button\": { \"save\": \"Guardar\" } } }"),
            new("TestClass.it.json", "{ \"common\": { \"button\": { \"cancel\": \"Annulla\" } } }"), // Missing save
        };

        var verifier = new CodeVerifier(source, additionalFiles);

        await verifier.VerifyDiagnostic(
            DiagnosticResult.Create(
                Diagnostics.PartialMissingKey.Id,
                DiagnosticSeverity.Warning,
                14,
                32,
                "Localization key 'common.button.save' is missing in cultures: it"));
    }

    [Test]
    public async Task KeyExists_InMinority_MissingInMajority_ProducesLOC003()
    {
        var source = GetFormattedSource("rare.feature.title");

        var additionalFiles = new AdditionalFile[]
        {
            new("TestClass.en.json", "{ \"common\": { \"title\": \"Common\" } }"),
            new("TestClass.fr.json", "{ \"common\": { \"title\": \"Commun\" } }"),
            new("TestClass.de.json", "{ \"rare\": { \"feature\": { \"title\": \"Seltenes Feature\" } } }"), // Only one has it
            new("TestClass.es.json", "{ \"common\": { \"title\": \"Común\" } }"),
        };

        var verifier = new CodeVerifier(source, additionalFiles);
        var diagnostics = await verifier.GetDiagnosticsAsync();

        var partialMissingDiagnostics = diagnostics.Where(d => d.Id == Diagnostics.PartialMissingKey.Id).ToList();
        Assert.That(partialMissingDiagnostics.Count, Is.EqualTo(1),
            "Expected 1 LOC003 warning for rare.feature.title missing in most cultures");

        var diagnostic = partialMissingDiagnostics.First();
        Assert.That(diagnostic.GetMessage(), Does.Contain("rare.feature.title"));
        Assert.That(diagnostic.GetMessage(), Does.Contain("missing in cultures:"));
    }

    [Test]
    public async Task ComplexMissingPatterns_AcrossCultures_ProducesMultipleLOC003()
    {
        const string source =
            """
            using Microsoft.Extensions.Localization;

            public class TestClass
            {
                private readonly IStringLocalizer<TestClass> _localizer;

                public TestClass(IStringLocalizer<TestClass> localizer)
                {
                    _localizer = localizer;
                }

                public void TestMethod()
                {
                    var title = _localizer["page.title"];
                    var subtitle = _localizer["page.subtitle"];
                    var footer = _localizer["page.footer"];
                }
            }
            """;

        var additionalFiles = new AdditionalFile[]
        {
            // en: has title, subtitle
            new("TestClass.en.json", "{ \"page\": { \"title\": \"Title\", \"subtitle\": \"Subtitle\" } }"),
            // fr: has title, footer  
            new("TestClass.fr.json", "{ \"page\": { \"title\": \"Titre\", \"footer\": \"Pied de page\" } }"),
            // de: has subtitle, footer
            new("TestClass.de.json", "{ \"page\": { \"subtitle\": \"Untertitel\", \"footer\": \"Fußzeile\" } }"),
        };

        var verifier = new CodeVerifier(source, additionalFiles);
        var diagnostics = await verifier.GetDiagnosticsAsync();

        var partialMissingDiagnostics = diagnostics.Where(d => d.Id == Diagnostics.PartialMissingKey.Id).ToList();
        Assert.That(partialMissingDiagnostics.Count, Is.EqualTo(3),
            "Expected 3 LOC003 warnings: each key is missing from at least one culture");

        // Verify each key has a diagnostic
        var messages = partialMissingDiagnostics.Select(d => d.GetMessage()).ToList();
        Assert.That(messages.Any(m => m.Contains("page.title")), Is.True);
        Assert.That(messages.Any(m => m.Contains("page.subtitle")), Is.True);
        Assert.That(messages.Any(m => m.Contains("page.footer")), Is.True);
    }

    [Test]
    public async Task ThreePlusCultures_VariousMissingPatterns_ProducesLOC003()
    {
        var source = GetFormattedSource("nav.menu.item");

        var additionalFiles = new AdditionalFile[]
        {
            new("TestClass.en.json", "{ \"nav\": { \"menu\": { \"item\": \"Menu Item\" } } }"),
            new("TestClass.fr.json", "{ \"nav\": { \"menu\": { \"item\": \"Élément de menu\" } } }"),
            new("TestClass.de.json", "{ \"nav\": { \"menu\": { \"item\": \"Menüpunkt\" } } }"),
            new("TestClass.es.json", "{ \"nav\": { \"menu\": { \"item\": \"Elemento del menú\" } } }"),
            new("TestClass.it.json", "{ \"nav\": { \"menu\": { \"item\": \"Voce di menu\" } } }"),
            new("TestClass.pt.json", "{ \"nav\": { \"other\": { \"item\": \"Item\" } } }"), // Missing nav.menu.item
            new("TestClass.ru.json", "{ \"nav\": { \"other\": { \"item\": \"Элемент\" } } }"), // Missing nav.menu.item
        };

        var verifier = new CodeVerifier(source, additionalFiles);
        var diagnostics = await verifier.GetDiagnosticsAsync();

        var partialMissingDiagnostics = diagnostics.Where(d => d.Id == Diagnostics.PartialMissingKey.Id).ToList();
        Assert.That(partialMissingDiagnostics.Count, Is.EqualTo(1),
            "Expected 1 LOC003 warning for nav.menu.item missing in pt, ru");

        var diagnostic = partialMissingDiagnostics.First();
        Assert.That(diagnostic.GetMessage(), Does.Contain("nav.menu.item"));
        Assert.That(diagnostic.GetMessage(), Does.Contain("missing in cultures:"));
    }

    [Test]
    public async Task DifferentCultureNamingConventions_ProducesLOC003()
    {
        var source = GetFormattedSource("dialog.confirm");

        var additionalFiles = new AdditionalFile[]
        {
            new("TestClass.en-US.json", "{ \"dialog\": { \"confirm\": \"Confirm\" } }"),
            new("TestClass.en-GB.json", "{ \"dialog\": { \"confirm\": \"Confirm\" } }"),
            new("TestClass.fr-FR.json", "{ \"dialog\": { \"confirm\": \"Confirmer\" } }"),
            new("TestClass.de-DE.json", "{ \"dialog\": { \"confirm\": \"Bestätigen\" } }"),
            new("TestClass.zh-CN.json", "{ \"dialog\": { \"cancel\": \"取消\" } }"), // Missing confirm
        };

        var verifier = new CodeVerifier(source, additionalFiles);

        await verifier.VerifyDiagnostic(
            DiagnosticResult.Create(
                Diagnostics.PartialMissingKey.Id,
                DiagnosticSeverity.Warning,
                14,
                32,
                "Localization key 'dialog.confirm' is missing in cultures: zh-cn"));
    }

    [Test]
    public async Task CultureNameCaseSensitivity_ProducesLOC003()
    {
        var source = GetFormattedSource("form.submit");

        var additionalFiles = new AdditionalFile[]
        {
            new("TestClass.EN.json", "{ \"form\": { \"submit\": \"Submit\" } }"),
            new("TestClass.Fr.json", "{ \"form\": { \"submit\": \"Soumettre\" } }"),
            new("TestClass.DE.json", "{ \"form\": { \"submit\": \"Absenden\" } }"),
            new("TestClass.es.json", "{ \"form\": { \"cancel\": \"Cancelar\" } }"), // Missing submit
        };

        var verifier = new CodeVerifier(source, additionalFiles);
        var diagnostics = await verifier.GetDiagnosticsAsync();

        var partialMissingDiagnostics = diagnostics.Where(d => d.Id == Diagnostics.PartialMissingKey.Id).ToList();
        Assert.That(partialMissingDiagnostics.Count, Is.EqualTo(1),
            "Expected 1 LOC003 warning despite culture name case differences");

        var diagnostic = partialMissingDiagnostics.First();
        Assert.That(diagnostic.GetMessage(), Does.Contain("form.submit"));
    }

    [Test]
    public async Task DefaultCulture_VsRealCultures_HandlingInLOC003()
    {
        var source = GetFormattedSource("error.message");

        var additionalFiles = new AdditionalFile[]
        {
            new("TestClass.json", "{ \"error\": { \"message\": \"Default Error\" } }"), // Default culture
            new("TestClass.en.json", "{ \"error\": { \"message\": \"Error\" } }"),
            new("TestClass.fr.json", "{ \"error\": { \"message\": \"Erreur\" } }"),
            new("TestClass.de.json", "{ \"error\": { \"title\": \"Fehler\" } }"), // Missing message
        };

        var verifier = new CodeVerifier(source, additionalFiles);
        var diagnostics = await verifier.GetDiagnosticsAsync();

        var partialMissingDiagnostics = diagnostics.Where(d => d.Id == Diagnostics.PartialMissingKey.Id).ToList();

        // Should produce LOC003 because the "default" culture is filtered out when real cultures exist
        Assert.That(partialMissingDiagnostics.Count, Is.EqualTo(1),
            "Expected 1 LOC003 warning with default culture filtered out");
    }

    [Test]
    public async Task ParentExists_ChildMissingInSomeCultures_ProducesLOC003()
    {
        var source = GetFormattedSource("ui.buttons.primary.label");

        var additionalFiles = new AdditionalFile[]
        {
            new("TestClass.en.json", "{ \"ui\": { \"buttons\": { \"primary\": { \"label\": \"Primary\", \"tooltip\": \"Click me\" } } } }"),
            new("TestClass.fr.json", "{ \"ui\": { \"buttons\": { \"primary\": { \"tooltip\": \"Cliquez-moi\" } } } }"), // Missing label
            new("TestClass.de.json", "{ \"ui\": { \"buttons\": { \"primary\": { \"label\": \"Primär\", \"tooltip\": \"Klick mich\" } } } }"),
        };

        var verifier = new CodeVerifier(source, additionalFiles);

        await verifier.VerifyDiagnostic(
            DiagnosticResult.Create(
                Diagnostics.PartialMissingKey.Id,
                DiagnosticSeverity.Warning,
                14,
                32,
                "Localization key 'ui.buttons.primary.label' is missing in cultures: fr"));
    }

    [Test]
    public async Task ChildExists_ParentStructureMissingInSomeCultures_ProducesLOC003()
    {
        var source = GetFormattedSource("settings.advanced.security.timeout");

        var additionalFiles = new AdditionalFile[]
        {
            new("TestClass.en.json", "{ \"settings\": { \"advanced\": { \"security\": { \"timeout\": \"300\" } } } }"),
            new("TestClass.fr.json", "{ \"settings\": { \"basic\": { \"language\": \"fr\" } } }"), // Missing entire advanced structure
            new("TestClass.de.json", "{ \"settings\": { \"advanced\": { \"security\": { \"timeout\": \"300\" } } } }"),
        };

        var verifier = new CodeVerifier(source, additionalFiles);
        var diagnostics = await verifier.GetDiagnosticsAsync();

        var partialMissingDiagnostics = diagnostics.Where(d => d.Id == Diagnostics.PartialMissingKey.Id).ToList();
        Assert.That(partialMissingDiagnostics.Count, Is.EqualTo(1),
            "Expected 1 LOC003 warning for settings.advanced.security.timeout missing in fr");

        var diagnostic = partialMissingDiagnostics.First();
        Assert.That(diagnostic.GetMessage(), Does.Contain("settings.advanced.security.timeout"));
    }

    [Test]
    public async Task DeepNesting_PartialMissingAtDifferentLevels_ProducesMultipleLOC003()
    {
        const string source =
            """
            using Microsoft.Extensions.Localization;

            public class TestClass
            {
                private readonly IStringLocalizer<TestClass> _localizer;

                public TestClass(IStringLocalizer<TestClass> localizer)
                {
                    _localizer = localizer;
                }

                public void TestMethod()
                {
                    var level1 = _localizer["app.config.database.host"];
                    var level2 = _localizer["app.config.database.port"];
                    var level3 = _localizer["app.config.cache.redis.url"];
                }
            }
            """;

        var additionalFiles = new AdditionalFile[]
        {
            // en: has all nested keys
            new("TestClass.en.json", """
                                     {
                                       "app": {
                                         "config": {
                                           "database": {
                                             "host": "localhost",
                                             "port": "5432"
                                           },
                                           "cache": {
                                             "redis": {
                                               "url": "redis://localhost"
                                             }
                                           }
                                         }
                                       }
                                     }
                                     """),
            // fr: missing database.port and cache.redis.url
            new("TestClass.fr.json", """
                                     {
                                       "app": {
                                         "config": {
                                           "database": {
                                             "host": "localhost"
                                           },
                                           "cache": {
                                             "enabled": true
                                           }
                                         }
                                       }
                                     }
                                     """),
            // de: missing entire cache structure
            new("TestClass.de.json", """
                                     {
                                       "app": {
                                         "config": {
                                           "database": {
                                             "host": "localhost",
                                             "port": "5432"
                                           }
                                         }
                                       }
                                     }
                                     """),
        };

        var verifier = new CodeVerifier(source, additionalFiles);
        var diagnostics = await verifier.GetDiagnosticsAsync();

        var partialMissingDiagnostics = diagnostics.Where(d => d.Id == Diagnostics.PartialMissingKey.Id).ToList();
        Assert.That(partialMissingDiagnostics.Count, Is.EqualTo(2),
            "Expected 2 LOC003 warnings for different missing nested keys");
    }

    [Test]
    public async Task MixedFlatNested_AcrossCultures_ProducesLOC003()
    {
        var source = GetFormattedSource("notification.email.subject");

        var additionalFiles = new AdditionalFile[]
        {
            // en: nested structure
            new("TestClass.en.json", "{ \"notification\": { \"email\": { \"subject\": \"Subject\", \"body\": \"Body\" } } }"),
            // fr: flat structure with dots - should still work due to flattening
            new("TestClass.fr.json", "{ \"notification.email.subject\": \"Sujet\", \"notification.email.body\": \"Corps\" }"),
            // de: missing the key entirely
            new("TestClass.de.json", "{ \"notification\": { \"sms\": { \"message\": \"Nachricht\" } } }"),
        };

        var verifier = new CodeVerifier(source, additionalFiles);

        await verifier.VerifyDiagnostic(
            DiagnosticResult.Create(
                Diagnostics.PartialMissingKey.Id,
                DiagnosticSeverity.Warning,
                14,
                32,
                "Localization key 'notification.email.subject' is missing in cultures: de"));
    }

    [Test]
    public async Task SiblingKeys_DifferentMissingPatterns_ProducesIndividualLOC003()
    {
        const string source =
            """
            using Microsoft.Extensions.Localization;

            public class TestClass
            {
                private readonly IStringLocalizer<TestClass> _localizer;

                public TestClass(IStringLocalizer<TestClass> localizer)
                {
                    _localizer = localizer;
                }

                public void TestMethod()
                {
                    var firstName = _localizer["profile.name.first"];
                    var lastName = _localizer["profile.name.last"];
                    var middleName = _localizer["profile.name.middle"];
                }
            }
            """;

        var additionalFiles = new AdditionalFile[]
        {
            // en: has first, last, middle
            new("TestClass.en.json", "{ \"profile\": { \"name\": { \"first\": \"First\", \"last\": \"Last\", \"middle\": \"Middle\" } } }"),
            // fr: has first, last (missing middle)
            new("TestClass.fr.json", "{ \"profile\": { \"name\": { \"first\": \"Prénom\", \"last\": \"Nom\" } } }"),
            // de: has first, middle (missing last)
            new("TestClass.de.json", "{ \"profile\": { \"name\": { \"first\": \"Vorname\", \"middle\": \"Zweiter Vorname\" } } }"),
        };

        var verifier = new CodeVerifier(source, additionalFiles);
        var diagnostics = await verifier.GetDiagnosticsAsync();

        var partialMissingDiagnostics = diagnostics.Where(d => d.Id == Diagnostics.PartialMissingKey.Id).ToList();
        Assert.That(partialMissingDiagnostics.Count, Is.EqualTo(2),
            "Expected 2 LOC003 warnings: middle missing in fr, last missing in de");

        var messages = partialMissingDiagnostics.Select(d => d.GetMessage()).ToList();
        Assert.That(messages.Any(m => m.Contains("profile.name.middle")), Is.True);
        Assert.That(messages.Any(m => m.Contains("profile.name.last")), Is.True);
    }

    [Test]
    public async Task RootLevelKeys_PartialMissing_ProducesLOC003()
    {
        var source = GetFormattedSource("appName");

        var additionalFiles = new AdditionalFile[]
        {
            new("TestClass.en.json", "{ \"appName\": \"My App\", \"version\": \"1.0\" }"),
            new("TestClass.fr.json", "{ \"appName\": \"Mon App\", \"version\": \"1.0\" }"),
            new("TestClass.de.json", "{ \"version\": \"1.0\" }"), // Missing appName at root level
        };

        var verifier = new CodeVerifier(source, additionalFiles);

        await verifier.VerifyDiagnostic(
            DiagnosticResult.Create(
                Diagnostics.PartialMissingKey.Id,
                DiagnosticSeverity.Warning,
                14,
                32,
                "Localization key 'appName' is missing in cultures: de"));
    }

    [Test]
    public async Task MixedDataTypes_PartialMissing_ProducesLOC003()
    {
        const string source =
            """
            using Microsoft.Extensions.Localization;

            public class TestClass
            {
                private readonly IStringLocalizer<TestClass> _localizer;

                public TestClass(IStringLocalizer<TestClass> localizer)
                {
                    _localizer = localizer;
                }

                public void TestMethod()
                {
                    var timeout = _localizer["config.timeout"];
                    var enabled = _localizer["config.enabled"];
                    var count = _localizer["config.maxRetries"];
                }
            }
            """;

        var additionalFiles = new AdditionalFile[]
        {
            // en: has string, boolean, number
            new("TestClass.en.json", "{ \"config\": { \"timeout\": \"30\", \"enabled\": true, \"maxRetries\": 5 } }"),
            // fr: missing the boolean value
            new("TestClass.fr.json", "{ \"config\": { \"timeout\": \"30\", \"maxRetries\": 5 } }"),
            // de: missing the number value
            new("TestClass.de.json", "{ \"config\": { \"timeout\": \"30\", \"enabled\": true } }"),
        };

        var verifier = new CodeVerifier(source, additionalFiles);
        var diagnostics = await verifier.GetDiagnosticsAsync();

        var partialMissingDiagnostics = diagnostics.Where(d => d.Id == Diagnostics.PartialMissingKey.Id).ToList();
        Assert.That(partialMissingDiagnostics.Count, Is.EqualTo(2),
            "Expected 2 LOC003 warnings: config.enabled missing in fr, config.maxRetries missing in de");

        var messages = partialMissingDiagnostics.Select(d => d.GetMessage()).ToList();
        Assert.That(messages.Any(m => m.Contains("config.enabled")), Is.True);
        Assert.That(messages.Any(m => m.Contains("config.maxRetries")), Is.True);
    }

    [Test]
    public async Task EmptyObjects_VsMissingKeys_ProducesLOC003()
    {
        var source = GetFormattedSource("feature.dashboard.title");

        var additionalFiles = new AdditionalFile[]
        {
            new("TestClass.en.json", "{ \"feature\": { \"dashboard\": { \"title\": \"Dashboard\", \"subtitle\": \"Overview\" } } }"),
            new("TestClass.fr.json", "{ \"feature\": { \"dashboard\": {} } }"), // Empty object - key is missing
            new("TestClass.de.json", "{ \"feature\": { \"other\": { \"title\": \"Andere\" } } }"), // Missing dashboard entirely
            new("TestClass.es.json", "{ \"feature\": { \"dashboard\": { \"subtitle\": \"Vista general\" } } }"), // Missing title
        };

        var verifier = new CodeVerifier(source, additionalFiles);
        var diagnostics = await verifier.GetDiagnosticsAsync();

        var partialMissingDiagnostics = diagnostics.Where(d => d.Id == Diagnostics.PartialMissingKey.Id).ToList();
        Assert.That(partialMissingDiagnostics.Count, Is.EqualTo(1),
            "Expected 1 LOC003 warning for feature.dashboard.title missing in multiple cultures");

        var diagnostic = partialMissingDiagnostics.First();
        Assert.That(diagnostic.GetMessage(), Does.Contain("feature.dashboard.title"));
        Assert.That(diagnostic.GetMessage(), Does.Contain("missing in cultures:"));
        // Should contain fr, de, es as missing cultures
        var message = diagnostic.GetMessage();
        Assert.That(message.Contains("fr") || message.Contains("de") || message.Contains("es"), Is.True);
    }

    [Test]
    public async Task SpecialCharacters_InKeys_PartialMissing_ProducesLOC003()
    {
        const string source =
            """
            using Microsoft.Extensions.Localization;

            public class TestClass
            {
                private readonly IStringLocalizer<TestClass> _localizer;

                public TestClass(IStringLocalizer<TestClass> localizer)
                {
                    _localizer = localizer;
                }

                public void TestMethod()
                {
                    var percent = _localizer["math.symbols.%"];
                    var at = _localizer["email.symbols.@"];
                    var dollar = _localizer["currency.symbols.$"];
                }
            }
            """;

        var additionalFiles = new AdditionalFile[]
        {
            new("TestClass.en.json", "{ \"math\": { \"symbols\": { \"%\": \"percent\" } }, \"email\": { \"symbols\": { \"@\": \"at\" } }, \"currency\": { \"symbols\": { \"$\": \"dollar\" } } }"),
            new("TestClass.fr.json", "{ \"math\": { \"symbols\": { \"%\": \"pourcent\" } }, \"email\": { \"symbols\": { \"@\": \"arobase\" } } }"), // Missing $
            new("TestClass.de.json", "{ \"math\": { \"symbols\": { \"%\": \"Prozent\" } }, \"currency\": { \"symbols\": { \"$\": \"Dollar\" } } }"), // Missing @
        };

        var verifier = new CodeVerifier(source, additionalFiles);
        var diagnostics = await verifier.GetDiagnosticsAsync();

        var partialMissingDiagnostics = diagnostics.Where(d => d.Id == Diagnostics.PartialMissingKey.Id).ToList();
        Assert.That(partialMissingDiagnostics.Count, Is.EqualTo(2),
            "Expected 2 LOC003 warnings for special character keys");

        var messages = partialMissingDiagnostics.Select(d => d.GetMessage()).ToList();
        Assert.That(messages.Any(m => m.Contains("currency.symbols.$")), Is.True);
        Assert.That(messages.Any(m => m.Contains("email.symbols.@")), Is.True);
    }

    [Test]
    public async Task UnicodeKeys_PartialMissing_ProducesLOC003()
    {
        const string source =
            """
            using Microsoft.Extensions.Localization;

            public class TestClass
            {
                private readonly IStringLocalizer<TestClass> _localizer;

                public TestClass(IStringLocalizer<TestClass> localizer)
                {
                    _localizer = localizer;
                }

                public void TestMethod()
                {
                    var emoji = _localizer["ui.icons.😊"];
                    var chinese = _localizer["text.greeting.你好"];
                    var arabic = _localizer["text.greeting.مرحبا"];
                }
            }
            """;

        var additionalFiles = new AdditionalFile[]
        {
            new("TestClass.en.json", "{ \"ui\": { \"icons\": { \"😊\": \"smile\" } }, \"text\": { \"greeting\": { \"你好\": \"hello\", \"مرحبا\": \"welcome\" } } }"),
            new("TestClass.fr.json", "{ \"ui\": { \"icons\": { \"😊\": \"sourire\" } }, \"text\": { \"greeting\": { \"你好\": \"bonjour\" } } }"), // Missing مرحبا
            new("TestClass.de.json", "{ \"ui\": { \"icons\": { \"😊\": \"lächeln\" } }, \"text\": { \"greeting\": { \"مرحبا\": \"willkommen\" } } }"), // Missing 你好
        };

        var verifier = new CodeVerifier(source, additionalFiles);
        var diagnostics = await verifier.GetDiagnosticsAsync();

        var partialMissingDiagnostics = diagnostics.Where(d => d.Id == Diagnostics.PartialMissingKey.Id).ToList();
        Assert.That(partialMissingDiagnostics.Count, Is.EqualTo(2),
            "Expected 2 LOC003 warnings for Unicode keys");

        var messages = partialMissingDiagnostics.Select(d => d.GetMessage()).ToList();
        Assert.That(messages.Any(m => m.Contains("text.greeting.مرحبا")), Is.True);
        Assert.That(messages.Any(m => m.Contains("text.greeting.你好")), Is.True);
    }

    [Test]
    public async Task VeryLongKeyNames_PartialMissing_ProducesLOC003()
    {
        var longKey = "very.deeply.nested.structure.with.many.levels.that.goes.on.and.on.until.it.becomes.quite.long.indeed.final.key";
        var source = GetFormattedSource(longKey);

        var additionalFiles = new AdditionalFile[]
        {
            new("TestClass.en.json",
                "{ \"very\": { \"deeply\": { \"nested\": { \"structure\": { \"with\": { \"many\": { \"levels\": { \"that\": { \"goes\": { \"on\": { \"and\": { \"on\": { \"until\": { \"it\": { \"becomes\": { \"quite\": { \"long\": { \"indeed\": { \"final\": { \"key\": \"Long Key Value\" } } } } } } } } } } } } } } } } } } }"),
            new("TestClass.fr.json",
                "{ \"very\": { \"deeply\": { \"nested\": { \"structure\": { \"with\": { \"many\": { \"levels\": { \"that\": { \"goes\": { \"on\": { \"and\": { \"on\": { \"until\": { \"it\": { \"becomes\": { \"quite\": { \"long\": { \"indeed\": { \"final\": { \"key\": \"Valeur de clé longue\" } } } } } } } } } } } } } } } } } } }"),
            new("TestClass.de.json",
                "{ \"very\": { \"deeply\": { \"nested\": { \"other\": { \"path\": \"Different path\" } } } } }"), // Missing the long key
        };

        var verifier = new CodeVerifier(source, additionalFiles);

        await verifier.VerifyDiagnostic(
            DiagnosticResult.Create(
                Diagnostics.PartialMissingKey.Id,
                DiagnosticSeverity.Warning,
                14,
                32,
                $"Localization key '{longKey}' is missing in cultures: de"));
    }

    [Test]
    public async Task KeysLookingLikePathsOrUrls_PartialMissing_ProducesLOC003()
    {
        const string source =
            """
            using Microsoft.Extensions.Localization;

            public class TestClass
            {
                private readonly IStringLocalizer<TestClass> _localizer;

                public TestClass(IStringLocalizer<TestClass> localizer)
                {
                    _localizer = localizer;
                }

                public void TestMethod()
                {
                    var filePath = _localizer["resources.files./path/to/file.txt"];
                    var url = _localizer["api.endpoints.https://api.example.com/data"];
                    var email = _localizer["contacts.support.email@example.com"];
                }
            }
            """;

        var additionalFiles = new AdditionalFile[]
        {
            new("TestClass.en.json",
                "{ \"resources\": { \"files\": { \"/path/to/file.txt\": \"File Path\" } }, \"api\": { \"endpoints\": { \"https://api.example.com/data\": \"Data API\" } }, \"contacts\": { \"support\": { \"email@example.com\": \"Support Email\" } } }"),
            new("TestClass.fr.json",
                "{ \"resources\": { \"files\": { \"/path/to/file.txt\": \"Chemin du fichier\" } }, \"api\": { \"endpoints\": { \"https://api.example.com/data\": \"API de données\" } } }"), // Missing email
            new("TestClass.de.json",
                "{ \"resources\": { \"files\": { \"/path/to/file.txt\": \"Dateipfad\" } }, \"contacts\": { \"support\": { \"email@example.com\": \"Support E-Mail\" } } }"), // Missing URL
        };

        var verifier = new CodeVerifier(source, additionalFiles);
        var diagnostics = await verifier.GetDiagnosticsAsync();

        var partialMissingDiagnostics = diagnostics.Where(d => d.Id == Diagnostics.PartialMissingKey.Id).ToList();
        Assert.That(partialMissingDiagnostics.Count, Is.EqualTo(2),
            "Expected 2 LOC003 warnings for path/URL-like keys");

        var messages = partialMissingDiagnostics.Select(d => d.GetMessage()).ToList();
        Assert.That(messages.Any(m => m.Contains("contacts.support.email@example.com")), Is.True);
        Assert.That(messages.Any(m => m.Contains("api.endpoints.https://api.example.com/data")), Is.True);
    }

    [Test]
    public async Task SameKey_UsedMultipleTimes_WithPartialMissing_ProducesOneLOC003()
    {
        const string source =
            """
            using Microsoft.Extensions.Localization;

            public class TestClass
            {
                private readonly IStringLocalizer<TestClass> _localizer;

                public TestClass(IStringLocalizer<TestClass> localizer)
                {
                    _localizer = localizer;
                }

                public void TestMethod()
                {
                    var title1 = _localizer["page.title"];
                    var title2 = _localizer["page.title"]; // Same key used twice
                    var title3 = _localizer["page.title"]; // Same key used thrice
                }

                public void AnotherMethod()
                {
                    var sameTitle = _localizer["page.title"]; // Same key in different method
                }
            }
            """;

        var additionalFiles = new AdditionalFile[]
        {
            new("TestClass.en.json", "{ \"page\": { \"title\": \"Page Title\" } }"),
            new("TestClass.fr.json", "{ \"page\": { \"title\": \"Titre de la page\" } }"),
            new("TestClass.de.json", "{ \"page\": { \"subtitle\": \"Untertitel\" } }"), // Missing title
        };

        var verifier = new CodeVerifier(source, additionalFiles);
        var diagnostics = await verifier.GetDiagnosticsAsync();

        var partialMissingDiagnostics = diagnostics.Where(d => d.Id == Diagnostics.PartialMissingKey.Id).ToList();
        Assert.That(partialMissingDiagnostics.Count, Is.EqualTo(4),
            "Expected 4 LOC003 warnings (one per usage location) even though it's the same key");

        var diagnostic = partialMissingDiagnostics.First();
        Assert.That(diagnostic.GetMessage(), Does.Contain("page.title"));
        Assert.That(diagnostic.GetMessage(), Does.Contain("missing in cultures: de"));
    }

    [Test]
    public async Task MultipleKeys_PartialMissing_InSameFile_ProducesMultipleLOC003()
    {
        const string source =
            """
            using Microsoft.Extensions.Localization;

            public class TestClass
            {
                private readonly IStringLocalizer<TestClass> _localizer;

                public TestClass(IStringLocalizer<TestClass> localizer)
                {
                    _localizer = localizer;
                }

                public void Method1()
                {
                    var header = _localizer["ui.header.title"];
                    var footer = _localizer["ui.footer.copyright"];
                }

                public void Method2()
                {
                    var sidebar = _localizer["ui.sidebar.menu"];
                    var modal = _localizer["ui.modal.close"];
                }

                public void Method3()
                {
                    var button = _localizer["ui.button.submit"];
                }
            }
            """;

        var additionalFiles = new AdditionalFile[]
        {
            // en: has all keys
            new("TestClass.en.json",
                "{ \"ui\": { \"header\": { \"title\": \"Header\" }, \"footer\": { \"copyright\": \"Copyright\" }, \"sidebar\": { \"menu\": \"Menu\" }, \"modal\": { \"close\": \"Close\" }, \"button\": { \"submit\": \"Submit\" } } }"),
            // fr: missing footer.copyright and modal.close
            new("TestClass.fr.json",
                "{ \"ui\": { \"header\": { \"title\": \"En-tête\" }, \"sidebar\": { \"menu\": \"Menu\" }, \"button\": { \"submit\": \"Soumettre\" } } }"),
            // de: missing sidebar.menu and button.submit
            new("TestClass.de.json",
                "{ \"ui\": { \"header\": { \"title\": \"Kopfzeile\" }, \"footer\": { \"copyright\": \"Urheberrecht\" }, \"modal\": { \"close\": \"Schließen\" } } }"),
        };

        var verifier = new CodeVerifier(source, additionalFiles);
        var diagnostics = await verifier.GetDiagnosticsAsync();

        var partialMissingDiagnostics = diagnostics.Where(d => d.Id == Diagnostics.PartialMissingKey.Id).ToList();
        Assert.That(partialMissingDiagnostics.Count, Is.EqualTo(4),
            "Expected 4 LOC003 warnings for different keys missing in different cultures");

        // Verify we have diagnostics for all expected missing keys
        var messages = partialMissingDiagnostics.Select(d => d.GetMessage()).ToList();
        var keysMissingInFr = messages.Where(m => m.Contains("fr")).Select(m => m).ToList();
        var keysMissingInDe = messages.Where(m => m.Contains("de")).Select(m => m).ToList();

        Assert.That(keysMissingInFr.Count + keysMissingInDe.Count, Is.GreaterThanOrEqualTo(4),
            "Should have warnings for keys missing in fr and de");
    }

    [Test]
    public async Task DifferentAccessorPatterns_WithPartialMissing_ProducesLOC003()
    {
        const string source =
            """
            using Microsoft.Extensions.Localization;

            public class TestClass
            {
                private readonly IStringLocalizer<TestClass> _localizer;

                public TestClass(IStringLocalizer<TestClass> localizer)
                {
                    _localizer = localizer;
                }

                public void TestIndexerAccess()
                {
                    var indexer1 = _localizer["validation.required"];
                    var indexer2 = _localizer["validation.email"];
                }
                
                public void TestPropertyAccess() 
                {
                    // Note: Property access might not be recognized by the analyzer
                    // This tests the behavior with different access patterns
                    var prop = _localizer["validation.minlength"];
                }
            }
            """;

        var additionalFiles = new AdditionalFile[]
        {
            new("TestClass.en.json", "{ \"validation\": { \"required\": \"Required\", \"email\": \"Invalid email\", \"minlength\": \"Too short\" } }"),
            new("TestClass.fr.json", "{ \"validation\": { \"required\": \"Requis\", \"email\": \"E-mail invalide\" } }"), // Missing minlength
            new("TestClass.de.json", "{ \"validation\": { \"required\": \"Erforderlich\", \"minlength\": \"Zu kurz\" } }"), // Missing email
        };

        var verifier = new CodeVerifier(source, additionalFiles);
        var diagnostics = await verifier.GetDiagnosticsAsync();

        var partialMissingDiagnostics = diagnostics.Where(d => d.Id == Diagnostics.PartialMissingKey.Id).ToList();
        Assert.That(partialMissingDiagnostics.Count, Is.EqualTo(2),
            "Expected 2 LOC003 warnings regardless of accessor pattern");

        var messages = partialMissingDiagnostics.Select(d => d.GetMessage()).ToList();
        Assert.That(messages.Any(m => m.Contains("validation.minlength")), Is.True);
        Assert.That(messages.Any(m => m.Contains("validation.email")), Is.True);
    }

    [Test]
    public async Task KeyExists_InAllCultures_DoesNotProduceLOC003()
    {
        const string source =
            """
            using Microsoft.Extensions.Localization;

            public class TestClass
            {
                private readonly IStringLocalizer<TestClass> _localizer;

                public TestClass(IStringLocalizer<TestClass> localizer)
                {
                    _localizer = localizer;
                }

                public void TestMethod()
                {
                    var title = _localizer["app.title"];
                    var description = _localizer["app.description"];
                    var version = _localizer["app.version"];
                }
            }
            """;

        var additionalFiles = new AdditionalFile[]
        {
            new("TestClass.en.json", "{ \"app\": { \"title\": \"My App\", \"description\": \"A great app\", \"version\": \"1.0\" } }"),
            new("TestClass.fr.json", "{ \"app\": { \"title\": \"Mon App\", \"description\": \"Une super app\", \"version\": \"1.0\" } }"),
            new("TestClass.de.json", "{ \"app\": { \"title\": \"Meine App\", \"description\": \"Eine tolle App\", \"version\": \"1.0\" } }"),
            new("TestClass.es.json", "{ \"app\": { \"title\": \"Mi App\", \"description\": \"Una gran app\", \"version\": \"1.0\" } }"),
        };

        var verifier = new CodeVerifier(source, additionalFiles);
        var diagnostics = await verifier.GetDiagnosticsAsync();

        var partialMissingDiagnostics = diagnostics.Where(d => d.Id == Diagnostics.PartialMissingKey.Id).ToList();
        Assert.That(partialMissingDiagnostics.Count, Is.EqualTo(0),
            "Expected NO LOC003 warnings when keys exist in all cultures");

        // Should not have any LOC001 (missing key) diagnostics either since all keys exist
        var missingKeyDiagnostics = diagnostics.Where(d => d.Id == Diagnostics.MissingKey.Id).ToList();
        Assert.That(missingKeyDiagnostics.Count, Is.EqualTo(0),
            "Expected NO LOC001 warnings either since keys exist in all cultures");
    }

    [Test]
    public async Task KeyMissing_InAllCultures_ProducesLOC001_NotLOC003()
    {
        var source = GetFormattedSource("completely.missing.key");

        var additionalFiles = new AdditionalFile[]
        {
            new("TestClass.en.json", "{ \"app\": { \"title\": \"My App\" } }"),
            new("TestClass.fr.json", "{ \"app\": { \"title\": \"Mon App\" } }"),
            new("TestClass.de.json", "{ \"app\": { \"title\": \"Meine App\" } }"),
        };

        var verifier = new CodeVerifier(source, additionalFiles);
        var diagnostics = await verifier.GetDiagnosticsAsync();

        var partialMissingDiagnostics = diagnostics.Where(d => d.Id == Diagnostics.PartialMissingKey.Id).ToList();
        Assert.That(partialMissingDiagnostics.Count, Is.EqualTo(0),
            "Expected NO LOC003 warnings when key is missing in ALL cultures");

        // Should produce LOC001 (missing key) instead
        var missingKeyDiagnostics = diagnostics.Where(d => d.Id == Diagnostics.MissingKey.Id).ToList();
        Assert.That(missingKeyDiagnostics.Count, Is.EqualTo(1),
            "Expected 1 LOC001 warning when key is missing in ALL cultures");
    }

    [Test]
    public async Task SingleCulture_Scenario_DoesNotProduceLOC003()
    {
        var source = GetFormattedSource("single.culture.key");

        var additionalFiles = new AdditionalFile[]
        {
            // Only one culture file - LOC003 requires multiple cultures to trigger
            new("TestClass.en.json", "{ \"single\": { \"culture\": { \"different\": \"Different Key\" } } }"), // Missing the key
        };

        var verifier = new CodeVerifier(source, additionalFiles);
        var diagnostics = await verifier.GetDiagnosticsAsync();

        var partialMissingDiagnostics = diagnostics.Where(d => d.Id == Diagnostics.PartialMissingKey.Id).ToList();
        Assert.That(partialMissingDiagnostics.Count, Is.EqualTo(0),
            "Expected NO LOC003 warnings when only one culture exists");

        // Should produce LOC001 (missing key) instead since key doesn't exist
        var missingKeyDiagnostics = diagnostics.Where(d => d.Id == Diagnostics.MissingKey.Id).ToList();
        Assert.That(missingKeyDiagnostics.Count, Is.EqualTo(1),
            "Expected 1 LOC001 warning when key is missing in single culture");
    }

    [Test]
    public async Task UnusedKeys_WithPartialStructure_ProducesOnlyLOC002_NotLOC003()
    {
        // This test uses keys that are NOT referenced in code
        // Should trigger LOC002 (unused) but NOT LOC003 (partial missing) because they're not used
        const string source =
            """
            using Microsoft.Extensions.Localization;

            public class TestClass
            {
                private readonly IStringLocalizer<TestClass> _localizer;

                public TestClass(IStringLocalizer<TestClass> localizer)
                {
                    _localizer = localizer;
                }

                public void TestMethod()
                {
                    // No keys are actually used - all keys in JSON are unused
                }
            }
            """;

        var additionalFiles = new AdditionalFile[]
        {
            new("TestClass.en.json", "{ \"unused\": { \"key1\": \"Value 1\", \"key2\": \"Value 2\" } }"),
            new("TestClass.fr.json", "{ \"unused\": { \"key1\": \"Valeur 1\" } }"), // Missing key2
            new("TestClass.de.json", "{ \"unused\": { \"key2\": \"Wert 2\" } }"), // Missing key1
        };

        var verifier = new CodeVerifier(source, additionalFiles);
        var diagnostics = await verifier.GetDiagnosticsAsync();

        var partialMissingDiagnostics = diagnostics.Where(d => d.Id == Diagnostics.PartialMissingKey.Id).ToList();
        Assert.That(partialMissingDiagnostics.Count, Is.EqualTo(0),
            "Expected NO LOC003 warnings for unused keys even if they have partial missing structure");

        // Should produce LOC002 (unused key) diagnostics instead
        var unusedKeyDiagnostics = diagnostics.Where(d => d.Id == Diagnostics.UnusedKey.Id).ToList();
        Assert.That(unusedKeyDiagnostics.Count, Is.GreaterThan(0),
            "Expected LOC002 warnings for unused keys");
    }
}