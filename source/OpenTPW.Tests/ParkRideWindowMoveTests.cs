using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenTPW.UI;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;

namespace OpenTPW.Tests;

/// <summary>
/// The ride window's <c>b_move</c> (<c>0x3e2c</c>) is greyed for a kart or water ride once track runs from either mouth:
/// <c>FUN_004ad4e0</c> enables it by <c>TrackRide_MayMove</c> (<c>FUN_0052c2b0</c>; <c>docs/exe/park-engine.md</c>, "The ride
/// window's verbs, shown and hidden").
/// </summary>
[TestClass]
public class ParkRideWindowMoveTests
{
	[TestInitialize]
	[TestCleanup]
	public void ForgetTheTool() => ParkBuildMode.Forget();

	private static ParkState Bought( int trackType, int x, int y, int angle )
	{
		var state = new ParkState( parkIsClosed: false, visitorsToDate: 0, balance: 60000 );
		ParkTrackTool.Forget();
		ParkTrackPieces.LayStation( state, trackType, x, y, angle );
		return state;
	}

	/// <summary>A bought kart or water ride's mouths each link back alone, at every turn, so it may be moved.</summary>
	/// <remarks><b>Mutations:</b> the station's section tested in place of a mouth; a mouth's link count taken as two.</remarks>
	[TestMethod]
	[DataRow( ItemDescriptionFile.CarTrack, 0 )]
	[DataRow( ItemDescriptionFile.CarTrack, 90 )]
	[DataRow( ItemDescriptionFile.CarTrack, 180 )]
	[DataRow( ItemDescriptionFile.CarTrack, 270 )]
	[DataRow( ItemDescriptionFile.WaterTrack, 0 )]
	[DataRow( ItemDescriptionFile.WaterTrack, 270 )]
	public void ABoughtKartOrWaterRideMayBeMoved( int trackType, int angle )
		=> Assert.IsTrue( ParkTrackPieces.MayMove( Bought( trackType, 60, 60, angle ), trackType, 60, 60, angle ) );

	/// <summary>
	/// A Dino Karts at (30,24) turned 0: a run laid from the mouth (34,25), or from the other mouth (28,25), gives that mouth
	/// two links, and the move is greyed; so is it round a closed loop.
	/// </summary>
	/// <remarks><b>Mutations:</b> always enabled; one link or more; the mouth alone tested; the other mouth alone.</remarks>
	[TestMethod]
	public void ARunFromEitherMouthGreysTheMove()
	{
		var owner = (ushort)MapStep.CellId( 30, 24 );

		var state = Bought( ItemDescriptionFile.CarTrack, 30, 24, 0 );
		ParkTrackTool.LayRun( state, 0xb, owner, (34, 25), (34, 29), 200 );
		Assert.AreEqual( 2, CellEdge.Links( state.Record( 34, 25 ).TrackNeighbours ), "the mouth links on" );
		Assert.IsFalse( ParkTrackPieces.MayMove( state, 1, 30, 24, 0 ), "a run from the mouth" );

		state = Bought( ItemDescriptionFile.CarTrack, 30, 24, 0 );
		ParkTrackTool.LayRun( state, 0xb, owner, (28, 25), (28, 29), 200 );
		Assert.AreEqual( 2, CellEdge.Links( state.Record( 28, 25 ).TrackNeighbours ), "the other mouth links on" );
		Assert.IsFalse( ParkTrackPieces.MayMove( state, 1, 30, 24, 0 ), "a run from the other mouth" );

		ParkTrackTool.LayRun( state, 0xb, owner, (28, 29), (34, 29), 200 );
		ParkTrackTool.LayRun( state, 0xb, owner, (34, 29), (34, 25), 200 );
		Assert.IsTrue( ParkTrackTool.WalkToOpenEnd( state, 30, 24, 0 ).Closed, "the loop is closed" );
		Assert.IsFalse( ParkTrackPieces.MayMove( state, 1, 30, 24, 0 ), "round a closed loop" );
	}

	/// <summary>
	/// Only track types 1 and 2 are asked: a flat ride and a coaster may be moved with no station laid at all, where a kart
	/// ride whose mouths hold nothing may not. At a turn not one of the four the ride's own cell is asked, twice.
	/// </summary>
	/// <remarks><b>Mutations:</b> the coaster asked too; an unlaid mouth taken as one link; another turn taken as 0; the four cardinal bits counted alone.</remarks>
	[TestMethod]
	public void OnlyAKartOrWaterRideIsAsked()
	{
		var bare = new ParkState( parkIsClosed: false, visitorsToDate: 0 );

		Assert.IsTrue( ParkTrackPieces.MayMove( bare, 0, 30, 24, 0 ), "a flat ride" );
		Assert.IsTrue( ParkTrackPieces.MayMove( bare, ItemDescriptionFile.CoasterTrack, 30, 24, 0 ), "a coaster" );
		Assert.IsFalse( ParkTrackPieces.MayMove( bare, ItemDescriptionFile.CarTrack, 30, 24, 0 ), "a kart ride with no mouths" );

		var turned = Bought( ItemDescriptionFile.CarTrack, 30, 24, 0 );
		Assert.IsFalse( ParkTrackPieces.MayMove( turned, 1, 30, 24, 45 ), "at 45 the ride's own cell, which holds no track" );

		turned.SetRecord( 30, 24, turned.Record( 30, 24 ) with { TrackNeighbours = 0x04 } );
		Assert.IsTrue( ParkTrackPieces.MayMove( turned, 1, 30, 24, 45 ), "and with one link there" );

		// Cell_CountMaskBits counts all eight bits (0x005227aa..0x005227dd): a diagonal bit beside the one link is a second.
		turned.SetRecord( 30, 24, turned.Record( 30, 24 ) with { TrackNeighbours = 0x04 | 0x02 } );
		Assert.IsFalse( ParkTrackPieces.MayMove( turned, 1, 30, 24, 45 ), "a cardinal and a diagonal bit, two" );
		turned.SetRecord( 30, 24, turned.Record( 30, 24 ) with { TrackNeighbours = 0x80 } );
		Assert.IsTrue( ParkTrackPieces.MayMove( turned, 1, 30, 24, 45 ), "a diagonal bit alone, one" );
	}
}

