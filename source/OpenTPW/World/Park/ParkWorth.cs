namespace OpenTPW;

/// <summary>
/// What the park is worth, one whole number - the original's <c>FUN_004c8240</c>, and what its two readers make of
/// it: the size of a load of arrivals (<c>FUN_004cf5b0</c>) and a guest's idea of a fair fee at the gate
/// (<c>FUN_004ff5b0</c>, <see cref="ParkAdmission.IdealPrice"/>). <c>docs/exe/park.md</c>, "The headcount score".
///
/// <para>
/// <b>Nothing keeps it.</b> Each reader takes the sum afresh, so a ride shut, not loading or with a full queue on
/// the sweep it is asked is worth nought on that sweep.
/// </para>
/// </summary>
public static class ParkWorth
{
	/// <summary>A funny day in the 100-nanosecond units <c>FUN_004f88b0</c> answers in - the divisor <c>0xc92a69c000</c>.</summary>
	private const long OneDay = 864_000_000_000L;

	/// <summary>The load's factor with no drops falling, the float at <c>0x00700364</c>.</summary>
	public const float FairWeatherFactor = 1.2f;

	/// <summary>The load's factor while drops fall, the float at <c>0x00700368</c>.</summary>
	public const float RainFactor = 0.8f;

	/// <summary>
	/// The sum over the object chain. An object adds only when its item is a ride or a sideshow
	/// (<c>WhichUIType</c> 0 or 2), it passes the offer gate (<see cref="ParkRideChoice.CanBeOffered"/>) and its
	/// item's record counts at least one standing (<see cref="ParkState.BuiltOf"/>). What it adds is
	/// <c>( AttractionValue + bonus ) / n</c>, a signed whole division: for an item on no track <c>n</c> is the
	/// item's standing count and the age runs from the item's first build; a track ride adds its whole value and is
	/// aged by its own purchase (<see cref="ParkState.AgeInDays(ParkWorld.CatalogueObject)"/>).
	/// </summary>
	/// <param name="queueLength">How many queue for a thing, as the offer gate counts them.</param>
	public static int Of( ParkState state, IParkInitialState? park, ParkItemCatalogue? catalogue,
		Func<ParkWorld.CatalogueObject, int> queueLength )
	{
		if ( catalogue == null )
			return 0;

		var worth = 0;

		foreach ( var thing in state.ObjectsInChainOrder() )
		{
			if ( !catalogue.TryGet( thing.CatalogueId, out var item ) )
				continue;

			if ( item.UiType is not (ItemDescriptionFile.Ride or ItemDescriptionFile.SideShow) )
				continue;

			if ( !ParkRideChoice.CanBeOffered( thing, queueLength( thing ), item.TrackType, park ) )
				continue;

			var (standing, firstBuilt) = state.BuiltOf( thing.CatalogueId );

			if ( standing <= 0 )
				continue;

			var (share, days) = item.TrackType == 0
				? (standing, DaysSince( state.GameTick, firstBuilt, GameCalendar.Rate ))
				: (1, state.AgeInDays( thing ));

			worth += (item.AttractionValue + NewBonus( item, days )) / share;
		}

		return worth;
	}

	/// <summary>
	/// Whole days since a stamp on the park's clock - <c>FUN_004f88b0</c> over a day (<c>0x004c836d</c>): the ticks
	/// since, taken unsigned, times the calendar's rate, over four, as funny seconds. 23.04 sweeps at 15,000.
	/// </summary>
	public static int DaysSince( int gameTick, uint stamp, int rate )
	{
		unchecked
		{
			var ticks = (long)(uint)(gameTick - (int)stamp);
			var hundredNanoseconds = ticks * rate / 4 * 10_000_000L;

			return (int)(hundredNanoseconds / OneDay);
		}
	}

	/// <summary>
	/// What being new adds at an age: <c>Attraction[ days / NewAttractionDecayTime ].NewBonus</c> while that step
	/// is 0, 1 or 2, nought before and past them (<c>0x004c83b5</c>, signed). The item files' bound keeps the decay
	/// time at 1 or more; an item made with none is divided by 1.
	/// </summary>
	public static int NewBonus( ParkItemCatalogue.Item item, int days )
	{
		var step = days / Math.Max( 1, item.NewAttractionDecayTime );

		return step is >= 0 and < ItemDescriptionFile.NewBonusSteps ? item.NewBonusAt( step ) : 0;
	}

	/// <summary>
	/// How many a load brings - <c>FUN_004cf5b0</c>'s arithmetic (<c>0x004cf5dd</c>..<c>0x004cf648</c>):
	/// <c>Arrival.NewParkBonus</c> plus the worth, times <see cref="RainFactor"/> or
	/// <see cref="FairWeatherFactor"/> as a float and cut to a whole number, over <c>Arrival.PointsPerVisitor</c>
	/// held to 1 or more, and never under <c>Arrival.MinPeople</c>.
	/// </summary>
	public static int LoadSize( int worth, bool raining, int newParkBonus, int pointsPerVisitor, int minPeople )
	{
		var points = (int)((double)(newParkBonus + worth) * (raining ? RainFactor : FairWeatherFactor));

		return Math.Max( minPeople, points / Math.Max( 1, pointsPerVisitor ) );
	}
}
