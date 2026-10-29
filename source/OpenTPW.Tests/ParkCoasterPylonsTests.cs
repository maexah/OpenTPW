using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Numerics = System.Numerics;

namespace OpenTPW.Tests;

/// <summary>
/// A coaster's pylons drawn (<see cref="ParkCoasterPylons"/>, <see cref="ParkCoasterHeights.Pylons"/>; <c>docs/exe/park-engine.md</c>,
/// "A coaster's pylons"; Q817). The Temple Of Gloom's 38 pieces are the ones Alexah's jungle <c>New Save</c> holds, stood on the shipped
/// jungle ground with the shipped Minecart's own files, as <see cref="ParkCoasterTrackTests"/> does; the original's 40 pylons are
/// the 38 and the station's two (Q816, <c>q816/pylonwords.txt</c>).
/// </summary>
[TestClass]
public class ParkCoasterPylonsTests
{
	private const string Minecart = "levels/jungle/rides/minecart";

	/// <summary>The Temple Of Gloom's pieces: cell, loft, bank, incline, yaw (the first four and the last two of its 38).</summary>
	private static readonly (int X, int Y, float Loft, float Bank, float Incline, float Yaw)[] Pieces =
	[
		( 26, 35, 0.09999999403953552f, 0.0f, -0.7168300747871399f, 1.5707963705062866f ),
		( 29, 35, 0.9999998211860657f, 0.0f, -0.6028791069984436f, 1.5707963705062866f ),
		( 32, 35, 0.9999998211860657f, -0.001859996933490038f, 0.5273827910423279f, 1.636208415031433f ),
		( 37, 34, 0.009999990463256836f, 0.011096315458416939f, 0.5256524085998535f, 1.7311091423034668f ),
		( 14, 35, 0.0f, 0.0f, -0.02550358511507511f, 1.5707963705062866f ),
	];

	private static List<SavedCoasterPiece> Saved( int stack = 0 )
		=> [.. Pieces.Select( p => new SavedCoasterPiece( 0, p.X, p.Y, stack, p.Loft, p.Bank, p.Incline, p.Yaw, 0f ) )];

	private static HeightfieldFile JungleGround( BaseFileSystem data )
	{
		using var md2 = data.OpenRead( "levels/jungle/terrain/base.MD2" );

		return new HeightfieldFile( md2 );
	}

	private static List<ParkCoasterHeights.PylonPlace> Places( BaseFileSystem data, ParkCoasterPylon pylon, List<SavedCoasterPiece> pieces )
	{
		var station = RideNodes.Load( Minecart, "minecart", data, false, [] )!;
		station.Place( new Vector3( 190f, 340f, 2.5f ), 0 );

		return ParkCoasterHeights.Pylons( pieces, pylon, JungleGround( data ), 2.5f,
			id => station.Find( id, ParkCoasterPylon.TrackSpace, out var at ) == NodeEnd.Posed ? at : null )!;
	}

	private static ModelFile Model( BaseFileSystem data )
	{
		using var stream = data.OpenRead( $"{Minecart}/StdPylon.MD2" );

		return new ModelFile( stream );
	}

	/// <summary>
	/// <b>A coaster's pylons are the station's first and third nodes and a piece's each</b>: the station's two stand on the cells under them
	/// (18, 35) and (23, 35), the pieces on their own, and the original's 40 of her park are the 38 and these two.
	/// </summary>
	/// <remarks><b>Mutations:</b> the station's third node dropped; a piece's cell read as its neighbour's; the station's pylons given the first piece's loft.</remarks>
	[TestMethod]
	public void EachStationEndAndEachPieceStandsAPylonOnItsCell()
	{
		var data = GameData.Required();
		var pylon = ParkCoasterPylon.Load( Minecart, data )!;
		var places = Places( data, pylon, Saved() );

		Assert.AreEqual( 2 + Pieces.Length, places.Count );
		Assert.AreEqual( "station 1", places[0].Which );
		Assert.AreEqual( "station 3", places[1].Which );
		Assert.AreEqual( (18, 35), (places[0].CellX, places[0].CellY) );
		Assert.AreEqual( (23, 35), (places[1].CellX, places[1].CellY) );

		// Their knot stands the node's 12.042 above the model's root (the loft 0.0227), not at a piece's loft.
		Assert.AreEqual( 0.0227f, places[0].Loft, 0.0005f );
		Assert.AreEqual( places[0].Loft, places[1].Loft, 1e-6f );

		for ( var i = 0; i < Pieces.Length; ++i )
		{
			Assert.AreEqual( (Pieces[i].X, Pieces[i].Y), (places[2 + i].CellX, places[2 + i].CellY) );
			Assert.AreEqual( Pieces[i].Loft, places[2 + i].Loft );
			Assert.AreEqual( Pieces[i].Yaw, places[2 + i].Yaw );
			Assert.AreEqual( Pieces[i].Incline, places[2 + i].Incline );
			Assert.AreEqual( Pieces[i].Bank, places[2 + i].Bank );
		}
	}

