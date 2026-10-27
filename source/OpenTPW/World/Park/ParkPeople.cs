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
/// amounts to. <b>Twenty-one of the twenty-two are built</b>; the other, the litter-bin errand
/// (<see cref="PeepState.GoingToMinorDestination"/>), stands at the foot of its switch saying what it waits on.
/// <para>
/// <b>They choose now.</b> A guest judges the admission fee, pays it, decides where to go, walks there,
/// joins the queue and steps up it, is invited aboard, rides, and is let off at the exit. Six of this
/// park's objects can be offered: the ride, the sideshow, the drinks shop and the three toilets
/// (<see cref="ParkRideChoice.Offerable"/>).
/// </para>
/// </para>
/// </summary>
public sealed partial class ParkPeople : Entity
{
	/// <summary>
	/// The simulation this park is running, so the debug console can read the census back and a sale can
	/// tell the park's people a thing has gone (<see cref="ThingRemoved"/>). Same arrangement as
	/// <see cref="ParkGuestSprites.Current"/>.
	/// </summary>
	internal static ParkPeople? Current { get; private set; }

	/// <summary>
	/// A guest was made - the original's broadcast <c>0x1c</c> from the guest constructor (<c>0x004fb2fd</c>), which
	/// the all-visitors list answers by adding their row (<c>docs/exe/hud.md</c>, "How allpeeps keeps itself current").
	/// </summary>
	internal event Action<Peep>? GuestArrived;

	/// <summary>
	/// A guest is going - the original's broadcast <c>0x1b</c> from the thing delete (<c>0x0050b7f9</c>, before the
	/// free), which the all-visitors list answers by removing their row. Handed their thing id.
	/// </summary>
	internal event Action<int>? GuestLeaving;

	private readonly List<Peep> _peeps;

	/// <summary>
	/// The same guests again, by thing id. Indexed beside <see cref="_walks"/> rather than searched for,
	/// because a ride's turn asks "who is at the head of my queue" by id and would otherwise walk the whole
	/// list per ride per tick - see <see cref="ParkRideOperation"/>, which takes exactly this shape.
	/// </summary>
	private readonly Dictionary<int, Peep> _byId = [];

	private readonly Dictionary<int, PeepWalk> _walks = [];

	private readonly Dictionary<int, SpriteScript> _sprites = [];

	/// <summary>
	/// Balloons let go and still bursting, each where it was last placed, until its script frees it. Kept here
	/// rather than on their guests, who may go home first.
	/// </summary>
	private readonly List<Balloon> _bursting = [];

	/// <summary>How many banks of each guest kind the park draws over - see <see cref="ParkSpriteBanks"/>.</summary>
	private readonly ParkSpriteBanks _banks;

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

	/// <summary>Whether drops are falling, as the guests' choice asks it - see <see cref="PeepBehaviour.Raining"/>.</summary>
	internal Func<bool> Raining
	{
		get => _behaviour.Raining;
		set => _behaviour.Raining = value;
	}

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
	/// <summary>Which items the park has researched, the save's word first: what the advisor's facts count the kinds of ride on offer by.</summary>
	private readonly ParkResearch? _research;

	private readonly int _gateThing;

	/// <summary>
	/// What a ride's turn rolls with: <see cref="Peep.SetState"/>, which none of the states a ride puts a guest into
	/// consults, and a shop's docks on its ingredient as a guest leaves (<see cref="ParkRideOperation.Dismiss"/>), which
	/// the original draws from the park's one generator.
	/// </summary>
	private readonly Random _rideRandom;

	/// <summary>What an arriving guest's initial values are drawn with - see <see cref="Admit"/>. A test seeds it.</summary>
	private readonly Random _arrivalRandom;

	/// <summary>
	/// The mode every edge question in this park is asked in.
	///
	/// <para>
	/// The original keeps it in a field of the navigator at <c>+0xb4</c>, read by the pathfinder at
	/// <c>0050f931</c>, by the steering step at <c>0050f501</c> and twenty times over by <c>avoid_walls</c>. The
	/// navigator's constructor writes zero (<c>0051009f</c>, <c>FUN_0050ffe0</c>). <b>A guest's is also written
	/// through the guest</b>, whose navigator sits at <c>+0xd4</c>, as <c>+0x188</c>: 1 by the gate's states, the
	/// put-down <c>FUN_004feb50</c> and the leaving arm's retry, 0 again by the leaving arm's failure and state
	/// 18. <b>Every person here walks in mode 0</b>; mode 1 also lets a step leave a path for a cell that is not path,
	/// queue or footprint, bare ground included (<c>0x004d8a37</c>, the <c>mode</c> of <see cref="CellEdge"/>).
	/// </para>
	/// </summary>
	public const int WalkingMode = 0;

