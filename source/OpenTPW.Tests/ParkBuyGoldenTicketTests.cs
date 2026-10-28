using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenTPW.UI;
using System.Linq;
using System.Reflection;

namespace OpenTPW.Tests;

/// <summary>
/// The buy list's golden-ticket rows and their click (<c>docs/exe/hud.md</c>, "Spending golden tickets"), on the player
/// the original was measured with (<c>q709/gms-two.dat</c>: 4 tickets earned, 2 spent, no ride bought).
/// </summary>
[TestClass]
public class ParkBuyGoldenTicketTests
{
	private Level? _level;
	private Point2 _screen;
	private Player? _player;
	private int _sort;

	private static readonly PropertyInfo CurrentPlayer = typeof( Players ).GetProperty( nameof( Players.Current ) )!;

	private static readonly FieldInfo Sort = typeof( ParkBuyScreen ).GetField( "_sort", BindingFlags.Static | BindingFlags.NonPublic )!;

	[TestInitialize]
	public void Keep()
	{
		_level = Level.Current;
		_screen = Screen.Size;
		_player = Players.Roster.Current;
		_sort = (int)Sort.GetValue( null )!;
		Log ??= new();
		FileSystem = GameData.Required();
		Sort.SetValue( null, 1 );
		typeof( ParkBuyScreen ).GetField( "_lastTab", BindingFlags.Static | BindingFlags.NonPublic )!.SetValue( null, 0 );
	}

	[TestCleanup]
	public void PutBack()
	{
		ParkHand.LetGo();
		Level.Current = _level!;
		Screen.Size = _screen;
		CurrentPlayer.SetValue( Players.Roster, _player );
		Sort.SetValue( null, _sort );
	}

	/// <summary>The player of <c>gms-two.dat</c>: 4 tickets earned, 2 spent, no ride bought.</summary>
	private static PlayerFile TwoLeft()
	{
		var file = new PlayerFile { RecordTickets = [1, 1, 1, 1], TicketsSpent = 2 };
		CurrentPlayer.SetValue( Players.Roster, new Player( 0, "Test", file ) );

		return file;
	}

	private static void SetBalance( int balance )
		=> typeof( ParkState ).GetProperty( nameof( ParkState.Balance ) )!.SetValue( Level.Current!.ParkState, balance );

	private static UiList ListOf( ParkBuyScreen screen )
		=> (UiList)typeof( ParkBuyScreen ).GetField( "_list", BindingFlags.NonPublic | BindingFlags.Instance )!.GetValue( screen )!;

	/// <summary>The colour of the cell lettered <paramref name="text"/>.</summary>
	private static UiColour CellColour( UiList list, string text )
		=> list.Children.Single( cell => cell.Visible && cell.Text == text ).TextColour;

	/// <summary>
	/// <c>PriceSkin_SetValue</c>: "$ " before a number at or above nought, " gt" after the negation of one below, and a
	/// number past the bias lettered less it.
	/// </summary>
	/// <remarks><b>Mutations:</b> the negative not negated; nought taken as tickets; the bias's bound strict the other way.</remarks>
	[TestMethod]
	public void ThePriceCellLettersCashAndTickets()
	{
		Assert.AreEqual( "$ 2500", ParkBuyScreen.PriceText( 2500 ) );
		Assert.AreEqual( "$ 0", ParkBuyScreen.PriceText( 0 ) );
		Assert.AreEqual( "1 gt", ParkBuyScreen.PriceText( -1 ) );
		Assert.AreEqual( "3 gt", ParkBuyScreen.PriceText( -3 ) );
		Assert.AreEqual( "$ 250539758", ParkBuyScreen.PriceText( 0xeeeeeee ) );
		Assert.AreEqual( "250539758 gt", ParkBuyScreen.PriceText( -0xeeeeeee ) );
		Assert.AreEqual( "$ 1", ParkBuyScreen.PriceText( 0xeeeeeef ) );
		Assert.AreEqual( "-$ 5", ParkBuyScreen.PriceText( -(0xeeeeeee + 5) ) );
	}

