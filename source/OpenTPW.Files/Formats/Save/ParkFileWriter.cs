using System.Buffers.Binary;
using System.IO.Compression;

namespace OpenTPW;

/// <summary>
/// Writes a park file from the one a park was loaded from (<c>docs/exe/saves.md</c>, "The writer" and "Module by
/// module"): the original's is <c>FUN_00414920</c>, the container, over <c>FUN_004164c0</c>, the body.
///
/// <para>
/// <b>Every module is carried</b>: the body goes out as the file's own, with the fields under <see cref="Running"/>
/// written over it. Nothing else the running park has changed is written yet, so a park loaded from the file this
/// writes has the people and objects of the file it was loaded from, on the running park's ground and under its
/// clock and cash.
/// </para>
/// <para>
/// <b>The container</b> is the version, 500 (<c>0x006fd928</c>), whatever the file loaded carried; the rest of that
/// file's preamble, where the original writes its running language's legal text; a fresh <c>BILZ</c> header; and the
/// body deflated.
/// </para>
/// <para>
/// <b>A deviation: the stream is not the original's, byte for byte.</b> The original deflates at memory level 9
/// (<c>deflateInit2_( strm, -1, 8, 15, 9, 0, ... )</c>), which <see cref="ZLibStream"/> cannot be asked for. The
/// header still reads 15 and 9, and the original loads the file: its loader needs a stream that inflates to the
/// length the header gives.
/// </para>
/// </summary>
public static class ParkFileWriter
{
	/// <summary>The version every park file the original writes opens with (<c>0x006fd928</c>).</summary>
	private const int Version = 500;

	/// <summary>The <c>BILZ</c> header's size, its tag included, which the block's length counts.</summary>
	private const int BlockHeaderSize = 28;

	/// <summary>The header's two dwords after the lengths: the window bits and the memory level the original deflates with.</summary>
	private const int WindowBits = 15;

	private const int MemoryLevel = 9;

	/// <summary>
	/// What of the running park is written over the carried body: <c>mGameTick</c>, <c>mParkClosed</c>,
	/// <c>mNumberOfVisitorsToDate</c>, the economy thing's <c>mBalance</c>, the camera, and the cells to write over
	/// the file's, by their place in <see cref="ParkWorld.Cells"/> (<see cref="ParkWorld.PutCells"/> says which of a
	/// cell's fields); none where it is null.
	/// </summary>
	public readonly record struct Running( int GameTick, bool ParkClosed, int VisitorsToDate, int Balance,
		ParkCameraModule.View Camera, IReadOnlyDictionary<int, ParkWorld.MapCell>? Cells = null );

	/// <summary>
	/// The inflated body of the file: a copy of <paramref name="loaded"/>'s with <paramref name="running"/> written
	/// over it.
	/// </summary>
	/// <exception cref="InvalidOperationException">
	/// The body was not walked to its end, or holds no economy thing or no camera module, so a field's place in it is
	/// not known; or a cell to write has no record in it.
	/// </exception>
	public static byte[] Body( ParkWorld loaded, Running running )
	{
		ArgumentNullException.ThrowIfNull( loaded );

		if ( loaded.Problem != null )
			throw new InvalidOperationException( $"the park file it was loaded from was not read whole: {loaded.Problem}" );

		if ( !loaded.ClosedOnTrailer || loaded.HeaderAt < 0 )
			throw new InvalidOperationException( "the park file it was loaded from did not end where its world block should" );

		if ( loaded.EconomyAt < 0 )
			throw new InvalidOperationException( "the park file it was loaded from holds no economy thing" );

		var body = (byte[])loaded.Body.Clone();

		PutInt32( body, loaded.HeaderAt + ParkWorld.GameTickAt, running.GameTick );
		PutInt32( body, loaded.HeaderAt + ParkWorld.ParkClosedAt, running.ParkClosed ? 1 : 0 );
		PutInt32( body, loaded.HeaderAt + ParkWorld.NumberOfVisitorsToDateAt, running.VisitorsToDate );
		PutInt32( body, loaded.EconomyAt + ParkWorld.BalanceAt, running.Balance );

		loaded.Camera.Put( body, running.Camera );

		if ( running.Cells is { } cells )
			loaded.PutCells( body, cells );

		return body;
	}

	/// <summary>The whole file: <see cref="Body"/> in its container.</summary>
	/// <exception cref="InvalidOperationException"><see cref="Body"/>'s, or the park was loaded from no file.</exception>
	public static byte[] Write( ParkWorld loaded, Running running )
	{
		ArgumentNullException.ThrowIfNull( loaded );

		if ( loaded.Preamble is not { } preamble )
			throw new InvalidOperationException( "the park was loaded from no file, so there is no preamble to carry" );

		return Container( preamble, Body( loaded, running ) );
	}

	/// <summary>A body behind a preamble: the version, the preamble's own bytes after its version, the block.</summary>
	private static byte[] Container( byte[] preamble, byte[] body )
	{
		using var stream = new MemoryStream();

		using ( var deflate = new ZLibStream( stream, CompressionLevel.Optimal, leaveOpen: true ) )
			deflate.Write( body );

		using var file = new MemoryStream();
		using var writer = new BinaryWriter( file );

		writer.Write( Version );
		writer.Write( preamble, 4, preamble.Length - 4 );
		writer.Write( "BILZ"u8 );
		writer.Write( body.Length );
		writer.Write( checked((int)stream.Length + BlockHeaderSize) );
		writer.Write( WindowBits );
		writer.Write( MemoryLevel );
		writer.Write( 0 );
		writer.Write( 0 );
		writer.Write( stream.GetBuffer(), 0, (int)stream.Length );
		writer.Flush();

		return file.ToArray();
	}

	private static void PutInt32( byte[] body, int at, int value ) =>
		BinaryPrimitives.WriteInt32LittleEndian( body.AsSpan( at, 4 ), value );
}
