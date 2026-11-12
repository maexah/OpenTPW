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
}