	/// <summary>
	/// A stacked pylon is counted and not stood here (the stack's rule, <c>Coaster_StandCellPylons</c>, is decoded and not built): the
	/// coaster keeps its other pylons.
	/// </summary>
	/// <remarks><b>Mutations:</b> the stack test removed (a stacked piece stands on the ground).</remarks>
	[TestMethod]
	public void AStackedPieceHasNoPylonButItsNeighboursKeepTheirs()
	{
		var data = GameData.Required();
		var pylon = ParkCoasterPylon.Load( Minecart, data )!;
		var pieces = Saved();

		pieces[1] = pieces[1] with { Stack = 1 };

		var places = Places( data, pylon, pieces );

		Assert.AreEqual( 2 + Pieces.Length - 1, places.Count );
		Assert.IsFalse( places.Any( place => (place.CellX, place.CellY) == (Pieces[1].X, Pieces[1].Y) ) );
	}

	/// <summary>
	/// The station's pylons face along the station, as the chain's knots do: the yaw that turns the knot to a heading is the one
	/// <see cref="ParkCoasterPylon.DirectionAt"/> points that way, for every heading round the compass (a sign taken the wrong way
	/// turns a heading of +0.4 to -0.4).
	/// </summary>
	/// <remarks><b>Mutations:</b> the sign of the small yaw ignored; the heading's difference taken the other way.</remarks>
	[TestMethod]
	public void TheYawThatFacesAHeadingPointsTheKnotThere()
	{
		var data = GameData.Required();
		var pylon = ParkCoasterPylon.Load( Minecart, data )!;

		foreach ( var degrees in new[] { 0, 25, 90, 135, 180, 250, 315 } )
		{
			var heading = degrees * MathF.PI / 180f;
			var level = new Numerics.Vector3( MathF.Sin( heading ), 0f, MathF.Cos( heading ) );
			var facing = pylon.DirectionAt( pylon.YawFacing( level ), 0f );

			Assert.AreEqual( level.X, facing.X, 0.01f, $"{degrees} degrees, across" );
			Assert.AreEqual( level.Z, facing.Z, 0.01f, $"{degrees} degrees, along" );
		}
	}

	/// <summary>
	/// <b>The pylon's floor is found on a one-cell footprint.</b> Made on none, the floor search lists nothing (the first build here
	/// drew every floor unconformed); on one cell it lists the model's four floor corners, the original's 4 of each pylon (160 in her
	/// park, <c>q816/orig/orig2/bv80pylon.txt</c>), all on the floor mesh <c>mc_pylbase</c>.
	/// </summary>
	/// <remarks><b>Mutations:</b> the footprint dropped from the maker (null); a footprint of two cells.</remarks>
	[TestMethod]
	public void AOneCellFootprintListsTheFloorsFourCorners()
	{
		var data = GameData.Required();
		var file = Model( data );
		var grid = new ProceduralBase.Grid( 0f, 0f, 10f, 10f );

		Assert.AreEqual( 0, ProceduralBase.BaseVertices( file, null, grid ).Count );

		var listed = ProceduralBase.BaseVertices( file, ParkCoasterPylons.Footprint, grid );

		Assert.AreEqual( 1, listed.Count );
		Assert.AreEqual( "mc_pylbase", file.Nodes[listed[0].Mesh].Name );
		Assert.AreEqual( 4, listed[0].Vertices.Length );
	}

