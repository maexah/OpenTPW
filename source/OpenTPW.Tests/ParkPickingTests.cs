using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenTPW.Tests;

/// <summary>
/// The picker's ground and its last cell (<see cref="ParkPicking"/>; <c>docs/exe/park-engine.md</c>, "The picker never
/// hands the mode a cell off the map"). The figures are the original's own, read at the stock park (<c>q472/orig/</c>):
/// the pointer on (95,40) answered (94,40) with the hit at x 958.24, z 405.89; on (40,84) it answered (40,83) with the
/// hit at x 404.11, z 847.36; past the heightfield and over the sea the cell stood.
/// </summary>
[TestClass]
public class ParkPickingTests
{
	private BaseFileSystem data = null!;

	[TestInitialize]
	public void MountTheGame() => data = GameData.Required();

	private HeightfieldFile Jungle()
	{
		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/terrain/base.MD2" ) );
		return new HeightfieldFile( stream );
	}

	private (int Width, int Height) Limits()
	{
		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Standard.sam" ) );
		var balance = new SettingsFile( stream );

		return (int.Parse( balance["MapInfo.HeightfieldWidth"]!, CultureInfo.InvariantCulture ),
			int.Parse( balance["MapInfo.HeightfieldHeight"]!, CultureInfo.InvariantCulture ));
	}

	/// <summary>A point on the ground of the jungle's heightfield, for a ray to aim at.</summary>
	private static Vector3 OnTheGround( HeightfieldFile field, float x, float y ) => new( x, y, field.HeightAtWorld( x, y ) );

	private static Vector3 Toward( Vector3 from, Vector3 to ) => (to - from).Normal;

	/// <summary>
	/// The hit's cell is truncated, held to the heightfield's last cell, then below <c>MapInfo</c>'s width and height:
	/// the original's two hits in column 95 and row 84 answer 94 and 83. Without the limits they would answer 95 and 84,
	/// which is what OpenTPW answered before.
	/// </summary>
	[TestMethod]
	public void TheHitIsHeldBelowMapInfosLimits()
	{
		var field = Jungle();
		var (width, height) = Limits();

		Assert.AreEqual( (95, 84, 96, 85), (width, height, field.CellsX, field.CellsY) );
		Assert.AreEqual( (94, 40), ParkPicking.CellOf( field, new Vector3( 958.24f, 405.89f, 0f ), width, height ), "A3" );
		Assert.AreEqual( (40, 83), ParkPicking.CellOf( field, new Vector3( 404.11f, 847.36f, 0f ), width, height ), "A6" );
		Assert.AreEqual( (93, 40), ParkPicking.CellOf( field, new Vector3( 939.9f, 409.9f, 0f ), width, height ), "inside, untouched" );
		Assert.AreEqual( (95, 84), ParkPicking.CellOf( field, new Vector3( 958.24f, 847.36f, 0f ), 0, 0 ), "no limits: the heightfield's own" );
		Assert.AreEqual( (95, 84), ParkPicking.CellOf( field, new Vector3( 1200f, 1100f, 0f ), 0, 0 ), "held to its last cell" );
		Assert.AreEqual( 5215, ParkPicking.Answered( field, new Vector3( 958.24f, 405.89f, 0f ), 0, width, height ), "packed as the original's 0x145f" );
	}

