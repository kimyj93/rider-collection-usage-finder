using JetBrains.Annotations;

namespace ReSharperPlugin.CollectionUsageFinder.Search
{
    public sealed class CollectionUsageOccurrence
    {
        public CollectionUsageOccurrence(
            CollectionUsageKind kind,
            CollectionUsageOperationKind operationKind,
            [NotNull] string operationName,
            int startOffset,
            int length,
            int line,
            int column,
            [NotNull] string text)
        {
            Kind = kind;
            OperationKind = operationKind;
            OperationName = operationName;
            StartOffset = startOffset;
            Length = length;
            Line = line;
            Column = column;
            Text = text;
        }

        public CollectionUsageKind Kind { get; }

        public CollectionUsageOperationKind OperationKind { get; }

        [NotNull]
        public string OperationName { get; }

        public int StartOffset { get; }

        public int Length { get; }

        public int Line { get; }

        public int Column { get; }

        [NotNull]
        public string Text { get; }
    }
}
