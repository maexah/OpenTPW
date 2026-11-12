using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenTPW.UI;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace OpenTPW.Tests;

/// <summary>
/// <c>FUN_00485f60</c>'s arms before the queue's (<see cref="ParkClosedStatus.For"/>) and <c>FUN_004ad4e0</c>'s verb
/// settings on them: the condemned hide, the door at the track's statuses and <c>b_callmech</c> down
/// (docs/exe/park-engine.md, "The ride window's verbs, shown and hidden").
/// </summary>
[TestClass]
public class ParkRideWindowStatusTests
{
	private const int Ride = 13, Mechanic = 26, Handyman = 27, Tick = 5000;

	private static ParkWorld.CatalogueObject Open( int state = 0, int service = 0, bool toilet = false, int valid = 1 )
		=> new( Ride, 1104, 0, 0, 0, Flags: (ushort)(toilet ? ParkWorld.CatalogueObject.ToiletFlag : 0), State: state,
			RequestedService: service, CanLoad: 1, IsTrackRideValid: valid );

	private static Staff Member( int thingId, int model, int activity = 0, int toilet = 0, int repair = 0 )
		=> new( thingId, model, new ParkWorld.StaffState( activity, 0, 50f, 100f, 0, 0, 0, 0, 0, 0,
			ToiletToClean: toilet, ObjectToRepair: repair ), default );

	private static ParkClosedStatus.Staffing Staffing( bool mechanicsStrike = false, params Staff[] staff )
	{
		var strikes = new ParkStrikes( new ParkWorld.StaffHqState( new int[5], Strikes:
		[
			new( 0, 0, 0 ), new( 0, mechanicsStrike ? 1 : 0, 0 ), new( 0, 0, 0 ), new( 0, 0, 0 ), new( 0, 0, 0 )
		] ) );

		return new( staff, strikes, Tick );
	}

	private static int StatusOf( ParkWorld.CatalogueObject ride, int trackType = 0, ParkClosedStatus.Staffing? staffing = null )
		=> ParkClosedStatus.For( null, ride, trackType, staffing ?? Staffing() );

	[TestMethod]
	public void CondemnedComesFirstThenTheTrackThenTheUpgradeThenTheCall()
	{
		Assert.AreEqual( 3, StatusOf( Open( state: 4, service: 1, valid: 0 ), trackType: 1 ) );
		Assert.AreEqual( 0x19, StatusOf( Open( state: 2, service: 1, valid: 0 ), trackType: 1 ) );
		Assert.AreEqual( 0x19, StatusOf( Open( valid: 0 ), trackType: 2 ) );
		Assert.AreEqual( 5, StatusOf( Open( state: 2, service: 1 ) ) );
		Assert.AreEqual( 0x11, StatusOf( Open( state: 1, service: 1 ) ) );
		Assert.AreEqual( 2, StatusOf( Open( state: 1 ) ) );
	}

	[TestMethod]
	public void OnlyAKartOrWaterRideAsksItsCircuit()
	{
		Assert.AreEqual( 0, StatusOf( Open( valid: 0 ) ), "a flat ride's mIsTrackRideValid is not read" );
		Assert.AreEqual( 0, StatusOf( Open( valid: 1 ), trackType: 1 ) );
		Assert.AreEqual( 0, StatusOf( Open( valid: 0 ), trackType: 3 ), "a coaster's goes to its track record (0x004861c0's case 3), not +0x2c" );
	}

	[TestMethod]
	public void ACallAsksTheMechanicsAndAToiletsTheHandymen()
	{
		Assert.AreEqual( 0x11, StatusOf( Open( service: 1 ), staffing: Staffing( false, Member( Handyman, 5 ) ) ) );
		Assert.AreEqual( 0xb, StatusOf( Open( service: 1, toilet: true ), staffing: Staffing( false, Member( Mechanic, 4 ) ) ) );
		Assert.AreEqual( 0x12, StatusOf( Open( service: 1 ), staffing: Staffing( false, Member( Mechanic, 4 ) ) ) );
		Assert.AreEqual( 0xc, StatusOf( Open( service: 1, toilet: true ), staffing: Staffing( false, Member( Handyman, 5 ) ) ) );
	}

	[TestMethod]
	public void TheStrikeComesBeforeTheAssignment()
	{
		var assigned = Open( service: 1 ) with { AssignedStaff = Mechanic, TimeMarkedForMaintenance = Tick };

		Assert.AreEqual( 0x13, StatusOf( assigned, staffing: Staffing( true, Member( Mechanic, 4 ) ) ) );
		Assert.AreEqual( 0x14, StatusOf( assigned, staffing: Staffing( false, Member( Mechanic, 4 ) ) ) );
		Assert.AreEqual( 0xc, StatusOf( Open( service: 1, toilet: true ), staffing: Staffing( true, Member( Handyman, 5 ) ) ),
			"the mechanics' strike is not the handymen's" );
	}

