using System;
using System.Collections;
using JetBrains.Annotations;
using JetBrains.Application.DataContext;
using JetBrains.Application.UI.Actions;
using JetBrains.Application.UI.ActionsRevised.Menu;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.DataContext;
using ReSharperPlugin.CollectionUsageFinder.Search;

namespace ReSharperPlugin.CollectionUsageFinder.Actions
{
    // Kept as a dormant backend fallback for local debugging.
    // The public 1.0 entry point is the Rider frontend action that opens the custom popup.
    public class FindCollectionUsagesAction :
        IExecutableAction
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
        public static string FindCollectionUsagesActionText => "컬렉션 사용 위치 찾기";
    }
}
