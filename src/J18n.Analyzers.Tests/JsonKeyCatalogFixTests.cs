namespace J18n.Analyzers.Tests;

using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using NUnit.Framework;
using Verifier;

/// <summary>
/// Tests for X2 (array key extraction) and A2 (culture inference) fixes in JsonKeyCatalog.
/// </summary>
[TestFixture]
public class JsonKeyCatalogFixTests
{
    // -------------------------------------------------------------------------
    // Source template helpers
    // -------------------------------------------------------------------------

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

    private static string GetFormattedSource(string key) =>
        SourceTemplate.Replace("$key", key);

    // =========================================================================
    // X2: Array key extraction
    // =========================================================================

    /// <summary>
    /// items[0] and items[1] must be valid keys (no LOC001) when the JSON has
    /// { "items": ["a", "b"] }.
    /// </summary>
    [Test]
    public async Task ArrayElement_IndexedKey_NoLOC001()
    {
        var source = GetFormattedSource("items[0]");

        var additionalFiles = new AdditionalFile[]
        {
            new("TestClass.en.json", """{ "items": ["a", "b"] }""")
        };

        var verifier = new CodeVerifier(source, additionalFiles);
        var diagnostics = await verifier.GetDiagnosticsAsync();

        var loc001 = diagnostics.Where(d => d.Id == Diagnostics.MissingKey.Id).ToList();
        Assert.That(loc001, Is.Empty,
            "Expected no LOC001 for items[0] when JSON contains array ['a','b']");
    }

    [Test]
    public async Task ArrayElement_SecondIndex_NoLOC001()
    {
        var source = GetFormattedSource("items[1]");

        var additionalFiles = new AdditionalFile[]
        {
            new("TestClass.en.json", """{ "items": ["a", "b"] }""")
        };

        var verifier = new CodeVerifier(source, additionalFiles);
        var diagnostics = await verifier.GetDiagnosticsAsync();

        var loc001 = diagnostics.Where(d => d.Id == Diagnostics.MissingKey.Id).ToList();
        Assert.That(loc001, Is.Empty,
            "Expected no LOC001 for items[1] when JSON contains array ['a','b']");
    }

    /// <summary>
    /// The bare container key "items" must no longer be a valid key once the array
    /// is expanded into indexed entries — it must produce LOC001.
    /// </summary>
    [Test]
    public async Task ArrayContainer_BareKey_ProducesLOC001()
    {
        var source = GetFormattedSource("items");

        var additionalFiles = new AdditionalFile[]
        {
            new("TestClass.en.json", """{ "items": ["a", "b"] }""")
        };

        var verifier = new CodeVerifier(source, additionalFiles);

        await verifier.VerifyDiagnostic(
            DiagnosticResult.Create(
                Diagnostics.MissingKey.Id,
                DiagnosticSeverity.Error,
                14,
                32,
                "Localization key 'items' is not found in any configured culture"));
    }

    /// <summary>
    /// A nested array-of-objects: list[0].name must be a valid key.
    /// </summary>
    [Test]
    public async Task NestedArrayOfObjects_IndexedDotKey_NoLOC001()
    {
        var source = GetFormattedSource("list[0].name");

        var additionalFiles = new AdditionalFile[]
        {
            new("TestClass.en.json", """{ "list": [ { "name": "x" } ] }""")
        };

        var verifier = new CodeVerifier(source, additionalFiles);
        var diagnostics = await verifier.GetDiagnosticsAsync();

        var loc001 = diagnostics.Where(d => d.Id == Diagnostics.MissingKey.Id).ToList();
        Assert.That(loc001, Is.Empty,
            "Expected no LOC001 for list[0].name when JSON has nested array of objects");
    }

