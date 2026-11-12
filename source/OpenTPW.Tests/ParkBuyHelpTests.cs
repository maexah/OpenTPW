using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenTPW.UI;
using System.Linq;
using System.Reflection;

namespace OpenTPW.Tests;

/// <summary>
/// The buy screen's help rows (<c>docs/exe/hud.md</c>, "The buy screen's help rows"): <c>FUN_004abc80</c> posts the row of
/// the item the list last selected while the pointer is on the list's row area, and only on a change of the item or of
/// its state. The rows and the order of what is posted are the original's, measured in it (<c>q495/orig/</c>).
/// </summary>
[TestClass]
public class ParkBuyHelpTests
{
	private Level? _level;
	private Point2 _screen;
	private bool _popupHelp;

	[TestInitialize]
	public void Keep()
	{
		_level = Level.Current;
		_screen = Screen.Size;
		_popupHelp = GameOptions.Current.PopupHelp;
		GameOptions.Current.PopupHelp = true;
		Log ??= new();
		Input.Mouse = new();
		HelpState = ParkBuyScreen.HelpOffList;
	}

	[TestCleanup]
	public void PutBack()
	{
		Level.Current = _level!;
		Screen.Size = _screen;
		GameOptions.Current.PopupHelp = _popupHelp;
		Input.Mouse = new();
		HelpState = ParkBuyScreen.HelpOffList;
	}

	/// <summary>The poster's state, a static as the original's global is: put back so one test does not hand it to the next.</summary>
	private static int HelpState
	{
		set => typeof( ParkBuyScreen ).GetField( "_helpState", BindingFlags.NonPublic | BindingFlags.Static )!.SetValue( null, value );
	}

	private static void SetBalance( int balance )
		=> typeof( ParkState ).GetProperty( nameof( ParkState.Balance ) )!.SetValue( Level.Current!.ParkState, balance );

	private static UiList ListOf( ParkBuyScreen screen )
		=> (UiList)typeof( ParkBuyScreen ).GetField( "_list", BindingFlags.NonPublic | BindingFlags.Instance )!.GetValue( screen )!;

	/// <summary>The middle of a row's slot on the list, by the row's id.</summary>
	private static Vector2 RowAt( UiList list, int id )
	{
		var slot = Enumerable.Range( 0, list.Rows.Count ).Single( at => list.Rows[at].Id == id );
		Assert.IsTrue( slot < list.VisibleRows );

		return new Vector2( (list.RowArea.Left + list.RowArea.Right) / 2f, list.RowArea.Top + (slot * list.RowHeight) + (list.RowHeight / 2f) );
	}

	/// <summary>One frame of the stack with the pointer moved to <paramref name="at"/>: what the bar says after it.</summary>
	private static string Frame( WindowStack stack, Vector2 at, bool moved = true )
	{
		Input.Mouse = new() { Position = at, Delta = moved ? new Vector2( 1, 0 ) : Vector2.Zero };
		stack.Update();

		return stack.HelpBarText;
	}

	/// <summary>
	/// The state is the price against the bank, a price equal to it buying the item, and for a mystery ride the player's
	/// tickets against its cost, equal buying it; a ride the player has unlocked is priced as any other.
	/// </summary>
	/// <remarks><b>Mutations:</b> the bank compared strictly; the tickets compared strictly; the mystery's two states swapped; the unlocked ride taken as a mystery.</remarks>
	[TestMethod]
	public void TheStateIsThePriceAgainstTheBankAndAMysteryRidesCostAgainstTheTickets()
	{
		var (catalogue, _, _) = ParkFootprintPictureTests.BuyScreen();
		var belly = catalogue.All.Single( item => item.Name == "Belly Bounce" );
		var mystery = catalogue.All.First( item => item.GoldenTicketCost > 1 );

		Assert.AreEqual( 500, belly.BuildPrice );
		Assert.AreEqual( ParkBuyScreen.HelpToBuy, ParkBuyScreen.HelpStateOf( belly, 500, null, 0 ) );
		Assert.AreEqual( ParkBuyScreen.HelpDear, ParkBuyScreen.HelpStateOf( belly, 499, null, 0 ) );

		var cost = mystery.GoldenTicketCost;
		Assert.AreEqual( ParkBuyScreen.HelpMysteryBought, ParkBuyScreen.HelpStateOf( mystery, 0, null, cost ) );
		Assert.AreEqual( ParkBuyScreen.HelpMysteryShort, ParkBuyScreen.HelpStateOf( mystery, 1_000_000, null, cost - 1 ) );

		ushort[] unlocked = [(ushort)mystery.Id];
		Assert.AreEqual( ParkBuyScreen.HelpToBuy, ParkBuyScreen.HelpStateOf( mystery, mystery.BuildPrice, unlocked, 0 ) );
		Assert.AreEqual( ParkBuyScreen.HelpDear, ParkBuyScreen.HelpStateOf( mystery, mystery.BuildPrice - 1, unlocked, cost ) );
	}

