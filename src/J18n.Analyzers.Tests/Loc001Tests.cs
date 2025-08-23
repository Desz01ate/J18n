namespace J18n.Analyzers.Tests;

using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using NUnit.Framework;
using Verifier;

[TestFixture]
public class Loc001Tests
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
    public async Task MissingKey_Produces_LOC001_Error()
    {
        var source = GetFormattedSource("missing.key");

        var additionalFiles = new AdditionalFile[]
        {
            new("TestClass.en.json", "{ }")
        };

        var verifier = new CodeVerifier(source, additionalFiles);

        await verifier.VerifyDiagnostic(
            DiagnosticResult.Create(
                Diagnostics.MissingKey.Id,
                DiagnosticSeverity.Error,
                14,
                32,
                "Localization key 'missing.key' is not found in any configured culture"));
    }

    [Test]
    public async Task EmptyJsonFile_Produces_LOC001_Error()
    {
        var source = GetFormattedSource("user.name");

        var additionalFiles = new AdditionalFile[]
        {
            new("TestClass.en.json", "{}")
        };

        var verifier = new CodeVerifier(source, additionalFiles);

        await verifier.VerifyDiagnostic(
            DiagnosticResult.Create(
                Diagnostics.MissingKey.Id,
                DiagnosticSeverity.Error,
                14,
                32,
                "Localization key 'user.name' is not found in any configured culture"));
    }

    [Test]
    public async Task InvalidJsonFile_Produces_LOC001_Error()
    {
        var source = GetFormattedSource("user.name");

        var additionalFiles = new AdditionalFile[]
        {
            new("TestClass.en.json", "{ invalid json syntax }")
        };

        var verifier = new CodeVerifier(source, additionalFiles);

        await verifier.VerifyDiagnostic(
            DiagnosticResult.Create(
                Diagnostics.MissingKey.Id,
                DiagnosticSeverity.Error,
                14,
                32,
                "Localization key 'user.name' is not found in any configured culture"));
    }

    [Test]
    public async Task NoJsonFiles_Produces_LOC001_Error()
    {
        var source = GetFormattedSource("user.name");

        var additionalFiles = new AdditionalFile[] { };

        var verifier = new CodeVerifier(source, additionalFiles);

        await verifier.VerifyDiagnostic(
            DiagnosticResult.Create(
                Diagnostics.MissingKey.Id,
                DiagnosticSeverity.Error,
                14,
                32,
                "Localization key 'user.name' is not found in any configured culture"));
    }

    [Test]
    public async Task MultipleEmptyFiles_Produces_LOC001_Error()
    {
        var source = GetFormattedSource("user.name");

        var additionalFiles = new AdditionalFile[]
        {
            new("TestClass.en.json", "{}"),
            new("TestClass.fr.json", "{}"),
            new("TestClass.de.json", "{}")
        };

        var verifier = new CodeVerifier(source, additionalFiles);

        await verifier.VerifyDiagnostic(
            DiagnosticResult.Create(
                Diagnostics.MissingKey.Id,
                DiagnosticSeverity.Error,
                14,
                32,
                "Localization key 'user.name' is not found in any configured culture"));
    }

    [Test]
    public async Task MissingNestedKey_Produces_LOC001_Error()
    {
        var source = GetFormattedSource("user.name");

        var additionalFiles = new AdditionalFile[]
        {
            new("TestClass.en.json", 
                """
                {
                  "user": {}
                }
                """)
        };

        var verifier = new CodeVerifier(source, additionalFiles);

        await verifier.VerifyDiagnostic(
            DiagnosticResult.Create(
                Diagnostics.MissingKey.Id,
                DiagnosticSeverity.Error,
                14,
                32,
                "Localization key 'user.name' is not found in any configured culture"));
    }

    [Test]
    public async Task MissingParentKey_Produces_LOC001_Error()
    {
        var source = GetFormattedSource("user.name");

        var additionalFiles = new AdditionalFile[]
        {
            new("TestClass.en.json", 
                """
                {
                  "other": {
                    "key": "Value"
                  }
                }
                """)
        };

        var verifier = new CodeVerifier(source, additionalFiles);

        await verifier.VerifyDiagnostic(
            DiagnosticResult.Create(
                Diagnostics.MissingKey.Id,
                DiagnosticSeverity.Error,
                14,
                32,
                "Localization key 'user.name' is not found in any configured culture"));
    }

    [Test]
    public async Task DeeplyNestedMissing_Produces_LOC001_Error()
    {
        var source = GetFormattedSource("level1.level2.level3.level4.missingKey");

        var additionalFiles = new AdditionalFile[]
        {
            new("TestClass.en.json", 
                """
                {
                  "level1": {
                    "level2": {
                      "level3": {
                        "level4": {
                          "existingKey": "Value"
                        }
                      }
                    }
                  }
                }
                """)
        };

        var verifier = new CodeVerifier(source, additionalFiles);

        await verifier.VerifyDiagnostic(
            DiagnosticResult.Create(
                Diagnostics.MissingKey.Id,
                DiagnosticSeverity.Error,
                14,
                32,
                "Localization key 'level1.level2.level3.level4.missingKey' is not found in any configured culture"));
    }

    [Test]
    public async Task MissingFromAllCultures_Produces_LOC001_Error()
    {
        var source = GetFormattedSource("missing.key");

        var additionalFiles = new AdditionalFile[]
        {
            new("TestClass.en.json", 
                """
                {
                  "existing": {
                    "key": "English Value"
                  }
                }
                """),
            new("TestClass.fr.json", 
                """
                {
                  "existing": {
                    "key": "French Value"
                  }
                }
                """),
            new("TestClass.de.json", 
                """
                {
                  "existing": {
                    "key": "German Value"
                  }
                }
                """)
        };

        var verifier = new CodeVerifier(source, additionalFiles);

        await verifier.VerifyDiagnostic(
            DiagnosticResult.Create(
                Diagnostics.MissingKey.Id,
                DiagnosticSeverity.Error,
                14,
                32,
                "Localization key 'missing.key' is not found in any configured culture"));
    }

    [Test]
    public async Task LargeCultureSet_Produces_LOC001_Error()
    {
        var source = GetFormattedSource("missing.key");

        var additionalFiles = new AdditionalFile[]
        {
            new("TestClass.en.json", """{"existing": "English"}"""),
            new("TestClass.fr.json", """{"existing": "French"}"""),
            new("TestClass.de.json", """{"existing": "German"}"""),
            new("TestClass.es.json", """{"existing": "Spanish"}"""),
            new("TestClass.it.json", """{"existing": "Italian"}"""),
            new("TestClass.pt.json", """{"existing": "Portuguese"}"""),
            new("TestClass.ru.json", """{"existing": "Russian"}"""),
            new("TestClass.ja.json", """{"existing": "Japanese"}"""),
            new("TestClass.zh.json", """{"existing": "Chinese"}"""),
            new("TestClass.ko.json", """{"existing": "Korean"}""")
        };

        var verifier = new CodeVerifier(source, additionalFiles);

        await verifier.VerifyDiagnostic(
            DiagnosticResult.Create(
                Diagnostics.MissingKey.Id,
                DiagnosticSeverity.Error,
                14,
                32,
                "Localization key 'missing.key' is not found in any configured culture"));
    }

    [Test]
    public async Task EmptyStringKey_Produces_LOC001_Error()
    {
        var source = GetFormattedSource("");

        var additionalFiles = new AdditionalFile[]
        {
            new("TestClass.en.json", 
                """
                {
                  "notEmpty": "Value"
                }
                """)
        };

        var verifier = new CodeVerifier(source, additionalFiles);

        await verifier.VerifyDiagnostic(
            DiagnosticResult.Create(
                Diagnostics.MissingKey.Id,
                DiagnosticSeverity.Error,
                14,
                32,
                "Localization key '' is not found in any configured culture"));
    }

    [Test]
    public async Task SpecialCharacterKey_Produces_LOC001_Error()
    {
        var source = GetFormattedSource("special-key_with@symbols#and.dots");

        var additionalFiles = new AdditionalFile[]
        {
            new("TestClass.en.json", 
                """
                {
                  "regular": {
                    "key": "Value"
                  },
                  "título": "Unicode Title",
                  "key-with-dash": "Dash Value"
                }
                """)
        };

        var verifier = new CodeVerifier(source, additionalFiles);

        await verifier.VerifyDiagnostic(
            DiagnosticResult.Create(
                Diagnostics.MissingKey.Id,
                DiagnosticSeverity.Error,
                14,
                32,
                "Localization key 'special-key_with@symbols#and.dots' is not found in any configured culture"));
    }

    [Test]
    public async Task NumericStringKey_Produces_LOC001_Error()
    {
        var source = GetFormattedSource("12345");

        var additionalFiles = new AdditionalFile[]
        {
            new("TestClass.en.json", 
                """
                {
                  "123": "Numeric Key 123",
                  "abc": "Text Key"
                }
                """)
        };

        var verifier = new CodeVerifier(source, additionalFiles);

        await verifier.VerifyDiagnostic(
            DiagnosticResult.Create(
                Diagnostics.MissingKey.Id,
                DiagnosticSeverity.Error,
                14,
                32,
                "Localization key '12345' is not found in any configured culture"));
    }

    [Test]
    public async Task WhitespaceKey_Produces_LOC001_Error()
    {
        var source = GetFormattedSource("key with spaces");

        var additionalFiles = new AdditionalFile[]
        {
            new("TestClass.en.json", 
                """
                {
                  "keyWithoutSpaces": "Value",
                  "key_with_underscores": "Underscore Value"
                }
                """)
        };

        var verifier = new CodeVerifier(source, additionalFiles);

        await verifier.VerifyDiagnostic(
            DiagnosticResult.Create(
                Diagnostics.MissingKey.Id,
                DiagnosticSeverity.Error,
                14,
                32,
                "Localization key 'key with spaces' is not found in any configured culture"));
    }

    [Test]
    public async Task ComplexNestedStructure_Produces_LOC001_Error()
    {
        var source = GetFormattedSource("missing.nested.key");

        var additionalFiles = new AdditionalFile[]
        {
            new("TestClass.en.json", 
                """
                {
                  "user": {
                    "profile": {
                      "name": "Name",
                      "settings": {
                        "theme": "dark",
                        "language": "en"
                      }
                    },
                    "preferences": ["option1", "option2"],
                    "metadata": {
                      "created": "2023-01-01",
                      "version": 1.0,
                      "active": true,
                      "tags": null
                    }
                  },
                  "app": {
                    "config": {
                      "debug": false,
                      "features": {
                        "chat": true,
                        "notifications": false
                      }
                    }
                  }
                }
                """)
        };

        var verifier = new CodeVerifier(source, additionalFiles);

        await verifier.VerifyDiagnostic(
            DiagnosticResult.Create(
                Diagnostics.MissingKey.Id,
                DiagnosticSeverity.Error,
                14,
                32,
                "Localization key 'missing.nested.key' is not found in any configured culture"));
    }

    [Test]
    public async Task VeryLargeJsonFile_Produces_LOC001_Error()
    {
        var source = GetFormattedSource("missing.key");

        var jsonBuilder = new System.Text.StringBuilder();
        jsonBuilder.AppendLine("{");
        
        for (var i = 0; i < 100; i++)
        {
            jsonBuilder.AppendLine($"  \"section{i}\": {{");
            for (var j = 0; j < 50; j++)
            {
                var comma = j < 49 ? "," : "";
                jsonBuilder.AppendLine($"    \"key{j}\": \"Value {i}-{j}\"{comma}");
            }
            var sectionComma = i < 99 ? "," : "";
            jsonBuilder.AppendLine($"  }}{sectionComma}");
        }
        jsonBuilder.AppendLine("}");

        var additionalFiles = new AdditionalFile[]
        {
            new("TestClass.en.json", jsonBuilder.ToString())
        };

        var verifier = new CodeVerifier(source, additionalFiles);

        await verifier.VerifyDiagnostic(
            DiagnosticResult.Create(
                Diagnostics.MissingKey.Id,
                DiagnosticSeverity.Error,
                14,
                32,
                "Localization key 'missing.key' is not found in any configured culture"));
    }

    [Test]
    public async Task ValidNestedKey_DoesNotProduce_LOC001_Error()
    {
        var source = GetFormattedSource("user.profile.name");

        var additionalFiles = new AdditionalFile[]
        {
            new("TestClass.en.json", 
                """
                {
                  "user": {
                    "profile": {
                      "name": "User Name",
                      "email": "user@example.com"
                    },
                    "settings": {
                      "theme": "dark"
                    }
                  }
                }
                """)
        };

        var verifier = new CodeVerifier(source, additionalFiles);

        var diagnostics = await verifier.GetDiagnosticsAsync();

        var loc001Diagnostics = diagnostics.Where(d => d.Id == Diagnostics.MissingKey.Id);
        Assert.That(loc001Diagnostics, Is.Empty,
            "Expected no LOC001 (MissingKey) diagnostics for valid nested key");
    }
}