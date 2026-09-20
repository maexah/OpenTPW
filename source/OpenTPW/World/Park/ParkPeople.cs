using System.Linq;

namespace OpenTPW;

/// <summary>
/// The park's guests as a simulation rather than as pictures: what each of them wants, ticked at the
/// game's own beat.
///
/// <para>
/// <b>This is separate from <see cref="ParkGuestSprites"/> on purpose.</b> That one owns what a guest
/// looks like and never runs a tick; this one owns what a guest wants and never touches a vertex. They
/// are joined only by the save that named both, and keeping them apart is what lets the simulation be
/// tested without a graphics device at all.
/// </para>
/// <para>
/// <b>Guests walk, and arriving somewhere now means something.</b> The needs loop is the first of the
/// original's two per-guest calls and the twenty-two state behaviours are the second - see
/// <see cref="PeepBehaviour"/>, which is that second call and which decides what a walk coming to an end
/// amounts to. <b>Thirteen of the twenty-two are built</b> - this said five, and named choosing, judging
/// the fee and waiting for the gate as the three that were not, all of which have since landed.
/// <para>
/// <b>They choose now.</b> A guest judges the admission fee, pays it, decides where to go, walks there,
/// joins the queue and steps up it, is invited aboard, rides, and is let off at the exit. What stops a
/// guest is no longer a missing handler but a missing thing to want: only two of this park's objects can
/// be offered at all. Bringing them up one at a time is how the ride VM was done, and it is why this
/// could be trusted at each step rather than all at once at the end.
/// </para>
/// </para>
/// </summary>
public sealed class ParkPeople : Entity
{
	/// <summary>
	/// The simulation this park is running, so the debug console can read the census back. Same
	/// arrangement as <see cref="ParkGuestSprites.Current"/>, and it exists for the console alone.
	/// </summary>
	internal static ParkPeople? Current { get; private set; }

	private readonly List<Peep> _peeps;

	/// <summary>
	/// The same guests again, by thing id. Built once beside <see cref="_walks"/> rather than searched for,
	/// because a ride's turn asks "who is at the head of my queue" by id and would otherwise walk the whole
	/// list per ride per tick - see <see cref="ParkRideOperation"/>, which takes exactly this shape.
	/// </summary>
	private readonly Dictionary<int, Peep> _byId = [];

	private readonly Dictionary<int, PeepWalk> _walks = [];

	private readonly Dictionary<int, SpriteScript> _sprites = [];

	// Kept rather than rebuilt, so a guest who arrives after the load can be given a walk. It closes
	// over the park, which never changes, so holding it costs nothing and cannot go stale.
	private readonly Func<int, int, StepDirection, bool>? _blocked;

	// The balance stack, for what a new guest starts with - see Admit.
	private readonly ParkBalance? _balance;

	// The next free thing id and sprite slot for somebody who was not in the save. Both are one past
	// the highest the file used. <b>Neither is provably free:</b> the reader surfaces people and
	// objects, and the save holds things it does not - the economy manager among them - so this is
	// "above everything that can be seen" rather than "unused". It has held for this park.
	private int _nextThingId;
	private int _nextSpriteSlot;

	/// <summary>
	/// What each guest is doing, and what arriving somewhere means - the original's <c>FUN_005019f0</c>.
	/// One for the park rather than one per guest, because it carries the park's own facts: whether the
	/// gates are open, and how many visitors have ever been let in.
	/// </summary>
	private readonly PeepBehaviour _behaviour;

	/// <summary>
	/// The park's five members of staff, which are a different simulation from its guests - see
	/// <see cref="Staff"/> for why they are a separate type rather than a guest with a job.
	/// </summary>
	private readonly List<Staff> _staff;

	private readonly Dictionary<int, PeepWalk> _staffWalks = [];

	/// <summary>
	/// What each member of staff is doing - the shared half of the original's five per-kind behaviours.
	/// One for the park, as <see cref="_behaviour"/> is, because the constants it reads are the park's.
	/// </summary>
	private readonly StaffBehaviour _staffBehaviour;

	/// <summary>
	/// A thing's own ride script, or null where nothing binds one - <c>ParkRides.ScriptFor</c> through the
	/// scheduler.
	///
	/// <para>
	/// <b>A delegate rather than the rides themselves, for the reason the gate already gives:</b> a ride's
	/// turn needs one script per thing and nothing else from them, and taking the object would tie the
	/// people to the scripts for far more than that. It also sidesteps the construction order -
	/// <see cref="Level"/> builds the rides before the people, so the people cannot be handed to them.
	/// </para>
	/// </summary>
	private readonly System.Func<int, RideScript?>? _scriptFor;

	/// <summary>
	/// What a ride's turn rolls with. Only <see cref="Peep.SetState"/> reads it, and none of the states a
	/// ride puts a guest into consults it, so the seed is immaterial - it exists because the call asks for
	/// one.
	/// </summary>
	private readonly Random _rideRandom = new();

	/// <summary>
	/// The mode every edge question in this park is asked in.
	///
	/// <para>
	/// <b>Measured, not chosen.</b> The original keeps it in a field of the navigator at <c>+0xb4</c>, read
	/// by the pathfinder at <c>0050f931</c>, by the steering step at <c>0050f501</c> and twenty times over by
	/// <c>avoid_walls</c>. A scan of all 881,521 instructions in the executable finds exactly one
	/// instruction that writes that field on a navigator - <c>0051009f</c>, in the constructor at
	/// <c>FUN_0050ffe0</c>, and it writes zero. So zero is what every person in the game walks in, and this
	/// is a reproduction rather than a default. (The other hundred writes to <c>+0xb4</c> in the image belong
	/// to other structures entirely - particles, interface objects and stack frames.)
	/// </para>
	/// </summary>
	public const int WalkingMode = 0;

