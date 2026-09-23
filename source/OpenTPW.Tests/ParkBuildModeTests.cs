using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenTPW.Tests;

/// <summary>
/// The list of run ends Backspace pops - <see cref="ParkBuildMode.Pending"/>, the original's list at
/// <c>0x0081b740</c>. It needs no game.
/// </summary>
[TestClass]
public class ParkBuildModeTests
{
	[TestInitialize]
	[TestCleanup]
	public void ForgetTheTool() => ParkBuildMode.Forget();

	/// <summary>
	/// A pop takes the top off and hands back the new top as the run's start. <b>The tool's first cell is
	/// never popped</b>: with one entry, or none, nothing happens.
	/// </summary>
	[TestMethod]
	public void BackspacePopsOneRunAndNeverTheFirstClick()
	{
		Assert.IsFalse( ParkBuildMode.TryPopRun( out _, out _ ), "an empty list has no run to undo" );

		ParkBuildMode.Push( 42, 24 );
		Assert.IsFalse( ParkBuildMode.TryPopRun( out _, out _ ), "the first click alone is not a run" );
		Assert.AreEqual( 1, ParkBuildMode.Pending.Count, "and it stays" );

		ParkBuildMode.Push( 45, 24 );
		ParkBuildMode.Push( 45, 26 );

		Assert.IsTrue( ParkBuildMode.TryPopRun( out var end, out var start ) );
		Assert.AreEqual( (45, 26), end, "the last run's end comes off" );
		Assert.AreEqual( (45, 24), start, "and it started where the list's top now is" );

		Assert.IsTrue( ParkBuildMode.TryPopRun( out end, out start ) );
		Assert.AreEqual( ((45, 24), (42, 24)), (end, start) );

		Assert.IsFalse( ParkBuildMode.TryPopRun( out _, out _ ), "back to the first click" );
		Assert.AreEqual( (42, 24), ParkBuildMode.Pending[0] );
	}

	/// <summary>Arming does not touch the list - only putting the park away does.</summary>
	[TestMethod]
	public void ArmingKeepsTheListAndForgettingClearsIt()
	{
		ParkBuildMode.Push( 1, 2 );
		ParkBuildMode.Arm( ParkBuildMode.Path );
		ParkBuildMode.ArmAt( ParkBuildMode.Queue, 9, 3, 4 );

		Assert.AreEqual( 1, ParkBuildMode.Pending.Count, "the two setters leave it alone" );

		ParkBuildMode.Forget();

		Assert.AreEqual( 0, ParkBuildMode.Pending.Count );
	}

	/// <summary>The list holds 1,024 ends and refuses the next.</summary>
	[TestMethod]
	public void TheListStopsAtItsSize()
	{
		for ( var i = 0; i < 1100; ++i )
			ParkBuildMode.Push( i % 128, i / 128 );

		Assert.AreEqual( 0x400, ParkBuildMode.Pending.Count );
	}
}