/// <summary>The same rule through the window, over the shipped park and the jungle's items; skipped without the game (<see cref="GameData"/>).</summary>
[TestClass]
public class ParkRideWindowMoveWindowTests
{
	private Level? _level;
	private Point2 _screen;
	private ParkState _state = null!;
	private WindowStack _stack = null!;

	[TestInitialize]
	public void LoadThePark()
	{
		_level = Level.Current;
		_screen = Screen.Size;

		Screen.Size = new Point2( 2048, 1536 );
		Log ??= new();
		FileSystem = GameData.Required();
		ParkBuildMode.Forget();
		ParkTrackTool.Forget();

		using var stream = new MemoryStream( FileSystem.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		var world = new ParkWorld( new SaveReader( stream ).ReadFile() );
		_state = new ParkState( world );

		var level = (Level)RuntimeHelpers.GetUninitializedObject( typeof( Level ) );
		typeof( Level ).GetProperty( nameof( Level.Park ) )!.SetValue( level, world );
		typeof( Level ).GetProperty( nameof( Level.ParkState ) )!.SetValue( level, _state );
		typeof( Level ).GetProperty( nameof( Level.Catalogue ) )!.SetValue( level, new ParkItemCatalogue( "jungle", FileSystem ) );
		Level.Current = level;

		_stack = new WindowStack();
	}

	[TestCleanup]
	public void PutItAllBack()
	{
		Entity.ApplyDeletions();
		ParkBuildMode.Forget();
		Level.Current = _level!;
		Screen.Size = _screen;
	}

	/// <summary>
	/// <b>A Dino Karts' move is live while it is bare and greyed once a run leaves its mouth; a Splish Splash's the same;
	/// a flat ride's stays live.</b> The stock park's thing 13 is a flat ride; a Dino Karts (1150) at (30,24) and a Splish
	/// Splash (1160) at (18,18) are added with their stations, and the one window is shown on each in turn.
	/// </summary>
	/// <remarks><b>Mutations:</b> the window never greys it; greys it once and never enables it again.</remarks>
	[TestMethod]
	public void TheWindowGreysTheMoveOnceTrackRunsFromAMouth()
	{
		var karts = Add( 1150, 30, 24 );
		var splash = Add( 1160, 18, 18 );

		var window = new ParkObjectWindow( _stack, 13 );
		_stack.Open( window );

		var move = (UiButton)Find( window.Root, 0x3e2c );
		Assert.IsTrue( move.Enabled, "a flat ride's move" );

		window.Show( karts );
		Assert.IsTrue( move.Enabled, "a bare Dino Karts' move" );

		ParkTrackTool.LayRun( _state, 0xb, (ushort)MapStep.CellId( 30, 24 ), (34, 25), (34, 29), 200 );
		window.Show( karts );
		Assert.IsFalse( move.Enabled, "the karts' move once a run leaves the mouth" );
		Assert.AreEqual( 1, move.State, "drawn in its disabled part" );

		window.Show( splash );
		Assert.IsTrue( move.Enabled, "a bare Splish Splash's move" );

		ParkTrackTool.LayRun( _state, 0x10, (ushort)MapStep.CellId( 18, 18 ), (16, 19), (16, 23), 250 );
		window.Show( splash );
		Assert.IsFalse( move.Enabled, "the water ride's move once a run leaves the other mouth" );

		window.Show( 13 );
		Assert.IsTrue( move.Enabled, "back on the flat ride, live again" );
	}

	private int Add( int item, int x, int y )
	{
		var thing = new ParkWorld.CatalogueObject( _state.NextThingId(), item, x << 8, y << 8, 0 );
		_state.AddObject( thing );
		Assert.AreEqual( 4, ParkTrackPieces.LayStation( _state, item == 1160 ? ItemDescriptionFile.WaterTrack : ItemDescriptionFile.CarTrack, x, y, 0 ),
			$"item {item}'s station and mouths at ({x},{y})" );
		return thing.ThingId;
	}

	private static UiControl Find( UiControl root, int id )
		=> FindOrNull( root, id ) ?? throw new AssertFailedException( $"no control 0x{id:x}" );

	private static UiControl? FindOrNull( UiControl root, int id )
		=> root.Id == id ? root : root.Children.Select( child => FindOrNull( child, id ) ).FirstOrDefault( found => found != null );
}