	/// <summary>
	/// <b>The conform stands the floor's four corners on the ground's corners.</b> On a cell whose corners differ, the made floor is at the
	/// cell's midpoint height and off the corners; after the conform all four stand on them (the original's 4 of 4, Q816).
	/// </summary>
	/// <remarks><b>Mutations:</b> the conform skipped; the footprint null (lists none, so nothing moves).</remarks>
	[TestMethod]
	public void TheConformStandsAPylonsFloorOnTheCornersOfASlopedCell()
	{
		var data = GameData.Required();
		var field = JungleGround( data );
		var cell = Enumerable.Range( 0, 96 * 85 ).Select( i => (X: i % 96, Y: i / 96) )
			.First( c => Math.Abs( field.HeightAt( c.X, c.Y ) - field.HeightAt( c.X + 1, c.Y + 1 ) ) > 1f
				&& Math.Abs( field.HeightAt( c.X + 1, c.Y ) - field.HeightAt( c.X, c.Y + 1 ) ) > 0.1f );
		var origin = new Numerics.Vector3( cell.X * 10f, cell.Y * 10f, ParkCoasterHeights.GroundAt( field, cell.X, cell.Y ) );
		var file = Model( data );

		var before = ProceduralConform.Measure( file, ParkCoasterPylons.Footprint, origin, Quaternion.Identity, field );

		Assert.AreEqual( 4, before.Listed );
		Assert.IsTrue( before.OnCorner < 4, $"made at the midpoint, off the corners of ({cell.X},{cell.Y})" );

		var moved = ProceduralConform.Conform( file, ParkCoasterPylons.Footprint, origin, Quaternion.Identity, field );
		var after = ProceduralConform.Measure( file, ParkCoasterPylons.Footprint, origin, Quaternion.Identity, field );

		Assert.IsTrue( moved >= 1 );
		Assert.AreEqual( 4, after.OnCorner );
		Assert.AreEqual( 0f, after.LargestOff, 0.001f );
	}

	/// <summary>
	/// The type's named parts are the lookup records in space <c>0x80000</c> by id (<c>Coaster_SetupPylonModel</c>, <c>0x00430590</c>): the
	/// Minecart's pylon has the one of id 1, <c>mc_bridge</c> (the type flag <c>0x400</c>), and none of 2, 3 and 4, so none of the other three
	/// arms hides or shows anything; <c>StackHeightDummy</c> (id 1 in another space) is not one.
	/// </summary>
	/// <remarks><b>Mutations:</b> the space ignored (id 1 finds the stack-height dummy last); the id off by one.</remarks>
	[TestMethod]
	public void TheMinecartsPylonHasOneNamedPartAndItIsTheBridge()
	{
		var data = GameData.Required();
		var pylon = ParkCoasterPylon.Load( Minecart, data )!;

		Assert.AreEqual( "mc_bridge", pylon.NodeName( pylon.NamedNode( 1 ) ) );
		Assert.AreEqual( -1, pylon.NamedNode( 2 ) );
		Assert.AreEqual( -1, pylon.NamedNode( 3 ) );
		Assert.AreEqual( -1, pylon.NamedNode( 4 ) );
	}

