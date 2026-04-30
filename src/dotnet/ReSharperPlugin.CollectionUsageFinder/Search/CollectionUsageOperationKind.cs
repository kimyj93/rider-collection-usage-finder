namespace ReSharperPlugin.CollectionUsageFinder.Search
{
    public enum CollectionUsageOperationKind
    {
        ElementAdd,
        ElementRemove,
        ElementClear,
        ElementSet,
        CollectionReorder,
        SetOperation,
        CollectionAssignment,
        ElementContentWrite,
        ElementReference
    }
}