	/// <param name="balance">
	/// The park's balance stack: the five numbers that turn an admission fee into an opinion, each kind of
	/// guest's preferred excitement and starting cash, the exit level, and the staff's constants. Null leaves
	/// the fee unjudged and a guest standing at the booths - see <see cref="PeepBehaviour.Admission"/> - and
	/// every kind preferring 50.
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
	/// <param name="random">What an arriving guest's initial values are drawn with. Null for the game; a test seeds one.</param>
	/// <param name="banks">
	/// How many banks of each guest kind the park draws over, which the level reads from the data
	/// (<see cref="ParkSpriteBanks.Read"/>). Null counts none: every arrival a child of bank nought, a costume and a
	/// balloon's colour drawn over nothing, and a balloon's life with no balloon to show.
	/// </param>
	/// <param name="behaviourRandom">Guest decisions; null keeps an independent runtime generator.</param>
	/// <param name="rideRandom">Ride settlement choices; null keeps an independent runtime generator.</param>
	/// <param name="staffRandom">Staff decisions; null keeps an independent runtime generator.</param>
	/// <param name="research">
	/// The park's lab, which the level owns and the buy list reads; a researcher's hand-out spends into it. Null makes
	/// one from <paramref name="park"/>, which is what a test wants.
	/// </param>
	public ParkPeople( IParkInitialState? park, ParkBalance? balance = null, System.Func<int>? gateStatus = null,
		ParkState? state = null, ParkItemCatalogue? catalogue = null,
		System.Func<int, RideScript?>? scriptFor = null, Random? random = null, ParkSpriteBanks? banks = null,
		Random? behaviourRandom = null, Random? rideRandom = null, Random? staffRandom = null, ParkResearch? research = null )
	{
		_scriptFor = scriptFor;
		_research = research ?? (park == null || catalogue == null ? null : new ParkResearch( park.ObjectControlRecords, catalogue,
			park.ResearchProjects, park.ResearchKinds, park.ResearchWorkLoad, ParkResearch.GroupOpensAt( balance ),
			park.ResearchMostExpensiveProject ));
		_gateThing = park?.ParkGates ?? 0;
		_banks = banks ?? new ParkSpriteBanks( 0, 0, 0 );
		_arrivalRandom = random ?? new Random();
		_rideRandom = rideRandom ?? new Random();

		_peeps = PeepsIn( park );

		// Indexed here rather than searched for on demand. <b>This is not built once:</b> Admit adds
		// a guest who was not in the save, so every structure derived from _peeps - this one, _walks and
		// _sprites - has to be added to in the same breath. Admit is the only place that may do it.
		foreach ( var peep in _peeps )
		{
			_byId[peep.ThingId] = peep;

			// What they wear, brought within the banks this park loads, as a person's load does (0x004f93a6); staff are
			// drawn from their saved sprite, brought within theirs as it is drawn (ParkGuestSprites.LookOf).
			peep.SpriteBank = _banks.Reduce( peep.SpriteKind, peep.SpriteBank );
		}

		_balance = balance;

		// Thing 1's training budgets (the save's staff HQ; FileFormats saves.md, "The staff HQ").
		if ( park?.StaffHq is { } hq )
		{
			for ( var kind = 0; kind < TrainingBudgets.Length && kind < hq.Budgets.Count; ++kind )
				TrainingBudgets[kind] = hq.Budgets[kind];

			HaveEverTrained = hq.HaveEverTrained;
		}

		// One past the highest the file used, for anybody who arrives later. Objects and people share the
		// one numbering, so both are counted.
		_nextThingId = 1 + Math.Max(
			park?.People.Count > 0 ? park.People.Max( person => person.ThingId ) : 0,
			park?.Objects.Count > 0 ? park.Objects.Max( placed => placed.ThingId ) : 0 );

		_nextSpriteSlot = 1 + (park?.Sprites.Count > 0 ? park.Sprites.Max( sprite => sprite.Slot ) : 0);

		// The arrival timer carries on the wait the park was saved in: its mark is the save's mTimeSig, counted
		// against ParkState.GameTick, the save's mGameTick, as FUN_005179c0 loads both (park.md, "Arrivals").
		_arrivalMark = park?.Arrival.TimeSig ?? 0;

		// A load saved half-dropped is carried on: the manager's turn reads mOffloading and mPeopleOnBus as it
		// finds them (FUN_004cf3e0), so the rest are dropped once a vehicle answers that it is unloading.
		if ( park?.Arrival is { Offloading: true } savedLoad )
		{
			_offloading = true;
			_arrivalsRemaining = savedLoad.PeopleOnBus;
		}

		// The vehicle the save holds current (mCurrentArrivalVehicle, a thing id) is current here: whichever of the
		// three sizes' things it is. Its script is resumed with the others, so its status is the save's. One that
		// is none of the three's is counted and not current.
		_vehicleThings = [park?.ArrivalVehicleForSmallCrowd ?? 0, park?.ArrivalVehicleForMediumCrowd ?? 0, park?.ArrivalVehicleForLargeCrowd ?? 0];

		if ( park is { CurrentArrivalVehicle: not 0 } )
		{
			_arrivalVehicle = Array.IndexOf( _vehicleThings, park.CurrentArrivalVehicle ) + 1;

			if ( _arrivalVehicle == 0 )
				Unimplemented.Report( "SAVED_CURRENT_ARRIVAL_VEHICLE" );

			Log.Info( $"People: the save's current arrival vehicle is thing {park.CurrentArrivalVehicle}, "
				+ (_arrivalVehicle != 0 ? $"vehicle {_arrivalVehicle} ({ParkFixedItems.VehicleName( _arrivalVehicle )})" : "none of its three: no vehicle current") );
		}

		// What the park charges is on its economy thing, kept by the running park so the entry-price screen can
		// move it, and what a guest will put up with is in the balance file: the gate takes both, and reads the
		// fee from the running park each time it is asked.
		var running = state ?? new ParkState( park );
		var admission = park?.Economy != null && balance != null
			? new ParkAdmission( balance, running )
			: null;

		// Built from the park rather than from the guests: what a guest does on arrival turns on whether
		// the gates are open and on how many visitors have ever been let in, and both are the park's.
		// And the way a guest asks a ride to take them aboard. It closes over this object because the
		// admission needs both halves that PeepBehaviour lacks - the ride's script, and the park's guests
		// by thing id - and handing it a delegate keeps the guest's turn from depending on ride operation
		// for anything more than a yes or no.
		_behaviour = new PeepBehaviour( park, behaviourRandom, admission, gateStatus, running, catalogue,
			( ride, personId ) => new ParkRideOperation( State, Guests )
				.AdmitPerson( _scriptFor?.Invoke( ride.ThingId ), ride, personId ),
			( ride, tick ) => new ParkRideOperation( State, Guests )
				.CompleteAdmission( _scriptFor?.Invoke( ride.ThingId ), ride.ThingId, tick, _rideRandom ),
			// And the third: telling a thing's script how the visit went, which only something holding the
			// script lookup can do. A script that declares no such variable takes the write nowhere, which
			// is the right answer for the shop - Coconut.RSE declares VAR_PARAM and never reads it.
			( ride, outcome ) =>
				_scriptFor?.Invoke( ride.ThingId )?.Set( ParkRideOperation.OutcomeVariable, outcome ),
			// And the fourth: the ride's side of a guest walking away from its door, too expensive - it
			// forgets them (FUN_004e0ac0) and they leave its queue (FUN_004ddd20), both through its script.
			( ride, personId ) =>
			{
				var script = _scriptFor?.Invoke( ride.ThingId );

				new ParkRideOperation( State, Guests ).Forget( script, ride.ThingId, personId );
				ParkRideOperation.LeaveQueue( State, script, ride.ThingId, personId );
			},
			// And the fifth and sixth, for a queuer put out by their own turn or by a failed walk to their place:
			// the ride lets go of them (FUN_004ddd20, through its script), and the queue walk asks whether each
			// guest it passes still queues.
			( ride, personId ) =>
				ParkRideOperation.LeaveQueue( State, _scriptFor?.Invoke( ride.ThingId ), ride.ThingId, personId ),
			StillQueueing,
			// And the balance, for what each type of guest likes when they choose and when they arrive.
			balance )
		{
			// The park's own weather, asked at each choice: shelter is worth more while drops fall.
			Raining = static () => ParkWeather.Current is { Drops: > 0 },
			// And the bus's report, which a guest heading for the gate runs for.
			BusStatus = BusStatus,
			VehicleStatus = VehicleStatus,
			LargerVehicleCurrent = () => LargerVehicleIsCurrent,
			MayCrossTheRoad = MayCrossTheRoad,
			// And who stands near a deciding guest, which only the holder of everybody's walk can say.
			EntertainerToWatch = EntertainerToWatch,
			BalloonBeside = BalloonBeside
		};

		// Staff take the balance stack alone: every constant they run on is a per-grade entry in it, and
		// none of what a guest needs - the fee, the gate - means anything to them.
		_staff = StaffIn( park );
		Strikes = new ParkStrikes( park?.StaffHq );
		_staffBehaviour = new StaffBehaviour( balance, staffRandom, State )
		{
			Lab = _research,
			Strikes = Strikes,
			GateStatus = gateStatus,
			GuestsInside = () => GateGuestCensus,
			ScriptFor = scriptFor,
			TrackTypeOf = TrackTypeOf,
			StaffById = id => _staff.Find( member => member.ThingId == id ),
			GuestsNear = GuestsNear,
			StateGroupsOf = member => BankOf( member.ThingId )?.StateGroupsInUse ?? 0,
			Sound = static ( member, effect ) =>
			{
				if ( ParkGuestSprites.Feet( member.Navigator.Position ) is { } feet )
					ParkAudio.Current?.StaffSound( effect, feet );
			}
		};

		// A cell edit that measures a queue again tells the people in it - see QueueRemeasured - and the
		// park's door closes and opens the rides through their scripts - see DoorMoved.
		State.QueueRemeasured = QueueRemeasured;
		State.DoorMoved = DoorMoved;

		Current = this;

		if ( park != null )
		{
			var blocked = _blocked = CellEdge.For( park, WalkingMode ).Blocked;
			var saved = park.People.ToDictionary( person => person.ThingId, person => person );
			var pictures = park.Sprites.ToDictionary( picture => picture.Slot );

			// The balloon slots a guest's record names; a balloon on any other was let go.
			var named = new HashSet<int>();

			foreach ( var peep in _peeps )
			{
				if ( !saved.TryGetValue( peep.ThingId, out var person ) )
					continue;

				// The heading is seeded from the file rather than left at zero: a guest who has not taken a
				// step yet faces the way they were saved facing, and only a step they actually take turns
				// them. Starting everyone at zero would swing the whole park round on the first frame.
				_walks[peep.ThingId] = new PeepWalk( peep.Navigator, blocked )
				{
					Ground = State,
					Heading = person.Angle
				};

				// And the animation is picked up exactly where the park was saved - which script, and how
				// far through it. The same argument as the heading, and the file is emphatic about it: the
				// sixteen people saved mid-walk are stopped at seven different pictures of the one cycle,
				// so starting them all at the first would put the entire park in step with itself.
				if ( pictures.TryGetValue( person.SpriteSlot, out var picture ) )
				{
					_sprites[peep.ThingId] = SavedSprite( park, picture );
				}
				else if ( person.SpriteSlot == 0 )
				{
					// A rider on a thing that keeps no sprite was saved with none: admission destroyed it (0x00502147),
					// and the original makes it again from what they wear as the thing lets them go (FUN_004d4140).
					// Nothing here destroys a rider's sprite, so it is made again now, standing where they are.
					var slot = _nextSpriteSlot++;
					var one = (float)ParkWorld.NavigatorState.One;
					var again = new SpriteScript( SpriteScript.None, 0, spriteNumber: 0, frame: 0 );

					again.Start( SpriteScript.Standing );
					again.ScheduleFrom( 0 );

					_sprites[peep.ThingId] = again;

					ParkGuestSprites.Current?.Add( person with { SpriteSlot = slot }, new ParkWorld.Sprite(
						Slot: slot, Type: peep.SpriteKind, Bank: peep.SpriteBank, SpriteNumber: 0,
						X: person.Navigator.X / one, Height: 0f, Y: person.Navigator.Y / one, Facing: person.Facing,
						Frame: 0, Alpha: 255, State: 0, Script: SpriteScript.None, Pc: 0 ) );

					Log.Info( $"People: guest {peep.ThingId}, saved with no sprite, is given one again on slot {slot}" );
				}

				// And the balloon, by the slot the guest names: the table is saved slot for slot, the balloon's own
				// sprite with it (FUN_00475730). A slot that is not a balloon's is not taken for one.
				if ( person.Guest is { BalloonScript: not 0 } guest
					&& pictures.TryGetValue( guest.BalloonScript, out var held ) && held.Type == Balloon.SpriteKind )
				{
					peep.Balloon = Balloon.Saved( held );
					named.Add( guest.BalloonScript );
				}

				// And the bubble over them, by its slot, as the script its sprite was made on shows it.
				if ( person.Guest is { ThoughtScript: not 0 } thinker
					&& pictures.TryGetValue( thinker.ThoughtScript, out var thought ) && thought.Type == Thoughts.SpriteKind )
					peep.Thoughts.Restore( thinker.LastThought, thinker.TimeBubbleShown, thought.Script );
			}

			// A balloon saved let go and still bursting is a sprite of the table that no guest names any more
			// (0x004fe96b): it goes on bursting where it was, inside its fade's loop.
			foreach ( var orphan in pictures.Values.Where( sprite => sprite.Type == Balloon.SpriteKind && !named.Contains( sprite.Slot ) ).OrderBy( sprite => sprite.Slot ) )
				_bursting.Add( Balloon.Saved( orphan, (park as ParkWorld)?.SpriteLoopsOf( orphan.Slot ) ) );

			if ( _bursting.Count > 0 )
				Log.Info( $"People: {_bursting.Count} balloons of the save's let go and still bursting" );

			// And the staff, seeded exactly as the guests are and for the same two reasons: they are saved
			// facing a particular way and part-way through a picture, and starting either afresh would turn
			// the whole park on its first frame.
			foreach ( var member in _staff )
			{
				if ( !saved.TryGetValue( member.ThingId, out var person ) )
					continue;

				_staffWalks[member.ThingId] = new PeepWalk( member.Navigator, blocked )
				{
					Ground = State,
					Heading = person.Angle
				};

				if ( pictures.TryGetValue( person.SpriteSlot, out var picture ) )
				{
					_sprites[member.ThingId] = SavedSprite( park, picture );
					_staffLooks[member.ThingId] = (picture.Type, picture.Bank);
				}
				else
					_staffLooks[member.ThingId] = (person.SpriteKind, person.SpriteBank);

				if ( person.Staff is { ThoughtScript: not 0 } thinker
					&& pictures.TryGetValue( thinker.ThoughtScript, out var thought ) && thought.Type == Thoughts.SpriteKind )
					member.Thoughts.Restore( thinker.LastThought, thinker.TimeBubbleShown, thought.Script );
			}
		}

		if ( _sprites.Values.Count( sprite => sprite.Loops.Count > 0 ) is > 0 and var looping )
			Log.Info( $"People: {looping} of the save's people's sprites inside a loop of their script" );

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
	/// occupancy list, which the entry booths read.
	/// </para>
	/// <para>
	/// Initial values follow <c>FUN_004faec0</c>, including the base constructor's speed draw first
	/// (<c>docs/exe/guest-arrivals.md</c>). The draws here still use a separate <see cref="Random"/>;
	/// the original uses its shared world generator and reseeds it with the guest's id before choosing
	/// the child bank. <see cref="ParkSpriteBanks.ChildOf"/> reproduces that bank, but not the reseed's
	/// effect on later world draws. The entrance-state deviation is described below.
	/// </para>
	/// </summary>
	/// <param name="personType">
	/// Which kind of guest to make. Null draws one, as the original's constructor does for every guest it makes;
	/// only a test names one.
	/// </param>
	internal int Admit( int cellX, int cellY, int? personType = null )
	{
		if ( _blocked == null || !ParkState.OnMap( cellX, cellY ) )
			return 0;

		// Base constructor first, then the eight direct guest draws in docs/exe/guest-arrivals.md.
		var pace = new ParkWorld.PaceState(
			AdjustorSpeed: 0,
			BaseSpeed: Peep.BaseSpeeds[(uint)_arrivalRandom.Next() % Peep.BaseSpeeds.Length],
			PreviousSpeed: 0f,
			PurposeSpeed: Peep.HurryingSpeed );

		var exitVariation = _balance?.Int( "PeepInfo.ExitLevelVar", 60 ) ?? 60;
		var exitLevel = (_balance?.Int( "PeepInfo.ExitLevel", 120 ) ?? 120)
			+ _arrivalRandom.Next() % (2 * exitVariation) - exitVariation;

		// The shipped balance stacks all define eight kinds. The parser's general row-count rule is Q54.
		// An instrument's forced kind still consumes the original draw.
		var drawnType = (byte)(_arrivalRandom.Next() % ParkWorld.GuestState.PersonTypes);
		var type = personType ?? drawnType;
		var cashVariation = _balance?.Int( "PeepInfo.StartingCashVarPc", 15 ) ?? 15;
		var cashPercent = _arrivalRandom.Next() % (2 * cashVariation + 1) - cashVariation + 100;
		var startingCash = _balance?.Int( $"PeepTypes[{type}].StartingCash", 300 ) ?? 300;
		var cash = Math.Max( 0, unchecked( cashPercent * startingCash ) / 100 );
		var thirst = (uint)_arrivalRandom.Next() % 50;
		var hunger = (uint)_arrivalRandom.Next() % 50;
		var toilet = (uint)_arrivalRandom.Next() % 30;
		_ = _arrivalRandom.Next();
		var prankery = (uint)_arrivalRandom.Next() % 100
			< (uint)(_balance?.Int( "PeepInfo.PrankeryLikelihood", 5 ) ?? 5);

		var one = ParkWorld.NavigatorState.One;

		// ParkState owns the ONE numbering objects and people share, the original's free list - see
		// ParkState.NextThingId. The counter here is the fallback for a ParkPeople built without a running
		// park, which the tests do.
		var thingId = ParkState.Current?.NextThingId() ?? _nextThingId++;

		// A full thing table makes nobody (ParkState.NextThingId).
		if ( thingId == 0 )
			return 0;

		var slot = _nextSpriteSlot++;

		// The middle of the cell, the way every other position in this park is measured - and the way a
		// passing test already builds the bus stop's own coordinates.
		var x = (cellX * one) + (one / 2);
		var y = (cellY * one) + (one / 2);


		// The mover's constructor's force and speed, a speed of one (FUN_0050ffe0), which the guest's first turn
		// eases over before they take a step (Peep.Pace).
		var navigator = new ParkWorld.NavigatorState(
			X: x, Y: y, VelocityX: 0, VelocityY: 0, TargetX: x, TargetY: y,
			Mass: ParkWorld.NavigatorState.DefaultMass,
			Radius: ParkWorld.NavigatorState.DefaultRadius,
			MaxForce: (int)Peep.MaxForcePerSpeed, MaxSpeed: (int)Peep.MaxSpeedPerSpeed,
			NavMode: 0, CantReachDest: 0, PathFinished: true,
			PathCount: 0, PathTotalCount: 0, PathBufferCount: 0,
			BufferedDistance: 0, TailDistance: 0, TotalDistance: 0, StuckBits: 0 );

		// The constructor's first state (FUN_004faec0, 0x004fb1d0 on). A guest made on a cell that counts as the
		// park's (FUN_004fa990, Peep.CountsOn) is deciding from the start. One made outside it, which the bus
		// stop is, walks to the crossing's bus-stop side in state 0 and stands there in state 1 until the road is
		// clear (PeepBehaviour.WalkInFromTheStop, below; docs/exe/park.md, "Arrivals").
		var inThePark = Peep.CountsOn( State.Record( cellX, cellY ).Type );

		var guest = new ParkWorld.GuestState(
			State: (int)(inThePark ? PeepState.Deciding : PeepState.Walking), SavedState: ParkWorld.GuestState.Deciding,
			PersonType: type, Cash: cash, ExitLevel: exitLevel,
			Happiness: 50f, Thirst: thirst, Hunger: hunger, Toilet: toilet, Vomit: 0f, Litter: 0f,
			MajorDest: 0, QueuePos: 0, PrankeryIndex: prankery ? 100 + (thingId & 0xffff) % 3 : 0,
			// The constructor's stamp of the park's clock (0x004fafcf).
			ArrivalDate: State.GameTick );

		// The child they arrive as, by their id alone (FUN_004faec0, 0x004fb18d..0x004fb1bc).
		var child = _banks.ChildOf( thingId );

		var peep = new Peep( thingId, guest, navigator, pace )
		{
			SpriteKind = ParkSpriteBanks.ChildKind,
			SpriteBank = child
		};

		// A new thing heads the used-thing list (FUN_00516270), which the sweep walks from its head, so the
		// newest guest takes the first turn. The save's guests are already in that list's order.
		_peeps.Insert( 0, peep );
		_byId[thingId] = peep;
		var walk = _walks[thingId] = new PeepWalk( peep.Navigator, _blocked )
		{
			Ground = State,
			Heading = PeepBehaviour.ArrivalHeading
		};

		// With no route the constructor makes them in state 6 instead (0x004fb259). A park that names no crossing,
		// which only a test builds, has nowhere to send them: they stand at the roadside's state where they are.
		var (sideB, across) = PeepBehaviour.WalkInDraws( thingId );

		if ( !inThePark && !_behaviour.WalkInFromTheStop( peep, walk, sideB, across ) )
		{
			peep.SetState( _behaviour.Admission == null ? PeepState.AtGate : PeepState.Deciding,
				State.GameTick, _arrivalRandom );
		}

		var person = new ParkWorld.Person(
			ThingId: thingId, Model: ParkWorld.GuestModel, RawX: x >> 8, RawY: y >> 8,
			SpriteSlot: slot, Angle: PeepBehaviour.ArrivalHeading,
			Navigator: navigator, Guest: guest, Pace: pace, SpriteKind: ParkSpriteBanks.ChildKind, SpriteBank: child );

		// The bank has to be one this park already packs - see ParkGuestSprites.BanksToPack, which packs every child bank.
		var picture = new ParkWorld.Sprite(
			Slot: slot, Type: ParkSpriteBanks.ChildKind, Bank: child, SpriteNumber: 0,
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

		// Made on a cell that counts as the park's, they are a visitor from the start (0x004fb1df); made outside,
		// which every arrival is, they become one as they come through the gate (PeepBehaviour, Entering).
		if ( inThePark )
			peep.VisitorNumber = _behaviour.State.Admit();

		Log.Info( $"People: guest {thingId} arrived at ({cellX},{cellY}) on mGameTick {State.GameTick} - "
			+ $"{_peeps.Count} guests now; kind {type} happy {peep.Happiness:0} "
			+ $"thirst {thirst} hunger {hunger} toilet {toilet} cash {cash} exit {exitLevel} prankery {peep.PrankeryIndex}" );

		GuestArrived?.Invoke( peep );

		return thingId;
	}

	/// <summary>
	/// A new staff sprite from the native constructors, independent of saved employees.
	/// Slot and position are assigned when Hire adds it; docs/exe/park-engine.md, hiring.
	/// </summary>
	internal static ParkWorld.Sprite? StaffPicture( ParkStaffPool.Candidate candidate, ParkSpriteBanks banks, Random random )
	{
		var kind = ParkStaffPool.SpriteKindFor( candidate.Kind );

		if ( kind < 0 )
			return null;

		// The guard chooses anew (FUN_004d5de0); the other four constructors are handed the candidate's costume
		// byte as it is (FUN_0046c8e0, 0x0046c9b9 and its kin) and make the sprite on it.
		// As with arrivals, the draw is the arrivals' generator's, not the park's (ParkState.RandomSeed).
		var bank = kind == 7 && banks.CountOf( kind ) > 0
			? (random.Next() >> 2) % banks.CountOf( kind ) : candidate.Costume & 0xff;

		if ( banks.StaffBanks != null )
		{
			// A kind with no bank loaded has no picture to make: the sprite maker asserts one (0x00475a71).
			if ( banks.CountOf( kind ) == 0 )
				return null;

			// <b>A deviation, at Alexah's word</b> (docs/DECISIONS.md, "A hired costume is brought within the banks
			// loaded"). The original keeps a costume past its kind's banks and draws bank base + costume of one flat
			// table (FUN_00542010), in which the kinds follow one another (Sprites_LoadBanks): on low detail, where
			// the mechanics load one bank, a candidate saved with the second costume is drawn with the guards'
			// pictures until a save and load reduces it (0x004f93a6). Here it is reduced at the hire, as that load
			// would.
			bank = banks.Reduce( kind, bank );
		}

		return new ParkWorld.Sprite(
			Slot: 0, Type: kind, Bank: bank, SpriteNumber: 0,
			X: 0f, Height: 0f, Y: 0f, Facing: 0, Frame: 0, Alpha: 255, State: 1,
			Script: SpriteScript.None, Pc: 0 );
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

		if ( StaffPicture( candidate, _banks, _arrivalRandom ) is not { } picture )
		{
			Log.Warning( $"People: sprite for staff kind {candidate.Kind} is unavailable - not hired" );
			return 0;
		}

		var one = ParkWorld.NavigatorState.One;

		// ParkState owns the ONE numbering objects and people share, the original's free list - see
		// ParkState.NextThingId. The counter here is the fallback for a ParkPeople built without a running
		// park, which the tests do.
		var thingId = ParkState.Current?.NextThingId() ?? _nextThingId++;

		// A full thing table makes nobody (ParkState.NextThingId).
		if ( thingId == 0 )
			return 0;

		var slot = _nextSpriteSlot++;

		var x = (cellX * one) + (one / 2);
		var y = (cellY * one) + (one / 2);

		// The person base's speed words as the staff constructor leaves them (FUN_00504b90): no hurry, no
		// adjustor, a speed of nought to ease up from, and the hire's base, which the decide below replaces.
		var pace = new ParkWorld.PaceState(
			AdjustorSpeed: 0, BaseSpeed: global::OpenTPW.Staff.HiredBaseSpeed( model, candidate.Grade ),
			PreviousSpeed: 0f, PurposeSpeed: 0 );

		var navigator = new ParkWorld.NavigatorState(
			X: x, Y: y, VelocityX: 0, VelocityY: 0, TargetX: x, TargetY: y,
			Mass: ParkWorld.NavigatorState.DefaultMass,
			Radius: ParkWorld.NavigatorState.DefaultRadius,
			MaxForce: Peep.LeastMaxSpeed, MaxSpeed: Peep.LeastMaxSpeed,
			NavMode: 0, CantReachDest: 0, PathFinished: true,
			PathCount: 0, PathTotalCount: 0, PathBufferCount: 0,
			BufferedDistance: 0, TailDistance: 0, TotalDistance: 0, StuckBits: 0 );

		// Idle, patrolling the whole map as the constructor leaves them (FUN_00504b90). Their training starts at
		// the grade they were hired at.
		var state = new ParkWorld.StaffState(
			State: (int)StaffActivity.Idle, PayGrade: candidate.Grade,
			Happiness: 100f, Tiredness: 100f, JobsDone: 0,
			PatrolBottomLeft: global::OpenTPW.Staff.WholeMapBottomLeft,
			PatrolTopRight: global::OpenTPW.Staff.WholeMapTopRight, RestArea: 0,
			PercentageThroughGrade: 0, TimeStartedIdling: 0, Name: candidate.Name,
			TimeHired: ParkWorld.StaffState.FileTimeOf( State.CalendarNow ) );

		var member = new global::OpenTPW.Staff( thingId, model, state, navigator, pace, MapStep.CellId( cellX, cellY ) );

		State.StampStaffHired();

		// An entertainer's and a guard's constructor stamps their effect round the cell they are made on.
		if ( ParkRegionEffects.OfStaff( model ) is { } effect )
			State.StampEffect( effect, cellX, cellY );

		// At the head, as a new guest is (FUN_00516270).
		_staff.Insert( 0, member );
		_staffWalks[thingId] = new PeepWalk( member.Navigator, _blocked ) { Ground = State };

		var person = new ParkWorld.Person(
			ThingId: thingId, Model: model, RawX: x >> 8, RawY: y >> 8,
			SpriteSlot: slot, Angle: 0, Navigator: navigator, Guest: null, Staff: state );

		var animation = new SpriteScript( SpriteScript.None, 0, spriteNumber: 0, frame: 0 );

		animation.Start( SpriteScript.Standing );
		animation.ScheduleFrom( 0 );

		_sprites[thingId] = animation;
		_staffLooks[thingId] = (picture.Type, picture.Bank);

		ParkGuestSprites.Current?.Add( person, picture with { Slot = slot, X = cellX, Y = cellY } );

		// Made on this cell, they head its list (FUN_0050afe0); StaffBehaviour.Step relinks them as they walk.
		_behaviour.State.StandOn( thingId, cellX, cellY );

		// Every kind's constructor ends in its decide, where they were put.
		_staffBehaviour.Hired( member, _staffWalks[thingId], State.GameTick );

		Log.Info( $"People: hired {candidate.Name}, a grade {candidate.Grade} " +
			$"{ParkStaffPool.NameOfKind( candidate.Kind ).ToLowerInvariant()} at {candidate.Wage} a month, " +
			$"as thing {thingId} at ({cellX},{cellY}) - {_staff.Count} staff now" );

		return thingId;
	}

	/// <summary>Sets how rested a member of staff is, for the console's <c>staffrest</c>. Says whether there is such a member.</summary>
	internal bool SetStaffRest( int thingId, float rest )
	{
		if ( _staff.Find( person => person.ThingId == thingId ) is not { } member )
			return false;

		member.Tiredness = Math.Clamp( rest, global::OpenTPW.Staff.Least, global::OpenTPW.Staff.Most );

		return true;
	}

	/// <summary>Whether a thing id is one of the park's staff.</summary>
	internal bool IsStaff( int thingId ) => _staff.Exists( member => member.ThingId == thingId );

	/// <summary>
	/// Thing 1's monthly training budgets, <c>mBudget[0..4]</c>, one a kind in <see cref="ParkStaffPool"/>'s order -
	/// read from the save's staff HQ, and nought where it has none. The Staff Training Budgets screen sets them
	/// (<see cref="SetTrainingBudget"/>), as the original's alone does.
	/// </summary>
	internal int[] TrainingBudgets { get; } = new int[ParkWorld.StaffHqState.Kinds];

	/// <summary>Thing 1's <c>mHaveEverTrained</c> (<c>+0x20</c>): a kind has been given a budget over nought.</summary>
	internal bool HaveEverTrained { get; private set; }

	/// <summary>
	/// The Staff Training Budgets screen's spinner was given a value, by a press or as the screen opens (its handler's
	/// message <c>0x800</c>, <c>0x004b2534</c>): the kind's budget is the value, nought for one below nought, and a
	/// value over nought sets <see cref="HaveEverTrained"/>, which nothing there clears.
	/// </summary>
	internal void SetTrainingBudget( int kind, int value )
	{
		TrainingBudgets[kind] = Math.Max( value, 0 );

		if ( value > 0 )
			HaveEverTrained = true;

		Log.Info( $"Staff: training budget {kind} set to {TrainingBudgets[kind]}, budgets {string.Join( ",", TrainingBudgets )} evertrained {(HaveEverTrained ? 1 : 0)}" );
	}

	/// <summary>One kind's members, budget and grades added up, as the advisor's rows 28 to 32 and the Staff Training Budgets screen's skill bars read them.</summary>
	internal ParkAdvisorMessages.KindTraining TrainingOf( int kind )
	{
		var members = _staff.Where( member => ParkStaffPool.KindFor( member.Model ) == kind ).ToList();
		return new ParkAdvisorMessages.KindTraining( members.Count, TrainingBudgets[kind], members.Sum( member => member.PayGrade ) );
	}

	/// <summary>Thing 1's strike records, read from the save's staff HQ and looked at as each month turns.</summary>
	internal ParkStrikes Strikes { get; }

	/// <summary>
	/// The month's change reaches thing 1, the staff HQ - message <c>0xc</c>, the first to hear it, whose training
	/// <c>FUN_0050c800</c> divides each kind's budget among that kind's members (signed) and trains every member with
	/// the share (<see cref="Train"/>), whether the park is open or shut (<c>docs/exe/ride-operation.md</c>, "The
	/// month's change"). A nought budget still trains, with nought. Then its look at strikes
	/// (<see cref="ParkStrikes.Look"/>).
	/// </summary>
	public void TrainTheStaff()
	{
		var counts = new int[ParkStaffPool.Kinds];

		foreach ( var member in _staff )
		{
			var kind = ParkStaffPool.KindFor( member.Model );

			if ( kind >= 0 )
				++counts[kind];
		}

		foreach ( var member in _staff.OrderBy( member => member.ThingId ).ToList() )
		{
			var kind = ParkStaffPool.KindFor( member.Model );

			if ( kind >= 0 )
				Train( member, kind, TrainingBudgets[kind] / counts[kind] );
		}

		Strikes.Look( State.GameTick, State.ParkIsClosed, GateGuestCensus, _staff );
	}

	/// <summary>
	/// <c>CStaff::TrainMe</c>, <c>FUN_00505a10</c>: at grade 4 nothing; otherwise the share withdrawn whole, and a
	/// point for each <see cref="ParkStaffPool.PoundsPerTrainingPoint"/> it buys, held to 100, onto
	/// <see cref="Staff.PercentageThroughGrade"/>; at 100 the member goes up a grade, keeps what passed 100, and is
	/// made happy (100). What the division leaves over is spent for nothing.
	/// </summary>
	internal void Train( Staff member, int kind, int share )
	{
		if ( member.PayGrade >= ParkWorld.StaffState.PayGrades - 1 )
			return;

		_behaviour.State.Spend( share );
		_behaviour.State.AddTrainingCost( share );

		var cost = ParkStaffPool.PoundsPerTrainingPoint( _balance, kind, member.PayGrade );

		// The original divides by the table's value unguarded; every shipped file sets grades 0 to 3.
		if ( cost == 0 )
		{
			Unimplemented.Report( "STAFF_TRAINING_NO_POINT_COST" );
			return;
		}

		var points = Math.Min( share / cost, 100 );

		if ( points + member.PercentageThroughGrade < 100 )
		{
			member.PercentageThroughGrade = (byte)(member.PercentageThroughGrade + points);
			return;
		}

		member.PayGrade += 1;
		member.PercentageThroughGrade = (byte)(member.PercentageThroughGrade + points - 100);
		member.Happiness = 100;

		Log.Info( $"People: thing {member.ThingId} promoted to grade {member.PayGrade}, "
			+ $"{member.PercentageThroughGrade} through it" );
	}

	/// <summary>
	/// The month's change as the original's calendar sends it: thing 1's training, then the analyser and the bank, then
	/// each wage, in ascending thing id (<c>docs/exe/ride-operation.md</c>, "The month's change").
	/// </summary>
	public static void TurnTheMonth( ParkPeople? people, ParkState? state, ParkItemCatalogue? catalogue )
	{
		people?.TrainTheStaff();
		state?.TurnTheMonth( catalogue, people?.GateGuestCensus ?? 0, people?.GuestsInsideHappiness ?? 0f, people?.ScoreStaff ?? default );
		people?.PayTheWages();
	}

	/// <summary>
	/// The staff the month's <see cref="ParkScore"/> counts: every member on the staff's list by their type, in the hand
	/// or not (<c>FUN_004c9960</c> asks only that a thing's <c>+3</c> be nought, and a member fired here is gone at once).
	/// </summary>
	internal ParkScore.StaffCounts ScoreStaff => new( _staff.Count( member => member.Model == HandymanModel ), _staff.Count( member => member.Model == MechanicModel ),
		_staff.Count( member => member.Model == EntertainerModel ), _staff.Count( member => member.Model == GuardModel ), _staff.Count( member => member.Model == ResearcherModel ) );

	/// <summary>
	/// The month's change reaches each member of staff, the last to hear it - message <c>0xc</c>, whose handler
	/// withdraws a month's wage (<c>FUN_00504c70</c>, <see cref="ParkStaffPool.WageFrom"/>) in ascending thing id,
	/// with nothing tested first: a member resting, in the hand or hired this month pays the whole month. Each wage goes
	/// onto the analyser's month of staff costs too (<see cref="ParkState.AddStaffCost"/>).
	/// </summary>
	public void PayTheWages()
	{
		foreach ( var member in _staff.OrderBy( member => member.ThingId ).ToList() )
		{
			var kind = ParkStaffPool.KindFor( member.Model );

			if ( kind < 0 )
				continue;

			var wage = ParkStaffPool.WageFrom( _balance, kind, member.PayGrade );

			_behaviour.State.Spend( wage );
			_behaviour.State.AddStaffCost( wage );
		}
	}

	/// <summary>
	/// Dismisses a member of staff, charging one further month's wage. Answers whether there was one.
	/// </summary>
	/// <remarks>
	/// <b>It is <see cref="Depart"/> for staff, and the worker is deleted outright rather than walked
	/// out</b> - <c>FUN_00505790</c> charges, spawns a particle and calls the thing's delete, with no
	/// state change and no walk to the gate. The severance is exactly one wage, by the same expression
	/// the monthly charge uses, and goes onto the analyser's month of staff costs as a wage does.
	/// <para>
	/// It first installs the idle mode where a worker is carried, which puts them back down
	/// (<c>0x00505830</c>; <see cref="PutBack"/>). The puff of particle <c>0x13</c> at the member
	/// (<c>FUN_004fa470</c>) is counted, not built.
	/// </para>
	/// </remarks>
	internal bool Fire( int thingId )
	{
		var at = _staff.FindIndex( member => member.ThingId == thingId );

		if ( at < 0 )
			return false;

		if ( _carriedStaff != 0 )
			Log.Info( $"Hand: {PutBack()}" );

		var member = _staff[at];
		var kind = ParkStaffPool.KindFor( member.Model );
		var severance = kind >= 0
			? Level.Current?.StaffPool?.WageFor( kind, member.PayGrade ) ?? 0
			: 0;

		_staff.RemoveAt( at );
		_staffWalks.Remove( thingId );
		_sprites.Remove( thingId );

		// The destructor takes their effect off the cell it was last stamped round (FUN_004d4620, FUN_004d5eb0).
		if ( ParkRegionEffects.OfStaff( member.Model ) is { } effect && MapStep.IsCellId( member.RecordedCell ) )
		{
			var (x, y) = MapStep.CellAt( member.RecordedCell );

			State.UnstampEffect( effect, x, y );
		}

		_behaviour.State.Forget( thingId );
		ParkGuestSprites.Current?.Remove( thingId );
		State.DeleteThing( thingId );

		_behaviour.State.Spend( severance );
		_behaviour.State.AddStaffCost( severance );

		Unimplemented.Report( "STAFF_DISMISSAL_PARTICLE" );

		Log.Info( $"People: dismissed thing {thingId}, a grade {member.PayGrade} " +
			$"{ParkStaffPool.NameOfKind( kind ).ToLowerInvariant()} - one month's wage of {severance} paid, " +
			$"{_staff.Count} staff left" );

		return true;
	}

	/// <summary>
	/// An entertainer's and a guard's pre-step, before the staff's own (<c>FUN_004d4660</c>, <c>FUN_004d6360</c>):
	/// on a sweep that finds them on another cell than the one recorded, their region effect is taken off round the
	/// recorded cell and stamped round the one they stand on, which is recorded. A recorded id that is no cell of the
	/// map has nothing taken off, as the original's stamp walks no cell for one.
	/// </summary>
	internal void MoveEffect( Staff member )
	{
		if ( ParkRegionEffects.OfStaff( member.Model ) is not { } effect )
			return;

		var (cellX, cellY) = member.Navigator.Position.Cell;
		var now = MapStep.CellId( cellX, cellY );

		if ( member.RecordedCell == now )
			return;

		if ( MapStep.IsCellId( member.RecordedCell ) )
		{
			var (x, y) = MapStep.CellAt( member.RecordedCell );

			State.UnstampEffect( effect, x, y );
		}

		State.StampEffect( effect, cellX, cellY );
		member.RecordedCell = now;
	}

	/// <summary>The staff member in the player's hand, or nought - the save's <c>mStaffMemberPickedUp</c>.</summary>
	private int _carriedStaff;

	/// <summary>A mechanic's thing model - see <see cref="Staff.Model"/>.</summary>
	internal const int MechanicModel = 4;
	internal const int HandymanModel = 5;
	internal const int GuardModel = 7;
	internal const int ResearcherModel = 8;

	/// <summary>Who is being carried, for the debug console.</summary>
	internal int CarriedStaff => _carriedStaff;

	/// <summary>
	/// Picks a member of staff up. They stop doing whatever they were doing and wait to be put down.
	/// </summary>
	/// <remarks>
	/// <b>This is what <see cref="StaffActivity.Held"/> is for.</b>
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

		member.SetActivity( StaffActivity.Held, State.GameTick );
		_carriedStaff = thingId;

		// The type-6 mode keeps the member's sprite bank and costume and hangs them under the pointer (vtable
		// 0x006fea70); here a carried member is only taken out of the drawing (ParkGuestSprites).
		Unimplemented.Report( "STAFF_PICKED_UP_PREVIEW" );

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
		member.SetActivity( StaffActivity.Idle, State.GameTick );

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

	// How many of this load are still to be dropped, which vehicle is bringing them, whether a load is held at
	// all, and the mGameTick the wait for the next is counted from: the original's mPeopleOnBus, the vehicle it
	// caches, mOffloading and mTimeSig. The flag is apart from the count because the load is let go a sweep
	// after its last guest is dropped, not on the drop (FUN_004cf3e0, 0x004cf56b).
	private int _arrivalsRemaining;
	private int _arrivalVehicle;
	private bool _offloading;
	private int _arrivalMark;

	/// <summary>The <c>mGameTick</c> the next load's wait is counted from - the original's <c>mTimeSig</c>.</summary>
	/// Dead by CODE: only ParkTickTests and ParkFileWriterPoolTests call it.
	internal int ArrivalMark => _arrivalMark;

	/// <summary>Whether a load is held, from the sweep that calls it to the sweep that lets it go - <c>mOffloading</c>.</summary>
	/// Dead by CODE: only ParkTickTests and ParkFileWriterPoolTests call it.
	internal bool LoadHeld => _offloading;

	/// <summary>How many of the load held are still to be dropped - <c>mPeopleOnBus</c>.</summary>
	/// Dead by CODE: only ParkTickTests and ParkFileWriterPoolTests call it.
	internal int StillToDrop => _arrivalsRemaining;

	/// <summary>
	/// A person's own sprite picked up where the save left it: its script, word, set and frame, the loop starts
	/// its program had pushed, its drawing flags, and the three words a bank's state script reads. One saved inside a state
	/// animation's loop goes round it from the frame it was on.
	/// </summary>
	private static SpriteScript SavedSprite( IParkInitialState park, ParkWorld.Sprite picture )
	{
		var file = park as ParkWorld;

		var sprite = new SpriteScript( picture.Script, picture.Pc, picture.SpriteNumber, picture.Frame,
			loops: file?.SpriteLoopsOf( picture.Slot ), state: file?.SpriteStateWordsOf( picture.Slot ) ?? default,
			flags: file?.SpriteFlagsOf( picture.Slot ) ?? 0 );

		// And when it first comes due, which the original's constructor does as the sprite is made. Nought is
		// the clock at load, the only moment this is built.
		sprite.ScheduleFrom( 0 );

		return sprite;
	}

	/// <summary>The things the save's <c>mArrivalVehicle_Size1..3</c> name, and each made at its summons since; nought for a vehicle not made yet.</summary>
	private readonly int[] _vehicleThings;

	/// <summary>The three vehicles' slots as a park file holds them, <c>mArrivalVehicle_Size1..3</c>: each one's thing, nought where none is made.</summary>
	internal IReadOnlyList<int> VehicleThings => _vehicleThings;

	/// <summary>Which of the three vehicles is current, 1 to 3, or nought - the slot <c>mCurrentArrivalVehicle</c>'s thing is in.</summary>
	internal int CurrentVehicle => _arrivalVehicle;

	/// <summary>
	/// The arrival timer as a park file holds it: the mark, the count still to drop, whether a load is held, the
	/// three vehicles' things (<c>mArrivalVehicle_Size1..3</c>) and the vehicle that is current, as the thing its
	/// slot names (<c>mCurrentArrivalVehicle</c>), nought with none.
	///
	/// <para>
	/// The handles and the vehicles' scripts are one state (<c>FUN_0051a690</c>), so they are written only with
	/// the scripts: where <paramref name="written"/> is null they are left the file's. A vehicle made at its
	/// summons is written with its thing (<see cref="MakeVehicle"/>). <b>A vehicle whose thing is not in the file
	/// being written is counted</b> (<c>SAVE_PARK_ARRIVAL_VEHICLE</c>) and its slot written as none, and as no
	/// vehicle current where it was: one this park could make no thing for, or whose made thing the writer leaves
	/// out. A load it held is then brought by the vehicle its size summons.
	/// </para>
	/// </summary>
	/// <param name="written">Whether a thing that is no person is in the file being written; null where the things go out as the file's.</param>
	internal ParkFileWriter.ArrivalTimer WrittenArrival( Func<int, bool>? written = null )
	{
		if ( written == null )
		{
			if ( _arrivalVehicle != 0 )
				Unimplemented.Report( "SAVE_PARK_ARRIVAL_VEHICLE" );

			return new ParkFileWriter.ArrivalTimer( _arrivalMark, _arrivalsRemaining, _offloading );
		}

		var slots = new int[_vehicleThings.Length];

		for ( var vehicle = 1; vehicle <= slots.Length; ++vehicle )
		{
			var thing = _vehicleThings[vehicle - 1];

			if ( thing != 0 && written( thing ) )
				slots[vehicle - 1] = thing;
			else if ( thing != 0 || vehicle == _arrivalVehicle )
				Unimplemented.Report( "SAVE_PARK_ARRIVAL_VEHICLE" );
		}

		return new ParkFileWriter.ArrivalTimer( _arrivalMark, _arrivalsRemaining, _offloading,
			_arrivalVehicle != 0 ? slots[_arrivalVehicle - 1] : 0, (slots[0], slots[1], slots[2]) );
	}

	/// <summary>How long a load waits after the last, in fours of sweeps - <c>Arrival.TimeBetweenArrivals</c>, 150.</summary>
	private int ArrivalPeriod => _balance?.Int( "Arrival.TimeBetweenArrivals", 150 ) ?? 150;

	/// <summary>
	/// Whether a load is due: <c>FUN_0041a990</c> and the compare after it at <c>0x004cf3f6</c>. The clock and the
	/// mark are each shifted down two unsigned (<c>SHR</c>), so one count is four sweeps, 0.99 s, and their
	/// difference has to be MORE than the period, unsigned (<c>JBE</c> skips the call). So the load comes on the
	/// first sweep whose elapsed count is 151 for a period of 150, and a mark ahead of the clock wraps the difference
	/// and makes one due at once.
	/// </summary>
	internal static bool LoadIsDue( int gameTick, int mark, int period )
		=> unchecked(((uint)gameTick >> 2) - ((uint)mark >> 2)) > (uint)period;

	/// <summary>The first <c>mGameTick</c> at or after the mark on which <see cref="LoadIsDue"/> answers yes.</summary>
	internal static int FirstDueTick( int mark, int period )
		=> (int)((((uint)mark >> 2) + (uint)period + 1) << 2);

	/// <summary>
	/// Which of the three vehicles brings a crowd this big. <c>FUN_004cf3e0</c> takes the first for a
	/// headcount under <c>0x24</c> and otherwise <c>(0x3c &lt; count) + 2</c>, so the second up to 60 and
	/// the third beyond - and the save's own <c>mArrivalVehicle_Size1..3</c> naming says the same.
	/// </summary>
	internal static int VehicleFor( int people )
		=> people < 36 ? 1 : people > 60 ? 3 : 2;

	/// <summary>
	/// The cell a guest of this load is made on - <c>FUN_004cf720</c>. Every one is made at the second bus stop
	/// (<c>FUN_004d8650( 1 )</c>, <c>0x004cf745</c>), and two rows nearer the map's edge while a vehicle other than
	/// the small crowd's stands (<c>FUN_0051aad0</c>: the current vehicle is not <c>mArrivalVehicle_Size1</c>'s; the
	/// packed id less <c>0x100</c>, <c>0x004cf75c</c>): (53,5) for a bus load in Lost Kingdom, (53,3) for a larger one.
	/// The first stop is never an arrival's (<c>docs/exe/park.md</c>, "Arrivals"). No vehicle current is the bus's
	/// answer, as the original's test reads it.
	/// </summary>
	/// <remarks>
	/// The vehicle is the one that is CURRENT, and a current vehicle is reused whatever size the load asked for
	/// (<c>FUN_0051a2f0</c>, <c>0x0051a314</c>), so a load of one can be dropped two rows out by a larger vehicle still
	/// about (<see cref="Summon"/>).
	/// </remarks>
	internal static (int X, int Y) ArrivalCell( (int X, int Y) stopB, int vehicle )
		=> vehicle is 2 or 3 ? (stopB.X, stopB.Y - 2) : stopB;

	/// <summary>The variable a vehicle's script reports itself through, as its own file declares it.</summary>
	private const string VehicleState = "VAR_STATUS";

	/// <summary>
	/// What a vehicle's script spins on until somebody sets it. Ferry.RSE and seaplane.RSE both read
	/// <c>TEST VAR_TRIGGER / ENDSLICE / BRANCH_Z</c> back onto themselves, so a vehicle that is never
	/// told to go stands at the stop for ever.
	/// </summary>
	private const string VehicleTrigger = "VAR_TRIGGER";

	/// <summary>
	/// The state a vehicle reports once it has arrived and is ready to unload. The original drops one
	/// guest a thing sweep for exactly as long as <c>FUN_0051a690</c> answers this.
	/// </summary>
	private const int VehicleIsUnloading = 2;

	/// <summary>
	/// The state a vehicle reports once it has moved on from the arrivals' stop and stands for whoever is going
	/// home - the second of the three points every vehicle script parks at.
	/// </summary>
	private const int VehicleIsLeaving = 4;

	/// <summary>
	/// The state a vehicle reports when it has finished its circuit. <c>FUN_0051a690</c> answers this by
	/// <b>forgetting the vehicle</b> - it clears <c>mCurrentArrivalVehicle</c> and reports -1 instead - so
	/// the next load picks afresh rather than re-using one that has driven off.
	/// </summary>
	private const int VehicleIsSpent = 6;

	/// <summary>
	/// Starts a load of <paramref name="people"/> now, whatever the timer says, and answers the vehicle that is
	/// current, or with none the one that size calls for. Answering the console rather than the park. A current
	/// vehicle past its unloading is spent before the load drops, and the load is then summoned by its size. The load
	/// goes onto the analyser's ring of loads as one the timer calls does.
	///
	/// <para>
	/// <b>It exists to bring the second and third vehicles in the stock park.</b> The headcount is what
	/// the park is worth (<see cref="LoadSize"/>), 13 and 12 for the stock park's first two loads, and
	/// <see cref="VehicleFor"/> gives a load under 36 the bus, so those loads come by bus and a seaplane
	/// is not asked for. This is the way to watch the other two arrive without a park worth that much.
	/// </para>
	/// </summary>
	internal int ForceArrival( int people )
	{
		_arrivalsRemaining = Math.Max( 1, people );
		_offloading = true;
		State.AddArrivalLoad( _arrivalsRemaining );

		// A vehicle still current brings it, whatever its size, as the original's summons reuses one.
		var by = _arrivalVehicle != 0 ? _arrivalVehicle : VehicleFor( _arrivalsRemaining );

		Log.Info( $"People: {_arrivalsRemaining} arriving by hand, vehicle {by} ({ParkFixedItems.VehicleName( by )})" );

		return by;
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
	/// thirsty at all (<see cref="Peep.Tick"/> shares the drift by thing id, and 4 divides 16).
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
	/// Sets the life of every balloon held, for the debug console's <c>balloon</c>; a guest holding none is left
	/// alone. Answers how many.
	/// </summary>
	internal int SetBalloonLife( int life )
	{
		var touched = 0;

		foreach ( var peep in _peeps.Where( peep => peep.Balloon != null ) )
		{
			peep.BalloonLife = life;
			++touched;
		}

		Log.Info( $"People: {touched} balloons now have life {life}" );

		return touched;
	}

	/// <summary>
	/// Makes a guest on a cell who has already come through the gate, for the debug console's <c>admit</c>: an
	/// INSTRUMENT. <see cref="Admit"/> leaves one made outside the park to walk to the gate and pay at a booth;
	/// this then does what <see cref="PeepState.Entering"/>'s arrival does (<see cref="PeepBehaviour.AdmitAsEntered"/>),
	/// so they stand where they were made, deciding, numbered a visitor once. No player reaches it. Answers their
	/// thing id, or nought.
	/// </summary>
	internal int AdmitInside( int cellX, int cellY, int? personType )
	{
		var thingId = Admit( cellX, cellY, personType );

		if ( thingId != 0 )
			_behaviour.AdmitAsEntered( _byId[thingId], State.GameTick );

		return thingId;
	}

	/// <summary>
	/// Sends a guest to a thing as if they had chosen it, for the debug console's <c>send</c> - see
	/// <see cref="PeepBehaviour.SendAsChosen"/>. Answers why not, or null when they set off.
	/// </summary>
	internal string? SendAsChosen( int guestId, int thingId )
	{
		if ( !_byId.TryGetValue( guestId, out var peep ) || !_walks.TryGetValue( guestId, out var walk ) )
			return $"no guest {guestId}";

		return _behaviour.SendAsChosen( peep, walk, thingId, State.GameTick );
	}

	/// <summary>
	/// Sets every guest's happiness, for the debug console's <c>happy</c>. An INSTRUMENT, as
	/// <see cref="MakeThirsty"/> is: set a known level to exercise mood thresholds and measure a queue exit's
	/// happiness dock. It sets a meter the game itself moves, and nothing else.
	/// </summary>
	/// <returns>How many guests were set.</returns>
	internal int SetHappiness( float level )
	{
		foreach ( var peep in _peeps )
			peep.Happiness = Math.Clamp( level, Peep.Least, Peep.Most );

		Log.Info( $"People: {_peeps.Count} guests are now happiness {level}" );

		return _peeps.Count;
	}

	/// <summary>
	/// Sets every guest's toilet need, for the debug console's <c>toilet</c>. An INSTRUMENT, as
	/// <see cref="MakeThirsty"/> is: a queuer leaves for a toilet once the need is above 80 (<see cref="PeepBehaviour"/>'s
	/// queue turn), and only a quarter of guests grow the need on their own, one point every sixteen sweeps. At 80
	/// a guest whose id divides by four passes 80 at their next drift and no other does. It sets a meter the game
	/// itself moves, and nothing else.
	/// </summary>
	/// <param name="only">One guest's thing id, or nought for every guest.</param>
	/// <returns>How many guests were set.</returns>
	internal int SetToilet( float level, int only = 0 )
	{
		var set = 0;

		foreach ( var peep in _peeps )
		{
			if ( only != 0 && peep.ThingId != only )
				continue;

			peep.Toilet = Math.Clamp( level, Peep.Least, Peep.Most );
			set++;
		}

		Log.Info( $"People: {set} guests are now toilet {level}" );

		return set;
	}

	/// <summary>The thing model of an entertainer, which <c>FUN_004fec90</c> hands <c>FUN_004c8eb0</c> (<c>0x004feff2</c>).</summary>
	public const int EntertainerModel = 6;

	/// <summary>
	/// Whether an entertainer stands on the nine cells around (x, y), for a deciding guest's watching arm. The
	/// original walks those cells' thing lists; this asks each member's walk where they are.
	/// </summary>
	private bool EntertainerBeside( int x, int y )
	{
		foreach ( var member in _staff )
		{
			if ( member.Model != EntertainerModel || !_staffWalks.TryGetValue( member.ThingId, out var walk ) )
				continue;

			var (cellX, cellY) = walk.Position.Cell;

			if ( Math.Abs( cellX - x ) <= 1 && Math.Abs( cellY - y ) <= 1 )
				return true;
		}

		return false;
	}

	/// <summary>
	/// The cell of the entertainer a deciding guest on (x, y) watches, or none (<c>0x004fefcf</c>..<c>0x004ff051</c>):
	/// one must stand beside them (<see cref="EntertainerBeside"/>), and then the nearest entertainer of all by squared
	/// cells, the first of equals (<c>FUN_00500f00</c>, unsigned and strict), must be performing (state <c>0xe</c>,
	/// <c>0x004ff04a</c>). The original walks its chain of entertainers (world <c>+0x1da740</c>, linked by <c>+0x210</c>);
	/// this walks <see cref="Staff"/>'s order, which is not read against that chain's, and decides only between two
	/// entertainers at the same distance.
	/// </summary>
	private (int X, int Y)? EntertainerToWatch( int x, int y )
	{
		if ( !EntertainerBeside( x, y ) )
			return null;

		var best = uint.MaxValue;
		(Staff Member, int X, int Y)? nearest = null;

		foreach ( var member in _staff )
		{
			if ( member.Model != EntertainerModel || !_staffWalks.TryGetValue( member.ThingId, out var walk ) )
				continue;

			var (cellX, cellY) = walk.Position.Cell;
			var squared = (uint)((cellX - x) * (cellX - x) + (cellY - y) * (cellY - y));

			if ( squared < best )
				(best, nearest) = (squared, (member, cellX, cellY));
		}

		return nearest is { } found && found.Member.Activity == StaffActivity.Performing ? (found.X, found.Y) : null;
	}

	/// <summary>
	/// Whether another guest on (x, y) holds a balloon, for a prankster's third prank (<c>0x004ff1c8</c>..
	/// <c>0x004ff1d9</c>: not themselves, a guest, <c>mBalloonScript</c> set).
	/// </summary>
	private bool BalloonBeside( Peep prankster, int x, int y )
	{
		foreach ( var peep in _peeps )
		{
			if ( peep == prankster || peep.Balloon == null || !_walks.TryGetValue( peep.ThingId, out var walk ) )
				continue;

			if ( walk.Position.Cell == (x, y) )
				return true;
		}

		return false;
	}

	/// <summary>
	/// Sets a level the deciding turn's unbuilt arms test, for the debug console's <c>need</c>: vomit, litter or
	/// prankery, or the hunger the advisor's row 4 counts. An INSTRUMENT, as <see cref="SetToilet"/> is: a visit adds
	/// only a little illness, two drinks fill their litter, and one arrival in twenty is a prankster.
	/// </summary>
	/// <param name="only">One guest's thing id, or nought for every guest.</param>
	/// <returns>How many guests were set, or -1 for a name that is none of the four.</returns>
	internal int SetNeed( string which, int level, int only = 0 )
	{
		var set = 0;

		foreach ( var peep in _peeps )
		{
			if ( only != 0 && peep.ThingId != only )
				continue;

			switch ( which )
			{
				case "vomit": peep.Vomit = Math.Clamp( level, Peep.Least, Peep.Most ); break;
				case "litter": peep.Litter = Math.Clamp( level, Peep.Least, Peep.Most ); break;
				case "hunger": peep.Hunger = Math.Clamp( level, Peep.Least, Peep.Most ); break;
				case "prankery": peep.PrankeryIndex = level; break;
				default: return -1;
			}

			set++;
		}

		Log.Info( $"People: {set} guests are now {which} {level}" );

		return set;
	}

	/// <summary>
	/// Sets every guest's cash, for the debug console's <c>cash</c>. An INSTRUMENT, as <see cref="SetHappiness"/>
	/// is. A new guest's varied cash covers Lost Kingdom's admission price; this lets the insufficient-cash
	/// branch of <see cref="PeepPriceOpinion"/> be exercised. It sets a meter the game itself moves.
	/// </summary>
	internal int SetCash( int amount )
	{
		foreach ( var peep in _peeps )
			peep.Cash = amount;

		Log.Info( $"People: {_peeps.Count} guests now carry cash {amount}" );

		return _peeps.Count;
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

	/// <summary>Each vehicle's <c>VAR_STATUS</c> as the last sweep's turn left it, for <see cref="LogVehicleStatuses"/>.</summary>
	private readonly int?[] _loggedVehicleStatus = new int?[3];

	/// <summary>
	/// Debug logging (Q627): one line each time a vehicle's <c>VAR_STATUS</c> changes between two sweeps, with
	/// <c>mGameTick</c>, so a visit is counted however short it is: 2 the arrivals' stop, 4 the leavers'
	/// (<c>docs/exe/ride-operation.md</c>, "Q128"). A stand for leavers can last one sweep, under any census poll.
	/// </summary>
	private void LogVehicleStatuses()
	{
		for ( var vehicle = 1; vehicle <= 3; ++vehicle )
		{
			int? status = VehicleScript( vehicle ) is { } script ? script[VehicleState] : null;

			if ( status == _loggedVehicleStatus[vehicle - 1] )
				continue;

			if ( status != null )
			{
				Log.Info( $"People: the {ParkFixedItems.VehicleName( vehicle )}'s status {_loggedVehicleStatus[vehicle - 1]?.ToString() ?? "none"} "
					+ $"-> {status} on mGameTick {State.GameTick}{(vehicle == _arrivalVehicle ? ", current" : "")}" );
			}

			_loggedVehicleStatus[vehicle - 1] = status;
		}
	}

	/// <summary>
	/// The arrival timer, for the debug console's <c>arrivals</c>: the park's clock, the mark, and either the load
	/// held or the <c>mGameTick</c> the next is due on, then the vehicle that is current, with its thing where the
	/// save names one. Answering the console rather than the park.
	/// </summary>
	internal string ArrivalCensus()
		=> $"arrivals: mGameTick {State.GameTick} mark {_arrivalMark} worth {_behaviour.ParkExcitement} "
			+ $"load now {LoadSize( _behaviour.ParkExcitement, Raining() )} " + (_offloading
			? $"load held, vehicle {_arrivalVehicle}, {_arrivalsRemaining} still to drop"
			: $"next load due on mGameTick {FirstDueTick( _arrivalMark, ArrivalPeriod )}")
			+ (_arrivalVehicle != 0
				? $"; current vehicle {_arrivalVehicle} ({ParkFixedItems.VehicleName( _arrivalVehicle )}, thing {_vehicleThings[_arrivalVehicle - 1]}) status {VehicleScript( _arrivalVehicle )?[VehicleState] ?? NoVehicle}"
				: "; no vehicle current");

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
				yield return thing == 0 ? $"{name}: not made" : $"{name}: thing {thing}, NO SCRIPT BOUND";
				continue;
			}

			var carrying = vehicle == _arrivalVehicle && _arrivalsRemaining > 0
				? $" <- carrying this load, {_arrivalsRemaining} still to drop"
				: "";

			yield return $"{name}: thing {thing} script '{script.Name}' pc {script.Position} "
				+ $"running {script.Running} {VehicleState} {script[VehicleState]} "
				+ $"{VehicleTrigger} {script[VehicleTrigger]}{carrying}{ParkFixedItems.Current?.BodyCensus( name )}";
		}
	}

	/// <summary>
	/// One turn of the arrival manager, <c>FUN_004cf3e0</c>, which the original calls once a thing sweep
	/// (<c>0x004d7b29</c>). While no load is held it waits out <c>Arrival.TimeBetweenArrivals</c> on the park's own
	/// clock (<see cref="LoadIsDue"/>). Then it decides how many are coming and on what, drops <b>one guest a
	/// sweep</b> for as long as the vehicle answers that it is unloading, and lets the load go on the first sweep
	/// after the last drop that still finds it unloading. That sweep is the mark the next wait counts from, so the
	/// next load is called 602 to 605 sweeps after the last guest got off (<c>docs/exe/park.md</c>, "Arrivals").
	///
	/// <para>
	/// <b>The headcount is the original's</b> (<c>FUN_004cf5b0</c>, <see cref="LoadSize"/>): what the park is worth
	/// on the calling sweep (<see cref="ParkWorth.Of"/>) with <c>Arrival.NewParkBonus</c>, more in fair weather
	/// than in rain, over <c>Arrival.PointsPerVisitor</c>. The stock park's first two loads are 13 and 12.
	/// </para>
	/// <para>
	/// <b>Its two refusals are a deviation.</b> In world state 4, and with the park at
	/// <see cref="MostPeopleInAPark"/>, the original still calls a load, of nobody (<c>FUN_004cf5b0</c>), and its
	/// vehicle still comes and restarts the wait; here neither calls a load, and world state 4 stops a load already
	/// held. Lost Kingdom reaches neither: it is saved in world state 0 with 13 guests. Where each guest is made is
	/// <see cref="ArrivalCell"/>.
	/// </para>
	/// </summary>
	private void StepArrivals()
	{
		if ( _blocked == null || _behaviour.Park is not { } park )
			return;

		if ( park.WorldState == NoArrivalsWorldState )
			return;

		var tick = State.GameTick;

		if ( !_offloading )
		{
			if ( !LoadIsDue( tick, _arrivalMark, ArrivalPeriod ) )
				return;

			if ( _peeps.Count >= MostPeopleInAPark )
				return;

			var worth = _behaviour.ParkExcitement;
			var raining = Raining();

			_arrivalsRemaining = LoadSize( worth, raining );
			_offloading = true;
			State.AddArrivalLoad( _arrivalsRemaining );

			Log.Info( $"People: {_arrivalsRemaining} arriving on mGameTick {tick} (mark {_arrivalMark}), "
				+ $"the park worth {worth}, {(raining ? "in rain" : "no rain")}" );

			// And on in the same turn to ask the vehicle, as the original does from 0x004cf455.
		}

		// <b>No vehicle answering: the load's own is summoned, by its size</b> (0x004cf489), on this sweep and on
		// every one after it until one answers. A spent vehicle answers as none (VehicleStatus), so every load has
		// the drive in.
		var status = VehicleStatus();

		if ( status == NoVehicle )
		{
			Summon( VehicleFor( _arrivalsRemaining ) );
			return;
		}

		// <b>The vehicle says when it is ready, which is the original's own handshake.</b> Its script plays its
		// arrival animation, sets VAR_STATUS to 2 and then spins on VAR_TRIGGER; FUN_004cf3e0 drops one guest a
		// sweep, and lets the load go, only while FUN_0051a690 reports that.
		//
		// <b>Where there is no script to ask, the guests still come.</b> A vehicle that is missing or unbound must
		// not be able to stop a park getting visitors at all - and gating on a state that will never arrive is
		// precisely what would do that. All three vehicles' scripts declare VAR_STATUS.
		var vehicle = VehicleScript( _arrivalVehicle );

		if ( vehicle != null && status != VehicleIsUnloading )
			return;

		if ( _arrivalsRemaining > 0 )
		{
			var (stopX, stopY) = ArrivalCell( _behaviour.Admission?.BusStopB ?? (53, 5), _arrivalVehicle );

			// A stop that will not take one ends the load rather than retrying it for ever.
			if ( Admit( stopX, stopY ) == 0 )
				_arrivalsRemaining = 0;
			else
			{
				--_arrivalsRemaining;
				State.AddArrival();
			}

			return;
		}

		// <b>Nobody left, and the vehicle still unloading: the load is let go on this sweep, not the drop's.</b> The
		// sweep that drops the last guest returns above, as the original's goes to its tail (0x004cf594), so this is
		// the sweep after it at the soonest (0x004cf56b), and its tick is the next wait's mark (FUN_0041a960).
		_arrivalMark = tick;
		_offloading = false;

		Log.Info( $"People: the load is all off on mGameTick {tick}; the next is due on mGameTick "
			+ $"{FirstDueTick( tick, ArrivalPeriod )}" );

		// The same call the summons is, on the vehicle that is current: its trigger, which sends it on.
		Summon( 0 );

		// One with no script reports nothing ever again, so it cannot be left current to answer for the next load.
		if ( vehicle == null )
			_arrivalVehicle = 0;
	}

	/// <summary>
	/// How many the load called now brings: <see cref="ParkWorth.LoadSize"/> on the balance file's three numbers,
	/// then held to what the park has room for, as the original caps it against <see cref="MostPeopleInAPark"/>
	/// (<c>FUN_004cf5b0</c>'s tail). <b>The room is counted on everybody here</b>, where the original's count is its crowd
	/// census <c>FUN_004c7fa0</c>, the guests on a counting cell; no shipped park comes near the cap.
	/// </summary>
	internal int LoadSize( int worth, bool raining )
	{
		var size = ParkWorth.LoadSize( worth, raining,
			_balance?.Int( "Arrival.NewParkBonus", 20 ) ?? 20,
			_balance?.Int( "Arrival.PointsPerVisitor", 6 ) ?? 6,
			_balance?.Int( "Arrival.MinPeople", 1 ) ?? 1 );

		return Math.Min( size, MostPeopleInAPark - _peeps.Count );
	}

	/// <summary>What <see cref="VehicleStatus"/> answers with no vehicle current.</summary>
	internal const int NoVehicle = -1;

	/// <summary>
	/// What the current arrival vehicle reports - <c>FUN_0051a690</c>: <see cref="NoVehicle"/> with none current, else
	/// its script's <c>VAR_STATUS</c>. <b>A 6, the end of its circuit, is answered as none, and the asking itself
	/// forgets the vehicle</b>: the script's <c>VAR_STATUS</c> is written nought and the vehicle is no longer current,
	/// with no trigger, so its script stays at its last spin until a summons (<c>docs/exe/park.md</c>, "The spent
	/// vehicle"). A vehicle with no script bound answers <see cref="VehicleIsUnloading"/>, so its load still comes.
	/// </summary>
	internal int VehicleStatus()
	{
		if ( _arrivalVehicle == 0 )
			return NoVehicle;

		if ( VehicleScript( _arrivalVehicle ) is not { } vehicle )
			return VehicleIsUnloading;

		var status = vehicle[VehicleState];

		if ( status != VehicleIsSpent )
			return status;

		vehicle.Set( VehicleState, 0 );

		Log.Info( $"People: the {ParkFixedItems.VehicleName( _arrivalVehicle )} is spent on mGameTick {State.GameTick}: "
			+ "forgotten until it is summoned" );

		_arrivalVehicle = 0;

		return NoVehicle;
	}

	/// <summary>
	/// The summons - <c>FUN_0051a2f0</c>, the one thing that sets a vehicle's <c>VAR_TRIGGER</c>. With a vehicle
	/// current it triggers that one whatever <paramref name="size"/> says (<c>0x0051a663</c>), which is how a load all
	/// dropped and a vehicle standing for leavers are sent on. With none, the vehicle of that size is made current and
	/// triggered, which starts its script's circuit from its last spin; one the park has no thing for is made first
	/// (<see cref="MakeVehicle"/>), and its script, which starts at its top, is given <c>VAR_STATUS</c> 1 and no trigger
	/// (<c>0x0051a5ed</c>, the same call with the other variable). One whose item this theme lacks is passed over for the
	/// ferry, the seaplane and the bus in that order (<see cref="FallbackOrder"/>), and so is one the full thing table
	/// cannot make (<see cref="ParkState.NextThingId"/>).
	/// </summary>
	/// <remarks>
	/// <b>Not the original's in one way</b>: where none of the three has an item the original sets no vehicle current
	/// (its <c>"Fatal error: Could not find either the bus or the plane feature"</c>), and here the one asked for is
	/// current with no thing, so the guests still come (<see cref="StepArrivals"/>). Every theme ships all three.
	/// </remarks>
	private void Summon( int size )
	{
		if ( _arrivalVehicle == 0 )
		{
			// Size nought is one of the three at random, the leavers' summons: one draw of the park's generator, the
			// save's own seed (FUN_0051a2f0).
			var seed = State.RandomSeed;
			var asked = size != 0 ? size : VehicleAtRandom( State.Draw() );

			Log.Info( $"People: the {ParkFixedItems.VehicleName( asked )} summoned on mGameTick {State.GameTick}"
				+ (size == 0 ? $", at random: mRandomSeed 0x{seed:x8} to 0x{State.RandomSeed:x8}" : "") );

			_arrivalVehicle = asked;

			foreach ( var tried in FallbackOrder( asked ) )
			{
				if ( _vehicleThings[tried - 1] != 0 )
				{
					_arrivalVehicle = tried;
					break;
				}

				if ( MakeVehicle( tried ) is not (var thing and not 0) )
					continue;

				_vehicleThings[tried - 1] = thing;
				_arrivalVehicle = tried;

				if ( VehicleScript( tried ) is { } made )
					made.Set( VehicleState, 1 );

				return;
			}
		}

		if ( VehicleScript( _arrivalVehicle ) is { } vehicle && !vehicle.Set( VehicleTrigger, 1 ) )
		{
			Log.Warning( $"People: the {ParkFixedItems.VehicleName( _arrivalVehicle )}'s script "
				+ $"declares no {VehicleTrigger}, so it cannot be triggered" );
		}
	}

	/// <summary>
	/// The vehicles a summons tries, in turn, for the one <paramref name="asked"/> for: that one, then of the others
	/// the ferry, the seaplane and the bus, as <c>FUN_0051a2f0</c>'s bitmask walks them (bus 1, seaplane 2, ferry 4).
	/// </summary>
	internal static IEnumerable<int> FallbackOrder( int asked )
	{
		yield return asked;

		foreach ( var vehicle in new[] { 3, 2, 1 } )
		{
			if ( vehicle != asked )
				yield return vehicle;
		}
	}

	/// <summary>
	/// Makes the vehicle the file names no thing for, as the summons does through the object constructor
	/// (<c>FUN_0051a2f0</c>, <c>FUN_004db090</c>; <c>docs/exe/saves.md</c>, "OpenTPW's writer, a vehicle made at its
	/// summons"): an object of the vehicle's item on cell (0,0), facing nought, its two ends that cell, at the head
	/// of the object list and of the cell's own, which stamps the analyser's <c>mLastObjectBuilt</c>; its script bound from
	/// its top to its animations (<see cref="ParkRides.BindNew"/>), and its model stood after the frame's walk
	/// (<see cref="ParkFixedItems.Make"/>).
	/// Answers the thing, or nought where this theme has no such item (said and counted) or the thing table is full
	/// (<see cref="ParkState.NextThingId"/>, a deviation: the original falls over); either way nothing is made.
	/// </summary>
	/// <remarks>
	/// Its id is the free list's head (<see cref="ParkState.NextThingId"/>), as the original's summons takes it.
	/// </remarks>
	private int MakeVehicle( int vehicle )
	{
		if ( _behaviour.Catalogue is not { } catalogue || !catalogue.TryGet( ParkFixedItems.VehicleItem( vehicle ), out var item ) )
		{
			Log.Warning( $"People: Could not find the '{ParkFixedItems.VehicleName( vehicle )}' feature" );
			Unimplemented.Report( "VEHICLE_MADE_NO_ITEM" );
			return 0;
		}

		var corner = MapStep.CellId( 0, 0 );
		var thing = State.NextThingId();

		// A full thing table makes no vehicle (ParkState.NextThingId).
		if ( thing == 0 )
			return 0;
		var placed = ParkBuilding.Constructed( item, thing, 0, 0, 0, corner, corner, ParkWorld.BuiltWhen.At( State.CalendarNow ) );

		State.AddObject( placed );
		State.EnterCell( 0, 0, thing );
		var animations = ParkFixedItems.Current?.Make( vehicle, thing );

		ParkRides.Current?.BindNew( placed, item, animations );

		Log.Info( $"People: the {ParkFixedItems.VehicleName( vehicle )} is made thing {thing} on mGameTick {State.GameTick}" );

		return thing;
	}

	/// <summary>
	/// What the current arrival vehicle reports when it is the bus, as a guest heading for the gate asks it:
	/// <c>FUN_0051aad0</c> answers whether a vehicle other than the small crowd's is current, and only when it is
	/// not does <c>FUN_004ff730</c> ask <c>FUN_0051a690</c>, which reads the vehicle script's <c>VAR_STATUS</c> and
	/// answers -1 with none current.
	///
	/// <para>
	/// <b>The original's asking forgets a spent vehicle</b>: <c>FUN_0051a690</c> answers state 6 by setting the
	/// script's <c>VAR_STATUS</c> to nought and clearing <c>mCurrentArrivalVehicle</c>, so a guest heading for the gate
	/// then can drop the bus before the manager sees it, as <see cref="VehicleStatus"/> does here.
	/// </para>
	/// </summary>
	private int BusStatus()
		=> _arrivalVehicle is 2 or 3 ? NoVehicle : VehicleStatus();

	/// <summary>
	/// The park advisor's turn hands a line to the speaker: the response's two props, its slot, its lip file and its row of
	/// the gesture table (<see cref="ParkAdvisorMessages.GestureRowOf"/>, -1 for the random chain). <paramref name="speak"/>
	/// is the speaker's <c>PlayParkResponse</c> (response, sample, props, slot, lip, row), none while there is no speaker.
	/// </summary>
	internal static int SpeakResponse( Func<int, int, int, int, int, int, int, int>? speak, int response, int sample )
	{
		var (first, second) = ParkAdvisorMessages.PropsOf( response );
		return speak?.Invoke( response, sample, first, second, ParkAdvisorMessages.SlotOf( response ),
			ParkAdvisorMessages.LipOf( response, sample ), ParkAdvisorMessages.GestureRowOf( response ) ) ?? 0;
	}

	/// <summary>
	/// The manager's tail, which it reaches on every sweep (<c>FUN_004cf3e0</c>, <c>0x004cf4b6</c>). It asks the
	/// vehicle's status, which is what forgets a spent one (<see cref="VehicleStatus"/>), and acts by whether a guest
	/// stands at the stop to go home (<see cref="LeaverAtTheStop"/>). <b>Nobody</b>: a vehicle at status 4, the stand
	/// it makes for them, is triggered and no other (<c>0x004cf533</c>). <b>Somebody</b>: with no vehicle one is
	/// summoned at random; one at status 0 is triggered, and so is one at 2 whose load is all dropped
	/// (<c>0x004cf4cb</c>..<c>0x004cf526</c>). <c>docs/exe/park.md</c>, "The spent vehicle".
	/// </summary>
	private void StepVehicle()
	{
		var status = VehicleStatus();

		if ( !LeaverAtTheStop() )
		{
			if ( status == VehicleIsLeaving )
				Summon( 0 );

			return;
		}

		if ( status == NoVehicle || status == 0 || (status == VehicleIsUnloading && _arrivalsRemaining == 0) )
			Summon( 0 );
	}

	/// <summary>
	/// The vehicle a draw of the park's generator picks - the summons for size nought: <c>% 3</c>, unsigned, nought the
	/// bus, one the seaplane, two the ferry (<c>FUN_0051a2f0</c>).
	/// </summary>
	internal static int VehicleAtRandom( uint draw ) => (int)(draw % 3) + 1;

	/// <summary>Whether a vehicle other than the small crowd's is current - <c>FUN_0051aad0</c>.</summary>
	internal bool LargerVehicleIsCurrent => _arrivalVehicle is 2 or 3;

	/// <summary>Where this park's guests are admitted and where they go home from, for whoever needs the cells.</summary>
	internal ParkAdmission? Admission => _behaviour.Admission;

	/// <summary>Holds a load with this many still to drop, or with nought lets go of it, so a test can keep a vehicle at any status with or without its load.</summary>
	internal void HoldTheLoad( int stillToDrop = 1 )
	{
		_arrivalsRemaining = stillToDrop;
		_offloading = stillToDrop > 0;
	}

	/// <summary>
	/// Whether a guest stands at the stop to go home - <c>FUN_0051a9d0</c>: the head of any of stop A's four cells
	/// is a guest in state <c>0x15</c>.
	/// </summary>
	internal bool LeaverAtTheStop()
	{
		if ( _behaviour.Admission is not { } admission )
			return false;

		foreach ( var (x, y) in PeepBehaviour.StopCells( admission.BusStopA ) )
		{
			if ( !ParkState.OnMap( x, y ) || State.CellAt( x, y ).Occupant is not (var head and not 0) )
				continue;

			if ( _byId.TryGetValue( head, out var guest ) && guest.State == PeepState.AtTheBusStop )
				return true;
		}

		return false;
	}

	/// <summary>
	/// Whether a guest standing by the road may cross it - <c>FUN_0051a760</c>, the test of a leaver at the crossing's
	/// park side and of an arrival at its bus-stop side alike. Yes with no vehicle current,
	/// or one that is not the small crowd's. With the bus current: no while it moves on (status 3); then yes if more
	/// than nine of the load are still to drop; no while it drives in (1) or unloads (2); else yes. A spent bus is
	/// forgotten by the asking, as <see cref="VehicleStatus"/> does it, and reads as none.
	/// </summary>
	internal bool MayCrossTheRoad()
	{
		if ( _arrivalVehicle != 1 )
			return true;

		var status = VehicleStatus();

		if ( status == 3 )
			return false;

		if ( _arrivalsRemaining > 9 )
			return true;

		return status is not (1 or 2);
	}

	/// <summary>
	/// Takes a guest out of the park - the other half of <see cref="Admit"/>, and what answers
	/// <see cref="PeepState.Leaving"/>. Answers whether one went.
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

		if ( PeepBehaviour.HeldByAThing( peep ) )
			return false;

		GuestLeaving?.Invoke( thingId );

		// A balloon they hold goes with them, deleted rather than let go, as the original's does at the bus
		// (FUN_004fb330, 0x004fb333..0x004fb346).
		_peeps.Remove( peep );
		_byId.Remove( thingId );
		_walks.Remove( thingId );
		_sprites.Remove( thingId );

		_behaviour.State.Forget( thingId );

		ParkGuestSprites.Current?.Remove( thingId );

		State.DeleteThing( thingId );

		Log.Info( $"People: guest {thingId} went home - {_peeps.Count} guests now" );

		return true;
	}

