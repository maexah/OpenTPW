using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>Q93: a bought thing with a queue starts closed and its first joined measure opens it; a queue cut off later closes nothing.</summary>
[TestClass]
public class ParkBoughtClosedTests
{
	private ParkWorld world = null!;
	private ParkState state = null!;
	private ParkItemCatalogue catalogue = null!;
	private ParkRides rides = null!;
	private ParkPeople people = null!;
	private const int Bought = 9000;

	[TestInitialize]
	public void Open()
	{
		Log ??= new();
		FileSystem = GameData.Required();
		using var stream = new MemoryStream( FileSystem.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		world = new( new SaveReader( stream ).ReadFile() );
		state = new( world );
		catalogue = new( "jungle", FileSystem );
		rides = new( "jungle", world, catalogue, FileSystem );
		people = new( world, new ParkBalance( "jungle", easyMode: true ), null, state, catalogue,
			id => rides.Scheduler.Find( rides.ScriptFor( id ) ) );
	}

	[TestCleanup]
	public void Close()
	{
		people?.Delete();
		rides?.Delete();
		Entity.ApplyDeletions();
	}

	private ParkWorld.CatalogueObject Ride => state.Objects.Single( x => x.ThingId == Bought );
	private RideScript Script => rides.Scheduler.Find( rides.ScriptFor( Bought ) )!;

	private ParkItemCatalogue.Item Construct( int catalogueId )
	{
		Assert.IsTrue( catalogue.TryGet( catalogueId, out var item ) );
		state.AddObject( ParkBuilding.Constructed( item, Bought, 19, 11, 0,
			MapStep.CellId( 20, 11 ), MapStep.CellId( 20, 14 ), default ) );
		state.BindHoarding( Bought );
		return item;
	}

	[TestMethod]
	public void AQueuedPurchaseClosesTheNewScriptAndClearsItsNominee()
	{
		var item = Construct( 1100 );
		state.NominateForLoading( Bought, people.Peeps.First().ThingId );
		ParkBuilding.BindOperation( state, rides, Ride, item );
		Assert.IsNotNull( Script, "the close reaches a bound script" );
		Assert.AreEqual( 0, Ride.CanLoad );
		Assert.AreEqual( 1, Script[ParkRideOperation.ClosedVariable] );
		Assert.AreEqual( 0, state.PersonBeingLoaded( Bought ) );
		Assert.AreEqual( 0.2f, state.HoardingFor( Bought )!.Rate );
		Assert.AreEqual( 0, Ride.State, "ordinary closure keeps the operating state" );
		Assert.AreEqual( 5, Script[ParkRideOperation.CapacityVariable] );
	}

	[TestMethod]
	[DataRow( false )]
	[DataRow( true )]
	public void TheFirstConnectedQueueMeasureOpensTheBoundScript( bool connectedAtFirstMeasure )
	{
		var item = Construct( 1100 );
		ParkBuilding.BindOperation( state, rides, Ride, item );
		Assert.AreEqual( 0, Ride.CanLoad );
		ParkBuilding.MarkWaysInAndOut( state, world, 20, 11, 20, 14, 0x10, 0x10, 0, true,
			MapStep.CellId( 19, 11 ) );
		if ( connectedAtFirstMeasure )
			Connect();
		state.RemeasureQueue( Bought );
		Assert.AreEqual( connectedAtFirstMeasure ? 1 : 0, Ride.CanLoad );
		Assert.AreEqual( connectedAtFirstMeasure ? 0 : 1, Script[ParkRideOperation.ClosedVariable] );
		if ( !connectedAtFirstMeasure )
		{
			Connect();
			state.RemeasureQueue( Bought );
		}
		Assert.IsTrue( ParkRideOperation.BackOfQueueConnected( world, Ride ) );
		Assert.AreEqual( 1, Ride.CanLoad );
		Assert.AreEqual( 0, Script[ParkRideOperation.ClosedVariable] );
		Assert.AreEqual( -0.3f, state.HoardingFor( Bought )!.Rate );
	}


	private void StandAndConnect()
	{
		var item = Construct( 1100 );
		ParkBuilding.BindOperation( state, rides, Ride, item );
		ParkBuilding.Stamp( state, (19, 11, 21, 14), 19, 11 );
		state.EnterCell( 19, 11, Bought );
		ParkBuilding.MarkWaysInAndOut( state, world, 20, 11, 20, 14, 0x10, 0x10, 0, true,
			MapStep.CellId( 19, 11 ) );
		Connect();
		state.RemeasureQueue( Bought );
		Assert.AreEqual( 1, Ride.CanLoad );
	}

	private void AssertOpen()
	{
		Assert.AreEqual( 1, Ride.CanLoad );
		Assert.AreEqual( 0, Script[ParkRideOperation.ClosedVariable] );
		var hoarding = state.HoardingFor( Bought )!;
		hoarding.Advance( 6f );
		Assert.AreEqual( 0f, hoarding.Progress );
		Assert.IsFalse( hoarding.Active );
	}

	/// <summary>
	/// The original leaves an open ride open when its queue is cut off from the path, and its window reads
	/// status 22 (docs/exe/ride-operation.md, Q93): ClearCell measures entrances only, and nothing closes.
	/// </summary>
	[TestMethod]
	public void ClearingThePathAtTheQueueTailLeavesAnOpenRideOpen()
	{
		StandAndConnect();
		var measured = 0;
		var tell = state.QueueRemeasured;
		state.QueueRemeasured = id => { measured++; tell?.Invoke( id ); };
		Assert.IsTrue( ParkPathBuilding.ClearPathCell( state, world, 19, 10, stepped: true ) );
		Assert.IsFalse( ParkRideOperation.BackOfQueueConnected( world, Ride ) );
		Assert.AreEqual( 0, measured, "a path beside a queue cell is not an entrance: nothing is measured" );
		AssertOpen();
		Assert.AreEqual( 22, UI.ParkClosedStatus.For( world, Ride ) );
		Connect();
		state.RemeasureQueue( Bought );
		AssertOpen();
		Assert.AreEqual( 0, UI.ParkClosedStatus.For( world, Ride ) );
	}

	[TestMethod]
	public void AMeasureOfADisconnectedQueueKeepsTheDoorTheRidersAndTheNominee()
	{
		StandAndConnect();
		var guest = people.Peeps.First();
		guest.MajorDest = Bought;
		guest.SetState( PeepState.Riding, tick: 1, new System.Random( 1 ) );
		Script.Set( ParkRideOperation.OnRideVariable, 1 );
		var nominee = people.Peeps.Skip( 1 ).First().ThingId;
		state.NominateForLoading( Bought, nominee );
		var tail = state.Record( 20, 10 );
		state.SetRecord( 20, 10, tail with { Neighbours = tail.Direction } );
		state.RemeasureQueue( Bought );
		Assert.IsFalse( ParkRideOperation.BackOfQueueConnected( world, Ride ) );
		AssertOpen();
		Assert.AreEqual( nominee, state.PersonBeingLoaded( Bought ) );
		Assert.AreEqual( PeepState.Riding, guest.State );
		Assert.AreEqual( 1, Script[ParkRideOperation.OnRideVariable] );
		Assert.AreEqual( Bought, guest.MajorDest );
	}

	/// <summary><c>FUN_004df390</c> opens whatever its guard answers; the closed, disconnected status is 23.</summary>
	[TestMethod]
	public void AnOpenRequestOpensADisconnectedRide()
	{
		var item = Construct( 1100 );
		ParkBuilding.BindOperation( state, rides, Ride, item );
		Assert.AreEqual( 0, Ride.CanLoad );
		Assert.AreEqual( 23, UI.ParkClosedStatus.For( world, Ride ) );
		new ParkRideOperation( state, people.Guests ).Open( Script, Bought );
		Assert.AreEqual( 1, Ride.CanLoad );
		Assert.AreEqual( 0, Script[ParkRideOperation.ClosedVariable] );
		Assert.AreEqual( -0.3f, state.HoardingFor( Bought )!.Rate );
		Assert.AreEqual( 22, UI.ParkClosedStatus.For( world, Ride ) );
	}

	/// <summary>Loading a park closes nothing: a ride keeps the door its save gave it, queue joined or not.</summary>
	[TestMethod]
	[DataRow( 0 )]
	[DataRow( 1 )]
	public void ALoadedDisconnectedRideKeepsItsSavedDoor( int canLoad )
	{
		var item = Construct( 1100 );
		rides.BindNew( Ride, item );
		state.ReplaceObject( Ride with { CanLoad = canLoad } );
		people.Delete();
		Entity.ApplyDeletions();
		people = new( world, new ParkBalance( "jungle", easyMode: true ), null, state, catalogue,
			id => rides.Scheduler.Find( rides.ScriptFor( id ) ) );
		Assert.AreEqual( canLoad, Ride.CanLoad );
		Assert.AreEqual( 0, Script[ParkRideOperation.ClosedVariable], "the script is as it was bound" );
		Assert.AreEqual( 0f, state.HoardingFor( Bought )!.Rate );
	}

	private void Connect()
	{
		ParkPathBuilding.LayPathRun( state, world, 19, 10, 19, 10, 20 );
		ParkPathBuilding.JoinQueueToPath( state, world, 20, 10, 19, 10, Ride );
	}

	[TestMethod]
	public void APurchaseWithoutAQueueStaysOpen()
	{
		var item = Construct( 1203 );
		Assert.IsFalse( item.HasQueue );
		ParkBuilding.BindOperation( state, rides, Ride, item );
		Assert.IsNotNull( Script );
		Assert.AreEqual( 1, Ride.CanLoad );
		Assert.AreEqual( 0, Script[ParkRideOperation.ClosedVariable] );
		Assert.AreEqual( 0f, state.HoardingFor( Bought )!.Rate );
	}
}