	/// <summary>
	/// The rides tab as the original drew it (<c>q709/orig/a-lists.png</c>): the two mystery rows first, Jurassic Tours'
	/// (1170) before Eruption's (1112) as the catalogue walks them, "1 gt" in gold and "3 gt" in red; a cash row white, red
	/// once its price is above the bank on the next tick; and a land row (231, 239, 75).
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> the rows handed over in name order; the colour not asked as the rows are drawn; the tickets
	/// compared strictly; a mystery row coloured by the bank; the price lettered bare.
	/// </remarks>
	[TestMethod]
	public void TheMysteryRowsStandInTheCataloguesOrderLetteredAndColouredByTheTickets()
	{
		TwoLeft();
		var (_, screen, _) = ParkFootprintPictureTests.BuyScreen();
		var list = ListOf( screen );
		SetBalance( 50000 );
		list.Refresh();

		Assert.AreEqual( "1170 1112", string.Join( ' ', list.Rows.Take( 2 ).Select( row => row.Id ) ) );
		Assert.AreEqual( new UiColour( 255, 224, 0 ), CellColour( list, "1 gt" ) );
		Assert.AreEqual( new UiColour( 255, 0, 0 ), CellColour( list, "3 gt" ) );
		Assert.AreEqual( UiColour.White, CellColour( list, "$ 500" ), "the Belly Bounce" );

		SetBalance( 500 );
		list.Refresh();
		Assert.AreEqual( UiColour.White, CellColour( list, "$ 500" ), "a price equal to the bank" );

		SetBalance( 499 );
		list.Refresh();
		Assert.AreEqual( ParkBuyScreen.ShortColour, CellColour( list, "$ 500" ) );
		Assert.AreEqual( ParkBuyScreen.TicketsColour, CellColour( list, "1 gt" ), "the bank not asked" );

		screen.PickTab( ItemDescriptionFile.Feature );
		Assert.AreEqual( new UiColour( 231, 239, 75 ), list.RowColour!( list.Rows.Single( row => row.Id == -1 ) ) );
	}

	/// <summary>
	/// The click (<c>BuyScreen_OnMessage</c>, <c>0x004ac5a9</c>): with an empty bank, the row the tickets buy goes into the
	/// hand and the screen closes, tickets equal to its cost buying it; the one they do not is refused with <c>cat_ui</c>
	/// <c>0x1D</c> and the screen stays.
	/// </summary>
	/// <remarks><b>Mutations:</b> the bank asked of a mystery ride; the refusal silent; the tickets compared strictly.</remarks>
	[TestMethod]
	public void AMysteryRowIsCarriedByTheTicketsAndRefusedWithout()
	{
		var file = TwoLeft();
		var (_, screen, stack) = ParkFootprintPictureTests.BuyScreen();
		var list = ListOf( screen );
		SetBalance( 0 );
		UiSounds.LastAsked = null;

		list.Activated!( 1112 );

		Assert.AreEqual( (0, ParkBuyScreen.RefusedSound), (ParkBuilding.Carrying, UiSounds.LastAsked) );
		Assert.IsTrue( stack.Windows.Contains( screen ), "the screen stays" );

		// One left, Jurassic Tours' cost: equal buys it.
		file.TicketsSpent = 3;
		UiSounds.LastAsked = null;
		list.Activated!( 1170 );

		Assert.AreEqual( (1170, (int?)null), (ParkBuilding.Carrying, UiSounds.LastAsked) );
		Assert.IsFalse( stack.Windows.Contains( screen ), "closed behind it" );
	}

