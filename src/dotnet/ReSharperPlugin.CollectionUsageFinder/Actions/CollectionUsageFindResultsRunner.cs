using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using JetBrains.Application.DataContext;
using JetBrains.Application.Progress;
using JetBrains.Application.UI.Actions;
using JetBrains.DocumentModel;
using JetBrains.ProjectModel;
using JetBrains.ReSharper.Feature.Services.Navigation.ContextNavigation;
using JetBrains.ReSharper.Feature.Services.Navigation.Descriptors;
using JetBrains.ReSharper.Feature.Services.Navigation.Requests;
using JetBrains.ReSharper.Feature.Services.Occurrences;
using JetBrains.ReSharper.Feature.Services.Presentation;
using JetBrains.ReSharper.Feature.Services.Tree;
using JetBrains.ReSharper.Feature.Services.Tree.SectionsManagement;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.DataContext;
using JetBrains.ReSharper.Psi.Files;
using JetBrains.ReSharper.Psi.Impl;
using JetBrains.ReSharper.Psi.Resolve;
using JetBrains.ReSharper.Psi.Search;
using JetBrains.ReSharper.Psi.Tree;
using JetBrains.Util;
using ReSharperPlugin.CollectionUsageFinder.Search;

namespace ReSharperPlugin.CollectionUsageFinder.Actions
{
    internal static class CollectionUsageFindResultsRunner
    {
        public static void Execute(
            [NotNull] IDataContext dataContext,
            [NotNull] CollectionSearchTarget target,
            [CanBeNull] INavigationExecutionHost host)
        {
            if (!TryCreateSearchContext(dataContext, out var solution, out var sourceFile, out var document))
            {
                MessageBox.ShowInfo("Open a C# source file before running Find Collection Usages.");
                return;
            }

            Execute(solution, sourceFile, document, target, host);
        }

        public static void Execute(
            [NotNull] ISolution solution,
            [NotNull] IPsiSourceFile sourceFile,
            [NotNull] IDocument document,
            [NotNull] CollectionSearchTarget target,
            [CanBeNull] INavigationExecutionHost host = null)
        {
            var result = Analyze(solution, sourceFile, document, target);

            if (result.Items.Count == 0)
            {
                MessageBox.ShowInfo(
                    "No collection-specific usages found for '" + target.DisplayName + "' in " +
                    GetScopeDisplayName(result.Scope) + ".");
                return;
            }

            var occurrences = CreateOccurrences(result.Items).ToArray();

            IOccurrenceBrowserDescriptor CreateDescriptor()
            {
                return new CollectionUsageSearchDescriptor(
                    new CollectionUsageSearchRequest(solution, target, result.Scope, occurrences),
                    occurrences);
            }

            if (host != null)
                host.ShowFindResults(CreateDescriptor);
            else
                FindResultsBrowserUtil.ShowResults(CreateDescriptor());
        }

        [NotNull]
        public static CollectionUsageAnalysisResult Analyze(
            [NotNull] ISolution solution,
            [NotNull] IPsiSourceFile sourceFile,
            [NotNull] IDocument document,
            [NotNull] CollectionSearchTarget target)
        {
            var searchDocuments = TryCollectReferenceSearchDocuments(sourceFile, target, out var seededDocuments)
                ? seededDocuments
                : CollectSearchDocuments(sourceFile, document, target).ToArray();
            var scope = CollectionSearchScopePolicy.GetSearchScope(target.TargetKind);
            var analyzer = new CSharpCollectionUsageTextAnalyzer();
            var items = searchDocuments
                .SelectMany(searchDocument => AnalyzeDocument(searchDocument, analyzer, target))
                .ToArray();

            return new CollectionUsageAnalysisResult(
                target.DisplayName,
                target.CollectionKind.ToString(),
                scope,
                items);
        }

