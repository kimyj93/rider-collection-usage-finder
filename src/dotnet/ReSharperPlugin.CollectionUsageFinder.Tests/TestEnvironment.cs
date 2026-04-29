using System.Threading;
using JetBrains.Application.BuildScript.Application.Zones;
using JetBrains.ReSharper.Feature.Services;
using JetBrains.ReSharper.Psi.CSharp;
using JetBrains.ReSharper.TestFramework;
using JetBrains.TestFramework;
using JetBrains.TestFramework.Application.Zones;
using NUnit.Framework;

[assembly: Apartment(ApartmentState.STA)]

namespace ReSharperPlugin.CollectionUsageFinder.Tests
{
    [ZoneDefinition]
    public class CollectionUsageFinderTestEnvironmentZone : ITestsEnvZone, IRequire<PsiFeatureTestZone>, IRequire<ICollectionUsageFinderZone> { }

    [ZoneMarker]
    public class ZoneMarker : IRequire<ICodeEditingZone>, IRequire<ILanguageCSharpZone>, IRequire<CollectionUsageFinderTestEnvironmentZone> { }

    [SetUpFixture]
    public class CollectionUsageFinderTestsAssembly : ExtensionTestEnvironmentAssembly<CollectionUsageFinderTestEnvironmentZone> { }
}
