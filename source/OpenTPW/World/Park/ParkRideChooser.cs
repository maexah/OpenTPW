namespace OpenTPW;

/// <summary>
/// Which of a park's objects a guest actually sets off for - the original's <c>FUN_004fcb10</c>, which
/// walks the world's object list, asks <see cref="ParkRideChoice"/> whether each candidate may be offered,
/// scores the survivors with <see cref="ParkRideScore"/> and takes the best it can route the guest to.
///
/// <para>
/// <b>The list is walked the original's way</b>, from <c>mFirstObject</c> along each object's own
/// <c>mNext</c>, rather than in whatever order the reader happens to hold them. That matters here in a
/// way it does not in <see cref="ParkRideChoice.Offerable"/>: two candidates can tie, and which one a tie
/// goes to depends on where each sits in the walk.
/// </para>
/// <para>
/// <b>The chain it walks is the RUNNING park's, not the file's.</b> The original's is live - the object
/// constructor links a newly built thing in at the head (<c>FUN_00519d80</c>) and the demolish unlinks it
/// (<c>FUN_00519dc0</c>), one call site apiece - so something bought this session is considered, and
/// considered first. Given no <see cref="ParkState"/> this falls back to the save's own chain, which
/// reaches only what the file placed and is what a test holding a bare <see cref="ParkWorld"/> means.
/// </para>
/// <para>
/// <b>Nothing is chosen unless it beats nine.</b> The original compares each score against 9 and keeps
/// only what is strictly greater, so a park full of poor candidates leaves a guest standing - which is a
/// real outcome rather than a failure, and is what a guest already does here when no arm fires.
/// </para>
/// <para>
/// <b>What it hands the score is the guest's and the park's, gathered here</b>: the kind of the thing the guest
/// left last, looked up in the running park; each candidate's age on the park's calendar
/// (<see cref="ParkState.AgeInDays"/>); its walked queue cells; and its excitement
/// (<see cref="ParkRideScore.ExcitementOf"/>).
/// </para>
/// </summary>
public sealed class ParkRideChooser
{
	private readonly IParkInitialState? _park;
	private readonly ParkItemCatalogue? _catalogue;
	private readonly ParkState? _state;

	/// <param name="park">
	/// The park being chosen from. Null chooses nothing, which is what a guest in a park with no save
	/// behind them should do.
	/// </param>
	/// <param name="catalogue">
	/// What each object actually is - its excitement, what it does for thirst and hunger, whether it is
	/// shelter. <b>Null leaves every candidate scoring as a bare object</b> rather than guessing at those,
	/// which costs the thirst, hunger, relief and shelter terms and keeps the distance and queue ones.
	/// </param>
	/// <param name="state">
	/// The park as it is being played, whose object chain is the live one. Null falls back to the save's
	/// chain - see the class remarks.
	/// </param>
	public ParkRideChooser( IParkInitialState? park, ParkItemCatalogue? catalogue = null, ParkRideScore? score = null,
		ParkState? state = null )
	{
		_park = park;
		_catalogue = catalogue;
		_state = state;
		Score = score ?? new ParkRideScore();
	}

	/// <summary>
	/// The candidates in the order the original considers them. The running park's chain where there is
	/// one, and the save's own where there is not.
	/// </summary>
	private IEnumerable<ParkWorld.CatalogueObject> Candidates()
	{
		if ( _state != null )
			return _state.ObjectsInChainOrder();

		return FileChainOrder();
	}

	/// <summary>The save's chain, walked from its header's <c>mFirstObject</c>.</summary>
	private IEnumerable<ParkWorld.CatalogueObject> FileChainOrder()
	{
		if ( _park == null )
			yield break;

		var byId = new Dictionary<int, ParkWorld.CatalogueObject>();

		foreach ( var thing in _park.Objects )
			byId[thing.ThingId] = thing;

		var seen = 0;

		for ( var id = _park.FirstObject; id != 0 && seen <= byId.Count; ++seen )
		{
			if ( !byId.TryGetValue( id, out var candidate ) )
				break;

			yield return candidate;

			id = candidate.NextObject;
		}
	}

