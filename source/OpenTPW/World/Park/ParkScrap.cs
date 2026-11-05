namespace OpenTPW;

/// <summary>
/// What a scrapped object is worth, as a percentage of its price - the original's <c>FUN_004e2290</c>
/// (<c>docs/exe/park-engine.md</c>, "DEMOLISH refunds"). The queue's clear reads it for the ride a cell belongs to
/// (<see cref="ParkPathBuilding.QueueRefund"/>).
/// </summary>
public static class ParkScrap
{
	/// <summary>A year in the 100-nanosecond units the age is taken in - the divisor <c>0x11ed178c6c000</c> (<c>0x004e22ce</c>).</summary>
	private const long OneYear = 315_360_000_000_000L;

	/// <summary>Thirty days in the same units - the divisor <c>0x1792f8648000</c> (<c>0x004e237b</c>).</summary>
	private const long ThirtyDays = 25_920_000_000_000L;

	/// <summary>
	/// The percentage for an object of the given item, upgrade tier and stamp, <paramref name="customers"/> being its
	/// <c>mNumCustomers</c> (<c>+0x1a0</c>). <b>A literal 100 only</b> while the age is under thirty days, no customer
	/// has come (<c>0x004e2390</c>) and the item's tier-nought first-year figure is above nought (<c>0x004e23b2</c>); else
	/// the tier's figure for the year the age falls in. The age is the signed 64-bit difference divided toward nought, as
	/// the original's <c>__alldiv</c> takes it, so a stamp in the future is year 0 up to a year ahead, and new (under thirty days) only up to thirty days ahead, and
	/// a later one falls past year 2 into the fourth year's figure. A stamp that makes no date reads as age nought, as
	/// <see cref="ParkState.AgeInDays(DateTime, ParkWorld.BuiltWhen)"/> does.
	/// </summary>
	public static int Percent( DateTime now, ParkWorld.BuiltWhen built, int customers, int tier, ParkItemCatalogue.Item item )
	{
		var age = built.ToDateTime() is { } when ? (now - when).Ticks : 0L;
		var year = age / OneYear;

		if ( year == 0 )
		{
			if ( age / ThirtyDays == 0 && customers == 0 && item.ScrapValueAt( 0, 1 ) > 0 )
				return 100;

			return item.ScrapValueAt( tier, 1 );
		}

		return item.ScrapValueAt( tier, year == 1 ? 2 : year == 2 ? 3 : 4 );
	}
}
