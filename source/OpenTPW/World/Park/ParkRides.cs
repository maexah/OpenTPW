namespace OpenTPW;

/// <summary>
/// The scripts the things standing in a park run.
///
/// <para>
/// <b>This joins up what the scripts need.</b> <see cref="RideScriptFile"/> reads the format,
/// <see cref="RideScript"/> runs it, <see cref="RideScriptScheduler"/> gives each one its turn and
/// <see cref="ParkItemCatalogue"/> knows where every item's files live; this binds a
/// <see cref="RideScript"/> to each thing standing in a running park whose item ships one.
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
/// <b>What this deliberately does not do yet: the SPEED word alone.</b> The original's object constructor
/// (<c>FUN_004db090</c>, after the binder returns) pushes the item's own operating speed into the script's speed
/// word (<c>FUN_0055a300</c>, field <c>+0xc0</c>) and its operating duration into variable 3, which is
/// <see cref="RideVariables.VAR_DURATION"/>, logging "SPEED = %d" and "DUR = %d" as it does. <b>The duration is
/// pushed</b> - from the save's own <c>mOperatingDuration</c>, beside the capacity, further down this file. The
/// speed stays out, because <see cref="RideScript"/> keeps no speed word (<c>docs/QUEUE.md</c> Q155): it is the
/// divisor <c>WAIT</c> scales by, exactly neutral only at the 50 the loader seeds, and the constructor pushes
/// <c>Upgrades[0].InitSpeed</c> into it when that is above nought (<c>0x004db51c</c>..<c>0x004db534</c>).
/// </para>
///
/// <para>
/// <b>What handing over the model includes.</b> A bound script gets its thing and that thing's animation
/// players (see <see cref="PlayersFor"/>): the very ones <see cref="ParkObjects.Sweep"/> advances and the model
/// is posed from, so the animation instructions answer real clip lengths instead of the engine's floor. As in
/// the engine, a trigger onto a busy channel queues behind the running clip and adds that clip's remaining time
/// to the length it answers, and <c>FLUSHANIM</c> clears the channel's <i>queued</i> role (<c>FUN_00473270</c>)
/// and nothing else, so the running clip plays out.
/// </para>
///
/// <para>
/// <b>Selling a thing takes its script down</b> - see <see cref="Unbind"/>, the object destructor's call of
/// the script teardown (<c>FUN_004dd0a0</c> at <c>0x004dd2c9</c>).
/// </para>
/// </summary>
public sealed class ParkRides : Entity
{
	/// <summary>The theme folder these were loaded from - "jungle", "fantasy", "hallow" or "space".</summary>
	public string ThemeName { get; }

	/// <summary>
	/// The scripts of the park currently loaded, or null outside one - the arrangement
	/// <see cref="ParkObjects.Current"/> already uses, and needed for the same reason: something bought
	/// after the park has loaded has to be given a script, and whoever builds it is not holding this.
	/// Cleared as the park ends - see <see cref="OnDelete"/>.
	/// </summary>
	public static ParkRides? Current { get; private set; }

	/// <summary>
	/// Gives a newly built thing its script, the way the load gives one to everything the save placed.
	/// Answers whether it got one - an item with no <c>.RSE</c> is ordinary data, not a failure.
	/// </summary>
	/// <remarks>
	/// The engine does this inside the object constructor itself (<c>FUN_004dcf90</c>, called from
	/// <c>0x004db517</c>), so a bought thing and a loaded one are running the same code. Here they are
	/// two call sites of the same three steps - spawn, bind the thing, hand over its own animation
	/// player - and the capacity and duration come from the object record exactly as they do at load.
	/// </remarks>
	public bool BindNew( ParkWorld.CatalogueObject placed, ParkItemCatalogue.Item item )
	{
		if ( _scripts.ContainsKey( placed.ThingId ) )
			return true;

		var id = Scheduler.Spawn( ScriptPathFor( item ) );

		if ( id == 0 )
		{
			++Scriptless;
			return false;
		}

		_scripts[placed.ThingId] = id;

		if ( Scheduler.Find( id ) is not { } script )
			return false;

		script.ThingId = placed.ThingId;
		script.Set( ParkRideOperation.CapacityVariable, placed.OperatingCapacity );
		script.Set( ParkRideOperation.DurationVariable, placed.OperatingDuration );

		// <b>Nothing is resumed here, and that asymmetry with the load is deliberate.</b> This is the
		// path a player takes by BUILDING the thing, which is the one moment its construction clip is meant
		// to play: the engine's build path (FUN_00463060) triggers role 0 and then role 13 at once, which
		// holds the clip at frame nought until the script, run from word 0, plays it. A save has state
		// to restore and a new thing has none, so calling Resume here would be putting back a past it never
		// had - and would stop the one animation a player is waiting to watch.
		script.Animations = PlayersFor( placed.ThingId, item );
		script.Nodes = NodesFor( script, placed, item );
		BindTrackRide( script, placed, item );

		if ( script.Animations.Loaded > 0 )
			_animated.Add( placed.ThingId );

		Log.Info( $"{ThemeName}: thing {placed.ThingId} ('{item.Name}') now runs {ScriptPathFor( item )}" );

		return true;
	}

