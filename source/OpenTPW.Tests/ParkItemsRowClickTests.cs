using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenTPW.UI;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace OpenTPW.Tests;

/// <summary>
/// <b>A row's left click in the all-items list opens its thing's window</b> (Q1041): the row's <c>0x400</c>
/// (<c>FUN_00495360</c>, <c>0x00495495</c>) hands the row's thing to <c>Thing_OpenWindow</c> (<c>0x004954ca</c>), whose
/// ride-class arms are the add-on's other window (<c>+0x288</c> is 2), the ride its <c>mUpgradeParent</c> (<c>+0x30</c>) names,
/// and nothing for an item fitting a ride that names none (<c>docs/exe/park-engine.md</c>, "An add-on has no window and no sale
/// of its own"). Measured in the original under Proton on the New Save: a ride's row, a shop's, a sideshow's and a
/// miscellaneous row each opened its own window, and the Jump's row and each Tunnel row opened the DinoKarts', the list gone
/// every time. Skipped without the game (<see cref="GameData"/>).
/// </summary>
[TestClass]
public class ParkItemsRowClickTests
{
	private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

	private Level? _level;
	private WindowStack _stack = null!;
	private ParkState _state = null!;

	[TestInitialize]
	public void LoadThePark()
	{
		_level = Level.Current;

		Screen.Size = new Point2( 2048, 1536 );
		Log ??= new();
		FileSystem = GameData.Required();
		Input.Mouse = new();
		Unimplemented.Forget();

		Stand();
	}

	[TestCleanup]
	public void PutItAllBack()
	{
		Level.Current = _level!;
		Unimplemented.Forget();
	}

