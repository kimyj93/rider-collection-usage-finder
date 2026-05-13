namespace ReSharperPlugin.CollectionUsageFinder.Search
{
    public enum CollectionUsageKind
    {
        CollectionStructureUsage,
        CollectionAssignment,
        ElementWrite,
        ElementMethodCall,
        ElementAlias,
        ElementEscape,
        CollectionEscape,
        CollectionRead
    }
}
