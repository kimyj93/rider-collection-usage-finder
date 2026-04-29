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
            var searchDocuments = CollectSearchDocuments(sourceFile, document, target).ToArray();
            var scope = CollectionSearchScopePolicy.GetSearchScope(target.TargetKind);
            var analyzer = new CSharpCollectionUsageTextAnalyzer();
            var occurrences = searchDocuments
                .SelectMany(searchDocument => AnalyzeDocument(searchDocument, analyzer, target))
                .ToArray();

            if (occurrences.Length == 0)
            {
                MessageBox.ShowInfo(
                    "No collection-specific usages found for '" + target.DisplayName + "' in " +
                    GetScopeDisplayName(scope) + ".");
                return;
            }

            IOccurrenceBrowserDescriptor CreateDescriptor()
            {
                return new CollectionUsageSearchDescriptor(
                    new CollectionUsageSearchRequest(solution, target, scope, occurrences),
                    occurrences);
            }

            if (host != null)
                host.ShowFindResults(CreateDescriptor);
            else
                FindResultsBrowserUtil.ShowResults(CreateDescriptor());
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
        private static IEnumerable<IOccurrence> AnalyzeDocument(
            [NotNull] CollectionUsageSearchDocument searchDocument,
            [NotNull] CSharpCollectionUsageTextAnalyzer analyzer,
            [NotNull] CollectionSearchTarget target)
        {
            var targetName = target.DisplayName;
            var sourceText = searchDocument.Document.GetText();
            if (sourceText.IndexOf(targetName, StringComparison.Ordinal) < 0)
                return Array.Empty<IOccurrence>();

            var analysisOccurrences = analyzer.Analyze(
                sourceText,
                targetName,
                targetOffset => IsTargetReferenceAllowed(searchDocument, target, targetOffset));
            return CreateOccurrences(searchDocument.SourceFile, searchDocument.Document, analysisOccurrences);
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
            [NotNull] IPsiSourceFile sourceFile,
            [NotNull] IDocument document,
            [NotNull] IEnumerable<CollectionUsageOccurrence> analysisOccurrences)
        {
            var documentLength = document.GetTextLength();
            if (documentLength <= 0)
                yield break;

            foreach (var occurrence in analysisOccurrences)
            {
                if (occurrence.StartOffset < 0 || occurrence.StartOffset >= documentLength)
                    continue;

                var length = Math.Max(1, Math.Min(occurrence.Length, documentLength - occurrence.StartOffset));
                var range = new DocumentRange(
                    document,
                    TextRange.FromLength(occurrence.StartOffset, length));

                yield return new CollectionUsageRangeOccurrence(sourceFile, range, occurrence);
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
            public CollectionUsageSearchDocument([NotNull] IPsiSourceFile sourceFile, [NotNull] IDocument document)
            {
                SourceFile = sourceFile;
                Document = document;
            }

            [NotNull]
            public IPsiSourceFile SourceFile { get; }

            [NotNull]
            public IDocument Document { get; }
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