	/// <summary>The scorer this chooses with - <c>FUN_004fcc30</c>.</summary>
	public ParkRideScore Score { get; }

	/// <summary>
	/// What a candidate has to beat to be worth walking to. The original tests <c>&gt; 9</c>, so nine
	/// itself is not enough.
	/// </summary>
	public const int WorthGoingTo = 9;

	/// <summary>
	/// The best thing this guest could set off for, or null if nothing in the park is worth it.
	/// </summary>
	/// <param name="gameTick">
	/// The count whose <b>bottom bit alone</b> settles a tie - see <see cref="Beats"/>: the original's <c>mGameTick</c>,
	/// where <see cref="PeepBehaviour"/> hands in the thing tick (Q132).
	/// </param>
	/// <param name="queueLength">
	/// How long each object's queue is. <b>Null walks the save's queue</b> - from the object's
	/// <c>mFirstInQ</c> along each guest's own <c>mQNext</c> - which comes to nought in the shipped park,
	/// because nobody has ever been admitted to it. A running park hands in
	/// <see cref="ParkState.QueueCount"/>, <c>FUN_004ddf50( 0 )</c>.
	/// </param>
	/// <param name="now">
	/// Now on the park's calendar, <see cref="ParkState.CalendarNow"/>, which each thing's age is measured against.
	/// Null treats everything as no longer new.
	/// </param>
	/// <param name="raining">Whether drops are falling, which multiplies what is indoors.</param>
	/// <param name="route">
	/// Routes the guest to a candidate's back of queue and answers whether a route was found - the original's
	/// <c>FUN_004fa530</c>, called inside the walk for every candidate that beats the best so far
	/// (<c>0x004fcbcf</c>). A candidate that does not route is not kept, and the best is not raised to its score, so a
	/// later one need only beat the last that did route. <b>Each call rewrites the guest's walker</b>, so the walker
	/// is left as the last call left it, whichever candidate is answered. Null asks nothing, and answers the best
	/// candidate with no walker touched.
	/// </param>
	/// <param name="scored">Told each offered candidate's score as it is taken, for a census. Null tells nobody.</param>
	public ParkWorld.CatalogueObject? ChooseFor( ParkRideScore.Wants wants, int fromX, int fromY, int gameTick,
		Func<ParkWorld.CatalogueObject, int>? queueLength = null, DateTime? now = null, bool raining = false,
		Func<ParkWorld.CatalogueObject, int, bool>? route = null, Action<ParkWorld.CatalogueObject, int>? scored = null )
	{
		if ( _park == null )
			return null;

		ParkWorld.CatalogueObject? best = null;
		var bestScore = WorthGoingTo;

		wants = wants with { LastVisitKind = KindOf( wants.Visits is { Count: > 0 } visits ? visits[0] : 0 ) };

		foreach ( var candidate in Candidates() )
		{
			var item = ItemFor( candidate );
			var queue = queueLength?.Invoke( candidate ) ?? ParkRideChoice.QueueLength( _park, candidate );

			if ( !ParkRideChoice.CanBeOffered( candidate, queue, item?.TrackType ?? 0, _park ) )
				continue;

			var score = ScoreOf( wants, candidate, item, queue, fromX, fromY, now, raining );

			scored?.Invoke( candidate, score );

			if ( !Beats( score, bestScore, best, gameTick ) )
				continue;

			if ( route != null && !route( candidate, score ) )
				continue;

			bestScore = score;
			best = candidate;
		}

		return best;
	}

