using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenTPW.Tests;

/// <summary>
/// Where a thing's body may stand (Q37): the placer's test pass asks the verdict <c>FUN_00535670</c> in mode 4 of each
/// cell its picture marks, and that passes only in-park bare ground or path, not NOMODIFY, with no track under it
/// (<see cref="ParkBuilding.BodyCellRefusal"/>; <c>docs/exe/park-engine.md</c>, "Where a thing may stand"). These read the
/// shipped Lost Kingdom and are skipped where there is no installation.
/// </summary>
[TestClass]
public class ParkPlacementTerrainTests
{
	private BaseFileSystem data = null!;
	private ParkWorld shipped = null!;
	private ParkItemCatalogue catalogue = null!;

	/// <summary>Every shipped ride's and shop's <c>Info.OverwritePriority</c>.</summary>
	private const int RidePriority = 5;

	private const int BellyBounceItem = 1100;

	[TestInitialize]
	public void ReadTheShippedPark()
	{
		Log ??= new();
		data = GameData.Required();
		FileSystem = data;
		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		shipped = new ParkWorld( new SaveReader( stream ).ReadFile() );
		catalogue = new ParkItemCatalogue( "jungle", data );
		Unimplemented.Forget();
	}

	private static readonly System.Func<ParkState, ushort, int?> LevelPriority = ParkTrackTool.OverwritePriorityOf;

	[TestCleanup]
	public void Forget()
	{
		ParkBuilding.StandThing = null;
		ParkTrackTool.OverwritePriorityOf = LevelPriority;
		Unimplemented.Forget();
	}

	private static int TimesReported( string what )
		=> Unimplemented.Summary.FirstOrDefault( entry => entry.What == what ).Times;

	/// <summary>
	/// <b>One cell of each kind the verdict tells apart</b>, each one a rule that is not the original's gets wrong: a path and bare
	/// ground pass; land outside the park, rock inside it, NOMODIFY (on path and on bare ground), a queue, a thing's cell, track,
	/// type <c>0x15</c> and the boundary refuse.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> refusing a path (the old rule) fails (40,21); dropping the outside-the-park test passes (43,16); dropping
	/// NOMODIFY passes (47,21) and the flagged bare cell; letting any type through passes (45,17); dropping the track test
	/// passes the kart cell.
	/// </remarks>
	[TestMethod]
	public void TheBodyVerdictTakesOnlyInParkBareGroundOrPath()
	{
		var state = new ParkState( shipped );

		string? At( int x, int y ) => ParkBuilding.BodyCellRefusal( state, x, y, RidePriority );

		Assert.IsNull( At( 60, 30 ), "in-park bare ground" );
		Assert.AreEqual( CellEdge.Path, state.Record( 40, 21 ).Type );
		Assert.IsNull( At( 40, 21 ), "a path the park laid" );

		StringAssert.Contains( At( 43, 16 ), "is outside the park" );
		StringAssert.Contains( At( 45, 17 ), "is type 7" );
		StringAssert.Contains( At( 47, 21 ), "is marked NOMODIFY" );
		StringAssert.Contains( At( 50, 22 ), "is a queue" );
		StringAssert.Contains( At( 52, 24 ), "already has something built on it" );

		var bare = state.Record( 60, 30 );

		state.SetRecord( 60, 30, bare with { Flags = ParkPathBuilding.NoModify } );
		StringAssert.Contains( At( 60, 30 ), "is marked NOMODIFY", "NOMODIFY refuses mode 4 on any type (0x005356aa)" );

		state.SetRecord( 60, 30, bare with { TrackType = ParkTrackPieces.KartAnchor } );
		StringAssert.Contains( At( 60, 30 ), "is part of a track" );

		state.SetRecord( 60, 30, bare with { Type = 0x15 } );
		StringAssert.Contains( At( 60, 30 ), "is part of a track" );

		state.SetRecord( 60, 30, bare with { TrackType = ParkTrackPieces.Boundary } );
		StringAssert.Contains( At( 60, 30 ), "is the boundary's" );
	}

