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
/// amounts to. Of the twenty-two, five are built: a guest reaching the gate judges the admission fee, a
/// guest coming through it is counted as a visitor and goes on to decide, and the rest stop where the
/// original's own handler would need something this project has not read yet.
/// <para>
/// <b>What they still do not do is choose.</b> Deciding, judging the fee and waiting for the gate are the
/// three handlers a guest can now reach and none is built, so the park fills up with guests who have
/// arrived somewhere sensible and stay there. Bringing them up one at a time is how the ride VM was done,
/// and it is why this can be trusted at each step rather than all at once at the end.
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

		// Indexed once here rather than on demand: the guests are fixed for the life of the park - nothing
		// yet adds or removes one - so this cannot fall out of step with the list it is built from.
		foreach ( var peep in _peeps )
			_byId[peep.ThingId] = peep;

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
			var blocked = CellEdge.For( park, WalkingMode ).Blocked;
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
					_sprites[peep.ThingId] = new SpriteScript(
						picture.Script, picture.Pc, picture.SpriteNumber, picture.Frame );
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
					_sprites[member.ThingId] = new SpriteScript(
						picture.Script, picture.Pc, picture.SpriteNumber, picture.Frame );
				}
			}
		}

		Log.Info( $"People: {_peeps.Count} guests and {_staff.Count} staff simulating" );
	}

	/// <summary>
	/// Every guest the save named, as a running copy. Staff are left out: they have a block of their own
	/// that nothing reads yet, and five state machines of their own that nothing runs.
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
					operation.Invite( script, thing, TrackTypeOf( thing ) );

					if ( script != null && script[ParkRideOperation.BrokenVariable] == 0 )
						operation.Dismiss( script, thing, thingTick, _rideRandom, WalkFor );

					break;

				// Closing or broken: finish whoever was mid-admission, then let them off.
				case ParkRideChoice.StateRefusedOne:
				case 2:
				case ParkRideChoice.StateRefusedFour:
					operation.CompleteAdmission( script, thing.ThingId, thingTick, _rideRandom );
					operation.Dismiss( script, thing, thingTick, _rideRandom, WalkFor );
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

	/// <summary>This guest's walk, for the tests and the debug console.</summary>
	internal PeepWalk? WalkFor( int thingId ) => _walks.GetValueOrDefault( thingId );

	/// <summary>This staff member's walk, for the same.</summary>
	internal PeepWalk? StaffWalkFor( int thingId ) => _staffWalks.GetValueOrDefault( thingId );

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
}
