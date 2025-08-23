namespace J18n.Analyzers.Tests;

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
}