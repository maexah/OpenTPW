using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;
using System.Reflection;

namespace OpenTPW.Tests;

/// <summary>
/// A track add-on's put-down through the carry (<see cref="ParkBuilding.CarryFor"/>, <see cref="ParkBuilding.PlaceCarried(int, int, int?, bool)"/>)
/// on a kart loop, with the standing of the model (<see cref="ParkBuilding.StandThing"/>) made by the test, which has no
/// graphics device: the thing keeps the ride it is put down on in <c>mUpgradeParent</c> (<c>+0x30</c>), its walk of the ride's
/// circuit runs (<c>0x00524d0a</c>), and the hand's angle is 0 after it (<c>0x005259fc</c>).
/// </summary>
[TestClass]
public class ParkTrackAddOnPutDownTests
{
	private const int Tunnel = 1501;

	private static (ParkState State, ParkWorld.CatalogueObject Ride, ParkItemCatalogue Catalogue) Setup()
	{
		var (catalogue, _, _) = ParkFootprintPictureTests.BuyScreen();
		var (state, ride) = ParkTrackToolTests.Loop( 35 );

		typeof( Level ).GetProperty( nameof( Level.ParkState ) )!.SetValue( Level.Current, state );
		typeof( ParkState ).GetProperty( nameof( ParkState.Balance ) )!.SetValue( state, 90000 );

		return (state, ride, catalogue);
	}

	private static string PlaceAt( ParkState state, ParkItemCatalogue catalogue, int pointerX, int pointerY )
	{
		Assert.IsTrue( catalogue.TryGet( Tunnel, out var item ) );
		var carried = typeof( ParkBuilding ).GetMethod( "CarriedAnchorUnder", BindingFlags.NonPublic | BindingFlags.Static )!;
		object?[] args = [state, item, pointerX, pointerY, null];
		var (x, y) = ((int X, int Y))carried.Invoke( null, args )!;
		var place = typeof( ParkBuilding ).GetMethods( BindingFlags.NonPublic | BindingFlags.Static )
			.Single( method => method.Name == "PlaceCarried" && method.GetParameters().Length == 9 );

		return (string)place.Invoke( null, [state, Level.Current!.Park, catalogue, null, null, x, y, null, true] )!;
	}

	[TestMethod]
	public void ABoughtTunnelKeepsItsRideWalksItsCircuitAndTurnsTheHandToNought()
	{
		var (state, ride, catalogue) = Setup();
		var seen = new System.Collections.Generic.List<ParkWorld.CatalogueObject>();
		ParkBuilding.StandThing = placed => { seen.Add( placed ); return true; };

		try
		{
			StringAssert.Contains( ParkBuilding.CarryFor( Tunnel, ride.ThingId ), "holding" );
			Assert.AreEqual( ride.ThingId, ParkBuilding.CarryingFor );

			var answer = PlaceAt( state, catalogue, 34, 30 );

			Assert.AreEqual( 1, seen.Count, answer );
			Assert.AreEqual( (ushort)ride.ThingId, seen[0].UpgradeParent, "it keeps the ride it is on" );
			Assert.AreEqual( 1, state.TryObject( ride.ThingId, out var walked ) ? walked.IsTrackRideValid : -1, "the walk stored its answer" );
			Assert.AreEqual( 0, ParkBuilding.CarryingAngle, "the hand turned to nought" );

			// Another add-on bought from the placed one's window is for the placed one's ride (FUN_005276d0, 0x005277b3).
			StringAssert.Contains( ParkBuilding.CarryFor( Tunnel, seen[0].ThingId ), "holding" );
			Assert.AreEqual( ride.ThingId, ParkBuilding.CarryingFor, "the add-on names its ride, not itself" );
		}
		finally
		{
			ParkBuilding.StandThing = null;
		}
	}

