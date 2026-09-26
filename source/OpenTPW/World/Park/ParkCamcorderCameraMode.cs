using System.Numerics;

namespace OpenTPW;

/// <summary>
/// Camcorder mode - a park seen from the ground at eye height, which is the original's first-person
/// view.
///
/// <para>
/// <b>It is what makes a park's sky worth having.</b> A park builds a real one - the theme's own sky
/// folder, centred on the origin at <see cref="Sky.ParkHeight"/> and untinted - but from
/// <see cref="ParkOrbitCameraMode"/>, looking down at forty-five degrees or more, it is only a margin
/// around the edge of a top-down view. From the ground it fills the upper half of the frame with its
/// cloud band legible. <b>Measured by capture, not asserted</b>: the orbit camera shows some sky, just
/// not as anything you would call a sky.
/// </para>
///
/// <para>
/// The original reaches it two ways, and both are already named here: the 'C' key
/// (<see cref="InputButton.CamcorderMode"/>, shortcuts action 16) and button id 99 on the park
/// management gadget, <b>which exists and wires this up</b> - see <c>ParkGadget</c>, where that button's
/// click calls <see cref="Enter"/>. Both end at the same place - the chain, the shortcuts
/// table and the button ids are all in the park engine notes under "Camcorder mode".
/// </para>
///
/// <para>
/// Some of its numbers come from the original's camera update (FUN_0042b1c0) and some are chosen, and
/// the difference is marked at each one. That function splits on <c>gui_CameraFlags</c>: the branch the
/// orbit camera takes swings the eye away from a point of interest, while the branch this follows
/// zeroes that offset and puts the eye <b>at</b> the position, with the look direction coming from
/// where the pointer is.
/// </para>
///
/// <para>
/// <b>It is not a faithful copy.</b> The exe
/// has two first-person sub-branches and they are mutually exclusive: only the one selected by
/// <c>(flags &amp; 0x3c) == 0</c> sets an eye height, and it pairs that with the +/-0.4 bands and a
/// scale of 2.0, while the 3.5 belongs to the +/-0.2 bands of the <i>other</i> sub-branch, which sets
/// no eye height at all. This follows the first of those. Which flag bits choose between them is not
/// traced, so the yaw control law is a reading rather than a reproduction, and the walk speed is
/// frankly a choice - both are marked where they are defined.
/// </para>
/// </summary>
public sealed class ParkCamcorderCameraMode : CameraMode
{
	/// <summary>
	/// Where the viewer stands, in world units. Only X and Y are used - the ground decides the height,
	/// so writing a Z here does nothing.
	///
	/// <para>
	/// Static, like <see cref="ParkOrbitCameraMode"/>'s own state and for the same two reasons:
	/// <see cref="Camera.SetCameraMode{T}"/> builds a fresh instance every time the mode changes, so an
	/// instance field would forget where the viewer was standing the moment they looked away and back;
	/// and the debug console drives it without holding the instance.
	/// </para>
	/// </summary>
	public static Vector3 Stand { get; set; }

	/// <summary>Which way the viewer faces, in radians. Zero looks along +Y, as the orbit camera does.</summary>
	public static float Yaw { get; set; }

	/// <summary>How far up or down they are looking, in degrees. Positive is up.</summary>
	public static float Pitch { get; set; }

	/// <summary>
	/// Whether the park is being looked at from the ground right now. Kept here rather than asked of
	/// <see cref="Camera"/> so that nothing outside has to know how a camera mode is stored.
	/// </summary>
	public static bool Active { get; private set; }

	/// <summary>
	/// How far the eye sits above the ground, in world units.
	///
	/// <para>
	/// Read, not chosen: the original's first-person branch sets its eye height to the sampled ground
	/// height minus <c>_DAT_006fde10</c>, which holds <b>-5</b> - so ground plus five. A cell is ten
	/// units across, making this half a cell, which is a sensible eye height at this world's scale.
	/// </para>
	/// </summary>
	public const float EyeHeight = 5f;

	/// <summary>
	/// A ceiling on how far up or down the view can tip, in degrees.
	///
	/// <para>
	/// <b>Ours, not the original's, and it never actually bites.</b> The 1.4708 radians this borrows is
	/// clamped in exactly one place in the whole executable and it is the <i>orbit</i> branch, on its
	/// zoom-derived pitch; the first-person pitch is never clamped there at all, because mapping it
	/// from the pointer bounds it by construction. <b>This camera's own bound is +/-41.25 degrees</b> -
	/// <see cref="Beyond"/> caps at <c>1 - 0.4 = 0.6</c> and <see cref="PitchScale"/> is 1.2 - so the
	/// clamp is a guard against a future change to the mapping and nothing more.
	/// </para>
	/// <para>
	/// The original's own extremes are about 55 degrees up and 34 down, and those come from a mapping
	/// this file does not implement - see <see cref="DeadZone"/>. Do not read them as this camera's.
	/// </para>
	/// </summary>
	private const float MaxPitch = 84.2673f;

