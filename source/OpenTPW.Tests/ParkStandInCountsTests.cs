using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenTPW.UI;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace OpenTPW.Tests;

/// <summary>
/// Four park-screen readouts are stand-ins for the original's, and each is counted where it is drawn (Q865, the
/// counting piece of Q158): the object window's scrap row
/// (<c>FUN_004e2400</c>), the entry-price screen's park-name heading (<c>0x00498e4c</c>), the gadget's date (UITEXT 448)
/// and the buy screen's turning model and kind row (<c>0x1ec</c>). Reads the shipped park; skipped without the game
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
	/// <b>An object window letters a built ride's age as UITEXT 433 and its scrap row as money, and counts the scrap
	/// percentage it does not compute</b>; a thing with no build date has no age, and its scrap row still prints.
	/// </summary>
	/// <remarks><b>Mutations:</b> the scrap report taken out; the age a bare number; the age's row 434 or 435; the age with no build date lettered; the scrap row a bare number.</remarks>
	[TestMethod]
	public void AnObjectWindowWordsItsAgeAsDaysAndLettersItsScrapAsMoneyAndCountsThePercentage()
	{
		var scrap = Times( "SCRAP_VALUE_DEPRECIATION" );

		var window = new ParkObjectWindow( _stack, 13 );
		_stack.Open( window );

		var state = Level.Current!.ParkState!;
		Assert.IsTrue( state.TryObject( 13, out var built ) );
		_catalogue.TryGet( built.CatalogueId, out var item );

		Assert.AreEqual( $"{state.AgeInDays( built )} days", Find( window.Root, 0x3e1b ).Text, "UITEXT 433, always days" );
		Assert.AreEqual( ParkItemsScreen.Money( item.BuildPrice ), Find( window.Root, 0x3e23 ).Text, "UITEXT 449" );
		Assert.IsTrue( Find( window.Root, 0x3e23 ).Text!.StartsWith( "$ " ) );
		Assert.IsTrue( Times( "SCRAP_VALUE_DEPRECIATION" ) > scrap, "its scrap row is the build price" );

		// A thing with no build date prints no age; its scrap row still prints.
		state.AddObject( new ParkWorld.CatalogueObject( ThingId: 9000, CatalogueId: built.CatalogueId, RawX: built.RawX, RawY: built.RawY, Angle: 0 ) );

		scrap = Times( "SCRAP_VALUE_DEPRECIATION" );

		var unbuilt = new ParkObjectWindow( _stack, 9000 );
		_stack.Open( unbuilt );

		Assert.IsTrue( string.IsNullOrEmpty( Find( unbuilt.Root, 0x3e1b ).Text ), "no build date, no age" );
		Assert.IsTrue( Times( "SCRAP_VALUE_DEPRECIATION" ) > scrap, "the scrap row prints for it all the same" );
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

	/// <summary><b>The entry-price screen counts its empty heading when it is built.</b></summary>
	/// <remarks><b>Mutations:</b> the report taken out.</remarks>
	[TestMethod]
	public void TheEntryPriceScreenCountsItsParkNameHeading()
	{
		var before = Times( "ENTRY_PRICE_TITLE_PARK_NAME" );

		_stack.Open( new ParkEntryPriceScreen( _stack ) );

		Assert.AreEqual( before + 1, Times( "ENTRY_PRICE_TITLE_PARK_NAME" ) );
	}

	/// <summary><b>The gadget counts its date each time it writes it</b>: once built, and again each frame.</summary>
	/// <remarks><b>Mutations:</b> the report taken out; counted only at the build.</remarks>
	[TestMethod]
	public void TheGadgetCountsItsDateEachTimeItIsWritten()
	{
		var before = Times( "GADGET_DATE_UITEXT_448" );

		var gadget = new ParkGadget( _stack );
		_stack.Open( gadget );

		var built = Times( "GADGET_DATE_UITEXT_448" );
		Assert.IsTrue( built > before, "written when built" );

		_stack.Update();

		Assert.IsTrue( Times( "GADGET_DATE_UITEXT_448" ) > built, "and rewritten on a frame" );
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
