using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using JetBrains.Annotations;

namespace ReSharperPlugin.CollectionUsageFinder.Search
{
    public sealed class CSharpCollectionUsageTextAnalyzer
    {
        private static readonly string[] StructuralUsageMemberNames =
        {
            "Add",
            "AddRange",
            "Insert",
            "InsertRange",
            "Remove",
            "RemoveAt",
            "RemoveRange",
            "RemoveAll",
            "Clear",
            "Sort",
            "Reverse",
            "TryAdd",
            "RemoveWhere",
            "UnionWith",
            "IntersectWith",
            "ExceptWith",
            "SymmetricExceptWith",
            "Enqueue",
            "Dequeue",
            "TryDequeue",
            "Push",
            "Pop",
            "TryPop"
        };

        [NotNull]
        public IReadOnlyList<CollectionUsageOccurrence> Analyze([NotNull] string sourceText, [NotNull] string targetName)
        {
            return Analyze(sourceText, targetName, static _ => true);
        }

        [NotNull]
        public IReadOnlyList<CollectionUsageOccurrence> Analyze(
            [NotNull] string sourceText,
            [NotNull] string targetName,
            [NotNull] Func<int, bool> isTargetReferenceAllowed)
        {
            if (string.IsNullOrWhiteSpace(sourceText) || string.IsNullOrWhiteSpace(targetName))
                return Array.Empty<CollectionUsageOccurrence>();

            var sanitizedText = CSharpTextSanitizer.Sanitize(sourceText);
            var targetPattern = Regex.Escape(targetName);
            var occurrences = new List<CollectionUsageOccurrence>();

            CollectStructuralUsages(sourceText, sanitizedText, targetPattern, isTargetReferenceAllowed, occurrences);
            CollectDirectElementWrites(sourceText, sanitizedText, targetPattern, isTargetReferenceAllowed, occurrences);
            var aliases = CollectAliases(sourceText, sanitizedText, targetPattern, isTargetReferenceAllowed, occurrences);
            CollectAliasElementWrites(sourceText, sanitizedText, aliases, occurrences);
            CollectEscapes(sourceText, sanitizedText, targetPattern, isTargetReferenceAllowed, occurrences);

            return occurrences
                .GroupBy(static occurrence => occurrence.Kind + ":" + occurrence.StartOffset)
                .Select(static group => group.First())
                .OrderBy(static occurrence => occurrence.StartOffset)
                .ToArray();
        }

        private static void CollectStructuralUsages(
            string sourceText,
            string sanitizedText,
            string targetPattern,
            Func<int, bool> isTargetReferenceAllowed,
            ICollection<CollectionUsageOccurrence> occurrences)
        {
            var structuralUsageMembers = string.Join("|", StructuralUsageMemberNames.Select(Regex.Escape));
            AddTargetMatches(
                sourceText,
                sanitizedText,
                $@"(?<target>\b{targetPattern})\s*\.\s*(?:{structuralUsageMembers})\s*\(",
                CollectionUsageKind.CollectionStructureUsage,
                isTargetReferenceAllowed,
                occurrences);

            AddTargetMatches(
                sourceText,
                sanitizedText,
                $@"(?<target>\b{targetPattern})\s*\[[^\]\r\n]+\]\s*(?:[+\-*/%&|^]?=|\+\+|--)",
                CollectionUsageKind.CollectionStructureUsage,
                isTargetReferenceAllowed,
                occurrences);
        }

        private static void CollectDirectElementWrites(
            string sourceText,
            string sanitizedText,
            string targetPattern,
            Func<int, bool> isTargetReferenceAllowed,
            ICollection<CollectionUsageOccurrence> occurrences)
        {
            AddTargetMatches(
                sourceText,
                sanitizedText,
                $@"(?<target>\b{targetPattern})\s*\[[^\]\r\n]+\]\s*\.\s*[A-Za-z_]\w*(?:\s*\.\s*[A-Za-z_]\w*)*\s*(?:[+\-*/%&|^]?=|\+\+|--)",
                CollectionUsageKind.ElementWrite,
                isTargetReferenceAllowed,
                occurrences);
        }

