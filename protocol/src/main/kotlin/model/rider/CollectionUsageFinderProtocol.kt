package model.rider

import com.jetbrains.rd.generator.nova.Ext
import com.jetbrains.rd.generator.nova.ImmutableListOfScalars
import com.jetbrains.rd.generator.nova.Member
import com.jetbrains.rd.generator.nova.PredefinedType
import com.jetbrains.rider.model.nova.ide.IdeRoot

@Suppress("unused")
object CollectionUsageFinderProtocol : Ext(IdeRoot) {
    private val findCollectionUsagesRequest = structdef("CollectionUsageFindRequest") {
        append(Member.Field("filePath", PredefinedType.string))
        append(Member.Field("caretOffset", PredefinedType.int))
    }

    private val collectionUsageResultItem = structdef("CollectionUsageResultItem") {
        append(Member.Field("filePath", PredefinedType.string))
        append(Member.Field("startOffset", PredefinedType.int))
        append(Member.Field("length", PredefinedType.int))
        append(Member.Field("line", PredefinedType.int))
        append(Member.Field("column", PredefinedType.int))
        append(Member.Field("kind", PredefinedType.string))
        append(Member.Field("kindDisplayName", PredefinedType.string))
        append(Member.Field("operationKind", PredefinedType.string))
        append(Member.Field("operationDisplayName", PredefinedType.string))
        append(Member.Field("operationName", PredefinedType.string))
        append(Member.Field("text", PredefinedType.string))
        append(Member.Field("previewText", PredefinedType.string))
        append(Member.Field("previewStartLine", PredefinedType.int))
        append(Member.Field("previewHighlightStart", PredefinedType.int))
        append(Member.Field("previewHighlightLength", PredefinedType.int))
    }

    private val findCollectionUsagesResponse = structdef("CollectionUsageFindResponse") {
        append(Member.Field("success", PredefinedType.bool))
        append(Member.Field("message", PredefinedType.string))
        append(Member.Field("targetName", PredefinedType.string))
        append(Member.Field("collectionKind", PredefinedType.string))
        append(Member.Field("scope", PredefinedType.string))
        append(Member.Field("items", ImmutableListOfScalars(collectionUsageResultItem)))
    }

    init {
        append(
            Member.Reactive.Task(
                "findCollectionUsages",
                findCollectionUsagesRequest,
                findCollectionUsagesResponse
            )
        )
    }
}
