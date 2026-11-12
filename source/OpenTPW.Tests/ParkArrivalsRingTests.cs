using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// The park analyser's month of arrivals - <c>docs/exe/hud.md</c>, "The arrivals": the 13th int ring (<c>+0x20cc0</c>),
/// one for every guest the load's vehicle drops off (<c>FUN_004cf3e0</c>), closed with the money rings. Reads Lost
/// Kingdom's save, and is skipped where there is no installation - see <see cref="GameData"/>.
/// </summary>
[TestClass]
public class ParkArrivalsRingTests
{
	private BaseFileSystem? data;

	private BaseFileSystem Data() => data ??= FileSystem = GameData.Required();

	[TestCleanup]
	public void LetThePeopleGo() => TestRun.DeleteEvery<ParkPeople>();

	private byte[] Body()
	{
		using var stream = new MemoryStream( Data().ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		return new SaveReader( stream ).ReadFile();
	}

	private static void Frame( float seconds )
	{
		Time.Update( seconds );
		GameClock.Update( paused: false, GameClock.ParkCatchUp );
	}

	/// <summary>One thing sweep: frames of a tenth of a second until the park's clock has gone one up.</summary>
	private static void Sweep( ParkPeople people )
	{
		var from = people.State.GameTick;

		for ( var frame = 0; frame < 10 && people.State.GameTick == from; ++frame )
		{
			Frame( 0.1f );
			ParkFrame.Run( people );
		}

		Assert.AreEqual( from + 1, people.State.GameTick, "one sweep" );
	}

	/// <summary>
	/// <b>A park ended in this session drops nobody</b>: the arrival manager reads the running world state, not the file's
	/// (0 here), so a load of three held as the park ends makes no guest and counts no arrival; the original's load of
	/// nobody and its vehicle are counted, <c>ARRIVALS_IN_AN_ENDED_PARK</c>. <see cref="EachGuestDroppedIsAnArrivalOfTheMonth"/>
	/// is the same load in a running park.
	/// </summary>
	/// <remarks><b>Mutation:</b> the arrival manager reading the file's state (<c>park.WorldState</c>).</remarks>
	[TestMethod]
	public void AParkEndedInTheSessionDropsNobody()
	{
		var world = new ParkWorld( Body() );
		var state = new ParkState( world );
		var people = new ParkPeople( world, new ParkBalance( "jungle", easyMode: true ), null, state );

		Time.Paused = false;
		Time.StepFrames = 0;
		GameClock.Rebase();
		Frame( 0f );

		people.HoldTheLoad( 3 );
		state.EndThePark();

		var (guests, counted) = (people.Peeps.Count, Unimplemented.Summary.FirstOrDefault( gap => gap.What == "ARRIVALS_IN_AN_ENDED_PARK" ).Times);

		for ( var i = 0; i < 4; ++i )
			Sweep( people );

		Assert.AreEqual( (0, 0, guests), (world.WorldState, state.MonthArrivals.Total, people.Peeps.Count) );
		Assert.IsTrue( Unimplemented.Summary.FirstOrDefault( gap => gap.What == "ARRIVALS_IN_AN_ENDED_PARK" ).Times > counted );
	}

	/// <summary>
	/// <b>Each guest the vehicle drops adds one to the month</b>, as it is made: a load of three held, the first sweep
	/// summons the bus (no script here, so it answers unloading), the next three make a guest apiece and the month goes
	/// 1, 2, 3, and the sweep that lets the load go adds nothing. The close then puts the three on the money rings' slot
	/// and starts the month at nought, and the writer is handed the ring as itself. A stop off the map ends the load with
	/// nobody made or counted.
	/// </summary>
	/// <remarks><b>Mutations:</b> the add left out, or made on the summons, the let-go or a refused stop; added twice; the ring not closed, or closed onto another slot; the writer handed another ring.</remarks>
	[TestMethod]
	public void EachGuestDroppedIsAnArrivalOfTheMonth()
	{
		var world = new ParkWorld( Body() );
		var state = new ParkState( world );
		var people = new ParkPeople( world, new ParkBalance( "jungle", easyMode: true ), null, state );

		Time.Paused = false;
		Time.StepFrames = 0;
		GameClock.Rebase();
		Frame( 0f );

		Assert.AreEqual( (0, 13, 0), (state.MonthArrivals.Current, state.MonthArrivals.Newest, state.MonthArrivals.Total), "the file's" );

		people.HoldTheLoad( 3 );
		var guests = people.Peeps.Count;

		Sweep( people );
		Assert.AreEqual( (0, guests), (state.MonthArrivals.Total, people.Peeps.Count), "the summons drops nobody" );

		for ( var dropped = 1; dropped <= 3; ++dropped )
		{
			Sweep( people );
			Assert.AreEqual( (dropped, guests + dropped), (state.MonthArrivals.Total, people.Peeps.Count), $"guest {dropped} made and counted" );
		}

		Sweep( people );
		Assert.IsFalse( people.LoadHeld, "the load let go" );
		Assert.AreEqual( 3, state.MonthArrivals.Total, "on a sweep that drops nobody" );

		ParkPeople.TurnTheMonth( people, state, null );

		Assert.AreEqual( (1, 3, 0), (state.MonthArrivals.Current, state.MonthArrivals.Newest, state.MonthArrivals.Total) );
		Assert.AreEqual( state.MonthCosts.Current, state.MonthArrivals.Current, "on the money rings' slot" );
		Assert.AreSame( state.MonthArrivals, state.MonthRings.Arrivals, "the writer handed the 13th as itself" );

		// A stop off the map makes nobody: this game's own end of the load, and nobody counted.
		typeof( ParkAdmission ).GetField( $"<{nameof( ParkAdmission.BusStopB )}>k__BackingField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic )!
			.SetValue( people.Admission!, (53, -10) );
		people.HoldTheLoad( 4 );
		guests = people.Peeps.Count;

		Sweep( people );
		Assert.AreEqual( 4, people.StillToDrop, "the bus summoned again" );
		Sweep( people );
		Assert.AreEqual( (0, 0, guests), (people.StillToDrop, state.MonthArrivals.Total, people.Peeps.Count), "the load ended, nobody made or counted" );
	}

	/// <summary>A park made with no file starts the month and its ring empty.</summary>
	[TestMethod]
	public void AParkMadeWithNoFileHasNoArrivals()
	{
		var fresh = new ParkState( parkIsClosed: false, visitorsToDate: 0, balance: 0 );

		Assert.AreEqual( (-1, 0, 0), (fresh.MonthArrivals.Current, fresh.MonthArrivals.Newest, fresh.MonthArrivals.Total) );
	}

	/// <summary>
	/// <b>The 13th ring reads and writes back</b>: thing 5's record at 62,598 + 12 x 589, one month closed on the shipped
	/// park's thirteen guests, nothing so far. Written back with a month so far and a month closed, it reads back as
	/// written, at its own place and nowhere else.
	/// </summary>
	/// <remarks><b>Mutations:</b> the ring read or written as its neighbour, the 12th or the 14th; the writer not handed it.</remarks>
	[TestMethod]
	public void TheShippedParksArrivalsRingReadsAndWritesBack()
	{
		var shipped = new ParkWorld( Body() );
		var arrivals = shipped.MonthArrivals!.Copy();

		Assert.AreEqual( (0, 13, 0), (arrivals.Current, arrivals.Newest, arrivals.Total) );

		arrivals.Add( 41 );
		arrivals.Close();
		arrivals.Add( 7 );

		var same = ParkFileWriter.Body( shipped, new ParkFileWriter.Running( shipped.GameTick, false, 13, 0, shipped.Camera.Saved!.Value,
			MonthRings: new( shipped.MonthBalance!, shipped.MonthParkValue!, shipped.MonthCosts!, shipped.MonthStaffCosts!, shipped.MonthTraining!,
				shipped.MonthCashIn!, shipped.MonthGateTakings!, shipped.MonthShopTakings!, shipped.MonthSideshowTakings!, shipped.MonthGuestsInside!,
				shipped.MonthVisitors!, shipped.MonthRides!, arrivals, shipped.MonthHappiness!, shipped.MonthMeanStay!, shipped.LeaverStays!, shipped.MonthParkScore!, shipped.ArrivalLoads! ) ) );
		var written = new ParkWorld( same );

		Assert.AreEqual( (1, 41, 7), (written.MonthArrivals!.Current, written.MonthArrivals.Newest, written.MonthArrivals.Total) );

		var original = ParkFileWriter.Body( shipped, new ParkFileWriter.Running( shipped.GameTick, false, 13, 0, shipped.Camera.Saved!.Value ) );
		var differ = Enumerable.Range( 0, same.Length ).Where( at => same[at] != original[at] ).ToArray();
		var thirteenth = shipped.AnalyserAt + 62598 + 12 * ParkMonthRing.Size;

		Assert.IsTrue( differ.Length > 0 && differ.All( at => at >= thirteenth && at < thirteenth + ParkMonthRing.Size ), "the 13th ring alone" );
	}
}
