namespace OpenTPW;

/// <summary>
/// What the pointer is over in the park: which cell of the map, and which thing is standing on it.
///
/// <para>
/// <b>Nothing in this project could answer that before.</b> The only mouse-to-world arithmetic in the
/// tree was the camcorder's look direction, so every verb that starts with pointing at the ground -
/// buying, placing, moving, selling, dropping a member of staff - had nothing to ask.
/// </para>
///
/// <para>
/// <b>The original picks by RAY CAST, not by inverting a grid.</b> <c>FUN_0045bf90</c> builds a ray
/// from the cursor, walks the terrain height grid testing the two triangles of each cell, then marches
/// the ray again against object meshes. <c>FUN_0045d560</c> turns the hit point into a packed cell id
/// and writes it to the one global everything else reads - and that packing is
/// <c>y * 0x80 + 1 + x</c>, which is exactly the <c>y * 128 + x + 1</c> this project already uses.
/// </para>
///
/// <para>
/// <b>A declared deviation: this marches and refines where the original intersects triangles.</b> The
/// step is a quarter of a cell and the crossing is then bisected, which lands the hit well inside the
/// cell it belongs to - the cell is the answer, and a hit anywhere in it gives the same one. Testing
/// two triangles per cell would be more faithful and is what to write if the sub-cell position ever
/// starts mattering (it would, for a ride that has to sit on a slope).
/// </para>
///
/// <para>
/// <b>The camera's own roll may NOT be used to build the ray.</b> <see cref="Camera.Rotation"/> comes
/// from <c>Rotation.LookAt</c>, whose roll is arbitrary and documented as such - only its forward
/// vector means anything. The view matrix is built by <c>CreateLookTo</c> against the world's up, so
/// the basis here is rebuilt the same way. Taking <c>Rotation.Right</c> instead would work only while
/// the roll happened to be zero.
/// </para>
/// </summary>
public static class ParkPicking
{
	/// <summary>
	/// The cell under the pointer, packed as <c>y * 128 + x + 1</c>, or <b>nought when the pointer is
	/// not over the map at all</b> - the original's own sentinel, and the reason the packing is
	/// one-based rather than plain.
	/// </summary>
	public static int Cell { get; private set; }

	/// <summary>Where the ray actually met the ground, for anything that wants more than a cell.</summary>
	public static Vector3 WorldPoint { get; private set; }

	/// <summary>
	/// The thing standing on the cell under the pointer, or nought - the original latches the same
	/// answer by asking the cell rather than by hit-testing the thing's own model.
	/// </summary>
	public static int ThingUnderCursor { get; private set; }

	/// <summary>The cell under the pointer as a pair, or false when the pointer is off the map.</summary>
	public static bool TryCell( out int cellX, out int cellY )
	{
		if ( Cell <= 0 )
		{
			cellX = cellY = 0;
			return false;
		}

		cellX = (Cell - 1) % ParkWorld.MapSize;
		cellY = (Cell - 1) / ParkWorld.MapSize;

		return true;
	}

	/// <summary>How far along the ray to look before giving up, in world units.</summary>
	/// <remarks>
	/// The park is 128 cells of ten units and the camera sits between 70 and 130 units out, so a ray
	/// that has travelled four thousand units either left the map or is running along the sky.
	/// </remarks>
	private const float Reach = 4000f;

	/// <summary>How far each step of the march goes, as a fraction of a cell.</summary>
	private const float StepOfACell = 0.25f;

	/// <summary>How many times the crossing is halved once the march has straddled the ground.</summary>
	private const int Refinements = 12;

