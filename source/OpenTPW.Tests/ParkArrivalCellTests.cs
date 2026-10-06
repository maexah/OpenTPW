using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace OpenTPW.Tests;

/// <summary>
/// Where a load's guests are made (<c>FUN_004cf720</c>; <c>docs/exe/park.md</c>, "Arrivals"): every one at the second
/// bus stop, and two rows nearer the edge for a vehicle other than the small crowd's.
/// </summary>
[TestClass]
public class ParkArrivalCellTests
{
	/// <summary>
	/// <b>The cell is stop B for the bus and two rows less for the other two</b>, whatever the stop is.
	/// </summary>
	/// <remarks><b>Mutations:</b> the larger vehicles at stop B; the bus two rows out; the rows added, or one row; the row held to 3 or more.</remarks>
	[TestMethod]
	public void TheCellIsStopBAndTwoRowsOutForALargerVehicle()
	{
		Assert.AreEqual( (53, 5), ParkPeople.ArrivalCell( (53, 5), 1 ), "the bus" );
		Assert.AreEqual( (53, 3), ParkPeople.ArrivalCell( (53, 5), 2 ), "the second vehicle" );
		Assert.AreEqual( (53, 3), ParkPeople.ArrivalCell( (53, 5), 3 ), "the third" );
		Assert.AreEqual( (10, 18), ParkPeople.ArrivalCell( (10, 20), 2 ), "another park's stop" );
		Assert.AreEqual( (7, 1), ParkPeople.ArrivalCell( (7, 3), 3 ), "two rows whatever the row: nothing holds it on the map" );
		Assert.AreEqual( (53, 5), ParkPeople.ArrivalCell( (53, 5), 0 ), "no vehicle current: the test answers as for the bus" );
	}

	/// <summary>
	/// <b>Every guest of a load is made on that cell, in Lost Kingdom's own park</b>: three by bus at (53,5), on three
	/// sweeps running, none at the first stop (42,5); forty by the second vehicle at (53,3). No vehicle's script is
	/// bound here, so the guests come without waiting for one: what is pinned is the load's vehicle number reaching
	/// the cell, not a vehicle standing at the stop, which the game run shows.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> the stops taken in turn by the sweep's parity; the first stop used; the vehicle not asked, so
	/// a larger load lands at (53,5).
	/// </remarks>
	[TestMethod]
	public void EveryGuestOfALoadIsMadeAtStopB()
	{
		var data = GameData.Required();
		FileSystem = data;

		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		var world = new ParkWorld( new SaveReader( stream ).ReadFile() );

		using var clock = new SimulationClockScope();
		var people = new ParkPeople( world, new ParkBalance( "jungle", easyMode: true ) );

		var cells = new List<string>();
		Logger.LogDelegate note = ( _, text ) =>
		{
			if ( Regex.Match( text, @"guest \d+ arrived at (\(\d+,\d+\))" ) is { Success: true } made )
				cells.Add( made.Groups[1].Value );
		};

		Logger.OnLog += note;

		try
		{
			Assert.AreEqual( 1, people.ForceArrival( 3 ), "three come by bus" );

			for ( var sweep = 0; sweep < 6; ++sweep )
			{
				SimulationClockScope.Frame( GameClock.TickSeconds * 8 );
				people.Update();
			}

			CollectionAssert.AreEqual( new[] { "(53,5)", "(53,5)", "(53,5)" }, cells, "the bus's three, all at stop B" );

			cells.Clear();
			Assert.AreEqual( 2, people.ForceArrival( 40 ), "forty come by the second vehicle" );

			for ( var sweep = 0; sweep < 45; ++sweep )
			{
				SimulationClockScope.Frame( GameClock.TickSeconds * 8 );
				people.Update();
			}

			Assert.AreEqual( 40, cells.Count, "all forty made" );
			Assert.IsTrue( cells.All( cell => cell == "(53,3)" ), $"all two rows out: {string.Join( " ", cells.Distinct() )}" );
		}
		finally
		{
			Logger.OnLog -= note;
			people.Delete();
			Entity.ApplyDeletions();
		}
	}

	/// <summary>
	/// <b>A park with no balance to read its stops from makes its arrivals at Lost Kingdom's second stop</b>, (53,5),
	/// this build's fallback and not the original's (which has no park without a balance).
	/// </summary>
	/// <remarks><b>Mutations:</b> the fallback moved to the first stop.</remarks>
	[TestMethod]
	public void WithNoBalanceTheLoadIsMadeAtLostKingdomsSecondStop()
	{
		var data = GameData.Required();
		FileSystem = data;

		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		var world = new ParkWorld( new SaveReader( stream ).ReadFile() );

		using var clock = new SimulationClockScope();
		var people = new ParkPeople( world );

		var cells = new List<string>();
		Logger.LogDelegate note = ( _, text ) =>
		{
			if ( Regex.Match( text, @"guest \d+ arrived at (\(\d+,\d+\))" ) is { Success: true } made )
				cells.Add( made.Groups[1].Value );
		};

		Logger.OnLog += note;

		try
		{
			Assert.IsNull( people.Admission, "no balance, no stops" );
			people.ForceArrival( 1 );

			for ( var sweep = 0; sweep < 3; ++sweep )
			{
				SimulationClockScope.Frame( GameClock.TickSeconds * 8 );
				people.Update();
			}

			CollectionAssert.AreEqual( new[] { "(53,5)" }, cells );
		}
		finally
		{
			Logger.OnLog -= note;
			people.Delete();
			Entity.ApplyDeletions();
		}
	}
}
