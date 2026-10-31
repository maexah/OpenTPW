using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenTPW.Tests;

/// <summary>
/// What the console's <c>mem vulkan</c> reads from NeoVeldrid's memory manager (Q834): the live blocks apart from the chunks, the
/// chunks apart by whether they are mapped, and the free blocks of each. The reader goes by the library's private names, so a
/// test holds the library to them: an upgrade that renames one fails here, not as a silent "0 chunks" in the game.
/// <b>Mutations:</b> live and chunk bytes swapped; the mapped and unmapped lists swapped; a chunk's size not added; the free blocks
/// not summed, or only the last one; the largest free block taken as the sum; a manager missing a field read as zeros; an inner member renamed read as zeros.
/// </summary>
[TestClass]
public class VulkanMemoryTests
{
	private struct FakeBlock
	{
		public ulong Size;
	}

	private class FakeChunk
	{
		private readonly ulong _totalMemorySize;
		private readonly List<FakeBlock> _freeBlocks;

		public FakeChunk( ulong size, params ulong[] free )
		{
			_totalMemorySize = size;
			_freeBlocks = free.Select( f => new FakeBlock { Size = f } ).ToList();
		}
	}

	private class FakeSet
	{
		private readonly List<FakeChunk> _allocators;

		public FakeSet( params FakeChunk[] chunks ) => _allocators = chunks.ToList();
	}

	private class FakeResource
	{
		public FakeBlock Memory { get; }

		public FakeResource( ulong size ) => Memory = new FakeBlock { Size = size };
	}

	// Named as the library's own type, which the reader finds a device's manager by.
	private class VkDeviceMemoryManager
	{
		internal ulong _totalAllocatedBytes = 100UL << 20;
		private readonly Dictionary<uint, FakeSet> _allocatorsByMemoryType = new();
		private readonly Dictionary<uint, FakeSet> _allocatorsByMemoryTypeUnmapped = new();

		public VkDeviceMemoryManager()
		{
			_allocatorsByMemoryType[1] = new FakeSet( new FakeChunk( 64UL << 20, 10UL << 20, 4UL << 20 ) );
			_allocatorsByMemoryType[2] = new FakeSet( new FakeChunk( 64UL << 20, 1UL << 20 ), new FakeChunk( 64UL << 20, 64UL << 20 ) );
			_allocatorsByMemoryTypeUnmapped[3] = new FakeSet( new FakeChunk( 256UL << 20, 30UL << 20, 7UL << 20, 5UL << 20 ) );
		}
	}

	private class FakeDevice
	{
		private readonly VkDeviceMemoryManager _memoryManager = new();
		private readonly List<FakeResource> _availableStagingTextures = new() { new FakeResource( 8UL << 20 ), new FakeResource( 3UL << 20 ) };
		private readonly List<FakeResource> _availableStagingBuffers = new() { new FakeResource( 512 ) };
	}

	/// <summary>Live blocks, and each side's chunks: count, bytes, free bytes (all the free blocks) and the largest free block.</summary>
	[TestMethod]
	public void LiveBlocksAndChunksAreReadApart()
	{
		var snapshot = VulkanMemory.Read( new FakeDevice() ) ?? throw new AssertFailedException( "the fake manager has every field" );

		Assert.AreEqual( 100UL << 20, snapshot.Live );
		Assert.AreEqual( 3, snapshot.Mapped.Count );
		Assert.AreEqual( 192UL << 20, snapshot.Mapped.Bytes );
		Assert.AreEqual( 79UL << 20, snapshot.Mapped.FreeBytes, "10 + 4 + 1 + 64 MB" );
		Assert.AreEqual( 64UL << 20, snapshot.Mapped.LargestFree );
		Assert.AreEqual( 1, snapshot.Unmapped.Count );
		Assert.AreEqual( 256UL << 20, snapshot.Unmapped.Bytes );
		Assert.AreEqual( 42UL << 20, snapshot.Unmapped.FreeBytes, "30 + 7 + 5 MB" );
		Assert.AreEqual( 30UL << 20, snapshot.Unmapped.LargestFree );
		Assert.AreEqual( 448UL << 20, snapshot.ChunkBytes );
		Assert.AreEqual( new VulkanMemory.Pool( 2, 11UL << 20, 1, 512 ), snapshot.Staging );
	}

