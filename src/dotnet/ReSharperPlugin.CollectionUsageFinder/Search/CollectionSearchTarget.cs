using JetBrains.ReSharper.Psi;

namespace ReSharperPlugin.CollectionUsageFinder.Search
{
    public enum CollectionSearchTargetKind
    {
        LocalVariable,
        Field,
        Property,
        Parameter
    }

    public enum SupportedCollectionKind
    {
        List,
        ICollection,
        IList,
        Collection,
        Dictionary,
        HashSet,
        ISet,
        Queue,
        Stack,
        Array
    }

    public sealed class CollectionSearchTarget
    {
        public CollectionSearchTarget(
            IDeclaredElement declaredElement,
            string displayName,
            CollectionSearchTargetKind targetKind,
            SupportedCollectionKind collectionKind)
        {
            DeclaredElement = declaredElement;
            DisplayName = displayName;
            TargetKind = targetKind;
            CollectionKind = collectionKind;
        }

        public IDeclaredElement DeclaredElement { get; }

        public string DisplayName { get; }

        public CollectionSearchTargetKind TargetKind { get; }

        public SupportedCollectionKind CollectionKind { get; }
    }
}