	[TestMethod]
	public void RepairsInProgressOnlyWhileTheAssigneeIsAtWorkOnIt()
	{
		var assigned = Open( service: 1 ) with { AssignedStaff = Mechanic, TimeMarkedForMaintenance = Tick };

		Assert.AreEqual( 0x15, StatusOf( assigned, staffing: Staffing( false, Member( Mechanic, 4, activity: 0xd, repair: Ride ) ) ) );
		Assert.AreEqual( 0x14, StatusOf( assigned, staffing: Staffing( false, Member( Mechanic, 4, activity: 0xc, repair: Ride ) ) ),
			"walking to it is not at work" );
		Assert.AreEqual( 0x14, StatusOf( assigned, staffing: Staffing( false, Member( Mechanic, 4, activity: 0xd, repair: 99 ) ) ),
			"repairing another" );
		Assert.AreEqual( 0x15, StatusOf( assigned with { State = 1, RequestedService = 0 },
			staffing: Staffing( false, Member( Mechanic, 4, activity: 0xd, repair: Ride ) ) ) );
		Assert.AreEqual( 0xf, StatusOf( Open( service: 1, toilet: true ) with { AssignedStaff = Handyman, TimeMarkedForMaintenance = Tick },
			staffing: Staffing( false, Member( Handyman, 5, activity: 0xb, toilet: Ride ) ) ) );
	}

	[TestMethod]
	public void ALapsedAssignmentToAMemberAimedElsewhereIsNobody()
	{
		var lapsed = Open( service: 1 ) with { AssignedStaff = Mechanic, TimeMarkedForMaintenance = Tick - StaffBehaviour.AssignmentKept - 1 };

		Assert.AreEqual( 0x12, StatusOf( lapsed, staffing: Staffing( false, Member( Mechanic, 4, repair: 99 ) ) ) );
		Assert.AreEqual( 0x14, StatusOf( lapsed with { TimeMarkedForMaintenance = Tick - StaffBehaviour.AssignmentKept },
			staffing: Staffing( false, Member( Mechanic, 4, repair: 99 ) ) ) );
		Assert.AreEqual( 0x14, StatusOf( lapsed, staffing: Staffing( false, Member( Mechanic, 4, repair: Ride ) ) ) );
	}

	[TestMethod]
	public void EachStatusHasItsRowAndColour()
	{
		// The whole table at 0x0074fbc0, one row a status from nought to 0x1a, read from the executable.
		int[] rows = [0, 365, 366, 367, 368, 372, 370, 371, 369, 373, 374, 378, 376, 377, 375, 379, 380, 384, 382, 383, 381, 385, 388, 389, 390, 386, 387];

		CollectionAssert.AreEqual( rows, Enumerable.Range( 0, rows.Length ).Select( ParkClosedStatus.Row ).ToArray() );
		Assert.AreEqual( 0, ParkClosedStatus.Row( 0x1b ), "past the table" );
		Assert.AreEqual( new UiColour( 255, 50, 30 ), ParkClosedStatus.Colour( 3 ) );
		Assert.AreEqual( new UiColour( 80, 100, 255 ), ParkClosedStatus.Colour( 0x11 ) );
		Assert.AreEqual( new UiColour( 80, 100, 255 ), ParkClosedStatus.Colour( 4 ) );
		Assert.AreEqual( new UiColour( 255, 150, 30 ), ParkClosedStatus.Colour( 0x19 ) );
		Assert.AreEqual( new UiColour( 255, 50, 30 ), ParkClosedStatus.Colour( 2 ), "broken down is red, as condemned" );
		Assert.AreEqual( new UiColour( 80, 100, 255 ), ParkClosedStatus.Colour( 0x15 ), "the blue runs to 0x15" );
		Assert.AreEqual( new UiColour( 255, 150, 30 ), ParkClosedStatus.Colour( 0x16 ), "and the orange from 0x16" );
		Assert.IsFalse( ParkClosedStatus.CallsTheMechanic( 0x10 ) );
		Assert.IsTrue( ParkClosedStatus.CallsTheMechanic( 0x11 ) );
		Assert.IsTrue( ParkClosedStatus.CallsTheMechanic( 0x15 ) );
		Assert.IsFalse( ParkClosedStatus.CallsTheMechanic( 0x16 ) );
	}
}

/// <summary>The ride window's verbs on a real window over the stock park (needs the game).</summary>
[TestClass]
public class ParkRideWindowVerbTests
{
	private const int DinoKarts = 1150;

	private Level? before;
	private Point2 size;
	private ParkState state = null!;
	private ParkPeople people = null!;
	private ParkObjectWindow window = null!;
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
		var level = (Level)RuntimeHelpers.GetUninitializedObject( typeof( Level ) );
		typeof( Level ).GetProperty( nameof( Level.Park ) )!.SetValue( level, world );
		typeof( Level ).GetProperty( nameof( Level.ParkState ) )!.SetValue( level, state );
		typeof( Level ).GetProperty( nameof( Level.Catalogue ) )!.SetValue( level, catalogue );
		Level.Current = level;
		people = new( world, new ParkBalance( "jungle", easyMode: true ), null, state, catalogue, _ => null );
		var stack = (WindowStack)RuntimeHelpers.GetUninitializedObject( typeof( WindowStack ) );
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

