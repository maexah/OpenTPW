using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// The park's money, which is a thing rather than a header field: <c>mBankAccount</c> is a handle, the
/// thing it names is model 16, and until this was read nothing in the tree knew what a park charges.
///
/// <para>
/// <b>The point of these tests is the cross-file agreement, not the numbers.</b> A record layout read out
/// of a serialiser can be plausible and wrong, so the assertions that matter here are the ones a
/// misaligned layout could not pass: the eight saved loans are checked against
/// <c>LoanInfo[0..7]</c> in the balance file - a completely separate file that knows nothing about the
/// save - and every monthly repayment is checked against its own amount and period.
/// </para>
/// <para>
/// These read real game files and are skipped where there is no installation - see <see cref="GameData"/>.
/// </para>
/// </summary>
[TestClass]
public class ParkEconomyTests
{
	private BaseFileSystem data = null!;

	[TestInitialize]
	public void MountTheGame() => FileSystem = data = GameData.Required();

	private const string ShippedPark = "levels/jungle/Easymode.TPWI";

	private ParkWorld World()
	{
		using var stream = new MemoryStream( data.ReadAllBytes( ShippedPark ) );
		return new ParkWorld( new SaveReader( stream ).ReadFile() );
	}

	private ParkWorld.EconomyState Money()
	{
		var economy = World().Economy;

		Assert.IsNotNull( economy, "the walk should have reached the park's model 16" );

		return economy!.Value;
	}

	/// <summary>
	/// The economy is the thing the header's own <c>mBankAccount</c> points at, and that handle is 8.
	///
	/// <para>
	/// <b>The anti-vacuity check is the first assertion, not the last.</b> If the walk stopped early, every
	/// field below would read nought and several of them are legitimately nought - so the trailer and the
	/// thing count are asserted before anything is believed about the contents.
	/// </para>
	/// </summary>
	[TestMethod]
	public void ThePracticalParkKeepsItsMoneyOnTheThingTheHeaderNames()
	{
		var world = World();

		Assert.IsNull( world.Problem, "nothing should have stopped the walk" );
		Assert.IsTrue( world.ClosedOnTrailer, "the walk should have landed exactly on the WRLD trailer" );
		Assert.AreEqual( 42, world.ThingCount, "the shipped park's thing count" );

		Assert.AreEqual( 8, world.BankAccount, "mBankAccount names thing 8" );
		Assert.IsNotNull( world.Economy, "and thing 8 should have been read as the economy" );
	}

	/// <summary>
	/// What Lost Kingdom charges, holds and owes.
	///
	/// <para>
	/// <b>The admission fee is 25 and that number does real work</b> - it is what every guest at the ticket
	/// booths judges, and it is above <c>PeepInfo.MinimumEntryFee</c>, so the early-out in
	/// <c>FUN_004ff5b0</c> that would let everybody in happily does <b>not</b> fire for this park.
	/// </para>
	/// <para>
	/// <b><see cref="ParkWorld.EconomyState.Balance"/> and
	/// <see cref="ParkWorld.EconomyState.LastBalance"/> differ by exactly 200</b>, which is the structural
	/// check on this pair: two adjacent fields of a misread record would be unrelated numbers, and these
	/// are a running balance and the previous reading of it.
	/// </para>
	/// </summary>
	[TestMethod]
	public void TheParkChargesTwentyFiveAndIsInTheBlack()
	{
		var money = Money();

		Assert.AreEqual( 25, money.AdmissionFee, "mAdmissionFee" );
		Assert.AreEqual( 87987, money.Balance, "mBalance" );
		Assert.AreEqual( 87787, money.LastBalance, "mLastBalance" );
		Assert.AreEqual( 200, money.Balance - money.LastBalance, "the balance has moved by 200 since it was last read" );

		Assert.AreEqual( 0, money.BatchBalance, "mBatchBalance - nothing is part-way through being banked" );
		Assert.AreEqual( 1, money.WithdrawalsEnabled, "mWithdrawalsEnabled reads as a flag" );
		Assert.AreEqual( 0, money.TurnEnteredRed, "mTurnEnteredRed - a park in the black never entered it" );
		Assert.AreEqual( -12013, money.ProfitThisYear, "mProfitThisYear - the park has been built and not yet opened" );

		Assert.IsTrue( money.AdmissionFee is > 0 and < ParkWorld.EconomyState.EnormousDeposit,
			"the fee is inside the bound the original itself asserts before banking one" );
	}

