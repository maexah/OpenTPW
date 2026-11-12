using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace OpenTPW.Tests;

/// <summary>
/// The entrance arm of the camcorder's edge test (<see cref="ParkEntranceGate"/>; docs/exe/park-engine.md, "An
/// entrance is shut to the viewer"): the first catalogue object on the owner's cell decides alone, open only with a
/// ride view and <c>UsageInfo.CannotRide</c> nought.
/// </summary>
[TestClass]
public class ParkEntranceGateTests
{
	private const int BellyBounce = 1100;
	private const int JungleSpray = 1303;
	private const int DrinksShop = 1203;
	private const int BalloonShop = 1209;

	[TestCleanup]
	public void ForgetTheWalk() => ParkCamcorderCameraMode.Forget();

	/// <summary>
	/// <b>Lost Kingdom's eight entrances answer as the original's memory reads them</b> (<c>q140/orig/probe.log</c>):
	/// the Belly Bounce's is open, and the seven others shut.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> the view test taken out opens nothing more here (the seven without one cannot be ridden);
	/// <c>CannotRide</c> taken out opens the Jungle Spray's; the verdicts swapped fails all eight.
	/// </remarks>
	[TestMethod]
	public void TheEightEntrancesOfLostKingdomAnswerAsTheOriginals()
	{
		var world = Jungle();
		var catalogue = new ParkItemCatalogue( "jungle", GameData.Required() );

		WithState( world, state =>
		{
			var gate = ParkEntranceGate.For( state, catalogue );
			var verdicts = new Dictionary<(int, int), QueueVerdict>();

			for ( var y = 0; y < ParkWorld.MapSize; ++y )
			{
				for ( var x = 0; x < ParkWorld.MapSize; ++x )
				{
					if ( world.CellAt( x, y ).Type == CellEdge.RideEnd )
						verdicts[(x, y)] = gate.Ahead( world.CellAt( x, y ) );
				}
			}

			Assert.AreEqual( 8, verdicts.Count, "the park's type-9 cells" );
			Assert.AreEqual( QueueVerdict.LetThemThrough, verdicts[(52, 23)], "the Belly Bounce's" );

			foreach ( var shut in new[] { (52, 30), (43, 30), (55, 15), (55, 16), (55, 17), (58, 15), (44, 29) } )
				Assert.AreEqual( QueueVerdict.InTheWay, verdicts[shut], $"{shut}" );
		} );
	}

	/// <summary>
	/// <b>The view is the model's own node, id 1 in space <c>0x1000</c>.</b> The Belly Bounce's and the Jungle Spray's
	/// models carry it and the Drinks Shop's does not, so <c>CannotRide</c> alone shuts the Jungle Spray's entrance.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> the id asked as 2 finds none on the two; the space asked as <c>0x800</c>, a walk node's, finds
	/// one on the Balloon Shop, whose model has a walk node 1 and no view; a missing model taken as having one.
	/// </remarks>
	[TestMethod]
	public void TheViewNodeIsReadFromTheItemsModel()
	{
		var data = GameData.Required();
		var catalogue = new ParkItemCatalogue( "jungle", data );

		Assert.IsTrue( catalogue.TryGet( BellyBounce, out var bounce ) );
		Assert.IsTrue( catalogue.TryGet( JungleSpray, out var spray ) );
		Assert.IsTrue( catalogue.TryGet( DrinksShop, out var drinks ) );

		Assert.IsTrue( ParkEntranceGate.ModelHasViewNode( bounce, data ), "the Belly Bounce" );
		Assert.IsTrue( ParkEntranceGate.ModelHasViewNode( spray, data ), "the Jungle Spray" );
		Assert.IsFalse( ParkEntranceGate.ModelHasViewNode( drinks, data ), "the Drinks Shop" );

		Assert.IsTrue( catalogue.TryGet( BalloonShop, out var balloons ) );
		Assert.IsFalse( ParkEntranceGate.ModelHasViewNode( balloons, data ), "the Balloon Shop: a walk node 1, no view" );
		Assert.IsFalse( ParkEntranceGate.ModelHasViewNode( bounce with { Stem = "no-such-model" }, data ), "no model" );

		Assert.IsFalse( bounce.CannotRide );
		Assert.IsTrue( spray.CannotRide );
	}

