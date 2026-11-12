using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenTPW.UI;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace OpenTPW.Tests;

/// <summary>
/// The shop's window (<c>FUN_004b0e30</c>, its handler <c>FUN_004b0b30</c>; <c>docs/exe/hud.md</c>, "The shop's window"),
/// on the shipped park's Drinks Shop, thing 16. The window's tests are skipped without the game (<see cref="GameData"/>);
/// the local happiness's and apply to all's are not.
/// </summary>
[TestClass]
public class ParkShopWindowTests
{
	private const int DrinksShop = 16;
	private const int BellyBounce = 13;

	private Level? _level;
	private Point2 _screen;
	private float _now;
	private Vector3 _looksAt;
	private ParkPeople? _people;
	private ParkState _state = null!;
	private WindowStack _stack = null!;
	private Level _park = null!;
	private bool _confirmations;
	private ParkObjects? _objects;

	private void LoadThePark()
	{
		Screen.Size = new Point2( 2048, 1536 );
		Log ??= new();
		FileSystem = GameData.Required();
		Input.Mouse = new();
		Unimplemented.Forget();

		using var stream = new MemoryStream( FileSystem.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		var world = new ParkWorld( new SaveReader( stream ).ReadFile() );
		var catalogue = new ParkItemCatalogue( "jungle", FileSystem );
		_state = new ParkState( world );

		_park = (Level)RuntimeHelpers.GetUninitializedObject( typeof( Level ) );
		typeof( Level ).GetProperty( nameof( Level.Park ) )!.SetValue( _park, world );
		typeof( Level ).GetProperty( nameof( Level.ParkState ) )!.SetValue( _park, _state );
		typeof( Level ).GetProperty( nameof( Level.Catalogue ) )!.SetValue( _park, catalogue );
		typeof( Level ).GetProperty( nameof( Level.Kind ) )!.SetValue( _park, Level.Scene.Park );
		Level.Current = _park;

		_people = new ParkPeople( world, new ParkBalance( "jungle", easyMode: true ), null, _state, catalogue );
		_stack = new WindowStack();
		typeof( Level ).GetField( "_windows", BindingFlags.NonPublic | BindingFlags.Instance )!.SetValue( _park, _stack );
	}

	[TestInitialize]
	public void KeepWhatTheTestsChange()
	{
		_level = Level.Current;
		_screen = Screen.Size;
		_now = Time.Now;
		_looksAt = ParkOrbitCameraMode.PointOfInterest;
		_confirmations = GameOptions.Current.Confirmations;
	}

	[TestCleanup]
	public void PutItAllBack()
	{
		ParkHand.LetGo();
		_objects?.Delete();
		_people?.Delete();
		Entity.ApplyDeletions();
		GameOptions.Current.Confirmations = _confirmations;
		Level.Current = _level!;
		Screen.Size = _screen;
		Time.Now = _now;
		Input.Mouse = new();
		ParkOrbitCameraMode.PointOfInterest = _looksAt;
		Unimplemented.Forget();
	}

	private ParkShopWindow? Window => _stack.Windows.OfType<ParkShopWindow>().FirstOrDefault();

	private ParkWorld.CatalogueObject Shop => _state.Objects.Single( placed => placed.ThingId == DrinksShop );

	private static int Times( string what ) => Unimplemented.Summary.FirstOrDefault( gap => gap.What == what ).Times;

	private void Click( UiControl root, int id )
	{
		var control = Find( root, id );
		Assert.IsTrue( _stack.ClickAt( (control.Rect.Left + control.Rect.Right) / 2, (control.Rect.Top + control.Rect.Bottom) / 2 ), $"{id:x} clicked" );
	}

	private ParkShopWindow Open()
	{
		_park.OpenObjectWindow( DrinksShop );
		Assert.IsNotNull( Window, "the shop's click opens the shop's window" );
		return Window!;
	}

	/// <summary>
	/// <b>A shop's click opens its window</b> (<c>FUN_00486920</c>'s arm 1), as the original's on the same shop
	/// (<c>q31/orig/b-1-window.png</c>): titled "Drinks Shop", the labels UITEXT 32 to 38, "0 of 0", "$ 20", "$ 0", "$ 0",
	/// the scrap value "$ 325" (its 650 at 50 per cent, <c>docs/exe/hud.md</c>, "Measured in the original"), satisfaction 50 as a bar of 512, the price box "$ 30", the quality's middle icon lit, "Amount of ice", and with
	/// one shop no arrows; nothing counted as <c>SHOP_WINDOW</c>.
	/// </summary>
	/// <remarks><b>Mutations:</b> the arm 1 route gone; a label's row; C and M swapped in UITEXT 460; the quality's
	/// divide by 50; the scrap row the whole build price; the ingredient's row less one; the arrows shown under two shops.</remarks>
	[TestMethod]
	public void AShopsClickOpensItsWindow()
	{
		LoadThePark();
		var window = Open();

		Assert.AreEqual( 0, Times( "SHOP_WINDOW" ) );
		Assert.AreEqual( "Drinks Shop", Find( window.Root, 0xc06e ).Text );

		CollectionAssert.AreEqual(
			new[] { "Customers last month", "Cost of goods", "Takings last month", "Profit last month", "Customer satisfaction", "Scrap value", "Local happiness" },
			new[] { 0xc075, 0xc071, 0xc072, 0xc073, 0xc074, 0xc07a, 0xc07b }.Select( id => Find( window.Root, id ).Text ).ToArray() );

		CollectionAssert.AreEqual( new[] { "0 of 0", "$ 20", "$ 0", "$ 0", "$ 325" },
			new[] { 0xc070, 0xc076, 0xc077, 0xc078, 0xc07d }.Select( id => Find( window.Root, id ).Text ).ToArray() );

		Assert.AreEqual( 512, ((ParkObjectWindow.UiStatBar)Find( window.Root, 0xc079 )).Units, "satisfaction 50 with no day" );
		Assert.AreEqual( "$ 30", Find( window.Root, 0xc085 ).Text );
		Assert.AreEqual( "Amount of ice", Find( window.Root, 0xc084 ).Text );
		Assert.AreEqual( ("Quality of goods", "Sale price"), (Find( window.Root, 0xc07e ).Text, Find( window.Root, 0xc083 ).Text) );
		StringAssert.Contains( window.Census(), "quality 1 (icon 1) amount 50" );
		Assert.IsFalse( Find( window.Root, 0xc069 ).Visible || Find( window.Root, 0xc06a ).Visible, "one shop: no arrows" );
	}

	/// <summary>
	/// <b>The month's figures are the finished days'</b> (<c>FUN_004b01d0</c>): a day of three customers and two walk-aways,
	/// 90 taken and 60 spent, satisfaction 30, then a customer today: "3 of 5" (C the customers, M those and the
	/// walk-aways), "$ 90", "$ 30", and the shipped ring's thirty days with this one's 30 averaging 1, so 51 as
	/// (51 &lt;&lt; 10) / 100 = 522.
	/// </summary>
	/// <remarks><b>Mutations:</b> C and M swapped; M without the walk-aways; today's customer counted; profit as takings.</remarks>
	[TestMethod]
	public void TheMonthsFiguresAreTheFinishedDays()
	{
		LoadThePark();

		var rings = _state.RingsFor( DrinksShop );

		for ( var i = 0; i < 3; ++i )
			rings.CountCustomer();

		rings.CountWalkAway();
		rings.CountWalkAway();
		rings.Takings.Today = 90;
		rings.Costs.Today = 60;
		rings.Satisfy( 30 );
		rings.Roll();
		rings.CountCustomer();

		var window = Open();

		CollectionAssert.AreEqual( new[] { "3 of 5", "$ 90", "$ 30" },
			new[] { 0xc070, 0xc077, 0xc078 }.Select( id => Find( window.Root, id ).Text ).ToArray() );
		Assert.AreEqual( 522, ((ParkObjectWindow.UiStatBar)Find( window.Root, 0xc079 )).Units );
	}

	/// <summary>
	/// <b>The settings are buffered and written on the close</b> (<c>FUN_004b0aa0</c> from <c>0x0048d200</c>'s message
	/// <c>0x14</c>), as in the original (<c>q31/orig</c> rounds b and c): the quality to the right and the amount to the
	/// left show a cost of goods of "$ 30" at once and leave the shop as it was; five clicks on plus make "$ 35"; the
	/// close writes quality 100, amount 0 and price 35.
	/// </summary>
	/// <remarks><b>Mutations:</b> the apply on the close gone; the quality written as its index; the cost of goods read
	/// from the shop instead of the window; the price's step.</remarks>
	[TestMethod]
	public void TheSettingsAreBufferedAndWrittenOnTheClose()
	{
		LoadThePark();
		var window = Open();

		var quality = (UiSlider)Find( window.Root, 0xc06b );
		var amount = (UiSlider)Find( window.Root, 0xc086 );

		quality.SetValue( 2 );
		quality.Moved!();
		amount.SetValue( 0 );
		amount.Moved!();

		for ( var i = 0; i < 5; ++i )
			ClickPlus( window );

		Assert.AreEqual( "$ 30", Find( window.Root, 0xc076 ).Text, "ice at quality 100 and amount 0: 20 x 1.5" );
		Assert.AreEqual( "$ 35", Find( window.Root, 0xc085 ).Text );
		StringAssert.Contains( window.Census(), "quality 2 (icon 2)" );
		Assert.AreEqual( (50, 50, 30), (Shop.QualityOfGoods, Shop.AmountOfSpecialIngredient, Shop.PricePerUse), "buffered" );

		_stack.Close( window );

		Assert.AreEqual( (100, 0, 35), (Shop.QualityOfGoods, Shop.AmountOfSpecialIngredient, Shop.PricePerUse), "written on the close" );

		window = Open();
		StringAssert.Contains( window.Census(), "quality 2 (icon 2) amount 0" );
		Assert.AreEqual( "$ 35", Find( window.Root, 0xc085 ).Text );
	}

	/// <summary>The spinner's plus, its op 0xe child, clicked by its place: it has no id of its own.</summary>
	private void ClickPlus( ParkShopWindow window )
	{
		var plus = Find( window.Root, 0xc085 ).Children[1];
		Assert.IsTrue( _stack.ClickAt( (plus.Rect.Left + plus.Rect.Right) / 2, (plus.Rect.Top + plus.Rect.Bottom) / 2 ), "plus clicked" );
	}

	/// <summary>
	/// <b>The door shuts and opens the shop</b> (<c>FUN_0048ccf0</c>), the preview's box lettered UITEXT 365 while it is
	/// shut, as in the original (<c>q31/orig/c-3-door.png</c>).
	/// </summary>
	/// <remarks><b>Mutations:</b> the door's state inverted; the status box not refreshed.</remarks>
	[TestMethod]
	public void TheDoorShutsAndOpensTheShop()
	{
		LoadThePark();
		var window = Open();

		Click( window.Root, 0xc082 );
		Assert.AreEqual( 0, Shop.CanLoad, "shut" );
		Assert.IsTrue( Find( window.Root, 0xc06d ).Visible );
		Assert.AreEqual( Localization.Text( 365 ), Find( window.Root, 0xc06d ).Text );

		Click( window.Root, 0xc082 );
		Assert.AreNotEqual( 0, Shop.CanLoad, "open again" );
		Assert.IsFalse( Find( window.Root, 0xc06d ).Visible );
	}

	/// <summary><b>b_allthings opens the all-items list on its shops tab with this shop's row picked</b> (<c>FUN_00495aa0( 1, shop )</c>).</summary>
	[TestMethod]
	public void TheListButtonOpensTheShopsTabOnThisShop()
	{
		LoadThePark();

		// A Balloon Shop, whose row sorts before the Drinks Shop's, so the pick is not the first row's.
		_state.AddObject( new ParkWorld.CatalogueObject( 60, 1209, 30 << 8, 30 << 8, 0 ) );

		var window = Open();

		Click( window.Root, 0xc067 );

		var list = _stack.Windows.OfType<ParkItemsScreen>().SingleOrDefault();
		Assert.IsNotNull( list );
		Assert.AreEqual( (1, DrinksShop, (ParkShopWindow?)null), (list!.ShownTab, list.Picked, Window) );
	}

	/// <summary><c>FUN_004e2200</c>: <c>SpecialIngredient</c> 1 to 4 are rows 0 to 3 of <c>INGREDIENT.str</c>, any other none.</summary>
	[TestMethod]
	public void TheIngredientsAreTheirTablesRows()
	{
		FileSystem = GameData.Required();

		CollectionAssert.AreEqual( new[] { "", "Amount of fat", "Amount of salt", "Amount of ice", "Amount of sugar", "" },
			Enumerable.Range( 0, 6 ).Select( ParkShopWindow.IngredientName ).ToArray() );
	}

	/// <summary>
	/// <b>The local happiness</b> (<c>FUN_004cd880( cell, 1 )</c>): the guests on the nine cells round the shop's, each
	/// one's happiness truncated to its low byte, averaged; a guest two cells off and anyone not a guest are not counted;
	/// at x 0 the cell west of the shop's is the row above's last, as the packed id wraps.
	/// </summary>
	/// <remarks><b>Mutations:</b> a radius of 2; the low byte dropped; a member counted; the wrap clamped.</remarks>
	[TestMethod]
	public void TheLocalHappinessIsTheNineCellsGuests()
	{
		var state = new ParkState( parkIsClosed: false, visitorsToDate: 0 );
		var happiness = new Dictionary<int, float> { [1] = 80.7f, [2] = 61f, [3] = 300f, [4] = 99f, [6] = 20f };

		state.StandOn( 1, 10, 10 );
		state.StandOn( 2, 11, 11 );
		state.StandOn( 3, 9, 11 );
		state.StandOn( 4, 12, 10 );
		state.StandOn( 5, 10, 9 );

		float? Of( int thing ) => happiness.TryGetValue( thing, out var h ) ? h : null;

		Assert.AreEqual( (80 + 61 + 44) / 3, ParkShopWindow.LocalHappiness( state, 10, 10, Of ), "300 is 44 in a byte; thing 4 two off, 5 no guest" );
		Assert.AreEqual( 0, ParkShopWindow.LocalHappiness( state, 30, 30, Of ), "nobody" );

		state.StandOn( 6, 127, 4 );
		Assert.AreEqual( 20, ParkShopWindow.LocalHappiness( state, 0, 5, Of ), "(-1, 5) is (127, 4)" );
	}

	/// <summary>
	/// <b>Apply to all reaches the shops of the shown one's item</b> (<c>0x00482040</c> on mask <c>0x200</c>, then
	/// <c>0x004b0ae0</c>'s item test), not every shop; and the apply's setters write the quality index times 50.
	/// </summary>
	[TestMethod]
	public void ApplyToAllReachesTheSameItemsShops()
	{
		var catalogue = new ParkItemCatalogue( "jungle", GameData.Required() );
		var unplaced = ParkWorld.CatalogueObject.Unplaced;

		ParkWorld.CatalogueObject[] objects =
		[
			new( DrinksShop, 1203, 43 << 8, 31 << 8, 0 ),
			new( 40, 1203, 50 << 8, 20 << 8, 0 ),
			new( 41, 1206, 52 << 8, 20 << 8, 0 ),
			new( BellyBounce, 1100, 52 << 8, 24 << 8, 0 ),
			new( 42, 1203, unplaced, unplaced, 0 ),
		];

		CollectionAssert.AreEqual( new[] { DrinksShop, 40 }, ParkShopWindow.ShopsOfItem( objects, catalogue, 1203 ) );

		var applied = ParkShopWindow.Applied( objects[0], quality: 1, amount: 7, price: 45 );
		Assert.AreEqual( (50, 7, 45), (applied.QualityOfGoods, applied.AmountOfSpecialIngredient, applied.PricePerUse) );
	}

	private ParkWorld.CatalogueObject? Of( int thingId ) => _state.TryObject( thingId, out var placed ) ? placed : null;

	/// <summary>The window's quality slider moved to the right, as a drag would: the buffered quality 2.</summary>
	private static void QualityToTheRight( ParkShopWindow window )
	{
		var quality = (UiSlider)Find( window.Root, 0xc06b );
		quality.SetValue( 2 );
		quality.Moved!();
	}

	/// <summary>
	/// <b>The used-thing order</b> (<see cref="ParkState.ObjectsInUsedOrder"/>, <c>FUN_00516270</c>'s head insert): what is
	/// bought comes newest first, then the file's objects in the order the file lists them, which is not thing-id order;
	/// a sold file object leaves the rest where they were.
	/// </summary>
	/// <remarks><b>Mutations:</b> the bought ones oldest first; a sold file object not counted off the file's part.</remarks>
	[TestMethod]
	public void TheUsedThingOrderIsTheBoughtNewestFirstThenTheFiles()
	{
		LoadThePark();
		var file = _state.Objects.Select( placed => placed.ThingId ).ToList();
		CollectionAssert.AreNotEqual( file.OrderByDescending( id => id ).ToList(), file, "the control: the file's list is not in id order" );

		_state.AddObject( new ParkWorld.CatalogueObject( 60, 1203, 32 << 8, 30 << 8, 0 ) );
		_state.AddObject( new ParkWorld.CatalogueObject( 61, 1209, 30 << 8, 30 << 8, 0 ) );
		Assert.IsTrue( _state.RemoveObject( file[0] ) );

		CollectionAssert.AreEqual( new[] { 61, 60 }.Concat( file.Skip( 1 ) ).ToArray(),
			_state.ObjectsInUsedOrder().Select( placed => placed.ThingId ).ToArray() );
	}

	/// <summary>
	/// <b>The arrows walk the used-thing list, applying first</b> (<c>FUN_0048cbe0</c>, <c>FUN_0048caf0</c> on mask
	/// <c>0x200</c>, <c>FUN_004828f0( 1, current )</c>): a Balloon Shop bought, then a Drinks Shop, head the list newest
	/// first, so it reads 60, 61, then the file's 16. From 16, <c>0xc069</c> steps towards the newest, to 61 (thing-id
	/// order would give 60), with 16's moved quality written first; <c>0xc06a</c> steps back to 16, and again round from the
	/// tail to the head, 60.
	/// </summary>
	/// <remarks><b>Mutations:</b> the list in thing-id order; the arrows' directions swapped; the apply before the step gone.</remarks>
	[TestMethod]
	public void TheArrowsWalkTheUsedThingListApplyingFirst()
	{
		LoadThePark();
		_state.AddObject( new ParkWorld.CatalogueObject( 61, 1209, 30 << 8, 30 << 8, 0 ) );
		_state.AddObject( new ParkWorld.CatalogueObject( 60, 1203, 32 << 8, 30 << 8, 0 ) );

		var window = Open();
		Assert.IsTrue( Find( window.Root, 0xc069 ).Visible && Find( window.Root, 0xc06a ).Visible, "three shops: the arrows" );
		QualityToTheRight( window );

		Click( window.Root, 0xc069 );
		Assert.AreEqual( (61, 100), (window.ThingId, Shop.QualityOfGoods), "towards the newest, 16's quality applied first" );

		Click( window.Root, 0xc06a );
		Assert.AreEqual( DrinksShop, window.ThingId, "towards the oldest" );

		Click( window.Root, 0xc06a );
		Assert.AreEqual( 60, window.ThingId, "round from the tail to the head" );
	}

	/// <summary>
	/// <b>Apply to all, pressed, writes every shop of the shown one's item</b> (<c>0xc081</c>, <c>FUN_00481bc0( 0x200,
	/// 0x004b0ae0 )</c>): a second Drinks Shop takes the shown one's quality 100 and price 35 with it; a Balloon Shop does
	/// not move.
	/// </summary>
	/// <remarks><b>Mutations:</b> apply to all reduced to the shown shop.</remarks>
	[TestMethod]
	public void ApplyToAllWritesEveryShopOfTheItem()
	{
		LoadThePark();
		_state.AddObject( new ParkWorld.CatalogueObject( 60, 1203, 32 << 8, 30 << 8, 0 ) );
		_state.AddObject( new ParkWorld.CatalogueObject( 61, 1209, 30 << 8, 30 << 8, 0 ) );
		var balloon = Of( 61 );

		var window = Open();
		QualityToTheRight( window );

		for ( var i = 0; i < 5; ++i )
			ClickPlus( window );

		Click( window.Root, 0xc081 );

		Assert.AreEqual( (100, 35), (Shop.QualityOfGoods, Shop.PricePerUse), "the shop shown" );
		Assert.AreEqual( (100, 35), (Of( 60 )!.Value.QualityOfGoods, Of( 60 )!.Value.PricePerUse), "the other Drinks Shop" );
		Assert.AreEqual( balloon, Of( 61 ), "the Balloon Shop untouched" );
		Assert.IsNotNull( Window, "the window stays" );
	}

	/// <summary><b>Move takes the shop into the hand</b> (<c>0xc080</c>, <c>FUN_0048cfa0</c>): sold, item 1203 held, the window closed.</summary>
	/// <remarks><b>Mutations:</b> nothing picked up; the window left up.</remarks>
	[TestMethod]
	public void MoveTakesTheShopIntoTheHand()
	{
		LoadThePark();
		_objects = new ParkObjects( "jungle", null, null );
		var window = Open();

		Click( window.Root, 0xc080 );

		Assert.AreEqual( (null, (ParkWorld.CatalogueObject?)null, 1203), (Window, Of( DrinksShop ), ParkBuilding.Carrying) );
	}

	/// <summary>
	/// <b>Delete asks first where the Confirmations option is on</b> (<c>0xc068</c>, <c>FUN_0048cd10</c>,
	/// <c>[0x0078d90f]</c>): the box on UITEXT 396 over the window closed at once, the shop still standing and its moved
	/// quality NOT written (<c>[0x007c2658]</c> zeroed before the close); the cross keeps it as it was; the tick sells it.
	/// </summary>
	/// <remarks><b>Mutations:</b> no box; the box's text; the settings applied on the close; the cross selling; the tick
	/// selling nothing.</remarks>
	[TestMethod]
	public void DeleteAsksFirstWhereConfirmationsAreOn()
	{
		LoadThePark();
		GameOptions.Current.Confirmations = true;
		_objects = new ParkObjects( "jungle", null, null );
		var before = Shop;

		var window = Open();
		QualityToTheRight( window );
		Click( window.Root, 0xc068 );

		var box = _stack.Windows.OfType<MessageBox>().Single();
		Assert.IsNull( Window, "the window closed behind the box" );
		Assert.AreEqual( Localization.Get( UIStrings.DeleteItem ), Find( box.Root, 0x9873c7 ).Text );
		StringAssert.StartsWith( Find( box.Root, 0x9873c7 ).Text, "DELETE ITEM" );
		Assert.AreEqual( before, Of( DrinksShop ), "asked, not yet sold, and the quality not applied" );

		Click( box.Root, 0x9873c8 );
		Assert.AreEqual( (0, before), (_stack.Windows.OfType<MessageBox>().Count(), Of( DrinksShop )), "the cross keeps it" );

		Click( Open().Root, 0xc068 );
		Click( _stack.Windows.OfType<MessageBox>().Single().Root, 0x9873c9 );
		Assert.IsNull( Of( DrinksShop ), "the tick sells it" );
	}

	/// <summary><b>With the Confirmations option off Delete sells at once</b>, and the window closes; no box.</summary>
	/// <remarks><b>Mutations:</b> the option not read; the window left up.</remarks>
	[TestMethod]
	public void DeleteSellsAtOnceWhereConfirmationsAreOff()
	{
		LoadThePark();
		GameOptions.Current.Confirmations = false;
		_objects = new ParkObjects( "jungle", null, null );

		var window = Open();
		QualityToTheRight( window );
		Click( window.Root, 0xc068 );

		Assert.AreEqual( (0, (ParkShopWindow?)null, (ParkWorld.CatalogueObject?)null),
			(_stack.Windows.OfType<MessageBox>().Count(), Window, Of( DrinksShop )) );
	}

	private ParkObjectWindow? RideWindow => _stack.Windows.OfType<ParkObjectWindow>().FirstOrDefault();

	/// <summary>The ride window open on the Belly Bounce with its first movable slider moved: the ride as it stood before.</summary>
	private ParkWorld.CatalogueObject OpenTheRideAndMoveASlider()
	{
		var before = Of( BellyBounce )!.Value;
		_park.OpenObjectWindow( BellyBounce );
		Assert.IsNotNull( RideWindow );

		var slider = new[] { 0x3e30, 0x3e2d, 0x3e2f }.Select( id => (UiSlider)Find( RideWindow!.Root, id ) )
			.First( each => each.Visible && each.Maximum > each.Minimum );
		slider.SetValue( slider.Value == slider.Minimum ? slider.Maximum : slider.Minimum );
		slider.Moved!();

		return before;
	}

	/// <summary>
	/// <b>The ride window's delete is the same</b> (<c>0x3e2b</c>, <c>FUN_0048cd10</c>): with Confirmations on, the box on
	/// UITEXT 396, the window closed with its moved slider not committed, the cross keeping the ride, the tick selling it;
	/// with it off, sold at once with no box.
	/// </summary>
	/// <remarks><b>Mutations:</b> no box; the slider committed on the close; the option not read.</remarks>
	[TestMethod]
	public void TheRideWindowsDeleteAsksFirstToo()
	{
		LoadThePark();
		GameOptions.Current.Confirmations = true;
		_objects = new ParkObjects( "jungle", null, null );

		var before = OpenTheRideAndMoveASlider();
		Click( RideWindow!.Root, 0x3e2b );

		var box = _stack.Windows.OfType<MessageBox>().Single();
		Assert.AreEqual( Localization.Get( UIStrings.DeleteItem ), Find( box.Root, 0x9873c7 ).Text );
		Assert.AreEqual( ((ParkObjectWindow?)null, before), (RideWindow, Of( BellyBounce )), "closed, the slider not committed" );

		Click( box.Root, 0x9873c8 );
		Assert.AreEqual( before, Of( BellyBounce ), "the cross keeps it" );

		OpenTheRideAndMoveASlider();
		Click( RideWindow!.Root, 0x3e2b );
		Click( _stack.Windows.OfType<MessageBox>().Single().Root, 0x9873c9 );
		Assert.IsNull( Of( BellyBounce ), "the tick sells it" );
	}

	/// <summary><b>With the Confirmations option off the ride window's delete sells at once</b>; no box.</summary>
	[TestMethod]
	public void TheRideWindowsDeleteSellsAtOnceWhereConfirmationsAreOff()
	{
		LoadThePark();
		GameOptions.Current.Confirmations = false;
		_objects = new ParkObjects( "jungle", null, null );

		OpenTheRideAndMoveASlider();
		Click( RideWindow!.Root, 0x3e2b );

		Assert.AreEqual( (0, (ParkObjectWindow?)null, (ParkWorld.CatalogueObject?)null),
			(_stack.Windows.OfType<MessageBox>().Count(), RideWindow, Of( BellyBounce )) );
	}

	/// <summary>
	/// <b>The ride window's arrows walk the used-thing list too</b> (mask <c>0x80</c>, <c>FUN_004828f0( 0, current )</c>),
	/// skipping an add-on (<c>+0x284</c> not nought): Belly Bounces bought as 61, then the Lava Jump add-on 62, then 60, so
	/// the list's head reads 60, 62, 61. From 60 b_ardown (<c>0x3e27</c>) steps towards the oldest past the add-on to 61
	/// (thing-id order would give a file ride below 60); b_arup (<c>0x3e26</c>) steps back to 60.
	/// </summary>
	/// <remarks><b>Mutations:</b> the list in thing-id order; the arrows swapped; the add-on not skipped.</remarks>
	[TestMethod]
	public void TheRideWindowsArrowsWalkTheUsedThingList()
	{
		LoadThePark();
		Assert.IsTrue( _park.Catalogue!.TryGet( 1500, out var lavaJump ) );
		Assert.AreEqual( (0, 1150), (lavaJump.UiType, lavaJump.AddOnUpgradesId), "a ride's add-on, UI type 0" );

		_state.AddObject( new ParkWorld.CatalogueObject( 61, 1100, 30 << 8, 30 << 8, 0 ) );
		_state.AddObject( new ParkWorld.CatalogueObject( 62, 1500, 36 << 8, 30 << 8, 0 ) );
		_state.AddObject( new ParkWorld.CatalogueObject( 60, 1100, 33 << 8, 30 << 8, 0 ) );

		_park.OpenObjectWindow( 60 );
		var window = RideWindow!;

		Click( window.Root, 0x3e27 );
		Assert.AreEqual( 61, window.ThingId, "towards the oldest, past the add-on" );

		Click( window.Root, 0x3e26 );
		Assert.AreEqual( 60, window.ThingId, "towards the newest" );
	}

	private static UiControl Find( UiControl root, int id )
		=> FindOrNull( root, id ) ?? throw new AssertFailedException( $"no control {id:x}" );

	private static UiControl? FindOrNull( UiControl root, int id )
		=> root.Id == id ? root : root.Children.Select( child => FindOrNull( child, id ) ).FirstOrDefault( found => found != null );
}
