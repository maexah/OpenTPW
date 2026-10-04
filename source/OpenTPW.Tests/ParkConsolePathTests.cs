using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;

namespace OpenTPW.Tests;

/// <summary>The console's public path command shares the player-tool verdict (Q94).</summary>
[TestClass]
public class ParkConsolePathTests
{
	private Level? previous;
	private ParkWorld world = null!;
	private ParkState state = null!;

	[TestInitialize]
	public void OpenPark()
	{
		previous = Level.Current;
		FileSystem = GameData.Required();
		Log ??= new();
		using var stream = new MemoryStream( FileSystem.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		world = new( new SaveReader( stream ).ReadFile() );
		state = new( world );
		var level = (Level)RuntimeHelpers.GetUninitializedObject( typeof( Level ) );
		typeof( Level ).GetProperty( nameof( Level.Park ) )!.SetValue( level, world );
		typeof( Level ).GetProperty( nameof( Level.ParkState ) )!.SetValue( level, state );
		Level.Current = level;
		ParkBuildMode.Forget();
	}

	[TestCleanup]
	public void RestoreLevel()
	{
		Level.Current = previous!;
		ParkBuildMode.Forget();
		ParkState.ForgetCurrent();
	}

	private ParkWorld.MapCell[] Cells() => Enumerable.Range( 0, ParkWorld.MapSize * ParkWorld.MapSize )
		.Select( i => ParkState.CellFor( world, i % ParkWorld.MapSize, i / ParkWorld.MapSize ) ).ToArray();

	private void RefusesWithoutChanges( int x, int y, string reason )
	{
		var cells = Cells();
		var balance = state.Balance;
		Assert.AreEqual( $"path: nothing laid - ({x},{y}) {reason}", ParkPathBuilding.Lay( x, y ) );
		Assert.AreEqual( balance, state.Balance, "a refusal costs nothing" );
		CollectionAssert.AreEqual( cells, Cells(), "a refusal cannot stamp, unlink or retile any cell" );
	}

	[TestMethod]
	[DataRow( 49 )]
	[DataRow( 50 )]
	[DataRow( 51 )]
	[DataRow( 52 )]
	public void ConnectedQueueIsRefusedWithoutChangingThePark( int x )
		=> RefusesWithoutChanges( x, 22, "is a queue that is not a loose end" );

	[TestMethod]
	public void OutsideLandUsesThePlayerReason()
		=> RefusesWithoutChanges( 43, 16, "is outside the park" );

	[TestMethod]
	public void TrackOnBareGroundUsesThePlayerReason()
	{
		state.SetRecord( 10, 10, ParkState.CellFor( world, 10, 10 ) with { TrackType = 0x19 } );
		RefusesWithoutChanges( 10, 10, "is part of a track" );
	}

	[TestMethod]
	public void UnaffordablePathUsesThePlayerReason()
	{
		state.Spend( state.Balance - 19 );
		RefusesWithoutChanges( 10, 10, "would bring the run to 20 against a balance of 19" );
	}

	[TestMethod]
	public void OutsideMapCannotChangeThePark()
	{
		var cells = Cells();
		var balance = state.Balance;
		Assert.AreEqual( "path: (-1,22) is off the map", ParkPathBuilding.Lay( -1, 22 ) );
		Assert.AreEqual( balance, state.Balance );
		CollectionAssert.AreEqual( cells, Cells() );
	}

	[TestMethod]
	public void ALooseQueueEndCanStillBecomePath()
	{
		// The queue editor detaches the tail from the path, leaving its one link to queue (50,22).
		ParkPathBuilding.EditQueue( 13 );
		var balance = state.Balance;
		Assert.AreEqual( $"path: laid at (49,22) for 20, balance {balance - 20}", ParkPathBuilding.Lay( 49, 22 ) );
		Assert.AreEqual( CellEdge.Path, ParkState.CellFor( world, 49, 22 ).Type );
		Assert.AreEqual( balance - 20, state.Balance );
		Assert.AreEqual( ParkRideChoice.QueueCellType, ParkState.CellFor( world, 50, 22 ).Type );
	}

	[TestMethod]
	public void BareGroundIsChargedOnceAndExistingPathIsFree()
	{
		var balance = state.Balance;
		StringAssert.StartsWith( ParkPathBuilding.Lay( 10, 10 ), "path: laid at (10,10) for 20" );
		Assert.AreEqual( CellEdge.Path, ParkState.CellFor( world, 10, 10 ).Type );
		StringAssert.StartsWith( ParkPathBuilding.Lay( 10, 10 ), "path: (10,10) is already path" );
		Assert.AreEqual( balance - 20, state.Balance );
		Assert.AreEqual( 1, ParkState.CellFor( world, 10, 10 ).OverlapCounter );
	}

	[TestMethod]
	public void ConsoleVerdictDoesNotUseOrChangeThePointerAnchor()
	{
		ParkBuildMode.Arm( ParkBuildMode.Path );
		ParkBuildMode.AnchorAt( 10, 10 );
		RefusesWithoutChanges( 51, 22, "is a queue that is not a loose end" );
		Assert.AreEqual( (10, 10), ParkBuildMode.Anchor );
		Assert.AreEqual( ParkBuildMode.Path, ParkBuildMode.Current );
	}
}
