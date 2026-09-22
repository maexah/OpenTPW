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
/// <b>What this deliberately does not do yet: the SPEED word alone.</b> The original's binder pushes the
/// item's own operating speed into the script's speed word (<c>FUN_0055a300</c>, field <c>+0xc0</c>) and
/// its operating duration into variable 3, which is <see cref="RideVariables.VAR_DURATION"/>, logging
/// "SPEED = %d" and "DUR = %d" as it does. <b>The duration IS pushed now</b> - from the save's own
/// <c>mOperatingDuration</c>, beside the capacity, further down this file; this said neither was, which
/// stopped being true when a ride needed a duration to carry anyone for. The speed stays out, because which key of the item's
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
	/// The scripts of the park currently loaded, or null outside one - the arrangement
	/// <see cref="ParkObjects.Current"/> already uses, and needed for the same reason: something bought
	/// after the park has loaded has to be given a script, and whoever builds it is not holding this.
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
	/// player - and the capacity and duration come from the object record exactly as they do above.
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

		// <b>Nothing is resumed here, and that asymmetry with the load above is deliberate.</b> This is the
		// path a player takes by BUILDING the thing, which is the one moment its construction clip is meant
		// to play: the engine's build path checks role 0 exists, triggers it, and queues role 13 behind it
		// (FUN_00463060), which freezes the model on the clip's last frame once it has run. A save has state
		// to restore and a new thing has none, so calling Resume here would be putting back a past it never
		// had - and would stop the one animation a player is waiting to watch.
		script.Animations = _objects?.AnimationsFor( placed.ThingId )
			?? RideAnimations.Load( item.Directory, item.Stem, _files, item.AnimationChannels );

		if ( script.Animations.Loaded > 0 )
			++Animated;

		Log.Info( $"{ThemeName}: thing {placed.ThingId} ('{item.Name}') now runs {ScriptPathFor( item )}" );

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

				// <b>And what the ride can hold, which nothing else will ever tell its script.</b>
				// Bouncy.RSE declares VAR_CAPACITY and only ever READS it (one CMP against its rider
				// count); it carries no COAST instruction at all. The engine writes it, in FUN_004dd7f0 -
				// which logs "CAPACITY = %d", writes script variable 2, and stores the same number to the
				// object's mOperatingCapacity. That call sits on the open-and-repair path (FUN_004df8f0),
				// beside the writes of VAR_DURATION and the speed.
				//
				// Without it every variable starts at nought, so a ride's own turn reads capacity 0
				// against nought aboard, finds itself FULL, and refuses to invite anybody for ever. That
				// was measured rather than reasoned: a real park ran 2,688 thing ticks with guests queuing
				// and standing at the front of the queue, and not one was ever called aboard.
				//
				// The SAVE's value is used unclamped on purpose. FUN_004dd7f0 clamps the wanted capacity
				// between the item description's own minimum and maximum and then stores the result in
				// mOperatingCapacity - so what the file holds is already the clamped answer, and applying
				// the rule again (against fields nothing here reads) would be doing it twice.
				// Through the constants rather than by spelling the names again here: two spellings of one
				// variable are two things that can drift, which is how this file's own track-type pair
				// went wrong earlier today.
				script.Set( ParkRideOperation.CapacityVariable, placed.OperatingCapacity );
				script.Set( ParkRideOperation.DurationVariable, placed.OperatingDuration );

				// And where this script had got to when the park was saved, which is the whole of why a
				// loaded park does not watch everything in it being built again. Before this, every script
				// started at its own first instruction - and for the Belly Bounce that instruction is
				// WAITANIM 0 0, the construction clip, so the ride hatched out of its egg on every load.
				// See ParkScriptStates, and RideScript.ResumeAt for what it refuses.
				Resume( script, placed, world );

				// Its own thing's player where the thing is standing, so that what the script triggers and
				// what the model is posed from are the same one. Read afresh only where nothing was drawn,
				// which is what a test binding scripts against a park it never builds is doing.
				script.Animations = _objects?.AnimationsFor( placed.ThingId )
					?? RideAnimations.Load( item.Directory, item.Stem, _files, item.AnimationChannels );

				if ( script.Animations.Loaded > 0 )
					++Animated;

				// And the other half of the restore, which has to come AFTER the player exists: putting
				// the script back without putting its model's channels back leaves ten of this park's
				// fourteen things frozen for good. See Restore.
				if ( _saved.TryGetValue( placed.ThingId, out var savedThing ) )
					Restore( script, savedThing, 0 );
			}
		}

		// And tell the gate whether this park is open. This is one of several writes of a script variable
		// from outside a script - the capacity and duration above are two more, and ride operation writes
		// VAR_LETMEON and VAR_LETMEOFF - though it was the only one when this line was written.
		// <b>And the things this park stood that the save never named.</b> The loop above walks
		// world.Objects, so it reaches only what the file placed. A vehicle this park has not used is
		// not in that list at all - the engine makes the thing the first time a crowd of that size
		// arrives rather than shipping one - so the ferry and the seaplane were being stood, drawn, and
		// then left without a script, which is why they sat at their spawns while the bus drove.
		//
		// Nothing new is needed to bind them: ParkFixedItems records which catalogue item each was
		// stood as, and the script path comes from that item exactly as it does above. A thing already
		// bound by the first pass is stepped over, which is how the gates and the lights - which ARE in
		// the save's list - fall out of this without being named here.
		if ( _objects is { } stood )
		{
			// What the first pass managed on its own, so that what this one adds is a measured
			// difference rather than a number inferred from a census counting a different population.
			// Deducing "one of the two failed" from a total whose baseline had never been taken is
			// exactly how the wrong half of this got investigated - and the census that total came
			// from prints a header line of its own, so it was never even counting the same things.
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

				script.Animations = stood.AnimationsFor( thingId )
					?? RideAnimations.Load( item.Directory, item.Stem, _files, item.AnimationChannels );

				if ( script.Animations.Loaded > 0 )
					++Animated;
			}
		}

		// Last, because it needs the binding above to have run.
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
			$"; {ChannelsRestored} animation channels put back" );
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
	/// The channel is started rather than having its timebase copied field by field: the file carries no
	/// frame counts, and the engine recomputes <c>TotalAnimFrames</c> and <c>AnimFrame</c> on the way in.
	/// <b>It does carry the speed, though, and that is restored.</b> Saying the file held nothing further
	/// was wrong - the record's sixth dword lands on the channel's <c>+0xc</c>, and while fourteen of the
	/// fifteen running channels are saved at 1, the Belly Bounce is saved at <b>1.1</b>, so passing a
	/// literal 1 ran the park's only ride at the wrong rate for the whole session.
	/// </para>
	/// </summary>
	private void Restore( RideScript script, SavedThing saved, int now )
	{
		if ( script.Animations is not { } players )
			return;

		for ( var index = 0; index < saved.Channels.Length && index < players.ChannelCount; ++index )
		{
			var channel = saved.Channels[index];

			// The sentinel is the engine's own "this channel was running nothing", and it is the common
			// case: of the 163 channels the shipped park saves, 148 hold it.
			if ( channel.Role == ParkThingStates.NoRole )
				continue;

			// <b>The saved word is the engine's INTERNAL flag field, and only two of its bits mean the
			// same thing to a caller.</b> 0x1 (loop) and 0x8 (do not apply the hide list) carry across
			// unchanged; 0x2 and 0x4 do not. Internally those two say the channel was FROZEN at frame
			// nought or HELD at its last frame, and a caller's 0x2 and 0x4 mean "start at once" and "do
			// not lay the rest pose down" - different questions entirely. Passing the word through
			// therefore read a held channel as a keep-pose request, and AnimTimeControl.Start clears
			// 0x6 on the way in, so the hold was dropped and the clip restarted from frame nought as an
			// ordinary one-shot. Eleven of this park's fifteen restored channels carry 0x4.
			// A channel saved as running carries a real speed; nought only ever appears on one that was
			// not, and those are skipped above. Floored anyway, because a nought here would stop the
			// clip dead rather than play it slowly.
			var speed = channel.Speed > 0f ? channel.Speed : 1f;

			players.Trigger( channel.Role, channel.Entry,
				channel.Flags & (AnimTimeControl.LoopFlag | AnimTimeControl.KeepShownFlag),
				speed, now, index );

			// And then the state it was left in, which the engine expresses by re-entering the channel
			// with a pseudo-role rather than by a flag: both act on the clip just loaded, and Start
			// returns early for them having moved only the timebase - a hold backdates it a whole clip
			// so the elapsed frame lands exactly on the total.
			// The same speed, because a hold backdates the timebase by a whole clip and that arithmetic
			// is done in it - handing these a different figure would put the channel somewhere its own
			// clip never reaches.
			if ( (channel.Flags & HeldAtEnd) != 0 )
				players.Trigger( AnimTimeControl.HoldAtEnd, 0, AnimTimeControl.KeepShownFlag, speed, now, index );
			else if ( (channel.Flags & FrozenAtStart) != 0 )
				players.Trigger( AnimTimeControl.FreezeAtStart, 0, AnimTimeControl.KeepShownFlag, speed, now, index );

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
	/// <b>Within one catalogue id the pairing is unobservable</b> - those records' channels are
	/// identical in this park - so this is an ordering that matches rather than a decoded thing handle,
	/// and nothing here relies on telling two of a kind apart.
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
				+ $"keeps whatever its construction left - {states.Problem}" );

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
	/// Puts one script back where the park file left it - its counter, its variables and its name.
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

		// Last, and only if the position is real: an unknown one would stop the script dead rather than
		// erring, so a thing that cannot be resumed is better left running from its beginning.
		if ( !script.ResumeAt( saved.Position ) )
		{
			Log.Warning( $"{ThemeName}: thing {placed.ThingId} was saved at word {saved.Position} of "
				+ $"{saved.BodyWords}, which is not the start of an instruction, so it starts from the beginning" );

			++NotResumed;
			return;
		}

		++Resumed;
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