	/// <param name="balance">
	/// The park's balance stack, for the five numbers that turn an admission fee into an opinion. Null
	/// leaves the fee unjudged and a guest standing at the booths - see
	/// <see cref="PeepBehaviour.Admission"/>.
	/// </param>
	/// <param name="gateStatus">
	/// What the gate's own script says it is doing, which is <c>ParkRides.GateStatus</c>. Taken as a
	/// delegate rather than as the rides themselves, because this needs one number from them and taking
	/// the object would tie the people to the scripts for nothing else.
	/// </param>
	/// <param name="state">
	/// The park's own running state - the balance, the visitor count and the mutable cells. Null makes one
	/// from <paramref name="park"/>, which is what a test wants; a park being played hands in the one the
	/// level owns, so that everything reads and moves the same numbers.
	/// </param>
	/// <param name="catalogue">
	/// Everything this theme sells, which the level has already read once. Handed on so that a guest
	/// choosing where to go can score a thing by what it actually is rather than by where it stands -
	/// see <see cref="ParkRideChooser"/>. Null leaves that arm scoring on distance and queue alone.
	/// </param>
	public ParkPeople( ParkWorld? park, ParkBalance? balance = null, System.Func<int>? gateStatus = null,
		ParkState? state = null, ParkItemCatalogue? catalogue = null,
		System.Func<int, RideScript?>? scriptFor = null )
	{
		_scriptFor = scriptFor;

		_peeps = PeepsIn( park );

		// Indexed here rather than searched for on demand. <b>This is no longer built once:</b> Admit adds
		// a guest who was not in the save, so every structure derived from _peeps - this one, _walks and
		// _sprites - has to be added to in the same breath. Admit is the only place that may do it.
		foreach ( var peep in _peeps )
			_byId[peep.ThingId] = peep;

		_balance = balance;

		// One past the highest the file used, for anybody who arrives later. Objects and people share the
		// one numbering, so both are counted.
		_nextThingId = 1 + Math.Max(
			park?.People.Count > 0 ? park.People.Max( person => person.ThingId ) : 0,
			park?.Objects.Count > 0 ? park.Objects.Max( placed => placed.ThingId ) : 0 );

		_nextSpriteSlot = 1 + (park?.Sprites.Count > 0 ? park.Sprites.Max( sprite => sprite.Slot ) : 0);

		// What the park charges is on its economy thing and what a guest will put up with is in the
		// balance file, so it takes both - and neither on its own is enough to price the gate.
		var admission = park?.Economy is { } money && balance != null
			? new ParkAdmission( balance, money.AdmissionFee )
			: null;

		// Built from the park rather than from the guests: what a guest does on arrival turns on whether
		// the gates are open and on how many visitors have ever been let in, and both are the park's.
		// And the way a guest asks a ride to take them aboard. It closes over this object because the
		// admission needs both halves that PeepBehaviour lacks - the ride's script, and the park's guests
		// by thing id - and handing it a delegate keeps the guest's turn from depending on ride operation
		// for anything more than a yes or no.
		_behaviour = new PeepBehaviour( park, random: null, admission, gateStatus, state, catalogue,
			( ride, personId ) => new ParkRideOperation( State, Guests )
				.AdmitPerson( _scriptFor?.Invoke( ride.ThingId ), ride, personId ),
			( ride, tick ) => new ParkRideOperation( State, Guests )
				.CompleteAdmission( _scriptFor?.Invoke( ride.ThingId ), ride.ThingId, tick, _rideRandom ) );

		// Staff take the balance stack alone: every constant they run on is a per-grade entry in it, and
		// none of what a guest needs - the fee, the gate - means anything to them.
		_staff = StaffIn( park );
		_staffBehaviour = new StaffBehaviour( balance, random: null, park );

		Current = this;

		if ( park != null )
		{
			var blocked = _blocked = CellEdge.For( park, WalkingMode ).Blocked;
			var saved = park.People.ToDictionary( person => person.ThingId, person => person );
			var pictures = park.Sprites.ToDictionary( picture => picture.Slot );

			foreach ( var peep in _peeps )
			{
				if ( !saved.TryGetValue( peep.ThingId, out var person ) )
					continue;

				// The heading is seeded from the file rather than left at zero: a guest who has not taken a
				// step yet faces the way they were saved facing, and only a step they actually take turns
				// them. Starting everyone at zero would swing the whole park round on the first frame.
				_walks[peep.ThingId] = new PeepWalk( peep.Navigator, blocked )
				{
					Heading = person.Angle
				};

				// And the animation is picked up exactly where the park was saved - which script, and how
				// far through it. The same argument as the heading, and the file is emphatic about it: the
				// sixteen people saved mid-walk are stopped at seven different pictures of the one cycle,
				// so starting them all at the first would put the entire park in step with itself.
				if ( pictures.TryGetValue( person.SpriteSlot, out var picture ) )
				{
					var sprite = new SpriteScript(
						picture.Script, picture.Pc, picture.SpriteNumber, picture.Frame );

					// <b>And when it first comes due, which the original's constructor does as the sprite
					// is made.</b> Ours left Due at nought, so every sprite was due on the first turn the
					// clock had passed - one interval early, once, at load. ScheduleFrom existed and was
					// tested eleven times over without ever being called; the codepath audit found it.
					// Nought is the clock at load, which is the only moment either of these is built.
					//
					// <b>NOT pinned by the suite, and that is measured.</b> Taking this away again leaves
					// all 777 tests green: SpriteScriptTests seeds Due itself in eight places, and no
					// test drives the park-load path. The tests modelled the original while production
					// did not, and a green suite could not tell the difference in either direction.
					sprite.ScheduleFrom( 0 );

					_sprites[peep.ThingId] = sprite;
				}
			}

			// And the staff, seeded exactly as the guests are and for the same two reasons: they are saved
			// facing a particular way and part-way through a picture, and starting either afresh would turn
			// the whole park on its first frame.
			foreach ( var member in _staff )
			{
				if ( !saved.TryGetValue( member.ThingId, out var person ) )
					continue;

				_staffWalks[member.ThingId] = new PeepWalk( member.Navigator, blocked )
				{
					Heading = person.Angle
				};

				if ( pictures.TryGetValue( person.SpriteSlot, out var picture ) )
				{
					var sprite = new SpriteScript(
						picture.Script, picture.Pc, picture.SpriteNumber, picture.Frame );

					sprite.ScheduleFrom( 0 );

					_sprites[member.ThingId] = sprite;
				}
			}
		}

		Log.Info( $"People: {_peeps.Count} guests and {_staff.Count} staff simulating" );
	}

