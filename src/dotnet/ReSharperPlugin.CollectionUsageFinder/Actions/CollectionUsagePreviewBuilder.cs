using System;
using System.Collections.Generic;
using JetBrains.Annotations;

namespace ReSharperPlugin.CollectionUsageFinder.Actions
{
    internal static class CollectionUsagePreviewBuilder
    {
        [NotNull]
        public static CollectionUsagePreviewSource CreateSource([NotNull] string sourceText)
        {
            return new CollectionUsagePreviewSource(sourceText);
        }

        [NotNull]
        public static CollectionUsagePreview Build(
            [NotNull] string sourceText,
            int startOffset,
            int length,
            int contextLineCount)
        {
            return CreateSource(sourceText).Build(startOffset, length, contextLineCount);
        }
    }

    internal sealed class CollectionUsagePreviewSource
    {
        [NotNull] private readonly string sourceText;
        [NotNull] private readonly List<int> lineStarts;

        public CollectionUsagePreviewSource([NotNull] string sourceText)
        {
            this.sourceText = sourceText ?? throw new ArgumentNullException(nameof(sourceText));
            lineStarts = GetLineStarts(sourceText);
        }

        [NotNull]
        public CollectionUsagePreview Build(int startOffset, int length, int contextLineCount)
        {
            if (sourceText.Length == 0)
                return new CollectionUsagePreview(string.Empty, 1, 0, 0);

            var normalizedOffset = Math.Max(0, Math.Min(startOffset, sourceText.Length - 1));
            var occurrenceLineIndex = GetLineIndex(lineStarts, normalizedOffset);
            var startLineIndex = Math.Max(0, occurrenceLineIndex - contextLineCount);
            var endLineIndex = Math.Min(lineStarts.Count - 1, occurrenceLineIndex + contextLineCount);
            var previewStartOffset = lineStarts[startLineIndex];
            var previewEndOffset = endLineIndex + 1 < lineStarts.Count
                ? TrimPreviousLineBreak(sourceText, lineStarts[endLineIndex + 1])
                : sourceText.Length;
            var previewLength = Math.Max(0, previewEndOffset - previewStartOffset);
            var previewText = sourceText.Substring(previewStartOffset, previewLength);
            var highlightStart = Math.Max(0, Math.Min(normalizedOffset - previewStartOffset, previewText.Length));
            var highlightLength = Math.Max(0, Math.Min(Math.Max(1, length), previewText.Length - highlightStart));

            return new CollectionUsagePreview(
                previewText,
                startLineIndex + 1,
                highlightStart,
                highlightLength);
        }

        [NotNull]
        private static List<int> GetLineStarts([NotNull] string text)
        {
            var lineStarts = new List<int> { 0 };
            for (var i = 0; i < text.Length; i++)
            {
                if (text[i] == '\n' && i + 1 < text.Length)
                    lineStarts.Add(i + 1);
            }

            return lineStarts;
        }

        private static int GetLineIndex([NotNull] IList<int> lineStarts, int offset)
        {
            var low = 0;
            var high = lineStarts.Count - 1;
            while (low <= high)
            {
                var mid = low + ((high - low) / 2);
                if (lineStarts[mid] <= offset)
                    low = mid + 1;
                else
                    high = mid - 1;
            }

            return Math.Max(0, high);
        }

        private static int TrimPreviousLineBreak([NotNull] string text, int offset)
        {
            var result = offset;
            if (result > 0 && text[result - 1] == '\n')
                result--;
            if (result > 0 && text[result - 1] == '\r')
                result--;
            return result;
        }
    }

    internal sealed class CollectionUsagePreview
    {
        public CollectionUsagePreview(
            [NotNull] string text,
            int startLine,
            int highlightStart,
            int highlightLength)
        {
            Text = text;
            StartLine = startLine;
            HighlightStart = highlightStart;
            HighlightLength = highlightLength;
        }

        [NotNull]
        public string Text { get; }

        public int StartLine { get; }

        public int HighlightStart { get; }

        public int HighlightLength { get; }
    }
}
