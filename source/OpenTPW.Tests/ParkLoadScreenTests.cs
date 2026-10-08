using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeoVeldrid;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace OpenTPW.Tests;

/// <summary>
/// The Load Park screen (<c>docs/exe/saves.md</c>, "One screen, two uses" to "What the handlers answer"): the
/// folder's list, a row's date, the menu row that opens it, and the park file a row's click asks for. Over a
/// disposable save folder.
/// </summary>
[TestClass]
public class ParkLoadScreenTests
{
	private static readonly Vector2 Grass = new( 1100, 800 );

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
		root = Directory.CreateTempSubdirectory( "opentpw-load-park-" ).FullName;
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
		ForgetWhatWasAsked();
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
		Unimplemented.Forget();
		Screen.Size = _screen;
	}

	/// <summary><b>A string's parts are its text and its parameters in order</b>, and a parameter takes the value handed for its number.</summary>
	/// <remarks>
	/// <b>Mutations:</b> a parameter part dropped gives ".."; the value taken by position and not by number gives the
	/// year first; the text after a parameter dropped loses " exists".
	/// </remarks>
	[TestMethod]
	public void ARowsParametersAreFilledByNumber()
	{
		var text = new StringFile( "Language/English/UITEXT.str" );

		Assert.AreEqual( "{16}.{17}.{18}", Shape( text.Parts[448] ) );
		Assert.AreEqual( "{2} exists\n\nOverwrite ?", Shape( text.Parts[205] ) );
		Assert.AreEqual( "Load Park", Shape( text.Parts[202] ) );

		Assert.AreEqual( "8.10.2026", Localization.Format( 448, (18, "2026"), (16, "8"), (17, "10") ) );
		Assert.AreEqual( ".10.", Localization.Format( 448, (17, "10") ), "a parameter handed no value is nothing" );
		Assert.AreEqual( "Load Park", Localization.Format( 202 ) );
		Assert.AreEqual( "", Localization.Format( 99999 ) );
	}

	/// <summary>
	/// <b>Every shipped table reads as parts</b>, and a string's first text part is the string the table has always
	/// given, where that one is not cut short at 255 characters.
	/// </summary>
	/// <remarks><b>Mutations:</b> the padding to four bytes dropped loses the parts after the first text.</remarks>
	[TestMethod]
	public void EveryShippedStringReadsAsParts()
	{
		var strings = 0;
		var parameters = 0;

		foreach ( var folder in new[] { "Language/English", "Language/american" } )
		{
			// Each folder's strings go through that folder's own character table, and the reader has the English one.
			if ( folder != "Language/English" )
				continue;

			foreach ( var path in FileSystem.GetFiles( folder ).Where( file => file.EndsWith( ".str", StringComparison.OrdinalIgnoreCase ) ) )
			{
				var file = new StringFile( path );

				Assert.AreEqual( file.Entries.Length, file.Parts.Length, path );

				for ( var row = 0; row < file.Parts.Length; ++row )
				{
					var parts = file.Parts[row];
					var first = parts.Length > 0 ? parts[0].Text : "";

					if ( first != null && first.Length < 256 )
						Assert.AreEqual( file.Entries[row], first, $"{path} row {row}" );

					++strings;
					parameters += parts.Count( part => part.Text == null );
				}
			}
		}

		Assert.AreEqual( 2365, strings, "the 21 English tables' strings" );
		Assert.IsTrue( parameters > 0 );
		Console.WriteLine( $"{strings} strings, {parameters} parameter parts" );
	}

	/// <summary><b>A save's date is day, month and year unpadded, then the hour and the minute in two digits each.</b></summary>
	/// <remarks><b>Mutations:</b> the minute unpadded reads 3:5; month and day swapped reads 2.1.</remarks>
	[TestMethod]
	public void ASavesDateIsWrittenAsTheOriginalWritesIt()
	{
		Assert.AreEqual( "8.10.2026 12:28", UI.ParkLoadScreen.DateOf( new DateTime( 2026, 10, 8, 12, 28, 59 ) ) );
		Assert.AreEqual( "1.2.2000 03:05", UI.ParkLoadScreen.DateOf( new DateTime( 2000, 2, 1, 3, 5, 0 ) ) );
	}

	/// <summary><b>A row is as tall as its font's height times 0x600 over the screen's height, plus 6.</b></summary>
	/// <remarks><b>Mutations:</b> the six left off.</remarks>
	[TestMethod]
	public void ARowsHeightComesFromItsFont()
	{
		Assert.AreEqual( 41, UI.ParkLoadScreen.RowHeightFor( 11, 480 ), "the original's own at 640 by 480, read from 0x007cb24c" );
		Assert.AreEqual( 40, UI.ParkLoadScreen.RowHeightFor( 17, 768 ) );
		Assert.AreEqual( 44, UI.ParkLoadScreen.RowHeightFor( 15, 600 ) );
		Assert.AreEqual( 44, UI.ParkLoadScreen.RowHeightFor( null, 768 ), "no font: the list's own height" );
	}

	/// <summary>
	/// <b>The saved parks are the folder's <c>*.TPWS</c></b>, each with its name less the extension and its last write
	/// in local time: not easymode.TPWI, not restart.INTS, and none for a folder that is not there.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> the <c>.TPW</c> pattern of the park's entry lists easymode; the extension left on names
	/// "b.TPWS"; UTC kept differs by the machine's offset wherever it has one; the creation time read differs.
	/// </remarks>
	[TestMethod]
	public void TheSavedParksAreTheFoldersTpwsFiles()
	{
		var written = new DateTime( 2026, 10, 8, 12, 28, 0, DateTimeKind.Local );

		Put( "easymode.TPWI", written );
		Put( "restart.INTS", written );
		Put( "gms.dat", written );
		Put( "b.TPWS", written );
		Put( "A park.tpws", written.AddDays( -7 ) );
		Put( "autosave.TPWS", written.AddMinutes( 1 ) );

		var parks = SaveFolder.SavedParks( 0, "Test", "jungle" );

		Assert.AreEqual( "A park, autosave, b", string.Join( ", ", parks.Select( park => park.Name ) ) );
		Assert.AreEqual( written, parks[2].Written );
		Assert.AreEqual( DateTimeKind.Local, parks[2].Written.Kind );
		Assert.AreEqual( written.AddDays( -7 ), parks[0].Written );
		Assert.AreEqual( Path.Join( "users", "1Test", "jungle", "A park.tpws" ), parks[0].Path );

		Assert.AreEqual( 0, SaveFolder.SavedParks( 0, "Test", "space" ).Count );
		Assert.AreEqual( 0, SaveFolder.SavedParks( 1, "Nobody", "jungle" ).Count );
	}

	/// <summary>
	/// <b>The menu's Load Game closes the menu and opens the Load Park screen</b>, modal and pausing, counted as
	/// nothing, its rows the player's saved parks with their dates, titled UITEXT 202, with no OK button and no name box.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> the row left counting <c>LOAD_GAME</c> opens nothing; the menu left open stands under the
	/// screen; the screen not modal or not pausing; the fill skipped leaves no rows; another player's folder read
	/// leaves none.
	/// </remarks>
	[TestMethod]
	public void LoadGameOpensTheLoadParkScreenOnThePlayersSavedParks()
	{
		SetCurrentPlayer( new Player( 0, "Test", new PlayerFile { InstantAction = true } ) );
		Put( "New Save.TPWS", new DateTime( 2026, 10, 8, 12, 28, 0, DateTimeKind.Local ) );
		Put( "Old Park.TPWS", new DateTime( 2026, 10, 1, 14, 18, 0, DateTimeKind.Local ) );
		Put( "easymode.TPWI", new DateTime( 2026, 10, 1, 14, 18, 0, DateTimeKind.Local ) );

		var stack = APark();
		var screen = OpenLoadPark( stack );

		Assert.AreEqual( "ParkGadget, ParkViewfinder, ParkLoadScreen", Names( stack ), "the menu closes and the screen opens" );
		Assert.IsFalse( Unimplemented.Summary.Any( entry => entry.What is "LOAD_GAME" or "LOAD_PARK_LIST_SCROLLBAR" ) );
		Assert.IsTrue( screen.Modal );
		Assert.IsTrue( screen.Pauses );
		Assert.IsTrue( stack.AnyPausing );
		Assert.IsTrue( screen.ClosesParkScreen );

		var list = ListOf( screen );

		Assert.AreEqual( "0 New Save 8.10.2026 12:28 | 1 Old Park 1.10.2026 14:18",
			string.Join( " | ", list.Rows.Select( row => $"{row.Id} {row.Name} {row.Values![0]}" ) ) );
		Assert.IsTrue( list.SelectsUnderPointer );
		Assert.AreEqual( 0, list.SortWord, "no sort: the folder's order" );

		var controls = Everything( screen.Root ).ToArray();

		Assert.AreEqual( Localization.Text( 202 ), controls.Single( control => control.Id == 0x23bce1 ).Text );
		Assert.IsFalse( controls.Any( control => control.Id is -1 or 0x23bce2 ), "Load hides OK and the name box" );
		Assert.IsNull( WhatWasAsked().Theme, "opening loads nothing" );
	}

	/// <summary>
	/// <b>A click on a row asks for that file and closes the screen</b>; the folder is read again at the click, so the
	/// row's place is looked up in the folder as it stands then, and a place it no longer has loads nothing.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> the first row loaded whatever was clicked; the file left out of the request (the park entered
	/// afresh); the screen left open; the list as it stood at the opening kept, which asks for the deleted file.
	/// </remarks>
	[TestMethod]
	public void ARowsClickAsksForThatFileAsTheFolderStandsAtTheClick()
	{
		SetCurrentPlayer( new Player( 0, "Test", new PlayerFile { InstantAction = true } ) );
		Put( "a.TPWS", DateTime.Now );
		Put( "b.TPWS", DateTime.Now );
		Put( "c.TPWS", DateTime.Now );

		var stack = APark();
		var screen = OpenLoadPark( stack );

		ListOf( screen ).Activated!( 1 );
		Assert.AreEqual( "ParkGadget, ParkViewfinder", Names( stack ), "the screen closes" );
		Assert.AreEqual( ("jungle", Path.Join( "users", "1Test", "jungle", "b.TPWS" )), WhatWasAsked() );

		ForgetWhatWasAsked();
		screen = OpenLoadPark( stack );
		File.Delete( Path.Combine( Jungle, "a.TPWS" ) );
		ListOf( screen ).Activated!( 1 );
		Assert.AreEqual( ("jungle", Path.Join( "users", "1Test", "jungle", "c.TPWS" )), WhatWasAsked(), "the second entry now" );

		ForgetWhatWasAsked();
		screen = OpenLoadPark( stack );
		File.Delete( Path.Combine( Jungle, "c.TPWS" ) );
		ListOf( screen ).Activated!( 1 );
		Assert.AreEqual( "ParkGadget, ParkViewfinder", Names( stack ), "a place the folder has lost: closed" );
		Assert.IsNull( WhatWasAsked().Theme, "and nothing loaded" );
	}

	/// <summary><b>The cross closes the screen and loads nothing; Enter and Escape do nothing.</b></summary>
	/// <remarks>
	/// <b>Mutations:</b> the cross loading a row; the screen not modal, which lets Escape open the game menu over it.
	/// </remarks>
	[TestMethod]
	public void TheCrossClosesAndTheKeysDoNothing()
	{
		SetCurrentPlayer( new Player( 0, "Test", new PlayerFile { InstantAction = true } ) );
		Put( "a.TPWS", DateTime.Now );

		var stack = APark();
		var screen = OpenLoadPark( stack );

		Press( stack, Key.Escape );
		Press( stack, Key.Enter );
		Assert.AreEqual( "ParkGadget, ParkViewfinder, ParkLoadScreen", Names( stack ) );

		((UI.UiButton)Everything( screen.Root ).Single( control => control.Id == -2 )).Clicked!();
		Assert.AreEqual( "ParkGadget, ParkViewfinder", Names( stack ) );
		Assert.IsFalse( stack.AnyPausing );
		Assert.IsNull( WhatWasAsked().Theme );
	}

	/// <summary><b>With nobody playing the list is empty.</b></summary>
	[TestMethod]
	public void WithNobodyPlayingTheListIsEmpty()
	{
		Put( "a.TPWS", DateTime.Now );
		SetCurrentPlayer( null );

		Assert.AreEqual( 0, ListOf( OpenLoadPark( APark() ) ).Rows.Count );
	}

	/// <summary>
	/// <b>A park asked for by file is read from that file</b>, not from the newest in the folder: the older file is
	/// the shipped park's copy, 755, and the newer one will not read.
	/// </summary>
	/// <remarks><b>Mutations:</b> the file ignored reads the newer one and gives nothing.</remarks>
	[TestMethod]
	public void AParkAskedForByFileIsReadFromThatFile()
	{
		SetCurrentPlayer( new Player( 0, "Test", new PlayerFile { InstantAction = true } ) );
		File.WriteAllBytes( Path.Combine( Jungle, "Old Park.TPWS" ), FileSystem.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		File.SetLastWriteTime( Path.Combine( Jungle, "Old Park.TPWS" ), DateTime.Now.AddDays( -3 ) );
		Put( "Newer.TPWS", DateTime.Now );

		var balance = new ParkBalance( "jungle", easyMode: Level.InstantAction );
		var catalogue = new ParkItemCatalogue( "jungle", instantAction: Level.InstantAction );

		var park = Level.CreatePark( "jungle", balance, catalogue, Path.Join( "users", "1Test", "jungle", "Old Park.TPWS" ) );

		Assert.IsInstanceOfType( park, typeof( ParkWorld ) );
		Assert.AreEqual( 755, park!.GameTick );
		Assert.IsNull( Level.CreatePark( "jungle", balance, catalogue ), "entered, the newest is read, which will not" );
	}

	private static string Shape( StringPart[] parts )
		=> string.Concat( parts.Select( part => part.Text ?? $"{{{part.Parameter}}}" ) );

	private static UI.WindowStack APark()
	{
		var stack = new UI.WindowStack();

		_ = new UI.ParkFrontEnd( stack, "jungle" );

		return stack;
	}

	/// <summary>Opens the game menu with Escape and chooses its first row, Load Game, as a click let go on it does.</summary>
	private static UI.ParkLoadScreen OpenLoadPark( UI.WindowStack stack )
	{
		Press( stack, Key.Escape );

		if ( !stack.Windows.OfType<UI.GameMenu>().Any() )
			Press( stack, Key.Escape );

		var menu = stack.Windows.OfType<UI.GameMenu>().Single();
		var choices = (IList)typeof( UI.GameMenu ).GetField( "_choices", BindingFlags.Instance | BindingFlags.NonPublic )!.GetValue( menu )!;

		((UI.UiControl)choices[0]!).LeftClicked!();

		return stack.Windows.OfType<UI.ParkLoadScreen>().Single();
	}

	private static UI.UiList ListOf( UI.ParkLoadScreen screen ) => Everything( screen.Root ).OfType<UI.UiList>().Single();

	private static IEnumerable<UI.UiControl> Everything( UI.UiControl control )
		=> control.Children.SelectMany( Everything ).Prepend( control );

	private static void Frame( UI.WindowStack stack, params KeyEvent[] keys )
	{
		Input.UpdateFrom( new Snapshot( keys, Grass ) );
		stack.Update();
	}

	private static void Press( UI.WindowStack stack, Key key )
	{
		Frame( stack, new KeyEvent( key, true, ModifierKeys.None ) );
		Frame( stack, new KeyEvent( key, false, ModifierKeys.None ) );
	}

	private static string Names( UI.WindowStack stack ) => string.Join( ", ", stack.Windows.Select( window => window.GetType().Name ) );

	/// <summary>The park the game has been asked to load between frames, and the file to load it from.</summary>
	private static (string? Theme, string? File) WhatWasAsked()
		=> ((string?)Asked( "_parkAsked" ).GetValue( null ), (string?)Asked( "_parkFileAsked" ).GetValue( null ));

	private static void ForgetWhatWasAsked()
	{
		Asked( "_parkAsked" ).SetValue( null, null );
		Asked( "_parkFileAsked" ).SetValue( null, null );
	}

	private static FieldInfo Asked( string field ) => typeof( Game ).GetField( field, BindingFlags.Static | BindingFlags.NonPublic )!;

	private void Put( string name, DateTime written )
	{
		var path = Path.Combine( Jungle, name );
		File.WriteAllBytes( path, [1, 2, 3, 4] );
		File.SetLastWriteTime( path, written );
	}

	private static void SetCurrentPlayer( Player? player )
		=> typeof( Players ).GetProperty( nameof( Players.Current ) )!.SetValue( Players.Roster, player );

	/// <summary>SDL's events for one frame: the keys, where the pointer is and which buttons are down.</summary>
	private sealed class Snapshot( KeyEvent[] keys, Vector2 at ) : InputSnapshot
	{
		public IReadOnlyList<KeyEvent> KeyEvents => keys;
		public IReadOnlyList<MouseEvent> MouseEvents => [];
		public IReadOnlyList<char> KeyCharPresses => [];
		public System.Numerics.Vector2 MousePosition => new( at.X, at.Y );
		public float WheelDelta => 0f;
		public bool IsMouseDown( MouseButton button ) => false;
	}
}
