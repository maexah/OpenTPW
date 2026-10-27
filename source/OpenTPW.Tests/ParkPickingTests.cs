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
}
