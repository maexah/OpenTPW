namespace OpenTPW;

public class LobbyCameraMode : CameraMode
{
	private float Height => 20f;
	private float Distance => 70f;
	private float Speed => 0.2f;

	private Vector3 Target => new Vector3( 400, 400, 12.5f );

	/// <summary>
	/// Static so it survives <see cref="Camera.SetCameraMode{T}"/> creating a fresh instance.
	/// </summary>
	public static bool Paused { get; set; }

	// Accumulated separately from Time.Now so unpausing resumes where it stopped instead of
	// snapping back onto the wall-clock orbit.
	private float _orbitTime;

	public override void Update()
	{
		if ( Input.Pressed( InputButton.FreezeCamera ) )
			Paused = !Paused;

		if ( !Paused )
			_orbitTime += Time.Delta;

		float x = MathF.Sin( _orbitTime * Speed ) * Distance;
		float y = MathF.Cos( _orbitTime * Speed ) * Distance;

		Position = new Vector3( Target.X + x, Target.Y + y, Height );
		Rotation = Rotation.LookAt( Target - Position );

		FieldOfView = 60;
	}
}