	/// <summary>
	/// How far the pointer has to leave the middle of the screen before the view turns, as a fraction
	/// of half the screen.
	///
	/// <para>
	/// <b>The look is steered by where the pointer is, not by how far it moved.</b> That is worth
	/// saying because free mouse-look is the obvious guess and it is wrong: the original converts the
	/// pointer to normalised screen coordinates and compares them against bands at <c>+/-0.2</c> and
	/// <c>+/-0.4</c>, turning at a rate once outside. Inside the band nothing happens at all.
	/// </para>
	/// <para>
	/// <b>Only the outer band is reproduced, and only for yaw.</b> Which band applies to yaw depends on
	/// bits of <c>gui_CameraFlags</c> that are not traced, and guessing at a sub-mode would be
	/// inventing behaviour rather than copying it.
	/// </para>
	/// <para>
	/// <b>Pitch is a simplification, marked here because it is a choice.</b> The original does not use
	/// a symmetric band for it at all: it mixes the two numbers into one asymmetric band, normalised y
	/// in <c>[-0.4, +0.2]</c>, in both sub-branches. This uses the same <c>+/-0.4</c> both ways, which
	/// is even where the original is not - see <see cref="PitchScale"/>.
	/// </para>
	/// </summary>
	private const float DeadZone = 0.4f;

	/// <summary>
	/// Scales how far the pointer is past <see cref="DeadZone"/> into radians a second of turn, so the
	/// turn accelerates as the pointer goes further out and is zero inside the band.
	///
	/// <para>
	/// <b>2.0 goes with the 0.4 band.</b> The original carries two pairs - <c>_DAT_006fdddc</c> 3.5
	/// with its +/-0.2 bands and <c>_DAT_006fdde8</c> 2.0 with its +/-0.4 ones - so the 0.4 band with
	/// the 3.5 would be neither of the original's settings.
	/// </para>
	/// </summary>
	private const float YawRate = 2.0f;

	/// <summary>
	/// Radians of pitch per unit of deflection past <see cref="DeadZone"/> - <c>_DAT_006fddf0</c>, 1.2.
	/// Not a rate: the angle is mapped straight from where the pointer is, so it settles rather than
	/// winding on. See <see cref="Steer"/>.
	///
	/// <para>
	/// <b>Applied both ways here, which the original does not do.</b> There the 1.2 scales only the
	/// upper side of the pitch band; the lower side is an unscaled <c>-y - 0.4</c>. Using one scale
	/// symmetrically is a simplification, and it is why this camera's extremes are even at 41.25
	/// degrees where the original's are 55 up and 34 down.
	/// </para>
	///
	/// <para>
	/// It bounds the view by itself, which is why <see cref="MaxPitch"/> never comes into it:
	/// <see cref="Beyond"/> can return at most <c>1 - 0.4 = 0.6</c>, so the pitch saturates at
	/// <c>0.6 * 1.2</c> radians, a little over 41 degrees either way.
	/// </para>
	/// </summary>
	private const float PitchScale = 1.2f;

	/// <summary>
	/// How fast the viewer walks, in world units a second. <b>A choice, not a finding</b> - the
	/// original's speed is folded into the flag-branch integration in FUN_0042b1c0 along with an
	/// acceleration and a drag term, and reading a single number out of that would be guessing. This is
	/// a fifth of <see cref="ParkOrbitCameraMode"/>'s scroll, which reads as a walk rather than a glide.
	/// </summary>
	private const float WalkSpeed = 40f;

	/// <summary>As <see cref="ParkOrbitCameraMode"/>'s, so the eye rides the ground the same way.</summary>
	private const float GroundFollowRate = 22.36f;

	private float _groundHeight;
	private bool _groundSampled;

	/// <summary>
	/// Set when the viewer has been put somewhere else outright rather than walking there, so that the
	/// next ground sample is taken as-is instead of eased into - see <see cref="StandAt"/>. Static
	/// because the thing doing the putting has no instance to talk to: the camera mode is rebuilt from
	/// scratch whenever the camera changes.
	/// </summary>
	private static bool _restand;

