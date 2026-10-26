using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeoVeldrid;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace OpenTPW.Tests;

/// <summary>
/// The Save Park screen (<c>docs/exe/saves.md</c>, "One screen, two uses", "What the handlers answer" and "OpenTPW's
/// Save Park"): the menu row that opens it, the name box, a row's click, OK, Enter and Escape, the overwrite question,
/// and the save it makes of the running park. Over a disposable save folder.
/// </summary>
[TestClass]
public class ParkSaveScreenTests
{
	private const int NameBox = 0x23bce2;
	private const int Ok = -1;
	private const int Cross = -2;
	private const int QuestionCross = 0x9873c8;
	private const int QuestionTick = 0x9873c9;

	private static readonly Vector2 Grass = new( 1100, 800 );

	/// <summary>The middle of the name box, 605, 811, 1343, 895 on a screen the virtual one's size.</summary>
	private static readonly Vector2 InTheBox = new( 974, 853 );

	private string? root;
	private BaseFileSystem oldSaves = null!;
	private Point2 _screen;
	private Level? _level;

	private string Jungle => Path.Combine( root!, "users", "1Test", "jungle" );

	[TestInitialize]
	public void IsolateSaves()
	{
		Log ??= new();
		FileSystem = GameData.Required();

		oldSaves = SaveFileSystem;
		root = Directory.CreateTempSubdirectory( "opentpw-save-park-" ).FullName;
		Directory.CreateDirectory( Jungle );
		SaveFileSystem = new BaseFileSystem( root );

		_screen = Screen.Size;
		Screen.Size = new Point2( 2048, 1536 );
		_level = Level.Current;

		Input.ForgetHeldKeys();
		Input.Mouse = new();
		Input.TextCaptured = false;
		Unimplemented.Forget();
		Time.Now = 100f;
		SetCurrentPlayer( new Player( 0, "Test", new PlayerFile { InstantAction = true } ) );
	}

	[TestCleanup]
	public void RestoreSaves()
	{
		// Without the game the set-up stops before anything is changed.
		if ( root == null )
			return;

		ForgetWhatWasAsked();
		SetCurrentPlayer( null );
		SaveFileSystem = oldSaves;
		Directory.Delete( root, true );

		ParkBuildMode.Forget();
		Level.Current = _level!;
		Input.ForgetHeldKeys();
		Input.Mouse = new();
		Input.TextCaptured = false;
		Unimplemented.Forget();
		Screen.Size = _screen;
	}

	/// <summary>
	/// <b>The menu's Save Game closes the menu and opens the Save Park screen</b>, modal and pausing, counted as
	/// nothing, titled UITEXT 201, its rows the player's saved parks, with an OK button and a name box of fifteen
	/// characters that reads "New Save" and has the keys.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> the row left counting <c>SAVE_GAME</c> opens nothing; the menu left open stands under the
	/// screen; Load's title; the fill skipped leaves no rows; the box given no focus leaves the keys with the park;
	/// the box left empty.
	/// </remarks>
	[TestMethod]
	public void SaveGameOpensTheSaveParkScreenWithItsNameBox()
	{
		Put( "New Save.TPWS", new DateTime( 2026, 10, 8, 12, 28, 0, DateTimeKind.Local ) );
		Put( "Old Park.TPWS", new DateTime( 2026, 10, 1, 14, 18, 0, DateTimeKind.Local ) );
		Put( "easymode.TPWI", new DateTime( 2026, 10, 1, 14, 18, 0, DateTimeKind.Local ) );

		var stack = APark();
		var screen = OpenSavePark( stack );

		Assert.AreEqual( "ParkGadget, ParkViewfinder, ParkSaveScreen", Names( stack ), "the menu closes and the screen opens" );
		Assert.AreEqual( "", Counted(), "opening counts nothing" );
		Assert.IsTrue( screen.Modal );
		Assert.IsTrue( screen.Pauses );
		Assert.IsTrue( screen.ClosesParkScreen );

		Assert.AreEqual( "0 New Save 8.10.2026 12:28 | 1 Old Park 1.10.2026 14:18",
			string.Join( " | ", ListOf( screen ).Rows.Select( row => $"{row.Id} {row.Name} {row.Values![0]}" ) ) );

		var controls = Everything( screen.Root ).ToArray();

		Assert.AreEqual( "Save Park", controls.Single( control => control.Id == 0x23bce1 ).Text );
		Assert.AreEqual( Localization.Text( 201 ), controls.Single( control => control.Id == 0x23bce1 ).Text );
		Assert.IsInstanceOfType( controls.Single( control => control.Id == Ok ), typeof( UI.UiButton ) );

		var box = (UI.UiEdit)controls.Single( control => control.Id == NameBox );

		Assert.AreEqual( 15, box.MaxLength );
		Assert.AreEqual( "New Save", box.Value );
		Assert.AreEqual( Localization.Text( 206 ), screen.Name );
		Assert.AreSame( box, screen.Focus );

		Frame( stack );
		Assert.IsTrue( Input.TextCaptured, "the box has the keys, so the park's shortcuts hear none" );
	}

	/// <summary>
	/// <b>What is typed first replaces the name the box opened on; a character a file name cannot hold is dropped,
	/// and so is the sixteenth.</b> The original's box read "ab", then "aba!cdefghijklm" and no more.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> the opening text not selected gives "New Saveab"; a limit of sixteen takes the p; the refused
	/// list left empty takes the nine.
	/// </remarks>
	[TestMethod]
	public void TheBoxTakesFifteenCharactersAndRefusesNine()
	{
		var stack = APark();
		var screen = OpenSavePark( stack );

		Type( stack, "ab" );
		Assert.AreEqual( "ab", screen.Name );

		Type( stack, "\\/*?:|<>\"" );
		Assert.AreEqual( "ab", screen.Name, "none of the nine is taken" );

		Type( stack, "a!cdefghijklm" );
		Assert.AreEqual( "aba!cdefghijklm", screen.Name );
		Assert.AreEqual( 15, screen.Name.Length );

		Type( stack, "nop q" );
		Assert.AreEqual( "aba!cdefghijklm", screen.Name, "the sixteenth is dropped" );

		Press( stack, Key.BackSpace );
		Press( stack, Key.BackSpace );
		Type( stack, " Z" );
		Assert.AreEqual( "aba!cdefghijk Z", screen.Name, "a space and a capital are taken where there is room" );
	}