	/// <summary>
	/// Takes a sold thing's script down, and whatever that script spawned with it - the object destructor's
	/// last word on its script (<c>0x004dd2c9</c>). Answers whether the thing had been given a script at all;
	/// one that never was is the original's id-nought no-op. See <c>docs/exe/park.md</c>, "What selling a
	/// thing does to its script".
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>The teardown is the one <c>REMOVECHILD</c> and a finished script already go through</b>,
	/// <see cref="RideScriptScheduler.Destroy"/>, with the mode it is called with while a park is played,
	/// <b>7</b>, which sets both bits the teardown reads. <c>0x2</c> spawns the item's
	/// <c>Info.DestroyParticleEffect</c> over the footprint, for the script named here and not for the ones it
	/// takes with it; it is counted rather than drawn, because nothing here draws a particle effect in the
	/// world rather than on the screen. <c>0x4</c> takes the heads <c>ADDHEAD</c> hung on the model off it,
	/// and there are none to take: nothing here hangs one.
	/// </para>
	/// <para>
	/// <b>A script that has already run off its end spawns nothing</b>, because the original's teardown finds
	/// no such id in its registry and returns. Its thing's id comes off the map either way.
	/// </para>
	/// </remarks>
	public bool Unbind( int thingId, ParkItemCatalogue.Item item )
	{
		if ( !_scripts.Remove( thingId, out var id ) )
			return false;

		_animated.Remove( thingId );

		var before = Scheduler.Count;
		var live = Scheduler.Find( id ) is not null;

		if ( live && item.DestroyParticleEffect != 0 )
			Unimplemented.Report( "DESTROY_PARTICLE_EFFECT" );

		Scheduler.Destroy( id );

		Log.Info( $"{ThemeName}: thing {thingId} ('{item.Name}') sold - script {id} " +
			(live ? $"torn down with {before - Scheduler.Count} script(s)" : "had already finished") +
			$", {Scheduler.Count} running" );

		return true;
	}

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

	/// <summary>How many things standing in the park were given a script - selling one takes it off.</summary>
	public int Bound => _scripts.Count;

	/// <summary>
	/// How many of those were also given a model with animations on it - the engine's <c>+0xc8</c>. It is
	/// counted rather than assumed because an item shipping no clips at all is ordinary data: the drinks
	/// shop ships none and its script names none either.
	/// </summary>
	public int Animated => _animated.Count;

	/// <summary>The things counted by <see cref="Animated"/>, so that selling one takes it back off.</summary>
	private readonly HashSet<int> _animated = [];

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

		// Before the two bails below, for the reason ParkObjects sets its own here: a park that places
		// nothing still runs scripts, and something bought afterwards has to be able to find this.
		Current = this;

		_files = files ?? FileSystem;
		_objects = objects;

		// The scripts' own way of reaching another script. It is set even where there is nothing to bind,
		// because it is the scheduler's property and not the park's.
		Scheduler.Loader = Load;

		if ( world == null || catalogue == null )
			return;

		// Said out loud, because the consequence is silent and looks exactly like the bug this fixes: with
		// no saved script state every thing starts at its own first instruction and builds itself again.
		// The model half warns from inside PairSavedThings for the same reason.
		if ( world.ScriptStates.Problem != null )
		{
			Log.Warning( $"{ThemeName}: the park file's script states would not read, so everything in it "
				+ $"starts from its own beginning and will replay its construction - {world.ScriptStates.Problem}" );
		}

		_saved = PairSavedThings( world, catalogue );

		// The moment this load stands for on the clock these scripts and their channels run on. It is the save's
		// own moment on the save's clock: the engine makes its clock read the saved reading again as the load
		// ends (FUN_00415140, 0x00415193), so everything the save measured against that clock keeps its distance
		// from now. See OnThisClock.
		_loaded = (int)(GameClock.Ticks * MillisecondsPerTick);
		_clock = world.Clock;

		if ( _clock.Problem != null )
		{
			Log.Warning( $"{ThemeName}: the park file's clock would not read, so every saved wait, timer and clip "
				+ $"starts afresh at the load - {_clock.Problem}" );
		}

		// The scheduler's own state as the park was saved: its tick, so each script keeps the phase of its one turn in
		// eight, and the next handle, so a script made from here on is numbered past every saved one.
		if ( world.ScriptStates.Problem == null )
			Scheduler.Restore( world.ScriptStates.Tick, world.ScriptStates.NextHandle );

		// In the order the module holds the scripts, which the engine's reader inserts one by one at the head of its
		// list (0x005599d3), so that the scheduler's newest-first walk takes their turns as the engine's does. A thing
		// with no saved script comes after, numbered afresh.
		// Only from a module that read whole, as the next handle is restored only then: one that stopped part-way
		// could have the counter hand out a handle a script it did not reach was saved under.
		var saveOrder = new Dictionary<int, int>();

		for ( var at = 0; world.ScriptStates.Problem == null && at < world.ScriptStates.Order.Count; ++at )
			saveOrder[world.ScriptStates.Order[at]] = at;

		var inSaveOrder = world.Objects
			.OrderBy( placed => saveOrder.TryGetValue( placed.RideScript, out var at ) ? at : int.MaxValue );