	/// <summary>
	/// Every guest the save named, as a running copy. Staff are left out of <i>this</i> list because they
	/// are a different kind with a block and a behaviour of their own - read by <c>StaffIn</c> just below
	/// and run by <c>StaffBehaviour.Step</c> from the update.
	///
	/// <para>
	/// Static, and takes the park rather than reaching for one, so that a test can build the same list
	/// from the same file without constructing an entity - the rule the park tests already follow for
	/// the file system.
	/// </para>
	/// </summary>
	internal static List<Peep> PeepsIn( IParkInitialState? park )
		=> park == null
			? []
			: [.. park.People
				.Where( person => person.Guest != null )
				.Select( person => new Peep( person.ThingId, person.Guest!.Value, person.Navigator, person.Pace )
				{
					SpriteKind = person.SpriteKind,
					SpriteBank = person.SpriteBank
				} )];

	/// <summary>
	/// Every member of staff the save named, as a running copy - the five kinds of person that are not
	/// model 1.
	/// </summary>
	internal static List<Staff> StaffIn( IParkInitialState? park )
		=> park == null
			? []
			: [.. park.People
				.Where( person => person.Staff != null )
				.Select( person => new Staff(
					person.ThingId, person.Model, person.Staff!.Value, person.Navigator, person.Pace,
					person.LastRecordedMapId ) )];