	/// <summary>
	/// The carry's preview never refuses a mystery ride for money (<c>BuildTool_PreviewPass</c>, <c>0x005235e6</c>): at a
	/// cell where the same ride, once bought, is refused because the bank is below its price, the mystery ride passes that
	/// test and goes on to the next.
	/// </summary>
	/// <remarks><b>Mutation:</b> the money test made of a mystery ride too.</remarks>
	[TestMethod]
	public void AMysteryRideIsNotRefusedForMoneyWhereItIsPutDown()
	{
		var file = TwoLeft();
		var (catalogue, _, _) = ParkFootprintPictureTests.BuyScreen();
		var state = Level.Current!.ParkState!;
		SetBalance( -1000 );

		var build = typeof( ParkBuilding ).GetMethods( BindingFlags.NonPublic | BindingFlags.Static )
			.Single( method => method.Name == "Build" && method.GetParameters().Length == 10 );

		string Answer( int x, int y )
		{
			var built = build.Invoke( null, [state, Level.Current.Park, catalogue, null, null, 1170, x, y, 0, 0] )!;
			return (string)built.GetType().GetProperty( "Answer" )!.GetValue( built )!;
		}

		file.RideIds.Add( 1170 );
		var cell = Enumerable.Range( 0, 128 * 128 ).Select( at => (X: at % 128, Y: at / 128) )
			.First( at => Answer( at.X, at.Y ).Contains( "and the park has -1000" ) );

		file.RideIds.Clear();
		var mystery = Answer( cell.X, cell.Y );

		Assert.IsFalse( mystery.Contains( "and the park has" ), mystery );
		Assert.AreEqual( (2, 0), (file.TicketsSpent, state.Balance + 1000), "nothing paid where nothing was built" );
	}

	/// <summary>
	/// The click only carries the ride (<c>BuyScreen_OnMessage</c>, <c>0x004ac270</c>); the tickets are spent where it is put
	/// down (<c>Object_Construct</c>, <c>0x004db4a6</c>): after the click the spent count and the ride ids are as they were,
	/// a put-down that does not stand the ride spends nothing either, and <see cref="ParkBuilding.PayFor(ParkState, ParkItemCatalogue.Item, Player)"/>,
	/// which <c>Build</c> calls once the ride stands, spends the cost once and takes no cash.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> the tickets spent at the click; the tickets spent before the ride stands; <c>PayFor</c> leaving the
	/// tickets alone; the cash taken as well. What is not reached here: <c>Build</c>'s own call of <c>PayFor</c> after a ride
	/// stands, because standing a ride wants a graphics device (<c>ParkObjects.Place</c>).
	/// </remarks>
	[TestMethod]
	public void TheTicketsAreSpentWhereTheRideIsPutDownNotWhereItIsClicked()
	{
		var file = TwoLeft();
		var (catalogue, screen, _) = ParkFootprintPictureTests.BuyScreen();
		var state = Level.Current!.ParkState!;
		SetBalance( 10000 );

		ListOf( screen ).Activated!( 1170 );

		Assert.AreEqual( 1170, ParkBuilding.Carrying );
		Assert.AreEqual( (2, ""), (file.TicketsSpent, string.Join( ' ', file.RideIds )), "carried, not yet bought" );

		var build = typeof( ParkBuilding ).GetMethods( BindingFlags.NonPublic | BindingFlags.Static )
			.Single( method => method.Name == "Build" && method.GetParameters().Length == 10 );
		var objects = new ParkObjects( "jungle", null, null );

		string Answer( int x, int y )
		{
			var built = build.Invoke( null, [state, Level.Current.Park, catalogue, objects, null, 1170, x, y, 0, 0] )!;
			return (string)built.GetType().GetProperty( "Answer" )!.GetValue( built )!;
		}

		var cell = Enumerable.Range( 0, 128 * 128 ).Select( at => (X: at % 128, Y: at / 128) )
			.First( at => Answer( at.X, at.Y ).Contains( "would not load" ) );

		Assert.AreEqual( (2, "", 10000), (file.TicketsSpent, string.Join( ' ', file.RideIds ), state.Balance), "not stood, not paid" );

		var item = catalogue.All.Single( i => i.Id == 1170 );
		Assert.AreEqual( "1 golden tickets", ParkBuilding.PayFor( state, item, Players.Roster.Current ) );
		Assert.AreEqual( (3, "1170", 10000), (file.TicketsSpent, string.Join( ' ', file.RideIds ), state.Balance), "put down: paid in tickets" );
	}

