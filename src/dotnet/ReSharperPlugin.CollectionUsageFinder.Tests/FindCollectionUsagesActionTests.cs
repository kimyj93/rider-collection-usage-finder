using System.Linq;
using JetBrains.Application.DataContext;
using JetBrains.Application.UI.Actions;
using JetBrains.Application.UI.ActionsRevised.Menu;
using JetBrains.Application.UI.ActionSystem.ActionsRevised.Menu;
using JetBrains.ReSharper.Feature.Services.Menu;
using NUnit.Framework;
using ReSharperPlugin.CollectionUsageFinder.Actions;

namespace ReSharperPlugin.CollectionUsageFinder.Tests
{
    [TestFixture]
    public class FindCollectionUsagesActionTests
    {
        [Test]
        public void Action_ImplementsExecutableAction()
        {
            Assert.That(
                typeof(IExecutableAction).IsAssignableFrom(typeof(FindCollectionUsagesAction)),
                Is.True);
        }

        [Test]
        public void Action_IsNotRegisteredAsBackendUserAction()
        {
            var actionAttributes = typeof(FindCollectionUsagesAction)
                .GetCustomAttributes(typeof(ActionAttribute), false)
                .Cast<ActionAttribute>();

            Assert.That(actionAttributes, Is.Empty);
        }

        [Test]
        public void Action_IsNotInsertedIntoFindUsagesMenus()
        {
            Assert.That(
                typeof(IInsertLast<FindUsagesGroup>).IsAssignableFrom(typeof(FindCollectionUsagesAction)),
                Is.False);

            Assert.That(
                typeof(IInsertLast<FindUsagesContextualGroup>).IsAssignableFrom(typeof(FindCollectionUsagesAction)),
                Is.False);
        }

        [Test]
        public void Action_IsNotInsertedIntoToolsMenu()
        {
            Assert.That(
                typeof(IInsertLast<ToolsMenu>).IsAssignableFrom(typeof(FindCollectionUsagesAction)),
                Is.False);
        }

        [Test]
        public void TargetLocator_IsStaticHelper()
        {
            var targetLocatorType = typeof(FindCollectionUsagesAction).Assembly.GetType(
                "ReSharperPlugin.CollectionUsageFinder.Actions.FindCollectionUsagesTargetLocator",
                throwOnError: true);

            Assert.That(
                targetLocatorType.IsAbstract &&
                targetLocatorType.IsSealed,
                Is.True);
        }

        [Test]
        public void Action_DeclaresExpectedMethods()
        {
            Assert.That(
                typeof(FindCollectionUsagesAction).GetMethod(
                    nameof(IExecutableAction.Update),
                    new[] { typeof(IDataContext), typeof(ActionPresentation), typeof(DelegateUpdate) }),
                Is.Not.Null);

            Assert.That(
                typeof(FindCollectionUsagesAction).GetMethod(
                    nameof(IExecutableAction.Execute),
                    new[] { typeof(IDataContext), typeof(DelegateExecute) }),
                Is.Not.Null);
        }
    }
}