	/// <summary>
	/// The nearer thing the walk's minor decision takes, with its score, or null for none - the window and the pick
	/// of <c>FUN_004fd570</c> (<c>docs/exe/ride-operation.md</c>, "A second toilet"); the switch test and the switch
	/// are <see cref="PeepBehaviour"/>'s.
	/// </summary>
	/// <remarks>
	/// The window is the guest's cell less two to plus one, x outer and y inner, on the map; a cell counts only if it
	/// is strictly nearer the major's back cell, squared, than the guest's own (<c>0x004fd660</c>..<c>0x004fd7a4</c>).
	/// An object is found on the cell it stands on (its <c>mWho</c> list, which it is linked on at construction and
	/// never moved from), not on its footprint or entry. Each that is not the major passes the offer gate and is then
	/// scored, the same two calls the chooser makes (<c>0x004fd73f</c>, <c>0x004fd74b</c>).
	/// <para>
	/// <b>There is no floor, and a first candidate at nought wins on an odd tick</b>: the best starts at nought with
	/// nothing held, a higher score takes it, and an equal one takes it when the tick's bottom bit is set
	/// (<c>0x004fd750</c>..<c>0x004fd76d</c>) - which is why this does not share <see cref="Beats"/>. So right after a
	/// toilet, when every toilet scores nought, one is still taken on an odd tick.
	/// </para>
	/// <para>
	/// <b>One difference.</b> Within one cell the original walks its list newest-linked first where this takes the
	/// park's own order, which cannot differ while no two objects stand on one cell, as none do in Lost Kingdom.
	/// </para>
	/// </remarks>
	/// <param name="majorBack">The back-of-queue cell of the thing the guest is bound for.</param>
	public (ParkWorld.CatalogueObject Thing, int Score)? MinorDecisionFor( ParkRideScore.Wants wants, int x, int y,
		int majorId, (int X, int Y) majorBack, int gameTick, Func<ParkWorld.CatalogueObject, int>? queueLength = null,
		DateTime? now = null, bool raining = false )
	{
		if ( _park == null )
			return null;

		wants = wants with { LastVisitKind = KindOf( wants.Visits is { Count: > 0 } visits ? visits[0] : 0 ) };

		var guestNearness = SquaredDistance( x, y, majorBack );
		ParkWorld.CatalogueObject? best = null;
		var bestScore = 0;

		for ( var cellX = x - 2; cellX <= x + 1; ++cellX )
		{
			for ( var cellY = y - 2; cellY <= y + 1; ++cellY )
			{
				if ( !ParkState.OnMap( cellX, cellY ) || SquaredDistance( cellX, cellY, majorBack ) >= guestNearness )
					continue;

				foreach ( var candidate in _state?.Objects ?? _park.Objects )
				{
					if ( candidate.CellX != cellX || candidate.CellY != cellY || candidate.ThingId == majorId )
						continue;

					var item = ItemFor( candidate );
					var queue = queueLength?.Invoke( candidate ) ?? ParkRideChoice.QueueLength( _park, candidate );

					if ( !ParkRideChoice.CanBeOffered( candidate, queue, item?.TrackType ?? 0, _park ) )
						continue;

					var score = ScoreOf( wants, candidate, item, queue, x, y, now, raining );

					if ( score > bestScore || (score == bestScore && (gameTick & 1) != 0) )
					{
						best = candidate;
						bestScore = score;
					}
				}
			}
		}

		return best is { } taken ? (taken, bestScore) : null;
	}

	private static int SquaredDistance( int x, int y, (int X, int Y) to )
		=> ((x - to.X) * (x - to.X)) + ((y - to.Y) * (y - to.Y));

	/// <summary>
	/// Whether this candidate takes the lead from the one already held.
	/// </summary>
	/// <remarks>
	/// <b>A tie is broken on the clock's bottom bit, and it is a real rule rather than a tidy-up.</b> The
	/// original keeps the later candidate on a tie only when <c>mGameTick &amp; 1</c> is set, so two
	/// equally good things alternate between ticks instead of the earlier one in the walk always winning.
	/// The first candidate to clear the bar is a different case: there is nothing to tie with, so it is
	/// taken on the strict comparison alone.
	/// </remarks>
	private static bool Beats( int score, int bestScore, ParkWorld.CatalogueObject? best, int gameTick )
	{
		if ( score > bestScore )
			return true;

		return best != null && score == bestScore && (gameTick & 1) != 0;
	}

	/// <summary>
	/// The catalogue id of the thing this handle names in the running park, or nought for none - the thing table
	/// lookup <c>FUN_004fcc30</c> makes of <c>mPreviousRides[0]</c> (<c>0x004fcd79</c>).
	/// </summary>
	private int KindOf( int thingId )
	{
		if ( thingId == 0 )
			return 0;

		foreach ( var thing in _state?.Objects ?? _park!.Objects )
		{
			if ( thing.ThingId == thingId )
				return thing.CatalogueId;
		}

		return 0;
	}