	/// <summary>
	/// Through the stack, as the original's round a went (<c>q495/PREDICTION.txt</c>, O2 to O8): the kind's name and the
	/// row's text with no space between, the heading's and the tab's own rows (both off the row area), and the row's line
	/// again on the way back from either; a change of the bank under a resting pointer posts the row again.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> the row not taken from the selection; the off-list state not forgotten; the list's whole
	/// rectangle for its row area; the screen's post asked before the control's; the item's own name for its kind's; the
	/// dear and buy rows swapped.
	/// </remarks>
	[TestMethod]
	public void TheBuyListPostsItsItemsRowOnTheRows()
	{
		var (_, screen, stack) = ParkFootprintPictureTests.BuyScreen();
		var list = ListOf( screen );
		SetBalance( 1500 );

		var heading = new Vector2( 1243, 370 );
		var sideshows = new Vector2( 1453, 246 );
		var panel = new Vector2( 300, 300 );
		const string ape = "Pirate ShipYou can't afford to buy this item!";

		Assert.AreEqual( Localization.Help( 0x90 ), Frame( stack, heading ), "the heading is off the rows: its own row" );
		Assert.AreEqual( "Bouncy CastleLeft-click to buy", Frame( stack, RowAt( list, 1100 ) ) );
		Assert.AreEqual( Localization.Help( 0x90 ), Frame( stack, heading ), "the heading is off the rows: its own row (O2)" );
		Assert.AreEqual( ape, Frame( stack, RowAt( list, 1101 ) ) );
		Assert.AreEqual( ape, Frame( stack, RowAt( list, 1101 ), false ), "it stands" );
		Assert.AreEqual( Localization.Help( 142 ), Frame( stack, sideshows ), "the tab's own row (O5)" );
		Assert.AreEqual( ape, Frame( stack, RowAt( list, 1101 ) ), "back from the tab, which is off the rows: posted again (O6)" );
		Assert.AreEqual( "", Frame( stack, panel ) );
		Assert.AreEqual( ape, Frame( stack, RowAt( list, 1101 ) ), "from off the list: posted again (O8)" );

		SetBalance( 2000 );
		Assert.AreEqual( "Pirate ShipLeft-click to buy", Frame( stack, RowAt( list, 1101 ), false ), "the bank moved: the state did" );
		Assert.AreEqual( "help item 1101 posted 1101 state 3", screen.HelpCensus() );
	}

	/// <summary>
	/// <b>The fill's pick is posted as soon as the pointer comes onto the row area</b>, before any row is under it (Q495,
	/// built by Q232): one frame with the pointer on the strip under the last row letters the top row's line, the line the
	/// pointer on the top row keeps.
	/// </summary>
	/// <remarks><b>Mutations:</b> the buy list without its first row's pick; the pick not taken in <c>Update</c>.</remarks>
	[TestMethod]
	public void TheTopRowsLineIsPostedWithThePointerOnTheStripUnderTheRows()
	{
		var (_, screen, stack) = ParkFootprintPictureTests.BuyScreen();
		var list = ListOf( screen );
		SetBalance( 1500 );

		var top = list.Rows[0].Id;
		var strip = new Vector2( (list.RowArea.Left + list.RowArea.Right) / 2f, list.RowArea.Bottom - 2f );
		Assert.IsTrue( strip.Y > list.RowArea.Top + (System.Math.Min( list.Rows.Count, list.VisibleRows ) * list.RowHeight), "no row under the strip" );

		var posted = Frame( stack, strip );
		Assert.IsTrue( screen.HelpCensus().StartsWith( $"help item {top} posted {top} " ), screen.HelpCensus() );
		Assert.AreNotEqual( "", posted, "a line posted" );
		Assert.AreEqual( posted, Frame( stack, RowAt( list, top ) ), "the top row's own line" );
	}

	/// <summary>
	/// The poster itself: nothing until the item or its state changes, and from off the rows the item spoken of again.
	/// </summary>
	/// <remarks><b>Mutations:</b> posted every frame; posted off the rows; the state not compared.</remarks>
	[TestMethod]
	public void ThePosterPostsOnAChangeOfTheItemOrItsStateAlone()
	{
		var (_, screen, _) = ParkFootprintPictureTests.BuyScreen();
		screen.RowSelected( 1100 );

		Assert.AreEqual( 0x94, screen.HelpPost( true, 500, null, 0 )?.Row );
		Assert.IsNull( screen.HelpPost( true, 500, null, 0 ), "the same item in the same state" );
		Assert.AreEqual( 0x96, screen.HelpPost( true, 499, null, 0 )?.Row, "the bank fell under the price" );
		Assert.IsNull( screen.HelpPost( false, 499, null, 0 ), "off the rows: nothing" );
		Assert.AreEqual( 0x96, screen.HelpPost( true, 499, null, 0 )?.Row, "back on them: spoken of again" );
		screen.RowSelected( 1101 );
		Assert.AreEqual( 0x96, screen.HelpPost( true, 499, null, 0 )?.Row, "another item in the same state" );
	}

