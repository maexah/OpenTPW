namespace OpenTPW;

public class Asset
{
	public string Path { get; set; }
	public static List<Asset> All { get; private set; } = new();

	/// <summary>
	/// Adds this to <see cref="All"/>, and counts as one step of the loading screen if one is up.
	///
	/// That is where the original's loading bar moves from as well: its texture and mesh loaders
	/// each take a step as they finish (0x00587db0 and 0x00579e00, through 0x00587c80), so the bar
	/// counts what has loaded rather than timing it.
	/// </summary>
	protected void Register()
	{
		All.Add( this );
		LoadingScreen.Step();
	}
}