	/// <summary>
	/// <b>A person on a path a build takes up is counted</b> (Q1508): what the original does with them is unread, and ours leaves
	/// them where they stand.
	/// </summary>
	/// <remarks><b>Mutation:</b> not counting, or counting every path cell, fails the counts.</remarks>
	[TestMethod]
	public void APersonOnAPathABuildTakesUpIsCounted()
	{
		var state = new ParkState( shipped );
		var rides = new ParkRides( "jungle", shipped, catalogue, data );
		ParkBuilding.StandThing = _ => true;

		try
		{
			state.EnterCell( 41, 21, 9999 );

			var built = ParkBuilding.Build( state, shipped, catalogue, null, rides, BellyBounceItem, 40, 20, 0 );

			Assert.AreNotEqual( 0, built.ThingId, built.Answer );
			Assert.AreEqual( 1, TimesReported( "BUILD_OVER_PEOPLE_ON_A_PATH" ), "one of the three path cells has someone on it" );
		}
		finally
		{
			rides.Delete();
			Entity.ApplyDeletions();
		}
	}

	/// <summary>
	/// <b>A ride over scenery is refused and counted</b>: the verdict passes a thing's cell whose owner's priority is below the held
	/// item's (<c>0x00535d66</c>), and the build would tear the scenery down first (Q449); here it is refused, and counted only then.
	/// </summary>
	/// <remarks><b>Mutation:</b> counting at equal priority, or not at all, fails the counts.</remarks>
	[TestMethod]
	public void ARideOverLowerPriorityThingsIsRefusedAndCounted()
	{
		var state = new ParkState( shipped );

		ParkTrackTool.OverwritePriorityOf = ( _, _ ) => RidePriority;
		StringAssert.Contains( ParkBuilding.BodyCellRefusal( state, 52, 24, RidePriority ), "already has something built on it" );
		Assert.AreEqual( 0, TimesReported( "PLACEMENT_OVER_SCENERY" ), "an equal priority is no scenery" );

		ParkTrackTool.OverwritePriorityOf = ( _, _ ) => 1;
		StringAssert.Contains( ParkBuilding.BodyCellRefusal( state, 52, 24, RidePriority ), "already has something built on it" );
		Assert.AreEqual( 1, TimesReported( "PLACEMENT_OVER_SCENERY" ), "a feature's 1 under a ride's 5" );
	}

	/// <summary>
	/// <b>The shipped park offers 2,134 cells to a ride's body</b>: the 2,074 of its 2,362 in-park bare cells with no track, and the
	/// 60 of its 78 paths that are not NOMODIFY (measured from the file with <c>celltypes.py -s</c>). The other 288 bare cells carry the
	/// boundary's pieces, 215 of track kind 12 parented to 73 of kind 25 (<c>0x19</c>), which the verdict refuses after the redirect
	/// (<c>0x005358b5</c>). The old rule, which refused path and passed land outside the park, rock and NOMODIFY, offers another number.
	/// </summary>
	[TestMethod]
	public void TheShippedParkOffersItsBareGroundAndUnflaggedPaths()
	{
		var state = new ParkState( shipped );
		var taken = 0;

		for ( var y = 0; y < ParkWorld.MapSize; ++y )
		{
			for ( var x = 0; x < ParkWorld.MapSize; ++x )
			{
				if ( ParkBuilding.BodyCellRefusal( state, x, y, RidePriority ) == null )
					++taken;
			}
		}

		Assert.AreEqual( 2134, taken );
	}

	/// <summary>
	/// <b>A ride bought over a path takes the path up</b> (Q672's Belly Bounce, refused here before): the placer clears each cell
	/// before it stamps it (<c>Cell_Clear( 0, 0 )</c>, <c>0x0052910c</c>), so the three path cells of row 21 under it become its
	/// body, the paths either side lose their link toward it, and nothing is refunded for them.
	/// </summary>
	/// <remarks><b>Mutation:</b> stamping without the clear leaves (39,21) linked east into the ride.</remarks>
	[TestMethod]
	public void ARideBoughtOverAPathTakesThePathUp()
	{
		var state = new ParkState( shipped );
		var rides = new ParkRides( "jungle", shipped, catalogue, data );
		ParkBuilding.StandThing = _ => true;

		try
		{
			Assert.IsTrue( catalogue.TryGet( BellyBounceItem, out var bounce ) );
			Assert.AreEqual( 0x04, state.Record( 39, 21 ).Neighbours & 0x04, "(39,21) is linked east to begin with" );
			Assert.AreEqual( 0x40, state.Record( 43, 21 ).Neighbours & 0x40, "(43,21) is linked west to begin with" );

			var before = state.Balance;
			var built = ParkBuilding.Build( state, shipped, catalogue, null, rides, BellyBounceItem, 40, 20, 0 );

			Assert.AreNotEqual( 0, built.ThingId, built.Answer );
			Assert.AreEqual( before - bounce.BuildPrice, state.Balance, "the price alone" );

			for ( var x = 40; x <= 42; ++x )
			{
				var cell = state.Record( x, 21 );

				Assert.AreEqual( CellEdge.Footprint, cell.Type, $"({x},21) is the ride's" );
				Assert.AreEqual( MapStep.CellId( 40, 20 ), (int)cell.ParentId, $"({x},21) is owned by its anchor" );
			}

			Assert.AreEqual( 0, state.Record( 39, 21 ).Neighbours & 0x04, "(39,21) no longer links east" );
			Assert.AreEqual( 0, state.Record( 43, 21 ).Neighbours & 0x40, "(43,21) no longer links west" );
			Assert.AreEqual( 0, TimesReported( "BUILD_OVER_PEOPLE_ON_A_PATH" ), "nobody stood on the path" );
		}
		finally
		{
			rides.Delete();
			Entity.ApplyDeletions();
		}
	}

