namespace OpenTPW;

/// <summary>
/// Puts the game's own icon on the window.
///
/// <para>
/// It comes from TP.ico, which the original installs beside TP.exe - the grinning head Windows showed for
/// Theme Park World. The same four images are built into TP.exe, TP.ICD and the patched executable as their
/// icon resource, byte for byte, so the loose file is the game's icon rather than a stray left in the folder.
/// </para>
/// <para>
/// A copy of the game without it - or with one this cannot read - leaves the window with whatever the desktop
/// gives it, which is what happened before there was any icon at all. It is not worth stopping over.
/// </para>
/// </summary>
internal static class GameIcon
{
	/// <summary>As it is spelled in the install; <see cref="GameDir.GetPath"/> finds it in any case.</summary>
	private const string FileName = "TP.ico";

	public static void Apply( Window window )
	{
		var path = GameDir.GetPath( FileName );

		if ( !File.Exists( path ) )
		{
			Log.Warning( $"Window icon: there is no {FileName} in {GameDir.Root}, so the window keeps the desktop's icon" );
			return;
		}

		try
		{
			using var stream = File.OpenRead( path );

			var icon = new IconFile( stream );

			if ( icon.Best is not { } image )
			{
				Log.Warning( $"Window icon: nothing in {FileName} could be read, so the window keeps the desktop's icon" );
				return;
			}

			window.SetIcon( image.Width, image.Height, image.Pixels );

			Log.Info( $"Window icon: {image.Width}x{image.Height} at {image.BitsPerPixel} bits, from {FileName}" );
		}
		catch ( Exception e )
		{
			Log.Warning( $"Window icon: {FileName} could not be read, so the window keeps the desktop's icon - {e.Message}" );
		}
	}
}
