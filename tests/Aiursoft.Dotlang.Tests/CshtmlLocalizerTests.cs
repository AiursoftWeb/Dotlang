using Aiursoft.Dotlang.AspNetTranslate.Services;

namespace Aiursoft.Dotlang.Tests
{
    [TestClass]
    public class CshtmlLocalizerTests
    {
        [TestMethod]
        public void TestExtractLocalizerKeys()
        {
            var localizer = new CshtmlLocalizer();
            var content = "<div>\n    <h1>@Localizer[\"Title\"]</h1>\n    <p>@Localizer[\"Content\"]</p>\n    <span>@Localizer[\"Escaped \\\"Quote\\\"\"]</span>\n</div>";

            var keys = localizer.ExtractLocalizerKeys(content);

            Assert.AreEqual(3, keys.Length);
            CollectionAssert.Contains(keys, "Title");
            CollectionAssert.Contains(keys, "Content");
            // CshtmlLocalizer unescapes \" to "
            CollectionAssert.Contains(keys, "Escaped \"Quote\"");
        }

        [TestMethod]
        public void TestProcess()
        {
            var localizer = new CshtmlLocalizer();
            var content = "<div>\n    <h1>Hello World</h1>\n    <p>This is a test.</p>\n</div>";

            var (transformed, keys) = localizer.Process(content);

            // Verify keys extracted
            Assert.AreEqual(2, keys.Count);
            CollectionAssert.Contains(keys, "Hello World");
            CollectionAssert.Contains(keys, "This is a test.");

            // Verify transformation
            StringAssert.Contains(transformed, "@Localizer[\"Hello World\"]");
            StringAssert.Contains(transformed, "@Localizer[\"This is a test.\"]");
        }

        [TestMethod]
        public void TestProcessWithExistingLocalizer()
        {
            var localizer = new CshtmlLocalizer();
            var content = "<div>\n    <p>@Localizer[\"Existing\"]</p>\n    <p>New Text</p>\n</div>";

            var (transformed, keys) = localizer.Process(content);

            Assert.AreEqual(1, keys.Count);
            CollectionAssert.Contains(keys, "New Text");

            StringAssert.Contains(transformed, "@Localizer[\"Existing\"]");
            StringAssert.Contains(transformed, "@Localizer[\"New Text\"]");
        }

        [TestMethod]
        public void TestProcessIgnoreRazor()
        {
             var localizer = new CshtmlLocalizer();
             var content = "@{ \n    var x = \"Don't translate me\"; \n}\n<div>\n    <p>Translate Me</p>\n</div>";
            
            var (transformed, keys) = localizer.Process(content);

            Assert.AreEqual(1, keys.Count);
            CollectionAssert.Contains(keys, "Translate Me");
            
            // Should not translate C# variable
            Assert.IsFalse(keys.Contains("Don't translate me"));
        }
    }
}
