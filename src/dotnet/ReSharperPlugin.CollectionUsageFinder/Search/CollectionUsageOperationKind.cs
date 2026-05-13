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
        CollectionReference,
        ByRefCollectionReference,
        ElementRead,
        ConditionRead,
        ConditionComparisonRead,
        EnumerationRead,
        CopyRead,
        QueryRead,
        DirectElementMethodCall
    }
}