	/// <summary>The line the console prints says live and chunks apart, with the sizes in megabytes.</summary>
	[TestMethod]
	public void TheConsoleLineNamesLiveAndChunks()
	{
		var line = VulkanMemory.Read( new FakeDevice() )!.Value.ToString();

		StringAssert.StartsWith( line, "live 100.0MB; chunks 448MB" );
		StringAssert.Contains( line, "3 mapped of 192MB, 79.0MB free, largest free block 64.0MB" );
		StringAssert.Contains( line, "1 unmapped of 256MB, 42.0MB free, largest free block 30.0MB" );
		StringAssert.EndsWith( line, "staging pool kept for the device's life: 2 textures 11.0MB, 1 buffers 0.0MB" );
	}

	// A device whose manager is there but whose staging pool is not (a library that renamed it).
	private class DeviceWithoutAPool
	{
		private readonly VkDeviceMemoryManager _memoryManager = new();

		internal int Handle => _memoryManager.GetHashCode();
	}

	private class RenamedSet
	{
		private readonly List<FakeChunk> _chunks = new() { new FakeChunk( 64UL << 20, 1UL << 20 ) };
	}

	private class RenamedChunk
	{
		internal readonly ulong _size = 64UL << 20;
		private readonly List<FakeBlock> _freeBlocks = new() { new FakeBlock { Size = 1UL << 20 } };
	}

	private class RenamedBlockSize
	{
		public FakeBlock[] Free { get; } = new[] { new FakeBlock { Size = 1UL << 20 } };
	}

	private class RenamedChunkHolder
	{
		private readonly List<object> _allocators;

		public RenamedChunkHolder( object chunk ) => _allocators = new List<object> { chunk };
	}

	private class RenamedPoolEntry
	{
		public RenamedBlockSize Memory { get; } = new();
	}

	// The manager's inner shapes are objects, so a test can put a renamed one in without a second manager type.
	private static class Loose
	{
		public class VkDeviceMemoryManager
		{
			internal ulong _totalAllocatedBytes = 1;
			private readonly Dictionary<uint, object> _allocatorsByMemoryType = new();
			private readonly Dictionary<uint, object> _allocatorsByMemoryTypeUnmapped = new();

			public VkDeviceMemoryManager( object mapped ) => _allocatorsByMemoryType[1] = mapped;
		}

		public class Device
		{
			private readonly VkDeviceMemoryManager _memoryManager;
			private readonly List<object> _availableStagingTextures;
			private readonly List<object> _availableStagingBuffers = new();

			public Device( object mapped, object? texture = null )
			{
				_memoryManager = new VkDeviceMemoryManager( mapped );
				_availableStagingTextures = texture is null ? new() : new() { texture };
			}
		}
	}

	/// <summary>An inner member renamed (the set's list, a chunk's size or free list, a block's size, a pooled texture's size) reads as null, not as "0MB".</summary>
	[TestMethod]
	public void ARenamedInnerMemberIsNotReadAsNought()
	{
		Assert.IsNotNull( VulkanMemory.Read( new Loose.Device( new FakeSet( new FakeChunk( 64UL << 20, 1UL << 20 ) ) ) ), "the control reads" );
		Assert.IsNull( VulkanMemory.Read( new Loose.Device( new RenamedSet() ) ), "the set's list" );
		Assert.IsNull( VulkanMemory.Read( new Loose.Device( new RenamedChunkHolder( new RenamedChunk() ) ) ), "a chunk's size" );
		Assert.IsNull( VulkanMemory.Read( new Loose.Device( new RenamedChunkHolder( new FakeChunkWithoutFreeList() ) ) ), "a chunk's free list" );
		Assert.IsNull( VulkanMemory.Read( new Loose.Device( new RenamedChunkHolder( new FakeChunkWithRenamedBlock() ) ) ), "a free block's size" );
		Assert.IsNull( VulkanMemory.Read( new Loose.Device( new FakeSet(), new RenamedPoolEntry() ) ), "a pooled texture's size" );
	}