        private static bool TryCreateSearchContext(
            [NotNull] IDataContext dataContext,
            [CanBeNull] out ISolution solution,
            [CanBeNull] out IPsiSourceFile sourceFile,
            [CanBeNull] out IDocument document)
        {
            var editorView = dataContext.GetData(PsiDataConstants.PSI_EDITOR_VIEW);
            if (editorView != null)
            {
                solution = editorView.Solution;
                sourceFile = editorView.DefaultSourceFile.SortedSourceFiles.FirstOrDefault();
                document = editorView.DefaultSourceFile.DocumentRangeFromMainDocument.Document;
                return solution != null && sourceFile != null && document != null;
            }

            var documentView = dataContext.GetData(PsiDataConstants.PSI_DOCUMENT_VIEW);
            if (documentView != null)
            {
                solution = documentView.Solution;
                sourceFile = documentView.DefaultSourceFile.SortedSourceFiles.FirstOrDefault();
                document = sourceFile?.Document;
                return solution != null && sourceFile != null && document != null;
            }

            solution = null;
            sourceFile = null;
            document = null;
            return false;
        }

        private static bool TryCollectReferenceSearchDocuments(
            [NotNull] IPsiSourceFile currentSourceFile,
            [NotNull] CollectionSearchTarget target,
            [NotNull] out IReadOnlyList<CollectionUsageSearchDocument> searchDocuments)
        {
            searchDocuments = Array.Empty<CollectionUsageSearchDocument>();

            try
            {
                var psiServices = target.DeclaredElement.GetPsiServices();
                var domain = CreateSearchDomain(currentSourceFile, target, psiServices.SearchDomainFactory);
                var consumer = new ReferenceSearchConsumer();

                psiServices.SingleThreadedFinder.FindReferences<IReference>(
                    target.DeclaredElement,
                    domain,
                    consumer,
                    new CollectionUsageProgressIndicator(),
                    true);

                searchDocuments = CreateReferenceSearchDocuments(consumer.References, target).ToArray();
                if (consumer.References.Count > 0 && searchDocuments.Count == 0)
                    return false;

                return true;
            }
            catch
            {
                // Keep the plugin usable if the ReSharper finder rejects a target or SDK behavior changes.
                return false;
            }
        }

        [NotNull]
        private static ISearchDomain CreateSearchDomain(
            [NotNull] IPsiSourceFile currentSourceFile,
            [NotNull] CollectionSearchTarget target,
            [NotNull] SearchDomainFactory searchDomainFactory)
        {
            var scope = CollectionSearchScopePolicy.GetSearchScope(target.TargetKind);
            if (scope == CollectionSearchScopeKind.CurrentFile)
                return searchDomainFactory.CreateSearchDomain(currentSourceFile);

            var declaringSourceFile = target.DeclaredElement.GetSourceFiles().FirstOrDefault() ?? currentSourceFile;
            var declaringProject = declaringSourceFile.GetProject();
            if (declaringProject != null)
                return searchDomainFactory.CreateSearchDomain(declaringProject);

            return searchDomainFactory.CreateSearchDomain(currentSourceFile);
        }

        [NotNull]
        private static IEnumerable<CollectionUsageSearchDocument> CreateReferenceSearchDocuments(
            [NotNull] IEnumerable<IReference> references,
            [NotNull] CollectionSearchTarget target)
        {
            var documents = new Dictionary<string, ReferenceSearchDocumentBuilder>();
            foreach (var reference in references)
            {
                if (!TryGetReferenceLocation(reference, target.DisplayName, out var sourceFile, out var document, out var targetOffset))
                    continue;

                var persistentId = sourceFile.GetPersistentID();
                if (!documents.TryGetValue(persistentId, out var builder))
                {
                    builder = new ReferenceSearchDocumentBuilder(sourceFile, document);
                    documents.Add(persistentId, builder);
                }

                builder.TargetOffsets.Add(targetOffset);
            }

            return documents.Values.Select(static builder => builder.Build());
        }