	/// <summary>
	/// Puts one new guest at <paramref name="cellX"/>, <paramref name="cellY"/> - somebody who was not
	/// in the save. Answers their thing id, or nought where the park cannot take one.
	///
	/// <para>
	/// <b>This is what an arrival is.</b> The original's manager (<c>FUN_004cf3e0</c>) makes exactly one
	/// of these per thing tick while a vehicle is unloading, through <c>FUN_004cf720</c>, which picks a
	/// cell and constructs a person on it. Nobody is ever carried inside the vehicle, here or there.
	/// </para>
	/// <para>
	/// <b>Five places have to learn about them, and missing any one fails quietly in its own way.</b>
	/// <see cref="_peeps"/> is the simulation; <see cref="_byId"/> is how a ride finds who is at its
	/// queue head; <see cref="_walks"/> is the only reason they move; <see cref="_sprites"/> is the only
	/// reason they are drawn; and <see cref="ParkState.StandOn"/> is what puts them in a cell's
	/// occupancy list - without which the gate cannot see them, which is a fault this park has had
	/// before.
	/// </para>
	/// <para>
	/// <b>Their needs are a deviation and are declared as one.</b> The balance file states a starting
	/// cash (<c>PeepTypes[x].StartingCash</c>) and a starting exit level (<c>PeepInfo.ExitLevel</c>,
	/// "starting value... in SECONDS") and says nothing at all about hunger, thirst, toilet, vomit,
	/// litter or happiness. Those six begin at nought here because a number had to be chosen, not
	/// because anything was decoded.
	/// </para>
	/// </summary>
	internal int Admit( int cellX, int cellY, int personType = 0, int spriteBank = 0 )
	{
		if ( _blocked == null || !ParkState.OnMap( cellX, cellY ) || _peeps.Count == 0 )
			return 0;

		var one = ParkWorld.NavigatorState.One;
		var pattern = _peeps[0].Navigator;

		var thingId = _nextThingId++;
		var slot = _nextSpriteSlot++;

		// The middle of the cell, the way every other position in this park is measured - and the way a
		// passing test already builds the bus stop's own coordinates.
		var x = (cellX * one) + (one / 2);
		var y = (cellY * one) + (one / 2);

		var cash = _balance?.Int( $"PeepTypes[{personType}].StartingCash", 300 ) ?? 300;
		var exitLevel = _balance?.Int( "PeepInfo.ExitLevel", 120 ) ?? 120;

		// MaxSpeed is factor * 0.2 of a cell per thing tick (FUN_00510190) and the shipped park's guests
		// carry 1.2 of it; MaxForce has no derivation written down, so it is taken from a guest already
		// here rather than invented.
		var navigator = new ParkWorld.NavigatorState(
			X: x, Y: y, VelocityX: 0, VelocityY: 0, TargetX: x, TargetY: y,
			Mass: ParkWorld.NavigatorState.DefaultMass,
			Radius: ParkWorld.NavigatorState.DefaultRadius,
			MaxForce: pattern.MaxForce, MaxSpeed: pattern.MaxSpeed,
			NavMode: 0, CantReachDest: 0, PathFinished: true,
			PathCount: 0, PathTotalCount: 0, PathBufferCount: 0,
			BufferedDistance: 0, TailDistance: 0, TotalDistance: 0, StuckBits: 0 );

		// <b>A deviation, and the reason is that the faithful path is not buildable yet.</b> The engine
		// constructs a guest in Deciding and walks them in from outside through WalkingOutside and
		// AtTheBusStop - both of which take their cells from FUN_004d8650, whose balance-file pair is
		// unproven, so PeepBehaviour deliberately answers neither. Left in Deciding out here a guest
		// stands for ever: Decide looks for somewhere inside the park, and they are outside it and
		// unadmitted. AtGate is the head of the admission sequence the original joins them to anyway -
		// it picks a ticket booth and sends them to be charged - so this starts them there and skips
		// the walk in. Put it back the moment that cell pair is measured.
		var guest = new ParkWorld.GuestState(
			State: (int)PeepState.AtGate, SavedState: ParkWorld.GuestState.Deciding,
			PersonType: personType, Cash: cash, ExitLevel: exitLevel,
			Happiness: 0f, Thirst: 0f, Hunger: 0f, Toilet: 0f, Vomit: 0f, Litter: 0f,
			MajorDest: 0, QueuePos: 0, PrankeryIndex: 0 );

		var peep = new Peep( thingId, guest, navigator );

		_peeps.Add( peep );
		_byId[thingId] = peep;
		_walks[thingId] = new PeepWalk( peep.Navigator, _blocked )
		{
			Heading = PeepBehaviour.ArrivalHeading
		};

		var person = new ParkWorld.Person(
			ThingId: thingId, Model: ParkWorld.GuestModel, RawX: x >> 8, RawY: y >> 8,
			SpriteSlot: slot, Angle: PeepBehaviour.ArrivalHeading,
			Navigator: navigator, Guest: guest );

		// The bank has to be one this park already packs - see ParkGuestSprites.Add.
		var picture = new ParkWorld.Sprite(
			Slot: slot, Type: 0, Bank: spriteBank, SpriteNumber: 0,
			X: cellX, Height: 0f, Y: cellY, Facing: person.Facing,
			Frame: 0, Alpha: 255, State: 0, Script: SpriteScript.None, Pc: 0 );

		// Standing to begin with - the one picture every one-shot animation ends by jumping into - and
		// PeepBehaviour puts them on Walking itself as soon as they take a step. Built through Start
		// rather than by naming a script number, because which script an animation means is the sprite
		// table's business and the shipped guests are on two different ones.
		//
		// ScheduleFrom is handed nought rather than the tick, which the load path does for the same
		// reason and with the same consequence: it comes due one interval early, once. Admit has no
		// clock of its own, and a standing sprite coming due a turn early cannot be seen.
		var animation = new SpriteScript( SpriteScript.None, 0, spriteNumber: 0, frame: 0 );

		animation.Start( SpriteScript.Standing );
		animation.ScheduleFrom( 0 );

		_sprites[thingId] = animation;

		ParkGuestSprites.Current?.Add( person, picture );

		_behaviour.State.StandOn( thingId, cellX, cellY );
		_behaviour.State.Admit();

		Log.Info( $"People: guest {thingId} arrived at ({cellX},{cellY}) - {_peeps.Count} guests now" );

		return thingId;
	}