	/// <summary>Whether a member of staff in the park has this name - what the staff pool asks before it gives one out (<c>FUN_005083f0</c>).</summary>
	internal bool StaffNamed( string name ) => _staff.Exists( member => member.Name == name );

	/// <summary>
	/// Every guest, in the order the sweep gives them their turns: the original's used-thing list from its head
	/// (<c>FUN_00516380</c>, <c>0x005163a4</c>), the newest thing first, then the save's in the order it lists them.
	/// </summary>
	/// <remarks>
	/// The original keeps every kind of thing in that one list, so a hire made after a guest takes their turn before
	/// them; here the guests, the staff and the rides are swept apart, each in the list's order among its own
	/// (<c>docs/QUEUE.md</c>, Q150).
	/// </remarks>
	internal IReadOnlyList<Peep> Peeps => _peeps;

	/// <summary>
	/// The park's guests by thing id - what <see cref="ParkRideOperation"/> takes, so that a ride can ask
	/// what the guest at the head of its queue is doing without being handed the whole simulation.
	/// </summary>
	internal IReadOnlyDictionary<int, Peep> Guests => _byId;

	/// <summary>Every member of staff: the newest hire first, then the save's in the order it lists them.</summary>
	internal IReadOnlyList<Staff> Staff => _staff;