	/// <summary>
	/// The eight saved loans agree with <c>LoanInfo[0..7]</c> in the balance file, field for field.
	///
	/// <para>
	/// <b>This is the assertion that makes the record layout more than a reading.</b> The balance file is a
	/// different file in a different format that says nothing about saves, and the amounts and repayment
	/// periods in it are 100000/50000/25000/10000/18000/30000/80000/65000 and 36/36/36/36/24/30/48/30. A
	/// layout off by one field - or by one loan's stride - could not reproduce sixteen unrelated numbers in
	/// order.
	/// </para>
	/// <para>
	/// Every monthly repayment is then its own amount divided by its own period, truncated, which ties the
	/// three fields of each loan to one another rather than to a constant.
	/// </para>
	/// </summary>
	[TestMethod]
	public void EverySavedLoanAgreesWithTheBalanceFileThatDescribesIt()
	{
		var money = Money();
		var balance = new ParkBalance( "jungle", easyMode: true );

		Assert.AreEqual( ParkWorld.EconomyState.LoanSlots, money.Loans.Count, "eight slots are always written" );

		for ( var i = 0; i < money.Loans.Count; ++i )
		{
			var loan = money.Loans[i];

			// A fallback that is deliberately not the answer, so a missing key cannot pass as a match.
			var amount = balance.Int( $"LoanInfo[{i}].LoanAmount", -999 );
			var months = balance.Int( $"LoanInfo[{i}].RepaymentPeriodInMonths", -999 );

			Assert.AreEqual( amount, loan.AmountAvailable, $"loan {i} amount_available" );
			Assert.AreEqual( months, loan.RepaymentMonths, $"loan {i} repayment_period_in_months" );
			Assert.AreEqual( i, loan.LenderNameIndex, $"loan {i} lenderNameIndex" );

			Assert.AreEqual( amount / months, loan.MonthlyRepayment, $"loan {i} monthly_repayment" );

			Assert.AreEqual( 0, loan.Bought, $"loan {i} has not been taken out" );
			Assert.AreEqual( 0, loan.MonthsRepaid, $"loan {i} has nothing repaid" );
			Assert.IsTrue( loan.Available is 0 or 1, $"loan {i} loan_available reads as a flag, not {loan.Available}" );
		}

		// Exactly one of the eight is on offer, which is a fact about this park rather than about the
		// format - and it is what says the flag is read per loan rather than shared.
		Assert.AreEqual( 1, money.Loans.Count( loan => loan.Available != 0 ),
			"one of Lost Kingdom's eight loans is on offer" );
	}

	/// <summary>
	/// The park was built in <b>easy mode</b>, and its own loans are the evidence.
	///
	/// <para>
	/// <b>This is the test that justifies <see cref="ParkBalance"/>'s third layer</b>, and it is written as
	/// a discrimination rather than a match: the saved APRs are nought, <c>Easy_Standard.sam</c> says
	/// nought, and the global <c>Standard.sam</c> says 20, 20, 20, 20, 23, 22, 18 and 21. Asserting only
	/// "the save says nought" would pass just as happily if the two balance files agreed, which is exactly
	/// the vacuity this spells out. It matters because those same two files disagree about
	/// <c>PeepInfo.AveragePriceMultiplier</c>, and that one decides whether a guest pays 25 to come in or
	/// turns round and goes home.
	/// </para>
	/// </summary>
	[TestMethod]
	public void TheSavedLoansCarryEasyModesInterestRatesAndNotTheStandardGames()
	{
		var money = Money();

		var easy = new ParkBalance( "jungle", easyMode: true );
		var standard = new ParkBalance( "jungle" );

		var differ = 0;

		for ( var i = 0; i < money.Loans.Count; ++i )
		{
			var easyApr = easy.Int( $"LoanInfo[{i}].APRInPercent", -999 );
			var standardApr = standard.Int( $"LoanInfo[{i}].APRInPercent", -999 );

			Assert.AreEqual( easyApr, money.Loans[i].AprPercent, $"loan {i} APR matches easy mode" );

			if ( easyApr != standardApr )
				++differ;
		}

		// Without this the test above would be satisfied by two files that say the same thing.
		Assert.AreEqual( ParkWorld.EconomyState.LoanSlots, differ,
			"all eight APRs must actually differ between the two balance files, or this proves nothing" );

		// And the pair that does the damage: the price a guest will put up with.
		Assert.AreEqual( 1.5f, easy.Float( "PeepInfo.AveragePriceMultiplier", -999f ), 0.0001f, "easy mode" );
		Assert.AreEqual( 1.25f, standard.Float( "PeepInfo.AveragePriceMultiplier", -999f ), 0.0001f, "the standard game" );
	}

	/// <summary>
	/// Layering the easy file changes what it says it changes and leaves the rest of the stack alone -
	/// the check that a third pass has not quietly moved the peep constants everything else reads.
	/// </summary>
	[TestMethod]
	public void TheEasyLayerMovesThePricesAndNothingTheOtherTestsRelyOn()
	{
		var easy = new ParkBalance( "jungle", easyMode: true );
		var standard = new ParkBalance( "jungle" );

		Assert.AreEqual( standard.Count, easy.Count,
			"the easy file introduces no keys of its own, so the stack is the same size" );

		// Read with the accessor that matches the value's own type. <b>Getting this wrong is what the two
		// different fallbacks are for</b>: an earlier draft looked PeepInfo.CheapPriceMultiplier up with
		// Int, which cannot parse "0.75", so BOTH stacks returned their fallbacks - and because those
		// fallbacks differ, the assertion failed loudly instead of passing while proving nothing about
		// that key. Matching fallbacks would have hidden it.
		foreach ( var key in new[]
		{
			"PeepInfo.ExitLevel", "PeepInfo.ExitLevelVar", "PeepInfo.StartingCashVarPc",
			"PeepInfo.ToiletDesparate", "PeepInfo.ExcitementToCostDivisor", "PeepInfo.MinimumEntryFee",
			"Seasons[0].AvgWeatherQuality",
			"FixedItemInfo.EntranceAPosX", "FixedItemInfo.TicketBoothAPosY", "FixedItemInfo.BusStopAPosX"
		} )
		{
			var read = standard.Int( key, -999 );

			Assert.AreNotEqual( -999, read, $"{key} should be in the standard stack at all" );
			Assert.AreEqual( read, easy.Int( key, -998 ), $"{key} should read the same either way" );
		}

		// The one multiplier the easy file restates without changing, which has to go through Float.
		Assert.AreEqual( 0.75f, standard.Float( "PeepInfo.CheapPriceMultiplier", -999f ), 0.0001f,
			"the standard game's cheap-price multiplier" );
		Assert.AreEqual( 0.75f, easy.Float( "PeepInfo.CheapPriceMultiplier", -998f ), 0.0001f,
			"and easy mode restates it unchanged" );
	}
}