	/// <summary>
	/// <b>A full thing table builds nothing</b> (<see cref="ParkState.NextThingId"/>): at a cell where the ride passes
	/// every test, with every id held the build is refused and says why. The original falls over here
	/// (<c>FUN_004d91f0</c>).
	/// </summary>
	/// <remarks>
	/// <b>Mutation:</b> the build going on with id nought. With no object layer here every build that goes on ends at
	/// "would not load" before <c>PayFor</c>, so the balance check below fails only a payment moved ahead of the id
	/// test; that the refusal comes before <c>PayFor</c> is by code order, not measured.
	/// </remarks>
	[TestMethod]
	public void AFullThingTableBuildsNothing()
	{
		var file = TwoLeft();
		var (catalogue, _, _) = ParkFootprintPictureTests.BuyScreen();
		var state = Level.Current!.ParkState!;
		SetBalance( 10000 );
		file.RideIds.Add( 1170 );

		var build = typeof( ParkBuilding ).GetMethods( BindingFlags.NonPublic | BindingFlags.Static )
			.Single( method => method.Name == "Build" && method.GetParameters().Length == 10 );

		string Answer( int x, int y )
		{
			var built = build.Invoke( null, [state, Level.Current.Park, catalogue, null, null, 1170, x, y, 0, 0] )!;
			return (string)built.GetType().GetProperty( "Answer" )!.GetValue( built )!;
		}

		var cell = Enumerable.Range( 0, 128 * 128 ).Select( at => (X: at % 128, Y: at / 128) )
			.First( at => Answer( at.X, at.Y ).Contains( "would not load" ) );

		// Bounded, so a table with no end fails rather than spins.
		for ( var taken = 0; taken <= ParkState.LastThingId && state.NextThingId() != 0; ++taken )
		{
		}

		StringAssert.Contains( Answer( cell.X, cell.Y ), "the thing table is full" );
		Assert.AreEqual( 10000, state.Balance, "nothing paid before the id test" );
	}

	/// <summary>
	/// A cash row the bank cannot pay is refused with the same sound as a mystery row the tickets cannot buy
	/// (<c>0x004ac6ca</c> to <c>0x004ac7f6</c>, <c>0x00486ae0</c>); one the bank can pay is carried, silently.
	/// </summary>
	/// <remarks><b>Mutations:</b> the refusal silent on the cash arm; the price compared strictly.</remarks>
	[TestMethod]
	public void ACashRowTheBankCannotPayIsRefusedWithTheSound()
	{
		TwoLeft();
		var (catalogue, screen, stack) = ParkFootprintPictureTests.BuyScreen();
		var list = ListOf( screen );
		var bounce = catalogue.All.Single( item => item.Id == 1100 );
		Assert.AreEqual( 500, bounce.BuildPrice );

		SetBalance( 499 );
		UiSounds.LastAsked = null;
		list.Activated!( 1100 );

		Assert.AreEqual( (0, ParkBuyScreen.RefusedSound), (ParkBuilding.Carrying, UiSounds.LastAsked) );
		Assert.IsTrue( stack.Windows.Contains( screen ), "the screen stays" );

		SetBalance( 500 );
		UiSounds.LastAsked = null;
		list.Activated!( 1100 );

		Assert.AreEqual( (1100, (int?)null), (ParkBuilding.Carrying, UiSounds.LastAsked) );
		Assert.IsFalse( stack.Windows.Contains( screen ) );
	}
}
