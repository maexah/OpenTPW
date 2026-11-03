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
		while ( SaveTrouble.Take() != null ) { }
		Directory.Delete( root, true );
	}

	/// <remarks>
	/// <b>Mutations:</b> the <c>KeptSize</c> call taken out of <c>Keep</c>: the size stays nought and is saved so; the <c>ScreenResolution</c> line taken out: Config.tcf keeps 1;
	/// the <c>ApplyDisplay</c> call taken out of <c>Keep</c>, or moved above the <c>KeptSize</c> block: nothing, or the size of nothing, reaches <c>apply</c>.
	/// </remarks>
	[TestMethod]
	public void TheTickKeepsAFirstRunsFullScreenAtTheModeTheSliderShows()
	{
		GameOptions.Current = new GameOptions { DisplayMode = DisplayMode.FullScreen };

		var stack = new UI.WindowStack();
		UI.OptionsScreen.Open( stack, () => { } );
		var screen = stack.Windows.OfType<UI.OptionsScreen>().Single();

		var shown = new VideoMode( 2560, 1440, 144 );
		typeof( UI.OptionsScreen ).GetField( "_modes", BindingFlags.NonPublic | BindingFlags.Instance )!.SetValue( screen, new[] { shown } );

		var applied = new List<(DisplayMode, VideoMode)>();
		screen.Keep( ( mode, size ) => applied.Add( (mode, size) ) );

		Assert.AreEqual( shown, GameOptions.Current.FullScreenSize );
		Assert.AreEqual( 2, GameOptions.Current.ScreenResolution, "Config.tcf gets the nearest of the original's three, as the slider's move sets it" );
		StringAssert.Contains( File.ReadAllText( Path.Combine( root, "opentpw.cfg" ) ), "fullscreen 2560 1440 144" );
		CollectionAssert.AreEqual( new[] { (DisplayMode.FullScreen, shown) }, applied, "Keep's own call: the window is asked for the mode the slider shows, once" );
	}

	/// <summary>
	/// <b>The display the tick applies</b> (Q987), through <c>ApplyDisplay</c> (what <c>Keep</c> calls; <c>Keep</c>'s own call is the test above): a first tick that
	/// changes the display applies it with its size, a second that sets it back to the opening one applies that too (RESTART GAME's cross leaves the
	/// screen open), and one that keeps what is in effect applies nothing, at the same size or not; with no window nothing is applied.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> comparing with the opening options alone (the old rule) fails "set back to the opening display" here and the Keep test below; not remembering what was applied fails "not again" (the borderless step);
	/// <c>apply( Options.DisplayMode, _before.FullScreenSize )</c> fails "the size asked for"; <c>_applied</c> remembering <c>_before.FullScreenSize</c> fails "not again at the new size".
	/// </remarks>
	[TestMethod]
	public void TheTickAppliesTheDisplayAgainstWhatIsInEffect()
	{
		var shown = new VideoMode( 1280, 720, 60 );
		var other = new VideoMode( 1920, 1080, 60 );
		GameOptions.Current = new GameOptions { DisplayMode = DisplayMode.Windowed, FullScreenSize = shown };
		var stack = new UI.WindowStack();
		UI.OptionsScreen.Open( stack, () => { } );
		var screen = stack.Windows.OfType<UI.OptionsScreen>().Single();

		var applied = new List<(DisplayMode, VideoMode)>();
		Action<DisplayMode, VideoMode> apply = ( mode, size ) => applied.Add( (mode, size) );

		screen.ApplyDisplay( apply );
		Assert.AreEqual( 0, applied.Count, "nothing changed, nothing applied" );

		GameOptions.Current.DisplayMode = DisplayMode.BorderlessFullScreen;
		screen.ApplyDisplay( null );
		Assert.AreEqual( 0, applied.Count, "no window, nothing applied" );

		screen.ApplyDisplay( apply );
		screen.ApplyDisplay( apply );
		CollectionAssert.AreEqual( new[] { (DisplayMode.BorderlessFullScreen, shown) }, applied, "applied once, and not again while it is the one in effect" );

		GameOptions.Current.DisplayMode = DisplayMode.Windowed;
		screen.ApplyDisplay( apply );
		CollectionAssert.AreEqual( new[] { (DisplayMode.BorderlessFullScreen, shown), (DisplayMode.Windowed, shown) }, applied, "set back to the opening display: the window still shows the first tick's" );

		GameOptions.Current.DisplayMode = DisplayMode.FullScreen;
		GameOptions.Current.FullScreenSize = other;
		screen.ApplyDisplay( apply );
		screen.ApplyDisplay( apply );
		Assert.AreEqual( 3, applied.Count, "not again at the new size" );
		Assert.AreEqual( (DisplayMode.FullScreen, other), applied[2], "the size asked for is the options' own, not the opening one" );

		GameOptions.Current.FullScreenSize = shown;
		screen.ApplyDisplay( apply );
		Assert.AreEqual( (DisplayMode.FullScreen, shown), applied[^1], "the size set back to the opening one is still a change from the one in effect" );
		Assert.AreEqual( 4, applied.Count );
	}

	/// <summary>
	/// <b>The tick's own sequence</b> (Q987), through <c>Keep( apply )</c>: the display and the video card changed, the tick (RESTART GAME's box opens and
	/// the screen stays), the display set back to the opening one, the tick again: the second tick puts the window back.
	/// </summary>
	/// <remarks><b>Mutation:</b> <c>DisplayToApply</c> comparing with the opening options alone (the old rule) applies nothing on the second tick.</remarks>
	[TestMethod]
	public void ASecondTickThroughKeepSetsTheWindowBack()
	{
		var shown = new VideoMode( 1280, 720, 60 );
		GameOptions.Current = new GameOptions { DisplayMode = DisplayMode.Windowed, FullScreenSize = shown };
		var stack = new UI.WindowStack();
		UI.OptionsScreen.Open( stack, () => { } );
		var screen = stack.Windows.OfType<UI.OptionsScreen>().Single();

		var applied = new List<(DisplayMode, VideoMode)>();
		Action<DisplayMode, VideoMode> apply = ( mode, size ) => applied.Add( (mode, size) );

		GameOptions.Current.DisplayMode = DisplayMode.BorderlessFullScreen;
		GameOptions.Current.SecondaryVideoCard = true;
		screen.Keep( apply );
		CollectionAssert.AreEqual( new[] { (DisplayMode.BorderlessFullScreen, shown) }, applied, "the first tick applies the new display" );

		GameOptions.Current.DisplayMode = DisplayMode.Windowed;
		screen.Keep( apply );
		CollectionAssert.AreEqual( new[] { (DisplayMode.BorderlessFullScreen, shown), (DisplayMode.Windowed, shown) }, applied, "the second tick puts the window back to the opening display" );
	}

	private static string TextOf( UI.UiControl control )
		=> control.Text ?? control.Children.Select( TextOf ).FirstOrDefault( text => text != null ) ?? "";

	private UI.WindowStack TickWithTheCardChangedAndAFileBlocked( string? blocked, out string[] boxes )
	{
		while ( SaveTrouble.Take() != null ) { }

		GameOptions.Current = new GameOptions { SecondaryVideoCard = false };
		var stack = new UI.WindowStack();
		UI.OptionsScreen.Open( stack, () => { } );
		var screen = stack.Windows.OfType<UI.OptionsScreen>().Single();

		GameOptions.Current.SecondaryVideoCard = true;

		// A directory where the write's copy goes (BaseFileSystem.TemporarySuffix) fails that one file and no other.
		if ( blocked != null )
			Directory.CreateDirectory( Path.Combine( root, blocked + BaseFileSystem.TemporarySuffix ) );

		screen.Keep( null );
		boxes = stack.Windows.OfType<UI.MessageBox>().Select( box => TextOf( box.Root ) ).ToArray();
		return stack;
	}

	/// <summary>
	/// <b>RESTART GAME is asked over a lost opentpw.cfg</b> (Q999): the card is in Config.tcf, which was written, so it arrives at the next start
	/// and the player is asked. The box carries the lost write's words (Q986's concern: the scene's own box waits while this one is up and the
	/// tick leaves the game), and nothing is left to tell afterwards.
	/// </summary>
	/// <remarks><b>Mutations:</b> <c>Keep</c> passing <c>!SaveTrouble.LostAWrite</c> as the card kept (Q986's rule): no box; <c>RestartText</c> without the failure: the box lacks OPTIONS NOT SAVED; <c>TakeFailure</c> not letting the failure go: it is told twice.</remarks>
	[TestMethod]
	public void TheTickAsksToRestartOverALostOpentpwCfgAndTellsItInTheBox()
	{
		var stack = TickWithTheCardChangedAndAFileBlocked( "opentpw.cfg", out var boxes );

		Assert.AreEqual( 1, boxes.Length, "RESTART GAME is asked" );
		StringAssert.StartsWith( boxes[0], Localization.Get( UIStrings.RestartGame ) );
		StringAssert.Contains( boxes[0], "OPTIONS NOT SAVED" );
		Assert.IsTrue( stack.Windows.OfType<UI.OptionsScreen>().Any(), "the screen stays open under the box" );
		Assert.IsFalse( SaveTrouble.Pending, "the box took the failure, so no second box follows" );
		Assert.AreEqual( 1, BitConverter.ToInt32( File.ReadAllBytes( Path.Combine( root, "Config.tcf" ) ), 16 ), "the card was kept in Config.tcf" );
	}

	/// <summary>
	/// <b>RESTART GAME is not asked over a lost Config.tcf</b> (Q999, the reverse, as it stood): the card was not kept, so there is nothing to
	/// restart for; the screen closes and the lost write is told by the scene as it always was.
	/// </summary>
	/// <remarks><b>Mutation:</b> <c>Keep</c> passing nothing but <c>NeedsRestart</c>: the box opens over a card that was not written.</remarks>
	[TestMethod]
	public void TheTickDoesNotAskToRestartOverALostConfigTcf()
	{
		var stack = TickWithTheCardChangedAndAFileBlocked( "Config.tcf", out var boxes );

		Assert.AreEqual( 0, boxes.Length, "no RESTART GAME" );
		Assert.IsFalse( stack.Windows.OfType<UI.OptionsScreen>().Any(), "the tick closed the screen" );
		Assert.IsTrue( SaveTrouble.Pending, "the failure waits for the scene" );
		StringAssert.StartsWith( SaveTrouble.Take(), "OPTIONS NOT SAVED" );
	}

	/// <summary><b>The control</b> (Q999): both files written, the box is the game's own words alone and nothing waits.</summary>
	/// <remarks><b>Mutation:</b> <c>RestartText</c> always adding a paragraph: the words differ.</remarks>
	[TestMethod]
	public void TheTickAsksToRestartInTheGamesOwnWordsWhereBothFilesWereWritten()
	{
		var stack = TickWithTheCardChangedAndAFileBlocked( null, out var boxes );

		CollectionAssert.AreEqual( new[] { Localization.Get( UIStrings.RestartGame ) }, boxes );
		Assert.IsFalse( SaveTrouble.Pending );
		Assert.IsTrue( stack.Windows.OfType<UI.OptionsScreen>().Any() );
	}
}