        private static IReadOnlyCollection<string> CollectAliases(
            string sourceText,
            string sanitizedText,
            string targetPattern,
            Func<int, bool> isTargetReferenceAllowed,
            ICollection<CollectionUsageOccurrence> occurrences)
        {
            var aliases = new HashSet<string>(StringComparer.Ordinal);

            foreach (Match match in Regex.Matches(
                         sanitizedText,
                         $@"\b(?:var|[A-Za-z_]\w*(?:\s*<[^;\r\n=]+>)?(?:\s*\[\])?)\s+([A-Za-z_]\w*)\s*=\s*(?<target>{targetPattern})\s*\[[^\]\r\n]+\]\s*;",
                         RegexOptions.Multiline))
            {
                if (!IsTargetMatchAllowed(match, isTargetReferenceAllowed))
                    continue;

                aliases.Add(match.Groups[1].Value);
                AddOccurrence(sourceText, match, CollectionUsageKind.ElementAlias, occurrences);
            }

            foreach (Match match in Regex.Matches(
                         sanitizedText,
                         $@"\bforeach\s*\(\s*(?:var|[A-Za-z_]\w*(?:\s*<[^;\r\n=]+>)?)\s+([A-Za-z_]\w*)\s+in\s+(?<target>{targetPattern})\s*\)",
                         RegexOptions.Multiline))
            {
                if (!IsTargetMatchAllowed(match, isTargetReferenceAllowed))
                    continue;

                aliases.Add(match.Groups[1].Value);
                AddOccurrence(sourceText, match, CollectionUsageKind.ElementAlias, occurrences);
            }

            return aliases;
        }

        private static void CollectAliasElementWrites(
            string sourceText,
            string sanitizedText,
            IEnumerable<string> aliases,
            ICollection<CollectionUsageOccurrence> occurrences)
        {
            foreach (var alias in aliases)
            {
                AddMatches(
                    sourceText,
                    sanitizedText,
                    $@"\b{Regex.Escape(alias)}\s*\.\s*[A-Za-z_]\w*(?:\s*\.\s*[A-Za-z_]\w*)*\s*(?:[+\-*/%&|^]?=|\+\+|--)",
                    CollectionUsageKind.ElementWrite,
                    occurrences);
            }
        }

        private static void CollectEscapes(
            string sourceText,
            string sanitizedText,
            string targetPattern,
            Func<int, bool> isTargetReferenceAllowed,
            ICollection<CollectionUsageOccurrence> occurrences)
        {
            AddTargetMatches(
                sourceText,
                sanitizedText,
                $@"\breturn\s+(?<target>{targetPattern})\s*\[[^\]\r\n]+\]\s*;",
                CollectionUsageKind.ElementEscape,
                isTargetReferenceAllowed,
                occurrences);

            AddTargetMatches(
                sourceText,
                sanitizedText,
                $@"(?:^|[;\{{]\s*)[A-Za-z_]\w*(?:\s*\.\s*[A-Za-z_]\w*)?\s*=\s*(?<target>{targetPattern})\s*\[[^\]\r\n]+\]\s*;",
                CollectionUsageKind.ElementEscape,
                isTargetReferenceAllowed,
                occurrences);

            AddTargetMatches(
                sourceText,
                sanitizedText,
                $@"\b(?!if\b|for\b|foreach\b|while\b|switch\b|using\b|lock\b|return\b)[A-Za-z_]\w*(?:\s*\.\s*[A-Za-z_]\w*)?\s*\([^;\r\n]*(?<target>\b{targetPattern})\s*\[[^\]\r\n]+\](?!\s*\.)[^;\r\n]*\)",
                CollectionUsageKind.ElementEscape,
                isTargetReferenceAllowed,
                occurrences);
        }

        private static void AddTargetMatches(
            string sourceText,
            string sanitizedText,
            string pattern,
            CollectionUsageKind kind,
            Func<int, bool> isTargetReferenceAllowed,
            ICollection<CollectionUsageOccurrence> occurrences)
        {
            foreach (Match match in Regex.Matches(sanitizedText, pattern, RegexOptions.Multiline))
            {
                if (!IsTargetMatchAllowed(match, isTargetReferenceAllowed))
                    continue;

                AddOccurrence(sourceText, match, kind, occurrences);
            }
        }

        private static bool IsTargetMatchAllowed(Match match, Func<int, bool> isTargetReferenceAllowed)
        {
            var targetGroup = match.Groups["target"];
            return targetGroup.Success && isTargetReferenceAllowed(targetGroup.Index);
        }

