package com.jetbrains.rider.plugins.collectionusagefinder

import com.intellij.openapi.actionSystem.ActionUpdateThread
import com.intellij.openapi.actionSystem.AnAction
import com.intellij.openapi.actionSystem.AnActionEvent
import com.intellij.openapi.actionSystem.CommonDataKeys
import com.intellij.openapi.fileEditor.FileDocumentManager
import com.intellij.openapi.project.Project
import com.intellij.openapi.ui.Messages
import com.jetbrains.rd.ide.model.CollectionUsageFindRequest
import com.jetbrains.rd.ide.model.collectionUsageFinderProtocol
import com.jetbrains.rider.protocol.protocol

class FindCollectionUsagesFrontendAction : AnAction(
    "Find Collection Usages",
    "Find collection-specific usages for supported C# BCL collections",
    null
) {
    override fun getActionUpdateThread(): ActionUpdateThread = ActionUpdateThread.EDT

    override fun update(e: AnActionEvent) {
        e.presentation.isVisible = true
        val virtualFile = e.getData(CommonDataKeys.VIRTUAL_FILE)
        e.presentation.isEnabled = e.project != null && virtualFile?.extension.equals("cs", ignoreCase = true)
    }

    override fun actionPerformed(e: AnActionEvent) {
        val project = e.project
        if (project == null) {
            Messages.showErrorDialog(
                null as Project?,
                "Open a C# project before running Find Collection Usages.",
                "CollectionUsageFinder"
            )
            return
        }

        val editor = e.getData(CommonDataKeys.EDITOR)
        val virtualFile = e.getData(CommonDataKeys.VIRTUAL_FILE)
        if (editor == null || virtualFile == null || !virtualFile.extension.equals("cs", ignoreCase = true)) {
            Messages.showErrorDialog(
                project,
                "Place the caret on a supported C# collection symbol before running Find Collection Usages.",
                "CollectionUsageFinder"
            )
            return
        }

        FileDocumentManager.getInstance().saveDocument(editor.document)
        val request = CollectionUsageFindRequest(virtualFile.path, editor.caretModel.offset)

        runCatching {
            project.protocol
                .collectionUsageFinderProtocol
                .findCollectionUsages
                .start(request)
        }.getOrElse { error ->
            Messages.showErrorDialog(
                project,
                "CollectionUsageFinder backend request failed:\n${error.message ?: error.javaClass.name}",
                "CollectionUsageFinder"
            )
            return
        }
    }
}
