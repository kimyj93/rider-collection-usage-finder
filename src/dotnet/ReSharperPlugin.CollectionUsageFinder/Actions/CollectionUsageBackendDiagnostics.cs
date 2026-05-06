using System;
using System.IO;
using System.Linq;
using JetBrains.Annotations;
using JetBrains.Application;
using JetBrains.Application.Parts;
using JetBrains.Application.UI.Actions.ActionManager;
using ReSharperPlugin.CollectionUsageFinder.Actions;

namespace ReSharperPlugin.CollectionUsageFinder.Actions
{
    [ShellComponent]
    public class CollectionUsageBackendDiagnostics
    {
        public const string RequestedActionId = "CollectionUsageFinder.FindCollectionUsages";
        public const string DiagnosticsFileName = "CollectionUsageFinder.backend-diagnostics.txt";

        public CollectionUsageBackendDiagnostics([NotNull] IActionManager actionManager)
        {
            WriteSnapshot(actionManager);
        }

        internal static string GetDiagnosticsFilePath()
        {
            return Path.Combine(Path.GetTempPath(), DiagnosticsFileName);
        }

        private static void WriteSnapshot([NotNull] IActionManager actionManager)
        {
            try
            {
                var defs = actionManager.Defs;
                var directMatch = defs.TryGetActionDefById(RequestedActionId);
                var dollarMatch = defs.TryGetActionDefById("$" + RequestedActionId);
                var relatedActionIds = defs.GetAllActionDefs()
                    .Select(def => def.ActionId)
                    .Where(id => !string.IsNullOrEmpty(id))
                    .Where(id =>
                        id.IndexOf("CollectionUsage", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        id.IndexOf("FindCollectionUsages", StringComparison.OrdinalIgnoreCase) >= 0)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(id => id, StringComparer.Ordinal)
                    .ToArray();

                var lines = new[]
                {
                    "GeneratedAt=" + DateTimeOffset.Now.ToString("O"),
                    "RequestedActionId=" + RequestedActionId,
                    "RequestedActionRegistered=" + (directMatch != null),
                    "DollarRequestedActionRegistered=" + (dollarMatch != null),
                    "BackendLegacyActionRegistered=<disabled-in-1.0>",
#if RIDER
                    "BackendProtocolHostCreated=" + CollectionUsageProtocolHost.InstanceCreated,
                    "BackendProtocolLastEvent=" + CollectionUsageProtocolHost.LastEvent,
#else
                    "BackendProtocolHostCreated=<not-built-in-rider-target>",
                    "BackendProtocolLastEvent=<not-built-in-rider-target>",
#endif
                    "RelatedActionIds=" + (relatedActionIds.Length == 0 ? "<none>" : string.Join("|", relatedActionIds))
                };

                File.WriteAllLines(GetDiagnosticsFilePath(), lines);
            }
            catch (Exception exception)
            {
                try
                {
                    File.WriteAllLines(
                        GetDiagnosticsFilePath(),
                        new[]
                        {
                            "GeneratedAt=" + DateTimeOffset.Now.ToString("O"),
                            "RequestedActionId=" + RequestedActionId,
                            "DiagnosticsError=" + exception
                        });
                }
                catch
                {
                    // Best-effort diagnostics only.
                }
            }
        }

        internal static void AppendProtocolEvent([NotNull] string eventName, [CanBeNull] string detail)
        {
            try
            {
#if RIDER
                var lines = new[]
                {
                    "BackendProtocolEventAt=" + DateTimeOffset.Now.ToString("O"),
                    "BackendProtocolEvent=" + eventName,
                    "BackendProtocolEventDetail=" + (detail ?? string.Empty),
                    "BackendProtocolHostCreatedNow=" + CollectionUsageProtocolHost.InstanceCreated,
                    "BackendProtocolLastEventNow=" + CollectionUsageProtocolHost.LastEvent
                };
#else
                var lines = new[]
                {
                    "BackendProtocolEventAt=" + DateTimeOffset.Now.ToString("O"),
                    "BackendProtocolEvent=" + eventName,
                    "BackendProtocolEventDetail=" + (detail ?? string.Empty)
                };
#endif

                File.AppendAllLines(
                    GetDiagnosticsFilePath(),
                    lines);
            }
            catch
            {
                // Best-effort diagnostics only.
            }
        }
    }
}