	/// <summary>
	/// <b>A row's click puts that save's name in the box, unselected</b>, so the next letter goes on its end; the
	/// folder is read again at the click; the screen stays open and nothing is counted.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> the name put in selected gives "x" alone; the first row's name whatever was clicked; the
	/// list as it stood at the opening kept names the deleted file; the click saving or closing.
	/// </remarks>
	[TestMethod]
	public void ARowsClickPutsItsNameInTheBoxUnselected()
	{
		Put( "a.TPWS", DateTime.Now );
		Put( "b park.TPWS", DateTime.Now );
		Put( "c is a name of twenty.TPWS", DateTime.Now );

		var stack = APark();
		var screen = OpenSavePark( stack );

		Type( stack, "qw" );
		ListOf( screen ).Activated!( 1 );
		Assert.AreEqual( "b park", screen.Name );

		Type( stack, "x" );
		Assert.AreEqual( "b parkx", screen.Name, "not selected: the letter is added" );

		ListOf( screen ).Activated!( 2 );
		Assert.AreEqual( "c is a name of ", screen.Name, "the box's fifteen" );

		File.Delete( Path.Combine( Jungle, "a.TPWS" ) );
		ListOf( screen ).Activated!( 0 );
		Assert.AreEqual( "b park", screen.Name, "the first entry now" );

		ListOf( screen ).Activated!( 1 );
		ListOf( screen ).Activated!( 2 );
		Assert.AreEqual( "c is a name of ", screen.Name, "a place the folder has lost changes nothing" );

		Assert.AreEqual( "ParkGadget, ParkViewfinder, ParkSaveScreen", Names( stack ) );
		Assert.AreEqual( "", Counted() );
	}

	/// <summary>
	/// <b>OK over a name the folder does not hold writes the running park under it at once and closes the
	/// screen</b>, the player's gms.dat with it. A name that differs from a save's only by a space at its end is such
	/// a name, and so is an empty one, written as <c>.TPWS</c>; one that differs only in its case is asked nothing
	/// and written over the save of that name, as the original under Wine wrote over <c>New Save.TPWS</c>.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> the save not made writes nothing; the screen left open; a compare that ignores case asks,
	/// and so does a name trimmed; an empty name refused stays open; the player not saved leaves no gms.dat.
	/// </remarks>
	[TestMethod]
	[DataRow( "zz", "New Save.TPWS, zz.TPWS" )]
	[DataRow( "new save", "New Save.TPWS" )]
	[DataRow( "New Save ", "New Save .TPWS, New Save.TPWS" )]
	[DataRow( "", ".TPWS, New Save.TPWS" )]
	public void OkOverANewNameWritesTheParkAndCloses( string name, string files )
	{
		Put( "New Save.TPWS", DateTime.Now );
		RunningPark( 4321 );

		var stack = APark();
		var screen = OpenSavePark( stack );

		Press( stack, Key.BackSpace );
		Type( stack, name );
		Assert.AreEqual( name, screen.Name );

		Click( screen, Ok );

		Assert.AreEqual( "ParkGadget, ParkViewfinder", Names( stack ), "the screen closes, with no question" );
		Assert.AreEqual( "", Counted() );
		Assert.IsFalse( stack.AnyPausing );
		Assert.AreEqual( files, Parks(), "the park is written under the name" );
		Assert.AreEqual( 4321, TickIn( Directory.GetFiles( Jungle ).Single( file => string.Equals( Path.GetFileName( file ), name + ".TPWS", StringComparison.OrdinalIgnoreCase ) ) ), "and is the running park" );
		Assert.IsTrue( File.Exists( Path.Combine( Jungle, "..", "gms.dat" ) ), "the player is written with it" );
	}

	/// <summary><b>Enter in the box is the OK button and Escape the cancel button; the cross closes too.</b></summary>
	/// <remarks>
	/// <b>Mutations:</b> Enter not answered leaves the screen; Escape saving writes one; the cross saving writes one.
	/// </remarks>
	[TestMethod]
	public void EnterIsOkAndEscapeIsCancel()
	{
		RunningPark( 900 );

		var stack = APark();

		OpenSavePark( stack );
		Press( stack, Key.Escape );
		Assert.AreEqual( "ParkGadget, ParkViewfinder", Names( stack ), "Escape closes" );
		Assert.AreEqual( "", Parks(), "and saves nothing" );
		Frame( stack );
		Assert.IsFalse( Input.TextCaptured, "the keys are the park's again" );

		Click( OpenSavePark( stack ), Cross );
		Assert.AreEqual( "ParkGadget, ParkViewfinder", Names( stack ), "the cross closes" );
		Assert.AreEqual( "", Parks() );

		OpenSavePark( stack );
		Type( stack, "mine" );
		Press( stack, Key.Enter );
		Assert.AreEqual( "ParkGadget, ParkViewfinder", Names( stack ), "Enter closes" );
		Assert.AreEqual( "mine.TPWS", Parks(), "and is the save" );
	}

	/// <summary>
	/// <b>OK over a name the folder holds asks UITEXT 205 first</b>, the name its parameter; the question's tick is
	/// the save, written over the file, and closes both, and its cross leaves the screen open and the file as it was.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> the question skipped writes at once; the name left out reads " exists"; the tick not saving
	/// leaves the old file; the cross saving; the folder as it stood at the opening kept asks nothing of a file written
	/// since.
	/// </remarks>
	[TestMethod]
	public void ANameInTheFolderIsAskedAboutFirst()
	{
		RunningPark( 1234 );

		var stack = APark();
		var screen = OpenSavePark( stack );

		// Written after the screen opened: the folder is read again at OK.
		Put( "New Save.TPWS", DateTime.Now );

		Press( stack, Key.Enter );
		Assert.AreEqual( "ParkGadget, ParkViewfinder, ParkSaveScreen, MessageBox", Names( stack ) );
		Assert.AreEqual( 4, new FileInfo( OldSave ).Length, "nothing is saved yet" );

		var question = stack.Windows.OfType<UI.MessageBox>().Single();

		Assert.AreEqual( "New Save exists\n\nOverwrite ?", Everything( question.Root ).Single( control => control.Id == 0x9873c7 ).Text );

		Click( question, QuestionCross );
		Assert.AreEqual( "ParkGadget, ParkViewfinder, ParkSaveScreen", Names( stack ), "the cross leaves the screen" );
		Assert.AreEqual( 4, new FileInfo( OldSave ).Length, "and the file" );

		Click( screen, Ok );
		Click( stack.Windows.OfType<UI.MessageBox>().Single(), QuestionTick );
		Assert.AreEqual( "ParkGadget, ParkViewfinder", Names( stack ), "the tick closes both" );
		Assert.AreEqual( "New Save.TPWS", Parks() );
		Assert.AreEqual( 1234, TickIn( OldSave ), "the running park is written over the old save" );
	}

