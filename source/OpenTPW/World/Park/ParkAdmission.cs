using System;

namespace OpenTPW;

/// <summary>
/// What the park charges to come in, what a guest makes of that price, and the cells the whole business
/// happens on - the original's <c>FUN_004ff5b0</c> and the three pairs of gate cells its callers walk to.
///
/// <para>
/// <b>The fee is not a constant and it is not in the balance file.</b> It lives on the park's economy
/// thing, which the save header names through <c>mBankAccount</c> - see
/// <see cref="ParkWorld.EconomyState"/>. The balance file supplies only the five numbers that turn a fee
/// into an opinion, which is why this takes both.
/// </para>
/// <para>
/// <b>The five constants are the balance file's own, and its comments decode the function line by
/// line</b>: <c>MinimumEntryFee</c> is "minimum price below which everybody always pays up happily",
/// which is exactly the early-out; <c>CheapPriceMultiplier</c> is "this number * ideal price = top end of
/// 'cheap entry fee' territory"; and the average and expensive multipliers say the same for their bands.
/// The executable's own copies read back as zeros, because these are data rather than code.
/// </para>
/// <para>
/// <b>Which balance stack is loaded changes the answer for the shipped park, so it is not a detail.</b>
/// Lost Kingdom charges 25 against an ideal price of 20, and
/// <c>PeepInfo.AveragePriceMultiplier</c> is 1.25 in the standard game and 1.5 in easy mode - so the
/// standard stack puts 25 in "expensive" territory and easy mode calls it about right. The park is an
/// easy-mode park; <see cref="ParkBalance"/> records the two fields of the save that prove it.
/// </para>
/// </summary>
public sealed class ParkAdmission
{
	/// <summary>
	/// What a guest thinks of the price, and the numbers are the original's own return values - they are
	/// what <c>FUN_004ff9d0</c> switches on, so they are fixed rather than ours to choose.
	/// </summary>
	public enum Opinion
	{
		/// <summary>"Person: park far too expensive" - the guest turns round and heads for the bus stop.</summary>
		FarTooExpensive = 0,

		/// <summary>"Person: park on the expensive side" - they wait a while, sulk, and judge again.</summary>
		OnTheExpensiveSide = 1,

		/// <summary>"Person: park on the cheap side" - they cheer up and pay.</summary>
		OnTheCheapSide = 2,

		/// <summary>"Person: your admission fee is ab[out right]" - they pay without comment.</summary>
		AboutRight = 3
	}

	/// <param name="balance">The park's balance stack, for the five numbers that turn a fee into a band.</param>
	/// <param name="fee">
	/// What the park charges, from its economy thing. Taken as a number rather than as the thing, so that a
	/// test can ask what a guest would make of a price this park does not charge.
	/// </param>
	public ParkAdmission( ParkBalance balance, int fee )
	{
		ArgumentNullException.ThrowIfNull( balance );

		Fee = fee;

		// The fallbacks are the global file's own values rather than zeros: a missing key should leave the
		// simulation playable, and a zero divisor here would be a division by nought a few lines below.
		ExcitementToCostDivisor = Math.Max( 1, balance.Int( "PeepInfo.ExcitementToCostDivisor", 4 ) );
		MinimumEntryFee = balance.Int( "PeepInfo.MinimumEntryFee", 20 );
		CheapMultiplier = balance.Float( "PeepInfo.CheapPriceMultiplier", 0.75f );
		AverageMultiplier = balance.Float( "PeepInfo.AveragePriceMultiplier", 1.25f );
		ExpensiveMultiplier = balance.Float( "PeepInfo.ExpensivePriceMultiplier", 2f );
		SmallHappinessChange = balance.Int( "PeepInfo.SmallHappinessChange", 5 );
		MediumHappinessChange = balance.Int( "PeepInfo.MediumHappinessChange", 15 );
		BigHappinessChange = balance.Int( "PeepInfo.BigHappinessChange", 25 );

		TicketBoothA = Cell( balance, "TicketBoothA" );
		TicketBoothB = Cell( balance, "TicketBoothB" );
		EntranceA = Cell( balance, "EntranceA" );
		EntranceB = Cell( balance, "EntranceB" );
		BusStopA = Cell( balance, "BusStopA" );
		BusStopB = Cell( balance, "BusStopB" );
	}

