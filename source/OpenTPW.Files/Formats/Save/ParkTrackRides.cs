namespace OpenTPW;

/// <summary>
/// One track ride as a park save left it: its handle and where it stands.
/// </summary>
/// <param name="Handle">
/// The ride's slot in its low byte and its item's <c>Bumper.BumperType</c> above it - the number the object
/// record's <c>mTrackRideHandle</c> holds, so <c>0xfffffc00</c> is a Dino Karts in slot 0.
/// </param>
/// <param name="X">Where it stands across, in map cells times <c>0xc00</c>.</param>
/// <param name="Y">And down.</param>
/// <param name="Orientation">The orientation code the placer hands <c>FUN_00545890</c>.</param>
/// <param name="ItemId">The item's <c>Info.Id</c>.</param>
public readonly record struct SavedTrackRide( int Handle, int X, int Y, int Orientation, int ItemId );

/// <summary>
/// One section of track as a park save left it.
/// </summary>
/// <param name="Handle">The handle of the ride it belongs to.</param>
/// <param name="Type">
/// Its type dword: the low 16 bits are the type <c>FUN_00545310</c> reads, and bit 16 marks the first two
/// straights from the station.
/// </param>
/// <param name="X">Where it lies across, in map cells times <c>0xc00</c>.</param>
/// <param name="Y">And down.</param>
/// <param name="RidesBefore">How many ride records the file holds before it - the loader reads them in turn.</param>
public readonly record struct SavedTrackSection( int Handle, int Type, int X, int Y, int RidesBefore );

/// <summary>
/// The track-rides module of a park save (<c>KART</c>): every ride whose item has a <c>Bumper.BumperType</c>, and
/// the track laid for it. FileFormats <c>saves.md</c>, "The track-rides module".
///
/// <para>
/// <b>It is walked, and the walk checks itself.</b> The module follows the <c>RSYS</c> tag - stored little-endian,
/// so it reads <c>SYSR</c> in a dump - and is a tree of chunks, each a type, its own size and its whole size: one
/// root of type 1 whose whole size is the module, then its children. Stepped chunk by chunk by whole size, the walk
/// has to land on the root's end and then on the <c>KART</c> tag, or the module is refused and no ride is read.
/// </para>
/// <para>
/// <b>Two chunk types are read and the rest are stepped over by length</b>: a ride (3) and a track section (4), each
/// in file order, which is circuit order from the station. The stamp (2), the cars (5), their records (9) and a
/// ride's close (6) are not, because nothing here models them. What the loader does with them is
/// <see cref="ParkTrackRideTable"/>'s.
/// </para>
/// </summary>
public sealed class ParkTrackRides
{
	/// <summary>The tag closing the ride system module before this one, little-endian - so it reads <c>SYSR</c> in a dump.</summary>
	private const string PrecedingTrailer = "SYSR";

	/// <summary>This module's own tag, little-endian - <c>TRAK</c> read backwards.</summary>
	private const string Trailer = "KART";

	private const int RootType = 1;
	private const int RideType = 3;
	private const int SectionType = 4;

	/// <summary>A chunk's header: its type, its own size and its whole size, a dword each.</summary>
	private const int HeaderSize = 12;

	private readonly byte[] _data;

	private readonly List<SavedTrackRide> _rides = [];
	private readonly List<SavedTrackSection> _sections = [];

	/// <summary>Why the walk refused the module, or null where it did not. A refused module reads no ride.</summary>
	public string? Problem { get; private set; }

	/// <summary>Whether the walk landed on the <c>KART</c> tag.</summary>
	public bool ClosedOnTag { get; private set; }

	/// <summary>The rides in file order.</summary>
	public IReadOnlyList<SavedTrackRide> Rides => _rides;

	/// <summary>The sections in file order.</summary>
	public IReadOnlyList<SavedTrackSection> Sections => _sections;

	/// <summary>
	/// Reads the module out of the inflated payload - what <see cref="SaveReader.ReadFile"/> hands back, and the
	/// same array <see cref="ParkWorld"/> is given.
	/// </summary>
	public ParkTrackRides( byte[] inflatedPayload )
	{
		_data = inflatedPayload ?? throw new ArgumentNullException( nameof( inflatedPayload ) );

		try
		{
			Walk();
		}
		catch ( Exception e )
		{
			_rides.Clear();
			_sections.Clear();
			Problem ??= e.Message;
		}
	}

	private void Walk()
	{
		var start = FindModule();
		var (rootType, rootOwn, rootWhole) = HeaderAt( start );

		if ( rootType != RootType || rootOwn < HeaderSize || rootWhole < rootOwn )
			throw new InvalidDataException( $"the track-rides module opens on a type {rootType} chunk of {rootOwn} and {rootWhole} bytes" );

		var end = start + rootWhole;

		if ( end + Trailer.Length > _data.Length )
			throw new InvalidDataException( $"the track-rides module's {rootWhole} bytes run past the payload" );

		var rides = new List<SavedTrackRide>();
		var sections = new List<SavedTrackSection>();

		for ( var at = start + rootOwn; at != end; )
		{
			if ( at + HeaderSize > end )
				throw new InvalidDataException( $"a chunk header at {at} runs past the module's end at {end}" );

			var (type, own, whole) = HeaderAt( at );

			if ( own < HeaderSize || whole < own || at + whole > end )
				throw new InvalidDataException( $"a type {type} chunk at {at} claims {own} and {whole} bytes" );

			if ( type == RideType )
			{
				rides.Add( new SavedTrackRide( ReadInt32At( at + 12 ), ReadInt32At( at + 16 ), ReadInt32At( at + 20 ),
					ReadInt32At( at + 24 ), ReadInt32At( at + 28 ) ) );
			}
			else if ( type == SectionType )
			{
				sections.Add( new SavedTrackSection( ReadInt32At( at + 12 ), ReadInt32At( at + 16 ),
					ReadInt32At( at + 20 ), ReadInt32At( at + 24 ), rides.Count ) );
			}

			at += whole;
		}

		if ( System.Text.Encoding.ASCII.GetString( _data, end, Trailer.Length ) != Trailer )
			throw new InvalidDataException( $"the track-rides module ends at {end} on no {Trailer} tag" );

		_rides.AddRange( rides );
		_sections.AddRange( sections );
		ClosedOnTag = true;
	}

	/// <summary>The module's start: right after the first <c>SYSR</c> tag, which is the only one in all nine park files read.</summary>
	private int FindModule()
	{
		var tag = System.Text.Encoding.ASCII.GetBytes( PrecedingTrailer );

		for ( var at = 0; at + tag.Length + HeaderSize <= _data.Length; ++at )
		{
			if ( _data.AsSpan( at, tag.Length ).SequenceEqual( tag ) )
				return at + tag.Length;
		}

		throw new InvalidDataException( $"no {PrecedingTrailer} tag anywhere in {_data.Length} bytes" );
	}

	private (int Type, int Own, int Whole) HeaderAt( int at )
		=> (ReadInt32At( at ), ReadInt32At( at + 4 ), ReadInt32At( at + 8 ));

	private int ReadInt32At( int at ) => BitConverter.ToInt32( _data, at );
}
