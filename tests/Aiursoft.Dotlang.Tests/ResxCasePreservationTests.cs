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

            // Cs file triggers "Go To" and "go to"
            // "Go To" appears first.
            // "go to" appears second.
            // Expected: "Go To" is generated. "go to" is ignored (conflict).
            var dummyCsPath = Path.Combine(tempDir, "CaseTest.cs");
            await File.WriteAllTextAsync(dummyCsPath, @"
public class CaseTest { 
    public void Method(IStringLocalizer localizer) { 
        var a = localizer[""Go To""]; 
        var b = localizer[""go to""]; 
    } 
}");

            var mockEngine = new Mock<CachedTranslateEngine>(null!, null!, null!);
            mockEngine.Setup(x => x.TranslateWordInParagraphAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync((string _, string w, string _) => w);

            var entry = new TranslateEntry(
                new DataAnnotationKeyExtractor(),
                new CSharpKeyExtractor(),
                new CanonPool(new NullLogger<CanonPool>()),
                new CshtmlLocalizer(),
                mockEngine.Object,
                new NullLogger<TranslateEntry>()
            );

            // Act
            var method = typeof(TranslateEntry).GetMethod("LocalizeContentInCSharp", BindingFlags.NonPublic | BindingFlags.Instance);
            if (method == null) Assert.Fail("Method LocalizeContentInCSharp not found");

            await (Task)method.Invoke(entry, new object[] { tempDir, dummyCsPath, "zh-CN", true, 1 })!;

            // Assert
            var expectedResxPath = Path.Combine(resourcesDir, "CaseTest.zh-CN.resx");
            Assert.IsTrue(File.Exists(expectedResxPath));

            var content = await File.ReadAllTextAsync(expectedResxPath);
            Console.WriteLine(content);

            // Should contain "Go To"
            StringAssert.Contains(content, "name=\"Go To\"");
            // Should NOT contain "go to" as a key
            // Note: Since we are not doing strict valid XML parsing here but string check, 
            // ensure we don't accidentally match part of something else.
            // But "name=\"go to\"" is specific enough.
            Assert.DoesNotContain("name=\"go to\"", content);

            Directory.Delete(tempDir, true);
        }
    }
}
