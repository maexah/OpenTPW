using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace OpenTPW.Tests;

/// <summary>The Options screen's tick reaches <see cref="UI.OptionsScreen.KeptSize"/> (the week-end audit of 2026-10-31, ST-F2), over a disposable save folder.</summary>
[TestClass]
public class OptionsKeepTests
{
	private string root = null!;
	private BaseFileSystem? oldData;
	private BaseFileSystem? oldSaves;
	private GameOptions oldOptions = null!;

	[TestInitialize]
	public void Isolate()
	{
		// First: with no game this skips here, before anything below is set (the cleanup then restores nothing).
		var data = GameData.Required();
		Log ??= new();
		oldData = FileSystemOrNull;
		FileSystem = data;
		oldSaves = SaveFileSystemOrNull;
		oldOptions = GameOptions.Current;
		root = Directory.CreateTempSubdirectory( "opentpw-keep-" ).FullName;
		SaveFileSystem = new BaseFileSystem( root );
	}

	[TestCleanup]
	public void PutBack()
	{
		if ( root == null )
			return;

		FileSystemOrNull = oldData;
		SaveFileSystemOrNull = oldSaves;
		GameOptions.Current = oldOptions;
		oldOptions.ApplySound();
		Directory.Delete( root, true );
	}

	/// <remarks><b>Mutations:</b> the <c>KeptSize</c> call taken out of <c>Keep</c>: the size stays nought and is saved so; the <c>ScreenResolution</c> line taken out: Config.tcf keeps 1.</remarks>
	[TestMethod]
	public void TheTickKeepsAFirstRunsFullScreenAtTheModeTheSliderShows()
	{
		GameOptions.Current = new GameOptions { DisplayMode = DisplayMode.FullScreen };

		var stack = new UI.WindowStack();
		UI.OptionsScreen.Open( stack, () => { } );
		var screen = stack.Windows.OfType<UI.OptionsScreen>().Single();

		var shown = new VideoMode( 2560, 1440, 144 );
		typeof( UI.OptionsScreen ).GetField( "_modes", BindingFlags.NonPublic | BindingFlags.Instance )!.SetValue( screen, new[] { shown } );

		typeof( UI.OptionsScreen ).GetMethod( "Keep", BindingFlags.NonPublic | BindingFlags.Instance )!.Invoke( screen, null );

		Assert.AreEqual( shown, GameOptions.Current.FullScreenSize );
		Assert.AreEqual( 2, GameOptions.Current.ScreenResolution, "Config.tcf gets the nearest of the original's three, as the slider's move sets it" );
		StringAssert.Contains( File.ReadAllText( Path.Combine( root, "opentpw.cfg" ) ), "fullscreen 2560 1440 144" );
	}
}
