using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using NUnit.Framework;

namespace J18n.Analyzers.Tests;

using Verifier;

/// <summary>
/// Unit tests for PartialMissingKeyCodeFixProvider
/// Tests the code fix functionality for LOC003 diagnostics (keys missing in some cultures).
/// </summary>
[TestFixture]
public class PartialMissingKeyCodeFixProviderUnitTests
{
    [Test]
    public async Task PartialMissingKey_WithSingleMissingCulture_AddsKeyToMissingCulture()
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
                    var value = _localizer["user.name"];
                }
            }
            """;

        var additionalFiles = new AdditionalFile[]
        {
            new("TestClass.en.json", """
                                     {
                                       "user": {
                                         "name": "Name",
                                         "email": "Email"
                                       }
                                     }
                                     """),
            new("TestClass.es.json", """
                                     {
                                       "user": {
                                         "name": "Nombre",
                                         "email": "Correo"
                                       }
                                     }
                                     """),
            new("TestClass.fr.json", """
                                     {
                                       "user": {
                                         "email": "Email"
                                       }
                                     }
                                     """),
        };

        var expectedFixedFile = new AdditionalFile("TestClass.fr.json", """
                                                                        {
                                                                          "user": {
                                                                            "email": "Email",
                                                                            "name": "TODO: Translate from en"
                                                                          }
                                                                        }
                                                                        """);

        var verifier = new PartialMissingKeyCodeVerifier(source, additionalFiles);

        await verifier.VerifyDiagnostic(
            DiagnosticResult.Create(
                Diagnostics.PartialMissingKey.Id,
                DiagnosticSeverity.Warning,
                14,
                32,
                "Localization key 'user.name' is missing in cultures: fr"));

        await verifier.VerifyAdditionalFileCodeFix(expectedFixedFile.Path, expectedFixedFile.Content, 0);
    }

    [Test]
    public async Task PartialMissingKey_WithMultipleMissingCultures_AddsKeyToAllMissingCultures()
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
                    var value = _localizer["common.save"];
                }
            }
            """;

        var additionalFiles = new AdditionalFile[]
        {
            new("TestClass.en.json", """
                                     {
                                       "common": {
                                         "save": "Save",
                                         "cancel": "Cancel"
                                       }
                                     }
                                     """),
            new("TestClass.es.json", """
                                     {
                                       "common": {
                                         "cancel": "Cancelar"
                                       }
                                     }
                                     """),
            new("TestClass.fr.json", """
                                     {
                                       "common": {
                                         "cancel": "Annuler"
                                       }
                                     }
                                     """),
        };

        var expectedFixedFiles = new[]
        {
            new AdditionalFile("TestClass.es.json", """
                                                    {
                                                      "common": {
                                                        "cancel": "Cancelar",
                                                        "save": "TODO: Translate from en"
                                                      }
                                                    }
                                                    """),
            new AdditionalFile("TestClass.fr.json", """
                                                    {
                                                      "common": {
                                                        "cancel": "Annuler",
                                                        "save": "TODO: Translate from en"
                                                      }
                                                    }
                                                    """),
        };

        var verifier = new PartialMissingKeyCodeVerifier(source, additionalFiles);

        await verifier.VerifyDiagnostic(
            DiagnosticResult.Create(
                Diagnostics.PartialMissingKey.Id,
                DiagnosticSeverity.Warning,
                14,
                32,
                "Localization key 'common.save' is missing in cultures: es, fr"));

        foreach (var expectedFile in expectedFixedFiles)
        {
            await verifier.VerifyAdditionalFileCodeFix(expectedFile.Path, expectedFile.Content, 0);
        }
    }

    [Test]
    public async Task PartialMissingKey_WithNestedKeyStructure_AddsNestedKeyCorrectly()
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
                    var value = _localizer["forms.validation.required"];
                }
            }
            """;

        var additionalFiles = new AdditionalFile[]
        {
            new("TestClass.en.json", """
                                     {
                                       "forms": {
                                         "validation": {
                                           "required": "This field is required",
                                           "email": "Invalid email format"
                                         }
                                       }
                                     }
                                     """),
            new("TestClass.de.json", """
                                     {
                                       "forms": {
                                         "validation": {
                                           "email": "Ungültiges E-Mail-Format"
                                         }
                                       }
                                     }
                                     """),
        };

        var expectedFixedFile = new AdditionalFile("TestClass.de.json", """
                                                                        {
                                                                          "forms": {
                                                                            "validation": {
                                                                              "email": "Ungültiges E-Mail-Format",
                                                                              "required": "TODO: Translate from en"
                                                                            }
                                                                          }
                                                                        }
                                                                        """);

        var verifier = new PartialMissingKeyCodeVerifier(source, additionalFiles);

        await verifier.VerifyDiagnostic(
            DiagnosticResult.Create(
                Diagnostics.PartialMissingKey.Id,
                DiagnosticSeverity.Warning,
                14,
                32,
                "Localization key 'forms.validation.required' is missing in cultures: de"));

        await verifier.VerifyAdditionalFileCodeFix(expectedFixedFile.Path, expectedFixedFile.Content, 0);
    }

    [Test]
    public async Task PartialMissingKey_WithEmptyTargetJson_CreatesNestedStructure()
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
                    var value = _localizer["user.profile.avatar"];
                }
            }
            """;

        var additionalFiles = new AdditionalFile[]
        {
            new("TestClass.en.json", """
                                     {
                                       "user": {
                                         "profile": {
                                           "avatar": "Profile Picture"
                                         }
                                       }
                                     }
                                     """),
            new("TestClass.ja.json", "{}"),
        };

        var expectedFixedFile = new AdditionalFile("TestClass.ja.json", """
                                                                        {
                                                                          "user": {
                                                                            "profile": {
                                                                              "avatar": "TODO: Translate from en"
                                                                            }
                                                                          }
                                                                        }
                                                                        """);

        var verifier = new PartialMissingKeyCodeVerifier(source, additionalFiles);

        await verifier.VerifyDiagnostic(
            DiagnosticResult.Create(
                Diagnostics.PartialMissingKey.Id,
                DiagnosticSeverity.Warning,
                14,
                32,
                "Localization key 'user.profile.avatar' is missing in cultures: ja"));

        await verifier.VerifyAdditionalFileCodeFix(expectedFixedFile.Path, expectedFixedFile.Content, 0);
    }

    [Test]
    public async Task PartialMissingKey_WithNoExistingTranslation_UsesGenericTodoMessage()
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
                    var value = _localizer["new.key"];
                }
            }
            """;

        var additionalFiles = new AdditionalFile[]
        {
            new("TestClass.en.json", "{}"),
            new("TestClass.pt.json", "{}"),
        };

        var verifier = new PartialMissingKeyCodeVerifier(source, additionalFiles);

        await verifier.VerifyDiagnostic(
            DiagnosticResult.Create(
                Diagnostics.MissingKey.Id,
                DiagnosticSeverity.Error,
                14,
                32,
                "Localization key 'new.key' is not found in any configured culture"));
    }

    [Test]
    public async Task PartialMissingKey_WithSingleCultureFiles_DoesNotProduceDiagnostic()
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
                    var value = _localizer["user.name"];
                }
            }
            """;

        var additionalFiles = new AdditionalFile[]
        {
            new("TestClass.en.json", """
                                     {
                                       "user": {
                                         "name": "Name"
                                       }
                                     }
                                     """),
        };

        var verifier = new PartialMissingKeyCodeVerifier(source, additionalFiles);

        await verifier.VerifyDiagnostic();
    }
}