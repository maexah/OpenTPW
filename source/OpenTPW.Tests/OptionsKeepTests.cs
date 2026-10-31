using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Collections.Generic;
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

	/// <summary>
	/// <b>The tick's own call</b> (Q987): a first tick that changes the display applies it, a second that sets it back to the opening one applies that
	/// too (RESTART GAME's cross leaves the screen open), and one that keeps what is in effect applies nothing; with no window nothing is applied.
	/// </summary>
	/// <remarks><b>Mutation:</b> <c>Keep</c> comparing with the opening options alone (the old rule) fails the second step; not remembering what was applied fails the third.</remarks>
	[TestMethod]
	public void TheTickAppliesTheDisplayAgainstWhatIsInEffect()
	{
		var shown = new VideoMode( 1280, 720, 60 );
		GameOptions.Current = new GameOptions { DisplayMode = DisplayMode.Windowed, FullScreenSize = shown };
		var stack = new UI.WindowStack();
		UI.OptionsScreen.Open( stack, () => { } );
		var screen = stack.Windows.OfType<UI.OptionsScreen>().Single();

		var applied = new List<DisplayMode>();
		Action<DisplayMode, VideoMode> apply = ( mode, _ ) => applied.Add( mode );

		screen.ApplyDisplay( apply );
		CollectionAssert.AreEqual( System.Array.Empty<DisplayMode>(), applied, "nothing changed, nothing applied" );

		GameOptions.Current.DisplayMode = DisplayMode.BorderlessFullScreen;
		screen.ApplyDisplay( null );
		CollectionAssert.AreEqual( System.Array.Empty<DisplayMode>(), applied, "no window, nothing applied" );

		screen.ApplyDisplay( apply );
		screen.ApplyDisplay( apply );
		CollectionAssert.AreEqual( new[] { DisplayMode.BorderlessFullScreen }, applied, "applied once, and not again while it is the one in effect" );

		GameOptions.Current.DisplayMode = DisplayMode.Windowed;
		screen.ApplyDisplay( apply );
		CollectionAssert.AreEqual( new[] { DisplayMode.BorderlessFullScreen, DisplayMode.Windowed }, applied, "set back to the opening display: the window still shows the first tick's" );
	}
}
