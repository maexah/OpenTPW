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
/// <param name="Performance">The record's <c>+0x1c</c>, which the loader hands <c>Bumper_SetPerformance</c>.</param>
/// <param name="MeshBase">Its <c>+0x14</c>: the first supplemental mesh a car is.</param>
/// <param name="MeshCount">Its <c>+0x18</c>: how many a car cycles through.</param>
/// <param name="Duration">Its <c>+0x04</c>: what a car's timer is set to, in track ticks.</param>
/// <param name="State">Its <c>+0x50</c>: 0 closed, 1 loading, 2 running.</param>
public readonly record struct SavedTrackRide( int Handle, int X, int Y, int Orientation, int ItemId,
	int Performance = 0, int MeshBase = 0, int MeshCount = 0, int Duration = 0, int State = 0 );

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
/// <para>
/// <b>Written</b> by <see cref="Splice"/> (<c>docs/exe/saves.md</c>, "OpenTPW's writer, a track ride's record"): a
/// ride made since the load goes in as its record and its close, and one gone is taken out with every chunk under
/// its handle.
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
	private const int CarType = 5;
	private const int CloseType = 6;
	private const int CarRecordType = 9;

	/// <summary>A ride chunk's size: the header, the handle, where it stands, its orientation, its item and five dwords.</summary>
	private const int RideSize = 52;

	/// <summary>A close chunk's size: the header and the handle.</summary>
	private const int CloseSize = 16;

	/// <summary>How many car chunks (5) and car record chunks (9) the module steps over - what the loader restores whole.</summary>
	public int CarChunks { get; private set; }

	/// <summary>A chunk's header: its type, its own size and its whole size, a dword each.</summary>
	private const int HeaderSize = 12;

	private readonly byte[] _data;

	private readonly List<SavedTrackRide> _rides = [];
	private readonly List<SavedTrackSection> _sections = [];

	/// <summary>Every chunk after the root's own data that opens on a handle: its type, the handle, where it lies and its whole size.</summary>
	private readonly List<(int Type, int Handle, int At, int Whole)> _chunks = [];

	/// <summary>Where the root chunk begins and ends, or -1 where the walk refused the module.</summary>
	private int _start = -1;

	private int _end = -1;

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
		var chunks = new List<(int Type, int Handle, int At, int Whole)>();
		var carChunks = 0;

		for ( var at = start + rootOwn; at != end; )
		{
			if ( at + HeaderSize > end )
				throw new InvalidDataException( $"a chunk header at {at} runs past the module's end at {end}" );

			var (type, own, whole) = HeaderAt( at );

			if ( own < HeaderSize || whole < own || at + whole > end )
				throw new InvalidDataException( $"a type {type} chunk at {at} claims {own} and {whole} bytes" );

			// Every chunk but the stamp opens on its ride's handle (FUN_005428e0).
			if ( type >= RideType && own >= HeaderSize + 4 )
				chunks.Add( (type, ReadInt32At( at + HeaderSize ), at, whole) );

			if ( type == RideType )
			{
				// The five dwords after the item are the record's +0x1c, +0x14, +0x18, +0x04 and +0x50, in that
				// order (0x00542a6e..0x00542ae0); a shorter chunk reads them nought, as the loader's reads do.
				int Field( int index ) => own >= 32 + (index * 4) + 4 ? ReadInt32At( at + 32 + (index * 4) ) : 0;

				rides.Add( new SavedTrackRide( ReadInt32At( at + 12 ), ReadInt32At( at + 16 ), ReadInt32At( at + 20 ),
					ReadInt32At( at + 24 ), ReadInt32At( at + 28 ), Field( 0 ), Field( 1 ), Field( 2 ), Field( 3 ), Field( 4 ) ) );
			}
			else if ( type is CarType or CarRecordType )
			{
				++carChunks;
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
		_chunks.AddRange( chunks );
		_start = start;
		_end = end;
		CarChunks = carChunks;
		ClosedOnTag = true;
	}

	/// <summary>How many cars the module holds under a ride's handle - its type 5 chunks.</summary>
	public int CarsOf( int handle ) => _chunks.Count( chunk => chunk.Type == CarType && chunk.Handle == handle );

	/// <summary>
	/// A ride's record and its close as the original's writer leaves them for a ride with no section, no car and
	/// nobody on its lists (<c>FUN_005428e0</c>): the type 3 chunk, then the type 6.
	/// </summary>
	public static byte[] RideChunks( SavedTrackRide ride )
	{
		var chunks = new byte[RideSize + CloseSize];

		int[] record =
		[
			RideType, RideSize, RideSize, ride.Handle, ride.X, ride.Y, ride.Orientation, ride.ItemId, ride.Performance,
			ride.MeshBase, ride.MeshCount, ride.Duration, ride.State, CloseType, CloseSize, CloseSize, ride.Handle
		];

		Buffer.BlockCopy( record, 0, chunks, 0, chunks.Length );

		return chunks;
	}

	/// <summary>
	/// Writes each ride's performance, mesh words, duration and state over its own record where the file has it;
	/// a ride the module does not hold is skipped.
	/// </summary>
	/// <returns>How many records were written over.</returns>
	public int Put( byte[] body, IReadOnlyList<SavedTrackRide> rides )
	{
		var put = 0;

		foreach ( var ride in rides )
		{
			var at = _chunks.FindIndex( chunk => chunk.Type == RideType && chunk.Handle == ride.Handle && chunk.Whole >= RideSize );

			if ( at < 0 )
				continue;

			int[] fields = [ride.Performance, ride.MeshBase, ride.MeshCount, ride.Duration, ride.State];

			Buffer.BlockCopy( fields, 0, body, _chunks[at].At + 32, fields.Length * 4 );
			++put;
		}

		return put;
	}

	/// <summary>
	/// The body with the rides in <paramref name="gone"/> taken out, each with every chunk under its handle, and
	/// the rides in <paramref name="made"/> put in (<see cref="RideChunks"/>), the root's whole size following.
	///
	/// <para>
	/// The original writes its table slot by slot (<c>0x005429f3</c>), so a made ride goes before the first ride
	/// left whose slot is higher, and at the module's end where none is. Every other byte of the body is carried.
	/// </para>
	/// </summary>
	/// <param name="body">The body, with this module where the file has it.</param>
	/// <exception cref="InvalidOperationException">The module was refused by the walk, so where a ride lies is not known.</exception>
	public byte[] Splice( byte[] body, IReadOnlyCollection<int> gone, IReadOnlyList<SavedTrackRide> made )
	{
		if ( gone.Count == 0 && made.Count == 0 )
			return body;

		if ( !ClosedOnTag )
			throw new InvalidOperationException( $"the file's track-rides module was not read whole: {Problem}" );

		// The module's pieces in order: each kept chunk, and each made ride's before the first kept ride of a higher slot.
		var pieces = new List<(int Slot, byte[] Bytes)>();
		var first = _chunks.Count > 0 ? _chunks[0].At : _end;

		foreach ( var chunk in _chunks )
		{
			if ( !gone.Contains( chunk.Handle ) )
				pieces.Add( (chunk.Handle & 0xff, body.AsSpan( chunk.At, chunk.Whole ).ToArray()) );
		}

		foreach ( var ride in made.OrderBy( ride => ride.Handle & 0xff ) )
		{
			var slot = ride.Handle & 0xff;
			var before = pieces.FindIndex( piece => piece.Slot > slot );

			pieces.Insert( before < 0 ? pieces.Count : before, (slot, RideChunks( ride )) );
		}

		var length = pieces.Sum( piece => piece.Bytes.Length );
		var spliced = new byte[body.Length - (_end - first) + length];
		var at = first;

		Buffer.BlockCopy( body, 0, spliced, 0, first );

		foreach ( var (_, bytes) in pieces )
		{
			Buffer.BlockCopy( bytes, 0, spliced, at, bytes.Length );
			at += bytes.Length;
		}

		Buffer.BlockCopy( body, _end, spliced, at, body.Length - _end );
		BitConverter.TryWriteBytes( spliced.AsSpan( _start + 8, 4 ), at - _start );

		return spliced;
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