	/// <summary>
	/// Every guest the save named, as a running copy. Staff are left out of <i>this</i> list because they
	/// are a different kind with a block and a behaviour of their own - both of which are now read and
	/// run, by <c>StaffIn</c> just below and by <c>StaffBehaviour.Step</c> from the update. This said
	/// nothing read the block and nothing ran the machines, which was the reason at the time.
	///
	/// <para>
	/// Static, and takes the park rather than reaching for one, so that a test can build the same list
	/// from the same file without constructing an entity - the rule the park tests already follow for
	/// the file system.
	/// </para>
	/// </summary>
	internal static List<Peep> PeepsIn( ParkWorld? park )
		=> park == null
			? []
			: [.. park.People
				.Where( person => person.Guest != null )
				.Select( person => new Peep( person.ThingId, person.Guest!.Value, person.Navigator ) )];

	/// <summary>
	/// Every member of staff the save named, as a running copy - the five kinds of person that are not
	/// model 1.
	/// </summary>
	internal static List<Staff> StaffIn( ParkWorld? park )
		=> park == null
			? []
			: [.. park.People
				.Where( person => person.Staff != null )
				.Select( person => new Staff(
					person.ThingId, person.Model, person.Staff!.Value, person.Navigator ) )];

	/// <summary>Every guest, in the order the save lists them.</summary>
	internal IReadOnlyList<Peep> Peeps => _peeps;

	/// <summary>
	/// The park's guests by thing id - what <see cref="ParkRideOperation"/> takes, so that a ride can ask
	/// what the guest at the head of its queue is doing without being handed the whole simulation.
	/// </summary>
	internal IReadOnlyDictionary<int, Peep> Guests => _byId;

	/// <summary>Every member of staff, in the order the save lists them.</summary>
	internal IReadOnlyList<Staff> Staff => _staff;

	/// <summary>
	/// How many of the game's 31ms ticks pass between turns of the thing engine.
	///
	/// <para>
	/// <b>The thing engine is NOT on the 31ms beat, and believing it was made every guest in the park run
	/// eight times too fast.</b> The park loop gates it at <c>0054f668</c> -
	/// <c>TEST byte ptr [0x00877d34],0x7</c> then <c>JNZ</c> - so the whole block below that test, which
	/// contains <b>both</b> routes to <c>FUN_00516380</c> (the direct call at <c>0054f7bb</c> and
	/// <c>FUN_005166b0</c> at <c>0054f760</c>), runs only when the counter divides by eight. That counter
	/// is the loop's own tick, incremented once per step at <c>0054f4cd</c>/<c>0054f4d6</c> and zeroed at
	/// park entry.
	/// </para>
	/// <para>
	/// <b>The arithmetic that confirms it.</b> A person's <c>MaxSpeed</c> is set by <c>FUN_00510190</c> as
	/// <c>factor * 13107.2</c>, and 13107.2 is <c>0.2 * 65536</c> - so a factor of one is a fifth of a cell
	/// per <i>thing</i> tick. The shipped park's guests carry 15728, which is a factor of exactly 1.2. At
	/// eight game ticks to a thing tick that is <b>0.96 cells a second</b>, a walking pace; at one it is
	/// 7.7, which is what a park looked like before this existed.
	/// </para>
	/// </summary>
	public const int ThingTickEvery = 8;

	/// <summary>
	/// How many of the game's 31ms ticks pass between turns of the sprite system, which plays the
	/// animations - see <see cref="SpriteScript"/>.
	///
	/// <para>
	/// <b>Two, and it is a different gate from the thing engine's eight.</b> The park loop reaches
	/// <c>FUN_00475360</c> at <c>0054f5fb</c>, inside the same 31ms loop, but behind a test of its own at
	/// <c>0054f5d7</c> - <c>TEST AL,0x1</c> then <c>JNZ</c> straight past it - so the sprites turn on even
	/// ticks only. That is 62ms, and it is exactly the interval the sprite constructor writes at
	/// <c>004759c4</c>, which is what makes a sprite left on that default come due every other turn.
	/// </para>
	/// </summary>
	public const int SpriteTickEvery = 2;

	/// <summary>
	/// How long one of the game's ticks is in whole milliseconds, which is what the sprite system counts in.
	///
	/// <para>
	/// <b>Written as an integer on purpose.</b> <see cref="GameClock.TickSeconds"/> is <c>0.031f</c>, and
	/// the nearest float to it is a shade under - so <c>(int)( GameClock.TickSeconds * 1000f )</c> comes
	/// out as <b>30</b>, not 31. Deriving it the obvious way would run every animation in the park slow by
	/// a thirty-first, and drift further the longer the park stayed open.
	/// </para>
	/// </summary>
	public const int MillisecondsPerTick = 31;

