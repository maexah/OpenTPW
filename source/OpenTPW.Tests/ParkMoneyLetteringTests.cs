using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// The money's lettering on the entry price's spinner and the gadget's balance (Q525; <c>docs/exe/hud.md</c>, "A money cell"
/// and "The place"). These read the shipped game's meshes, fonts and UITEXT and are skipped without it - see <see cref="GameData"/>.
/// </summary>
[TestClass]
public class ParkMoneyLetteringTests
{
	[TestInitialize]
	public void Forget()
	{
		Log ??= new();
		Unimplemented.Forget();
	}

	[TestCleanup]
	public void ForgetAgain() => Unimplemented.Forget();

	/// <summary>
	/// <b>The entry price's spinner letters the fee as a money cell</b>: UITEXT <c>0x1c1</c> "$ " and the figure, "$ 28" at the
	/// fee the original's frame showed it at, and its plus moves the lettering with the fee. The fee is held to 0..10000, so
	/// the cell's <c>0x1c2</c> is not reached from here.
	/// </summary>
	/// <remarks><b>Mutations:</b> the fee lettered bare ("28", as before); "$" with no space.</remarks>
	[TestMethod]
	public void TheEntryPricesSpinnerLettersTheFeeAsAMoneyCell()
	{
		var world = ParkTrendArrowTests.Stock();
		var state = new ParkState( parkIsClosed: false, visitorsToDate: 0 );
		var (screen, level) = (Screen.Size, Level.Current);

		try
		{
			Screen.Size = new Point2( 2048, 1536 );
			Level.Current = ParkTrendArrowTests.RunningLevel( world, state );

			foreach ( var (fee, text) in new[] { (28, "$ 28"), (0, "$ 0"), (10000, "$ 10000") } )
			{
				state.SetAdmissionFee( fee );

				var price = new UI.ParkEntryPriceScreen( ParkTrendArrowTests.AStack() );

				StringAssert.StartsWith( price.Census(), $"fee {text} closed", $"the fee {fee}" );
			}

			state.SetAdmissionFee( 28 );
			var nudged = new UI.ParkEntryPriceScreen( ParkTrendArrowTests.AStack() );
			var plus = ParkTrendArrowTests.Find( nudged.Root, 0x4f3ae ).Children.OfType<UI.UiButton>().Last();

			plus.Clicked!();
			Assert.AreEqual( 29, state.AdmissionFee );
			StringAssert.StartsWith( nudged.Census(), "fee $ 29 closed", "the plus moves the lettering" );
		}
		finally
		{
			Screen.Size = screen;
			Level.Current = level;
		}
	}

	/// <summary>
	/// <b>The gadget letters a balance under nought as the original does, with no 0x1c2</b>: -796, the original's own file's
	/// balance, is "796" with the icon's <c>dollarsignneg</c> part ("-$"), not "-$ 796" or "$ -796" in the label.
	/// <b>The balance's colour is counted below the white band</b> (Q403's six bands, <c>(balance + 999) / 1000</c> held to
	/// 0..5): at -796, 0 and 4000 the colour is not ours, at 4001 and over it is white as ours is.
	/// </summary>
	/// <remarks><b>Mutations:</b> the count taken out; counted only under nought; counted in the white band too.</remarks>
	[TestMethod]
	public void ABalanceUnderNoughtIsTheIconsMinusAndItsSizeAndItsColourIsCounted()
	{
		var world = ParkTrendArrowTests.Stock();

		foreach ( var (balance, text, icon, counted) in new[] { (-796, "796", 1, true), (0, "0", 0, true), (4000, "4000", 0, true),
			(4001, "4001", 0, false), (87787, "87787", 0, false) } )
		{
			Unimplemented.Forget();

			ParkTrendArrowTests.WithAGadget( world, new ParkState( parkIsClosed: false, visitorsToDate: 0, balance: balance ), gadget =>
			{
				Assert.AreEqual( text, ParkTrendArrowTests.Find( gadget.Root, 0x2f ).Text, $"{balance}'s lettering" );
				Assert.AreEqual( icon, ParkTrendArrowTests.Find( gadget.Root, 0x32 ).Frame, $"{balance}'s icon" );
				Assert.AreEqual( counted, Unimplemented.Summary.Any( entry => entry.What == "GADGET_BALANCE_COLOUR" ), $"{balance}'s colour counted" );
			} );
		}

		Assert.AreEqual( (0, 0, 4, 5, 5), (UI.ParkGadget.BalanceBand( -796 ), UI.ParkGadget.BalanceBand( 0 ), UI.ParkGadget.BalanceBand( 4000 ),
			UI.ParkGadget.BalanceBand( 4001 ), UI.ParkGadget.BalanceBand( 87787 )) );
	}
}
