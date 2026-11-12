using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace OpenTPW.Tests;

/// <summary>
/// The park gadget's research button, <c>0x2b</c>, which runs the research opener (<c>FUN_004aa480</c>,
/// <c>docs/exe/hud.md</c>, "The nine screens behind Info, Money and Research"). These read the gadget's meshes from the
/// shipped game and are skipped where there is no installation - see <see cref="GameData"/>.
/// </summary>
[TestClass]
public class ParkGadgetTests
{
	private Point2 screenBefore;

	[TestInitialize]
	public void MountTheGame()
	{
		// The interface's own 2048x1536, so a point in the window is the same point on the gadget's layout. Before the game
		// is required, so that a skip leaves the cleanup the real size to put back.
		screenBefore = Screen.Size;
		Screen.Size = new Point2( 2048, 1536 );

		Log ??= new();
		FileSystem = GameData.Required();
	}

	[TestCleanup]
	public void PutTheScreenBack() => Screen.Size = screenBefore;

	/// <summary>
	/// <b>Each click on the research button runs the research opener</b>, which in Instant Action is its box; a click on
	/// the gauge beside it, which takes the press and has nothing to do, opens none.
	/// </summary>
	/// <remarks><b>Mutations:</b> the opener taken out opens nothing; one on every release opens on the gauge's click.</remarks>
	[TestMethod]
	public void EachClickOnResearchRunsTheOpener()
	{
		var stack = AStack();
		stack.Open( new UI.ParkGadget( stack ) );
		Assert.IsTrue( Level.InstantAction, "no player: Instant Action" );
		Assert.IsTrue( stack.ClickAt( 333, 1313 ), "the middle of 0x2b's (274,1254)-(392,1372)" );
		Assert.AreEqual( ParkResearchOpenerTests.InstantActionText, ParkResearchOpenerTests.TextOf( (UI.MessageBox)stack.Windows[^1] ) );
		stack.Close( stack.Windows[^1] );

		Assert.IsTrue( stack.ClickAt( 333, 1313 ) );
		Assert.IsInstanceOfType( stack.Windows[^1], typeof( UI.MessageBox ), "every click, not the first alone" );
		stack.Close( stack.Windows[^1] );

		Assert.IsTrue( stack.ClickAt( 115, 1200 ), "the gauge, 0x1e" );
		CollectionAssert.AreEqual( new[] { "ParkGadget" }, stack.Windows.Select( window => window.GetType().Name ).ToArray(),
			"the gauge's click opens nothing" );
	}

	/// <summary>
	/// <b>The corner shows the keys held and the tickets not spent, each with its icon hidden at none</b> (<c>0x004a1002</c>..
	/// <c>0x004a115b</c>): two jungle tickets and the first key, one spent, read "1  x" and "1  x" (two spaces, <c>0x00752ef8</c>)
	/// as the original's did before its award (<c>q706/orig/O/g-05.png</c>); both spent hide the ticket and its count, the keys
	/// staying; three spent, -1, is never the first count written, so the corner keeps its seeded "0  x" and the ticket shown, as the original's
	/// (<c>q707/orig/O2/g-05.png</c>); no key given hides the key row; Instant Action hides it whatever the keys.
	/// </summary>
	/// <remarks><b>Mutations:</b> the tickets earned shown; the spent added; the ticket left shown at none; the count
	/// written whatever the last, or floored at nought; the last written not stored; the seed left out; the last written starting at 0; the key left shown at none or in
	/// Instant Action; a single space in the format.</remarks>
	[TestMethod]
	public void TheCornerShowsTheKeysAndTheTicketsNotSpent()
	{
		var file = new PlayerFile { InstantAction = false, KeysGiven = 1, TicketsSpent = 1 };
		file.Park( "jungle" ).Tickets[0] = file.Park( "jungle" ).Tickets[2] = 1;
		var current = typeof( Players ).GetProperty( nameof( Players.Current ) )!;

		try
		{
			current.SetValue( Players.Roster, new Player( 0, "Test", file ) );
			var gadget = new UI.ParkGadget( AStack() );

			Assert.AreEqual( ("1  x", true, true), (Find( gadget.Root, 0x37 ).Text, Find( gadget.Root, 0x37 ).Visible, Find( gadget.Root, 0x35 ).Visible) );
			Assert.AreEqual( "1  x", Find( gadget.Root, 0x36 ).Text, "two tickets and the key given: one key" );
			Assert.IsTrue( Find( gadget.Root, 0x1e0f0 ).Visible, "the key row, one key held" );

			file.TicketsSpent = 2;
			gadget = new UI.ParkGadget( AStack() );

			Assert.AreEqual( (false, false), (Find( gadget.Root, 0x37 ).Visible, Find( gadget.Root, 0x35 ).Visible), "none unspent: hidden" );
			Assert.AreEqual( "1  x", Find( gadget.Root, 0x36 ).Text );

			file.TicketsSpent = 3;
			gadget = new UI.ParkGadget( AStack() );

			Assert.AreEqual( ("0  x", true, true), (Find( gadget.Root, 0x37 ).Text, Find( gadget.Root, 0x37 ).Visible, Find( gadget.Root, 0x35 ).Visible),
				"one more spent than won, -1, equals the last written's -1: never written, the seed stays (0x004a10d2)" );

			file.TicketsSpent = 4;
			gadget = new UI.ParkGadget( AStack() );

			Assert.AreEqual( ("-2  x", true, true), (Find( gadget.Root, 0x37 ).Text, Find( gadget.Root, 0x37 ).Visible, Find( gadget.Root, 0x35 ).Visible),
				"-2 differs from the last written's -1: written as any count" );

			file.TicketsSpent = 1;
			gadget = new UI.ParkGadget( AStack() );
			file.TicketsSpent = 3;
			gadget.ShowEarned();

			Assert.AreEqual( ("-1  x", true, true), (Find( gadget.Root, 0x37 ).Text, Find( gadget.Root, 0x37 ).Visible, Find( gadget.Root, 0x35 ).Visible),
				"-1 after a written 1 differs from the last written: written; only a first count of -1 keeps the seed" );
			file.TicketsSpent = 3;

			file.KeysGiven = 0;
			gadget = new UI.ParkGadget( AStack() );

			Assert.IsFalse( Find( gadget.Root, 0x1e0f0 ).Visible, "no key held: the key and its count are hidden" );

			file.KeysGiven = 1;
			file.InstantAction = true;
			gadget = new UI.ParkGadget( AStack() );

			Assert.IsFalse( Find( gadget.Root, 0x1e0f0 ).Visible, "Instant Action: no key row" );
		}
		finally
		{
			current.SetValue( Players.Roster, null );
		}
	}