	/// <summary>
	/// The first anchor and angle at which an item of the kind given stands on the shipped park with every body cell passing the
	/// verdict and its entrance facing the cell named, or null where none does.
	/// </summary>
	private (ParkItemCatalogue.Item Item, int X, int Y, int Angle)? FacingFrom( ParkState state, System.Func<ParkItemCatalogue.Item, bool> kind,
		(int X, int Y) faced )
	{
		foreach ( var item in catalogue.All.Where( item => item.HasEntrance && !ParkTrackAddOn.Is( item ) && kind( item ) ).OrderBy( item => item.Id ) )
		{
			foreach ( var angle in new[] { 0, 90, 180, 270 } )
			{
				var (entryX, entryY) = ParkBuilding.RotateDelta( item.EntryDeltaX, item.EntryDeltaY, angle );
				var (x, y) = ParkBuilding.Step( faced.X, faced.Y, ParkBuilding.RotateBit( item.EntryDirection, angle ) );
				(x, y) = (x - entryX, y - entryY);

				if ( ParkBuilding.Step( x + entryX, y + entryY, CellEdge.Opposite( ParkBuilding.RotateBit( item.EntryDirection, angle ) ) ) != faced )
					continue;

				if ( ParkObjects.CoveredCells( item, x, y, angle ).All( cell => ParkState.OnMap( cell.X, cell.Y )
					&& ParkBuilding.BodyCellRefusal( state, cell.X, cell.Y, item.OverwritePriority ) == null )
					&& item.BuildPrice <= state.Balance )
					return (item, x, y, angle);
			}
		}

		return null;
	}

	/// <summary>
	/// <b>A queued ride's entrance may not face the boundary</b>: the placer asks the faced cell <c>0x204</c> (<c>0x005297a4</c>),
	/// the body's mode 4, and the 288 in-park bare cells carrying the boundary's track (as (2,3) does) are refused after the
	/// redirect (<c>0x005358b5</c>). The old rule passed any bare ground there, so the ride was built and its queue cell laid on the
	/// hoarding's.
	/// </summary>
	/// <remarks><b>Mutation:</b> QueueEndBlocked passing bare ground (the old rule) builds it.</remarks>
	[TestMethod]
	public void AQueuedRidesEntranceFacingTheBoundaryIsRefused()
	{
		var state = new ParkState( shipped );

		Assert.AreEqual( ParkTrackPieces.Boundary, ParkTrackAddOn.Head( state, 2, 3 ).TrackType, "(2,3) is under the boundary's piece" );
		StringAssert.Contains( ParkBuilding.QueueEndBlocked( state, (2, 3), RidePriority ), "(2,3) is the boundary's" );

		var boundary = Enumerable.Range( 0, ParkWorld.MapSize * ParkWorld.MapSize )
			.Select( at => (X: at % ParkWorld.MapSize, Y: at / ParkWorld.MapSize) )
			.Where( at => state.Record( at.X, at.Y ) is { Type: CellEdge.Nothing } cell && (cell.Flags & ParkPathBuilding.OutsideThePark) == 0
				&& ParkTrackAddOn.Head( state, at.X, at.Y ).TrackType == ParkTrackPieces.Boundary );

		var found = boundary.Select( at => (At: at, Found: FacingFrom( state, item => item.HasQueue, at )) ).FirstOrDefault( pair => pair.Found != null );

		Assert.IsNotNull( found.Found, "some queued ride stands with its entrance facing in-park bare ground under the boundary" );
		var (ride, x, y, angle) = found.Found.Value;

		StringAssert.Contains( ParkBuilding.PlacementRefusal( state, ride, x, y, angle ), $"({found.At.X},{found.At.Y}) is the boundary's",
			$"'{ride.Name}' at ({x},{y}) turned {angle}" );
	}

