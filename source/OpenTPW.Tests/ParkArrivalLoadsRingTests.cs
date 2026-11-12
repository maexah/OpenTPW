using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// The arrival manager's ring of the last five loads on the park analyser - <c>docs/exe/hud.md</c>, "The arrivals":
/// each load's size as the load is called (<c>FUN_004cf3e0</c>, <c>0x004cf41f</c>..<c>0x004cf451</c>), read and written
/// whole (<c>FUN_004ce660</c>). Reads Lost Kingdom's save, and is skipped where there is no installation - see
/// <see cref="GameData"/>.
/// </summary>
[TestClass]
public class ParkArrivalLoadsRingTests
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

	/// <summary>The wait's mark put ahead of the clock, so the next sweep finds a load due (the wait wraps).</summary>
	private static void MakeALoadDue( ParkPeople people )
		=> typeof( ParkPeople ).GetField( "_arrivalMark", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic )!
			.SetValue( people, people.State.GameTick + 8 );

	/// <summary>
	/// <b>Each load called goes into the ring at its size, once</b>: the file's one load of 13 in slot 0; the sweep that
	/// calls a load puts its headcount in slot 1 before any guest is dropped, and the drops, the let-go and the month's
	/// close put nothing more. The console's load goes in as one the timer calls. Five loads on from slot 0 the ring has
	/// gone round to slot 0, and the writer is handed it as itself.
	/// </summary>
	/// <remarks><b>Mutations:</b> the push left out of the timer's call or the console's, made on a drop or the let-go, made twice; the size pushed after a drop; the file's own ring run on as itself; the ring not wrapped, or wrapped a slot early; the close pushing into it; the writer handed another ring.</remarks>
	[TestMethod]
	public void EachLoadCalledGoesIntoTheRingAtItsSize()
	{
		var world = new ParkWorld( Body() );
		var state = new ParkState( world );
		var people = new ParkPeople( world, new ParkBalance( "jungle", easyMode: true ), null, state );

		Time.Paused = false;
		Time.StepFrames = 0;
		GameClock.Rebase();
		Frame( 0f );

		Assert.AreEqual( (0, 13, false, 5), (state.ArrivalLoads.Current, state.ArrivalLoads.Newest, state.ArrivalLoads.Wrapped, state.ArrivalLoads.Length), "the file's" );

		MakeALoadDue( people );
		Sweep( people );

		Assert.IsTrue( people.LoadHeld, "a load called" );
		var size = people.StillToDrop;
		Assert.IsTrue( size > 1, $"a load of {size}" );
		Assert.AreEqual( (1, size), (state.ArrivalLoads.Current, state.ArrivalLoads.Newest), "its size as called, in the next slot" );
		Assert.AreEqual( (0, 13), (world.ArrivalLoads!.Current, world.ArrivalLoads.Newest), "the file's own ring as read" );

		for ( var sweep = 0; sweep < size + 3 && people.LoadHeld; ++sweep )
			Sweep( people );

		Assert.IsFalse( people.LoadHeld, "the load dropped and let go" );
		Assert.AreEqual( (1, size), (state.ArrivalLoads.Current, state.ArrivalLoads.Newest), "nothing more for the drops or the let-go" );

		ParkPeople.TurnTheMonth( people, state, null );
		Assert.AreEqual( 1, state.ArrivalLoads.Current, "nor for the month's close" );

		people.ForceArrival( 7 );
		Assert.AreEqual( (2, 7), (state.ArrivalLoads.Current, state.ArrivalLoads.Newest), "the console's load" );

		state.AddArrivalLoad( 21 );
		state.AddArrivalLoad( 22 );
		Assert.AreEqual( (4, 22, false), (state.ArrivalLoads.Current, state.ArrivalLoads.Newest, state.ArrivalLoads.Wrapped), "the fifth slot, not yet round" );

		state.AddArrivalLoad( 23 );
		Assert.AreEqual( (0, 23, true), (state.ArrivalLoads.Current, state.ArrivalLoads.Newest, state.ArrivalLoads.Wrapped), "round to the first" );
		Assert.AreSame( state.ArrivalLoads, state.MonthRings.ArrivalLoads, "the writer handed the ring as itself" );
	}

	/// <summary>A park made with no file starts the ring empty: five slots of nought, none called, as the original's own fresh parks write it.</summary>
	[TestMethod]
	public void AParkMadeWithNoFileHasCalledNoLoad()
	{
		var fresh = new ParkState( parkIsClosed: false, visitorsToDate: 0, balance: 0 );

		Assert.AreEqual( (-1, 0, false, 5), (fresh.ArrivalLoads.Current, fresh.ArrivalLoads.Newest, fresh.ArrivalLoads.Wrapped, fresh.ArrivalLoads.Length) );
	}

	/// <summary>
	/// <b>The ring reads and writes back whole</b>: thing 5's record at 72,235, after the sixteen month rings and the fifty
	/// stays, 33 bytes, one load of 13 in the shipped park and <c>0xcdcdcdcd</c> in the four slots no load reached. Written
	/// back with two loads more, it reads back as written, the slot no load reached still the file's, and the bytes that
	/// differ from the file's are the ring's alone.
	/// </summary>
	/// <remarks><b>Mutations:</b> the ring read or written at the stays' place or past it; read as fifty or written as fifty; the writer not handed it.</remarks>
	[TestMethod]
	public void TheShippedParksLoadsRingReadsAndWritesBack()
	{
		var shipped = new ParkWorld( Body() );
		var loads = shipped.ArrivalLoads!.Copy();

		Assert.AreEqual( (0, 13, false, 5), (loads.Current, loads.Newest, loads.Wrapped, loads.Length) );

		loads.Push( 41 );
		loads.Push( 12 );

		var same = ParkFileWriter.Body( shipped, new ParkFileWriter.Running( shipped.GameTick, false, 13, 0, shipped.Camera.Saved!.Value,
			MonthRings: new( shipped.MonthBalance!, shipped.MonthParkValue!, shipped.MonthCosts!, shipped.MonthStaffCosts!, shipped.MonthTraining!,
				shipped.MonthCashIn!, shipped.MonthGateTakings!, shipped.MonthShopTakings!, shipped.MonthSideshowTakings!, shipped.MonthGuestsInside!,
				shipped.MonthVisitors!, shipped.MonthRides!, shipped.MonthArrivals!, shipped.MonthHappiness!, shipped.MonthMeanStay!, shipped.LeaverStays!,
				shipped.MonthParkScore!, loads ) ) );
		var written = new ParkWorld( same );

		Assert.AreEqual( (2, 12, false), (written.ArrivalLoads!.Current, written.ArrivalLoads.Newest, written.ArrivalLoads.Wrapped) );

		var ring = shipped.AnalyserAt + 72235;

		Assert.AreEqual( 5, System.BitConverter.ToInt32( same, ring + 4 ), "mNumEntries" );
		Assert.AreEqual( (13, 41, 12, unchecked((int)0xcdcdcdcd)), (System.BitConverter.ToInt32( same, ring + 13 ), System.BitConverter.ToInt32( same, ring + 17 ),
			System.BitConverter.ToInt32( same, ring + 21 ), System.BitConverter.ToInt32( same, ring + 25 )), "the loads, and a slot no load reached" );

		var original = ParkFileWriter.Body( shipped, new ParkFileWriter.Running( shipped.GameTick, false, 13, 0, shipped.Camera.Saved!.Value ) );
		var differ = Enumerable.Range( 0, same.Length ).Where( at => same[at] != original[at] ).ToArray();

		Assert.IsTrue( differ.Length > 0 && differ.All( at => at >= ring && at < ring + ParkMonthRing.SizeOf( ParkMonthRing.Loads ) ), "the ring alone" );
	}
}
