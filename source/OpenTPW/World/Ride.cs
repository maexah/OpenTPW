namespace OpenTPW;

/// <summary>
/// <b>The ride runtime is not built, and this code does not run.</b> Nothing in the tree constructs a
/// <c>Ride</c>, and if anything did it would throw before the constructor returned: <c>RideVM</c>'s own
/// constructor writes <c>Variables[VAR_RIDECLOSED]</c> - index 6 - into the list it has just initialised
/// empty.
///
/// <para>
/// The reader it wants is not merely commented out, it is absent: <c>RideScriptFile</c> survives only as a
/// commented line in RideVM, and no such type exists anywhere in the tree, so that line cannot simply be
/// uncommented. The path built below is wrong for this project too - it joins with a backslash where
/// everything else uses forward slashes against the Zio file system.
/// </para>
///
/// <para>
/// Kept rather than deleted because the opcode tables under <c>VM/</c> are real work on the original's ride
/// script format and are worth holding. Read this file as a note of where that work stopped, not as a
/// foundation to build on.
/// </para>
/// </summary>
public class Ride : Entity
{
	public RideVM VM { get; private set; }

	public Ride( string rideArchive )
	{
		var rideName = Path.GetFileNameWithoutExtension( rideArchive );

		VM = new RideVM( FileSystem.OpenRead( rideArchive + "\\" + rideName + ".rse" ) );
		var settingsFile = new SettingsFile( FileSystem.OpenRead( rideArchive + "\\" + rideName + ".sam" ) );

		Log.Trace( $"Loaded ride {settingsFile.Entries.First( x => x.Key == "Info.Name" ).Value}" );
	}

	protected override void OnUpdate()
	{
		VM.Update();
	}
}
