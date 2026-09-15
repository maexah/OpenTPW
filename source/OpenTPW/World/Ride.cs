namespace OpenTPW;

/// <summary>
/// <b>The ride runtime is not built, and this code does not run.</b> Nothing in the tree constructs a
/// <c>Ride</c>, and if anything did it would throw before the constructor returned: <c>RideVM</c>'s own
/// constructor writes <c>Variables[VAR_RIDECLOSED]</c> - index 6 - into the list it has just initialised
/// empty.
///
/// <para>
/// The reader it wants no longer has to be written: <see cref="RideScriptFile"/> reads the format, and
/// <see cref="RideScript"/> runs it against a <see cref="RideState"/>. Neither is wired up here, because
/// this class is still wrong in the ways below - it builds its path with a backslash where everything else
/// uses forward slashes against the Zio file system, and it drives <c>RideVM</c>, whose constructor throws.
/// A ride that loads and runs its script belongs in something built for it rather than in this - and that
/// is now <see cref="ParkRides"/>, which gives every thing standing in a park the script its own archive
/// holds, the way the original's object constructor does.
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
