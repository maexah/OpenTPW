namespace OpenTPW;

/// <summary>
/// <b>This is not the park camera.</b> It is a free-fly development camera left from the old test scene -
/// <c>WASD</c> across a plane, right-drag to look, the wheel for height, and ninety-degree yaw steps - and
/// nothing builds one: the lobby sets <see cref="LobbyCameraMode"/> and a park sets
/// <see cref="ParkOrbitCameraMode"/>, which is the camera that actually flies over a park. It has never
/// run in the game as shipped.
///
/// <para>
/// <b>This used to say the lobby's was the only SetCameraMode call anywhere</b>, which stopped being true
/// the moment parks were built. Its numbers give it away too: <c>wishHeight</c> is clamped to 1..10, where
/// the camera framing a park twelve hundred units across sits ninety-odd units up.
/// </para>
///
/// <para>
/// The original's park camera is a different thing entirely - it scrolls, rotates and zooms over the
/// terrain. When parks are built, that camera wants writing against the original's own code, the way
/// <see cref="LobbyCameraMode"/> was. Do not take this file as the starting point; it is kept only so the
/// flycam is not written twice.
/// </para>
///
/// <para>
/// <b>It is NOT the only reader of the camera bindings, and this paragraph claimed to be until
/// 2026-09-17.</b> <c>Input.Forward</c> and <c>Input.Right</c> have three readers - this file,
/// <see cref="ParkOrbitCameraMode"/> and <see cref="ParkCamcorderCameraMode"/> - and
/// <c>InputButton.RotateLeft</c> / <c>RotateRight</c> have two, this file and
/// <see cref="ParkOrbitCameraMode"/>; the camcorder turns with the pointer rather than with those keys.
/// <b>The old sentence was a trap rather than merely stale</b>: anyone deleting this dead flycam on the
/// strength of it would have believed a park's scrolling and its rotation went with it, when both live in
/// the orbit camera and neither would have noticed.
/// </para>
/// </summary>
public class ParkCameraMode : CameraMode
{
	private Vector3 wishVelocity = new();
	private Vector3 velocity = new();
	private float wishYaw;
	private bool wasPressed;
	private Vector2 mouseAnchor;
	private float cameraSpeed = 128f;

	// Per-second rates for the three things that ease towards a target, applied through
	// Time.SmoothingFactor so they land the same way at any frame rate.
	private const float DragRate = 5f;
	private const float TurnRate = 10f;
	private const float HeightRate = 10f;
	private float wishHeight = 2f;
	private float yaw;

	public override void Update()
	{
		//
		// Get user input
		//
		var wishDir = new Vector3( Input.Forward, Input.Right, 0 ).Normal;

		if ( Input.Mouse.Right && !wasPressed )
		{
			mouseAnchor = Input.Mouse.Position;
		}

		if ( Input.Mouse.Right )
		{
			var delta = mouseAnchor - Input.Mouse.Position;
			wishDir = new Vector3( delta.Y, -delta.X, 0 ) / 512f;
		}

		wasPressed = Input.Mouse.Right;

		// An acceleration, in units per second squared - the frame delta belongs where this is
		// integrated below, and used to be multiplied in here as well as there.
		wishVelocity = Rotation.Forward * wishDir.X * cameraSpeed;
		wishVelocity += Rotation.Right * wishDir.Y * cameraSpeed;
		wishVelocity.Z = 0;

		wishHeight += -Input.Mouse.Wheel;
		wishHeight = wishHeight.Clamp( 1f, 10f );

		if ( Input.Pressed( InputButton.RotateLeft ) )
			wishYaw -= 90;
		if ( Input.Pressed( InputButton.RotateRight ) )
			wishYaw += 90;

		//
		// Apply everything
		//

		// Apply velocity
		velocity += wishVelocity * Time.Delta;

		// Apply drag. Top speed still settles at cameraSpeed / DragRate, but it holds there at
		// any frame rate now, and a long frame can no longer drag past a standstill.
		velocity = velocity.LerpTo( Vector3.Zero, Time.SmoothingFactor( DragRate ) );

		// Rotate camera
		yaw = yaw.LerpTo( wishYaw, Time.SmoothingFactor( TurnRate ) );

		// Move camera
		Position += velocity * Time.Delta;
		Position = Position.WithZ( Position.Z.LerpTo( wishHeight, Time.SmoothingFactor( HeightRate ) ) );
	}
}
