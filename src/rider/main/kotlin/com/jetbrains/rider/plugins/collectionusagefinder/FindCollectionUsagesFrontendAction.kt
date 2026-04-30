package com.jetbrains.rider.plugins.collectionusagefinder

import com.intellij.openapi.application.ApplicationManager
import com.intellij.openapi.actionSystem.ActionUpdateThread
import com.intellij.openapi.actionSystem.AnAction
import com.intellij.openapi.actionSystem.AnActionEvent
import com.intellij.openapi.actionSystem.CommonDataKeys
import com.intellij.openapi.editor.Editor
import com.intellij.openapi.fileEditor.FileDocumentManager
import com.intellij.openapi.fileEditor.OpenFileDescriptor
import com.intellij.openapi.project.Project
import com.intellij.openapi.ui.Messages
import com.intellij.openapi.ui.popup.JBPopup
import com.intellij.openapi.ui.popup.JBPopupFactory
import com.intellij.openapi.util.io.FileUtil
import com.intellij.openapi.vfs.LocalFileSystem
import com.intellij.ui.ColoredListCellRenderer
import com.intellij.ui.ScrollPaneFactory
import com.intellij.ui.SimpleTextAttributes
import com.intellij.ui.components.JBCheckBox
import com.intellij.ui.components.JBLabel
import com.intellij.ui.components.JBList
import com.jetbrains.rd.framework.RdTaskResult
import com.jetbrains.rd.ide.model.CollectionUsageFindRequest
import com.jetbrains.rd.ide.model.CollectionUsageFindResponse
import com.jetbrains.rd.ide.model.CollectionUsageResultItem
import com.jetbrains.rd.ide.model.collectionUsageFinderProtocol
import com.jetbrains.rider.protocol.protocol
import java.awt.BorderLayout
import java.awt.Dimension
import java.awt.FlowLayout
import java.awt.Font
import java.awt.event.KeyAdapter
import java.awt.event.KeyEvent
import java.awt.event.MouseAdapter
import java.awt.event.MouseEvent
import java.io.File
import javax.swing.DefaultListModel
import javax.swing.JList
import javax.swing.JPanel
import javax.swing.JSplitPane
import javax.swing.JTextPane
import javax.swing.ListSelectionModel
import javax.swing.border.EmptyBorder
import javax.swing.text.DefaultStyledDocument
import javax.swing.text.SimpleAttributeSet
import javax.swing.text.StyleConstants

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
        val protocol = project.protocol

        val task = runCatching {
            protocol.collectionUsageFinderProtocol
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

        task.result.advise(protocol.lifetime) { taskResult ->
            ApplicationManager.getApplication().invokeLater {
                if (project.isDisposed) {
                    return@invokeLater
                }

                when (taskResult) {
                    is RdTaskResult.Success -> showResult(project, editor, taskResult.value)
                    is RdTaskResult.Fault -> Messages.showErrorDialog(
                        project,
                        "CollectionUsageFinder backend request failed:\n${taskResult.error.reasonMessage.ifBlank { taskResult.error.message ?: taskResult.error.javaClass.name }}",
                        "CollectionUsageFinder"
                    )
                    is RdTaskResult.Cancelled -> Messages.showInfoMessage(
                        project,
                        "CollectionUsageFinder backend request was cancelled.",
                        "CollectionUsageFinder"
                    )
                }
            }
        }
    }

    private fun showResult(project: Project, editor: Editor, response: CollectionUsageFindResponse) {
        if (!response.success) {
            Messages.showInfoMessage(project, response.message, "CollectionUsageFinder")
            return
        }

        if (response.items.isEmpty()) {
            Messages.showInfoMessage(
                project,
                response.message.ifBlank {
                    "No collection-specific usages found for '${response.targetName}'."
                },
                "CollectionUsageFinder"
            )
            return
        }

        val categories = createUsageCategories()
        val selectedCategoryIds = categories.mapTo(mutableSetOf()) { it.id }
        val listModel = DefaultListModel<PopupRow>()
        val list = JBList(listModel)
        val countLabel = JBLabel()
        val previewTitle = JBLabel("미리보기")
        val previewPane = createPreviewPane()

        list.visibleRowCount = minOf(response.items.size + categories.size, 14)
        list.selectionMode = ListSelectionModel.SINGLE_SELECTION
        list.cellRenderer = CollectionUsagePopupRenderer()

        fun refreshRows() {
            val visibleItems = response.items.filter { item ->
                categories.any { category -> category.id in selectedCategoryIds && category.matches(item) }
            }
            val rows = createRows(visibleItems, categories.filter { it.id in selectedCategoryIds })
            setRows(listModel, rows)
            countLabel.text = if (visibleItems.size == response.items.size) {
                "${response.items.size}개의 사용 위치"
            } else {
                "${visibleItems.size} / ${response.items.size}개의 사용 위치"
            }
            selectFirstUsageRow(list)
            updatePreview(previewTitle, previewPane, list.selectedValue as? PopupRow.Usage)
        }

        list.addListSelectionListener { event ->
            if (!event.valueIsAdjusting) {
                updatePreview(previewTitle, previewPane, list.selectedValue as? PopupRow.Usage)
            }
        }

        val headerPanel = JPanel(BorderLayout())
        headerPanel.add(JBLabel("'${response.targetName}' 사용 위치"), BorderLayout.WEST)
        headerPanel.add(countLabel, BorderLayout.EAST)

        val filtersPanel = JPanel(FlowLayout(FlowLayout.LEFT, 8, 0))
        for (category in categories) {
            val count = response.items.count(category.matches)
            val checkBox = JBCheckBox("${category.title} ($count)", true)
            checkBox.addActionListener {
                if (checkBox.isSelected) {
                    selectedCategoryIds += category.id
                } else {
                    selectedCategoryIds -= category.id
                }
                refreshRows()
            }
            filtersPanel.add(checkBox)
        }

        val controlsPanel = JPanel(BorderLayout())
        controlsPanel.border = EmptyBorder(8, 10, 6, 10)
        controlsPanel.add(headerPanel, BorderLayout.NORTH)
        controlsPanel.add(filtersPanel, BorderLayout.SOUTH)

        val resultsPanel = JPanel(BorderLayout())
        resultsPanel.add(controlsPanel, BorderLayout.NORTH)
        resultsPanel.add(ScrollPaneFactory.createScrollPane(list), BorderLayout.CENTER)

        val previewPanel = JPanel(BorderLayout())
        previewTitle.border = EmptyBorder(6, 10, 4, 10)
        previewPanel.add(previewTitle, BorderLayout.NORTH)
        previewPanel.add(ScrollPaneFactory.createScrollPane(previewPane), BorderLayout.CENTER)

        val splitter = JSplitPane(JSplitPane.VERTICAL_SPLIT, resultsPanel, previewPanel)
        splitter.resizeWeight = 0.56
        splitter.dividerSize = 5

        val panel = JPanel(BorderLayout())
        panel.preferredSize = Dimension(920, 640)
        panel.add(splitter, BorderLayout.CENTER)

        val popup = JBPopupFactory.getInstance()
            .createComponentPopupBuilder(panel, list)
            .setTitle("Collection Usages of '${response.targetName}'")
            .setResizable(true)
            .setMovable(true)
            .setRequestFocus(true)
            .createPopup()

        installNavigationHandlers(project, list, popup)
        refreshRows()
        popup.showInBestPositionFor(editor)
    }

    private fun createRows(
        items: List<CollectionUsageResultItem>,
        categories: List<UsageCategory>
    ): List<PopupRow> {
        val rows = mutableListOf<PopupRow>()
        val consumed = mutableSetOf<CollectionUsageResultItem>()

        for (category in categories) {
            val groupItems = items.filter(category.matches)
            if (groupItems.isEmpty()) {
                continue
            }

            rows += PopupRow.Header(category.title, groupItems.size)
            rows += groupItems.map { PopupRow.Usage(it) }
            consumed += groupItems
        }

        val otherItems = items.filterNot(consumed::contains)
        if (otherItems.isNotEmpty()) {
            rows += PopupRow.Header("기타", otherItems.size)
            rows += otherItems.map { PopupRow.Usage(it) }
        }

        return rows
    }

    private fun setRows(model: DefaultListModel<PopupRow>, rows: List<PopupRow>) {
        model.clear()
        rows.forEach(model::addElement)
    }

    private fun installNavigationHandlers(project: Project, list: JBList<PopupRow>, popup: JBPopup) {
        fun navigateSelected() {
            val row = list.selectedValue as? PopupRow.Usage ?: return
            navigateToUsage(project, row.item, popup)
        }

        list.addMouseListener(object : MouseAdapter() {
            override fun mouseClicked(e: MouseEvent) {
                if (e.clickCount == 2) {
                    navigateSelected()
                }
            }
        })
        list.addKeyListener(object : KeyAdapter() {
            override fun keyPressed(e: KeyEvent) {
                if (e.keyCode == KeyEvent.VK_ENTER) {
                    navigateSelected()
                    e.consume()
                }
            }
        })
    }

    private fun navigateToUsage(project: Project, item: CollectionUsageResultItem, popup: JBPopup) {
        val filePath = FileUtil.toSystemIndependentName(item.filePath)
        val virtualFile = LocalFileSystem.getInstance().findFileByPath(filePath)
        if (virtualFile == null) {
            Messages.showErrorDialog(
                project,
                "Could not open result file:\n${item.filePath}",
                "CollectionUsageFinder"
            )
            return
        }

        popup.cancel()
        OpenFileDescriptor(project, virtualFile, maxOf(0, item.startOffset)).navigate(true)
    }

    private fun selectFirstUsageRow(list: JBList<PopupRow>) {
        val index = (0 until list.model.size).firstOrNull { list.model.getElementAt(it) is PopupRow.Usage } ?: return
        list.selectedIndex = index
    }

    private fun createPreviewPane(): JTextPane {
        val pane = JTextPane()
        pane.isEditable = false
        pane.font = Font(Font.MONOSPACED, Font.PLAIN, 12)
        pane.border = EmptyBorder(8, 10, 8, 10)
        return pane
    }

    private fun updatePreview(
        title: JBLabel,
        previewPane: JTextPane,
        row: PopupRow.Usage?
    ) {
        if (row == null) {
            title.text = "미리보기"
            previewPane.text = "표시할 결과가 없습니다."
            return
        }

        val item = row.item
        title.text = "${File(item.filePath).name}  (${item.previewStartLine}줄부터)"
        val document = DefaultStyledDocument()
        val regular = SimpleAttributeSet()
        StyleConstants.setFontFamily(regular, Font.MONOSPACED)
        StyleConstants.setFontSize(regular, 12)
        document.insertString(0, item.previewText, regular)

        if (item.previewHighlightLength > 0 && item.previewHighlightStart < item.previewText.length) {
            val highlight = SimpleAttributeSet()
            StyleConstants.setFontFamily(highlight, Font.MONOSPACED)
            StyleConstants.setFontSize(highlight, 12)
            StyleConstants.setBold(highlight, true)
            StyleConstants.setForeground(highlight, java.awt.Color.WHITE)
            StyleConstants.setBackground(highlight, java.awt.Color(88, 118, 169))
            val highlightLength = minOf(item.previewHighlightLength, item.previewText.length - item.previewHighlightStart)
            document.setCharacterAttributes(item.previewHighlightStart, highlightLength, highlight, false)
        }

        previewPane.document = document
        previewPane.caretPosition = maxOf(0, minOf(item.previewHighlightStart, item.previewText.length))
    }

    private fun createUsageCategories(): List<UsageCategory> {
        return listOf(
            UsageCategory("structure", "원소 추가/삭제") { it.kind == "CollectionStructureUsage" },
            UsageCategory("assignment", "컬렉션 대입") { it.kind == "CollectionAssignment" },
            UsageCategory("write", "내용물 수정") { it.kind == "ElementWrite" },
            UsageCategory("reference", "레퍼런스 넘기기") { it.kind == "ElementAlias" || it.kind == "ElementEscape" }
        )
    }

    private data class UsageCategory(
        val id: String,
        val title: String,
        val matches: (CollectionUsageResultItem) -> Boolean
    )

    private sealed class PopupRow {
        data class Header(val title: String, val count: Int) : PopupRow()
        data class Usage(val item: CollectionUsageResultItem) : PopupRow()
    }

    private class CollectionUsagePopupRenderer : ColoredListCellRenderer<PopupRow>() {
        override fun customizeCellRenderer(
            list: JList<out PopupRow>,
            value: PopupRow,
            index: Int,
            selected: Boolean,
            hasFocus: Boolean
        ) {
            when (value) {
                is PopupRow.Header -> {
                    append("${value.title}  ${value.count}", SimpleTextAttributes.REGULAR_BOLD_ATTRIBUTES)
                }
                is PopupRow.Usage -> {
                    append("  (${value.item.line}:${value.item.column}) ", SimpleTextAttributes.GRAY_ATTRIBUTES)
                    append(File(value.item.filePath).name, SimpleTextAttributes.REGULAR_ATTRIBUTES)
                    append("  ", SimpleTextAttributes.REGULAR_ATTRIBUTES)
                    append(value.item.text, SimpleTextAttributes.GRAYED_ATTRIBUTES)
                }
            }
        }
    }
}
