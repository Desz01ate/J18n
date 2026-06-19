using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;

namespace J18n.Analyzers.Tests;

using Verifier;

[TestFixture]
public class Loc004Tests
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
    public async Task DuplicateKey_Produces_LOC004_Error()
    {
        var source = GetFormattedSource("user.name");

        // Duplicate raw key in the same JSON file
        var additionalFiles = new AdditionalFile[]
        {
            new("TestClass.en.json",
                """
                {
                  "user": {
                    "name": "Name",
                    "name": "Duplicate"
                  }
                }
                """),
        };

        var verifier = new CodeVerifier(source, additionalFiles);

        var diagnostics = await verifier.GetDiagnosticsAsync();

        Assert.That(diagnostics.Any(d => d.Id == Diagnostics.DuplicateKey.Id),
            "Expected LOC004 (DuplicateKey) to be reported for duplicate JSON key");
    }

    [Test]
    public async Task NestedKeysWithSameName_DoesNotProduce_LOC004_Error()
    {
        var source = GetFormattedSource("Home.Title");

        var additionalFiles = new AdditionalFile[]
        {
            new("TestClass.en.json",
                """
                {
                  "Home": {
                    "Title": "Home Page"
                  },
                  "Settings": {
                    "Title": "Settings Page"
                  }
                }
                """),
        };

        var verifier = new CodeVerifier(source, additionalFiles);

        var diagnostics = await verifier.GetDiagnosticsAsync();

        Assert.That(diagnostics.All(d => d.Id != Diagnostics.DuplicateKey.Id),
            "Expected LOC004 (DuplicateKey) NOT to be reported for nested keys with same leaf name");
    }

    [Test]
    public async Task RootLevelDuplicates_Produces_LOC004_Error()
    {
        var source = GetFormattedSource("title");

        var additionalFiles = new AdditionalFile[]
        {
            new("TestClass.en.json",
                """
                {
                  "title": "First Title",
                  "title": "Second Title"
                }
                """),
        };

        var verifier = new CodeVerifier(source, additionalFiles);
        var diagnostics = await verifier.GetDiagnosticsAsync();

        Assert.That(diagnostics.Any(d => d.Id == Diagnostics.DuplicateKey.Id),
            "Expected LOC004 (DuplicateKey) to be reported for duplicate root level keys");
    }

    [Test]
    public async Task DeepNestedDuplicates_Produces_LOC004_Error()
    {
        var source = GetFormattedSource("level1.level2.level3.key");

        var additionalFiles = new AdditionalFile[]
        {
            new("TestClass.en.json",
                """
                {
                  "level1": {
                    "level2": {
                      "level3": {
                        "key": "First Value",
                        "key": "Second Value"
                      }
                    }
                  }
                }
                """),
        };

        var verifier = new CodeVerifier(source, additionalFiles);
        var diagnostics = await verifier.GetDiagnosticsAsync();

        Assert.That(diagnostics.Any(d => d.Id == Diagnostics.DuplicateKey.Id),
            "Expected LOC004 (DuplicateKey) to be reported for deep nested duplicate keys");
    }

    [Test]
    public async Task MixedValueTypesDuplicates_Produces_LOC004_Error()
    {
        var source = GetFormattedSource("mixedKey");

        var additionalFiles = new AdditionalFile[]
        {
            new("TestClass.en.json",
                """
                {
                  "mixedKey": "string value",
                  "mixedKey": null
                }
                """),
        };

        var verifier = new CodeVerifier(source, additionalFiles);
        var diagnostics = await verifier.GetDiagnosticsAsync();

        Assert.That(diagnostics.Any(d => d.Id == Diagnostics.DuplicateKey.Id),
            "Expected LOC004 (DuplicateKey) to be reported for duplicate keys with different value types");
    }

    [Test]
    public async Task EmptyKeyDuplicates_Produces_LOC004_Error()
    {
        var source = GetFormattedSource("");

        var additionalFiles = new AdditionalFile[]
        {
            new("TestClass.en.json",
                """
                {
                  "": "First Empty Key",
                  "": "Second Empty Key"
                }
                """),
        };

        var verifier = new CodeVerifier(source, additionalFiles);
        var diagnostics = await verifier.GetDiagnosticsAsync();

        Assert.That(diagnostics.Any(d => d.Id == Diagnostics.DuplicateKey.Id),
            "Expected LOC004 (DuplicateKey) to be reported for duplicate empty keys");
    }

    [Test]
    public async Task SpecialCharacterKeyDuplicates_Produces_LOC004_Error()
    {
        var source = GetFormattedSource("key-with-dash");

        var additionalFiles = new AdditionalFile[]
        {
            new("TestClass.en.json",
                """
                {
                  "key-with-dash": "First Value",
                  "key-with-dash": "Second Value",
                  "key_with_underscore": "First Underscore",
                  "key_with_underscore": "Second Underscore"
                }
                """),
        };

        var verifier = new CodeVerifier(source, additionalFiles);
        var diagnostics = await verifier.GetDiagnosticsAsync();

        Assert.That(diagnostics.Count(d => d.Id == Diagnostics.DuplicateKey.Id) >= 2,
            "Expected LOC004 (DuplicateKey) to be reported for duplicate special character keys");
    }

    [Test]
    public async Task NumericKeyDuplicates_Produces_LOC004_Error()
    {
        var source = GetFormattedSource("123");

        var additionalFiles = new AdditionalFile[]
        {
            new("TestClass.en.json",
                """
                {
                  "123": "First Numeric",
                  "123": "Second Numeric"
                }
                """),
        };

        var verifier = new CodeVerifier(source, additionalFiles);
        var diagnostics = await verifier.GetDiagnosticsAsync();

        Assert.That(diagnostics.Any(d => d.Id == Diagnostics.DuplicateKey.Id),
            "Expected LOC004 (DuplicateKey) to be reported for duplicate numeric keys");
    }

    [Test]
    public async Task UnicodeKeyDuplicates_Produces_LOC004_Error()
    {
        var source = GetFormattedSource("título");

        var additionalFiles = new AdditionalFile[]
        {
            new("TestClass.en.json",
                """
                {
                  "título": "First Unicode",
                  "título": "Second Unicode"
                }
                """),
        };

        var verifier = new CodeVerifier(source, additionalFiles);
        var diagnostics = await verifier.GetDiagnosticsAsync();

        Assert.That(diagnostics.Any(d => d.Id == Diagnostics.DuplicateKey.Id),
            "Expected LOC004 (DuplicateKey) to be reported for duplicate unicode keys");
    }

    [Test]
    public async Task CaseSensitiveDifferentCases_DoesNotProduce_LOC004_Error()
    {
        var source = GetFormattedSource("Title");

        var additionalFiles = new AdditionalFile[]
        {
            new("TestClass.en.json",
                """
                {
                  "Title": "Capital Title",
                  "title": "Lowercase Title"
                }
                """),
        };

        var verifier = new CodeVerifier(source, additionalFiles);
        var diagnostics = await verifier.GetDiagnosticsAsync();

        Assert.That(diagnostics.All(d => d.Id != Diagnostics.DuplicateKey.Id),
            "Expected LOC004 (DuplicateKey) NOT to be reported for different cases with case sensitivity enabled (default)");
    }

    [Test]
    public async Task NoDuplicates_DoesNotProduce_LOC004_Error()
    {
        var source = GetFormattedSource("user.name");

        var additionalFiles = new AdditionalFile[]
        {
            new("TestClass.en.json",
                """
                {
                  "user": {
                    "name": "Name",
                    "email": "Email"
                  },
                  "product": {
                    "description": "Description",
                    "price": "Price"
                  }
                }
                """),
        };

        var verifier = new CodeVerifier(source, additionalFiles);
        var diagnostics = await verifier.GetDiagnosticsAsync();

        Assert.That(diagnostics.All(d => d.Id != Diagnostics.DuplicateKey.Id),
            "Expected LOC004 (DuplicateKey) NOT to be reported when no duplicates exist");
    }

    [Test]
    public async Task EmptyObjects_DoesNotProduce_LOC004_Error()
    {
        var source = GetFormattedSource("nonexistent");

        var additionalFiles = new AdditionalFile[]
        {
            new("TestClass.en.json",
                """
                {
                  "empty1": {},
                  "empty2": {},
                  "user": {
                    "profile": {}
                  }
                }
                """),
        };

        var verifier = new CodeVerifier(source, additionalFiles);
        var diagnostics = await verifier.GetDiagnosticsAsync();

        Assert.That(diagnostics.All(d => d.Id != Diagnostics.DuplicateKey.Id),
            "Expected LOC004 (DuplicateKey) NOT to be reported for empty objects");
    }

    [Test]
    public async Task MixedNestingLevels_DoesNotProduce_LOC004_Error()
    {
        var source = GetFormattedSource("user.name");

        var additionalFiles = new AdditionalFile[]
        {
            new("TestClass.en.json",
                """
                {
                  "user": {
                    "name": "User Name"
                  },
                  "admin": {
                    "user": {
                      "role": "Admin Role"
                    }
                  },
                  "config": {
                    "database": {
                      "user": {
                        "connection": "Connection String"
                      }
                    }
                  }
                }
                """),
        };

        var verifier = new CodeVerifier(source, additionalFiles);
        var diagnostics = await verifier.GetDiagnosticsAsync();

        Assert.That(diagnostics.All(d => d.Id != Diagnostics.DuplicateKey.Id),
            "Expected LOC004 (DuplicateKey) NOT to be reported for mixed nesting levels with different flattened paths");
    }

    [Test]
    public async Task VeryDeepNesting_Produces_LOC004_Error()
    {
        var source = GetFormattedSource("level1.level2.level3.level4.level5.key");

        var additionalFiles = new AdditionalFile[]
        {
            new("TestClass.en.json",
                """
                {
                  "level1": {
                    "level2": {
                      "level3": {
                        "level4": {
                          "level5": {
                            "key": "First Deep Value",
                            "key": "Second Deep Value"
                          }
                        }
                      }
                    }
                  }
                }
                """),
        };

        var verifier = new CodeVerifier(source, additionalFiles);
        var diagnostics = await verifier.GetDiagnosticsAsync();

        Assert.That(diagnostics.Any(d => d.Id == Diagnostics.DuplicateKey.Id),
            "Expected LOC004 (DuplicateKey) to be reported for duplicate keys at very deep nesting levels");
    }

    [Test]
    public async Task InvalidJSON_DoesNotCrash_LOC004()
    {
        var source = GetFormattedSource("any.key");

        var additionalFiles = new AdditionalFile[]
        {
            new("TestClass.en.json", "{ invalid json syntax }"),
        };

        var verifier = new CodeVerifier(source, additionalFiles);
        var diagnostics = await verifier.GetDiagnosticsAsync();

        // Should not crash, but we expect a missing key diagnostic instead
        Assert.That(diagnostics.Any(d => d.Id == Diagnostics.MissingKey.Id),
            "Expected LOC001 (MissingKey) to be reported for invalid JSON, not crash with LOC004");
        Assert.That(diagnostics.All(d => d.Id != Diagnostics.DuplicateKey.Id),
            "Expected LOC004 (DuplicateKey) NOT to be reported for invalid JSON");
    }

    [Test]
    public async Task LargeJSONFile_Produces_LOC004_Error()
    {
        var source = GetFormattedSource("common.button");

        // Create a large JSON with many keys and one duplicate
        var jsonContent = """
        {
          "common": {
            "button": "First Button",
            "label": "Label",
            "input": "Input",
            "checkbox": "Checkbox",
            "radio": "Radio",
            "select": "Select",
            "textarea": "Textarea",
            "table": "Table",
            "form": "Form",
            "modal": "Modal",
            "dialog": "Dialog",
            "menu": "Menu",
            "navigation": "Navigation",
            "header": "Header",
            "footer": "Footer",
            "sidebar": "Sidebar",
            "content": "Content",
            "button": "Second Button"
          },
          "user": {
            "profile": "Profile",
            "settings": "Settings",
            "preferences": "Preferences",
            "account": "Account",
            "security": "Security",
            "privacy": "Privacy",
            "notifications": "Notifications"
          },
          "admin": {
            "dashboard": "Dashboard",
            "users": "Users",
            "reports": "Reports",
            "analytics": "Analytics",
            "logs": "Logs",
            "system": "System",
            "configuration": "Configuration"
          }
        }
        """;

        var additionalFiles = new AdditionalFile[]
        {
            new("TestClass.en.json", jsonContent),
        };

        var verifier = new CodeVerifier(source, additionalFiles);
        var diagnostics = await verifier.GetDiagnosticsAsync();

        Assert.That(diagnostics.Any(d => d.Id == Diagnostics.DuplicateKey.Id),
            "Expected LOC004 (DuplicateKey) to be reported for duplicate keys in large JSON file");
    }

    [Test]
    public async Task MultipleCulturesWithDuplicates_Produces_LOC004_Error()
    {
        var source = GetFormattedSource("greeting");

        var additionalFiles = new AdditionalFile[]
        {
            new("TestClass.en.json",
                """
                {
                  "greeting": "Hello",
                  "greeting": "Hi"
                }
                """),
            new("TestClass.fr.json",
                """
                {
                  "greeting": "Bonjour",
                  "greeting": "Salut"
                }
                """),
        };

        var verifier = new CodeVerifier(source, additionalFiles);
        var diagnostics = await verifier.GetDiagnosticsAsync();

        Assert.That(diagnostics.Count(d => d.Id == Diagnostics.DuplicateKey.Id) >= 2,
            "Expected LOC004 (DuplicateKey) to be reported for duplicate keys in multiple cultures");
    }

    [Test]
    public async Task LOC004_ComprehensiveScenarios_Test()
    {
        var source = GetFormattedSource("test.key");

        var additionalFiles = new AdditionalFile[]
        {
            new("TestClass.en.json",
                """
                {
                  "test": {
                    "key": "Valid Test Key"
                  },
                  "duplicates": {
                    "basic": "First Basic",
                    "basic": "Second Basic",
                    "nested": {
                      "deep": {
                        "value": "First Deep",
                        "value": "Second Deep"
                      }
                    }
                  },
                  "valid": {
                    "no_duplicates": "This is fine",
                    "different": "Different key"
                  },
                  "case_test": {
                    "Upper": "Upper Case",
                    "lower": "Lower Case"
                  },
                  "special": {
                    "key-with-dash": "Dash Value",
                    "key_with_underscore": "Underscore Value",
                    "123": "Numeric Key",
                    "título": "Unicode Key"
                  },
                  "edge_cases": {
                    "": "Empty Key",
                    "null_value": null,
                    "boolean_value": true
                  }
                }
                """),
        };

        var verifier = new CodeVerifier(source, additionalFiles);
        var diagnostics = await verifier.GetDiagnosticsAsync();

        var duplicateKeyDiagnostics = diagnostics.Where(d => d.Id == Diagnostics.DuplicateKey.Id).ToList();

        // Should detect exactly 2 duplicate scenarios: "basic" and "value"
        Assert.That(duplicateKeyDiagnostics.Count, Is.EqualTo(2),
            "Expected exactly 2 LOC004 (DuplicateKey) diagnostics for the duplicate scenarios in comprehensive test");

        // Should not crash or produce false positives for valid scenarios
        Assert.That(diagnostics.Any(d => d.Id == Diagnostics.MissingKey.Id), Is.False,
            "Should not produce missing key diagnostic when valid key exists");
    }
}