	/// <summary>
	/// Only the heightfield is ground: a ray aimed past its last column, past its last row, or over the sea before its
	/// first row meets nothing, where holding the edge height past it would meet the held ground and answer a cell off
	/// the heightfield (98,40), (40,85).
	/// </summary>
	[TestMethod]
	public void NothingPastTheHeightfieldIsGround()
	{
		var field = Jungle();

		var east = new Vector3( 900f, 405.89f, 90f );
		Assert.IsTrue( ParkPicking.MeetGround( field, east, Toward( east, OnTheGround( field, 958.24f, 405.89f ) ), out var hit ) );
		Assert.AreEqual( 958.24f, hit.X, 0.5f, "met in column 95" );
		Assert.IsFalse( ParkPicking.MeetGround( field, east, Toward( east, OnTheGround( field, 985.8f, 405.89f ) ), out _ ), "past column 95" );

		var north = new Vector3( 404.11f, 780f, 90f );
		Assert.IsTrue( ParkPicking.MeetGround( field, north, Toward( north, OnTheGround( field, 404.11f, 847.36f ) ), out hit ) );
		Assert.AreEqual( 847.36f, hit.Y, 0.5f, "met in row 84" );
		Assert.IsFalse( ParkPicking.MeetGround( field, north, Toward( north, OnTheGround( field, 404.11f, 875f ) ), out _ ), "past row 84" );

		// Just past the edge, inside the extra cell that the vertex count (97 x 86) would add to the cell count (96 x 85):
		// the slab ends at x 960 and y 850, not 970 and 860.
		Assert.IsFalse( ParkPicking.MeetGround( field, east, Toward( east, OnTheGround( field, 965f, 405.89f ) ), out _ ), "between 960 and 970" );
		Assert.IsFalse( ParkPicking.MeetGround( field, north, Toward( north, OnTheGround( field, 404.11f, 855f ) ), out _ ), "between 850 and 860" );

		var sea = new Vector3( 455f, 30f, 90f );
		Assert.IsTrue( ParkPicking.MeetGround( field, sea, Toward( sea, OnTheGround( field, 455f, 25f ) ), out _ ), "row 2" );
		Assert.IsFalse( ParkPicking.MeetGround( field, sea, Toward( sea, OnTheGround( field, 455f, -35f ) ), out _ ), "the sea past row 0" );
		Assert.IsFalse( ParkPicking.MeetGround( field, sea, new Vector3( 0f, 0f, 1f ), out _ ), "the sky" );
	}

	/// <summary>
	/// The original's sequence at its first view: the pointer on (95,40) answers (94,40), then past the heightfield the
	/// ray meets nothing and the pointer's (94,40) stands; before any meeting there is no cell.
	/// </summary>
	[TestMethod]
	public void TheLastCellStandsWhereTheRayMeetsNothing()
	{
		var field = Jungle();
		var (width, height) = Limits();
		var eye = new Vector3( 900f, 405.89f, 90f );
		var cell = typeof( ParkPicking ).GetProperty( nameof( ParkPicking.Cell ) )!;
		var was = ParkPicking.Cell;

		(int Cell, bool Met) Pick( float x, int standing )
		{
			cell.SetValue( null, standing );
			var (packed, met, _) = ParkPicking.Meet( field, eye, Toward( eye, OnTheGround( field, x, 405.89f ) ), width, height );
			return (packed, met);
		}

		try
		{
			var a3 = Pick( 958.24f, 0 );
			Assert.AreEqual( ((94, 40), true), (MapStep.CellAt( a3.Cell ), a3.Met ), "A3" );
			Assert.AreEqual( (a3.Cell, false), Pick( 985.8f, a3.Cell ), "A4: past the heightfield the cell stands" );
			Assert.AreEqual( (93, 40), MapStep.CellAt( Pick( 935f, a3.Cell ).Cell ), "met again, the new cell" );
			Assert.AreEqual( (0, false), Pick( 985.8f, 0 ), "no cell before the first meeting" );
		}
		finally
		{
			cell.SetValue( null, was );
		}
	}

	/// <summary>A park's end lets the last cell go, so the next park's picker has no cell before its first meeting.</summary>
	[TestMethod]
	public void AParksEndForgetsTheLastCell()
	{
		var cell = typeof( ParkPicking ).GetProperty( nameof( ParkPicking.Cell ) )!;
		var (was, pinned) = (ParkPicking.Cell, ParkPicking.Pinned);

		try
		{
			cell.SetValue( null, 5215 );
			ParkPicking.Pinned = (40, 83);
			Level.ForgetPark();
			Assert.AreEqual( (0, ((int, int)?)null), (ParkPicking.Cell, ParkPicking.Pinned) );
		}
		finally
		{
			cell.SetValue( null, was );
			ParkPicking.Pinned = pinned;
		}
	}

