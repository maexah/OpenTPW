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
	/// position is asserted to be a <i>different</i> answer first - which is what catches the mutation that draws
	/// everyone at the position the save left them.
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
		// would have been.</b> The angle cannot be asserted to differ from the file's, by symmetry with the
		// two above: after twenty ticks the walk works out 1067 and the
		// save separately records 1067. That is not a value that failed to move: it is two independent
		// sources agreeing on an eleven-bit heading, because thing 42 walks the way the file already had
		// them facing. The walk is told nothing about the saved angle except as a starting value, and a
		// mirrored or rotated arctangent table would not land back on it.
		Assert.AreEqual( person.Angle, angle,
			"the walk arrives at the very heading the save recorded, having been told none of it" );
	}

	/// <summary>
	/// With no simulation running this park, the saved position is what gets drawn, so a park with nobody
	/// simulating looks exactly as its save left it.
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
	/// <b>A guest a ride is carrying is drawn on the ride, not where they walked to.</b> Alexah found
	/// this by playing: the children never appeared on the Belly Bounce - their sprite stayed at the
	/// front of the queue until the ride was over.
	///
	/// <para>
	/// Half of that is the original's own behaviour, which is what made it confusing rather than plainly
	/// broken: nothing in the engine moves a rider either. All five callers of its "place a person"
	/// routine are accounted for and not one is a rider, so a rider's walk goes on reporting the cell
	/// they queued on and the DRAWING is what puts them on a node of the ride's own model.
	/// </para>
	/// </summary>
	/// <remarks>
	/// <b>Written for the same reason this whole file was.</b> Resolving a seat needs
	/// <c>ParkObjects</c>, which wants a graphics device, so no test reaches the seat lookup: one that
	/// answered nothing would put every rider back in the queue and the suite would not say a word. The
	/// arithmetic below needs no device, so the CHOICE is pinned here;
	/// that a seat is correctly resolved rests on the ride census and the screenshots.
	/// </remarks>
	[TestMethod]
	public void AGuestARideIsCarryingIsDrawnOnTheRide()
	{
		// Where the walk says they are - the queue cell, where a rider's walk goes on reporting them.
		const float WalkX = 525.4f;
		const float WalkY = 234.0f;
		const float Ground = 0.5f;
		const float SpriteHeight = 3f;

		// And where the ride says they are: one of the Belly Bounce's ten body nodes, up in the air.
		var seat = new Vector3( 525.4f, 252.6f, 10.3f );

		var on = ParkGuestSprites.Centre( seat, WalkX, WalkY, Ground, SpriteHeight );

		Assert.AreEqual( seat.X, on.X, 0.0001f, "a rider is drawn at their seat's x" );
		Assert.AreEqual( seat.Y, on.Y, 0.0001f, "and its y - this is the one the bug got wrong" );
		Assert.AreEqual( seat.Z + SpriteHeight, on.Z, 0.0001f,
			"and above it by the sprite's own offset, exactly as a walker stands above the land" );

		// <b>The guard that kills the bug.</b> Drawing them at the walk position is precisely what left
		// the children standing in the queue for the whole ride, so the two must not agree.
		Assert.AreNotEqual( WalkY, on.Y, "a rider drawn at the walk's y is the bug itself" );

		// And with nobody carrying them, the ground answer is unchanged - so this cannot have been
		// bought by breaking everybody who is merely walking about.
		var walking = ParkGuestSprites.Centre( null, WalkX, WalkY, Ground, SpriteHeight );

		Assert.AreEqual( WalkX, walking.X, 0.0001f );
		Assert.AreEqual( WalkY, walking.Y, 0.0001f );
		Assert.AreEqual( Ground + SpriteHeight, walking.Z, 0.0001f,
			"the land under them is what decides where their feet go" );
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
	/// <b>Two navigators placed a known distance apart is what tests the scale.</b> Two walks built from
	/// the same person and moved by neither land in the same place whatever the scale is.
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

	/// <summary>
	/// <b>The blend: nought draws where the tick started, one where it ended, and a half half way
	/// between.</b> That is the arithmetic of <c>FUN_004f9f00</c> - <c>prev + (cur - prev) * t</c> -
	/// whose two halves sit at <c>0x004f9f89</c> and <c>0x004f9fb6</c>.
	///
	/// <para>
	/// <b>The default is asserted as carefully as the blend, because that is what makes the change
	/// safe.</b> One is the default, and alpha one returns the position the simulation actually reached,
	/// so every caller that knows nothing about frames - including four of the tests above - is answered
	/// as if nothing were blended.
	/// </para>
	/// </summary>
	[TestMethod]
	public void TheDrawnPositionIsBlendedBetweenTheTwoPositionsOfATick()
	{
		var world = World();
		var (person, sprite, _) = Guest( world );
		var blocked = CellEdge.For( world, ParkPeople.WalkingMode ).Blocked;

		// Mid-tick: stamped standing on (40,20), and since moved to (41,20).
		var navigator = At( 40, 20 );
		var walk = new PeepWalk( navigator, blocked );

		navigator.Position = new FixedVector( 41 * FixedVector.One, 20 * FixedVector.One );

		Assert.AreEqual( 40 * Cell, walk.Previous.X / (float)FixedVector.One * Cell, 0.0001f,
			"the stamp should still hold where the tick started" );

		var started = ParkGuestSprites.Standing( walk, Cell, Cell, person, sprite, 0f );
		var halfway = ParkGuestSprites.Standing( walk, Cell, Cell, person, sprite, 0.5f );
		var arrived = ParkGuestSprites.Standing( walk, Cell, Cell, person, sprite, 1f );
		var byDefault = ParkGuestSprites.Standing( walk, Cell, Cell, person, sprite );

		Assert.AreEqual( 40 * Cell, started.X, 0.0001f, "alpha nought is where the tick started" );
		Assert.AreEqual( 41 * Cell, arrived.X, 0.0001f, "alpha one is where it ended" );
		Assert.AreEqual( 40.5f * Cell, halfway.X, 0.0001f, "and a half is half way between the two" );

		Assert.AreEqual( arrived.X, byDefault.X, 0.0001f, "the default should be alpha one" );
		Assert.AreEqual( arrived.Y, byDefault.Y, 0.0001f );

		// Nothing moved on the other axis, so nothing is blended along it either.
		Assert.AreEqual( 20 * Cell, started.Y, 0.0001f );
		Assert.AreEqual( 20 * Cell, halfway.Y, 0.0001f );
	}

	/// <summary>
	/// <b>The heading is NOT blended, and that is the original's own choice rather than something left
	/// out here.</b> <c>0x004fa015</c> copies the octant straight off the thing into the out-param while
	/// the two positions either side of it are being interpolated - so a guest's position glides and
	/// their facing snaps, about four times a second.
	/// </summary>
	[TestMethod]
	public void TheHeadingIsNotBlendedWithThePosition()
	{
		var world = World();
		var (person, sprite, _) = Guest( world );
		var blocked = CellEdge.For( world, ParkPeople.WalkingMode ).Blocked;

		var navigator = At( 40, 20 );
		var walk = new PeepWalk( navigator, blocked ) { Heading = 512 };

		navigator.Position = new FixedVector( 41 * FixedVector.One, 21 * FixedVector.One );

		var started = ParkGuestSprites.Standing( walk, Cell, Cell, person, sprite, 0f );
		var halfway = ParkGuestSprites.Standing( walk, Cell, Cell, person, sprite, 0.5f );
		var arrived = ParkGuestSprites.Standing( walk, Cell, Cell, person, sprite, 1f );

		// The position moved across all three - so the angle holding still is a result, not a vacuum.
		Assert.AreNotEqual( started.X, arrived.X, "the position should differ across the tick" );

		// Both axes are blended: a walk along y glides as a walk along x does.
		Assert.AreEqual( 20 * Cell, started.Y, 0.0001f );
		Assert.AreEqual( 20.5f * Cell, halfway.Y, 0.0001f );
		Assert.AreEqual( 21 * Cell, arrived.Y, 0.0001f );

		Assert.AreEqual( 512, started.Angle );
		Assert.AreEqual( 512, halfway.Angle );
		Assert.AreEqual( 512, arrived.Angle );
	}

	/// <summary>
	/// <b>Somebody standing still is drawn standing still, whatever the frame.</b>
	///
	/// <para>
	/// Where the stamp is made is a trap. The original stamps previous := current in <c>FUN_004fa870</c>,
	/// the first call of <b>every</b> person's tick handler and ahead of the guest handler's own
	/// <c>(id &amp; 3)</c> stagger, so it happens whatever state they are in. Stamping it inside the walk
	/// instead - which is the obvious place and the wrong one - would leave a guest who had stopped holding
	/// two different positions for ever, and the drawing would swing them between the two on every frame,
	/// about a quarter of a cell, for as long as they stood there.
	/// </para>
	/// <para>
	/// This test stamps by hand, so it pins what the drawing makes of a stamp and not where the park makes
	/// it. <c>ParkTickTests.EverySweepStampsEverybodyWhereTheyStoodAsItBegan</c> pins that, by running the
	/// shipped park.
	/// </para>
	/// </summary>
	[TestMethod]
	public void AGuestWhoHasStoppedIsDrawnInOnePlace()
	{
		var world = World();
		var (person, sprite, _) = Guest( world );
		var blocked = CellEdge.For( world, ParkPeople.WalkingMode ).Blocked;

		var navigator = At( 40, 20 );
		var walk = new PeepWalk( navigator, blocked );

		// They moved last tick...
		navigator.Position = new FixedVector( 41 * FixedVector.One, 20 * FixedVector.One );

		// ...and then a tick began in which they did not, which is exactly what the stamp records.
		navigator.StampPrevious();

		foreach ( var alpha in new[] { 0f, 0.25f, 0.5f, 0.75f, 1f } )
		{
			var at = ParkGuestSprites.Standing( walk, Cell, Cell, person, sprite, alpha );

			Assert.AreEqual( 41 * Cell, at.X, 0.0001f, $"alpha {alpha} moved a standing guest" );
			Assert.AreEqual( 20 * Cell, at.Y, 0.0001f, $"alpha {alpha} moved a standing guest" );
		}
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
