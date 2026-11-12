using System.Numerics;

namespace OpenTPW;

public static class Camera
{
	public static Vector3 Position => CameraMode?.Position ?? Vector3.Zero;
	public static Rotation Rotation => CameraMode?.Rotation ?? Rotation.Identity;

	/// <summary>The picture's up, which the view is built against - see <see cref="CameraMode.Up"/>.</summary>
	public static Vector3 Up => CameraMode?.Up ?? Vector3.Up;

	/// <summary>The way the camera is turned for a sprite's sector - see <see cref="CameraMode.SectorLook"/>.</summary>
	public static Vector3 SectorLook => CameraMode?.SectorLook ?? Rotation.Forward;

	/// <summary>Where the scene is heard from - see <see cref="CameraMode.Ears"/>.</summary>
	public static Vector3 Ears => CameraMode?.Ears ?? Vector3.Zero;

	public static Matrix4x4 ViewMatrix { get; set; }
	public static Matrix4x4 ProjMatrix { get; set; }

	/// <summary>
	/// The shape the game was made at. Every mode in its own _Resolution.sam is 4:3 but for
	/// 1280x1024, and every camera angle in its data was set against it.
	/// </summary>
	public const float ReferenceAspect = 4f / 3f;

	/// <summary>
	/// How far the frustum reaches to either side one unit ahead of the camera, and how far it
	/// reached at 4:3 - what anything sized to fill the screen has to be measured against on a
	/// window of another shape. See <see cref="CalcViewProjMatrix"/>; both are zero until the
	/// camera has run once.
	/// </summary>
	public static float HorizontalTangent { get; private set; }

	public static float ReferenceHorizontalTangent { get; private set; }

	private static CameraMode CameraMode = new();

	/// <summary>
	/// The vertical picture at 4:3 that a view handed <paramref name="handedDegrees"/> draws in the
	/// original: <c>2 atan( tan(a/2) / 2 )</c>. Its draw matrix multiplies the screen scale
	/// <c>0x0087b0d0</c>, diag( 1.5, -2 ) at 4:3, in after the projection (<c>FUN_005783b0</c>), so every
	/// view shows half the tangent it asked for (<c>docs/exe/park-engine.md</c>, "The lens"): the park's
	/// 90 draws 53.13, the lobby's ISLANDFOV 100 draws 61.58.
	/// </summary>
	public static float DrawnAngle( float handedDegrees )
		=> (2f * MathF.Atan( MathF.Tan( handedDegrees.DegreesToRadians() * 0.5f ) / ScreenScale )).RadiansToDegrees();

	/// <summary>The screen scale's vertical term, the -2 at <c>0x0087b0e4</c> (its sign is the screen's flip).</summary>
	private const float ScreenScale = 2f;

	/// <summary>
	/// How many units of the original's clip space a unit square on to the eye is, at any depth, in a view handed
	/// <paramref name="handedDegrees"/>: <c>2 cos( a/2 )</c>, 1.41 for the park's 90. A size the original adds after its
	/// draw matrix, a sprite's span, is drawn that span over this in the world (<c>docs/exe/park-engine.md</c>, "The
	/// size a sprite is drawn", where the law is written).
	/// </summary>
	public static float ClipPerWorld( float handedDegrees )
		=> ScreenScale * MathF.Cos( handedDegrees.DegreesToRadians() * 0.5f );

	/// <summary>The camera mode, lens and window shape last logged, so the log says each once.</summary>
	private static (Type? Mode, float Angle, float Aspect) _logged;

	/// <summary>
	/// The projection for a picture <paramref name="verticalDegrees"/> high at 4:3 on a window of
	/// <paramref name="aspect"/>, and how far it reaches sideways one unit ahead, there and at 4:3.
	/// </summary>
	internal static (Matrix4x4 Projection, float Across, float ReferenceAcross) Lens( float verticalDegrees, float aspect )
	{
		// Its own floor under the aspect, so a window with no area at all - one that has just been
		// minimised - cannot put a NaN through the projection and out into everything drawn with it.
		aspect = MathF.Max( aspect, 0.01f );

		// The angle a camera mode asks for is its vertical angle at 4:3, the shape it was set at.
		//
		// A window wider than that keeps it and sees further out to the sides, which is what a fixed
		// vertical angle has always done here and is what a wider screen should buy. A window
		// narrower than 4:3 would, by the same rule, see less across than the original ever showed -
		// at 3:4 not much over half of it - and the lobby's islands would be cropped off the sides of
		// a tall window. So below 4:3 it is the horizontal angle that is held at its 4:3 value and
		// the vertical one that opens up instead. Either way everything the original framed at 4:3 is
		// still on the screen, and at 4:3 itself the two rules meet and nothing changes.
		var vertical = verticalDegrees.DegreesToRadians();
		var reference = MathF.Tan( vertical * 0.5f ) * ReferenceAspect;

		if ( aspect < ReferenceAspect )
			vertical = 2f * MathF.Atan( MathF.Tan( vertical * 0.5f ) * ReferenceAspect / aspect );

		var projection = Matrix4x4.CreatePerspectiveFieldOfView(
			vertical,
			aspect,
			0.1f,
			10000.0f
		);

		return (projection, MathF.Tan( vertical * 0.5f ) * aspect, reference);
	}

	private static void CalcViewProjMatrix()
	{
		if ( CameraMode == null )
			return;

		var up = CameraMode.Up;
		var direction = CameraMode.Rotation.Forward;
		var position = CameraMode.Position;

		ViewMatrix = Matrix4x4.CreateLookTo( 
			position.GetSystemVector3(), 
			direction.GetSystemVector3(), 
			up.GetSystemVector3()
		);

		var lens = Lens( CameraMode.FieldOfView, Screen.Aspect );

		ProjMatrix = lens.Projection;
		HorizontalTangent = lens.Across;
		ReferenceHorizontalTangent = lens.ReferenceAcross;

		if ( _logged != (CameraMode.GetType(), CameraMode.FieldOfView, Screen.Aspect) )
		{
			_logged = (CameraMode.GetType(), CameraMode.FieldOfView, Screen.Aspect);
			Log.Trace( $"Camera: {CameraMode.GetType().Name} lens {CameraMode.FieldOfView:F2} high at 4:3, across {lens.ReferenceAcross:F4} there and {lens.Across:F4} at {Screen.Width} x {Screen.Height}" );
		}
	}

	public static void SetCameraMode<T>() where T : CameraMode
	{
		CameraMode = Activator.CreateInstance<T>();
	}

	public static void Update()
	{
		CameraMode.Update();

		// Run view/proj matrix calculations
		CalcViewProjMatrix();
	}
}
