using System.Collections;
using System.Reflection;

namespace OpenTPW;

/// <summary>
/// What NeoVeldrid's Vulkan memory manager holds, read by reflection because the library has no property for it (Q834; the
/// decompile's facts are in <c>docs/DECISIONS.md</c>, "NeoVeldrid's memory: what it counts, and what it keeps"). Its <c>_totalAllocatedBytes</c> is the live
/// blocks, the sizes handed out and not yet freed. The driver is asked for memory in chunks of 64 MB (mapped) or 256 MB
/// (unmapped), one list per memory type, a new chunk whenever no chunk has a free block that fits; a chunk goes back to the
/// driver only when the whole manager is disposed. A block of a chunk's size or more is its own allocation and goes back at
/// its free. The device also keeps the staging textures and buffers an upload went through (<c>_availableStagingTextures</c>, a
/// texture's first-fit pool, and <c>_availableStagingBuffers</c>) for its whole life: a texture is never let go of, a buffer only above 512 bytes. Debug accounting (<c>AGENTS.md</c> rule 5): the console's <c>mem vulkan</c> prints it.
/// </summary>
public static class VulkanMemory
{
	public readonly record struct Chunks( int Count, ulong Bytes, ulong FreeBytes, ulong LargestFree );

	public readonly record struct Pool( int Textures, ulong TextureBytes, int Buffers, ulong BufferBytes );

	public readonly record struct Snapshot( ulong Live, Chunks Mapped, Chunks Unmapped, Pool Staging )
	{
		public ulong ChunkBytes => Mapped.Bytes + Unmapped.Bytes;

		public override string ToString()
		{
			return $"live {Live / 1048576.0:F1}MB; chunks {ChunkBytes / 1048576.0:F0}MB taken from the driver and kept until the device ends "
				+ $"({Describe( "mapped", Mapped )}, {Describe( "unmapped", Unmapped )}); "
				+ $"staging pool kept for the device's life: {Staging.Textures} textures {Staging.TextureBytes / 1048576.0:F1}MB, {Staging.Buffers} buffers {Staging.BufferBytes / 1048576.0:F1}MB";
		}

		private static string Describe( string name, Chunks chunks )
		{
			return $"{chunks.Count} {name} of {chunks.Bytes / 1048576.0:F0}MB, {chunks.FreeBytes / 1048576.0:F1}MB free, largest free block {chunks.LargestFree / 1048576.0:F1}MB";
		}
	}

	private const BindingFlags All = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

	/// <summary>The manager's live bytes, its chunks and the device's staging pool; null where the device has no manager (OpenGL, or a library that renamed it or any field the reader goes by, inner ones too).</summary>
	public static Snapshot? Read( object device )
	{
		if ( FindManager( device ) is not { } manager )
			return null;

		var live = Member( manager, "_totalAllocatedBytes" ) as ulong?;
		var mapped = Member( manager, "_allocatorsByMemoryType" ) as IDictionary;
		var unmapped = Member( manager, "_allocatorsByMemoryTypeUnmapped" ) as IDictionary;
		var textures = Member( device, "_availableStagingTextures" ) as IEnumerable;
		var buffers = Member( device, "_availableStagingBuffers" ) as IEnumerable;

		if ( live is null || mapped is null || unmapped is null || textures is null || buffers is null )
			return null;

		if ( Held( textures ) is not { } heldTextures || Held( buffers ) is not { } heldBuffers || Sum( mapped ) is not { } mappedChunks || Sum( unmapped ) is not { } unmappedChunks )
			return null;

		return new Snapshot( live.Value, mappedChunks, unmappedChunks, new Pool( heldTextures.Count, heldTextures.Bytes, heldBuffers.Count, heldBuffers.Bytes ) );
	}

	private static object? FindManager( object device )
	{
		for ( var type = device.GetType(); type != null; type = type.BaseType )
		{
			foreach ( var field in type.GetFields( All ) )
			{
				if ( field.FieldType.Name == "VkDeviceMemoryManager" && field.GetValue( device ) is { } manager )
					return manager;
			}
		}

		return null;
	}

	/// <summary>The device memory of each pooled staging resource (each has a <c>Memory</c> block); null where one has no readable size.</summary>
	private static (int Count, ulong Bytes)? Held( IEnumerable pool )
	{
		int count = 0;
		ulong bytes = 0;

		foreach ( var resource in pool )
		{
			count++;

			if ( Member( Member( resource, "Memory" ) ?? new object(), "Size" ) is not ulong size )
				return null;

			bytes += size;
		}

		return (count, bytes);
	}

	/// <summary>The chunks of one side; null where a list, a chunk's size or a block's size is not readable.</summary>
	private static Chunks? Sum( IDictionary sets )
	{
		int count = 0;
		ulong bytes = 0, free = 0, largest = 0;

		foreach ( var set in sets.Values )
		{
			if ( Member( set, "_allocators" ) is not IEnumerable chunks )
				return null;

			foreach ( var chunk in chunks )
			{
				count++;

				if ( Member( chunk, "_totalMemorySize" ) is not ulong total || Member( chunk, "_freeBlocks" ) is not IEnumerable blocks )
					return null;

				bytes += total;

				foreach ( var block in blocks )
				{
					if ( Member( block, "Size" ) is not ulong size )
					return null;

					free += size;
					largest = Math.Max( largest, size );
				}
			}
		}

		return new Chunks( count, bytes, free, largest );
	}

	private static object? Member( object owner, string name )
	{
		var type = owner.GetType();
		return type.GetField( name, All )?.GetValue( owner ) ?? type.GetProperty( name, All )?.GetValue( owner );
	}
}