	/// <summary>
	/// One turn of every guest for each thing tick that has come due - see
	/// <see cref="ThingTickEvery"/>, which is why that is not every 31ms tick.
	///
	/// <para>
	/// The tick <i>number</i> is worked back rather than counted locally, because the guests are spread
	/// across four slots by <c>id &amp; 3</c> and a local counter would put them in the wrong ones. By
	/// the time an entity updates, <see cref="GameClock.Ticks"/> already counts this frame's ticks, so
	/// the first of them is that many less <see cref="GameClock.TicksDue"/>, plus one -
	/// <see cref="ParkRides"/> works its own instants back the same way and for the same reason.
	/// </para>
	/// </summary>
	protected override void OnUpdate()
	{
		for ( var i = 0; i < GameClock.TicksDue; ++i )
		{
			var tick = GameClock.Ticks - GameClock.TicksDue + 1 + i;

			// The park loop steps the sprites BEFORE it reaches the thing gate - FUN_00475360 at 0054f5fb,
			// the gate at 0054f668 - so they are taken in that order here too.
			if ( (tick & (SpriteTickEvery - 1)) == 0 )
			{
				foreach ( var playing in _sprites.Values )
					playing.Step( tick * MillisecondsPerTick );
			}

			if ( (tick & (ThingTickEvery - 1)) != 0 )
				continue;

			// <b>The number handed on is the THING tick, not the game tick, and that is not cosmetic.</b>
			// Peep.Tick spreads guests across four slots by (id & 3) == (tick & 3); every game tick that
			// reaches here is a multiple of eight, and eight divides four, so passing the game tick would
			// make that test true only for guests whose id divides four and starve the other three
			// quarters of their needs for ever. The original has the same split and reads a separate
			// counter for it.
			var thingTick = tick / ThingTickEvery;

			foreach ( var peep in _peeps )
			{
				peep.Tick( thingTick );

				var playing = _sprites.GetValueOrDefault( peep.ThingId );

				// Every thing tick, and not one in four: the share gates the needs alone, and the
				// behaviours are a separate call the original never gates. FUN_0050b360 makes both of
				// these for every guest, back to back, needs first.
				//
				// The STATE is asked before the walk rather than after it, which is the original's order:
				// FUN_005019f0 switches on what a guest is doing and only then asks whether they got
				// anywhere, so a guest in a state that does not walk never reaches the walk at all.
				if ( _walks.TryGetValue( peep.ThingId, out var walk ) )
					_behaviour.Step( peep, walk, playing, thingTick );

				// FUN_004d4190, whose only caller is the per-guest needs call - so what the walk asked for
				// lands on that guest's own turn in four rather than at once. Which side of the walk it
				// sits on is not established, and cannot matter here: the walk asks for an animation only
				// when the sprite is not already playing it, so a turn either way changes nothing after
				// the first.
				if ( playing != null && peep.DueOn( thingTick ) )
					Apply( peep, playing );
			}

			// The staff run on the same beat. FUN_0050b360 switches on the thing's model byte and gives
			// every person-kind a needs call and a behaviour call back to back, so a member of staff takes
			// their turn exactly where a guest takes theirs - there is no second clock.
			//
			// <b>The needs half is deliberately absent for staff.</b> A guest's needs are hunger, thirst
			// and the rest; a staff member's are their pay and their training, which nothing here runs.
			foreach ( var member in _staff )
			{
				var playing = _sprites.GetValueOrDefault( member.ThingId );

				// <b>The GAME tick, not the thing tick, and the difference is a factor of eight.</b> A
				// guest's behaviours take the thing tick because nothing in them compares against a clock;
				// a staff member's idle countdown does, and what it reads is named: FUN_004d6410 tests
				// against [DAT_0080239c + 0x1da70c], which the executable's own field table pairs with
				// mGameTick. Handing over the thing tick would have made every staff member idle eight
				// times as long as the balance file asks.
				//
				// <b>What is still not established</b> is whether mGameTick advances once per 31ms step or
				// once per turn of the thing engine - the same open question PeepBehaviour.Step records for
				// a guest. It decides how long staff pause between decisions and nothing else, so it is
				// named here rather than guessed at.
				if ( _staffWalks.TryGetValue( member.ThingId, out var walk ) )
					_staffBehaviour.Step( member, walk, playing, tick );

				if ( playing == null )
					continue;

				if ( member.NextAnimation != 0 )
				{
					playing.Start( member.NextAnimation );
					member.NextAnimation = 0;
				}

				if ( member.NextInterval != 0 )
				{
					playing.Interval = member.NextInterval;
					member.NextInterval = 0;
				}
			}

			TakeTheRidesTurns( thingTick );
		}
	}

	/// <summary>
	/// Every ride's turn, on the same beat the people take theirs - the original's <c>FUN_004e0b90</c> and
	/// <c>FUN_004e0e00</c>, which <c>FUN_0050b360</c> reaches for a model-3 thing exactly as it reaches a
	/// guest's needs and behaviours for a model-1 one.
	///
	/// <para>
	/// <b>It lives here because the original ticks every thing from ONE sweep.</b> <c>FUN_00516380</c>
	/// walks the whole thing list once and dispatches on the model byte, so guests, staff and rides all
	/// come off the same loop - which is what this method is. The class is named for its people and now
	/// does a little more than that; renaming it would be a change to a great deal of unrelated code.
	/// </para>
	/// <para>
	/// <b>The scripts have already advanced when this runs</b>, and that ordering is the original's:
	/// <c>Game_StateMachine</c> calls the script system at <c>0054f56b</c>, before the thing gate at
	/// <c>0054f668</c>. So a variable a ride writes here is read by its script on the following turn
	/// rather than this one.
	/// </para>
	/// </summary>
	private void TakeTheRidesTurns( int thingTick )
	{
		if ( _scriptFor == null || _behaviour.Park is not { } world )
			return;

		var operation = new ParkRideOperation( _behaviour.State, Guests );

		foreach ( var thing in world.Objects )
		{
			var script = _scriptFor( thing.ThingId );

			// FUN_004e0b90's tail. States 3 and 4 return before ever reaching it.
			if ( thing.State is not (3 or ParkRideChoice.StateRefusedFour) )
				operation.DropStaleQueueHead( thing.ThingId );

			// FUN_004e0e00 is a switch on mState and nothing else.
			switch ( thing.State )
			{
				// FUN_004e14e0: invite, then let anybody off unless the ride has broken. Everything else
				// that function does is the breakdown and condemned transitions, which nothing here models.
				case 0:
					// <b>The watchdog the tail of FUN_004e1220 runs on every turn that does not
					// invite.</b> Invite bails while the ride already holds a nominee, and the only
					// other thing that clears one is CompleteAdmission on success - so a guest who was
					// called forward and then stopped heading for the ride would hold the nomination for
					// ever and nobody else could be called. DropUnreadyNominee existed, was tested, and
					// nothing had ever called it.
					//
					// <b>Measured before wiring, and it is NOT a fault anybody has seen:</b> over 50
					// samples of a live park the Belly Bounce held a nominee in 8 of them and the longest
					// unbroken hold was 2, so nominations clear on their own here. This closes a dead
					// path and matches the original's order; it does not fix an observed freeze.
					// <b>NOT PINNED BY THE SUITE, and that is measured rather than assumed.</b> Unwiring
					// this again leaves all 774 tests green. ParkTickTests does drive the real turn, but
					// it asserts the handshake SUCCEEDING, and this fires only on a turn that does not
					// invite - so no test reaches it. Exercising it wants a STALE nominee, which the
					// shipped park never produces: over 50 samples the longest hold was 2. It stands on
					// fidelity to FUN_004e1220's tail and on that measurement, not on coverage.
					if ( operation.Invite( script, thing, TrackTypeOf( thing ) ) == 0 )
						operation.DropUnreadyNominee( thing );

					if ( script != null && script[ParkRideOperation.BrokenVariable] == 0 )
						operation.Dismiss( script, thing, thingTick, _rideRandom, WalkFor, _behaviour.Park,
							_behaviour.Catalogue );

					break;

				// Closing or broken: finish whoever was mid-admission, then let them off.
				case ParkRideChoice.StateRefusedOne:
				case 2:
				case ParkRideChoice.StateRefusedFour:
					operation.CompleteAdmission( script, thing.ThingId, thingTick, _rideRandom );
					operation.Dismiss( script, thing, thingTick, _rideRandom, WalkFor, _behaviour.Park,
						_behaviour.Catalogue );
					break;
			}
		}
	}