	/// <summary>
	/// A tab change takes the item with the old tab's rows: back on the new tab's row area, on a slot no row fills, the bar
	/// is not told of the old tab's row (the review of Q495: the Pirate Ship was posted over the sideshows).
	/// </summary>
	/// <remarks><b>Mutations:</b> the item kept across <c>Show</c>.</remarks>
	[TestMethod]
	public void ATabChangeForgetsTheOldTabsItem()
	{
		var (_, screen, _) = ParkFootprintPictureTests.BuyScreen();
		screen.RowSelected( 1101 );

		Assert.AreEqual( 0x96, screen.HelpPost( true, 1500, null, 0 )?.Row, "the Pirate Ship, dear" );

		screen.PickTab( 2 );

		Assert.IsNull( screen.HelpPost( false, 1500, null, 0 ), "the tab is off the rows" );
		Assert.IsNull( screen.HelpPost( true, 1500, null, 0 ), "back on the rows before Update takes the new tab's pick" );
		Assert.AreEqual( "help item 0 posted 1101 state -1", screen.HelpCensus() );
	}

	/// <summary>
	/// A golden-ticket ride's two rows: the one it is bought by when the tickets reach its cost (<c>0x97</c>), the one it is
	/// refused by below it (<c>0x98</c>), whatever the bank holds; and a ride the player has unlocked is posted as any other.
	/// </summary>
	/// <remarks><b>Mutations:</b> the two mystery rows swapped; the mystery priced by the bank.</remarks>
	[TestMethod]
	public void AMysteryRidePostsItsTwoRowsByTheTickets()
	{
		var (catalogue, screen, _) = ParkFootprintPictureTests.BuyScreen();
		var mystery = catalogue.All.First( item => item.GoldenTicketCost > 1 );
		var cost = mystery.GoldenTicketCost;

		screen.RowSelected( mystery.Id );

		Assert.AreEqual( 0x97, screen.HelpPost( true, 0, null, cost )?.Row, "the tickets reach the cost" );
		Assert.AreEqual( 0x98, screen.HelpPost( true, 1_000_000, null, cost - 1 )?.Row, "one ticket short, a full bank" );

		ushort[] unlocked = [(ushort)mystery.Id];
		var row = screen.HelpPost( true, mystery.BuildPrice, unlocked, 0 );

		Assert.AreEqual( 0x94, row?.Row, "unlocked: priced by the bank" );
	}

	/// <summary>
	/// The tickets a ride can still be bought with are those earned less those spent (<c>0x005af5d0</c>).
	/// </summary>
	/// <remarks><b>Mutations:</b> the spent not taken off; the spent added.</remarks>
	[TestMethod]
	public void TheTicketsUnspentAreTheEarnedLessTheSpent()
	{
		var file = new PlayerFile { RecordTickets = [1, 1, 1, 1], BonusTickets = [1, 0], TicketsSpent = 2 };
		var player = new Player( 0, "Test", file );

		Assert.AreEqual( 5, player.Tickets );
		Assert.AreEqual( 3, player.TicketsUnspent );
	}

	/// <summary>
	/// The name the row gives is the kind's, <c>Info.RideTypeStringIndex</c>'s row of <c>ITEMTYPES.str</c>: a sideshow's
	/// too, and a shop, which sets none, is named nothing (round b, B2 and B3).
	/// </summary>
	/// <remarks><b>Mutations:</b> the key not read; the item's own name; a missing index read as row 1.</remarks>
	[TestMethod]
	public void AnItemIsNamedByItsKind()
	{
		var (catalogue, _, _) = ParkFootprintPictureTests.BuyScreen();
		string Kind( string name ) => ParkBuyScreen.TypeNameOf( catalogue.All.Single( item => item.Name == name ) );

		Assert.AreEqual( "Bouncy Castle", Kind( "Belly Bounce" ) );
		Assert.AreEqual( "Simulator", Kind( "Aztec Mayhem" ) );
		Assert.AreEqual( "Shooting Gallery", Kind( "Jungle Spray" ) );
		Assert.AreEqual( "", Kind( "Drinks Shop" ) );
	}

	/// <summary>
	/// The land rows (O10 to O12): Buy Land's line and Clear Land's, each posted once for as long as it is the item, so
	/// back on Buy Land from off the list the bar stays empty, and the state is left forgotten.
	/// </summary>
	/// <remarks><b>Mutations:</b> the land rows' lines swapped; a land row posted again from off the list.</remarks>
	[TestMethod]
	public void ALandRowIsPostedOnceForAsLongAsItIsTheItem()
	{
		var (_, screen, stack) = ParkFootprintPictureTests.BuyScreen();
		var list = ListOf( screen );
		screen.PickTab( 3 );

		var panel = new Vector2( 300, 300 );

		Assert.AreEqual( Localization.Help( 0x21a ), Frame( stack, RowAt( list, -1 ) ) );
		Assert.AreEqual( "", Frame( stack, panel ) );
		Assert.AreEqual( "", Frame( stack, RowAt( list, -1 ) ), "a land row already posted is not posted again" );
		Assert.AreEqual( "help item -1 posted -1 state -1", screen.HelpCensus() );
		Assert.AreEqual( Localization.Help( 0x21b ), Frame( stack, RowAt( list, -2 ) ) );
		Assert.AreEqual( Localization.Help( 0x21a ), Frame( stack, RowAt( list, -1 ) ) );
	}
}