	/// <summary>
	/// Stands the viewer where the orbit camera was looking, facing the way it faced, and hands the
	/// camera over. Called when the player asks for camcorder mode.
	/// </summary>
	public static void Enter()
	{
		// The camcorder is an interaction mode of its own, installed through the setter over whatever was
		// held (FUN_00481a10), so the hand is let go of first.
		if ( ParkHand.LetGo() is { } letGo )
			Log.Info( $"Hand: {letGo}" );

		Stand = ParkOrbitCameraMode.PointOfInterest;
		Yaw = ParkOrbitCameraMode.Yaw;
		Pitch = 0f;
		Active = true;

		Camera.SetCameraMode<ParkCamcorderCameraMode>();
	}

	/// <summary>Puts the orbit camera back, looking at wherever the viewer had walked to.</summary>
	public static void Leave()
	{
		ParkOrbitCameraMode.PointOfInterest = new Vector3( Stand.X, Stand.Y, 0f );
		ParkOrbitCameraMode.Yaw = Yaw;
		Active = false;

		// Only a park gets the park camera. Nothing should be able to ask for this outside one, but
		// handing the lobby a camera that orbits park coordinates is a bad enough outcome to be worth
		// refusing outright rather than relying on every caller to have checked.
		if ( Level.Current?.Kind == Level.Scene.Park )
			Camera.SetCameraMode<ParkOrbitCameraMode>();
	}

	/// <summary>
	/// Drops the ground-level view as a scene ends, so nothing of it survives into the next one - see
	/// <see cref="Level.Unload"/>. The state here is static, because a camera mode is rebuilt from
	/// scratch every time the camera changes, so it outlives the scene unless something says otherwise.
	/// </summary>
	/// <remarks>
	/// That includes the edge test <see cref="EdgeTest"/> keeps, which holds the left park's save: the
	/// next park replaces it only at its first step on the ground, and a park with no save never does.
	/// The original's edge test reads its live world on every step, and that world is freed on leaving
	/// (<c>FUN_00409180</c>; <c>docs/exe/park-engine.md</c>, "Walking on the ground").
	/// </remarks>
	public static void Forget()
	{
		Active = false;
		Stand = Vector3.Zero;
		Yaw = 0f;
		Pitch = 0f;

		_blockedFor = null;
		_blocked = null;
	}

	/// <summary>
	/// Puts the viewer down somewhere else outright, and takes the ground there as-is.
	/// </summary>
	/// <remarks>
	/// <see cref="Stand"/> on its own is what <see cref="Walk"/> writes a step at a time, and the eye
	/// eases up to the ground so that walking over a ridge lifts it rather than stepping it. Being
	/// picked up and put down is not walking: easing from the height of wherever the viewer used to be
	/// is meaningless, and with the clock stopped - which is how frames are captured - the ease never
	/// advances at all, so the eye would stay at the old height for good.
	/// </remarks>
	public static void StandAt( Vector3 where )
	{
		Stand = where;
		_restand = true;
	}

	public ParkCamcorderCameraMode()
	{
		// The same lens the orbit camera uses - see its constructor for why 90 is a vertical angle.
		FieldOfView = 90f;

		// Already standing where Enter put the viewer, before anything reads the camera - see Place.
		// The ground is PEEKED rather than sampled: GroundUnderStand spends its one un-eased sample on
		// its first call, and whoever spends it decides where the eye snaps to. The debug console sets
		// Stand AFTER Enter has built this, so spending it here would leave the eye at the height of
		// wherever the orbit camera had been looking - and with the clock paused, which is how frames
		// are captured, the ease that should correct it never advances at all.
		Place( PeekGround() );
	}

	public override void Update()
	{
		// Nothing moves while a menu holds the park - the same test the game clock stops on. Without
		// it the view goes on turning behind an open menu for as long as the pointer sits outside the
		// dead zone, which it usually does, since the player is reaching for a button.
		if ( Level.Current?.PausedByWindow() == true )
			return;

		if ( Input.Pressed( InputButton.CamcorderMode ) )
		{
			Leave();
			return;
		}

		Steer();
		Walk();

		Place( GroundUnderStand() );
	}

