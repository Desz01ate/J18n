namespace J18n.Analyzers.Tests;

using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Verifier;

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
}