	/// <summary>
	/// Works out what the pointer is over, once a frame.
	/// </summary>
	/// <remarks>
	/// <b>It reads the camera as the last frame left it.</b> The camera moves in <see cref="Level.Render"/>,
	/// after this runs, so the ray is built from the previous frame's eye - which is what the interface
	/// is already drawn against, and is invisible while the camera only moves on input. The alternative,
	/// picking after the camera, would put the answer a frame ahead of the picture instead.
	/// </remarks>
	public static void Update()
	{
		var answer = Resolve( Input.Mouse.Position );

		Cell = answer.Cell;
		WorldPoint = answer.World;
		ThingUnderCursor = answer.Thing;
	}

	/// <summary>What the ray through one point of the window meets.</summary>
	private readonly record struct Answer( int Cell, Vector3 World, int Thing );

	/// <summary>
	/// The whole of the picking arithmetic, given a position on the window. <see cref="Update"/> hands
	/// it the pointer; <see cref="PickAt"/> hands it whatever it is asked to.
	/// </summary>
	private static Answer Resolve( Vector2 screen )
	{
		if ( ParkGround.Current?.Heightfield is not { } field )
			return default;

		// Zero until the camera has run once - see Camera.CalcViewProjMatrix. A ray built from it on
		// the first frame would be a line straight down the forward axis.
		if ( Camera.HorizontalTangent <= 0f )
			return default;

		var direction = RayThrough( screen );

		if ( !MarchToGround( field, Camera.Position, direction, out var hit ) )
			return default;

		var cellX = (int)MathF.Floor( hit.X / field.CellSizeX );
		var cellY = (int)MathF.Floor( hit.Y / field.CellSizeY );

		if ( !ParkState.OnMap( cellX, cellY ) )
			return default;

		var thing = Level.Current?.ParkState is { } state
			? state.CellAt( cellX, cellY ).Occupant
			: 0;

		return new Answer( (cellY * ParkWorld.MapSize) + cellX + 1, hit, thing );
	}

	/// <summary>
	/// What the ray through an arbitrary point of the window meets, without disturbing what the
	/// pointer is over.
	///
	/// <para>
	/// <b>It exists because the pointer is not a thing a test can move.</b> Picking is a function of
	/// the mouse, so asking the running game "what is under the cursor" only ever reports wherever the
	/// cursor was left - and a harness that warps the pointer is then testing the window system as
	/// much as the arithmetic. This separates them: hand it a position and the answer depends on
	/// nothing but the camera and the land. It is the same justification the <c>arrive</c>,
	/// <c>load</c> and <c>thirst</c> commands already carry - an instrument that creates the condition
	/// it needs to measure.
	/// </para>
	/// </summary>
	public static string PickAt( float screenX, float screenY )
	{
		var answer = Resolve( new Vector2( screenX, screenY ) );

		return Describe( answer, $"asked ({screenX:F0},{screenY:F0})" );
	}

	/// <summary>
	/// The direction the pointer points, in world space.
	///
	/// <para>
	/// The basis is rebuilt against the world's up rather than taken from the camera's rotation - see
	/// the class remarks for why that is not a tidying. <see cref="Camera.HorizontalTangent"/> is how
	/// far the frustum reaches sideways one unit ahead, and it already carries the whole of the
	/// aspect-ratio rule this game applies to windows that are not 4:3, so the vertical reach is simply
	/// that over the aspect.
	/// </para>
	/// </summary>
	private static Vector3 RayThrough( Vector2 screen )
	{
		var forward = Camera.Rotation.Forward.Normal;

		// The same handedness the engine's own constants use: facing +X with Z up, this gives
		// (0,-1,0), which is exactly Vector3.Right.
		var right = Cross( forward, Vector3.Up ).Normal;
		var up = Cross( right, forward ).Normal;

		// Pixels to the -1..1 box. Down the screen is positive in pixels and negative here, which is
		// the flip every projection makes.
		var acrossFraction = (2f * (screen.X / MathF.Max( Screen.Width, 1f ))) - 1f;
		var downFraction = 1f - (2f * (screen.Y / MathF.Max( Screen.Height, 1f )));

		var across = Camera.HorizontalTangent;
		var vertical = across / MathF.Max( Screen.Aspect, 0.01f );

		return (forward + (right * (acrossFraction * across)) + (up * (downFraction * vertical))).Normal;
	}

