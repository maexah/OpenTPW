namespace OpenTPW;

/// <summary>
/// The scripts the things standing in a park run - the reference nothing has ever handed out before.
///
/// <para>
/// <b>Everything needed for this existed and was not joined up.</b> <see cref="RideScriptFile"/> reads the
/// format, <see cref="RideScript"/> runs it, <see cref="RideScriptScheduler"/> gives each one its turn and
/// <see cref="ParkItemCatalogue"/> already knows where every item's files live - and yet nothing in the
/// whole tree ever constructed a <see cref="RideScript"/> outside a test. So "199 of the 308 shipped
/// scripts run start to finish" was measured in a harness, and in a running park the number was nought.
/// This is the entity that makes it not nought.
/// </para>
///
/// <para>
/// <b>The original does exactly this, in one function, as an object is built.</b> <c>FUN_004dcf90</c>,
/// called from the object constructor at <c>0x004db517</c>, builds a path with <c>sprintf</c> and the
/// format <c>"%s\\%S%s"</c> - the item's own directory, its name, and <c>".rse"</c> from
/// <c>DAT_00700540</c> - opens it, hands it to the RSSE loader <c>FUN_005587f0</c>, and keeps <b>the id
/// the loader answers</b> at the thing's field <c>+0x24</c>. It keeps no pointer: everything that reaches
/// a script afterwards does so by id through the one registry, which is why <see cref="ScriptFor"/>
/// answers an id here too.
/// </para>
///
/// <para>
/// <b>An item with no script is ordinary data and not a failure.</b> The original answers that case by
/// logging "Ride script not located for %s - using placeholder" and loading
/// <c>Data\TestScript\test.rse</c> instead - and <b>that file does not ship</b>, so in a real installation
/// the fallback fails to open as well and the loader simply answers nought. The placeholder is therefore
/// deliberately not copied; what is copied is that nothing goes wrong. Such items are counted in
/// <see cref="Scriptless"/> so the count can be seen rather than guessed at.
/// </para>
///
/// <para>
/// <b>What this deliberately does not do yet.</b> The original's binder goes on to push the item's own
/// operating speed into the script's speed word (<c>FUN_0055a300</c>, field <c>+0xc0</c>) and its
/// operating duration into variable 3, which is <see cref="RideVariables.VAR_DURATION"/>, logging
/// "SPEED = %d" and "DUR = %d" as it does. Neither is done here, because which key of the item's
/// description feeds which of them is <b>not</b> established: the constructor reads its record through a
/// two-byte pointer, so the offsets Ghidra prints are not byte offsets, and they do not line up with
/// where <c>FUN_004db7d0</c> parses <c>mOperatingSpeed</c> and <c>mOperatingDuration</c>. Guessing it
/// would matter rather than being harmless - the speed word is the divisor <c>WAIT</c> scales by, and it
/// is exactly neutral only at the 50 the loader seeds.
/// </para>
/// </summary>
public sealed class ParkRides : Entity
{
	/// <summary>The theme folder these were loaded from - "jungle", "fantasy", "hallow" or "space".</summary>
	public string ThemeName { get; }

	/// <summary>
	/// Every script this park is running, and the registry each one reaches the others through. It is the
	/// engine's single global list (<c>DAT_008791b0</c>), which is why there is one of these per park and
	/// not one per item.
	/// </summary>
	public RideScriptScheduler Scheduler { get; } = new();

	/// <summary>Where the scripts are read from - see <see cref="ParkItemCatalogue"/> for why this is asked for.</summary>
	private readonly BaseFileSystem _files;

	/// <summary>The id of the script each placed thing is running, by the thing id the park file gives it.</summary>
	private readonly Dictionary<int, int> _scripts = [];

	/// <summary>How many placed things were given a script.</summary>
	public int Bound => _scripts.Count;

	/// <summary>
	/// How many placed things had no script to give them. Ordinary: a litter bin has no more use for one
	/// than the original's placeholder does - see the remarks on this class.
	/// </summary>
	public int Scriptless { get; private set; }

	/// <summary>
	/// The script the thing with this id is running, or nought where it has none - the engine's field
	/// <c>+0x24</c>, an id rather than a pointer. Nought is the same answer its loader gives.
	/// </summary>
	public int ScriptFor( int thingId ) => _scripts.TryGetValue( thingId, out var id ) ? id : 0;

	/// <summary>
	/// Where an item's script lives: <c>&lt;its own directory&gt;/&lt;its own name&gt;.RSE</c>, which is
	/// the original's <c>"%s\\%S%s"</c> with the separator this file system uses.
	///
	/// <para>
	/// <b>The case is not corrected and must not be.</b> The 76 ride archives spell their scripts every
	/// which way - <c>B_DRIP.RSE</c>, <c>Bigapple.RSE</c>, <c>GoKarts.RSE</c>, <c>mBUGGY.RSE</c> - and
	/// what resolves them is the file system folding case for the whole path, archives included, which is
	/// pinned by <c>FileSystemCaseTests</c>. Spelling a guess here would work on Windows and fail nowhere
	/// else, which is the worst way for it to be wrong.
	/// </para>
	/// </summary>
	public static string ScriptPathFor( ParkItemCatalogue.Item item ) => $"{item.Directory}/{item.Stem}.RSE";