	/// <summary>
	/// Puts the eye where the viewer stands, at head height, looking the way they are looking.
	/// </summary>
	/// <remarks>
	/// Called from the constructor as well as from <see cref="Update"/>.
	/// <see cref="Camera.SetCameraMode{T}"/> builds a fresh instance, and <see cref="Camera.Update"/>
	/// builds the view matrix from it in the same call that swapped it - so a mode that waited for
	/// its first update would have a frame drawn, and the sound heard, from the world origin.
	/// </remarks>
	private void Place( float ground )
	{
		Position = new Vector3( Stand.X, Stand.Y, ground + EyeHeight );

		var pitch = Pitch.DegreesToRadians();
		var flat = MathF.Cos( pitch );

		// Yaw zero looks along +Y, which is the direction the orbit camera looks when its own yaw is
		// zero: it sits at -Y of its point of interest and looks back at it.
		Rotation = Rotation.LookAt( new Vector3(
			-MathF.Sin( Yaw ) * flat,
			MathF.Cos( Yaw ) * flat,
			MathF.Sin( pitch ) ) );
	}

	/// <summary>
	/// Turns the view according to how far the pointer is from the middle of the screen - see
	/// <see cref="DeadZone"/> for why it is the position and not the movement.
	/// </summary>
	private void Steer()
	{
		var size = Screen.Size;

		if ( size.X <= 0f || size.Y <= 0f )
			return;

		// Normalised to -1..1 with zero in the middle, which is the form the original compares against
		// its bands. Y is negated so that pushing the pointer up looks up.
		var acrossX = ((Input.Mouse.Position.X / size.X) * 2f) - 1f;
		var acrossY = 1f - ((Input.Mouse.Position.Y / size.Y) * 2f);

		// Minus, not plus. Increasing Yaw turns the view LEFT here - the look direction is
		// (-sin Yaw, cos Yaw, .), whose derivative points along screen-left at every yaw - so adding a
		// positive deflection would turn away from the pointer. Measured in the running game to be
		// sure of it: the pointer held at the right edge takes yaw 0.000 to -1.950 to -3.030, turning
		// toward the pointer.
		//
		// The exe does not settle the sign: it SUBTRACTS its yaw step's _DAT_006fdda8 = -0.0012 term, so
		// its net step is positive for a pointer on the right, and what that does on screen needs
		// FUN_0046f650's matrix convention, which is not traced. The geometry above and the measurement
		// stand on their own.
		Yaw -= Beyond( acrossX ) * YawRate * Time.Delta;
		// Pitch is ASSIGNED, not accumulated. The original works out where to look from where the
		// pointer IS - no frame delta anywhere near it - and hands the result to the same argument the
		// orbit branch fills with its zoom-derived pitch ANGLE, which is what fixes 1.2 as radians of
		// pitch per unit of deflection rather than a turn rate. So parking the pointer at the top of
		// the screen settles at a fixed look-up instead of winding round until it meets a limit.
		Pitch = (Beyond( acrossY ) * PitchScale).RadiansToDegrees().Clamp( -MaxPitch, MaxPitch );
	}

	/// <summary>How far past the dead zone a normalised coordinate is, and zero inside it.</summary>
	private static float Beyond( float across )
	{
		if ( across > DeadZone )
			return across - DeadZone;

		if ( across < -DeadZone )
			return across + DeadZone;

		return 0f;
	}

	/// <summary>
	/// The mode the original's camcorder asks the edge test on - the fourth argument of
	/// <c>FUN_004d8750</c>, pushed as a literal <b>2</b> at both of its call sites inside
	/// <c>FUN_0042b1c0</c> (<c>0x0042c093</c> for the X axis, <c>0x0042c290</c> for the Y).
	///
	/// <para>
	/// That is the <i>strict</i> mode, not the <c>0</c> a guest walks on: on 2 the approach stops being
	/// freely passable and a path-to-open-ground step is allowed outright. See <see cref="CellEdge"/>,
	/// whose constructor documents what each number changes.
	/// </para>
	/// </summary>
	private const int WalkingMode = 2;

	/// <summary>How many world units a cell is across: <c>_DAT_006fdd7c</c>, 10, which turns a cell back into a position.</summary>
	private const float CellSize = 10f;

	/// <summary>Cells to a world unit: <c>_DAT_006fdd58</c>, 0.1, which the sweep multiplies a position by.</summary>
	private const float Tenth = 0.1f;

	/// <summary>
	/// Where in a cell a viewer going positive is parked, or put back: <c>cell * 10 + 9.999</c>
	/// (<c>0x0074c9c4</c>). Going negative it is <c>cell * 10</c>, which is still that cell.
	/// </summary>
	private const float FarSide = 9.999f;

	/// <summary>
	/// How far a position that landed on a boundary without leaving its cell is pushed on, the way the rest
	/// of the step goes: <c>0x0074c9d0</c>.
	/// </summary>
	private const float Nudge = 0.001f;

