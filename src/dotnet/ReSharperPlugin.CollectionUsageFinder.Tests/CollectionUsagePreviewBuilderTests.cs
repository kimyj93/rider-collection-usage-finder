using NUnit.Framework;
using ReSharperPlugin.CollectionUsageFinder.Actions;

namespace ReSharperPlugin.CollectionUsageFinder.Tests
{
    [TestFixture]
    public class CollectionUsagePreviewBuilderTests
    {
        [Test]
        public void Build_IncludesSurroundingLinesAndRelativeHighlight()
        {
            var source = string.Join("\n", new[]
            {
                "line 1",
                "line 2",
                "line 3",
                "var target = list[0];",
                "line 5",
                "line 6",
                "line 7"
            });
            var targetOffset = source.IndexOf("target", System.StringComparison.Ordinal);

            var preview = CollectionUsagePreviewBuilder.Build(source, targetOffset, "target".Length, 2);

            Assert.That(preview.StartLine, Is.EqualTo(2));
            Assert.That(preview.Text, Does.StartWith("line 2\nline 3\n"));
            Assert.That(preview.Text, Does.Contain("var target = list[0];"));
            Assert.That(preview.Text, Does.EndWith("line 6"));
            Assert.That(preview.HighlightStart, Is.EqualTo(preview.Text.IndexOf("target", System.StringComparison.Ordinal)));
            Assert.That(preview.HighlightLength, Is.EqualTo("target".Length));
        }

        [Test]
        public void Build_ClampsContextAtFileEdges()
        {
            const string source = "target.Add(item);\nline 2\nline 3";

            var preview = CollectionUsagePreviewBuilder.Build(source, 0, "target".Length, 4);

            Assert.That(preview.StartLine, Is.EqualTo(1));
            Assert.That(preview.Text, Is.EqualTo(source));
            Assert.That(preview.HighlightStart, Is.EqualTo(0));
            Assert.That(preview.HighlightLength, Is.EqualTo("target".Length));
        }
    }
}
