using JetBrains.Annotations;

namespace ReSharperPlugin.CollectionUsageFinder.Search
{
    public sealed class CollectionUsageOccurrence
    {
        public CollectionUsageOccurrence(
            CollectionUsageKind kind,
            int startOffset,
            int length,
            int line,
            int column,
            [NotNull] string text)
        {
            Kind = kind;
            StartOffset = startOffset;
            Length = length;
            Line = line;
            Column = column;
            Text = text;
        }

        public CollectionUsageKind Kind { get; }

        public int StartOffset { get; }

        public int Length { get; }

        public int Line { get; }

        public int Column { get; }

        [NotNull]
        public string Text { get; }
    }
}