	/// <summary>Lost Kingdom's shipped park standing as the running level, a window stack on it.</summary>
	private void Stand()
	{
		using var stream = new MemoryStream( FileSystem.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		var world = new ParkWorld( new SaveReader( stream ).ReadFile() );
		var catalogue = new ParkItemCatalogue( "jungle", FileSystem );

		_state = new ParkState( world );

		var level = (Level)RuntimeHelpers.GetUninitializedObject( typeof( Level ) );
		typeof( Level ).GetProperty( nameof( Level.Park ) )!.SetValue( level, world );
		typeof( Level ).GetProperty( nameof( Level.ParkState ) )!.SetValue( level, _state );
		typeof( Level ).GetProperty( nameof( Level.Catalogue ) )!.SetValue( level, catalogue );
		typeof( Level ).GetProperty( nameof( Level.Kind ) )!.SetValue( level, Level.Scene.Park );
		Level.Current = level;

		_stack = new WindowStack();
		typeof( Level ).GetField( "_windows", Private )!.SetValue( level, _stack );
	}

	/// <summary>An item standing as a new thing, with the ride it fits in <c>mUpgradeParent</c>.</summary>
	private int StandAdd( int item, int x, int y, int parent )
	{
		Assert.IsTrue( Level.Current!.Catalogue!.TryGet( item, out var found ), $"item {item}" );

		var thing = ParkBuilding.Constructed( found, _state.NextThingId(), x, y, 0, 0, 0, ParkWorld.BuiltWhen.At( _state.CalendarNow ) )
			with { UpgradeParent = (ushort)parent };

		_state.AddObject( thing );

		return thing.ThingId;
	}

	/// <summary>The list screen opened on a tab with a thing's row picked, then the row's activation as a click sends it.</summary>
	private ParkItemsScreen ClickRowOf( int tab, int thing )
	{
		var screen = new ParkItemsScreen( _stack, tab, thing );
		_stack.Open( screen );

		var lists = (Dictionary<int, UiList>)typeof( ParkItemsScreen ).GetField( "_lists", Private )!.GetValue( screen )!;
		var shown = lists.Values.Single( list => list.Visible );

		Assert.AreEqual( thing, shown.Selected, $"the row of thing {thing} is the one picked" );
		Assert.IsTrue( shown.Rows.Any( row => row.Id == thing ), $"tab {tab} lists thing {thing}" );

		// The press and the release of a click activate the selected row; a double click does the same (FUN_006656fa).
		shown.LeftDoubleClicked();

		return screen;
	}

	private int Times( string what ) => Unimplemented.Summary.FirstOrDefault( entry => entry.What == what ).Times;

	/// <summary>
	/// <b>A ride's row and a shop's row open their own windows and the list closes</b>, as the original's did on the New
	/// Save: the DinoKarts', the Costume Shop's.
	/// </summary>
	/// <remarks><b>Mutations:</b> the row's activation left unanswered.</remarks>
	[TestMethod]
	public void ARidesRowAndAShopsRowOpenTheirOwnWindowsAndTheListCloses()
	{
		// The Belly Bounce (13) and the Drinks Shop (16), the stock park's.
		var rides = ClickRowOf( 0, 13 );
		Assert.AreEqual( 13, _stack.Windows.OfType<ParkObjectWindow>().Single().ThingId );
		Assert.IsFalse( _stack.Windows.Contains( rides ), "the list is gone" );
		Assert.AreEqual( 0, Times( "LIST_ROW_ACTIVATED_UNANSWERED" ) );

		var shops = ClickRowOf( 1, 16 );
		Assert.AreEqual( 16, _stack.Windows.OfType<ParkShopWindow>().Single().ThingId );
		Assert.IsFalse( _stack.Windows.Contains( shops ), "the list is gone" );
	}

	/// <summary>
	/// <b>An add-on's row opens its ride's window</b> (the <c>+0x30</c> arm, <c>0x00486983</c>): the Jump's row on the Belly
	/// Bounce shows thing 13, not an add-on window, and the window it opens is the named thing's whatever that is (the
	/// original's <c>FUN_004af980</c> is called on it, the dispatcher is not run again): a Tunnel naming the Jump opens the
	/// Jump's own ride window.
	/// </summary>
	/// <remarks><b>Mutations:</b> the parent arm removed; the parent dispatched again; the nothing-opens arm tested before it.</remarks>
	[TestMethod]
	public void AnAddOnsRowOpensItsRidesWindowAndNotItsOwn()
	{
		var jump = StandAdd( 1500, 17, 21, 13 );
		var tunnel = StandAdd( 1501, 23, 21, jump );

		ClickRowOf( 0, jump );
		Assert.AreEqual( 13, _stack.Windows.OfType<ParkObjectWindow>().Single().ThingId, "the Jump's row shows its ride" );

		// One ride window, retargeted: the second click shows the thing the Tunnel names, the Jump, with no second dispatch.
		var screen = ClickRowOf( 0, tunnel );
		Assert.AreEqual( jump, _stack.Windows.OfType<ParkObjectWindow>().Single().ThingId, "the named thing's window, whatever its kind" );
		Assert.IsFalse( _stack.Windows.Contains( screen ) );
	}

	/// <summary>
	/// <b>An item that fits a ride and names none opens nothing, and the list stays</b> (<c>+0x30</c> nought, <c>+0x284</c> not
	/// nought, <c>0x004869d4</c>), where an item that fits no ride opens its own: the Tunnel with no ride against the Belly
	/// Bounce.
	/// </summary>
	/// <remarks><b>Mutations:</b> the nothing arm removed (the add-on's own ride window opens).</remarks>
	[TestMethod]
	public void AnAddOnNamingNoRideOpensNothingAndTheListStays()
	{
		var tunnel = StandAdd( 1501, 23, 21, 0 );

		var screen = ClickRowOf( 0, tunnel );
		Assert.AreEqual( 0, _stack.Windows.OfType<ParkObjectWindow>().Count(), "no window" );
		Assert.IsTrue( _stack.Windows.Contains( screen ), "the list is still there" );
		Assert.AreEqual( 0, Times( "LIST_ROW_ACTIVATED_UNANSWERED" ) );
	}

	/// <summary>
	/// <b>An add-on of upgrade type 2 opens another window, before its ride is looked at</b> (<c>0x00486945</c>,
	/// <c>FUN_004b6ae0</c>): unbuilt, counted, and no ride window opens even when the thing names a ride.
	/// </summary>
	/// <remarks><b>Mutations:</b> the type 2 arm removed; the type tested after the parent.</remarks>
	[TestMethod]
	public void AnUpgradeTypeTwoAddOnsRowCountsItsOtherWindowAndOpensNoRideWindow()
	{
		// Lost Kingdom ships none (space's zob2, 3502, is the one): the Tunnel's catalogue entry made one.
		var items = (Dictionary<int, ParkItemCatalogue.Item>)typeof( ParkItemCatalogue ).GetField( "_items", Private )!.GetValue( Level.Current!.Catalogue! )!;
		items[1501] = items[1501] with { AddOnUpgradeType = 2 };

		var ride = 13;
		var thing = StandAdd( 1501, 23, 21, ride );

		var screen = ClickRowOf( 0, thing );
		Assert.AreEqual( 1, Times( "ADD_ON_UPGRADE_TYPE_2_WINDOW" ) );
		Assert.AreEqual( 0, _stack.Windows.OfType<ParkObjectWindow>().Count(), "its ride's window does not open" );
		Assert.IsTrue( _stack.Windows.Contains( screen ) );
	}
}
