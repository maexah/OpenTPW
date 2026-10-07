using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace OpenTPW.Tests;

/// <summary>
/// The all-visitors screen kept current in place, as the original's (<c>docs/exe/hud.md</c>, "How allpeeps keeps
/// itself current"): its two-second rewrite keeps the scroll, and a guest made or gone while it is open adds or removes
/// their row, until it closes. Reads the shipped park; skipped without the game (<see cref="GameData"/>).
/// </summary>
[TestClass]
public class ParkVisitorsScreenTests
{
	private float nowBefore;
	private Point2 screenBefore;
	private ParkPeople? people;

	[TestInitialize]
	public void MountTheGame()
	{
		screenBefore = Screen.Size;
		Screen.Size = new Point2( 2048, 1536 );

		Log ??= new();
		FileSystem = GameData.Required();
		nowBefore = Time.Now;
	}

	[TestCleanup]
	public void PutAwayWhatWasMade()
	{
		Time.Now = nowBefore;
		Time.PinWall( 0 );
		Screen.Size = screenBefore;

		people?.Delete();
		Entity.ApplyDeletions();
	}

	private static UI.WindowStack AStack()
	{
		var stack = (UI.WindowStack)RuntimeHelpers.GetUninitializedObject( typeof( UI.WindowStack ) );

		typeof( UI.WindowStack ).GetField( "_windows", BindingFlags.Instance | BindingFlags.NonPublic )!
			.SetValue( stack, new List<UI.UiWindow>() );

		return stack;
	}

	private static UI.UiList ListOf( UI.ParkVisitorsScreen screen )
		=> (UI.UiList)typeof( UI.ParkVisitorsScreen ).GetField( "_list", BindingFlags.Instance | BindingFlags.NonPublic )!
			.GetValue( screen )!;

