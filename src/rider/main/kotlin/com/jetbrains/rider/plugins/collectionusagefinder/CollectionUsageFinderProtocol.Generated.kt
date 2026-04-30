@file:Suppress("EXPERIMENTAL_API_USAGE","EXPERIMENTAL_UNSIGNED_LITERALS","PackageDirectoryMismatch","UnusedImport","unused","LocalVariableName","CanBeVal","PropertyName","EnumEntryName","ClassName","ObjectPropertyName","UnnecessaryVariable","SpellCheckingInspection")
package com.jetbrains.rd.ide.model

import com.jetbrains.rd.framework.*
import com.jetbrains.rd.framework.base.*
import com.jetbrains.rd.framework.impl.*

import com.jetbrains.rd.util.lifetime.*
import com.jetbrains.rd.util.reactive.*
import com.jetbrains.rd.util.string.*
import com.jetbrains.rd.util.*
import kotlin.time.Duration
import kotlin.reflect.KClass
import kotlin.jvm.JvmStatic



/**
 * #### Generated from [CollectionUsageFinderProtocol.kt:10]
 */
class CollectionUsageFinderProtocol private constructor(
    private val _findCollectionUsages: RdCall<CollectionUsageFindRequest, CollectionUsageFindResponse>
) : RdExtBase() {
    //companion

    companion object : ISerializersOwner {

        override fun registerSerializersCore(serializers: ISerializers)  {
            val classLoader = javaClass.classLoader
            serializers.register(LazyCompanionMarshaller(RdId(-2015687819842928794), classLoader, "com.jetbrains.rd.ide.model.CollectionUsageFindRequest"))
            serializers.register(LazyCompanionMarshaller(RdId(530348090014804480), classLoader, "com.jetbrains.rd.ide.model.CollectionUsageResultItem"))
            serializers.register(LazyCompanionMarshaller(RdId(-7146090193949203894), classLoader, "com.jetbrains.rd.ide.model.CollectionUsageFindResponse"))
        }


        @JvmStatic
        @JvmName("internalCreateModel")
        @Deprecated("Use create instead", ReplaceWith("create(lifetime, protocol)"))
        internal fun createModel(lifetime: Lifetime, protocol: IProtocol): CollectionUsageFinderProtocol  {
            @Suppress("DEPRECATION")
            return create(lifetime, protocol)
        }

        @JvmStatic
        @Deprecated("Use protocol.collectionUsageFinderProtocol or revise the extension scope instead", ReplaceWith("protocol.collectionUsageFinderProtocol"))
        fun create(lifetime: Lifetime, protocol: IProtocol): CollectionUsageFinderProtocol  {
            IdeRoot.register(protocol.serializers)

            return CollectionUsageFinderProtocol()
        }


        const val serializationHash = -5550225387213516425L

    }
    override val serializersOwner: ISerializersOwner get() = CollectionUsageFinderProtocol
    override val serializationHash: Long get() = CollectionUsageFinderProtocol.serializationHash

    //fields
    val findCollectionUsages: RdCall<CollectionUsageFindRequest, CollectionUsageFindResponse> get() = _findCollectionUsages
    //methods
    //initializer
    init {
        bindableChildren.add("findCollectionUsages" to _findCollectionUsages)
    }

    //secondary constructor
    private constructor(
    ) : this(
        RdCall<CollectionUsageFindRequest, CollectionUsageFindResponse>(CollectionUsageFindRequest, CollectionUsageFindResponse)
    )

    //equals trait
    //hash code trait
    //pretty print
    override fun print(printer: PrettyPrinter)  {
        printer.println("CollectionUsageFinderProtocol (")
        printer.indent {
            print("findCollectionUsages = "); _findCollectionUsages.print(printer); println()
        }
        printer.print(")")
    }
    //deepClone
    override fun deepClone(): CollectionUsageFinderProtocol   {
        return CollectionUsageFinderProtocol(
            _findCollectionUsages.deepClonePolymorphic()
        )
    }
    //contexts
    //threading
    override val extThreading: ExtThreadingKind get() = ExtThreadingKind.Default
}
val IProtocol.collectionUsageFinderProtocol get() = getOrCreateExtension(CollectionUsageFinderProtocol::class) { @Suppress("DEPRECATION") CollectionUsageFinderProtocol.create(lifetime, this) }



/**
 * #### Generated from [CollectionUsageFinderProtocol.kt:11]
 */