	/// <summary>
	/// What an exact tie divides the X step by, and multiplies X's reach by, so that Y is asked:
	/// <c>0x0074c9c8</c>, 1.01.
	/// </summary>
	private const float TieBreak = 1.01f;

	/// <summary>
	/// A step inside this band counts as no step at all - <c>_DAT_006fde00</c> and
	/// <c>_DAT_006fde04</c>, which the original compares each axis against before it sweeps anything.
	/// </summary>
	private const float StillnessBand = 1e-4f;

	/// <summary>The reach of an axis that is not moving: past the 1 that means "the whole step".</summary>
	private const float NoReach = 2f;

	/// <summary>
	/// A ceiling on the passes of one sweep. <b>Ours</b>: the original loops until the step is spent. Every
	/// pass spends part of a step, zeroes one or ends the sweep, so a frame's walk takes a handful. A NaN is
	/// never spent here: the x87's compares read one as nought, which ends the original's sweep, and C#'s
	/// read it as nothing at all, so this cap is what ends it.
	/// </summary>
	private const int MaxPasses = 1024;

	/// <summary>
	/// Walks the viewer about, facing-relative, swept against the cell edges by <see cref="Slide"/>.
	/// </summary>
	private void Walk()
	{
		// The original also walks forward while a right press on the viewfinder's layer is held, as far as the Up arrow
		// takes it, whatever RMB cancel is set to (0x0042b935). Not built (docs/QUEUE.md Q121). Counted every frame the
		// button is down, a press held on the eject button included, where the original does not walk.
		if ( Input.Mouse.Right )
			Unimplemented.Report( "FIRST_PERSON_RIGHT_BUTTON_WALK" );

		if ( Input.Forward == 0f && Input.Right == 0f )
			return;

		Step( Input.Forward, Input.Right, WalkSpeed * Time.Delta );
	}

	/// <summary>
	/// One step of the walk, facing-relative and swept - the body <see cref="Walk"/> runs with the real
	/// keys, and the one the debug console drives.
	/// </summary>
	/// <remarks>
	/// Shared rather than copied: a harness cannot press a
	/// key, so if the console had its own copy of this the thing measured would be the copy. Only the
	/// reading of <see cref="Input"/> is skipped; the trig, the sweep, the edge test and the clamp are all
	/// the ones a player gets.
	/// </remarks>
	internal static void Step( float forward, float right, float distance )
	{
		var extent = ParkWorld.MapSize * 10f;

		var dx = ((forward * -MathF.Sin( Yaw )) + (right * MathF.Cos( Yaw ))) * distance;
		var dy = ((forward * MathF.Cos( Yaw )) + (right * MathF.Sin( Yaw ))) * distance;

		// No park means no cells to ask about - a scene that is not a park cannot reach this camera, but
		// the sweep is written so that the answer without one is the plain step it always was.
		var walked = Slide( Stand, dx, dy, EdgeTest( Level.Current?.ParkState?.Park ),
			RideAt( Level.Current?.ParkState, Level.Current?.Catalogue ) );

		// The clamp is ours. The original clamps nothing: its bound is soft, on the velocity, at the 96 by 85
		// heightfield, and it stands the viewer only on a walkable cell inside that, so its sweep never comes
		// near the map's edge (docs/exe/park-engine.md, "Entering and leaving first person"). Enter stands the
		// viewer wherever the orbit looks (docs/QUEUE.md Q25), and this holds them on the map.
		Stand = new Vector3( walked.X.Clamp( 1f, extent ), walked.Y.Clamp( 1f, extent ), 0f );
	}

	/// <summary>
	/// Walks for a number of frames at the ordinary walking speed, for the debug console - the capture
	/// this camera's collision has to be confirmed by cannot be taken with <see cref="StandAt"/>, which
	/// puts the viewer down rather than walking them there and so crosses no cell edge at all.
	/// </summary>
	internal static void DebugWalk( float forward, float right, int frames )
	{
		for ( var frame = 0; frame < frames; ++frame )
			Step( forward, right, WalkSpeed / 60f );
	}

	/// <summary>
	/// The park's own edge test, built once per park rather than once per frame, and let go of by
	/// <see cref="Forget"/>. Null for no park, which leaves what is kept alone.
	/// </summary>
	internal static Func<int, int, StepDirection, bool>? EdgeTest( ParkWorld? park )
	{
		if ( park == null )
			return null;

		if ( !ReferenceEquals( park, _blockedFor ) )
		{
			_blockedFor = park;
			_blocked = CellEdge.For( park, WalkingMode ).Blocked;
		}

		return _blocked;
	}