	/// <summary>
	/// <b>The question takes the keys from the box, and its closing does not hand them back</b>: no letter, Enter or
	/// Escape is heard until the box is clicked, as in the original, where "y" then read "New Savey".
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> the focus kept through the question takes the first y and closes on Escape; a click on the
	/// box giving nothing back leaves "New Save".
	/// </remarks>
	[TestMethod]
	public void TheBoxLosesTheKeysToTheQuestionUntilItIsClicked()
	{
		Put( "New Save.TPWS", DateTime.Now );
		RunningPark( 900 );

		var stack = APark();
		var screen = OpenSavePark( stack );

		ListOf( screen ).Activated!( 0 );
		Press( stack, Key.Enter );
		Click( stack.Windows.OfType<UI.MessageBox>().Single(), QuestionCross );

		Type( stack, "y" );
		Press( stack, Key.Enter );
		Press( stack, Key.Escape );
		Assert.AreEqual( "New Save", screen.Name, "the letter is not heard" );
		Assert.IsFalse( ((UI.UiEdit)Everything( screen.Root ).Single( control => control.Id == NameBox )).HasFocus, "and the box shows no caret" );
		Assert.AreEqual( "ParkGadget, ParkViewfinder, ParkSaveScreen", Names( stack ), "nor Enter, nor Escape, and no menu opens" );
		Assert.IsFalse( Input.TextCaptured );

		Frame( stack, InTheBox, left: true );
		Frame( stack, InTheBox, left: false );
		Type( stack, "y" );
		Assert.AreEqual( "New Savey", screen.Name, "a click on the box gives it the keys again" );

		Press( stack, Key.Enter );
		Assert.AreEqual( "ParkGadget, ParkViewfinder", Names( stack ) );
		Assert.AreEqual( "New Save.TPWS, New Savey.TPWS", Parks() );
	}

	/// <summary>
	/// <b>With nobody playing the list is empty, and OK writes nothing, there being no folder, and closes.</b> With no
	/// park running it writes nothing either.
	/// </summary>
	[TestMethod]
	public void WithNobodyPlayingTheListIsEmpty()
	{
		Put( "a.TPWS", DateTime.Now );
		SetCurrentPlayer( null );
		RunningPark( 900 );

		var stack = APark();
		var screen = OpenSavePark( stack );

		Assert.AreEqual( 0, ListOf( screen ).Rows.Count );

		Click( screen, Ok );
		Assert.AreEqual( "ParkGadget, ParkViewfinder", Names( stack ) );
		Assert.AreEqual( "a.TPWS", Parks() );

		SetCurrentPlayer( new Player( 0, "Test", new PlayerFile { InstantAction = true } ) );
		Level.Current = null!;
		Click( OpenSavePark( stack ), Ok );
		Assert.AreEqual( "ParkGadget, ParkViewfinder", Names( stack ) );
		Assert.AreEqual( "a.TPWS", Parks(), "no park, no save" );
	}

	/// <summary>
	/// <b>The save installs the idle mode before it writes</b> (<c>0x00516d13</c>): a build tool armed is put away.
	/// The menu's Escape has already done it on the way to the screen, so this is the console's <c>savepark</c>.
	/// </summary>
	/// <remarks><b>Mutations:</b> the hand not let go leaves the path tool armed.</remarks>
	[TestMethod]
	public void TheSaveLetsGoOfTheHand()
	{
		RunningPark( 900 );
		ParkBuildMode.Arm( ParkBuildMode.Path );

		Assert.IsNotNull( Level.Current.WritePark( "held" ) );
		Assert.AreEqual( ParkBuildMode.None, ParkBuildMode.Current );
		Assert.AreEqual( 900, TickIn( Path.Combine( Jungle, "held.TPWS" ) ) );
	}

	/// <summary>
	/// <b>The player's gms.dat is written before the park</b> (<c>FUN_005c8a10</c>, the save's step 1): every line
	/// the save logs that finds the park file finds the player's file too, and one finds the player's alone.
	/// </summary>
	/// <remarks><b>Mutations:</b> the player saved after the writer leaves the park on the disc without them.</remarks>
	[TestMethod]
	public void ThePlayerIsWrittenBeforeThePark()
	{
		RunningPark( 900 );

		var player = Path.Combine( Jungle, "..", "gms.dat" );
		var park = Path.Combine( Jungle, "ordered.TPWS" );
		var seen = new List<(bool Player, bool Park)>();
		Logger.LogDelegate look = ( _, _ ) => seen.Add( (File.Exists( player ), File.Exists( park )) );

		Logger.OnLog += look;

		try
		{
			Assert.IsNotNull( Level.Current.WritePark( "ordered" ) );
		}
		finally
		{
			Logger.OnLog -= look;
		}

		Assert.IsTrue( seen.Any( line => line.Player && !line.Park ), "the player is on the disc while the park is still to be written" );
		Assert.IsFalse( seen.Any( line => line.Park && !line.Player ), "the park is never there without the player" );
	}

	/// <summary>
	/// <b>A worker in the hand is put down before the park is written</b> (<c>0x00516d13</c>): back on the middle of
	/// the cell they were picked up from, and written there, not where they hung.
	/// </summary>
	/// <remarks><b>Mutations:</b> the hand let go of after the writer writes them where they were picked up.</remarks>
	[TestMethod]
	public void AWorkerInTheHandIsPutDownBeforeTheParkIsWritten()
	{
		RunningPark( 900 );

		var people = new ParkPeople( Level.Current.Park, state: Level.Current.ParkState );

		try
		{
			var member = people.Staff.First( staff => staff.Activity == StaffActivity.Walking );
			var (cellX, cellY) = member.Navigator.Position.Cell;
			var centre = (PeepNavigator.WaypointCentre( cellX ), PeepNavigator.WaypointCentre( cellY ));

			member.Navigator.Position = new FixedVector( centre.Item1 + 9000, centre.Item2 - 7000 );
			Assert.AreEqual( (cellX, cellY), member.Navigator.Position.Cell, "off the middle, in the same cell" );
			Assert.IsTrue( people.PickUp( member.ThingId ) );

			Assert.IsNotNull( Level.Current.WritePark( "held" ) );
			Assert.AreEqual( 0, people.CarriedStaff, "put down" );

			var read = ParkIn( Path.Combine( Jungle, "held.TPWS" ) ).People.Single( person => person.ThingId == member.ThingId );

			Assert.AreEqual( centre, (read.Navigator.X, read.Navigator.Y), "written on the middle of their cell" );
			Assert.AreEqual( (int)StaffActivity.Idle, read.Staff!.Value.State );
		}
		finally
		{
			TestRun.DeleteEvery<ParkPeople>();
		}
	}