	/// <summary>
	/// What kind of track an item runs on, for the one gate in <see cref="ParkRideOperation.Invite"/> that
	/// reads the item rather than the object. Reached the same way <c>ParkRideChooser.ItemFor</c> reaches
	/// it, so there is one lookup rather than two that can disagree.
	/// </summary>
	private int TrackTypeOf( ParkWorld.CatalogueObject thing )
		=> _behaviour.Catalogue is { } catalogue && catalogue.TryGet( thing.CatalogueId, out var item )
			? item.TrackType
			: 0;

	/// <summary>
	/// Hands a guest's queued animation and interval to their sprite - <c>FUN_004d4190</c>, which tests each
	/// against zero rather than assigning it, so that "nothing was asked for" and "run as fast as you can"
	/// stay different things.
	/// </summary>
	private static void Apply( Peep peep, SpriteScript playing )
	{
		if ( peep.NextAnimation != 0 )
		{
			playing.Start( peep.NextAnimation );
			peep.NextAnimation = 0;
		}

		if ( peep.NextInterval != 0 )
		{
			playing.Interval = peep.NextInterval;
			peep.NextInterval = 0;
		}
	}

	/// <summary>
	/// This guest's walk, looked up by thing id. Used in production by the ride turn, which hands it to
	/// <c>Dismiss</c> so a guest let off can be walked to the exit, as well as by the tests and the debug
	/// console. This said it was for the tests and the console alone.
	/// </summary>
	internal PeepWalk? WalkFor( int thingId ) => _walks.GetValueOrDefault( thingId );

	/// <summary>This staff member's walk, for the same.</summary>
	internal PeepWalk? StaffWalkFor( int thingId ) => _staffWalks.GetValueOrDefault( thingId );

	/// <summary>
	/// This person's walk, whoever they are. Guests and staff are held in separate pools, but their thing
	/// ids come from one numbering, so asking each in turn is unambiguous.
	/// </summary>
	/// <remarks>
	/// <b>Alexah found this by playing: "the staff still don't walk".</b> They do. The staff census shows
	/// the guard and the researcher taking 71 and 72 distinct positions in a single run, both with routes.
	/// It was the DRAWING that could not see it: it asked <see cref="WalkFor"/>, which knows only
	/// <c>_walks</c>, got null for every member of staff, and <see cref="ParkGuestSprites.Standing"/> then
	/// fell back to the position the save left them at. They were simulated, routed, moving - and drawn
	/// standing still for the whole run, which is indistinguishable from a behaviour that never ran.
	/// </remarks>
	internal PeepWalk? AnyWalkFor( int thingId )
		=> _walks.GetValueOrDefault( thingId ) ?? _staffWalks.GetValueOrDefault( thingId );

	/// <summary>
	/// The thing this guest is being carried by and the node they are carried on, or false when they are
	/// not on anything.
	///
	/// <para>
	/// <b>Alexah found this by playing: the children never appear ON the ride, bouncing - their sprite
	/// stays at the front of the queue until the ride is over.</b> That is half right of the original,
	/// which is what made it confusing rather than obviously broken: nothing in the engine moves a rider
	/// either. All five callers of its "place a person" routine are accounted for - the ride exit, a
	/// generic put-down, a wrapper, the handyman's litter arm and dropping a staff member - and not one
	/// of them is a rider. Their world position legitimately stays where they queued, and the DRAWING
	/// puts them on the ride's own node. We did the first half and never the second.
	/// </para>
	/// <para>
	/// <b>Asked of the scripts rather than of the guest.</b> <see cref="Peep.MajorDest"/> would be the
	/// obvious handle and is the wrong one: several arms clear it. The ride's script holds the guest's
	/// thing id in the slot <c>BOUNCE</c> filled in, so it is the only thing that knows.
	/// </para>
	/// </summary>
	internal bool TrySeatOf( int guestThingId, out int rideThingId, out int node )
	{
		if ( _scriptFor != null && _behaviour.Park is { } world )
		{
			foreach ( var thing in world.Objects )
			{
				if ( _scriptFor( thing.ThingId ) is not { } script )
					continue;

				if ( !script.TryBounceNode( guestThingId, out node ) )
					continue;

				rideThingId = thing.ThingId;

				return true;
			}
		}

		rideThingId = 0;
		node = 0;

		return false;
	}

	/// <summary>
	/// What a ride's bounce node is called in its model - node nought is <c>body</c> and the rest are
	/// <c>body01</c> upwards.
	/// </summary>
	/// <remarks>
	/// <b>Measured off <c>bouncy.MD2</c>, and corroborated twice over.</b> Its ten rider nodes are named
	/// <c>body</c>, <c>body01</c> .. <c>body09</c> and carry ids 1 to 10 - so the name order and the id
	/// order agree, and there are exactly as many as <c>Bouncy.RSE</c> declares bounce slots. They sit
	/// nine to twelve units up in the air above the ride, which is where a bouncing rider belongs.
	/// <para>
	/// <b>Why by name rather than by id.</b> A node's id is only unique within its capability: id 1
	/// belongs to <c>body</c>, <c>air</c>, <c>camera</c> and <c>body11</c> in this one model, and what
	/// the capability word means is not decoded. A lookup on the bare number would have drawn riders on
	/// the camera. The names carry no such ambiguity, and <c>body10</c> upwards belong to other groups
	/// and are never reached because the slots stop at nine.
	/// </para>
	/// </remarks>
	internal static string BounceNodeName( int node )
		=> node == 0 ? "body" : $"body{node:00}";

