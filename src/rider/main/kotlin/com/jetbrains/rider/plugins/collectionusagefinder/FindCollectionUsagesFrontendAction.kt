package com.jetbrains.rider.plugins.collectionusagefinder

import com.intellij.icons.AllIcons
import com.intellij.openapi.actionSystem.ActionUpdateThread
import com.intellij.openapi.actionSystem.AnAction
import com.intellij.openapi.actionSystem.AnActionEvent
import com.intellij.openapi.actionSystem.CommonDataKeys
import com.intellij.openapi.application.ApplicationManager
import com.intellij.openapi.editor.Editor
import com.intellij.openapi.fileEditor.FileDocumentManager
import com.intellij.openapi.fileEditor.OpenFileDescriptor
import com.intellij.openapi.fileTypes.FileType
import com.intellij.openapi.fileTypes.FileTypeManager
import com.intellij.openapi.project.Project
import com.intellij.openapi.ui.Messages
import com.intellij.openapi.ui.popup.JBPopup
import com.intellij.openapi.ui.popup.JBPopupFactory
import com.intellij.openapi.util.io.FileUtil
import com.intellij.openapi.vfs.LocalFileSystem
import com.intellij.ui.ColoredListCellRenderer
import com.intellij.ui.JBColor
import com.intellij.ui.ScrollPaneFactory
import com.intellij.ui.SimpleTextAttributes
import com.intellij.ui.awt.RelativePoint
import com.intellij.ui.components.JBLabel
import com.intellij.ui.components.JBList
import com.intellij.util.ui.AsyncProcessIcon
import com.intellij.util.ui.JBUI
import com.intellij.psi.PsiManager
import com.intellij.usageView.UsageInfo
import com.intellij.usages.UsageViewPresentation
import com.intellij.usages.impl.UsagePreviewPanel
import com.jetbrains.rd.framework.RdTaskResult
import com.jetbrains.rd.ide.model.CollectionUsageFindRequest
import com.jetbrains.rd.ide.model.CollectionUsageFindResponse
import com.jetbrains.rd.ide.model.CollectionUsageResultItem
import com.jetbrains.rd.ide.model.collectionUsageFinderProtocol
import com.jetbrains.rider.protocol.protocol
import java.awt.BorderLayout
import java.awt.CardLayout
import java.awt.Color
import java.awt.Component
import java.awt.Cursor
import java.awt.Dimension
import java.awt.FlowLayout
import java.awt.Font
import java.awt.Graphics
import java.awt.Graphics2D
import java.awt.Insets
import java.awt.RenderingHints
import java.awt.event.KeyAdapter
import java.awt.event.KeyEvent
import java.awt.event.MouseAdapter
import java.awt.event.MouseEvent
import java.io.File
import javax.swing.Box
import javax.swing.DefaultListModel
import javax.swing.Icon
import javax.swing.JComponent
import javax.swing.JList
import javax.swing.JPanel
import javax.swing.JSplitPane
import javax.swing.JToggleButton
import javax.swing.ListSelectionModel
import javax.swing.SwingConstants

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

        val csharpFileType = FileTypeManager.getInstance().getFileTypeByExtension("cs")
        val popupUi = CollectionUsagePopupUi(project, csharpFileType)
        val popup = JBPopupFactory.getInstance()
            .createComponentPopupBuilder(popupUi, popupUi.focusComponent)
            .setTitle("Collection Usage Finder")
            .setResizable(true)
            .setMovable(true)
            .setRequestFocus(true)
            .setCancelCallback {
                popupUi.release()
                true
            }
            .createPopup()

        popupUi.bindPopup(popup)
        popupUi.showSearching()
        popup.showAtCaret(editor)

        val request = CollectionUsageFindRequest(virtualFile.path, editor.caretModel.offset)
        val protocol = project.protocol
        val task = runCatching {
            protocol.collectionUsageFinderProtocol
                .findCollectionUsages
                .start(request)
        }.getOrElse { error ->
            popupUi.showError(
                "검색 요청 실패",
                error.message ?: error.javaClass.name
            )
            return
        }

        task.result.advise(protocol.lifetime) { taskResult ->
            ApplicationManager.getApplication().invokeLater {
                if (project.isDisposed || popupUi.isReleased) {
                    return@invokeLater
                }

                when (taskResult) {
                    is RdTaskResult.Success -> popupUi.showResponse(taskResult.value)
                    is RdTaskResult.Fault -> popupUi.showError(
                        "backend 검색 실패",
                        taskResult.error.reasonMessage.ifBlank {
                            taskResult.error.message ?: taskResult.error.javaClass.name
                        }
                    )
                    is RdTaskResult.Cancelled -> popupUi.showEmpty(
                        "검색 취소됨",
                        "CollectionUsageFinder backend request was cancelled."
                    )
                }
            }
        }
    }

    private fun JBPopup.showAtCaret(editor: Editor) {
        val caretPoint = editor.visualPositionToXY(editor.caretModel.visualPosition)
        val popupHeight = content.preferredSize.height.takeIf { it > 0 } ?: JBUI.scale(660)
        caretPoint.y -= (popupHeight / 2) - editor.lineHeight
        caretPoint.x += JBUI.scale(8)
        show(RelativePoint(editor.contentComponent, caretPoint))
    }

    private class CollectionUsagePopupUi(
        private val project: Project,
        private val csharpFileType: FileType
    ) : JPanel(BorderLayout()) {
        private val categories = createUsageCategories()
        private val operationFilters = createUsageOperationFilters()
        private val selectedCategoryIds = categories.mapTo(mutableSetOf()) { it.id }
        private val selectedOperationIds = operationFilters.mapTo(mutableSetOf()) { it.id }
        private val collapsedCategoryIds = mutableSetOf<String>()
        private val collapsedOperationIds = mutableSetOf<String>()
        private val cardLayout = CardLayout()
        private val cards = JPanel(cardLayout)
        private val listModel = DefaultListModel<PopupRow>()
        private val list = JBList(listModel)
        private val countLabel = JBLabel()
        private val titleLabel = JBLabel("Collection Usage 검색")
    private val filtersPanel = JPanel(FlowLayout(FlowLayout.LEFT, 3, 0))
    private val operationFiltersPanel = JPanel(FlowLayout(FlowLayout.LEFT, 3, 0))
        private val previewTitle = JBLabel("미리보기")
        private val usagePreview = CollectionUsagePreviewPanel(project)
        private val messageIcon = JBLabel()
        private val messageSpinner = AsyncProcessIcon("CollectionUsageFinder")
        private val messageTitle = JBLabel()
        private val messageDescription = JBLabel()
        private val renderer = CollectionUsagePopupRenderer("", csharpFileType.icon)
        private var popup: JBPopup? = null
        private var response: CollectionUsageFindResponse? = null
        private var released = false

        val focusComponent: JComponent = list

        val isReleased: Boolean
            get() = released

        init {
            background = UsagePopupColors.panelBackground
            preferredSize = Dimension(940, 660)

            configureList()
            cards.background = UsagePopupColors.panelBackground
            cards.add(createMessagePanel(), MESSAGE_CARD)
            cards.add(createResultsPanel(), RESULTS_CARD)

            add(createHeaderPanel(), BorderLayout.NORTH)
            add(cards, BorderLayout.CENTER)
        }

        fun bindPopup(popup: JBPopup) {
            this.popup = popup
            installNavigationHandlers()
        }

        fun showSearching() {
            titleLabel.text = "Collection Usage 검색 · backend 분석 요청 중"
            countLabel.text = ""
            showMessageCard(
                title = "검색 중...",
                description = "대상 컬렉션의 사용 위치를 찾는 중입니다.",
                icon = null,
                spinning = true
            )
        }

        fun showResponse(response: CollectionUsageFindResponse) {
            this.response = response
            titleLabel.text = "'${response.targetName.ifBlank { "컬렉션" }}' 사용 위치 · " +
                "${response.collectionKind.ifBlank { "C# collection" }} · ${formatScope(response.scope)}"
            renderer.targetName = response.targetName

            if (!response.success) {
                countLabel.text = ""
                showMessageCard(
                    title = "검색할 수 없음",
                    description = response.message.ifBlank { "지원하지 않는 컬렉션 대상입니다." },
                    icon = AllIcons.General.Information,
                    spinning = false
                )
                return
            }

            if (response.items.isEmpty()) {
                countLabel.text = "0개의 사용 위치"
                showMessageCard(
                    title = "검색 결과 없음",
                    description = response.message.ifBlank {
                        "'${response.targetName}'에 대한 컬렉션 전용 사용 위치를 찾지 못했습니다."
                    },
                    icon = AllIcons.General.Information,
                    spinning = false
                )
                return
            }

            selectedCategoryIds.clear()
            selectedCategoryIds += categories.map { it.id }
            selectedOperationIds.clear()
            selectedOperationIds += operationFilters.map { it.id }
            collapsedCategoryIds.clear()
            collapsedOperationIds.clear()
            rebuildFilters(response)
            refreshRows()
            cardLayout.show(cards, RESULTS_CARD)
        }

        fun showEmpty(title: String, description: String) {
            countLabel.text = ""
            showMessageCard(title, description, AllIcons.General.Information, spinning = false)
        }

        fun showError(title: String, description: String) {
            countLabel.text = ""
            showMessageCard(title, description, AllIcons.General.Error, spinning = false)
        }

        fun release() {
            if (released) {
                return
            }

            released = true
            usagePreview.release()
        }

        private fun configureList() {
            list.visibleRowCount = 14
            list.selectionMode = ListSelectionModel.SINGLE_SELECTION
            list.cellRenderer = renderer
            list.background = UsagePopupColors.listBackground
            list.selectionBackground = UsagePopupColors.selectionBackground
            list.selectionForeground = UsagePopupColors.selectedForeground
            list.fixedCellHeight = -1
        }

        private fun createHeaderPanel(): JPanel {
            titleLabel.font = titleLabel.font.deriveFont(Font.BOLD, titleLabel.font.size2D)
            titleLabel.foreground = UsagePopupColors.primaryForeground

            countLabel.foreground = UsagePopupColors.mutedForeground
            countLabel.font = countLabel.font.deriveFont(Font.PLAIN, countLabel.font.size2D - 1f)

            val headerPanel = JPanel(BorderLayout())
            headerPanel.background = UsagePopupColors.panelBackground
            headerPanel.border = JBUI.Borders.empty(6, 12, 5, 12)
            headerPanel.add(titleLabel, BorderLayout.WEST)
            headerPanel.add(countLabel, BorderLayout.EAST)
            return headerPanel
        }

        private fun createMessagePanel(): JPanel {
            val content = Box.createVerticalBox()
            content.alignmentX = Component.CENTER_ALIGNMENT

            messageSpinner.alignmentX = Component.CENTER_ALIGNMENT
            messageIcon.alignmentX = Component.CENTER_ALIGNMENT
            messageTitle.alignmentX = Component.CENTER_ALIGNMENT
            messageDescription.alignmentX = Component.CENTER_ALIGNMENT
            messageTitle.horizontalAlignment = SwingConstants.CENTER
            messageDescription.horizontalAlignment = SwingConstants.CENTER
            messageTitle.font = messageTitle.font.deriveFont(Font.BOLD, messageTitle.font.size2D + 1f)
            messageTitle.foreground = UsagePopupColors.primaryForeground
            messageDescription.foreground = UsagePopupColors.mutedForeground

            content.add(messageSpinner)
            content.add(messageIcon)
            content.add(Box.createVerticalStrut(JBUI.scale(12)))
            content.add(messageTitle)
            content.add(Box.createVerticalStrut(JBUI.scale(6)))
            content.add(messageDescription)

            val panel = JPanel(BorderLayout())
            panel.background = UsagePopupColors.listBackground
            panel.border = JBUI.Borders.empty(48, 32, 48, 32)
            panel.add(content, BorderLayout.CENTER)
            return panel
        }

        private fun createResultsPanel(): JPanel {
            filtersPanel.background = UsagePopupColors.panelBackground
            filtersPanel.border = JBUI.Borders.empty()
            operationFiltersPanel.background = UsagePopupColors.panelBackground
            operationFiltersPanel.border = JBUI.Borders.empty()

            val filtersStack = JPanel(BorderLayout())
            filtersStack.background = UsagePopupColors.panelBackground
            filtersStack.border = JBUI.Borders.empty(0, 6, 1, 6)
            filtersStack.add(createFilterSection("유형", filtersPanel, prominent = true), BorderLayout.NORTH)
            filtersStack.add(createFilterSection("상세", operationFiltersPanel, prominent = false), BorderLayout.CENTER)

            val topPanel = JPanel(BorderLayout())
            topPanel.background = UsagePopupColors.panelBackground
            topPanel.add(filtersStack, BorderLayout.CENTER)

            val resultsPanel = JPanel(BorderLayout())
            resultsPanel.background = UsagePopupColors.panelBackground
            resultsPanel.add(topPanel, BorderLayout.NORTH)
            val resultsScrollPane = ScrollPaneFactory.createScrollPane(list)
            resultsScrollPane.border = JBUI.Borders.customLine(UsagePopupColors.border, 1, 0, 0, 0)
            resultsScrollPane.viewport.background = UsagePopupColors.listBackground
            resultsPanel.add(resultsScrollPane, BorderLayout.CENTER)

            val previewPanel = JPanel(BorderLayout())
            previewPanel.background = UsagePopupColors.previewHeaderBackground
            previewPanel.border = JBUI.Borders.customLine(UsagePopupColors.border, 1, 0, 0, 0)
            previewTitle.foreground = UsagePopupColors.primaryForeground
            previewTitle.font = previewTitle.font.deriveFont(Font.BOLD)
            previewTitle.border = JBUI.Borders.empty(7, 12, 5, 12)
            previewPanel.add(previewTitle, BorderLayout.NORTH)
            previewPanel.add(usagePreview, BorderLayout.CENTER)

            val splitter = JSplitPane(JSplitPane.VERTICAL_SPLIT, resultsPanel, previewPanel)
            splitter.resizeWeight = 0.56
            splitter.dividerSize = JBUI.scale(4)
            splitter.border = JBUI.Borders.empty()
            splitter.background = UsagePopupColors.panelBackground

            val panel = JPanel(BorderLayout())
            panel.background = UsagePopupColors.panelBackground
            panel.add(splitter, BorderLayout.CENTER)
            return panel
        }

        private fun createFilterSection(title: String, contentPanel: JPanel, prominent: Boolean): JPanel {
            val titleLabel = JBLabel(title)
            titleLabel.foreground = if (prominent) UsagePopupColors.primaryForeground else UsagePopupColors.mutedForeground
            titleLabel.font = titleLabel.font.deriveFont(
                if (prominent) Font.BOLD else Font.PLAIN,
                titleLabel.font.size2D + if (prominent) -0.5f else -1.5f
            )
            titleLabel.border = JBUI.Borders.empty(0, 0, 0, 4)

            val section = JPanel(BorderLayout())
            section.background = if (prominent) UsagePopupColors.panelBackground else UsagePopupColors.detailFilterBackground
            section.border = if (prominent) {
                JBUI.Borders.empty(0, 0, 0, 0)
            } else {
                JBUI.Borders.compound(
                    JBUI.Borders.customLine(UsagePopupColors.border, 1, 0, 0, 0),
                    JBUI.Borders.empty(2, 0, 0, 0)
                )
            }

            contentPanel.background = section.background
            section.add(titleLabel, BorderLayout.WEST)
            section.add(contentPanel, BorderLayout.CENTER)
            return section
        }

        private fun showMessageCard(title: String, description: String, icon: Icon?, spinning: Boolean) {
            filtersPanel.removeAll()
            operationFiltersPanel.removeAll()
            listModel.clear()
            usagePreview.clear()

            messageTitle.text = title
            messageDescription.text = "<html><div style='width: 540px; text-align: center;'>${escapeHtml(description)}</div></html>"
            messageSpinner.isVisible = spinning
            messageIcon.icon = icon
            messageIcon.isVisible = !spinning && icon != null
            cardLayout.show(cards, MESSAGE_CARD)
        }

        private fun rebuildFilters(response: CollectionUsageFindResponse) {
            filtersPanel.removeAll()
            for (category in categories) {
                val count = response.items.count(category.matches)
                val chip = FilterChipButton("${category.title}  $count", category.color, prominent = true)
                chip.icon = category.icon
                chip.isSelected = true
                chip.isEnabled = count > 0
                chip.addActionListener {
                    if (chip.isSelected) {
                        selectedCategoryIds += category.id
                    } else {
                        selectedCategoryIds -= category.id
                    }
                    refreshRows()
                }
                filtersPanel.add(chip)
            }

            filtersPanel.revalidate()
            filtersPanel.repaint()

            operationFiltersPanel.removeAll()
            for (operationFilter in operationFilters) {
                val count = response.items.count(operationFilter.matches)
                if (count == 0) {
                    continue
                }

                val chip = FilterChipButton("${operationFilter.title} $count", operationFilter.color, prominent = false)
                chip.icon = operationFilter.icon
                chip.isSelected = true
                chip.addActionListener {
                    if (chip.isSelected) {
                        selectedOperationIds += operationFilter.id
                    } else {
                        selectedOperationIds -= operationFilter.id
                    }
                    refreshRows()
                }
                operationFiltersPanel.add(chip)
            }

            operationFiltersPanel.revalidate()
            operationFiltersPanel.repaint()
        }

        private fun refreshRows() {
            val currentResponse = response ?: return
            val visibleItems = currentResponse.items.filter { item ->
                isSelectedCategory(item) && isSelectedOperation(item)
            }
            val rows = createRows(visibleItems, categories.filter { it.id in selectedCategoryIds })
            setRows(listModel, rows)
            countLabel.text = if (visibleItems.size == currentResponse.items.size) {
                "${currentResponse.items.size}개의 사용 위치"
            } else {
                "${visibleItems.size} / ${currentResponse.items.size}개의 사용 위치"
            }
            selectFirstUsageRow()
            updatePreview(list.selectedValue as? PopupRow.Usage)
        }

        private fun isSelectedCategory(item: CollectionUsageResultItem): Boolean {
            return categories.any { category -> category.id in selectedCategoryIds && category.matches(item) }
        }

        private fun isSelectedOperation(item: CollectionUsageResultItem): Boolean {
            val matchingOperationFilters = operationFilters.filter { it.matches(item) }
            if (matchingOperationFilters.isEmpty()) {
                return true
            }

            return matchingOperationFilters.any { it.id in selectedOperationIds }
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

                val categoryCollapsed = category.id in collapsedCategoryIds
                rows += PopupRow.CategoryHeader(category, groupItems.size, categoryCollapsed)
                consumed += groupItems
                if (categoryCollapsed) {
                    continue
                }

                val operationConsumed = mutableSetOf<CollectionUsageResultItem>()
                for (operationFilter in operationFilters.filter { it.id in selectedOperationIds }) {
                    val operationItems = groupItems.filter(operationFilter.matches)
                    if (operationItems.isEmpty()) {
                        continue
                    }

                    val operationId = createOperationCollapseId(category.id, operationFilter.id)
                    val operationCollapsed = operationId in collapsedOperationIds
                    rows += PopupRow.OperationHeader(
                        operationId,
                        operationFilter.title,
                        operationItems.size,
                        operationFilter.color,
                        operationFilter.icon,
                        operationCollapsed
                    )
                    operationConsumed += operationItems
                    if (!operationCollapsed) {
                        rows += operationItems.map { PopupRow.Usage(it) }
                    }
                }

                val otherOperationItems = groupItems.filterNot(operationConsumed::contains)
                if (otherOperationItems.isNotEmpty()) {
                    val operationId = createOperationCollapseId(category.id, "other")
                    val operationCollapsed = operationId in collapsedOperationIds
                    rows += PopupRow.OperationHeader(
                        operationId,
                        "기타",
                        otherOperationItems.size,
                        UsagePopupColors.mutedForeground,
                        null,
                        operationCollapsed
                    )
                    if (!operationCollapsed) {
                        rows += otherOperationItems.map { PopupRow.Usage(it) }
                    }
                }
            }

            val otherItems = items.filterNot(consumed::contains)
            if (otherItems.isNotEmpty()) {
                rows += PopupRow.CategoryHeader(
                    UsageCategory("other", "기타", UsagePopupColors.mutedForeground, AllIcons.General.Information) { false },
                    otherItems.size,
                    collapsed = false
                )
                rows += otherItems.map { PopupRow.Usage(it) }
            }

            return rows
        }

        private fun createOperationCollapseId(categoryId: String, operationId: String): String {
            return "$categoryId:$operationId"
        }

        private fun setRows(model: DefaultListModel<PopupRow>, rows: List<PopupRow>) {
            model.clear()
            rows.forEach(model::addElement)
        }

        private fun installNavigationHandlers() {
            fun activateRow(row: PopupRow?) {
                when (row) {
                    is PopupRow.CategoryHeader -> toggleCategory(row.category.id)
                    is PopupRow.OperationHeader -> toggleOperation(row.id)
                    is PopupRow.Usage -> navigateToUsage(row.item)
                    null -> return
                }
            }

            list.addMouseListener(object : MouseAdapter() {
                override fun mouseClicked(e: MouseEvent) {
                    val index = list.locationToIndex(e.point)
                    if (index < 0) {
                        return
                    }

                    val row = list.model.getElementAt(index)
                    if (row is PopupRow.CategoryHeader || row is PopupRow.OperationHeader) {
                        activateRow(row)
                        e.consume()
                        return
                    }

                    if (e.clickCount == 2 && row is PopupRow.Usage) {
                        activateRow(row)
                    }
                }
            })
            list.addKeyListener(object : KeyAdapter() {
                override fun keyPressed(e: KeyEvent) {
                    if (e.keyCode == KeyEvent.VK_ENTER) {
                        activateRow(list.selectedValue)
                        e.consume()
                    }
                }
            })
            list.addListSelectionListener { event ->
                if (!event.valueIsAdjusting) {
                    updatePreview(list.selectedValue as? PopupRow.Usage)
                }
            }
        }

        private fun toggleCategory(categoryId: String) {
            if (categoryId in collapsedCategoryIds) {
                collapsedCategoryIds -= categoryId
            } else {
                collapsedCategoryIds += categoryId
            }
            refreshRows()
        }

        private fun toggleOperation(operationId: String) {
            if (operationId in collapsedOperationIds) {
                collapsedOperationIds -= operationId
            } else {
                collapsedOperationIds += operationId
            }
            refreshRows()
        }

        private fun navigateToUsage(item: CollectionUsageResultItem) {
            val filePath = FileUtil.toSystemIndependentName(item.filePath)
            val virtualFile = LocalFileSystem.getInstance().findFileByPath(filePath)
            if (virtualFile == null) {
                showError("파일을 열 수 없음", item.filePath)
                return
            }

            popup?.cancel()
            OpenFileDescriptor(project, virtualFile, maxOf(0, item.startOffset)).navigate(true)
        }

        private fun selectFirstUsageRow() {
            val index = (0 until list.model.size).firstOrNull { list.model.getElementAt(it) is PopupRow.Usage } ?: -1
            list.selectedIndex = index
        }

        private fun updatePreview(row: PopupRow.Usage?) {
            if (row == null) {
                previewTitle.text = "미리보기"
                usagePreview.clear()
                return
            }

            val item = row.item
            previewTitle.text = "${File(item.filePath).name} · ${item.line}줄 · ${item.operationDisplayName}"
            usagePreview.updateUsage(item)
        }
    }

    private data class UsageCategory(
        val id: String,
        val title: String,
        val color: Color,
        val icon: Icon,
        val matches: (CollectionUsageResultItem) -> Boolean
    )

    private data class UsageOperationFilter(
        val id: String,
        val title: String,
        val color: Color,
        val icon: Icon,
        val matches: (CollectionUsageResultItem) -> Boolean
    )

    private sealed class PopupRow {
        data class CategoryHeader(
            val category: UsageCategory,
            val count: Int,
            val collapsed: Boolean
        ) : PopupRow()

        data class OperationHeader(
            val id: String,
            val title: String,
            val count: Int,
            val color: Color,
            val icon: Icon?,
            val collapsed: Boolean
        ) : PopupRow()

        data class Usage(val item: CollectionUsageResultItem) : PopupRow()
    }

    private object UsagePopupColors {
        val panelBackground: Color = JBColor(Color(0xF4F6F8), Color(0x24272E))
        val previewHeaderBackground: Color = JBColor(Color(0xEEF2F5), Color(0x2B2F36))
        val detailFilterBackground: Color = JBColor(Color(0xEEF2F5), Color(0x272B32))
        val listBackground: Color = JBColor(Color(0xFAFBFC), Color(0x25282E))
        val gutterBackground: Color = JBColor(Color(0xF1F4F7), Color(0x24272E))
        val border: Color = JBColor(Color(0xD7DDE3), Color(0x3C424B))
        val primaryForeground: Color = JBColor(Color(0x1F2328), Color(0xD9E0EA))
        val selectedForeground: Color = JBColor(Color.WHITE, Color(0xF4F8FF))
        val mutedForeground: Color = JBColor(Color(0x68717D), Color(0x8F98A6))
        val codeForeground: Color = JBColor(Color(0x3E4652), Color(0xB7C0CC))
        val selectionBackground: Color = JBColor(Color(0xD8E8FF), Color(0x2F4D7A))
        val highlightBackground: Color = JBColor(Color(0xFFE7A3), Color(0x405C88))
        val neutralChipFill: Color = JBColor(Color(0xEEF2F5), Color(0x323740))
        val neutralChipOutline: Color = JBColor(Color(0xB9C3CE), Color(0x6B7480))
        val structureUsage: Color = JBColor(Color(0x227A46), Color(0x72D39B))
        val assignmentUsage: Color = JBColor(Color(0x9A6512), Color(0xE4B363))
        val elementWrite: Color = JBColor(Color(0xB13B3B), Color(0xF08A8A))
        val referenceUsage: Color = JBColor(Color(0x2F65B0), Color(0x8BB8FF))
        val removeOperation: Color = JBColor(Color(0xA0442C), Color(0xF0A06A))
        val clearOperation: Color = JBColor(Color(0xB13B3B), Color(0xFF8F8F))
        val setOperation: Color = JBColor(Color(0x7B5BB8), Color(0xC2A4FF))
        val reorderOperation: Color = JBColor(Color(0x247A8A), Color(0x76D6E8))
        val setMathOperation: Color = JBColor(Color(0x3569A8), Color(0x93C5FD))

        fun colorForKind(kind: String): Color {
            return when (kind) {
                "CollectionStructureUsage" -> structureUsage
                "CollectionAssignment" -> assignmentUsage
                "ElementWrite" -> elementWrite
                "ElementAlias", "ElementEscape" -> referenceUsage
                else -> mutedForeground
            }
        }

        fun highlightForKind(kind: String): Color {
            return when (kind) {
                "CollectionStructureUsage" -> JBColor(Color(0xB7F1CD), Color(0x295A3B))
                "CollectionAssignment" -> JBColor(Color(0xFFE2A3), Color(0x604A24))
                "ElementWrite" -> JBColor(Color(0xFFD2D2), Color(0x663638))
                "ElementAlias", "ElementEscape" -> JBColor(Color(0xCFE2FF), Color(0x334F78))
                else -> highlightBackground
            }
        }
    }

    private class FilterChipButton(
        text: String,
        private val accentColor: Color,
        private val prominent: Boolean
    ) : JToggleButton(text) {
        init {
            isOpaque = false
            isContentAreaFilled = false
            isBorderPainted = false
            isFocusPainted = false
            margin = Insets(0, 0, 0, 0)
            iconTextGap = JBUI.scale(2)
            cursor = Cursor.getPredefinedCursor(Cursor.HAND_CURSOR)
            border = JBUI.Borders.empty(1, if (prominent) 4 else 3, 1, if (prominent) 4 else 3)
            foreground = if (prominent) UsagePopupColors.primaryForeground else UsagePopupColors.mutedForeground
            font = font.deriveFont(
                if (prominent) Font.BOLD else Font.PLAIN,
                font.size2D + if (prominent) -0.5f else -1.5f
            )
        }

        override fun paintComponent(g: Graphics) {
            val graphics = g.create() as Graphics2D
            graphics.setRenderingHint(RenderingHints.KEY_ANTIALIASING, RenderingHints.VALUE_ANTIALIAS_ON)

            val radius = JBUI.scale(if (prominent) 8 else 7)
            val fill = when {
                !isEnabled -> transparent(UsagePopupColors.border, 38)
                isSelected && prominent -> UsagePopupColors.neutralChipFill
                isSelected -> transparent(accentColor, 34)
                model.isRollover -> transparent(UsagePopupColors.border, 42)
                prominent -> transparent(UsagePopupColors.border, 24)
                else -> transparent(UsagePopupColors.border, 14)
            }
            val outline = when {
                !isEnabled -> transparent(UsagePopupColors.border, 70)
                isSelected && prominent -> UsagePopupColors.neutralChipOutline
                isSelected -> transparent(accentColor, 145)
                else -> transparent(UsagePopupColors.border, 95)
            }

            graphics.color = fill
            graphics.fillRoundRect(0, 0, width - 1, height - 1, radius, radius)
            graphics.color = outline
            graphics.drawRoundRect(0, 0, width - 1, height - 1, radius, radius)
            graphics.dispose()

            foreground = when {
                !isEnabled -> UsagePopupColors.mutedForeground
                isSelected && !prominent -> UsagePopupColors.primaryForeground
                isSelected && prominent -> UsagePopupColors.primaryForeground
                else -> UsagePopupColors.mutedForeground
            }
            super.paintComponent(g)
        }

        override fun setIcon(defaultIcon: Icon?) {
            super.setIcon(defaultIcon?.let { CompactFilterIcon(it, this) })
        }

        override fun getPreferredSize(): Dimension {
            val size = super.getPreferredSize()
            val iconHeight = icon?.iconHeight ?: 0
            val contentHeight = maxOf(getFontMetrics(font).height, iconHeight)
            size.height = contentHeight + JBUI.scale(if (prominent) 4 else 3)
            return size
        }

        private fun transparent(color: Color, alpha: Int): Color {
            return Color(color.red, color.green, color.blue, alpha)
        }
    }

    private class CompactFilterIcon(
        private val delegate: Icon,
        private val owner: JComponent
    ) : Icon {
        override fun getIconWidth(): Int {
            return targetSize()
        }

        override fun getIconHeight(): Int {
            return targetSize()
        }

        override fun paintIcon(component: Component?, graphics: Graphics, x: Int, y: Int) {
            val sourceWidth = maxOf(delegate.iconWidth, 1)
            val sourceHeight = maxOf(delegate.iconHeight, 1)
            val targetSize = targetSize()
            val scale = targetSize.toDouble() / maxOf(sourceWidth, sourceHeight)
            val targetWidth = (sourceWidth * scale).toInt()
            val targetHeight = (sourceHeight * scale).toInt()
            val scaledGraphics = graphics.create() as Graphics2D

            scaledGraphics.setRenderingHint(RenderingHints.KEY_INTERPOLATION, RenderingHints.VALUE_INTERPOLATION_BILINEAR)
            scaledGraphics.translate(x + (targetSize - targetWidth) / 2, y + (targetSize - targetHeight) / 2)
            scaledGraphics.scale(scale, scale)
            delegate.paintIcon(component, scaledGraphics, 0, 0)
            scaledGraphics.dispose()
        }

        private fun targetSize(): Int {
            val textHeight = owner.getFontMetrics(owner.font).height
            return maxOf(JBUI.scale(10), textHeight - JBUI.scale(2))
        }
    }

    private class CollectionUsagePreviewPanel(
        private val project: Project
    ) : JPanel(BorderLayout()) {
        private val presentation = UsageViewPresentation().apply {
            setCodeUsages(true)
            setUsagesString("usage")
            setTabText("Collection Usage Preview")
        }
        private val panel = UsagePreviewPanel(project, presentation)
        private var released = false

        init {
            background = UsagePopupColors.listBackground
            border = JBUI.Borders.empty()
            add(panel.createComponent(), BorderLayout.CENTER)
        }

        fun clear() {
            panel.updateLayout(emptyList())
        }

        fun updateUsage(item: CollectionUsageResultItem) {
            val usageInfo = createUsageInfo(item)
            if (usageInfo == null) {
                clear()
                return
            }

            panel.updateLayout(listOf(usageInfo))
        }

        private fun createUsageInfo(item: CollectionUsageResultItem): UsageInfo? {
            return ApplicationManager.getApplication().runReadAction<UsageInfo?> {
                val filePath = FileUtil.toSystemIndependentName(item.filePath)
                val virtualFile = LocalFileSystem.getInstance().findFileByPath(filePath)
                    ?: LocalFileSystem.getInstance().refreshAndFindFileByPath(filePath)
                    ?: return@runReadAction null

                val psiFile = PsiManager.getInstance(project).findFile(virtualFile)
                    ?: return@runReadAction null
                val textLength = psiFile.textLength
                val start = item.startOffset.coerceIn(0, textLength)
                val requestedEnd = item.startOffset + maxOf(item.length, 1)
                val end = requestedEnd.coerceIn(start, textLength).let {
                    if (it == start && start < textLength) start + 1 else it
                }

                UsageInfo(psiFile, start, end)
            }
        }

        fun release() {
            if (released) {
                return
            }

            released = true
            panel.dispose()
        }
    }

    private class CollectionUsagePopupRenderer(
        var targetName: String,
        private val fileIcon: Icon?
    ) : ColoredListCellRenderer<PopupRow>() {
        override fun customizeCellRenderer(
            list: JList<out PopupRow>,
            value: PopupRow,
            index: Int,
            selected: Boolean,
            hasFocus: Boolean
        ) {
            iconTextGap = JBUI.scale(3)
            background = when {
                selected -> list.selectionBackground
                value is PopupRow.CategoryHeader -> UsagePopupColors.previewHeaderBackground
                value is PopupRow.OperationHeader -> UsagePopupColors.detailFilterBackground
                else -> UsagePopupColors.listBackground
            }

            when (value) {
                is PopupRow.CategoryHeader -> {
                    icon = null
                    border = JBUI.Borders.empty(6, 6, 4, 10)
                    val primary = if (selected) list.selectionForeground else UsagePopupColors.primaryForeground
                    val muted = if (selected) list.selectionForeground else UsagePopupColors.mutedForeground
                    append(collapseMarker(value.collapsed) + " ", SimpleTextAttributes(SimpleTextAttributes.STYLE_BOLD, muted))
                    append(
                        "${value.category.title}  ${value.count}",
                        SimpleTextAttributes(SimpleTextAttributes.STYLE_BOLD, primary)
                    )
                    if (value.collapsed) {
                        append("  접힘", SimpleTextAttributes(SimpleTextAttributes.STYLE_ITALIC, muted))
                    }
                }
                is PopupRow.OperationHeader -> {
                    icon = null
                    border = JBUI.Borders.empty(3, 28, 2, 10)
                    val muted = if (selected) list.selectionForeground else UsagePopupColors.mutedForeground
                    val accent = if (selected) list.selectionForeground else value.color
                    append(collapseMarker(value.collapsed) + " ", SimpleTextAttributes(SimpleTextAttributes.STYLE_PLAIN, muted))
                    append(
                        "${value.title}  ${value.count}",
                        SimpleTextAttributes(SimpleTextAttributes.STYLE_BOLD, accent)
                    )
                    if (value.collapsed) {
                        append("  접힘", SimpleTextAttributes(SimpleTextAttributes.STYLE_ITALIC, muted))
                    }
                }
                is PopupRow.Usage -> {
                    border = JBUI.Borders.empty(2, 44, 2, 10)
                    icon = fileIcon

                    val primary = if (selected) list.selectionForeground else UsagePopupColors.primaryForeground
                    val muted = if (selected) list.selectionForeground else UsagePopupColors.mutedForeground
                    val code = if (selected) list.selectionForeground else UsagePopupColors.codeForeground

                    append(File(value.item.filePath).name, SimpleTextAttributes(SimpleTextAttributes.STYLE_PLAIN, primary))
                    append(" ", SimpleTextAttributes(SimpleTextAttributes.STYLE_PLAIN, muted))
                    append("(${value.item.line}:${value.item.column})", SimpleTextAttributes(SimpleTextAttributes.STYLE_ITALIC, muted))
                    append("  ", SimpleTextAttributes(SimpleTextAttributes.STYLE_PLAIN, muted))
                    appendHighlightedCode(
                        value.item.text,
                        SimpleTextAttributes(SimpleTextAttributes.STYLE_PLAIN, code),
                        SimpleTextAttributes(SimpleTextAttributes.STYLE_BOLD, if (selected) primary else UsagePopupColors.colorForKind(value.item.kind))
                    )
                }
            }
        }

        private fun collapseMarker(collapsed: Boolean): String {
            return if (collapsed) "▸" else "▾"
        }

        private fun appendHighlightedCode(
            text: String,
            regularAttributes: SimpleTextAttributes,
            targetAttributes: SimpleTextAttributes
        ) {
            val targetIndex = if (targetName.isNotEmpty()) text.indexOf(targetName) else -1
            if (targetIndex < 0) {
                append(text, regularAttributes)
                return
            }

            if (targetIndex > 0) {
                append(text.substring(0, targetIndex), regularAttributes)
            }

            append(text.substring(targetIndex, targetIndex + targetName.length), targetAttributes)

            val afterTarget = targetIndex + targetName.length
            if (afterTarget < text.length) {
                append(text.substring(afterTarget), regularAttributes)
            }
        }
    }

    companion object {
        private const val MESSAGE_CARD = "message"
        private const val RESULTS_CARD = "results"

        private fun createUsageCategories(): List<UsageCategory> {
            return listOf(
                UsageCategory("structure", "원소 추가/삭제", UsagePopupColors.structureUsage, AllIcons.General.Add) {
                    it.kind == "CollectionStructureUsage"
                },
                UsageCategory("assignment", "컬렉션 대입", UsagePopupColors.assignmentUsage, AllIcons.Actions.Replace) {
                    it.kind == "CollectionAssignment"
                },
                UsageCategory("write", "내용물 수정", UsagePopupColors.elementWrite, AllIcons.Actions.Edit) {
                    it.kind == "ElementWrite"
                },
                UsageCategory("reference", "레퍼런스 넘기기", UsagePopupColors.referenceUsage, AllIcons.Actions.Forward) {
                    it.kind == "ElementAlias" || it.kind == "ElementEscape"
                }
            )
        }

        private fun createUsageOperationFilters(): List<UsageOperationFilter> {
            return listOf(
                UsageOperationFilter("element-add", "원소 추가", UsagePopupColors.structureUsage, AllIcons.General.Add) {
                    it.operationKind == "ElementAdd"
                },
                UsageOperationFilter("element-remove", "원소 삭제", UsagePopupColors.removeOperation, AllIcons.General.Remove) {
                    it.operationKind == "ElementRemove"
                },
                UsageOperationFilter("element-clear", "전체 삭제", UsagePopupColors.clearOperation, AllIcons.General.Remove) {
                    it.operationKind == "ElementClear"
                },
                UsageOperationFilter("element-set", "인덱서 설정/교체", UsagePopupColors.setOperation, AllIcons.Actions.Replace) {
                    it.operationKind == "ElementSet"
                },
                UsageOperationFilter("collection-reorder", "순서 변경", UsagePopupColors.reorderOperation, AllIcons.Actions.Replace) {
                    it.operationKind == "CollectionReorder"
                },
                UsageOperationFilter("set-operation", "집합 연산", UsagePopupColors.setMathOperation, AllIcons.Actions.Replace) {
                    it.operationKind == "SetOperation"
                },
                UsageOperationFilter("collection-assignment", "컬렉션 대입", UsagePopupColors.assignmentUsage, AllIcons.Actions.Replace) {
                    it.operationKind == "CollectionAssignment"
                },
                UsageOperationFilter("element-content-write", "내용물 수정", UsagePopupColors.elementWrite, AllIcons.Actions.Edit) {
                    it.operationKind == "ElementContentWrite"
                },
                UsageOperationFilter("element-reference", "레퍼런스 넘기기", UsagePopupColors.referenceUsage, AllIcons.Actions.Forward) {
                    it.operationKind == "ElementReference"
                }
            )
        }

        private fun iconForKind(kind: String): Icon? {
            return when (kind) {
                "CollectionStructureUsage" -> AllIcons.General.Add
                "CollectionAssignment" -> AllIcons.Actions.Replace
                "ElementWrite" -> AllIcons.Actions.Edit
                "ElementAlias", "ElementEscape" -> AllIcons.Actions.Forward
                else -> null
            }
        }

        private fun formatScope(scope: String): String {
            return when (scope) {
                "CurrentFile" -> "현재 파일"
                "DeclaringProject" -> "선언 프로젝트"
                else -> scope
            }
        }

        private fun escapeHtml(text: String): String {
            return text
                .replace("&", "&amp;")
                .replace("<", "&lt;")
                .replace(">", "&gt;")
                .replace("\"", "&quot;")
                .replace("\n", "<br>")
        }
    }
}