	/// <summary>The park <see cref="EdgeTest"/> is kept for, for the debug console's <c>parks</c>.</summary>
	internal static ParkWorld? EdgeTestPark => _blockedFor;

	private static ParkWorld? _blockedFor;
	private static Func<int, int, StepDirection, bool>? _blocked;

	/// <summary>
	/// One frame's movement, swept across the cell grid and stopped at any side that is shut: the first-person
	/// branch of the original's camera update, pass for pass (<c>0x0042bdd8</c>; <c>docs/exe/park-engine.md</c>,
	/// "Walking on the ground is swept against the cell edges").
	///
	/// <para>
	/// Each pass works out, for each axis, what fraction of what is left of its step meets the next cell
	/// boundary, and asks about the side met first - <c>FUN_004d8750( x, y, direction, 2 )</c>, which is
	/// <see cref="CellEdge"/>'s own function and the test every guest walks on. A shut side stops that axis in
	/// the cell being left while the other carries on, which is what makes a viewer slide along a wall.
	/// <b>Only the axis asked may leave its cell</b>: the other is put back if it crossed, and so is either axis
	/// of a step that meets no boundary, so every change of cell is one the edge test allowed.
	/// </para>
	/// <para>
	/// The direction comes from the sign of that axis's movement, and the numbering is the original's:
	/// it pushes <b>3</b> for a negative X step and <b>1</b> for a positive one, <b>0</b> for negative Y
	/// and <b>2</b> for positive - which is exactly <see cref="StepDirection"/>.
	/// </para>
	/// <para>
	/// <b>The arithmetic is the original's at 53 bits.</b> Between its stores it works on the x87 stack at the
	/// precision the runtime starts it in, which is what <see cref="double"/> is here, and each <c>(float)</c>
	/// is one of its stores. After a failed frame it would run at 24 bits instead; which one is live is open,
	/// and it moves only the margin (park-engine.md, "Which rounding is live is not settled").
	/// </para>
	/// <para>
	/// <b>One branch of the original's loop is not built.</b> After every pass it asks <c>FUN_0042a340</c>
	/// whether the cell it ends in rides something (<see cref="RideAt"/>), and on finding a ride starts that
	/// ride's view from first person - the ride window's "Ride it!", <c>FUN_004e15b0</c> - and ends the sweep.
	/// There is no ride view here, so the walk carries on and the pass is counted instead.
	/// </para>
	/// </summary>
	/// <param name="blocked">
	/// Whether a side of a cell is shut - <c>CellEdge.For( park, 2 ).Blocked</c> for a real park.
	/// <b>Null takes the step whole</b>, unswept.
	/// </param>
	/// <param name="rideAt">Whether a cell rides something - <see cref="RideAt"/> for a real park; null asks nothing.</param>
	internal static Vector3 Slide( Vector3 from, float dx, float dy,
		Func<int, int, StepDirection, bool>? blocked, Func<int, int, bool>? rideAt = null )
	{
		// The original zeroes each axis against its own dead band before sweeping anything, so a step too
		// small to leave the cell cannot spend a pass on a rounding error. It tests X's lower edge on
		// dt * velocity before that is stored (0x0042bdf6), and this has only the float, so a step that rounds
		// to exactly -1e-4 is zeroed here where the original may keep it.
		if ( MathF.Abs( dx ) <= StillnessBand )
			dx = 0f;

		if ( MathF.Abs( dy ) <= StillnessBand )
			dy = 0f;

		var x = from.X;
		var y = from.Y;

		if ( blocked == null )
			return new Vector3( x + dx, y + dy, 0f );

		for ( var pass = 0; (dx != 0f || dy != 0f) && pass < MaxPasses; ++pass )
		{
			var cellX = CellOf( x );
			var cellY = CellOf( y );

			var reachX = Reach( x, dx );
			var reachY = Reach( y, dy );

			// An exact tie is broken toward Y: X's step is cut, for the rest of the frame, so that X stops short
			// of its own boundary rather than landing on it (0x0042bff8).
			if ( reachX == reachY && dx != 0f && dy != 0f )
			{
				dx = (float)(dx / (double)TieBreak);
				reachX = (float)(reachX * (double)TieBreak);
			}

			var askingX = reachX < reachY;

			// No boundary is met inside the step, so it is taken whole and the sweep ends - but an axis whose
			// cell still changed, by rounding onto its boundary, is put back unasked (0x0042c460).
			if ( (askingX ? reachX : reachY) >= 1f )
			{
				var wholeX = (float)((double)x + dx);
				var wholeY = (float)((double)y + dy);

				x = CellOf( wholeX ) == cellX ? wholeX : PutBack( cellX, dx );
				y = CellOf( wholeY ) == cellY ? wholeY : PutBack( cellY, dy );
				CountARide( rideAt, x, y );
				break;
			}

			var direction = askingX
				? (dx < 0f ? StepDirection.West : StepDirection.East)
				: (dy < 0f ? StepDirection.North : StepDirection.South);

			// Off the map every side is shut. Ours: CellEdge holds no cell there, where the original's test reads
			// its neighbours' records and its put-back reads a cell below 0 as unsigned. Nothing the original does
			// brings a viewer there (see Step).
			var shut = !ParkState.OnMap( cellX, cellY ) || blocked( cellX, cellY, direction );

			if ( askingX )
				Pass( ref x, ref dx, cellX, ref y, ref dy, cellY, reachX, shut );
			else
				Pass( ref y, ref dy, cellY, ref x, ref dx, cellX, reachY, shut );

			CountARide( rideAt, x, y );
		}

		return new Vector3( x, y, 0f );
	}

