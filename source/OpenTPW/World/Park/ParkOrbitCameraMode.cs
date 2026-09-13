using System.Numerics;

namespace OpenTPW;

/// <summary>
/// The park's own camera, as the original flies it. <b>Not</b> <see cref="ParkCameraMode"/>, which is a
/// development flycam left over from an old test scene and has never run in the shipped game - its own
/// summary says so.
///
/// <para>
/// The original keeps this camera as a save module of its own ("SAD_CAMERA", chunk magic <c>KAME</c>,
/// code 0x0042a190-0x0042d130), and its entire persisted state is four things: where it is looking, how
/// far round it has been spun, how far out it is zoomed, and a flags word. Everything else below is
/// derived from those each frame, which is why this has so few fields.
/// </para>
///
/// <para>
/// Zoom drives pitch, so there is only one control for "how close am I": pulling in also lowers the
/// camera towards the ground, and pushing out raises it towards a plan view.
/// </para>
///
/// <para>
/// The three pieces of state are static so that the debug console can drive the camera without holding
/// the instance, the same way <see cref="LobbyCameraMode"/> exposes its own. <b>Two things are
/// deliberately unfinished and both are marked at their site</b> - the field of view's convention, and
/// riding the ground.
/// </para>
/// </summary>
public sealed class ParkOrbitCameraMode : CameraMode
{
	/// <summary>
	/// What the camera looks at, on the ground. The original's default is (475, 175) in the park's own
	/// axes, which is grid cell (47, 17) - the park entrance, and the same cell
	/// <c>MapInfo.FixedItemOrigin</c> names. A cell is 10 units across and its centre is at
	/// <c>grid * 10 + 5</c>, which is exactly where those two numbers come from.
	/// </summary>
	public static Vector3 PointOfInterest { get; set; } = new( 475f, 175f, 0f );

	/// <summary>How far the camera has been spun about the point of interest, in radians.</summary>
	public static float Yaw { get; set; }

	/// <summary>
	/// Distance from the point of interest, and the only "how close" control - <see cref="Pitch"/> is
	/// derived from it. The original starts at 110 and clamps to 70..130.
	/// </summary>
	public static float Zoom { get; set; } = 110f;

	public const float MinZoom = 70f;
	public const float MaxZoom = 130f;

	/// <summary>Pitch pulled all the way in, in degrees.</summary>
	private const float PitchAtMinZoom = 45f;

	/// <summary>Pitch pushed all the way out, in degrees.</summary>
	private const float PitchAtMaxZoom = 65f;

	/// <summary>
	/// The original's own ceiling on pitch. An oddly precise number because it clamps the result of the
	/// interpolation rather than being a design value, and it is kept exactly as it reads there so that
	/// nothing here quietly rounds the camera somewhere the original would not put it. With the range
	/// above it is never actually reached; it will matter if zoom is ever driven outside 70..130.
	/// </summary>
	private const float MaxPitch = 84.2673f;

	/// <summary>
	/// Pitch for the current zoom: 45 degrees pulled in, 65 pushed out, straight line between, then
	/// clamped. Zoom and pitch are one control in the original and they stay one control here.
	/// </summary>
	public static float Pitch
	{
		get
		{
			var t = ((Zoom - MinZoom) / (MaxZoom - MinZoom)).Clamp( 0f, 1f );
			var degrees = PitchAtMinZoom + ((PitchAtMaxZoom - PitchAtMinZoom) * t);

			return degrees.Clamp( 0f, MaxPitch );
		}
	}

	/// <summary>
	/// How fast the eye catches up with the ground beneath it.
	///
	/// <para>
	/// The original eases it with a one-pole filter at alpha 0.5 - half the remaining distance every
	/// tick - and its park loop ticks at a fixed 31ms. That is a half-life of 31ms, so the rate here
	/// is <c>ln(2) / 0.031</c>. Written as a rate rather than as a per-frame fraction because a fixed
	/// fraction settles at a different speed on every machine; see <see cref="Time.SmoothingFactor"/>.
	/// </para>
	/// </summary>
	private const float GroundFollowRate = 22.36f;

	/// <summary>The eased ground height, and whether it has ever been sampled - see <see cref="GroundUnderPoi"/>.</summary>
	private float _groundHeight;

	private bool _groundSampled;

	/// <summary>
	/// The ground under the point of interest, eased. The original raises the eye by this so that
	/// scrolling over a cliff lifts the camera with the land rather than burying it.
	///
	/// <para>
	/// The first sample is taken outright rather than eased into, so that a park opens with the camera
	/// already at the right height instead of rising into place over the first second. Zero where
	/// there is no ground to ask - which is any scene that is not a park.
	/// </para>
	/// </summary>
	private float GroundUnderPoi()
	{
		var field = ParkGround.Current?.Heightfield;

		if ( field == null )
			return 0f;

		var sample = field.HeightAtWorld( PointOfInterest.X, PointOfInterest.Y );

		if ( !_groundSampled )
		{
			_groundHeight = sample;
			_groundSampled = true;
		}
		else
		{
			_groundHeight = _groundHeight.LerpTo( sample, Time.SmoothingFactor( GroundFollowRate ) );
		}

		return _groundHeight;
	}

