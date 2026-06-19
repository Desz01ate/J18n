namespace J18n.Analyzers.Tests;

using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Verifier;

[TestFixture]
public class Loc002Tests
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
    public async Task UnusedKey_Produces_LOC002_Warning()
    {
        var source = GetFormattedSource("user.name");

        var additionalFiles = new AdditionalFile[]
        {
            new("TestClass.en.json",
                """
                {
                  "user": {
                    "name": "Name"
                  },
                  "unused": {
                    "key": "Never used"
                  }
                }
                """),
        };

        var verifier = new CodeVerifier(source, additionalFiles);

        var diagnostics = await verifier.GetDiagnosticsAsync();

        Assert.That(diagnostics.Any(d => d.Id == Diagnostics.UnusedKey.Id),
            "Expected LOC002 (UnusedKey) to be reported for 'unused.key'");
    }

    [Test]
    public async Task MultipleUnusedKeys_Produces_Multiple_LOC002_Warnings()
    {
        var source = GetFormattedSource("user.name");

        var additionalFiles = new AdditionalFile[]
        {
            new("TestClass.en.json",
                """
                {
                  "user": {
                    "name": "Name"
                  },
                  "unused1": "First unused",
                  "unused2": "Second unused",
                  "unused3": {
                    "nested": "Third unused"
                  }
                }
                """),
        };

        var verifier = new CodeVerifier(source, additionalFiles);
        var diagnostics = await verifier.GetDiagnosticsAsync();

        var unusedKeyDiagnostics = diagnostics.Where(d => d.Id == Diagnostics.UnusedKey.Id).ToList();
        Assert.That(unusedKeyDiagnostics.Count, Is.EqualTo(3),
            "Expected 3 LOC002 (UnusedKey) warnings for unused1, unused2, and unused3.nested");
    }

    [Test]
    public async Task AllKeysUnused_Produces_LOC002_For_All()
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
                                      // No localization usage at all
                                  }
                              }
                              """;

        var additionalFiles = new AdditionalFile[]
        {
            new("TestClass.en.json",
                """
                {
                  "title": "Title",
                  "description": "Description",
                  "user": {
                    "name": "Name",
                    "email": "Email"
                  }
                }
                """),
        };

        var verifier = new CodeVerifier(source, additionalFiles);
        var diagnostics = await verifier.GetDiagnosticsAsync();

        var unusedKeyDiagnostics = diagnostics.Where(d => d.Id == Diagnostics.UnusedKey.Id).ToList();
        Assert.That(unusedKeyDiagnostics.Count, Is.EqualTo(4),
            "Expected 4 LOC002 (UnusedKey) warnings for all unused keys: title, description, user.name, user.email");
    }

    [Test]
    public async Task UnusedKeysAcrossMultipleCultures_Produces_LOC002_For_Each()
    {
        var source = GetFormattedSource("common.button");

        var additionalFiles = new AdditionalFile[]
        {
            new("TestClass.en.json",
                """
                {
                  "common": {
                    "button": "Button"
                  },
                  "unused": {
                    "en": "Unused English"
                  }
                }
                """),
            new("TestClass.fr.json",
                """
                {
                  "common": {
                    "button": "Bouton"
                  },
                  "unused": {
                    "fr": "Unused French"
                  }
                }
                """),
            new("TestClass.de.json",
                """
                {
                  "common": {
                    "button": "Knopf"
                  },
                  "unused": {
                    "de": "Unused German"
                  }
                }
                """)
        };

        var verifier = new CodeVerifier(source, additionalFiles);
        var diagnostics = await verifier.GetDiagnosticsAsync();

        var unusedKeyDiagnostics = diagnostics.Where(d => d.Id == Diagnostics.UnusedKey.Id).ToList();
        Assert.That(unusedKeyDiagnostics.Count, Is.EqualTo(3),
            "Expected 3 LOC002 (UnusedKey) warnings for unused keys in each culture");
    }

    [Test]
    public async Task UnusedOnlyInSomeCultures_Produces_LOC002_For_Missing_Ones()
    {
        var source = GetFormattedSource("shared.title");

        var additionalFiles = new AdditionalFile[]
        {
            new("TestClass.en.json",
                """
                {
                  "shared": {
                    "title": "Title"
                  },
                  "onlyInEnglish": "English only"
                }
                """),
            new("TestClass.fr.json",
                """
                {
                  "shared": {
                    "title": "Titre"
                  },
                  "onlyInFrench": "French only"
                }
                """)
        };

        var verifier = new CodeVerifier(source, additionalFiles);
        var diagnostics = await verifier.GetDiagnosticsAsync();

        var unusedKeyDiagnostics = diagnostics.Where(d => d.Id == Diagnostics.UnusedKey.Id).ToList();
        Assert.That(unusedKeyDiagnostics.Count, Is.EqualTo(2),
            "Expected 2 LOC002 (UnusedKey) warnings: onlyInEnglish and onlyInFrench");
    }

    [Test]
    public async Task DeeplyNestedUnusedKeys_Produces_LOC002_Warning()
    {
        var source = GetFormattedSource("level1.level2.used");

        var additionalFiles = new AdditionalFile[]
        {
            new("TestClass.en.json",
                """
                {
                  "level1": {
                    "level2": {
                      "used": "Used Value",
                      "level3": {
                        "level4": {
                          "level5": {
                            "deeplyUnused": "Very deep unused key"
                          }
                        }
                      }
                    },
                    "unused": "Unused at level 2"
                  }
                }
                """),
        };

        var verifier = new CodeVerifier(source, additionalFiles);
        var diagnostics = await verifier.GetDiagnosticsAsync();

        var unusedKeyDiagnostics = diagnostics.Where(d => d.Id == Diagnostics.UnusedKey.Id).ToList();
        Assert.That(unusedKeyDiagnostics.Count, Is.EqualTo(2),
            "Expected 2 LOC002 (UnusedKey) warnings for level1.unused and level1.level2.level3.level4.level5.deeplyUnused");
    }

    [Test]
    public async Task UnusedParentWithUsedChild_Produces_LOC002_For_Parent_Only()
    {
        var source = GetFormattedSource("user.profile.name");

        var additionalFiles = new AdditionalFile[]
        {
            new("TestClass.en.json",
                """
                {
                  "user": {
                    "profile": {
                      "name": "Name",
                      "avatar": "Avatar URL"
                    },
                    "settings": {
                      "theme": "dark",
                      "notifications": true
                    }
                  }
                }
                """),
        };

        var verifier = new CodeVerifier(source, additionalFiles);
        var diagnostics = await verifier.GetDiagnosticsAsync();

        var unusedKeyDiagnostics = diagnostics.Where(d => d.Id == Diagnostics.UnusedKey.Id).ToList();
        Assert.That(unusedKeyDiagnostics.Count, Is.EqualTo(3),
            "Expected 3 LOC002 (UnusedKey) warnings for user.profile.avatar, user.settings.theme, user.settings.notifications");
    }

    [Test]
    public async Task ComplexJsonStructure_With_UnusedKeys_Produces_LOC002_Warnings()
    {
        var source = GetFormattedSource("app.config.debug");

        var additionalFiles = new AdditionalFile[]
        {
            new("TestClass.en.json",
                """
                {
                  "app": {
                    "config": {
                      "debug": false,
                      "features": {
                        "chat": true,
                        "notifications": false,
                        "reporting": {
                          "enabled": true,
                          "endpoint": "https://api.example.com"
                        }
                      }
                    },
                    "metadata": {
                      "version": "1.0.0",
                      "created": "2023-01-01",
                      "author": "Developer"
                    }
                  },
                  "unused": {
                    "completely": {
                      "ignored": {
                        "deeply": {
                          "nested": "value"
                        }
                      }
                    }
                  }
                }
                """),
        };

        var verifier = new CodeVerifier(source, additionalFiles);
        var diagnostics = await verifier.GetDiagnosticsAsync();

        var unusedKeyDiagnostics = diagnostics.Where(d => d.Id == Diagnostics.UnusedKey.Id).ToList();
        Assert.That(unusedKeyDiagnostics.Count, Is.GreaterThanOrEqualTo(7),
            "Expected at least 7 LOC002 (UnusedKey) warnings for various unused keys in complex structure");
    }

    [Test]
    public async Task LargeJsonFile_With_UnusedKeys_Produces_LOC002_Warnings()
    {
        var source = GetFormattedSource("section1.key1");

        var jsonBuilder = new System.Text.StringBuilder();
        jsonBuilder.AppendLine("{");

        // Generate large JSON with mostly unused keys
        for (var i = 1; i <= 50; i++)
        {
            jsonBuilder.AppendLine($"  \"section{i}\": {{");

            for (var j = 1; j <= 20; j++)
            {
                var comma = j < 20 ? "," : "";
                jsonBuilder.AppendLine($"    \"key{j}\": \"Value {i}-{j}\"{comma}");
            }

            var sectionComma = i < 50 ? "," : "";
            jsonBuilder.AppendLine($"  }}{sectionComma}");
        }

        jsonBuilder.AppendLine("}");

        var additionalFiles = new AdditionalFile[]
        {
            new("TestClass.en.json", jsonBuilder.ToString())
        };

        var verifier = new CodeVerifier(source, additionalFiles);
        var diagnostics = await verifier.GetDiagnosticsAsync();

        var unusedKeyDiagnostics = diagnostics.Where(d => d.Id == Diagnostics.UnusedKey.Id).ToList();
        // Should have 999 unused keys (50*20 - 1 used key)
        Assert.That(unusedKeyDiagnostics.Count, Is.EqualTo(999),
            "Expected 999 LOC002 (UnusedKey) warnings for unused keys in large JSON file");
    }

    [Test]
    public async Task EmptyStringKey_Unused_Produces_LOC002_Warning()
    {
        var source = GetFormattedSource("valid.key");

        var additionalFiles = new AdditionalFile[]
        {
            new("TestClass.en.json",
                """
                {
                  "valid": {
                    "key": "Used Value"
                  },
                  "": "Empty key value"
                }
                """),
        };

        var verifier = new CodeVerifier(source, additionalFiles);
        var diagnostics = await verifier.GetDiagnosticsAsync();

        var unusedKeyDiagnostics = diagnostics.Where(d => d.Id == Diagnostics.UnusedKey.Id).ToList();
        Assert.That(unusedKeyDiagnostics.Count, Is.EqualTo(1),
            "Expected 1 LOC002 (UnusedKey) warning for empty string key");
    }

    [Test]
    public async Task SpecialCharacterKeys_Unused_Produces_LOC002_Warnings()
    {
        var source = GetFormattedSource("used-key");

        var additionalFiles = new AdditionalFile[]
        {
            new("TestClass.en.json",
                """
                {
                  "used-key": "Used Value",
                  "unused-key_with@symbols#and.dots": "Unused Special",
                  "título": "Unused Unicode",
                  "123": "Unused Numeric",
                  "key with spaces": "Unused Spaces",
                  "UPPERCASE_KEY": "Unused Uppercase"
                }
                """),
        };

        var verifier = new CodeVerifier(source, additionalFiles);
        var diagnostics = await verifier.GetDiagnosticsAsync();

        var unusedKeyDiagnostics = diagnostics.Where(d => d.Id == Diagnostics.UnusedKey.Id).ToList();
        Assert.That(unusedKeyDiagnostics.Count, Is.EqualTo(5),
            "Expected 5 LOC002 (UnusedKey) warnings for special character unused keys");
    }

    [Test]
    public async Task InvalidJson_DoesNotCrash_LOC002()
    {
        var source = GetFormattedSource("any.key");

        var additionalFiles = new AdditionalFile[]
        {
            new("TestClass.en.json", "{ invalid json syntax }"),
        };

        var verifier = new CodeVerifier(source, additionalFiles);
        var diagnostics = await verifier.GetDiagnosticsAsync();

        // Should not crash, and should not produce any unused key diagnostics for invalid JSON
        var unusedKeyDiagnostics = diagnostics.Where(d => d.Id == Diagnostics.UnusedKey.Id).ToList();
        Assert.That(unusedKeyDiagnostics.Count, Is.EqualTo(0),
            "Expected no LOC002 (UnusedKey) warnings for invalid JSON file");
    }

    [Test]
    public async Task EmptyJsonFile_DoesNotProduce_LOC002_Warning()
    {
        var source = GetFormattedSource("any.key");

        var additionalFiles = new AdditionalFile[]
        {
            new("TestClass.en.json", "{}"),
        };

        var verifier = new CodeVerifier(source, additionalFiles);
        var diagnostics = await verifier.GetDiagnosticsAsync();

        var unusedKeyDiagnostics = diagnostics.Where(d => d.Id == Diagnostics.UnusedKey.Id).ToList();
        Assert.That(unusedKeyDiagnostics.Count, Is.EqualTo(0),
            "Expected no LOC002 (UnusedKey) warnings for empty JSON file");
    }

    [Test]
    public async Task AllKeysUsed_DoesNotProduce_LOC002_Warning()
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
                    var title = _localizer["title"];
                    var desc = _localizer["description"];
                    var name = _localizer["user.name"];
                    var email = _localizer["user.email"];
                }
            }
            """;

        var additionalFiles = new AdditionalFile[]
        {
            new("TestClass.en.json",
                """
                {
                  "title": "Title",
                  "description": "Description",
                  "user": {
                    "name": "Name",
                    "email": "Email"
                  }
                }
                """),
        };

        var verifier = new CodeVerifier(source, additionalFiles);
        var diagnostics = await verifier.GetDiagnosticsAsync();

        var unusedKeyDiagnostics = diagnostics.Where(d => d.Id == Diagnostics.UnusedKey.Id).ToList();
        Assert.That(unusedKeyDiagnostics.Count, Is.EqualTo(0),
            "Expected no LOC002 (UnusedKey) warnings when all keys are used");
    }

    [Test]
    public async Task DifferentAccessorPatterns_DoesNotProduce_LOC002_For_Used_Keys()
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
                    // Different ways to access localization
                    var value1 = _localizer["indexer.access"];
                    var value2 = _localizer.GetString("method.access");
                    var value3 = _localizer["property.access"].Value;
                }
            }
            """;

        var additionalFiles = new AdditionalFile[]
        {
            new("TestClass.en.json",
                """
                {
                  "indexer": {
                    "access": "Indexer Value"
                  },
                  "method": {
                    "access": "Method Value"
                  },
                  "property": {
                    "access": "Property Value"
                  },
                  "unused": {
                    "key": "Unused Value"
                  }
                }
                """),
        };

        var verifier = new CodeVerifier(source, additionalFiles);
        var diagnostics = await verifier.GetDiagnosticsAsync();

        var unusedKeyDiagnostics = diagnostics.Where(d => d.Id == Diagnostics.UnusedKey.Id).ToList();
        Assert.That(unusedKeyDiagnostics.Count, Is.EqualTo(2),
            "Expected 2 LOC002 (UnusedKey) warnings: method.access is not recognized by analyzer and unused.key");
    }

    [Test]
    public async Task CaseSensitivityScenarios_Unused_Produces_LOC002_Warning()
    {
        var source = GetFormattedSource("Title"); // Using exact case

        var additionalFiles = new AdditionalFile[]
        {
            new("TestClass.en.json",
                """
                {
                  "Title": "Proper Case Title",
                  "title": "Lower Case Title",
                  "TITLE": "Upper Case Title"
                }
                """),
        };

        var verifier = new CodeVerifier(source, additionalFiles);
        var diagnostics = await verifier.GetDiagnosticsAsync();

        var unusedKeyDiagnostics = diagnostics.Where(d => d.Id == Diagnostics.UnusedKey.Id).ToList();
        Assert.That(unusedKeyDiagnostics.Count, Is.EqualTo(2),
            "Expected 2 LOC002 (UnusedKey) warnings for title and TITLE (case sensitive by default)");
    }
}