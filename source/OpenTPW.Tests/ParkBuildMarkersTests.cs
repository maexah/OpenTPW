using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenTPW.Tests;

/// <summary>
/// The queue tool's squares - <see cref="ParkBuildMarkers"/>. No game files needed.
/// </summary>
[TestClass]
public class ParkBuildMarkersTests
{
	/// <summary>
	/// The wave the queue tool's squares ride on: <c>sin( phase + x + z )</c> in world units, one unit either
	/// way - the 4,096-entry table <c>FUN_004708d0</c> fills, read at <c>(phase + x + z) * 4096 / 2π</c>.
	/// </summary>
	[TestMethod]
	public void TheQueueSquaresWaveOneUnitAlongTheDiagonal()
	{
		Assert.AreEqual( 0f, ParkBuildMarkers.Wave( 0f, 0f, 0f ), 1e-5f, "flat at the origin with no phase" );
		Assert.AreEqual( 1f, ParkBuildMarkers.Wave( System.MathF.PI / 2f, 0f, 0f ), 1e-5f, "one unit at the crest" );
		Assert.AreEqual( ParkBuildMarkers.Wave( 0f, 10f, 20f ), ParkBuildMarkers.Wave( 0f, 20f, 10f ), 1e-5f,
			"the two ground axes count alike, so the crests run corner to corner" );
		Assert.AreEqual( System.MathF.Sin( 30f ), ParkBuildMarkers.Wave( 0f, 10f, 20f ), 1e-5f, "in world units, not cells" );
	}

	/// <summary>
	/// A square's record sits 1.5 over its lift: the original's records read 21.5 over the Belly Bounce's lift of 20,
	/// 11.5 over a lift of 10 and 1.5 on bare ground (<c>docs/exe/park-engine.md</c>, "Placement feedback"; Q433, Q435).
	/// </summary>
	[TestMethod]
	public void ASquaresRecordSitsOneAndAHalfOverItsLift()
	{
		Assert.AreEqual( 1.5f, ParkBuildMarkers.RecordHeight( 0f ), 1e-5f, "bare ground" );
		Assert.AreEqual( 11.5f, ParkBuildMarkers.RecordHeight( 10f ), 1e-5f, "a lift of ten" );
		Assert.AreEqual( 21.5f, ParkBuildMarkers.RecordHeight( 20f ), 1e-5f, "over the Belly Bounce" );
	}

	/// <summary>
	/// The strip the mesh holds records each square at <see cref="ParkBuildMarkers.RecordHeight"/> of its own lift, not
	/// of the bare ground: the game's <c>strip</c> census prints <c>Over - Lift</c>, so a lifted cell reads 20 (Q435, Q892).
	/// </summary>
	[TestMethod]
	public void TheStripRecordsEachSquareOverItsOwnLift()
	{
		var strip = ParkBuildMarkers.Recorded(
		[
			((52, 26, ParkPathBuilding.MarkerBlue, 0, false, 1), 20f),
			((53, 26, ParkPathBuilding.MarkerRed, 1, true, 2), 0f)
		] );

		CollectionAssert.AreEqual( new[] { 21.5f, 1.5f }, strip.Select( square => square.Over ).ToArray() );
		Assert.AreEqual( (53, 26, ParkPathBuilding.MarkerRed, 1, true, 2),
			(strip[1].X, strip[1].Y, strip[1].Marker, strip[1].Turn, strip[1].Glides, strip[1].Size), "the rest carried over" );
	}

	/// <summary>Four corners to a square or a wall; the vertex array is sized from squares and walls together (Q892).</summary>
	[TestMethod]
	public void TheMeshHoldsFourCornersToEverySquareAndWall()
	{
		Assert.AreEqual( 0, ParkBuildMarkers.VertexCount( 0, 0 ) );
		Assert.AreEqual( 4, ParkBuildMarkers.VertexCount( 1, 0 ) );
		Assert.AreEqual( 4, ParkBuildMarkers.VertexCount( 0, 1 ) );
		Assert.AreEqual( 28, ParkBuildMarkers.VertexCount( 3, 4 ) );
		Assert.AreEqual( 6, ParkBuildMarkers.ElementCount( 1, 0 ), "six indices to a square" );
		Assert.AreEqual( 12, ParkBuildMarkers.ElementCount( 0, 1 ), "twelve to a wall, its two sides" );
		Assert.AreEqual( 66, ParkBuildMarkers.ElementCount( 3, 4 ) );
	}

