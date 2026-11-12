using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenTPW.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace OpenTPW.Tests;

/// <summary>
/// The staff member's window (<c>FUN_004b6080</c>, its handler <c>FUN_004b5cb0</c>; <c>docs/exe/hud.md</c>, "The person's
/// windows"), opened from the locator's strip and the all-staff list on the shipped park's staff. Skipped without the
/// game (<see cref="GameData"/>).
/// </summary>
[TestClass]
public class ParkStaffWindowTests
{
	private Level? _level;
	private Point2 _screen;
	private float _now;
	private Vector3 _looksAt;
	private ParkPeople _people = null!;
	private WindowStack _stack = null!;
	private ParkGadget _gadget = null!;
	private bool _confirmations;
	private bool _rmbCancel;

	[TestInitialize]
	public void LoadThePark()
	{
		_level = Level.Current;
		_screen = Screen.Size;
		_now = Time.Now;
		_looksAt = ParkOrbitCameraMode.PointOfInterest;
		_confirmations = GameOptions.Current.Confirmations;
		_rmbCancel = GameOptions.Current.RmbCancel;

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
		typeof( Level ).GetProperty( nameof( Level.StaffPool ) )!.SetValue( level, new ParkStaffPool( new ParkBalance( "jungle", easyMode: true ), saved: world ) );
		Level.Current = level;

		_people = new ParkPeople( world, new ParkBalance( "jungle", easyMode: true ), null, state, catalogue );
		_stack = new WindowStack();
		_gadget = new ParkGadget( _stack );
		_stack.Open( _gadget );

		ParkOrbitCameraMode.PointOfInterest = new Vector3( 300f, 300f, 0f );
	}

	[TestCleanup]
	public void PutItAllBack()
	{
		_people?.Delete();
		Entity.ApplyDeletions();
		Level.Current = _level!;
		Screen.Size = _screen;
		Time.Now = _now;
		Time.PinWall( 0 );
		Input.Mouse = new();
		ParkOrbitCameraMode.PointOfInterest = _looksAt;
		GameOptions.Current.Confirmations = _confirmations;
		GameOptions.Current.RmbCancel = _rmbCancel;
		ParkBuildMode.Forget();
		Unimplemented.Forget();
	}

	private ParkLocator Locator => _gadget.Locator;

	private ParkStaffWindow? Window => _stack.Windows.OfType<ParkStaffWindow>().FirstOrDefault();

	private static int Times( string what ) => Unimplemented.Summary.FirstOrDefault( gap => gap.What == what ).Times;

	private void Click( UiControl root, int id )
	{
		var control = Find( root, id );
		Assert.IsTrue( _stack.ClickAt( (control.Rect.Left + control.Rect.Right) / 2, (control.Rect.Top + control.Rect.Bottom) / 2 ), $"{id:x} clicked" );
	}

	private Staff Mechanic => _people.Staff.First( member => member.Model == ParkPeople.MechanicModel );

	/// <summary>
	/// <b>The strip's click opens the window on its member</b> (<c>FUN_004b3c10</c>): a park screen, so the arm folds the
	/// locator closed; titled with the member's name, its six labels UITEXT 64, 65, 67, 69, 68 and 66 in the stream's order
	/// down; the preview counted.
	/// </summary>
	/// <remarks><b>Mutations:</b> the strip's opener gone; a label's row swapped; the title not the name; the preview not counted.</remarks>
	[TestMethod]
	public void TheStripsClickOpensTheWindowOnItsMember()
	{
		Locator.OpenStaff( _gadget.CarryLocatorPanel, () => _stack.CloseParkScreen() );
		ParkGadgetPressTests.Settle( _gadget );
		Click( _gadget.Root, 0x95f );
		ParkGadgetPressTests.Settle( _gadget );

		var member = _people.Staff.First( member => member.ThingId == Locator.Actor );
		Click( _gadget.Root, 0x146 );
		_gadget.Update();
		ParkGadgetPressTests.Settle( _gadget );

		Assert.IsNotNull( Window, "the window is up" );
		Assert.AreEqual( member.ThingId, Window!.ThingId );
		Assert.AreEqual( ParkLocator.Kind.None, Locator.Open, "the arm folded the locator away" );
		Assert.AreEqual( member.Name, Find( Window.Root, 0x520 ).Text );
		Assert.AreEqual( 1, Times( "STAFF_WINDOW_PREVIEW" ) );
		Assert.AreEqual( 0, Times( "STAFF_WINDOW" ) );

		CollectionAssert.AreEqual(
			new[] { "Employed for", "Monthly wage", "Skill", "Happiness", "Energy", "Current status" },
			new[] { 0x51a, 0x51d, 0x519, 0x518, 0x51c, 0x516 }.Select( id => Find( Window.Root, id ).Text ).ToArray() );
	}

	/// <summary>
	/// <b>The figures are the member's</b> (<c>FUN_004b58b0</c>): "{P8} days" of whole days since <c>mTimeHired</c>, the
	/// wage a money cell, the skill's bar, the happiness's and the energy's of their low byte, the status picture and the
	/// <c>STAFFSTATES</c> word; refreshed four seconds of real time on.
	/// </summary>
	/// <remarks><b>Mutations:</b> the days' divide or parameter; the happiness and the energy swapped; the byte mask; the picture's part; the refresh's period.</remarks>
	[TestMethod]
	public void TheFiguresAreTheMembersAndRefreshEveryFourSeconds()
	{
		Time.PinWall( 1000 );
		var member = Mechanic;
		var hired = ParkWorld.StaffState.HiredWhenOf( member.TimeHired )!.Value;
		member.Happiness = 84.97f + 256f;
		member.Tiredness = 65.3f;
		member.SetActivity( StaffActivity.Resting, 0 );

		var now = hired.AddDays( 45.5 );
		var wage = Level.Current!.StaffPool!.WageFor( ParkStaffPool.KindFor( member.Model ), member.PayGrade );
		var figures = ParkStaffWindow.FiguresOf( member, now, wage );
		Assert.AreEqual( ("45 days", $"$ {wage}", (ParkStaffScreen.SkillOf( member ) << 10) / 100, (84 << 10) / 100, (65 << 10) / 100, 2, "Resting"),
			figures );
		Assert.AreEqual( "1 days", ParkStaffWindow.FiguresOf( member, hired.AddDays( 1 ), wage ).Days, "the plural at any count" );
		Assert.AreEqual( 0, ParkStaffWindow.DaysEmployed( member, hired.AddHours( 23 ) ) );

		ParkStaffWindow.Open( _stack, member.ThingId );
		var window = Window!;
		Assert.AreEqual( ("Resting", 2, (84 << 10) / 100, (65 << 10) / 100),
			(Find( window.Root, 0x517 ).Text, Find( window.Root, 0x51f ).Frame, Bar( window, 0x513 ), Bar( window, 0x515 )) );
		Assert.AreEqual( (ParkStaffScreen.SkillOf( member ) << 10) / 100, Bar( window, 0x514 ) );
		Assert.AreEqual( $"$ {wage}", Find( window.Root, 0x51e ).Text );

		member.Tiredness = 12f;
		member.SetActivity( StaffActivity.OnStrike, 0 );
		Time.PinWall( 4999 );
		window.Update();
		Assert.AreEqual( (65 << 10) / 100, Bar( window, 0x515 ), "not yet" );

		Time.PinWall( 5000 );
		window.Update();
		Assert.AreEqual( ((12 << 10) / 100, 3, "On strike"), (Bar( window, 0x515 ), Find( window.Root, 0x51f ).Frame, Find( window.Root, 0x517 ).Text) );
	}

	/// <summary>
	/// <b>A walk reads as idle where the member patrols the whole map</b>, and as patrolling where they are fenced
	/// (<c>FUN_00506eb0</c>); both show picture 0. A fenced member's show puts the patrol switch down.
	/// </summary>
	/// <remarks><b>Mutations:</b> the whole-map test reversed; the switch not put down.</remarks>
	[TestMethod]
	public void AFencedWalkIsPatrollingAndPutsTheSwitchDown()
	{
		var member = Mechanic;
		member.SetActivity( StaffActivity.Walking, 0 );
		Fence( member, Staff.WholeMapBottomLeft, Staff.WholeMapTopRight );

		ParkStaffWindow.Open( _stack, member.ThingId );
		Assert.AreEqual( ("Idle", 0, false), (Find( Window!.Root, 0x517 ).Text, Find( Window.Root, 0x51f ).Frame, ((UiButton)Find( Window.Root, 0x510 )).IsDown) );
		_stack.Close( Window );

		Fence( member, 1 + 10 + 10 * 128, 1 + 20 + 20 * 128 );
		ParkStaffWindow.Open( _stack, member.ThingId );
		Assert.AreEqual( ("Patrolling", 0, true), (Find( Window!.Root, 0x517 ).Text, Find( Window.Root, 0x51f ).Frame, ((UiButton)Find( Window.Root, 0x510 )).IsDown) );
	}

