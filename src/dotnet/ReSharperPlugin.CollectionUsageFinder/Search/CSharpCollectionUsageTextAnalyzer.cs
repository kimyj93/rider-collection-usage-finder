using System;
using System.Collections.Concurrent;
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

        private static readonly string[] ReadUsageMemberNames =
        {
            "Contains",
            "ContainsKey",
            "ContainsValue",
            "TryGetValue",
            "IndexOf",
            "BinarySearch",
            "Exists",
            "Any",
            "All",
            "TrueForAll",
            "Peek",
            "TryPeek",
            "First",
            "FirstOrDefault",
            "Last",
            "LastOrDefault",
            "Single",
            "SingleOrDefault",
            "Find",
            "FindAll",
            "FindIndex",
            "FindLast",
            "FindLastIndex",
            "GetEnumerator",
            "Where",
            "Select",
            "ToArray",
            "ToList",
            "ToDictionary"
        };

        private static readonly string[] ReadUsagePropertyNames =
        {
            "Count",
            "Length",
            "Keys",
            "Values"
        };

        private const string CollectionMemberAccessPattern = @"(?:\?\.\s*|\.\s*)";
        private const string CollectionIndexerPattern = @"(?:\?\s*)?\[(?:[^\[\]\r\n]|\[[^\]\r\n]*\])+\]";
        private const string MutationOperatorPattern = @"(?:\+\+|--|\?\?=|(?:>>>|<<|>>|[+\-*/%&|^])=|=(?!=|>))";
        private const string SingleCharacterCompoundAssignmentOperators = "+-*/%&|^";
        private const RegexOptions AnalyzerRegexOptions = RegexOptions.Multiline | RegexOptions.CultureInvariant;
        private static readonly string StructuralUsageMembersPattern = string.Join("|", StructuralUsageMemberNames.Select(Regex.Escape));
        private static readonly string ReadUsageMembersPattern = string.Join("|", ReadUsageMemberNames.Select(Regex.Escape));
        private static readonly string ReadUsagePropertiesPattern = string.Join("|", ReadUsagePropertyNames.Select(Regex.Escape));
        private static readonly ConcurrentDictionary<string, AnalyzerRegexSet> RegexSetCache =
            new ConcurrentDictionary<string, AnalyzerRegexSet>();

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
            var regexSet = RegexSetCache.GetOrAdd(targetName, static name => new AnalyzerRegexSet(Regex.Escape(name)));
            var lineMap = new SourceLineMap(sourceText);
            var occurrences = new List<CollectionUsageOccurrence>();

            CollectWholeCollectionAssignments(sourceText, sanitizedText, regexSet, lineMap, isTargetReferenceAllowed, occurrences);
            CollectStructuralUsages(sourceText, sanitizedText, regexSet, lineMap, isTargetReferenceAllowed, occurrences);
            CollectDirectElementWrites(sourceText, sanitizedText, regexSet, lineMap, isTargetReferenceAllowed, occurrences);
            CollectBalancedIndexerUsages(sourceText, sanitizedText, regexSet, lineMap, isTargetReferenceAllowed, occurrences);
            var aliases = CollectAliases(sourceText, sanitizedText, regexSet, lineMap, isTargetReferenceAllowed, occurrences);
            CollectAliasElementWrites(sourceText, sanitizedText, regexSet, lineMap, aliases, occurrences);
            CollectEscapes(sourceText, sanitizedText, regexSet, lineMap, isTargetReferenceAllowed, occurrences);
            CollectReadUsages(sourceText, sanitizedText, regexSet, lineMap, isTargetReferenceAllowed, occurrences);

            return occurrences
                .GroupBy(static occurrence => occurrence.Kind + ":" + occurrence.OperationKind + ":" + occurrence.StartOffset)
                .Select(static group => group.First())
                .OrderBy(static occurrence => occurrence.StartOffset)
                .ToArray();
        }

        private static void CollectWholeCollectionAssignments(
            string sourceText,
            string sanitizedText,
            AnalyzerRegexSet regexSet,
            SourceLineMap lineMap,
            Func<int, bool> isTargetReferenceAllowed,
            ICollection<CollectionUsageOccurrence> occurrences)
        {
            foreach (Match match in regexSet.TargetReferences.Matches(sanitizedText))
            {
                if (!isTargetReferenceAllowed(match.Index))
                    continue;

                if (LooksLikeDeclarationTarget(sanitizedText, match.Index))
                    continue;

                var operatorOffset = SkipWhitespace(sanitizedText, match.Index + match.Length);
                if (!TryGetCollectionAssignmentOperatorLength(sanitizedText, operatorOffset, out var operatorLength))
                    continue;

                AddOccurrence(
                    sourceText,
                    match.Index,
                    operatorOffset + operatorLength - match.Index,
                    CollectionUsageKind.CollectionAssignment,
                    CollectionUsageOperationKind.CollectionAssignment,
                    GetAssignmentOperationName(sanitizedText, operatorOffset, operatorLength),
                    lineMap,
                    occurrences);
            }
        }

        private static void CollectStructuralUsages(
            string sourceText,
            string sanitizedText,
            AnalyzerRegexSet regexSet,
            SourceLineMap lineMap,
            Func<int, bool> isTargetReferenceAllowed,
            ICollection<CollectionUsageOccurrence> occurrences)
        {
            foreach (Match match in regexSet.StructuralMemberUsage.Matches(sanitizedText))
            {
                if (!IsTargetMatchAllowed(match, isTargetReferenceAllowed))
                    continue;

                var memberName = match.Groups["member"].Value;
                AddTargetOccurrence(
                    sourceText,
                    match,
                    CollectionUsageKind.CollectionStructureUsage,
                    CollectionUsageOperations.FromStructuralMemberName(memberName),
                    memberName,
                    lineMap,
                    occurrences);
            }

            AddTargetMatches(
                sourceText,
                sanitizedText,
                regexSet.StructuralIndexerUsage,
                CollectionUsageKind.CollectionStructureUsage,
                CollectionUsageOperationKind.ElementSet,
                "IndexerSet",
                lineMap,
                isTargetReferenceAllowed,
                occurrences);
        }

        private static void CollectBalancedIndexerUsages(
            string sourceText,
            string sanitizedText,
            AnalyzerRegexSet regexSet,
            SourceLineMap lineMap,
            Func<int, bool> isTargetReferenceAllowed,
            ICollection<CollectionUsageOccurrence> occurrences)
        {
            foreach (Match match in regexSet.TargetReferences.Matches(sanitizedText))
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
                        CollectionUsageOperationKind.ElementSet,
                        "IndexerSet",
                        lineMap,
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
                        CollectionUsageOperationKind.ElementContentWrite,
                        "IndexerMemberWrite",
                        lineMap,
                        occurrences);
                }
            }
        }

        private static void CollectDirectElementWrites(
            string sourceText,
            string sanitizedText,
            AnalyzerRegexSet regexSet,
            SourceLineMap lineMap,
            Func<int, bool> isTargetReferenceAllowed,
            ICollection<CollectionUsageOccurrence> occurrences)
        {
            AddTargetMatches(
                sourceText,
                sanitizedText,
                regexSet.DirectElementWrite,
                CollectionUsageKind.ElementWrite,
                CollectionUsageOperationKind.ElementContentWrite,
                "IndexerMemberWrite",
                lineMap,
                isTargetReferenceAllowed,
                occurrences);
        }

        private static IReadOnlyCollection<AliasScope> CollectAliases(
            string sourceText,
            string sanitizedText,
            AnalyzerRegexSet regexSet,
            SourceLineMap lineMap,
            Func<int, bool> isTargetReferenceAllowed,
            ICollection<CollectionUsageOccurrence> occurrences)
        {
            var aliases = new List<AliasScope>();

            foreach (Match match in regexSet.LocalAlias.Matches(sanitizedText))
            {
                if (!IsTargetMatchAllowed(match, isTargetReferenceAllowed))
                    continue;

                aliases.Add(new AliasScope(
                    match.Groups[1].Value,
                    match.Index + match.Length,
                    GetContainingBlockEnd(sanitizedText, match.Index)));
                AddOccurrence(
                    sourceText,
                    match,
                    CollectionUsageKind.ElementAlias,
                    CollectionUsageOperationKind.ElementReference,
                    "LocalAlias",
                    lineMap,
                    occurrences);
            }

            foreach (Match match in regexSet.ForeachAlias.Matches(sanitizedText))
            {
                if (!IsTargetMatchAllowed(match, isTargetReferenceAllowed))
                    continue;

                aliases.Add(CreateForeachAliasScope(sanitizedText, match.Groups[1].Value, match.Index, match.Index + match.Length));
                AddOccurrence(
                    sourceText,
                    match,
                    CollectionUsageKind.CollectionRead,
                    CollectionUsageOperationKind.EnumerationRead,
                    "Foreach",
                    lineMap,
                    occurrences);
            }

            return aliases;
        }

        private static void CollectAliasElementWrites(
            string sourceText,
            string sanitizedText,
            AnalyzerRegexSet regexSet,
            SourceLineMap lineMap,
            IEnumerable<AliasScope> aliases,
            ICollection<CollectionUsageOccurrence> occurrences)
        {
            foreach (var alias in aliases)
            {
                foreach (Match match in regexSet.GetAliasElementWrite(alias.Name).Matches(sanitizedText))
                {
                    if (match.Index < alias.StartOffset || match.Index >= alias.EndOffset)
                        continue;

                    AddOccurrence(
                        sourceText,
                        match,
                        CollectionUsageKind.ElementWrite,
                        CollectionUsageOperationKind.ElementContentWrite,
                        "AliasMemberWrite",
                        lineMap,
                        occurrences);
                }
            }
        }

        private static void CollectEscapes(
            string sourceText,
            string sanitizedText,
            AnalyzerRegexSet regexSet,
            SourceLineMap lineMap,
            Func<int, bool> isTargetReferenceAllowed,
            ICollection<CollectionUsageOccurrence> occurrences)
        {
            AddTargetMatches(
                sourceText,
                sanitizedText,
                regexSet.ReturnEscape,
                CollectionUsageKind.ElementEscape,
                CollectionUsageOperationKind.ElementReference,
                "Return",
                lineMap,
                isTargetReferenceAllowed,
                occurrences);

            AddTargetMatches(
                sourceText,
                sanitizedText,
                regexSet.AssignmentEscape,
                CollectionUsageKind.ElementEscape,
                CollectionUsageOperationKind.ElementReference,
                "Assignment",
                lineMap,
                isTargetReferenceAllowed,
                occurrences);

            AddTargetMatches(
                sourceText,
                sanitizedText,
                regexSet.ArgumentEscape,
                CollectionUsageKind.ElementEscape,
                CollectionUsageOperationKind.ElementReference,
                "Argument",
                lineMap,
                isTargetReferenceAllowed,
                occurrences);
        }

        private static void CollectReadUsages(
            string sourceText,
            string sanitizedText,
            AnalyzerRegexSet regexSet,
            SourceLineMap lineMap,
            Func<int, bool> isTargetReferenceAllowed,
            ICollection<CollectionUsageOccurrence> occurrences)
        {
            foreach (Match match in regexSet.ReadMemberUsage.Matches(sanitizedText))
            {
                if (!IsTargetMatchAllowed(match, isTargetReferenceAllowed))
                    continue;

                var targetGroup = match.Groups["target"];
                if (HasNonReadOccurrenceCovering(occurrences, targetGroup.Index))
                    continue;

                var memberName = match.Groups["member"].Value;
                AddTargetOccurrence(
                    sourceText,
                    match,
                    CollectionUsageKind.CollectionRead,
                    GetReadOperationKind(memberName),
                    memberName,
                    lineMap,
                    occurrences);
            }

            foreach (Match match in regexSet.ReadPropertyUsage.Matches(sanitizedText))
            {
                if (!IsTargetMatchAllowed(match, isTargetReferenceAllowed))
                    continue;

                var targetGroup = match.Groups["target"];
                if (HasNonReadOccurrenceCovering(occurrences, targetGroup.Index))
                    continue;

                var propertyName = match.Groups["property"].Value;
                AddTargetOccurrence(
                    sourceText,
                    match,
                    CollectionUsageKind.CollectionRead,
                    GetReadOperationKind(propertyName),
                    propertyName,
                    lineMap,
                    occurrences);
            }

            foreach (Match match in regexSet.TargetReferences.Matches(sanitizedText))
            {
                if (!isTargetReferenceAllowed(match.Index))
                    continue;

                if (HasNonReadOccurrenceCovering(occurrences, match.Index))
                    continue;

                var indexerOpen = TryGetIndexerOpen(sanitizedText, match.Index + match.Length);
                if (indexerOpen < 0)
                    continue;

                var indexerClose = TryFindMatchingBracket(sanitizedText, indexerOpen);
                if (indexerClose < 0)
                    continue;

                var afterIndexer = SkipWhitespace(sanitizedText, indexerClose + 1);
                if (TryGetMutationOperatorLength(sanitizedText, afterIndexer, out _))
                    continue;

                if (TryGetMemberMutationEnd(sanitizedText, afterIndexer, out _))
                    continue;

                AddOccurrence(
                    sourceText,
                    match.Index,
                    indexerClose + 1 - match.Index,
                    CollectionUsageKind.CollectionRead,
                    CollectionUsageOperationKind.ElementRead,
                    "IndexerRead",
                    lineMap,
                    occurrences);
            }
        }

        private static bool HasNonReadOccurrenceCovering(
            IEnumerable<CollectionUsageOccurrence> occurrences,
            int targetOffset)
        {
            return occurrences.Any(occurrence =>
                occurrence.Kind != CollectionUsageKind.CollectionRead &&
                occurrence.StartOffset <= targetOffset &&
                targetOffset < occurrence.StartOffset + occurrence.Length);
        }

        private static CollectionUsageOperationKind GetReadOperationKind(string memberName)
        {
            switch (memberName)
            {
                case "TryGetValue":
                case "Peek":
                case "TryPeek":
                case "First":
                case "FirstOrDefault":
                case "Last":
                case "LastOrDefault":
                case "Single":
                case "SingleOrDefault":
                case "Find":
                    return CollectionUsageOperationKind.ElementRead;

                case "GetEnumerator":
                case "Where":
                case "Select":
                case "Keys":
                case "Values":
                    return CollectionUsageOperationKind.EnumerationRead;

                case "FindAll":
                case "ToArray":
                case "ToList":
                case "ToDictionary":
                    return CollectionUsageOperationKind.CopyRead;

                default:
                    return CollectionUsageOperationKind.ConditionRead;
            }
        }

        private static void AddTargetMatches(
            string sourceText,
            string sanitizedText,
            Regex regex,
            CollectionUsageKind kind,
            CollectionUsageOperationKind operationKind,
            string operationName,
            SourceLineMap lineMap,
            Func<int, bool> isTargetReferenceAllowed,
            ICollection<CollectionUsageOccurrence> occurrences)
        {
            foreach (Match match in regex.Matches(sanitizedText))
            {
                if (!IsTargetMatchAllowed(match, isTargetReferenceAllowed))
                    continue;

                AddTargetOccurrence(sourceText, match, kind, operationKind, operationName, lineMap, occurrences);
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
            CollectionUsageOperationKind operationKind,
            string operationName,
            SourceLineMap lineMap,
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
                operationKind,
                operationName,
                lineMap,
                occurrences);
        }

        private static void AddMatches(
            string sourceText,
            string sanitizedText,
            Regex regex,
            CollectionUsageKind kind,
            CollectionUsageOperationKind operationKind,
            string operationName,
            SourceLineMap lineMap,
            ICollection<CollectionUsageOccurrence> occurrences)
        {
            foreach (Match match in regex.Matches(sanitizedText))
                AddOccurrence(sourceText, match, kind, operationKind, operationName, lineMap, occurrences);
        }

        private static void AddOccurrence(
            string sourceText,
            Match match,
            CollectionUsageKind kind,
            CollectionUsageOperationKind operationKind,
            string operationName,
            SourceLineMap lineMap,
            ICollection<CollectionUsageOccurrence> occurrences)
        {
            AddOccurrence(sourceText, match.Index, match.Length, kind, operationKind, operationName, lineMap, occurrences);
        }

        private static void AddOccurrence(
            string sourceText,
            int startOffset,
            int length,
            CollectionUsageKind kind,
            CollectionUsageOperationKind operationKind,
            string operationName,
            SourceLineMap lineMap,
            ICollection<CollectionUsageOccurrence> occurrences)
        {
            var location = lineMap.GetLineColumn(startOffset);
            occurrences.Add(new CollectionUsageOccurrence(
                kind,
                operationKind,
                operationName,
                startOffset,
                Math.Max(1, Math.Min(length, sourceText.Length - startOffset)),
                location.Line,
                location.Column,
                lineMap.GetLineText(startOffset)));
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

        private static bool TryGetCollectionAssignmentOperatorLength(string text, int offset, out int operatorLength)
        {
            operatorLength = 0;
            if (offset >= text.Length)
                return false;

            if (StartsWith(text, offset, "??="))
            {
                operatorLength = 3;
                return true;
            }

            if (text[offset] == '=' && (offset + 1 >= text.Length || text[offset + 1] != '=' && text[offset + 1] != '>'))
            {
                operatorLength = 1;
                return true;
            }

            return false;
        }

        [NotNull]
        private static string GetAssignmentOperationName(string text, int operatorOffset, int operatorLength)
        {
            if (operatorLength <= 0 || operatorOffset < 0 || operatorOffset + operatorLength > text.Length)
                return "Assignment";

            return text.Substring(operatorOffset, operatorLength) == "??="
                ? "NullCoalescingAssignment"
                : "Assignment";
        }

        private static bool LooksLikeDeclarationTarget(string text, int targetOffset)
        {
            var previous = FindPreviousNonWhitespace(text, targetOffset - 1);
            if (previous < 0)
                return false;

            var previousCharacter = text[previous];
            if (previousCharacter == '.')
                return false;

            if (IsIdentifierPart(previousCharacter))
            {
                var previousIdentifier = GetIdentifierEndingAt(text, previous);
                return previousIdentifier != "else" && previousIdentifier != "do";
            }

            return previousCharacter == '>' || previousCharacter == ']' || previousCharacter == '?';
        }

        private static int FindPreviousNonWhitespace(string text, int offset)
        {
            var current = offset;
            while (current >= 0 && char.IsWhiteSpace(text[current]))
                current--;
            return current;
        }

        private static string GetIdentifierEndingAt(string text, int endOffset)
        {
            var startOffset = endOffset;
            while (startOffset > 0 && IsIdentifierPart(text[startOffset - 1]))
                startOffset--;

            return text.Substring(startOffset, endOffset - startOffset + 1);
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

        private sealed class AnalyzerRegexSet
        {
            private readonly ConcurrentDictionary<string, Regex> aliasElementWriteRegexes =
                new ConcurrentDictionary<string, Regex>();

            public AnalyzerRegexSet(string targetPattern)
            {
                TargetReferences = CreateRegex($@"\b{targetPattern}\b");
                StructuralMemberUsage = CreateRegex(
                    $@"(?<target>\b{targetPattern})\s*{CollectionMemberAccessPattern}(?<member>{StructuralUsageMembersPattern})\s*\(");
                StructuralIndexerUsage = CreateRegex(
                    $@"(?<target>\b{targetPattern})\s*{CollectionIndexerPattern}\s*{MutationOperatorPattern}");
                DirectElementWrite = CreateRegex(
                    $@"(?<target>\b{targetPattern})\s*{CollectionIndexerPattern}\s*\.\s*[A-Za-z_]\w*(?:\s*\.\s*[A-Za-z_]\w*)*\s*{MutationOperatorPattern}");
                ReadMemberUsage = CreateRegex(
                    $@"(?<target>\b{targetPattern})\s*{CollectionMemberAccessPattern}(?<member>{ReadUsageMembersPattern})\s*\(");
                ReadPropertyUsage = CreateRegex(
                    $@"(?<target>\b{targetPattern})\s*{CollectionMemberAccessPattern}(?<property>{ReadUsagePropertiesPattern})\b(?!\s*\()");
                LocalAlias = CreateRegex(
                    $@"\b(?:var|[A-Za-z_]\w*(?:\s*<[^;\r\n=]+>)?(?:\s*\[\])?)\s+([A-Za-z_]\w*)\s*=\s*(?<target>{targetPattern})\s*{CollectionIndexerPattern}\s*;");
                ForeachAlias = CreateRegex(
                    $@"\bforeach\s*\(\s*(?:var|[A-Za-z_]\w*(?:\s*<[^;\r\n=]+>)?)\s+([A-Za-z_]\w*)\s+in\s+(?<target>{targetPattern})\s*\)");
                ReturnEscape = CreateRegex(
                    $@"\breturn\s+(?<target>{targetPattern})\s*{CollectionIndexerPattern}\s*;");
                AssignmentEscape = CreateRegex(
                    $@"(?:^|[;\{{]\s*)[A-Za-z_]\w*(?:\s*\.\s*[A-Za-z_]\w*)?\s*=\s*(?<target>{targetPattern})\s*{CollectionIndexerPattern}\s*;");
                ArgumentEscape = CreateRegex(
                    $@"\b(?!if\b|for\b|foreach\b|while\b|switch\b|using\b|lock\b|return\b)[A-Za-z_]\w*(?:\s*\.\s*[A-Za-z_]\w*)?\s*\([^;\r\n]*(?<target>\b{targetPattern})\s*{CollectionIndexerPattern}(?!\s*\.)[^;\r\n]*\)");
            }

            public Regex TargetReferences { get; }

            public Regex StructuralMemberUsage { get; }

            public Regex StructuralIndexerUsage { get; }

            public Regex DirectElementWrite { get; }

            public Regex ReadMemberUsage { get; }

            public Regex ReadPropertyUsage { get; }

            public Regex LocalAlias { get; }

            public Regex ForeachAlias { get; }

            public Regex ReturnEscape { get; }

            public Regex AssignmentEscape { get; }

            public Regex ArgumentEscape { get; }

            public Regex GetAliasElementWrite(string aliasName)
            {
                return aliasElementWriteRegexes.GetOrAdd(
                    aliasName,
                    static name => CreateRegex($@"\b{Regex.Escape(name)}\s*\.\s*[A-Za-z_]\w*(?:\s*\.\s*[A-Za-z_]\w*)*\s*{MutationOperatorPattern}"));
            }

            private static Regex CreateRegex(string pattern)
            {
                return new Regex(pattern, AnalyzerRegexOptions);
            }
        }

        private sealed class SourceLineMap
        {
            private readonly string text;
            private readonly List<int> lineStarts;

            public SourceLineMap(string text)
            {
                this.text = text;
                lineStarts = new List<int> { 0 };
                for (var i = 0; i < text.Length; i++)
                {
                    if (text[i] == '\n' && i + 1 < text.Length)
                        lineStarts.Add(i + 1);
                }
            }

            public (int Line, int Column) GetLineColumn(int offset)
            {
                var normalizedOffset = Math.Max(0, Math.Min(offset, Math.Max(0, text.Length - 1)));
                var lineIndex = GetLineIndex(normalizedOffset);
                return (lineIndex + 1, normalizedOffset - lineStarts[lineIndex] + 1);
            }

            public string GetLineText(int offset)
            {
                if (text.Length == 0)
                    return string.Empty;

                var normalizedOffset = Math.Max(0, Math.Min(offset, text.Length - 1));
                var lineIndex = GetLineIndex(normalizedOffset);
                var lineStart = lineStarts[lineIndex];
                var lineEnd = lineIndex + 1 < lineStarts.Count
                    ? TrimPreviousLineBreak(lineStarts[lineIndex + 1])
                    : text.Length;

                return text.Substring(lineStart, Math.Max(0, lineEnd - lineStart)).Trim();
            }

            private int GetLineIndex(int offset)
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

            private int TrimPreviousLineBreak(int offset)
            {
                var result = offset;
                if (result > 0 && text[result - 1] == '\n')
                    result--;
                if (result > 0 && text[result - 1] == '\r')
                    result--;
                return result;
            }
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