	/// <summary>
	/// A lifted square's walls, as the original queued them on the stock park (<c>q435/orig/b-5-squares.txt</c>): (52,26),
	/// lifted 20 beside (51,26) at 20, (53,26) at 10 and (52,27) on the ground, with (52,25) as high, laid its square at
	/// 21.5 and then records at 11.5, 1.5 and 11.5 - one wall toward the east and two toward the south, lowest first.
	/// </summary>
	[TestMethod]
	public void ALiftedSquareIsWalledDownToEachLowerNeighbourTenAtATime()
	{
		var lifts = new System.Collections.Generic.Dictionary<(int, int), float>
		{
			[(52, 25)] = 20f, [(53, 26)] = 10f, [(52, 27)] = 0f, [(51, 26)] = 20f
		};

		var walls = ParkBuildMarkers.WallsAt( 52, 26, 20f, ParkPathBuilding.MarkerBlue, ( x, y ) => lifts[(x, y)] );

		CollectionAssert.AreEqual(
			new[] { ParkBuildMarkers.FaceEast, ParkBuildMarkers.FaceSouth, ParkBuildMarkers.FaceSouth },
			walls.Select( wall => wall.Face ).ToArray() );
		CollectionAssert.AreEqual( new[] { 11.5f, 1.5f, 11.5f }, walls.Select( wall => wall.Over ).ToArray(),
			"the original's records after the square's 21.5" );
		Assert.IsTrue( walls.All( wall => (wall.X, wall.Y, wall.Marker) == (52, 26, ParkPathBuilding.MarkerBlue) ), "the square's own cell, blue" );
		Assert.IsTrue( ParkBuildMarkers.WallsAt( 52, 26, 20f, ParkPathBuilding.MarkerRed, ( x, y ) => lifts[(x, y)] )
			.All( wall => wall.Marker == ParkPathBuilding.MarkerRed ), "red under a red square" );
		Assert.IsTrue( ParkBuildMarkers.WallsAt( 52, 26, 20f, ParkPathBuilding.MarkerLink, ( x, y ) => lifts[(x, y)] )
			.All( wall => wall.Marker == ParkPathBuilding.MarkerBlue ), "blue under m_link" );
	}

	/// <summary>The four neighbours are asked north, east, south, west, and a missing or higher one gets no wall.</summary>
	[TestMethod]
	public void TheWallsGoNorthEastSouthWestAndNoneOffTheMapOrUpward()
	{
		var lifts = new System.Collections.Generic.Dictionary<(int, int), float?>
		{
			[(5, 4)] = 0f, [(6, 5)] = 0f, [(5, 6)] = 0f, [(4, 5)] = 0f
		};

		CollectionAssert.AreEqual(
			new[] { ParkBuildMarkers.FaceNorth, ParkBuildMarkers.FaceEast, ParkBuildMarkers.FaceSouth, ParkBuildMarkers.FaceWest },
			ParkBuildMarkers.WallsAt( 5, 5, 10f, 0, ( x, y ) => lifts[(x, y)] ).Select( wall => wall.Face ).ToArray() );

		Assert.AreEqual( 0, ParkBuildMarkers.Walls( 20f, null, 20f, 30f, null ).Count, "off the map, as high, higher" );
		Assert.AreEqual( 0, ParkBuildMarkers.WallsAt( 5, 5, 0f, 0, ( _, _ ) => -10f ).Count, "a square on the ground has none" );
		Assert.AreEqual( 2, ParkBuildMarkers.Walls( 20f, 0f, null, null, null ).Count, "ten at a time, not one" );
	}

	/// <summary>Each wall stands on its own edge of the cell, from the corner the original lays its foot first.</summary>
	[TestMethod]
	public void EachWallStandsOnTheEdgeTowardItsNeighbour()
	{
		Assert.AreEqual( ((5, 5), (6, 5)), ParkBuildMarkers.Edge( 5, 5, ParkBuildMarkers.FaceNorth ) );
		Assert.AreEqual( ((6, 5), (6, 6)), ParkBuildMarkers.Edge( 5, 5, ParkBuildMarkers.FaceEast ) );
		Assert.AreEqual( ((6, 6), (5, 6)), ParkBuildMarkers.Edge( 5, 5, ParkBuildMarkers.FaceSouth ) );
		Assert.AreEqual( ((5, 6), (5, 5)), ParkBuildMarkers.Edge( 5, 5, ParkBuildMarkers.FaceWest ) );
		Assert.AreEqual( 10f, ParkBuildMarkers.WallHeight, "the -10.0 at 0x00700e1c" );
	}