        private static bool TryGetReferenceLocation(
            [NotNull] IReference reference,
            [NotNull] string targetName,
            [CanBeNull] out IPsiSourceFile sourceFile,
            [CanBeNull] out IDocument document,
            out int targetOffset)
        {
            sourceFile = null;
            document = null;
            targetOffset = -1;

            if (!reference.IsValid())
                return false;

            var node = reference.GetTreeNode();
            if (node == null || !node.IsValid())
                return false;

            sourceFile = node.GetSourceFile();
            if (sourceFile == null || !sourceFile.IsValid())
                return false;

            document = sourceFile.Document;
            if (document == null)
                return false;

            var range = reference.GetDocumentRange();
            if (!range.IsValid())
                range = node.GetDocumentRange();

            if (!range.IsValid())
                return false;

            targetOffset = GetTargetNameOffset(document, range, targetName);
            return targetOffset >= 0;
        }

        private static int GetTargetNameOffset(
            [NotNull] IDocument document,
            DocumentRange referenceRange,
            [NotNull] string targetName)
        {
            var sourceText = document.GetText();
            var documentLength = sourceText.Length;
            var startOffset = Math.Max(0, Math.Min(referenceRange.TextRange.StartOffset, documentLength));
            if (IsTargetAt(sourceText, startOffset, targetName))
                return startOffset;

            var endOffset = Math.Max(startOffset, Math.Min(referenceRange.TextRange.EndOffset, documentLength));
            var searchLength = Math.Max(targetName.Length, endOffset - startOffset);
            searchLength = Math.Min(searchLength + targetName.Length + 8, documentLength - startOffset);
            var nestedOffset = sourceText.IndexOf(targetName, startOffset, searchLength, StringComparison.Ordinal);
            return nestedOffset >= 0 ? nestedOffset : -1;
        }

        private static bool IsTargetAt([NotNull] string sourceText, int offset, [NotNull] string targetName)
        {
            if (offset < 0 || offset + targetName.Length > sourceText.Length)
                return false;

            for (var i = 0; i < targetName.Length; i++)
            {
                if (sourceText[offset + i] != targetName[i])
                    return false;
            }

            return true;
        }

        [NotNull]
        private static IEnumerable<CollectionUsageSearchDocument> CollectSearchDocuments(
            [NotNull] IPsiSourceFile currentSourceFile,
            [NotNull] IDocument currentDocument,
            [NotNull] CollectionSearchTarget target)
        {
            var scope = CollectionSearchScopePolicy.GetSearchScope(target.TargetKind);
            if (scope == CollectionSearchScopeKind.CurrentFile)
            {
                yield return new CollectionUsageSearchDocument(currentSourceFile, currentDocument);
                yield break;
            }

            var seenSourceFiles = new HashSet<string>();
            foreach (var sourceFile in GetDeclaringProjectSourceFiles(currentSourceFile, target))
            {
                if (sourceFile == null || !sourceFile.IsValid())
                    continue;

                var persistentId = sourceFile.GetPersistentID();
                if (!seenSourceFiles.Add(persistentId))
                    continue;

                var sourceDocument = sourceFile.Document;
                if (sourceDocument == null)
                    continue;

                yield return new CollectionUsageSearchDocument(sourceFile, sourceDocument);
            }
        }

        [NotNull]
        private static IEnumerable<IPsiSourceFile> GetDeclaringProjectSourceFiles(
            [NotNull] IPsiSourceFile currentSourceFile,
            [NotNull] CollectionSearchTarget target)
        {
            var declaringSourceFile = target.DeclaredElement.GetSourceFiles().FirstOrDefault() ?? currentSourceFile;
            var declaringProject = declaringSourceFile.GetProject();
            var declaringProjectFolder = declaringProject as IProjectFolder;
            if (declaringProjectFolder == null)
            {
                yield return currentSourceFile;
                yield break;
            }

            foreach (var projectFile in ProjectExtensions.GetAllProjectFiles(declaringProjectFolder, IsCSharpProjectFile))
            {
                var sourceFile = projectFile.ToSourceFile();
                if (sourceFile != null)
                    yield return sourceFile;
            }
        }