	/// <summary>
	/// <b>The pylon's vertex and texture keys are offsets to add to the model's rest</b> (an additive model's morph, header <c>0x4</c>,
	/// <c>0x00471bef</c>): the loft clip's first frame reads nought on every vertex of the column and its last 90 up, and it adds up to
	/// 15 to the V of the top vertices (one lattice repeat to 6 units). Read as absolute positions the column's first frame would be a
	/// pile of vertices at the origin.
	/// </summary>
	/// <remarks><b>Mutations:</b> <c>MeshAnimator.PoseAdditive</c> assigning the keys (the first frame collapses the column).</remarks>
	[TestMethod]
	public void TheLoftClipsKeysAreOffsetsAndAddOneLatticeRepeatPerSixUnits()
	{
		var data = GameData.Required();
		var file = Model( data );
		var clip = RideAnimations.Load( Minecart, "StdPylon", data ).Clip( 3, 0 )!;
		var morph = clip.MorphTrackFor( 1 )!;
		var column = file.Meshes[1];

		Assert.AreEqual( (int)column.VertexCount + 2, (int)morph.ChannelCount );

		var raised = 0;

		for ( var vertex = 0; vertex < column.VertexCount; ++vertex )
		{
			morph.TryGetChannel( vertex, out var record, out var slot );

			var first = morph.DecodePosition( record!.Raw( 0, slot ) );
			var last = morph.DecodePosition( record.Raw( record.FrameIndices.Length - 1, slot ) );

			Assert.AreEqual( 0f, MathF.Sqrt( (first.X * first.X) + (first.Y * first.Y) + (first.Z * first.Z) ), 0.01f, $"vertex {vertex} at the first frame" );
			Assert.IsTrue( Math.Abs( last.Y ) < 0.01f || Math.Abs( last.Y - 90f ) < 0.01f, $"vertex {vertex} at the last frame is {last.Y}: the foot stays, the rest rise 90" );
			raised += Math.Abs( last.Y - 90f ) < 0.01f ? 1 : 0;
		}

		Assert.IsTrue( raised >= column.VertexCount / 2, $"{raised} of {column.VertexCount} vertices rise 90" );

		var uv = clip.UvTrackFor( 1 )!;
		var tops = Enumerable.Range( 0, uv.EntryCount ).Select( entry => uv.Sample( entry, clip.DeclaredLastFrame ).Y ).Max();

		Assert.AreEqual( 14.985f, tops, 0.01f );
		Assert.AreEqual( 0f, uv.Sample( 1, clip.DeclaredFirstFrame ).Y, 1e-4f );
	}

	/// <summary>
	/// <b>The morph is the rest plus each clip's keys at its frame.</b> The loft clip at its first frame leaves every vertex at rest and every texture
	/// coordinate, at its last raises the column's head 90 and adds 14.985 to the V of the top; the incline clip's first frame then adds about -3.49 on
	/// top of that raise (the clips sum), where a morph that assigned would put the vertex at the keys alone.
	/// </summary>
	/// <remarks><b>Mutations:</b> the keys assigned in place of added; the texture offsets left out; the second clip replacing the first.</remarks>
	[TestMethod]
	public void TheMorphIsTheRestPlusEveryClipsKeysAtItsFrame()
	{
		var data = GameData.Required();
		var file = Model( data );
		var clips = RideAnimations.Load( Minecart, "StdPylon", data );
		var loft = clips.Clip( 3, 0 )!;
		var incline = clips.Clip( 2, 0 )!;
		var mesh = file.Meshes[1];
		var rest = MeshAnimator.RestOf( mesh );
		var restUvs = mesh.TexCoords.Select( uv => new Numerics.Vector2( uv.X, uv.Y ) ).ToArray();

		var (first, firstUvs) = MeshAnimator.Additive( mesh, 1, rest, restUvs, [(loft, loft.DeclaredFirstFrame)] );

		for ( var v = 0; v < rest.Length; ++v )
			Assert.AreEqual( rest[v].Y, first[v].Y, 0.01f, $"vertex {v} at the loft's first frame" );

		Assert.AreEqual( restUvs[1].Y, firstUvs[1].Y, 1e-3f );

		var (raised, raisedUvs) = MeshAnimator.Additive( mesh, 1, rest, restUvs, [(loft, loft.DeclaredLastFrame)] );

		Assert.AreEqual( rest[0].Y + 90f, raised[0].Y, 0.01f );
		Assert.AreEqual( restUvs[1].Y + 14.985f, raisedUvs[1].Y, 0.01f );

		var (both, _) = MeshAnimator.Additive( mesh, 1, rest, restUvs, [(loft, loft.DeclaredLastFrame), (incline, incline.DeclaredFirstFrame)] );

		Assert.AreEqual( rest[0].Y + 90f - 3.4857f, both[0].Y, 0.01f );
	}

