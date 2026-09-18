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
/// cloud band legible. <b>Measured by capture, not asserted</b>: an earlier draft of this very comment
/// said the orbit camera never shows sky at all, and the two frames plainly refute that - it shows
/// some, just not as anything you would call a sky.
/// </para>
///
/// <para>
/// The original reaches it two ways, and both are already named here: the 'C' key
/// (<see cref="InputButton.CamcorderMode"/>, shortcuts action 16) and button id 99 on the park
/// management gadget, <b>which exists and wires this up</b> - see <c>ParkGadget</c>, where that button's
/// click calls <see cref="Enter"/>. This said the gadget did not exist yet. Both end at the same place - the chain, the shortcuts
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
/// <b>It is not a faithful copy, and an earlier draft of this comment wrongly said it was.</b> The exe
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
	/// with its +/-0.2 bands and <c>_DAT_006fdde8</c> 2.0 with its +/-0.4 ones - and an earlier draft
	/// here took the 0.4 band from one pair and the 3.5 from the other, which is neither of the
	/// original's settings.
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
	public static void Forget()
	{
		Active = false;
		Stand = Vector3.Zero;
		Yaw = 0f;
		Pitch = 0f;
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
		// An earlier note here claimed the exe corroborates the sign, on the grounds that its yaw step
		// is scaled by _DAT_006fdda8 = -0.0012. That argument was wrong twice and is withdrawn: the
		// exe SUBTRACTS that term, so its net step is positive for a pointer on the right; and if this
		// engine's Yaw really is the negation of the exe's, a negative step there would be a positive
		// step here, which is the opposite of the conclusion drawn. Settling what the exe does on
		// screen needs FUN_0046f650's matrix convention, which is not traced. The geometry above and
		// the measurement stand on their own.
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
	/// Walks the viewer about, facing-relative, and keeps them on the map, clamped to the same
	/// <c>1..MapExtent</c> a lightning bolt is.
	///
	/// <para>
	/// The clamp is about staying on the park, not about the heightfield: <see cref="HeightfieldFile"/>
	/// answers outside the map too, by holding the nearest edge height, so walking off it would give a
	/// flat plain rather than a crash. An earlier note here claimed otherwise. Note also that a park is
	/// not necessarily square with the extent - the clamp keeps the viewer in the world, not
	/// necessarily on drawn ground.
	/// </para>
	/// </summary>
	private void Walk()
	{
		if ( Input.Forward == 0f && Input.Right == 0f )
			return;

		var step = WalkSpeed * Time.Delta;
		var extent = ParkWorld.MapSize * 10f;

		var x = Stand.X + (((Input.Forward * -MathF.Sin( Yaw )) + (Input.Right * MathF.Cos( Yaw ))) * step);
		var y = Stand.Y + (((Input.Forward * MathF.Cos( Yaw )) + (Input.Right * MathF.Sin( Yaw ))) * step);

		Stand = new Vector3( x.Clamp( 1f, extent ), y.Clamp( 1f, extent ), 0f );
	}

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

		return $"stand=({Stand.X:F0},{Stand.Y:F0}) " +
			$"cell=({Stand.X / 10f:F1},{Stand.Y / 10f:F1}) " +
			$"yaw={Yaw:F2} pitch={Pitch:F1} " +
			$"ground={(ground.HasValue ? ground.Value.ToString( "F1" ) : "-")} " +
			$"eye={(ground ?? 0f) + EyeHeight:F1}";
	}
}
