using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using NUnit.Framework;

namespace J18n.Analyzers.Tests;

using Verifier;

[TestFixture]
public class DiagnosticsUnitTests
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
}