	/// <summary>
	/// <b>The object march finds a placed thing wherever it stands in the park's list</b>: the first, a middle one and the
	/// last of the stock park's objects each answer with their own record, as a search by id through the list would, and
	/// nought and a thing nobody placed answer nothing.
	/// </summary>
	/// <remarks><b>Mutations:</b> the loop starting at the second object or stopping before the last; the id compared to
	/// the wrong field; a thing of nought answered with the first object.</remarks>
	[TestMethod]
	public void APlacedThingIsFoundAtAnyPlaceInTheList()
	{
		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		var state = new ParkState( new ParkWorld( new SaveReader( stream ).ReadFile() ) );
		var objects = state.Objects;

		Assert.IsTrue( objects.Count >= 3, "the control: a list with a first, a middle and a last" );
		Assert.AreEqual( objects.Count, objects.Select( o => o.ThingId ).Distinct().Count(), "the control: every id once, so first is the only" );

		foreach ( var place in new[] { 0, objects.Count / 2, objects.Count - 1 } )
		{
			var found = ParkPicking.PlacedThing( state, objects[place].ThingId );

			Assert.IsNotNull( found, $"place {place} of {objects.Count}" );
			Assert.AreEqual( objects[place], found.Value, $"place {place} of {objects.Count}" );
		}

		Assert.IsNull( ParkPicking.PlacedThing( state, 0 ), "nought is no thing" );
		Assert.IsNull( ParkPicking.PlacedThing( state, objects.Max( o => o.ThingId ) + 1000 ), "a thing nobody placed" );
	}

	/// <summary>One triangle of a cell as the original cuts it: its three corners as (u, v, height), u across and v down the cell.</summary>
	private static (float U, float V, float H)[] Triangle( HeightfieldFile field, int cellX, int cellY, bool diagonal, bool nearHalf )
	{
		(float U, float V, float H) Corner( int du, int dv ) => (du, dv, field.HeightAt( cellX + du, cellY + dv ));

		// Cut from (x, y) to (x+1, y+1): c-d-a holds v >= u, b-a-d the rest. Cut from (x+1, y) to (x, y+1): a-b-c holds u + v <= 1, d-c-b the rest.
		return (diagonal, nearHalf) switch
		{
			(false, true) => [Corner( 0, 0 ), Corner( 1, 0 ), Corner( 0, 1 )],
			(false, false) => [Corner( 1, 1 ), Corner( 0, 1 ), Corner( 1, 0 )],
			(true, true) => [Corner( 0, 1 ), Corner( 1, 1 ), Corner( 0, 0 )],
			(true, false) => [Corner( 1, 0 ), Corner( 0, 0 ), Corner( 1, 1 )],
		};
	}

	/// <summary>The height at (u, v) of the plane through three corners, solved by Cramer's rule rather than the picker's normal.</summary>
	private static float PlaneAt( (float U, float V, float H)[] corner, float u, float v )
	{
		var (a, b, c) = (corner[0], corner[1], corner[2]);
		var det = ((b.U - a.U) * (c.V - a.V)) - ((c.U - a.U) * (b.V - a.V));
		var gradU = (((b.H - a.H) * (c.V - a.V)) - ((c.H - a.H) * (b.V - a.V))) / det;
		var gradV = (((c.H - a.H) * (b.U - a.U)) - ((b.H - a.H) * (c.U - a.U))) / det;

		return a.H + (gradU * (u - a.U)) + (gradV * (v - a.V));
	}

	/// <summary>
	/// The first ground cell (no hole bit) cut the given way where, at (u, v) in its <paramref name="nearHalf"/> triangle, the other
	/// triangle's plane carried over stands at least a fifth of a unit above (<paramref name="ridge"/>: the drawn ground is the lower
	/// plane) or below (a valley: the drawn ground is the higher one).
	/// </summary>
	private (int X, int Y) FindCell( HeightfieldFile field, bool diagonal, bool nearHalf, bool ridge, float u, float v )
	{
		for ( var y = 0; y < field.CellsY; ++y )
		{
			for ( var x = 0; x < field.CellsX; ++x )
			{
				var word = field.FlagsAt( x, y );

				if ( (word & 0x1) != 0 || ((word & 0x2) != 0) != diagonal )
					continue;

				var own = PlaneAt( Triangle( field, x, y, diagonal, nearHalf ), u, v );
				var other = PlaneAt( Triangle( field, x, y, diagonal, !nearHalf ), u, v );

				if ( ridge ? other - own >= 0.2f : own - other >= 0.2f )
					return (x, y);
			}
		}

		Assert.Fail( $"no cell cut diagonal {diagonal}, near half {nearHalf}, ridge {ridge}" );
		return default;
	}

	/// <summary>An eye 60 up and 30 across and 15 down from a cell's point (u, v), the ray from it through the point on <paramref name="height"/>.</summary>
	private static (Vector3 Eye, Vector3 Direction, Vector3 Aim) RayAt( HeightfieldFile field, (int X, int Y) cell, float u, float v, float height )
	{
		var aim = new Vector3( field.OriginX + ((cell.X + u) * field.CellSizeX), field.OriginY + ((cell.Y + v) * field.CellSizeY), height );
		var eye = aim + new Vector3( 30f, 15f, 60f );

		return (eye, Toward( eye, aim ), aim);
	}

