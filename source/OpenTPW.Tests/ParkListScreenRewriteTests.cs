using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using OpenTPW.UI;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace OpenTPW.Tests;

/// <summary>
/// The all-staff and all-items screens rewrite their rows in place every two seconds of real time, as the original's
/// timer <c>0x80083</c> does (<c>docs/exe/hud.md</c>, "How allstaff and allitems keep their rows current"), and frame
/// their headings as its openers do. Reads the shipped park; skipped without the game (<see cref="GameData"/>).
/// </summary>
[TestClass]
public class ParkListScreenRewriteTests
{
	private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

	private Level? before;
	private Point2 size;
	private ParkWorld world = null!;
	private ParkState state = null!;
	private ParkPeople people = null!;
	private Level level = null!;
	private WindowStack stack = null!;
	private readonly List<string> lines = [];

	[TestInitialize]
	public void OpenThePark()
	{
		before = Level.Current;
		size = Screen.Size;
		Screen.Size = new( 2048, 1536 );
		Log ??= new();
		FileSystem = GameData.Required();

		using var stream = new MemoryStream( FileSystem.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		world = new ParkWorld( new SaveReader( stream ).ReadFile() );
		state = new( world );

		var balance = new ParkBalance( "jungle", easyMode: true );
		var catalogue = new ParkItemCatalogue( "jungle", FileSystem );

		level = (Level)RuntimeHelpers.GetUninitializedObject( typeof( Level ) );
		typeof( Level ).GetProperty( nameof( Level.Park ) )!.SetValue( level, world );
		typeof( Level ).GetProperty( nameof( Level.ParkState ) )!.SetValue( level, state );
		typeof( Level ).GetProperty( nameof( Level.Catalogue ) )!.SetValue( level, catalogue );
		typeof( Level ).GetProperty( nameof( Level.StaffPool ) )!.SetValue( level, Level.StaffPoolFor( world, balance, world.GameTick ) );
		Level.Current = level;

		people = new( world, balance, null, state, catalogue );

		stack = (WindowStack)RuntimeHelpers.GetUninitializedObject( typeof( WindowStack ) );
		typeof( WindowStack ).GetField( "_windows", Private )!.SetValue( stack, new List<UiWindow>() );

		Logger.OnLog += Heard;
	}

	[TestCleanup]
	public void PutBack()
	{
		Logger.OnLog -= Heard;
		Time.PinWall( 0 );
		people?.Delete();
		Entity.ApplyDeletions();
		Level.Current = before!;
		Screen.Size = size;
	}

	private void Heard( Logger.Level severity, string line ) => lines.Add( line );

	private int Rewrites( string screen ) => lines.Count( line => line.StartsWith( screen ) && line.Contains( "rows rewritten" ) );

	private static int Times( string what ) => Unimplemented.Summary.FirstOrDefault( entry => entry.What == what ).Times;

	private static T Field<T>( object screen, string name ) => (T)screen.GetType().GetField( name, Private )!.GetValue( screen )!;

	/// <summary>
	/// <b>The all-staff list rewrites each row's status, skill and happiness every two seconds from its opening, and
	/// leaves the name, the wage and both meters as they were</b> (<c>FUN_00495da0</c>, mask <c>0x1a</c>); a tab does not
	/// restart the timer, armed once by the opener.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> no rewrite; the rewrite a frame early; the wage rewritten too; the status, the skill or the
	/// happiness kept; the meters moved every frame, by the rewrite or every member's by a tab; a tab restarting the timer.
	/// </remarks>
	[TestMethod]
	public void TheStaffListRewritesStatusSkillAndHappinessEveryTwoSecondsAndNotTheWageOrTheMeters()
	{
		var member = people.Staff[0];
		var kind = ParkStaffPool.KindFor( member.Model );
		var pool = level.StaffPool!;
		var grade = member.PayGrade == 0 ? 1 : 0;

		Assert.AreNotEqual( pool.WageFor( kind, member.PayGrade ), pool.WageFor( kind, grade ), "two grades, two wages" );

		typeof( ParkStaffScreen ).GetField( "_lastKind", BindingFlags.Static | BindingFlags.NonPublic )!.SetValue( null, kind );

		// Opened five seconds into real time: the rewrite is two seconds from there, not from nought.
		Time.PinWall( 5000 );

		var screen = new ParkStaffScreen( stack );
		var list = Field<UiList>( screen, "_list" );
		var kindMeter = Field<ParkObjectWindow.UiStatBar>( screen, "_kindHappiness" );
		var allMeter = Field<ParkObjectWindow.UiStatBar>( screen, "_allHappiness" );
		var row = list.Rows.ToList().FindIndex( each => each.Id == member.ThingId );
		var opened = list.Rows[row].Values!.ToArray();
		var openedNumbers = list.Rows[row].Numbers!.ToArray();
		var (kindAtOpening, allAtOpening) = (kindMeter.Units, allMeter.Units);

		var activity = member.Activity == StaffActivity.Held ? StaffActivity.Idle : StaffActivity.Held;

		member.PayGrade = grade;
		member.Happiness = member.Happiness < 50 ? 90 : 10;
		typeof( Staff ).GetProperty( nameof( Staff.Activity ) )!.SetValue( member, activity );

		Time.PinWall( 6999 );
		screen.Update();
		CollectionAssert.AreEqual( opened, list.Rows[row].Values!.ToArray(), "1999 ms: not rewritten" );
		CollectionAssert.AreEqual( openedNumbers, list.Rows[row].Numbers!.ToArray(), "nor its bars" );
		Assert.AreEqual( kindAtOpening, kindMeter.Units, "the kind's meter stands between rewrites" );
		Assert.AreEqual( allAtOpening, allMeter.Units, "and every member's" );

		Time.PinWall( 7000 );
		screen.Update();
		var now = list.Rows[row].Values!;
		var numbers = list.Rows[row].Numbers!;
		Assert.AreNotEqual( ParkStaffScreen.StatusOf( member ), opened[0] );
		Assert.AreEqual( ParkStaffScreen.StatusOf( member ), now[0], "the status rewritten" );
		Assert.AreEqual( opened[1], now[1], "the wage as the fill wrote it" );
		Assert.AreEqual( openedNumbers[2], numbers[2], "and its number" );
		Assert.AreEqual( (ParkStaffScreen.SkillOf( member ) << 10) / 100, numbers[3], "the skill's bar rewritten" );
		Assert.AreNotEqual( openedNumbers[3], numbers[3] );
		Assert.AreEqual( (((int)member.Happiness & 0xff) << 10) / 100, numbers[4], "the happiness's bar rewritten" );
		Assert.AreNotEqual( openedNumbers[4], numbers[4] );
		Assert.AreEqual( member.ThingId, list.Rows[row].Id );
		Assert.AreEqual( kindAtOpening, kindMeter.Units, "the rewrite leaves the kind's meter" );
		Assert.AreEqual( allAtOpening, allMeter.Units, "and every member's" );
		Assert.AreEqual( 1, Rewrites( "Staff list" ) );

		// A tab at 7500: the timer keeps the opening's phase, so the next rewrite is at 9000.
		Time.PinWall( 7500 );
		var tabs = Field<UiRadioGroup>( screen, "_tabs" );
		tabs.Select( tabs.Children.OfType<UiButton>().First( option => option.Id != tabs.Selected ).Id );
		screen.Update();
		Assert.AreEqual( allAtOpening, allMeter.Units, "a tab sets the kind's meter alone" );

		Time.PinWall( 8999 );
		screen.Update();
		Assert.AreEqual( 1, Rewrites( "Staff list" ), "8999 ms: not yet" );

		Time.PinWall( 9000 );
		screen.Update();
		Assert.AreEqual( 2, Rewrites( "Staff list" ), "9000 ms: two seconds from the last, not from the tab" );
	}

	/// <summary>
	/// <b>The all-staff list keeps one sort for the session, the name rising at first</b> (<c>[0x0075118c]</c>, 1, handed
	/// to <c>FUN_006659af</c> by the opener <c>0x004966fb</c> and written on <c>0x406</c> at <c>0x00495fe0</c>): a kind's
	/// rows stand in name order whatever order they were hired in, capitals before small letters; Monthly Wage clicked
	/// sorts by wage rising with equal wages as they stood, again falling; Current Status sorts as text; and the screen
	/// opened again, on another tab, keeps the word.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> no sort handed over; the word not kept; the status sorted as a number; the wage as text; the
	/// sort started at another column.
	/// </remarks>
	[TestMethod]
	public void TheStaffListSortsByNameThenByTheHeadingClickedAndKeepsItsSort()
	{
		var sort = typeof( ParkStaffScreen ).GetField( "_sort", BindingFlags.Static | BindingFlags.NonPublic )!;
		var lastKind = typeof( ParkStaffScreen ).GetField( "_lastKind", BindingFlags.Static | BindingFlags.NonPublic )!;
		var counted = Times( "LIST_HEADING_SORT_NOT_BUILT" );

		try
		{
			Assert.AreEqual( 1, (int)sort.GetValue( null )! );

			// Hired out of name order: "Zed", then "bea" (a small letter, after every capital), then "Abe".
			foreach ( var (id, name, grade, x) in new[] { (997, "Zed Test", 0, 47), (998, "bea Test", 0, 50), (999, "Abe Test", 2, 55) } )
				Assert.AreNotEqual( 0, people.Hire( new ParkStaffPool.Candidate( Id: id, Kind: 0, Name: name, Grade: grade, Costume: 0, Wage: 0 ), x, 30 ) );

			lastKind.SetValue( null, 0 );
			var screen = new ParkStaffScreen( stack );
			var list = Field<UiList>( screen, "_list" );

			string[] Names() => list.Rows.Select( row => row.Name ).ToArray();
			UiButton Heading( int column ) => (UiButton)list.Children.Single( child => child.Id == 0x10 + column );

			var names = Names();
			Assert.AreEqual( 4, names.Length, "the stock handyman and three hires" );
			CollectionAssert.AreEqual( names.OrderBy( name => name, StringComparer.Ordinal ).ToArray(), names, "by name rising" );
			Assert.AreEqual( "Abe Test", names[0] );
			Assert.AreEqual( "bea Test", names[^1], "a small letter after every capital" );

			// OrderBy and OrderByDescending keep equal rows as they stood, as FUN_006628f4 does.
			var byName = list.Rows.ToArray();

			Heading( 2 ).Clicked!();
			Assert.AreEqual( 3, (int)sort.GetValue( null )!, "the wage, rising: kept" );
			var rising = byName.OrderBy( row => row.Numbers![2] ).Select( row => row.Name ).ToArray();
			CollectionAssert.AreEqual( rising, Names(), "by wage rising, equal wages in name order" );
			Assert.IsTrue( Names().ToList().IndexOf( "Zed Test" ) + 1 == Names().ToList().IndexOf( "bea Test" ), "the two of grade 0 side by side, Zed first" );
			Assert.AreNotEqual( "Abe Test", Names()[0], "a dearer wage than grade 0's" );

			Heading( 2 ).Clicked!();
			Assert.AreEqual( -3, (int)sort.GetValue( null )! );
			var falling = list.Rows.ToArray();
			CollectionAssert.AreEqual( falling.OrderByDescending( row => row.Numbers![2] ).ToArray(), falling, "falling" );
			Assert.IsTrue( Names().ToList().IndexOf( "Zed Test" ) + 1 == Names().ToList().IndexOf( "bea Test" ), "equal wages still as they stood" );

			Heading( 1 ).Clicked!();
			Assert.AreEqual( 2, (int)sort.GetValue( null )! );
			var statuses = list.Rows.Select( row => row.Values![0] ).ToArray();
			CollectionAssert.AreEqual( statuses.OrderBy( status => status, StringComparer.Ordinal ).ToArray(), statuses, "the status as text" );

			Heading( 2 ).Clicked!();
			Heading( 2 ).Clicked!();
			lastKind.SetValue( null, 1 );
			var again = Field<UiList>( new ParkStaffScreen( stack ), "_list" );
			Assert.AreEqual( -3, again.SortWord, "kept by another opening, on another tab" );

			// A mechanic of grade 2 is paid three digits, "$ 115", which as text would stand before "$ 92".
			foreach ( var (id, name, grade, x) in new[] { (995, "Cy Test", 1, 47), (996, "Di Test", 2, 50) } )
				Assert.AreNotEqual( 0, people.Hire( new ParkStaffPool.Candidate( Id: id, Kind: 1, Name: name, Grade: grade, Costume: 0, Wage: 0 ), x, 31 ) );

			sort.SetValue( null, 3 );
			var mechanics = Field<UiList>( new ParkStaffScreen( stack ), "_list" );
			var paid = mechanics.Rows.Select( row => row.Numbers![2] ).ToArray();
			Assert.IsTrue( paid.Any( wage => wage >= 100 ) && paid.Any( wage => wage < 100 ), "two and three digits" );
			CollectionAssert.AreEqual( paid.Order().ToArray(), paid, "the wage sorted as a number" );

			Assert.AreEqual( counted, Times( "LIST_HEADING_SORT_NOT_BUILT" ), "no click counted" );
		}
		finally
		{
			sort.SetValue( null, 1 );
			lastKind.SetValue( null, 0 );
		}
	}

	/// <summary>
	/// <b>The all-staff row's cells are the styler <c>FUN_00496d20</c>'s</b>: the wage lettered as a money cell, the skill a
	/// bar in <c>ridestatbar.wct</c> and the happiness a bar in <c>happygrad.wct</c>, each inset 8, filled from the row
	/// adder's <c>(skill &lt;&lt; 10) / 100</c> and <c>((happiness &amp; 0xff) &lt;&lt; 10) / 100</c>; and the two meters
	/// are the same skin in <c>happygrad.wct</c>, inset 8 (<c>docs/exe/hud.md</c>, "The all-staff rows: a money cell, then
	/// bars").
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> the skill as the grade's fifths alone; the percentage left out or not a byte; the happiness not
	/// a byte; the wage as a bare number; the happiness in the ride bars' skin; an inset of 10; the meters not inset or
	/// in the ride bars' skin.
	/// </remarks>
	[TestMethod]
	public void TheStaffRowsLetterTheWageAsMoneyAndDrawSkillAndHappinessAsTheOriginalsBars()
	{
		var member = people.Staff[0];
		var kind = ParkStaffPool.KindFor( member.Model );

		member.PayGrade = 3;
		member.PercentageThroughGrade = 0x10f;
		member.Happiness = 300.75f;

		typeof( ParkStaffScreen ).GetField( "_lastKind", BindingFlags.Static | BindingFlags.NonPublic )!.SetValue( null, kind );

		var screen = new ParkStaffScreen( stack );
		var list = Field<UiList>( screen, "_list" );
		var row = list.Rows.First( each => each.Id == member.ThingId );
		var wage = level.StaffPool!.WageFor( kind, 3 );

		Assert.AreEqual( 63, ParkStaffScreen.SkillOf( member ), "(3 x 100 + 15) / 5: the percentage's low byte" );
		CollectionAssert.AreEqual( new[] { 0, 0, wage, 645, 450 }, row.Numbers!.ToArray(),
			"the skill's (63 << 10) / 100 and the happiness's ((300 & 0xff) << 10) / 100" );
		Assert.AreEqual( $"$ {wage}", row.Values![1], "the wage in a money cell" );

		member.PayGrade = 4;
		member.PercentageThroughGrade = 99;
		Assert.AreEqual( 99, ParkStaffScreen.SkillOf( member ), "(400 + 99) / 5 truncated" );

		var bars = list.Children.OfType<ParkObjectWindow.UiStatBar>().ToArray();
		var top = bars.Min( bar => bar.Rect.Top );
		var first = bars.Where( bar => bar.Rect.Top == top ).OrderBy( bar => bar.Rect.Left ).ToArray();

		Assert.AreEqual( 2, first.Length, "two bar cells a row" );
		Assert.AreEqual( (1231, ParkObjectWindow.UiStatBar.RideStatBar), (first[0].Rect.Left, first[0].Skin), "the skill" );
		Assert.AreEqual( (1488, ParkObjectWindow.UiStatBar.HappyGrad), (first[1].Rect.Left, first[1].Skin), "the happiness" );
		Assert.AreEqual( 418 + 8, top, "inset 8 from the row's top" );
		Assert.AreEqual( member.ThingId, list.Rows[0].Id, "the member stands first" );
		Assert.AreEqual( 645, first[0].Units, "the first row's skill bar filled" );
		Assert.AreEqual( 450, first[1].Units, "and its happiness bar" );

		foreach ( var (name, rect) in new[] { ("_kindHappiness", new UiRect( 1191, 840, 1567, 869 )), ("_allHappiness", new UiRect( 1191, 891, 1567, 920 )) } )
		{
			var meter = Field<ParkObjectWindow.UiStatBar>( screen, name );
			Assert.AreEqual( rect, meter.Rect, $"{name} inset 8" );
			Assert.AreEqual( ParkObjectWindow.UiStatBar.HappyGrad, meter.Skin, name );
			Assert.IsTrue( meter.Known, $"{name} drawn at any value" );
		}
	}

	/// <summary>
	/// <b>The all-staff list letters each member's status from <c>STAFFSTATES.str</c> as the row adder picks its row</b>
	/// (<c>FUN_005064f0</c>): Idle, a walk Patrolling, or Idle where the patrol area is the whole map (<c>FUN_00506eb0</c>,
	/// cells 1 to <c>0x4000</c>; or none, this game's hire's), a rest and the way to it Resting, a strike and the way to it On
	/// strike, carried Picked up, anything else Working (<c>docs/exe/hud.md</c>, "The all-staff status words").
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> the activity's own name; a walk always Patrolling; the whole map's corners not both tested; no area
	/// read as an area; the way to a rest or a strike Working; Waiting not Working; the rows shifted by one.
	/// </remarks>
	[TestMethod]
	public void TheStaffListLettersEachStatusFromTheGamesOwnTableAsTheOriginalsAdder()
	{
		CollectionAssert.AreEqual( new[] { "Idle", "Patrolling", "Working", "Resting", "On strike", "Picked up" },
			new StringFile( ParkStaffScreen.StatusTable ).Entries, "the table's six rows" );

		var patrolling = people.Staff.First( each => each.HasPatrolArea && !each.PatrolsWholeMap );
		var anywhere = people.Staff.First( each => each.PatrolBottomLeft == 1 && each.PatrolTopRight == 0x4000 );

		void Set( Staff member, StaffActivity activity ) => typeof( Staff ).GetProperty( nameof( Staff.Activity ) )!.SetValue( member, activity );

		void Patrol( Staff member, int from, int to )
		{
			typeof( Staff ).GetField( $"<{nameof( Staff.PatrolBottomLeft )}>k__BackingField", Private )!.SetValue( member, from );
			typeof( Staff ).GetField( $"<{nameof( Staff.PatrolTopRight )}>k__BackingField", Private )!.SetValue( member, to );
		}

		var words = new Dictionary<StaffActivity, string>
		{
			[StaffActivity.Idle] = "Idle", [StaffActivity.Walking] = "Patrolling", [StaffActivity.GoingToRest] = "Resting",
			[StaffActivity.Resting] = "Resting", [StaffActivity.GoingOnStrike] = "On strike", [StaffActivity.OnStrike] = "On strike",
			[StaffActivity.Waiting] = "Working", [StaffActivity.Held] = "Picked up", [StaffActivity.GoingToLoo] = "Working",
			[StaffActivity.Cleaning] = "Working", [StaffActivity.GoingToRide] = "Working", [StaffActivity.Repairing] = "Working",
			[StaffActivity.Performing] = "Working", [StaffActivity.Researching] = "Working"
		};

		foreach ( var (activity, word) in words )
		{
			Set( patrolling, activity );
			Assert.AreEqual( word, ParkStaffScreen.StatusOf( patrolling ), $"{activity}, a patrol area" );
		}

		Set( anywhere, StaffActivity.Walking );
		Assert.AreEqual( "Idle", ParkStaffScreen.StatusOf( anywhere ), "a walk with cells 1 to 0x4000 for an area" );

		Patrol( anywhere, 1, 0x3fff );
		Assert.AreEqual( "Patrolling", ParkStaffScreen.StatusOf( anywhere ), "short of the last cell" );

		Patrol( anywhere, 2, 0x4000 );
		Assert.AreEqual( "Patrolling", ParkStaffScreen.StatusOf( anywhere ), "short of the first cell" );

		Patrol( anywhere, 0, 0 );
		Assert.AreEqual( "Idle", ParkStaffScreen.StatusOf( anywhere ), "no area, as a hire here" );

		Set( anywhere, StaffActivity.Held );
		var kind = ParkStaffPool.KindFor( anywhere.Model );
		typeof( ParkStaffScreen ).GetField( "_lastKind", BindingFlags.Static | BindingFlags.NonPublic )!.SetValue( null, kind );

		var list = Field<UiList>( new ParkStaffScreen( stack ), "_list" );
		Assert.AreEqual( "Picked up", list.Rows.First( each => each.Id == anywhere.ThingId ).Values![0], "the row's status cell" );
		Assert.IsTrue( lines.Any( line => line.Contains( $"row {anywhere.ThingId} " ) && line.Contains( ": \"Picked up\" " ) ), "and its log line" );
	}

	/// <summary>
	/// <b>The all-items list rewrites each row's columns after its name every two seconds on the rides', shops' and
	/// sideshows' tabs, a tab starting the timer again, and never on the miscellaneous tab</b> (<c>FUN_004955e0</c>,
	/// armed by each tab's own list tree at message <c>0x15</c>).
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> no rewrite; the rewrite a frame early; the values kept; the name rewritten; a tab keeping the
	/// old phase; the miscellaneous tab rewritten.
	/// </remarks>
	[TestMethod]
	public void TheItemsListRewritesItsRowsEveryTwoSecondsFromItsTabAndNotTheMiscellaneousOne()
	{
		typeof( ParkItemsScreen ).GetField( "_lastTab", BindingFlags.Static | BindingFlags.NonPublic )!.SetValue( null, 0 );

		Time.PinWall( 5000 );

		var screen = new ParkItemsScreen( stack );
		var list = Field<Dictionary<int, UiList>>( screen, "_lists" )[5];
		var ride = state.Objects.First( placed => list.Rows.Any( row => row.Id == placed.ThingId ) );
		var at = list.Rows.ToList().FindIndex( row => row.Id == ride.ThingId );
		var name = list.Rows[at].Name;
		var opened = list.Rows[at].Numbers!.ToArray();
		var repair = (int)ride.StateOfRepair == 40 ? 60f : 40f;

		state.ReplaceObject( ride with { StateOfRepair = repair } );

		Time.PinWall( 6999 );
		screen.Update();
		CollectionAssert.AreEqual( opened, list.Rows[at].Numbers!.ToArray(), "1999 ms: not rewritten" );

		Time.PinWall( 7000 );
		screen.Update();
		Assert.AreEqual( ((int)repair << 10) / 100, list.Rows[at].Numbers![3], "the state of repair's bar rewritten" );
		Assert.AreEqual( name, list.Rows[at].Name, "the name as the fill wrote it" );
		Assert.AreEqual( 1, Rewrites( "Items list" ) );

		// The shops' tab at 7500 starts the timer again: nothing at 9000, a rewrite at 9500.
		Time.PinWall( 7500 );
		var show = typeof( ParkItemsScreen ).GetMethod( "Show", Private )!;
		show.Invoke( screen, [1] );

		Time.PinWall( 9000 );
		screen.Update();
		Assert.AreEqual( 1, Rewrites( "Items list" ), "9000 ms: 1500 ms from the tab" );

		Time.PinWall( 9500 );
		screen.Update();
		Assert.AreEqual( 2, Rewrites( "Items list" ), "9500 ms: two seconds from the tab" );

		// The miscellaneous tab: no rewrite, however long.
		show.Invoke( screen, [3] );
		Time.PinWall( 20000 );
		screen.Update();
		Assert.AreEqual( 2, Rewrites( "Items list" ), "the miscellaneous tab is never rewritten" );
	}

	/// <summary>
	/// <b>Each tab's second column is the customers of the last thirty finished days, a number, and the rides'
	/// excitement, state of repair and remaining life and the sideshows' excitement are bars</b>, each
	/// <c>((value &amp; 0xff) &lt;&lt; 10) / 100</c> of 1024, as the fill <c>FUN_00493cd0</c> and the rewrite
	/// <c>FUN_004955e0</c> hand them; the shops' and sideshows' satisfaction bars are left unfilled and counted.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> the customers not written, or today's taken in, or more than thirty days; a bar's low byte not
	/// taken; the repair and the life exchanged; the excitement left out; the rides' bars in the shops' columns; the
	/// sideshows' excitement not a bar; the count left on the rides' tab.
	/// </remarks>
	[TestMethod]
	public void TheItemsRowsAreTheCustomersAsANumberAndTheBarsAsTheOriginalHandsThem()
	{
		Assert.AreEqual( 1024, ParkItemsScreen.Bar( 100 ) );
		Assert.AreEqual( 890, ParkItemsScreen.Bar( 87 ) );
		Assert.AreEqual( 1024, ParkItemsScreen.Bar( 356 ), "the low byte alone" );
		Assert.AreEqual( 2611, ParkItemsScreen.Bar( -1 ), "255 of it, held by the bar" );

		typeof( ParkItemsScreen ).GetField( "_lastTab", BindingFlags.Static | BindingFlags.NonPublic )!.SetValue( null, 0 );

		// The Belly Bounce given thirty-two finished days of 1 to 32 customers and 1000 today, a repair of 87.9 and a
		// life of 300: the column is days 3 to 32, 525; the repair's bar 87's, the life's its low byte 44's.
		var bounce = state.Objects.Single( placed => placed.ThingId == 13 );
		var rings = state.RingsFor( 13 );

		for ( var day = 1; day <= 32; ++day )
		{
			rings.Customers.Today = day;
			rings.Roll();
		}

		rings.Customers.Today = 1000;
		state.ReplaceObject( bounce with { StateOfRepair = 87.9f, RemainingLife = 300f } );
		bounce = state.Objects.Single( placed => placed.ThingId == 13 );

		var screen = new ParkItemsScreen( stack );
		var lists = Field<Dictionary<int, UiList>>( screen, "_lists" );
		var rides = lists[5];
		var at = rides.Rows.ToList().FindIndex( row => row.Id == 13 );
		Assert.IsTrue( at >= 0, "the Belly Bounce is listed" );

		var catalogue = level.Catalogue!;
		catalogue.TryGet( bounce.CatalogueId, out var item );

		// Its excitement is 40 (the `spend` census), 409 of 1024.
		Assert.AreEqual( 40, ParkRideScore.ExcitementOf( bounce, item, state.TrackRides ) );
		CollectionAssert.AreEqual( new[] { 0, 525, 409, 890, 450 }, rides.Rows[at].Numbers!.ToArray() );
		CollectionAssert.AreEqual( new[] { "525", "", "", "" }, rides.Rows[at].Values!.ToArray() );
		CollectionAssert.AreEqual( new[] { false, false, true, true, true }, rides.BarColumns );
		CollectionAssert.AreEqual( new[] { 868, 1167, 1466 },
			rides.Children.OfType<ParkObjectWindow.UiStatBar>().Select( bar => bar.Rect.Left ).Distinct().ToArray() );

		var show = typeof( ParkItemsScreen ).GetMethod( "Show", Private )!;

		// The first shop and the first sideshow: twenty-eight days to push the file's out of the thirty, the first of them
		// 7 taken at a satisfaction of -30, then two finished days of 40 taken against 10 and 25 spent and of satisfaction
		// -20 and -25, and today 70 against 100.
		show.Invoke( screen, [1] );
		var shop = lists[5].Rows[0].Id;
		show.Invoke( screen, [2] );
		var sideshow = lists[6].Rows[0].Id;

		foreach ( var id in new[] { shop, sideshow } )
		{
			var each = state.RingsFor( id );
			var totalBefore = state.TakingsFor( id ) - state.CostsFor( id );

			for ( var day = 0; day < 28; ++day )
			{
				if ( day == 0 )
				{
					state.TakeAt( id, 7 );
					each.Satisfaction.Today = -30;
				}

				each.Roll();
			}

			for ( var day = 0; day < 2; ++day )
			{
				state.TakeAt( id, 40 );
				state.BookCostOfGoods( id, day == 0 ? 10 : 25 );
				each.Satisfaction.Today = day == 0 ? -20 : -25;
				each.Roll();
			}

			state.TakeAt( id, 70 );
			state.BookCostOfGoods( id, 100 );
			Assert.AreEqual( totalBefore + 157 - 135, state.TakingsFor( id ) - state.CostsFor( id ) );
		}

		show.Invoke( screen, [1] );
		var shops = lists[5];
		CollectionAssert.AreEqual( new[] { false, false, false, false, true }, shops.BarColumns, "the shops' one bar" );
		CollectionAssert.AreEqual( new[] { 1466 },
			shops.Children.OfType<ParkObjectWindow.UiStatBar>().Select( bar => bar.Rect.Left ).Distinct().ToArray(),
			"its cells made again: the rides' three bars gone" );

		// Last month 87 - 35 = 52 (today's not among it; 45 over twenty-nine days); the total the file's and today's; the
		// satisfaction 50 plus -75 / 30 truncated toward nought, 48, a bar of 491 (floored 481; over twenty-nine days 501).
		var shopRow = shops.Rows.Single( row => row.Id == shop );
		var shopTotal = state.TakingsFor( shop ) - state.CostsFor( shop );
		CollectionAssert.AreEqual( new[] { 0, state.RingsFor( shop ).Customers.LastDays( 30 ), 52, shopTotal, 491 },
			shopRow.Numbers!.ToArray() );
		Assert.AreEqual( "$ 52", shopRow.Values![1] );
		Assert.AreEqual( ParkItemsScreen.Money( shopTotal ), shopRow.Values[2] );
		Assert.AreEqual( "", shopRow.Values[3], "the satisfaction is a bar" );

		show.Invoke( screen, [2] );
		var sideshows = lists[6];
		CollectionAssert.AreEqual( new[] { false, false, true, false, false, true }, sideshows.BarColumns );
		Assert.IsTrue( sideshows.Rows.Count > 0, "the stock park has sideshows" );

		foreach ( var row in sideshows.Rows )
		{
			var placed = state.Objects.Single( each => each.ThingId == row.Id );
			catalogue.TryGet( placed.CatalogueId, out var game );
			Assert.AreEqual( ((ParkRideScore.ExcitementOf( placed, game, state.TrackRides ) & 0xff) << 10) / 100, row.Numbers![2] );
		}

		var sideshowRow = sideshows.Rows.Single( row => row.Id == sideshow );
		Assert.AreEqual( 52, sideshowRow.Numbers![3] );
		Assert.AreEqual( state.TakingsFor( sideshow ) - state.CostsFor( sideshow ), sideshowRow.Numbers[4] );
		Assert.AreEqual( 491, sideshowRow.Numbers[5] );
		CollectionAssert.AreEqual( new[] { "", "$ 52", "" }, new[] { sideshowRow.Values![1], sideshowRow.Values[2], sideshowRow.Values[4] } );
	}

	/// <summary>
	/// <b>The all-staff, all-items and visitors lists' headings are framed as the original's openers frame them</b>
	/// (<c>FUN_00486660</c>): <c>!cbut</c>, font slot 8, 255, 239, 0, the text centred, and each column's own help row,
	/// the items' by tab (<c>docs/exe/hud.md</c>, "The list headings' frames").
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> a screen's headings bare; another font or colour; a help row off by one, or the items' rides' rows
	/// kept on the shops' tab; the sideshows' third column's help taken from the shops'. Another frame's name is the
	/// game's to catch: a mesh loads only beside the renderer, and the screens log each heading's.
	/// </remarks>
	[TestMethod]
	public void TheStaffItemsAndVisitorsListsFrameTheirHeadingsWithEachColumnsHelpRow()
	{
		static void Framed( UiList list, int first, int[] helps, string screen )
		{
			for ( var column = 0; column < helps.Length; ++column )
			{
				var heading = list.Children.OfType<UiButton>().Single( each => each.Id == 0x10 + column );
				var at = $"{screen} column {column}";

				Assert.AreEqual( 8, heading.Font, at );
				Assert.AreEqual( new UiColour( 255, 239, 0 ), heading.TextColour, at );
				// Centred only where a frame is named (UiList.AddHeading); the frame itself needs the renderer to load.
				Assert.AreEqual( TextAlign.Centre, heading.TextAcross, at );
				Assert.AreEqual( helps[column], heading.HelpText, at );
				Assert.AreEqual( Localization.Text( first + column ), heading.Text, at );
			}
		}

		var staff = new ParkStaffScreen( stack );
		Framed( Field<UiList>( staff, "_list" ), 101, [119, 120, 121, 122, 123], "staff" );

		var visitors = new ParkVisitorsScreen( stack );
		Framed( Field<UiList>( visitors, "_list" ), 113, [129, 130, 131, 132, 133, 134], "visitors" );

		typeof( ParkItemsScreen ).GetField( "_lastTab", BindingFlags.Static | BindingFlags.NonPublic )!.SetValue( null, 0 );
		var items = new ParkItemsScreen( stack );
		var lists = Field<Dictionary<int, UiList>>( items, "_lists" );
		var show = typeof( ParkItemsScreen ).GetMethod( "Show", Private )!;

		Framed( lists[5], 82, [105, 106, 107, 108, 109], "rides" );

		show.Invoke( items, [1] );
		Framed( lists[5], 87, [105, 584, 585, 586, 587], "shops" );

		show.Invoke( items, [2] );
		Framed( lists[6], 92, [105, 584, 107, 585, 586, 587], "sideshows" );

		show.Invoke( items, [3] );
		Framed( lists[2], 98, [105, 588], "misc" );

		Assert.IsTrue( lines.Any( line => line.StartsWith( "Staff list: headings " ) && line.Contains( " 8 119 \"Name\"" ) ), "the staff's log line" );
	}

	/// <summary>
	/// <b>The visitors rows letter a visitor number of nought "New Visitor", the cash as a money cell, and draw the
	/// happiness as a bar in <c>happygrad.wct</c> inset 8</b>, the number centred, the cash to the right and the rides
	/// from the left (the styler <c>FUN_004939d0</c>, the adder <c>FUN_00493800</c>; <c>docs/exe/hud.md</c>, "The visitors
	/// rows").
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> nought lettered "0"; the cash a bare number; the happiness not a byte or not a bar; the bar in
	/// the ride bars' skin or inset 10; the number or the cash aligned otherwise; the rewrite lettering the old way.
	/// </remarks>
	[TestMethod]
	public void TheVisitorsRowsLetterNewVisitorTheCashAsMoneyAndTheHappinessAsABar()
	{
		var guests = people.Peeps.ToArray();
		var numbered = guests[0];
		var fresh = guests[1];

		numbered.VisitorNumber = 7;
		numbered.Cash = -55;
		numbered.Happiness = 300.75f;
		fresh.VisitorNumber = 0;
		fresh.Cash = 306;
		fresh.Happiness = 33.9f;

		foreach ( var other in guests.Skip( 2 ) )
			other.VisitorNumber = 1;

		Time.PinWall( 1000 );

		var screen = new ParkVisitorsScreen( stack );
		var list = Field<UiList>( screen, "_list" );

		stack.Open( screen );

		var row = list.Rows.Single( each => each.Id == numbered.ThingId );
		Assert.AreEqual( "7", row.Name );
		Assert.AreEqual( "-$ 55", row.Values![0], "the cash in a money cell" );
		Assert.AreEqual( 450, row.Numbers![5], "((300 & 0xff) << 10) / 100" );
		Assert.AreEqual( "", row.Values[4], "the happiness lettered nowhere" );

		Assert.AreEqual( fresh.ThingId, list.Rows[0].Id, "nought sorts first" );
		Assert.AreEqual( "New Visitor", list.Rows[0].Name );
		Assert.AreEqual( "$ 306", list.Rows[0].Values![0] );
		Assert.AreEqual( 337, list.Rows[0].Numbers![5], "(33 << 10) / 100" );

		var bars = list.Children.OfType<ParkObjectWindow.UiStatBar>().ToArray();
		var top = bars.Min( bar => bar.Rect.Top );
		var first = bars.Where( bar => bar.Rect.Top == top ).ToArray();

		Assert.AreEqual( 1, first.Length, "one bar cell a row" );
		Assert.AreEqual( (1478, ParkObjectWindow.UiStatBar.HappyGrad), (first[0].Rect.Left, first[0].Skin), "the happiness" );
		Assert.AreEqual( 315 + 8, top, "inset 8 from the row's top" );
		Assert.AreEqual( 337, first[0].Units, "the first row's bar filled" );

		CollectionAssert.AreEqual(
			new[] { TextAlign.Centre, TextAlign.End, TextAlign.Centre, TextAlign.Start, TextAlign.Start, TextAlign.End },
			list.ColumnAligns, "the number and time centred, the cash right, the rides left" );

		fresh.Happiness = 90.5f;
		state.SetGameTick( fresh.ArrivalDate + 4 * 30 );
		Time.PinWall( 3000 );
		screen.Update();

		Assert.AreEqual( 921, list.Rows[0].Numbers![5], "the rewrite: (90 << 10) / 100" );
		Assert.AreEqual( 921, first[0].Units );
		Assert.IsTrue( lines.Any( line => line.StartsWith( "Visitors list: " ) && line.Contains( " rows rewritten, row 0 " )
			&& line.EndsWith( "New Visitor: $ 306 5 days (125 h) 0 part 999 921" ) ), "the rewrite's log line: its stay times four" );
	}

	/// <summary>
	/// <b>A guest's time in park is the stay's whole park hours, lettered in hours up to 24, then "day" to 48, then
	/// "days"</b>, each in days the hours over 24 (the adder <c>0x00493834</c>, the setter <c>0x00490ff0</c>; <c>docs/exe/hud.md</c>,
	/// "The visitors rows"); the two-second rewrite letters four times the hours (<c>0x004932db</c>), and the list sorts
	/// on the hours.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> the stay not in fours of sweeps, or signed; the rate not over four; the hours rounded; the
	/// product not wrapped in 64 bits; a bound at 24 or 48 taken as reached; the days not the hours over 24; "day" and
	/// "days" swapped; a negative count lettered with its sign; the rewrite's stay not shifted back, or the fill's
	/// shifted; the column's number not the hours; a sort on it still counted.
	/// </remarks>
	[TestMethod]
	public void TheVisitorsTimeInParkIsItsHoursThenADayThenDays()
	{
		Assert.AreEqual( GameCalendar.DefaultRate, GameCalendar.Rate, "the shipped park's rate" );

		// stay x 15000 / 4 / 3600: 23 is 23.96 hours, 24 is 25, 47 is 48.96, 48 is 50.
		CollectionAssert.AreEqual( new[] { 0, 23, 25, 48, 50, 1041 },
			new uint[] { 0, 23, 24, 47, 48, 1000 }.Select( ParkVisitorsScreen.HoursIn ).ToArray() );

		// Stamped after the clock: the stay is unsigned, and stay x 3750 x 10,000,000 wraps in 64 bits; the rewrite's
		// stay, four times, wraps past the sign.
		Assert.AreEqual( 93661950, ParkVisitorsScreen.HoursIn( unchecked( (uint)-4 ) >> 2 ) );
		Assert.AreEqual( 93660909, ParkVisitorsScreen.HoursIn( unchecked( (uint)-4000 ) >> 2 ) );
		Assert.AreEqual( -137761755, ParkVisitorsScreen.HoursIn( unchecked( (uint)-4 ) >> 2 << 2 ) );

		CollectionAssert.AreEqual(
			new[] { "0 hours", "1 hours", "24 hours", "1 day", "1 day", "2 day", "2 days", "2 days", "3 days", "43 days" },
			new[] { 0, 1, 24, 25, 47, 48, 49, 71, 72, 1041 }.Select( ParkVisitorsScreen.TimeText ).ToArray() );

		// A negative count letters its size: the writer prints the value word and not the sign word.
		Assert.AreEqual( "137761755 hours", ParkVisitorsScreen.TimeText( -137761755 ) );

		var guests = people.Peeps.ToArray();
		var tick = guests.Max( guest => guest.ArrivalDate ) + 4 * 23;

		// FUN_004fd950 takes the difference unsigned before the shift: a stamp after the clock is a stay near 2^30.
		Assert.AreEqual( 0x3fffffffu, guests[0].StayAt( guests[0].ArrivalDate - 4 ) );
		Assert.AreEqual( 23u, guests[0].StayAt( guests[0].ArrivalDate + 4 * 23 + 3 ) );

		state.SetGameTick( tick );
		Time.PinWall( 1000 );

		var screen = new ParkVisitorsScreen( stack );
		var list = Field<UiList>( screen, "_list" );

		foreach ( var guest in guests )
		{
			var row = list.Rows.Single( each => each.Id == guest.ThingId );
			var hours = (int)((long)((uint)(tick - guest.ArrivalDate) >> 2) * 15000 / 4 / 3600);

			Assert.AreEqual( hours, row.Numbers![2], $"guest {guest.ThingId}" );
			Assert.AreEqual( ParkVisitorsScreen.TimeText( hours ), row.Values![1] );
		}

		Assert.IsTrue( list.Rows.Any( row => row.Values![1] == "23 hours" ), "the last to arrive" );

		// The two-second rewrite shifts each stay back left by two (0x004932db): four times the hours.
		Time.PinWall( 3000 );
		screen.Update();

		foreach ( var guest in guests )
		{
			var row = list.Rows.Single( each => each.Id == guest.ThingId );
			var hours = (int)((long)((uint)(tick - guest.ArrivalDate) >> 2 << 2) * 15000 / 4 / 3600);

			Assert.AreEqual( hours, row.Numbers![2], $"guest {guest.ThingId} rewritten" );
			Assert.AreEqual( ParkVisitorsScreen.TimeText( hours ), row.Values![1] );
		}

		Assert.IsTrue( list.Rows.Any( row => row.Values![1] == "3 days" ), "the last to arrive, 95 hours" );

		((UiButton)list.Children.Single( child => child.Id == 0x12 )).Clicked!();

		var hoursInOrder = list.Rows.Select( row => row.Numbers![2] ).ToArray();
		Assert.IsTrue( hoursInOrder.Zip( hoursInOrder.Skip( 1 ), ( a, b ) => a <= b ).All( ordered => ordered ), "by hours" );
		Assert.IsTrue( hoursInOrder.Distinct().Count() > 1, "and the hours differ" );

		typeof( ParkVisitorsScreen ).GetField( "_sort", BindingFlags.Static | BindingFlags.NonPublic )!.SetValue( null, 1 );
	}

	/// <summary>
	/// <b>Both screens letter larger, and their titles and money as the original's.</b> Every text control under either
	/// root letters at <see cref="UiFonts.ParkScreenLettering"/>; the title at (569, 74)-(1513, 153), (234, 239, 102), with
	/// the edge; the money with the edge; the hire screen's money in the buy screen's words (UITEXT <c>0x1ca</c>,
	/// <c>FUN_0049bdd0</c>), "Cash  $ 87987" for the stock park, not "87987".
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> either root left at the original's size; a title white, without its edge or in the stream's rect;
	/// a money without its edge; the hire money the bare balance.
	/// </remarks>
	[TestMethod]
	public void TheBuyAndHireScreensLetterLargerAndTheirTitlesAndMoneyAsTheOriginals()
	{
		var buy = new ParkBuyScreen( stack );
		var hire = new ParkHireScreen( stack );
		hire.Update();

		Assert.AreEqual( 1.2f, UiFonts.ParkScreenLettering, "the improvement's size" );

		foreach ( var (root, title, money, name) in new[] { (buy.Root!, 0x1fe, 0x200, "buy"), (hire.Root!, 0x2480, 0x2496, "hire") } )
		{
			var lettered = Under( root ).Where( control => control.Font >= 0 ).ToList();
			Assert.IsTrue( lettered.Count > 5, $"{name}: its lettered controls" );

			foreach ( var control in lettered )
				Assert.AreEqual( UiFonts.ParkScreenLettering, control.LetteringSize, $"{name}: control 0x{control.Id:x}" );

			var t = root.Children.Single( child => child.Id == title );
			Assert.AreEqual( (new UiRect( 569, 74, 1513, 153 ), new UiColour( 234, 239, 102 ), true, 5),
				(t.Rect, t.TextColour, t.TextShadow, t.Font), $"{name}: the title" );

			var m = root.Children.Single( child => child.Id == money );
			Assert.AreEqual( (UiColour.White, true, 5, TextAlign.End), (m.TextColour, m.TextShadow, m.Font, m.TextAcross), $"{name}: the money" );
		}

		Assert.AreEqual( 87987, state.Balance, "the stock park's bank" );
		Assert.AreEqual( "Cash  $ 87987", hire.Root!.Children.Single( child => child.Id == 0x2496 ).Text, "the hire screen's money" );
	}

	/// <summary>
	/// <b>Under <c>graphics faithful</c> both screens letter at the original's size</b> (<see cref="GameOptions.Faithful"/>,
	/// Q645): every text control under either root letters at 1.
	/// </summary>
	/// <remarks><b>Mutations:</b> the toggle not read by <see cref="UiFonts.ParkScreenLettering"/>; a screen lettering at a
	/// literal 1.2.</remarks>
	[TestMethod]
	public void UnderGraphicsFaithfulBothScreensLetterAtTheOriginalsSize()
	{
		var was = GameOptions.Current;

		try
		{
			GameOptions.Current = new() { Faithful = true };
			var buy = new ParkBuyScreen( stack );
			var hire = new ParkHireScreen( stack );
			hire.Update();

			foreach ( var (root, name) in new[] { (buy.Root!, "buy"), (hire.Root!, "hire") } )
			{
				var lettered = Under( root ).Where( control => control.Font >= 0 ).ToList();
				Assert.IsTrue( lettered.Count > 5, $"{name}: its lettered controls" );

				foreach ( var control in lettered )
					Assert.AreEqual( 1f, control.LetteringSize, $"{name}: control 0x{control.Id:x}" );
			}
		}
		finally { GameOptions.Current = was; }
	}

	private static IEnumerable<UiControl> Under( UiControl control )
		=> control.Children.SelectMany( child => Under( child ).Prepend( child ) );

	/// <summary>
	/// <b>The hire screen's skill bar stands in 10 from its rectangle's top and bottom</b>, (722, 546)-(957, 565) of the
	/// stream's (722, 536)-(957, 575) (<c>FUN_00485e30( bar, 0, 10 )</c> at <c>0x0049c2ef</c>; the original's skin
	/// keeps that rect, read live by <c>q636/orig/read.py</c>), and the "Skill" label beside it keeps the whole row.
	/// </summary>
	/// <remarks><b>Mutations:</b> no inset; an inset of 8; the inset across rather than down; the label inset too.</remarks>
	[TestMethod]
	public void TheHireScreensSkillBarStandsInTenFromItsRowsTopAndBottom()
	{
		var info = new ParkHireScreen( stack ).Root.Children.Single( child => child.Id == 0x2481 );

		Assert.AreEqual( new UiRect( 722, 546, 957, 565 ), info.Children.Single( child => child.Id == 0x2482 ).Rect, "the bar" );
		Assert.AreEqual( new UiRect( 283, 536, 679, 575 ), info.Children.Single( child => child.Id == 0x2483 ).Rect, "the label" );
	}

	/// <summary>
	/// <b>The visitors, hire, buy, all-staff and all-items rows are lettered in font 7</b>, the visitors' 0x38 tall
	/// (<c>FUN_004938c0</c>) and the others' as tall as their factories measure font 7 (<c>FUN_0049bbb0</c>,
	/// <c>FUN_004ad240</c>, <c>0x00496c10</c>, <c>FUN_004944d0</c>; <c>docs/exe/hud.md</c>, "The rows' font and height").
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> any list left at font 6 or at 44; the visitors measured as the others; the others fixed at 0x38.
	/// </remarks>
	[TestMethod]
	public void TheListsRowsAreLetteredInFont7AtTheirFactoriesHeights()
	{
		var measured = UiList.RowHeightFor( UiFonts.Get( 7 )!.LineHeight, UiFonts.SetScreenHeight );
		Assert.AreEqual( measured, UiList.RowHeightOf( 7 ) );
		Assert.AreNotEqual( 44, measured, "no set's font 7 makes the default" );
		Assert.AreNotEqual( 0x38, measured, "nor the visitors' height" );

		var visitors = Field<UiList>( new ParkVisitorsScreen( stack ), "_list" );
		var staff = Field<UiList>( new ParkStaffScreen( stack ), "_list" );
		var items = Field<Dictionary<int, UiList>>( new ParkItemsScreen( stack ), "_lists" );
		var hire = Field<UiList>( new ParkHireScreen( stack ), "_list" );
		var buy = Field<UiList>( new ParkBuyScreen( stack ), "_list" );

		foreach ( var (list, height, name) in new[]
			{
				(visitors, 0x38, "visitors"), (staff, measured, "staff"), (hire, measured, "hire"), (buy, measured, "buy")
			}
			.Concat( items.Select( each => (each.Value, measured, $"items {each.Key}") ) ) )
		{
			Assert.AreEqual( (7, height), (list.RowFont, list.RowHeight), name );
			Assert.AreEqual( (list.RowArea.Bottom - list.RowArea.Top) / height, list.VisibleRows, $"{name}: rows that fit" );

			// The second slot's cells stand a row lower, lettered in the row font.
			var text = list.Children.Where( child => child.Font == 7 && child.Rect.Top == list.RowArea.Top + height ).ToArray();
			Assert.IsTrue( text.Length > 0, $"{name}: the second slot's lettered cells" );
			Assert.IsTrue( text.All( cell => cell.Rect.Bottom == list.RowArea.Top + (2 * height) ), $"{name}: a row tall" );
		}
	}

	/// <summary>
	/// <b>Each all-items tab letters the name from the left, centres the count and sets the money from the right</b>, as
	/// the styler <c>FUN_00494690</c> hands <c>FUN_0065c428</c>; the all-staff styler <c>FUN_00496d20</c> leaves the name
	/// and the status at a label's default, from the left, and sets the wage from the right (<c>docs/exe/hud.md</c>, "How
	/// a list's cells are aligned").
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> the count from the right or the left; the money centred; the name centred; the staff's status or
	/// wage moved.
	/// </remarks>
	[TestMethod]
	public void TheItemsListsCentreTheCountAndTheStaffListSetsTheWageFromTheRight()
	{
		typeof( ParkItemsScreen ).GetField( "_lastTab", BindingFlags.Static | BindingFlags.NonPublic )!.SetValue( null, 0 );
		var screen = new ParkItemsScreen( stack );
		var items = Field<Dictionary<int, UiList>>( screen, "_lists" );
		var show = typeof( ParkItemsScreen ).GetMethod( "Show", Private )!;
		var staff = Field<UiList>( new ParkStaffScreen( stack ), "_list" );

		// The first slot's cells, by their column: how a lettered one is aligned, and null for a bar.
		static TextAlign?[] Lettered( UiList list ) => [.. list.Columns.Select( column => list.Children
			.Where( child => child.Rect.Left == column.Left && (child.Font == list.RowFont || child is ParkObjectWindow.UiStatBar)
				&& child.Rect.Top >= list.RowArea.Top && child.Rect.Top < list.RowArea.Top + list.RowHeight )
			.Select( child => child is ParkObjectWindow.UiStatBar ? (TextAlign?)null : child.TextAcross )
			.Single() )];

		TextAlign? start = TextAlign.Start, centre = TextAlign.Centre, end = TextAlign.End, bar = null;

		show.Invoke( screen, [0] );
		CollectionAssert.AreEqual( new[] { start, centre, bar, bar, bar }, Lettered( items[5] ), "rides" );
		show.Invoke( screen, [1] );
		CollectionAssert.AreEqual( new[] { start, centre, end, end, bar }, Lettered( items[5] ), "shops: the money from the right" );
		show.Invoke( screen, [2] );
		CollectionAssert.AreEqual( new[] { start, centre, bar, end, end, bar }, Lettered( items[6] ), "sideshows" );
		show.Invoke( screen, [3] );
		CollectionAssert.AreEqual( new[] { start, centre }, Lettered( items[2] ), "misc" );

		CollectionAssert.AreEqual( new[] { start, start, end, bar, bar }, Lettered( staff ),
			"staff: the name and the status from the left, the wage from the right" );
	}

	/// <summary>
	/// <b>The hire and buy lists letter the name from the left and the wage or the price from the right</b>, as their
	/// stylers <c>FUN_0049c880</c> and <c>FUN_004ad340</c> leave the name's label at its default and hand the figure
	/// <c>FUN_0065c428( 2, 1 )</c>; the buy list's tick-box is a mesh with no lettering (<c>docs/exe/hud.md</c>, "How a
	/// list's cells are aligned").
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> either name centred or from the right; either figure centred or from the left; the tick-box
	/// lettered.
	/// </remarks>
	[TestMethod]
	public void TheHireAndBuyListsSetTheirFiguresFromTheRight()
	{
		var hire = Field<UiList>( new ParkHireScreen( stack ), "_list" );
		var buy = Field<UiList>( new ParkBuyScreen( stack ), "_list" );

		// The first slot's cell at a column's left edge.
		static UiControl Cell( UiList list, int column ) => list.Children
			.Single( child => child.Rect.Left == list.Columns[column].Left && child.Font == list.RowFont
				&& child.Rect.Top == list.RowArea.Top );

		Assert.AreEqual( (TextAlign.Start, TextAlign.End), (Cell( hire, 0 ).TextAcross, Cell( hire, 1 ).TextAcross ),
			"hire: the name from the left, the wage from the right" );
		Assert.AreEqual( (TextAlign.Start, TextAlign.End), (Cell( buy, 0 ).TextAcross, Cell( buy, 1 ).TextAcross ),
			"buy: the name from the left, the price from the right" );

		// A row with a state, owned: its tick-box is framed by it and lettered with nothing.
		buy.Clear();
		buy.Add( new UiList.Row( 1, "Belly Bounce", 500, 1 ) );
		Assert.AreEqual( (1, true), (Cell( buy, 2 ).Frame, Cell( buy, 2 ).Visible), "buy: the tick-box framed by the state" );
		Assert.IsTrue( string.IsNullOrEmpty( Cell( buy, 2 ).Text ), "buy: the tick-box letters nothing" );
	}

	/// <summary>
	/// <b>A visitors row shows the guest's last thought as a part of <c>thoughts.MD2</c></b>, the thought less one, or 999,
	/// past every part, with none (the adder <c>0x0049385a</c>, the rewrite <c>0x0049330c</c>); the cell is the column inset 4
	/// across (<c>FUN_00485e30( cell, 4, 0 )</c>), and the list sorts on its number (<c>docs/exe/hud.md</c>, "The visitors
	/// rows").
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> the part the thought itself, nought or less one with none; the column's number nought; the mesh another
	/// or in another column; the cell not inset, inset down too, not made, not framed or lettered; a sort on it counted.
	/// </remarks>
	[TestMethod]
	public void TheVisitorsRowsShowTheLastThoughtAsAPartOfTheThoughtsMesh()
	{
		Assert.AreEqual( 999, ParkVisitorsScreen.ThoughtPart( 0 ) );
		Assert.AreEqual( 0, ParkVisitorsScreen.ThoughtPart( 1 ), "hungry, the first part" );
		Assert.AreEqual( 16, ParkVisitorsScreen.ThoughtPart( Thoughts.Confused ), "confused, the last" );

		// The parts are the model's meshes, in order: the original files the model under the first one's name.
		var parts = new ModelFile( $"ui/{ParkVisitorsScreen.ThoughtsMesh}.md2" ).Meshes
			.Select( each => each.Name.TrimEnd( '\0' ) ).ToArray();
		Assert.AreEqual( 17, parts.Length );
		Assert.AreEqual( ("ti_hungry", "ti_sick", "ti_confused"), (parts[0], parts[13], parts[16]), "thoughts 1, 0xe and 0x11" );

		var guests = people.Peeps.ToArray();
		var thoughts = new[] { 1, 0, 0xe, 0x12 };

		for ( var each = 0; each < guests.Length; ++each )
			guests[each].Thoughts.Restore( thoughts[each % thoughts.Length], 0 );

		Time.PinWall( 1000 );

		var unnamed = Times( "VISITOR_COLUMN_117_UNNAMED" );
		var sorts = Times( "VISITOR_SORT_ON_UNBUILT_COLUMN" );
		var screen = new ParkVisitorsScreen( stack );
		var list = Field<UiList>( screen, "_list" );

		stack.Open( screen );
		Assert.AreEqual( unnamed, Times( "VISITOR_COLUMN_117_UNNAMED" ), "the column is built: nothing counted" );

		foreach ( var guest in guests )
		{
			var row = list.Rows.Single( each => each.Id == guest.ThingId );
			Assert.AreEqual( ParkVisitorsScreen.ThoughtPart( guest.Thoughts.Last ), row.Numbers![4], $"guest {guest.ThingId}" );
			Assert.AreEqual( "", row.Values![3], "the thought lettered nowhere" );
		}

		CollectionAssert.AreEqual( new[] { null, null, null, null, ParkVisitorsScreen.ThoughtsMesh, null }, list.MeshColumns );

		// The column (1372, 1474), inset 4 across and none down.
		var cells = list.Children.Where( child => child.Rect.Left == 1372 + 4 ).ToArray();
		Assert.AreEqual( list.VisibleRows, cells.Length, "one thought cell a row" );
		Assert.AreEqual( (1474 - 4, 315, 315 + list.RowHeight),
			(cells[0].Rect.Right, cells[0].Rect.Top, cells[0].Rect.Bottom), "the column inset 4 across" );

		var first = cells.MinBy( cell => cell.Rect.Top )!;
		Assert.AreEqual( list.Rows[0].Numbers![4], first.Frame, "the first row's part" );
		Assert.IsNull( first.Text );

		// The rewrite takes the thought as it stands.
		var top = guests.Single( guest => guest.ThingId == list.Rows[0].Id );
		top.Thoughts.Restore( 9, 0 );
		Time.PinWall( 3000 );
		screen.Update();

		Assert.AreEqual( 8, list.Rows[0].Numbers![4], "happy, part 8" );
		Assert.AreEqual( 8, first.Frame );
		Assert.IsTrue( lines.Any( line => line.StartsWith( "Visitors list: " ) && line.Contains( " rows rewritten, row 0 " )
			&& line.Contains( " part 8 " ) ), "the rewrite's log line" );

		// The "?" heading sorts on the part: none (999) last.
		((UiButton)list.Children.Single( child => child.Id == 0x14 )).Clicked!();

		var sorted = list.Rows.Select( row => row.Numbers![4] ).ToArray();
		Assert.IsTrue( sorted.Zip( sorted.Skip( 1 ), ( a, b ) => a <= b ).All( ordered => ordered ), "by the part" );
		Assert.AreEqual( 999, sorted[^1] );
		Assert.AreEqual( sorts, Times( "VISITOR_SORT_ON_UNBUILT_COLUMN" ), "a sort on the thought counts nothing" );

		typeof( ParkVisitorsScreen ).GetField( "_sort", BindingFlags.Static | BindingFlags.NonPublic )!.SetValue( null, 1 );
	}

	/// <summary>
	/// A money cell letters UITEXT 449 and the figure, or 450 and the figure without its sign under nought
	/// (<c>0x004903e0</c>).
	/// </summary>
	[TestMethod]
	public void AMoneyCellIsTheDollarAndTheFigureOrTheMinusDollarAndItsSize()
	{
		Assert.AreEqual( "$ 0", ParkItemsScreen.Money( 0 ) );
		Assert.AreEqual( "$ 1234", ParkItemsScreen.Money( 1234 ) );
		Assert.AreEqual( "-$ 55", ParkItemsScreen.Money( -55 ) );
	}
}