	private static (int X, int Y) Cell( ParkBalance balance, string name )
		=> (balance.Int( $"FixedItemInfo.{name}PosX", -1 ), balance.Int( $"FixedItemInfo.{name}PosY", -1 ));

	/// <summary>What the park charges - the economy thing's <c>mAdmissionFee</c>.</summary>
	public int Fee { get; }

	/// <summary>"Scaling factor for entry fee", and the base of the jitter the ideal price is divided by.</summary>
	public int ExcitementToCostDivisor { get; }

	/// <summary>
	/// The price below which everybody pays up happily. It is added to every ideal price <b>and</b> is the
	/// floor the early-out tests against, which is why an ideal price can never fall below it.
	/// </summary>
	public int MinimumEntryFee { get; }

	/// <summary>Top of the "cheap" band, as a multiple of the ideal price - 0.75.</summary>
	public float CheapMultiplier { get; }

	/// <summary>Top of the "about right" band - 1.25 in the standard game, 1.5 in easy mode.</summary>
	public float AverageMultiplier { get; }

	/// <summary>Bottom of the "far too expensive" band - 2.0 in the standard game, 2.5 in easy mode.</summary>
	public float ExpensiveMultiplier { get; }

	/// <summary>
	/// The smallest of the three mood changes - <c>PeepInfo.SmallHappinessChange</c>, 5, the global at
	/// <c>0x00785058</c>. Every guest whose thing is sold from under them loses it (<c>FUN_004fb360</c>).
	/// </summary>
	public int SmallHappinessChange { get; }

	/// <summary>
	/// How much happiness a guest gains or loses over the price - <c>PeepInfo.MediumHappinessChange</c>, 15.
	///
	/// <para>
	/// The balance file names three (<c>Small</c> 5, <c>Medium</c> 15, <c>Big</c> 25) and the original picks
	/// between them by an argument to <c>FUN_004fe9c0</c> / <c>FUN_004fea70</c>; judging the fee passes 1 on
	/// both the cheap and the expensive arm, which is this one.
	/// </para>
	/// </summary>
	public int MediumHappinessChange { get; }

	/// <summary>
	/// The largest of the three mood changes - <c>PeepInfo.BigHappinessChange</c>, 25. Losing this is what
	/// happens to a guest whose park shuts while they are deciding what to do (<c>FUN_004fec90</c> passes 2).
	/// </summary>
	/// <remarks>
	/// <b>All three live here, and this is an awkward home for them.</b> They are about a guest's mood, not
	/// about the price of coming in, and each is read by something that is not the gate: the settle-up, the
	/// sold-thing eviction, a park shutting. The balance loader places them side by side in its
	/// <c>PeepInfo</c> block (<c>0x00785058</c>, <c>0x0078505c</c>, <c>0x00785060</c>; see
	/// <c>docs/exe/park-engine.md</c>, "Selling and the people on it"). Moving all three into a holder of
	/// their own is owed. Splitting them across two homes would be worse than this.
	/// </remarks>
	public int BigHappinessChange { get; }

	/// <summary>
	/// The two cells in front of the gate where a guest stands to be charged - the balance file's
	/// <c>TicketBoothA/B</c>, (47,13) and (48,13) in every theme the game ships.
	///
	/// <para>
	/// <b>These are the cells Alexah saw guests stopped on</b>, four rows short of the archway.
	/// <c>FUN_004d8610</c> hands one of them out, and entering <see cref="PeepState.JudgingTheFee"/> turns
	/// a guest to face a fixed heading chosen by <i>which</i> of the two they are standing on.
	/// </para>
	/// </summary>
	public (int X, int Y) TicketBoothA { get; }

	/// <inheritdoc cref="TicketBoothA"/>
	public (int X, int Y) TicketBoothB { get; }

	/// <summary>
	/// The two cells of the gateway itself - <c>EntranceA/B</c>, (47,17) and (48,17). <c>FUN_004d8690</c>
	/// hands one out, and it is where a guest who has paid walks to. The park's gate model stands across
	/// exactly these two cells, which is checked independently in <c>ParkFixedItemsTests</c>.
	/// </summary>
	public (int X, int Y) EntranceA { get; }