	/// <summary>
	/// <b>The pick meets the land as the original's two planes a cell</b> (<c>FUN_0045bf90</c>, <c>0x0045c6b7</c>): in a valley cell, in
	/// either cut and either triangle, a ray aimed at the drawn ground meets it; in a ridge cell it meets the other triangle's plane
	/// carried over the half it was aimed at, above the drawn ground by that plane's rise (solved here by the plane's gradient, not
	/// the picker's normal).
	/// </summary>
	/// <remarks><b>Mutations:</b> the diagonal flag read the wrong way round; the nearest hit replaced by the first or the last; the
	/// distance taken without its sign; the cell's square not bounding the plane's hit.</remarks>
	[TestMethod]
	public void ARidgeIsMetOnTheOtherTrianglesPlaneAndAValleyOnTheGround()
	{
		var field = Jungle();

		foreach ( var diagonal in new[] { false, true } )
		{
			foreach ( var nearHalf in new[] { true, false } )
			{
				// A point off the diagonal in the half asked for, and off the line u = v, where the two legs of a triangle's plane would swap unseen.
				var (u, v) = (diagonal, nearHalf) switch
				{
					(false, true) => (0.15f, 0.3f),
					(false, false) => (0.85f, 0.6f),
					(true, true) => (0.2f, 0.7f),
					_ => (0.7f, 0.2f),
				};

				var label = $"diagonal {diagonal}, half {(nearHalf ? "near" : "far")}";

				var valley = FindCell( field, diagonal, nearHalf, ridge: false, u, v );
				var ground = PlaneAt( Triangle( field, valley.X, valley.Y, diagonal, nearHalf ), u, v );
				var (eye, direction, aim) = RayAt( field, valley, u, v, ground );

				Assert.IsTrue( ParkPicking.MeetGround( field, eye, direction, out var hit ), $"valley, {label}" );
				Assert.AreEqual( aim.X, hit.X, 0.002f, $"valley x, {label}" );
				Assert.AreEqual( aim.Y, hit.Y, 0.002f, $"valley y, {label}" );
				Assert.AreEqual( ground, hit.Z, 0.002f, $"valley height, {label}" );

				// The same line run the other way (aimed up and away from the land) meets the same point: the distance is taken either way.
				Assert.IsTrue( ParkPicking.MeetGround( field, eye, -direction, out hit ), $"valley, behind the eye, {label}" );
				Assert.AreEqual( ground, hit.Z, 0.002f, $"valley behind, {label}" );

				var ridge = FindCell( field, diagonal, nearHalf, ridge: true, u, v );
				var own = Triangle( field, ridge.X, ridge.Y, diagonal, nearHalf );
				var other = Triangle( field, ridge.X, ridge.Y, diagonal, !nearHalf );
				var floor = PlaneAt( own, u, v );
				(eye, direction, aim) = RayAt( field, ridge, u, v, floor );

				// Along the line eye + s * direction the other plane stands at h0 + gradient . (p - p0): solve for the s where it meets the line.
				var cell = (X: field.OriginX + (ridge.X * field.CellSizeX), Y: field.OriginY + (ridge.Y * field.CellSizeY));
				float OtherAt( float s ) => PlaneAt( other, (eye.X + (s * direction.X) - cell.X) / field.CellSizeX, (eye.Y + (s * direction.Y) - cell.Y) / field.CellSizeY );
				var (s0, s1) = (0f, 100f);
				var (f0, f1) = (eye.Z - OtherAt( s0 ), eye.Z + (s1 * direction.Z) - OtherAt( s1 ));
				var along = s0 - (f0 * (s1 - s0) / (f1 - f0));

				Assert.IsTrue( ParkPicking.MeetGround( field, eye, direction, out hit ), $"ridge, {label}" );
				Assert.AreEqual( eye.X + (along * direction.X), hit.X, 0.002f, $"ridge x, {label}" );
				Assert.AreEqual( eye.Y + (along * direction.Y), hit.Y, 0.002f, $"ridge y, {label}" );
				Assert.AreEqual( eye.Z + (along * direction.Z), hit.Z, 0.002f, $"ridge height, {label}" );
				Assert.IsTrue( hit.Z - PlaneAt( own, (hit.X - cell.X) / field.CellSizeX, (hit.Y - cell.Y) / field.CellSizeY ) > 0.05f, $"ridge above the drawn ground, {label}" );
			}
		}
	}

