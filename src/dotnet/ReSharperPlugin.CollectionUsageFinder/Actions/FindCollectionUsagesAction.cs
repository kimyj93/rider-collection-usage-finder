using System;
using System.Collections;
using JetBrains.Annotations;
using JetBrains.Application.DataContext;
using JetBrains.Application.UI.Actions;
using JetBrains.Application.Shortcuts.ShortcutManager;
using JetBrains.Application.UI.ActionsRevised.Menu;
using JetBrains.Application.UI.ActionSystem.ActionsRevised.Menu;
using JetBrains.ReSharper.Feature.Services.Menu;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.DataContext;
using JetBrains.Util;
using ReSharperPlugin.CollectionUsageFinder.Search;

namespace ReSharperPlugin.CollectionUsageFinder.Actions
{
#pragma warning disable 612 // Custom backend action IDs still require the legacy constructor in this SDK surface.
    [Action(
        "CollectionUsageFinder.FindCollectionUsages",
        typeof(CollectionUsageActionStrings),
        nameof(CollectionUsageActionStrings.FindCollectionUsagesActionText),
        IdeaShortcuts = new[] { "Control+F12" },
        DefaultShortcutText = "Ctrl+F12",
        ShortcutScope = ShortcutScope.TextEditor)]
    public class FindCollectionUsagesAction :
        IExecutableAction,
        IInsertLast<FindUsagesGroup>,
        IInsertLast<FindUsagesContextualGroup>
    {
        public bool Update(IDataContext context, ActionPresentation presentation, DelegateUpdate nextUpdate)
        {
            return FindCollectionUsagesTargetLocator.TryGetTarget(context) != null;
        }

        public void Execute(IDataContext context, DelegateExecute nextExecute)
        {
            var target = FindCollectionUsagesTargetLocator.TryGetTarget(context);
            if (target == null)
                return;

            CollectionUsageFindResultsRunner.Execute(context, target, null);
        }
    }
#pragma warning restore 612

    internal static class FindCollectionUsagesTargetLocator
    {
        [CanBeNull]
        internal static CollectionSearchTarget TryGetTarget([NotNull] IDataContext dataContext)
        {
            var declaredElements = dataContext.GetData(PsiDataConstants.DECLARED_ELEMENTS) as IEnumerable;
            if (declaredElements == null)
                return null;

            foreach (var candidate in declaredElements)
            {
                if (candidate is not IDeclaredElement declaredElement)
                    continue;

                var target = CollectionSupport.TryCreateTarget(declaredElement);
                if (target != null)
                    return target;
            }

            return null;
        }
    }

    public static class CollectionUsageActionStrings
    {
        public static string FindCollectionUsagesActionText => "Find Collection Usages";
    }
}
