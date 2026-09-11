namespace OpenTPW;

/// <summary>
/// save\Config.tcf: the options that belong to the machine rather than to whoever is playing.
///
/// <para>
/// The original reads it as it starts (WinMain_Main 0x0045aab4, 0x00424930) and writes it when the options
/// screen's tick is clicked (GameOptions_Accept, 0x004237f0) and whenever a player is saved - on Select New
/// Player and on quitting (0x00424820, from 0x005c8650). Its name is written "Config.tcf"; the safe-mode
/// batch file shipped beside the game copies safemode.tcf to "save\config.tcf", which on the original's
/// file system is the same file.
/// </para>
/// <para>
/// Thirty-two bytes, all little-endian, fields of the options object at 0x0078d8d8 in the order
/// GameOptions_ReadWriteConfig (0x004242c0) handles them:
/// </para>
/// <code>
/// 0x00  dword  version, 1
/// 0x04  dword  3D card rendering (1) or software (0)      options +0x00
/// 0x08  dword  screen resolution, 0 to 2                  options +0x04
/// 0x0c  dword  graphics quality, 0 to 2                   options +0x08
/// 0x10  dword  video card, 0 primary or 1 secondary        options +0x0c
/// 0x14  byte   movie sound on, then 3 bytes of padding    options +0x28
/// 0x18  dword  movie volume, 0 to 100                     options +0x2c
/// 0x1c  dword  audio quality, 0 to 100                    options +0x30
/// </code>
/// <para>
/// A version of 0 leaves the options at their defaults; any other version is read the same way, as the
/// original checks nothing more. The shipped safemode.tcf reads as software rendering, 640 x 480, low
/// graphics, the primary card, movie sound on at 100, and audio quality 32.
/// </para>
/// <para>
/// The sound effects, music and speech volumes and the seven switches are not here: they are the
/// player's, in their gms.dat. The movie volume is in both, and the player's wins when they are picked.
/// </para>
/// </summary>
public sealed class ConfigFile
{
	/// <summary>The version the original writes.</summary>
	public const int CurrentVersion = 1;

	public int CardRendering;
	public int ScreenResolution;
	public int GraphicsQuality;
	public int VideoCard;
	public bool MovieOn;
	public int MovieVolume;
	public int AudioQuality;

	/// <summary>Reads the file, or null for one whose version is 0 - which the original takes as "keep the defaults".</summary>
	public static ConfigFile? Read( Stream stream )
	{
		var record = RecordStream.ForReading( stream );

		var version = 0;
		record.Int32( ref version );

		if ( version == 0 )
			return null;

		var file = new ConfigFile();
		file.Record( record );
		return file;
	}

	public void Write( Stream stream )
	{
		var record = RecordStream.ForWriting( stream );

		var version = CurrentVersion;
		record.Int32( ref version );

		Record( record );
	}

	private void Record( RecordStream record )
	{
		record.Int32( ref CardRendering );
		record.Int32( ref ScreenResolution );
		record.Int32( ref GraphicsQuality );
		record.Int32( ref VideoCard );
		record.Bool( ref MovieOn );
		record.Padding( 3 );
		record.Int32( ref MovieVolume );
		record.Int32( ref AudioQuality );
	}
}