	/// <summary>
	/// <b>The pick meets the land where every cell's two planes, tried in turn, say</b>: a thousand rays from above the jungle's
	/// park toward random points of it (and the same lines run backward) are met at the same point as a reference that tries all
	/// 8,160 cells with no cull and no rectangle, solving each plane by Cramer's rule and keeping the nearest by distance either way.
	/// The picker's cull of cells is the original's (<c>0x0045c4f4</c> and <c>0x0045c648</c>); a hit it drops is a hit outside the cell's
	/// heights, which a ray from above the park never has the nearest of.
	/// </summary>
	/// <remarks><b>Mutations:</b> the diagonal flag inverted; a triangle's plane through the wrong corners; the nearest replaced by the last.</remarks>
	[TestMethod]
	public void EveryRayMeetsWhereAllTheCellsPlanesSay()
	{
		var field = Jungle();
		var random = new System.Random( 824 );
		var differing = 0;
		var met = 0;

		for ( var i = 0; i < 1000; ++i )
		{
			var aim = new Vector3( random.NextSingle() * 940f + 10f, random.NextSingle() * 830f + 10f, 0f );
			aim.Z = field.HeightAtWorld( aim.X, aim.Y );
			var eye = aim + new Vector3( (random.NextSingle() - 0.5f) * 160f, (random.NextSingle() - 0.5f) * 160f, 40f + (random.NextSingle() * 90f) );
			var direction = Toward( eye, aim );

			foreach ( var along in new[] { direction, -direction } )
			{
				var expected = EveryPlane( field, eye, along );
				var found = ParkPicking.MeetGround( field, eye, along, out var hit );

				if ( expected is null || !found )
				{
					Assert.AreEqual( expected is not null, found, $"ray {i}: met" );
					continue;
				}

				++met;

				if ( (hit - expected.Value).Length > 0.01f )
					++differing;
			}
		}

		Assert.IsTrue( met > 1500, $"the control: most rays meet the land ({met})" );
		Assert.AreEqual( 0, differing, "rays met elsewhere than by every plane" );
	}

	/// <summary>The nearest hit of a line with every cell's two triangle planes, each counting anywhere in its cell's square.</summary>
	private static Vector3? EveryPlane( HeightfieldFile field, Vector3 eye, Vector3 direction )
	{
		Vector3? best = null;
		var nearest = double.MaxValue;

		for ( var cy = 0; cy < field.CellsY; ++cy )
		{
			for ( var cx = 0; cx < field.CellsX; ++cx )
			{
				var diagonal = (field.FlagsAt( cx, cy ) & 0x2) != 0;

				foreach ( var nearHalf in new[] { true, false } )
				{
					var corner = Triangle( field, cx, cy, diagonal, nearHalf );
					var (a, b, c) = (corner[0], corner[1], corner[2]);
					var det = ((b.U - a.U) * (c.V - a.V)) - ((c.U - a.U) * (b.V - a.V));
					var gradU = (((b.H - a.H) * (c.V - a.V)) - ((c.H - a.H) * (b.V - a.V))) / det;
					var gradV = (((c.H - a.H) * (b.U - a.U)) - ((b.H - a.H) * (c.U - a.U))) / det;

					// eye.Z + s * direction.Z = a.H + gradU * (u - a.U) + gradV * (v - a.V), u and v linear in s.
					var u0 = (eye.X - field.OriginX) / field.CellSizeX - cx;
					var v0 = (eye.Y - field.OriginY) / field.CellSizeY - cy;
					var du = direction.X / field.CellSizeX;
					var dv = direction.Y / field.CellSizeY;
					var s = (a.H + (gradU * (u0 - a.U)) + (gradV * (v0 - a.V)) - eye.Z)
						/ (direction.Z - (gradU * du) - (gradV * dv));
					var (u, v) = (u0 + (s * du), v0 + (s * dv));

					if ( u >= 0 && u <= 1 && v >= 0 && v <= 1 && System.Math.Abs( s ) < nearest )
					{
						nearest = System.Math.Abs( s );
						best = eye + (direction * s);
					}
				}
			}
		}

		return best;
	}
}