	/// <summary>
	/// The end of every pass: the original asks <c>FUN_0042a340</c> about the cell the pass ended in, clamped to
	/// the map (<c>0x0042c587</c>), and on a ride starts its view and ends the sweep (docs/exe/park-engine.md,
	/// "Walking on the ground is swept against the cell edges"). Not built. Counted each pass that ends on such a
	/// cell - twice as the viewer crosses in, then once each frame they step in it, moved or stopped by a shut side -
	/// where the original takes it once and leaves the walk.
	/// </summary>
	private static void CountARide( Func<int, int, bool>? rideAt, float x, float y )
	{
		var last = ParkWorld.MapSize - 1;

		if ( rideAt?.Invoke( Math.Clamp( CellOf( x ), 0, last ), Math.Clamp( CellOf( y ), 0, last ) ) == true )
			Unimplemented.Report( "FIRST_PERSON_WALK_INTO_RIDE" );
	}

	/// <summary>
	/// Whether the original's walk rides something from a cell - <c>FUN_0042a340</c>: the cell is an entrance
	/// (type 9), and its owner's cell holds a thing whose item's <c>UsageInfo.CannotRide</c> is nought. Null
	/// without a running park or its catalogue, which asks nothing.
	/// </summary>
	/// <remarks>
	/// The original walks the owner cell's thing chain for the first catalogue object that passes; the placed
	/// objects anchored on that cell are this port's copy of that chain, as they are for
	/// <see cref="ParkPathBuilding.OwnerOf"/>.
	/// </remarks>
	internal static Func<int, int, bool>? RideAt( ParkState? state, ParkItemCatalogue? catalogue )
	{
		if ( state?.Park is not { } park || catalogue == null )
			return null;

		return ( x, y ) =>
		{
			var cell = ParkState.CellFor( park, x, y );

			if ( cell.Type != CellEdge.RideEnd || cell.ParentId == 0 )
				return false;

			var (ownerX, ownerY) = MapStep.CellAt( cell.ParentId );

			foreach ( var placed in state.Objects )
			{
				if ( placed.CellX == ownerX && placed.CellY == ownerY
					&& catalogue.TryGet( placed.CatalogueId, out var item ) && !item.CannotRide )
					return true;
			}

			return false;
		};
	}

	/// <summary>
	/// One pass of <see cref="Slide"/> that asked about the side of the <paramref name="asked"/> axis,
	/// <paramref name="reach"/> being the fraction of what is left of its step that meets it.
	/// </summary>
	private static void Pass( ref float asked, ref float askedStep, int askedCell,
		ref float other, ref float otherStep, int otherCell, float reach, bool shut )
	{
		if ( shut )
		{
			asked = PutBack( askedCell, askedStep );
			askedStep = 0f;
		}
		else
		{
			var moved = (double)reach * askedStep;

			asked = (float)(asked + moved);
			askedStep = (float)(askedStep - moved);

			// A landing on the boundary itself that left the cell unchanged is pushed on the way the step goes:
			// a step going negative lands on cell * 10, which is still the cell, and would measure nought to the
			// same side on every pass after (0x0042c14d). When X is asked the original reads the rest of the step
			// unrounded, here and in its loop's test; the two differ only if it underflows.
			if ( CellOf( asked ) == askedCell )
				asked = (float)(askedStep < 0f ? asked - (double)Nudge : asked + (double)Nudge);
		}

		// The other axis goes the same fraction of its own step and may not change cell. If it did, by rounding,
		// it is put back, with its step left for a later pass to ask about (0x0042c197).
		var carried = (double)reach * otherStep;
		var carriedTo = (float)(other + carried);

		otherStep = (float)(otherStep - carried);
		other = CellOf( carriedTo ) == otherCell ? carriedTo : PutBack( otherCell, otherStep );
	}

