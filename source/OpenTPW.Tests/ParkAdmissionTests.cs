using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// What a guest makes of the price of coming in - <c>FUN_004ff5b0</c>, and the three pairs of cells the
/// admission states walk to.
///
/// <para>
/// <b>The test that matters most is the one that discriminates.</b> Lost Kingdom charges 25 against an
/// ideal price of 20, and that lands in a different band depending on which balance stack is loaded: the
/// standard game calls it expensive, easy mode calls it about right. So this file asserts <i>both</i>
/// answers rather than only the one we ship, because a test that pinned the shipped answer alone would
/// pass just as happily if the stack were being chosen wrongly.
/// </para>
/// <para>
/// These read real game files and are skipped where there is no installation - see <see cref="GameData"/>.
/// </para>
/// </summary>
[TestClass]
public class ParkAdmissionTests
{
	private BaseFileSystem data = null!;

	[TestInitialize]
	public void MountTheGame() => FileSystem = data = GameData.Required();

	/// <summary>What the park actually charges, taken from the save rather than typed in here.</summary>
	private int ShippedFee()
	{
		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );

		var economy = new ParkWorld( new SaveReader( stream ).ReadFile() ).Economy;

		Assert.IsNotNull( economy, "the shipped park should carry an economy" );

		return economy!.Value.AdmissionFee;
	}

	private static ParkAdmission Easy( int fee ) => new( new ParkBalance( "jungle", easyMode: true ), fee );

	private static ParkAdmission Standard( int fee ) => new( new ParkBalance( "jungle" ), fee );

	/// <summary>
	/// The five numbers that turn a fee into an opinion, read from the stack rather than defaulted.
	///
	/// <para>
	/// The fallbacks inside <see cref="ParkAdmission"/> are the global file's own values, so a key that
	/// went missing would leave a plausible number behind. These assertions are against the two files'
	/// <i>different</i> values, which is what shows the keys were actually found.
	/// </para>
	/// </summary>
	[TestMethod]
	public void TheFeeConstantsComeFromWhicheverBalanceStackIsLoaded()
	{
		var easy = Easy( 25 );
		var standard = Standard( 25 );

		foreach ( var admission in new[] { easy, standard } )
		{
			Assert.AreEqual( 4, admission.ExcitementToCostDivisor, "PeepInfo.ExcitementToCostDivisor" );
			Assert.AreEqual( 20, admission.MinimumEntryFee, "PeepInfo.MinimumEntryFee" );
			Assert.AreEqual( 0.75f, admission.CheapMultiplier, 0.0001f, "PeepInfo.CheapPriceMultiplier" );
		}

		// The two the easy file changes, and the whole reason the stack matters.
		Assert.AreEqual( 1.5f, easy.AverageMultiplier, 0.0001f, "easy mode's average multiplier" );
		Assert.AreEqual( 2.5f, easy.ExpensiveMultiplier, 0.0001f, "easy mode's expensive multiplier" );

		Assert.AreEqual( 1.25f, standard.AverageMultiplier, 0.0001f, "the standard game's average multiplier" );
		Assert.AreEqual( 2f, standard.ExpensiveMultiplier, 0.0001f, "the standard game's expensive multiplier" );
	}

	/// <summary>
	/// The three pairs of cells, which are what told the three cell-pickers apart.
	///
	/// <para>
	/// <c>FUN_004d8610</c> reads a Y of its own and two X's, <c>FUN_004d8690</c> likewise, and
	/// <c>FUN_004d86d0</c> reads <b>one shared Y</b> with two X's. Only the bus stops have that shape -
	/// both on row 5, 42 and 53 apart - which is how the leave path was distinguished from the other two.
	/// </para>
	/// </summary>
	[TestMethod]
	public void TheThreePairsOfGateCellsAreTheOnesTheBalanceFileNames()
	{
		var admission = Easy( 25 );

		Assert.AreEqual( (47, 13), admission.TicketBoothA, "TicketBoothA - where guests are charged" );
		Assert.AreEqual( (48, 13), admission.TicketBoothB, "TicketBoothB" );

		Assert.AreEqual( (47, 17), admission.EntranceA, "EntranceA - the gateway itself" );
		Assert.AreEqual( (48, 17), admission.EntranceB, "EntranceB" );

		Assert.AreEqual( (42, 5), admission.BusStopA, "BusStopA - where a guest who will not pay goes" );
		Assert.AreEqual( (53, 5), admission.BusStopB, "BusStopB" );

		// The booths are four rows in front of the gateway, which is exactly the gap the guests standing
		// there were measured at - see ParkGuestStateTests.
		Assert.AreEqual( 4, admission.EntranceA.Y - admission.TicketBoothA.Y,
			"the booths stand four cells short of the arch" );

		// The shared row is the bus stops' own signature, and the other two pairs must not share it.
		Assert.AreEqual( admission.BusStopA.Y, admission.BusStopB.Y, "the bus stops are on one row" );
		Assert.AreNotEqual( admission.BusStopA.X, admission.BusStopB.X, "and far apart along it" );
	}

	/// <summary>
	/// The ideal price cannot fall below the minimum entry fee, and for a park with nothing running it is
	/// exactly that.
	///
	/// <para>
	/// <b>This is what makes the shipped park's verdict turn on the fee alone.</b> The excitement term is
	/// nought - the original's sum counts only things with somebody in their queue, and the save records
	/// that the park has never admitted a visitor - so the jittered division contributes nothing and the
	/// ideal price is the floor.
	/// </para>
	/// </summary>
	[TestMethod]
	public void AParkWithNothingRunningIsWorthExactlyTheMinimumEntryFee()
	{
		var admission = Easy( 25 );

		for ( var seed = 0; seed < 50; ++seed )
		{
			Assert.AreEqual( admission.MinimumEntryFee, admission.IdealPrice( 0, new Random( seed ) ),
				$"seed {seed}: no excitement means no more than the floor" );
		}
	}

	/// <summary>
	/// The division really is jittered, so the same park is worth slightly different amounts to different
	/// guests - <c>rand % (divisor / 2 + 1) + divisor</c>, which for the shipped 4 gives four, five or six.
	///
	/// <para>
	/// The assertion is that more than one answer occurs. A jitter that had been read as a plain divide
	/// would give one value for every guest and would still look entirely reasonable.
	/// </para>
	/// </summary>
	[TestMethod]
	public void TheIdealPriceIsJitteredRatherThanADivisionEverybodyAgreesOn()
	{
		var admission = Easy( 25 );

		var seen = Enumerable.Range( 0, 200 )
			.Select( seed => admission.IdealPrice( 100, new Random( seed ) ) )
			.ToHashSet();

		// 100 / 4, 5 or 6 is 25, 20 or 16, and the floor of 20 is added to each.
		Assert.IsTrue( seen.SetEquals( new[] { 36, 40, 45 } ),
			$"the three divisors should give three prices, not {string.Join( ", ", seen.OrderBy( p => p ) )}" );
	}

	/// <summary>
	/// <b>Lost Kingdom's own fee, judged both ways - the discrimination this whole file exists for.</b>
	///
	/// <para>
	/// 25 against an ideal price of 20 is <c>25.0 &lt;= 1.25 * 20</c> in the standard game, which is true by
	/// a hair, so a guest there finds the park expensive and eventually leaves. Easy mode's 1.5 puts the
	/// line at 30, so the same guest pays without comment. The park is an easy-mode park - its loans carry
	/// easy mode's nought interest - so <see cref="ParkAdmission.Opinion.AboutRight"/> is the answer that
	/// reaches the screen.
	/// </para>
	/// </summary>
	[TestMethod]
	public void TwentyFiveIsAboutRightInEasyModeAndExpensiveInTheStandardGame()
	{
		var fee = ShippedFee();

		Assert.AreEqual( 25, fee, "the shipped park's admission fee" );

		Assert.AreEqual( ParkAdmission.Opinion.AboutRight, Easy( fee ).OpinionOf( 20 ),
			"in easy mode the guests pay and come in" );

		Assert.AreEqual( ParkAdmission.Opinion.OnTheExpensiveSide, Standard( fee ).OpinionOf( 20 ),
			"in the standard game the very same fee is over the line" );
	}

	/// <summary>
	/// The four bands and the two edges the original puts them on.
	///
	/// <para>
	/// The boundaries are the point: a fee landing exactly on the average line is <i>already</i> expensive
	/// (the test is <c>average * ideal &lt;= fee</c>), a fee landing exactly on the expensive line is
	/// <b>far too</b> expensive (the original asks whether the fee is <i>not below</i> it), and a fee
	/// landing exactly on the cheap line is cheap (<c>fee &lt;= cheap * ideal</c>). All three inclusive
	/// edges fall on the side a naive reading would put them on the wrong side of.
	/// </para>
	/// <para>
	/// <b>The ideal price here is 40 rather than 20, and that is a finding rather than a convenience.</b>
	/// At an ideal price of 20 the cheap line is <c>0.75 * 20 = 15</c>, which lies <i>below</i>
	/// <c>PeepInfo.MinimumEntryFee</c> - so the early-out catches every fee that would have been cheap and
	/// answers <see cref="ParkAdmission.Opinion.AboutRight"/> instead. <b>The cheap band is unreachable
	/// for a park with nothing running</b>, and nobody can feel they got a bargain until the park is worth
	/// more than the floor. An earlier draft of this test asserted the cheap band at 20 and failed for
	/// exactly that reason.
	/// </para>
	/// </summary>
	[TestMethod]
	public void EachBandIncludesItsOwnEdge()
	{
		// Standard multipliers against an ideal price of 40, so the three lines sit at 30, 50 and 80 - all
		// of them above the floor of 20, which is what makes every band reachable.
		Assert.AreEqual( ParkAdmission.Opinion.OnTheCheapSide, Standard( 30 ).OpinionOf( 40 ), "on the cheap line" );
		Assert.AreEqual( ParkAdmission.Opinion.AboutRight, Standard( 31 ).OpinionOf( 40 ), "just above it" );

		Assert.AreEqual( ParkAdmission.Opinion.AboutRight, Standard( 49 ).OpinionOf( 40 ), "just below the average line" );
		Assert.AreEqual( ParkAdmission.Opinion.OnTheExpensiveSide, Standard( 50 ).OpinionOf( 40 ), "on it" );

		Assert.AreEqual( ParkAdmission.Opinion.OnTheExpensiveSide, Standard( 79 ).OpinionOf( 40 ), "just below the expensive line" );
		Assert.AreEqual( ParkAdmission.Opinion.FarTooExpensive, Standard( 80 ).OpinionOf( 40 ), "on it" );
		Assert.AreEqual( ParkAdmission.Opinion.FarTooExpensive, Standard( 200 ).OpinionOf( 40 ), "well past it" );
	}

	/// <summary>
	/// And the cheap band really is out of reach for a park worth only the floor, which is the other half
	/// of the paragraph above - stated as its own test so the fact is asserted rather than only described.
	/// </summary>
	[TestMethod]
	public void NoFeeCanFeelCheapWhileTheParkIsWorthOnlyTheMinimumEntryFee()
	{
		var admission = Standard( 1 );

		for ( var fee = 0; fee <= admission.MinimumEntryFee; ++fee )
		{
			Assert.AreNotEqual( ParkAdmission.Opinion.OnTheCheapSide,
				Standard( fee ).OpinionOf( admission.MinimumEntryFee ),
				$"a fee of {fee} against an ideal price of {admission.MinimumEntryFee}" );
		}

		// The same fee against a park worth more IS a bargain, so this is a property of the floor rather
		// than of the band being unreachable altogether.
		Assert.AreEqual( ParkAdmission.Opinion.OnTheCheapSide, Standard( 25 ).OpinionOf( 40 ),
			"the very same fee is cheap once the park is worth 40" );
	}

	/// <summary>
	/// The balance file's "minimum price below which everybody always pays up happily" - and it is an
	/// early-out that compares nothing, so it holds even where the multipliers would say otherwise.
	///
	/// <para>
	/// A fee of 20 against an ideal price of 20 is on the standard game's average line, so without the
	/// early-out it would read as expensive. It does not, because neither number is above the floor.
	/// </para>
	/// </summary>
	[TestMethod]
	public void NobodyMindsAFeeAtOrBelowTheMinimumEntryFee()
	{
		foreach ( var fee in new[] { 0, 1, 19, 20 } )
		{
			Assert.AreEqual( ParkAdmission.Opinion.AboutRight, Standard( fee ).OpinionOf( 20 ),
				$"a fee of {fee} is at or below the floor, so nobody minds it" );
		}

		// And one above the floor is judged normally, or the early-out would be swallowing everything.
		//
		// The control has to land in a DIFFERENT band to show anything, which an earlier draft got wrong:
		// it used 21, and 21 against an ideal price of 20 is below the average line of 25, so it is about
		// right for the ordinary reason and proved nothing about the early-out at all.
		Assert.AreEqual( ParkAdmission.Opinion.OnTheExpensiveSide, Standard( 25 ).OpinionOf( 20 ),
			"a fee above the floor is judged rather than waved through" );

		Assert.AreEqual( ParkAdmission.Opinion.FarTooExpensive, Standard( 40 ).OpinionOf( 20 ),
			"and one well above it is judged all the way to the top band" );
	}

	/// <summary>
	/// <b>The whole admission loop, driven on the real park - the test that reaches the new code at all.</b>
	///
	/// <para>
	/// Every other test of the behaviour builds a <see cref="PeepBehaviour"/> without an admission, so
	/// <see cref="PeepBehaviour.Admission"/> is null and the fee is never judged. That means the existing
	/// suite passed unchanged when <c>JudgingTheFee</c> was built, which is not reassurance - it is the
	/// unit-versus-wiring trap, and this is the test that closes it. Removing the admission argument below
	/// must make this fail.
	/// </para>
	/// <para>
	/// The five guests Alexah found standing at the ticket booths walk there, judge 25 against an ideal
	/// price of 20, find it about right under easy mode's multipliers, pay, and settle down to wait for the
	/// gate. The park's takings are the arithmetic check on top: five admissions at the fee the save
	/// carries, and nothing from the eight guests who were already past the booths.
	/// </para>
	/// </summary>
	[TestMethod]
	public void TheGuestsAtTheTicketBoothsPayTheirWayIntoTheParkAndWaitForTheGate()
	{
		var world = Park();
		var admission = new ParkAdmission( new ParkBalance( "jungle", easyMode: true ), ShippedFee() );

		var (behaviour, guests) = Run( world, admission, () => ParkRides.GateIsOpen, turns: 200 );

		foreach ( var id in AtTheBooths )
		{
			Assert.IsTrue( guests[id].PaidAdmission, $"guest {id} should have paid to come in" );
			Assert.AreEqual( PeepState.WaitingForOpening, guests[id].State,
				$"guest {id} should have paid and be waiting for the gate" );
		}

		Assert.AreEqual( AtTheBooths.Length * admission.Fee, behaviour.Takings,
			"the park has taken exactly five admissions" );
	}

	/// <summary>
	/// The other arm: a gate that never opens is waited on for as long as the guest rolled, and then they
	/// give up and set off for a bus stop.
	///
	/// <para>
	/// <b>This is what makes the wait a countdown rather than a decoration.</b> Entering the waiting state
	/// rolls 200 to 349 turns, so a run of 200 would leave everybody still waiting; it takes a longer run
	/// to see anyone leave, and their destination - a bus stop cell centre - is what says they were sent
	/// somewhere rather than merely relabelled.
	/// </para>
	/// </summary>
	[TestMethod]
	public void AGateThatNeverOpensSendsThemToTheBusStopEventually()
	{
		var world = Park();
		var admission = new ParkAdmission( new ParkBalance( "jungle", easyMode: true ), ShippedFee() );

		// The park is open but the gate's script never reports itself open, which is the second half of
		// the pair FUN_004ff7f0 asks about.
		var (_, guests) = Run( world, admission, () => 0, turns: 600 );

		var one = ParkWorld.NavigatorState.One;

		var stops = new[] { admission.BusStopA, admission.BusStopB }
			.Select( cell => ((cell.X * one) + (one / 2), (cell.Y * one) + (one / 2)) )
			.ToHashSet();

		foreach ( var id in AtTheBooths )
		{
			Assert.AreEqual( PeepState.HeadingForExit, guests[id].State,
				$"guest {id} should have given up on a gate that never opened" );

			Assert.IsTrue( stops.Contains( (guests[id].Navigator.Target.X, guests[id].Navigator.Target.Y) ),
				$"guest {id} should be walking to a bus stop, not to {guests[id].Navigator.Target}" );
		}
	}

	/// <summary>The five guests the save leaves standing at the ticket booths.</summary>
	private static readonly int[] AtTheBooths = [42, 39, 35, 31, 29];

	private ParkWorld Park()
	{
		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		return new ParkWorld( new SaveReader( stream ).ReadFile() );
	}

	/// <summary>
	/// Steps every guest of a real park for a while, the same way <c>ParkPeople</c> does - the state is
	/// asked before the walk, which is the original's order.
	/// </summary>
	private static (PeepBehaviour Behaviour, System.Collections.Generic.Dictionary<int, Peep> Guests) Run(
		ParkWorld world, ParkAdmission admission, Func<int> gateStatus, int turns )
	{
		var behaviour = new PeepBehaviour( world, new Random( 1234 ), admission, gateStatus );
		var blocked = CellEdge.For( world, ParkPeople.WalkingMode ).Blocked;

		var guests = ParkPeople.PeepsIn( world ).ToDictionary( peep => peep.ThingId );
		var walks = guests.Values.ToDictionary( peep => peep.ThingId,
			peep => new PeepWalk( peep.Navigator, blocked ) );

		for ( var tick = 1; tick <= turns; ++tick )
		{
			foreach ( var peep in guests.Values )
				behaviour.Step( peep, walks[peep.ThingId], playing: null, tick );
		}

		return (behaviour, guests);
	}
}