	private UiButton Verb( int id ) => (UiButton)Find( window.Root, id )!;
	private UiControl Status => Find( window.Root, 0x3e25 )!;
	private static UiControl? Find( UiControl root, int id )
		=> root.Id == id ? root : root.Children.Select( c => Find( c, id ) ).FirstOrDefault( c => c != null );
	private ParkWorld.CatalogueObject Ride => state.Objects.Single( x => x.ThingId == 13 );

	private static readonly int[] Hidden = [0x3e38, 0x3e34, 0x3e2a, 0x3e36, 0x3e2c, 0x3e37, 0x3e35];

	[TestMethod]
	public void ACondemnedRidesWindowHidesSevenVerbsAndKeepsDelete()
	{
		window.Update();
		Assert.IsTrue( Hidden.Where( id => id != 0x3e2a ).All( id => Verb( id ).Visible ) );

		state.ReplaceObject( Ride with { State = 4, CanLoad = 0 } );
		window.Update();

		Assert.IsTrue( Hidden.All( id => !Verb( id ).Visible ), string.Join( " ", Hidden.Where( id => Verb( id ).Visible ).Select( id => $"{id:x}" ) ) );
		Assert.IsTrue( Verb( 0x3e2b ).Visible );
		Assert.AreEqual( Localization.Text( 367 ), Status.Text );
		Assert.AreEqual( new UiColour( 255, 50, 30 ), Status.TextColour );

		state.ReplaceObject( Ride with { State = 0, CanLoad = 1 } );
		window.Update();
		Assert.IsTrue( Hidden.Where( id => id != 0x3e2a ).All( id => Verb( id ).Visible ), "shown again on any other status" );
		Assert.IsFalse( Verb( 0x3e2a ).Visible, "the Belly Bounce has no track" );

		state.ReplaceObject( Ride with { CatalogueId = DinoKarts, IsTrackRideValid = 1, State = 4, CanLoad = 0 } );
		window.Update();
		Assert.IsFalse( Verb( 0x3e2a ).Visible, "a condemned kart ride's track button is hidden too" );
		Assert.IsFalse( Verb( 0x3e36 ).Visible );
	}

	[TestMethod]
	public void AKartRideWithItsCircuitOpenDisablesTheDoorAndLeavesItsPosition()
	{
		state.ReplaceObject( Ride with { CatalogueId = DinoKarts, IsTrackRideValid = 1, CanLoad = 1 } );
		window.Update();
		Assert.IsTrue( Verb( 0x3e2a ).Visible );
		Assert.IsFalse( Verb( 0x3e38 ).IsDown );

		state.ReplaceObject( Ride with { IsTrackRideValid = 0, CanLoad = 0 } );
		window.Update();

		Assert.IsFalse( Verb( 0x3e38 ).Enabled );
		Assert.IsFalse( Verb( 0x3e38 ).IsDown, "the closed ride's door keeps the position it had" );
		Assert.AreEqual( Localization.Text( 386 ), Status.Text );
		Assert.AreEqual( new UiColour( 255, 150, 30 ), Status.TextColour );
	}

	[TestMethod]
	public void ADoorTheGuardRefusesKeepsItsPosition()
	{
		window.Update();
		Assert.IsTrue( Verb( 0x3e38 ).Enabled && !Verb( 0x3e38 ).IsDown );

		state.ReplaceObject( Ride with { State = 1, CanLoad = 0 } );
		window.Update();

		Assert.IsFalse( Verb( 0x3e38 ).Enabled );
		Assert.IsFalse( Verb( 0x3e38 ).IsDown, "a broken ride's door is left up, as the original's tree reads it" );
		Assert.AreEqual( 13, Verb( 0x3e38 ).HelpText );
	}

	[TestMethod]
	public void TheCallButtonIsDownWhileAMechanicIsCalled()
	{
		window.Update();
		Assert.IsFalse( Verb( 0x3e36 ).IsDown );

		state.ReplaceObject( Ride with { RequestedService = 1, CanLoad = 0 } );
		window.Update();
		Assert.IsTrue( Verb( 0x3e36 ).IsDown );
		Assert.IsTrue( Verb( 0x3e36 ).Visible );

		state.ReplaceObject( Ride with { RequestedService = 0, State = 4 } );
		window.Update();
		state.ReplaceObject( Ride with { State = 1 } );
		window.Update();
		Assert.IsFalse( Verb( 0x3e36 ).IsDown, "broken down with nobody at work is status 2" );
		Assert.AreEqual( Localization.Text( 366 ), Status.Text );
	}
}