	/// <summary>
	/// What each placed thing's script is doing, and who it is carrying.
	///
	/// <para>
	/// <b>Written because four different faults produce one symptom.</b> Riders were drawn at the front
	/// of the queue, and that is equally consistent with: a guest being <see cref="PeepState.Riding"/>
	/// while holding no bounce slot; the script never reaching <c>BOUNCE</c>; the park's objects not
	/// being reachable from the drawing; and the node lookup failing on a slot that is properly filled.
	/// Each wants a different fix, and no census here could tell them apart - there was no ride census
	/// at all.
	/// </para>
	/// </summary>
	internal IEnumerable<string> RideCensus()
	{
		yield return $"objects {(ParkObjects.Current == null ? "NOT REACHABLE - Current is null" : "reachable")}";

		if ( _scriptFor == null )
		{
			yield return "no script lookup was handed in, so no ride runs a script here";
			yield break;
		}

		if ( _behaviour.Park is not { } world )
		{
			yield return "no park";
			yield break;
		}

		foreach ( var thing in world.Objects )
		{
			if ( _scriptFor( thing.ThingId ) is not { } script )
				continue;

			// A variable a script does not declare is not a fault worth throwing a census over.
			string Read( string name )
			{
				try { return script[name].ToString(); }
				catch ( Exception ) { return "-"; }
			}

			// Every animation player, not just the first. The _CH family gives a sideshow one channel per
			// lane - the Jungle Spray declares three in UsageInfo.NumSimultAnims - so a census showing only
			// channel nought would make two idle lanes look exactly like two playing ones, which is the
			// shape of fault this census exists to tell apart.
			string Players()
			{
				if ( script.Animations is not { } players )
					return "none";

				return string.Join( ", ", Enumerable.Range( 0, players.ChannelCount ).Select( index =>
				{
					if ( players.Channel( index ) is not { } channel )
						return $"{index}:MISSING";

					if ( channel.IsIdle )
						return $"{index}:idle";

					// HELD is the bit GETANIM_CH answers -1 for, and -1 is the only answer that lets a
					// rider off - so it is the single most useful thing this line can say.
					var held = (channel.Flags & AnimTimeControl.KeepPoseFlag) != 0 ? " HELD" : "";

					return $"{index}:role {channel.AnimID} entry {channel.SubAnim} "
						+ $"frame {channel.AnimFrame:0.0}/{channel.TotalAnimFrames:0.0}{held}";
				} ) );
			}

			var aboard = script.Bouncing().ToArray();

			var seats = aboard.Length == 0
				? "nobody"
				: string.Join( ", ", aboard.Select( slot =>
				{
					var node = BounceNodeName( slot.Node );

					if ( ParkObjects.Current is not { } objects )
						return $"{slot.Handle}@{slot.Node}'{node}' (objects unreachable)";

					return objects.TryNodeOn( thing.ThingId, node, out var at )
						? $"{slot.Handle}@{slot.Node}'{node}' -> ({at.X:0.0},{at.Y:0.0},{at.Z:0.0})"
						: $"{slot.Handle}@{slot.Node}'{node}' -> NODE NOT FOUND";
				} ) );

			yield return $"thing {thing.ThingId,2} cat {thing.CatalogueId} '{script.Name}' "
				// The nominee, because a stale one is invisible otherwise: Invite bails while somebody is
				// nominated, and the only thing that clears a stale nomination is DropUnreadyNominee,
				// which nothing calls. A queue stuck on that would look exactly like a quiet ride.
				+ $"nominee {_behaviour.State.PersonBeingLoaded( thing.ThingId )} "
				+ $"running {script.Running} letmeon {Read( ParkRideOperation.AdmitVariable )} "
				+ $"letmeoff {Read( ParkRideOperation.DismissVariable )} "
				+ $"capacity {Read( ParkRideOperation.CapacityVariable )} "
				+ $"duration {Read( ParkRideOperation.DurationVariable )} "
				+ $"var_running {Read( ParkRideOperation.RunningVariable )} "
				+ $"onride {Read( ParkRideOperation.OnRideVariable )} "
				+ $"bouncing {aboard.Length}: {seats} "
				+ $"channels [{Players()}]";
		}
	}

	/// <summary>This guest's animation, for the drawing, the tests and the debug console.</summary>
	internal SpriteScript? SpriteFor( int thingId ) => _sprites.GetValueOrDefault( thingId );

	/// <summary>
	/// How many guests this park has admitted, counting on from what the save recorded - see
	/// <see cref="PeepBehaviour.VisitorsToDate"/>. Exposed so that a test can watch it move through the
	/// park's own tick rather than by driving the behaviours directly.
	/// </summary>
	internal int Visitors => _behaviour.VisitorsToDate;

	/// <summary>
	/// What this park has taken at the gate since it opened - see <see cref="PeepBehaviour.Takings"/>,
	/// which says why the running total lives on the behaviours rather than on the park itself.
	/// </summary>
	internal int Takings => _behaviour.Takings;

	/// <summary>
	/// The park as it is being played - see <see cref="ParkState"/>. The same object the level owns, when
	/// a park is running; one of this simulation's own, when a test built it from a file alone.
	/// </summary>
	internal ParkState State => _behaviour.State;

