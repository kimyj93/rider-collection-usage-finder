namespace ReSharperPlugin.CollectionUsageFinder.Search
{
    public enum CollectionSearchScopeKind
    {
        CurrentFile,
        DeclaringProject
    }

    public static class CollectionSearchScopePolicy
    {
        public static CollectionSearchScopeKind GetSearchScope(CollectionSearchTargetKind targetKind)
        {
            switch (targetKind)
            {
                case CollectionSearchTargetKind.Field:
                case CollectionSearchTargetKind.Property:
                    return CollectionSearchScopeKind.DeclaringProject;

                case CollectionSearchTargetKind.LocalVariable:
                case CollectionSearchTargetKind.Parameter:
                default:
                    return CollectionSearchScopeKind.CurrentFile;
            }
        }
    }
}
