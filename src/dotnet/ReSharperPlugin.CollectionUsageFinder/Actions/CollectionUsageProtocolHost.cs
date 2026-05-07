#if RIDER
using System;
using System.Collections.Generic;
using System.Linq;
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

                return ExecuteSearch(request);
            }
            catch (Exception exception)
            {
                SetLastEvent("RequestFailed");
                CollectionUsageBackendDiagnostics.AppendProtocolEvent("RequestFailed", exception.ToString());
                return CreateFailureResponse(
                    false,
                    "CollectionUsageFinder backend failed:\n" + exception.Message);
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

                return ExecuteWithCompatibleReadLock(() => ExecuteUnderReadLock(request));
            }
            catch (Exception exception)
            {
                SetLastEvent("SearchFailed");
                CollectionUsageBackendDiagnostics.AppendProtocolEvent("SearchFailed", exception.ToString());
                return CreateFailureResponse(
                    false,
                "CollectionUsageFinder backend search failed:\n" + exception.Message);
            }
        }

        [NotNull]
        private CollectionUsageFindResponse ExecuteWithCompatibleReadLock([NotNull] Func<CollectionUsageFindResponse> action)
        {
            CollectionUsageFindResponse response = null;

            InvokeCompatibleExecuteWithReadLock(() =>
            {
                response = action();
            });

            return response ?? CreateFailureResponse(
                false,
                "CollectionUsageFinder backend search failed:\nRead lock execution did not return a result.");
        }

        private void InvokeCompatibleExecuteWithReadLock([NotNull] Action action)
        {
            var shellLocksExType = typeof(IShellLocks).Assembly.GetType("JetBrains.Application.Threading.IShellLocksEx");
            if (shellLocksExType == null)
                throw new InvalidOperationException("IShellLocksEx type was not found.");

            var methods = shellLocksExType.GetMethods(
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

            var methodWithCallerInfo = methods.FirstOrDefault(static method =>
            {
                if (method.Name != "ExecuteWithReadLock" || method.ReturnType != typeof(void))
                    return false;

                var parameters = method.GetParameters();
                return parameters.Length == 4 &&
                       parameters[0].ParameterType == typeof(IShellLocks) &&
                       parameters[1].ParameterType == typeof(Action) &&
                       parameters[2].ParameterType == typeof(string) &&
                       parameters[3].ParameterType == typeof(string);
            });

            var legacyMethod = methods.FirstOrDefault(static method =>
            {
                if (method.Name != "ExecuteWithReadLock" || method.ReturnType != typeof(void))
                    return false;

                var parameters = method.GetParameters();
                return parameters.Length == 2 &&
                       parameters[0].ParameterType == typeof(IShellLocks) &&
                       parameters[1].ParameterType == typeof(Action);
            });

            var method = methodWithCallerInfo ?? legacyMethod;
            if (method == null)
                throw new MissingMethodException(
                    "JetBrains.Application.Threading.IShellLocksEx.ExecuteWithReadLock",
                    "No supported Action overload was found.");

            try
            {
                if (method.GetParameters().Length == 4)
                    method.Invoke(null, new object[] { myLocks, action, "CollectionUsageFinder", "FindCollectionUsages" });
                else
                    method.Invoke(null, new object[] { myLocks, action });
            }
            catch (System.Reflection.TargetInvocationException exception)
            {
                throw exception.InnerException ?? exception;
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
                return CreateFailureResponse(
                    false,
                    "컬렉션 사용 위치를 찾기 전에 C# 솔루션을 열어야 합니다.");
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
                return CreateFailureResponse(false, failureReason);
            }

            SetLastEvent("SearchStarted");
            CollectionUsageBackendDiagnostics.AppendProtocolEvent(
                "SearchStarted",
                target.DisplayName + " (" + target.CollectionKind + ")");

            var result = CollectionUsageFindResultsRunner.Analyze(solution, sourceFile, document, target);
            var items = result.Items
                .Select(item => new CollectionUsageResultItem(
                    item.FilePath,
                    item.StartOffset,
                    item.Length,
                    item.Line,
                    item.Column,
                    item.Kind,
                    item.KindDisplayName,
                    item.OperationKind,
                    item.OperationDisplayName,
                    item.OperationName,
                    item.Text,
                    item.PreviewText,
                    item.PreviewStartLine,
                    item.PreviewHighlightStart,
                    item.PreviewHighlightLength))
                .ToList();

            SetLastEvent("SearchCompleted");
            CollectionUsageBackendDiagnostics.AppendProtocolEvent(
                "SearchCompleted",
                target.DisplayName + " (" + items.Count + ")");
            return new CollectionUsageFindResponse(
                true,
                items.Count == 0
                    ? "No collection-specific usages found for '" + target.DisplayName + "'."
                    : string.Empty,
                result.TargetName,
                result.CollectionKind,
                result.Scope.ToString(),
                items);
        }

        [NotNull]
        private static CollectionUsageFindResponse CreateFailureResponse(
            bool success,
            [NotNull] string message)
        {
            return new CollectionUsageFindResponse(
                success,
                message,
                string.Empty,
                string.Empty,
                string.Empty,
                new List<CollectionUsageResultItem>());
        }

        private static void SetLastEvent([NotNull] string eventName)
        {
            LastEvent = DateTimeOffset.Now.ToString("O") + " " + eventName;
        }
    }
}
#endif
