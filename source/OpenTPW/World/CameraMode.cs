namespace OpenTPW;

public class CameraMode
{
	public Vector3 Position { get; set; } = new();
	public Rotation Rotation { get; set; } = Rotation.Identity;

	/// <summary>
	/// The vertical angle it sees, in degrees, at the 4:3 the game was made at. What a window of
	/// another shape does with it is <see cref="Camera.ReferenceAspect"/>'s to say.
	/// </summary>
	public float FieldOfView { get; set; } = 90f;

	public virtual void Update()
	{

	}
}