	/// <summary>
	/// <b>The first thing on the owner's cell decides, and nothing after it is asked</b>; with none there the step
	/// goes back to the ordinary tests; and a thing that can be ridden but has no view is in the way.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> passing over a shut thing to the next (as <c>FUN_0042a340</c> does) opens the first case;
	/// the owner's cell not compared answers for a thing standing anywhere.
	/// </remarks>
	[TestMethod]
	public void TheFirstThingOnTheOwnersCellDecidesAlone()
	{
		var world = Jungle();
		var catalogue = new ParkItemCatalogue( "jungle", GameData.Required() );
		var entrance = world.CellAt( 52, 23 );

		var bounce = world.Objects.Single( thing => thing.CatalogueId == BellyBounce );
		var shop = world.Objects.Single( thing => thing.CatalogueId == DrinksShop ) with { RawX = bounce.RawX, RawY = bounce.RawY };

		Assert.AreEqual( (51, 23), (bounce.CellX, bounce.CellY), "the owner's cell" );

		QueueVerdict Ask( bool view, params ParkWorld.CatalogueObject[] things )
			=> new ParkEntranceGate( () => things, catalogue, item => view && item.Id == BellyBounce ).Ahead( entrance );

		Assert.AreEqual( QueueVerdict.InTheWay, Ask( true, shop, bounce ), "the shop first: shut, the ride never asked" );
		Assert.AreEqual( QueueVerdict.LetThemThrough, Ask( true, bounce, shop ), "the ride first" );
		Assert.AreEqual( QueueVerdict.InTheWay, Ask( false, bounce ), "a ride with no view" );
		Assert.AreEqual( QueueVerdict.NothingThere, Ask( true ), "nothing on the owner's cell" );
		Assert.AreEqual( QueueVerdict.NothingThere,
			Ask( true, bounce with { RawX = bounce.RawX + 256 } ), "a thing beside the owner's cell is not on it" );
	}

	/// <summary>
	/// <b>The three views that are not built are counted where the original would ask them</b>, and the model is
	/// read once an item.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> each report taken out; the cache taken out reads the model four times; a counted view taken as
	/// holding gives the thing with no node a view.
	/// </remarks>
	[TestMethod]
	public void TheUnbuiltViewsAreCountedAndTheModelIsReadOnce()
	{
		var world = Jungle();
		var data = GameData.Required();
		var catalogue = new ParkItemCatalogue( "jungle", data );

		var bounce = world.Objects.Single( thing => thing.CatalogueId == BellyBounce );
		Assert.IsTrue( catalogue.TryGet( BellyBounce, out var item ) );

		var read = 0;
		var gate = new ParkEntranceGate( () => [], catalogue, _ => ++read > 0, thing => thing == 999 );

		var car = Times( "RIDE_VIEW_TRACK_RIDE_LEAD_CAR" );
		var tour = Times( "RIDE_VIEW_TOUR_CAR" );
		var coaster = Times( "RIDE_VIEW_COASTER_NODE" );

		Assert.IsTrue( gate.HasView( bounce, item ) );
		Assert.AreEqual( (car, tour, coaster),
			(Times( "RIDE_VIEW_TRACK_RIDE_LEAD_CAR" ), Times( "RIDE_VIEW_TOUR_CAR" ), Times( "RIDE_VIEW_COASTER_NODE" )),
			"the Belly Bounce asks none of the three" );

		gate.HasView( bounce with { TrackRide = 7 }, item );
		Assert.AreEqual( car + 1, Times( "RIDE_VIEW_TRACK_RIDE_LEAD_CAR" ) );

		gate.HasView( bounce with { ThingId = 999 }, item );
		Assert.AreEqual( tour + 1, Times( "RIDE_VIEW_TOUR_CAR" ) );

		gate.HasView( bounce, item with { TrackType = ItemDescriptionFile.CoasterTrack } );
		Assert.AreEqual( coaster + 1, Times( "RIDE_VIEW_COASTER_NODE" ) );

		Assert.AreEqual( (car + 1, tour + 1), (Times( "RIDE_VIEW_TRACK_RIDE_LEAD_CAR" ), Times( "RIDE_VIEW_TOUR_CAR" )) );
		Assert.AreEqual( 1, read, "one item, read once" );

		var blind = new ParkEntranceGate( () => [], catalogue, _ => false, _ => true );

		Assert.IsFalse( blind.HasView( bounce with { TrackRide = 7 }, item with { TrackType = ItemDescriptionFile.CoasterTrack } ),
			"counted, and taken as not holding: the model's node alone answers" );
	}

