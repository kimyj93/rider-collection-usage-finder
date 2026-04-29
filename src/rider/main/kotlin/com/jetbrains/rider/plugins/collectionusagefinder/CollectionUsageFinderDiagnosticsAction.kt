package com.jetbrains.rider.plugins.collectionusagefinder

import com.intellij.ide.plugins.PluginManagerCore
import com.intellij.openapi.actionSystem.ActionManager
import com.intellij.openapi.actionSystem.ActionUpdateThread
import com.intellij.openapi.actionSystem.AnAction
import com.intellij.openapi.actionSystem.AnActionEvent
import com.intellij.openapi.project.DumbAware
import com.intellij.openapi.extensions.PluginId
import com.intellij.openapi.ui.Messages
import java.nio.file.Files
import java.nio.file.Path
import java.time.OffsetDateTime

class CollectionUsageFinderDiagnosticsAction : AnAction(
    "CollectionUsageFinder Diagnostics",
    "Show frontend diagnostics for CollectionUsageFinder",
    null
), DumbAware {
    override fun getActionUpdateThread(): ActionUpdateThread = ActionUpdateThread.EDT

    override fun update(e: AnActionEvent) {
        e.presentation.isVisible = true
        e.presentation.isEnabled = true
    }

    override fun actionPerformed(e: AnActionEvent) {
        val pluginId = PluginId.getId("com.jetbrains.rider.plugins.collectionusagefinder")
        val plugin = PluginManagerCore.getPlugin(pluginId)
        val actionManager = ActionManager.getInstance()
        val backendDiagnosticsPath = Path.of(System.getProperty("java.io.tmpdir"), "CollectionUsageFinder.backend-diagnostics.txt")
        val backendDiagnostics = if (Files.exists(backendDiagnosticsPath)) {
            runCatching { Files.readString(backendDiagnosticsPath) }.getOrDefault("<failed to read backend diagnostics>")
        } else {
            "<missing>"
        }
        val backendDiagnosticsModifiedAt = if (Files.exists(backendDiagnosticsPath)) {
            runCatching { Files.getLastModifiedTime(backendDiagnosticsPath).toString() }
                .getOrDefault("<failed to read last modified time>")
        } else {
            "<missing>"
        }
        val lines = listOf(
            "Frontend diagnostics generated at: ${OffsetDateTime.now()}",
            "Plugin loaded: ${plugin != null}",
            "Plugin version: ${plugin?.version ?: "<missing>"}",
            "Plugin path: ${plugin?.pluginPath ?: "<missing>"}",
            "Launcher action registered: ${actionManager.getAction("CollectionUsageFinder.FindCollectionUsagesLauncher") != null}",
            "Launcher action type: ${actionManager.getAction("CollectionUsageFinder.FindCollectionUsagesLauncher")?.javaClass?.name ?: "<missing>"}",
            "Diagnostics action registered: ${actionManager.getAction("CollectionUsageFinder.Diagnostics") != null}",
            "Backend diagnostics path: $backendDiagnosticsPath",
            "Backend diagnostics modified at: $backendDiagnosticsModifiedAt",
            "",
            "Backend diagnostics:",
            backendDiagnostics,
            "",
            "If this dialog opens, the frontend plugin is installed and loaded.",
            "After Rider starts, expected backend event is HostCreated.",
            "After invoking 'Find Collection Usages', expected backend events are RequestReceived, SearchExecuting, SearchStarted, then SearchCompleted/TargetNotFound/SearchFailed."
        )

        Messages.showInfoMessage(
            e.project,
            lines.joinToString("\n"),
            "CollectionUsageFinder Diagnostics"
        )
    }
}
