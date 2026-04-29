#if RIDER
using System;
using JetBrains.Annotations;
using JetBrains.Application;
using JetBrains.Application.Progress;
using JetBrains.Application.Threading;
using JetBrains.ProjectModel;
using JetBrains.RdBackend.Common.Features.Util;
using JetBrains.Rider.Model;
using JetBrains.Util;

namespace ReSharperPlugin.CollectionUsageFinder.Actions
{
    [ShellComponent]
    public class CollectionUsageProtocolHost
    {
        private readonly SolutionsManager mySolutionsManager;
        private readonly IShellLocks myLocks;

        public static bool InstanceCreated { get; private set; }

        [NotNull]
        public static string LastEvent { get; private set; } = "<none>";

        public CollectionUsageProtocolHost(
            [NotNull] SolutionsManager solutionsManager,
            [NotNull] IShellLocks locks,
            [NotNull] CollectionUsageFinderProtocol protocol)
        {
            mySolutionsManager = solutionsManager;
            myLocks = locks;
            InstanceCreated = true;
            SetLastEvent("HostCreated");
            CollectionUsageBackendDiagnostics.AppendProtocolEvent("HostCreated", null);

            protocol.FindCollectionUsages.Set(HandleFindCollectionUsages);
        }

        [NotNull]
        private CollectionUsageFindResponse HandleFindCollectionUsages(
            [NotNull] CollectionUsageFindRequest request,
            [NotNull] IProgressIndicator progress)
        {
            try
            {
                SetLastEvent("RequestReceived");
                CollectionUsageBackendDiagnostics.AppendProtocolEvent(
                    "RequestReceived",
                    request.FilePath + ":" + request.CaretOffset);

                var response = ExecuteSearch(request);
                if (!response.Success)
                    MessageBox.ShowInfo(response.Message);

                return response;
            }
            catch (Exception exception)
            {
                SetLastEvent("RequestFailed");
                CollectionUsageBackendDiagnostics.AppendProtocolEvent("RequestFailed", exception.ToString());
                var response = new CollectionUsageFindResponse(
                    false,
                    "CollectionUsageFinder backend failed:\n" + exception.Message);
                MessageBox.ShowInfo(response.Message);
                return response;
            }
        }

        [NotNull]
        private CollectionUsageFindResponse ExecuteSearch([NotNull] CollectionUsageFindRequest request)
        {
            try
            {
                SetLastEvent("SearchExecuting");
                CollectionUsageBackendDiagnostics.AppendProtocolEvent(
                    "SearchExecuting",
                    request.FilePath + ":" + request.CaretOffset);

                return myLocks.ExecuteWithReadLock(
                    () => ExecuteUnderReadLock(request),
                    "CollectionUsageFinder",
                    "FindCollectionUsages");
            }
            catch (Exception exception)
            {
                SetLastEvent("SearchFailed");
                CollectionUsageBackendDiagnostics.AppendProtocolEvent("SearchFailed", exception.ToString());
                return new CollectionUsageFindResponse(
                    false,
                    "CollectionUsageFinder backend search failed:\n" + exception.Message);
            }
        }

        [NotNull]
        private CollectionUsageFindResponse ExecuteUnderReadLock([NotNull] CollectionUsageFindRequest request)
        {
            var solution = mySolutionsManager.Solution;
            if (solution == null)
            {
                SetLastEvent("SolutionNotFound");
                CollectionUsageBackendDiagnostics.AppendProtocolEvent("SolutionNotFound", null);
                return new CollectionUsageFindResponse(
                    false,
                    "Open a C# solution before running Find Collection Usages.");
            }

            var target = CollectionUsageProtocolTargetLocator.TryGetTarget(
                solution,
                request.FilePath,
                request.CaretOffset,
                out var sourceFile,
                out var document,
                out var failureReason);

            if (target == null || sourceFile == null || document == null)
            {
                SetLastEvent("TargetNotFound");
                CollectionUsageBackendDiagnostics.AppendProtocolEvent("TargetNotFound", failureReason);
                return new CollectionUsageFindResponse(false, failureReason);
            }

            SetLastEvent("SearchStarted");
            CollectionUsageBackendDiagnostics.AppendProtocolEvent(
                "SearchStarted",
                target.DisplayName + " (" + target.CollectionKind + ")");

            CollectionUsageFindResultsRunner.Execute(solution, sourceFile, document, target);

            SetLastEvent("SearchCompleted");
            CollectionUsageBackendDiagnostics.AppendProtocolEvent("SearchCompleted", target.DisplayName);
            return new CollectionUsageFindResponse(true, "Search completed.");
        }

        private static void SetLastEvent([NotNull] string eventName)
        {
            LastEvent = DateTimeOffset.Now.ToString("O") + " " + eventName;
        }
    }
}
#endif
