using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using NUnit.Framework;

namespace J18n.Analyzer.Tests;

using Verifier;

/// <summary>
/// Unit tests for KeySuggestionCodeFixProvider
/// Tests the code fix functionality for LOC001 diagnostics (completely missing keys).
/// </summary>
[TestFixture]
public class KeySuggestionCodeFixProviderUnitTests
{
    [Test]
    public async Task KeySuggestion_WithSimilarKey_SuggestsCorrectFix()
    {
        // Arrange
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
                    var value = _localizer["user.nam"]; // Missing 'e' at the end
                }
            }
            """;

        const string fixedSource =
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
                    var value = _localizer["user.name"]; // Missing 'e' at the end
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
                                       },
                                       "common": {
                                         "save": "Save",
                                         "cancel": "Cancel"
                                       }
                                     }
                                     """),
            new("TestClass.es.json", """
                                     {
                                       "user": {
                                         "name": "Nombre",
                                         "email": "Correo"
                                       },
                                       "common": {
                                         "save": "Guardar",
                                         "cancel": "Cancelar"
                                       }
                                     }
                                     """),
        };

        var verifier = new KeySuggestionCodeVerifier(source, additionalFiles);

        // Act and Assert
        await verifier.VerifyDiagnostic(
            DiagnosticResult.Create(
                Diagnostics.MissingKey.Id,
                DiagnosticSeverity.Error,
                14,
                32,
                "Localization key 'user.nam' is not found in any configured culture"));

        await verifier.VerifyCodeFix(fixedSource, codeFixIndex: 0);
    }

    [Test]
    public async Task NestedKey_WithSimilarExisting_SuggestsCorrectKey()
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
                    var value = _localizer["forms.validation.require"]; // Missing 'd' at the end
                }
            }
            """;

        const string fixedSource =
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
                    var value = _localizer["forms.validation.required"]; // Missing 'd' at the end
                }
            }
            """;

        var additionalFiles = new AdditionalFile[]
        {
            new("TestClass.en.json",
                """
                {
                  "forms": {
                    "validation": {
                      "required": "This field is required",
                      "email": "Invalid email format"
                    }
                  }
                }
                """)
        };

        var verifier = new KeySuggestionCodeVerifier(source, additionalFiles);

        // Verify the diagnostic is produced
        await verifier.VerifyDiagnostic(
            DiagnosticResult.Create(
                Diagnostics.MissingKey.Id,
                DiagnosticSeverity.Error,
                14,
                32,
                "Localization key 'forms.validation.require' is not found in any configured culture"));

        // Verify the code fix works
        await verifier.VerifyCodeFix(fixedSource, codeFixIndex: 0);
    }

    [Test]
    public async Task CreateMissingKey_WithNoSimilarKeys_OffersCreateAction()
    {
        // Arrange
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
                    var value = _localizer["completely.new.key"];
                }
            }
            """;

        var additionalFiles = new AdditionalFile[]
        {
            new("TestClass.en.json",
                """
                {
                  "user": {
                    "name": "Name",
                    "email": "Email"
                  }
                }
                """),
            new("TestClass.es.json",
                """
                {
                  "user": {
                    "name": "Nombre",
                    "email": "Correo"
                  }
                }
                """),
        };

        var expectFixedFiles = new AdditionalFile[]
        {
            new("TestClass.en.json",
                """
                {
                  "user": {
                    "name": "Name",
                    "email": "Email"
                  },
                  "completely": {
                    "new": {
                      "key": "TODO: Add translation"
                    }
                  }
                }
                """),
            new("TestClass.es.json",
                """
                {
                  "user": {
                    "name": "Nombre",
                    "email": "Correo"
                  },
                  "completely": {
                    "new": {
                      "key": "TODO: Add translation"
                    }
                  }
                }
                """),
        };

        var verifier = new KeySuggestionCodeVerifier(source, additionalFiles);

        // Act and Assert
        // Verify the diagnostic is produced
        await verifier.VerifyDiagnostic(
            DiagnosticResult.Create(
                Diagnostics.MissingKey.Id,
                DiagnosticSeverity.Error,
                14,
                32,
                "Localization key 'completely.new.key' is not found in any configured culture"));

        foreach (var file in expectFixedFiles)
        {
            // Index 2 for create_key action.
            await verifier.VerifyAdditionalFileCodeFix(file.Path, file.Content, 2);
        }
    }

    [Test]
    public async Task EmptyJsonFiles_DoNotCrash()
    {
        const string source = """
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
                                      var value = _localizer["any.key"];
                                  }
                              }
                              """;

        var additionalFiles = new AdditionalFile[]
        {
            new("TestClass.en.json", "{}"),
            new("TestClass.es.json", "{}")
        };

        var verifier = new KeySuggestionCodeVerifier(source, additionalFiles);

        // Should not crash and should produce diagnostic
        await verifier.VerifyDiagnostic(
            DiagnosticResult.Create(
                Diagnostics.MissingKey.Id,
                DiagnosticSeverity.Error,
                14,
                32,
                "Localization key 'any.key' is not found in any configured culture"));
    }

    [Test]
    public async Task CaseSensitiveKeys_WorkCorrectly()
    {
        const string source = """
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
                                      var value = _localizer["User.Name"]; // Different case
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

        var verifier = new KeySuggestionCodeVerifier(source, additionalFiles);

        // Should produce diagnostic for case mismatch (assuming case sensitivity is enabled)
        await verifier.VerifyDiagnostic(
            DiagnosticResult.Create(
                Diagnostics.MissingKey.Id,
                DiagnosticSeverity.Error,
                14,
                32,
                "Localization key 'User.Name' is not found in any configured culture"));
    }
}