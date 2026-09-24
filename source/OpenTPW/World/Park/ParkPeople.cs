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
	/// The simulation this park is running, so the debug console can read the census back and a sale can
	/// tell the park's people a thing has gone (<see cref="ThingRemoved"/>). Same arrangement as
	/// <see cref="ParkGuestSprites.Current"/>.
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
	/// A sprite the save's own staff of each thing model wear, so that a new hire can be dressed in
	/// one the atlas already holds - see <see cref="Hire"/>. Keyed by MODEL, which is the number a
	/// <see cref="Staff"/> carries.
	/// </summary>
	private readonly Dictionary<int, ParkWorld.Sprite> _staffSprite = [];

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

		// <b>The arrival timer starts now, not at nought.</b> Left at nought the first load is due the
		// instant the park is ticked, because the clock counts from the program starting rather than
		// from this park opening - so a park would get a busload before anybody could look at it. The
		// original resets the same mark (FUN_0041a960) every time a load finishes, and this is the same
		// reset for the load that has not happened yet.
		_arrivalMark = GameClock.Ticks;

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
				.CompleteAdmission( _scriptFor?.Invoke( ride.ThingId ), ride.ThingId, tick, _rideRandom ),
			// And the third: telling a thing's script how the visit went, which only something holding the
			// script lookup can do. A script that declares no such variable takes the write nowhere, which
			// is the right answer for the shop - Coconut.RSE declares VAR_PARAM and never reads it.
			( ride, outcome ) =>
				_scriptFor?.Invoke( ride.ThingId )?.Set( ParkRideOperation.OutcomeVariable, outcome ) );

		// Staff take the balance stack alone: every constant they run on is a per-grade entry in it, and
		// none of what a guest needs - the fee, the gate - means anything to them.
		_staff = StaffIn( park );
		_staffBehaviour = new StaffBehaviour( balance, random: null, State );

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
					// <b>NOT pinned by the suite, and that is measured.</b> Taking this away again left
					// the whole suite green when that was measured (777 tests), and it has not been
					// re-measured since: SpriteScriptTests seeds Due itself in eight
					// places, and no test drives the park-load path. The tests modelled the original
					// while production did not, and a green suite could not tell the difference in
					// either direction.
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

					// Kept so that somebody hired later can WEAR a pair this park already packs - see
					// Hire. The atlas is built once from the banks the save's own people wear and
					// nothing adds to it, so a newcomer in an unpacked bank has no picture at all and
					// reads as a broken hire rather than a missing texture.
					_staffSprite[member.Model] = picture;
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

		// ParkState owns the ONE numbering objects and people share - see ParkState.NextThingId, which
		// records the collision that made this necessary. The counter here is the fallback for a
		// ParkPeople built without a running park, which the tests do.
		var thingId = ParkState.Current?.NextThingId() ?? _nextThingId++;
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
	/// Puts a hired candidate into the park at a cell. Answers their thing id, or nought.
	///
	/// <para>
	/// <b>It is <see cref="Admit"/> for staff, and the list of things that have to learn about them is
	/// the same shape</b> - the simulation, the walk, the animation, the drawing and the cell's
	/// occupancy - with two differences. There is no by-id index, because <see cref="_byId"/> is how a
	/// ride finds who is at its queue head and staff never queue. And <see cref="ParkState.Admit"/> is
	/// NOT called: that counts <i>visitors</i>, and an employee is not one.
	/// </para>
	/// <para>
	/// <b>No money moves here.</b> Hiring has no fee in the original - <c>BaseCostPerStaff</c> sits in
	/// the balance file unread - and the wage is monthly. See <see cref="ParkStaffPool"/>.
	/// </para>
	/// </summary>
	internal int Hire( ParkStaffPool.Candidate candidate, int cellX, int cellY )
	{
		if ( _blocked == null || !ParkState.OnMap( cellX, cellY ) )
			return 0;

		var model = ParkStaffPool.ModelFor( candidate.Kind );

		// Dressed in a pair this park already packs, never an invented one - see _staffSprite. A park
		// with nobody of that kind cannot clothe them, and that is said out loud rather than drawn as
		// nothing.
		if ( !_staffSprite.TryGetValue( model, out var picture ) )
		{
			Log.Warning( $"People: nothing in this park wears model {model}, so a " +
				$"{ParkStaffPool.NameOfKind( candidate.Kind ).ToLowerInvariant()} would have no picture - not hired" );

			return 0;
		}

		var one = ParkWorld.NavigatorState.One;
		var pattern = _staff.Count > 0 ? _staff[0].Navigator : _peeps.Count > 0 ? _peeps[0].Navigator : null;

		if ( pattern == null )
			return 0;

		// ParkState owns the ONE numbering objects and people share - see ParkState.NextThingId, which
		// records the collision that made this necessary. The counter here is the fallback for a
		// ParkPeople built without a running park, which the tests do.
		var thingId = ParkState.Current?.NextThingId() ?? _nextThingId++;
		var slot = _nextSpriteSlot++;

		var x = (cellX * one) + (one / 2);
		var y = (cellY * one) + (one / 2);

		var navigator = new ParkWorld.NavigatorState(
			X: x, Y: y, VelocityX: 0, VelocityY: 0, TargetX: x, TargetY: y,
			Mass: ParkWorld.NavigatorState.DefaultMass,
			Radius: ParkWorld.NavigatorState.DefaultRadius,
			MaxForce: pattern.MaxForce, MaxSpeed: pattern.MaxSpeed,
			NavMode: 0, CantReachDest: 0, PathFinished: true,
			PathCount: 0, PathTotalCount: 0, PathBufferCount: 0,
			BufferedDistance: 0, TailDistance: 0, TotalDistance: 0, StuckBits: 0 );

		// Idle, with no patrol area - nought and nought is the whole map, which is what a staff member
		// hired without one keeps. Their training starts at the grade they were hired at.
		var state = new ParkWorld.StaffState(
			State: (int)StaffActivity.Idle, PayGrade: candidate.Grade,
			Happiness: 100f, Tiredness: 100f, JobsDone: 0,
			PatrolBottomLeft: 0, PatrolTopRight: 0, RestArea: 0,
			PercentageThroughGrade: 0, TimeStartedIdling: 0 );

		var member = new global::OpenTPW.Staff( thingId, model, state, navigator );

		_staff.Add( member );
		_staffWalks[thingId] = new PeepWalk( member.Navigator, _blocked );

		var person = new ParkWorld.Person(
			ThingId: thingId, Model: model, RawX: x >> 8, RawY: y >> 8,
			SpriteSlot: slot, Angle: 0, Navigator: navigator, Guest: null, Staff: state );

		var animation = new SpriteScript( SpriteScript.None, 0, spriteNumber: 0, frame: 0 );

		animation.Start( SpriteScript.Standing );
		animation.ScheduleFrom( 0 );

		_sprites[thingId] = animation;

		ParkGuestSprites.Current?.Add( person, picture with { Slot = slot, X = cellX, Y = cellY, Facing = 0 } );

		// Staff have never been in a cell's occupancy list - StandOn is called only from PeepBehaviour,
		// which is guests. This is the first thing to put one there.
		_behaviour.State.StandOn( thingId, cellX, cellY );

		Log.Info( $"People: hired {candidate.Name}, a grade {candidate.Grade} " +
			$"{ParkStaffPool.NameOfKind( candidate.Kind ).ToLowerInvariant()} at {candidate.Wage} a month, " +
			$"as thing {thingId} at ({cellX},{cellY}) - {_staff.Count} staff now" );

		return thingId;
	}

	/// <summary>
	/// Dismisses a member of staff, charging one further month's wage. Answers whether there was one.
	/// </summary>
	/// <remarks>
	/// <b>It is <see cref="Depart"/> for staff, and the worker is deleted outright rather than walked
	/// out</b> - <c>FUN_00505790</c> charges, spawns a particle and calls the thing's delete, with no
	/// state change and no walk to the gate. The severance is exactly one wage, by the same expression
	/// the monthly charge uses.
	/// </remarks>
	/// <summary>Whether a thing id is one of the park's staff.</summary>
	internal bool IsStaff( int thingId ) => _staff.Exists( member => member.ThingId == thingId );

	internal bool Fire( int thingId )
	{
		var at = _staff.FindIndex( member => member.ThingId == thingId );

		if ( at < 0 )
			return false;

		var member = _staff[at];
		var kind = ParkStaffPool.KindFor( member.Model );
		var severance = kind >= 0
			? Level.Current?.StaffPool?.WageFor( kind, member.PayGrade ) ?? 0
			: 0;

		_staff.RemoveAt( at );
		_staffWalks.Remove( thingId );
		_sprites.Remove( thingId );

		_behaviour.State.Forget( thingId );
		ParkGuestSprites.Current?.Remove( thingId );

		if ( severance > 0 )
			_behaviour.State.Spend( severance );

		Log.Info( $"People: dismissed thing {thingId}, a grade {member.PayGrade} " +
			$"{ParkStaffPool.NameOfKind( kind ).ToLowerInvariant()} - one month's wage of {severance} paid, " +
			$"{_staff.Count} staff left" );

		return true;
	}

	/// <summary>The staff member in the player's hand, or nought - the save's <c>mStaffMemberPickedUp</c>.</summary>
	private int _carriedStaff;

	/// <summary>A mechanic's thing model - see <see cref="Staff.Model"/>.</summary>
	private const int MechanicModel = 4;

	/// <summary>Who is being carried, for the debug console.</summary>
	internal int CarriedStaff => _carriedStaff;

	/// <summary>
	/// Picks a member of staff up. They stop doing whatever they were doing and wait to be put down.
	/// </summary>
	/// <remarks>
	/// <b><see cref="StaffActivity.Held"/> already existed for exactly this and nothing ever set it.</b>
	/// Its own doc says "a staff member being carried by the player is put here", and the shared
	/// behaviour switch answers case 7 with an empty body - so a held worker is idle by construction
	/// rather than by a special case. <c>FUN_00505c50</c> also proceeds whatever they were doing;
	/// there is no state it refuses from.
	/// </remarks>
	internal bool PickUp( int thingId )
	{
		var member = _staff.Find( person => person.ThingId == thingId );

		if ( member == null )
			return false;

		// Carrying somebody is a mode of its own, installed over whatever was current: a tool is put away and
		// anything in the hand let go of first, a worker already carried among them.
		if ( ParkHand.LetGo() is { } letGo )
			Log.Info( $"Hand: {letGo}" );

		member.SetActivity( StaffActivity.Held, (int)GameClock.Ticks );
		_carriedStaff = thingId;

		Log.Info( $"People: picked up thing {thingId}" );

		return true;
	}

	/// <summary>
	/// Puts a carried member of staff back down in the cell they stand in, because they were let go of rather
	/// than dropped - the place-worker mode's uninstall (<c>0x0046cdc0</c>), which every way out but a drop runs.
	/// Nothing moves a carried worker, so that is the cell they were picked up from; the original reads it off
	/// the worker too, and puts them down with the drop's own body. Answers what it did.
	/// </summary>
	internal string PutBack()
	{
		var was = _carriedStaff;

		if ( _staff.Find( person => person.ThingId == was ) is not { } member )
		{
			_carriedStaff = 0;

			return $"let go of thing {was}, who is not on the staff";
		}

		var (cellX, cellY) = member.Navigator.Position.Cell;

		return DropStaff( cellX, cellY )
			? $"put thing {was} back down at ({cellX},{cellY}), where they were picked up"
			: $"thing {was} could not be put back down at ({cellX},{cellY}) and is still carried";
	}

	/// <summary>
	/// Puts a carried member of staff down on a cell.
	/// </summary>
	/// <remarks>
	/// <b>It TELEPORTS them, and that is the difference between the two carry modes rather than a
	/// shortcut.</b> A fresh hire is carried by mode type 5, which CONSTRUCTS a worker where it is
	/// clicked; an existing one picked up is carried by type 6, which moves the thing that already
	/// exists to the centre of the cell (<c>FUN_00505ea0</c>). Their destination is not set and no patrol
	/// anchor is written.
	/// <para>
	/// <b>Every kind is set idle here, where the original sets idle all but the mechanic</b>, whom it sends
	/// straight into their job search (<c>FUN_004da5b0</c>) - counted, not built.
	/// </para>
	/// </remarks>
	internal bool DropStaff( int cellX, int cellY )
	{
		if ( _carriedStaff == 0 || !ParkState.OnMap( cellX, cellY ) )
			return false;

		var member = _staff.Find( person => person.ThingId == _carriedStaff );

		if ( member == null )
		{
			_carriedStaff = 0;
			return false;
		}

		var centre = new FixedVector(
			PeepNavigator.WaypointCentre( cellX ), PeepNavigator.WaypointCentre( cellY ) );

		member.Navigator.Position = centre;
		member.Navigator.Target = centre;

		// Put down, not walked - so the previous position moves with them and the drawing does not slide
		// them across the park from wherever they were picked up. Same re-stamp the original's own
		// placement makes at 0x004fa95d.
		member.Navigator.StampPrevious();
		member.SetActivity( StaffActivity.Idle, (int)GameClock.Ticks );

		if ( member.Model == MechanicModel )
			Unimplemented.Report( "MECHANIC_PUT_DOWN_JOB_SEARCH" );

		_behaviour.State.StandOn( member.ThingId, cellX, cellY );

		Log.Info( $"People: put thing {member.ThingId} down at ({cellX},{cellY})" );

		_carriedStaff = 0;

		return true;
	}

	/// <summary>The world state in which nobody arrives at all - <c>FUN_004cf5b0</c>'s first test.</summary>
	private const int NoArrivalsWorldState = 4;

	/// <summary>
	/// The most people the original will let a park hold offline, from the cap in <c>FUN_004cf5b0</c>
	/// that logs "Capping the number of people in o...". Online it is 500 instead.
	/// </summary>
	public const int MostPeopleInAPark = 0x5dc;

	// How many of this load are still to be dropped, which vehicle is bringing them, and the tick the
	// last load finished on. All nought until the first is due.
	private int _arrivalsRemaining;
	private int _arrivalVehicle;
	private int _arrivalMark;

	/// <summary>
	/// Which of the three vehicles brings a crowd this big. <c>FUN_004cf3e0</c> takes the first for a
	/// headcount under <c>0x24</c> and otherwise <c>(0x3c &lt; count) + 2</c>, so the second up to 60 and
	/// the third beyond - and the save's own <c>mArrivalVehicle_Size1..3</c> naming says the same.
	/// </summary>
	internal static int VehicleFor( int people )
		=> people < 36 ? 1 : people > 60 ? 3 : 2;

	/// <summary>The variable a vehicle's script reports itself through, as its own file declares it.</summary>
	private const string VehicleState = "VAR_STATUS";

	/// <summary>
	/// What a vehicle's script spins on until somebody sets it. Ferry.RSE and seaplane.RSE both read
	/// <c>TEST VAR_TRIGGER / ENDSLICE / BRANCH_Z</c> back onto themselves, so a vehicle that is never
	/// told to go stands at the stop for ever - which is exactly how they behaved before this.
	/// </summary>
	private const string VehicleTrigger = "VAR_TRIGGER";

	/// <summary>
	/// The state a vehicle reports once it has arrived and is ready to unload. The original drops one
	/// guest a tick for exactly as long as <c>FUN_0051a690</c> answers this.
	/// </summary>
	private const int VehicleIsUnloading = 2;

	/// <summary>
	/// The state a vehicle reports between runs, waiting to be sent off on the next leg.
	/// </summary>
	private const int VehicleIsIdle = 0;

	/// <summary>
	/// The state a vehicle reports once it has pulled away and is waiting to be released again - the
	/// second of the three points every vehicle script parks at.
	/// </summary>
	private const int VehicleIsLeaving = 4;

	/// <summary>
	/// The state a vehicle reports when it has finished its circuit. <c>FUN_0051a690</c> answers this by
	/// <b>forgetting the vehicle</b> - it clears <c>mCurrentArrivalVehicle</c> and reports -1 instead - so
	/// the next load picks afresh rather than re-using one that has driven off.
	/// </summary>
	private const int VehicleIsSpent = 6;

	/// <summary>
	/// Starts a load of <paramref name="people"/> now, whatever the timer says, and answers which
	/// vehicle that size calls for. Answering the console rather than the park.
	///
	/// <para>
	/// <b>It exists because the second and third vehicles are otherwise unreachable.</b> The headcount
	/// is floored at <c>Arrival.MinPeople</c> - one, in every theme the game ships - and
	/// <see cref="VehicleFor"/> gives one person the bus, so a park left to itself sends the bus every
	/// time and a seaplane is never asked for. Until the crowd is sized from the park's own draw, this
	/// is the only way to watch the other two arrive.
	/// </para>
	/// </summary>
	internal int ForceArrival( int people )
	{
		_arrivalsRemaining = Math.Max( 1, people );
		_arrivalVehicle = VehicleFor( _arrivalsRemaining );

		Log.Info( $"People: {_arrivalsRemaining} arriving by hand, vehicle {_arrivalVehicle} "
			+ $"({ParkFixedItems.VehicleName( _arrivalVehicle )})" );

		return _arrivalVehicle;
	}

	/// <summary>
	/// Makes every guest as thirsty as asked. Answering the console rather than the park.
	///
	/// <para>
	/// <b>It exists for the reason <see cref="ForceArrival"/> does: the condition it creates is otherwise
	/// almost unreachable, and without it a built feature cannot be watched.</b> A drinks shop is chosen on
	/// the thirst term - measured in <c>ParkRideChoiceTests</c>, where a parched guest picks it from four
	/// cells across the park and an unthirsty one picks the ride from the same spot. But a park left alone
	/// hardly ever holds a guest who is thirsty <i>and</i> still deciding: only a quarter of guests grow
	/// thirsty at all (<see cref="Peep.Tick"/> shares the drift by thing id, and 16 divides 4), and by the
	/// time they do their exit countdown has usually run out. Measured over a 400-second run: of 148
	/// samples carrying thirst 50 or more, <b>73 were HeadingForExit and only 11 were Deciding</b>, and 68%
	/// had an exit countdown already past nought.
	/// </para>
	/// <para>
	/// <b>It sets a meter the game itself moves, and nothing else.</b> It does not choose for anybody, does
	/// not place anybody and does not touch a till - whatever happens next is the chooser, the walk and the
	/// settle-up running exactly as they do unattended.
	/// </para>
	/// </summary>
	/// <returns>How many guests were made thirsty.</returns>
	internal int MakeThirsty( float level )
	{
		var touched = 0;

		foreach ( var peep in _peeps )
		{
			peep.Thirst = Math.Clamp( level, Peep.Least, Peep.Most );

			++touched;
		}

		Log.Info( $"People: {touched} guests are now thirst {level}" );

		return touched;
	}

	/// <summary>
	/// The script of whichever vehicle is bringing this load, or null where this park has no such thing
	/// standing or nothing bound to it.
	/// </summary>
	private RideScript? VehicleScript( int vehicle )
	{
		if ( _scriptFor == null || ParkFixedItems.Current is not { } items )
			return null;

		var thing = items.ThingFor( ParkFixedItems.VehicleName( vehicle ) );

		return thing == 0 ? null : _scriptFor( thing );
	}

	/// <summary>
	/// What each of the three vehicles is doing right now - the thing it was stood as, whether a script
	/// is bound, where that script's program counter has got to, and the two variables the arrival
	/// handshake turns on. Answering the console rather than the park.
	///
	/// <para>
	/// <b>A vehicle parked for ever looks exactly like one that is running, in every other census here.</b>
	/// The position lines in <c>paths</c> only move while an animation plays, so a script waiting on a
	/// trigger nobody will send reads as "arrived, and idle between runs". The program counter is the one
	/// number that tells those two apart, which is why this exists at all.
	/// </para>
	/// </summary>
	internal IEnumerable<string> VehicleCensus()
	{
		for ( var vehicle = 1; vehicle <= 3; ++vehicle )
		{
			var name = ParkFixedItems.VehicleName( vehicle );
			var thing = ParkFixedItems.Current?.ThingFor( name ) ?? 0;
			var script = VehicleScript( vehicle );

			if ( script is null )
			{
				yield return $"{name}: thing {thing}, NO SCRIPT BOUND";
				continue;
			}

			var carrying = vehicle == _arrivalVehicle && _arrivalsRemaining > 0
				? $" <- carrying this load, {_arrivalsRemaining} still to drop"
				: "";

			yield return $"{name}: thing {thing} script '{script.Name}' pc {script.Position} "
				+ $"running {script.Running} {VehicleState} {script[VehicleState]} "
				+ $"{VehicleTrigger} {script[VehicleTrigger]}{carrying}";
		}
	}

	/// <summary>
	/// One turn of the arrival manager - <c>FUN_004cf3e0</c>. It waits out a period, decides how many
	/// are coming and on what, and then drops <b>one guest per thing tick</b> until that load is spent.
	///
	/// <para>
	/// <b>The headcount is a deviation and this is the whole of it.</b> The original sizes a load from
	/// a park-attractiveness score summed over the rides (<c>FUN_004c8240</c>: per ride a capacity, a
	/// duration divided down, and a three-entry table indexed off it), divided by
	/// <c>Arrival.PointsPerVisitor</c> and floored at <c>Arrival.MinPeople</c>. That score reads four
	/// ride fields this project has not named, so what is reproduced here is the floor alone - the
	/// smallest load the original would ever send. Everything else about the cycle is the original's:
	/// the period, the world-state refusal, the cap, the one-a-tick drip and the choice of vehicle.
	/// </para>
	/// </summary>
	private void StepArrivals( int thingTick )
	{
		if ( _blocked == null || _behaviour.Park is not { } park )
			return;

		if ( park.WorldState == NoArrivalsWorldState )
			return;

		if ( _arrivalsRemaining > 0 )
		{
			var vehicle = VehicleScript( _arrivalVehicle );

			// <b>The vehicle says when it is ready, which is the original's own handshake.</b> Its
			// script plays its arrival animation, sets VAR_STATUS to 2 and then spins on VAR_TRIGGER;
			// FUN_004cf3e0 drops one guest a tick for exactly as long as FUN_0051a690 reports that.
			//
			// <b>Where there is no script to ask, the guests still come.</b> A vehicle that is missing,
			// unbound, or declares no such variable must not be able to stop a park getting visitors at
			// all - and gating on a state that will never arrive is precisely what would do that.
			if ( vehicle != null && vehicle[VehicleState] != VehicleIsUnloading )
				return;

			var useA = (thingTick & 1) == 0;
			var stopX = 42;
			var stopY = 5;

			if ( _behaviour.Admission is { } admission )
			{
				stopX = useA ? admission.BusStopA.X : admission.BusStopB.X;
				stopY = useA ? admission.BusStopA.Y : admission.BusStopB.Y;
			}

			// A stop that will not take one ends the load rather than retrying it for ever.
			if ( Admit( stopX, stopY ) == 0 )
				_arrivalsRemaining = 0;
			else
				--_arrivalsRemaining;

			if ( _arrivalsRemaining == 0 )
			{
				_arrivalMark = GameClock.Ticks;

				// And send it away. The script will not leave the stop until this changes, so a load
				// that is finished with and never released leaves the vehicle sitting there - said out
				// loud when the script declares no such variable, because a vehicle that never departs
				// looks exactly like one that was never told to.
				if ( vehicle != null && !vehicle.Set( VehicleTrigger, 1 ) )
				{
					Log.Warning( $"People: the {ParkFixedItems.VehicleName( _arrivalVehicle )}'s script "
						+ $"declares no {VehicleTrigger}, so it cannot be sent away" );
				}
			}

			return;
		}

		// The engine's own timer: the game tick shifted down two, against Arrival.TimeBetweenArrivals.
		var period = _balance?.Int( "Arrival.TimeBetweenArrivals", 150 ) ?? 150;

		if ( (GameClock.Ticks >> 2) - (_arrivalMark >> 2) < period )
			return;

		if ( _peeps.Count >= MostPeopleInAPark )
			return;

		_arrivalsRemaining = Math.Max( 1, _balance?.Int( "Arrival.MinPeople", 1 ) ?? 1 );
		_arrivalVehicle = VehicleFor( _arrivalsRemaining );

		Log.Info( $"People: {_arrivalsRemaining} arriving, vehicle {_arrivalVehicle}" );
	}

	/// <summary>
	/// One turn of the vehicle itself, which the original does on <b>every</b> tick and not only while a
	/// load is being dropped - the tail of <c>FUN_004cf3e0</c> at <c>LAB_004cf4b6</c>.
	///
	/// <para>
	/// <b>Every vehicle script parks three times a circuit, and one release is not enough.</b> Each of
	/// them sets a status, spins on <c>TEST VAR_TRIGGER / ENDSLICE / BRANCH_Z</c> back onto itself, and
	/// goes no further until something writes that variable - <c>bus.RSE</c> at instructions 42, 87 and
	/// 117, and the other two the same. Releasing only the first, which is what sending a spent load away
	/// did, leaves the vehicle stopped at the second for ever: measured in a live park as the bus sitting
	/// at pc 90 with <c>VAR_STATUS</c> 4 from 69s to 169s while the park emptied itself.
	/// </para>
	///
	/// <para>
	/// <b>Summoning, releasing and sending away are all one write.</b> <c>FUN_0051a2f0</c> ends at
	/// <c>0x51a66b</c> by setting variable nought - <c>VAR_TRIGGER</c> - to one on the vehicle it already
	/// has standing, and the manager reaches it from every arm: when there is no vehicle, when one reports
	/// idle, when one reports leaving, and when a load is spent. Only a freshly <i>created</i> thing is
	/// treated differently, getting <c>VAR_STATUS</c> = 1 instead.
	/// </para>
	///
	/// <para>
	/// <b>State 2 is deliberately not released while a load is outstanding.</b> That is the one the drip
	/// depends on: the original drops a guest per tick for exactly as long as the vehicle answers 2, so
	/// nudging it early would send the vehicle off with its passengers still aboard.
	/// </para>
	///
	/// <para>
	/// <b>One approximation, named rather than hidden.</b> The original chooses between two sets of states
	/// by <c>FUN_0051a9d0</c>, which answers whether a guest is standing at the stop - a peep (model byte
	/// 1) in state <c>0x15</c>, <see cref="PeepState.AtTheBusStop"/>, on one of the four cells
	/// <c>{c, c+1, c-0x100, c-0xff}</c> around <c>FUN_004d8650</c>'s first cell. <b>Which balance-file
	/// pair that getter returns is still unproven</b> - see <see cref="PeepBehaviour"/>, where the same
	/// open item blocks two states - so the choice between the arms is not reproduced and every state the
	/// original ever nudges is nudged here. The difference is confined to which arm fires, never to
	/// whether a vehicle moves, and no guest in this park reaches those cells to be counted anyway.
	/// </para>
	/// </summary>
	/// <summary>
	/// Whether a vehicle reporting <paramref name="state"/>, with <paramref name="stillToDrop"/> of its
	/// load left, should be sent on - the rule out of <c>FUN_004cf3e0</c>'s arms, on its own so that it
	/// can be read and tested without a park standing around it.
	///
	/// <para>
	/// <b>The one that matters is the refusal.</b> Unloading with somebody still aboard must NOT be
	/// released: the original drops a guest per tick for exactly as long as the vehicle answers 2, so
	/// letting it go early would send it off with its passengers still on it. Every other state the
	/// original ever nudges is nudged.
	/// </para>
	/// </summary>
	internal static bool ReleasesVehicle( int state, int stillToDrop )
		=> state is VehicleIsIdle or VehicleIsLeaving or VehicleIsSpent
			|| (state == VehicleIsUnloading && stillToDrop == 0);

	private void StepVehicle()
	{
		// Nought is "no vehicle", and it must be refused here: VehicleName answers "bus" for anything it
		// does not recognise, so asking about vehicle nought would quietly command the bus.
		if ( _arrivalVehicle == 0 || VehicleScript( _arrivalVehicle ) is not { } vehicle )
			return;

		var state = vehicle[VehicleState];

		if ( !ReleasesVehicle( state, _arrivalsRemaining ) )
			return;

		if ( !vehicle.Set( VehicleTrigger, 1 ) )
		{
			Log.Warning( $"People: the {ParkFixedItems.VehicleName( _arrivalVehicle )}'s script "
				+ $"declares no {VehicleTrigger}, so it cannot be released" );

			return;
		}

		// FUN_0051a690's own answer to state 6: let go of the vehicle, so the next load summons one
		// rather than commanding one that has already driven off.
		if ( state == VehicleIsSpent )
			_arrivalVehicle = 0;
	}

	/// <summary>
	/// Takes a guest out of the park - the other half of <see cref="Admit"/>, and the thing whose
	/// absence kept <see cref="PeepState.Leaving"/> unanswered. Answers whether one went.
	///
	/// <para>
	/// <b>Every list <see cref="Admit"/> added them to has to let go, and one of them is not this
	/// class's.</b> <see cref="ParkState.Forget"/> is what takes them off the map: leaving only the
	/// cell's own chain unlinked would keep their id naming a cell they are no longer on.
	/// </para>
	/// <para>
	/// <b>A guest a thing is holding stays.</b> That is the original's own refusal rather than caution
	/// here - see <see cref="PeepBehaviour.HeldByAThing"/> - and without it a ride or a queue would go
	/// on naming somebody who no longer exists.
	/// </para>
	/// </summary>
	internal bool Depart( int thingId )
	{
		if ( !_byId.TryGetValue( thingId, out var peep ) )
			return false;

		if ( PeepBehaviour.HeldByAThing( peep.State ) )
			return false;

		_peeps.Remove( peep );
		_byId.Remove( thingId );
		_walks.Remove( thingId );
		_sprites.Remove( thingId );

		_behaviour.State.Forget( thingId );

		ParkGuestSprites.Current?.Remove( thingId );

		Log.Info( $"People: guest {thingId} went home - {_peeps.Count} guests now" );

		return true;
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
	/// How far through the current thing tick the frame being drawn is, from nought to one - what the
	/// drawing interpolates a walking person's position with.
	///
	/// <para>
	/// <b>The beat is 248ms, not 31ms, and that is the whole point.</b> The original computes three of
	/// these in one per-frame block off a single clock sample at <c>[0x008786bc]</c>, each against its own
	/// baseline and its own reciprocal: 1/31 for placed objects, 1/62 for particles, and <b>1/248.000007
	/// for peeps and staff</b> (<c>0x0054fa5c</c>, driving <c>FUN_00518f90</c>). Its baseline
	/// <c>[0x00878a1c]</c> is re-stamped at <c>0x0054f683</c>, <i>inside</i> the every-eighth-tick gate -
	/// so the fraction measures time since the last thing sweep, and eight 31ms ticks is 248ms exactly.
	/// </para>
	/// <para>
	/// Clamped, as the original clamps it: <c>FUN_004f9f00</c>'s placement sample is held to [0, 1] with
	/// immediate stores of nought and <c>0x3f800000</c>. So a frame that arrives late draws somebody at
	/// the position the simulation actually reached and never extrapolates past it.
	/// </para>
	/// </summary>
	public static float ThingTickFraction
		=> Math.Clamp( ((GameClock.Ticks % ThingTickEvery) + GameClock.PartialTick) / ThingTickEvery,
			0f, 1f );

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
				// Where they start this tick, before anything moves them - the original's FUN_004fa870,
				// which is the first call of the guest tick handler (0x00501658) and sits ahead of that
				// handler's own (id & 3) stagger. So it runs for every guest on every sweep whatever
				// state they are in, and NOT only for the ones that walk: see
				// PeepNavigator.StampPrevious for why stamping only walkers makes a stopped guest
				// oscillate for ever.
				peep.Navigator.StampPrevious();

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
				// The same stamp, for the same reason: the original gives every person kind a needs call
				// and a behaviour call back to back off one switch, and FUN_00505490 opens with
				// FUN_004fa870 at 0x00505495 exactly as the guest handler does.
				member.Navigator.StampPrevious();

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

			// Anybody who has walked out of the park goes home, taken out here rather than inside the
			// loop above because removing from a list while it is being walked would throw - and
			// walked backwards so that removing one does not skip the next. PeepBehaviour puts them
			// into these states and deliberately does not act on either: it owns what a guest wants,
			// never the list they are in.
			//
			// <b>The deviation is here rather than in the transition that reaches it.</b> The original
			// walks a leaver HeadingForExit -> PickingACellOutside (19) -> AtTheBusStop (21) and
			// deletes them at Leaving (17); 19 and 21 both take their cells from FUN_004d8650, whose
			// balance-file pair is unproven, so neither can be built and a guest reaching 19 would
			// stand there for ever. So 19 is treated as the end of the walk rather than the middle of
			// it. Rerouting HeadingForExit itself was tried first and was worse: it changed a
			// transition the original really makes, and three tests that pin it said so.
			for ( var at = _peeps.Count - 1; at >= 0; --at )
			{
				if ( _peeps[at].State is PeepState.PickingACellOutside or PeepState.Leaving )
					Depart( _peeps[at].ThingId );
			}

			StepArrivals( thingTick );

			// After the load, and on every tick rather than only while one is running - the original
			// reaches its own tail the same way, from every arm above. Without this the vehicle is
			// released once and parks at the next of its three spins for ever.
			StepVehicle();

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
		if ( _scriptFor == null )
			return;

		// The admission goes in for the SETTLE-UP alone: it carries PeepInfo.MediumHappinessChange, which is
		// both what a guest loses when a visit gives them nothing and the multiplier on what winning is
		// worth. Without it both arms leave happiness alone rather than moving it by an invented number.
		var operation = new ParkRideOperation( _behaviour.State, Guests, _behaviour.Admission );

		// <b>The park as it stands, not as the file left it.</b> A thing bought this session lives in
		// ParkState's list and in no other, so a sweep over the save's list hands it no turn at all - it
		// binds a script and animates, and then never invites, never dismisses and never takes a fare,
		// which is a ride that looks alive and is not.
		foreach ( var thing in _behaviour.State.Objects )
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

				// State 3 is what the constructor gives every object whose item is not choosable, so nobody is
				// ever offered it. FUN_004e0e00's jump table sends 3 straight to its return
				// (docs/exe/ride-operation.md, "The second half"), and this does nothing either.
				case 3:
					break;

				// Broken down (1), waiting for an upgrade (2) or condemned (4): finish whoever was
				// mid-admission, then let them off.
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

	/// <summary>
	/// Why each guest is or is not setting off anywhere - what the chooser answers them, and whether a
	/// route to it exists. See <see cref="PeepBehaviour.Explain"/> for why the `peeps` census cannot
	/// answer this and this one has to.
	/// </summary>
	internal IEnumerable<string> WhyCensus()
	{
		foreach ( var (thingId, peep) in _byId )
		{
			if ( !_walks.TryGetValue( thingId, out var walk ) )
				continue;

			yield return $"thing {thingId,3} {peep.State,-18} "
				+ _behaviour.Explain( peep, walk, GameClock.Ticks );
		}
	}

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
	/// Tells everybody in the park a thing has gone - the type-10 message the object destructor
	/// <c>FUN_004dd0a0</c> sends at <c>0x004dd150</c>, after the thing has left the object chain and before
	/// its refund and its script teardown. Every guest and every member of staff answers it on their own:
	/// see <see cref="PeepBehaviour.ThingRemoved"/> and <see cref="StaffBehaviour.ThingRemoved"/>.
	/// </summary>
	/// <remarks>
	/// <b>Being put off makes a sound</b>, the kids' effect <c>0x80</c> where the guest's sprite is - see
	/// <see cref="SoundFor"/> for when and where.
	/// <para>
	/// The original delivers the message in ascending thing id across every kind (<c>FUN_0040fb10</c>).
	/// No answer reads another person, so guests and then staff come to the same thing.
	/// </para>
	/// </remarks>
	internal void ThingRemoved( ParkWorld.CatalogueObject thing )
	{
		var tick = GameClock.Ticks;
		var thingTick = tick / ThingTickEvery;

		foreach ( var peep in _peeps )
		{
			// Asked before the answer, while the ride's script still holds them.
			var seat = peep.MajorDest == thing.ThingId && peep.State == PeepState.Riding
				? SeatOn( thing, peep )
				: null;

			var how = _behaviour.ThingRemoved( peep, thing.ThingId, thingTick );

			if ( how == PeepBehaviour.PutOff.No )
				continue;

			Log.Info( $"People: guest {peep.ThingId} put off thing {thing.ThingId} ({how}), "
				+ $"now {peep.State} with happiness {peep.Happiness:0}" );

			Vector3? heardAt = SoundFor( how, peep.ThingId, thing.Flags, seat is not null ) switch
			{
				PutOffSound.Origin => Vector3.Zero,
				PutOffSound.Seat => seat,
				PutOffSound.Feet => ParkGuestSprites.Feet( peep.Navigator.Position ),
				_ => null
			};

			if ( heardAt is { } at )
				ParkAudio.Current?.PutOff( at );
		}

		foreach ( var member in _staff )
			_staffBehaviour.ThingRemoved( member, _staffWalks.GetValueOrDefault( member.ThingId ), thing.ThingId,
				tick );
	}

	/// <summary>Where the put-off sound plays for one guest - see <see cref="SoundFor"/>.</summary>
	internal enum PutOffSound
	{
		None,
		Origin,
		Seat,
		Feet
	}

	/// <summary>
	/// Whether a guest's answer to a sale makes the put-off sound, and where: always for a rider
	/// (<c>0x004fb3f5</c>), for a queuer only when their thing id is a multiple of eight
	/// (<c>0x0050133d</c>), and for nobody else.
	/// </summary>
	/// <remarks>
	/// <b>A rider's sprite decides the place.</b> On a thing with
	/// <see cref="ParkWorld.CatalogueObject.KeepsRidersSpriteFlag"/> the ride holds the sprite: a bounce rider
	/// is on the seat node. Without the flag, admission destroyed the sprite; the eviction makes a new one
	/// whose position is still nought when the sound reads it (<c>0x004fb3cd</c>, then <c>FUN_004faa00</c>),
	/// so it plays at the world's origin. A thing bought this session carries no such flag yet
	/// (<c>BOUGHT_OBJECT_FLAG_BITS</c>).
	/// <para>
	/// <b>A deviation:</b> a walk-on rider (the Jungle Spray's <c>WALKON</c>) is where the <c>WALK</c> stepper
	/// last put their sprite (<c>FUN_005580a0</c>, <c>FUN_004f9e60</c>). Nothing here places a walk-on rider, so
	/// with no seat the sound plays at the rider's feet.
	/// </para>
	/// </remarks>
	internal static PutOffSound SoundFor( PeepBehaviour.PutOff how, int guestId, int thingFlags, bool seated )
		=> how switch
		{
			PeepBehaviour.PutOff.Riding when (thingFlags & ParkWorld.CatalogueObject.KeepsRidersSpriteFlag) == 0
				=> PutOffSound.Origin,
			PeepBehaviour.PutOff.Riding => seated ? PutOffSound.Seat : PutOffSound.Feet,
			PeepBehaviour.PutOff.Queueing when (guestId & 7) == 0 => PutOffSound.Feet,
			_ => PutOffSound.None
		};

	/// <summary>The seat node a ride's script holds this rider on, in the world, or null.</summary>
	private Vector3? SeatOn( ParkWorld.CatalogueObject thing, Peep rider )
	{
		if ( _scriptFor?.Invoke( thing.ThingId ) is { } script
			&& script.TryBounceNode( rider.ThingId, out var node )
			&& ParkObjects.Current is { } objects
			&& objects.TryNodeOn( thing.ThingId, BounceNodeName( node ), out var seat ) )
			return seat;

		return null;
	}

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
		if ( _scriptFor != null )
		{
			// ParkState's list, so a guest aboard something bought this session is found too. Missed here,
			// the drawing falls back to their ground position and they are drawn standing at the front of
			// the queue instead of on the ride's own node.
			foreach ( var thing in _behaviour.State.Objects )
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

		if ( _behaviour.Park is null )
		{
			yield return "no park";
			yield break;
		}

		// ParkState's list, because this census is the instrument that has to be able to SEE a thing
		// bought this session. Walking the save's list, a bought ride prints no line at all and reads
		// exactly like one that was never built.
		foreach ( var thing in _behaviour.State.Objects )
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
				// Whether it is screaming, because the pause holds a scream's VOICE and not the script:
				// a held park stops the VM one instruction short of STOPSCREAM, so "still screaming"
				// and "gone quiet" have to be readable apart. A pure getter, so polling cannot perturb it.
				+ $"running {script.Running} screaming {script.Screaming} "
				// The longest critical section this script has run in one turn, against the cap that ends
				// one where the original would hang.
				+ $"critical {script.LongestCritical} "
				// How often it took a lock on the last unit of its budget, and the longest section it then ran in
				// that same turn: the arrival the budget's charging decides.
				+ $"lastunit {script.LastUnitLocks} ran {script.LongestLastUnitSection} "
				// WHICH scream, not just whether: a ride that replays a fresh sample every pass and one
				// that loops a single clip for ever both read "screaming True". The sample name and the pass
				// count tell them apart.
				+ $"scream [{ParkAudio.Current?.ScreamState( script.Id ) ?? "no park audio"}] "
				+ $"letmeon {Read( ParkRideOperation.AdmitVariable )} "
				+ $"letmeoff {Read( ParkRideOperation.DismissVariable )} "
				+ $"capacity {Read( ParkRideOperation.CapacityVariable )} "
				+ $"duration {Read( ParkRideOperation.DurationVariable )} "
				+ $"var_running {Read( ParkRideOperation.RunningVariable )} "
				+ $"onride {Read( ParkRideOperation.OnRideVariable )} "
				+ $"bouncing {aboard.Length}: {seats} "
				+ $"channels [{Players()}]";
		}
	}

	/// <summary>
	/// What every thing a guest may be sent to has taken, and why it can or cannot be sent to.
	///
	/// <para>
	/// <b>No other census here can answer the question this one exists for.</b> <c>rides</c> reports what a
	/// script is doing and <c>peeps</c> reports what a guest is carrying, but whether a guest can be
	/// OFFERED a thing at all is decided by a walk over the map that neither of them makes - and that walk
	/// is the whole of why the Drinks Shop and the three toilets were unreachable. So this prints the
	/// computed cell count beside the record's own, which is the one line that tells a shop that is
	/// genuinely refused apart from a shop the filter never considered.
	/// </para>
	/// <para>
	/// <b>It prints every visitable thing rather than the ones that pass</b>, because a census that showed
	/// only the survivors would read identically whether four objects were refused or never looked at -
	/// which is exactly the confusion this replaces (<c>docs/VERIFYING.md</c> rule 85).
	/// </para>
	/// </summary>
	internal IEnumerable<string> SpendCensus()
	{
		if ( _behaviour.Park is not { } world )
		{
			yield return "no park - one has to be loaded";
			yield break;
		}

		var catalogue = _behaviour.Catalogue;

		// ParkState's list for the same reason `rides` takes it. The world is still wanted below, because
		// the queue is walked over the MAP rather than read from the record.
		foreach ( var thing in _behaviour.State.Objects )
		{
			if ( !thing.IsVisitable )
				continue;

			ParkItemCatalogue.Item item = default;
			var described = catalogue != null && catalogue.TryGet( thing.CatalogueId, out item );

			var queue = State.QueueLength( thing.ThingId );
			var (back, cells) = ParkRideChoice.QueueCellsFor( world, thing );
			var offerable = ParkRideChoice.CanBeOffered( thing, queue, TrackTypeOf( thing ), world );

			yield return $"thing {thing.ThingId,2} cat {thing.CatalogueId} "
				+ $"'{(described ? item.Name : "unknown")}' "
				+ $"price {thing.PricePerUse,3} took {State.TakingsFor( thing.ThingId ),6} "
				// The record's own count beside the walked one. They agree wherever the save cached a
				// back-of-queue and differ on exactly the objects that made this work necessary.
				+ $"cells {cells} (record {thing.QueueSizeInCells}) back {back} "
				+ $"queue {queue}/{cells * ParkRideChoice.QueueRoomPerCell} "
				+ $"win {(described ? item.ChanceOfWinning : -1)}% "
				+ $"prize {(described ? item.CostOfGoods : -1)} "
				+ $"OFFERABLE {offerable}";
		}

		yield return $"park balance {State.Balance} gate takings {State.Takings}";
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

			// <b>Where they are HEADING, which no census here could say.</b> Without it a park where
			// nobody ever chooses the shop reads exactly like a park where everybody chooses it and
			// something downstream refuses them - and those want opposite fixes. MajorDest is the thing
			// they picked, nought for a guest who has picked nothing.
			yield return $"thing {peep.ThingId,2} kind {peep.PersonType} state {peep.State} "
				+ $"dest {peep.MajorDest,2} "
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
				+ $"idleSince {member.TimeStartedIdling,4} jobs {member.JobsDone} rest {member.RestArea} "
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