	/// <summary>
	/// <b>The visitors list stands by Visitor Number as it opens, and a click on Cash Remaining sorts it by cash</b>
	/// (<c>[0x007508bc]</c>, <c>docs/exe/hud.md</c>, "A list's order"): every column is a number.
	/// </summary>
	/// <remarks><b>Mutations:</b> the list given no sort; the cash sorted as the visitor number; the word not kept.</remarks>
	[TestMethod]
	public void TheListSortsOnTheHeadingClicked()
	{
		var sort = typeof( UI.ParkVisitorsScreen ).GetField( "_sort", BindingFlags.Static | BindingFlags.NonPublic )!;

		using var stream = new MemoryStream( FileSystem.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		var world = new ParkWorld( new SaveReader( stream ).ReadFile() );

		people = new ParkPeople( world, new ParkBalance( "jungle", easyMode: true ), null, new ParkState( world ) );
		sort.SetValue( null, 1 );

		try
		{
			var stack = AStack();
			var list = ListOf( new UI.ParkVisitorsScreen( stack ) );

			int[] Cash() => list.Rows.Select( row => people!.Guests[row.Id].Cash ).ToArray();
			static bool Rising( int[] values ) => values.Zip( values.Skip( 1 ), ( a, b ) => a <= b ).All( ordered => ordered );

			Assert.AreEqual( 1, list.SortWord );
			Assert.IsTrue( Rising( list.Rows.Select( row => row.Value ).ToArray() ), "by visitor number" );
			Assert.IsFalse( Rising( Cash() ), "which is not by cash" );

			((UI.UiButton)list.Children.Single( child => child.Id == 0x11 )).Clicked!();

			Assert.IsTrue( Rising( Cash() ), "by cash" );
			Assert.AreEqual( 2, ListOf( new UI.ParkVisitorsScreen( stack ) ).SortWord, "the next screen opened keeps it" );
		}
		finally
		{
			sort.SetValue( null, 1 );
		}
	}

	/// <remarks>
	/// <b>Mutations:</b> the rewrite clearing and refilling the list (the build before Q200b) throws the top row back to
	/// nought; no subscription leaves the row count unmoved by an arrival; no unsubscription keeps adding rows after
	/// the screen closes; the first rewrite timed from nought and not from the opening; each later one too; the next
	/// tick a period after a late one; the timer run under a message box, or not stamped afresh as the box goes.
	/// </remarks>
	[TestMethod]
	public void TheListKeepsItsScrollAndFollowsGuestsWhileOpen()
	{
		using var stream = new MemoryStream( FileSystem.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		var world = new ParkWorld( new SaveReader( stream ).ReadFile() );
		var state = new ParkState( world );

		people = new ParkPeople( world, new ParkBalance( "jungle", easyMode: true ), null, state );

		for ( var n = 0; n < 20; ++n )
			Assert.AreNotEqual( 0, people.Admit( 42, 5 ), "the park takes a guest at the bus stop" );

		// Made and opened five seconds into real time: the rewrite is two seconds from there, not from nought.
		Time.PinWall( 5000 );

		var stack = AStack();
		var screen = new UI.ParkVisitorsScreen( stack );
		var list = ListOf( screen );

		stack.Open( screen );

		Assert.AreEqual( people.Peeps.Count, list.Rows.Count, "a row a guest" );
		Assert.IsTrue( list.Scrolls, "more guests than rows fit" );

		list.Scroll( 4 );
		var first = list.Rows[4].Id;

		// The rewrite's two seconds are real time (a control's timer, FUN_00661fe5), whatever the frame clock does.
		var values = list.Rows[4].Values;
		Time.PinWall( 6999 );
		Time.Now += 10f;
		screen.Update();
		Assert.AreSame( values, list.Rows[4].Values, "1999 ms of real time, ten seconds on the frame clock: not rewritten" );

		Time.PinWall( 7000 );
		screen.Update();
		Assert.AreNotSame( values, list.Rows[4].Values, "2000 ms: rewritten" );

		values = list.Rows[4].Values;
		Time.PinWall( 8999 );
		screen.Update();
		Assert.AreSame( values, list.Rows[4].Values, "and not again for two seconds" );

		Time.PinWall( 9000 );
		screen.Update();
		Assert.AreNotSame( values, list.Rows[4].Values, "then again" );

		// A late frame does not move the timer's phase (FUN_00661fe5 leaves the stamp the remainder behind): a tick
		// taken 100 ms late is followed by one 1900 ms on.
		Time.PinWall( 11100 );
		screen.Update();
		values = list.Rows[4].Values;
		Time.PinWall( 12999 );
		screen.Update();
		Assert.AreSame( values, list.Rows[4].Values, "1899 ms after a late tick: not yet" );
		Time.PinWall( 13000 );
		screen.Update();
		Assert.AreNotSame( values, list.Rows[4].Values, "on the period's own beat" );

		// Under a message box the timers are held, and stamped afresh as it goes (FUN_006622d2, FUN_00662420).
		var box = new UI.MessageBox( stack, "held", () => { } );

		stack.Open( box );
		Assert.IsTrue( stack.HoldsTimers );
		values = list.Rows[4].Values;
		Time.PinWall( 20000 );
		screen.Update();
		Assert.AreSame( values, list.Rows[4].Values, "seven seconds under a message box: not rewritten" );

		stack.Close( box );
		Assert.IsFalse( stack.HoldsTimers );

		Time.PinWall( 20500 );
		screen.Update();
		Assert.AreSame( values, list.Rows[4].Values, "nor as it goes: the period starts again there" );
		Time.PinWall( 22499 );
		screen.Update();
		Assert.AreSame( values, list.Rows[4].Values );
		Time.PinWall( 22500 );
		screen.Update();
		Assert.AreNotSame( values, list.Rows[4].Values, "two seconds after the box went" );

		Assert.AreEqual( 4, list.ScrollTop, "the two-second rewrite keeps the scroll" );
		Assert.AreEqual( first, list.Rows[4].Id, "and the row there" );

		var id = people.Admit( 42, 5 );
		Assert.AreEqual( people.Peeps.Count, list.Rows.Count, "an arrival's row added" );
		Assert.AreEqual( 4, list.ScrollTop, "without scrolling" );

		Assert.IsTrue( people.Depart( id ) );
		Assert.AreEqual( people.Peeps.Count, list.Rows.Count, "and removed as they go" );

		stack.Close( screen );
		var rows = list.Rows.Count;

		people.Admit( 42, 5 );
		Assert.AreEqual( rows, list.Rows.Count, "a closed list hears of no one" );

		// The park's map holds the timers too (FUN_005f0b40, 0x005f0b8a).
		stack.Open( (UI.ParkMapScreen)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject( typeof( UI.ParkMapScreen ) ) );
		Assert.IsTrue( stack.HoldsTimers, "the map is up" );
	}
}