		foreach ( var placed in inSaveOrder )
		{
			// Everything this park actually stood up, which is not the same as everything it placed. The gate
			// and the traffic lights carry no position of their own - the engine builds those two by name out
			// of the item descriptions rather than from the save's placements (FUN_005156a0) - but they are
			// catalogue objects like any other and their scripts are bound by the very same constructor. So
			// what decides is whether this park has one standing, not whether the save gave it a cell. With
			// nothing drawn only IsPlaced decides, which is the case every test here exercises.
			if ( !placed.IsPlaced && _objects?.AnimationsFor( placed.ThingId ) is null )
				continue;

			// An id this theme has nothing for is already reported by ParkObjects, which cannot draw it
			// either. Saying so twice about one object would be noise.
			if ( !catalogue.TryGet( placed.CatalogueId, out var item ) )
				continue;

			// Under its saved handle, which the engine's reader keeps with the rest of the struct (FUN_005597a0): it
			// decides the script's turns and is the id every other script has stored for it. One the save does not
			// hold comes from the scheduler's counter, the engine's DAT_008791a8, which never reuses an id.
			var id = saveOrder.ContainsKey( placed.RideScript )
				? Scheduler.Spawn( ScriptPathFor( item ), placed.RideScript )
				: 0;

			if ( id == 0 )
				id = Scheduler.Spawn( ScriptPathFor( item ) );

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

				// <b>And what the ride can hold, which nothing else will ever tell its script.</b>
				// Bouncy.RSE declares VAR_CAPACITY and only ever READS it (one CMP against its rider
				// count); it carries no COAST instruction at all. The engine writes it, in FUN_004dd7f0 -
				// which logs "CAPACITY = %d", writes script variable 2, and stores the same number to the
				// object's mOperatingCapacity. That call sits in the object constructor (0x004db560) and on
				// the open-and-repair path (FUN_004df8f0), beside the writes of VAR_DURATION and the speed.
				//
				// Without it every variable starts at nought, so a ride's own turn reads capacity 0
				// against nought aboard, finds itself FULL, and refuses to invite anybody for ever.
				//
				// The SAVE's value is used unclamped on purpose. FUN_004dd7f0 clamps the wanted capacity
				// between the item description's own minimum and maximum and then stores the result in
				// mOperatingCapacity - so what the file holds is already the clamped answer, and applying
				// the rule again (against fields nothing here reads) would be doing it twice.
				// Through the constants rather than by spelling the names again here: two spellings of one
				// variable are two things that can drift.
				script.Set( ParkRideOperation.CapacityVariable, placed.OperatingCapacity );
				script.Set( ParkRideOperation.DurationVariable, placed.OperatingDuration );

				// And where this script had got to when the park was saved, which is the whole of why a
				// loaded park does not watch everything in it being built again. Started from its own first
				// instruction instead, the Belly Bounce would run WAITANIM 0 0, the construction clip, and hatch
				// out of its egg on every load.
				// See ParkScriptStates, and RideScript.ResumeAt for what it refuses.
				Resume( script, placed, world );

				// Its own thing's player where the thing is standing, so that what the script triggers and
				// what the model is posed from are the same one.
				script.Animations = PlayersFor( placed.ThingId, item );
				script.Nodes = NodesFor( script, placed, item );
				BindTrackRide( script, placed, item );

				if ( script.Animations.Loaded > 0 )
					_animated.Add( placed.ThingId );

				// And the other half of the restore, which has to come AFTER the player exists: putting
				// the script back without putting its model's channels back leaves ten of this park's
				// fourteen things frozen for good. See Restore.
				if ( _saved.TryGetValue( placed.ThingId, out var savedThing ) )
					Restore( script, savedThing );
			}
		}

		// <b>And the things this park stood that the save never named.</b> The loop above walks
		// world.Objects, so it reaches only what the file placed. A vehicle this park has not used is
		// not in that list at all - the engine makes the thing the first time a crowd of that size
		// arrives rather than shipping one - so without this pass the ferry and the seaplane would be stood
		// and drawn with no script, and sit at their spawns.
		//
		// Nothing new is needed to bind them: ParkFixedItems records which catalogue item each was
		// stood as, and the script path comes from that item exactly as it does above. A thing already
		// bound by the first pass is stepped over, which is how the gates and the lights - which ARE in
		// the save's list - fall out of this without being named here.
		if ( _objects is { } stood )
		{
			// What the first pass managed on its own, so that what this one adds is a measured
			// difference rather than a number inferred from a census counting a different population.
			//
			// Only the baseline is said here. What this pass then does is reported thing by thing
			// below, which is both more use and one fewer walk of an iterator that is about to be
			// walked anyway.
			Log.Info( $"{ThemeName}: {Bound} things bound from the save's own list, before the ones it "
				+ "does not name" );

			foreach ( var (thingId, catalogueId) in stood.Stood() )
			{
				// Already bound by the pass above, which is how the gates and the lights - which ARE in
				// the save's object list - step out of this without being named here.
				if ( _scripts.ContainsKey( thingId ) )
				{
					Log.Info( $"{ThemeName}: thing {thingId} (catalogue {catalogueId}) was already bound" );
					continue;
				}

				// Said rather than skipped in silence: a thing standing in the park as a catalogue
				// number this theme has no item for can never be given a script, and the only symptom
				// is that it never moves - which looks exactly like a thing that is bound and idle.
				if ( !catalogue.TryGet( catalogueId, out var item ) )
				{
					Log.Warning( $"{ThemeName}: thing {thingId} stands as catalogue item {catalogueId}, "
						+ "which this theme has no description for, so it can run no script" );

					++Scriptless;
					continue;
				}

				var id = Scheduler.Spawn( ScriptPathFor( item ) );

				if ( id == 0 )
				{
					++Scriptless;
					continue;
				}

				_scripts[thingId] = id;

				Log.Info( $"{ThemeName}: thing {thingId} (catalogue {catalogueId}) now runs "
					+ ScriptPathFor( item ) );

				if ( Scheduler.Find( id ) is not { } script )
					continue;

				script.ThingId = thingId;

				script.Animations = PlayersFor( thingId, item );

				if ( script.Animations.Loaded > 0 )
					_animated.Add( thingId );
			}
		}

		// And tell the gate whether this park is open - one of several writes of a script variable from
		// outside a script, beside the capacity and duration above and ride operation's VAR_LETMEON and
		// VAR_LETMEOFF. Last, because it needs the binding above to have run.
		CommandTheGate( world );

		// The restore counts go in this line because otherwise nothing anywhere reports them. A park whose
		// saved state stops reading does not fail: every script quietly starts at its own first
		// instruction again and the whole park replays its construction, which is the exact fault this
		// class exists to prevent - and with no count printed, the only way to notice is to watch it.
		Log.Info( $"{ThemeName}: {Bound} of the park's things are running a script" +
			(Scriptless > 0 ? $", and {Scriptless} have none to run" : "") +
			$"; {Animated} of them can see their own animations" +
			$"; {Resumed} resumed where the save left them" +
			(NotResumed > 0 ? $" and {NotResumed} did not" : "") +
			$"; {ChannelsRestored} animation channels put back" +
			$"; {KeptReadingsMoved} kept clock readings moved" );
	}

	/// <summary>
	/// Lets go of <see cref="Current"/>, which reaches the whole of the level it was made in through
	/// <see cref="Entity.Level"/> - its save, its running state and its interface - and would otherwise keep a left
	/// park in memory through the lobby. An entity, so this runs in the entity pass of <see cref="Level.Unload"/>,
	/// after an open ride window has written its settings through <see cref="Current"/> as the interface closed.
	/// </summary>
	protected override void OnDelete()
	{
		if ( Current == this )
			Current = null;
	}

	/// <summary>The gate's command variable, by the name its own script declares it under.</summary>
	private const string GateCommand = "VAR_COMMAND";

	/// <summary>Open the gate. <c>FUN_00519ef0</c> writes this when a park is opened.</summary>
	private const int OpenTheGate = 1;

	/// <summary>
	/// What a park saved closed commands: <b>2</b>, the value only the end-of-park path writes
	/// (<c>0x00519f40</c>). The door's own close writes 0, and only with nobody in the park - see the
	/// remarks below.
	/// </summary>
	private const int ShutTheGate = 2;

	/// <summary>
	/// Tells the park's gate whether the park is open, which is what makes it move at all.
	///
	/// <para>
	/// <b>The gate moves only when it is commanded.</b> <c>Gates.RSE</c> opens on a dispatch loop that reads
	/// <c>VAR_COMMAND</c>; with nought there it cycles five instructions for ever and reaches neither the open
	/// branch nor the close one. The only thing in the original that ever writes that variable is
	/// opening or closing a park - <c>FUN_00519ef0</c>, which looks the gate's script up from the header's
	/// own <c>mParkGates</c> handle and writes variable 0.
	/// </para>
	/// <para>
	/// <b>The values are read off the disassembly rather than the decompile.</b> The call sites push an extra
	/// argument that survives one call and is consumed by the next, so Ghidra hangs each value on the wrong
	/// call. Read as instructions, opening the park writes <b>1</b>; closing it writes <b>0</b>, and only when
	/// nobody is in the park and the gate reads open (<c>0x0051a0e8</c>..<c>0x0051a161</c>); <b>2</b> is written
	/// only by the end-of-park path (<c>0x00519f40</c>). Commanding 2 for a park saved closed is a stand-in for
	/// the door's close, whose 0 is not decoded on the script's side (<c>docs/exe/lobby.md</c>).
	/// </para>
	/// <para>
	/// <b>Commanding it as the park loads is a reproduction of the end state, not of a call anybody has
	/// traced.</b> The original restores the script's variables with the rest of the save (<c>FUN_005597a0</c>,
	/// see <see cref="Resume"/>); whether it also re-commands at load is not established here. What is
	/// established is that this park is saved open, so its gate belongs open; doing it this way makes the
	/// screen agree with <c>mParkClosed</c>.
	/// </para>
	/// </summary>
	private void CommandTheGate( ParkWorld world )
	{
		var id = ScriptFor( world.ParkGates );

		if ( id == 0 || Scheduler.Find( id ) is not { } gate )
			return;

		// Zero is open - see ParkWorld.ParkClosed.
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
	/// is the same trap that hangs the gate command's values on the wrong call.
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
	/// How many scripts were put back where the save left them, rather than started from the beginning.
	/// </summary>
	public int Resumed { get; private set; }

	/// <summary>
	/// How many were left at their own first instruction because the save had nothing usable for them.
	///
	/// <para>
	/// <b>Not a failure on its own.</b> A script the save never ran has no saved state to restore, and
	/// starting at the beginning is exactly right for it - that is what happens to anything the player
	/// builds while playing. It is only worth reading beside <see cref="Resumed"/>: in the shipped park
	/// every one of the fourteen placed things has a saved counter, so a large number here means the
	/// script module stopped being readable and everything is about to replay its construction.
	/// </para>
	/// </summary>
	public int NotResumed { get; private set; }

	/// <summary>
	/// How many clock readings the resumed scripts kept in their variables or result registers, moved onto this
	/// park's clock (<see cref="RideScript.MoveKeptReadings"/>). The shipped park keeps none.
	/// </summary>
	public int KeptReadingsMoved { get; private set; }

	/// <summary>
	/// Puts one thing's animation channels back where the park file left them.
	///
	/// <para>
	/// <b>Without this, resuming the script alone is a net loss.</b> A thing whose steady-state loop holds
	/// no animation instruction never reaches the <c>LOOPANIM</c> in its prologue again, so it stands
	/// frozen for good: measured on the shipped park, ten of the fourteen placed things - the Fountain,
	/// the Coconut Kiosk, all three lanes of the Jungle Spray, the Traffic Lights, the Gates, the Staff
	/// Room, the Litter Bin and the three Toilets - animated before the script counter was restored and
	/// not at all after it. The original restores both halves, and so does this.
	/// </para>
	///
	/// <para>
	/// <b>The saved flags are carried through and they matter.</b> Bit <c>0x1</c> loops; bit <c>0x4</c> is
	/// what makes a channel hold its last frame rather than count as busy, which is what the Gates, the
	/// Toilets, the Jungle Spray and the Bus are all saved doing. Dropping them would restart those clips
	/// as ordinary one-shots and lose the held pose.
	/// </para>
	///
	/// <para>
	/// <b>And the clip resumes where it was</b>: the engine copies the saved time stamps and queue back
	/// (<c>FUN_004647a0</c>, <c>0x00464bcb</c>..<c>0x00464c17</c>) against a clock the load puts back, so its
	/// next advance works the frame out from them. Here the stamps are moved onto this park's clock by their
	/// distance from the save's moment (<see cref="Moved"/>), and a queued clip is queued again. The speed
	/// comes back too: fourteen of the fifteen running channels are saved at 1, and the Belly Bounce at
	/// <b>1.1</b>.
	/// </para>
	///
	/// <para>
	/// <b>How long a restored speed lasts, measured rather than assumed.</b> It survives loop wraps and
	/// holds, because <see cref="RideAnimations.Advance"/> carries the channel's own speed into both -
	/// but the next trigger from the script replaces it, since <c>RideScript.StartAnimation</c> passes 1.0
	/// where the engine's handlers push the script's speed divisor, 0.5 + 0.01 x the speed word
	/// (<c>0x00552be4</c>) - the deviation docs/QUEUE.md Q155 names. The Belly Bounce's script meets its
	/// <c>LOOPANIM 2, 0</c> at word 43 with the saved looping key 2, so it skips it as the engine does and the
	/// saved loop plays on at 1.1.
	/// </para>
	/// </summary>
	private void Restore( RideScript script, SavedThing saved )
	{
		if ( script.Animations is not { } players )
			return;

		for ( var index = 0; index < saved.Channels.Length && index < players.ChannelCount; ++index )
		{
			var channel = saved.Channels[index];

			// What was queued behind it, which the next advance promotes when the clip ends; the engine copies the
			// queue whatever the channel holds, and an idle one's waits for a trigger to start something first.
			if ( channel.QueuedRole != ParkThingStates.NoRole )
				players.Channel( index )?.Queue( channel.QueuedRole, channel.QueuedEntry, channel.QueuedFlags, channel.QueuedSpeed );

			// The sentinel is the engine's own "this channel was running nothing", and it is the common
			// case: of the 163 channels the shipped park saves, 148 hold it, and none holds a queue.
			if ( channel.Role == ParkThingStates.NoRole )
				continue;

			// <b>The saved word is the engine's INTERNAL flag field, and only two of its bits mean the
			// same thing to a caller.</b> 0x1 (loop) and 0x8 (do not apply the hide list) carry across
			// unchanged; 0x2 and 0x4 do not. Internally those two say the channel was FROZEN at frame
			// nought or HELD at its last frame, and a caller's 0x2 and 0x4 mean "start at once" and "do
			// not lay the rest pose down" - different questions entirely. Passed through, the word would
			// read a held channel as a keep-pose request, and AnimTimeControl.Start clears 0x6 on the way
			// in, so the hold would be dropped and the clip restart from frame nought as an ordinary
			// one-shot. Eleven of this park's fifteen restored channels carry 0x4.
			// A channel saved as running carries a real speed; nought only ever appears on one that was
			// not, and those are skipped above. Floored anyway, because a nought here would stop the
			// clip dead rather than play it slowly.
			var speed = channel.Speed > 0f ? channel.Speed : 1f;

			players.Trigger( channel.Role, channel.Entry,
				channel.Flags & (AnimTimeControl.LoopFlag | AnimTimeControl.KeepShownFlag),
				speed, _loaded, index );

			// And then the state it was left in. The engine's restore copies the saved word whole and adds
			// 0x10 to a held channel; here the held or frozen state is re-entered through its pseudo-role
			// instead, which sets the same bits and pins the frame (docs/exe/ride-operation.md, the RSYS
			// restore). The speed passed is not read on that path.
			if ( (channel.Flags & HeldAtEnd) != 0 )
				players.Trigger( AnimTimeControl.HoldAtEnd, 0, AnimTimeControl.KeepShownFlag, speed, _loaded, index );
			else if ( (channel.Flags & FrozenAtStart) != 0 )
				players.Trigger( AnimTimeControl.FreezeAtStart, 0, AnimTimeControl.KeepShownFlag, speed, _loaded, index );

			if ( players.Channel( index ) is not { } player )
				continue;

			// The stamps where the save left them, so the clip goes on from the frame it had reached rather than
			// from nought. A channel the trigger above parked (a role or clip its model lacks) keeps nothing.
			if ( !player.IsIdle
				&& Moved( channel.StartTime ) is { } start
				&& Moved( channel.Time ) is { } time
				&& Moved( channel.NoPauseTime ) is { } noPause )
			{
				player.Restamp( start, time, noPause );
			}

			++ChannelsRestored;
		}
	}

	/// <summary>How many animation channels were put back where the save left them.</summary>
	public int ChannelsRestored { get; private set; }

	/// <summary>
	/// The bit a saved channel carries when it was HELD on its last frame - the engine's own internal
	/// mark, set by <c>AnimTimeControl.Start</c> as part of <c>0x14</c> for the hold pseudo-role. It is
	/// <b>not</b> the caller flag of the same value, which asks for something else entirely.
	/// </summary>
	private const int HeldAtEnd = 0x4;

	/// <summary>The same, for a channel frozen at frame nought. No record in Lost Kingdom carries it.</summary>
	private const int FrozenAtStart = 0x2;

	/// <summary>What the save says each thing's model was doing, by thing id - empty where it would not read.</summary>
	private Dictionary<int, SavedThing> _saved = [];

	/// <summary>
	/// Matches each placed thing to its saved model state.
	///
	/// <para>
	/// <b>The module names an ITEM, not a thing</b>, so three Small Toilets are three records that read
	/// alike and something has to say which is which. In the shipped park the records of placed things
	/// run in ascending script-handle order, so pairing them off in that order lines them up - and this
	/// walks the objects in that order deliberately, because <see cref="ParkWorld.Objects"/> is in the
	/// file's own order, which is the reverse. Pairing in the order the objects happen to arrive would
	/// hand the toilets each other's records.
	/// </para>
	/// <para>
	/// <b>Within one catalogue id the pairing is unobservable</b> - those records in this park differ only in
	/// their time stamps, which a held channel does not show - so this is an ordering that matches rather than a
	/// decoded thing handle, and nothing here relies on telling two of a kind apart.
	/// </para>
	/// </summary>
	private Dictionary<int, SavedThing> PairSavedThings( ParkWorld world, ParkItemCatalogue catalogue )
	{
		var paired = new Dictionary<int, SavedThing>();

		// How many channels an item runs at once - the one thing the module does not carry, and without
		// which its records cannot be stepped over at all.
		var states = world.ThingStates(
			id => catalogue.TryGet( id, out var item ) ? item.AnimationChannels : 1 );

		if ( states.Problem != null )
		{
			Log.Warning( $"{ThemeName}: the park file's model states would not read, so everything in it "
				+ $"keeps whatever its construction left, and its scripts' looping keys start afresh - {states.Problem}" );

			return paired;
		}

		var taken = new Dictionary<int, int>();

		foreach ( var placed in world.Objects.OrderBy( o => o.RideScript ) )
		{
			var ordinal = taken.TryGetValue( placed.CatalogueId, out var seen ) ? seen : 0;

			if ( states.For( placed.CatalogueId, ordinal ) is { } saved )
				paired[placed.ThingId] = saved;

			taken[placed.CatalogueId] = ordinal + 1;
		}

		return paired;
	}

	/// <summary>
	/// Puts one script back where the park file left it - its counter, its variables, its stack with both
	/// its indices, its result register and its name.
	///
	/// <para>
	/// <b>This is what stops a loaded park building itself all over again.</b> The original restores each
	/// script's whole record (<c>FUN_005597a0</c>) rather than guarding the construction clip anywhere, so
	/// a script resumes mid-flight; started from nought instead, the Belly Bounce runs the
	/// <c>WAITANIM 0 0</c> at word 4 and hatches out of its egg on every single load.
	/// </para>
	///
	/// <para>
	/// <b>It is only half of a restore, and the other half is not optional.</b> Resuming a script past its
	/// prologue means it never runs the <c>LOOPANIM</c> in it again, so without <see cref="Restore"/>
	/// putting the model's channels back, ten of this park's fourteen things stand frozen for good.
	/// </para>
	///
	/// <para>
	/// <b>The save wins over the capacity and duration written just above.</b> In the shipped park the two
	/// agree exactly - the file's own variable slots hold 5 and 30, and so do the object record's
	/// <c>mOperatingCapacity</c> and <c>mOperatingDuration</c> - so today this changes nothing either way.
	/// Where a future park disagreed, what it was saved holding is the better answer for a park being
	/// loaded, which is why this runs second rather than first.
	/// </para>
	///
	/// <para>
	/// <b>And its waits</b>, which the engine reads back with the rest of the struct: the <c>WAIT</c> and
	/// <c>WAITANIM</c> deadline, the <c>WAIT4ANIM</c> deadline, the looping key, <c>TRIGWAITANIM</c>'s mark and the
	/// <c>SETTIMER</c> deadline, each deadline moved onto this park's clock (<see cref="OnThisClock"/>), and the key
	/// and the mark only for a thing whose channels <see cref="Restore"/> puts back. The shipped
	/// park reaches two on every load: the security cameras are saved on <c>WAIT 5000</c> with 2,341 and 2,329 ms
	/// left, and the Belly Bounce with its looping key 2, so its <c>LOOPANIM 2, 0</c> at word 43 does nothing.
	/// </para>
	///
	/// <para>
	/// <b>Not its scream.</b> The engine reads the saving session's handle at <c>+0xd0</c> back with the struct,
	/// so a script saved screaming resumes holding a handle to no voice of this session; here it resumes
	/// holding nothing. Every path from a saved state reaches a <c>STOPSCREAM</c> before a <c>STARTSCREAM</c>,
	/// so the refusal that handle could cause is never reached (docs/exe/ride-operation.md, "How a scream VARIES").
	/// </para>
	/// </summary>
	private void Resume( RideScript script, ParkWorld.CatalogueObject placed, ParkWorld world )
	{
		if ( world.ScriptStates.For( placed.RideScript ) is not { } saved )
		{
			++NotResumed;
			return;
		}

		// The name first, because resuming steps over the NAME every one of these scripts opens with, and
		// a nameless script cannot be found by FINDSCRIPTRAND - see RideScript.TakeDeclaredName.
		script.TakeDeclaredName();

		// A script whose saved array is a different length from the one it declares is not the script this
		// record belongs to. Said rather than silently truncated, because the slots would still be written.
		if ( saved.Variables.Length != script.Variables.Count )
		{
			Log.Info( $"{ThemeName}: thing {placed.ThingId} declares {script.Variables.Count} variables and "
				+ $"the save holds {saved.Variables.Length} for script handle {saved.Handle}" );
		}

		var slots = Math.Min( saved.Variables.Length, script.Variables.Count );

		for ( var slot = 0; slot < slots; ++slot )
			script.SeedVariable( slot, saved.Variables[slot] );

		// Only if the position is real: an unknown one would stop the script dead rather than erring, so a
		// thing that cannot be resumed is better left running from its beginning, with the loader's stacks.
		if ( !script.ResumeAt( saved.Position ) )
		{
			Log.Warning( $"{ThemeName}: thing {placed.ThingId} was saved at word {saved.Position} of "
				+ $"{saved.BodyWords}, which is not the start of an instruction, so it starts from the beginning" );

			++NotResumed;
			return;
		}

		// With the counter, the stack and the register it was saved with: a ride saved mid-cycle has its
		// riders' handles on the heap, and a branch it resumes before reads the register. The saved block's
		// length is the stack's size, as the engine takes it (FUN_005597a0, 0x00559af1).
		if ( saved.Stack.Length != script.Stack.Count )
		{
			Log.Info( $"{ThemeName}: thing {placed.ThingId} declares a stack of {script.Stack.Count} and the "
				+ $"save holds {saved.Stack.Length} for script handle {saved.Handle}" );
		}

		script.RestoreStacks( saved.Stack, saved.CallIndex, saved.HeapIndex, saved.Result );

		// The looping key and the mark answer for what channel 0 is playing, so they come back only with the
		// channels. Where the save's model states would not read, or hold no record for this thing, its channels start
		// idle, and a restored key would make its next LOOPANIM of that loop do nothing over a channel playing none -
		// a gap of this reader's, since the engine always reads both.
		var channels = _saved.ContainsKey( placed.ThingId );

		script.RestoreClockState( OnThisClock( saved.WaitDeadline ), OnThisClock( saved.AnimationDeadline ),
			channels ? saved.LoopingKey : script.LoopingKey, channels ? saved.AnimationMark : script.AnimationMark,
			OnThisClock( saved.TimerDeadline ) ?? 0f );

		// And the deadlines it keeps in its own variables, a deviation said at RideScript.MoveKeptReadings.
		// Only where the save's clock reads, as the struct's own deadlines are moved.
		var kept = _clock?.Reading is not null ? script.MoveKeptReadings( reading => Moved( unchecked((uint)reading) )!.Value ) : 0;

		if ( kept > 0 )
			Log.Info( $"{ThemeName}: thing {placed.ThingId} (script {saved.Handle} at word {saved.Position}) had {kept} kept clock readings moved" );

		KeptReadingsMoved += kept;

		++Resumed;
	}

	/// <summary>
	/// A saved deadline moved onto the clock this park's scripts run on (<see cref="Moved"/>), or null for nought,
	/// the engine's empty slot.
	/// </summary>
	private float? OnThisClock( uint reading ) => reading != 0 ? Moved( reading ) : null;

	/// <summary>
	/// A reading of the save's clock moved onto the clock this park's scripts and channels run on: the load's
	/// moment here, plus how far the reading lay from the save's moment there (<see cref="ParkClock.Since"/>).
	/// Null where the save's clock would not read.
	///
	/// <para>
	/// <b>Moved, not copied, and that is a deviation.</b> The engine puts its clock back to the saved reading, so
	/// every saved reading means what it meant; this park's clock is the tick count since the game began, and a
	/// float, which at a saved reading's size (114,374,804 ms in the shipped park) would hold it only to 8 ms. So each
	/// deadline and stamp keeps its distance from the save's moment instead, which is what any wait or clip compares.
	/// A deadline a script kept in a variable (<c>GETTIME</c> into <c>VAR_STARTNOW</c>) is moved the same way
	/// (<see cref="RideScript.MoveKeptReadings"/>).
	/// </para>
	/// </summary>
	private int? Moved( uint reading ) => _clock?.Since( reading ) is { } since ? _loaded + since : null;

	/// <summary>The load's moment on this park's clock - see <see cref="Moved"/>.</summary>
	private int _loaded;

	/// <summary>The load's moment on the clock this park's scripts run on, which the save's own moment maps to.</summary>
	internal int LoadedAt => _loaded;

	/// <summary>The save's clock, or null where no save was given.</summary>
	private ParkClock? _clock;

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
	/// The animation players a bound script triggers: its thing's own where the thing stands, so that what
	/// the script triggers and what the model is posed from are one, or a set read afresh where nothing
	/// stands, which is what a test binding scripts against a park it never builds is doing.
	/// </summary>
	/// <remarks>
	/// <b>Nothing advances a set read afresh</b> - only <see cref="ParkObjects.Sweep"/> does, over what stands
	/// - and a trigger asks a channel as the last advance left it, so on such a set no clip ever ends and every
	/// trigger onto a busy channel queues for good. Said in a drawn park, where it means a thing whose model
	/// would not stand.
	/// </remarks>
	private RideAnimations PlayersFor( int thingId, ParkItemCatalogue.Item item )
	{
		if ( _objects?.AnimationsFor( thingId ) is { } standing )
			return standing;

		if ( _objects is not null )
		{
			Log.Warning( $"{ThemeName}: thing {thingId} ('{item.Name}') is not standing, so nothing advances "
				+ "its animations and no clip it triggers ever ends" );
		}

		return RideAnimations.Load( item.Directory, item.Stem, _files, item.AnimationChannels );
	}

	/// <summary>
	/// The model nodes a bound script walks its riders between, standing where its thing stands - read only for a
	/// script that declares walk slots, the one family that asks (<see cref="RideNodes"/>). Null where the model will
	/// not read, which leaves every leg the shortest, counted.
	/// </summary>
	/// <remarks>
	/// The fixed items bound from what stands rather than from the save are given none: no fixed item's script
	/// declares a walk slot.
	/// </remarks>
	private RideNodes? NodesFor( RideScript script, ParkWorld.CatalogueObject placed, ParkItemCatalogue.Item item )
	{
		if ( script.WalkSlots == 0 )
			return null;

		RideNodes? nodes;

		try
		{
			nodes = RideNodes.Load( item.Directory, item.Stem, _files, item.DoHeadProcessing,
				script.Animations?.AllClips ?? [] );
		}
		catch ( Exception e )
		{
			Log.Warning( $"{ThemeName}: thing {placed.ThingId} ('{item.Name}') walks riders but its model will not "
				+ $"read, so every leg is the shortest - {e.Message}" );

			return null;
		}

		if ( nodes is null )
		{
			Log.Warning( $"{ThemeName}: thing {placed.ThingId} ('{item.Name}') walks riders but has no model, so every "
				+ "leg is the shortest" );

			return null;
		}

		nodes.Place( ParkObjects.OriginFor( placed.CellX, placed.CellY, placed.Angle ), placed.Angle );

		return nodes;
	}

	/// <summary>
	/// What <c>BUMP</c> works on: the thing's track-ride handle and <c>mIsTrackRideValid</c>, and the park's bumper rides,
	/// with the ride's arena put where the thing stands (<see cref="ParkBumperCars.ArenaCentre"/>).
	/// </summary>
	private static void BindTrackRide( RideScript script, ParkWorld.CatalogueObject placed, ParkItemCatalogue.Item item )
	{
		script.TrackRide = placed.TrackRide;
		script.TrackRideValid = placed.IsTrackRideValid;

		if ( placed.TrackRide == 0 || ParkState.Current?.TrackRides.Cars is not { } cars )
			return;

		script.Bumpers = cars;

		var (x, z) = ParkBumperCars.ArenaCentre( item, placed.CellX, placed.CellY, placed.Angle );
		cars.Place( placed.TrackRide, x, z );

		// The speed word the object constructor pushes (0x004db534), which the scheduler then pushes into the ride's
		// performance at each visit: the item's starting speed when over nought, else the loader's 50.
		cars.BindSpeedWord( placed.TrackRide, item.InitSpeed > 0 ? item.InitSpeed : 50 );
	}

	/// <summary>
	/// One tick for every 31ms that has come due, which is where the original runs the whole script
	/// system: <c>FUN_005516b0</c>, called once per park tick - after the track tick, which counts the bumper cars'
	/// goes (<see cref="ParkBumperCars.Tick"/>).
	/// </summary>
	protected override void OnUpdate()
	{
		for ( int i = 0; i < GameClock.TicksDue; ++i )
		{
			ParkState.Current?.TrackRides.Cars.Tick();
			Scheduler.Advance( MillisecondsAt( i ) );
			ParkState.Current?.TrackRides.Cars.PushSpeedWords();
		}

		// And then, once, whatever those ticks asked for is shown. The engine advances its animation players
		// in the scene draw (on-screen models from their scene-node callback, the rest by FUN_0044e410( 3 )),
		// past the back edge of this very catch-up loop, off one snapshot of the clock - so the sweep belongs after the loop rather than inside it,
		// and takes the moment the last tick ran at, which is exactly GameClock.Ticks beats in.
		_objects?.Sweep( (int)(GameClock.Ticks * MillisecondsPerTick) );
	}

	/// <summary>
	/// A tick's own instant, in the milliseconds the interpreter counts in - <c>WAIT</c> adds its operand
	/// to this clock unchanged, and the engine's is a millisecond counter ending at <c>timeGetTime</c>.
	///
	/// <para>
	/// <b>A deliberate deviation, kept by Alexah's decision</b> (<c>docs/exe/park.md</c>, difference 6). The
	/// engine hands every tick of a catch-up the frame's one instant (<c>now</c> read at <c>0x0054f47f</c>,
	/// above the loop), so a script's <c>WAIT</c> ends only on a frame boundary and a clip triggered
	/// mid-catch-up starts from nought: its timing hangs on the frame rate, about 7 ms late at 144 fps and up
	/// to a frame at 10. Here each tick's instant is 31 ms after the last, whatever the frame rate.
	/// </para>
	/// <para>
	/// Worked back from the tick number rather than read from <see cref="GameClock.Now"/>, which is one
	/// value for the whole frame. By the time an entity updates, <see cref="GameClock.Ticks"/> already
	/// counts this frame's ticks, so the first of them is that many less <see cref="GameClock.TicksDue"/>,
	/// plus one.
	/// </para>
	/// </summary>
	private static float MillisecondsAt( int index )
		=> (GameClock.Ticks - GameClock.TicksDue + 1 + index) * MillisecondsPerTick;

	/// <summary>The beat, in the units the scripts wait in - see <see cref="GameClock.TickSeconds"/>.</summary>
	private const float MillisecondsPerTick = GameClock.TickSeconds * 1000f;
}
