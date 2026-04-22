using Aiursoft.Dotlang.AspNetTranslate.Services;
using Aiursoft.Canon;
using Aiursoft.Dotlang.Shared;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Reflection;

namespace Aiursoft.Dotlang.Tests
{
    [TestClass]
    public class ResxCasePreservationTests
    {
        [TestMethod]
        public async Task TestCasePreservationAndConflictWarning()
        {
            // Arrange
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);

            var resourcesDir = Path.Combine(tempDir, "Resources");

            // Cs file triggers "Go To" and "go to" which are case-insensitively the same key.
            // IStringLocalizer uses case-insensitive matching, so only the first occurrence should be preserved.
            var dummyCsPath = Path.Combine(tempDir, "CaseTest.cs");
            await File.WriteAllTextAsync(dummyCsPath, @"
public class CaseTest {
    public void Method(IStringLocalizer localizer) {
        var a = localizer[""Go To""];
        var b = localizer[""go to""];
    }
}");

            var mockEngine = new Mock<CachedTranslateEngine>(null!, null!, null!);
            mockEngine.Setup(x => x.TranslateWordInParagraphAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string _, string w, string _, CancellationToken _) => w);

            var entry = new TranslateEntry(
                new DataAnnotationKeyExtractor(),
                new CSharpKeyExtractor(),
                new ViewMetadataExtractor(),
                new CanonPool(new NullLogger<CanonPool>()),
                new CshtmlLocalizer(),
                mockEngine.Object,
                new NullLogger<TranslateEntry>()
            );

            // Act
            var method = typeof(TranslateEntry).GetMethod("LocalizeContentInCSharp", BindingFlags.NonPublic | BindingFlags.Instance);
            if (method == null) Assert.Fail("Method LocalizeContentInCSharp not found");

            await (Task)method.Invoke(entry, new object[] { tempDir, dummyCsPath, "zh-CN", true, 1, CancellationToken.None })!;

            // Assert
            var expectedResxPath = Path.Combine(resourcesDir, "CaseTest.zh-CN.resx");
            Assert.IsTrue(File.Exists(expectedResxPath));

            var content = await File.ReadAllTextAsync(expectedResxPath);
            Console.WriteLine(content);

            // Should contain "Go To" (first occurrence wins)
            StringAssert.Contains(content, "name=\"Go To\"");
            // "go to" should NOT appear as a separate key since it's case-insensitively the same as "Go To"
            // and IStringLocalizer uses case-insensitive matching.
            var goToLowerCount = content.Split("name=\"go to\"").Length - 1;
            Assert.AreEqual(0, goToLowerCount, "Key 'go to' should not appear as a separate key since it is case-insensitively duplicate of 'Go To'");

            Directory.Delete(tempDir, true);
        }
    }
}