        private static bool IsCSharpProjectFile([NotNull] IProjectFile projectFile)
        {
            return Equals(projectFile.LanguageType, CSharpProjectFileType.Instance);
        }

        [NotNull]
        private static IEnumerable<CollectionUsageAnalysisItem> AnalyzeDocument(
            [NotNull] CollectionUsageSearchDocument searchDocument,
            [NotNull] CSharpCollectionUsageTextAnalyzer analyzer,
            [NotNull] CollectionSearchTarget target)
        {
            var targetName = target.DisplayName;
            var sourceText = searchDocument.Document.GetText();
            if (sourceText.IndexOf(targetName, StringComparison.Ordinal) < 0)
                return Array.Empty<CollectionUsageAnalysisItem>();

            var analysisOccurrences = analyzer.Analyze(
                sourceText,
                targetName,
                targetOffset => IsTargetReferenceAllowedForDocument(searchDocument, target, targetOffset));
            return analysisOccurrences.Select(
                occurrence => new CollectionUsageAnalysisItem(
                    searchDocument.SourceFile,
                    searchDocument.Document,
                    GetSourceFilePath(searchDocument.SourceFile),
                    sourceText,
                    occurrence));
        }

        private static bool IsTargetReferenceAllowedForDocument(
            [NotNull] CollectionUsageSearchDocument searchDocument,
            [NotNull] CollectionSearchTarget target,
            int targetOffset)
        {
            if (!searchDocument.IsTargetOffsetAllowed(targetOffset))
                return false;

            return searchDocument.HasExplicitTargetOffsets ||
                IsTargetReferenceAllowed(searchDocument, target, targetOffset);
        }

        private static bool IsTargetReferenceAllowed(
            [NotNull] CollectionUsageSearchDocument searchDocument,
            [NotNull] CollectionSearchTarget target,
            int targetOffset)
        {
            var targetNameLength = target.DisplayName.Length;
            var documentLength = searchDocument.Document.GetTextLength();
            if (targetOffset < 0 || targetOffset + targetNameLength > documentLength)
                return false;

            var targetRange = new DocumentRange(
                searchDocument.Document,
                TextRange.FromLength(targetOffset, targetNameLength));

            try
            {
                var psiFile = searchDocument.SourceFile.GetPrimaryPsiFile();
                if (psiFile == null)
                    return true;

                var treeRange = psiFile.Translate(targetRange);
                if (!treeRange.IsValid())
                    return true;

                var references = psiFile.FindReferencesAt(treeRange);
                if (references.Count == 0)
                    return true;

                foreach (var reference in references)
                {
                    if (!reference.IsValid())
                        continue;

                    var resolvedElement = reference.Resolve().DeclaredElement;
                    if (resolvedElement == null)
                        continue;

                    if (DeclaredElementEqualityComparer.ElementComparer.Equals(resolvedElement, target.DeclaredElement))
                        return true;
                }

                return false;
            }
            catch (Exception)
            {
                return true;
            }
        }

        [NotNull]
        private static IEnumerable<IOccurrence> CreateOccurrences(
            [NotNull] IEnumerable<CollectionUsageAnalysisItem> analysisItems)
        {
            foreach (var item in analysisItems)
            {
                var document = item.Document;
                var occurrence = item.Occurrence;
                var documentLength = document.GetTextLength();
                if (documentLength <= 0)
                    continue;

                if (occurrence.StartOffset < 0 || occurrence.StartOffset >= documentLength)
                    continue;

                var length = Math.Max(1, Math.Min(occurrence.Length, documentLength - occurrence.StartOffset));
                var range = new DocumentRange(
                    document,
                    TextRange.FromLength(occurrence.StartOffset, length));

                yield return new CollectionUsageRangeOccurrence(item.SourceFile, range, occurrence);
            }
        }

