namespace OpenTPW;

/// <summary>
/// save\users\&lt;slot&gt;&lt;name&gt;\gms.dat: one player's progress and their own options.
///
/// <para>
/// The player's name is not in it: their folder's name is their slot's number and then their name, and
/// that is all there is of either. The original writes the file when the player is made, when a new
/// player is handed their first key, after every park is saved, and when the player is saved on Select
/// New Player or on quitting (0x005afc60, through 0x005aff50). Every field is little-endian:
/// </para>
/// <code>
/// dword   version, 12
/// 4 bytes player-wide record tickets, one for each of four records      progress +0x18
/// 2 bytes two further tickets                                          +0x26
/// dword   golden tickets spent                                         +0x1c
/// dword   golden keys given outright                                   +0x20
/// byte    Instant Action (1) or Full Simulation (0)                    +0x24
/// byte    swearing filtered                                            +0x25
/// byte    first park still to come                                     +0x4c
/// dword   number of parks, then for each: dword name length, the name, and its record (see ParkRecord)
/// 39 bytes the player's options (see PlayerOptions)
/// dword   number of rides bought with tickets, then a word for each ride id
/// </code>
/// <para>
/// A brand-new player's file (0x005c7f40, 0x005aef50) is sixty-eight bytes: everything zero but the mode, the
/// swearing filter and the first-park flag, which start on, no parks and no rides, and the options as they
/// stand at the time. A park's record is only made once the player is picked (0x005c83b0), one for each
/// theme, and the records are written in order of their names' bytes, as the original keeps them sorted.
/// </para>
/// <para>
/// The original only refuses a file whose version is below 12, and a short read or two parks with one name
/// just stops the load where it failed (0x005afd70). It never checks, so the player keeps whatever was read
/// before that and the rest stays at its default. <see cref="Read"/> does the same, and says through
/// <see cref="IsComplete"/> whether it got to the end.
/// </para>
/// </summary>
public sealed class PlayerFile
{
	/// <summary>The version the original writes, and the lowest it reads.</summary>
	public const int CurrentVersion = 12;

	/// <summary>Tickets for the four player-wide records - set the first time any park holds the record (0x005af940).</summary>
	public byte[] RecordTickets = new byte[4];

	/// <summary>Two further player-wide tickets (0x005afb00).</summary>
	public byte[] BonusTickets = new byte[2];

	/// <summary>Golden tickets spent on rides (0x005af740).</summary>
	public int TicketsSpent;

	/// <summary>Golden keys given outright rather than earned by tickets - PlayerProgress_AddKey (0x005afc30).</summary>
	public int KeysGiven;

	/// <summary>Instant Action rather than Full Simulation, as the new player dialog chose.</summary>
	public bool InstantAction;

	/// <summary>Whether swearing is filtered out of what the player types (swears.txt, alloweds.txt). On for a new player.</summary>
	public bool FilterSwearing = true;

	/// <summary>Set until the player's first park starts, when the state machine clears it (0x005b0cc0). On for a new player.</summary>
	public bool FirstParkToCome = true;

	public List<ParkRecord> Parks = new();

	public PlayerOptions Options = new();

	/// <summary>The rides bought with golden tickets, by ride id.</summary>
	public List<ushort> RideIds = new();

	/// <summary>Whether <see cref="Read"/> got to the end of the file - see the class remarks.</summary>
	public bool IsComplete { get; private set; } = true;

	/// <summary>Whether it holds the player's options whole - always, for a file made here rather than read.</summary>
	public bool HasOptions { get; private set; } = true;

	/// <summary>Golden tickets earned: the player-wide ones, and each park's for the parks <paramref name="isPark"/> knows (0x005af530).</summary>
	public int CountTickets( Func<string, bool> isPark )
	{
		var tickets = RecordTickets.Count( b => b != 0 ) + BonusTickets.Count( b => b != 0 );

		foreach ( var park in Parks )
		{
			if ( isPark( park.Name ) )
				tickets += park.Tickets.Count( b => b != 0 );
		}

		return tickets;
	}

	/// <summary>Golden keys held - PlayerProgress_CountKeys (0x005af680): every three tickets make a key, and the keys given outright count too.</summary>
	public int CountKeys( Func<string, bool> isPark ) => (CountTickets( isPark ) / 3) + KeysGiven;

	/// <summary>The park's record, made if there is none yet - 0x005b09c0.</summary>
	public ParkRecord Park( string name )
	{
		var park = Parks.FirstOrDefault( p => p.Name == name );

		if ( park == null )
		{
			park = new ParkRecord { Name = name };
			Parks.Add( park );
		}

		return park;
	}

	public static PlayerFile Read( Stream stream )
	{
		var file = new PlayerFile { HasOptions = false };

		try
		{
			var record = RecordStream.ForReading( stream );

			var version = 0;
			record.Int32( ref version );

			if ( version < CurrentVersion )
			{
				file.IsComplete = false;
				return file;
			}

			file.Record( record );
		}
		catch ( Exception e ) when ( e is EndOfStreamException or InvalidDataException )
		{
			file.IsComplete = false;
		}

		return file;
	}

	public void Write( Stream stream )
	{
		var record = RecordStream.ForWriting( stream );

		var version = CurrentVersion;
		record.Int32( ref version );

		Record( record );
	}