	/// <summary>A person's thoughts, guest or staff, or null for nobody the park holds.</summary>
	internal Thoughts? ThoughtsOf( int thingId )
		=> _byId.TryGetValue( thingId, out var peep )
			? peep.Thoughts
			: _staff.FirstOrDefault( member => member.ThingId == thingId )?.Thoughts;

	/// <summary>
	/// How many of the game's 31ms ticks pass between turns of the thing engine.
	///
	/// <para>
	/// <b>The thing engine is NOT on the 31ms beat, and running it on that beat makes every guest in the
	/// park run eight times too fast.</b> The park loop gates it at <c>0054f668</c> -
	/// <c>TEST byte ptr [0x00877d34],0x7</c> then <c>JNZ</c> - so the whole block below that test, which
	/// contains <b>both</b> routes to <c>FUN_00516380</c> (the direct call at <c>0054f7bb</c> and
	/// <c>FUN_005166b0</c> at <c>0054f760</c>), runs only when the counter divides by eight. That counter
	/// is the loop's own tick, incremented once per step at <c>0054f4cd</c>/<c>0054f4d6</c> and zeroed at
	/// park entry.
	/// </para>
	/// <para>
	/// <b>The arithmetic that confirms it.</b> A person's <c>MaxSpeed</c> is set by <c>FUN_00510190</c> as
	/// <c>factor * 13107.2</c>, and 13107.2 is <c>0.2 * 65536</c> - so a factor of one is a fifth of a cell
	/// per <i>thing</i> tick. A guest settled at a base of 120 carries 15728, a factor of 1.2 (the shipped park's
	/// guests carry 0.6 to 1.4 by their base; <see cref="Peep.Pace"/>). At eight game ticks to a thing tick that
	/// is <b>0.96 cells a second</b>, a walking pace; at one it is 7.7.
	/// </para>
	/// </summary>
	public const int ThingTickEvery = 8;

	/// <summary>
	/// How many thing sweeps one pass of the park's loop may run: three. The loop counts a pass's sweeps
	/// (<c>[0x00879064]</c>, zeroed once a pass at <c>0x0054fc2a</c>, drawn or not) and a step that comes due with
	/// three already run skips the sweep (<c>0x0054f680</c>): the loop's own step counter moves on,
	/// <c>mGameTick</c> does not, and nothing makes it up (<c>docs/exe/park-engine.md</c>, "What the 31 ms tick
	/// drives"). The first three of a frame run. It binds only in a frame longer than about 0.74 s.
	/// <para>
	/// The sweep hands its things <see cref="ParkState.GameTick"/>, which a dropped step does not move.
	/// </para>
	/// </summary>
	internal const int SweepsAFrame = 3;

	/// <summary>
	/// The sprites' clock at the sweep being run, in milliseconds of the 31 ms tick: what the sprite step is handed,
	/// and so what a balloon built on a ride's turn starts from (<see cref="ParkRideOperation.SpriteNow"/>).
	/// </summary>
	private int _sweepSpriteClock;

	/// <summary>The sweeps run and the sweeps dropped by <see cref="SweepsAFrame"/> since the park was made, for the console.</summary>
	internal int SweepsRun { get; private set; }

	/// <inheritdoc cref="SweepsRun"/>
	internal int SweepsDropped { get; private set; }

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
		// The per-frame placement's half of the stranded bookkeeping (FUN_004fa030, 0x004fa0c7): a counter value
		// for every person, and a stranded stamp above it zeroed, which only a stamp from an earlier run of the
		// counter can be. Only a guest is ever stamped, so only guests are asked.
		foreach ( var peep in _peeps )
		{
			if ( ParkState.NextCounter() < peep.StrandedTime )
				peep.StrandedTime = 0;
		}

		var sweeps = 0;
		var dropped = 0;

		for ( var i = 0; i < GameClock.TicksDue; ++i )
		{
			var tick = GameClock.Ticks - GameClock.TicksDue + 1 + i;

			// The park loop steps the sprites BEFORE it reaches the thing gate - FUN_00475360 at 0054f5fb,
			// the gate at 0054f668 - so they are taken in that order here too.
			if ( (tick & (SpriteTickEvery - 1)) == 0 )
			{
				foreach ( var playing in _sprites.Values )
					playing.Step( tick * MillisecondsPerTick );

				// The balloons are instances of the same table, held and let go alike.
				foreach ( var peep in _peeps )
					peep.Balloon?.Sprite.Step( tick * MillisecondsPerTick );

				foreach ( var bursting in _bursting )
					bursting.Sprite.Step( tick * MillisecondsPerTick );

				_bursting.RemoveAll( bursting => bursting.Sprite.Freed );
			}

			if ( (tick & (ThingTickEvery - 1)) != 0 )
				continue;

			// A fourth sweep in one frame is dropped, not owed - see SweepsAFrame.
			if ( sweeps == SweepsAFrame )
			{
				++dropped;
				continue;
			}

			++sweeps;
			++SweepsRun;

			// The park's own clock goes one up before anything in the sweep runs (FUN_00516380, 0x00516394).
			State.AdvanceGameTick();
			// <b>The number handed on is the park's clock, mGameTick, not the 31 ms tick, and that is not
			// cosmetic.</b> Peep.Tick spreads guests across four slots by (id & 3) == (tick & 3); every 31 ms
			// tick that reaches here is a multiple of eight, and four divides eight, so passing that would make
			// the test true only for guests whose id is a multiple of four and starve the other three quarters
			// of their needs for ever. Every clock a guest's handler reads is mGameTick, one up a sweep
			// (docs/exe/ride-operation.md, "The guests' and the objects' clock").
			var thingTick = State.GameTick;

			// The sprites' clock at this sweep, the 31 ms tick's milliseconds, for a balloon built on a ride's turn.
			_sweepSpriteClock = tick * MillisecondsPerTick;

			foreach ( var peep in _peeps )
			{
				// FUN_004fa870 eases the walking speed first, then stamps the position.
				peep.Pace();

				// Where they start this tick, before anything moves them - the original's FUN_004fa870,
				// which is the first call of the guest tick handler (0x00501658) and sits ahead of that
				// handler's own (id & 3) stagger. So it runs for every guest on every sweep whatever
				// state they are in, and NOT only for the ones that walk: see
				// PeepNavigator.StampPrevious for why stamping only walkers makes a stopped guest
				// oscillate for ever.
				peep.Navigator.StampPrevious();

				peep.Tick( thingTick, OnACountingCell( peep ), ThinkOfNeeds, CellEffects( peep ) );

				// The needs turn's bubble call (0x00501951): on every needs turn, riding or not.
				if ( peep.DueOn( thingTick ) )
					peep.Thoughts.Expire( State.GameTick );

				// The guest tick handler's last call, after its (id & 3) needs block, so every sweep, on the park's
				// own clock (FUN_004fdc90, 0x005019da).
				peep.AgeRefusals( State.GameTick );

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

				// A balloon let go by the needs or by a state entered goes on bursting where it was.
				TakeLetGo( peep );

				// FUN_004d4190, whose only caller is the per-guest needs call - so what the walk asked for
				// lands on that guest's own turn in four rather than at once. Which side of the walk it
				// sits on is not established, and cannot matter here: the walk asks for an animation only
				// when the sprite is not already playing it, so a turn either way changes nothing after
				// the first.
				AskTheWalk( peep, playing );

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
				// FUN_004fa870 at 0x00505495 exactly as the guest handler does: the speed eased, then the stamp.
				MoveEffect( member );
				member.Pace();
				member.Navigator.StampPrevious();

				// The staff handler's own bubble call, on the member's sweep in four of the park's clock
				// (FUN_00505490, 0x005054b3..0x005054c0).
				if ( (member.ThingId & 3) == (State.GameTick & 3) )
					member.Thoughts.Expire( State.GameTick );

				var playing = _sprites.GetValueOrDefault( member.ThingId );

				// On the park's clock, mGameTick, as every staff handler reads it (0x004d6545 the guard's): the
				// idle stamps are readings of it, and the guard's walk-or-stay is its low two bits.
				if ( _staffWalks.TryGetValue( member.ThingId, out var walk ) )
					_staffBehaviour.Step( member, walk, playing, State.GameTick );

				if ( playing == null )
					continue;

				// A sprite on a state's script whose save kept no frames a direction (+0xbc nought) would loop on
				// it for ever: it is read from the bank again, and with no bank the sprite stands.
				if ( playing.IsOnAState && playing.FramesPerDirection <= 0 )
				{
					playing.FramesPerDirection = BankOf( member.ThingId ) is { } bank
						? bank.Sets[playing.Set].FramesPerDirection
						: 0;

					if ( playing.FramesPerDirection <= 0 )
					{
						Unimplemented.Report( "SPRITE_STATE_ANIMATION_NOT_STARTED" );
						playing.Start( SpriteScript.Standing );
					}
				}

				if ( member.NextAnimation != 0 )
				{
					StartAnimation( member.ThingId, playing, member.NextAnimation );
					member.NextAnimation = 0;
				}

				if ( member.NextInterval != 0 )
				{
					playing.Interval = member.NextInterval;
					member.NextInterval = 0;
				}
			}

			// Anybody who has walked out of the park goes home, taken out here rather than inside the
			// loop above because removing from a list while it is being walked would throw. PeepBehaviour
			// puts them into these states and deliberately does not act on either: it owns what a guest
			// wants, never the list they are in.
			//
			// A leaver walks HeadingForExit -> PickingACellOutside (19) -> WalkingOutside (20) ->
			// AtTheBusStop (21) and goes from there when a vehicle stands for them, which PeepBehaviour
			// answers by putting them in Leaving; the original deletes them in that turn (FUN_0050b780).
			// Newest first, the sweep's own order, which is the order their ids come free in.
			foreach ( var leaver in _peeps.Where( peep => peep.State is PeepState.Leaving ).Select( peep => peep.ThingId ).ToList() )
				Depart( leaver );

			StepArrivals();

			// After the load, and on every sweep rather than only while one is running - the original
			// reaches its own tail the same way, from every arm above. Without this the vehicle is
			// released once and parks at the next of its three spins for ever.
			StepVehicle();
			LogVehicleStatuses();

			// The staff pool's turn follows the arrival manager's in the sweep's tail (0x004d7b30).
			ParkStaffPool.Current?.Sweep( State.GameTick,
				kind => _staff.Count( member => ParkStaffPool.KindFor( member.Model ) == kind ),
				StaffNamed );

			TakeTheRidesTurns( thingTick );
			RetryGateClose();

			// The advisor's turn is the sweep's last: he is a thing (model 11, FUN_0050b360's arm 0xb), the list is
			// swept newest first, and the two things made before him have no arm, so a message posted on any
			// other thing's turn is said on the same tick (docs/exe/advisor-park.md, "The advisor's turn in the sweep").
			// Catch-up sweeps share a frame timestamp here; the native running clock may query the OS per call.
			// GameClock.Now keeps the engine's clamped-frame timing deviation (docs/exe/advisor-park.md).
			State.AdvisorMessages?.Tick( State.GameTick, (long)(GameClock.Now * 1000),
				( response, sample ) => SpeakResponse( Advisor.Current is { } advisor ? advisor.PlayParkResponse : null, response, sample ),
				(group, subject, second) =>
				{
					// The event's item word is the record's +0x04 (FUN_0059b620): the item a poster handed, else the subject's.
					var item = second ?? (State.TryObject( subject, out var about ) ? about.CatalogueId : 0);
					var name = _behaviour.Catalogue is { } catalogue && catalogue.TryGet( item, out var named ) ? named.Name : null;
					State.Tags.Said( group, subject, Time.WallMilliseconds, item, name );
				},
				AdvisorFacts );

			// The sweep's end: the ids of the things deleted in it go back on the free list (0x00516654..0x00516685), before the tail.
			State.FreeDeletedThings();

			// Then the sweep's tail, after every thing's turn and so after the advisor's (FUN_00516380 turns the things at
			// 0x005163fb and calls the tail at 0x00516695): the golden tickets on every hundredth tick (0x004d7b60) are
			// owed here and tested by Level.TurnTheCalendar once the calendar has turned, as the original's tail has them
			// (0x004d7b20), so a ticket's line waits for the next sweep's turn.
			ParkGoldenTickets.Owe( State.GameTick );
		}

		if ( dropped == 0 )
			return;