data class CollectionUsageFindRequest (
    val filePath: String,
    val caretOffset: Int
) : IPrintable {
    //companion

    companion object : IMarshaller<CollectionUsageFindRequest> {
        override val _type: KClass<CollectionUsageFindRequest> = CollectionUsageFindRequest::class
        override val id: RdId get() = RdId(-2015687819842928794)

        @Suppress("UNCHECKED_CAST")
        override fun read(ctx: SerializationCtx, buffer: AbstractBuffer): CollectionUsageFindRequest  {
            val filePath = buffer.readString()
            val caretOffset = buffer.readInt()
            return CollectionUsageFindRequest(filePath, caretOffset)
        }

        override fun write(ctx: SerializationCtx, buffer: AbstractBuffer, value: CollectionUsageFindRequest)  {
            buffer.writeString(value.filePath)
            buffer.writeInt(value.caretOffset)
        }


    }
    //fields
    //methods
    //initializer
    //secondary constructor
    //equals trait
    override fun equals(other: Any?): Boolean  {
        if (this === other) return true
        if (other == null || other::class != this::class) return false

        other as CollectionUsageFindRequest

        if (filePath != other.filePath) return false
        if (caretOffset != other.caretOffset) return false

        return true
    }
    //hash code trait
    override fun hashCode(): Int  {
        var __r = 0
        __r = __r*31 + filePath.hashCode()
        __r = __r*31 + caretOffset.hashCode()
        return __r
    }
    //pretty print
    override fun print(printer: PrettyPrinter)  {
        printer.println("CollectionUsageFindRequest (")
        printer.indent {
            print("filePath = "); filePath.print(printer); println()
            print("caretOffset = "); caretOffset.print(printer); println()
        }
        printer.print(")")
    }
    //deepClone
    //contexts
    //threading
}


/**
 * #### Generated from [CollectionUsageFinderProtocol.kt:34]
 */
data class CollectionUsageFindResponse (
    val success: Boolean,
    val message: String,
    val targetName: String,
    val collectionKind: String,
    val scope: String,
    val items: List<CollectionUsageResultItem>
) : IPrintable {
    //companion

    companion object : IMarshaller<CollectionUsageFindResponse> {
        override val _type: KClass<CollectionUsageFindResponse> = CollectionUsageFindResponse::class
        override val id: RdId get() = RdId(-7146090193949203894)

        @Suppress("UNCHECKED_CAST")
        override fun read(ctx: SerializationCtx, buffer: AbstractBuffer): CollectionUsageFindResponse  {
            val success = buffer.readBool()
            val message = buffer.readString()
            val targetName = buffer.readString()
            val collectionKind = buffer.readString()
            val scope = buffer.readString()
            val items = buffer.readList { CollectionUsageResultItem.read(ctx, buffer) }
            return CollectionUsageFindResponse(success, message, targetName, collectionKind, scope, items)
        }

        override fun write(ctx: SerializationCtx, buffer: AbstractBuffer, value: CollectionUsageFindResponse)  {
            buffer.writeBool(value.success)
            buffer.writeString(value.message)
            buffer.writeString(value.targetName)
            buffer.writeString(value.collectionKind)
            buffer.writeString(value.scope)
            buffer.writeList(value.items) { v -> CollectionUsageResultItem.write(ctx, buffer, v) }
        }


    }
    //fields
    //methods
    //initializer
    //secondary constructor
    //equals trait
    override fun equals(other: Any?): Boolean  {
        if (this === other) return true
        if (other == null || other::class != this::class) return false

        other as CollectionUsageFindResponse

        if (success != other.success) return false
        if (message != other.message) return false
        if (targetName != other.targetName) return false
        if (collectionKind != other.collectionKind) return false
        if (scope != other.scope) return false
        if (items != other.items) return false

        return true
    }
    //hash code trait
    override fun hashCode(): Int  {
        var __r = 0
        __r = __r*31 + success.hashCode()
        __r = __r*31 + message.hashCode()
        __r = __r*31 + targetName.hashCode()
        __r = __r*31 + collectionKind.hashCode()
        __r = __r*31 + scope.hashCode()
        __r = __r*31 + items.hashCode()
        return __r
    }
    //pretty print
    override fun print(printer: PrettyPrinter)  {
        printer.println("CollectionUsageFindResponse (")
        printer.indent {
            print("success = "); success.print(printer); println()
            print("message = "); message.print(printer); println()
            print("targetName = "); targetName.print(printer); println()
            print("collectionKind = "); collectionKind.print(printer); println()
            print("scope = "); scope.print(printer); println()
            print("items = "); items.print(printer); println()
        }
        printer.print(")")
    }
    //deepClone
    //contexts
    //threading
}


/**
 * #### Generated from [CollectionUsageFinderProtocol.kt:16]
 */
