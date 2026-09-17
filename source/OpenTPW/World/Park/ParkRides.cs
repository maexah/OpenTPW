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
///
/// <para>
/// <b>What handing over the model does and does not include.</b> A bound script gets its thing and the
/// twelve animation roles that thing's model carries, so the animation instructions answer real clip
/// lengths instead of the engine's floor. It does <b>not</b> get a playing channel: the engine keeps a
/// per-model array of animation players, and a trigger queues behind whatever one of them is already
/// running, adding that remaining time to the length it answers. Nothing here plays anything, so there is
/// never anything to queue behind - and the answer is then the new clip alone, which is exactly what the
/// engine itself computes for an idle channel rather than a simplification of it. <c>FLUSHANIM</c> is the
/// visible consequence: with a model the engine clears that channel's <i>queued</i> role
/// (<c>FUN_00473270</c>) and nothing else, so with no channel it remains the no-op it always was.
/// </para>
///
/// <para>
/// <b>Nor is the teardown half modelled.</b> The object's own destructor (<c>FUN_004dd0a0</c>) hands the
/// id at <c>+0x24</c> to the script teardown with a mode of 0, 4 or 7. Nothing here destroys a script when
/// the thing it belongs to goes, because nothing yet takes a thing out of a park.
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

	/// <summary>
	/// The things standing in this park, or null where there are none to stand - a test binds scripts
	/// without a park to draw. Where there is one, a script is given <i>its own thing's</i> animation
	/// player rather than a second reading of the same clips, and this is also what the per-frame sweep
	/// poses through.
	/// </summary>
	private readonly ParkObjects? _objects;

	/// <summary>The id of the script each placed thing is running, by the thing id the park file gives it.</summary>
	private readonly Dictionary<int, int> _scripts = [];

	/// <summary>How many placed things were given a script.</summary>
	public int Bound => _scripts.Count;

	/// <summary>
	/// How many of those were also given a model with animations on it - the engine's <c>+0xc8</c>. It is
	/// counted rather than assumed because an item shipping no clips at all is ordinary data: the drinks
	/// shop ships none and its script names none either.
	/// </summary>
	public int Animated { get; private set; }

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
	/// <param name="objects">
	/// The things already standing in this park, or null where nothing is drawn. It comes last so that a
	/// caller passing only a file system - which is what every test does - is unaffected.
	/// </param>
	public ParkRides( string themeName, ParkWorld? world, ParkItemCatalogue? catalogue, BaseFileSystem? files = null,
		ParkObjects? objects = null )
	{
		ThemeName = themeName;
		Name = $"{themeName} ride scripts";

		_files = files ?? FileSystem;
		_objects = objects;

		// The scripts' own way of reaching another script. It is set even where there is nothing to bind,
		// because it is the scheduler's property and not the park's.
		Scheduler.Loader = Load;

		if ( world == null || catalogue == null )
			return;

		foreach ( var placed in world.Objects )
		{
			// Everything this park actually stood up, which is not the same as everything it placed. The gate
			// and the traffic lights carry no position of their own - the engine builds those two by name out
			// of the item descriptions rather than from the save's placements (FUN_005156a0) - but they are
			// catalogue objects like any other and their scripts are bound by the very same constructor. So
			// what decides is whether this park has one standing, not whether the save gave it a cell. With
			// nothing drawn this is exactly the old test, which is the case every test here exercises.
			if ( !placed.IsPlaced && _objects?.AnimationsFor( placed.ThingId ) is null )
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

			// What the engine's loader does with the thing it was handed, before the script runs a single
			// instruction: it keeps the thing at +0xac and seeds the model handle at +0xc8 from that
			// thing's own entry in the world's table. Nothing writes +0xc8 afterwards, so this is the only
			// moment there is to do it - see RideScript.Animations.
			if ( Scheduler.Find( id ) is { } script )
			{
				script.ThingId = placed.ThingId;

				// Its own thing's player where the thing is standing, so that what the script triggers and
				// what the model is posed from are the same one. Read afresh only where nothing was drawn,
				// which is what a test binding scripts against a park it never builds is doing.
				script.Animations = _objects?.AnimationsFor( placed.ThingId )
					?? RideAnimations.Load( item.Directory, item.Stem, _files );

				if ( script.Animations.Loaded > 0 )
					++Animated;
			}
		}

		// And tell the gate whether this park is open, which is the one thing in the game that ever writes
		// a script variable from outside a script. Last, because it needs the binding above to have run.
		CommandTheGate( world );

		Log.Info( $"{ThemeName}: {Bound} of the park's things are running a script" +
			(Scriptless > 0 ? $", and {Scriptless} have none to run" : "") +
			$"; {Animated} of them can see their own animations" );
	}

	/// <summary>The gate's command variable, by the name its own script declares it under.</summary>
	private const string GateCommand = "VAR_COMMAND";

	/// <summary>Open the gate. <c>FUN_00519ef0</c> writes this when a park is opened.</summary>
	private const int OpenTheGate = 1;

	/// <summary>Shut it - and it is <b>2</b>, not 0, which only the disassembly says.</summary>
	private const int ShutTheGate = 2;

	/// <summary>
	/// Tells the park's gate whether the park is open, which is what makes it move at all.
	///
	/// <para>
	/// <b>Until this existed the gate could not be opened by anything.</b> <c>Gates.RSE</c> opens on a
	/// dispatch loop that reads <c>VAR_COMMAND</c>; every variable starts at nought, so it cycled five
	/// instructions for ever and reached neither the open branch nor the close one, and a park saved with
	/// its gates open drew them shut. The only thing in the original that ever writes that variable is
	/// opening or closing a park - <c>FUN_00519ef0</c>, which looks the gate's script up from the header's
	/// own <c>mParkGates</c> handle and writes variable 0.
	/// </para>
	/// <para>
	/// <b>The values are read off the disassembly rather than the decompile, and one of them is not what it
	/// looks like.</b> The call sites push an extra argument that survives one call and is consumed by the
	/// next, so Ghidra renders the argument lists wrongly; read as instructions, opening writes <b>1</b> and
	/// shutting writes <b>2</b>. A decompile-only reading gives "1 opens, 0 shuts", which is wrong.
	/// </para>
	/// <para>
	/// <b>Commanding it as the park loads is a reproduction of the end state, not of a call anybody has
	/// traced.</b> What the original does with its gate at load time - whether it re-commands, or restores
	/// the script's variables with the rest of the save - is not established here. What is established is
	/// that this park is saved open, so its gate belongs open; doing it this way makes the screen agree
	/// with <c>mParkClosed</c>, and it is the first thing in the tree to read that field for anything.
	/// </para>
	/// </summary>
	private void CommandTheGate( ParkWorld world )
	{
		var id = ScriptFor( world.ParkGates );

		if ( id == 0 || Scheduler.Find( id ) is not { } gate )
			return;

		// Zero is open - see ParkWorld.ParkClosed, where the name runs the other way to the value.
		var command = world.ParkClosed == 0 ? OpenTheGate : ShutTheGate;

		// Said out loud rather than shrugged off: a script that declares no such variable takes the write
		// nowhere, and a gate that never moves is exactly what that looks like from the outside.
		if ( !gate.Set( GateCommand, command ) )
			Log.Warning( $"{ThemeName}: the gate's script declares no {GateCommand}, so it cannot be opened" );
	}

	/// <summary>The variable the gate's script reports its own state through - see <see cref="GateStatus"/>.</summary>
	private const string GateState = "VAR_STATUS";

	/// <summary>What <see cref="GateStatus"/> answers where there is no gate, as the original's own does.</summary>
	public const int NoGate = -1;

	/// <summary>The value the gate reports when it is open and a guest may come through.</summary>
	public const int GateIsOpen = 1;

	/// <summary>
	/// What the park's gate says it is doing, which is what a guest waiting outside is waiting on -
	/// <c>FUN_0051a290</c>.
	///
	/// <para>
	/// <b>It reads the gate script's variable 1, and that index is off the disassembly rather than the
	/// decompile.</b> The call site pushes <c>1</c> <i>before</i> calling the script-lookup
	/// <c>FUN_0055a070</c>, whose <c>ADD ESP,0x4</c> cleans only its own argument - so the <c>1</c>
	/// survives and is consumed as the second argument of <c>FUN_0055a390</c>, cleaned by the later
	/// <c>ADD ESP,0x8</c>. Ghidra renders that as a one-argument call and hides the index completely. It
	/// is the same trap that gave the gate command a wrong "0 shuts".
	/// </para>
	/// <para>
	/// <b>The variable is reached by the name the script itself declares</b>, not by the number: all four
	/// themes' <c>Gates.RSE</c> declare exactly <c>VAR_COMMAND</c>, <c>VAR_STATUS</c> and
	/// <c>VAR_TEMP</c>, so variable 1 is <c>VAR_STATUS</c> - measured across all four rather than assumed
	/// from jungle's, because the four gate scripts otherwise differ.
	/// </para>
	/// <para>
	/// <see cref="NoGate"/> where the park has no gate thing at all, which is what the original returns
	/// and is deliberately not the same as a gate that is shut.
	/// </para>
	/// </summary>
	public int GateStatus( ParkWorld? world )
	{
		if ( world == null )
			return NoGate;

		var id = ScriptFor( world.ParkGates );

		if ( id == 0 || Scheduler.Find( id ) is not { } gate )
			return NoGate;

		// Nought from a name the script does not declare and nought from a gate that has not opened are
		// the same answer here, and both mean "not yet" - which is why the command write is the one that
		// reports a miss and this one does not need to.
		return gate[GateState];
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

		// And then, once, whatever those ticks asked for is shown. The engine sweeps its animation players
		// from the per-frame update (FUN_0044e410 at 0054fa96), past the back edge of this very catch-up
		// loop, off one snapshot of the clock - so the sweep belongs after the loop rather than inside it,
		// and takes the moment the last tick ran at, which is exactly GameClock.Ticks beats in.
		_objects?.Sweep( (int)(GameClock.Ticks * MillisecondsPerTick) );
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