    /// <summary>
    /// Array elements must appear as used keys — no LOC002 should fire for items[0] when it is referenced.
    /// </summary>
    [Test]
    public async Task ArrayElement_Used_NoLOC002()
    {
        var source = GetFormattedSource("items[0]");

        var additionalFiles = new AdditionalFile[]
        {
            new("TestClass.en.json", """{ "items": ["a", "b"] }""")
        };

        var verifier = new CodeVerifier(source, additionalFiles);
        var diagnostics = await verifier.GetDiagnosticsAsync();

        // items[1] is unused → expect LOC002 for it, but NOT for items[0]
        var loc002 = diagnostics.Where(d => d.Id == Diagnostics.UnusedKey.Id).ToList();
        Assert.That(loc002.Any(d => d.GetMessage().Contains("items[0]")), Is.False,
            "items[0] is used so must not appear as an unused key");
        Assert.That(loc002.Any(d => d.GetMessage().Contains("items[1]")), Is.True,
            "items[1] is not referenced, so it must produce LOC002");
    }

    /// <summary>
    /// A deeply nested array key like a[0].b[0].c must be correctly extracted.
    /// </summary>
    [Test]
    public async Task DeepNestedArray_KeyExtracted_NoLOC001()
    {
        var source = GetFormattedSource("a[0].b[0].c");

        var additionalFiles = new AdditionalFile[]
        {
            new("TestClass.en.json", """{ "a": [ { "b": [ { "c": "value" } ] } ] }""")
        };

        var verifier = new CodeVerifier(source, additionalFiles);
        var diagnostics = await verifier.GetDiagnosticsAsync();

        var loc001 = diagnostics.Where(d => d.Id == Diagnostics.MissingKey.Id).ToList();
        Assert.That(loc001, Is.Empty,
            "Expected no LOC001 for deeply nested array key a[0].b[0].c");
    }

    // =========================================================================
    // A2: Culture inference — exact token matching
    // =========================================================================

    /// <summary>
    /// A file named Home.en.json must be inferred as culture "en" (filename suffix).
    /// Verified indirectly: the key must be found in culture "en" (no LOC001).
    /// </summary>
    [Test]
    public async Task CultureFromFilenameSuffix_En_KeyFound_NoLOC001()
    {
        var source = GetFormattedSource("greeting");

        var additionalFiles = new AdditionalFile[]
        {
            new("Home.en.json", """{ "greeting": "Hello" }""")
        };

        var verifier = new CodeVerifier(source, additionalFiles);
        var diagnostics = await verifier.GetDiagnosticsAsync();

        var loc001 = diagnostics.Where(d => d.Id == Diagnostics.MissingKey.Id).ToList();
        Assert.That(loc001, Is.Empty,
            "Expected no LOC001: Home.en.json should be recognized as culture 'en'");
    }

