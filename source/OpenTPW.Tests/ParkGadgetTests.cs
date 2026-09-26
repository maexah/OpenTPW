using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace OpenTPW.Tests;

/// <summary>
/// The park gadget's research button, <c>0x2b</c>, whose screen and message boxes are not built: each click is counted as
/// <c>RESEARCH_BUTTON</c> (<c>docs/exe/hud.md</c>, "The nine screens behind Info, Money and Research"). These read the
/// gadget's meshes from the shipped game and are skipped where there is no installation - see <see cref="GameData"/>.
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
	/// <b>Each click on the research button is counted, and opens nothing</b>; a click on the gauge beside it, which
	/// takes the press and has nothing to do, counts none.
	/// </summary>
	/// <remarks><b>Mutations:</b> the report taken out counts none; one on every release counts the gauge's click.</remarks>
	[TestMethod]
	public void EachClickOnResearchIsCounted()
	{
		var stack = AStack();
		stack.Open( new UI.ParkGadget( stack ) );

		var before = Times( "RESEARCH_BUTTON" );

		Assert.IsTrue( stack.ClickAt( 333, 1313 ), "the middle of 0x2b's (274,1254)-(392,1372)" );
		Assert.AreEqual( before + 1, Times( "RESEARCH_BUTTON" ) );

		Assert.IsTrue( stack.ClickAt( 333, 1313 ) );
		Assert.AreEqual( before + 2, Times( "RESEARCH_BUTTON" ), "every click, not the first alone" );

		Assert.IsTrue( stack.ClickAt( 115, 1200 ), "the gauge, 0x1e" );
		Assert.AreEqual( before + 2, Times( "RESEARCH_BUTTON" ), "the gauge's click is not research's" );

		CollectionAssert.AreEqual( new[] { "ParkGadget" }, stack.Windows.Select( window => window.GetType().Name ).ToArray(),
			"nothing opened" );
	}

	private static int Times( string what ) => Unimplemented.Summary.FirstOrDefault( entry => entry.What == what ).Times;

	/// <summary>A window stack with no windows, made without its constructor.</summary>
	private static UI.WindowStack AStack()
	{
		var stack = (UI.WindowStack)RuntimeHelpers.GetUninitializedObject( typeof( UI.WindowStack ) );

		typeof( UI.WindowStack ).GetField( "_windows", BindingFlags.Instance | BindingFlags.NonPublic )!
			.SetValue( stack, new List<UI.UiWindow>() );

		return stack;
	}
}
