using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// Where a guest is drawn, and which of the two possible answers is used - <c>ParkGuestSprites.Standing</c>.
///
/// <para>
/// <b>This file exists because a control run said it had to.</b> The sprite join was written, built and
/// green, and a mutation that made the drawing ignore the simulation entirely - putting every guest back at
/// the position the save left them - broke <b>no test at all</b>. The render path needs a graphics device
/// and cannot be unit tested; what can be, and what actually carries the claim, is the choice between the
/// simulation and the save and the arithmetic that converts one to the other. That is what is pinned here.
/// </para>
/// <para>
/// The cell size is passed in rather than read from a heightfield, so these are arithmetic tests with no
/// terrain to load. Ten is used because it is this park's own, but nothing here depends on that - the point
/// is that the same number the ground is laid out with is the one a guest is placed with.
/// </para>
/// </summary>
[TestClass]
public class ParkGuestPlacementTests
{
	private BaseFileSystem data = null!;

	[TestInitialize]
	public void MountTheGame() => data = GameData.Required();

	private const string ShippedPark = "levels/jungle/Easymode.TPWI";

	/// <summary>The cell size this park's ground is actually laid out with.</summary>
	private const float Cell = 10f;

	private ParkWorld World()
	{
		using var stream = new MemoryStream( data.ReadAllBytes( ShippedPark ) );
		return new ParkWorld( new SaveReader( stream ).ReadFile() );
	}

	/// <summary>Thing 42 with its picture, and a walk that can move it.</summary>
	private (ParkWorld.Person Person, ParkWorld.Sprite Sprite, PeepWalk Walk) Guest( ParkWorld world )
	{
		var person = world.People.Single( p => p.ThingId == 42 );
		var sprite = world.Sprites.Single( s => s.Slot == person.SpriteSlot );
		var walk = new PeepWalk( new PeepNavigator( person.Navigator ),
			CellEdge.For( world, ParkPeople.WalkingMode ).Blocked ) { Heading = person.Angle };

		return (person, sprite, walk);
	}

	/// <summary>
	/// <b>A guest who has walked is drawn where they walked to, not where the file left them.</b>
	///
	/// <para>
	/// The second half is the half that matters: asserting only that the answer equals the simulation's
	/// position would still pass if the simulation happened to sit on the saved position. So the saved
	/// position is asserted to be a <i>different</i> answer first - which is exactly the mutation that got
	/// through before this test existed.
	/// </para>
	/// </summary>
	[TestMethod]
	public void AGuestIsDrawnWhereTheSimulationHasWalkedThemAndNotWhereTheSaveLeftThem()
	{
		var world = World();
		var (person, sprite, walk) = Guest( world );

		Assert.IsTrue( walk.PlanRoute() );

		for ( var tick = 0; tick < 20; ++tick )
			walk.Step();

		var at = walk.Position;
		var (x, y, angle) = ParkGuestSprites.Standing( walk, Cell, Cell, person, sprite );

		// The conversion: 16.16 where one is a map cell, times the size the ground lays a cell out at.
		Assert.AreEqual( (at.X / (float)FixedVector.One) * Cell, x, 0.0001f );
		Assert.AreEqual( (at.Y / (float)FixedVector.One) * Cell, y, 0.0001f );
		Assert.AreEqual( walk.Heading, angle );

		// The guard, and it is the half that kills a mutation drawing everyone at their saved position.
		Assert.AreNotEqual( sprite.X, x, "they are no longer drawn at the saved x" );
		Assert.AreNotEqual( sprite.Y, y, "nor the saved y" );

		// <b>The heading is deliberately NOT a guard here, and the reason is worth more than the guard
		// would have been.</b> This test first asserted that the angle must differ from the file's too, by
		// symmetry with the two above - and it failed. After twenty ticks the walk works out 1067 and the
		// save separately records 1067. That is not a value that failed to move: it is two independent
		// sources agreeing on an eleven-bit heading, because thing 42 walks the way the file already had
		// them facing. The walk is told nothing about the saved angle except as a starting value, and a
		// mirrored or rotated arctangent table would not land back on it.
		Assert.AreEqual( person.Angle, angle,
			"the walk arrives at the very heading the save recorded, having been told none of it" );
	}

