using NUnit.Framework;
using ReSharperPlugin.CollectionUsageFinder.Search;

namespace ReSharperPlugin.CollectionUsageFinder.Tests
{
    [TestFixture]
    public class CollectionSupportTests
    {
        [TestCase("System.Collections.Generic.List", SupportedCollectionKind.List)]
        [TestCase("System.Collections.Generic.List`1", SupportedCollectionKind.List)]
        [TestCase("System.Collections.Generic.ICollection", SupportedCollectionKind.ICollection)]
        [TestCase("System.Collections.Generic.ICollection`1", SupportedCollectionKind.ICollection)]
        [TestCase("System.Collections.Generic.IList", SupportedCollectionKind.IList)]
        [TestCase("System.Collections.Generic.IList`1", SupportedCollectionKind.IList)]
        [TestCase("System.Collections.ObjectModel.Collection", SupportedCollectionKind.Collection)]
        [TestCase("System.Collections.ObjectModel.Collection`1", SupportedCollectionKind.Collection)]
        [TestCase("System.Collections.Generic.Dictionary", SupportedCollectionKind.Dictionary)]
        [TestCase("System.Collections.Generic.Dictionary`2", SupportedCollectionKind.Dictionary)]
        [TestCase("System.Collections.Generic.IDictionary", SupportedCollectionKind.Dictionary)]
        [TestCase("System.Collections.Generic.IDictionary`2", SupportedCollectionKind.Dictionary)]
        [TestCase("System.Collections.Generic.HashSet", SupportedCollectionKind.HashSet)]
        [TestCase("System.Collections.Generic.HashSet`1", SupportedCollectionKind.HashSet)]
        [TestCase("System.Collections.Generic.ISet", SupportedCollectionKind.ISet)]
        [TestCase("System.Collections.Generic.ISet`1", SupportedCollectionKind.ISet)]
        [TestCase("System.Collections.Generic.Queue", SupportedCollectionKind.Queue)]
        [TestCase("System.Collections.Generic.Queue`1", SupportedCollectionKind.Queue)]
        [TestCase("System.Collections.Generic.Stack", SupportedCollectionKind.Stack)]
        [TestCase("System.Collections.Generic.Stack`1", SupportedCollectionKind.Stack)]
        public void TryGetSupportedCollectionKind_ReturnsExpectedKind_ForSupportedClrNames(
            string clrTypeName,
            SupportedCollectionKind expectedKind)
        {
            var actualKind = CollectionSupport.TryGetSupportedCollectionKind(clrTypeName, isArray: false);

            Assert.That(actualKind, Is.EqualTo(expectedKind));
        }

        [Test]
        public void TryGetSupportedCollectionKind_ReturnsArray_ForArrays()
        {
            var actualKind = CollectionSupport.TryGetSupportedCollectionKind(clrTypeName: null, isArray: true);

            Assert.That(actualKind, Is.EqualTo(SupportedCollectionKind.Array));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("System.String")]
        [TestCase("System.Collections.Generic.SortedSet`1")]
        [TestCase("MyCompany.CustomCollection`1")]
        public void TryGetSupportedCollectionKind_ReturnsNull_ForUnsupportedClrNames(string clrTypeName)
        {
            var actualKind = CollectionSupport.TryGetSupportedCollectionKind(clrTypeName, isArray: false);

            Assert.That(actualKind, Is.Null);
        }
    }
}