	private void Record( RecordStream record )
	{
		for ( int i = 0; i < RecordTickets.Length; ++i )
			record.Byte( ref RecordTickets[i] );

		for ( int i = 0; i < BonusTickets.Length; ++i )
			record.Byte( ref BonusTickets[i] );

		record.Int32( ref TicketsSpent );
		record.Int32( ref KeysGiven );
		record.Bool( ref InstantAction );
		record.Bool( ref FilterSwearing );
		record.Bool( ref FirstParkToCome );

		RecordParks( record );

		HasOptions = false;
		Options.Record( record );
		HasOptions = true;

		var rides = RideIds.Count;
		record.Int32( ref rides );

		if ( !record.IsWriting )
		{
			CheckCount( rides );
			RideIds = new List<ushort>( rides );
		}

		for ( int i = 0; i < rides; ++i )
		{
			ushort id = record.IsWriting ? RideIds[i] : (ushort)0;
			record.UInt16( ref id );

			if ( !record.IsWriting )
				RideIds.Add( id );
		}
	}

	/// <summary>The parks, in order of their names' bytes when written, as the original's sorted tree walks them.</summary>
	private void RecordParks( RecordStream record )
	{
		if ( record.IsWriting )
			Parks.Sort( ( a, b ) => string.CompareOrdinal( a.Name, b.Name ) );

		var count = Parks.Count;
		record.Int32( ref count );

		if ( !record.IsWriting )
		{
			CheckCount( count );
			Parks = new List<ParkRecord>( count );
		}

		for ( int i = 0; i < count; ++i )
		{
			var park = record.IsWriting ? Parks[i] : new ParkRecord();

			record.String( ref park.Name );
			park.Record( record );

			if ( record.IsWriting )
				continue;

			// The original's tree refuses a second record with the same name, and the load stops there.
			if ( Parks.Any( p => p.Name == park.Name ) )
				throw new InvalidDataException( $"Two parks called '{park.Name}'" );

			Parks.Add( park );
		}
	}

	private static void CheckCount( int count )
	{
		if ( count < 0 || count > 0x10000 )
			throw new InvalidDataException( $"{count} is not a count" );
	}
}

/// <summary>
/// What a player has done in one park, kept under its theme's name - the 0xbc-byte record 0x005b0d70 reads
/// and writes, of which these are the parts it saves.
/// </summary>
public sealed class ParkRecord
{
	/// <summary>The theme's folder name, as data\levels has it.</summary>
	public string Name = "";

	/// <summary>The six golden tickets for the park's goals (0x005af810).</summary>
	public byte[] Tickets = new byte[6];

	/// <summary>Whether this park holds each of the four player-wide records, and the record's value while it does (0x005af940).</summary>
	public byte[] HoldsRecord = new byte[4];

	public int[] RecordValues = new int[4];

	/// <summary>The two halves of a park name the player chose, as up to 33 UTF-16 units each, ended by a zero (mSignNameA, mSignNameB).</summary>
	public ushort[] SignNameA = new ushort[33];

	public ushort[] SignNameB = new ushort[33];

	/// <summary>Whether the player renamed the park, so the sign names stand in for the gate's own (mNameChanged).</summary>
	public bool NameChanged;

	/// <summary>mAllResearchCompleted.</summary>
	public bool AllResearchCompleted;

	internal void Record( RecordStream record )
	{
		for ( int i = 0; i < Tickets.Length; ++i )
			record.Byte( ref Tickets[i] );

		for ( int i = 0; i < HoldsRecord.Length; ++i )
		{
			record.Byte( ref HoldsRecord[i] );
			record.Int32( ref RecordValues[i] );
		}

		for ( int i = 0; i < SignNameA.Length; ++i )
		{
			record.UInt16( ref SignNameA[i] );
			record.UInt16( ref SignNameB[i] );
		}

		record.Bool( ref NameChanged );
		record.Bool( ref AllResearchCompleted );
	}
}

/// <summary>
/// The options that belong to a player rather than to the machine, in the order 0x00423e90 reads and writes
/// them. Each volume is a byte saying whether its group is on, three bytes of padding and a dword out of 100,
/// as the options object lays them out; each switch is a byte. The labels are the ones the original's reads
/// carry.
/// </summary>
public sealed class PlayerOptions
{
	public bool EffectsOn;
	public int EffectsVolume;
	public bool MusicOn;
	public int MusicVolume;
	public bool SpeechOn;
	public int SpeechVolume;
	public bool MovieOn;
	public int MovieVolume;

	/// <summary>AdvisorOn.</summary>
	public bool Advisor;

	/// <summary>TutorialOn.</summary>
	public bool Tutorial;

	/// <summary>TooltipsOn - the options screen's Popup help.</summary>
	public bool PopupHelp;

	/// <summary>ConfirmDeleteOn - Confirmations.</summary>
	public bool Confirmations;

	/// <summary>RMBScrollOn - Scroll's Right button rather than Pushscroll.</summary>
	public bool RightButtonScroll;

	/// <summary>RMBCancelOn.</summary>
	public bool RmbCancel;

	/// <summary>IsometricOn - Rotation's 90 degs rather than Smooth.</summary>
	public bool NinetyDegreeRotation;

	internal void Record( RecordStream record )
	{
		Volume( record, ref EffectsOn, ref EffectsVolume );
		Volume( record, ref MusicOn, ref MusicVolume );
		Volume( record, ref SpeechOn, ref SpeechVolume );
		Volume( record, ref MovieOn, ref MovieVolume );

		record.Bool( ref Advisor );
		record.Bool( ref Tutorial );
		record.Bool( ref PopupHelp );
		record.Bool( ref Confirmations );
		record.Bool( ref RightButtonScroll );
		record.Bool( ref RmbCancel );
		record.Bool( ref NinetyDegreeRotation );
	}

	private static void Volume( RecordStream record, ref bool on, ref int volume )
	{
		record.Bool( ref on );
		record.Padding( 3 );
		record.Int32( ref volume );
	}
}
