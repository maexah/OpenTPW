namespace OpenTPW;

/// <summary>
/// The rows of a ride window's Buy Upgrades list and what a choice of one does - <c>FUN_004ae140</c> fills it and the
/// handler's <c>0x400</c> arm (<c>0x004ae430</c>) hands the row's value to <c>FUN_004af480</c>
/// (<c>docs/exe/ride-operation.md</c>, "The ride window's Buy Upgrades list"). The window that shows it is
/// <c>UI.ParkUpgradesWindow</c>; this is the part that reads the park.
/// </summary>
public static class ParkUpgradeList
{
	/// <summary>The cancel row's value (<c>0x004ae2bd</c>).</summary>
	public const int CancelValue = -12999;

	/// <summary>A message row's value, which a choice ignores (<c>0x004ae3fa</c>).</summary>
	public const int MessageValue = -7890;

	/// <summary>The level a ride is offered no further upgrade at: the row is offered only below it (<c>0x004ae2d4</c>).</summary>
	public const int TopLevel = 2;

	/// <summary>What a row is: an add-on to buy, the next level to order, the cancel of a waiting order, or a message.</summary>
	public enum Kind { AddOn, Level, Cancel, Message }

	/// <summary>
	/// One row: what it is, the value the list hands the choice, the UITEXT row it is lettered from (nought for an
	/// add-on, which takes its item's name) and the number that row is filled with, and the price cell's number.
	/// </summary>
	public readonly record struct Row( Kind Kind, int Value, int Text, int Shown, int Price );

	/// <summary>
	/// The list's rows for <paramref name="ride"/>, in order (<c>FUN_004ae140</c>). In Instant Action there are none
	/// but the message (<c>0x004ae1aa</c>). Otherwise every item whose <c>AddOn.UpgradesId</c> (<c>+0x284</c>) is the
	/// ride's item and whose control record is researched (<c>+0x10</c>) is a row, priced at the record's <c>+0x04</c>
	/// (<c>0x004ae230</c>); then, while an upgrade waits (<c>mState</c> 2, <c>FUN_004e03e0</c>), the cancel, UITEXT 30
	/// with the level + 2; otherwise, below level 2 with level + 1 researched (the record's <c>+0x14</c>), UITEXT 29
	/// with the level + 2 at <c>CostOfUpgrade[level + 1]</c>. A list with no row says why: UITEXT 27 in Instant
	/// Action, 28 at level 2, else 26.
	/// </summary>
	/// <remarks>
	/// The original also skips an item whose model failed to load (<c>+0x508</c>); every catalogued item here has one
	/// to load, as the buy list's filter assumes (Q221). The message is one row here; the window splits it into a row a
	/// line, as the original does (<c>0x0067c430</c>).
	/// </remarks>
	public static IReadOnlyList<Row> For( ParkWorld.CatalogueObject ride, ParkItemCatalogue catalogue, ParkResearch research,
		bool instantAction )
	{
		var rows = new List<Row>();
		var level = (byte)ride.UpgradeLevel;

		if ( !instantAction )
		{
			foreach ( var item in catalogue.All )
			{
				if ( item.AddOnUpgradesId == ride.CatalogueId && research.IsResearched( item.Id ) )
					rows.Add( new Row( Kind.AddOn, item.Id, 0, 0, research.Price( item.Id ) ) );
			}

			if ( ride.State == UpgradeWaiting )
			{
				rows.Add( new Row( Kind.Cancel, CancelValue, (int)UIStrings.CancelLevel, level + 2, 0 ) );
			}
			else if ( level < TopLevel && level + 1 <= research.TierResearched( ride.CatalogueId )
				&& catalogue.TryGet( ride.CatalogueId, out var rideItem ) )
			{
				rows.Add( new Row( Kind.Level, -(level + 1), (int)UIStrings.UpgradeToLevel, level + 2,
					rideItem.UpgradeCostAt( level + 1 ) ) );
			}
		}

		if ( rows.Count == 0 )
		{
			var why = instantAction ? UIStrings.UpgradesNotAvailable : level == TopLevel ? UIStrings.RideFullyUpgraded : UIStrings.NoUpgrades;
			rows.Add( new Row( Kind.Message, MessageValue, (int)why, 0, 0 ) );
		}

		return rows;
	}

	/// <summary>The ride's <c>mState</c> while an ordered upgrade waits for a mechanic (<c>FUN_004e03e0</c>; <c>ride-operation.md</c>, "Where an object's state comes from").</summary>
	public const int UpgradeWaiting = 2;

	/// <summary>What a choice does (<c>FUN_004af480</c>).</summary>
	public enum Outcome { Nothing, Cancel, Order, OrderRefused, Carry, TooDear }

	/// <summary>
	/// What choosing the row of <paramref name="value"/> does with <paramref name="balance"/> in the bank. The message row
	/// does nothing; the cancel cancels; a level is ordered only when its <c>CostOfUpgrade</c> is <b>strictly below</b> the
	/// balance (<c>0x004af4e9</c>), and a dearer one is refused with no cue; an add-on is taken into the hand when its
	/// control record's price is <b>at most</b> the balance, and a dearer one sounds the refusal.
	/// </summary>
	public static Outcome Choose( int value, ParkWorld.CatalogueObject ride, ParkItemCatalogue catalogue, ParkResearch research,
		int balance )
	{
		if ( value == MessageValue )
			return Outcome.Nothing;

		if ( value == CancelValue )
			return Outcome.Cancel;

		if ( value < 0 )
		{
			return catalogue.TryGet( ride.CatalogueId, out var item ) && item.UpgradeCostAt( -value ) < balance
				? Outcome.Order
				: Outcome.OrderRefused;
		}

		return research.Price( value ) <= balance ? Outcome.Carry : Outcome.TooDear;
	}
}
