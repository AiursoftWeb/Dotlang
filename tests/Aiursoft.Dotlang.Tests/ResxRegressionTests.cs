using System.Text.RegularExpressions;
using Aiursoft.Dotlang.AspNetTranslate.Services;
using Aiursoft.Canon;
using Aiursoft.Dotlang.Shared;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Reflection;

namespace Aiursoft.Dotlang.Tests
{
    [TestClass]
    public class ResxRegressionTests
    {
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
                null!,
                new CshtmlLocalizer(),
                new CachedTranslateEngine(null!, null!, null!),
                new NullLogger<TranslateEntry>()
            );

            // Act
            var method = typeof(TranslateEntry).GetMethod("LocalizeContentInCSharp", BindingFlags.NonPublic | BindingFlags.Instance);
            if (method == null) Assert.Fail("Method LocalizeContentInCSharp not found");

            await (Task)method.Invoke(entry, new object[] { tempDir, dummyCsPath, "zh-CN", true, 1 });

            // Assert
            var expectedResxPath = Path.Combine(resourcesDir, "NoKeys.zh-CN.resx");

            // It should NOT exist
            Assert.IsFalse(File.Exists(expectedResxPath), $"File {expectedResxPath} should not have been created.");

            Directory.Delete(tempDir, true);
        }
    }
}