        private static void AddMatches(
            string sourceText,
            string sanitizedText,
            string pattern,
            CollectionUsageKind kind,
            ICollection<CollectionUsageOccurrence> occurrences)
        {
            foreach (Match match in Regex.Matches(sanitizedText, pattern, RegexOptions.Multiline))
                AddOccurrence(sourceText, match, kind, occurrences);
        }

        private static void AddOccurrence(
            string sourceText,
            Match match,
            CollectionUsageKind kind,
            ICollection<CollectionUsageOccurrence> occurrences)
        {
            var location = GetLineColumn(sourceText, match.Index);
            occurrences.Add(new CollectionUsageOccurrence(
                kind,
                match.Index,
                match.Length,
                location.Line,
                location.Column,
                GetLineText(sourceText, match.Index)));
        }

        private static (int Line, int Column) GetLineColumn(string text, int offset)
        {
            var line = 1;
            var column = 1;

            for (var i = 0; i < offset && i < text.Length; i++)
            {
                if (text[i] == '\n')
                {
                    line++;
                    column = 1;
                }
                else
                {
                    column++;
                }
            }

            return (line, column);
        }

        private static string GetLineText(string text, int offset)
        {
            var lineStart = offset;
            while (lineStart > 0 && text[lineStart - 1] != '\n' && text[lineStart - 1] != '\r')
                lineStart--;

            var lineEnd = offset;
            while (lineEnd < text.Length && text[lineEnd] != '\n' && text[lineEnd] != '\r')
                lineEnd++;

            return text.Substring(lineStart, lineEnd - lineStart).Trim();
        }

        private static class CSharpTextSanitizer
        {
            public static string Sanitize(string text)
            {
                var result = new StringBuilder(text.Length);
                var state = SanitizerState.Normal;

                for (var i = 0; i < text.Length; i++)
                {
                    var current = text[i];
                    var next = i + 1 < text.Length ? text[i + 1] : '\0';

                    switch (state)
                    {
                        case SanitizerState.Normal:
                            if (current == '/' && next == '/')
                            {
                                result.Append(' ');
                                result.Append(' ');
                                i++;
                                state = SanitizerState.LineComment;
                            }
                            else if (current == '/' && next == '*')
                            {
                                result.Append(' ');
                                result.Append(' ');
                                i++;
                                state = SanitizerState.BlockComment;
                            }
                            else if (current == '@' && next == '"')
                            {
                                result.Append(' ');
                                result.Append(' ');
                                i++;
                                state = SanitizerState.VerbatimString;
                            }
                            else if (current == '"')
                            {
                                result.Append(' ');
                                state = SanitizerState.String;
                            }
                            else if (current == '\'')
                            {
                                result.Append(' ');
                                state = SanitizerState.Character;
                            }
                            else
                            {
                                result.Append(current);
                            }

                            break;

                        case SanitizerState.LineComment:
                            AppendPreservingNewLine(result, current);
                            if (current == '\n')
                                state = SanitizerState.Normal;
                            break;

                        case SanitizerState.BlockComment:
                            AppendPreservingNewLine(result, current);
                            if (current == '*' && next == '/')
                            {
                                result.Append(' ');
                                i++;
                                state = SanitizerState.Normal;
                            }

                            break;

                        case SanitizerState.String:
                            AppendPreservingNewLine(result, current);
                            if (current == '\\' && next != '\0')
                            {
                                result.Append(next == '\n' ? '\n' : ' ');
                                i++;
                            }
                            else if (current == '"')
                            {
                                state = SanitizerState.Normal;
                            }

                            break;

                        case SanitizerState.VerbatimString:
                            AppendPreservingNewLine(result, current);
                            if (current == '"' && next == '"')
                            {
                                result.Append(' ');
                                i++;
                            }
                            else if (current == '"')
                            {
                                state = SanitizerState.Normal;
                            }

                            break;

                        case SanitizerState.Character:
                            AppendPreservingNewLine(result, current);
                            if (current == '\\' && next != '\0')
                            {
                                result.Append(next == '\n' ? '\n' : ' ');
                                i++;
                            }
                            else if (current == '\'')
                            {
                                state = SanitizerState.Normal;
                            }

                            break;
                    }
                }

                return result.ToString();
            }

            private static void AppendPreservingNewLine(StringBuilder builder, char value)
            {
                builder.Append(value == '\n' || value == '\r' ? value : ' ');
            }

            private enum SanitizerState
            {
                Normal,
                LineComment,
                BlockComment,
                String,
                VerbatimString,
                Character
            }
        }
    }
}