	/// <summary>
	/// <b>A save that fails is logged and the game goes on</b>: the screen and the question close as on a save made,
	/// as the original's do whatever its save answered (<c>0x0049e9fa</c>), and the old file is left as it was, where
	/// the original deletes it (a deviation, <c>docs/exe/saves.md</c>, "Where").
	/// </summary>
	/// <remarks><b>Mutations:</b> no catch around the save lets the failure out of the click.</remarks>
	[TestMethod]
	public void AFailedSaveIsLoggedAndTheScreenCloses()
	{
		Put( "New Save.TPWS", DateTime.Now );
		RunningPark( 900 );

		var failing = DispatchProxy.Create<IParkInitialState, FailingPark>();
		((FailingPark)(object)failing).Park = Level.Current.Park!;
		typeof( Level ).GetProperty( nameof( Level.Park ) )!.SetValue( Level.Current, failing );

		var errors = new List<string>();
		Logger.LogDelegate look = ( severity, text ) =>
		{
			if ( severity == Logger.Level.Error )
				errors.Add( text );
		};

		var stack = APark();

		OpenSavePark( stack );
		Press( stack, Key.Enter );
		Logger.OnLog += look;

		try
		{
			Click( stack.Windows.OfType<UI.MessageBox>().Single(), QuestionTick );
		}
		finally
		{
			Logger.OnLog -= look;
		}

		Assert.AreEqual( "ParkGadget, ParkViewfinder", Names( stack ), "the question and the screen close" );
		Assert.IsTrue( errors.Single().Contains( "the disc is full" ), "the failure is logged" );
		Assert.AreEqual( 4, new FileInfo( OldSave ).Length, "and the old save is left whole" );
	}

	/// <summary>
	/// <b>A name the folder holds in two cases is written in the case the question named</b>: only a file copied in
	/// makes two, and the question compares by case, so <c>A</c> asks about and writes <c>A.TPWS</c>, and <c>a</c>
	/// <c>a.TPWS</c>.
	/// </summary>
	/// <remarks><b>Mutations:</b> the first file in any case writes one of the two names over the other's file.</remarks>
	[TestMethod]
	public void ANameInTwoCasesIsWrittenInTheCaseAskedAbout()
	{
		Put( "a.TPWS", DateTime.Now );
		Put( "A.TPWS", DateTime.Now );
		RunningPark( 1234 );

		var stack = APark();

		OpenSavePark( stack );
		Type( stack, "A" );
		Press( stack, Key.Enter );
		Assert.AreEqual( "A exists\n\nOverwrite ?", Question( stack ) );
		Click( stack.Windows.OfType<UI.MessageBox>().Single(), QuestionTick );

		Assert.AreEqual( 1234, TickIn( Path.Combine( Jungle, "A.TPWS" ) ), "A is written" );
		Assert.AreEqual( 4, new FileInfo( Path.Combine( Jungle, "a.TPWS" ) ).Length, "and a is not" );

		RunningPark( 2345 );
		OpenSavePark( stack );
		Type( stack, "a" );
		Press( stack, Key.Enter );
		Assert.AreEqual( "a exists\n\nOverwrite ?", Question( stack ) );
		Click( stack.Windows.OfType<UI.MessageBox>().Single(), QuestionTick );

		Assert.AreEqual( 2345, TickIn( Path.Combine( Jungle, "a.TPWS" ) ), "a is written" );
		Assert.AreEqual( 1234, TickIn( Path.Combine( Jungle, "A.TPWS" ) ), "and A is not" );
		Assert.AreEqual( "A.TPWS, a.TPWS", Parks() );
	}

	/// <summary>
	/// <b>Leaving a park writes it as <c>autosave.TPWS</c></b> (state 0xb, <c>0x0054ff10</c>), at the tick it was left
	/// on, beside the menu's saves, and entering the theme again finds it as the newest park file. Leaving the lobby
	/// writes nothing.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> another name; the running park's tick not written (the file's 755); the guard on the scene
	/// turned round writes nothing from a park.
	/// </remarks>
	[TestMethod]
	public void LeavingAParkWritesTheAutosaveThatEnteringFindsNewest()
	{
		Put( "easymode.TPWI", new DateTime( 2026, 10, 1, 14, 18, 0, DateTimeKind.Local ) );
		Put( "New Save.TPWS", new DateTime( 2026, 10, 8, 12, 28, 0, DateTimeKind.Local ) );

		var lobby = (Level)RuntimeHelpers.GetUninitializedObject( typeof( Level ) );
		typeof( Level ).GetProperty( nameof( Level.Kind ) )!.SetValue( lobby, Level.Scene.Lobby );
		Assert.IsNull( lobby.WriteParkOnLeaving() );
		Assert.AreEqual( "New Save.TPWS", Parks() );

		RunningPark( 1234 );

		Assert.AreEqual( "users/1Test/jungle/autosave.TPWS", Level.Current.WriteParkOnLeaving() );
		Assert.AreEqual( "New Save.TPWS, autosave.TPWS", Parks() );
		Assert.AreEqual( 1234, TickIn( Path.Combine( Jungle, "autosave.TPWS" ) ) );
		Assert.AreEqual( "autosave.TPWS", Path.GetFileName( SaveFolder.NewestPark( 0, "Test", "jungle" ) ) );
		Assert.AreEqual( "", Counted() );
	}