	/// <summary>
	/// The cell a position is in: <c>__ftol( position * 0.1f )</c>, truncated, taken afresh every pass and never
	/// clamped.
	/// </summary>
	private static int CellOf( float position ) => (int)((double)position * Tenth);

	/// <summary>
	/// What fraction of what is left of an axis's step meets its next cell boundary, or <see cref="NoReach"/>
	/// where it is not moving. It is the fractional part of <c>position * 0.1f</c> by <c>modf</c>, or what is
	/// left of the cell going positive, over the step in cells (<c>0x0042bef5</c>).
	/// </summary>
	private static float Reach( float position, float step )
	{
		if ( step == 0f )
			return NoReach;

		var cells = (double)position * Tenth;
		var fraction = cells - Math.Truncate( cells );
		var stepCells = step * Tenth;

		// Going positive the fraction is stored as a float before it is taken from 1.
		return step < 0f
			? (float)Math.Abs( fraction / stepCells )
			: (float)Math.Abs( (1.0 - (float)fraction) / stepCells );
	}

	/// <summary>
	/// Where an axis is parked, or put back, in <paramref name="cell"/>: on the cell's near boundary going
	/// negative, which is still the cell, and <see cref="FarSide"/> into it going positive.
	/// </summary>
	private static float PutBack( int cell, float step )
		=> step < 0f ? (float)((double)cell * CellSize) : (float)(FarSide + ((double)cell * CellSize));

	/// <summary>The ground under the viewer right now, without touching the easing.</summary>
	/// <remarks>
	/// For placing the camera before its first <see cref="Update"/>. <see cref="GroundUnderStand"/>
	/// takes its one un-eased sample on the first call, so a constructor must not take it while
	/// <see cref="Stand"/> can still move before that first update - which is exactly what the debug
	/// console's two-argument <c>camcorder</c> does.
	/// </remarks>
	private static float PeekGround()
		=> ParkGround.Current?.Heightfield?.HeightAtWorld( Stand.X, Stand.Y ) ?? 0f;

	/// <summary>
	/// The ground under the viewer, eased, so that walking over a ridge lifts the eye with the land
	/// instead of stepping it. The first sample is taken outright, as the orbit camera's is, so
	/// entering camcorder mode does not rise into place over the first second.
	/// </summary>
	private float GroundUnderStand()
	{
		var field = ParkGround.Current?.Heightfield;

		if ( field == null )
			return 0f;

		var sample = field.HeightAtWorld( Stand.X, Stand.Y );

		if ( !_groundSampled || _restand )
		{
			_groundHeight = sample;
			_groundSampled = true;
			_restand = false;
		}
		else
		{
			_groundHeight = _groundHeight.LerpTo( sample, Time.SmoothingFactor( GroundFollowRate ) );
		}

		return _groundHeight;
	}

	/// <summary>Where the viewer is standing and looking, for the debug console.</summary>
	public static string State()
	{
		var ground = ParkGround.Current?.Heightfield?.HeightAtWorld( Stand.X, Stand.Y );

		// The cell actually stood in, as an index rather than the fractional `cell=` beside it, with what
		// the map says is built on it. A pure getter, and the one the collision is read off: "the viewer
		// is not inside the ride" is `type` never reading CellEdge.Footprint, which is a number the game
		// says about itself rather than something inferred from a photograph (docs/VERIFYING.md rule 99).
		var cellX = (int)MathF.Floor( Stand.X / 10f );
		var cellY = (int)MathF.Floor( Stand.Y / 10f );

		var type = Level.Current?.ParkState?.Park is { } park && ParkState.OnMap( cellX, cellY )
			? ParkState.CellFor( park, cellX, cellY ).Type.ToString()
			: "-";

		return $"stand=({Stand.X:F3},{Stand.Y:F3}) " +
			$"cell=({Stand.X / 10f:F1},{Stand.Y / 10f:F1}) " +
			$"at=({cellX},{cellY}) type={type} " +
			$"yaw={Yaw:F2} pitch={Pitch:F1} " +
			$"ground={(ground.HasValue ? ground.Value.ToString( "F1" ) : "-")} " +
			$"eye={(ground ?? 0f) + EyeHeight:F1}";
	}
}
