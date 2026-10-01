using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// A Full Simulation player is handed the Instant Action park, where the original builds them a fresh one, and that is
/// counted as <c>FULL_SIMULATION_NEW_PARK</c> (Q186; <c>docs/PLAYER-GAPS.md</c> gap 7).
///
/// <para>
/// <b>What this cannot see.</b> No test builds a park, so that <c>SetupParkEntities</c> calls the count rests on the
/// game run.
/// </para>
/// </summary>
[TestClass]
public class LevelFullSimulationParkTests
{
	[TestInitialize]
	public void NobodyPlaying()
	{
		Log ??= new();

		SetCurrentPlayer( null );
	}

	[TestCleanup]
	public void PutTheRosterBack() => SetCurrentPlayer( null );

	/// <summary><b>Only a Full Simulation player is counted</b>: not Instant Action, and not nobody (a console <c>park</c>).</summary>
	/// <remarks><b>Mutations:</b> the report taken out counts none; the player test taken out counts all three.</remarks>
	[TestMethod]
	public void OnlyAFullSimulationPlayerIsCounted()
	{
		var before = Times( "FULL_SIMULATION_NEW_PARK" );

		Level.CountAFullSimulationPark();
		Assert.AreEqual( before, Times( "FULL_SIMULATION_NEW_PARK" ), "nobody playing: a console park, not counted" );

		SetCurrentPlayer( new Player( 0, "Test", new PlayerFile { InstantAction = true } ) );
		Level.CountAFullSimulationPark();
		Assert.AreEqual( before, Times( "FULL_SIMULATION_NEW_PARK" ), "Instant Action is given its own park" );

		SetCurrentPlayer( new Player( 0, "Test", new PlayerFile { InstantAction = false } ) );
		Level.CountAFullSimulationPark();
		Assert.AreEqual( before + 1, Times( "FULL_SIMULATION_NEW_PARK" ), "Full Simulation is handed it, once" );
	}

	private static int Times( string what ) => Unimplemented.Summary.FirstOrDefault( entry => entry.What == what ).Times;

	private static void SetCurrentPlayer( Player? player )
		=> typeof( Players ).GetProperty( nameof( Players.Current ) )!.SetValue( Players.Roster, player );
}