	/// <param name="world">The park's own save, already walked, or null where the theme ships none.</param>
	/// <param name="catalogue">
	/// What this theme can have standing in it, built once by <see cref="Level"/> and shared with
	/// <see cref="ParkObjects"/>.
	/// </param>
	/// <param name="files">
	/// Where to read from, or null for the one a running game mounted. A test passes its own, so that
	/// binding a real theme's scripts needs no global to have been set.
	/// </param>
	public ParkRides( string themeName, ParkWorld? world, ParkItemCatalogue? catalogue, BaseFileSystem? files = null )
	{
		ThemeName = themeName;
		Name = $"{themeName} ride scripts";

		_files = files ?? FileSystem;

		// The scripts' own way of reaching another script. It is set even where there is nothing to bind,
		// because it is the scheduler's property and not the park's.
		Scheduler.Loader = Load;

		if ( world == null || catalogue == null )
			return;

		foreach ( var placed in world.Objects )
		{
			if ( !placed.IsPlaced )
				continue;

			// An id this theme has nothing for is already reported by ParkObjects, which cannot draw it
			// either. Saying so twice about one object would be noise.
			if ( !catalogue.TryGet( placed.CatalogueId, out var item ) )
				continue;

			// Through the scheduler rather than around it, so the ids come from one counter that is only
			// ever incremented - the engine's DAT_008791a8, which starts at 1 and never reuses an id, so
			// nothing can be left holding one that has quietly come to mean a different script.
			var id = Scheduler.Spawn( ScriptPathFor( item ) );

			if ( id == 0 )
			{
				++Scriptless;
				continue;
			}

			_scripts[placed.ThingId] = id;
		}

		Log.Info( $"{ThemeName}: {Bound} of the park's things are running a script" +
			(Scriptless > 0 ? $", and {Scriptless} have none to run" : "") );
	}

	/// <summary>
	/// Reads one script, or answers null where there is none - which is what both the park's binding and a
	/// script's own <c>SPAWNCHILD</c> do with the answer, and in both cases nought is a real result rather
	/// than an error.
	/// </summary>
	private RideScript? Load( string path )
	{
		try
		{
			using var stream = _files.OpenRead( path );

			if ( stream == null )
				return null;

			var file = new RideScriptFile( stream );

			if ( !file.IsValid )
				return null;

			return new RideScript( file )
			{
				// What lets this script spawn its own children: they are named relative to where it was
				// loaded from - see RideScript.Directory.
				Directory = DirectoryOf( path ),

				// A ride to drive and somewhere to put its effects. Both are pure state with nothing of
				// the world in them, and without them every COAST and every ADDOBJ would be counted as
				// unimplemented instead of being carried out.
				Ride = new RideState(),
				Effects = new RideEffects()
			};
		}
		catch ( Exception e )
		{
			// A script that will not read must not take the park down with it: the item still stands
			// there, it simply does nothing.
			Log.Warning( $"{ThemeName}: '{path}' would not read, so whatever placed it runs no script - {e.Message}" );
			return null;
		}
	}

	/// <summary>Everything up to the last separator, which is the prefix the engine keeps at <c>+0x38</c>.</summary>
	private static string DirectoryOf( string path )
	{
		var cut = path.LastIndexOf( '/' );

		return cut < 0 ? string.Empty : path[..cut];
	}

	/// <summary>
	/// One tick for every 31ms that has come due, which is where the original runs the whole script
	/// system: <c>FUN_005516b0</c>, called once per park tick at <c>0x0054f56b</c>.
	/// </summary>
	protected override void OnUpdate()
	{
		for ( int i = 0; i < GameClock.TicksDue; ++i )
			Scheduler.Advance( MillisecondsAt( i ) );
	}

	/// <summary>
	/// A tick's own instant, in the milliseconds the interpreter counts in - <c>WAIT</c> adds its operand
	/// to this clock unchanged, and the engine's is a millisecond counter ending at <c>timeGetTime</c>.
	///
	/// <para>
	/// Worked back from the tick number rather than read from <see cref="GameClock.Now"/>, which is one
	/// value for the whole frame: after a long frame several ticks come due at once, and handing them all
	/// the same instant would let a <c>WAIT</c> of a single tick come due immediately. By the time an
	/// entity updates, <see cref="GameClock.Ticks"/> already counts this frame's ticks, so the first of
	/// them is that many less <see cref="GameClock.TicksDue"/>, plus one.
	/// </para>
	/// </summary>
	private static float MillisecondsAt( int index )
		=> (GameClock.Ticks - GameClock.TicksDue + 1 + index) * MillisecondsPerTick;

	/// <summary>The beat, in the units the scripts wait in - see <see cref="GameClock.TickSeconds"/>.</summary>
	private const float MillisecondsPerTick = GameClock.TickSeconds * 1000f;
}
