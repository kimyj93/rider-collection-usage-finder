using System;
using System.Linq;
using JetBrains.Annotations;
using JetBrains.DocumentModel;
using JetBrains.ProjectModel;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.Files;
using JetBrains.ReSharper.Psi.Tree;
using JetBrains.Util;
using ReSharperPlugin.CollectionUsageFinder.Search;

namespace ReSharperPlugin.CollectionUsageFinder.Actions
{
    internal static class CollectionUsageProtocolTargetLocator
    {
        [CanBeNull]
        public static CollectionSearchTarget TryGetTarget(
            [NotNull] ISolution solution,
            [NotNull] string filePath,
            int caretOffset,
            [CanBeNull] out IPsiSourceFile sourceFile,
            [CanBeNull] out IDocument document,
            [NotNull] out string failureReason)
        {
            sourceFile = null;
            document = null;

            if (string.IsNullOrWhiteSpace(filePath))
            {
                failureReason = "컬렉션 사용 위치를 찾기 전에 C# 소스 파일을 열어야 합니다.";
                return null;
            }

            var projectFile = TryGetProjectFile(solution, filePath);
            if (projectFile == null)
            {
                failureReason = "The current file is not part of the loaded Rider solution.";
                return null;
            }

            sourceFile = projectFile.ToSourceFile();
            document = sourceFile?.Document;
            if (sourceFile == null || document == null)
            {
                failureReason = "Unable to resolve the current C# file in ReSharper PSI.";
                return null;
            }

            var identifierRange = TryGetIdentifierRange(document.GetText(), caretOffset);
            if (!identifierRange.IsValid)
            {
                failureReason = "Place the caret on a collection variable, field, property, or parameter name.";
                return null;
            }

            var target = TryGetTargetFromPsi(sourceFile, document, identifierRange);
            if (target == null)
            {
                failureReason = "The symbol under the caret is not a supported BCL collection target.";
                return null;
            }

            failureReason = string.Empty;
            return target;
        }

        [CanBeNull]
        private static IProjectFile TryGetProjectFile([NotNull] ISolution solution, [NotNull] string filePath)
        {
            try
            {
                var virtualPath = VirtualFileSystemPath.Parse(
                    filePath,
                    InteractionContext.SolutionContext,
                    FileSystemPathInternStrategy.INTERN);

                return solution
                    .FindProjectItemsByLocation(virtualPath)
                    .OfType<IProjectFile>()
                    .FirstOrDefault(IsCSharpProjectFile);
            }
            catch
            {
                return null;
            }
        }

        private static bool IsCSharpProjectFile([NotNull] IProjectFile projectFile)
        {
            return Equals(projectFile.LanguageType, CSharpProjectFileType.Instance);
        }

        [NotNull]
        private static TextRange TryGetIdentifierRange([NotNull] string text, int caretOffset)
        {
            if (text.Length == 0)
                return TextRange.InvalidRange;

            var offset = Math.Max(0, Math.Min(caretOffset, text.Length - 1));
            if (!IsIdentifierPart(text[offset]) && offset > 0)
                offset--;

            if (offset < 0 || !IsIdentifierPart(text[offset]))
                return TextRange.InvalidRange;

            var start = offset;
            while (start > 0 && IsIdentifierPart(text[start - 1]))
                start--;

            if (start > 0 && text[start - 1] == '@')
                start--;

            var end = offset + 1;
            while (end < text.Length && IsIdentifierPart(text[end]))
                end++;

            if (start >= end || (text[start] == '@' && end - start == 1))
                return TextRange.InvalidRange;

            return TextRange.FromLength(start, end - start);
        }

        private static bool IsIdentifierPart(char ch)
        {
            return ch == '_' || char.IsLetterOrDigit(ch);
        }

        [CanBeNull]
        private static CollectionSearchTarget TryGetTargetFromPsi(
            [NotNull] IPsiSourceFile sourceFile,
            [NotNull] IDocument document,
            [NotNull] TextRange identifierRange)
        {
            var psiFile = sourceFile.GetPrimaryPsiFile();
            if (psiFile == null)
                return null;

            var documentRange = new DocumentRange(document, identifierRange);
            var treeRange = psiFile.Translate(documentRange);
            if (!treeRange.IsValid())
                return null;

            foreach (var reference in psiFile.FindReferencesAt(treeRange))
            {
                if (!reference.IsValid())
                    continue;

                var target = CollectionSupport.TryCreateTarget(reference.Resolve().DeclaredElement);
                if (target != null)
                    return target;
            }

            var node = psiFile.FindNodeAt(treeRange);
            var declaration = node?.GetContainingNode<IDeclaration>(true);
            return CollectionSupport.TryCreateTarget(declaration?.DeclaredElement);
        }
    }
}
