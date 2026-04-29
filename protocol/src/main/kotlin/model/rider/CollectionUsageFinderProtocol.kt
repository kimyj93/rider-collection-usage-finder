package model.rider

import com.jetbrains.rd.generator.nova.Ext
import com.jetbrains.rd.generator.nova.Member
import com.jetbrains.rd.generator.nova.PredefinedType
import com.jetbrains.rider.model.nova.ide.IdeRoot

@Suppress("unused")
object CollectionUsageFinderProtocol : Ext(IdeRoot) {
    private val findCollectionUsagesRequest = structdef("CollectionUsageFindRequest") {
        append(Member.Field("filePath", PredefinedType.string))
        append(Member.Field("caretOffset", PredefinedType.int))
    }

    private val findCollectionUsagesResponse = structdef("CollectionUsageFindResponse") {
        append(Member.Field("success", PredefinedType.bool))
        append(Member.Field("message", PredefinedType.string))
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