		SweepsDropped += dropped;
		Log.Info( $"Park sweep: {sweeps} run and {dropped} dropped in a frame of {GameClock.TicksDue} ticks, mGameTick {State.GameTick}" );
	}

	/// <summary>
	/// Whether the view is first person, in which a thought is stored and no bubble is made
	/// (<c>gui_CameraFlags &amp; 0x16</c>, <c>FUN_0050be80</c>).
	/// </summary>
	internal static bool FirstPersonView => ParkGuestSprites.Current?.FirstPerson ?? false;

	/// <summary>
	/// The needs turn's thought (<c>0x00501913</c>..<c>0x0050192d</c>): one draw of the park's generator, and one
	/// time in ten the thought the needs pick (<see cref="Thoughts.PickFromNeeds"/>), set with no sound argument.
	/// The draw comes from the arrivals' generator here, not the park's (<see cref="ParkState.RandomSeed"/>, which only
	/// the leavers' summons draws).
	/// </summary>
	private void ThinkOfNeeds( Peep peep )
	{
		if ( (uint)_arrivalRandom.Next() % 10 != 0 || Thoughts.PickFromNeeds( peep ) is not { } thought )
			return;

		var shown = peep.Thoughts.Set( thought, State.GameTick, FirstPersonView );

		Log.Info( $"Person {peep.ThingId}: thought 0x{thought:x} from their needs, bubble {(shown ? "made" : "not made")}, "
			+ $"tick {State.GameTick}" );
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
	internal void TakeTheRidesTurns( int thingTick )
	{
		if ( _scriptFor == null )
			return;

		// The admission and the score go in for the SETTLE-UP alone: the admission carries the PeepInfo mood
		// constants - MediumHappinessChange, both what a guest loses when a visit gives them nothing and the
		// multiplier on what winning is worth, and the excitement match's four - and the score what each kind
		// likes. Without them those arms leave happiness alone rather than moving it by an invented number.
		var operation = new ParkRideOperation( _behaviour.State, Guests, _behaviour.Admission, _behaviour.Score,
			_banks )
		{
			SpriteNow = _sweepSpriteClock
		};

		// <b>The park as it stands, not as the file left it.</b> A thing bought this session lives in
		// ParkState's list and in no other, so a sweep over the save's list hands it no turn at all - it
		// binds a script and animates, and then never invites, never dismisses and never takes a fare,
		// which is a ride that looks alive and is not.
		//
		// A copy of the list, because a turn may replace a thing's record under the walk: a toilet's use lowers
		// its State of repair (ParkRideOperation.WearByUse), and a ride's running does (WearByRunning).
		foreach ( var listed in _behaviour.State.Objects.ToArray() )
		{
			var thing = listed;
			var script = _scriptFor( thing.ThingId );

			// FUN_004e0b90's last three steps: the wear and the breakdown request, the toilet's worn flag and the
			// stale head. States 3 and 4 return before ever reaching them.
			if ( thing.State is not (3 or ParkRideChoice.StateRefusedFour) )
			{
				operation.WearAndAskToBreak( script, thing, ItemOf( thing ), thingTick );

				// The rest of the turn reads the thing as the wear left it.
				if ( _behaviour.State.TryObject( thing.ThingId, out var worn ) )
					thing = worn;

				operation.TellTheWorn( script, thing.ThingId );
				operation.DropStaleQueueHead( thing.ThingId );
			}

			// FUN_004e0e00 is a switch on mState and nothing else.
			switch ( thing.State )
			{
				// FUN_004e14e0: invite, then let anybody off, or with the script's VAR_BROKEN set take the ride
				// to broken down or condemned (BreakDown).
				case 0:
					// <b>The watchdog the tail of FUN_004e1220 runs on every turn that does not
					// invite.</b> Invite bails while the ride already holds a nominee, and otherwise only
					// AdmitPerson, Forget and Close let one go - so a guest who was called forward and
					// then stopped heading for the ride would hold the nomination for ever and nobody else
					// could be called.
					//
					// <b>It is NOT a fault anybody has seen:</b> over 50 samples of a live park the Belly
					// Bounce held a nominee in 8 of them and the longest unbroken hold was 2, so
					// nominations clear on their own here. It matches the original's order; it does not
					// fix an observed freeze.
					// ParkClosedRideTests drives this call through the ride's own turn, and
					// ParkRideInviteTests the drop itself.
					//
					// A closed ride (mCanLoad nought) runs FUN_004e0450 in Invite's place and invites nobody,
					// and the watchdog is skipped with it (0x004e13fc).
					if ( thing.CanLoad == 0 )
						CompleteOrTurnAway( operation, script, thing, thingTick );
					else if ( operation.Invite( script, thing, TrackTypeOf( thing ) ) == 0 )
						operation.DropUnreadyNominee( thing );

					if ( script == null )
						break;

					if ( script[ParkRideOperation.BrokenVariable] == 0 )
						operation.Dismiss( script, thing, thingTick, _rideRandom, WalkFor, _behaviour.Park,
							_behaviour.Catalogue );
					else
						BreakDown( operation, script, thing, thingTick );

					break;

				// State 3 is what the constructor gives every object whose item is not choosable, so nobody is
				// ever offered it. FUN_004e0e00's jump table sends 3 straight to its return
				// (docs/exe/ride-operation.md, "The second half"), and this does nothing either.
				case 3:
					break;

				// Broken down (1), waiting for an upgrade (2) or condemned (4): finish whoever was
				// mid-admission or put the head out (FUN_004e0450), then let them off. A ride reaches 1 and 4
				// through BreakDown; nothing here moves one into 2, the upgrade being unbuilt.
				case ParkRideChoice.StateRefusedOne:
				case 2:
				case ParkRideChoice.StateRefusedFour:
					CompleteOrTurnAway( operation, script, thing, thingTick );

					operation.Dismiss( script, thing, thingTick, _rideRandom, WalkFor, _behaviour.Park,
						_behaviour.Catalogue );
					break;
			}
		}
	}

	/// <summary>
	/// A ride whose script says it has broken, on its own turn - the tail of <c>FUN_004e14e0</c> and
	/// <c>FUN_004e0e60</c>'s arms 1 and 4 (<c>docs/exe/ride-operation.md</c>, "The second half").
	///
	/// <para>
	/// The remaining life (<c>+0x48</c>) cut to a byte decides (<c>0x004e1508</c>): above nought the ride is
	/// broken down, its hoardings raised with the broken picture (<c>FUN_00454550( model, 2 )</c>) and its state
	/// set 1; at nought it is condemned, the picture and the state 4. Setting either state runs one completion,
	/// shuts the ride as every close does (<see cref="ParkRideOperation.Close"/>) and posts to the event bus,
	/// which is counted. The script reads <see cref="ParkRideOperation.ClosedVariable"/> and ends its go, and the
	/// turns after this one are the state's own: a completion and a rider let off on each.
	/// </para>
	/// </summary>
	internal void BreakDown( ParkRideOperation operation, RideScript script, ParkWorld.CatalogueObject thing,
		int thingTick )
	{
		var condemned = (byte)(int)thing.RemainingLife == 0;
		var state = condemned ? ParkRideChoice.StateRefusedFour : ParkRideChoice.StateRefusedOne;

		Log.Info( $"Object {thing.ThingId}: Setting state {(condemned ? "CONDEMNED" : "BROKEN_DOWN")}" );
		State.HoardingFor( thing.ThingId )?.Close( condemned ? 4 : 2 );

		if ( condemned )
			Log.Info( "Ride has become CONDEMNED!!!" );

		CompleteOrTurnAway( operation, script, thing, thingTick );
		operation.Close( script, thing.ThingId );

		// The event is sent before the state is stored. A break's is heard by the mechanics' base; its other
		// two hearers (the kind-17 relay's FUN_00481530 and the tag system) and a condemned ride's are counted.
		if ( !condemned && MessageForABreak( _staff, thing ) is var message and not 0 )
			State.AdvisorMessages?.Post( message, State.GameTick, thing.ThingId );

		Unimplemented.Report( condemned ? "RIDE_CONDEMNED_EVENT" : "RIDE_BROKEN_DOWN_EVENT" );

		if ( State.TryObject( thing.ThingId, out var shut ) )
			State.ReplaceObject( shut with { State = state } );
	}

	/// <summary>
	/// What the mechanics' base tells the advisor as a ride breaks down - <c>FUN_004dadc0</c>, thing kind 10's
	/// hearer of the event of type 7 (docs/exe/advisor-park.md, "A ride broken down and no mechanic to come").
	/// With no mechanic in the park, <see cref="ParkAdvisorMessages.NoMechanics"/>. Otherwise the mechanics
	/// are looked at in turn: one whose job already names the ride ends the look, with
	/// <see cref="ParkAdvisorMessages.MechanicOnHisWay"/> where nobody called him to it; one with no job who is
	/// neither resting, on his way to rest nor striking ends it with nothing said; and with nobody left,
	/// <see cref="ParkAdvisorMessages.MechanicsBusy"/>. Nought is no message.
	/// <para>
	/// The original looks along the mechanics' chain (<c>mFirstMechanic</c>, each one's <c>+0x210</c>); the order
	/// here is the staff list's, which is the chain's only by chance. The order matters only where one mechanic
	/// already names the ride and another is free.
	/// </para>
	/// </summary>
	internal static int MessageForABreak( IEnumerable<Staff> staff, ParkWorld.CatalogueObject ride )
	{
		var anyMechanic = false;

		foreach ( var member in staff )
		{
			if ( member.Model != MechanicModel )
				continue;

			anyMechanic = true;

			if ( member.ObjectToRepair == ride.ThingId )
				return ride.RequestedService == 0 ? ParkAdvisorMessages.MechanicOnHisWay : 0;

			if ( member.ObjectToRepair == 0 && member.Activity is not (StaffActivity.GoingToRest
				or StaffActivity.Resting or StaffActivity.GoingOnStrike or StaffActivity.OnStrike) )
				return 0;
		}

		return anyMechanic ? ParkAdvisorMessages.MechanicsBusy : ParkAdvisorMessages.NoMechanics;
	}

	/// <summary>
	/// A closing ride's completion - <c>FUN_004e0450</c>, one head per call: a head in
	/// <see cref="PeepState.EnteringRide"/> whom the script's admit slot no longer names is forced on
	/// (<see cref="ParkRideOperation.CompleteAdmission"/>), and any other head is put out of the queue
	/// (<c>0x004e0554</c>).
	/// </summary>
	/// <remarks>
	/// <b>Putting out is <c>FUN_004ddd20</c> and then <c>FUN_005012f0</c></b>: off the queue, the admit slot
	/// emptied if it names them, <see cref="ParkAdmission.MediumHappinessChange"/> off, back to deciding,
	/// and the kids' sound at their feet when their id divides by eight (<c>0x0050133d</c>). The head is the
	/// ride's own, so they leave its queue whatever they name. It runs on every turn of a closed ride and on
	/// every turn of one broken down, waiting for an upgrade or condemned.
	/// </remarks>
	internal void CompleteOrTurnAway( ParkRideOperation operation, RideScript? script,
		ParkWorld.CatalogueObject thing, int thingTick )
	{
		var head = State.FirstInQueue( thing.ThingId );

		if ( operation.CompleteAdmission( script, thing.ThingId, thingTick, _rideRandom ) )
		{
			Log.Info( $"Object {thing.ThingId}: script admitted person {head} but he doesn't know yet, "
				+ "forcing him onto ride" );
			return;
		}

		if ( head == 0 )
			return;

		ParkRideOperation.LeaveQueue( State, script, thing.ThingId, head );

		if ( !_byId.TryGetValue( head, out var peep ) )
			return;

		_behaviour.DismissFromTheQueue( peep, thingTick );

		Log.Info( $"People: guest {peep.ThingId} turned away by closed thing {thing.ThingId}, "
			+ $"now {peep.State} with happiness {peep.Happiness:0}" );

		PutOffAtTheirFeet( peep, thing );
	}

	/// <summary>
	/// The kids' sound for a guest put out of a queue, at their feet, when their id divides by eight - see
	/// <see cref="SoundFor"/>.
	/// </summary>
	internal static void PutOffAtTheirFeet( Peep peep, ParkWorld.CatalogueObject thing )
	{
		if ( SoundFor( PeepBehaviour.PutOff.Queueing, peep.ThingId, thing.Flags, seated: false ) == PutOffSound.Feet
			&& ParkGuestSprites.Feet( peep.Navigator.Position ) is { } feet )
			ParkAudio.Current?.PutOff( feet );
	}

	/// <summary>
	/// The gate command and rides of the park's door - <c>FUN_00519ef0</c>'s walk along the object chain, over every
	/// object a guest may be offered (<c>+0x32 &amp; 4</c>). Closing closes each (<c>0x0051a1ae</c>); opening
	/// opens each that <see cref="ParkRideOperation.MayOpen"/> allows (<c>0x0051a013</c>..<c>0x0051a01e</c>).
	/// See <see cref="ParkState.SetParkClosed"/>.
	/// </summary>
	internal void DoorMoved( bool closed )
	{
		CommandGateFromDoor( closed );

		var operation = new ParkRideOperation( State, Guests );

		foreach ( var thing in State.ObjectsInChainOrder().ToArray() )
		{
			if ( !thing.IsVisitable )
				continue;

			var script = _scriptFor?.Invoke( thing.ThingId );

			if ( closed )
				operation.Close( script, thing.ThingId );
			else if ( ParkRideOperation.MayOpen( _behaviour.Park, thing, TrackTypeOf( thing ) ) )
				operation.Open( script, thing.ThingId );
		}
	}

	/// <summary>The ride-window switch, FUN_0048ccf0: down closes, up opens without a handler guard.</summary>
	internal void SetRideClosed( int thingId, bool closed )
	{
		var operation = new ParkRideOperation( State, Guests );
		var script = _scriptFor?.Invoke( thingId );
		if ( closed )
			operation.Close( script, thingId );
		else
			operation.Open( script, thingId );
	}

	/// <summary>The position-cell test used by the door, independent of occupancy and admission state.</summary>
	private bool InsideGateCensus( PeepNavigator navigator )
	{
		var (x, y) = navigator.Position.Cell;
		return x >= 0 && y >= 0 && x < ParkWorld.MapSize && y < ParkWorld.MapSize
			&& Peep.CountsOn( State.Record( x, y ).Type );
	}

	internal int GateGuestCensus => _peeps.Count( peep => InsideGateCensus( peep.Navigator ) );

	/// <summary>
	/// The mean happiness of the guests <see cref="GateGuestCensus"/> counts, <c>FUN_004c7bb0</c>: each one's happiness
	/// truncated and cut to its low byte, summed as a whole number, over their count; nought with none inside or the park
	/// shut (<c>FUN_0051a280</c>).
	/// </summary>
	internal float GuestsInsideHappiness
	{
		get
		{
			if ( State.ParkIsClosed )
				return 0f;

			var sum = 0;
			var count = 0;

			foreach ( var peep in _peeps )
			{
				if ( !InsideGateCensus( peep.Navigator ) )
					continue;

				sum += (int)peep.Happiness & 0xff;
				++count;
			}

			return count > 0 ? (float)((double)sum / count) : 0f;
		}
	}

	/// <summary>
	/// The guests waiting for the park to open, FUN_004c91c0: those in <see cref="PeepState.WaitingForOpening"/>
	/// standing on either ticket booth's cell, whether or not they have paid.
	/// </summary>
	internal int WaitingToOpen => _behaviour.Admission is { } admission
		? _peeps.Count( peep => peep.State == PeepState.WaitingForOpening
			&& (peep.Navigator.Position.Cell == admission.TicketBoothA || peep.Navigator.Position.Cell == admission.TicketBoothB) )
		: 0;

	/// <summary>
	/// What the advisor's score functions read of the park: the door, the things standing of a ride's item, of a
	/// sideshow's and of a shop's (FUN_004ca8e0, a thing of type 3 whose item's WhichUIType is the kind asked
	/// for), the toilets (FUN_004c9ee0, an object's +0x32 &amp; 1), how many of the rides, shops, sideshows and
	/// toilets are shut (FUN_004ca9b0, FUN_004c9f90: mCanLoad nought), the staff rooms (FUN_004ca030, an object's
	/// +0x32 &amp; 2) and those of them shut (FUN_004ca0c0), the guests waiting to be let in, and the
	/// first thing a guest may choose that has a queue whose back is not joined to a path (FUN_004ccb00 asked
	/// with nought: an object's +0x32 &amp; 4 and &amp; 8, FUN_004de4a0 nought), the object the builder holds
	/// passed over (FUN_00527930); and the mechanics (FUN_004c9960 asked for type 4) and the rides worn
	/// (FUN_004ca3a0: a thing of a ride's item whose State of repair's whole number is under the limit row 5's
	/// score hands it, or whose mState is 1, broken down) and the rides broken down (FUN_004ca310: a thing of a
	/// ride's item whose mState is 1); and the whole days since the newest member of staff was made
	/// (FUN_004c90d0, <see cref="ParkState.DaysSinceStaffHired"/>); and the kinds of ride on offer (0x004ca450
	/// asked with nought: a catalogue item of a ride's kind whose control record is researched and whose
	/// GoldenTicketCost is nought), those of them of which a thing stands (0x004ca7b0 asked with nought and
	/// nought: the control record's count above nought) and the whole days since the newest object was made
	/// (0x004c9070, <see cref="ParkState.DaysSinceObjectBuilt"/>); and the bank's balance (FUN_005195d0's
	/// thing's +0xc, <see cref="ParkState.Balance"/>); and the guests inside the park (FUN_004c9130,
	/// <see cref="GateGuestCensus"/>) with those of them whose toilet need is over the advisor's level
	/// (0x004c9290), whose thirst is over its row 3's (0x004c93f0) and whose hunger is over its row 4's (0x004c9340), each
	/// need's whole number taken to its low byte; and the researchers (FUN_004c9960 asked for type 8) and the lab's projects of a feature's
	/// kind that are not done (0x004ca690, <see cref="ParkResearch.Unfinished"/>), and of a ride's (0x004ca630); and the map's developed cells
	/// (FUN_004c81b0, <see cref="ParkState.DevelopedCells"/>); and this month's misbehaving kids (FUN_004c94a0 asked with
	/// nought, <see cref="ParkState.MisbehavingKids"/>); and the guests inside under <see cref="ParkAdvisorMessages.UnhappyBelow"/>
	/// happiness, each one's truncated to its low byte (FUN_004c9750); and the staff pool's researchers to hire
	/// (FUN_00508380 asked for kind four); and the lifetime visitors (FUN_004c94c0), <see cref="GuestsInsideHappiness"/>
	/// (FUN_004c7bb0), the arrival loads called (five once the ring of loads has wrapped, else its slot plus one;
	/// 0x005a078e), <see cref="AdvisorTicketFacts"/>, which the golden-ticket hints read, and each shop's and sideshow's thirty days of
	/// customers, walk-aways and satisfaction (the outlets, rows 238 to 257). With no catalogue no kind is counted.
	/// </summary>
	/// <remarks>
	/// The builder's object is DAT_008186ac, which the original also sets from the thing the interface has
	/// selected and keeps after the tool is put away; here it is the thing the queue tool is armed for, and no
	/// other (docs/exe/advisor-park.md, "A queue not joined to a path").
	/// </remarks>
	internal ParkAdvisorMessages.ParkFacts AdvisorFacts()
	{
		var (rides, sideshows, shops, toilets, features) = (0, 0, 0, 0, 0);
		var (ridesShut, shopsShut, sideshowsShut, toiletsShut) = (0, 0, 0, 0);
		var (staffRooms, staffRoomsShut) = (0, 0);
		var unjoined = 0;
		var outlets = new List<ParkAdvisorMessages.OutletFigures>();
		var (ridesWorn, ridesBroken) = (0, 0);
		var wornBelow = State.AdvisorMessages?.WornBelow ?? 0;
		// The need is taken as its whole number's low byte, as the original compares it (0x004c9301); with no
		// advisor there is no level to be over.
		var toiletNeedOver = State.AdvisorMessages?.ToiletNeedOver ?? 0xff;
		var thirstierThan = State.AdvisorMessages?.ThirstierThan ?? 0xff;
		var hungrierThan = State.AdvisorMessages?.HungrierThan ?? 0xff;
		var (inside, needing, unhappy, thirsty, hungry) = (0, 0, 0, 0, 0);
		foreach ( var peep in _peeps )
		{
			if ( !InsideGateCensus( peep.Navigator ) ) continue;
			++inside;
			if ( ((int)peep.Toilet & 0xff) > toiletNeedOver ) ++needing;
			if ( ((int)peep.Thirst & 0xff) > thirstierThan ) ++thirsty;
			if ( ((int)peep.Hunger & 0xff) > hungrierThan ) ++hungry;
			if ( ((int)peep.Happiness & 0xff) < ParkAdvisorMessages.UnhappyBelow ) ++unhappy;
		}

		foreach ( var thing in State.ObjectsInChainOrder() )
		{
			var shut = thing.CanLoad == 0 ? 1 : 0;

			if ( unjoined == 0 && thing.IsVisitable && thing.HasQueuePath && thing.ThingId != ParkBuildMode.Serves
				&& !ParkRideOperation.BackOfQueueConnected( _behaviour.Park, thing ) )
				unjoined = thing.ThingId;

			if ( thing.IsToilet )
			{
				++toilets;
				toiletsShut += shut;
			}

			if ( thing.IsRestArea )
			{
				++staffRooms;
				staffRoomsShut += shut;
			}

			if ( _behaviour.Catalogue is not { } catalogue || !catalogue.TryGet( thing.CatalogueId, out var item ) ) continue;

			if ( ParkAdvisorMessages.OutletOf( item.ShopType, item.UiType ) is var outlet and not ParkAdvisorMessages.Outlet.None )
			{
				var days = State.RingsFor( thing.ThingId );
				outlets.Add( new( thing.ThingId, outlet, State.AgeInDays( thing ), days.Customers.LastDays( 30 ),
					days.WalkAways.LastDays( 30 ), days.SatisfactionOfLastDays( 30 ) ) );
			}

			if ( item.UiType == ItemDescriptionFile.Ride )
			{
				++rides;
				ridesShut += shut;
				if ( ((int)thing.StateOfRepair & 0xff) < wornBelow || thing.State == ParkRideChoice.StateRefusedOne )
					++ridesWorn;
				if ( thing.State == ParkRideChoice.StateRefusedOne )
					++ridesBroken;
			}
			else if ( item.UiType == ItemDescriptionFile.SideShow )
			{
				++sideshows;
				sideshowsShut += shut;
			}
			else if ( item.UiType == ParkRideOperation.ShopUiType )
			{
				++shops;
				shopsShut += shut;
			}
			else if ( item.UiType == ItemDescriptionFile.Feature )
			{
				++features;
			}
		}

		var shopKinds = Kinds( _behaviour.Catalogue?.All ?? [], ParkRideOperation.ShopUiType, id => _research?.IsResearched( id ) == true, id => State.BuiltOf( id ).Standing );
		var sideshowKinds = Kinds( _behaviour.Catalogue?.All ?? [], ItemDescriptionFile.SideShow, id => _research?.IsResearched( id ) == true, id => State.BuiltOf( id ).Standing );
		var featureKinds = Kinds( _behaviour.Catalogue?.All ?? [], ItemDescriptionFile.Feature, id => _research?.IsResearched( id ) == true, id => State.BuiltOf( id ).Standing );
		var ability = ResearchAbility;
		var patrols = MechanicPatrols();

		return new ParkAdvisorMessages.ParkFacts( State.ParkIsClosed, rides, sideshows, WaitingToOpen,
			shops, toilets, shopsShut, sideshowsShut, toiletsShut, ridesShut, unjoined, staffRooms, staffRoomsShut,
			_staff.Count( member => member.Model == MechanicModel ), ridesWorn, ridesBroken, State.DaysSinceStaffHired,
			RideKinds( item => _research?.IsResearched( item.Id ) == true ), RideKinds( item => State.BuiltOf( item.Id ).Standing > 0 ),
			State.DaysSinceObjectBuilt, shopKinds.OnOffer, shopKinds.Standing, State.Balance, sideshowKinds.OnOffer, sideshowKinds.Standing,
			features, featureKinds.OnOffer, featureKinds.Standing, inside, needing,
			_staff.Count( member => member.Model == ResearcherModel ), _research?.Unfinished( ItemDescriptionFile.Feature ) ?? 0,
			_research?.Unfinished( ItemDescriptionFile.SideShow ) ?? 0,
			_research?.Rate( ItemDescriptionFile.SideShow, ability, MostResearchAbility ) ?? 0, ability, MostResearchAbility,
			new ParkAdvisorMessages.StaffTraining( TrainingOf( 0 ), TrainingOf( 1 ), TrainingOf( 2 ), TrainingOf( 3 ), TrainingOf( 4 ) ),
			State.DevelopedCells(), State.MisbehavingKids.Month, unhappy, State.LitterShare(), State.MisbehavingKids.GotAwayThisMonth,
			_research?.Unfinished( ItemDescriptionFile.Ride ) ?? 0,
			ParkStaffPool.Current?.OfKind( ParkStaffPool.Kinds - 1 ).Count() ?? 0, thirsty, hungry,
			State.LifetimeVisitors, GuestsInsideHappiness,
			State.ArrivalLoads.Wrapped ? State.ArrivalLoads.Length : State.ArrivalLoads.Current + 1, AdvisorTicketFacts(), new( outlets ),
			patrols.Areas, patrols.Entrances, patrols.Patrolled );
	}

	/// <summary>
	/// What row 19's score reads of the mechanics' patrol areas: the mechanics whose corners are not the whole map's
	/// (<c>0x004c9b40</c> asked for type 4, <c>0x00506eb0</c>), the map's cells of <c>mType</c> 9, the ride entrances, and
	/// those of them inside one of those mechanics' rectangles (<c>0x004cd500</c> asked for type 4 and mode 2, after
	/// <c>0x004cd340</c> has marked every cell of each rectangle). A mechanic with no area at all, nought and nought,
	/// counts and holds no cell, as the original's, whose one mark for it lands on cell id nought, off the map.
	/// </summary>
	private (int Areas, int Entrances, int Patrolled) MechanicPatrols()
	{
		var penned = _staff.Where( member => member.Model == MechanicModel
			&& !(member.PatrolBottomLeft == OpenTPW.Staff.WholeMapBottomLeft && member.PatrolTopRight == OpenTPW.Staff.WholeMapTopRight) ).ToList();
		var (entrances, patrolled) = (0, 0);

		for ( var y = 0; y < ParkWorld.MapSize; ++y )
		{
			for ( var x = 0; x < ParkWorld.MapSize; ++x )
			{
				if ( State.Record( x, y ).Type != CellEdge.RideEnd ) continue;
				++entrances;
				if ( penned.Any( member => member.HasPatrolArea && member.Patrols( x, y ) ) )
					++patrolled;
			}
		}

		return (penned.Count, entrances, patrolled);
	}

	/// <summary>
	/// What the golden-ticket hints read of the player (<c>[0x00f7c58c]</c>, the progress the awards write) and the
	/// level's thresholds; null with no player or no balance, so that no hint scores.
	/// </summary>
	private static ParkAdvisorMessages.TicketFacts? AdvisorTicketFacts()
	{
		if ( Level.Current is not { Balance: { } balance } level || Players.Roster.Current is not { } player )
			return null;

		return ParkGoldenTickets.HintFacts( player.File, level.ThemeName, ParkGoldenTickets.Thresholds.From( balance ), SaveFolder.IsTheme );
	}

	/// <summary>
	/// The most research a park's staff could do at once, which a kind's rate sets the staff's against
	/// (FUN_004c9ed0): <c>StaffPoolInfo.MaxResearchersInPark</c> times the top grade's <c>ResearchAbility</c>.
	/// </summary>
	internal int MostResearchAbility
		=> ParkStaffPool.MostInPark( _balance, ParkStaffPool.Kinds - 1 ) * _staffBehaviour.ResearchAbilityAt( ParkWorld.StaffState.PayGrades - 1 );

	/// <summary>Every researcher on the staff's list's <c>ResearchAbility</c> by their grade, added up (<c>FUN_004c9e60</c> walks <c>mFirstResearcher</c>).</summary>
	internal int ResearchAbility
		=> _staff.Where( member => member.Model == ResearcherModel ).Sum( member => _staffBehaviour.ResearchAbilityAt( member.PayGrade ) );

	/// <summary>The catalogue's items of a ride's kind that cost no golden ticket and that <paramref name="counted"/> holds for (0x004ca450, 0x004ca7b0).</summary>
	private int RideKinds( Func<ParkItemCatalogue.Item, bool> counted )
		=> _behaviour.Catalogue?.All.Count( item => item.UiType == ItemDescriptionFile.Ride && item.GoldenTicketCost == 0 && counted( item ) ) ?? 0;

	/// <summary>
	/// The kinds of shop the advisor's row 71 reads of <paramref name="items"/>, of sideshow its row 75 and of
	/// feature its row 79, by <paramref name="uiType"/>: on offer, the items of that kind that are researched, a
	/// golden ticket's or not (0x004ca510, 0x004ca570, 0x004ca5d0); standing, those of that kind that cost no golden ticket and of which a thing
	/// stands, researched or not (0x004ca7b0 asked with the kind and nought).
	/// </summary>
	internal static (int OnOffer, int Standing) Kinds( IEnumerable<ParkItemCatalogue.Item> items, int uiType, Func<int, bool> researched, Func<int, int> standing )
	{
		var ofKind = items.Where( item => item.UiType == uiType ).ToArray();

		return (ofKind.Count( item => researched( item.Id ) ), ofKind.Count( item => item.GoldenTicketCost == 0 && standing( item.Id ) > 0 ));
	}

	// FUN_00516380 requires thing byte +3 == 0, not the staff activity. This list contains live staff:
	// Fire removes immediately; the original marks +3 then drains deletion after the retry. See park-gate.md.
	internal int GateStaffOutside => _staff.Count( member => member.Model is >= 4 and <= 8
		&& !InsideGateCensus( member.Navigator ) );

	private RideScript? GateScript => _gateThing == 0 ? null : _scriptFor?.Invoke( _gateThing );

	private void CommandGateFromDoor( bool closed )
	{
		if ( GateScript is not { } gate )
		{
			if ( _gateThing != 0 )
				Unimplemented.Report( "PARK_DOOR_COMMANDS_THE_GATE" );
			return;
		}

		if ( !closed || (gate[ParkRides.GateState] == ParkRides.GateIsOpen && GateGuestCensus == 0) )
			gate.Set( ParkRides.GateCommand, closed ? 0 : 1 );

		Log.Info( $"Gate door: {GateDescription()}" );
	}

	/// <summary>After the thing sweep, every 30 world ticks: FUN_00516380 (docs/exe/park-gate.md).</summary>
	internal void RetryGateClose()
	{
		if ( !State.ParkIsClosed || (uint)State.GameTick % 30 != 0 || GateScript is not { } gate
			|| gate[ParkRides.GateState] != ParkRides.GateIsOpen )
			return;

		if ( GateGuestCensus == 0 && GateStaffOutside == 0 )
			gate.Set( ParkRides.GateCommand, 0 );

		Log.Info( $"Gate retry: {GateDescription()}" );
	}

	/// <summary>A pure census for the console, including the clock whose multiples schedule retries.</summary>
	internal string GateDescription()
		=> $"gate {_gateThing} closed {(State.ParkIsClosed ? 1 : 0)} tick {State.GameTick} "
			+ $"command {GateScript?[ParkRides.GateCommand] ?? -1} status {GateScript?[ParkRides.GateState] ?? -1} "
			+ $"guests {GateGuestCensus} staffOutside {GateStaffOutside} waiting {WaitingToOpen}";

	/// <summary>
	/// What kind of track an item runs on, for the gates that read the item rather than the object: the ride's
	/// invite, its opening and reopening, what a guest may be offered, and the staff's own
	/// (<c>StaffBehaviour.TrackTypeOf</c>). Reached the same way <c>ParkRideChooser.ItemFor</c> reaches
	/// it, so there is one lookup rather than two that can disagree.
	/// </summary>
	private ParkItemCatalogue.Item? ItemOf( ParkWorld.CatalogueObject thing )
		=> _behaviour.Catalogue is { } catalogue && catalogue.TryGet( thing.CatalogueId, out var item ) ? item : null;

	private int TrackTypeOf( ParkWorld.CatalogueObject thing )
		=> _behaviour.Catalogue is { } catalogue && catalogue.TryGet( thing.CatalogueId, out var item )
			? item.TrackType
			: 0;

	/// <summary>
	/// Whether the cell a guest stands on counts them (<see cref="Peep.CountsOn"/>): the cell the park has them
	/// linked into, as the original reads the thing's own cell bytes, or the one under their feet before their
	/// first turn has linked them.
	/// </summary>
	/// <summary>
	/// The happiness, illness and hunger words of the region effects on the guest's cell, which their needs turn adds
	/// (<c>FUN_004d8410</c> at <c>0x005016ba</c>, the cell of the thing's own bytes). A cell off the map has none.
	/// </summary>
	internal (short Happiness, short Illness, short Hunger) CellEffects( Peep peep )
	{
		var (x, y) = State.CellOf( peep.ThingId )
			?? (peep.Navigator.Position.X >> 16, peep.Navigator.Position.Y >> 16);

		if ( (uint)x >= ParkWorld.MapSize || (uint)y >= ParkWorld.MapSize )
			return default;

		var words = State.EffectsAt( x, y );

		return (words[0], words[1], words[2]);
	}

	internal bool OnACountingCell( Peep peep )
	{
		var (x, y) = State.CellOf( peep.ThingId )
			?? (peep.Navigator.Position.X >> 16, peep.Navigator.Position.Y >> 16);

		return Peep.CountsOn( State.Record( x, y ).Type );
	}

	/// <summary>
	/// How many guests stand on the cells from <paramref name="reach"/> before to <paramref name="reach"/> after a
	/// packed cell each way, a square cut at the map's edges - <c>FUN_004c8d30( 1, cell, reach, 0 )</c>, which walks
	/// those cells' lists for things of kind 1. Cell nought, no cell, counts nobody.
	/// </summary>
	internal int GuestsNear( int cell, int reach )
	{
		if ( cell <= 0 )
			return 0;

		var (x, y) = ((cell - 1) % ParkWorld.MapSize, (cell - 1) / ParkWorld.MapSize);

		return _peeps.Count( peep =>
		{
			var (px, py) = State.CellOf( peep.ThingId )
				?? (peep.Navigator.Position.X >> 16, peep.Navigator.Position.Y >> 16);

			return Math.Abs( px - x ) <= reach && Math.Abs( py - y ) <= reach;
		} );
	}

	/// <summary>A balloon a guest has let go goes on bursting here, where it was.</summary>
	private void TakeLetGo( Peep peep )
	{
		if ( peep.TakeLetGo() is not { } letGo )
			return;

		_bursting.Add( letGo );

		Log.Info( $"Person {peep.ThingId}: let go of the balloon, colour {letGo.Sprite.Set}, at ({letGo.X:0.00},{letGo.Y:0.00}) "
			+ $"height {letGo.Height:0.00}, life {peep.BalloonLife}" );
	}

	/// <summary>Balloons let go and still bursting, for the drawing.</summary>
	internal IReadOnlyList<Balloon> Bursting => _bursting;

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
	/// console.
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
				+ _behaviour.Explain( peep, walk, State.GameTick );
		}
	}

	/// <summary>This staff member's walk, for the same.</summary>
	/// Dead by CODE: only tests call it (ParkStaffDrawingTests, ParkTickTests, ParkDecidingArmsTests, ParkEvictionTests, ParkStaffPlacementTests and ParkStaffRegionEffectsTests).
	internal PeepWalk? StaffWalkFor( int thingId ) => _staffWalks.GetValueOrDefault( thingId );

	/// <summary>
	/// This person's walk, whoever they are. Guests and staff are held in separate pools, but their thing
	/// ids come from one numbering, so asking each in turn is unambiguous.
	/// </summary>
	/// <remarks>
	/// <b>The drawing asks this rather than <see cref="WalkFor"/></b>, which knows only <c>_walks</c>: given
	/// its null for every member of staff, <see cref="ParkGuestSprites.Standing"/> would fall back to the
	/// position the save left them at, and staff who are simulated, routed and moving would be drawn
	/// standing still - indistinguishable from a behaviour that never ran.
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
		var thingTick = State.GameTick;

		foreach ( var peep in _peeps )
		{
			// Asked before the answer, while the ride's script still holds them.
			var seat = peep.MajorDest == thing.ThingId && peep.State == PeepState.Riding
				? SeatOn( thing, peep )
				: null;

			// Asked before the answer too, while the ride's walk still holds them.
			var walked = peep.MajorDest == thing.ThingId && peep.State == PeepState.Riding
				? ParkGuestSprites.WalkedOnRide( this, peep.ThingId, thing.ThingId )
				: null;

			var how = _behaviour.ThingRemoved( peep, thing.ThingId, thingTick );

			if ( how == PeepBehaviour.PutOff.No )
				continue;

			Log.Info( $"People: guest {peep.ThingId} put off thing {thing.ThingId} ({how}), "
				+ $"now {peep.State} with happiness {peep.Happiness:0}" );

			var sound = SoundFor( how, peep.ThingId, thing.Flags, seat is not null, walked is not null );
			Vector3? heardAt = sound == PutOffSound.Feet
				? ParkGuestSprites.Feet( peep.Navigator.Position )
				: PutOffPoint( sound, seat, walked?.Hidden ?? false, walked?.At ?? default );

			if ( heardAt is { } at )
				ParkAudio.Current?.PutOff( at );
		}

		foreach ( var member in _staff )
			_staffBehaviour.ThingRemoved( member, _staffWalks.GetValueOrDefault( member.ThingId ), thing.ThingId,
				State.GameTick );
	}

	/// <summary>
	/// Tells everybody in a queue it has been measured again - the walk <c>FUN_004de1f0</c> ends with,
	/// <i>"Telling people in queue to reevaluate"</i>, reached through <see cref="ParkState.RemeasureQueue"/>.
	/// Each guest answers through <see cref="PeepBehaviour.QueueShortened"/>, and whoever now stands past the
	/// end is put out. Then the function's tail: <see cref="ParkRideOperation.ReopenAfterRemeasure"/>.
	/// </summary>
	/// <remarks>
	/// <b>Everybody but the ride's nominee is asked</b> (<c>0x004de2b9</c>), head first, and each guest's next
	/// link is read before they answer (<c>0x004de2bd</c>), so the walk carries on past a guest who has just
	/// been let out. A guest's place is found as <c>FUN_004ddf50</c> finds it, giving up at anybody no longer
	/// queueing. One put out makes the kids' sound where a queuer put off by a sale does, when their id
	/// divides by eight (<c>0x0050133d</c>) - see <see cref="SoundFor"/>.
	/// </remarks>
	internal void QueueRemeasured( int objectId )
	{
		if ( !State.TryObject( objectId, out var thing ) )
			return;

		var (_, cells) = ParkRideChoice.QueueCellsFor( _behaviour.Park, thing );
		var room = cells * ParkRideChoice.QueueRoomPerCell;
		var nominee = State.PersonBeingLoaded( objectId );
		var script = _scriptFor?.Invoke( objectId );
		var thingTick = State.GameTick;
		var steps = 0;

		for ( var id = State.FirstInQueue( objectId ); id != 0 && steps < ParkState.LongestQueue; ++steps )
		{
			var next = State.NextInQueue( id );

			if ( id != nominee && _byId.TryGetValue( id, out var peep ) )
			{
				var place = State.PositionInQueue( objectId, id, StillQueueing );

				if ( _behaviour.QueueShortened( peep, place, room, script, thingTick ) )
				{
					Log.Info( $"People: guest {peep.ThingId} put out of thing {objectId}'s queue at place {place} "
						+ $"of {room}, now {peep.State} with happiness {peep.Happiness:0}" );

					PutOffAtTheirFeet( peep, thing );
				}
			}

			id = next;
		}

		new ParkRideOperation( State, Guests )
			.ReopenAfterRemeasure( script, objectId, _behaviour.Park, TrackTypeOf( thing ) );
	}

	/// <summary>Whether a guest is still in a queue's states - <c>FUN_00502430</c>, for the queue walk.</summary>
	private bool StillQueueing( int guestId )
		=> _byId.TryGetValue( guestId, out var peep ) && ParkRideOperation.IsQueueing( peep );

	/// <summary>Where the put-off sound plays for one guest - see <see cref="SoundFor"/>.</summary>
	internal enum PutOffSound
	{
		None,
		Origin,
		Seat,
		Feet,
		Walk
	}

	/// <summary>
	/// Whether a guest put off a sold thing or out of its queue makes the put-off sound, and where: always
	/// for a rider (<c>0x004fb3f5</c>), for a queuer only when their thing id is a multiple of eight
	/// (<c>0x0050133d</c>, in <c>FUN_005012f0</c>, which every way out of a queue runs), and for nobody else.
	/// </summary>
	/// <remarks>
	/// <b>A rider's sprite decides the place.</b> On a thing with
	/// <see cref="ParkWorld.CatalogueObject.KeepsRidersSpriteFlag"/> the ride holds the sprite: a bounce rider
	/// is on the seat node. Without the flag, admission destroyed the sprite; the eviction makes a new one
	/// whose position is still nought when the sound reads it (<c>0x004fb3cd</c>, then <c>FUN_004faa00</c>),
	/// so it plays at the world's origin.
	/// <para>
	/// A walk-on rider (the Jungle Spray's <c>WALKON</c>) has the sprite the <c>WALK</c> placer last put
	/// (<c>FUN_005580a0</c>) while it stands, and the sound plays at that point; one the placer hid (carried
	/// under action 1, 2 or 4: <c>FUN_004f9ed0</c> frees the sprite and writes its handle 0) has none, so the
	/// eviction makes one and the sound plays at the origin, as for a thing without the flag
	/// (<c>docs/exe/ride-operation.md</c>, "Where a walk's rider is drawn"). The point is <see cref="ParkGuestSprites.WalkedOnRide"/>'s,
	/// the absolute one as <see cref="PutOffSound.Seat"/>'s is: the original's sprite height is the point's less the
	/// ground, and every coordinate is truncated to a whole unit.
	/// </para>
	/// </remarks>
	internal static PutOffSound SoundFor( PeepBehaviour.PutOff how, int guestId, int thingFlags, bool seated, bool walked = false )
		=> how switch
		{
			PeepBehaviour.PutOff.Riding when (thingFlags & ParkWorld.CatalogueObject.KeepsRidersSpriteFlag) == 0
				=> PutOffSound.Origin,
			PeepBehaviour.PutOff.Riding => seated ? PutOffSound.Seat : walked ? PutOffSound.Walk : PutOffSound.Feet,
			PeepBehaviour.PutOff.Queueing when (guestId & 7) == 0 => PutOffSound.Feet,
			_ => PutOffSound.None
		};

	/// <summary>
	/// Where a put-off sound other than <see cref="PutOffSound.Feet"/> plays: the origin, the seat, or a walk's rider
	/// (hidden by the placer: the origin; else the point it put them at), null for none.
	/// </summary>
	internal static Vector3? PutOffPoint( PutOffSound sound, Vector3? seat, bool walkHidden, Vector3 walkAt )
		=> sound switch
		{
			PutOffSound.Origin => Vector3.Zero,
			PutOffSound.Seat => seat,
			PutOffSound.Walk => walkHidden ? Vector3.Zero : walkAt,
			_ => null
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
	/// <b>Nothing in the engine moves a rider.</b> All five callers of its "place a person" routine are
	/// accounted for - the ride exit, a generic put-down, a wrapper, the handyman's litter arm and
	/// dropping a staff member - and not one of them is a rider. Their world position legitimately stays
	/// where they queued, and the DRAWING puts them on the ride's own node, which is what this answers.
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
			// the queue instead of on the ride's own node. Walked by index: the drawing asks this of every
			// guest every frame, and a foreach over the interface boxes a new enumerator each time.
			var objects = _behaviour.State.Objects;

			for ( var i = 0; i < objects.Count; ++i )
			{
				var thing = objects[i];

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
	/// The script holding a head of this guest on one of its head nodes, and that node's id, or false where no script
	/// does - <c>ADDHEAD</c>'s table or a walk under action 4, asked of the scripts for the reason <see cref="TrySeatOf"/> gives.
	/// </summary>
	internal bool TryHeadOf( int guestThingId, out RideScript? script, out int node )
	{
		if ( _scriptFor != null )
		{
			var objects = _behaviour.State.Objects;

			for ( var i = 0; i < objects.Count; ++i )
			{
				if ( _scriptFor( objects[i].ThingId ) is not { } candidate || !candidate.TryHeadNode( guestThingId, out node ) )
					continue;

				script = candidate;

				return true;
			}
		}

		script = null;
		node = 0;

		return false;
	}

	/// <summary>
	/// The script whose walk holds this guest in a slot, and that slot (<see cref="RideScript.TryWalkOf"/>), or false where
	/// none does - asked of the scripts for the reason <see cref="TrySeatOf"/> gives. <paramref name="goingThing"/> names a thing
	/// the park has already let go of (a sale unlinks the object before its riders are put off), whose script is asked first.
	/// </summary>
	internal bool TryWalkOf( int guestThingId, out RideScript? script, out RideScript.WalkView slot, int goingThing = 0 )
	{
		if ( goingThing != 0 && _scriptFor?.Invoke( goingThing ) is { } going && going.TryWalkOf( guestThingId, out slot ) )
		{
			script = going;

			return true;
		}

		if ( _scriptFor != null )
		{
			var objects = _behaviour.State.Objects;

			for ( var i = 0; i < objects.Count; ++i )
			{
				if ( _scriptFor( objects[i].ThingId ) is not { } candidate || !candidate.TryWalkOf( guestThingId, out slot ) )
					continue;

				script = candidate;

				return true;
			}
		}

		script = null;
		slot = default;

		return false;
	}

	/// <summary>
	/// What the walk holding this guest asks of their sprite, once a sweep before the queued animation is handed over:
	/// the placer's animation where the sprite is neither on it nor queued for it (<c>FUN_004f9e60</c>, its test
	/// <c>FUN_004d4100</c>), or, for a guest it hides, the thought bubble freed (<c>FUN_004f9ed0</c>,
	/// <c>FUN_0050c0d0</c>).
	/// </summary>
	/// <remarks>
	/// The engine asks every frame, after its catch-up loop, and its hide frees the sprite itself, which the next placement
	/// makes again on the walk at once (<c>FUN_004d4140</c>); here it is asked as the sweep hands the queue over, and the
	/// sprite is kept and put on the walk then.
	/// </remarks>
	private void AskTheWalk( Peep peep, SpriteScript? playing )
	{
		if ( !TryWalkOf( peep.ThingId, out _, out var slot ) || WalkRiders.Of( slot ) is not { } placed )
			return;

		if ( placed.Hidden )
		{
			peep.Thoughts.Free();
			return;
		}

		if ( WalkRiders.Asked( placed, playing, peep.NextAnimation ) is { } animation )
			peep.NextAnimation = animation;
	}

	/// <summary>
	/// What a ride's bounce node is called in its model, by the node's id as a bounce slot holds it - id 1 is
	/// <c>body</c> and the rest are <c>body01</c> upwards.
	/// </summary>
	/// <remarks>
	/// <b>Measured off <c>bouncy.MD2</c>, and corroborated twice over.</b> Its ten rider nodes are named
	/// <c>body</c>, <c>body01</c> .. <c>body09</c> and carry ids 1 to 10 - so the name order and the id
	/// order agree, and there are exactly as many as <c>Bouncy.RSE</c> declares bounce slots. They sit
	/// nine to twelve units up in the air above the ride, which is where a bouncing rider belongs.
	/// <para>
	/// <b>Why by name rather than by id.</b> The engine looks a rider's node up by id in the walk space
	/// <c>0x800</c> (<c>FUN_00557ab0</c>, <c>0x00557b3a</c>; docs/exe/ride-operation.md, "Where a rider is
	/// drawn"), where id 1 is only <c>body</c>; <c>air</c>, <c>camera</c> and <c>body11</c> share it under other
	/// flags. The drawing here finds a seat by name, not by <see cref="ModelFile.FindNode"/> (docs/QUEUE.md Q22), and in
	/// this model the names give the same nodes. The names carry no such ambiguity, and <c>body10</c> upwards belong to other groups
	/// and are never reached because the slots stop at ten.
	/// </para>
	/// </remarks>
	internal static string BounceNodeName( int node )
		=> node <= 1 ? "body" : $"body{node - 1:00}";

	/// <summary>
	/// What each placed thing's script is doing, and who it is carrying.
	///
	/// <para>
	/// <b>Four different faults produce one symptom.</b> A rider drawn at the front of the queue is
	/// equally consistent with: a guest being <see cref="PeepState.Riding"/> while holding no bounce slot;
	/// the script never reaching <c>BOUNCE</c>; the park's objects not being reachable from the drawing;
	/// and the node lookup failing on a slot that is properly filled. Each wants a different fix, and
	/// telling them apart is what this census is for.
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

					// A loop and a hold stand alike on some models, the Aztec Mayhem's among them, so the flag
					// is said; and what is queued, which is what a trigger onto a busy channel leaves.
					var loop = (channel.Flags & AnimTimeControl.LoopFlag) != 0 ? " LOOP" : "";
					var next = channel.HasQueued ? $" then {channel.DeferredAnimID}/{channel.DeferredSubAnim}" : "";

					return $"{index}:role {channel.AnimID} entry {channel.SubAnim} "
						+ $"frame {channel.AnimFrame:0.0}/{channel.TotalAnimFrames:0.0}{held}{loop}{next} flags 0x{channel.Flags:x} time {channel.AnimTime}";
				} ) );
			}

			var aboard = script.Bouncing().ToArray();

			// Every head slot in use, and where its node stands in the park's axes as the drawing takes it, so a head
			// can be checked against the picture: node:visitor, "unhung" where the model has no such node.
			var heads = script.Heads().Concat( script.WalkHeads().Select( head => (head.Node, head.Handle, Hung: true) ) ).Select( head =>
			{
				if ( !head.Hung || script.Nodes is not { } nodes )
					return $"{head.Node}:{head.Handle} unhung";

				var end = nodes.FindHead( head.Node, out var world );
				var name = nodes.HeadName( head.Node ) ?? "";

				// Beside it, where the drawn model stands the node of that name at rest, which should agree, and this frame,
				// which the head is drawn at.
				var rest = "not drawn";
				var drawn = "not drawn";

				if ( ParkObjects.Current is { } objects )
				{
					if ( objects.TryNodeOn( thing.ThingId, name, out var at ) )
						rest = $"({at.X:0.0},{at.Y:0.0},{at.Z:0.0})";

					if ( objects.TryDrawnNodeOn( thing.ThingId, nodes.HeadIndex( head.Node ), out var now ) is var how
						&& how != DrawnNode.Missing )
						drawn = $"{how} ({now.X:0.0},{now.Y:0.0},{now.Z:0.0})";
				}

				return $"{head.Node}:{head.Handle} '{name}' {end} ({world.M41:0.0},{world.M43:0.0},{world.M42:0.0}) model {rest} "
					+ $"drawn {drawn}";
			} ).ToArray();

			// Every walk slot in use and, while it is walked or once a walk off is done, its leg, which is the whole of what a walk-on ride's timing
			// is: the one WALKON or WALKOFF worked out from the two nodes.
			var walking = script.Walking().ToArray();

			var walks = walking.Length == 0
				? "nobody"
				: string.Join( ", ", walking.Select( slot =>
					$"{slot.Slot}:{slot.Handle} {slot.State} {slot.From}->{slot.To} leg {slot.Leg?.ToString() ?? "-"} facing {slot.Facing}" ) );

			// And each slot let go and not taken again, with the walk off it keeps.
			var letGo = string.Join( ", ", script.LetGo().Select( slot =>
				$"{slot.Slot}:{slot.From}->{slot.To} leg {slot.Leg} facing {slot.Facing}" ) );

			// And each bounce slot let go and not taken again, with the node and the ride's length it keeps.
			var off = string.Join( ", ", script.BouncedOff().Select( slot => $"{slot.Slot}:{slot.Node} ride {slot.Ride}" ) );

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
				// Its handle, which decides its turns with the scheduler's tick, and where it stands.
				+ $"script {script.Id} at {script.Position} "
				// How long its WAIT has left, in milliseconds of the clock the scripts run on; "none" off a wait.
				+ $"wait {(script.WaitDeadline is { } due ? ((int)(due - (GameClock.Ticks * GameClock.TickSeconds * 1000f))).ToString() : "none")} "
				// The nominee, because a stale one is invisible otherwise: Invite bails while somebody is
				// nominated, and the only thing that clears a stale nomination is DropUnreadyNominee,
				// run on a turn that does not invite. A queue stuck on that would look exactly like a quiet ride.
				+ $"nominee {_behaviour.State.PersonBeingLoaded( thing.ThingId )} "
				// Whether it holds a scream (+0xd0), because the pause holds a scream's VOICE and not the script:
				// a held park stops the VM one instruction short of STOPSCREAM, so "still screaming"
				// and "gone quiet" have to be readable apart. A pure getter, so polling cannot perturb it.
				+ $"running {script.Running} screaming {script.Screaming} "
				// The longest critical section this script has run in one turn, against the cap that ends
				// one where the original would hang.
				+ $"critical {script.LongestCritical} "
				// How often it took a lock on the last unit of its budget, and the longest section it then ran in
				// that same turn: the arrival the budget's charging decides.
				+ $"lastunit {script.LastUnitLocks} ran {script.LongestLastUnitSection} "
				// What ADDOBJ has started and no KILLOBJ stopped, from the head of the list, each as
				// type:effect@tag/node/lookup record, the effect "?" on a record a load put back - records only,
				// since no sound is played from one and a particle is drawn by the park's particle system, not from here (RideEffects).
				+ $"effects [{string.Join( ' ', (script.Effects?.Records ?? []).Select( record => $"{record.Type}:{(record.Restored ? "?" : record.Effect.ToString())}@{record.Tag}/{record.Node}/{record.Index}" ) )}] "
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
				+ $"walking {walking.Length}: {walks} let go: {(letGo.Length == 0 ? "none" : letGo)} "
				+ $"off: {(off.Length == 0 ? "none" : off)} "
				+ $"heads {heads.Length}/{script.HeadSlots}: {(heads.Length == 0 ? "nobody" : string.Join( ", ", heads ))} "
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
	/// is what makes the Drinks Shop and the three toilets reachable. So this prints the
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

			var queue = State.QueueCount( thing.ThingId, StillQueueing );
			var (back, cells) = ParkRideChoice.QueueCellsFor( world, thing );
			var offerable = ParkRideChoice.CanBeOffered( thing, queue, TrackTypeOf( thing ), world );

			yield return $"thing {thing.ThingId,2} cat {thing.CatalogueId} "
				+ $"'{(described ? item.Name : "unknown")}' "
				+ $"price {thing.PricePerUse,3} took {State.TakingsFor( thing.ThingId ),6} costs {State.CostsFor( thing.ThingId ),6} "
				// The record's own count beside the walked one. They agree wherever the save cached a
				// back-of-queue and differ on exactly the objects that made this work necessary.
				+ $"cells {cells} (record {thing.QueueSizeInCells}) back {back} "
				+ $"queue {queue}/{cells * ParkRideChoice.QueueRoomPerCell} "
				// The longest queue a guest joins or stays in (FUN_004dda40); "-" where both gates let everyone through,
				// which for an unknown item or a tier past the third is counted, so not asked here.
				+ $"longest {(thing.HasQueuePath && (!described || thing.UpgradeLevel >= ItemDescriptionFile.Tiers) ? "-" : PeepBehaviour.LongestQueue( thing, described ? item : null ))} "
				+ $"win {(thing.ChanceOfWinning & 0xff)}% "
				+ $"prize {thing.CostOfGoods} "
				// What the score, the arrival's refusal and the settle-up read; a coaster's is counted, so not asked here.
				+ $"excitement {(!described ? "-" : item.TrackType == ItemDescriptionFile.CoasterTrack ? "coaster" : ParkRideScore.ExcitementOf( thing, item, State.TrackRides ))} "
				+ $"OFFERABLE {offerable}";
		}

		yield return $"park balance {State.Balance} gate takings {State.Takings}";
	}

	/// <summary>
	/// The sprite bank a person is drawn from, which a state animation asks for its group. The drawing's own
	/// (<see cref="ParkGuestSprites.BankOf"/>) unless a test hands another.
	/// </summary>
	internal Func<int, SpriteBankFile?> BankOf { get; set; } = static id => ParkGuestSprites.Current?.BankOf( id );

	/// <summary>
	/// A sprite bank by its kind and its number among the kind's, which the park file's writer asks for the frames
	/// a direction of the set a sprite was made on. The drawing's own unless a test hands another.
	/// </summary>
	internal Func<int, int, SpriteBankFile?> BankAt { get; set; } = static ( kind, bank ) => ParkGuestSprites.Current?.BankAt( kind, bank );

	/// <summary>
	/// Hands a queued animation to a person's sprite - <c>FUN_004d4190</c> into <c>FUN_00475b80</c>: a script by
	/// its number, or for 13 to 16 the state group of the sprite's own bank, with that set's frames a direction
	/// (<c>FUN_00540c60</c>). A state the bank has no group for, or whose script is not copied, is counted and
	/// leaves the sprite as it was.
	/// </summary>
	private void StartAnimation( int thingId, SpriteScript playing, int animation )
	{
		var state = SpriteScript.StateOf( animation );

		if ( state < 0 )
		{
			playing.Start( animation );

			return;
		}

		if ( BankOf( thingId ) is { } bank && bank.StateGroups[state] is { InUse: true, Set: > 0 } group
			&& group.Set <= bank.Sets.Length
			&& playing.StartState( group, bank.Sets[group.Set - 1].FramesPerDirection ) )
			return;

		Unimplemented.Report( "SPRITE_STATE_ANIMATION_NOT_STARTED" );
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

	/// <summary>The guests' turn, for a test of what this hands it.</summary>
	internal PeepBehaviour Behaviour => _behaviour;

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
	/// <b>ONE TERM IS NOT REPRODUCED.</b> The original counts a guest only where <c>FUN_004fa990</c> agrees,
	/// and that is a predicate on the CELL the guest stands on: handed the guest (<c>MOV ECX,ESI</c>), it
	/// packs the cell from the thing's own cell bytes and passes when that cell's <c>mType</c> is 0, 1, 3, 9 or 10,
	/// through five one-line helpers (<c>FUN_00536310</c> and its neighbours). Every guest is counted here.
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

		if ( State.QueueRemeasured == QueueRemeasured )
			State.QueueRemeasured = null;

		if ( State.DoorMoved == DoorMoved )
			State.DoorMoved = null;
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

			// What they LOOK like, without which a park where nobody animates reads exactly like one
			// where everybody does.
			var anim = playing == null
				? "none"
				: $"script {playing.Script} pc {playing.Pc} set {playing.Set} "
					+ $"frame {playing.Frame} every {playing.Interval}ms";

			// <b>Where they are HEADING.</b> Without it a park where
			// nobody ever chooses the shop reads exactly like a park where everybody chooses it and
			// something downstream refuses them - and those want opposite fixes. MajorDest is the thing
			// they picked, nought for a guest who has picked nothing.
			// And where they stand in its queue, as the original's walk finds it - which decides who a queue
			// measured shorter puts out (QueueRemeasured).
			var place = ParkRideOperation.IsQueueing( peep )
				? State.PositionInQueue( peep.MajorDest, peep.ThingId, StillQueueing ).ToString()
				: "-";

			yield return $"thing {peep.ThingId,2} kind {peep.PersonType} state {peep.State} "
				// The place they last took, mQueuePos, which their aim was worked out from and the walked place is not.
				+ $"dest {peep.MajorDest,2} place {place,2} recorded {peep.QueuePos & 0xff,2} "
				// The major a minor decision put by, and the walking turns counted toward the next decision.
				+ $"saved-major {peep.SavedMajorDest,2} turns {peep.WalkingTurns,2} "
				// mTimeStartedIdling, which the thinking gap in front of the chooser is measured from, with the other
				// two stamps of the park's clock: mArrivalDate and mTimeOfLastSpotAnim.
				+ $"idle {peep.TimeStartedIdling,4} arrived {peep.ArrivalDate,4} spot {peep.TimeOfLastSpotAnim,4} "
				+ $"(saved {peep.SavedState}) cash {peep.Cash,4} exit {peep.ExitLevel,4} "
				+ $"happy {peep.Happiness,3:0} thirst {peep.Thirst,3:0} hunger {peep.Hunger,3:0} "
				+ $"toilet {peep.Toilet,3:0} vomit {peep.Vomit,3:0} litter {peep.Litter,3:0} prankery {peep.PrankeryIndex} "
				// The two histories the ride score divides down by, newest first.
				+ $"visits [{string.Join( ",", peep.PreviousRides )}] refused [{string.Join( ",", peep.PreviousTemporaryRides )}] "
				// The three speed words, the eased speed and what it gave the walk (Peep.Pace).
				+ $"speed {peep.PurposeSpeed} base {peep.BaseSpeed} adjustor {peep.AdjustorSpeed} "
				+ $"eased {peep.PreviousSpeed} max {nav.MaxSpeed} "
				// Which visitor they are, nought until they are inside; then the visitor window's four counts, and the
				// happiness the settle-up measures a visit against.
				+ $"visitor {peep.VisitorNumber} rides {peep.NumRides} shops {peep.NumShops} sideshows {peep.NumSideshows} won {peep.NumSideshowsWon} "
				+ $"joined {peep.JoinHappiness:0} "
				// What they wear, the sprite kind and bank: 0 a child, 2 a costume.
				+ $"sprite {peep.SpriteKind}/{peep.SpriteBank} "
				// The stranded stamp, the thought last set and the bubble's bank and set, or "-" with none.
				+ $"stranded {peep.StrandedTime} thought 0x{peep.Thoughts.Last:x} "
				+ $"bubble {(peep.Thoughts.Bubble is { } bubble ? $"{bubble.Bank}/{bubble.Set} since {peep.Thoughts.TimeBubbleShown}" : "-")} "
				// The balloon's life, its colour and its picture's turn, or "-" for none held.
				+ $"balloon {peep.BalloonLife} {(peep.Balloon is { } held ? $"set {held.Sprite.Set} frame {held.Sprite.Frame} shown {held.Sprite.Shown}" : "-")} "
				// Where they ARE, without which a person whose needs change and whose position does not
				// reads the same as one who moves.
				+ $"at ({nav.Position.X / (float)FixedVector.One:0.000},"
				+ $"{nav.Position.Y / (float)FixedVector.One:0.000}) "
				// And where they are aimed, the exact point a route's last leg closes on - a place in a queue is one.
				+ $"aim ({nav.Target.X / (float)FixedVector.One:0.000},"
				+ $"{nav.Target.Y / (float)FixedVector.One:0.000}) "
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
	/// only and staff are a separate list, so a park where no member of staff ever moves reads, from
	/// there, exactly like one where they all do.
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

			yield return $"thing {member.ThingId,2} model {member.Model} \"{member.Name}\" {member.Activity} "
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
				+ $"st 0x{(int)member.Activity:x} s188 {(member.SettingOffForTheStrike ? 1 : 0)} "
				+ $"cell ({nav.Position.Cell.X},{nav.Position.Cell.Y}) heading {walk?.Heading ?? -1} "
				+ $"has {(walk == null ? "no-walk" : walk.HasRoute ? "route" : "no-route")} "
				+ $"loo {member.ToiletToClean} cleaningSince {member.TimeStartedCleaning} "
				+ $"repair {member.ObjectToRepair} repairLeft {member.DurationOfRepair} "
				+ $"performingSince {member.TimeStartedEntertaining} "
				+ $"researchingSince {member.TimeStartedResearching} "
				+ $"hired {(ParkWorld.StaffState.HiredWhenOf( member.TimeHired ) is { } hired ? hired.ToString( "d/M/yyyy HH:mm:ss" ) : $"0x{member.TimeHired:x}")} "
				+ $"sounds {member.Sounds.Played} draws {member.Sounds.Draws} "
				+ $"base {member.BaseSpeed} purpose {member.PurposeSpeed} adjustor {member.AdjustorSpeed} "
				+ $"pace {member.PreviousSpeed:0.0000} speed {nav.MaxSpeed} restExact {member.Tiredness:0.000} "
				// The cell recorded for them, where an entertainer's or a guard's region effect stands.
				+ $"recorded {(MapStep.IsCellId( member.RecordedCell ) ? $"({MapStep.CellAt( member.RecordedCell ).X},{MapStep.CellAt( member.RecordedCell ).Y})" : "none")} "
				+ $"through {member.PercentageThroughGrade}";
		}
	}
}