	private static Vector3 Cross( Vector3 a, Vector3 b )
		=> new(
			(a.Y * b.Z) - (a.Z * b.Y),
			(a.Z * b.X) - (a.X * b.Z),
			(a.X * b.Y) - (a.Y * b.X) );

	/// <summary>
	/// Walks the ray forward until it passes below the ground, then halves the crossing to find where.
	/// Answers false where the ray never meets the land - pointing at the sky, or off the side of the
	/// map.
	/// </summary>
	private static bool MarchToGround( HeightfieldFile field, Vector3 from, Vector3 direction, out Vector3 hit )
	{
		hit = default;

		var step = MathF.Min( field.CellSizeX, field.CellSizeY ) * StepOfACell;

		if ( step <= 0f )
			return false;

		var previous = from;
		var wasAbove = previous.Z >= Ground( field, previous );

		for ( var travelled = step; travelled <= Reach; travelled += step )
		{
			var at = from + (direction * travelled);
			var above = at.Z >= Ground( field, at );

			// Above then below is the crossing. Starting below the ground - which the camera never is -
			// would otherwise report the first step as a hit.
			if ( wasAbove && !above )
			{
				hit = Bisect( field, previous, at );
				return true;
			}

			previous = at;
			wasAbove = above;
		}

		return false;
	}

	/// <summary>The land under a world position, holding the edge height outside the map.</summary>
	/// <remarks>
	/// Holding rather than falling away matters: a ray aimed past the edge of the park would otherwise
	/// dive through a wall of nothing and report a hit in mid-air. <see cref="ParkState.OnMap"/> is what
	/// actually rejects those, after the crossing is found.
	/// </remarks>
	private static float Ground( HeightfieldFile field, Vector3 at )
		=> field.HeightAtWorld( at.X, at.Y );

	private static Vector3 Bisect( HeightfieldFile field, Vector3 above, Vector3 below )
	{
		for ( var i = 0; i < Refinements; ++i )
		{
			var middle = (above + below) * 0.5f;

			if ( middle.Z >= Ground( field, middle ) )
				above = middle;
			else
				below = middle;
		}

		return (above + below) * 0.5f;
	}

	/// <summary>
	/// A one-line summary for the debug console. The cell is printed both packed and as a pair,
	/// because the packed form is what every engine-side note is written in and the pair is what a
	/// person reads.
	/// </summary>
	/// <remarks>
	/// <b>It reports the pointer it READ, not only the answer.</b> A probe that warps the pointer and
	/// then asks for a cell cannot tell "the ray is wrong" from "the game never saw the mouse move" -
	/// both look like an answer that will not follow the cursor. Printing the position the picker
	/// actually used separates them in one line, which is what the first run of this needed and did
	/// not have.
	/// </remarks>
	public static string State()
	{
		var mouse = Input.Mouse.Position;

		return Describe( new Answer( Cell, WorldPoint, ThingUnderCursor ),
			$"mouse ({mouse.X:F0},{mouse.Y:F0})" );
	}

	private static string Describe( Answer answer, string asked )
	{
		var size = $"of {Screen.Width:F0}x{Screen.Height:F0}";

		if ( answer.Cell <= 0 )
			return $"pick: not over the map - {asked} {size}";

		var cellX = (answer.Cell - 1) % ParkWorld.MapSize;
		var cellY = (answer.Cell - 1) / ParkWorld.MapSize;
		var occupant = answer.Thing != 0 ? $"thing {answer.Thing}" : "nothing";

		return $"pick: cell ({cellX},{cellY}) packed {answer.Cell} " +
			$"world ({answer.World.X:F1},{answer.World.Y:F1},{answer.World.Z:F1}) " +
			$"standing {occupant} - {asked} {size}";
	}
}