	/// <summary>
	/// <b>The cursor keys walk the staff as the arrows do</b> (Q547, <c>FUN_0048d200</c>): from the newest, Down steps towards
	/// the oldest to the second, Up back, and Up again round from the newest to the oldest.
	/// </summary>
	/// <remarks><b>Mutations:</b> Up and Down swapped; the staff member's window left without the keys.</remarks>
	[TestMethod]
	public void TheCursorKeysWalkTheStaff()
	{
		var staff = _people.Staff.Select( member => member.ThingId ).ToList();
		Assert.IsTrue( staff.Count >= 3, "three staff, so round from the newest is not the second" );

		ParkStaffWindow.Open( _stack, staff[0] );
		var window = Window!;

		Assert.AreEqual( 1, ParkFrontEnd.ObjectWindowKeys( _stack.Windows[^1], [NeoVeldrid.Key.Down] ) );
		Assert.AreEqual( staff[1], window.ThingId, "Down: towards the oldest" );

		ParkFrontEnd.ObjectWindowKeys( _stack.Windows[^1], [NeoVeldrid.Key.Up, NeoVeldrid.Key.Up] );
		Assert.AreEqual( staff[^1], window.ThingId, "Up twice: back, then round from the newest" );
		_stack.Close( window );
	}

	/// <summary>
	/// <b>The arrows walk the staff</b>, newest first as the list keeps them: b_ardown towards the oldest, b_arup towards the
	/// newest, round from either end; with fewer than two staff both are hidden (<c>0x004b5a60</c>).
	/// </summary>
	/// <remarks><b>Mutations:</b> the arrows swapped; walking the guests; the arrows not hidden, or hidden with two.</remarks>
	[TestMethod]
	public void TheArrowsWalkTheStaff()
	{
		var staff = _people.Staff.Select( member => member.ThingId ).ToList();
		Assert.IsTrue( staff.Count >= 2 );

		ParkStaffWindow.Open( _stack, staff[0] );
		var window = Window!;
		Assert.IsTrue( Find( window.Root, 0x50d ).Visible && Find( window.Root, 0x50f ).Visible );

		Click( window.Root, 0x50f );
		Assert.AreEqual( staff[1], window.ThingId );
		Assert.AreEqual( _people.Staff[1].Name, Find( window.Root, 0x520 ).Text );

		Click( window.Root, 0x50d );
		Click( window.Root, 0x50d );
		Assert.AreEqual( staff[^1], window.ThingId, "round from the oldest" );
		_stack.Close( window );

		foreach ( var member in staff.Skip( 2 ) )
			_people.Fire( member );

		ParkStaffWindow.Open( _stack, staff[0] );
		Assert.AreEqual( (true, true), (Find( Window!.Root, 0x50d ).Visible, Find( Window.Root, 0x50f ).Visible), "two: shown" );
		_stack.Close( Window );

		_people.Fire( staff[1] );
		ParkStaffWindow.Open( _stack, staff[0] );
		Assert.AreEqual( (false, false), (Find( Window!.Root, 0x50d ).Visible, Find( Window.Root, 0x50f ).Visible), "one: hidden" );
	}

	/// <summary>
	/// <b>The advisor tells of the window as it is built</b>, message <c>0xc0</c> (response 411, sample 384), <b>and of a
	/// patrol area as a fenced member is shown</b>, <c>0xd4</c> (response 431, sample 457); the close takes both back.
	/// </summary>
	/// <remarks><b>Mutations:</b> either post missing or another id; <c>0xd4</c> posted for an unfenced member; a withdrawal missing; a row's response or sample.</remarks>
	[TestMethod]
	public void TheAdvisorTellsOfTheWindowAndOfAPatrolArea()
	{
		using var clock = new SimulationClockScope();
		var lines = new ParkAdvisorMessages( new SettingsFile( "Advisor/Advisor.sam" ), tutorial: () => true );
		var park = Level.Current!.ParkState!;
		park.AdvisorMessages = lines;
		var said = new List<(int Response, int Sample)>();
		int Play( int response, int sample ) { said.Add( (response, sample) ); return 9000; }

		var member = Mechanic;
		Fence( member, Staff.WholeMapBottomLeft, Staff.WholeMapTopRight );
		ParkStaffWindow.Open( _stack, member.ThingId );
		StringAssert.Contains( lines.Census(), "pending=1 attempts=0 threshold=25 slot=192/subject=0 " );
		lines.Tick( park.GameTick, 0, Play );
		CollectionAssert.AreEqual( new[] { (411, 384) }, said, "0xc0 alone for a member with no area" );
		_stack.Close( Window! );
		StringAssert.Contains( lines.Census(), "message=192/score=1000/line=0/" );
		said.Clear();

		Fence( member, 1 + 10 + 10 * 128, 1 + 20 + 20 * 128 );
		ParkStaffWindow.Open( _stack, member.ThingId );
		lines.Tick( park.GameTick, 0, Play );
		CollectionAssert.AreEqual( new[] { (431, 457) }, said, "0xd4 first, posted by the show inside the build" );
		StringAssert.Contains( lines.Census(), "slot=192/" );

		_stack.Close( Window! );
		StringAssert.Contains( lines.Census(), "message=212/score=1000/line=0/" );
		StringAssert.Contains( lines.Census(), "/said=0/slaps=1", "cut off" );
		Assert.IsFalse( lines.Census().Contains( "slot=192/" ), "0xc0 taken back" );
	}

	/// <summary>
	/// <b>Opening a window with one already up builds nothing and the window stays on its member</b>
	/// (<c>FUN_004b6080</c>), <b>but the advisor's <c>0xc0</c> is posted again</b>: a second member opened over the first leaves
	/// one window, on the first, and the post is made a second time (the advisor's log line, <c>post message=192</c>).
	/// </summary>
	/// <remarks><b>Mutations:</b> a second window built over the first; the post made only where the window was built.</remarks>
	[TestMethod]
	public void OpenWithTheWindowAlreadyUpStaysOnItsMemberAndPostsAgain()
	{
		using var clock = new SimulationClockScope();
		var park = Level.Current!.ParkState!;
		park.AdvisorMessages = new ParkAdvisorMessages( new SettingsFile( "Advisor/Advisor.sam" ), tutorial: () => true );
		var posts = 0;
		void Count( Logger.Level severity, string line ) => posts += line.Contains( "Park advisor: post message=192 " ) ? 1 : 0;
		var first = Mechanic;
		var second = _people.Staff.First( member => member != first );
		Fence( first, Staff.WholeMapBottomLeft, Staff.WholeMapTopRight );

		Logger.OnLog += Count;

		try
		{
			ParkStaffWindow.Open( _stack, first.ThingId );
			Assert.AreEqual( 1, posts, "told as the window was built" );
			var window = Window!;

			ParkStaffWindow.Open( _stack, second.ThingId );

			Assert.AreEqual( 1, _stack.Windows.OfType<ParkStaffWindow>().Count(), "no second window" );
			Assert.AreSame( window, Window, "the same window" );
			Assert.AreEqual( first.ThingId, Window!.ThingId, "still on its member" );
			Assert.AreEqual( 2, posts, "told again" );
		}
		finally
		{
			Logger.OnLog -= Count;
		}
	}

	/// <summary>
	/// <b>b_locate and Ctrl+S with the window up open the staff's locator on its member</b> (<c>FUN_004b3f60( member )</c>):
	/// the panel out, the kind's button down, the strip on them and followed, the status in its corner, and the window
	/// closed.
	/// </summary>
	/// <remarks><b>Mutations:</b> the kind's button or role; the strip hidden; not followed; the thought shown; the window left up.</remarks>
	[TestMethod]
	public void LocateOpensTheStaffsLocatorOnTheMember()
	{
		var member = Mechanic;
		ParkStaffWindow.Open( _stack, member.ThingId );
		ParkGadgetPressTests.Settle( _gadget );

		Click( Window!.Root, 0x521 );
		ParkGadgetPressTests.Settle( _gadget );

		Assert.IsNull( Window, "the window closed" );
		Assert.AreEqual( (ParkLocator.Kind.Staff, 2, member.ThingId, true, true),
			(Locator.Open, Locator.Role, Locator.Actor, Locator.Following, Locator.Strip.Visible) );
		StringAssert.Contains( Locator.Census(), "down=95f" );
		Assert.AreEqual( (true, false), (Find( Locator.Strip, 0x148 ).Visible, Find( Locator.Strip, 0x14a ).Visible) );
		Assert.AreEqual( (0x95e, 0x962, 0x961, 0x960),
			(ParkLocator.ButtonOf( ParkPeople.HandymanModel ), ParkLocator.ButtonOf( ParkPeople.GuardModel ), ParkLocator.ButtonOf( ParkPeople.EntertainerModel ), ParkLocator.ButtonOf( ParkPeople.ResearcherModel )) );
	}

	/// <summary>
	/// <b>A frame the orbit camera was panned by key ends the locator's follow, the strip left on the member</b>
	/// (<c>0x0042ba37</c>, <c>0x0042ba92</c>); a frame with no pan leaves it following.
	/// </summary>
	/// <remarks><b>Mutations:</b> the locator not reading the pan.</remarks>
	[TestMethod]
	public void APanEndsTheLocatorsFollow()
	{
		var member = Mechanic;
		var panning = typeof( ParkOrbitCameraMode ).GetProperty( nameof( ParkOrbitCameraMode.Panning ) )!;
		ParkStaffWindow.Open( _stack, member.ThingId );
		ParkGadgetPressTests.Settle( _gadget );
		Click( Window!.Root, 0x521 );
		ParkGadgetPressTests.Settle( _gadget );

		try
		{
			Locator.Update();
			Assert.AreEqual( (member.ThingId, true), (Locator.Actor, Locator.Following), "no pan: followed" );

			panning.SetValue( null, true );
			Locator.Update();
			Assert.AreEqual( (member.ThingId, false, true), (Locator.Actor, Locator.Following, Locator.Strip.Visible), "a pan: let go" );
		}
		finally
		{
			panning.SetValue( null, false );
		}
	}