	/// <summary>
	/// <b>Entering a park writes it as <c>restart.INTS</c> where the folder holds none</b> (state 0xf, <c>FUN_00550b30</c>),
	/// the player first, and never again once it is there; it is not a saved park, and entering does not find it.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> the file there not looked for writes it again at the later tick; the extension <c>.TPWS</c> puts
	/// it among the saved parks; the guard on the scene turned round writes nothing from a park.
	/// </remarks>
	[TestMethod]
	public void EnteringAParkWritesRestartIntsOnlyWhereThereIsNone()
	{
		Put( "easymode.TPWI", new DateTime( 2026, 10, 1, 14, 18, 0, DateTimeKind.Local ) );
		RunningPark( 1234 );

		Assert.AreEqual( "users/1Test/jungle/restart.INTS", Level.Current.WriteRestartOnEntering() );
		Assert.AreEqual( 1234, TickIn( Path.Combine( Jungle, "restart.INTS" ) ) );
		Assert.IsTrue( File.Exists( Path.Combine( Jungle, "..", "gms.dat" ) ), "the player is written first" );
		Assert.AreEqual( "", Parks(), "not a saved park" );
		Assert.AreEqual( "easymode.TPWI", Path.GetFileName( SaveFolder.NewestPark( 0, "Test", "jungle" ) ), "nor entered with" );

		RunningPark( 2000 );
		Assert.IsNull( Level.Current.WriteRestartOnEntering() );
		Assert.AreEqual( 1234, TickIn( Path.Combine( Jungle, "restart.INTS" ) ), "the park as first entered stays" );
		Assert.AreEqual( "", Counted() );
	}

