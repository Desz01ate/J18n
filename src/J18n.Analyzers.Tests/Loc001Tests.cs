namespace J18n.Analyzers.Tests;

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
}