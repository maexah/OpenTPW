using System.Numerics;

namespace OpenTPW;

public static class Camera
{
	public static Vector3 Position => CameraMode?.Position ?? Vector3.Zero;
	public static Rotation Rotation => CameraMode?.Rotation ?? Rotation.Identity;

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

	private static void CalcViewProjMatrix()
	{
		if ( CameraMode == null )
			return;

		var up = Vector3.Up;
		var direction = CameraMode.Rotation.Forward;
		var position = CameraMode.Position;

		ViewMatrix = Matrix4x4.CreateLookTo( 
			position.GetSystemVector3(), 
			direction.GetSystemVector3(), 
			up.GetSystemVector3()
		);

		// Its own floor under the aspect, so a window with no area at all - one that has just been
		// minimised - cannot put a NaN through the projection and out into everything drawn with it.
		var aspect = MathF.Max( Screen.Aspect, 0.01f );

		// The angle a camera mode asks for is its vertical angle at 4:3, the shape it was set at.
		//
		// A window wider than that keeps it and sees further out to the sides, which is what a fixed
		// vertical angle has always done here and is what a wider screen should buy. A window
		// narrower than 4:3 would, by the same rule, see less across than the original ever showed -
		// at 3:4 not much over half of it - and the lobby's islands would be cropped off the sides of
		// a tall window. So below 4:3 it is the horizontal angle that is held at its 4:3 value and
		// the vertical one that opens up instead. Either way everything the original framed at 4:3 is
		// still on the screen, and at 4:3 itself the two rules meet and nothing changes.
		var vertical = CameraMode.FieldOfView.DegreesToRadians();
		ReferenceHorizontalTangent = MathF.Tan( vertical * 0.5f ) * ReferenceAspect;

		if ( aspect < ReferenceAspect )
			vertical = 2f * MathF.Atan( MathF.Tan( vertical * 0.5f ) * ReferenceAspect / aspect );

		HorizontalTangent = MathF.Tan( vertical * 0.5f ) * aspect;

		ProjMatrix = Matrix4x4.CreatePerspectiveFieldOfView(
			vertical,
			aspect,
			0.1f,
			10000.0f
		);
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
