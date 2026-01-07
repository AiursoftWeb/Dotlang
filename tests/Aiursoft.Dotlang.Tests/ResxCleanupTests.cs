using Aiursoft.Dotlang.AspNetTranslate.Services;
using Aiursoft.Dotlang.Shared;
using Microsoft.Extensions.Logging.Abstractions;
using System.Reflection;

namespace Aiursoft.Dotlang.Tests
{
    [TestClass]
    public class ResxCleanupTests
    {
        [TestMethod]
        public async Task TestNormalizeAndDeduplicateResx()
        {
            // Arrange
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);

            // The tool writes to [ProjectRoot]/Resources/[RelativePathToCs].zh-CN.resx
            // Our Cs file is at [ProjectRoot]/Test.cs
            // So ResX should be at [ProjectRoot]/Resources/Test.zh-CN.resx
            var resourcesDir = Path.Combine(tempDir, "Resources");
            Directory.CreateDirectory(resourcesDir);
            var resxPath = Path.Combine(resourcesDir, "Test.zh-CN.resx");

            var initialResx = @"<?xml version=""1.0"" encoding=""utf-8""?>
<root>
    <data name=""Duplicate"" xml:space=""preserve"">
        <value>Value1</value>
    </data>
    <data name=""duplicate"" xml:space=""preserve"">
        <value>Value2</value>
    </data>
    <data name=""Normal"" xml:space=""preserve"">
        <value>NormalValue</value>
    </data>
</root>";
            await File.WriteAllTextAsync(resxPath, initialResx);

            var dummyCsPath = Path.Combine(tempDir, "Test.cs");
            await File.WriteAllTextAsync(dummyCsPath, "public class Test {}");

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

            await (Task)method.Invoke(entry, new object[] { tempDir, dummyCsPath, "zh-CN", true, 1 })!;

            // Assert
            var newContent = await File.ReadAllTextAsync(resxPath);

            StringAssert.Contains(newContent, "name=\"duplicate\"");
            StringAssert.Contains(newContent, "name=\"normal\"");

            Assert.DoesNotContain(newContent, "name=\"Duplicate\"");
            Assert.DoesNotContain(newContent, "name=\"Normal\"");

            StringAssert.Contains(newContent, "<value>Value1</value>");
            Assert.DoesNotContain(newContent, "<value>Value2</value>");

            Directory.Delete(tempDir, true);
        }
    }
}