	/// <summary>
	/// <b>The three walks of the game run, through the camcorder's own step.</b> At yaw 0 from the path the viewer parks
	/// at 299.999 against the Drinks Shop's entrance and the Jungle Spray's, as the original's (<c>q140/orig/a.log</c>);
	/// from the Belly Bounce's footprint they cross into its entrance, which is counted on each pass there: two as they
	/// cross and one for each of fourteen steps in the cell, before they walk on into the queue.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> the camcorder handing <see cref="CellEdge"/> no gate walks into both shops' entrances and
	/// stops at 240 in the footprint; a gate that always answers nothing there does the same.
	/// </remarks>
	[TestMethod]
	public void TheViewerIsHeldAtAShopsDoorAndWalksIntoTheBellyBounces()
	{
		var world = Jungle();
		var catalogue = new ParkItemCatalogue( "jungle", GameData.Required() );

		WithState( world, state =>
		{
			var level = (Level)RuntimeHelpers.GetUninitializedObject( typeof( Level ) );

			typeof( Level ).GetProperty( nameof( Level.ParkState ) )!.SetValue( level, state );
			typeof( Level ).GetProperty( nameof( Level.Catalogue ) )!.SetValue( level, catalogue );
			Level.Current = level;

			Vector3 Walk( float x, float y, float forward )
			{
				ParkCamcorderCameraMode.Stand = new Vector3( x, y, 0f );
				ParkCamcorderCameraMode.Yaw = 0f;
				ParkCamcorderCameraMode.DebugWalk( forward, 0f, 60 );

				return ParkCamcorderCameraMode.Stand;
			}

			Assert.AreEqual( new Vector3( 435f, 299.999f, 0f ), Walk( 435f, 283f, 1f ), "the Drinks Shop's door" );
			Assert.AreEqual( new Vector3( 525f, 299.999f, 0f ), Walk( 525f, 291f, 1f ), "the Jungle Spray's door" );

			var before = Times( "FIRST_PERSON_WALK_INTO_RIDE" );

			Assert.AreEqual( new Vector3( 525f, 220f, 0f ), Walk( 525f, 245f, -1f ), "through the Belly Bounce's entrance" );
			Assert.AreEqual( before + 16, Times( "FIRST_PERSON_WALK_INTO_RIDE" ) );
		} );
	}

	private static int Times( string what ) => Unimplemented.Summary.FirstOrDefault( entry => entry.What == what ).Times;

	private static ParkWorld Jungle()
	{
		var data = GameData.Required();

		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );

		return new ParkWorld( new SaveReader( stream ).ReadFile() );
	}

	/// <summary>A running park over <paramref name="world"/>, with the level and the park on show put back after.</summary>
	private static void WithState( ParkWorld world, Action<ParkState> ask )
	{
		var current = typeof( ParkState ).GetProperty( nameof( ParkState.Current ) )!;

		var levelBefore = Level.Current;
		var stateBefore = ParkState.Current;

		try
		{
			ask( new ParkState( world ) );
		}
		finally
		{
			Level.Current = levelBefore;
			current.SetValue( null, stateBefore );
		}
	}
}