	/// <summary>A wall's foot lies along its edge at the record's height and its top one wall higher; blinked off, it has no area.</summary>
	[TestMethod]
	public void AWallRisesTenFromItsRecordAlongItsEdge()
	{
		var corners = ParkBuildMarkers.WallCorners( new ParkBuildMarkers.Wall( 5, 5, ParkBuildMarkers.FaceEast, 0, 11.5f ), false );

		CollectionAssert.AreEqual( new[] { (6, 5, 11.5f), (6, 6, 11.5f), (6, 6, 21.5f), (6, 5, 21.5f) },
			corners.Select( corner => (corner.X, corner.Y, corner.Over) ).ToArray() );
		CollectionAssert.AreEqual( new[] { (0f, 0f), (1f, 0f), (1f, 1f), (0f, 1f) },
			corners.Select( corner => (corner.Uv.X, corner.Uv.Y) ).ToArray(), "u along the edge, v up it" );

		var gone = ParkBuildMarkers.WallCorners( new ParkBuildMarkers.Wall( 5, 5, ParkBuildMarkers.FaceEast, 1, 11.5f ), true );
		Assert.IsTrue( gone.All( corner => (corner.X, corner.Y, corner.Over) == (6, 5, 11.5f) ), "all on its first corner" );
	}

	/// <summary>A wall is red under a red square and blue under every other the tools draw.</summary>
	[TestMethod]
	public void AWallIsRedUnderRedAndBlueOtherwise()
	{
		Assert.AreEqual( ParkPathBuilding.MarkerRed, ParkBuildMarkers.WallMarker( ParkPathBuilding.MarkerRed ) );
		Assert.AreEqual( ParkPathBuilding.MarkerBlue, ParkBuildMarkers.WallMarker( ParkPathBuilding.MarkerBlue ) );
		Assert.AreEqual( ParkPathBuilding.MarkerBlue, ParkBuildMarkers.WallMarker( ParkPathBuilding.MarkerLink ) );
		Assert.AreEqual( ParkPathBuilding.MarkerBlue, ParkBuildMarkers.WallMarker( ParkPathBuilding.MarkerEnd ) );
	}

	/// <summary>
	/// <b>Nothing left to draw leaves no walls behind</b>: when the tool is put away the mesh goes, and the census that
	/// `strip` reads says "none" with no walls after it, as the original's list is emptied every frame.
	/// </summary>
	/// <remarks><b>Mutation:</b> the walls not cleared with the squares keep the last outline's walls in the census.</remarks>
	[TestMethod]
	public void NothingToDrawLeavesNoWallsInTheCensus()
	{
		var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
		var markers = (ParkBuildMarkers)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject( typeof( ParkBuildMarkers ) );
		var lifts = new System.Collections.Generic.Dictionary<(int, int), float> { [(52, 25)] = 20f, [(53, 26)] = 10f, [(52, 27)] = 0f, [(51, 26)] = 20f };
		var strip = typeof( ParkBuildMarkers ).GetField( "_strip", flags )!;
		strip.SetValue( markers, System.Activator.CreateInstance( strip.FieldType ) );
		typeof( ParkBuildMarkers ).GetField( "_walls", flags )!
			.SetValue( markers, ParkBuildMarkers.WallsAt( 52, 26, 20f, ParkPathBuilding.MarkerBlue, ( x, y ) => lifts[(x, y)] ) );
		StringAssert.Contains( markers.Census(), "| walls 3 " );

		typeof( ParkBuildMarkers ).GetMethod( "Build", flags )!.Invoke( markers,
			[new System.Collections.Generic.List<ParkPathBuilding.QueueSquare>(), new System.Collections.Generic.List<ParkBuilding.CarrySquare>(),
				new System.Collections.Generic.List<ParkPathBuilding.QueueSquare>(), new System.Collections.Generic.List<(int X, int Y)>()] );

		Assert.AreEqual( "none", markers.Census() );
	}

