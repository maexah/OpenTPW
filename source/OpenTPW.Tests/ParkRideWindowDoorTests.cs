using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenTPW.UI;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace OpenTPW.Tests;

/// <summary>Q92: real window callbacks, live status/guard refresh and rendered list cell colours.</summary>
[TestClass]
public class ParkRideWindowDoorTests
{
	private Level? before;
	private Point2 size;
	private ParkState state = null!;
	private ParkPeople people = null!;
	private RideScript script = null!;
	private ParkObjectWindow window = null!;
	private WindowStack stack = null!;
	private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

	[TestInitialize]
	public void OpenWindow()
	{
		before = Level.Current;
		size = Screen.Size;
		Screen.Size = new( 2048, 1536 );
		Log ??= new();
		FileSystem = GameData.Required();
		using var stream = new MemoryStream( FileSystem.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		var world = new ParkWorld( new SaveReader( stream ).ReadFile() );
		state = new( world );
		var catalogue = new ParkItemCatalogue( "jungle", FileSystem );
		using var rse = FileSystem.OpenRead( "levels/jungle/rides/bouncy/bouncy.RSE" );
		script = new( new RideScriptFile( rse ) );
		var level = (Level)RuntimeHelpers.GetUninitializedObject( typeof( Level ) );
		typeof( Level ).GetProperty( nameof( Level.Park ) )!.SetValue( level, world );
		typeof( Level ).GetProperty( nameof( Level.ParkState ) )!.SetValue( level, state );
		typeof( Level ).GetProperty( nameof( Level.Catalogue ) )!.SetValue( level, catalogue );
		Level.Current = level;
		people = new( world, new ParkBalance( "jungle", easyMode: true ), null, state, catalogue,
			id => id == 13 ? script : null );
		stack = (WindowStack)RuntimeHelpers.GetUninitializedObject( typeof( WindowStack ) );
		typeof( WindowStack ).GetField( "_windows", Private )!.SetValue( stack, new List<UiWindow>() );
		window = new( stack, 13 );
	}

	[TestCleanup]
	public void PutBack()
	{
		people?.Delete();
		Entity.ApplyDeletions();
		Level.Current = before!;
		Screen.Size = size;
	}

	private static UiControl Find( UiControl root, int id )
		=> root.Id == id ? root : root.Children.Select( c => FindOrNull( c, id ) ).First( c => c != null )!;
	private static UiControl? FindOrNull( UiControl root, int id )
		=> root.Id == id ? root : root.Children.Select( c => FindOrNull( c, id ) ).FirstOrDefault( c => c != null );
	private UiButton Door => (UiButton)Find( window.Root, 0x3e38 );
	private UiControl Status => Find( window.Root, 0x3e25 );
	private ParkWorld.CatalogueObject Ride => state.Objects.Single( x => x.ThingId == 13 );
	private void ClickDoor()
	{
		// WindowStack.Release performs the real toggle and invokes the callback, including disabled rejection.
		typeof( WindowStack ).GetField( "_pressed", Private )!.SetValue( stack, Door );
		typeof( WindowStack ).GetMethod( "Release", Private )!.Invoke( stack, [Door] );
	}

	[TestMethod]
	public void DoorClosesAndReopensTheSelectedRideAndItsScript()
	{
		Assert.IsFalse( Door.IsDown );
		Assert.IsFalse( Status.Visible );
		state.NominateForLoading( 13, people.Peeps.First().ThingId );
		ClickDoor();
		Assert.AreEqual( 0, Ride.CanLoad );
		Assert.AreEqual( 0, state.PersonBeingLoaded( 13 ) );
		Assert.AreEqual( 1, script[ParkRideOperation.ClosedVariable] );
		Assert.IsTrue( Door.IsDown && Door.Enabled );
		Assert.AreEqual( 12, Door.HelpText );
		Assert.IsTrue( Status.Visible );
		Assert.AreEqual( Localization.Text( 365 ), Status.Text );
		Assert.AreEqual( new UiColour( 128, 128, 128 ), Status.TextColour );
		Assert.IsTrue( state.Objects.Where( r => r.ThingId != 13 ).All( r => r.CanLoad == 1 ) );
		ClickDoor();
		Assert.AreEqual( 1, Ride.CanLoad );
		Assert.AreEqual( 0, script[ParkRideOperation.ClosedVariable] );
		Assert.IsFalse( Door.IsDown || Status.Visible );
		Assert.AreEqual( 13, Door.HelpText );
	}

	[TestMethod]
	public void QueueConnectionAndGuardRefreshWhileStillClosed()
	{
		ClickDoor();
		var back = state.Record( 49, 22 );
		state.SetRecord( 49, 22, back with { Neighbours = 0x04 } );
		window.Update();
		Assert.IsTrue( Door.IsDown );
		Assert.IsFalse( Door.Enabled );
		Assert.AreEqual( Localization.Text( 389 ), Status.Text );
		Assert.AreEqual( new UiColour( 255, 150, 30 ), Status.TextColour );
		ClickDoor();
		Assert.AreEqual( 0, Ride.CanLoad, "disabled release cannot reopen" );
		state.SetRecord( 49, 22, back );
		window.Update();
		Assert.IsTrue( Door.Enabled );
		Assert.AreEqual( Localization.Text( 365 ), Status.Text );
		ClickDoor();
		Assert.AreEqual( 1, Ride.CanLoad );
	}

	[TestMethod]
	[DataRow( 1, 0 )]
	[DataRow( 2, 0 )]
	[DataRow( 4, 0 )]
	[DataRow( 0, 1 )]
	public void ClosedDoorFollowsTheExistingRefusalGuard( int objectState, int service )
	{
		ClickDoor();
		state.ReplaceObject( Ride with { State = objectState, RequestedService = service } );
		window.Update();
		Assert.IsFalse( Door.Enabled );
		if ( objectState != 0 )
			Assert.IsFalse( Status.Visible, "do not mislabel higher-priority maintenance statuses as CLOSED" );
		ClickDoor();
		Assert.AreEqual( 0, Ride.CanLoad );
		state.ReplaceObject( Ride with { State = 0, RequestedService = 0 } );
		window.Update();
		Assert.IsTrue( Door.Enabled );
	}

	[TestMethod]
	public void AllItemsClosedRowColourReachesEveryRenderedCell()
	{
		ClickDoor();
		var items = new ParkItemsScreen( stack );
		typeof( ParkItemsScreen ).GetMethod( "Show", Private )!.Invoke( items, [0] );
		var lists = (Dictionary<int, UiList>)typeof( ParkItemsScreen ).GetField( "_lists", Private )!.GetValue( items )!;
		var list = lists[5];
		var index = list.Rows.ToList().FindIndex( r => r.Id == 13 );
		Assert.IsTrue( index >= 0 );
		Assert.AreEqual( new UiColour( 128, 128, 128 ), list.Rows[index].Colour );
		var slots = (List<UiControl[]>)typeof( UiList ).GetField( "_slots", Private )!.GetValue( list )!;
		Assert.IsTrue( slots[index].All( cell => cell.TextColour == new UiColour( 128, 128, 128 ) ) );
		// Reusing a slot for an ordinary row must remove its old colour.
		list.Replace( index, new UiList.Row( 13, "open", 0 ) );
		Assert.IsTrue( slots[index].All( cell => cell.TextColour == UiColour.White ) );
	}
}
