namespace OpenTPW;

public class CameraMode
{
	public Vector3 Position { get; set; } = new();
	public Rotation Rotation { get; set; } = Rotation.Identity;

	/// <summary>
	/// Which way is up in the picture: the world's up, unless the mode turns its camera about the way it looks. Only
	/// <see cref="Rotation"/>'s forward is used (<see cref="Rotation.LookAt"/> leaves its roll arbitrary), so a roll is
	/// said here; the park's orbit leans by it while it pans (<see cref="ParkOrbitCameraMode.LeanAt"/>).
	/// </summary>
	public Vector3 Up { get; set; } = Vector3.Up;

	/// <summary>
	/// The vertical angle it sees, in degrees, at the 4:3 the game was made at. What a window of
	/// another shape does with it is <see cref="Camera.ReferenceAspect"/>'s to say.
	/// </summary>
	public float FieldOfView { get; set; } = 90f;

	/// <summary>Where the scene is heard from: the eye, unless the mode says otherwise.</summary>
	public virtual Vector3 Ears => Position;

	public virtual void Update()
	{

	}
}