	/// <summary>
	/// With no simulation running this park, the saved position is what gets drawn - which is what happened
	/// before any of this existed, so a park with nobody simulating looks exactly as it did.
	/// </summary>
	[TestMethod]
	public void WithNoSimulationTheSavedPositionIsDrawn()
	{
		var world = World();
		var (person, sprite, _) = Guest( world );

		var (x, y, angle) = ParkGuestSprites.Standing( null, Cell, Cell, person, sprite );

		Assert.AreEqual( sprite.X, x );
		Assert.AreEqual( sprite.Y, y );
		Assert.AreEqual( person.Angle, angle );
	}

	/// <summary>
	/// <b>And before the ground has loaded, likewise.</b> The cell size comes from the heightfield, and
	/// until there is one there is no scale to place anybody at - so the answer falls back rather than
	/// multiplying by zero and stacking the whole park on the origin.
	/// </summary>
	[TestMethod]
	public void BeforeTheGroundIsLoadedTheSavedPositionIsDrawn()
	{
		var world = World();
		var (person, sprite, walk) = Guest( world );

		Assert.IsTrue( walk.PlanRoute() );

		for ( var tick = 0; tick < 20; ++tick )
			walk.Step();

		var (x, y, angle) = ParkGuestSprites.Standing( walk, 0f, 0f, person, sprite );

		Assert.AreEqual( sprite.X, x, "no cell size, so no placement - the saved one stands" );
		Assert.AreEqual( sprite.Y, y );
		Assert.AreEqual( person.Angle, angle );
	}

	/// <summary>
	/// The scale is a multiplication and nothing else: a guest one whole cell further along is drawn one
	/// whole cell size further along, and not moved at all on the other axis.
	///
	/// <para>
	/// <b>This replaced a version of itself that was vacuous, and it is worth saying why rather than
	/// quietly swapping it.</b> The first attempt built two walks from the same person, moved neither, and
	/// asserted they landed in the same place. Every assertion in it was true; not one of them was about
	/// the scale its name promised. Two navigators placed a known distance apart is what actually tests it.
	/// </para>
	/// </summary>
	[TestMethod]
	public void OneCellOfWalkingIsOneCellSizeOfDrawing()
	{
		var world = World();
		var (person, sprite, _) = Guest( world );
		var blocked = CellEdge.For( world, ParkPeople.WalkingMode ).Blocked;

		var here = ParkGuestSprites.Standing( new PeepWalk( At( 40, 20 ), blocked ),
			Cell, Cell, person, sprite );

		var across = ParkGuestSprites.Standing( new PeepWalk( At( 41, 20 ), blocked ),
			Cell, Cell, person, sprite );

		var down = ParkGuestSprites.Standing( new PeepWalk( At( 40, 21 ), blocked ),
			Cell, Cell, person, sprite );

		Assert.AreEqual( Cell, across.X - here.X, 0.0001f, "one cell along x is one cell size along x" );
		Assert.AreEqual( 0f, across.Y - here.Y, 0.0001f, "and nothing at all along y" );

		Assert.AreEqual( Cell, down.Y - here.Y, 0.0001f, "one cell along y is one cell size along y" );
		Assert.AreEqual( 0f, down.X - here.X, 0.0001f, "and nothing at all along x" );
	}

	/// <summary>A navigator standing exactly on a cell corner, so the arithmetic divides cleanly.</summary>
	private static PeepNavigator At( int cellX, int cellY )
		=> new( new ParkWorld.NavigatorState(
			X: cellX * FixedVector.One, Y: cellY * FixedVector.One,
			VelocityX: 0, VelocityY: 0, TargetX: 0, TargetY: 0,
			Mass: ParkWorld.NavigatorState.DefaultMass, Radius: ParkWorld.NavigatorState.DefaultRadius,
			MaxForce: 31457, MaxSpeed: 15728, NavMode: 0, CantReachDest: 0, PathFinished: false,
			PathCount: 0, PathTotalCount: 0, PathBufferCount: 0,
			BufferedDistance: 0, TailDistance: 0, TotalDistance: 0, StuckBits: 0 ) );
}