	/// <summary>The clips and frames a pylon is morphed by: the loft, incline and bank clips at their shares of their own length (the loft 0 to 100, the others 0 to 20, the neutral pitch at the middle frame).</summary>
	/// <remarks><b>Mutations:</b> the loft's share taken as nought; the incline's neutral half dropped.</remarks>
	[TestMethod]
	public void AMorphedPylonsClipsStandAtTheirShares()
	{
		var data = GameData.Required();
		var pylon = ParkCoasterPylon.Load( Minecart, data )!;

		var tall = pylon.MorphPoses( 1f, 0f, 0f );

		Assert.AreEqual( 3, tall.Count );
		Assert.AreEqual( 100f, tall[0].Frame, 0.01f );
		Assert.AreEqual( 10f, tall[1].Frame, 0.01f );
		Assert.AreEqual( 10f, tall[2].Frame, 0.01f );
		Assert.AreEqual( 50f, pylon.MorphPoses( 0.5f, 0f, 0f )[0].Frame, 0.01f );
	}

	/// <summary>A pylon alone on its cell hides the Minecart's bridge and nothing else (the others of the type's four named parts are not in its model).</summary>
	/// <remarks><b>Mutations:</b> the bridge left shown; a part of another id hidden.</remarks>
	[TestMethod]
	public void APylonAloneOnItsCellHidesTheBridge()
	{
		var data = GameData.Required();
		var pylon = ParkCoasterPylon.Load( Minecart, data )!;
		var parts = ParkCoasterPylons.PartsOf( pylon ).ToList();

		Assert.AreEqual( 1, parts.Count );
		Assert.AreEqual( "mc_bridge", pylon.NodeName( parts[0].Node ) );
		Assert.IsFalse( parts[0].Visible );
	}

	/// <summary>
	/// A pylon is made, conformed on its footprint by the stand hook, given its clips, set down, posed and then morphed, in that order: the model needs a
	/// graphics device, so this reads the source as the stand-hook test does.
	/// </summary>
	/// <remarks><b>Mutations:</b> the conform dropped from the hook; the clips not given; the pose or the morph removed or put before the set-down.</remarks>
	[TestMethod]
	public void APylonIsConformedGivenItsClipsSetDownPosedAndMorphedInThatOrder()
	{
		var text = File.ReadAllText( Path.Join( RepoFiles.Root(), "source", "OpenTPW", "World", "Park", "ParkCoasterPylons.cs" ) );

		StringAssert.Contains( text, "stand: file => conformed = ProceduralConform.Conform( file, Footprint, origin, Quaternion.Identity, field )" );
		StringAssert.Contains( text, "clips: type.Clips" );

		var made = text.IndexOf( "new LobbyModel(", StringComparison.Ordinal );
		var set = text.IndexOf( "model.SetTransform( origin", StringComparison.Ordinal );
		var posed = text.IndexOf( "model.PoseNodes( poser => type.Pose( poser, place.Loft, place.Yaw, place.Incline, place.Bank ) )", StringComparison.Ordinal );
		var morphed = text.IndexOf( "type.Morph( model, place.Loft, place.Incline, place.Bank )", StringComparison.Ordinal );
		var parts = text.IndexOf( "PartsOf( type )", text.IndexOf( "Standing? Make(", StringComparison.Ordinal ), StringComparison.Ordinal );

		Assert.IsTrue( made > 0 && made < set && set < posed && posed < morphed && morphed < parts, $"{made} {set} {posed} {morphed} {parts}" );
	}

