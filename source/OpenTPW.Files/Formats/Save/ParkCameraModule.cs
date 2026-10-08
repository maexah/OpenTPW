using System.Buffers.Binary;

namespace OpenTPW;

/// <summary>
/// The camera module of a park save (<c>EMAK</c>, "Camera"): where the park's camera stood when the park was saved.
/// FileFormats <c>saves.md</c>, "The camera module (<c>EMAK</c>)"; the writer is <c>FUN_0042cdc0</c> and the loader
/// <c>FUN_0042cec0</c>.
///
/// <para>
/// <b>Found by two anchors that have to agree</b>, as <see cref="ParkClock"/> is: the module is forty bytes between
/// the ride scripts' closing tag (<c>ESSR</c>) and its own (<c>EMAK</c>).
/// </para>
/// <para>
/// <b>The rotation is in radians.</b> The camera's update turns it by the constants at <c>0x006fdd94</c> and
/// <c>0x006fdd68</c>, a quarter turn either way (<c>0x0042b489</c>, <c>0x0042b4a4</c>).
/// </para>
/// </summary>
public sealed class ParkCameraModule
{
	/// <summary>The tag closing the ride scripts' module, little-endian - <c>RSSE</c> read backwards.</summary>
	private const string PrecedingTrailer = "ESSR";

	/// <summary>This module's own tag, little-endian - <c>KAME</c> read backwards.</summary>
	private const string Trailer = "EMAK";

	/// <summary>The module's size: a zoom, a rotation, a flags dword, two points of three floats and a saved rotation.</summary>
	private const int ModuleSize = 40;

	private const int ZoomAt = 0;
	private const int YRotationAt = 4;
	private const int PointXAt = 12;
	private const int PointZAt = 20;

	/// <summary>
	/// What of a camera the module holds that a running park moves: <c>gf_CameraZoom</c>, <c>gf_YRotation</c> in
	/// radians, and the two ground coordinates of <c>gs_RequiredPOIPosition</c>, ten units to a cell.
	/// </summary>
	public readonly record struct View( float Zoom, float YRotation, float PointX, float PointZ );

	/// <summary>Why the module was refused, or null where it was not. A refused module has no view.</summary>
	public string? Problem { get; private set; }

	/// <summary>Where the module begins in the body; -1 where it was refused.</summary>
	internal int At { get; } = -1;

	/// <summary>The camera as saved. Null where the module was refused.</summary>
	public View? Saved { get; private set; }

	/// <summary>
	/// Reads the module out of the inflated payload - what <see cref="SaveReader.ReadFile"/> hands back, and the same
	/// array <see cref="ParkWorld"/> is given.
	/// </summary>
	public ParkCameraModule( byte[] inflatedPayload )
	{
		ArgumentNullException.ThrowIfNull( inflatedPayload );

		try
		{
			At = FindModule( inflatedPayload );

			var module = inflatedPayload.AsSpan( At, ModuleSize );

			Saved = new View(
				Zoom: BinaryPrimitives.ReadSingleLittleEndian( module[ZoomAt..] ),
				YRotation: BinaryPrimitives.ReadSingleLittleEndian( module[YRotationAt..] ),
				PointX: BinaryPrimitives.ReadSingleLittleEndian( module[PointXAt..] ),
				PointZ: BinaryPrimitives.ReadSingleLittleEndian( module[PointZAt..] ) );
		}
		catch ( Exception e )
		{
			At = -1;
			Saved = null;
			Problem ??= e.Message;
		}
	}

	/// <summary>
	/// Writes a view over the module in a copy of the body this was read from. The flags, the height of the point, the
	/// saved point and the saved rotation are left as the file's: the loader zeroes the flags as it reads them
	/// (<c>0x0042d04c</c>), and the rest is nought in every park file read.
	/// </summary>
	internal void Put( byte[] body, View view )
	{
		if ( At < 0 )
			throw new InvalidOperationException( $"the camera module was not found: {Problem}" );

		var module = body.AsSpan( At, ModuleSize );

		BinaryPrimitives.WriteSingleLittleEndian( module[ZoomAt..], view.Zoom );
		BinaryPrimitives.WriteSingleLittleEndian( module[YRotationAt..], view.YRotation );
		BinaryPrimitives.WriteSingleLittleEndian( module[PointXAt..], view.PointX );
		BinaryPrimitives.WriteSingleLittleEndian( module[PointZAt..], view.PointZ );
	}

	/// <summary>The module's start: right after an <c>ESSR</c> tag with an <c>EMAK</c> tag the module's size after it.</summary>
	private static int FindModule( byte[] data )
	{
		var before = System.Text.Encoding.ASCII.GetBytes( PrecedingTrailer );
		var after = System.Text.Encoding.ASCII.GetBytes( Trailer );

		for ( var at = 0; at + before.Length + ModuleSize + after.Length <= data.Length; ++at )
		{
			if ( !data.AsSpan( at, before.Length ).SequenceEqual( before ) )
				continue;

			var module = at + before.Length;

			if ( data.AsSpan( module + ModuleSize, after.Length ).SequenceEqual( after ) )
				return module;
		}

		throw new InvalidDataException(
			$"no {Trailer} tag sits {ModuleSize} bytes after a {PrecedingTrailer} tag anywhere in {data.Length} bytes" );
	}
}
