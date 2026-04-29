using System.Collections.Generic;
using JetBrains.Annotations;
using JetBrains.DocumentModel;
using JetBrains.ReSharper.Feature.Services.Occurrences;
using JetBrains.ReSharper.Psi;

namespace ReSharperPlugin.CollectionUsageFinder.Search
{
    internal sealed class CollectionUsageRangeOccurrence : CustomRangeOccurrence, IOccurrenceKindOwner
    {
        private readonly CollectionUsageOccurrence occurrence;
        private readonly ICollection<OccurrenceKind> kinds;

        public CollectionUsageRangeOccurrence(
            [NotNull] IPsiSourceFile sourceFile,
            DocumentRange documentRange,
            [NotNull] CollectionUsageOccurrence occurrence)
            : base(sourceFile, documentRange, OccurrencePresentationOptions.DefaultOptions)
        {
            this.occurrence = occurrence;
            kinds = new[] { CollectionUsageOccurrenceKinds.ToOccurrenceKind(occurrence.Kind) };
        }

        public ICollection<OccurrenceKind> Kinds => kinds;

        public override string GetDisplayNameText()
        {
            return CollectionUsageOccurrenceKinds.ToDisplayText(occurrence.Kind) + ": " + occurrence.Text;
        }

        public override string DumpToString()
        {
            return GetDisplayNameText() + " at " + SourceFile.DisplayName + ":" + occurrence.Line + ":" + occurrence.Column;
        }
    }
}
