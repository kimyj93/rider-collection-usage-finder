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
 * #### Generated from [CollectionUsageFinderProtocol.kt:9]
 */
class CollectionUsageFinderProtocol private constructor(
    private val _findCollectionUsages: RdCall<CollectionUsageFindRequest, CollectionUsageFindResponse>
) : RdExtBase() {
    //companion
    
    companion object : ISerializersOwner {
        
        override fun registerSerializersCore(serializers: ISerializers)  {
            val classLoader = javaClass.classLoader
            serializers.register(LazyCompanionMarshaller(RdId(-2015687819842928794), classLoader, "com.jetbrains.rd.ide.model.CollectionUsageFindRequest"))
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
        
        
        const val serializationHash = -6506649137475799515L
        
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
 * #### Generated from [CollectionUsageFinderProtocol.kt:10]
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
 * #### Generated from [CollectionUsageFinderProtocol.kt:15]
 */
data class CollectionUsageFindResponse (
    val success: Boolean,
    val message: String
) : IPrintable {
    //companion
    
    companion object : IMarshaller<CollectionUsageFindResponse> {
        override val _type: KClass<CollectionUsageFindResponse> = CollectionUsageFindResponse::class
        override val id: RdId get() = RdId(-7146090193949203894)
        
        @Suppress("UNCHECKED_CAST")
        override fun read(ctx: SerializationCtx, buffer: AbstractBuffer): CollectionUsageFindResponse  {
            val success = buffer.readBool()
            val message = buffer.readString()
            return CollectionUsageFindResponse(success, message)
        }
        
        override fun write(ctx: SerializationCtx, buffer: AbstractBuffer, value: CollectionUsageFindResponse)  {
            buffer.writeBool(value.success)
            buffer.writeString(value.message)
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
        
        return true
    }
    //hash code trait
    override fun hashCode(): Int  {
        var __r = 0
        __r = __r*31 + success.hashCode()
        __r = __r*31 + message.hashCode()
        return __r
    }
    //pretty print
    override fun print(printer: PrettyPrinter)  {
        printer.println("CollectionUsageFindResponse (")
        printer.indent {
            print("success = "); success.print(printer); println()
            print("message = "); message.print(printer); println()
        }
        printer.print(")")
    }
    //deepClone
    //contexts
    //threading
}
