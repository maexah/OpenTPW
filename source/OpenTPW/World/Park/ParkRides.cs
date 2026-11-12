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
/// <b>The speed word is the script's own.</b> The original's object constructor (<c>FUN_004db090</c>, after the binder
/// returns) pushes the item's own operating speed into the script's speed word (<c>FUN_0055a300</c>, field <c>+0xc0</c>,
/// <see cref="RideScript.Speed"/>) when <c>Upgrades[0].InitSpeed</c> is above nought (<c>0x004db51c</c>..<c>0x004db534</c>) and its
/// operating duration into variable 3, which is <see cref="RideVariables.VAR_DURATION"/>, logging "SPEED = %d" and "DUR = %d" as
/// it does. Both are pushed here (<see cref="PushItemSpeed"/>; the duration beside the capacity, further down this file), and a load puts
/// the file's word back over the item's (<see cref="Resume(RideScript, SavedScript, string, bool, ParkWorld)"/>).
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
	/// Answers whether it got one - an item with no <c>.RSE</c> is ordinary data, not a failure. The script is bound to
	/// <paramref name="animations"/> where given, a made vehicle's whose model stands later in the frame, else to what
	/// stands as the thing.
	/// </summary>
	/// <remarks>
	/// The engine does this inside the object constructor itself (<c>FUN_004dcf90</c>, called from
	/// <c>0x004db517</c>), so a bought thing and a loaded one are running the same code. Here they are
	/// two call sites of the same three steps - spawn, bind the thing, hand over its own animation
	/// player - and the capacity and duration come from the object record exactly as they do at load.
	/// </remarks>
	public bool BindNew( ParkWorld.CatalogueObject placed, ParkItemCatalogue.Item item, RideAnimations? animations = null )
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
		PushItemSpeed( script, item );

		// <b>Nothing is resumed here, and that asymmetry with the load is deliberate.</b> This is the
		// path a player takes by BUILDING the thing, which is the one moment its construction clip is meant
		// to play: the engine's build path (FUN_00463060) triggers role 0 and then role 13 at once, which
		// holds the clip at frame nought until the script, run from word 0, plays it. A save has state
		// to restore and a new thing has none, so calling Resume here would be putting back a past it never
		// had - and would stop the one animation a player is waiting to watch.
		script.Animations = animations ?? PlayersFor( placed.ThingId, item );
		script.Nodes = NodesFor( script, placed, item );
		BindTrackRide( script, placed, item, Scheduler );

		// The engine's script loader starts the item's create effect over the cells its model covers as a bought
		// thing's script starts (FUN_005587f0, 0x00558e7c; docs/exe/park.md, "What building a thing gives off").
		// A fixed item stands on no cells of its own here, where the original's record holds cell (0,0) alone: counted. Dead by
		// CONTENT: every fixed item of the four themes declares Create 0 (FileFormats sam.md, "The particle effects an item gives
		// off"), and a made vehicle's model is stood only at the next ParkFixedItems.Sync, so it would take the branch below.
		if ( item.CreateParticleEffect != 0 )
		{
			if ( _objects is { } objects && objects.ModelFor( placed.ThingId ) is not null && objects.StoredFootprint( placed.ThingId ) is null )
			{
				Unimplemented.Report( "CREATE_PARTICLE_EFFECT_FIXED_ITEM" );
				Log.Info( $"{ThemeName}: thing {placed.ThingId} ('{item.Name}') built: its create effect " +
					$"{item.CreateParticleEffect} not started, a fixed item covers no cells (CREATE_PARTICLE_EFFECT_FIXED_ITEM)" );
			}
			else
			{
				var handle = ParkCreateEffect.Start( item, placed );

				Log.Info( $"{ThemeName}: thing {placed.ThingId} ('{item.Name}') built: its create effect {item.CreateParticleEffect} " +
					(handle != 0 ? $"started, handle 0x{handle:x}" : "not started (no particle system, ground or .hmp)") );
			}
		}

		// A thing made here has no file's words to start from: a fresh model's, kept from its first clip on.
		if ( TableModel( placed.CatalogueId ) is { } fresh )
			script.Animations.Nodes = new ParkModelTables.Running( fresh.Model, script.Animations.ChannelCount );

		// Drawn so from this frame on, as its construction begins, not finished, until the script's first tick plays it.
		if ( script.Animations.StandOnBuildClip( NowMilliseconds ) )
			_objects?.PoseNow( placed.ThingId );

		if ( script.Animations.Loaded > 0 )
			_animated.Add( placed.ThingId );

		Log.Info( $"{ThemeName}: thing {placed.ThingId} ('{item.Name}') now runs {ScriptPathFor( item )}" );

		return true;
	}

	/// <summary>
	/// Gives a fixed item made at its summons its item's script from word 0, its standing animation players and its model's
	/// nodes standing as the file holds them (<see cref="RideNodes.PlaceFixed"/>): the End feature
	/// (<c>World_MakeEndFeature</c>, <c>0x00516b00</c>; <see cref="ParkEndFeature"/>). Answers the script, or null where
	/// none would run. <see cref="Unbind"/> takes it down.
	/// </summary>
	public RideScript? BindFixed( int thingId, ParkItemCatalogue.Item item )
	{
		if ( _scripts.ContainsKey( thingId ) )
			return null;

		var id = Scheduler.Spawn( ScriptPathFor( item ) );

		if ( id == 0 || Scheduler.Find( id ) is not { } script )
		{
			++Scriptless;
			return null;
		}

		_scripts[thingId] = id;

		script.ThingId = thingId;
		script.Animations = PlayersFor( thingId, item );

		if ( script.WalkSlots != 0 || script.UsesHeads || script.UsesEffectNodes )
		{
			try
			{
				script.Nodes = RideNodes.Load( item.Directory, item.Stem, Files, item.DoHeadProcessing,
					script.Animations.AllClips );
				script.Nodes?.PlaceFixed();
			}
			catch ( Exception e )
			{
				Log.Warning( $"{ThemeName}: thing {thingId} ('{item.Name}')'s model will not read, so its effects stand on "
					+ $"no node - {e.Message}" );
			}
		}

		if ( script.Animations.Loaded > 0 )
			_animated.Add( thingId );

		Log.Info( $"{ThemeName}: thing {thingId} ('{item.Name}') now runs {ScriptPathFor( item )}, standing as its file holds it" );

		return script;
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
	/// <see cref="RideScriptScheduler.Destroy"/>, which takes no mode; the engine's teardown is called with one, in play
	/// <b>7</b>, which sets both bits the teardown reads. <c>0x2</c> spawns the item's
	/// <c>Info.DestroyParticleEffect</c> over the footprint, for the script named here and not for the ones it
	/// takes with it; it is counted rather than drawn, because nothing here draws a particle effect in the
	/// world rather than on the screen. <c>0x4</c> takes the heads <c>ADDHEAD</c> hung on the model off it,
	/// and a walk's with them, which here go with the script: a head is drawn from a live script's table and walk
	/// slots (<see cref="RideScript.Heads"/>, <see cref="RideScript.WalkHeads"/>).
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
	private readonly BaseFileSystem? _files;

	/// <summary>The one passed in, or the game's as it stood when this was made; read with neither, it says so.</summary>
	private BaseFileSystem Files => _files ?? throw new InvalidOperationException( "The rides were made with no file system to read" );

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
	public ParkRides( string themeName, IParkInitialState? world, ParkItemCatalogue? catalogue, BaseFileSystem? files = null,
		ParkObjects? objects = null )
	{
		ThemeName = themeName;
		Name = $"{themeName} ride scripts";

		// Before the two bails below, for the reason ParkObjects sets its own here: a park that places
		// nothing still runs scripts, and something bought afterwards has to be able to find this.
		Current = this;

		_files = files ?? FileSystemOrNull;
		_objects = objects;
		_catalogue = catalogue;

		// The scripts' own way of reaching another script. It is set even where there is nothing to bind,
		// because it is the scheduler's property and not the park's.
		Scheduler.Loader = Load;

		if ( world == null || catalogue == null )
			return;

		// Said out loud, because the consequence is silent and looks exactly like the bug this fixes: with
		// no saved script state every thing starts at its own first instruction and builds itself again.
		// The model half warns from inside PairSavedThings for the same reason.
		var saved = world.Save;

		if ( saved?.ScriptStates.Problem != null )
		{
			Log.Warning( $"{ThemeName}: the park file's script states would not read, so everything in it "
				+ $"starts from its own beginning and will replay its construction - {saved.ScriptStates.Problem}" );
		}

		_saved = saved == null ? [] : PairSavedThings( saved, catalogue );

		// The moment this load stands for on the clock these scripts and their channels run on. It is the save's
		// own moment on the save's clock: the engine makes its clock read the saved reading again as the load
		// ends (FUN_00415140, 0x00415193), so everything the save measured against that clock keeps its distance
		// from now. See OnThisClock.
		_loaded = (int)(GameClock.Ticks * MillisecondsPerTick);
		// A park made fresh has its file at tick nought, whose clock reads nought (FreshParkFile).
		_clock = saved?.Clock ?? (world as FreshPark)?.BaseFile().Clock;

		if ( _clock?.Problem != null )
		{
			Log.Warning( $"{ThemeName}: the park file's clock would not read, so every saved wait, timer and clip "
				+ $"starts afresh at the load - {_clock.Problem}" );
		}

		// The scheduler's own state as the park was saved: its tick, so each script keeps the phase of its one turn in
		// eight, and the next handle, so a script made from here on is numbered past every saved one.
		if ( saved?.ScriptStates is { Problem: null } scripts )
			Scheduler.Restore( scripts.Tick, scripts.NextHandle );

		// In the order the module holds the scripts, which the engine's reader inserts one by one at the head of its
		// list (0x005599d3), so that the scheduler's newest-first walk takes their turns as the engine's does. A thing
		// with no saved script comes after, numbered afresh.
		// Only from a module that read whole, as the next handle is restored only then: one that stopped part-way
		// could have the counter hand out a handle a script it did not reach was saved under.
		var saveOrder = new Dictionary<int, int>();
		var gateResumed = false;

		for ( var at = 0; saved?.ScriptStates.Problem == null && saved != null && at < saved.ScriptStates.Order.Count; ++at )
			saveOrder[saved.ScriptStates.Order[at]] = at;

		var inSaveOrder = world.Objects
			.OrderBy( placed => saveOrder.TryGetValue( placed.RideScript, out var at ) ? at : int.MaxValue );

		// The scripts the save holds that no object owns, which the object loop below does not start: a companion, a script an
		// object's script made with SPAWNCHILD (the parent's +0x0c) or SPAWNSOUND (+0x14). The parents made them at their start
		// and resume past that instruction, so they are started here from the save, at their place in its order (Q910).
		var companions = saved == null ? [] : CompanionsOf( world, catalogue, saved, saveOrder );
		var nextCompanion = 0;

		foreach ( var placed in inSaveOrder )
		{
			var placedAt = saveOrder.TryGetValue( placed.RideScript, out var found ) ? found : int.MaxValue;

			while ( nextCompanion < companions.Count && saveOrder[companions[nextCompanion].Handle] < placedAt )
				StartCompanion( companions[nextCompanion++] );

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
				PushItemSpeed( script, item );

				// And where this script had got to when the park was saved, which is the whole of why a
				// loaded park does not watch everything in it being built again. Started from its own first
				// instruction instead, the Belly Bounce would run WAITANIM 0 0, the construction clip, and hatch
				// out of its egg on every load.
				// See ParkScriptStates, and RideScript.ResumeAt for what it refuses.
				if ( saved != null )
				{
					var resumed = Resume( script, placed, saved );
					if ( placed.ThingId == world.ParkGates )
						gateResumed = resumed;
				}

				// Its own thing's player where the thing is standing, so that what the script triggers and
				// what the model is posed from are the same one.
				script.Animations = PlayersFor( placed.ThingId, item );
				script.Nodes = NodesFor( script, placed, item );
				BindTrackRide( script, placed, item, Scheduler );

				// The head table as it was saved, an empty one too, whose length the save gives (FUN_005597a0); after
				// NodesFor, which sizes a fresh one and gives the heads their nodes. Only where ADDHEAD or DELHEAD reads it.
				if ( script.UsesHeads && saved?.ScriptStates.For( placed.RideScript ) is { Heads: { } heads } )
					script.RestoreHeads( heads );

				if ( script.Animations.Loaded > 0 )
					_animated.Add( placed.ThingId );

				// And the other half of the restore, which has to come AFTER the player exists: putting
				// the script back without putting its model's channels back leaves ten of this park's
				// fourteen things frozen for good. See Restore.
				if ( _saved.TryGetValue( placed.ThingId, out var savedThing ) )
					Restore( script, savedThing );
				// One with no saved record, a park made fresh's gates and lights, has its model's tables as the model maker
				// leaves them, as a thing bought has (BindNew): the words of its file at rest, no clip bound.
				else if ( TableModel( placed.CatalogueId ) is { } fresh )
					script.Animations.Nodes = new ParkModelTables.Running( fresh.Model, script.Animations.ChannelCount );
			}
		}

		while ( nextCompanion < companions.Count )
			StartCompanion( companions[nextCompanion++] );

		if ( saved != null )
			LinkCompanions( world, saved );

		// A gate without saved state starts open or closed with its park. A resumed gate keeps its own
		// command and animation state. Last, because it needs the binding above to have run.
		CommandTheGate( world, gateResumed );

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
			$"; {KeptReadingsMoved} kept clock readings moved" +
			(CompanionsStarted > 0 ? $"; {CompanionsStarted} companions started from the save" : "") );
	}

	/// <summary>
	/// A companion script the save holds: the record <see cref="Handle"/> that a parent's <c>+0x0c</c> (<c>SPAWNCHILD</c>) or
	/// <c>+0x14</c> (<c>SPAWNSOUND</c>, <paramref name="Sound"/>) names, the parent's handle, and the file the parent's
	/// instruction loads for it.
	/// </summary>
	private readonly record struct Companion( int Handle, int Parent, string Path, bool Sound );

	private readonly List<Companion> _companions = [];

	/// <summary>The file's records a parent's link names that the load did not start, counted <c>LOADED_COMPANION_NOT_STARTED</c>: a save keeps them and their links as the file holds them.</summary>
	private readonly HashSet<int> _notStarted = [];

	/// <summary>How many companion scripts a load started from the save's records (<see cref="CompanionsOf"/>).</summary>
	public int CompanionsStarted { get; private set; }

	/// <summary>
	/// The companions of the park's objects, in the order the save holds them: for each object whose saved script has a
	/// child or a sound child, the record each names and the file it is. The record holds no name of its file, so it is
	/// the one of the parent's <c>SPAWNCHILD</c> (or <c>SPAWNSOUND</c>) files whose body is as long as the record's body;
	/// a record that fits none, or more than one, is counted and not started (<c>AGENTS.md</c> rule 4).
	/// </summary>
	private List<Companion> CompanionsOf( IParkInitialState world, ParkItemCatalogue catalogue, ParkWorld saved, Dictionary<int, int> saveOrder )
	{
		var found = new List<Companion>();

		if ( saved.ScriptStates.Problem != null )
			return found;

		foreach ( var placed in world.Objects )
		{
			if ( !placed.IsPlaced && _objects?.AnimationsFor( placed.ThingId ) is null )
				continue;

			if ( !catalogue.TryGet( placed.CatalogueId, out var item ) || !saveOrder.ContainsKey( placed.RideScript )
				|| saved.ScriptStates.For( placed.RideScript ) is not { } parent || (parent.Child == 0 && parent.SoundChild == 0) )
				continue;

			if ( Load( ScriptPathFor( item ) ) is not { } parentScript )
				continue;

			foreach ( var (handle, spawn) in new[] { (parent.Child, Opcode.SPAWNCHILD), (parent.SoundChild, Opcode.SPAWNSOUND) } )
			{
				if ( handle == 0 )
					continue;

				var path = saved.ScriptStates.For( handle ) is { } record && saveOrder.ContainsKey( handle )
					? FileOfCompanion( parentScript, spawn, record ) : null;

				if ( path is null )
				{
					Log.Info( $"{ThemeName}: the file's script {handle}, named by script {placed.RideScript}'s {spawn} link, is not a file that script loads, so it is not started" );
					Unimplemented.Report( "LOADED_COMPANION_NOT_STARTED" );
					_notStarted.Add( handle );
					continue;
				}

				found.Add( new Companion( handle, placed.RideScript, path, spawn == Opcode.SPAWNSOUND ) );
			}
		}

		return found.OrderBy( companion => saveOrder[companion.Handle] ).ToList();
	}

	/// <summary>The one file <paramref name="parent"/>'s <paramref name="spawn"/> instructions load whose body is as long as <paramref name="record"/>'s, or null for none or for more than one.</summary>
	private string? FileOfCompanion( RideScript parent, Opcode spawn, SavedScript record )
	{
		var fits = parent.SpawnPaths( spawn ).Where( path => Load( path )?.BodyWords == record.BodyWords ).ToList();

		return fits.Count == 1 ? fits[0] : null;
	}

	/// <summary>Makes one companion under its saved handle; <see cref="LinkCompanions"/> links and resumes it once every script stands.</summary>
	private void StartCompanion( Companion companion )
	{
		if ( Scheduler.Spawn( companion.Path, companion.Handle ) == 0 )
		{
			Log.Info( $"{ThemeName}: the file's script {companion.Handle} ({companion.Path}) could not be made, so it is not started" );
			Unimplemented.Report( "LOADED_COMPANION_NOT_STARTED" );
			_notStarted.Add( companion.Handle );
			return;
		}

		_companions.Add( companion );
	}

	/// <summary>
	/// Links each started companion to its parent as the spawning instruction does (<c>0x0055512b</c>, <c>0x00555255</c>):
	/// the parent's child or sound child, and for a child its parent, its thing and its model, then resumes it as an
	/// object's script is. Last, a count of the records the save holds that no object owns and nothing started.
	/// </summary>
	private void LinkCompanions( IParkInitialState world, ParkWorld saved )
	{
		foreach ( var companion in _companions )
		{
			var child = Scheduler.Find( companion.Handle );

			if ( child is null || Scheduler.Find( companion.Parent ) is not { } parent || saved.ScriptStates.For( companion.Handle ) is not { } record )
			{
				Log.Info( $"{ThemeName}: the file's script {companion.Handle} has no script {companion.Parent} to belong to, so it is not started" );
				Unimplemented.Report( "LOADED_COMPANION_NOT_STARTED" );
				Scheduler.Destroy( companion.Handle );
				_notStarted.Add( companion.Handle );
				continue;
			}

			if ( companion.Sound )
				parent.SoundChildId = companion.Handle;
			else
			{
				parent.ChildId = companion.Handle;
				child.ParentId = parent.Id;
				child.ThingId = parent.ThingId;
				child.Animations = parent.Animations;
				child.Nodes = parent.Nodes;
			}

			Resume( child, record, $"script {companion.Handle} (the {(companion.Sound ? "sound child" : "child")} of script {companion.Parent}, thing {child.ThingId})",
				!companion.Sound && _saved.ContainsKey( parent.ThingId ), saved );
			++CompanionsStarted;
		}

		if ( saved.ScriptStates.Problem != null )
			return;

		var owned = world.Objects.Select( placed => placed.RideScript ).ToHashSet();

		foreach ( var handle in saved.ScriptStates.Order )
		{
			if ( owned.Contains( handle ) || Scheduler.Find( handle ) is not null )
				continue;

			Log.Info( $"{ThemeName}: the file's script {handle} belongs to no object and no script's link starts it, so it is not started" );
			Unimplemented.Report( "LOADED_COMPANION_NOT_STARTED" );
		}
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

	/// <summary>The gate's command variable, resolved by its declared name.</summary>
	internal const string GateCommand = "VAR_COMMAND";

	/// <summary>The gate's command at the end of the park, the end sequence (<c>docs/exe/park-gate.md</c>).</summary>
	internal const int GateEnds = 2;

	/// <summary>
	/// Initialize only a gate without a valid resumed script. A fresh closed gate keeps command 0 and
	/// its closed base pose; 2 is the terminal end sequence, never an ordinary close. Saved commands,
	/// including a pending close/open animation, belong to the restored script (docs/exe/park-gate.md).
	/// </summary>
	private void CommandTheGate( IParkInitialState world, bool resumed )
	{
		var id = ScriptFor( world.ParkGates );

		if ( resumed || id == 0 || Scheduler.Find( id ) is not { } gate )
			return;

		if ( !gate.Set( GateCommand, world.ParkClosed == 0 ? 1 : 0 ) )
			Log.Warning( $"{ThemeName}: the gate's script declares no {GateCommand}" );
	}

	/// <summary>The variable the gate's script reports its own state through - see <see cref="GateStatus"/>.</summary>
	internal const string GateState = "VAR_STATUS";

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
	public int GateStatus( IParkInitialState? world )
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
	/// but the next trigger from the script replaces it, since <c>RideScript.StartAnimation</c> passes the script's
	/// speed divisor, 0.5 + 0.01 x the speed word (<c>0x00552be4</c>), as the engine's handlers push it (Q1039). The Belly Bounce's script meets its
	/// <c>LOOPANIM 2, 0</c> at word 43 with the saved looping key 2, so it skips it as the engine does and the
	/// saved loop plays on at 1.1.
	/// </para>
	/// </summary>
	private void Restore( RideScript script, SavedThing saved )
	{
		if ( script.Animations is { } players )
			Resume( players, saved );
	}

	/// <summary>
	/// A track ride's car's model put back where the park file left it - the record its handle names, which the
	/// loader's <c>FUN_00463ab0</c> finds as the ride system's reader left it (<c>docs/exe/saves.md</c>, "OpenTPW's
	/// reader, a car's model record"): its channels and its node words, as a placed thing's (<see cref="Restore"/>).
	/// </summary>
	/// <param name="handle">The car's <c>+0x08</c>: the model table's slot plus one.</param>
	/// <param name="itemId">The item the car's model is of; a record of another is not its model's, and is left.</param>
	/// <returns>The record's first channel, or null where the file holds no such record.</returns>
	internal SavedChannel? ResumeCar( RideAnimations players, int handle, int itemId )
		=> handle > 0 ? ResumeCar( players, _models?.InSlot( handle - 1 ), itemId ) : null;

	/// <summary>The same, of a record in hand; null for none, one of another item or one with no channel.</summary>
	internal SavedChannel? ResumeCar( RideAnimations players, SavedThing? record, int itemId )
	{
		if ( record is not { } saved || saved.CatalogueId != itemId || saved.Channels.Length == 0 )
			return null;

		Resume( players, saved );

		return saved.Channels[0];
	}

	private void Resume( RideAnimations players, SavedThing saved )
	{
		for ( var index = 0; index < saved.Channels.Length && index < players.ChannelCount; ++index )
		{
			var channel = saved.Channels[index];

			// What was queued behind it, which the next advance promotes when the clip ends; the engine copies the
			// queue whatever the channel holds, and an idle one's waits for a trigger to start something first.
			// The speed comes back even where nothing is queued (0x00464c05..0x00464c17 copies +0x30 for every channel): a loop
			// that replays before the script's first due visit starts at it (0x0047380a).
			if ( channel.QueuedRole != ParkThingStates.NoRole )
				players.Channel( index )?.Queue( channel.QueuedRole, channel.QueuedEntry, channel.QueuedFlags, channel.QueuedSpeed );
			else
				players.Channel( index )?.WriteQueuedSpeed( channel.QueuedSpeed );

			// The sentinel is the engine's own "this channel was running nothing", and it is the common
			// case: of the 163 channels the shipped park saves, 148 hold it, and none holds a queue.
			if ( channel.Role == ParkThingStates.NoRole )
				continue;

			// <b>The saved word is the engine's INTERNAL flag field, and only one of its bits means the
			// same thing to a caller.</b> 0x1 (loop) carries across unchanged; 0x2, 0x4 and 0x8 do not.
			// Internally 0x8 says a role nought clip has ended on the channel, put back below, and the other two say the channel was FROZEN at frame
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
				(channel.Flags & AnimTimeControl.LoopFlag) | AnimTimeControl.KeepShownFlag,
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

			player.RestoreEnded( (channel.Flags & AnimTimeControl.RoleNoughtEndedFlag) != 0 );

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

		// From here on a clip started is a clip bound: the file's words hold what the starts above stand for.
		if ( saved.NodeWords is { } words )
		{
			players.Nodes = new ParkModelTables.Running( words, [.. Enumerable.Range( 0, players.ChannelCount ).Select( index =>
				index < saved.Channels.Length && saved.Channels[index].Role != ParkThingStates.NoRole
					? players.Clip( saved.Channels[index].Role, saved.Channels[index].Entry ) : null )] );
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

	/// <summary>The save's model records as they read, by slot, or null with no save or one that would not read.</summary>
	private ParkThingStates? _models;

	/// <summary>What the save says each thing's model was doing, by thing id - empty where it would not read.</summary>
	private Dictionary<int, SavedThing> _saved = [];

	/// <summary>
	/// Matches each placed thing to its saved model state: the record that names the thing's script
	/// (<see cref="ParkThingStates.ForScript"/>; <c>docs/exe/saves.md</c>, "The objects, their scripts and their
	/// models"), which is what tells three Small Toilets' records apart. A record of another item than the thing's
	/// is not its model's, and is left.
	/// </summary>
	internal Dictionary<int, SavedThing> PairSavedThings( ParkWorld world, ParkItemCatalogue catalogue )
	{
		var paired = new Dictionary<int, SavedThing>();

		// How many channels each record's model holds - the one thing the module does not carry, and without
		// which its records cannot be stepped over at all.
		var states = world.ThingStates( catalogue.ChannelsFor );

		if ( states.Problem != null )
		{
			Log.Warning( $"{ThemeName}: the park file's model states would not read, so everything in it "
				+ $"keeps whatever its construction left, and its scripts' looping keys start afresh - {states.Problem}" );

			return paired;
		}

		_models = states;

		foreach ( var placed in world.Objects )
		{
			if ( states.ForScript( placed.RideScript ) is { } saved && saved.CatalogueId == placed.CatalogueId )
				paired[placed.ThingId] = saved;
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
	private bool Resume( RideScript script, ParkWorld.CatalogueObject placed, ParkWorld world )
	{
		if ( world.ScriptStates.For( placed.RideScript ) is not { } saved )
		{
			++NotResumed;
			return false;
		}

		return Resume( script, saved, $"thing {placed.ThingId}", _saved.ContainsKey( placed.ThingId ), world );
	}

	/// <summary>
	/// Resumes <paramref name="script"/> from its saved record, <paramref name="who"/> naming it in the log;
	/// <paramref name="channels"/> is whether its thing's model channels are put back, which the looping key and the mark
	/// answer for. A <c>SPAWNCHILD</c> companion shares its parent's channels, so it is whether the parent's are; a
	/// <c>SPAWNSOUND</c> companion has no model.
	/// </summary>
	private bool Resume( RideScript script, SavedScript saved, string who, bool channels, ParkWorld world )
	{
		// The name first, because resuming steps over the NAME every one of these scripts opens with, and
		// a nameless script cannot be found by FINDSCRIPTRAND - see RideScript.TakeDeclaredName.
		script.TakeDeclaredName();

		// The word as the file holds it (FUN_005597a0 reads the record raw and writes no +0xc0 after it; docs/exe/park.md, "What a loaded script holds").
		script.Speed = saved.Speed;

		// And the reverb level the same way (+0xd8; the record's, -1 where no SETREVERB had run).
		script.Reverb = saved.Reverb;

		// A script whose saved array is a different length from the one it declares is not the script this
		// record belongs to. Said rather than silently truncated, because the slots would still be written.
		if ( saved.Variables.Length != script.Variables.Count )
		{
			Log.Info( $"{ThemeName}: {who} declares {script.Variables.Count} variables and "
				+ $"the save holds {saved.Variables.Length} for script handle {saved.Handle}" );
		}

		var slots = Math.Min( saved.Variables.Length, script.Variables.Count );

		for ( var slot = 0; slot < slots; ++slot )
			script.SeedVariable( slot, saved.Variables[slot] );

		// Only if the position is real: an unknown one would stop the script dead rather than erring, so a
		// thing that cannot be resumed is better left running from its beginning, with the loader's stacks.
		if ( !script.ResumeAt( saved.Position ) )
		{
			Log.Warning( $"{ThemeName}: {who} was saved at word {saved.Position} of "
				+ $"{saved.BodyWords}, which is not the start of an instruction, so it starts from the beginning" );

			++NotResumed;
			return false;
		}

		// With the counter, the stack and the register it was saved with: a ride saved mid-cycle has its
		// riders' handles on the heap, and a branch it resumes before reads the register. The saved block's
		// length is the stack's size, as the engine takes it (FUN_005597a0, 0x00559af1).
		if ( saved.Stack.Length != script.Stack.Count )
		{
			Log.Info( $"{ThemeName}: {who} declares a stack of {script.Stack.Count} and the "
				+ $"save holds {saved.Stack.Length} for script handle {saved.Handle}" );
		}

		script.RestoreStacks( saved.Stack, saved.CallIndex, saved.HeapIndex, saved.Result );

		// The looping key and the mark answer for what channel 0 is playing, so they come back only with the
		// channels. Where the save's model states would not read, or hold no record for this thing, its channels start
		// idle, and a restored key would make its next LOOPANIM of that loop do nothing over a channel playing none -
		// a gap of this reader's, since the engine always reads both.
		script.RestoreClockState( OnThisClock( saved.WaitDeadline ), OnThisClock( saved.AnimationDeadline ),
			channels ? saved.LoopingKey : script.LoopingKey, channels ? saved.AnimationMark : script.AnimationMark,
			OnThisClock( saved.TimerDeadline ) ?? 0f );

		// And who it was carrying, each reading moved as the deadlines are.
		var riders = script.RestoreRiders( saved, reading => reading != 0 ? Moved( reading ) : null );

		if ( riders > 0 )
			Log.Info( $"{ThemeName}: {who} (script {saved.Handle} at word {saved.Position}) holds {riders} guests in its limbo, bounce and walk slots" );

		RidersRestored += riders;

		// And the effects it had started, kept so that its own KILLOBJ finds them.
		if ( saved.Effects is { } effects )
		{
			script.Effects?.Restore( effects );
			script.StartRestoredParticles( world.Particles );
		}

		// And the deadlines it keeps in its own variables, a deviation said at RideScript.MoveKeptReadings.
		// Only where the save's clock reads, as the struct's own deadlines are moved.
		var kept = _clock?.Reading is not null ? script.MoveKeptReadings( reading => Moved( unchecked((uint)reading) )!.Value ) : 0;

		if ( kept > 0 )
			Log.Info( $"{ThemeName}: {who} (script {saved.Handle} at word {saved.Position}) had {kept} kept clock readings moved" );

		KeptReadingsMoved += kept;

		++Resumed;
		return true;
	}

	/// <summary>How many guests the resumed scripts' limbo, bounce and walk slots were read holding.</summary>
	public int RidersRestored { get; private set; }

	/// <summary>
	/// The file's objects' scripts and models as they run, for a park file's writer (<c>docs/exe/saves.md</c>,
	/// "OpenTPW's writer, the objects"), or null where the file's clock did not read, so no moment here has a
	/// reading there.
	///
	/// <para>
	/// <b>The clock is moved on, the other way round from the load</b> (<see cref="Moved"/>): a moment on this
	/// park's clock is written as the file's own reading plus the time since the load, and the file's clock as the
	/// reading of now, so every deadline and stamp keeps its distance from the save's moment.
	/// </para>
	/// <para>
	/// A script is written under the handle the scheduler runs it on, into the file's record of that handle, and its
	/// three link words as they stand (nought for a child that has gone, REMOVECHILD's or its own end's). A child or sound
	/// child the file holds no record for is written whole, its thing and model its parent's for a child and none for a
	/// sound child (<see cref="ParkFileWriter.MadeCompanion"/>); any other script the file holds no record for is not
	/// written, counted. A record whose script has ended is left as the file's, counted, unless it is a companion
	/// the load started and its parent stands: that is taken out with its emitters. A record the load could not start is
	/// the file's, its parent's link too (the scheduler drops a script as it ends). A model is written into the record
	/// that names its thing's script. A thing bought is handed over whole, its script as it runs with what its
	/// loader took from its file and its model's channels, and a thing sold by its id.
	/// </para>
	/// <para>
	/// <b>A channel is written as the engine keeps one</b>: a held channel's clip time a whole clip after its
	/// start, a running one's the moment of the save, the third stamp the save's. Of the flag word the low six
	/// bits are the running channel's (loop, frozen, held, a role nought clip ended, <c>0x10</c> and <c>0x20</c>);
	/// the rest are left the file's. A head hung on a model's node goes into its node's lookup record (<see cref="WrittenHeads(RideScript?, int)"/>).
	/// The hoarding's bits are the thing's as they stand, the Closed kind a load gives one that names none among
	/// them: the original's own file of a loaded park holds it on the same things.
	/// </para>
	/// </summary>
	/// <param name="objects">The file's objects as they run, in the file's order.</param>
	/// <param name="channelsFor">How many channels a record's model holds (<see cref="ParkItemCatalogue.ChannelsFor"/>).</param>
	/// <param name="hoardingFor">A thing's hoarding, or null for a thing with none.</param>
	/// <param name="bought">The objects bought since the load, each written whole with its script and its model.</param>
	/// <param name="gone">The file's objects sold since, whose records are taken out.</param>
	/// <param name="built">Each item's standing count and first-build stamp.</param>
	internal ParkFileWriter.RunningThings? Written( ParkWorld loaded, IReadOnlyList<ParkWorld.CatalogueObject> objects,
		Func<int, int> channelsFor, Func<int, RideHoardingState?> hoardingFor, IReadOnlyList<BoughtThing>? bought = null,
		IReadOnlySet<int>? gone = null, IReadOnlyDictionary<int, (int Standing, uint FirstBuilt)>? built = null )
	{
		if ( _clock?.Reading is not { } saved || loaded.ScriptStates.Problem != null )
			return null;

		var states = loaded.ThingStates( channelsFor );

		if ( states.Problem != null )
			return null;

		// The file's moment is the frame's, the instant the channels were swept to: a clip held or replayed in this frame's
		// sweep is stamped after the last tick's instant, and a moment before that would load it started in the future.
		var now = NowMilliseconds;

		uint Reading( float moment ) => unchecked(saved + (uint)(int)(moment - _loaded));

		// The things bought: each one's script whole, and its model's channels with no record of the file's under them.
		var made = new List<ParkFileWriter.MadeThing>();
		var madeHandles = new HashSet<int>();

		foreach ( var thing in bought ?? [] )
		{
			var placed = thing.Object.Object;
			MadeScript? record = null;
			RideAnimations? animations = null;
			IReadOnlyList<ParkFileWriter.MadeCompanion>? companions = null;

			if ( _scripts.TryGetValue( placed.ThingId, out var handle ) && Scheduler.Find( handle ) is { } script )
			{
				companions = MadeCompanionsOf( script, thing.Directory, loaded, Reading );

				var ownRecord = script.Made( Reading, thing.Directory );

				record = ownRecord with { Script = Linked( ownRecord.Script, script, loaded ) };
				madeHandles.Add( handle );
				madeHandles.UnionWith( companions.Select( companion => companion.Script.Script.Handle ) );
				animations = script.Animations;
			}

			var channels = new SavedChannel[Math.Max( channelsFor( placed.CatalogueId ), 1 )];

			for ( var index = 0; index < channels.Length; ++index )
			{
				channels[index] = animations?.Channel( index ) is { } channel
					? WrittenChannel( channel, channel.Started ? MadeChannel : NeverStartedChannel, Reading, now, seeded: channel.SpeedWritten )
					: MadeChannel;
			}

			var raised = hoardingFor( placed.ThingId );

			// Both tables: the node words as the model's clips have left them, the lookup records from its file.
			// A thing whose model did not read declares none, and the engine keeps the fresh model's own.
			ParkThingStates.ModelTables? tables = null;

			if ( animations?.Nodes is { } kept && TableModel( placed.CatalogueId ) is { } model && kept.Count == model.Model.Nodes.Count )
			{
				tables = new ParkThingStates.ModelTables( kept.Words( animations.NodeFrames() ),
					ParkModelTables.Lookups( model.Model, model.DoHeadProcessing ), ParkModelTables.SharedFlags( model.Model, thing.Vehicle ) );
			}
			else
				Unimplemented.Report( "SAVE_PARK_MADE_MODEL_TABLES" );

			made.Add( new ParkFileWriter.MadeThing( thing.Object, thing.Across, thing.Down, record, channels,
				raised?.Flags ?? 0, raised?.Progress ?? 0f, tables,
				tables != null && _scripts.TryGetValue( placed.ThingId, out var own ) ? WrittenHeads( Scheduler.Find( own ), placed.CatalogueId ) : null,
				thing.Vehicle, thing.FixedCell, companions ) );
		}

		// The file's parents' children and sound children made since the load: whole, ahead of the file's records.
		var madeCompanions = new List<ParkFileWriter.MadeCompanion>();

		foreach ( var parent in Scheduler.Scripts )
		{
			if ( madeHandles.Contains( parent.Id ) || loaded.ScriptStates.For( parent.Id ) is not { } held )
				continue;

			if ( held.Directory is null )
			{
				Unimplemented.Report( "SAVE_PARK_SCRIPT_MADE_SINCE_THE_LOAD" );
				continue;
			}

			madeCompanions.AddRange( MadeCompanionsOf( parent, held.Directory, loaded, Reading ) );
		}

		madeHandles.UnionWith( madeCompanions.Select( companion => companion.Script.Script.Handle ) );

		var goneHandles = new HashSet<int>( (gone ?? new HashSet<int>()).SelectMany( loaded.ScriptStates.HandlesOf ) );
		var scripts = new List<WrittenScript>();
		var running = new HashSet<int>();

		foreach ( var script in Scheduler.Scripts )
		{
			running.Add( script.Id );

			if ( madeHandles.Contains( script.Id ) )
				continue;

			if ( loaded.ScriptStates.For( script.Id ) is null )
			{
				Unimplemented.Report( "SAVE_PARK_SCRIPT_MADE_SINCE_THE_LOAD" );
				continue;
			}

			scripts.Add( Linked( script.Written( Reading ), script, loaded ) );
		}

		// A companion the load started whose script has ended, its parent standing: its record goes from the file with its emitters.
		var ended = new HashSet<int>();

		foreach ( var companion in _companions )
		{
			if ( !running.Contains( companion.Handle ) && running.Contains( companion.Parent ) && !goneHandles.Contains( companion.Handle )
				&& loaded.ScriptStates.For( companion.Handle ) is not null )
			{
				Log.Info( $"{ThemeName}: the file's script {companion.Handle}, script {companion.Parent}'s {(companion.Sound ? "sound child" : "child")}, has ended, so its record is left out of the save" );
				ended.Add( companion.Handle );
			}
		}

		foreach ( var handle in loaded.ScriptStates.Order )
		{
			if ( running.Contains( handle ) || goneHandles.Contains( handle ) || ended.Contains( handle ) )
				continue;

			var thingId = loaded.ScriptStates.For( handle )?.Thing ?? 0;
			var catalogueId = loaded.Objects.Where( thing => thing.ThingId == thingId ).Select( thing => thing.CatalogueId ).FirstOrDefault();

			Log.Info( $"{ThemeName}: the file's script {handle} (thing {thingId}, catalogue {catalogueId}) is not running here, so its record stays the file's" );
			Unimplemented.Report( "SAVE_PARK_SCRIPT_ENDED" );
		}

		var models = new List<WrittenModel>();

		foreach ( var thing in objects )
		{
			if ( !_scripts.TryGetValue( thing.ThingId, out var handle ) || Scheduler.Find( handle )?.Animations is not { } players )
				continue;

			if ( states.ForScript( handle ) is not { } record || record.CatalogueId != thing.CatalogueId
				|| record.Channels.Length != players.ChannelCount )
				continue;

			var channels = new SavedChannel[record.Channels.Length];

			for ( var index = 0; index < channels.Length; ++index )
				channels[index] = WrittenChannel( players.Channel( index )!, record.Channels[index], Reading, now, seeded: true );

			var hoarding = hoardingFor( thing.ThingId );

			// The node words, the file's as the clips started since the load have left them.
			var words = players.Nodes?.Words( players.NodeFrames() );

			models.Add( new WrittenModel( record.Slot, channels, hoarding?.Flags, hoarding?.Progress ?? 0f, words,
				WrittenHeads( Scheduler.Find( handle ), thing.CatalogueId ) ) );
		}

		return new ParkFileWriter.RunningThings( objects, Scheduler.Tick, Scheduler.NextHandle, scripts, states, models,
			Reading( now ), made, gone, built, MadeScripts: madeCompanions, GoneScripts: ended );
	}

	/// <summary>
	/// The child and the sound child <paramref name="parent"/> made since the load, each written whole: the thing of a
	/// child is its parent's and a sound child's is none, the speed word each one's own (a child's is its parent's by the
	/// scheduler's copy, a sound child's the loader's), and the folder is the parent's (97 of 97 children and 125 of 125 sound children in
	/// the 766 distinct park files on the desk). A link whose script the file already holds a record for is the load's, not made here.
	/// </summary>
	private List<ParkFileWriter.MadeCompanion> MadeCompanionsOf( RideScript parent, string directory, ParkWorld loaded,
		Func<float, uint> reading )
	{
		var found = new List<ParkFileWriter.MadeCompanion>();

		foreach ( var (id, sound) in new[] { (parent.ChildId, false), (parent.SoundChildId, true) } )
		{
			if ( id == 0 || Scheduler.Find( id ) is not { } child || loaded.ScriptStates.For( id ) is not null )
				continue;

			if ( child.ChildId != 0 || child.SoundChildId != 0 )
				Unimplemented.Report( "SAVE_PARK_SCRIPT_MADE_SINCE_THE_LOAD" );

			var made = child.Made( reading, directory );

			found.Add( new ParkFileWriter.MadeCompanion( made with
			{
				Script = made.Script with { Child = 0, SoundChild = 0, Parent = sound ? 0 : parent.Id }
			}, parent.Id, sound ) );
		}

		return found;
	}

	/// <summary>
	/// <paramref name="written"/> with its three link words as <paramref name="script"/> stands: its child, its parent and its
	/// sound child where each is a script that stands, nought where it has gone (REMOVECHILD took it down, or it ended), and the
	/// file's, <c>-1</c>, where the load could not start the record the file's link names (<see cref="_notStarted"/>).
	/// </summary>
	private WrittenScript Linked( WrittenScript written, RideScript script, ParkWorld loaded )
	{
		var file = loaded.ScriptStates.For( script.Id );

		int Link( int live, int fromFile )
			=> live != 0 && Scheduler.Find( live ) is not null ? live : fromFile != 0 && _notStarted.Contains( fromFile ) ? -1 : 0;

		return written with
		{
			Child = Link( script.ChildId, file?.Child ?? 0 ),
			SoundChild = Link( script.SoundChildId, file?.SoundChild ?? 0 ),
			Parent = script.ParentId != 0 && Scheduler.Find( script.ParentId ) is not null ? script.ParentId : 0
		};
	}

	/// <summary>
	/// A moment of the clock this park's scripts and channels run on as a reading of the file's clock, as
	/// <see cref="Written"/> writes every stamp; null where the save's clock did not read.
	/// </summary>
	internal Func<float, uint>? Readings
		=> _clock?.Reading is { } saved ? moment => unchecked(saved + (uint)(int)(moment - _loaded)) : null;

	/// <summary>
	/// Now, on the clock the scripts and channels run on: the last tick's instant and the part of a tick owed since,
	/// the frame's own moment, as the engine's snapshot is the clock read once a drawn frame (<c>0x00473440</c>). It
	/// never reaches the next tick's instant, or meets it, past 2^24 ms, so every stamp a sweep makes lies before the next
	/// tick's scripts run, and a save written now holds no channel started after its own moment.
	/// </summary>
	internal static int NowMilliseconds => MomentAt( GameClock.Ticks, (int)(GameClock.PartialTick * MillisecondsPerTick) );

	/// <summary>
	/// The moment <paramref name="owed"/> milliseconds past tick <paramref name="ticks"/>'s instant, short of the next tick's.
	/// The sum is rounded to a float as the tick stamps are (<see cref="LastTickMilliseconds"/>, <see cref="MillisecondsAt"/>),
	/// which from 2^24 ms (4.7 hours of ticks) lose whole milliseconds: rounding to nearest never steps back across a
	/// tick, where a rounded instant and an exact part would from 2^26 ms (18.6 hours), and it stays between the two
	/// ticks' rounded instants.
	/// </summary>
	internal static int MomentAt( int ticks, int owed )
		=> (int)(float)(((long)ticks * (int)MillisecondsPerTick) + Math.Min( owed, (int)MillisecondsPerTick - 1 ));

	/// <summary>The last tick's instant on the same clock, exactly <see cref="GameClock.Ticks"/> beats in.</summary>
	internal static int LastTickMilliseconds => (int)(GameClock.Ticks * MillisecondsPerTick);

	/// <summary>
	/// The heads hung on a script's model's nodes, each by its node's lookup record, with every record one can hang
	/// on: a head table's (<c>ADDHEAD</c>), and a walk's on an item that keeps its head nodes posed
	/// (<see cref="RideScript.WalkHeads"/>: <c>Info.DoHeadProcessing</c> is set by the items whose scripts walk a
	/// rider on under action 4). Null for a script with neither, whose model's lookup records are not its to write
	/// (a coaster's and a tour's cars' heads are kept by nothing here), and for an item whose model will not read.
	/// </summary>
	private WrittenHeads? WrittenHeads( RideScript? script, int catalogueId )
	{
		if ( script == null || TableModel( catalogueId ) is not { } model )
			return null;

		var walked = script.WalkSlots > 0 && model.DoHeadProcessing;

		if ( script.HeadSlots == 0 && !walked )
			return null;

		int Record( int head ) => model.Model.FindNode( head, RideNodes.HeadSpace ) is var node && node >= 0 ? node - model.Model.LookupFirst : -1;

		// A walk can hang on any head node of the model: the run of ids from 1, as a head table is sized.
		var nodes = script.HeadSlots;

		while ( walked && model.Model.FindNode( nodes + 1, RideNodes.HeadSpace ) >= 0 )
			++nodes;

		return new WrittenHeads(
			[.. Enumerable.Range( 1, nodes ).Select( Record ).Where( record => record >= 0 )],
			[.. script.Heads().Where( head => head.Hung ).Select( head => (head.Node, head.Handle) )
				.Concat( walked ? script.WalkHeads() : [] )
				.Where( head => Record( head.Node ) >= 0 ).Select( head => (Record( head.Node ), head.Handle) )] );
	}

	/// <summary>
	/// An object bought since the load, for <see cref="Written"/>: its record and name, its item's footprint in cells
	/// and the folder its script was loaded from, as the engine keeps one (<c>data\levels\jungle\Rides\monkey\</c>);
	/// <paramref name="Vehicle"/> for an arrival vehicle made at its summons, <paramref name="FixedCell"/> for a fixed item's (the gates', the lights') model cell.
	/// </summary>
	internal readonly record struct BoughtThing( ParkWorld.MadeObject Object, int Across, int Down, string Directory, bool Vehicle = false,
		(int X, int Y)? FixedCell = null );

	/// <summary>A channel with nothing started on it and nothing queued, at speed 1: what a made model's record starts from.</summary>
	private static readonly SavedChannel MadeChannel =
		new( ParkThingStates.NoRole, 0, 0, 1f, 0, 0, 0, ParkThingStates.NoRole, 0, 0, 1f );

	/// <summary>
	/// A made model's channel no clip has ever been started on: the model maker's zeroed one, its two speeds nought, as the
	/// original's park made fresh holds its gates' and lights' at tick nought (FileFormats <c>saves.md</c>, "A park made fresh").
	/// </summary>
	private static readonly SavedChannel NeverStartedChannel = MadeChannel with { Speed = 0f, QueuedSpeed = 0f };

	/// <summary>The park's items, for a model's own file as the park file's two tables are written.</summary>
	private readonly ParkItemCatalogue? _catalogue;

	private readonly Dictionary<int, (ModelFile Model, bool DoHeadProcessing)?> _tableModels = [];

	/// <summary>
	/// An item's own model as its file gives it, for the park file's two tables, read once an item; null where the
	/// catalogue has no such item or its model will not read.
	/// </summary>
	private (ModelFile Model, bool DoHeadProcessing)? TableModel( int catalogueId )
	{
		if ( _tableModels.TryGetValue( catalogueId, out var known ) )
			return known;

		(ModelFile, bool)? read = null;

		if ( _catalogue != null && _catalogue.TryGet( catalogueId, out var item ) )
		{
			try
			{
				using var stream = Files.OpenReadOrNull( $"{item.Directory}/{item.Stem}.MD2" );

				if ( stream != null )
					read = (new ModelFile( stream ), item.DoHeadProcessing);
			}
			catch ( Exception e )
			{
				Log.Warning( $"{ThemeName}: '{item.Name}' is written with its file's model tables - its model will not read: {e.Message}" );
			}
		}

		return _tableModels[catalogueId] = read;
	}

	/// <summary>
	/// The bits of a channel's flag word a running channel keeps as the engine does: loop, frozen, held, a role
	/// nought clip ended, <c>0x10</c> and <c>0x20</c>.
	/// </summary>
	private const int ChannelFlagsKept = 0x3f;

	/// <summary>One channel as the engine keeps it - see <see cref="Written"/>.</summary>
	/// <param name="seeded">
	/// Whether the channel's queued speed has been written (<see cref="AnimTimeControl.SpeedWritten"/>): always for a loaded thing's
	/// channel, which the load seeds (<see cref="Resume(RideAnimations, SavedThing)"/>); for a made one, once a queue or the scheduler's
	/// visit has written it. False only for a made channel nothing has written, whose <paramref name="file"/> is a fallback.
	/// </param>
	internal static SavedChannel WrittenChannel( AnimTimeControl channel, SavedChannel file, Func<float, uint> reading, int now, bool seeded )
	{
		// What is queued behind it; with nothing queued the engine leaves the last queue's flags behind, which a channel here
		// does not keep, so the file's stay. A promotion or a start-at-once trigger zeroes the clip too, and the file's clip is
		// written here all the same: a deviation where the file holds a non-zero clip with nothing queued. Its speed is the
		// engine's +0x30 as it stands, which it saves whatever it holds: the load's seed or the scheduler's last write
		// (RideScript.WriteChannelSpeeds), or the nought a promotion or a start-at-once trigger left until the script's next due
		// visit (0x004738af, 0x00473408). A made channel nothing has yet written holds a nought nobody put there, and takes the
		// fallback (docs/exe/saves.md, "OpenTPW's writer, the objects", a model).
		var queued = channel.HasQueued
			? (Role: channel.DeferredAnimID, Entry: channel.DeferredSubAnim, Flags: channel.DeferredFlags, Speed: channel.DeferredSpeed)
			: (Role: ParkThingStates.NoRole, Entry: file.QueuedEntry, Flags: file.QueuedFlags,
				Speed: seeded || channel.DeferredSpeed != 0f ? channel.DeferredSpeed : file.QueuedSpeed);

		if ( channel.IsIdle )
		{
			// One idle in the file that has had nothing started on it since: the file's word and speed stand.
			// One stopped since holds nought in its three stamps, as every idle channel in the files does.
			return file.Role == ParkThingStates.NoRole && !channel.Started
				? file with { QueuedRole = queued.Role, QueuedEntry = queued.Entry, QueuedFlags = queued.Flags, QueuedSpeed = queued.Speed }
				: new SavedChannel( ParkThingStates.NoRole, 0, (file.Flags & ~ChannelFlagsKept) | (channel.Flags & ChannelFlagsKept),
					channel.Speed, 0, 0, 0, queued.Role, queued.Entry, queued.Flags, queued.Speed );
		}

		var start = reading( channel.StartAnimTime );

		// A channel held since before the load, on the clip and from the start the file gives it, keeps the file's
		// word and clip time: the engine's 0x10 on a held channel comes and goes with its posing (a file of the
		// original's holds 0x4 and 0x14 alike, and its load sets the bit again), and a channel here keeps it for
		// good. One held here is given a whole clip past its start, which this build's truncation can leave a
		// millisecond short of the engine's.
		var heldAsTheFile = (file.Flags & HeldAtEnd) != 0 && (channel.Flags & HeldAtEnd) != 0
			&& (file.Role, file.Entry, file.StartTime) == (channel.AnimID, channel.SubAnim, start);

		var flags = heldAsTheFile ? file.Flags : (file.Flags & ~ChannelFlagsKept) | (channel.Flags & ChannelFlagsKept);

		var time = (channel.Flags & FrozenAtStart) != 0 ? start
			: (channel.Flags & HeldAtEnd) != 0 ? heldAsTheFile ? file.Time : reading( channel.HeldTime )
			: reading( now );

		return new SavedChannel( channel.AnimID, channel.SubAnim, flags, channel.Speed, start, time, reading( now ),
			queued.Role, queued.Entry, queued.Flags, queued.Speed );
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
			using var stream = Files.OpenReadOrNull( path );

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

		return RideAnimations.Load( item.Directory, item.Stem, Files, item.AnimationChannels );
	}

	/// <summary>
	/// Stands a placed thing's nodes: at <paramref name="origin"/>, where its model stands
	/// (<see cref="ParkObjects.ModelOriginFor(ParkItemCatalogue.Item, ParkWorld.CatalogueObject)"/>), turned by its angle,
	/// but an arrival vehicle where its model's file puts
	/// it, root and all, as the End stands (<see cref="BindFixed"/>), since its cells name no place: the original's bus voice
	/// came at its body's place in the file, (296, 5, -114) (<c>docs/exe/audio.md</c>, "The music's class of a script").
	/// </summary>
	internal static void Stand( RideNodes nodes, int catalogueId, Vector3 origin, int angle )
	{
		if ( ParkFixedItems.IsVehicle( catalogueId ) )
			nodes.PlaceFixed();
		else
			nodes.Place( origin, angle );
	}

	/// <summary>
	/// The model nodes a bound script walks its riders between or hangs their heads on, standing where its thing
	/// stands - read only for a script that declares walk slots or carries <c>ADDHEAD</c> or <c>ADDOBJ</c>, the
	/// three families that ask (<see cref="RideNodes"/>), and giving the second its head slots. Null where the model
	/// will not read, which leaves every leg the shortest, counted, no head slot, and an effect on no lookup record.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>The engine sizes a head table for every script with a thing and a model</b> (<c>FUN_005587f0</c>); only
	/// <c>ADDHEAD</c> and <c>DELHEAD</c> read it, so a script without either is given none here, nor one from a save, which
	/// changes nothing it does, but a bought thing's record is written with no head table where the original's holds one
	/// (the TV Sim's 27): counted at the writer (<see cref="RideScript.Made"/>). A loaded record keeps the file's.
	/// </para>
	/// <para>
	/// A vehicle made at its summons comes through <c>BindNew</c> and is given its nodes as its model's file stands
	/// them (<c>Stand</c>); no fixed item's script declares a walk slot or carries <c>ADDHEAD</c>.
	/// </para>
	/// </remarks>
	private RideNodes? NodesFor( RideScript script, ParkWorld.CatalogueObject placed, ParkItemCatalogue.Item item )
	{
		if ( script.WalkSlots == 0 && !script.UsesHeads && !script.UsesEffectNodes )
			return null;

		RideNodes? nodes;

		try
		{
			nodes = RideNodes.Load( item.Directory, item.Stem, Files, item.DoHeadProcessing,
				script.Animations?.AllClips ?? [] );
		}
		catch ( Exception e )
		{
			if ( script.WalkSlots != 0 || script.UsesHeads )
			{
				Log.Warning( $"{ThemeName}: thing {placed.ThingId} ('{item.Name}') walks or seats riders but its model "
					+ $"will not read, so every leg is the shortest and no head is hung - {e.Message}" );
			}
			else
			{
				Log.Warning( $"{ThemeName}: thing {placed.ThingId} ('{item.Name}') uses effect nodes but its model "
					+ $"will not read, so it has none - {e.Message}" );
			}

			return null;
		}

		if ( nodes is null )
		{
			if ( script.WalkSlots != 0 || script.UsesHeads )
			{
				Log.Warning( $"{ThemeName}: thing {placed.ThingId} ('{item.Name}') walks or seats riders but has no model, "
					+ "so every leg is the shortest and no head is hung" );
			}

			return null;
		}

		Stand( nodes, placed.CatalogueId, ParkObjects.ModelOriginFor( item, placed ), placed.Angle );

		if ( script.UsesHeads )
			script.SizeHeads( nodes.HeadCount() );

		return nodes;
	}

	/// <summary>
	/// The three operating numbers the ride window's controls write into a running script: capacity and duration as named variables
	/// (<c>FUN_004dd7f0</c>, <c>FUN_004dd720</c>) and the speed as the script's word (<c>FUN_004dd6e0</c> through <c>FUN_0055a300</c>,
	/// <c>0x004dd6f6</c>, at any value). Answers whether the thing has a running script.
	/// </summary>
	public bool SetOperating( int thingId, int speed, int capacity, int duration )
	{
		if ( ScriptFor( thingId ) is not (var id and not 0) || Scheduler.Find( id ) is not { } script )
			return false;

		script.Set( ParkRideOperation.CapacityVariable, capacity );
		script.Set( ParkRideOperation.DurationVariable, duration );
		script.Speed = speed;

		return true;
	}

	/// <summary>
	/// The object constructor's push of the item's operating speed into its script's speed word (<c>0x004db534</c>, <c>FUN_0055a300</c>),
	/// made only when <c>Upgrades[0].InitSpeed</c> is above nought: otherwise the word stays the script creator's 50. A load puts the
	/// file's word over it (<see cref="Resume(RideScript, SavedScript, string, bool, ParkWorld)"/>).
	/// </summary>
	private static void PushItemSpeed( RideScript script, ParkItemCatalogue.Item item )
	{
		if ( item.InitSpeed > 0 )
			script.Speed = item.InitSpeed;
	}

	/// <summary>
	/// What <c>BUMP</c> works on: the thing's track-ride handle and <c>mIsTrackRideValid</c>, and the park's bumper rides,
	/// with the ride's arena put where the thing stands (<see cref="ParkBumperCars.ArenaCentre"/>).
	/// </summary>
	private static void BindTrackRide( RideScript script, ParkWorld.CatalogueObject placed, ParkItemCatalogue.Item item,
		RideScriptScheduler scripts )
	{
		script.TrackRide = placed.TrackRide;
		script.TrackRideValid = placed.IsTrackRideValid;

		if ( placed.TrackRide == 0 || ParkState.Current?.TrackRides.Cars is not { } cars )
			return;

		script.Bumpers = cars;

		// The cars' sounds read the ride's EventMap through the scripts that hold them.
		cars.Sounds ??= new ParkCarSounds( scripts );

		var (x, z) = ParkBumperCars.ArenaCentre( item, placed.CellX, placed.CellY, placed.Angle );
		cars.Place( placed.TrackRide, x, z );

		// The speed word is the script's own (RideScript.Speed), which the scheduler pushes into the ride's performance at each due visit
		// (0x00551844; RideScriptScheduler.Advance). The constructor's push and a load's record have already set it.

		// A kart or water ride has no record in the cars: its words are the track table's, placed at the place the
		// placer handed the record (its arena's centre less the half cell the record adds).
		ParkState.Current.TrackRides.PlaceKart( placed.TrackRide, x - (ParkBumperCars.CellUnits / 2), z - (ParkBumperCars.CellUnits / 2),
			script.Speed );
	}

	/// <summary>
	/// The rides' part of one step of the park's loop (<see cref="ParkLoop"/>), <paramref name="index"/> the step's
	/// place among the frame's ticks: the track tick, which counts the bumper cars' goes (<see cref="ParkBumperCars.Tick"/>),
	/// then the whole script system, <c>FUN_005516b0</c> (<c>0x0054f56b</c>).
	/// </summary>
	internal void Tick( int index )
	{
		ParkState.Current?.TrackRides.Cars.Tick();
		Scheduler.Advance( MillisecondsAt( index ) );

		// The walks after the scripts, as the engine's order has them, but once a tick where the engine's
		// FUN_00557ab0 runs once a frame past this loop (0x0054fa08): a deviation kept by Alexah's decision,
		// so an arrival is noticed within 31 ms whatever the frame rate (docs/history/queue-done.md Q183).
		Scheduler.StepTheWalks( MillisecondsAt( index ) );
	}

	/// <summary>
	/// Once a frame, after the park's loop has run the frame's ticks (<see cref="Tick"/>), whatever they asked for is shown.
	/// </summary>
	protected override void OnUpdate()
	{
		// The engine advances its animation players
		// in the scene draw (on-screen models from their scene-node callback, the rest by FUN_0044e410( 3 )),
		// past the back edge of the catch-up loop, off one snapshot of the clock - so the sweep belongs after the loop rather than inside it.
		// The snapshot is the frame's own moment, so a clip moves every drawn frame (0x00473740; docs/exe/park.md, difference 7). Our tick runs
		// at the end of its 31 ms and the engine's at the start, so the engine's snapshot lies between its last two ticks and ours past its
		// last: the same moment of the frame, a tick further on in the ticks' own stamps, which leaves these clips a tick ahead of the
		// bumper boats' eased position (ParkBumperBoats.Put).
		_objects?.Sweep( NowMilliseconds );
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
	/// value for the whole frame. By the time the park's loop runs, <see cref="GameClock.Ticks"/> already
	/// counts this frame's ticks, so the first of them is that many less <see cref="GameClock.TicksDue"/>,
	/// plus one.
	/// </para>
	/// </summary>
	private static float MillisecondsAt( int index )
		=> (GameClock.Ticks - GameClock.TicksDue + 1 + index) * MillisecondsPerTick;

	/// <summary>The beat, in the units the scripts wait in - see <see cref="GameClock.TickSeconds"/>.</summary>
	private const float MillisecondsPerTick = GameClock.TickSeconds * 1000f;
}
