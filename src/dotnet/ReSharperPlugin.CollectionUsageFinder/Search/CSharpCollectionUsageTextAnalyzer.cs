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

        private const string CollectionMemberAccessPattern = @"(?:\?\.\s*|\.\s*)";
        private const string CollectionIndexerPattern = @"(?:\?\s*)?\[(?:[^\[\]\r\n]|\[[^\]\r\n]*\])+\]";
        private const string MutationOperatorPattern = @"(?:\+\+|--|\?\?=|(?:>>>|<<|>>|[+\-*/%&|^])=|=(?!=|>))";
        private const string SingleCharacterCompoundAssignmentOperators = "+-*/%&|^";

        private static readonly string[] MultiCharacterMutationOperators =
        {
            "++",
            "--",
            "??=",
            ">>>=",
            "<<=",
            ">>="
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
            CollectBalancedIndexerUsages(sourceText, sanitizedText, targetPattern, isTargetReferenceAllowed, occurrences);
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
                $@"(?<target>\b{targetPattern})\s*{CollectionMemberAccessPattern}(?:{structuralUsageMembers})\s*\(",
                CollectionUsageKind.CollectionStructureUsage,
                isTargetReferenceAllowed,
                occurrences);

            AddTargetMatches(
                sourceText,
                sanitizedText,
                $@"(?<target>\b{targetPattern})\s*{CollectionIndexerPattern}\s*{MutationOperatorPattern}",
                CollectionUsageKind.CollectionStructureUsage,
                isTargetReferenceAllowed,
                occurrences);
        }

        private static void CollectBalancedIndexerUsages(
            string sourceText,
            string sanitizedText,
            string targetPattern,
            Func<int, bool> isTargetReferenceAllowed,
            ICollection<CollectionUsageOccurrence> occurrences)
        {
            foreach (Match match in Regex.Matches(sanitizedText, $@"\b{targetPattern}\b", RegexOptions.Multiline))
            {
                if (!isTargetReferenceAllowed(match.Index))
                    continue;

                var indexerOpen = TryGetIndexerOpen(sanitizedText, match.Index + match.Length);
                if (indexerOpen < 0)
                    continue;

                var indexerClose = TryFindMatchingBracket(sanitizedText, indexerOpen);
                if (indexerClose < 0)
                    continue;

                var afterIndexer = SkipWhitespace(sanitizedText, indexerClose + 1);
                if (TryGetMutationOperatorLength(sanitizedText, afterIndexer, out var indexerMutationLength))
                {
                    AddOccurrence(
                        sourceText,
                        match.Index,
                        afterIndexer + indexerMutationLength - match.Index,
                        CollectionUsageKind.CollectionStructureUsage,
                        occurrences);
                    continue;
                }

                if (TryGetMemberMutationEnd(sanitizedText, afterIndexer, out var memberMutationEnd))
                {
                    AddOccurrence(
                        sourceText,
                        match.Index,
                        memberMutationEnd - match.Index,
                        CollectionUsageKind.ElementWrite,
                        occurrences);
                }
            }
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
                $@"(?<target>\b{targetPattern})\s*{CollectionIndexerPattern}\s*\.\s*[A-Za-z_]\w*(?:\s*\.\s*[A-Za-z_]\w*)*\s*{MutationOperatorPattern}",
                CollectionUsageKind.ElementWrite,
                isTargetReferenceAllowed,
                occurrences);
        }

        private static IReadOnlyCollection<AliasScope> CollectAliases(
            string sourceText,
            string sanitizedText,
            string targetPattern,
            Func<int, bool> isTargetReferenceAllowed,
            ICollection<CollectionUsageOccurrence> occurrences)
        {
            var aliases = new List<AliasScope>();

            foreach (Match match in Regex.Matches(
                         sanitizedText,
                         $@"\b(?:var|[A-Za-z_]\w*(?:\s*<[^;\r\n=]+>)?(?:\s*\[\])?)\s+([A-Za-z_]\w*)\s*=\s*(?<target>{targetPattern})\s*{CollectionIndexerPattern}\s*;",
                         RegexOptions.Multiline))
            {
                if (!IsTargetMatchAllowed(match, isTargetReferenceAllowed))
                    continue;

                aliases.Add(new AliasScope(
                    match.Groups[1].Value,
                    match.Index + match.Length,
                    GetContainingBlockEnd(sanitizedText, match.Index)));
                AddOccurrence(sourceText, match, CollectionUsageKind.ElementAlias, occurrences);
            }

            foreach (Match match in Regex.Matches(
                         sanitizedText,
                         $@"\bforeach\s*\(\s*(?:var|[A-Za-z_]\w*(?:\s*<[^;\r\n=]+>)?)\s+([A-Za-z_]\w*)\s+in\s+(?<target>{targetPattern})\s*\)",
                         RegexOptions.Multiline))
            {
                if (!IsTargetMatchAllowed(match, isTargetReferenceAllowed))
                    continue;

                aliases.Add(CreateForeachAliasScope(sanitizedText, match.Groups[1].Value, match.Index, match.Index + match.Length));
                AddOccurrence(sourceText, match, CollectionUsageKind.ElementAlias, occurrences);
            }

            return aliases;
        }

        private static void CollectAliasElementWrites(
            string sourceText,
            string sanitizedText,
            IEnumerable<AliasScope> aliases,
            ICollection<CollectionUsageOccurrence> occurrences)
        {
            foreach (var alias in aliases)
            {
                foreach (Match match in Regex.Matches(
                             sanitizedText,
                             $@"\b{Regex.Escape(alias.Name)}\s*\.\s*[A-Za-z_]\w*(?:\s*\.\s*[A-Za-z_]\w*)*\s*{MutationOperatorPattern}",
                             RegexOptions.Multiline))
                {
                    if (match.Index < alias.StartOffset || match.Index >= alias.EndOffset)
                        continue;

                    AddOccurrence(sourceText, match, CollectionUsageKind.ElementWrite, occurrences);
                }
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
                $@"\breturn\s+(?<target>{targetPattern})\s*{CollectionIndexerPattern}\s*;",
                CollectionUsageKind.ElementEscape,
                isTargetReferenceAllowed,
                occurrences);

            AddTargetMatches(
                sourceText,
                sanitizedText,
                $@"(?:^|[;\{{]\s*)[A-Za-z_]\w*(?:\s*\.\s*[A-Za-z_]\w*)?\s*=\s*(?<target>{targetPattern})\s*{CollectionIndexerPattern}\s*;",
                CollectionUsageKind.ElementEscape,
                isTargetReferenceAllowed,
                occurrences);

            AddTargetMatches(
                sourceText,
                sanitizedText,
                $@"\b(?!if\b|for\b|foreach\b|while\b|switch\b|using\b|lock\b|return\b)[A-Za-z_]\w*(?:\s*\.\s*[A-Za-z_]\w*)?\s*\([^;\r\n]*(?<target>\b{targetPattern})\s*{CollectionIndexerPattern}(?!\s*\.)[^;\r\n]*\)",
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

                AddTargetOccurrence(sourceText, match, kind, occurrences);
            }
        }

        private static bool IsTargetMatchAllowed(Match match, Func<int, bool> isTargetReferenceAllowed)
        {
            var targetGroup = match.Groups["target"];
            return targetGroup.Success && isTargetReferenceAllowed(targetGroup.Index);
        }

        private static void AddTargetOccurrence(
            string sourceText,
            Match match,
            CollectionUsageKind kind,
            ICollection<CollectionUsageOccurrence> occurrences)
        {
            var targetGroup = match.Groups["target"];
            if (!targetGroup.Success)
                return;

            AddOccurrence(
                sourceText,
                targetGroup.Index,
                match.Index + match.Length - targetGroup.Index,
                kind,
                occurrences);
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
            AddOccurrence(sourceText, match.Index, match.Length, kind, occurrences);
        }

        private static void AddOccurrence(
            string sourceText,
            int startOffset,
            int length,
            CollectionUsageKind kind,
            ICollection<CollectionUsageOccurrence> occurrences)
        {
            var location = GetLineColumn(sourceText, startOffset);
            occurrences.Add(new CollectionUsageOccurrence(
                kind,
                startOffset,
                Math.Max(1, Math.Min(length, sourceText.Length - startOffset)),
                location.Line,
                location.Column,
                GetLineText(sourceText, startOffset)));
        }

        private static int TryGetIndexerOpen(string text, int offset)
        {
            var current = SkipWhitespace(text, offset);
            if (current < text.Length && text[current] == '?')
                current = SkipWhitespace(text, current + 1);

            return current < text.Length && text[current] == '[' ? current : -1;
        }

        private static int TryFindMatchingBracket(string text, int openBracketOffset)
        {
            var depth = 0;
            for (var i = openBracketOffset; i < text.Length; i++)
            {
                if (text[i] == '[')
                {
                    depth++;
                }
                else if (text[i] == ']')
                {
                    depth--;
                    if (depth == 0)
                        return i;
                }
            }

            return -1;
        }

        private static AliasScope CreateForeachAliasScope(string text, string name, int matchStart, int matchEnd)
        {
            var bodyStart = SkipWhitespace(text, matchEnd);
            if (bodyStart < text.Length && text[bodyStart] == '{')
            {
                var bodyEnd = TryFindMatchingBrace(text, bodyStart);
                return new AliasScope(name, bodyStart + 1, bodyEnd >= 0 ? bodyEnd : text.Length);
            }

            var statementEnd = FindStatementEnd(text, bodyStart);
            return new AliasScope(name, matchEnd, statementEnd >= 0 ? statementEnd : GetContainingBlockEnd(text, matchStart));
        }

        private static int GetContainingBlockEnd(string text, int offset)
        {
            var openBraces = new Stack<int>();
            for (var i = 0; i < offset && i < text.Length; i++)
            {
                if (text[i] == '{')
                {
                    openBraces.Push(i);
                }
                else if (text[i] == '}' && openBraces.Count > 0)
                {
                    openBraces.Pop();
                }
            }

            if (openBraces.Count == 0)
                return text.Length;

            var blockEnd = TryFindMatchingBrace(text, openBraces.Peek());
            return blockEnd >= 0 ? blockEnd : text.Length;
        }

        private static int TryFindMatchingBrace(string text, int openBraceOffset)
        {
            var depth = 0;
            for (var i = openBraceOffset; i < text.Length; i++)
            {
                if (text[i] == '{')
                {
                    depth++;
                }
                else if (text[i] == '}')
                {
                    depth--;
                    if (depth == 0)
                        return i;
                }
            }

            return -1;
        }

        private static int FindStatementEnd(string text, int offset)
        {
            for (var i = offset; i < text.Length; i++)
            {
                if (text[i] == ';')
                    return i + 1;

                if (text[i] == '{' || text[i] == '}')
                    return i;
            }

            return -1;
        }

        private static bool TryGetMemberMutationEnd(string text, int offset, out int endOffset)
        {
            endOffset = offset;
            var current = offset;
            if (current >= text.Length || text[current] != '.')
                return false;

            while (current < text.Length && text[current] == '.')
            {
                current = SkipWhitespace(text, current + 1);
                if (current >= text.Length || !IsIdentifierStart(text[current]))
                    return false;

                current++;
                while (current < text.Length && IsIdentifierPart(text[current]))
                    current++;

                current = SkipWhitespace(text, current);
            }

            if (!TryGetMutationOperatorLength(text, current, out var operatorLength))
                return false;

            endOffset = current + operatorLength;
            return true;
        }

        private static bool TryGetMutationOperatorLength(string text, int offset, out int operatorLength)
        {
            operatorLength = 0;
            if (offset >= text.Length)
                return false;

            foreach (var mutationOperator in MultiCharacterMutationOperators)
            {
                if (!StartsWith(text, offset, mutationOperator))
                    continue;

                operatorLength = mutationOperator.Length;
                return true;
            }

            if (text[offset] == '=' && (offset + 1 >= text.Length || text[offset + 1] != '=' && text[offset + 1] != '>'))
            {
                operatorLength = 1;
                return true;
            }

            if (offset + 1 < text.Length && SingleCharacterCompoundAssignmentOperators.IndexOf(text[offset]) >= 0 && text[offset + 1] == '=')
            {
                operatorLength = 2;
                return true;
            }

            return false;
        }

        private static bool StartsWith(string text, int offset, string value)
        {
            if (offset + value.Length > text.Length)
                return false;

            for (var i = 0; i < value.Length; i++)
            {
                if (text[offset + i] != value[i])
                    return false;
            }

            return true;
        }

        private static int SkipWhitespace(string text, int offset)
        {
            var current = offset;
            while (current < text.Length && char.IsWhiteSpace(text[current]))
                current++;
            return current;
        }

        private static bool IsIdentifierStart(char value)
        {
            return value == '_' || char.IsLetter(value);
        }

        private static bool IsIdentifierPart(char value)
        {
            return value == '_' || char.IsLetterOrDigit(value);
        }

        private sealed class AliasScope
        {
            public AliasScope(string name, int startOffset, int endOffset)
            {
                Name = name;
                StartOffset = startOffset;
                EndOffset = endOffset;
            }

            public string Name { get; }
            public int StartOffset { get; }
            public int EndOffset { get; }
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