	/// <summary>
	/// <b>A followed member's patrol edges are all red under the refusal latch and all blue without it</b>, each cell
	/// as given, a cell given twice laid twice: the original's verdict for them is <c>DAT_00816d50</c> alone
	/// (<c>0x005356df</c>). In the original (<c>q532/orig/a-run.out</c>) the mechanic's 48 were texture 1 after a
	/// refused path click on the Belly Bounce and texture 0 after one outside the park.
	/// </summary>
	/// <remarks><b>Mutations:</b> the latch not read (always blue, as before), or read the wrong way round.</remarks>
	[TestMethod]
	public void ThePatrolEdgesAreRedOnlyUnderTheRefusalLatch()
	{
		var cells = new System.Collections.Generic.List<(int X, int Y)> { (48, 18), (47, 18), (48, 18) };

		var red = ParkBuildMarkers.PatrolSquares( cells, latched: true );
		var blue = ParkBuildMarkers.PatrolSquares( cells, latched: false );

		CollectionAssert.AreEqual( new[] { (48, 18, 1), (47, 18, 1), (48, 18, 1) }, red.Select( square => (square.X, square.Y, square.Marker) ).ToArray() );
		CollectionAssert.AreEqual( new[] { (48, 18, 0), (47, 18, 0), (48, 18, 0) }, blue.Select( square => (square.X, square.Y, square.Marker) ).ToArray() );
	}

	/// <summary>
	/// <b>The frame lays the queued edges by the latch</b>: with it set the mesh is laid from red squares, and the
	/// latch changing with the same cells lays it again.
	/// </summary>
	/// <remarks><b>Mutations:</b> the frame laying the edges without the latch; the mesh not laid again when only it changes.</remarks>
	[TestMethod]
	public void TheFrameLaysTheQueuedEdgesByTheLatch()
	{
		var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
		var markers = (ParkBuildMarkers)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject( typeof( ParkBuildMarkers ) );
		var update = typeof( ParkBuildMarkers ).GetMethod( "OnUpdate", flags )!;
		var built = typeof( ParkBuildMarkers ).GetField( "_built", flags )!;

		try
		{
			ParkBuildMode.Forget();
			ParkBuildMode.RefusalLatched = true;
			markers.QueuePatrolEdges( [(48, 18), (47, 18)] );
			update.Invoke( markers, null );
			StringAssert.Contains( (string)built.GetValue( markers )!, "|48,18,1;47,18,1|" );

			ParkBuildMode.RefusalLatched = false;
			markers.QueuePatrolEdges( [(48, 18), (47, 18)] );
			update.Invoke( markers, null );
			StringAssert.Contains( (string)built.GetValue( markers )!, "|48,18,0;47,18,0|" );
		}
		finally
		{
			ParkBuildMode.Forget();
		}
	}

	/// <summary>
	/// <b>A frame's pass sets the latch only while a tool is armed</b>: a refused pass sets it, an accepted one clears it,
	/// and with nothing armed it stands (<c>q532/orig/a-run.out</c>: <c>d50</c> 1 after the refused path click, still 1
	/// with the locator out, 0 after the path tool was armed again and put away).
	/// </summary>
	/// <remarks><b>Mutations:</b> the latch taken from a frame with no tool armed; kept over an armed tool's pass; the pass taken with no cell picked (our own choice, pinned).</remarks>
	[TestMethod]
	public void OnlyAnArmedToolsPassMovesTheLatch()
	{
		Assert.IsTrue( ParkBuildMarkers.LatchAfterFrame( false, ParkBuildMode.Path, true, true ), "a refused pass" );
		Assert.IsFalse( ParkBuildMarkers.LatchAfterFrame( true, ParkBuildMode.Path, true, false ), "an accepted pass" );
		Assert.IsTrue( ParkBuildMarkers.LatchAfterFrame( true, ParkBuildMode.None, true, false ), "nothing armed: it stands" );
		Assert.IsFalse( ParkBuildMarkers.LatchAfterFrame( false, ParkBuildMode.None, true, true ) );
		Assert.IsTrue( ParkBuildMarkers.LatchAfterFrame( true, ParkBuildMode.Path, false, false ), "no cell picked: no pass" );
	}

