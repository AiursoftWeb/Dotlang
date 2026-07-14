using Aiursoft.Dotlang.AspNetTranslate.Services;
using Aiursoft.Dotlang.Shared;
using Microsoft.Extensions.Logging.Abstractions;
using System.Reflection;

namespace Aiursoft.Dotlang.Tests
{
    [TestClass]
    public class ResxRegressionTests
    {
        [TestMethod]
        public void GenerateXml_EscapesLiteralFormatPlaceholdersInValuesOnly()
        {
            var entries = new Dictionary<string, string>
            {
                ["/api/{id}"] = "/api/{name}",
                ["Composite format"] = "Value: {0}, price: {1:N2}",
                ["Escaped literal"] = "Path: {{path}}"
            };

            var method = typeof(TranslateEntry).GetMethod("GenerateXml", BindingFlags.NonPublic | BindingFlags.Static);
            if (method == null) Assert.Fail("Method GenerateXml not found");

            var xml = (string)method.Invoke(null, new object[] { entries })!;

            StringAssert.Contains(xml, "name=\"/api/{id}\"");
            StringAssert.Contains(xml, "<value>/api/{{name}}</value>");
            StringAssert.Contains(xml, "<value>Value: {0}, price: {1:N2}</value>");
            StringAssert.Contains(xml, "<value>Path: {{path}}</value>");
        }

        [TestMethod]
        public async Task TestNoResxGeneratedForNoKeys()
        {
            // Arrange
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);

            var resourcesDir = Path.Combine(tempDir, "Resources");

            var dummyCsPath = Path.Combine(tempDir, "NoKeys.cs");
            await File.WriteAllTextAsync(dummyCsPath, "public class NoKeys { public void Method() { Console.WriteLine(\"Hello\"); } }");

            var entry = new TranslateEntry(
                new DataAnnotationKeyExtractor(),
                new CSharpKeyExtractor(),
                new ViewMetadataExtractor(),
                null!,
                new CshtmlLocalizer(),
                new CachedTranslateEngine(null!, null!, null!),
                new NullLogger<TranslateEntry>()
            );

            // Act
            var method = typeof(TranslateEntry).GetMethod("LocalizeContentInCSharp", BindingFlags.NonPublic | BindingFlags.Instance);
            if (method == null) Assert.Fail("Method LocalizeContentInCSharp not found");

            await (Task)method.Invoke(entry, new object[] { tempDir, dummyCsPath, "zh-CN", true, 1, CancellationToken.None })!;

            // Assert
            var expectedResxPath = Path.Combine(resourcesDir, "NoKeys.zh-CN.resx");

            // It should NOT exist
            Assert.IsFalse(File.Exists(expectedResxPath), $"File {expectedResxPath} should not have been created.");

            Directory.Delete(tempDir, true);
        }
    }
}
