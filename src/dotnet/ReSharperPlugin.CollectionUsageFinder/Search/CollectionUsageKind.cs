namespace ReSharperPlugin.CollectionUsageFinder.Search
{
    public enum CollectionUsageKind
    {
        CollectionStructureUsage,
        CollectionAssignment,
        ElementWrite,
        ElementAlias,
        ElementEscape,
        CollectionRead
    }
}