	/// <summary>
	/// How happy the park's visitors are, which is the number the management gadget's gauge shows - the
	/// original's <c>FUN_004c7bb0</c>. Its meter asks for it every two seconds: <c>FUN_004a1cd0</c> arms a
	/// 2000ms timer, id 0x80083, and hands back whatever this comes to.
	///
	/// <para>
	/// <b>A shut park reads nought, and that is the original's own early-out rather than a stand-in.</b> It
	/// fetches <c>mParkClosed</c> through <c>FUN_0051a280</c> and, if the park is closed, returns
	/// <c>DAT_0070031c</c> without looking at anybody - and that constant is <b>0.0f</b> in the image. The
	/// same value comes back when nobody qualifies, so an empty park and a shut one read alike. That is
	/// what makes the gauge rest at the bottom rather than in the middle.
	/// </para>
	/// <para>
	/// <b>It is an integer mean, and the arithmetic is reproduced rather than tidied.</b> The original
	/// converts each guest's value with <c>__ftol</c>, masks it to a byte, sums into an integer and
	/// finishes with <c>FILD</c>/<c>FIDIV</c> - an integer division. So the answer moves in whole numbers
	/// and rounds towards nought. The byte mask is identity over the 0-100 these meters are clamped to, so
	/// it is not written out: a mask that can never bite would read as a rule rather than as a no-op.
	/// </para>
	/// <para>
	/// <b>ONE TERM IS DELIBERATELY NOT REPRODUCED, AND IT IS NAMED RATHER THAN GUESSED.</b> The original
	/// counts a guest only where <c>FUN_004fa990</c> agrees, and that is a predicate on the THING - the
	/// call site is <c>MOV ECX,ESI</c> with ESI the guest, not a cell - testing the field at
	/// <c>thing + 8</c> against <c>{0, 1, 3, 9, 10}</c> through five one-line helpers
	/// (<c>FUN_00536310</c> and its neighbours). <b>What that field holds has not been established</b>, so
	/// every guest is counted here rather than a meaning being invented for it. The shipped park's guests
	/// are all outside or at the gate, so no screen can yet tell the two apart - which is the reason to
	/// write the departure down rather than to lean on it.
	/// </para>
	/// </summary>
	internal int AverageHappiness()
	{
		if ( _behaviour.ParkIsClosed || _peeps.Count == 0 )
			return 0;

		var total = 0;

		foreach ( var peep in _peeps )
			total += (int)peep.Happiness;

		return total / _peeps.Count;
	}

	protected override void OnDelete()
	{
		if ( Current == this )
			Current = null;
	}

	/// <summary>
	/// Every guest and what they want, one line each, for the debug console's <c>peeps</c> command.
	/// Needs are shown whole because every one of them is, and a fraction appearing here would mean the
	/// block was being read at the wrong offset.
	/// </summary>
	internal IEnumerable<string> Census()
	{
		foreach ( var peep in _peeps )
		{
			var walk = _walks.GetValueOrDefault( peep.ThingId );
			var playing = _sprites.GetValueOrDefault( peep.ThingId );
			var nav = peep.Navigator;

			// What they LOOK like, which is the half of a guest this census could not see until the
			// scripts ran - a park where nobody animated read exactly like one where everybody did.
			var anim = playing == null
				? "none"
				: $"script {playing.Script} pc {playing.Pc} set {playing.Set} "
					+ $"frame {playing.Frame} every {playing.Interval}ms";

			yield return $"thing {peep.ThingId,2} kind {peep.PersonType} state {peep.State} "
				+ $"(saved {peep.SavedState}) cash {peep.Cash,4} exit {peep.ExitLevel,4} "
				+ $"happy {peep.Happiness,3:0} thirst {peep.Thirst,3:0} hunger {peep.Hunger,3:0} "
				+ $"toilet {peep.Toilet,3:0} vomit {peep.Vomit,3:0} litter {peep.Litter,3:0} "
				+ $"speed {peep.PurposeSpeed} "
				// Where they ARE, which is the half of a person this census could not see until a park
				// was opened and nobody moved. Needs change and position did not, and there was no way
				// to tell those apart from here.
				+ $"at ({nav.Position.X / (float)FixedVector.One:0.000},"
				+ $"{nav.Position.Y / (float)FixedVector.One:0.000}) "
				+ $"vel ({nav.Velocity.X},{nav.Velocity.Y}) "
				+ $"wp {nav.Waypoints.Count}/{nav.TotalWaypoints} cursor {nav.Cursor} "
				+ $"done {nav.Finished} stuck {nav.CannotReach} "
				+ $"walks {Peep.IsAWalkingState( peep.State )} "
				+ $"has {(walk == null ? "no-walk" : walk.HasRoute ? "route" : "no-route")} "
				+ $"anim {anim}";
		}
	}

	/// <summary>
	/// What each member of STAFF is doing. The guest census cannot show them: <c>_peeps</c> is guests
	/// only and staff are a separate list, so a park where no member of staff ever moved read, from
	/// there, exactly like one where they all did.
	/// </summary>
	/// <remarks>
	/// It prints the activity, the idle stamp and whether a route exists because those are what tell the
	/// two standing-still cases apart: somebody stuck in <see cref="StaffActivity.Walking"/> with no route
	/// is a different fault from somebody whose idle spell has simply not elapsed.
	/// </remarks>
	internal IEnumerable<string> StaffCensus()
	{
		foreach ( var member in _staff )
		{
			var walk = _staffWalks.GetValueOrDefault( member.ThingId );
			var nav = member.Navigator;

			yield return $"thing {member.ThingId,2} model {member.Model} {member.Activity} "
				+ $"grade {member.PayGrade} tired {member.Tiredness,3:0} happy {member.Happiness,3:0} "
				+ $"idleSince {member.TimeStartedIdling,4} jobs {member.JobsDone} "
				+ $"patrol {(member.HasPatrolArea ? $"{member.PatrolFrom}-{member.PatrolTo}" : "anywhere")} "
				+ $"at ({nav.Position.X / (float)FixedVector.One:0.000},"
				+ $"{nav.Position.Y / (float)FixedVector.One:0.000}) "
				// Qualified because this class has a Staff PROPERTY, which shadows the type of the same
				// name; and PARENTHESISED because inside an interpolation a bare ':' opens a format
				// specifier, so "global::" would otherwise split into the expression "global" and a
				// format string - which is a compile error rather than a wrong answer, thankfully.
				+ $"walks {(global::OpenTPW.Staff.IsAWalkingState( member.Activity ))} "
				+ $"has {(walk == null ? "no-walk" : walk.HasRoute ? "route" : "no-route")}";
		}
	}
}
