using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenTPW.UI;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace OpenTPW.Tests;

/// <summary>
/// Five park-screen readouts are stand-ins for the original's, and each is counted where it is drawn (Q865, the
/// counting piece of Q158): the object window's age wording (<c>FUN_006acd60</c> with <c>0x1b1</c>) and its scrap row
/// (<c>FUN_004e2400</c>), the entry-price screen's park-name heading (<c>0x00498e4c</c>), the gadget's date (UITEXT 448)
/// and the buy screen's turning model and name row (<c>0x1ec</c>). Reads the shipped park; skipped without the game
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
	/// <b>An object window of a built ride counts its age wording and its scrap value</b> when its figures are filled,
	/// and the scrap row counts the same gap the shop window and a sale do.
	/// </summary>
	/// <remarks><b>Mutations:</b> the age report taken out; the scrap report taken out; the age counted with no build date; either named for the other.</remarks>
	[TestMethod]
	public void AnObjectWindowCountsItsAgeWordingAndItsScrapValue()
	{
		var age = Times( "OBJECT_WINDOW_AGE_WORDING" );
		var scrap = Times( "SCRAP_VALUE_DEPRECIATION" );

		_stack.Open( new ParkObjectWindow( _stack, 13 ) );

		Assert.IsTrue( Times( "OBJECT_WINDOW_AGE_WORDING" ) > age, "the Belly Bounce is built, so its age is a bare number" );
		Assert.IsTrue( Times( "SCRAP_VALUE_DEPRECIATION" ) > scrap, "its scrap row is the build price" );

		// A thing with no build date prints no age, so there is no wording to count; its scrap row still prints.
		var state = Level.Current!.ParkState!;
		Assert.IsTrue( state.TryObject( 13, out var built ) );
		state.AddObject( new ParkWorld.CatalogueObject( ThingId: 9000, CatalogueId: built.CatalogueId, RawX: built.RawX, RawY: built.RawY, Angle: 0 ) );

		age = Times( "OBJECT_WINDOW_AGE_WORDING" );
		scrap = Times( "SCRAP_VALUE_DEPRECIATION" );

		_stack.Open( new ParkObjectWindow( _stack, 9000 ) );

		Assert.AreEqual( age, Times( "OBJECT_WINDOW_AGE_WORDING" ), "no build date, no age" );
		Assert.IsTrue( Times( "SCRAP_VALUE_DEPRECIATION" ) > scrap, "the scrap row prints for it all the same" );
	}

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
	/// <b>The buy screen counts the model and the name row where it paints a footprint</b> and nowhere else: a ride's row
	/// counts both, a land row (id 0) and a mystery ride's none.
	/// </summary>
	/// <remarks><b>Mutations:</b> either report taken out; counted for the land row too; the two names swapped.</remarks>
	[TestMethod]
	public void TheBuyScreenCountsTheModelAndTheNameRowWhereItPaintsAFootprint()
	{
		var screen = new ParkBuyScreen( _stack );
		_stack.Open( screen );

		var preview = typeof( ParkBuyScreen ).GetMethod( "Preview", BindingFlags.Instance | BindingFlags.NonPublic )!;
		var ride = _catalogue.All.First( item => item.Shape is not null && !GoldenTickets.IsMystery( item ) );

		var model = Times( "BUY_SCREEN_ITEM_MODEL" );
		var name = Times( "BUY_SCREEN_ITEM_NAME_ROW" );

		preview.Invoke( screen, [0] );

		Assert.AreEqual( model, Times( "BUY_SCREEN_ITEM_MODEL" ), "a land row paints no footprint" );
		Assert.AreEqual( name, Times( "BUY_SCREEN_ITEM_NAME_ROW" ) );

		var mystery = _catalogue.All.First( GoldenTickets.IsMystery );

		preview.Invoke( screen, [mystery.Id] );

		Assert.AreEqual( model, Times( "BUY_SCREEN_ITEM_MODEL" ), "a mystery ride's row clears the picture, as the land row does" );
		Assert.AreEqual( name, Times( "BUY_SCREEN_ITEM_NAME_ROW" ) );

		preview.Invoke( screen, [ride.Id] );

		Assert.AreEqual( model + 1, Times( "BUY_SCREEN_ITEM_MODEL" ), ride.Name );
		Assert.AreEqual( name + 1, Times( "BUY_SCREEN_ITEM_NAME_ROW" ), ride.Name );
	}

	private static int Times( string what ) => Unimplemented.Summary.FirstOrDefault( entry => entry.What == what ).Times;
}
