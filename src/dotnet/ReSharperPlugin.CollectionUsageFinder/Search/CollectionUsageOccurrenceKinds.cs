using JetBrains.ReSharper.Feature.Services.Occurrences;

namespace ReSharperPlugin.CollectionUsageFinder.Search
{
    internal static class CollectionUsageOccurrenceKinds
    {
        public static readonly OccurrenceKind CollectionStructureUsage =
            OccurrenceKind.CreateSemantic("Collection Structure Usage", true);

        public static readonly OccurrenceKind CollectionAssignment =
            OccurrenceKind.CreateSemantic("Collection Assignment", true);

        public static readonly OccurrenceKind ElementWrite =
            OccurrenceKind.CreateSemantic("Element Write", true);

        public static readonly OccurrenceKind ElementAlias =
            OccurrenceKind.CreateSemantic("Element Alias", true);

        public static readonly OccurrenceKind ElementEscape =
            OccurrenceKind.CreateSemantic("Element Escape", true);

        public static OccurrenceKind ToOccurrenceKind(CollectionUsageKind kind)
        {
            switch (kind)
            {
                case CollectionUsageKind.CollectionStructureUsage:
                    return CollectionStructureUsage;

                case CollectionUsageKind.CollectionAssignment:
                    return CollectionAssignment;

                case CollectionUsageKind.ElementWrite:
                    return ElementWrite;

                case CollectionUsageKind.ElementAlias:
                    return ElementAlias;

                case CollectionUsageKind.ElementEscape:
                    return ElementEscape;

                default:
                    return OccurrenceKind.Other;
            }
        }

        public static string ToDisplayText(CollectionUsageKind kind)
        {
            switch (kind)
            {
                case CollectionUsageKind.CollectionStructureUsage:
                    return "Collection Structure Usage";

                case CollectionUsageKind.CollectionAssignment:
                    return "Collection Assignment";

                case CollectionUsageKind.ElementWrite:
                    return "Element Write";

                case CollectionUsageKind.ElementAlias:
                    return "Element Alias";

                case CollectionUsageKind.ElementEscape:
                    return "Element Escape";

                default:
                    return "Collection Usage";
            }
        }
    }
}
