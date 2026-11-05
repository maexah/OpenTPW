using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenTPW.UI;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace OpenTPW.Tests;

/// <summary>
/// <b>An add-on's row in the all-items rides tab: its place and its figures</b> (Q1042). The rides tab adds a row for every
/// add-on after the rides' own (<c>FUN_00481bc0( 0x2000 )</c>, <c>FUN_00482100</c>) through <c>0x00493eb0</c>, which hands
/// <c>FUN_00493cd0</c> the add-on, the list and the ride its <c>+0x30</c> names: the row's name is the add-on's, its figures
/// the ride's, and it is added UNDER the ride's row (<c>FUN_0066403b</c> with the ride's row index), one level in. Measured
/// in the original on the New Save (Q1041, <c>q1041/orig/run1/c1-allitems.png</c>): DinoKarts, then Dino Karts Jump, Dino Karts
/// Tunnel and Dino Karts Tunnel indented under it, each with DinoKarts' 12 users. Skipped without the game
/// (<see cref="GameData"/>).
/// </summary>
[TestClass]
public class ParkItemsAddOnRowsTests
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

	[TestCleanup]
	public void PutItAllBack()
	{
		Time.PinWall( 0 );
		Level.Current = _level!;
		Unimplemented.Forget();
	}

	private int StandAdd( int item, int x, int y, int parent )
	{
		Assert.IsTrue( Level.Current!.Catalogue!.TryGet( item, out var found ), $"item {item}" );

		var thing = ParkBuilding.Constructed( found, _state.NextThingId(), x, y, 0, 0, 0, ParkWorld.BuiltWhen.At( _state.CalendarNow ) )
			with { UpgradeParent = (ushort)parent };

		_state.AddObject( thing );

		return thing.ThingId;
	}

	private UiList Rides( out ParkItemsScreen screen )
	{
		screen = new ParkItemsScreen( _stack, 0 );

		var lists = (Dictionary<int, UiList>)typeof( ParkItemsScreen ).GetField( "_lists", Private )!.GetValue( screen )!;

		return lists.Values.Single( list => list.Visible );
	}

	/// <summary>
	/// <b>The add-ons stand under their ride, one level in, in the child chain's name order</b>, never among the head rows: the
	/// Tunnel put down first and the Jump second still list Jump, Tunnel, under the Belly Bounce, after it and before the next
	/// ride (the original's DinoKarts, Dino Karts Jump, Dino Karts Tunnel).
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> the add-ons listed as head rows in the sort; the add-on walk dropped (no row); the child added with no
	/// depth; the child chain left in the order the things were walked.
	/// </remarks>
	[TestMethod]
	public void AnAddOnsRowStandsUnderItsRideInTheChildChainsOrder()
	{
		var tunnel = StandAdd( 1501, 23, 21, 13 );
		var jump = StandAdd( 1500, 17, 21, 13 );

		var rows = Rides( out _ ).Rows;
		var ride = rows.ToList().FindIndex( row => row.Id == 13 );

		Assert.IsTrue( ride >= 0, "the Belly Bounce's row" );
		Assert.AreEqual( 0, rows[ride].Depth );
		Assert.AreEqual( jump, rows[ride + 1].Id, "the Jump first, whichever was put down first" );
		Assert.AreEqual( tunnel, rows[ride + 2].Id );
		Assert.AreEqual( 1, rows[ride + 1].Depth );
		Assert.AreEqual( 1, rows[ride + 2].Depth );
		Assert.IsTrue( ride + 3 >= rows.Count || rows[ride + 3].Depth == 0, "the next row is a ride again" );
		Assert.AreEqual( 0, rows.Count( row => row.Depth == 0 && (row.Id == jump || row.Id == tunnel) ), "never a head row" );
	}

	/// <summary>
	/// <b>An add-on's row carries its ride's figures</b> (<c>FUN_00493cd0</c>: everything but the name from its third argument,
	/// the same swap in the rewrite <c>0x004955e0</c>): the Jump's cells and numbers are the Belly Bounce's, not its own.
	/// </summary>
	/// <remarks><b>Mutations:</b> the add-on's own figures; the ride's figures in the fill but not the rewrite.</remarks>
	[TestMethod]
	public void AnAddOnsRowHasItsRidesFigures()
	{
		var jump = StandAdd( 1500, 17, 21, 13 );

		_state.TryObject( 13, out var ride );
		_state.TryObject( jump, out var own );

		var catalogue = Level.Current!.Catalogue!;
		catalogue.TryGet( ride.CatalogueId, out var rideItem );
		catalogue.TryGet( own.CatalogueId, out var ownItem );

		var theirs = ParkItemsScreen.NumbersOf( 0, ride, rideItem, _state );
		var its = ParkItemsScreen.NumbersOf( 0, own, ownItem, _state );

		CollectionAssert.AreNotEqual( theirs, its, "the add-on's own figures differ from its ride's in this park, or the test is hollow" );

		var rows = Rides( out _ ).Rows;
		var row = rows.Single( found => found.Id == jump );

		CollectionAssert.AreEqual( theirs, row.Numbers!.ToArray(), "the ride's figures" );
		Assert.AreEqual( "Dino Karts Jump", row.Name, "its own name" );
	}

	/// <summary>
	/// <b>An add-on that names no ride is a head row of its own, with its own figures; one whose ride is no row adds nothing</b>
	/// (<c>0x00493eb0</c>: a nought <c>+0x30</c> hands <c>FUN_00493cd0</c> no third argument; <c>0x00493e4a</c> adds nothing
	/// where <c>FUN_00664d2a</c> finds no row).
	/// </summary>
	/// <remarks><b>Mutations:</b> a no-ride add-on dropped; a missing ride's add-on added as a head row.</remarks>
	[TestMethod]
	public void AnAddOnWithNoRideIsAHeadRowAndOneWhoseRideHasNoRowAddsNothing()
	{
		var lone = StandAdd( 1501, 23, 21, 0 );
		var orphan = StandAdd( 1500, 17, 21, 16 );

		var rows = Rides( out _ ).Rows;

		Assert.AreEqual( 0, rows.Single( row => row.Id == lone ).Depth );
		Assert.IsFalse( rows.Any( row => row.Id == orphan ), "the Drinks Shop (16) has no row on the rides tab" );
	}

	/// <summary>
	/// <b>An add-on whose ride has been sold stays listed, as a head row with its own figures</b> (the original's nought table slot
	/// takes <c>FUN_00493cd0</c>'s head-row arm, <c>0x00493e66</c>; ours' sale of a ride leaves its add-ons, <c>SALE_TRACK_TEARDOWN</c>).
	/// </summary>
	/// <remarks><b>Mutations:</b> the parent handed over whether or not the ride stands (the add-on dropped).</remarks>
	[TestMethod]
	public void AnAddOnWhoseRideWasSoldIsAHeadRowOfItsOwn()
	{
		var jump = StandAdd( 1500, 17, 21, 13 );

		Assert.IsTrue( _state.RemoveObject( 13 ), "the ride sold" );

		var rows = Rides( out _ ).Rows;

		Assert.IsTrue( rows.Any( found => found.Id == jump ), "the add-on is still listed" );
		Assert.AreEqual( 0, rows.Single( found => found.Id == jump ).Depth, "a head row" );
	}

	/// <summary>
	/// <b>The two-second rewrite hands an add-on's row its ride's figures too</b> (<c>0x004955e0</c> reads the row's thing, swaps
	/// to the ride its <c>+0x30</c> names at <c>0x004956bf</c> and takes every figure of that): the ride's state of repair
	/// changes, and the Jump's bar follows it.
	/// </summary>
	/// <remarks><b>Mutations:</b> the rewrite taking the add-on's own figures.</remarks>
	[TestMethod]
	public void TheRewriteGivesAnAddOnsRowItsRidesNewFigures()
	{
		Time.PinWall( 5000 );

		var jump = StandAdd( 1500, 17, 21, 13 );
		var list = Rides( out var screen );

		_state.TryObject( 13, out var ride );

		var repair = (int)ride.StateOfRepair == 40 ? 60f : 40f;

		_state.ReplaceObject( ride with { StateOfRepair = repair } );

		Time.PinWall( 7000 );
		screen.Update();

		Assert.AreEqual( ((int)repair << 10) / 100, list.Rows.Single( row => row.Id == jump ).Numbers![3], "the ride's bar" );
		Assert.AreEqual( 1, list.Rows.Single( row => row.Id == jump ).Depth, "still under it" );
	}
}