	/// <inheritdoc cref="EntranceA"/>
	public (int X, int Y) EntranceB { get; }

	/// <summary>
	/// Where a guest goes when they will not pay - <c>BusStopA/B</c>, (42,5) and (53,5).
	///
	/// <para>
	/// <c>FUN_004d86d0</c> reads <b>one shared Y and two X's</b>, which is this pair's own shape and is how
	/// the three pickers were told apart: the booth and entrance pickers each read a Y of their own.
	/// </para>
	/// </summary>
	public (int X, int Y) BusStopA { get; }

	/// <inheritdoc cref="BusStopA"/>
	public (int X, int Y) BusStopB { get; }

	/// <summary>
	/// What the park is worth to a guest, which the original logs as <c>GP</c> beside the fee's <c>AF</c>.
	///
	/// <para>
	/// <b>The division is jittered, so the same park is worth slightly different amounts to different
	/// guests</b>: the divisor is <c>rand % (divisor / 2 + 1) + divisor</c>, which for the shipped 4 gives
	/// four, five or six. Integer division, as the original's is.
	/// </para>
	/// <para>
	/// <b>It can never come out below <see cref="MinimumEntryFee"/></b>, because that is added on the end
	/// and the term in front of it cannot be negative. That is what makes the early-out in
	/// <see cref="OpinionOf"/> turn on the fee alone for a park with nothing running.
	/// </para>
	/// </summary>
	/// <param name="excitement">
	/// What the park's rides are worth, summed by <c>FUN_004c8240</c>.
	///
	/// <b>Nought for the shipped park, and by that park's own saved state rather than by omission:</b> the
	/// sum counts only things whose queue holds somebody, and the save records that the park had never
	/// admitted a visitor, so no queue could hold anyone <i>at load</i>. <b>Rides operate now and guests
	/// join queues</b>, so that nought is the starting value rather than a permanent one - this said
	/// nothing in the tree operated a ride. It is named and passed rather than being a zero nobody sees.
	/// </param>
	public int IdealPrice( int excitement, Random random )
	{
		ArgumentNullException.ThrowIfNull( random );

		var jitter = (random.Next() % ((ExcitementToCostDivisor / 2) + 1)) + ExcitementToCostDivisor;

		return (excitement / jitter) + MinimumEntryFee;
	}

	/// <summary>
	/// Which band <see cref="Fee"/> falls in against an ideal price - the tail of <c>FUN_004ff5b0</c>.
	///
	/// <para>
	/// <b>The early-out is the balance file's "everybody always pays up happily"</b>: if neither the ideal
	/// price nor the fee is above <see cref="MinimumEntryFee"/>, the function returns
	/// <see cref="Opinion.AboutRight"/> without comparing anything. Lost Kingdom does <b>not</b> take it -
	/// it charges 25 against a floor of 20.
	/// </para>
	/// <para>
	/// <b>The comparisons are in floating point while both prices arrive as integers</b>, which is the
	/// original's own arrangement and matters at the boundaries: 25 against an average multiplier of 1.25
	/// and an ideal price of 20 is <c>25.0 &lt;= 25.0</c>, which is true, so the standard game puts this
	/// park in the expensive band by a hair. Easy mode's 1.5 does not.
	/// </para>
	/// </summary>
	public Opinion OpinionOf( int idealPrice )
	{
		if ( idealPrice <= MinimumEntryFee && Fee <= MinimumEntryFee )
			return Opinion.AboutRight;

		float ideal = idealPrice;
		float fee = Fee;

		if ( AverageMultiplier * ideal <= fee )
		{
			// Read as "not below the expensive line", which is how the original spells it - so a fee
			// landing exactly on it is far too expensive rather than merely expensive.
			return fee < ExpensiveMultiplier * ideal ? Opinion.OnTheExpensiveSide : Opinion.FarTooExpensive;
		}

		return fee <= CheapMultiplier * ideal ? Opinion.OnTheCheapSide : Opinion.AboutRight;
	}

	/// <summary>Both at once, which is all any caller wants - see <see cref="IdealPrice"/> for the jitter.</summary>
	public Opinion OpinionAt( int excitement, Random random ) => OpinionOf( IdealPrice( excitement, random ) );
}