data class CollectionUsageResultItem (
    val filePath: String,
    val startOffset: Int,
    val length: Int,
    val line: Int,
    val column: Int,
    val kind: String,
    val kindDisplayName: String,
    val operationKind: String,
    val operationDisplayName: String,
    val operationName: String,
    val text: String,
    val previewText: String,
    val previewStartLine: Int,
    val previewHighlightStart: Int,
    val previewHighlightLength: Int
) : IPrintable {
    //companion

    companion object : IMarshaller<CollectionUsageResultItem> {
        override val _type: KClass<CollectionUsageResultItem> = CollectionUsageResultItem::class
        override val id: RdId get() = RdId(530348090014804480)

        @Suppress("UNCHECKED_CAST")
        override fun read(ctx: SerializationCtx, buffer: AbstractBuffer): CollectionUsageResultItem  {
            val filePath = buffer.readString()
            val startOffset = buffer.readInt()
            val length = buffer.readInt()
            val line = buffer.readInt()
            val column = buffer.readInt()
            val kind = buffer.readString()
            val kindDisplayName = buffer.readString()
            val operationKind = buffer.readString()
            val operationDisplayName = buffer.readString()
            val operationName = buffer.readString()
            val text = buffer.readString()
            val previewText = buffer.readString()
            val previewStartLine = buffer.readInt()
            val previewHighlightStart = buffer.readInt()
            val previewHighlightLength = buffer.readInt()
            return CollectionUsageResultItem(filePath, startOffset, length, line, column, kind, kindDisplayName, operationKind, operationDisplayName, operationName, text, previewText, previewStartLine, previewHighlightStart, previewHighlightLength)
        }

        override fun write(ctx: SerializationCtx, buffer: AbstractBuffer, value: CollectionUsageResultItem)  {
            buffer.writeString(value.filePath)
            buffer.writeInt(value.startOffset)
            buffer.writeInt(value.length)
            buffer.writeInt(value.line)
            buffer.writeInt(value.column)
            buffer.writeString(value.kind)
            buffer.writeString(value.kindDisplayName)
            buffer.writeString(value.operationKind)
            buffer.writeString(value.operationDisplayName)
            buffer.writeString(value.operationName)
            buffer.writeString(value.text)
            buffer.writeString(value.previewText)
            buffer.writeInt(value.previewStartLine)
            buffer.writeInt(value.previewHighlightStart)
            buffer.writeInt(value.previewHighlightLength)
        }


    }
    //fields
    //methods
    //initializer
    //secondary constructor
    //equals trait
    override fun equals(other: Any?): Boolean  {
        if (this === other) return true
        if (other == null || other::class != this::class) return false

        other as CollectionUsageResultItem

        if (filePath != other.filePath) return false
        if (startOffset != other.startOffset) return false
        if (length != other.length) return false
        if (line != other.line) return false
        if (column != other.column) return false
        if (kind != other.kind) return false
        if (kindDisplayName != other.kindDisplayName) return false
        if (operationKind != other.operationKind) return false
        if (operationDisplayName != other.operationDisplayName) return false
        if (operationName != other.operationName) return false
        if (text != other.text) return false
        if (previewText != other.previewText) return false
        if (previewStartLine != other.previewStartLine) return false
        if (previewHighlightStart != other.previewHighlightStart) return false
        if (previewHighlightLength != other.previewHighlightLength) return false

        return true
    }
    //hash code trait
    override fun hashCode(): Int  {
        var __r = 0
        __r = __r*31 + filePath.hashCode()
        __r = __r*31 + startOffset.hashCode()
        __r = __r*31 + length.hashCode()
        __r = __r*31 + line.hashCode()
        __r = __r*31 + column.hashCode()
        __r = __r*31 + kind.hashCode()
        __r = __r*31 + kindDisplayName.hashCode()
        __r = __r*31 + operationKind.hashCode()
        __r = __r*31 + operationDisplayName.hashCode()
        __r = __r*31 + operationName.hashCode()
        __r = __r*31 + text.hashCode()
        __r = __r*31 + previewText.hashCode()
        __r = __r*31 + previewStartLine.hashCode()
        __r = __r*31 + previewHighlightStart.hashCode()
        __r = __r*31 + previewHighlightLength.hashCode()
        return __r
    }
    //pretty print
    override fun print(printer: PrettyPrinter)  {
        printer.println("CollectionUsageResultItem (")
        printer.indent {
            print("filePath = "); filePath.print(printer); println()
            print("startOffset = "); startOffset.print(printer); println()
            print("length = "); length.print(printer); println()
            print("line = "); line.print(printer); println()
            print("column = "); column.print(printer); println()
            print("kind = "); kind.print(printer); println()
            print("kindDisplayName = "); kindDisplayName.print(printer); println()
            print("operationKind = "); operationKind.print(printer); println()
            print("operationDisplayName = "); operationDisplayName.print(printer); println()
            print("operationName = "); operationName.print(printer); println()
            print("text = "); text.print(printer); println()
            print("previewText = "); previewText.print(printer); println()
            print("previewStartLine = "); previewStartLine.print(printer); println()
            print("previewHighlightStart = "); previewHighlightStart.print(printer); println()
            print("previewHighlightLength = "); previewHighlightLength.print(printer); println()
        }
        printer.print(")")
    }
    //deepClone
    //contexts
    //threading
}
