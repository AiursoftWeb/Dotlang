using Aiursoft.Dotlang.AspNetTranslate.Services;

namespace Aiursoft.Dotlang.Tests
{
    [TestClass]
    public class KeyExtractorsTests
    {
        [TestMethod]
        public void TestCSharpKeyExtractor()
        {
            var extractor = new CSharpKeyExtractor();
            // Using regular string to avoid confusing escaping issues with verbatim strings
            var content =
                "public class MyClass\n" +
                "{\n" +
                "    public void MyMethod(IStringLocalizer localizer)\n" +
                "    {\n" +
                "        var t1 = localizer[\"Hello\"];\n" +
                "        var t2 = localizer[\"World\"];\n" +
                "        var t4 = localizer[\"Hello\"]; // Duplicate\n" +
                "    }\n" +
                "}";

            var keys = extractor.ExtractLocalizerKeys(content);

            Assert.HasCount(2, keys);
            CollectionAssert.Contains(keys, "Hello");
            CollectionAssert.Contains(keys, "World");
        }

        [TestMethod]
        public void TestDataAnnotationKeyExtractor()
        {
            var extractor = new DataAnnotationKeyExtractor();
            var content =
                "public class MyModel\n" +
                "{\n" +
                "    [Required(ErrorMessage = \"Required Field\")]\n" +
                "    [Display(Name = \"User Name\")]\n" +
                "    public string Name { get; set; }\n" +
                "\n" +
                "    [StringLength(10, ErrorMessage = \"Too Long\")]\n" +
                "    public string Description { get; set; }\n" +
                "}";

            var keys = extractor.ExtractKeys(content);

            Assert.HasCount(3, keys);
            CollectionAssert.Contains(keys, "Required Field");
            CollectionAssert.Contains(keys, "User Name");
            CollectionAssert.Contains(keys, "Too Long");
        }

        [TestMethod]
        public void TestViewMetadataExtractor()
        {
            var extractor = new ViewMetadataExtractor();
            var content1 =
                "[RenderInNavBar(NavGroupName = \"Dashboard\", LinkText = \"Overview\")]\n" +
                "public class HomeController : Controller\n" +
                "{\n" +
                "    [RenderInNavBar(CascadedLinksGroupName = \"Settings\", LinkText = \"Profile\")]\n" +
                "    public IActionResult Profile()\n" +
                "    {\n" +
                "        return View();\n" +
                "    }\n" +
                "}";

            var keys1 = extractor.ExtractKeys(content1);

            Assert.HasCount(4, keys1);
            CollectionAssert.Contains(keys1, "Dashboard");
            CollectionAssert.Contains(keys1, "Overview");
            CollectionAssert.Contains(keys1, "Settings");
            CollectionAssert.Contains(keys1, "Profile");

            var content2 =
                "using Aiursoft.UiStack.Layout;\n" +
                "\n" +
                "namespace Aiursoft.Template.Models.DashboardViewModels;\n" +
                "\n" +
                "public class IndexViewModel : UiStackLayoutViewModel\n" +
                "{\n" +
                "    public IndexViewModel()\n" +
                "    {\n" +
                "        PageTitle = \"Dashboard\";\n" +
                "        PageTitle = \"My Dashboard\";\n" +
                "    }\n" +
                "}\n";

            var keys2 = extractor.ExtractKeys(content2);

            Assert.HasCount(2, keys2);
            CollectionAssert.Contains(keys2, "Dashboard");
            CollectionAssert.Contains(keys2, "My Dashboard");
        }

        [TestMethod]
        public void TestExtractorsWithEmptyContent()
        {
            var csharpExtractor = new CSharpKeyExtractor();
            var dataAnnotationExtractor = new DataAnnotationKeyExtractor();
            var viewMetadataExtractor = new ViewMetadataExtractor();

            Assert.IsEmpty(csharpExtractor.ExtractLocalizerKeys(null!));
            Assert.IsEmpty(csharpExtractor.ExtractLocalizerKeys(""));

            Assert.IsEmpty(dataAnnotationExtractor.ExtractKeys(null!));
            Assert.IsEmpty(dataAnnotationExtractor.ExtractKeys(""));

            Assert.IsEmpty(viewMetadataExtractor.ExtractKeys(null!));
            Assert.IsEmpty(viewMetadataExtractor.ExtractKeys(""));
        }
    }
}