	/// <summary>The yaw that faces a heading holds for every posable pylon type of the jungle's coasters, whichever way its rotate clip turns.</summary>
	/// <remarks><b>Mutations:</b> the sign of the small yaw ignored.</remarks>
	[TestMethod]
	public void EveryPosableJunglePylonFacesAHeadingByItsYaw()
	{
		var data = GameData.Required();
		var tried = 0;

		foreach ( var directory in new[] { "minecart", "coaster1", "coaster3" } )
		{
			foreach ( var index in new[] { 0, 1 } )
			{
				if ( ParkCoasterPylon.Load( $"levels/jungle/rides/{directory}", data, index ) is not { Posable: true } pylon )
					continue;

				++tried;

				foreach ( var degrees in new[] { 40, 200 } )
				{
					var heading = degrees * MathF.PI / 180f;
					var level = new Numerics.Vector3( MathF.Sin( heading ), 0f, MathF.Cos( heading ) );
					var facing = pylon.DirectionAt( pylon.YawFacing( level ), 0f );

					Assert.AreEqual( level.X, facing.X, 0.02f, $"{directory} pylon {index} at {degrees} degrees" );
					Assert.AreEqual( level.Z, facing.Z, 0.02f, $"{directory} pylon {index} at {degrees} degrees" );
				}
			}
		}

		Assert.IsTrue( tried >= 1 );
	}

	/// <summary>
	/// The node tree a made pylon is posed by puts its knot where <see cref="ParkCoasterPylon.KnotAt"/> does, at every loft and yaw: the
	/// drawn pylon stands where the track's height was measured to.
	/// </summary>
	/// <remarks><b>Mutations:</b> the loft clip not posed; the yaw's turn laid on the wrong node.</remarks>
	[TestMethod]
	public void ThePosedNodeTreeCarriesTheKnotWhereTheTrackHeightsPutIt()
	{
		var data = GameData.Required();
		var pylon = ParkCoasterPylon.Load( Minecart, data )!;
		var file = Model( data );

		foreach ( var (loft, yaw, incline, bank) in new[] { (0f, 0f, 0f, 0f), (1f, 1.57f, -0.6f, 0.2f), (0.37f, -1.57f, 0.5f, -0.1f), (0.0227f, 3.0f, 0.1f, 1.1f) } )
		{
			var poser = new NodePoser( file );

			pylon.Pose( poser, loft, yaw, incline, bank );

			var knot = poser.World( pylon.KnotNode ).Translation;
			var asked = pylon.KnotAt( loft );

			Assert.AreEqual( asked.X, knot.X, 0.01f, $"loft {loft} yaw {yaw}, across" );
			Assert.AreEqual( asked.Y, knot.Y, 0.01f, $"loft {loft} yaw {yaw}, up" );
			Assert.AreEqual( asked.Z, knot.Z, 0.01f, $"loft {loft} yaw {yaw}, along" );
		}
	}

	/// <summary>
	/// The turn of a node's rotate clip is laid on its own place: <see cref="NodePoser.Turn"/> changes the rotation of the column's node and
	/// leaves its translation, and the column's mesh follows (a pylon turned a quarter turn faces its mesh's x axis along z).
	/// </summary>
	/// <remarks><b>Mutations:</b> the translation reset to nought by the turn.</remarks>
	[TestMethod]
	public void ATurnKeepsTheNodesPlace()
	{
		var data = GameData.Required();
		var file = Model( data );
		var poser = new NodePoser( file );
		var before = poser.Local( 1 ).Translation;

		poser.Turn( 1, Quaternion.CreateFromAxisAngle( Numerics.Vector3.UnitY, MathF.PI / 2f ) );

		Assert.AreEqual( before, poser.Local( 1 ).Translation );

		var across = Numerics.Vector3.TransformNormal( Numerics.Vector3.UnitX, poser.World( 1 ) );

		Assert.AreEqual( 0f, across.X, 1e-4f );
		Assert.AreEqual( -1f, across.Z, 1e-4f );
	}

	/// <summary>The park makes the pylons after its other standing things; its level wires them in and the console lists them.</summary>
	/// <remarks><b>Mutations:</b> the constructor call removed from <c>Level</c>; the console's <c>pylons</c> case removed.</remarks>
	[TestMethod]
	public void TheLevelMakesThePylonsAndTheConsoleListsThem()
	{
		var root = Path.Join( RepoFiles.Root(), "source", "OpenTPW" );

		StringAssert.Contains( File.ReadAllText( Path.Join( root, "World", "Level.cs" ) ), "new ParkCoasterPylons(" );
		StringAssert.Contains( File.ReadAllText( Path.Join( root, "Client", "Diagnostics", "DebugConsole.cs" ) ), "case \"pylons\":" );
	}
}