	/// <summary>
	/// <b>A frame the orbit camera was reset by Home or keypad 5 ends the locator's follow, the strip left on the member</b>
	/// (<c>0x0042b54d</c> clears the follow's bit), and the camera stays where the reset put it.
	/// </summary>
	/// <remarks><b>Mutations:</b> the locator not reading the reset.</remarks>
	[TestMethod]
	public void AResetEndsTheLocatorsFollow()
	{
		var member = Mechanic;
		var reset = typeof( ParkOrbitCameraMode ).GetProperty( nameof( ParkOrbitCameraMode.WasReset ) )!;
		ParkStaffWindow.Open( _stack, member.ThingId );
		ParkGadgetPressTests.Settle( _gadget );
		Click( Window!.Root, 0x521 );
		ParkGadgetPressTests.Settle( _gadget );

		try
		{
			Locator.Update();
			Assert.AreEqual( (member.ThingId, true), (Locator.Actor, Locator.Following), "no reset: followed" );

			ParkOrbitCameraMode.Reset();
			reset.SetValue( null, true );
			Locator.Update();
			Assert.AreEqual( (member.ThingId, false, true), (Locator.Actor, Locator.Following, Locator.Strip.Visible), "a reset: let go" );
			Assert.AreEqual( new System.Numerics.Vector3( 475f, 175f, 0f ), ParkOrbitCameraMode.PointOfInterest, "the point left at the reset's" );
		}
		finally
		{
			reset.SetValue( null, false );
			ParkOrbitCameraMode.Forget();
		}
	}

	/// <summary>
	/// <b>Ctrl+S with the window up opens the staff's locator on its member</b> (<c>FUN_004b3f60( [0x007c2658] )</c>), and
	/// with none up on nobody (<c>FUN_004b3f60( 0 )</c>).
	/// </summary>
	/// <remarks><b>Mutations:</b> the shortcut ignoring the window.</remarks>
	[TestMethod]
	public void CtrlSOverTheWindowOpensOnItsMember()
	{
		var stack = new WindowStack();
		var front = new ParkFrontEnd( stack, "jungle" );
		var shortcut = typeof( ParkFrontEnd ).GetMethod( "ShortcutKey", BindingFlags.Instance | BindingFlags.NonPublic )!;
		var member = Mechanic;

		ParkStaffWindow.Open( stack, member.ThingId );
		var window = stack.Windows.OfType<ParkStaffWindow>().Single();
		shortcut.Invoke( front, [window, 13] );
		ParkGadgetPressTests.Settle( front.Gadget );
		Assert.AreEqual( (ParkLocator.Kind.Staff, member.ThingId, 0), (front.Gadget.Locator.Open, front.Gadget.Locator.Actor, stack.Windows.OfType<ParkStaffWindow>().Count()) );

		var fresh = new WindowStack();
		var other = new ParkFrontEnd( fresh, "jungle" );
		shortcut.Invoke( other, [other.Gadget, 13] );
		ParkGadgetPressTests.Settle( other.Gadget );
		Assert.AreEqual( (ParkLocator.Kind.Staff, 0), (other.Gadget.Locator.Open, other.Gadget.Locator.Actor), "none up: on nobody" );
	}