	/// <summary>How fast the point of interest scrolls, in world units a second. Provisional - see Update.</summary>
	private const float ScrollSpeed = 200f;

	public ParkOrbitCameraMode()
	{
		// The original's park projection is 90, near 0.1, far 1000. Two caveats, both real:
		//
		// The 90 is certain as a number and uncertain as a meaning - the projection it is handed writes
		// m[0] == m[5] with no aspect term anywhere, so whether it is horizontal, vertical or diagonal
		// cannot be read off the code. CameraMode takes a vertical angle at 4:3, so that is what it is
		// treated as here; if the framing turns out tighter than the original's, this is the first
		// thing to change.
		//
		// The far plane stays Camera's own 10000 rather than the original's 1000. Nothing here depends
		// on it - it costs only depth precision - so it is left alone rather than reaching into shared
		// code to suit one camera.
		FieldOfView = 90f;
	}

	public override void Update()
	{
		// Spin and zoom. These reuse bindings that already exist rather than inventing new ones: what
		// the original binds is not traced yet, so the controls are provisional and the geometry below
		// is the part that matches it.
		if ( Input.Pressed( InputButton.RotateLeft ) )
			Yaw -= MathF.PI / 4f;

		if ( Input.Pressed( InputButton.RotateRight ) )
			Yaw += MathF.PI / 4f;

		if ( Input.Mouse.Wheel != 0 )
			Zoom = (Zoom - (Input.Mouse.Wheel * 5f)).Clamp( MinZoom, MaxZoom );

		// Scroll the point of interest across the ground, in whatever direction the camera faces, so
		// that "forward" means forward on the screen rather than forward on the map.
		if ( Input.Forward != 0f || Input.Right != 0f )
		{
			var scroll = ScrollSpeed * Time.Delta;

			PointOfInterest += new Vector3(
				((Input.Forward * MathF.Sin( Yaw )) + (Input.Right * MathF.Cos( Yaw ))) * scroll,
				((Input.Forward * MathF.Cos( Yaw )) - (Input.Right * MathF.Sin( Yaw ))) * scroll,
				0f );
		}

		// Where the eye goes. The original builds it as the point of interest plus
		// rotateY( yaw, (0, sin(pitch) * zoom, -cos(pitch) * zoom) ) - a height and a horizontal
		// distance, swung about the vertical. That is written in the original's axes, where Y is up;
		// this engine has Z up, so the height goes into Z and the swing happens about Z instead.
		var pitch = Pitch.DegreesToRadians();

		var horizontal = MathF.Cos( pitch ) * Zoom;
		var height = (MathF.Sin( pitch ) * Zoom) + GroundUnderPoi();

		Position = PointOfInterest + new Vector3(
			horizontal * MathF.Sin( Yaw ),
			-horizontal * MathF.Cos( Yaw ),
			height );

		// Only Forward is taken from this - see Rotation.LookAt, whose roll is arbitrary, and the note
		// on it in AudioListener. Camera builds its view from Forward and a fixed world up, so that is
		// all this has to be right about.
		Rotation = Rotation.LookAt( PointOfInterest - Position );
	}

	/// <summary>
	/// A one-line summary for the debug console. Cell comes back out of the world position by the same
	/// rule that put it in: <c>grid = (world - 5) / 10</c>.
	///
	/// <para>
	/// <c>ground</c> is sampled fresh rather than being the eased value the camera is actually riding,
	/// so it says what the land under the point of interest is, not where the eye has caught up to.
	/// That is the more useful of the two to read back: it is the thing being followed.
	/// </para>
	/// </summary>
	public static string State()
	{
		var ground = ParkGround.Current?.Heightfield?.HeightAtWorld( PointOfInterest.X, PointOfInterest.Y );

		// The cell the point of interest stands in, which is the world position divided by the cell
		// size - not the (world - 5) / 10 above, which recovers the cell a CENTRE belongs to and comes
		// back fractional anywhere else.
		var cellX = (int)MathF.Floor( PointOfInterest.X / 10f );
		var cellY = (int)MathF.Floor( PointOfInterest.Y / 10f );

		var attributes = ParkGround.Current?.Attributes;

		return $"poi=({PointOfInterest.X:F0},{PointOfInterest.Y:F0}) " +
			$"cell=({(PointOfInterest.X - 5f) / 10f:F1},{(PointOfInterest.Y - 5f) / 10f:F1}) " +
			$"yaw={Yaw:F2} zoom={Zoom:F0} pitch={Pitch:F1} " +
			$"ground={(ground.HasValue ? ground.Value.ToString( "F1" ) : "-")} " +
			$"attr={(attributes != null ? attributes.At( cellX, cellY ).ToString() : "-")}";
	}
}