	/// <summary>
	/// <b>Putting a tool away leaves the refusal latch as its last pass left it</b>: the setter clears the placer's
	/// <c>DAT_0081b604</c> and <c>d48</c> (<c>FUN_0052f200( 0 )</c>), never <c>d50</c>. <b>Installing a tool clears it</b>,
	/// because the install runs a pass under the old mode first (<c>FUN_0046d5a0</c>, <c>0x0046d5e8</c>). Leaving the
	/// park forgets it, as a park's start clears it (<c>FUN_0052f050</c>).
	/// </summary>
	/// <remarks><b>Mutations:</b> the latch cleared by <c>Disarm</c>; kept by <c>Arm</c> of a tool; not cleared by <c>Forget</c>.</remarks>
	[TestMethod]
	public void PuttingAToolAwayLeavesTheRefusalLatch()
	{
		try
		{
			ParkBuildMode.Arm( ParkBuildMode.Path );
			ParkBuildMode.RefusalLatched = true;

			ParkBuildMode.Disarm();
			Assert.IsTrue( ParkBuildMode.RefusalLatched, "put away" );

			ParkBuildMode.Arm( ParkBuildMode.Patrol, 26 );
			Assert.IsFalse( ParkBuildMode.RefusalLatched, "another tool armed: the install's pass cleared it" );

			ParkBuildMode.Forget();
			Assert.IsFalse( ParkBuildMode.RefusalLatched, "the park left" );
		}
		finally
		{
			ParkBuildMode.Forget();
		}
	}

	/// <summary>The ripple shading is counted as a gap only where the light is on: low quality's full light leaves no colour for it to change.</summary>
	/// <remarks><b>Mutation:</b> the test turned round, or dropped, fails.</remarks>
	[TestMethod]
	public void TheRippleShadingIsAGapOnlyUnderALitPark()
	{
		var ambient = new Vector3( 0.333f );
		var travels = new Vector3( 0.4f, 0.4f, -0.8f );

		Assert.IsTrue( ParkBuildMarkers.RipplesShade( null ), "the lobby has no park light" );
		Assert.IsTrue( ParkBuildMarkers.RipplesShade( new ParkLight( ambient, travels ) ) );
		Assert.IsFalse( ParkBuildMarkers.RipplesShade( new ParkLight( ambient, travels, Lit: false ) ) );
	}

	/// <summary>
	/// <b>The mesh's CPU half lays what Build sizes it from</b>: a blue square on the hill at (73,66) records over its lift of
	/// 40 (Recorded), and the vertex array holds four corners to it, to every wall under it and to the stranded cell added after
	/// them (VertexCount, counted after the stranded squares), the index list six to each square and twelve to each wall
	/// (ElementCount) (Q892, Q613).
	/// </summary>
	/// <remarks><b>Mutation:</b> at the call sites in <c>Lay</c>: <c>_strip = Recorded( lifts )</c> with every lift 0 reads
	/// <c>^0</c>; <c>VertexCount( _strip.Count, 0 )</c> or the count taken before the stranded AddRange, or the elements
	/// counted without the walls, break the lengths.</remarks>
	[TestMethod]
	public void TheMeshesCpuHalfSizesItsArraysFromTheSquaresAndWallsItLaid()
	{
		var data = GameData.Required();
		FileSystem = data;

		using var stream = new System.IO.MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		var state = new ParkState( new ParkWorld( new SaveReader( stream ).ReadFile() ) );
		HeightfieldFile field;

		using ( var terrain = data.OpenRead( "levels/jungle/terrain/base.MD2" ) )
			field = new HeightfieldFile( terrain );

		var stateProperty = typeof( ParkState ).GetProperty( nameof( ParkState.Current ) )!;
		var before = ParkState.Current;
		var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
		var markers = (ParkBuildMarkers)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject( typeof( ParkBuildMarkers ) );

		try
		{
			stateProperty.SetValue( null, state );

			Assert.IsTrue( markers.Lay(
				[new ParkPathBuilding.QueueSquare( 73, 66, ParkPathBuilding.MarkerBlue )], [], [], [(10, 10)], field ) );
		}
		finally
		{
			stateProperty.SetValue( null, before );
		}

		var walls = ((System.Collections.IEnumerable)typeof( ParkBuildMarkers ).GetField( "_walls", flags )!.GetValue( markers )!)
			.Cast<object>().Count();
		var vertices = (Vertex[])typeof( ParkBuildMarkers ).GetField( "_vertices", flags )!.GetValue( markers )!;
		var elements = (uint[])typeof( ParkBuildMarkers ).GetField( "_elements", flags )!.GetValue( markers )!;

		Assert.IsTrue( walls > 0, "the hill has walls under its square" );
		StringAssert.Contains( markers.Census(), "73,66,0^40", "recorded over its own lift" );
		Assert.AreEqual( 4 * (2 + walls), vertices.Length, "the square, the stranded cell and each wall" );
		Assert.AreEqual( (6 * 2) + (12 * walls), elements.Length, "six indices to each square, twelve to each wall" );
		Assert.AreEqual( (uint)vertices.Length - 1, elements.Max(), "the indices reach the last corner" );
	}
}