        [NotNull]
        private static string GetSourceFilePath([NotNull] IPsiSourceFile sourceFile)
        {
            try
            {
                return sourceFile.GetLocation().FullPath;
            }
            catch
            {
                return sourceFile.DisplayName;
            }
        }

        private static string GetScopeDisplayName(CollectionSearchScopeKind scope)
        {
            switch (scope)
            {
                case CollectionSearchScopeKind.DeclaringProject:
                    return "the declaring project";

                case CollectionSearchScopeKind.CurrentFile:
                default:
                    return "the current file";
            }
        }

        private sealed class CollectionUsageSearchDocument
        {
            private readonly HashSet<int> allowedTargetOffsets;

            public CollectionUsageSearchDocument(
                [NotNull] IPsiSourceFile sourceFile,
                [NotNull] IDocument document,
                [CanBeNull] IEnumerable<int> allowedTargetOffsets = null)
            {
                SourceFile = sourceFile;
                Document = document;
                this.allowedTargetOffsets = allowedTargetOffsets != null
                    ? new HashSet<int>(allowedTargetOffsets)
                    : null;
            }

            [NotNull]
            public IPsiSourceFile SourceFile { get; }

            [NotNull]
            public IDocument Document { get; }

            public bool HasExplicitTargetOffsets => allowedTargetOffsets != null;

            public bool IsTargetOffsetAllowed(int targetOffset)
            {
                return allowedTargetOffsets == null || allowedTargetOffsets.Contains(targetOffset);
            }
        }

        private sealed class ReferenceSearchDocumentBuilder
        {
            public ReferenceSearchDocumentBuilder([NotNull] IPsiSourceFile sourceFile, [NotNull] IDocument document)
            {
                SourceFile = sourceFile;
                Document = document;
                TargetOffsets = new HashSet<int>();
            }

            [NotNull]
            private IPsiSourceFile SourceFile { get; }

            [NotNull]
            private IDocument Document { get; }

            [NotNull]
            public HashSet<int> TargetOffsets { get; }

            [NotNull]
            public CollectionUsageSearchDocument Build()
            {
                return new CollectionUsageSearchDocument(SourceFile, Document, TargetOffsets);
            }
        }

        private sealed class ReferenceSearchConsumer : IFindResultConsumer<IReference>
        {
            private readonly List<IReference> references = new List<IReference>();

            [NotNull]
            public IReadOnlyList<IReference> References => references;

            public IReference Build(FindResult result)
            {
                var referenceResult = result as IFindResultReference;
                var reference = referenceResult?.Reference;
                return reference != null && reference.IsValid() ? reference : null;
            }

            public FindExecution Merge(IReference data)
            {
                if (data != null)
                    references.Add(data);

                return FindExecution.Continue;
            }
        }

        private sealed class CollectionUsageProgressIndicator : IProgressIndicator
        {
            public void Dispose()
            {
            }

            public string TaskName { get; set; }

            public string CurrentItemText { get; set; }

            public bool IsCanceled => false;

            public void Advance(double amount)
            {
            }

            public void Start(int count)
            {
            }

            public void Stop()
            {
            }
        }

        public sealed class CollectionUsageAnalysisResult
        {
            public CollectionUsageAnalysisResult(
                [NotNull] string targetName,
                [NotNull] string collectionKind,
                CollectionSearchScopeKind scope,
                [NotNull] IReadOnlyList<CollectionUsageAnalysisItem> items)
            {
                TargetName = targetName;
                CollectionKind = collectionKind;
                Scope = scope;
                Items = items;
            }

            [NotNull]
            public string TargetName { get; }

            [NotNull]
            public string CollectionKind { get; }

            public CollectionSearchScopeKind Scope { get; }

            [NotNull]
            public IReadOnlyList<CollectionUsageAnalysisItem> Items { get; }
        }