	private class FakeChunkWithoutFreeList
	{
		internal readonly ulong _totalMemorySize = 64UL << 20;
	}

	private struct RenamedBlock
	{
		public ulong Length;
	}

	private class FakeChunkWithRenamedBlock
	{
		internal readonly ulong _totalMemorySize = 64UL << 20;
		private readonly List<RenamedBlock> _freeBlocks = new() { new RenamedBlock { Length = 1 } };
	}

	/// <summary>A device without a manager, or without the fields, reads as null, never as an empty account.</summary>
	[TestMethod]
	public void ADeviceWithoutTheFieldsIsNotReadAsEmpty()
	{
		Assert.IsNull( VulkanMemory.Read( new object() ), "no manager" );
		Assert.IsNull( VulkanMemory.Read( new DeviceWithoutAPool() ), "a manager but no staging pool" );
	}

	/// <summary>The library's own types still hold every name the reader goes by (NeoVeldrid 1.2.1), the device's staging pool among them.</summary>
	[TestMethod]
	public void NeoVeldridStillHasTheNamesTheReaderGoesBy()
	{
		const BindingFlags all = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
		var library = typeof( NeoVeldrid.GraphicsDevice ).Assembly;
		var manager = library.GetType( "NeoVeldrid.Vk.VkDeviceMemoryManager" ) ?? throw new AssertFailedException( "VkDeviceMemoryManager is gone" );

		Assert.AreEqual( typeof( ulong ), manager.GetField( "_totalAllocatedBytes", all )?.FieldType );

		var device = library.GetType( "NeoVeldrid.Vk.VkGraphicsDevice" ) ?? throw new AssertFailedException( "VkGraphicsDevice is gone" );

		Assert.IsTrue( device.GetFields( all ).Any( f => f.FieldType == manager ), "VkGraphicsDevice no longer holds a VkDeviceMemoryManager field" );

		foreach ( var name in new[] { "_availableStagingTextures", "_availableStagingBuffers" } )
		{
			var resource = device.GetField( name, all )?.FieldType.GetGenericArguments()[0] ?? throw new AssertFailedException( name + " is gone" );
			var memory = resource.GetField( "Memory", all )?.FieldType ?? resource.GetProperty( "Memory", all )?.PropertyType ?? throw new AssertFailedException( name + "'s Memory is gone" );

			Assert.AreEqual( typeof( ulong ), memory.GetField( "Size", all )?.FieldType ?? memory.GetProperty( "Size", all )?.PropertyType );
		}

		foreach ( var name in new[] { "_allocatorsByMemoryType", "_allocatorsByMemoryTypeUnmapped" } )
		{
			var field = manager.GetField( name, all ) ?? throw new AssertFailedException( name + " is gone" );
			var set = field.FieldType.GetGenericArguments()[1];

			Assert.AreEqual( "ChunkAllocatorSet", set.Name );

			var chunk = set.GetField( "_allocators", all )?.FieldType.GetGenericArguments()[0] ?? throw new AssertFailedException( "_allocators is gone" );

			Assert.AreEqual( typeof( ulong ), chunk.GetField( "_totalMemorySize", all )?.FieldType );

			var block = chunk.GetField( "_freeBlocks", all )?.FieldType.GetGenericArguments()[0] ?? throw new AssertFailedException( "_freeBlocks is gone" );

			Assert.AreEqual( typeof( ulong ), (block.GetField( "Size", all )?.FieldType) ?? block.GetProperty( "Size", all )?.PropertyType );
		}
	}
}
