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
/// <b>Guests walk. They do not yet choose, queue or leave.</b> The needs loop is the first of the
/// original's two per-guest calls and the twenty-two state behaviours are the second; of those
/// twenty-two, what is built is the walking that eleven of them do - see <see cref="PeepWalk"/>. So a guest
/// goes to where the save was sending them and stops there, because deciding what to do on arrival is the
/// part of the state machine that does not exist yet. Bringing them up one at a time is how the ride VM
/// was done, and it is why this can be trusted at each step rather than all at once at the end.
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

	private readonly Dictionary<int, PeepWalk> _walks = [];

	private readonly Dictionary<int, SpriteScript> _sprites = [];

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

	public ParkPeople( ParkWorld? park )
	{
		_peeps = PeepsIn( park );

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
		}

		Log.Info( $"People: {_peeps.Count} guests simulating" );
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

	/// <summary>Every guest, in the order the save lists them.</summary>
	internal IReadOnlyList<Peep> Peeps => _peeps;

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

				// Every thing tick, and not one in four: the share gates the needs alone, and walking is
				// a separate call the original never gates.
				if ( Peep.IsAWalkingState( peep.State ) && _walks.TryGetValue( peep.ThingId, out var walk ) )
					WalkOn( peep, walk, playing );

				// FUN_004d4190, whose only caller is the per-guest needs call - so what the walk asked for
				// lands on that guest's own turn in four rather than at once. Which side of the walk it
				// sits on is not established, and cannot matter here: the walk asks for an animation only
				// when the sprite is not already playing it, so a turn either way changes nothing after
				// the first.
				if ( playing != null && peep.DueOn( thingTick ) )
					Apply( peep, playing );
			}
		}
	}

	/// <summary>
	/// One turn of walking for a guest who is going somewhere, giving them a route first if they have none.
	///
	/// <para>
	/// <b>The planning is a departure and it is named.</b> In the original a route is set by the state
	/// machine on the way into a walking state, through <c>FUN_00510100</c> - three times from
	/// <c>FUN_004f9490</c> and once each from <c>FUN_004fa530</c> and <c>FUN_004fa5f0</c>. Those behaviours
	/// are not built, and a guest restored from a file carries a destination and no route at all, the route
	/// being the one part of it the save deliberately does not keep. So the first tick of walking is what
	/// asks for one. A guest who has given up is not asked again, because nothing about them has changed
	/// since they did.
	/// </para>
	/// </summary>
	private static void WalkOn( Peep peep, PeepWalk walk, SpriteScript? playing )
	{
		if ( !walk.HasRoute && (peep.Navigator.CannotReach || !walk.PlanRoute()) )
			return;

		var verdict = walk.Step();

		if ( verdict == WalkVerdict.Walking )
		{
			var hurrying = peep.PurposeSpeed > Peep.UnhurriedSpeed;

			// FUN_004fa2a0 asks FUN_00475c50 whether the sprite is ALREADY on the walk before asking for
			// it, which is the whole reason a jump must not change a script's identity: without that test a
			// walking guest would be restarted at the first picture on every single tick.
			var wanted = hurrying ? (int)PeepAnimation.HurriedWalk : (int)PeepAnimation.Walk;

			if ( playing != null && !playing.IsOn( wanted ) )
				peep.NextAnimation = wanted;

			peep.NextInterval = SpriteScript.IntervalFor( walk.LastStep.X, walk.LastStep.Y, hurrying );

			return;
		}

		// <b>A DEPARTURE, and the same one this method already makes above.</b> The original does not stop
		// the walking animation here - its state machine does, by moving the guest into a state that asks
		// for the standing one, and those twenty-two behaviours are not built. Without this a guest who
		// reached the gate would stride on the spot for ever, which is worse to look at than the frozen
		// pose they had before any of this existed. The route planning a few lines up is licensed by
		// exactly this argument, and for exactly this reason.
		if ( verdict == WalkVerdict.Arrived && playing != null && !playing.IsOn( (int)PeepAnimation.Stand ) )
			peep.NextAnimation = (int)PeepAnimation.Stand;
	}

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

	/// <summary>This guest's animation, for the drawing, the tests and the debug console.</summary>
	internal SpriteScript? SpriteFor( int thingId ) => _sprites.GetValueOrDefault( thingId );

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
				+ $"toilet {peep.Toilet,3:0} ill {peep.Illness,3:0} litter {peep.Litter,3:0} "
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