	/// <summary>What the catalogue says this object is, or null where nothing can say.</summary>
	private ParkItemCatalogue.Item? ItemFor( ParkWorld.CatalogueObject placed )
		=> _catalogue != null && _catalogue.TryGet( placed.CatalogueId, out var item ) ? item : null;

	/// <summary>
	/// One candidate's score, with the facts that live outside its own record gathered first.
	/// </summary>
	/// <remarks>
	/// <b>The distance, the queue term's nearness test and the nearby effects are all read at the back-of-queue
	/// cell</b>, as <c>FUN_004fcc30</c> reads them: it asks <c>GetBackOfQueue</c> of the object
	/// (<c>0x004fcc7d</c>), unpacks that cell and measures the guest's own cell against it, then asks the effects
	/// record of the same cell (<c>0x004fce04</c>). It is also where a chosen guest is sent
	/// (<c>PeepBehaviour.ChooseSomewhereToGo</c>). <c>docs/exe/ride-operation.md</c>, "What a thing is worth to a
	/// guest", step 2.
	/// <para>
	/// A candidate with no back of queue is never scored: both callers ask
	/// <see cref="ParkRideChoice.CanBeOffered"/> first, which refuses it.
	/// </para>
	/// </remarks>
	private int ScoreOf( ParkRideScore.Wants wants, ParkWorld.CatalogueObject candidate,
		ParkItemCatalogue.Item? item, int queue, int fromX, int fromY, DateTime? now, bool raining )
	{
		// GetBackOfQueue's pair: the back cell, and the walked count, the object's +0x40 (0x004fce40).
		var (backOfQueue, cells) = ParkRideChoice.QueueCellsFor( _park, candidate );
		var (backX, backY) = MapStep.CellAt( backOfQueue );

		var acrossBy = fromX - backX;
		var downBy = fromY - backY;
		var distanceSquared = (acrossBy * acrossBy) + (downBy * downBy);

		// The back cell's own effects count, which divides the distance term.
		var effects = 0;

		if ( ParkState.OnMap( backX, backY ) )
			effects = _park!.CellAt( backX, backY ).NearbyEffects;

		var age = AgeOf( now, candidate.Built );

		var described = item ?? Undescribed;
		var excitement = item is { } known ? ParkRideScore.ExcitementOf( candidate, known, _state?.TrackRides ) : 0;

		return Score.Of( wants,
			new ParkRideScore.Candidate( candidate, described, distanceSquared, queue, effects,
				age, raining, excitement, cells ) );
	}

	/// <summary>
	/// A thing's age for the score: <see cref="ParkState.AgeInDays"/> on the calendar handed in, or
	/// <see cref="NotNew"/> with no calendar or no stamp that makes a date. No save or purchase the game makes holds
	/// such a stamp; the original's loader keeps whatever <c>+0x18</c> held before when a stamp will not convert
	/// (<c>FUN_005fc5c0</c>), which is not traced, so not new is this project's choice.
	/// </summary>
	public static int AgeOf( DateTime? now, ParkWorld.BuiltWhen built )
		=> now is { } today && built.ToDateTime() is not null ? ParkState.AgeInDays( today, built ) : NotNew;

	/// <summary>
	/// The age a thing is treated as when nothing can date it - past <see cref="ParkRideScore.NewForDays"/> by
	/// enough that no balance file could move the boundary over it.
	/// </summary>
	public const int NotNew = 10000;

	/// <summary>
	/// What an object the catalogue cannot describe is scored as: nothing declared, so the excitement
	/// weight falls away and the thirst, hunger, relief and shelter terms are all nought. The distance and
	/// queue terms still apply, which is what keeps a park with no catalogue choosing the nearest open
	/// thing rather than nothing at all.
	/// </summary>
	private static readonly ParkItemCatalogue.Item Undescribed =
		new( 0, "", "", "", 0, 0, null );
}
