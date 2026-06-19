namespace J18n.Analyzers.Tests;

using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Verifier;

/// <summary>
/// Tests for fix X1 (const key recognition) and fix A3 (exact type-name matching).
/// </summary>
[TestFixture]
public class ConstKeyAndSubstringMatchTests
{
    // -------------------------------------------------------------------------
    // X1: Analyzer recognizes const string field references as localization keys
    // -------------------------------------------------------------------------

    /// <summary>
    /// A const string field referencing an EXISTING key, used as localizer[Foo.Key],
    /// must NOT produce LOC001 (key found) and must NOT produce LOC002 (key is "used").
    /// </summary>
    [Test]
    public async Task ConstFieldKey_ExistingKey_NoLOC001_NoLOC002()
    {
        const string source = """
            using Microsoft.Extensions.Localization;

            public static class Keys
            {
                public const string Welcome = "home.welcome";
            }

            public class TestClass
            {
                private readonly IStringLocalizer<TestClass> _localizer;

                public TestClass(IStringLocalizer<TestClass> localizer)
                {
                    _localizer = localizer;
                }

                public void TestMethod()
                {
                    var value = _localizer[Keys.Welcome];
                }
            }
            """;

        var additionalFiles = new AdditionalFile[]
        {
            new("TestClass.en.json", """{ "home": { "welcome": "Welcome!" } }""")
        };

        var verifier = new CodeVerifier(source, additionalFiles);
        var diagnostics = await verifier.GetDiagnosticsAsync();

        var loc001 = diagnostics.Where(d => d.Id == Diagnostics.MissingKey.Id).ToList();
        Assert.That(loc001, Is.Empty,
            "Expected no LOC001 when a const field references an existing key");

        var loc002 = diagnostics.Where(d => d.Id == Diagnostics.UnusedKey.Id).ToList();
        Assert.That(loc002, Is.Empty,
            "Expected no LOC002: const key usage should count as 'used'");
    }

    /// <summary>
    /// A const string field referencing a key that is NOT in any JSON must produce LOC001.
    /// </summary>
    [Test]
    public async Task ConstFieldKey_MissingKey_ProducesLOC001()
    {
        const string source = """
            using Microsoft.Extensions.Localization;

            public static class Keys
            {
                public const string Missing = "home.missing";
            }

            public class TestClass
            {
                private readonly IStringLocalizer<TestClass> _localizer;

                public TestClass(IStringLocalizer<TestClass> localizer)
                {
                    _localizer = localizer;
                }

                public void TestMethod()
                {
                    var value = _localizer[Keys.Missing];
                }
            }
            """;

        var additionalFiles = new AdditionalFile[]
        {
            new("TestClass.en.json", """{ "home": { "welcome": "Welcome!" } }""")
        };

        var verifier = new CodeVerifier(source, additionalFiles);
        var diagnostics = await verifier.GetDiagnosticsAsync();

        var loc001 = diagnostics.Where(d => d.Id == Diagnostics.MissingKey.Id).ToList();
        Assert.That(loc001, Is.Not.Empty,
            "Expected LOC001 when a const field references a key not in any JSON");

        Assert.That(loc001.Any(d => d.GetMessage().Contains("home.missing")), Is.True,
            "LOC001 message should mention the missing key 'home.missing'");
    }

    /// <summary>
    /// A non-constant (runtime) variable used as the indexer argument must NOT produce
    /// any diagnostic (dynamic keys are intentionally skipped).
    /// </summary>
    [Test]
    public async Task NonConstantVariableKey_NoLOC001()
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

                public void TestMethod(string dynamicKey)
                {
                    var value = _localizer[dynamicKey];
                }
            }
            """;

        var additionalFiles = new AdditionalFile[]
        {
            new("TestClass.en.json", """{ "static": { "key": "Value" } }""")
        };

        var verifier = new CodeVerifier(source, additionalFiles);
        var diagnostics = await verifier.GetDiagnosticsAsync();

        var loc001 = diagnostics.Where(d => d.Id == Diagnostics.MissingKey.Id).ToList();
        Assert.That(loc001, Is.Empty,
            "Expected no LOC001 for a non-constant (runtime) variable indexer argument");
    }

    // -------------------------------------------------------------------------
    // A3: Exact type-name matching — substring names must NOT be treated as
    //     localization accessors
    // -------------------------------------------------------------------------

    /// <summary>
    /// An indexer on a type whose display string CONTAINS "IStringLocalizer" as a substring
    /// but which does NOT implement IStringLocalizer must NOT produce LOC001 (MissingKey).
    ///
    /// Under the buggy .Contains implementation, "IStringLocalizerExtensions" would
    /// falsely match the default accessor config for "IStringLocalizer", causing a false
    /// LOC001 for any key referenced via that type's indexer.
    /// </summary>
    [Test]
    public async Task SubstringTypeName_DoesNotMatchAccessor_NoLOC001()
    {
        // IStringLocalizerExtensions has "IStringLocalizer" as a substring in its name
        // but does NOT implement IStringLocalizer.
        const string source = """
            using Microsoft.Extensions.Localization;

            public class IStringLocalizerExtensions
            {
                public string this[string key] => key;
            }

            public class TestClass
            {
                private readonly IStringLocalizerExtensions _localizerExt;

                public TestClass(IStringLocalizerExtensions localizerExt)
                {
                    _localizerExt = localizerExt;
                }

                public void TestMethod()
                {
                    // "some.missing.key" is not in any JSON file.
                    // Under the old .Contains bug, this would trigger LOC001 because
                    // IStringLocalizerExtensions was falsely matched as a localizer.
                    var value = _localizerExt["some.missing.key"];
                }
            }
            """;

        // Empty JSON – "some.missing.key" is absent, so a false LOC001 would fire
        // under the old code.
        var additionalFiles = new AdditionalFile[]
        {
            new("TestClass.en.json", """{}""")
        };

        var verifier = new CodeVerifier(source, additionalFiles);
        var diagnostics = await verifier.GetDiagnosticsAsync();

        var loc001 = diagnostics.Where(d => d.Id == Diagnostics.MissingKey.Id).ToList();
        Assert.That(loc001, Is.Empty,
            "Expected no LOC001: IStringLocalizerExtensions is not a localization accessor, " +
            "even though its name contains 'IStringLocalizer' as a substring");
    }

    /// <summary>
    /// Regression: the real IStringLocalizer<T> indexer must still be detected (LOC001 when key missing).
    /// This ensures the exact-match fix does not break the default accessor detection.
    /// </summary>
    [Test]
    public async Task RealIStringLocalizer_Indexer_StillDetected_LOC001()
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
                    var value = _localizer["completely.missing"];
                }
            }
            """;

        var additionalFiles = new AdditionalFile[]
        {
            new("TestClass.en.json", """{ "other": "value" }""")
        };

        var verifier = new CodeVerifier(source, additionalFiles);
        var diagnostics = await verifier.GetDiagnosticsAsync();

        var loc001 = diagnostics.Where(d => d.Id == Diagnostics.MissingKey.Id).ToList();
        Assert.That(loc001, Is.Not.Empty,
            "Regression: IStringLocalizer<T> indexer must still trigger LOC001 after exact-match fix");
    }
}