	/// <summary>
	/// <b>b_allstaff opens the all-staff list on the member's kind with their row picked</b> (<c>FUN_00496620( kind, member )</c>),
	/// which closes the window; <b>a row's click opens the window on its member</b> (<c>0x00496088</c>), which closes the list.
	/// </summary>
	/// <remarks><b>Mutations:</b> the kind not handed over; the pick not made; the row's opener gone.</remarks>
	[TestMethod]
	public void TheListAndTheWindowOpenEachOther()
	{
		// A second mechanic, sorted after the first by name, so the pick is not the add's own first row.
		var guard = _people.Staff.First( one => one.Model == ParkPeople.GuardModel );
		typeof( Staff ).GetField( $"<{nameof( Staff.Model )}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic )!.SetValue( guard, ParkPeople.MechanicModel );
		var member = _people.Staff.Where( one => one.Model == ParkPeople.MechanicModel ).OrderBy( one => one.Name, StringComparer.Ordinal ).Last();
		ParkStaffWindow.Open( _stack, member.ThingId );

		Click( Window!.Root, 0x50b );
		var list = _stack.Windows.OfType<ParkStaffScreen>().Single();
		var rows = (UiList)typeof( ParkStaffScreen ).GetField( "_list", BindingFlags.Instance | BindingFlags.NonPublic )!.GetValue( list )!;
		Assert.IsNull( Window, "the window closed" );
		Assert.AreEqual( (member.ThingId, 2), (rows.Selected, rows.Rows.Count) );
		Assert.AreNotEqual( member.ThingId, rows.Rows[0].Id, "not the first row" );
		Assert.IsTrue( rows.Rows.All( row => _people.Staff.First( one => one.ThingId == row.Id ).Model == ParkPeople.MechanicModel ), "the mechanics' tab" );
		Assert.AreEqual( 1, Times( "STAFF_LIST_ADVISOR_LINE" ), "the list's own line, 0x11d, counted" );

		var first = rows.Rows[rows.ScrollTop];
		rows.LeftClickedAt( (rows.RowArea.Left + rows.RowArea.Right) / 2f, rows.RowArea.Top + rows.RowHeight / 2f );
		Assert.AreEqual( first.Id, Window?.ThingId );
		Assert.AreEqual( 0, _stack.Windows.OfType<ParkStaffScreen>().Count(), "the list closed" );
	}

	/// <summary>
	/// <b>The name's edit is counted</b>, the window staying up; <b>PICK UP is disabled</b> going on strike, on strike, carried and repairing, and off a cell of
	/// type 0, 1, 3, 9 or 10 (<c>FUN_00505bd0</c>).
	/// </summary>
	/// <remarks><b>Mutations:</b> the edit not counted; a state or a cell type missing from the test.</remarks>
	[TestMethod]
	public void TheVerbsLeftAreCountedAndPickUpIsEnabledByStateAndCell()
	{
		var member = Mechanic;
		Fence( member, Staff.WholeMapBottomLeft, Staff.WholeMapTopRight );
		member.SetActivity( StaffActivity.Idle, 0 );
		ParkStaffWindow.Open( _stack, member.ThingId );
		var window = Window!;

		Click( window.Root, 0x520 );

		Assert.AreEqual( 1, Times( "STAFF_WINDOW_NAME_EDIT" ) );
		Assert.AreSame( window, Window );

		var state = Level.Current!.ParkState!;
		var (x, y) = member.Navigator.Position.Cell;
		var cell = state.Record( x, y );

		foreach ( var (type, can) in new[] { (0, true), (1, true), (2, false), (3, true), (4, false), (9, true), (10, true), (11, false) } )
		{
			state.SetRecord( x, y, cell with { Type = type } );
			Assert.AreEqual( can, ParkStaffWindow.CanBePickedUp( member, state ), $"cell type {type}" );
		}

		state.SetRecord( x, y, cell with { Type = 1 } );

		// Off the map the bytes pack to another row's cell, as the original's 0x004fa990 reads them: x 128 reads (0, y + 1).
		// Past the map's last record nothing is read, and PICK UP is off.
		var standing = member.Navigator.Position;
		var wrapped = state.Record( 0, y + 1 );
		member.Navigator.Position = FixedVector.AtCell( ParkWorld.MapSize, y );
		state.SetRecord( 0, y + 1, wrapped with { Type = 4 } );
		Assert.IsFalse( ParkStaffWindow.CanBePickedUp( member, state ), "off the map, the next row's cell a fence" );
		state.SetRecord( 0, y + 1, wrapped with { Type = 1 } );
		Assert.IsTrue( ParkStaffWindow.CanBePickedUp( member, state ), "off the map, the next row's cell a path" );
		state.SetRecord( 0, y + 1, wrapped );
		member.Navigator.Position = FixedVector.AtCell( x, ParkWorld.MapSize );
		Assert.IsFalse( ParkStaffWindow.CanBePickedUp( member, state ), "past the map's last record" );
		member.Navigator.Position = standing;

		foreach ( var (activity, can) in new[] { (StaffActivity.Idle, true), (StaffActivity.GoingOnStrike, false), (StaffActivity.OnStrike, false),
			(StaffActivity.Held, false), (StaffActivity.Repairing, false), (StaffActivity.Resting, true), ((StaffActivity)0x12, false) } )
		{
			member.SetActivity( activity, 0 );
			Assert.AreEqual( can, ParkStaffWindow.CanBePickedUp( member, state ), $"{activity}" );
		}

		member.SetActivity( StaffActivity.OnStrike, 0 );
		window.Update();
		Assert.IsFalse( ((UiButton)Find( window.Root, 0x50e )).Enabled, "every frame" );
	}

	/// <summary>
	/// <b>FIRE asks first where the Confirmations option is on</b> (<c>0x50c</c>, <c>[0x0078d90f]</c>): the box on UITEXT 397
	/// over the window closed at once; its cross keeps the member; its tick (<c>FUN_004b6070</c>) dismisses them, one
	/// month's wage taken and put on the month's staff costs, the puff counted. The original's run took 161 each way.
	/// </summary>
	/// <remarks><b>Mutations:</b> no box; the box's text; the window left up; the cross dismissing; the tick dismissing nobody;
	/// the wage not taken; the puff not counted.</remarks>
	[TestMethod]
	public void FireAsksFirstWhereConfirmationsAreOn()
	{
		GameOptions.Current.Confirmations = true;
		var member = Mechanic;
		var state = Level.Current!.ParkState!;
		var (bank, costs) = (state.Balance, state.MonthStaffCosts.Total);

		ParkStaffWindow.Open( _stack, member.ThingId );
		Click( Window!.Root, 0x50c );

		var box = _stack.Windows.OfType<MessageBox>().Single();
		Assert.IsNull( Window, "the window closed behind the box" );
		Assert.AreEqual( Localization.Get( UIStrings.DismissEmployee ), Find( box.Root, 0x9873c7 ).Text );
		Assert.IsTrue( _people.IsStaff( member.ThingId ), "asked, not yet dismissed" );

		Click( box.Root, 0x9873c8 );
		Assert.AreEqual( (0, true, bank), (_stack.Windows.OfType<MessageBox>().Count(), _people.IsStaff( member.ThingId ), state.Balance), "the cross keeps them" );

		ParkStaffWindow.Open( _stack, member.ThingId );
		Click( Window!.Root, 0x50c );
		Click( _stack.Windows.OfType<MessageBox>().Single().Root, 0x9873c9 );

		Assert.IsFalse( _people.IsStaff( member.ThingId ), "the tick dismisses them" );
		Assert.AreEqual( (bank - 161, costs + 161), (state.Balance, state.MonthStaffCosts.Total), "one month's wage, 7 x 23" );
		Assert.AreEqual( 0, Times( "STAFF_WINDOW_FIRE" ) );
	}

	/// <summary>
	/// <b>A dismissal puts the puff up where the member stood</b> (<c>FUN_00505790</c>, <c>0x005059c3</c>): one emitter of
	/// effect 19 at their x and z on the ground there, started as the dismissal begins (the member is taken out of the roster after it).
	/// </summary>
	[TestMethod]
	public void FireStartsThePuffWhereTheMemberStood()
	{
		using var terrain = FileSystem.OpenRead( "levels/jungle/terrain/base.MD2" )!;
		var field = new HeightfieldFile( terrain );
		var system = new ParticleSystem( "Particle/Tp2.plb", 1000 );

		try
		{
			PersonPuffs.Ground = field;
			GameClock.Rebase();

			var member = Mechanic;
			var (x, height, z) = PersonPuffs.PlaceOn( field, member.Navigator.Position );

			Assert.IsTrue( _people.Fire( member.ThingId ) );

			var puffs = system.Emitters.Where( e => e.Active ).ToList();
			Assert.AreEqual( 1, puffs.Count );
			Assert.AreEqual( (0x13, x >> 4, height >> 4, z >> 4), (puffs[0].Effect, puffs[0].X, puffs[0].Y, puffs[0].Z) );
		}
		finally
		{
			PersonPuffs.Ground = null;
			system.Shutdown();
		}
	}

	/// <summary><b>With the Confirmations option off FIRE dismisses at once</b>, and the window closes; no box.</summary>
	/// <remarks><b>Mutations:</b> the option not read; the window left up.</remarks>
	[TestMethod]
	public void FireDismissesAtOnceWhereConfirmationsAreOff()
	{
		GameOptions.Current.Confirmations = false;
		var member = Mechanic;

		ParkStaffWindow.Open( _stack, member.ThingId );
		Click( Window!.Root, 0x50c );

		Assert.AreEqual( (false, 0), (_people.IsStaff( member.ThingId ), _stack.Windows.OfType<MessageBox>().Count()) );
		Assert.IsNull( Window );
	}

	/// <summary>
	/// <b>PICK UP puts the member in the hand and closes the window</b> (<c>0x50e</c>, <c>FUN_00505c50</c>): state 7, the
	/// hand naming them, the sprite under the pointer counted, and the member no longer drawn in the park.
	/// </summary>
	/// <remarks><b>Mutations:</b> nobody picked up; the window left up; the preview not counted; the carried member drawn.</remarks>
	[TestMethod]
	public void PickUpPutsTheMemberInTheHand()
	{
		var member = Mechanic;
		member.SetActivity( StaffActivity.Idle, 0 );
		ParkStaffWindow.Open( _stack, member.ThingId );
		Assert.IsFalse( ParkGuestSprites.InTheHand( _people, member.ThingId ), "the control: drawn before" );

		Click( Window!.Root, 0x50e );

		Assert.IsNull( Window, "closed" );
		Assert.AreEqual( (member.ThingId, StaffActivity.Held, 1), (_people.CarriedStaff, member.Activity, Times( "STAFF_PICKED_UP_PREVIEW" )) );
		Assert.IsTrue( ParkGuestSprites.InTheHand( _people, member.ThingId ), "not drawn in the park" );
		Assert.IsFalse( ParkGuestSprites.InTheHand( _people, _people.Staff.First( other => other != member ).ThingId ), "another member is" );
	}

	/// <summary>
	/// <b>A dismissal puts a carried worker back down first</b> (<c>FUN_00505790</c> installs the idle mode over type 6,
	/// <c>0x00505830</c>), where they stand.
	/// </summary>
	/// <remarks><b>Mutations:</b> the carried worker left in the hand.</remarks>
	[TestMethod]
	public void FireWithAWorkerInTheHandPutsThemBackFirst()
	{
		GameOptions.Current.Confirmations = false;
		var carried = _people.Staff.First( member => member.Model == ParkPeople.HandymanModel );
		Assert.IsTrue( _people.PickUp( carried.ThingId ) );

		ParkStaffWindow.Open( _stack, Mechanic.ThingId );
		Click( Window!.Root, 0x50c );

		Assert.AreEqual( (0, StaffActivity.Idle), (_people.CarriedStaff, carried.Activity) );
	}

	/// <summary>
	/// <b>The preview's left click puts the camera on the member and the window stays</b> (<c>FUN_004b5c80</c>,
	/// <c>FUN_0048ce80</c>).
	/// </summary>
	/// <remarks><b>Mutations:</b> the window closed; the camera not moved.</remarks>
	[TestMethod]
	public void ThePreviewsClickGoesToTheMemberAndTheWindowStays()
	{
		var member = Mechanic;
		var (x, y) = member.Navigator.Position.Cell;
		ParkStaffWindow.Open( _stack, member.ThingId );

		Click( Window!.Root, 0x511 );
		Assert.IsNotNull( Window, "kept" );
		Assert.AreEqual( new Vector3( x * 10f, y * 10f, 0f ), ParkOrbitCameraMode.PointOfInterest );
	}

	/// <summary><b>A member gone with the window up closes it at the next refresh</b>, counted.</summary>
	/// <remarks><b>Mutations:</b> the window left up; not counted.</remarks>
	[TestMethod]
	public void AMemberGoneClosesTheWindow()
	{
		Time.PinWall( 0 );
		var member = Mechanic;
		ParkStaffWindow.Open( _stack, member.ThingId );
		Assert.IsTrue( _people.Fire( member.ThingId ) );

		Time.PinWall( 4000 );
		Window!.Update();
		Assert.AreEqual( ((ParkStaffWindow?)null, 1), (Window, Times( "STAFF_WINDOW_MEMBER_GONE" )) );
	}

	/// <summary>
	/// <b>The patrol switch put down hands over the patrol tool</b> (<c>FUN_004b5b50( 1 )</c>): mode <c>0x36</c> for the
	/// member, the window closed, the advisor's <c>0xc1</c> posted (response 412, sample 385); <b>two clicks fence them</b>,
	/// the least corner's cell id first whichever corner is clicked first (<c>FUN_00506e50</c>), and the tool is put away,
	/// which withdraws <c>0xc1</c> (<c>FUN_0052f200</c>). The window shown again has the switch down.
	/// </summary>
	/// <remarks><b>Mutations:</b> no tool or another member; the window left up; <c>0xc1</c> not posted or not withdrawn; the
	/// corners swapped or not ordered; the tool left armed; the first click writing an area.</remarks>
	[TestMethod]
	public void ThePatrolSwitchDownHandsOverTheToolAndTwoClicksFenceTheMember()
	{
		using var clock = new SimulationClockScope();
		var lines = new ParkAdvisorMessages( new SettingsFile( "Advisor/Advisor.sam" ), tutorial: () => true );
		var park = Level.Current!.ParkState!;
		park.AdvisorMessages = lines;
		var said = new List<(int Response, int Sample)>();
		int Play( int response, int sample ) { said.Add( (response, sample) ); return 9000; }

		var member = Mechanic;
		Fence( member, Staff.WholeMapBottomLeft, Staff.WholeMapTopRight );
		ParkStaffWindow.Open( _stack, member.ThingId );
		Click( Window!.Root, 0x510 );

		Assert.IsNull( Window, "the window closed" );
		Assert.AreEqual( (ParkBuildMode.Patrol, member.ThingId, false), (ParkBuildMode.Current, ParkBuildMode.Serves, ParkBuildMode.Anchored) );
		StringAssert.Contains( lines.Census(), "slot=193/subject=0 " );
		Assert.IsFalse( lines.Census().Contains( "slot=192/" ), "0xc0 taken back by the close" );
		lines.Tick( park.GameTick, 0, Play );
		CollectionAssert.AreEqual( new[] { (412, 385) }, said );

		ParkBuildMode.Arm( ParkBuildMode.Patrol, member.ThingId );
		StringAssert.Matches( lines.Census(), new System.Text.RegularExpressions.Regex( @"message=193/\S*/said=1/slaps=0" ), "the same mode again withdraws nothing" );

		StringAssert.Contains( Level.Current!.ClickWorldAt( 56, 28, 0 ), "anchored at (56,28)" );
		Assert.IsTrue( member.PatrolsWholeMap, "the first click writes nothing" );
		Assert.AreEqual( ParkBuildMode.Patrol, ParkBuildMode.Current );

		UiSounds.LastAsked = null;
		StringAssert.Contains( Level.Current!.ClickWorldAt( 39, 21, 0 ), "patrols (39, 21)-(56, 28)" );
		Assert.IsNull( UiSounds.LastAsked, "the click that writes the area plays no sound" );
		Assert.AreEqual( (1 + 39 + 21 * 128, 1 + 56 + 28 * 128), (member.PatrolBottomLeft, member.PatrolTopRight) );
		Assert.AreEqual( (ParkBuildMode.None, false), (ParkBuildMode.Current, ParkBuildMode.Anchored) );
		StringAssert.Contains( lines.Census(), "message=193/score=1000/line=0/" );
		StringAssert.Contains( lines.Census(), "/said=0/slaps=1", "cut off as the tool is put away" );

		ParkStaffWindow.Open( _stack, member.ThingId );
		Assert.IsTrue( ((UiButton)Find( Window!.Root, 0x510 )).IsDown );
		Assert.AreEqual( 0, Times( "STAFF_WINDOW_PATROL" ) );
	}

	/// <summary>
	/// <b>A refused click plays sound <c>0xaf</c> only where its press reached the park</b> (<c>0x00524a77</c>: the flag the
	/// mode's button-down slot sets; a press beside an open park screen never calls that slot). The stock park's band
	/// (45,15)-(53,15) lies wholly outside it; the anchoring click plays nothing.
	/// </summary>
	[TestMethod]
	public void ARefusedPatrolClickIsHeardUnlessItsPressWentToAParkScreen()
	{
		var member = Mechanic;
		var area = (member.PatrolBottomLeft, member.PatrolTopRight);
		_stack.Update();
		Assert.IsFalse( WindowStack.ParkScreenOpen, "no park screen yet" );

		ParkBuildMode.Arm( ParkBuildMode.Patrol, member.ThingId );
		UiSounds.LastAsked = null;
		StringAssert.Contains( Level.Current!.ClickWorldAt( 45, 15, 0 ), "anchored at (45,15)" );
		Assert.IsNull( UiSounds.LastAsked, "the anchor plays nothing" );
		StringAssert.EndsWith( Level.Current!.ClickWorldAt( 53, 15, 0 ), "put away - sound 0xaf, no voice" );
		Assert.AreEqual( 0xaf, UiSounds.LastAsked );

		ParkStaffWindow.Open( _stack, member.ThingId );
		_stack.Update();
		Assert.IsTrue( WindowStack.ParkScreenOpen, "the staff window is a park screen" );

		ParkBuildMode.Arm( ParkBuildMode.Patrol, member.ThingId );
		UiSounds.LastAsked = null;
		StringAssert.Contains( Level.Current!.ClickWorldAt( 45, 15, 0 ), "anchored at (45,15)" );
		StringAssert.EndsWith( Level.Current!.ClickWorldAt( 53, 15, 0 ), "put away - no sound, the press went to a park screen" );
		Assert.IsNull( UiSounds.LastAsked );
		Assert.AreEqual( area, (member.PatrolBottomLeft, member.PatrolTopRight), "refused all the same, nothing written" );

		_stack.Close( Window! );
		_stack.Update();
		Assert.IsFalse( WindowStack.ParkScreenOpen );
	}

	/// <summary>
	/// <b>A tool armed before the window opened refuses in silence beside it</b>: <see cref="Level.ClickWorldAt"/> hands the
	/// click to the armed mode (<c>RunBuildMode</c>), which passes the press's reach on to the tool. The click that arms the path
	/// tool itself (bare ground) runs the tool at another call, so only a tool armed first, by <see cref="ParkBuildMode.Arm"/> for the
	/// path and <see cref="ParkPathBuilding.EditQueue"/> (the Belly Bounce's queue, 13) for the queue, reaches this one.
	/// </summary>
	/// <remarks><b>Mutations:</b> <c>RunPath( cellX, cellY, true )</c> and <c>RunQueue( cellX, cellY, true )</c> at the <c>RunBuildMode</c> call.</remarks>
	[TestMethod]
	public void AToolArmedBeforeTheWindowOpenedRefusesInSilenceBesideIt()
	{
		var mechanic = Mechanic.ThingId;

		foreach ( var screen in new[] { false, true } )
		{
			_stack.Update();
			Assert.IsFalse( WindowStack.ParkScreenOpen, "no park screen yet" );

			StringAssert.StartsWith( ParkPathBuilding.EditQueue( 13 ), "tool: laying queue for thing 13 from (49,22)" );

			if ( screen )
			{
				ParkStaffWindow.Open( _stack, mechanic );
				_stack.Update();
			}

			Assert.AreEqual( screen, WindowStack.ParkScreenOpen );
			var sound = screen ? "no sound, the press went to a park screen" : "sound 0xaf, no voice";

			UiSounds.LastAsked = null;
			Assert.AreEqual( $"world click: queue: nothing laid - (49,18) is type 7, which queue may not be laid over, so the queue tool is put away - {sound}",
				Level.Current!.ClickWorldAt( 52, 18, 0 ), "the queue tool, armed first" );
			Assert.AreEqual( screen ? null : 0xaf, UiSounds.LastAsked );

			// The path tool is armed after the queue's click put its tool away, the window already up or not.
			ParkBuildMode.Arm( ParkBuildMode.Path );
			UiSounds.LastAsked = null;
			Assert.AreEqual( $"world click: path: nothing laid - (43,16) is outside the park, so the path tool is put away - {sound}",
				Level.Current!.ClickWorldAt( 43, 16, 0 ), "the path tool, armed first" );
			Assert.AreEqual( screen ? null : 0xaf, UiSounds.LastAsked );
			Assert.AreEqual( ParkBuildMode.None, ParkBuildMode.Current );

			if ( screen )
			{
				_stack.Close( Window! );
				_stack.Update();
			}
		}

		UiSounds.LastAsked = null;
	}

	/// <summary>
	/// <b>An empty-hand path click leaves the latch its own pass over the clicked cell sets</b>, as the original's hover passes
	/// between the press and the release do (Q535): bare ground (10,10), one cell at 20, with 19 in the bank refuses on the
	/// money (<c>0x00535931</c>) and leaves <see cref="ParkBuildMode.RefusalLatched"/> set, though arming the tool cleared it;
	/// with exactly 20 the cell is affordable but the anchoring click asks twice it (Q1124) and anchors nothing; with 40 the
	/// click anchors and the latch is clear.
	/// </summary>
	/// <remarks><b>Mutations:</b> <c>RunPath</c> not storing its latch; the money test <c>Balance &lt;= owed</c>.</remarks>
	[TestMethod]
	public void AnEmptyHandPathClickLeavesTheLatchOfItsOwnPass()
	{
		var state = Level.Current!.ParkState!;

		state.Spend( state.Balance - 19 );
		ParkBuildMode.RefusalLatched = false;
		StringAssert.Contains( Level.Current.ClickWorldAt( 10, 10, 0 ),
			"path: nothing laid - (10,10) would bring the run to 20 against a balance of 19, so the path tool is put away" );
		Assert.AreEqual( ParkBuildMode.None, ParkBuildMode.Current );
		Assert.IsTrue( ParkBuildMode.RefusalLatched, "the click's own pass refused on the money" );

		// At 20 the strip's cell is affordable, but the anchoring click's own verdict asks twice the cell (Q1124), so it
		// anchors nothing and the tool stays out; at 40 it anchors.
		state.Spend( -1 );
		Assert.AreEqual( 20, state.Balance );
		StringAssert.Contains( Level.Current.ClickWorldAt( 10, 10, 0 ),
			"path: (10,10) would bring the run to 40 against a balance of 20 - nothing anchored, the path tool stays out" );
		Assert.AreEqual( ParkBuildMode.Path, ParkBuildMode.Current );

		state.Spend( -20 );
		Assert.AreEqual( 40, state.Balance );
		Assert.AreEqual( "world click: path: anchored at (10,10) - click again to lay a run", Level.Current.ClickWorldAt( 10, 10, 0 ) );
		Assert.IsFalse( ParkBuildMode.RefusalLatched, "one cell at 20 against 40 is affordable" );
	}

	/// <summary>
	/// <b>A placement's and the path and queue tools' refused clicks play the same sound <c>0xaf</c>, only where the press
	/// reached the park</b>: the commit plays it once ahead of its mode switch (<c>0x00524aae</c>), so every tool shares it.
	/// A click on (43,16), outside the park, arms the path tool and refuses at once; a click on the Belly Bounce's queue
	/// hands back the queue tool anchored on (49,22), and a click on (52,18) runs it to (49,18), over a type 7 cell; the Belly Bounce in the hand,
	/// centred on the pointer's (44,18), will not go down on (43,16) either, and stays in the hand.
	/// </summary>
	[TestMethod]
	public void RefusedBuildClicksAreHeardUnlessThePressWentToAParkScreen()
	{
		var mechanic = Mechanic.ThingId;
		var objects = new ParkObjects( "jungle", null, null );

		try
		{
			foreach ( var screen in new[] { false, true } )
			{
				if ( screen )
					ParkStaffWindow.Open( _stack, mechanic );

				_stack.Update();
				Assert.AreEqual( screen, WindowStack.ParkScreenOpen );
				var sound = screen ? "no sound, the press went to a park screen" : "sound 0xaf, no voice";

				UiSounds.LastAsked = null;
				Assert.AreEqual( $"world click: path: nothing laid - (43,16) is outside the park, so the path tool is put away - {sound}",
					Level.Current!.ClickWorldAt( 43, 16, 0 ) );
				Assert.AreEqual( screen ? null : 0xaf, UiSounds.LastAsked, "the path tool" );
				Assert.AreEqual( ParkBuildMode.None, ParkBuildMode.Current );

				StringAssert.Contains( Level.Current!.ClickWorldAt( 51, 22, 0 ), "from (49,22)" );
				UiSounds.LastAsked = null;
				Assert.AreEqual( $"world click: queue: nothing laid - (49,18) is type 7, which queue may not be laid over, so the queue tool is put away - {sound}",
					Level.Current!.ClickWorldAt( 52, 18, 0 ) );
				Assert.AreEqual( screen ? null : 0xaf, UiSounds.LastAsked, "the queue tool" );
				Assert.AreEqual( ParkBuildMode.None, ParkBuildMode.Current );

				StringAssert.StartsWith( ParkBuilding.Carry( 1100 ), "carry: holding" );
				UiSounds.LastAsked = null;
				var put = Level.Current!.ClickWorldAt( 44, 18, 0 );
				StringAssert.StartsWith( put, "world click: under (44,18): buy: 'Belly Bounce' will not fit at (43,16) - " );
				StringAssert.EndsWith( put, $" - {sound}" );
				Assert.AreEqual( (screen ? null : 0xaf, 1100), (UiSounds.LastAsked, ParkBuilding.Carrying), "a placement, still in the hand" );
				ParkHand.LetGo();
			}
		}
		finally
		{
			ParkHand.LetGo();
			objects.Delete();
			Entity.ApplyDeletions();
		}

		_stack.Close( Window! );
		_stack.Update();
		UiSounds.LastAsked = null;
		StringAssert.Contains( Level.Current!.ClickWorldAt( 10, 10, 0 ), "(10,10)" );
		Assert.AreEqual( (ParkBuildMode.Path, true), (ParkBuildMode.Current, ParkBuildMode.Anchored), "a click on the park's bare ground anchors" );
		Assert.AreEqual( 0x65, UiSounds.LastAsked, "and is not refused: the laying sound" );

		Assert.AreEqual( 0, Times( "PATH_TOOL_REFUSED_SOUND_0xAF" ) + Times( "QUEUE_TOOL_REFUSED_SOUND_0xAF" )
			+ Times( "PLACEMENT_REFUSED_SOUND_0xAF" ), "no longer counted" );
	}

	/// <summary>
	/// <b>A refused click of the track tool is silent beside a park screen, and heard otherwise</b>: the commit plays <c>0xaf</c>
	/// ahead of its mode switch (<c>0x00524a6f</c>..<c>0x00524aae</c>, before <c>0x00524acd</c>), so it gates the track modes as
	/// it does the path, queue and patrol modes. A kart ride's tool is armed, the balance is under the run's price, and a click
	/// is made with and without the staff window up.
	/// </summary>
	/// <remarks><b>Mutations:</b> <c>RunBuildMode</c> hands the track tool <c>true</c> for <c>pressReachedTheTool</c>; its track branch deleted.</remarks>
	[TestMethod]
	public void ARefusedTrackClickIsSilentBesideAParkScreen()
	{
		var level = Level.Current!;
		var state = level.ParkState!;

		typeof( Level ).GetProperty( nameof( Level.Balance ) )!.SetValue( level, new ParkBalance( "jungle", easyMode: true ) );
		Assert.IsTrue( ParkTrackPieces.PieceCost( level, 1 ) > 0, "a run costs something, or the balance cannot refuse it" );

		ParkTrackTool.Forget();
		ParkTrackPieces.LayStation( state, 1, 30, 24, 0 );

		var ride = new ParkWorld.CatalogueObject( state.NextThingId(), 1150, 30 << 8, 24 << 8, 0, TrackRide: state.TrackRides.Take( -4 ) );
		state.AddObject( ride );

		foreach ( var screen in new[] { false, true } )
		{
			if ( screen )
				ParkStaffWindow.Open( _stack, Mechanic.ThingId );

			_stack.Update();
			Assert.AreEqual( screen, WindowStack.ParkScreenOpen );

			state.SetBalance( 0 );
			ParkBuildMode.ArmAt( ParkBuildMode.KartTrack, ride.ThingId, 34, 25 );
			UiSounds.LastAsked = null;

			var said = level.ClickWorldAt( 34, 29, 0 );

			StringAssert.StartsWith( said, "world click: track: refused, " );
			StringAssert.EndsWith( said, screen ? " - no sound, the press went to a park screen, the tool put away"
				: " - sound 0xaf, no voice, the tool put away" );
			Assert.AreEqual( screen ? null : 0xaf, UiSounds.LastAsked, "the track tool" );
			Assert.AreEqual( ParkBuildMode.None, ParkBuildMode.Current );
		}
	}

	/// <summary>
	/// <b>Every path or queue click the red latch has not refused plays the laying sound <c>0x65</c></b>, before its arm
	/// runs and wherever the press went (<c>0x005271ea</c>): the path tool's arming click on (62,18) and its run to (66,18),
	/// the queue tool's run from the Belly Bounce's (49,22) that joins the path at (47,22), and a click on its anchor. The
	/// click on the queue that hands the queue tool back is no click of either tool, and a refused click plays <c>0xaf</c> alone.
	/// The unanchored queue tool sounds before its own verdict (<c>0x0052729d</c>), on a path it refuses too.
	/// </summary>
	/// <remarks><b>Mutations:</b> the sound dropped from either tool or from the unanchored queue click, gated on the press (the path's call, the queue run's, the anchor click's), played on a refusal or another effect, played twice; the unanchored click's red square let through; its preview empty.</remarks>
	[TestMethod]
	public void EveryUnrefusedPathAndQueueClickPlaysTheLayingSound()
	{
		foreach ( var screen in new[] { false, true } )
		{
			if ( screen )
				ParkStaffWindow.Open( _stack, Mechanic.ThingId );

			_stack.Update();
			Assert.AreEqual( screen, WindowStack.ParkScreenOpen );

			ParkBuildMode.Arm( ParkBuildMode.Path );
			UiSounds.LastAsked = null;
			StringAssert.Contains( Level.Current!.ClickWorldAt( 62, 18, 0 ), "anchored at" );
			Assert.AreEqual( 0x65, UiSounds.LastAsked, "the path tool's arming click" );

			UiSounds.LastAsked = null;
			StringAssert.Contains( Level.Current!.ClickWorldAt( 66, 18, 0 ), "path: laid " );
			Assert.AreEqual( 0x65, UiSounds.LastAsked, "the path tool's run" );

			UiSounds.LastAsked = null;
			StringAssert.Contains( Level.Current!.ClickWorldAt( 43, 16, 0 ), "path: nothing laid" );
			Assert.AreEqual( screen ? null : 0xaf, UiSounds.LastAsked, "a refusal plays 0xaf and not the laying sound" );

			if ( !screen )
			{
				UiSounds.LastAsked = null;
				StringAssert.Contains( Level.Current!.ClickWorldAt( 51, 22, 0 ), "from (49,22)" );
				Assert.IsNull( UiSounds.LastAsked, "handing the queue tool back is no tool's click" );

				StringAssert.Contains( Level.Current!.ClickWorldAt( 47, 22, 0 ), "queue: laid 1 from (49,22) and joined the path at (47,22)" );
				Assert.AreEqual( 0x65, UiSounds.LastAsked, "the queue tool's run" );
			}

			UiSounds.LastAsked = null;
			StringAssert.Contains( Level.Current!.ClickWorldAt( 51, 22, 0 ), "from (48,22)" );
			Assert.IsNull( UiSounds.LastAsked, "handing the queue tool back is no tool's click" );

			UiSounds.AskedCount = 0;
			StringAssert.Contains( Level.Current!.ClickWorldAt( 48, 22, 0 ), "queue: clicked the anchor (48,22)" );
			Assert.AreEqual( (0x65, 1), (UiSounds.LastAsked, UiSounds.AskedCount), "the queue tool's click on its anchor, once" );

			// The queue tool's run, beside an open park screen too: armed on the cell where the last run ended.
			StringAssert.Contains( Level.Current!.ClickWorldAt( 51, 22, 0 ), "from (48,22)" );
			UiSounds.LastAsked = null;
			UiSounds.AskedCount = 0;
			var run = Level.Current!.ClickWorldAt( 47, 22, 0 );
			Assert.AreEqual( (0x65, 1), (UiSounds.LastAsked, UiSounds.AskedCount), $"a queue run, once ({run})" );

			// The queue tool armed with no anchor (the console's `tool queue`): the click sounds before its anchor test
			// (0x005271ea, then 0x00527222), on a path its own verdict refuses as on bare ground it anchors.
			// Its one square red, the middle of the queue, refuses as a run does and puts the tool away.
			ParkBuildMode.Arm( ParkBuildMode.Queue, 13 );
			UiSounds.LastAsked = null;
			StringAssert.Contains( Level.Current!.ClickWorldAt( 48, 22, 0 ), "queue: nothing anchored - (48,22) is the middle of this ride's queue" );
			Assert.AreEqual( screen ? null : 0xaf, UiSounds.LastAsked, "a refused unanchored queue click" );
			Assert.AreEqual( ParkBuildMode.None, ParkBuildMode.Current );

			ParkBuildMode.Arm( ParkBuildMode.Queue, 13 );
			UiSounds.LastAsked = null;
			UiSounds.AskedCount = 0;
			StringAssert.Contains( Level.Current!.ClickWorldAt( 47, 22, 0 ), "queue: (47,22) is a path" );
			Assert.AreEqual( (0x65, 1), (UiSounds.LastAsked, UiSounds.AskedCount), "an unanchored queue click on a path, once" );

			UiSounds.LastAsked = null;
			UiSounds.AskedCount = 0;
			StringAssert.Contains( Level.Current!.ClickWorldAt( 11, 10, 0 ), "queue: anchored at (11,10)" );
			Assert.AreEqual( (0x65, 1), (UiSounds.LastAsked, UiSounds.AskedCount), "an unanchored queue click, once" );
			ParkBuildMode.Disarm();
		}

		_stack.Close( Window! );
		_stack.Update();
		Assert.AreEqual( 0, Times( "PATH_TOOL_CLICK_SOUND_0x65" ), "no longer counted" );
	}

	/// <summary>
	/// <b>The unanchored queue click is charged the queue price twice</b> (<c>ParkPathBuilding.RunQueue</c> into
	/// <c>FirstQueueClick</c>; docs/exe/park-engine.md, "The unanchored click"): on the bare ground at (11,10) a balance of
	/// twice the queue price less one refuses and anchors nothing, and twice the queue price anchors. The path tool's
	/// price is not the queue's, so either in the queue's place would anchor on the first.
	/// </summary>
	/// <remarks><b>Mutations:</b> <c>RunQueue</c> handing <c>FirstQueueClick</c> the path's price (<c>CellCost</c>).</remarks>
	[TestMethod]
	public void AnUnanchoredQueueClickAsksTwiceTheQueuePrice()
	{
		var level = Level.Current!;
		var state = level.ParkState!;
		var price = ParkPathBuilding.QueueCost( level );
		Assert.AreNotEqual( price, ParkPathBuilding.CellCost( level ), "the two tools' prices differ" );

		ParkBuildMode.Arm( ParkBuildMode.Queue, 13 );
		state.SetBalance( 2 * price - 1 );
		StringAssert.Contains( level.ClickWorldAt( 11, 10, 0 ),
			$"queue: (11,10) would bring the run to {2 * price} against a balance of {2 * price - 1} - nothing anchored" );
		Assert.IsFalse( ParkBuildMode.Anchored );

		state.SetBalance( 2 * price );
		StringAssert.Contains( level.ClickWorldAt( 11, 10, 0 ), "queue: anchored at (11,10)" );
		ParkBuildMode.Disarm();
	}

	/// <summary>
	/// <b>The patrol switch let up gives the member the whole map</b> (<c>FUN_004b5b50( 0 )</c>, <c>FUN_00506e90</c>: cells 1
	/// to <c>0x4000</c>); the window stays and no tool is armed.
	/// </summary>
	/// <remarks><b>Mutations:</b> the area left; another pair of cells; the window closed; the tool armed.</remarks>
	[TestMethod]
	public void ThePatrolSwitchUpGivesTheWholeMap()
	{
		var member = Mechanic;
		Fence( member, 1 + 39 + 21 * 128, 1 + 56 + 28 * 128 );
		ParkStaffWindow.Open( _stack, member.ThingId );
		var window = Window!;
		Click( window.Root, 0x510 );

		Assert.AreEqual( (1, 0x4000), (member.PatrolBottomLeft, member.PatrolTopRight) );
		Assert.AreSame( window, Window );
		Assert.IsFalse( ((UiButton)Find( window.Root, 0x510 )).IsDown );
		Assert.AreEqual( ParkBuildMode.None, ParkBuildMode.Current );
	}

	/// <summary>
	/// <b>The tool's outline</b> (<c>FUN_005234d0</c>, mode <c>0x36</c>): one blue square unanchored, then the rectangle's
	/// edges in the patrol edges' order, <b>all red where no cell of it is in the park</b> (<c>FUN_00532a50</c>: outside the
	/// park, or track type <c>0x19</c>, its own or its parent's), and then the click puts the tool away writing nothing, the
	/// sound counted.
	/// </summary>
	/// <remarks><b>Mutations:</b> the outside flag or the track type not read; the parent not followed; one good cell not
	/// enough; the unanchored square red; the refused click writing an area or leaving the tool armed.</remarks>
	[TestMethod]
	public void TheOutlineIsRedWhereNoCellIsInThePark()
	{
		var member = Mechanic;
		var state = Level.Current!.ParkState!;
		var park = Level.Current!.Park!;
		Fence( member, Staff.WholeMapBottomLeft, Staff.WholeMapTopRight );
		ParkBuildMode.Arm( ParkBuildMode.Patrol, member.ThingId );

		var one = ParkPatrolTool.Strip( 2, 2, out var unanchored );
		Assert.AreEqual( (1, 2, 2, ParkPathBuilding.MarkerBlue), (one.Count, one[0].X, one[0].Y, one[0].Marker) );
		Assert.IsFalse( unanchored, "unanchored, its pass sets no latch" );

		var cells = new[] { (2, 2), (3, 2), (2, 3), (3, 3) };
		var parent = state.Record( 5, 5 ) with { Flags = 0, TrackType = 0x19 };
		state.SetRecord( 5, 5, parent );

		foreach ( var (x, y) in cells )
			state.SetRecord( x, y, state.Record( x, y ) with { Flags = (ushort)ParkPathBuilding.OutsideThePark, TrackType = 0 } );

		state.SetRecord( 3, 3, state.Record( 3, 3 ) with { Flags = 0, TrackType = 0x19 } );
		state.SetRecord( 2, 3, state.Record( 2, 3 ) with { Flags = 0, TrackType = 12, TrackParentId = (ushort)MapStep.CellId( 5, 5 ) } );
		Assert.IsTrue( ParkPatrolTool.Refuses( park, (2, 2), (3, 3) ) );

		ParkBuildMode.AnchorAt( 3, 3 );
		CollectionAssert.AreEqual( Enumerable.Repeat( (3, 3, ParkPathBuilding.MarkerRed), 4 ).ToList(),
			ParkPathBuilding.Strip( 3, 3 ).Select( square => (square.X, square.Y, square.Marker) ).ToList(), "the anchored cell, a line each" );
		Assert.AreEqual( Input.CursorTypes.NoGo, Level.PatrolCursor( 2, 2 ), "the refusal's pointer" );
		var red = ParkPathBuilding.Strip( 2, 2, out var latched );
		CollectionAssert.AreEqual( ParkLocator.PatrolEdgeCells( (2, 2), (3, 3) ), red.Select( square => (square.X, square.Y) ).ToList() );
		Assert.IsTrue( red.All( square => square.Marker == ParkPathBuilding.MarkerRed ) );
		Assert.IsTrue( latched, "the rectangle's test sets the latch its pass leaves (0x00523dd3)" );

		StringAssert.Contains( ParkPatrolTool.Run( 2, 2 ), "takes in no cell of the park" );
		Assert.AreEqual( (ParkBuildMode.None, true), (ParkBuildMode.Current, member.PatrolsWholeMap) );

		ParkBuildMode.Arm( ParkBuildMode.Patrol, member.ThingId );
		ParkBuildMode.AnchorAt( 3, 3 );
		UiSounds.LastAsked = null;
		StringAssert.EndsWith( ParkPatrolTool.Run( 2, 2 ), "- sound 0xaf, no voice", "the refusal's sound, asked for (no mixer here)" );
		Assert.AreEqual( 0xaf, UiSounds.LastAsked );
		ParkBuildMode.Arm( ParkBuildMode.Patrol, member.ThingId );
		ParkBuildMode.AnchorAt( 3, 3 );
		UiSounds.LastAsked = null;
		StringAssert.EndsWith( ParkPatrolTool.Run( 2, 2, pressReachedTheTool: false ), "- no sound, the press went to a park screen" );
		Assert.IsNull( UiSounds.LastAsked );
		Assert.AreEqual( (ParkBuildMode.None, true), (ParkBuildMode.Current, member.PatrolsWholeMap), "refused in silence all the same" );

		state.SetRecord( 2, 3, state.Record( 2, 3 ) with { TrackParentId = 0 } );
		Assert.IsFalse( ParkPatrolTool.Refuses( park, (2, 2), (3, 3) ), "one cell of the park is enough" );

		// The anchor's own cell refused, then a rectangle taking in a park cell: blue, and the pointer stays c_noplace
		// (FUN_0052f950 sets cursor 8 on d48 and nothing else in mode 0x36), until the tool is put away.
		ParkBuildMode.Arm( ParkBuildMode.Patrol, member.ThingId );
		ParkBuildMode.AnchorAt( 3, 3 );
		Assert.AreEqual( Input.CursorTypes.Normal, Level.PatrolCursor(), "armed: the ordinary pointer" );
		Assert.AreEqual( Input.CursorTypes.NoGo, Level.PatrolCursor( 3, 3 ), "the anchor's cell alone is refused" );
		Assert.IsTrue( ParkPatrolTool.Strip( 2, 2 ).All( square => square.Marker == ParkPathBuilding.MarkerBlue ) );
		Assert.AreEqual( Input.CursorTypes.NoGo, Level.PatrolCursor( 2, 2 ), "a blue outline keeps the refusal's pointer" );
		Assert.AreEqual( Input.CursorTypes.NoGo, Level.PatrolCursor(), "off the cells too" );

		ParkBuildMode.Disarm();
		ParkBuildMode.Arm( ParkBuildMode.Patrol, member.ThingId );
		ParkBuildMode.AnchorAt( 3, 3 );
		Assert.IsTrue( ParkPatrolTool.Strip( 2, 2, out var accepted ).All( square => square.Marker == ParkPathBuilding.MarkerBlue ) );
		Assert.IsFalse( accepted, "an accepted pass leaves the latch clear" );
		Assert.AreEqual( Input.CursorTypes.Normal, Level.PatrolCursor( 2, 2 ), "no cursor of its own" );
		ParkPatrolTool.Run( 2, 2 );
		Assert.AreEqual( (MapStep.CellId( 2, 2 ), MapStep.CellId( 3, 3 )), (member.PatrolBottomLeft, member.PatrolTopRight) );
	}

	/// <summary>
	/// <b>The patrol tool's refused pointer outlives arming the tool over itself</b> (<c>FUN_0046d5a0</c> and the setter
	/// <c>FUN_0052f200</c> set no cursor for mode <c>0x36</c>; <c>docs/exe/hud.md</c>, "The patrol pointer"), and is cleared by
	/// putting the tool away or arming another; off the cells <c>Level.WantedCursor</c> wears it.
	/// </summary>
	/// <remarks><b>Mutations:</b> <c>Arm</c> clearing the flag on every call; <c>WantedCursor</c> returning the ordinary
	/// pointer off the cells with the tool armed.</remarks>
	[TestMethod]
	public void ArmingThePatrolToolAgainKeepsTheRefusedPointer()
	{
		ParkPicking.Forget();
		ParkBuildMode.Arm( ParkBuildMode.Patrol, 7 );
		ParkPatrolTool.PointerRefused = true;
		Assert.AreEqual( Input.CursorTypes.NoGo, Level.Current!.WantedCursor(), "off the cells, armed: the refusal's pointer" );

		ParkBuildMode.Arm( ParkBuildMode.Patrol, 7 );
		Assert.IsTrue( ParkPatrolTool.PointerRefused, "armed over itself: no cursor is set, the flag stands" );
		Assert.AreEqual( Input.CursorTypes.NoGo, Level.PatrolCursor() );
		Assert.AreEqual( Input.CursorTypes.NoGo, Level.Current!.WantedCursor() );

		ParkBuildMode.Disarm();
		Assert.IsFalse( ParkPatrolTool.PointerRefused, "put away: cleared" );

		ParkBuildMode.Arm( ParkBuildMode.Patrol, 7 );
		ParkPatrolTool.PointerRefused = true;
		ParkBuildMode.Arm( ParkBuildMode.Path );
		Assert.IsFalse( ParkPatrolTool.PointerRefused, "another tool armed: cleared" );
		ParkBuildMode.Arm( ParkBuildMode.Patrol, 7 );
		Assert.IsFalse( ParkPatrolTool.PointerRefused, "and arming the patrol tool from it starts clear" );
	}

	/// <summary>
	/// <b>Escape, or a quick right click with RMB cancel on, puts the patrol tool away and writes nothing</b>, anchored or
	/// not: the game table's row 0 (<c>0x0040c368</c>; over any mode but 0 and 1, where the menu opens) and the right button's armed
	/// release (<c>0x0048842b</c>; over any) each install the idle mode, so the anchor goes, the member keeps the area the switch left, <c>0xc1</c> is withdrawn
	/// and no menu opens. The original's, both ways: mode 0, the anchor -1, the area 1 and 16384 (<c>docs/exe/hud.md</c>,
	/// "The patrol switch and the patrol tool").
	/// </summary>
	/// <remarks><b>Mutations:</b> a way out leaving the tool armed or opening the menu; the anchor kept; an area written;
	/// <c>0xc1</c> not withdrawn.</remarks>
	[TestMethod]
	public void EscapeOrAQuickRightClickWithThePatrolToolArmedPutsItAwayAndWritesNothing()
	{
		using var clock = new SimulationClockScope();
		var lines = new ParkAdvisorMessages( new SettingsFile( "Advisor/Advisor.sam" ), tutorial: () => true );
		Level.Current!.ParkState!.AdvisorMessages = lines;
		GameOptions.Current.RmbCancel = true;
		var member = Mechanic;

		foreach ( var (way, anchored) in new[] { ("Escape", false), ("Escape", true), ("a quick right click", false), ("a quick right click", true) } )
		{
			Fence( member, Staff.WholeMapBottomLeft, Staff.WholeMapTopRight );
			ParkStaffWindow.Open( _stack, member.ThingId );
			Click( Window!.Root, 0x510 );
			Assert.AreEqual( ParkBuildMode.Patrol, ParkBuildMode.Current, $"armed, {way}, anchored {anchored}" );
			StringAssert.Contains( lines.Census(), "slot=193/", $"0xc1 posted, {way}, anchored {anchored}" );

			if ( anchored )
				StringAssert.Contains( Level.Current!.ClickWorldAt( 51, 27, 0 ), "anchored at (51,27)" );

			if ( way == "Escape" )
				Escape();
			else
				Assert.AreEqual( "world click: right click - the build tool is put away", QuickRightClick(), $"anchored {anchored}" );

			Assert.AreEqual( (ParkBuildMode.None, false), (ParkBuildMode.Current, ParkBuildMode.Anchored), $"put away, {way}, anchored {anchored}" );
			Assert.AreEqual( (1, 0x4000), (member.PatrolBottomLeft, member.PatrolTopRight), $"nothing written, {way}, anchored {anchored}" );
			Assert.IsFalse( lines.Census().Contains( "slot=193/" ), $"0xc1 withdrawn, {way}, anchored {anchored}" );
			Assert.IsFalse( _stack.Windows.OfType<GameMenu>().Any(), $"no menu, {way}, anchored {anchored}" );
		}
	}

	/// <summary>A press and a release of the right button 50 ms apart at one point on the park: a quick click.</summary>
	private static string? QuickRightClick()
	{
		var at = new Vector2( 1024, 600 );
		Time.PinWall( 50000 );
		Level.Current!.RightButton( true, at );
		Time.PinWall( 50050 );

		return Level.Current!.RightButton( false, at );
	}

	/// <summary>Escape let go with no modifier, through the park front end's own handler on this stack and gadget.</summary>
	private void Escape()
	{
		var frontEnd = RuntimeHelpers.GetUninitializedObject( typeof( ParkFrontEnd ) );
		var flags = BindingFlags.Instance | BindingFlags.NonPublic;
		typeof( ParkFrontEnd ).GetField( "_stack", flags )!.SetValue( frontEnd, _stack );
		typeof( ParkFrontEnd ).GetField( "_gadget", flags )!.SetValue( frontEnd, _gadget );
		typeof( ParkFrontEnd ).GetMethod( "MenuKey", flags )!.Invoke( frontEnd, [_stack.Windows[^1], true] );
	}

	private static int Bar( ParkStaffWindow window, int id ) => ((ParkObjectWindow.UiStatBar)Find( window.Root, id )).Units;

	private static void Fence( Staff member, int bottomLeft, int topRight ) => member.SetPatrolArea( bottomLeft, topRight );

	private static UiControl Find( UiControl root, int id )
		=> FindOrNull( root, id ) ?? throw new AssertFailedException( $"no control {id:x}" );

	private static UiControl? FindOrNull( UiControl root, int id )
		=> root.Id == id ? root : root.Children.Select( child => FindOrNull( child, id ) ).FirstOrDefault( found => found != null );
}