	/// <summary>
	/// <b>Restart Park loads <c>restart.INTS</c></b> (state 0xe, <c>0x0055036c</c>), found in any case, however much
	/// newer an autosave or a saved park is. With no such file a loaded park loads nothing; a park made fresh is made
	/// fresh again. The advisor is quietened first, file or none (state 0xe, <c>Advisor_StopQuietly(1)</c>).
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> the park entered again from the newest file (the menu's restart before <c>restart.INTS</c>) asks
	/// for no file; the fresh park's case taken for every missing file asks for one there too; the missing file not
	/// looked for asks for nothing at all; the quiet stop dropped leaves his line to come.
	/// </remarks>
	[TestMethod]
	public void RestartParkLoadsRestartIntsAndNotTheNewestFile()
	{
		RunningPark( 900 );
		var stack = APark();
		ForgetWhatWasAsked();

		var advisor = (Advisor)RuntimeHelpers.GetUninitializedObject( typeof( Advisor ) );
		var current = typeof( Advisor ).GetProperty( nameof( Advisor.Current ), BindingFlags.Static | BindingFlags.NonPublic )!;
		var pending = typeof( Advisor ).GetField( "_pending", BindingFlags.Instance | BindingFlags.NonPublic )!;
		var advisorBefore = current.GetValue( null );
		pending.SetValue( advisor, 412 );

		// Made without its constructor, so the field its quietening empties is given here.
		typeof( Advisor ).GetField( "_glints", BindingFlags.Instance | BindingFlags.NonPublic )!.SetValue( advisor, new UI.UiParticleChannel() );
		current.SetValue( null, advisor );

		try
		{
			Restart( stack );
		}
		finally
		{
			current.SetValue( null, advisorBefore );
		}

		Assert.AreEqual( 0, pending.GetValue( advisor ), "the advisor is quietened, file or none (0x0055035a)" );
		Assert.AreEqual( (null, null), WhatWasAsked(), "a loaded park with no restart file loads nothing" );
		Assert.AreEqual( "ParkGadget, ParkViewfinder", Names( stack ) );

		Put( "RESTART.ints", new DateTime( 2026, 9, 29, 8, 51, 0, DateTimeKind.Local ) );
		Put( "autosave.TPWS", DateTime.Now );
		Restart( stack );
		Assert.AreEqual( ("jungle", "users/1Test/jungle/RESTART.ints"), WhatWasAsked() );

		ForgetWhatWasAsked();
		File.Delete( Path.Combine( Jungle, "RESTART.ints" ) );
		typeof( Level ).GetProperty( nameof( Level.Park ) )!.SetValue( Level.Current, null );
		Restart( stack );
		Assert.AreEqual( ("jungle", null), WhatWasAsked(), "a park made fresh is made again" );
	}

	/// <summary>The game menu's third row, Restart Park, and its question's tick.</summary>
	private static void Restart( UI.WindowStack stack )
	{
		Press( stack, Key.Escape );

		if ( !stack.Windows.OfType<UI.GameMenu>().Any() )
			Press( stack, Key.Escape );

		var menu = stack.Windows.OfType<UI.GameMenu>().Single();
		var choices = (IList)typeof( UI.GameMenu ).GetField( "_choices", BindingFlags.Instance | BindingFlags.NonPublic )!.GetValue( menu )!;

		((UI.UiControl)choices[2]!).LeftClicked!();
		Click( stack.Windows.OfType<UI.MessageBox>().Single(), QuestionTick );
	}

	/// <summary>
	/// <b>Alt+S writes the running park under the theme's name</b> (the game table's row 12, <c>FUN_0040bf90</c>), as
	/// the menu's save does, the player first, over a file of that name in another case with no question. S alone,
	/// Ctrl+Alt+S and Alt+L write nothing.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> the binding without Alt writes on a plain S; the name <c>autosave</c>; the key-up not asked
	/// for writes nothing; S and L swapped writes on Alt+L.
	/// </remarks>
	[TestMethod]
	public void AltSWritesTheRunningParkAsTheThemesQuicksave()
	{
		Put( "Jungle.TPWS", DateTime.Now );
		RunningPark( 1234 );

		QuickKey( Key.S );
		QuickKey( Key.S, Key.ControlLeft, Key.AltLeft );
		QuickKey( Key.L, Key.AltLeft );
		Assert.AreEqual( 4, new FileInfo( Path.Combine( Jungle, "Jungle.TPWS" ) ).Length, "nothing is written but by Alt+S" );
		Assert.IsFalse( File.Exists( Path.Combine( Jungle, "..", "gms.dat" ) ) );

		QuickKey( Key.S, Key.AltLeft );
		Assert.AreEqual( "Jungle.TPWS", Parks(), "the one file, written over" );
		Assert.AreEqual( 1234, TickIn( Path.Combine( Jungle, "Jungle.TPWS" ) ), "with the running park" );
		Assert.IsTrue( File.Exists( Path.Combine( Jungle, "..", "gms.dat" ) ), "and the player with it" );
		Assert.AreEqual( "", Counted() );
	}

	/// <summary>
	/// <b>Alt+L loads the theme's quicksave</b> (row 11, <c>FUN_0040c040</c>), found in any case, as a Load Park row's
	/// click asks for a file; with none in the folder, or with nobody playing, nothing is asked for. L alone does not.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> the file's name not the theme's asks for nothing; the missing file not checked asks for one
	/// that is not there; the binding without Alt asks on a plain L.
	/// </remarks>
	[TestMethod]
	public void AltLLoadsTheThemesQuicksaveAndNothingWithoutOne()
	{
		Put( "New Save.TPWS", DateTime.Now );
		RunningPark( 900 );
		ForgetWhatWasAsked();

		QuickKey( Key.L, Key.AltLeft );
		Assert.AreEqual( (null, null), WhatWasAsked(), "no jungle.TPWS, no load" );

		Put( "JUNGLE.TPWS", DateTime.Now );
		QuickKey( Key.L );
		Assert.AreEqual( (null, null), WhatWasAsked(), "L alone is not the key" );

		GameTableFrames( [Down( Key.AltLeft ), Down( Key.L )], [Up( Key.AltLeft )], [Up( Key.L )] );
		Assert.AreEqual( (null, null), WhatWasAsked(), "Alt let go first leaves L to come up alone" );

		QuickKey( Key.L, Key.AltLeft );
		Assert.AreEqual( ("jungle", "users/1Test/jungle/JUNGLE.TPWS"), WhatWasAsked() );

		ForgetWhatWasAsked();
		SetCurrentPlayer( null );
		QuickKey( Key.L, Key.AltLeft );
		Assert.AreEqual( (null, null), WhatWasAsked(), "nobody playing, no folder" );
	}

	/// <summary>
	/// <b>Alt+S on a folder with no quicksave writes <c>jungle.TPWS</c></b>, lower case, the theme folder's own string,
	/// as the original wrote it (<c>docs/exe/saves.md</c>, "The quicksave and quickload keys", "Measured (Q545)").
	/// </summary>
	/// <remarks><b>Mutations:</b> the theme's name written with a capital writes <c>Jungle.TPWS</c>.</remarks>
	[TestMethod]
	public void AltSOnAFolderWithNoQuicksaveWritesTheThemesLowerCaseName()
	{
		RunningPark( 1234 );

		QuickKey( Key.S, Key.AltLeft );

		Assert.AreEqual( "jungle.TPWS", Parks() );
		Assert.AreEqual( 1234, TickIn( Path.Combine( Jungle, "jungle.TPWS" ) ) );
	}

	/// <summary>
	/// <b>In first person the game table's keys do nothing</b>: the viewfinder's layer has the focus and its key-up runs
	/// the camera's table alone (<c>FUN_00488a00</c>, <c>0x00488ae3</c>), so Alt+S writes nothing, Alt+L loads nothing,
	/// and Backspace and Delete leave the queue tool armed. Back in orbit each does its own.
	/// </summary>
	/// <remarks><b>Mutations:</b> first person taken out of <c>Level.GameTableKeys</c>' gate writes, loads, counts the queue's
	/// undo and puts the tool away.</remarks>
	[TestMethod]
	public void TheGameTableKeysDoNothingInFirstPerson()
	{
		Put( "jungle.TPWS", DateTime.Now );
		RunningPark( 1234 );
		ForgetWhatWasAsked();

		FirstPerson( true );

		try
		{
			TheFourKeysDoNothing( "in first person" );
		}
		finally
		{
			FirstPerson( false );
		}

		TheFourKeys();
		Assert.AreEqual( 1234, TickIn( Path.Combine( Jungle, "jungle.TPWS" ) ), "in orbit Alt+S writes the park" );
		Assert.AreEqual( ("jungle", "users/1Test/jungle/jungle.TPWS"), WhatWasAsked(), "Alt+L loads it" );
		Assert.AreEqual( ParkBuildMode.None, ParkBuildMode.Current, "and Delete puts the tool away" );
		Assert.AreEqual( "1x BACKSPACE_UNDO_QUEUE_RUN, 1x CLEAR_LAND_TOOL", Counted(), "Backspace's undo of the queue is counted" );
	}

	/// <summary>
	/// <b>Behind a park screen, a pausing window or the full-screen view the quick keys do nothing</b>, as the build keys
	/// do not: the gate is the game table's (<c>Level.GameTableKeys</c>).
	/// </summary>
	/// <remarks><b>Mutations:</b> the quick keys asked above the gate's return write and load under each.</remarks>
	[TestMethod]
	public void TheQuickKeysDoNothingBehindAParkScreenAPausingWindowOrTheView()
	{
		Put( "jungle.TPWS", DateTime.Now );
		RunningPark( 1234 );
		ForgetWhatWasAsked();

		var screenOpen = typeof( UI.WindowStack ).GetProperty( "ParkScreenOpen", BindingFlags.Static | BindingFlags.NonPublic )!;
		screenOpen.SetValue( null, true );

		try
		{
			TheFourKeysDoNothing( "behind a park screen" );
		}
		finally
		{
			screenOpen.SetValue( null, false );
		}

		// The armed tool would take the first Escape.
		ParkBuildMode.Forget();
		var menu = APark();
		Press( menu, Key.Escape );
		Assert.AreEqual( "ParkGadget, ParkViewfinder, GameMenu", Names( menu ) );
		Windows( menu );
		Assert.IsTrue( Level.Current.PausedByWindow() );
		TheFourKeysDoNothing( "behind the game menu" );

		var view = new UI.WindowStack();
		view.Cover( true );
		Windows( view );
		Assert.IsTrue( Level.Current.FullScreenView );
		TheFourKeysDoNothing( "under the full-screen view" );

		Windows( null );
		TheFourKeys();
		Assert.AreEqual( ("jungle", "users/1Test/jungle/jungle.TPWS"), WhatWasAsked(), "with none of them the keys are heard" );
	}

	/// <summary>Backspace, Delete, Alt+S and Alt+L let go in turn, with the queue tool armed.</summary>
	private static void TheFourKeys()
	{
		ParkBuildMode.Arm( ParkBuildMode.Queue );
		Unimplemented.Forget();

		QuickKey( Key.BackSpace );
		QuickKey( Key.Delete );
		QuickKey( Key.S, Key.AltLeft );
		QuickKey( Key.L, Key.AltLeft );
	}

	/// <summary><see cref="TheFourKeys"/>, and nothing done: the four-byte quicksave as it was, no load asked for, the tool armed and nothing counted.</summary>
	private void TheFourKeysDoNothing( string where )
	{
		TheFourKeys();

		Assert.AreEqual( 4, new FileInfo( Path.Combine( Jungle, "jungle.TPWS" ) ).Length, $"{where}: Alt+S writes nothing" );
		Assert.AreEqual( (null, null), WhatWasAsked(), $"{where}: Alt+L loads nothing" );
		Assert.AreEqual( ParkBuildMode.Queue, ParkBuildMode.Current, $"{where}: Delete and Alt+S leave the tool armed" );
		Assert.AreEqual( "", Counted(), $"{where}: Backspace counts no undo" );
	}

	private static void FirstPerson( bool on )
		=> typeof( ParkCamcorderCameraMode ).GetProperty( nameof( ParkCamcorderCameraMode.Active ) )!.SetValue( null, on );

	/// <summary>The running level's window stack.</summary>
	private static void Windows( UI.WindowStack? stack )
		=> typeof( Level ).GetField( "_windows", BindingFlags.Instance | BindingFlags.NonPublic )!.SetValue( Level.Current, stack );

	/// <summary>
	/// <b>Closing the game writes the autosave before the player is saved and let go</b> (<c>Game.LeaveOnClose</c>):
	/// Quit Game and the window's close, as <c>Game_Shutdown</c> runs state 0xb.
	/// </summary>
	/// <remarks><b>Mutations:</b> the write moved below <c>SaveAndDeselect</c> writes nothing, nobody being left playing.</remarks>
	[TestMethod]
	public void ClosingTheGameWritesTheAutosaveBeforeThePlayerIsSaved()
	{
		RunningPark( 1234 );

		Game.LeaveOnClose();

		Assert.AreEqual( 1234, TickIn( Path.Combine( Jungle, "autosave.TPWS" ) ) );
		Assert.IsNull( Players.Roster.Current, "and the player is saved and let go" );
	}

	/// <summary>
	/// <b>Only the lobby asked for writes the autosave as the park is left</b>, before the scene is unloaded
	/// (<c>Game.ChangeSceneIfAsked</c>): Restart Park and a Load Park row ask for a park, which passes state 0xb by, and
	/// write nothing. With nothing asked for nothing is done.
	/// </summary>
	/// <remarks><b>Mutations:</b> a write added on the park's branch writes the autosave for Restart Park; the lobby's write
	/// moved below the unload writes it after.</remarks>
	[TestMethod]
	public void OnlyTheLobbyAskedForWritesTheAutosaveAsTheParkIsLeft()
	{
		Put( "New Save.TPWS", DateTime.Now );
		RunningPark( 1234 );

		var autosave = Path.Combine( Jungle, "autosave.TPWS" );
		var done = new List<string>();

		void Leave() => Game.ChangeSceneIfAsked(
			() => done.Add( File.Exists( autosave ) ? "unload, the autosave written" : "unload" ),
			( theme, file ) => done.Add( $"park {theme} {file ?? "as entered"}" ),
			() => done.Add( "lobby" ) );

		Leave();
		Assert.AreEqual( "", string.Join( ", ", done ), "nothing asked for" );

		Game.RequestParkLoad( "jungle" );
		Leave();
		Game.RequestParkLoad( "jungle", "users/1Test/jungle/New Save.TPWS" );
		Leave();
		Assert.AreEqual( "unload, park jungle as entered, unload, park jungle users/1Test/jungle/New Save.TPWS", string.Join( ", ", done ) );
		Assert.AreEqual( "New Save.TPWS", Parks(), "Restart Park and Load Park write nothing" );

		done.Clear();
		Game.RequestLobbyReload();
		Leave();
		Assert.AreEqual( "unload, the autosave written, lobby", string.Join( ", ", done ) );
		Assert.AreEqual( 1234, TickIn( autosave ) );
	}

	/// <summary>
	/// <b>A park entered from the lobby is written as <c>restart.INTS</c> once it is built</b> (state 9 on to 0xf,
	/// <c>Game.ChangeSceneIfAsked</c>); a park built again from a park (a Load Park row, Alt+L, Restart Park) is loaded
	/// in place in the original, never passes 0xf, and writes none.
	/// </summary>
	/// <remarks><b>Mutations:</b> the call dropped writes nothing from the lobby; the guard on the scene dropped writes the
	/// file for a Load Park row.</remarks>
	[TestMethod]
	public void OnlyAParkEnteredFromTheLobbyWritesRestartInts()
	{
		var lobby = (Level)RuntimeHelpers.GetUninitializedObject( typeof( Level ) );
		typeof( Level ).GetProperty( nameof( Level.Kind ) )!.SetValue( lobby, Level.Scene.Lobby );
		Level.Current = lobby;

		var restart = Path.Combine( Jungle, "restart.INTS" );
		var tick = 755;

		void Change() => Game.ChangeSceneIfAsked( () => { }, ( _, _ ) => RunningPark( tick ), () => { } );

		Game.RequestParkLoad( "jungle" );
		Change();
		Assert.AreEqual( 755, File.Exists( restart ) ? TickIn( restart ) : -1, "entered from the lobby, the park as built" );

		File.Delete( restart );
		tick = 900;
		Game.RequestParkLoad( "jungle", "users/1Test/jungle/New Save.TPWS" );
		Change();
		Assert.IsFalse( File.Exists( restart ), "a Load Park row or Alt+L, from a park, writes none" );
		Assert.AreEqual( "", Counted() );
	}

	/// <summary>
	/// The key let go with the modifiers held round it, a frame each, the running level's game-table keys asked after
	/// every frame as <c>Level.Update</c> asks them.
	/// </summary>
	private static void QuickKey( Key key, params Key[] held )
		=> GameTableFrames( [.. held.Select( Down ), Down( key )], [Up( key )], [.. held.Select( Up )] );

	/// <summary>A frame for each set of key events, the running level's game-table keys asked after each.</summary>
	private static void GameTableFrames( params KeyEvent[][] frames )
	{
		var keys = typeof( Level ).GetMethod( "GameTableKeys", BindingFlags.Instance | BindingFlags.NonPublic )!;

		foreach ( var frame in frames )
		{
			Input.UpdateFrom( new Snapshot( frame, "", Grass, false ) );
			keys.Invoke( Level.Current, [] );
		}
	}

	private static KeyEvent Down( Key key ) => new( key, true, ModifierKeys.None );

	private static KeyEvent Up( Key key ) => new( key, false, ModifierKeys.None );

	/// <summary>The park the game has been asked to load between frames, and the file to load it from.</summary>
	private static (string? Theme, string? File) WhatWasAsked()
		=> ((string?)Asked( "_parkAsked" ).GetValue( null ), (string?)Asked( "_parkFileAsked" ).GetValue( null ));

	private static void ForgetWhatWasAsked()
	{
		Asked( "_parkAsked" ).SetValue( null, null );
		Asked( "_parkFileAsked" ).SetValue( null, null );
	}

	private static FieldInfo Asked( string field ) => typeof( Game ).GetField( field, BindingFlags.Static | BindingFlags.NonPublic )!;

	/// <summary>
	/// The shipped park running in a level of its own at a tick of its own, which is not the file's 755, so a save
	/// that wrote the file as loaded is told from one that wrote the running park.
	/// </summary>
	private void RunningPark( int tick )
	{
		using var stream = new MemoryStream( FileSystem.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		var reader = new SaveReader( stream );
		var world = new ParkWorld( reader.ReadFile(), reader.Preamble );
		var state = new ParkState( world );
		var level = (Level)RuntimeHelpers.GetUninitializedObject( typeof( Level ) );

		state.SetGameTick( tick );
		typeof( Level ).GetProperty( nameof( Level.Kind ) )!.SetValue( level, Level.Scene.Park );
		typeof( Level ).GetProperty( nameof( Level.ThemeName ) )!.SetValue( level, "jungle" );
		typeof( Level ).GetProperty( nameof( Level.Park ) )!.SetValue( level, world );
		typeof( Level ).GetProperty( nameof( Level.ParkState ) )!.SetValue( level, state );
		Level.Current = level;
	}

	/// <summary>The park files in the player's jungle folder, by name.</summary>
	private string Parks() => string.Join( ", ", Directory.GetFiles( Jungle, "*.TPWS" ).Select( Path.GetFileName ).Order( StringComparer.Ordinal ) );

	private string OldSave => Path.Combine( Jungle, "New Save.TPWS" );

	/// <summary>The <c>mGameTick</c> a park file holds.</summary>
	private static int TickIn( string path ) => ParkIn( path ).GameTick;

	/// <summary>A park file, read.</summary>
	private static ParkWorld ParkIn( string path )
	{
		using var stream = new MemoryStream( File.ReadAllBytes( path ) );
		var reader = new SaveReader( stream );

		return new ParkWorld( reader.ReadFile(), reader.Preamble );
	}

	/// <summary>The overwrite question's text.</summary>
	private static string Question( UI.WindowStack stack )
		=> Everything( stack.Windows.OfType<UI.MessageBox>().Single().Root ).Single( control => control.Id == 0x9873c7 ).Text!;

	/// <summary>The park as it stands, but for its file, which fails to be read as a full disc would fail a write.</summary>
	public class FailingPark : DispatchProxy
	{
		public IParkInitialState Park = null!;

		protected override object? Invoke( MethodInfo? method, object?[]? args )
			=> method!.Name == $"get_{nameof( IParkInitialState.Save )}" ? throw new IOException( "the disc is full" ) : method.Invoke( Park, args );
	}

	/// <summary>What has been counted, less the list's own count of a first row it does not select.</summary>
	private static string Counted() => string.Join( ", ", Unimplemented.Summary
		.Where( entry => entry.What != "LIST_FIRST_ROW_SELECTED" )
		.Select( entry => $"{entry.Times}x {entry.What}" ) );

	private static UI.WindowStack APark()
	{
		var stack = new UI.WindowStack();

		_ = new UI.ParkFrontEnd( stack, "jungle" );

		return stack;
	}

	/// <summary>Opens the game menu with Escape and chooses its second row, Save Game, as a click let go on it does.</summary>
	private static UI.ParkSaveScreen OpenSavePark( UI.WindowStack stack )
	{
		Press( stack, Key.Escape );

		if ( !stack.Windows.OfType<UI.GameMenu>().Any() )
			Press( stack, Key.Escape );

		var menu = stack.Windows.OfType<UI.GameMenu>().Single();
		var choices = (IList)typeof( UI.GameMenu ).GetField( "_choices", BindingFlags.Instance | BindingFlags.NonPublic )!.GetValue( menu )!;

		((UI.UiControl)choices[1]!).LeftClicked!();

		return stack.Windows.OfType<UI.ParkSaveScreen>().Single();
	}

	private static UI.UiList ListOf( UI.UiWindow screen ) => Everything( screen.Root ).OfType<UI.UiList>().Single();

	private static void Click( UI.UiWindow window, int id )
		=> ((UI.UiButton)Everything( window.Root ).Single( control => control.Id == id )).Clicked!();

	private static IEnumerable<UI.UiControl> Everything( UI.UiControl control )
		=> control.Children.SelectMany( Everything ).Prepend( control );

	private static void Frame( UI.WindowStack stack, params KeyEvent[] keys )
	{
		Input.UpdateFrom( new Snapshot( keys, "", Grass, false ) );
		stack.Update();
	}

	private static void Frame( UI.WindowStack stack, Vector2 at, bool left )
	{
		Input.UpdateFrom( new Snapshot( [], "", at, left ) );
		stack.Update();
	}

	private static void Press( UI.WindowStack stack, Key key )
	{
		Frame( stack, new KeyEvent( key, true, ModifierKeys.None ) );
		Frame( stack, new KeyEvent( key, false, ModifierKeys.None ) );
	}

	/// <summary>The characters as one frame's text input, then a frame with none.</summary>
	private static void Type( UI.WindowStack stack, string text )
	{
		Input.UpdateFrom( new Snapshot( [], text, Grass, false ) );
		stack.Update();
		Frame( stack );
	}

	private static string Names( UI.WindowStack stack ) => string.Join( ", ", stack.Windows.Select( window => window.GetType().Name ) );

	private void Put( string name, DateTime written )
	{
		var path = Path.Combine( Jungle, name );
		File.WriteAllBytes( path, [1, 2, 3, 4] );
		File.SetLastWriteTime( path, written );
	}

	private static void SetCurrentPlayer( Player? player )
		=> typeof( Players ).GetProperty( nameof( Players.Current ) )!.SetValue( Players.Roster, player );

	/// <summary>SDL's events for one frame: the keys, the text typed, where the pointer is and whether the left button is down.</summary>
	private sealed class Snapshot( KeyEvent[] keys, string typed, Vector2 at, bool left ) : InputSnapshot
	{
		public IReadOnlyList<KeyEvent> KeyEvents => keys;
		public IReadOnlyList<MouseEvent> MouseEvents => [];
		public IReadOnlyList<char> KeyCharPresses => typed.ToCharArray();
		public System.Numerics.Vector2 MousePosition => new( at.X, at.Y );
		public float WheelDelta => 0f;
		public bool IsMouseDown( MouseButton button ) => button == MouseButton.Left && left;
	}
}