    /// <summary>
    /// A neutral file Home.json (no culture suffix) must be inferred as "default",
    /// NOT mistaken for "es" (because "Resources" contains "es").
    ///
    /// Observable effect: With old bug, "Resources/Home.json" → "es" (spurious culture).
    /// Combined with TestClass.en.json, effective cultures = ["en", "es"].
    /// greeting exists in "es" (the neutral file) but NOT in "en" → LOC003 fires.
    /// With the fix, "Resources/Home.json" → "default" (filtered), only "en" is effective.
    /// Only 1 effective culture → LOC003 is suppressed. No LOC001 (greeting is in _allKeys).
    /// </summary>
    [Test]
    public async Task NeutralFile_InResourcesFolder_NotMisclassifiedAsEs()
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
                    var value = _localizer["greeting"];
                }
            }
            """;

        var additionalFiles = new AdditionalFile[]
        {
            // Neutral file in Resources folder — must NOT be treated as culture "es"
            new("Resources/Home.json", """{ "greeting": "Hello" }"""),
            // Real "en" culture file — does NOT have greeting
            new("TestClass.en.json", """{ "other": "Hi" }"""),
        };

        var verifier = new CodeVerifier(source, additionalFiles);
        var diagnostics = await verifier.GetDiagnosticsAsync();

        // With fix: "default" is filtered → effective cultures = ["en"] (1 culture) → no LOC003.
        // greeting exists in _allKeys (from the neutral file) → no LOC001.
        // With old bug: spurious "es" → 2 cultures → LOC003 for "greeting" missing in "en".
        var loc003 = diagnostics.Where(d => d.Id == Diagnostics.PartialMissingKey.Id).ToList();
        Assert.That(loc003, Is.Empty,
            "Expected no LOC003: Resources/Home.json must be 'default', not a spurious 'es' culture");

        // greeting is in _allKeys → no LOC001 (the neutral file's keys are still checked)
        var loc001 = diagnostics.Where(d => d.Id == Diagnostics.MissingKey.Id).ToList();
        Assert.That(loc001, Is.Empty,
            "Expected no LOC001: greeting exists in the neutral file's key catalog");
    }

    /// <summary>
    /// A culture-as-folder layout Resources/fr/Home.json must infer "fr".
    /// Verified: greeting in that file counts as present in fr culture.
    /// Combined with a TestClass.en.json, a key in both → no LOC003 for either.
    /// </summary>
    [Test]
    public async Task CultureAsDirectorySegment_Fr_KeyFound()
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
                    var value = _localizer["greeting"];
                }
            }
            """;

        var additionalFiles = new AdditionalFile[]
        {
            new("Resources/fr/Home.json", """{ "greeting": "Bonjour" }"""),
            new("TestClass.en.json", """{ "greeting": "Hello" }"""),
        };

        var verifier = new CodeVerifier(source, additionalFiles);
        var diagnostics = await verifier.GetDiagnosticsAsync();

        // greeting is in both en and fr → no LOC001, no LOC003
        var loc001 = diagnostics.Where(d => d.Id == Diagnostics.MissingKey.Id).ToList();
        Assert.That(loc001, Is.Empty,
            "Expected no LOC001: greeting is present in both en and fr");

        var loc003 = diagnostics.Where(d => d.Id == Diagnostics.PartialMissingKey.Id).ToList();
        Assert.That(loc003, Is.Empty,
            "Expected no LOC003: greeting is present in both cultures");
    }

    /// <summary>
    /// A neutral file named "Stress.json" must be inferred as "default", not "es",
    /// even though "Stress" contains "es" as a substring (via commonCultures IndexOf).
    ///
    /// Observable effect: With old bug, "Stress.json" → "es" (spurious culture).
    /// Combined with TestClass.en.json, effective cultures = ["en", "es"].
    /// greeting exists in "es" (Stress.json) but NOT in "en" → LOC003 fires.
    /// With the fix, "Stress.json" → "default" (filtered), only "en" is effective.
    /// Only 1 effective culture → LOC003 is suppressed. No LOC001 (greeting is in _allKeys).
    /// </summary>
    [Test]
    public async Task NeutralFile_WithSubstringCultureName_NotMisclassified()
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
                    var value = _localizer["greeting"];
                }
            }
            """;

        // "Stress.json" contains "es" as substring — must not be misidentified as "es".
        // Combined with a real "en" file that does NOT have greeting:
        //   - Old bug: Stress.json → "es", greeting found in "es" but not "en" → LOC003
        //   - Fix: Stress.json → "default" (filtered), only 1 culture ("en") → no LOC003
        var additionalFiles = new AdditionalFile[]
        {
            new("Stress.json", """{ "greeting": "Hello" }"""),
            new("TestClass.en.json", """{ "other": "World" }"""),
        };

        var verifier = new CodeVerifier(source, additionalFiles);
        var diagnostics = await verifier.GetDiagnosticsAsync();

        // With fix: Stress.json → "default" (filtered), effective cultures = ["en"] → no LOC003.
        // With old bug: spurious "es" → 2 cultures → LOC003 for "greeting".
        var loc003 = diagnostics.Where(d => d.Id == Diagnostics.PartialMissingKey.Id).ToList();
        Assert.That(loc003, Is.Empty,
            "Expected no LOC003: Stress.json must not be misidentified as a real 'es' culture");

        // greeting exists in _allKeys (from Stress.json/default) → no LOC001
        var loc001 = diagnostics.Where(d => d.Id == Diagnostics.MissingKey.Id).ToList();
        Assert.That(loc001, Is.Empty,
            "Expected no LOC001: greeting exists in the neutral file's key catalog");
    }
}