	/// <summary>A second bought ride of the type, standing on its own cell with a straight run of its own track.</summary>
	private static ParkWorld.CatalogueObject SecondRide( ParkState state, int trackType, int x, int y )
	{
		var station = ParkTrackPieces.LayStation( state, trackType, x, y, 0 );
		var ride = new ParkWorld.CatalogueObject( state.NextThingId(), trackType == 1 ? 1150 : 1160, x << 8, y << 8, 0,
			TrackRide: state.TrackRides.Take( trackType == 1 ? -4 : -5 ) );
		state.AddObject( ride );
		ParkTrackTool.LayRun( state, ParkTrackPieces.AnchorOf( trackType ), (ushort)MapStep.CellId( x, y ), (x + 4, y + 1), (x + 4, y + 12), 200 );

		return ride;
	}

	private static string Click( ParkState state, int pointerX, int pointerY )
		=> ParkBuilding.PlaceCarriedUnder( pointerX, pointerY );

	/// <summary>The click judges the add-on for the hand's own ride: another ride's track refuses it and the hand keeps its ride (<c>0x00524a63</c>).</summary>
	[TestMethod]
	public void AnAddOnCarriedForOneRideClickedOverAnotherRidesTrackBuildsNothing()
	{
		var (state, rideA, _) = Setup();
		var rideB = SecondRide( state, 1, 50, 24 );
		var seen = new System.Collections.Generic.List<ParkWorld.CatalogueObject>();
		ParkBuilding.StandThing = placed => { seen.Add( placed ); return true; };

		try
		{
			var before = state.Objects.Count;
			var balance = state.Balance;
			ParkBuilding.CarryFor( Tunnel, rideA.ThingId );

			var answer = Click( state, 54, 30 );

			Assert.AreEqual( 0, seen.Count, answer );
			Assert.AreEqual( before, state.Objects.Count, answer );
			Assert.AreEqual( balance, state.Balance, answer );
			Assert.AreEqual( rideA.ThingId, ParkBuilding.CarryingFor, "the hand keeps its own ride" );
			Assert.AreNotEqual( rideB.ThingId, ParkBuilding.CarryingFor );
			Assert.AreNotEqual( 0, ParkBuilding.Carrying, "and the add-on is still in the hand" );
		}
		finally
		{
			ParkBuilding.StandThing = null;
		}
	}

	/// <summary>An add-on from the buy list (the hand's ride 0) is red on every track cell, so the click builds nothing there.</summary>
	[TestMethod]
	public void AnAddOnFromTheBuyListClickedOverARidesTrackBuildsNothing()
	{
		var (state, _, _) = Setup();
		var seen = new System.Collections.Generic.List<ParkWorld.CatalogueObject>();
		ParkBuilding.StandThing = placed => { seen.Add( placed ); return true; };

		try
		{
			var before = state.Objects.Count;
			ParkBuilding.Carry( Tunnel );

			var answer = Click( state, 34, 30 );

			Assert.AreEqual( 0, seen.Count, answer );
			Assert.AreEqual( before, state.Objects.Count, answer );
			Assert.AreEqual( 0, ParkBuilding.CarryingFor, "no ride is taken from the track" );
		}
		finally
		{
			ParkBuilding.StandThing = null;
		}
	}

	/// <summary>A kart add-on over the water ride's track is another ride's track, so it is refused.</summary>
	[TestMethod]
	public void AKartAddOnOverSplishSplashsTrackIsRefused()
	{
		var (state, rideA, _) = Setup();
		SecondRide( state, 2, 50, 24 );
		var seen = new System.Collections.Generic.List<ParkWorld.CatalogueObject>();
		ParkBuilding.StandThing = placed => { seen.Add( placed ); return true; };

		try
		{
			var before = state.Objects.Count;
			ParkBuilding.CarryFor( Tunnel, rideA.ThingId );

			var answer = Click( state, 54, 30 );

			Assert.AreEqual( 0, seen.Count, answer );
			Assert.AreEqual( before, state.Objects.Count, answer );
			Assert.AreEqual( rideA.ThingId, ParkBuilding.CarryingFor );
		}
		finally
		{
			ParkBuilding.StandThing = null;
		}
	}
}
