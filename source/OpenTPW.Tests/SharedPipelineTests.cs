using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenTPW.Tests;

/// <summary>
/// What a model's material shares and what the console's `mem` counts (Q954). A park of 1,031 models made 1,031 pipelines of 13
/// distinct states, and the software Vulkan driver kept 5.9 GB for them; materials that would build the same pipeline now hold one
/// (<see cref="SharedCache{TKey, TValue}"/>), made once and let go of with its last user. The device itself is not in these tests:
/// the count of pipelines and the resident size are `mem` in the running game (<c>q954/run6</c>).
/// </summary>
/// <remarks><b>Mutations:</b> the key ignoring the flags, or the second shader; a value made on every acquire; a release that does not
/// count, or that reports the last user at one; the entry kept after its last user; a null release answering true; a texture's
/// mips left out or only the first counted; a multisampled target counted once.</remarks>
[TestClass]
public class SharedPipelineTests
{
	private sealed record Key( string Vertex, string Fragment, MaterialFlags Flags );

	/// <summary>Two users of one key hold one value, made once; a key differing in either shader or in the flags is another value.</summary>
	[TestMethod]
	public void MaterialsOfOneKeyShareOneValue()
	{
		var made = 0;
		var cache = new SharedCache<Key, string>();
		string Make( Key key ) => $"{key.Vertex}/{key.Fragment}/{key.Flags}#{++made}";

		var a = cache.Acquire( new( "v", "f", MaterialFlags.None ), Make );
		var b = cache.Acquire( new( "v", "f", MaterialFlags.None ), Make );

		Assert.AreSame( a, b );
		Assert.AreEqual( 1, made, "made once" );
		Assert.AreEqual( 2, a.Users );
		Assert.AreEqual( 1, cache.Count );

		var otherFlags = cache.Acquire( new( "v", "f", MaterialFlags.Additive ), Make );
		var otherVertex = cache.Acquire( new( "v2", "f", MaterialFlags.None ), Make );
		var otherFragment = cache.Acquire( new( "v", "f2", MaterialFlags.None ), Make );

		Assert.AreEqual( 4, cache.Count );
		Assert.AreEqual( 4, made );
		Assert.AreNotSame( a.Value, otherFlags.Value );
		Assert.AreNotSame( a.Value, otherVertex.Value );
		Assert.AreNotSame( a.Value, otherFragment.Value );
	}

	/// <summary>The value is let go of only with its last user: the first release says no and keeps it, the second says yes and drops it, and the key then makes a new one.</summary>
	[TestMethod]
	public void TheLastUserLetsGo()
	{
		var made = 0;
		var cache = new SharedCache<string, int>();
		var first = cache.Acquire( "k", _ => ++made );
		cache.Acquire( "k", _ => ++made );
		cache.Acquire( "k", _ => ++made );

		Assert.IsFalse( cache.Release( first ), "two users left" );
		Assert.IsFalse( cache.Release( first ), "one user left" );
		Assert.AreEqual( 1, cache.Count );
		Assert.IsTrue( cache.Release( first ), "the last" );
		Assert.AreEqual( 0, cache.Count );

		var again = cache.Acquire( "k", _ => ++made );
		Assert.AreEqual( 2, made, "a key with no users makes its value again" );
		Assert.AreEqual( 2, again.Value );
		Assert.AreEqual( 1, again.Users );
	}

	/// <summary>A material that never got a pipeline (or one already released) is not the last user of anything.</summary>
	[TestMethod]
	public void NothingReleasedIsNotTheLast()
	{
		var cache = new SharedCache<string, int>();
		Assert.IsFalse( cache.Release( null ) );
	}

	/// <summary>The bytes a texture holds: every mip level down to one texel, a side that reaches 1 staying 1, times the texel size and the samples.</summary>
	[TestMethod]
	public void ATexturesBytesCountEveryLevel()
	{
		Assert.AreEqual( (16 + 4 + 1) * 4, GpuMemory.TextureBytes( 4, 4, 3 ), "4x4, three levels, RGBA8" );
		Assert.AreEqual( (16 + 4 + 2 + 1) * 4, GpuMemory.TextureBytes( 8, 2, 4 ), "8x2, four levels: the short side stays 1 texel" );
		Assert.AreEqual( (16 + 4 + 2 + 1) * 4, GpuMemory.TextureBytes( 2, 8, 4 ), "2x8, four levels: the narrow side stays 1 texel" );
		Assert.AreEqual( 2 * 2 * 8 * 4, GpuMemory.TextureBytes( 2, 2, 1, bytesPerTexel: 8, samples: 4 ), "a four-sample depth target, 8 bytes a sample" );
		Assert.AreEqual( 5592404, GpuMemory.TextureBytes( 1024, 1024, 11 ), "1024x1024 down to one texel: 4/3 of its first level, 4 MB" );
	}

	/// <summary>The counters add and take back by kind, count what is alive, and keep the highest the bytes reached.</summary>
	[TestMethod]
	public void TheAccountAddsAndTakesBack()
	{
		var kind = GpuMemory.Kind.IndexBuffer;
		var bytes = GpuMemory.Bytes( kind );
		var count = GpuMemory.Count( kind );
		var other = GpuMemory.Bytes( GpuMemory.Kind.VertexBuffer );

		GpuMemory.Add( kind, 1000 );
		GpuMemory.Add( kind, 500 );

		Assert.AreEqual( bytes + 1500, GpuMemory.Bytes( kind ) );
		Assert.AreEqual( count + 2, GpuMemory.Count( kind ) );
		Assert.AreEqual( other, GpuMemory.Bytes( GpuMemory.Kind.VertexBuffer ), "another kind is untouched" );

		var peak = GpuMemory.Peak( kind );
		GpuMemory.Remove( kind, 1000 );

		Assert.AreEqual( bytes + 500, GpuMemory.Bytes( kind ) );
		Assert.AreEqual( count + 1, GpuMemory.Count( kind ) );
		Assert.AreEqual( peak, GpuMemory.Peak( kind ), "the peak stays" );
		Assert.IsTrue( GpuMemory.Peak( kind ) >= bytes + 1500, "the peak reached the sum" );

		GpuMemory.Remove( kind, 500 );
		Assert.AreEqual( bytes, GpuMemory.Bytes( kind ) );
		Assert.AreEqual( count, GpuMemory.Count( kind ) );

		// the highest the bytes reached, not the latest: up 5,000,000, down to 100, up to 300
		GpuMemory.Add( kind, 5_000_000 );
		GpuMemory.Remove( kind, 4_999_900 );
		GpuMemory.Add( kind, 200 );

		Assert.AreEqual( bytes + 300, GpuMemory.Bytes( kind ) );
		Assert.IsTrue( GpuMemory.Peak( kind ) >= bytes + 5_000_000, "the peak is the highest, not the latest" );

		GpuMemory.Remove( kind, 300 );
		Assert.AreEqual( bytes, GpuMemory.Bytes( kind ) );
		Assert.AreEqual( count, GpuMemory.Count( kind ) );
	}
}
