namespace OpenTPW;

/// <summary>
/// What the game asks the device to hold, by kind: logical bytes (a texture's whole mip chain, a buffer's size), counted where
/// the game creates and releases them. The driver's own figure is larger (it rounds, aligns and allocates in chunks) and is
/// read from outside: the process's resident size where the driver keeps device memory in host RAM (lavapipe), the DRM
/// fdinfo's <c>drm-memory-vram</c> where it does not (<c>tools/harness/lib/memmap.py</c>). Q954. Debug accounting
/// (<c>AGENTS.md</c> rule 5): the console's <c>mem</c> prints it.
/// </summary>
public static class GpuMemory
{
	public enum Kind { Texture, VertexBuffer, IndexBuffer, UniformBuffer, RenderTarget, Pipeline }

	private static readonly long[] _bytes = new long[Enum.GetValues<Kind>().Length];
	private static readonly long[] _count = new long[Enum.GetValues<Kind>().Length];
	private static readonly long[] _peak = new long[Enum.GetValues<Kind>().Length];

	public static void Add( Kind kind, long bytes )
	{
		var i = (int)kind;
		var now = Interlocked.Add( ref _bytes[i], bytes );
		Interlocked.Increment( ref _count[i] );

		if ( now > Interlocked.Read( ref _peak[i] ) )
			Interlocked.Exchange( ref _peak[i], now );
	}

	public static void Remove( Kind kind, long bytes )
	{
		var i = (int)kind;
		Interlocked.Add( ref _bytes[i], -bytes );
		Interlocked.Decrement( ref _count[i] );
	}

	public static long Bytes( Kind kind ) => Interlocked.Read( ref _bytes[(int)kind] );

	public static long Count( Kind kind ) => Interlocked.Read( ref _count[(int)kind] );

	public static long Peak( Kind kind ) => Interlocked.Read( ref _peak[(int)kind] );

	/// <summary>The bytes of a 2D RGBA8 texture with <paramref name="mipLevels"/> levels.</summary>
	public static long TextureBytes( uint width, uint height, uint mipLevels, uint bytesPerTexel = 4, uint samples = 1 )
	{
		long total = 0;

		for ( var level = 0u; level < mipLevels; level++ )
			total += (long)Math.Max( 1u, width >> (int)level ) * Math.Max( 1u, height >> (int)level ) * bytesPerTexel * samples;

		return total;
	}

	/// <summary>One line: each kind's live count, live megabytes and peak megabytes, then the total.</summary>
	public static string Summary()
	{
		var parts = new List<string>();
		long total = 0;

		foreach ( var kind in Enum.GetValues<Kind>() )
		{
			total += Bytes( kind );
			parts.Add( $"{kind} {Count( kind )} x {Bytes( kind ) / 1048576.0:F1}MB (peak {Peak( kind ) / 1048576.0:F1}MB)" );
		}

		return $"gpu logical {total / 1048576.0:F1}MB: " + string.Join( ", ", parts );
	}
}
