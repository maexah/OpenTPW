using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenTPW.UI;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace OpenTPW.Tests;

/// <summary>
/// The park-screen readouts of Q158: the object window's scrap row (<c>FUN_004e2400</c>), the entry-price screen's
/// park-name heading (<c>0x00498e4c</c>) and the gadget's date (UITEXT 448), each built, and the buy screen's turning model
/// and kind row (<c>0x1ec</c>), still counted where they are drawn (Q865). Reads the shipped park; skipped without the game
/// (<see cref="GameData"/>).
/// </summary>
[TestClass]
public class ParkStandInCountsTests
{
	private Level? _level;
	private Point2 _screen;
	private ParkPeople _people = null!;
	private ParkItemCatalogue _catalogue = null!;
	private WindowStack _stack = null!;

	[TestInitialize]
	public void LoadThePark()
	{
		_level = Level.Current;
		_screen = Screen.Size;

		Screen.Size = new Point2( 2048, 1536 );
		Log ??= new();
		FileSystem = GameData.Required();
		Input.Mouse = new();

		using var stream = new MemoryStream( FileSystem.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		var world = new ParkWorld( new SaveReader( stream ).ReadFile() );
		_catalogue = new ParkItemCatalogue( "jungle", FileSystem );

		var state = new ParkState( world );

		var level = (Level)RuntimeHelpers.GetUninitializedObject( typeof( Level ) );
		typeof( Level ).GetProperty( nameof( Level.Park ) )!.SetValue( level, world );
		typeof( Level ).GetProperty( nameof( Level.ParkState ) )!.SetValue( level, state );
		typeof( Level ).GetProperty( nameof( Level.Catalogue ) )!.SetValue( level, _catalogue );
		typeof( Level ).GetProperty( nameof( Level.Research ) )!.SetValue( level, new ParkResearch( world.ObjectControlRecords, _catalogue ) );
		Level.Current = level;

		_people = new ParkPeople( world, new ParkBalance( "jungle", easyMode: true ), null, state, _catalogue );
		_stack = new WindowStack();
	}

	[TestCleanup]
	public void PutItAllBack()
	{
		_people?.Delete();
		Entity.ApplyDeletions();
		Level.Current = _level!;
		Screen.Size = _screen;
		Input.Mouse = new();
	}

	/// <summary>
	/// <b>An object window letters a built ride's age as UITEXT 433 and its scrap row as money at the scrap percentage</b>
	/// (<c>FUN_004e2400</c>): the stock Belly Bounce, 32 days old, "$ 250", half its 500; a thing with no build date and no
	/// customer has no age, and its scrap row is the whole "$ 500". Nothing is counted for the row.
	/// </summary>
	/// <remarks><b>Mutations:</b> the build price whole fails "$ 250"; a flat half fails "$ 500"; the age a bare number; the age's
	/// row 434 or 435; the age with no build date lettered.</remarks>
	[TestMethod]
	public void AnObjectWindowWordsItsAgeAsDaysAndLettersItsScrapAtThePercentage()
	{
		var scrap = Times( "SCRAP_VALUE_DEPRECIATION" );

		var window = new ParkObjectWindow( _stack, 13 );
		_stack.Open( window );

		var state = Level.Current!.ParkState!;
		Assert.IsTrue( state.TryObject( 13, out var built ) );
		_catalogue.TryGet( built.CatalogueId, out var item );

		Assert.AreEqual( (32, 500), (state.AgeInDays( built ), item.BuildPrice), "the stock Belly Bounce" );
		Assert.AreEqual( "32 days", Find( window.Root, 0x3e1b ).Text, "UITEXT 433, always days" );
		Assert.AreEqual( "$ 250", Find( window.Root, 0x3e23 ).Text, "UITEXT 449 at 50 per cent" );

		// A thing with no build date prints no age; its scrap row is the whole price.
		state.AddObject( new ParkWorld.CatalogueObject( ThingId: 9000, CatalogueId: built.CatalogueId, RawX: built.RawX, RawY: built.RawY, Angle: 0 ) );

		var unbuilt = new ParkObjectWindow( _stack, 9000 );
		_stack.Open( unbuilt );

		Assert.IsTrue( string.IsNullOrEmpty( Find( unbuilt.Root, 0x3e1b ).Text ), "no build date, no age" );
		Assert.AreEqual( "$ 500", Find( unbuilt.Root, 0x3e23 ).Text, "new and unused: 100 per cent" );
		Assert.AreEqual( scrap, Times( "SCRAP_VALUE_DEPRECIATION" ), "nothing counted" );
	}

	/// <summary>
	/// <b>The ride window's readouts are their UITEXT rows with the value filled in</b>, so the duration keeps its unit
	/// word: 428 "Duration : {P7} sec" for one, 429 "Duration : {P7} secs" for any other number (<c>FUN_004aec30</c>).
	/// </summary>
	/// <remarks><b>Mutations:</b> the unit word dropped (the row's first part alone); the singular chosen for every value; the plural for every value.</remarks>
	[TestMethod]
	public void TheRideWindowsReadoutsKeepTheirUnitWords()
	{
		var window = new ParkObjectWindow( _stack, 13 );
		_stack.Open( window );

		var flags = BindingFlags.Instance | BindingFlags.NonPublic;
		var sliders = (UiSlider[])typeof( ParkObjectWindow ).GetField( "_sliders", flags )!.GetValue( window )!;
		var readouts = (UiControl[])typeof( ParkObjectWindow ).GetField( "_readouts", flags )!.GetValue( window )!;
		var letter = typeof( ParkObjectWindow ).GetMethod( "Letter", flags )!;

		Level.Current!.ParkState!.TryObject( 13, out var built );
		_catalogue.TryGet( built.CatalogueId, out var item );
		Assert.AreEqual( 1, item.DurationUnit, "the Belly Bounce counts seconds" );

		const int duration = 2;
		readouts[duration].Visible = true;

		sliders[duration].SetRange( 0, 60 );
		sliders[duration].SetValue( 30 );
		letter.Invoke( window, [duration] );
		Assert.AreEqual( "Duration : 30 secs", readouts[duration].Text );

		sliders[duration].SetValue( 1 );
		letter.Invoke( window, [duration] );
		Assert.AreEqual( "Duration : 1 sec", readouts[duration].Text );
	}

	private static UiControl Find( UiControl root, int id )
		=> root.Id == id ? root : root.Children.Select( c => FindOrNull( c, id ) ).First( c => c != null )!;

	private static UiControl? FindOrNull( UiControl root, int id )
		=> root.Id == id ? root : root.Children.Select( c => FindOrNull( c, id ) ).FirstOrDefault( c => c != null );

	/// <summary>
	/// <b>The entry-price screen letters the park's name in its heading as <c>UI_SetTitle</c> letters a title</b>
	/// (<c>0x00498e4c</c>): the stock park's gate thing (11) holds "LOST" and "Kingdom", so the heading reads "LOST Kingdom", in
	/// font 5 in (234, 239, 102) with its edge, in the stream's rect widened to 513..1457. The name is the file's lines, not
	/// the gate's item's rows of <c>OBJECT_NAMES</c> (which read the same in the stock park), and nothing is counted for a park with a file.
	/// </summary>
	/// <remarks><b>Mutations:</b> the heading left empty; the gate named by its item's table rows; the lines joined without
	/// the space; the lines swapped; the white of the stream; the stream's rect unwidened.</remarks>
	[TestMethod]
	public void TheEntryPriceScreenLettersTheParksNameInItsHeading()
	{
		var counted = Times( "ENTRY_PRICE_TITLE_PARK_NAME" ) + Times( "ENTRY_PRICE_FRESH_PARK_NAME" );

		_stack.Open( new ParkEntryPriceScreen( _stack ) );

		var heading = Find( _stack.Windows.OfType<ParkEntryPriceScreen>().Single().Root, 0x4f3af );

		Assert.AreEqual( "LOST Kingdom", heading.Text );
		Assert.AreEqual( "LOST Kingdom", ParkEntryPriceScreen.ParkName( Level.Current!.Park, _ => "the item's rows" ), "the file's lines, not the item's rows" );
		Assert.AreEqual( new UiColour( 234, 239, 102 ), heading.TextColour );
		Assert.IsTrue( heading.TextShadow );
		Assert.AreEqual( 5, heading.Font );
		Assert.AreEqual( new UiRect( 513, 174, 1457, 253 ), heading.Rect );
		Assert.AreEqual( counted, Times( "ENTRY_PRICE_TITLE_PARK_NAME" ) + Times( "ENTRY_PRICE_FRESH_PARK_NAME" ) );
	}

	/// <summary><b>The gadget writes its date again on a frame</b>, from UITEXT 448 (Q527): 10 January 2000 at the build,
	/// 13 February 2000 (tick 1000) after a frame, and nothing counted for it.</summary>
	/// <remarks><b>Mutations:</b> written only at the build; the report put back.</remarks>
	[TestMethod]
	public void TheGadgetRewritesItsDateOnAFrame()
	{
		var before = Times( "GADGET_DATE_UITEXT_448" );

		try
		{
			GameCalendar.Rebase( 208 );
			var gadget = new ParkGadget( _stack );
			_stack.Open( gadget );
			Assert.AreEqual( "1.10.2000", FindOrNull( gadget.Root, 0x20 )!.Text );

			GameCalendar.Rebase( 1000 );
			_stack.Update();

			Assert.AreEqual( "2.13.2000", FindOrNull( gadget.Root, 0x20 )!.Text );
			Assert.AreEqual( before, Times( "GADGET_DATE_UITEXT_448" ), "no longer a stand-in" );
		}
		finally
		{
			GameCalendar.Rebase();
		}
	}

	/// <summary>
	/// <b>A park file's objects keep the two lines of their names</b> (<c>+60</c> and <c>+62</c>, four bytes a character): the
	/// stock gate "LOST" and "Kingdom", the Belly Bounce "Belly" and "Bounce", the Traffic Lights one line and an empty second;
	/// an id that is no object has none.
	/// </summary>
	/// <remarks><b>Mutations:</b> the lines read two bytes a character; the two offsets swapped; a line read past its nought.</remarks>
	[TestMethod]
	public void AParkFilesObjectsKeepTheTwoLinesOfTheirNames()
	{
		var world = (ParkWorld)Level.Current!.Park!;

		Assert.IsTrue( world.TryNameOf( 11, out var a, out var b ) );
		Assert.AreEqual( ("LOST", "Kingdom"), (a, b) );
		Assert.IsTrue( world.TryNameOf( 13, out a, out b ) );
		Assert.AreEqual( ("Belly", "Bounce"), (a, b) );
		Assert.IsTrue( world.TryNameOf( 12, out a, out b ) );
		Assert.AreEqual( ("Traffic Lights", ""), (a, b) );
		Assert.IsFalse( world.TryNameOf( 0, out _, out _ ) );
	}

	/// <summary>
	/// <b>A park with no file names its gate as its item's rows do, and counts it</b>: what the original's fresh gate holds is not read.
	/// </summary>
	/// <remarks><b>Mutations:</b> the count taken out; the file's lines asked of a park with none.</remarks>
	[TestMethod]
	public void AParkWithNoFileNamesItsGateByItsItemAndCountsIt()
	{
		var fresh = new FreshPark( "jungle", new ParkBalance( "jungle", easyMode: false ), _catalogue, FileSystem );
		var before = Times( "ENTRY_PRICE_FRESH_PARK_NAME" );

		Assert.AreEqual( $"item rows of {fresh.ParkGates}", ParkEntryPriceScreen.ParkName( fresh, id => $"item rows of {id}" ) );
		Assert.AreEqual( before + 1, Times( "ENTRY_PRICE_FRESH_PARK_NAME" ) );
	}

	/// <summary>
	/// <b>The gadget letters the calendar's date from UITEXT 448</b>, day, month and year unpadded with the month from one and
	/// the year whole, as the american table orders them (Q527): <c>mGameTick</c> 5000 is 5 August 2000, "8.5.2000"; nothing is counted.
	/// </summary>
	/// <remarks><b>Mutations:</b> the machine's short date ("8/5/2000"); the month from nought ("7.5.2000"); the year less
	/// 1900 ("8.5.100"); the day and the month swapped ("5.8.2000"); the count left in.</remarks>
	[TestMethod]
	public void TheGadgetLettersTheDateFromUitext448()
	{
		var before = Times( "GADGET_DATE_UITEXT_448" );

		try
		{
			GameCalendar.Rebase( 5000 );

			var gadget = new ParkGadget( _stack );
			_stack.Open( gadget );
			_stack.Update();

			Assert.AreEqual( new DateTime( 2000, 8, 5, 0, 20, 0 ), GameCalendar.Now );
			Assert.AreEqual( "8.5.2000", Find( gadget.Root, 0x20 ).Text );
			Assert.AreEqual( before, Times( "GADGET_DATE_UITEXT_448" ) );
		}
		finally
		{
			GameCalendar.Rebase();
		}
	}

	/// <summary>
	/// <b>The buy screen counts the model and the kind row for every row it fills the panel for</b>: a ride's, both land
	/// rows' (-1 and -2) and a mystery ride's, which clear the picture but still build a model and write the row.
	/// </summary>
	/// <remarks><b>Mutations:</b> either report taken out; counted only where a footprint is painted; the two names swapped.</remarks>
	[TestMethod]
	public void TheBuyScreenCountsTheModelAndTheKindRowForEveryRow()
	{
		var screen = new ParkBuyScreen( _stack );
		_stack.Open( screen );

		var preview = typeof( ParkBuyScreen ).GetMethod( "Preview", BindingFlags.Instance | BindingFlags.NonPublic )!;
		var mystery = _catalogue.All.First( GoldenTickets.IsMystery );
		var ride = _catalogue.All.First( item => item.Shape is not null && !GoldenTickets.IsMystery( item ) );

		foreach ( var (row, what) in new[] { (-1, "Buy Land"), (-2, "Clear Land"), (mystery.Id, mystery.Name), (ride.Id, ride.Name) } )
		{
			var model = Times( "BUY_SCREEN_ITEM_MODEL" );
			var kind = Times( "BUY_SCREEN_ITEM_KIND_ROW" );

			preview.Invoke( screen, [row] );

			Assert.AreEqual( model + 1, Times( "BUY_SCREEN_ITEM_MODEL" ), what );
			Assert.AreEqual( kind + 1, Times( "BUY_SCREEN_ITEM_KIND_ROW" ), what );
		}
	}

	private static int Times( string what ) => Unimplemented.Summary.FirstOrDefault( entry => entry.What == what ).Times;
}
