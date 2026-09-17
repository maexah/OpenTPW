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
			var saved = park.People.ToDictionary( person => person.ThingId, person => person.Angle );

			foreach ( var peep in _peeps )
			{
				// The heading is seeded from the file rather than left at zero: a guest who has not taken a
				// step yet faces the way they were saved facing, and only a step they actually take turns
				// them. Starting everyone at zero would swing the whole park round on the first frame.
				_walks[peep.ThingId] = new PeepWalk( peep.Navigator, blocked )
				{
					Heading = saved.GetValueOrDefault( peep.ThingId )
				};
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
	/// One turn of every guest for each 31ms that has come due, which is where the original runs the
	/// thing engine: its per-tick call walks the thing list and gives each guest its needs tick.
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

			foreach ( var peep in _peeps )
			{
				peep.Tick( tick );

				// Every tick, and not one in four: the share gates the needs alone, and walking is a
				// separate call the original never gates. See Peep.TickShare, which used to say otherwise.
				if ( Peep.IsAWalkingState( peep.State ) && _walks.TryGetValue( peep.ThingId, out var walk ) )
					WalkOn( peep, walk );
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
	private static void WalkOn( Peep peep, PeepWalk walk )
	{
		if ( !walk.HasRoute && (peep.Navigator.CannotReach || !walk.PlanRoute()) )
			return;

		walk.Step();
	}

	/// <summary>This guest's walk, for the tests and the debug console.</summary>
	internal PeepWalk? WalkFor( int thingId ) => _walks.GetValueOrDefault( thingId );

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
			yield return $"thing {peep.ThingId,2} kind {peep.PersonType} state {peep.State} "
				+ $"(saved {peep.SavedState}) cash {peep.Cash,4} exit {peep.ExitLevel,4} "
				+ $"happy {peep.Happiness,3:0} thirst {peep.Thirst,3:0} hunger {peep.Hunger,3:0} "
				+ $"toilet {peep.Toilet,3:0} ill {peep.Illness,3:0} litter {peep.Litter,3:0} "
				+ $"speed {peep.PurposeSpeed}";
		}
	}
}
