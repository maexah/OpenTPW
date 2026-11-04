using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeoVeldrid;
using OpenTPW.UI;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace OpenTPW.Tests;

/// <summary>
/// <b>An add-on that fits a ride has no window</b> (Q1000; <c>docs/exe/park-engine.md</c>, "An add-on has no window and no sale of its
/// own"): the hover gives its cell no category (<c>FUN_00486d90</c>, <c>0x0048768d</c>), the click's switch on the category
/// (<c>FUN_004879d0</c>) has no arm for none, and so a click on the Dino Karts Tunnel's body cell opens nothing, where the
/// ride and a feature open theirs. Measured in the original under Proton on the New Save: the Tunnel's cells (11,16) and
/// (11,17) read category 0 for two seconds and a click there left the hand and the screen as they were.
/// Skipped without the game (<see cref="GameData"/>).
/// </summary>
[TestClass]
public class ParkAddOnClickTests
{
	private Level? _level;
	private WindowStack _stack = null!;

	[TestInitialize]
	public void LoadThePark()
	{
		_level = Level.Current;

		Screen.Size = new Point2( 2048, 1536 );
		Log ??= new();
		FileSystem = GameData.Required();
		Input.Mouse = new();
		Unimplemented.Forget();

		using var stream = new MemoryStream( FileSystem.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		var world = new ParkWorld( new SaveReader( stream ).ReadFile() );
		var catalogue = new ParkItemCatalogue( "jungle", FileSystem );
		var state = new ParkState( world );

		var level = (Level)RuntimeHelpers.GetUninitializedObject( typeof( Level ) );
		typeof( Level ).GetProperty( nameof( Level.Park ) )!.SetValue( level, world );
		typeof( Level ).GetProperty( nameof( Level.ParkState ) )!.SetValue( level, state );
		typeof( Level ).GetProperty( nameof( Level.Catalogue ) )!.SetValue( level, catalogue );
		typeof( Level ).GetProperty( nameof( Level.Kind ) )!.SetValue( level, Level.Scene.Park );
		Level.Current = level;

		var balance = new ParkBalance( "jungle", easyMode: true );
		typeof( Level ).GetProperty( nameof( Level.Balance ) )!.SetValue( level, balance );
		_stack = new WindowStack();
		typeof( Level ).GetField( "_windows", BindingFlags.NonPublic | BindingFlags.Instance )!.SetValue( level, _stack );
	}

	[TestCleanup]
	public void PutItAllBack()
	{
		Level.Current = _level!;
	}

	/// <summary>An item put down at (x, y) turned 0 with its cells marked as a purchase marks them, and the first of the cells
	/// it covers, which a click on lands on.</summary>
	private (int Thing, int CellX, int CellY) PutDown( int item, int x, int y )
	{
		var state = Level.Current!.ParkState!;
		Assert.IsTrue( Level.Current.Catalogue!.TryGet( item, out var found ) );

		var thing = ParkBuilding.Constructed( found, state.NextThingId(), x, y, 0, 0, 0, ParkWorld.BuiltWhen.At( state.CalendarNow ) );
		state.AddObject( thing );

		if ( ParkObjects.IsTrackAddOn( found ) )
			ParkTrackAddOn.Stamp( state, found, x, y, 0 );
		else
			ParkBuilding.Stamp( state, ParkObjects.CoveredCells( found, x, y, 0 ), x, y );

		var (cellX, cellY) = ParkObjects.CoveredCells( found, x, y, 0 ).First();
		Assert.AreEqual( CellEdge.Footprint, state.Record( cellX, cellY ).Type, "the click lands on a marked cell" );

		return (thing.ThingId, cellX, cellY);
	}

	[TestMethod]
	public void AClickOnAnAddOnOpensNothingWhereARideOpensItsWindow()
	{
		// The Dino Karts Tunnel (1501), Jump (1500) and the Splish Splash Tunnel (1502): items fitting a ride, all of type 1.
		foreach ( var addOn in new[] { 1500, 1501, 1502 } )
		{
			var (thing, x, y) = PutDown( addOn, 17, 21 );

			StringAssert.Contains( Level.Current!.ClickWorldAt( x, y, 0, thing ), $"nothing to do at ({x},{y})" );
			Assert.AreEqual( 0, _stack.Windows.OfType<ParkObjectWindow>().Count(), $"item {addOn}: no window" );
			Level.Current.ParkState!.RemoveObject( thing );
		}

		// Ctrl held: the add-on's guard comes before Ctrl's buy-another, so it still does nothing and the hand stays empty.
		var before = Input.Keyboard;

		try
		{
			Level.Current!.ParkState!.SetBalance( 88000 );
			Input.Keyboard = new Input.KeyboardInfo( new List<Key> { Key.ControlLeft } );
			var (ctrlThing, ctrlX, ctrlY) = PutDown( 1501, 17, 21 );

			StringAssert.Contains( Level.Current!.ClickWorldAt( ctrlX, ctrlY, 0, ctrlThing ), $"nothing to do at ({ctrlX},{ctrlY})" );
			Assert.AreEqual( 0, _stack.Windows.OfType<ParkObjectWindow>().Count(), "Ctrl: no window" );
			Assert.AreEqual( 0, ParkBuilding.Carrying, "Ctrl: nothing in the hand" );
			Level.Current.ParkState!.RemoveObject( ctrlThing );
		}
		finally
		{
			Input.Keyboard = before;
		}

		// The Belly Bounce (13), which fits no ride, opens its window (a feature's is unbuilt, FEATURE_WINDOW).
		StringAssert.Contains( Level.Current!.ClickWorldAt( 51, 23, 0, 13 ), "opened the window for thing 13" );
		Assert.AreEqual( 13, _stack.Windows.OfType<ParkObjectWindow>().Single().ThingId );
		_stack.Close( _stack.Windows.OfType<ParkObjectWindow>().Single() );
	}

	[TestMethod]
	public void OnlyAnAddOnOfAnotherRidesKindOpensAWindow()
	{
		var catalogue = Level.Current!.Catalogue!;

		foreach ( var addOn in new[] { 1500, 1501, 1502 } )
		{
			Assert.IsTrue( catalogue.TryGet( addOn, out var item ) );
			Assert.AreEqual( (true, 1), (ParkObjects.OpensNoWindow( item ), item.AddOnUpgradeType), $"item {addOn}" );
		}

		foreach ( var other in new[] { 1150, 1160, 1422 } )
		{
			Assert.IsTrue( catalogue.TryGet( other, out var item ) );
			Assert.IsFalse( ParkObjects.OpensNoWindow( item ), $"item {other} opens its window" );
		}
	}
}