	/// <summary>
	/// <b>The date is the american UITEXT 448 with the day as parameter 16, the month as 17 and the year as 18, none
	/// padded</b> (<c>0x004a0ea1</c>): 10 January 2002 reads "1.10.2002", month.day.year as the original's gadget read it
	/// (<c>q526/orig/a-2-loaded-gadget.png</c>); the English row, handed in, reads it day first.
	/// </summary>
	/// <remarks><b>Mutations:</b> the day and the month swapped; either padded to two digits; the year less 1900; the
	/// machine's short date; the English table read by default.</remarks>
	[TestMethod]
	public void TheDateIsLetteredFromRow448()
	{
		Assert.AreEqual( "1.10.2002", UI.ParkGadget.DateOf( new DateTime( 2002, 1, 10 ) ) );
		Assert.AreEqual( "12.31.1999", UI.ParkGadget.DateOf( new DateTime( 1999, 12, 31, 23, 59, 59 ) ) );
		Assert.AreEqual( "1.6.2002", UI.ParkGadget.DateOf( new DateTime( 2002, 1, 6 ) ) );
		Assert.AreEqual( "10.1.2002", UI.ParkGadget.DateOf( new DateTime( 2002, 1, 10 ), new StringFile( "Language/English/UITEXT.str" ) ),
			"the English row is day first" );
	}

	/// <summary>
	/// <b>The gadget letters the park's date when it is built</b>: tick 208 is 10 January 2000 (208 x 15,000 / 4 seconds
	/// is nine days and a little), so "1.10.2000", and nothing is counted for it.
	/// </summary>
	/// <remarks><b>Mutations:</b> the machine's short date; the report put back.</remarks>
	[TestMethod]
	public void TheGadgetLettersTheParksDateWhenBuilt()
	{
		var counted = Unimplemented.Summary.FirstOrDefault( entry => entry.What == "GADGET_DATE_UITEXT_448" ).Times;

		try
		{
			GameCalendar.Rebase( 208 );
			var gadget = new UI.ParkGadget( AStack() );

			Assert.AreEqual( new DateTime( 2000, 1, 10 ), GameCalendar.Now.Date );
			Assert.AreEqual( "1.10.2000", Find( gadget.Root, 0x20 ).Text );
			Assert.AreEqual( counted, Unimplemented.Summary.FirstOrDefault( entry => entry.What == "GADGET_DATE_UITEXT_448" ).Times );
		}
		finally
		{
			GameCalendar.Rebase();
		}
	}

	private static UI.UiControl Find( UI.UiControl control, int id )
		=> control.Id == id ? control : control.Children.Select( child => Find( child, id ) ).FirstOrDefault( found => found != null )!;

	/// <summary>A window stack with no windows, made without its constructor.</summary>
	private static UI.WindowStack AStack()
	{
		var stack = (UI.WindowStack)RuntimeHelpers.GetUninitializedObject( typeof( UI.WindowStack ) );

		typeof( UI.WindowStack ).GetField( "_windows", BindingFlags.Instance | BindingFlags.NonPublic )!
			.SetValue( stack, new List<UI.UiWindow>() );

		return stack;
	}
}