	/// <summary>
	/// <b>A shop's entrance may not face rock</b>: a thing with no queue asks its faced cell <c>0x201</c> (<c>0x005298b9</c>), mode 1,
	/// which passes only bare ground, path and a queue linked on to another queue; (45,18) is in-park rock (type 7), red
	/// (<c>0x00535d5d</c>). The old rule refused only another thing's cells, so the shop was built and its path stub counted.
	/// The same mode passes a NOMODIFY path, which mode 4 refuses.
	/// </summary>
	/// <remarks><b>Mutation:</b> EndBlocked refusing only a thing's cells (the old rule) builds it; EndBlocked as mode 4 refuses the
	/// NOMODIFY path.</remarks>
	[TestMethod]
	public void AShopsEntranceFacingRockIsRefused()
	{
		var state = new ParkState( shipped );
		var rock = state.Record( 45, 18 );

		Assert.AreEqual( 7, rock.Type, "(45,18) is rock" );
		Assert.AreEqual( 0, rock.Flags & ParkPathBuilding.OutsideThePark, "inside the park" );

		var found = FacingFrom( state, item => !item.HasQueue, (45, 18) );

		Assert.IsNotNull( found, "some shop or sideshow stands with its entrance facing (45,18)" );
		var (shop, x, y, angle) = found.Value;

		StringAssert.Contains( ParkBuilding.PlacementRefusal( state, shop, x, y, angle ), "(45,18) is type 7",
			$"'{shop.Name}' at ({x},{y}) turned {angle}" );

		// Mode 1 against mode 4 on the cells they tell apart.
		Assert.AreEqual( CellEdge.Path, state.Record( 47, 21 ).Type );
		Assert.AreNotEqual( 0, state.Record( 47, 21 ).Flags & ParkPathBuilding.NoModify, "(47,21) is a NOMODIFY path" );
		Assert.IsNull( ParkBuilding.EndBlocked( state, (47, 21), RidePriority ), "mode 1 passes a NOMODIFY path (0x00535bf5)" );
		StringAssert.Contains( ParkBuilding.EndBlocked( state, (43, 16), RidePriority ), "is outside the park" );
		StringAssert.Contains( ParkBuilding.EndBlocked( state, (2, 3), RidePriority ), "is the boundary's" );

		var bare = state.Record( 60, 30 );

		state.SetRecord( 60, 30, bare with { TrackType = ParkTrackPieces.KartAnchor } );
		Assert.IsNull( ParkBuilding.EndBlocked( state, (60, 30), RidePriority ), "mode 1 asks no track test (0x00535bb3 is mode 4's)" );
	}

	/// <summary>
	/// <b>A queue cell passes mode 1 only with one link, to another queue cell</b> (<c>0x00535cdb</c>..<c>0x00535d0b</c>, answer 8):
	/// the end of a queue that runs on passes; one with no link, two links, or a link to a path refuses.
	/// </summary>
	/// <remarks><b>Mutation:</b> refusing every queue, or passing every queue, fails one half.</remarks>
	[TestMethod]
	public void AQueueCellPassesModeOneOnlyLinkedOnToAnotherQueue()
	{
		var state = new ParkState( shipped );
		var ground = state.Record( 60, 30 );
		var queue = ground with { Type = ParkRideChoice.QueueCellType, TileSet = ParkQueues.QueueTileSet };

		state.SetRecord( 60, 30, queue with { Neighbours = 0x04 } );
		state.SetRecord( 61, 30, queue with { Neighbours = 0x40 } );
		Assert.IsNull( ParkBuilding.EndBlocked( state, (60, 30), RidePriority ), "one link east, to a queue" );

		state.SetRecord( 60, 30, queue with { Neighbours = 0x05 } );
		StringAssert.Contains( ParkBuilding.EndBlocked( state, (60, 30), RidePriority ), "is a queue", "two links" );

		state.SetRecord( 60, 30, queue with { Neighbours = 0 } );
		StringAssert.Contains( ParkBuilding.EndBlocked( state, (60, 30), RidePriority ), "is a queue", "no link" );

		state.SetRecord( 60, 30, queue with { Neighbours = 0x04 } );
		state.SetRecord( 61, 30, ground with { Type = CellEdge.Path } );
		StringAssert.Contains( ParkBuilding.EndBlocked( state, (60, 30), RidePriority ), "is a queue", "linked to a path" );
	}
}
