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

	/// <summary>The tick hands the audio quality slider's value on as the sound detail (<c>0x00423850</c>): at 30 or below a fade is a stop (Q509).</summary>
	/// <remarks><b>Mutations:</b> the <c>ApplySoundDetail</c> call taken out of <c>Keep</c> leaves fading on.</remarks>
	[TestMethod]
	public void TheTickHandsTheAudioQualityOnAsTheSoundDetail()
	{
		GameOptions.Current = new GameOptions { AudioQuality = 20 };

		try
		{
			var stack = new UI.WindowStack();
			UI.OptionsScreen.Open( stack, () => { } );
			stack.Windows.OfType<UI.OptionsScreen>().Single().Keep( null );

			Assert.IsFalse( Audio.FadesOn, "a quality of 20 is at or below sound.sam's FADETOGGLE 30" );
		}
		finally
		{
			Audio.SetSoundDetail( 50, 30 );
		}
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

	private UI.WindowStack TickWithTheCardChangedAndAFileBlocked( string? blocked, out string[] boxes, bool oddConfig = false )
	{
		while ( SaveTrouble.Take() != null ) { }

		// A Config.tcf with two bytes more than this game writes is not read whole, so the tick copies it aside first (Q998).
		if ( oddConfig )
		{
			using var memory = new MemoryStream();
			new ConfigFile().Write( memory );
			File.WriteAllBytes( Path.Combine( root, "Config.tcf" ), memory.ToArray().Concat( new byte[] { 0xA5, 0x5A } ).ToArray() );
			SaveFolder.LoadConfig();
		}

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
	/// and the player is asked. The box carries the lost write's title, SOME OPTIONS NOT SAVED, alone (Q938; Q986's concern: the scene's own box
	/// waits while this one is up and the tick leaves the game). Nothing is left to tell afterwards.
	/// </summary>
	/// <remarks><b>Mutations:</b> <c>Keep</c> passing <c>!SaveTrouble.LostAWrite</c> as the card kept (Q986's rule): no box; <c>RestartText</c> without the failure: the box lacks the words; <c>TakeFailure</c> not letting the failure go: it is told twice; <c>cardSaved</c> not passed: the generic words.</remarks>
	[TestMethod]
	public void TheTickAsksToRestartOverALostOpentpwCfgAndTellsItInTheBox()
	{
		var stack = TickWithTheCardChangedAndAFileBlocked( "opentpw.cfg", out var boxes );

		Assert.AreEqual( 1, boxes.Length, "RESTART GAME is asked" );
		StringAssert.StartsWith( boxes[0], Localization.Get( UIStrings.RestartGame ) );
		StringAssert.EndsWith( boxes[0], "\n\nSOME OPTIONS NOT SAVED" );
		Assert.IsFalse( boxes[0].Contains( "video card is saved" ), "the title alone (Q938)" );
		Assert.IsFalse( boxes[0].Contains( "cannot be written" ), "no folder word: the card was written to it" );
		Assert.IsTrue( stack.Windows.OfType<UI.OptionsScreen>().Any(), "the screen stays open under the box" );
		Assert.IsFalse( SaveTrouble.Pending, "the box took the failure, so no second box follows" );
		Assert.AreEqual( 1, BitConverter.ToInt32( File.ReadAllBytes( Path.Combine( root, "Config.tcf" ) ), 16 ), "the card was kept in Config.tcf" );
	}

	/// <summary>
	/// <b>RESTART GAME carries a copy made at the same tick</b> (Q998, Q999): an odd Config.tcf is copied aside by the tick and opentpw.cfg is lost.
	/// The box carries both, the copy's words first, so nothing waits behind it and nothing goes untold when the lobby's tick quits (Q986).
	/// </summary>
	/// <remarks><b>Mutation:</b> <c>RestartText</c> taking <c>SaveTrouble.Take()</c> (one text) or the failure alone: the box has the copy's words alone, or lacks them, and something is left waiting.</remarks>
	[TestMethod]
	public void TheRestartBoxCarriesACopyMadeAtTheTickAndTheLostWriteTogether()
	{
		var stack = TickWithTheCardChangedAndAFileBlocked( "opentpw.cfg", out var boxes, oddConfig: true );

		Assert.AreEqual( 1, boxes.Length, "RESTART GAME is asked" );
		StringAssert.StartsWith( boxes[0], Localization.Get( UIStrings.RestartGame ) );
		StringAssert.Contains( boxes[0], "OPTIONS FILE BACKED UP" );
		StringAssert.Contains( boxes[0], "SOME OPTIONS NOT SAVED" );
		Assert.IsTrue( boxes[0].IndexOf( "BACKED UP", StringComparison.Ordinal ) < boxes[0].IndexOf( "NOT SAVED", StringComparison.Ordinal ), "the copy first, then the loss" );
		Assert.IsFalse( SaveTrouble.Pending, "nothing waits behind the box" );
		Assert.IsTrue( File.Exists( Path.Combine( root, "Config.tcf.opentpw-backup" ) ), "the copy was made" );
		Assert.IsTrue( stack.Windows.OfType<UI.OptionsScreen>().Any() );
	}

	/// <summary><b>A copy alone at the tick</b> (Q998): the box carries it after the game's own words, and nothing waits.</summary>
	/// <remarks><b>Mutation:</b> <c>TakeForARestartBox</c> without the backups: the box is the game's own words and the copy waits behind it.</remarks>
	[TestMethod]
	public void TheRestartBoxCarriesACopyMadeAtTheTick()
	{
		TickWithTheCardChangedAndAFileBlocked( null, out var boxes, oddConfig: true );

		Assert.AreEqual( 1, boxes.Length );
		StringAssert.StartsWith( boxes[0], Localization.Get( UIStrings.RestartGame ) );
		StringAssert.Contains( boxes[0], "OPTIONS FILE BACKED UP" );
		Assert.IsFalse( boxes[0].Contains( "NOT SAVED" ) );
		Assert.IsFalse( SaveTrouble.Pending );
	}

	/// <summary>
	/// <b>The longest RESTART GAME box holds its text</b> (Q938): the game's words, a copy kept and a write lost, wrapped as the box wraps them
	/// (font 4 of the set the size calls for, at the text rectangle's width) and as tall as the text rectangle at 1024 x 768 and 640 x 480, where
	/// text is never drawn smaller than the original's (<see cref="UI.UiText.SizeIn"/>). With the parts' bodies it was 252 px in 239 at 1024 x 768
	/// and 240 in 149 at 640 x 480.
	/// </summary>
	/// <remarks><b>Mutation:</b> the parts' bodies put back in <c>TakeForARestartBox</c>: the text is taller than the rectangle at both sizes.</remarks>
	[TestMethod]
	public void TheLongestRestartBoxHoldsItsTextWhereTheTextIsNotDrawnSmaller()
	{
		var screen = Screen.Size;

		try
		{
			foreach ( var (width, height) in new[] { (1024, 768), (640, 480) } )
			{
				Screen.Size = new Point2( width, height );

				SaveTrouble.NewVisit();
				SaveTrouble.BackedUp( "OPTIONS" );
				SaveTrouble.Failed( "OPTIONS", new IOException() );
				var text = $"{Localization.Get( UIStrings.RestartGame )}\n\n{SaveTrouble.TakeForARestartBox()}";

				var font = UI.UiFonts.Get( 4 ) ?? throw new AssertFailedException( "font 4 did not load" );
				var area = (Width: (1345 - 610) * UI.VirtualScreen.Scale, Height: (753 - 276) * UI.VirtualScreen.Scale);
				var lines = UI.UiText.Wrap( font, text, UI.UiText.WrapWidthOf( area.Width ) );
				var tall = lines.Count * font.LineHeight * UI.UiFonts.Scale;

				Assert.IsTrue( tall <= area.Height + 0.5f, $"{width} x {height}: {lines.Count} lines, {tall:0} px in {area.Height:0}" );
			}
		}
		finally
		{
			Screen.Size = screen;
		}
	}

	/// <summary>
	/// <b>RESTART GAME is not asked over a lost Config.tcf</b> (Q999, the reverse, as it stood): the card was not kept, so there is nothing to
	/// restart for; the screen closes and the lost write is told by the scene as it always was. The card is put back, so no later write of
	/// Config.tcf (a second tick, Select New Player, the game's close) keeps it unasked.
	/// </summary>
	/// <remarks><b>Mutations:</b> <c>Keep</c> passing nothing but <c>NeedsRestart</c>: the box opens over a card that was not written; the put-back taken out of <c>Keep</c>: the card stays chosen in memory.</remarks>
	[TestMethod]
	public void TheTickDoesNotAskToRestartOverALostConfigTcf()
	{
		var stack = TickWithTheCardChangedAndAFileBlocked( "Config.tcf", out var boxes );

		Assert.AreEqual( 0, boxes.Length, "no RESTART GAME" );
		Assert.IsFalse( stack.Windows.OfType<UI.OptionsScreen>().Any(), "the tick closed the screen" );
		Assert.IsFalse( GameOptions.Current.SecondaryVideoCard, "the card that was not kept is put back" );
		Assert.IsTrue( SaveTrouble.Pending, "the failure waits for the scene" );
		StringAssert.StartsWith( SaveTrouble.Take(), "OPTIONS NOT SAVED" );
	}

	/// <summary>The control (Q999): both files written, the box is the game's own words alone and nothing waits.</summary>
	/// <remarks><b>Mutation:</b> <c>RestartText</c> always adding a paragraph: the words differ.</remarks>
	[TestMethod]
	public void TheTickAsksToRestartInTheGamesOwnWordsWhereBothFilesWereWritten()
	{
		var stack = TickWithTheCardChangedAndAFileBlocked( null, out var boxes );

		CollectionAssert.AreEqual( new[] { Localization.Get( UIStrings.RestartGame ) }, boxes );
		Assert.IsFalse( SaveTrouble.Pending );
		Assert.IsTrue( stack.Windows.OfType<UI.OptionsScreen>().Any() );
		Assert.IsTrue( GameOptions.Current.SecondaryVideoCard, "the card kept stays" );
	}
}
