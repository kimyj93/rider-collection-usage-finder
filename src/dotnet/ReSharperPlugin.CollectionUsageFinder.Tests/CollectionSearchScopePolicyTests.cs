using NUnit.Framework;
using ReSharperPlugin.CollectionUsageFinder.Search;

namespace ReSharperPlugin.CollectionUsageFinder.Tests
{
    [TestFixture]
    public class CollectionSearchScopePolicyTests
    {
        [TestCase(CollectionSearchTargetKind.LocalVariable)]
        [TestCase(CollectionSearchTargetKind.Parameter)]
        public void GetSearchScope_ReturnsCurrentFile_ForLocalKinds(CollectionSearchTargetKind targetKind)
        {
            var scope = CollectionSearchScopePolicy.GetSearchScope(targetKind);

            Assert.That(scope, Is.EqualTo(CollectionSearchScopeKind.CurrentFile));
        }

        [TestCase(CollectionSearchTargetKind.Field)]
        [TestCase(CollectionSearchTargetKind.Property)]
        public void GetSearchScope_ReturnsDeclaringProject_ForMemberKinds(CollectionSearchTargetKind targetKind)
        {
            var scope = CollectionSearchScopePolicy.GetSearchScope(targetKind);

            Assert.That(scope, Is.EqualTo(CollectionSearchScopeKind.DeclaringProject));
        }
    }
}