        public sealed class CollectionUsageAnalysisItem
        {
            public CollectionUsageAnalysisItem(
                [NotNull] IPsiSourceFile sourceFile,
                [NotNull] IDocument document,
                [NotNull] string filePath,
                [NotNull] string sourceText,
                [NotNull] CollectionUsageOccurrence occurrence)
            {
                SourceFile = sourceFile;
                Document = document;
                FilePath = filePath;
                Occurrence = occurrence;
                Preview = CollectionUsagePreviewBuilder.Build(
                    sourceText,
                    occurrence.StartOffset,
                    occurrence.Length,
                    4);
            }

            [NotNull]
            internal IPsiSourceFile SourceFile { get; }

            [NotNull]
            internal IDocument Document { get; }

            [NotNull]
            internal CollectionUsageOccurrence Occurrence { get; }

            [NotNull]
            private CollectionUsagePreview Preview { get; }

            [NotNull]
            public string FilePath { get; }

            public int StartOffset => Occurrence.StartOffset;

            public int Length => Occurrence.Length;

            public int Line => Occurrence.Line;

            public int Column => Occurrence.Column;

            [NotNull]
            public string Kind => Occurrence.Kind.ToString();

            [NotNull]
            public string KindDisplayName => GetCategoryDisplayName(Occurrence.Kind);

            [NotNull]
            public string Text => Occurrence.Text;

            [NotNull]
            public string PreviewText => Preview.Text;

            public int PreviewStartLine => Preview.StartLine;

            public int PreviewHighlightStart => Preview.HighlightStart;

            public int PreviewHighlightLength => Preview.HighlightLength;
        }

        [NotNull]
        private static string GetCategoryDisplayName(CollectionUsageKind kind)
        {
            switch (kind)
            {
                case CollectionUsageKind.CollectionStructureUsage:
                    return "원소 추가/삭제";

                case CollectionUsageKind.CollectionAssignment:
                    return "컬렉션 대입";

                case CollectionUsageKind.ElementWrite:
                    return "내용물 수정";

                case CollectionUsageKind.ElementAlias:
                case CollectionUsageKind.ElementEscape:
                    return "레퍼런스 넘기기";

                default:
                    return "컬렉션 사용";
            }
        }

        private sealed class CollectionUsageSearchRequest : SearchRequest
        {
            private readonly ISolution solution;
            private readonly CollectionSearchTarget target;
            private readonly CollectionSearchScopeKind scope;
            private readonly ICollection<IOccurrence> occurrences;
            private readonly ICollection searchTargets;

            public CollectionUsageSearchRequest(
                [NotNull] ISolution solution,
                [NotNull] CollectionSearchTarget target,
                CollectionSearchScopeKind scope,
                [NotNull] ICollection<IOccurrence> occurrences)
            {
                this.solution = solution;
                this.target = target;
                this.scope = scope;
                this.occurrences = occurrences;
                searchTargets = new object[] { target.DeclaredElement };
            }

            public override string Title => "Collection usages of '" + target.DisplayName + "'";

            public override ISolution Solution => solution;

            public override ICollection SearchTargets => searchTargets;

            public override ICollection<IOccurrence> Search(IProgressIndicator progressIndicator)
            {
                return occurrences;
            }

            public override string GetNotFoundMessage()
            {
                return "No collection-specific usages found for '" + target.DisplayName + "' in " +
                    GetScopeDisplayName(scope) + ".";
            }
        }

        private sealed class CollectionUsageSearchDescriptor : SearchDescriptor
        {
            public CollectionUsageSearchDescriptor(
                [NotNull] SearchRequest request,
                [NotNull] ICollection<IOccurrence> results)
                : base(request, results, null)
            {
                MergeOccurrences = false;
                KindFilterEnabled = true;
            }

            public override string GetResultsTitle(OccurrenceSection section)
            {
                var count = section.TotalCount;
                return count == 1
                    ? "1 collection usage"
                    : count + " collection usages";
            }

            protected override Func<SearchRequest, IOccurrenceBrowserDescriptor> GetDescriptorFactory()
            {
                return request => new CollectionUsageSearchDescriptor(request, request.Search());
            }
        }
    }
}
