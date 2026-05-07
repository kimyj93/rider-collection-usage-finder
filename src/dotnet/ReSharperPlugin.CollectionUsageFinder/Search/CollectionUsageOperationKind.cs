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
        CollectionInitialization,
        CollectionAssignment,
        ElementContentWrite,
        ElementReference,
        ElementRead,
        ConditionRead,
        EnumerationRead,
        CopyRead
    }
}
