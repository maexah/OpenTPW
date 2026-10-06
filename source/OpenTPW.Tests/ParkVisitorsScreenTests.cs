using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.IO;
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

	/// <remarks>
	/// <b>Mutations:</b> the rewrite clearing and refilling the list (the build before Q200b) throws the top row back to
	/// nought; no subscription leaves the row count unmoved by an arrival; no unsubscription keeps adding rows after
	/// the screen closes.
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
		Time.PinWall( 1999 );
		Time.Now += 10f;
		screen.Update();
		Assert.AreSame( values, list.Rows[4].Values, "1999 ms of real time, ten seconds on the frame clock: not rewritten" );

		Time.PinWall( 2000 );
		screen.Update();
		Assert.AreNotSame( values, list.Rows[4].Values, "2000 ms: rewritten" );

		Time.PinWall( 3000 );
		screen.Update();

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
	}
}
