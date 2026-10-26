using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// The research screen (<see cref="UI.ParkResearchScreen"/>, <c>ResearchScreen_Open</c> and <c>ResearchScreen_OnMessage</c>)
/// over a fresh Full Simulation Lost Kingdom lab, the one the original makes (<c>restart.INTS</c>; Q349): its bars, labels,
/// sliders and arrows, and the lab's setters behind them. These read the item files and the interface's meshes from the
/// shipped game and are skipped where there is no installation - see <see cref="GameData"/>.
/// </summary>
[TestClass]
public class ParkResearchScreenTests
{
	private BaseFileSystem data = null!;
	private Point2 _screen;

	/// <summary>Full Simulation's most: <c>StaffPoolInfo.MaxResearchersInPark</c> 10 times the top grade's ability 6.</summary>
	private const int Most = 60;

	[TestInitialize]
	public void Mount()
	{
		_screen = Screen.Size;
		Screen.Size = new Point2( 2048, 1536 );
		Log ??= new();
		FileSystem = data = GameData.Required();
	}

	[TestCleanup]
	public void PutTheScreenBack() => Screen.Size = _screen;

	private ParkResearch FreshLab() => FreshLab( data );

	/// <summary>A fresh Full Simulation jungle's lab, as the park's setup makes it.</summary>
	internal static ParkResearch FreshLab( BaseFileSystem data )
	{
		var catalogue = new ParkItemCatalogue( "jungle", data );
		var balance = new ParkBalance( "jungle", easyMode: false );
		var fresh = new FreshPark( "jungle", balance, catalogue, data );
		return new ParkResearch( fresh.ObjectControlRecords, catalogue, fresh.ResearchProjects, fresh.ResearchKinds,
			fresh.ResearchWorkLoad, ParkResearch.GroupOpensAt( balance ), fresh.ResearchMostExpensiveProject );
	}

	private static UI.ParkResearchScreen Open( UI.WindowStack stack, ParkResearch lab, int ability )
	{
		var screen = new UI.ParkResearchScreen( stack, lab, () => ability, Most );
		stack.Open( screen );
		return screen;
	}

	private static IEnumerable<UI.UiControl> Controls( UI.UiControl control )
		=> new[] { control }.Concat( control.Children.SelectMany( Controls ) );

	private static UI.UiSlider Slider( UI.ParkResearchScreen screen, int id ) => Controls( screen.Root ).OfType<UI.UiSlider>().Single( slider => slider.Id == id );

	/// <summary>The id of the panel a kind stands in.</summary>
	private static int Panel( int kind ) => UI.ParkResearchScreen.Rows.Single( row => row.Kind == kind ).Panel;

	/// <summary>Each kind's bar, by the panel it stands in.</summary>
	private static UI.ParkObjectWindow.UiStatBar Bar( UI.ParkResearchScreen screen, int panel )
		=> screen.Root.Children.Single( control => control.Id == panel ).Children.OfType<UI.ParkObjectWindow.UiStatBar>().Single();

	private static UI.UiButton Arrow( UI.ParkResearchScreen screen, int panel, int id )
		=> screen.Root.Children.Single( control => control.Id == panel ).Children.OfType<UI.UiButton>().Single( button => button.Id == id );

	/// <summary>
	/// <b>The fresh lab's dearest project is project 3, and the five bars open at the original's</b>: rides (Dino Karts,
	/// 600 of 1300 left) 46 per cent, so 552 of 1024; shops (1212, 450) 675; sideshows (1304, 700) 481; miscellaneous
	/// (Stone Head, 250) 829; upgrades (1100's tier 1, 250) 829 (<c>docs/exe/hud.md</c>, "The research screen's panels").
	/// Each label is UITEXT 446, the item and the price the original letters.
	/// </summary>
	/// <remarks><b>Mutations:</b> the dearest taken as the last strictly dearer, or the first at all; the per cent of the
	/// project's own cost; the fill of what is left rather than done; kind 4's price of tier nought.</remarks>
	[TestMethod]
	public void TheBarsOpenAtTheOriginals()
	{
		var lab = FreshLab();
		Assert.AreEqual( 3, lab.MostExpensiveProject );
		CollectionAssert.AreEqual( new[] { 46, 34, 53, 19, 19 }, Enumerable.Range( 0, 5 ).Select( lab.PercentLeftOfDearest ).ToArray() );

		var screen = Open( new UI.WindowStack(), lab, ability: 4 );
		CollectionAssert.AreEqual( new[] { 552, 829, 675, 481, 829 },
			UI.ParkResearchScreen.Rows.Select( row => Bar( screen, row.Panel ).Units ).ToArray(), "rides, upgrades, shops, sideshows, miscellaneous" );
		CollectionAssert.AreEqual( new[]
		{
			"DinoKarts\nEstimated price : $ 6500", "Belly Bounce\nEstimated price : $ 400", "Fries Shop\nEstimated price : $ 750",
			"Laughing Hyenas\nEstimated price : $ 1750", "Stone Head\nEstimated price : $ 100"
		}, UI.ParkResearchScreen.Rows.Select( row => Bar( screen, row.Panel ).Text ).ToArray() );
	}

	/// <summary>
	/// <b>The dearest project is the first strictly dearer than all before it</b> (the fill's compare, <c>0x00503243</c>):
	/// of 500, 1300, 1300 and 200 it is the first 1300, project 1; with none, -1.
	/// </summary>
	/// <remarks><b>Mutations:</b> the compare taken at or over, which answers the second 1300.</remarks>
	[TestMethod]
	public void TheDearestIsTheFirstOfTheDearest()
	{
		ParkWorld.ResearchProject Costing( float cost ) => new( 1150, 0, 1, 0, cost, 0 );

		Assert.AreEqual( 1, ParkResearch.MostExpensive( [Costing( 500 ), Costing( 1300 ), Costing( 1300 ), Costing( 200 )] ) );
		Assert.AreEqual( -1, ParkResearch.MostExpensive( [] ) );
	}

	/// <summary>
	/// <b>Opening the screen takes the work load round the knob</b>, as the original does (measured, Q590): with one
	/// researcher of ability 3 the limit is <c>(97 / 3 + 3) × 1024 / 60</c> = 597, the knob <c>15 × 597 / 30</c> = 298,
	/// and back <c>30 × 298 / 597 + 70</c> = 84; the next opening 83. Every output is worked again by it, 48, 6, 14, 6 and
	/// 4. Of ability 4 the round trip comes back: the limit 614, the knob 307, the work load 85.
	/// </summary>
	/// <remarks><b>Mutations:</b> the knob set without telling the handler; the limit's third taken of c; the knob's
	/// division rounded; the outputs left as they were.</remarks>
	[TestMethod]
	public void OpeningTakesTheWorkLoadRoundTheKnob()
	{
		var lab = FreshLab();
		var stack = new UI.WindowStack();
		var screen = Open( stack, lab, ability: 3 );

		StringAssert.StartsWith( screen.Census(), "workload 84 limit 597 knob 298 dial 4 gauge 597 ability 3 most 60;" );
		CollectionAssert.AreEqual( new[] { 48, 6, 14, 6, 4 }, lab.Kinds.Select( kind => kind.Output ).ToArray() );
		CollectionAssert.AreEqual( new[] { 100, 15, 30, 15, 10 }, lab.Kinds.Select( kind => kind.Effort ).ToArray(), "the efforts written back as they stand" );

		stack.Close( screen );
		Open( stack, lab, ability: 3 );
		Assert.AreEqual( 83, lab.WorkLoad );

		var other = FreshLab();
		StringAssert.StartsWith( Open( new UI.WindowStack(), other, ability: 4 ).Census(), "workload 85 limit 614 knob 307 dial 4 gauge 614 ability 4" );
		CollectionAssert.AreEqual( new[] { 49, 6, 14, 6, 4 }, other.Kinds.Select( kind => kind.Output ).ToArray() );
	}

	/// <summary>
	/// <b>The efforts' seeding works the outputs again by itself</b>: at work load 70 the knob stays at nought, so it tells
	/// nobody, and a lab whose outputs read 1 to 5 opens with every kind's worked from its effort, 40, 5, 11, 5 and 3.
	/// </summary>
	/// <remarks><b>Mutations:</b> the seeding's write-back taken out.</remarks>
	[TestMethod]
	public void TheEffortsSeedingWorksTheOutputs()
	{
		var catalogue = new ParkItemCatalogue( "jungle", data );
		var balance = new ParkBalance( "jungle", easyMode: false );
		var fresh = new FreshPark( "jungle", balance, catalogue, data );
		var stale = fresh.ResearchKinds.Select( ( kind, at ) => kind with { Output = at + 1 } ).ToArray();
		var lab = new ParkResearch( fresh.ObjectControlRecords, catalogue, fresh.ResearchProjects, stale, 70,
			ParkResearch.GroupOpensAt( balance ), fresh.ResearchMostExpensiveProject );

		StringAssert.StartsWith( Open( new UI.WindowStack(), lab, ability: 3 ).Census(), "workload 70 limit 597 knob 0 dial 0 " );
		CollectionAssert.AreEqual( new[] { 40, 5, 11, 5, 3 }, lab.Kinds.Select( kind => kind.Output ).ToArray() );
	}

	/// <summary>
	/// <b>The work-load slider runs up and down, held to its limit</b>: at the top, 1023, it is held to 597 and the work
	/// load is 100, the dial at frame 8; at its foot the work load is 70 and the dial 0; at 300, 85 and frame 4. The knob
	/// stands 513 down, <c>(1023 - 298) × (481 - 67) / 1023</c> under the track's top at 220.
	/// </summary>
	/// <remarks><b>Mutations:</b> the hold taken out; the dial's frame of the value rather than of its thirtieths; the
	/// slider laid across, or its top the lowest.</remarks>
	[TestMethod]
	public void TheWorkLoadSliderIsHeldToItsLimit()
	{
		var lab = FreshLab();
		var screen = Open( new UI.WindowStack(), lab, ability: 3 );
		var slider = Slider( screen, 0xd2d10 );

		Assert.AreEqual( (513, 580), (slider.Thumb!.Rect.Top, slider.Thumb.Rect.Bottom), "the knob at 298" );

		foreach ( var (value, workLoad, dial, held) in new[] { (1023, 100, 8, 597), (0, 70, 0, 0), (300, 85, 4, 300) } )
		{
			slider.SetValue( value );
			slider.Moved!();
			Assert.AreEqual( (held, workLoad), (slider.Value, lab.WorkLoad), $"{value}" );
			StringAssert.Contains( screen.Census(), $"knob {held} dial {dial} " );
		}

		lab.SetWorkLoad( 120 );
		Assert.AreEqual( 100, lab.WorkLoad, "held to a hundred" );
	}

	/// <summary>
	/// <b>An effort slider sets its kind's effort, and every output follows</b>: the rides to 50 of a sum of 120 at work
	/// load 84 give 34, 10, 21, 10 and 6. An effort over a hundred is held there, and so is one under nought, which the
	/// original takes unsigned.
	/// </summary>
	/// <remarks><b>Mutations:</b> the slider's kinds swapped; only the kind's own output worked again.</remarks>
	[TestMethod]
	public void AnEffortSliderSetsItsKind()
	{
		var lab = FreshLab();
		var screen = Open( new UI.WindowStack(), lab, ability: 3 );
		var rides = Slider( screen, 0xd2d0e );

		rides.SetValue( 50 );
		rides.Moved!();
		CollectionAssert.AreEqual( new[] { 50, 15, 30, 15, 10 }, lab.Kinds.Select( kind => kind.Effort ).ToArray() );
		CollectionAssert.AreEqual( new[] { 34, 10, 21, 10, 6 }, lab.Kinds.Select( kind => kind.Output ).ToArray() );

		Slider( screen, 0xd2d0b ).SetValue( 40 );
		Slider( screen, 0xd2d0b ).Moved!();
		Assert.AreEqual( 40, lab.Kinds[ItemDescriptionFile.Feature].Effort, "0xd2d0b is the miscellaneous" );

		Assert.AreEqual( (100, 100), (lab.SetEffort( 1, 150 ), lab.SetEffort( 2, -5 )) );
	}

	/// <summary>
	/// <b>The arrows pick the kind's next and previous project</b>: right from Dino Karts (project 4) is project 5, item
	/// 1106 (550, so 593 of 1024), left from there is 4 again, and left from 4, with no open ride project of group 1
	/// before it, is the last such after it, project 7.
	/// </summary>
	/// <remarks><b>Mutations:</b> the left arrow taking the first after; the bar left unset after an arrow.</remarks>
	[TestMethod]
	public void TheArrowsPickTheKindsProjects()
	{
		var lab = FreshLab();
		var screen = Open( new UI.WindowStack(), lab, ability: 3 );

		Arrow( screen, 0xd2d01, 0xd2d04 ).Clicked!();
		Assert.AreEqual( (5, 1106, 593), (lab.Kinds[0].CurrentProject, lab.CurrentItem( 0 ), Bar( screen, 0xd2d01 ).Units) );

		Arrow( screen, 0xd2d01, 0xd2d03 ).Clicked!();
		Assert.AreEqual( 4, lab.Kinds[0].CurrentProject );
		Arrow( screen, 0xd2d01, 0xd2d03 ).Clicked!();
		Assert.AreEqual( (7, 1180), (lab.Kinds[0].CurrentProject, lab.CurrentItem( 0 )) );
	}

	/// <summary>
	/// <b>A change in the researchers' ability sets the limit and the knob again</b>, on the next pass (message
	/// <c>0x1e</c>): ability 6 gives 631 and a knob of <c>14 × 631 / 30</c> = 294, whose round trip takes the 84 to 83.
	/// </summary>
	[TestMethod]
	public void AChangedAbilitySetsTheLimitAgain()
	{
		var lab = FreshLab();
		var ability = 3;
		var screen = new UI.ParkResearchScreen( new UI.WindowStack(), lab, () => ability, Most );
		Assert.AreEqual( 84, lab.WorkLoad );

		ability = 6;
		screen.Update();
		StringAssert.StartsWith( screen.Census(), "workload 83 limit 631 knob 294 " );
	}

	/// <summary>
	/// <b>The pumps take their kinds' rates as the screen opens, and move 30 passes a second</b>: with one researcher of
	/// ability 3 of a most of 60 the rides' output 48 is a rate of <c>48 × 5 / 100</c> = 2 and the others' 6, 14, 6 and 4
	/// nought; the rides' pump starts at <c>0x30</c> at 14417, the rest stand. A second is 30 passes, and
	/// <c>0x300000 + 30 × 28834</c> is frame <c>0x3d</c>, drawn on the panel's pump; seven passes and a half more start the
	/// run, drawn as <c>fluid</c> moved by the half pass owed. The rides' effort dragged to nought takes its output, and its
	/// rate, to nought: the pump drains from <c>0x20</c>. Of ability 60 every kind's rate is its output, and every pump starts.
	/// </summary>
	/// <remarks><b>Mutations:</b> the rate given without a current project's test; 60 passes a second; the drag's refresh
	/// skipped; the pumps refreshed before the opening's efforts are seeded; the shift not handed to the control, or without the passes owed.</remarks>
	[TestMethod]
	public void ThePumpsTakeTheirRatesAndMove()
	{
		var lab = FreshLab();
		var screen = Open( new UI.WindowStack(), lab, ability: 3 );
		var rides = screen.Pump( 0 );

		CollectionAssert.AreEqual( new[] { 2, 0, 0, 0, 0 }, Enumerable.Range( 0, 5 ).Select( screen.PumpRate ).ToArray() );
		Assert.AreEqual( (UI.ResearchPump.PumpState.Starting, 14417), (rides.State, rides.Speed) );
		CollectionAssert.AreEqual( new[] { "Stopped", "Stopped", "Stopped", "Stopped" },
			new[] { 1, 2, 3, 4 }.Select( kind => screen.Pump( kind ).State.ToString() ).ToArray() );

		screen.RunPasses( 0.5f );
		screen.RunPasses( 0.5f );
		Assert.AreEqual( (30, 0x3d), (rides.Passes, rides.Frame) );

		var drawn = Controls( screen.Root ).Single( control => control.Id == 0xd2d01 ).Children.Single( control => control.Id == 0xd2d06 );
		Assert.AreEqual( (0x3d, 0f), (drawn.Frame, drawn.TextureShift) );

		// Seven passes and a half more: the 37th starts the run, drawn as fluid moved by the half pass owed.
		screen.RunPasses( 0.25f );
		Assert.AreEqual( (37, UI.ResearchPump.PumpState.Running, 0), (rides.Passes, rides.State, drawn.Frame) );
		Assert.AreEqual( -(0.5f * 28834 / 65536) / 31, drawn.TextureShift, 1e-6f );

		// A frame of exactly one pass, its pass boundary half way: the scroll moved by the whole pass, the pass counted once.
		screen.RunPasses( 1f / 30 );
		Assert.AreEqual( (38, UI.ResearchPump.PumpState.Running), (rides.Passes, rides.State) );
		Assert.AreEqual( 1.5f * 28834, rides.Scroll, 1f );
		Assert.AreEqual( -(1.5f * 28834 / 65536) / 31, drawn.TextureShift, 1e-5f );

		var effort = Slider( screen, 0xd2d0e );
		effort.SetValue( 0 );
		effort.Moved!();
		Assert.AreEqual( (UI.ResearchPump.PumpState.Stopping, 0x20, 0x20, 0f), (rides.State, rides.Frame, drawn.Frame, drawn.TextureShift) );

		var all = Open( new UI.WindowStack(), FreshLab(), ability: 60 );
		CollectionAssert.AreEqual( new[] { 48, 6, 14, 6, 4 }, Enumerable.Range( 0, 5 ).Select( all.PumpRate ).ToArray() );
		Assert.IsTrue( Enumerable.Range( 0, 5 ).All( kind => all.Pump( kind ).State == UI.ResearchPump.PumpState.Starting ) );

		// The sideshows with no current project: "All Items Researched", a rate of nought whatever their output, a pump
		// that stands, and the loop's mean of the other four.
		var catalogue = new ParkItemCatalogue( "jungle", data );
		var balance = new ParkBalance( "jungle", easyMode: false );
		var fresh = new FreshPark( "jungle", balance, catalogue, data );
		var none = fresh.ResearchKinds.Select( ( kind, at ) => at == ItemDescriptionFile.SideShow ? kind with { CurrentProject = -1 } : kind ).ToArray();
		var bare = new ParkResearch( fresh.ObjectControlRecords, catalogue, fresh.ResearchProjects, none, fresh.ResearchWorkLoad,
			ParkResearch.GroupOpensAt( balance ), fresh.ResearchMostExpensiveProject );
		var left = Open( new UI.WindowStack(), bare, ability: 60 );

		Assert.AreNotEqual( 0, bare.Kinds[ItemDescriptionFile.SideShow].Output );
		Assert.AreEqual( (0, UI.ResearchPump.PumpState.Stopped), (left.PumpRate( ItemDescriptionFile.SideShow ), left.Pump( ItemDescriptionFile.SideShow ).State) );
		StringAssert.Contains( Bar( left, 0xd2d09 ).Text, Localization.Get( UIStrings.AllItemsResearched ) );
	}

	/// <summary>
	/// <b>A kind that finishes a project with the screen open is lettered and filled again at once</b>
	/// (<c>Research_OnKindFinished</c>, <c>0x004aaec0</c>): Dino Karts finished by hand-outs of ability 1000, the rides'
	/// label is the next project's and its bar that project's fill, not Dino Karts' until the next arrow. Closed, the
	/// screen hears no more.
	/// </summary>
	/// <remarks><b>Mutations:</b> the telling not raised; raised before the lab's pick; the closed screen left listening.</remarks>
	[TestMethod]
	public void AFinishedKindIsLetteredAgainAtOnce()
	{
		var lab = FreshLab();
		var stack = new UI.WindowStack();
		var screen = Open( stack, lab, ability: 3 );
		var bar = Bar( screen, 0xd2d01 );
		var before = bar.Text;
		StringAssert.Contains( before, "DinoKarts" );
		var others = new[] { 4, 1, 2, 3 }.Select( kind => (Bar( screen, Panel( kind ) ).Text, Bar( screen, Panel( kind ) ).Units) ).ToArray();

		var finished = new List<ParkResearch.Finished>();

		for ( var tick = 0; tick < 50 && !finished.Any( done => done.Kind == 0 ); tick += 20 )
			finished.AddRange( lab.SpendPoints( 1000, tick ) );

		Assert.AreEqual( 1150, finished.First( done => done.Kind == 0 ).ItemId );
		Assert.AreNotEqual( before, bar.Text );
		StringAssert.Contains( bar.Text, ParkObjectNames.NameOf( lab.Item( lab.CurrentItem( 0 ) )!.Value ) );
		Assert.AreEqual( (100 - lab.PercentLeftOfDearest( 0 )) * 1024 / 100, bar.Units );
		CollectionAssert.AreEqual( others, new[] { 4, 1, 2, 3 }.Select( kind => (Bar( screen, Panel( kind ) ).Text, Bar( screen, Panel( kind ) ).Units) ).ToArray(),
			"the other kinds are left as they stand" );

		stack.Close( screen );
		Assert.IsNull( lab.KindFinished );
	}

	/// <summary>A slider running up and down in a 2048 x 1536 window (a virtual unit is a pixel), a thumb 40 high on a track 400 high.</summary>
	private static (UI.UiSlider Slider, UI.UiSliderThumb Thumb) AVerticalSlider()
	{
		var slider = new UI.UiSlider
		{
			Rect = new UI.UiRect( 0, 100, 100, 500 ), HitRect = new UI.UiRect( 0, 100, 100, 500 ),
			Track = new UI.UiRect( 0, 100, 100, 500 ), Maximum = 100, Vertical = true
		};
		var thumb = slider.AddThumb( new UI.UiSliderThumb { Rect = new UI.UiRect( 0, 100, 100, 140 ) } );

		return (slider, thumb);
	}

	/// <summary>A slider running across, a thumb 40 wide on a track 400 wide.</summary>
	private static (UI.UiSlider Slider, UI.UiSliderThumb Thumb) AnAcrossSlider()
	{
		var slider = new UI.UiSlider
		{
			Rect = new UI.UiRect( 0, 0, 400, 100 ), HitRect = new UI.UiRect( 0, 0, 400, 100 ),
			Track = new UI.UiRect( 0, 0, 400, 100 ), Maximum = 100
		};
		var thumb = slider.AddThumb( new UI.UiSliderThumb { Rect = new UI.UiRect( 0, 0, 40, 100 ) } );

		return (slider, thumb);
	}

	/// <summary>
	/// <b>A vertical slider's thumb is dragged and its value read from the thumb's height above the track's foot</b>
	/// (<c>Slider_ReadThumb</c>, <c>0x0066af19</c>): at 50 the thumb stands 280 to 320; taken at y 300 and drawn to 240 its
	/// top is 220, 240 above the foot over a travel of 360, so 66; dragged past the top it stops there at 100, and past the
	/// foot at nought.
	/// </summary>
	/// <remarks><b>Mutations:</b> the drag read as an across one; the value taken from the top down.</remarks>
	[TestMethod]
	public void AVerticalSliderIsDraggedByItsThumbsHeight()
	{
		var screen = Screen.Size;
		Screen.Size = new Point2( 2048, 1536 );

		try
		{
			var (slider, thumb) = AVerticalSlider();
			slider.SetValue( 50 );
			Assert.AreEqual( (280, 320), (thumb.Rect.Top, thumb.Rect.Bottom) );

			var moved = 0;
			slider.Moved = () => ++moved;
			thumb.PointerPressed( 50, 300 );
			thumb.PointerDragged( 50, 240 );
			Assert.AreEqual( (66, 220, 260, 1), (slider.Value, thumb.Rect.Top, thumb.Rect.Bottom, moved) );

			thumb.PointerDragged( 50, 0 );
			Assert.AreEqual( (100, 100), (slider.Value, thumb.Rect.Top) );
			thumb.PointerDragged( 50, 1000 );
			Assert.AreEqual( (0, 460), (slider.Value, thumb.Rect.Top) );
			thumb.PointerReleased();
		}
		finally
		{
			Screen.Size = screen;
		}
	}

	/// <summary>
	/// <b>A click off the thumb pages the value towards the click, by the thumb's far edges</b> (<c>Slider_PageTowards</c>,
	/// <c>0x0066b8aa</c>): a vertical slider pages down where the click is below the thumb's bottom, up otherwise; one
	/// running across pages down where the click is left of the thumb's right edge, up otherwise. The clicks inside the
	/// thumb's own band, 300 down a thumb of 280 to 320 and 200 along one of 180 to 220, are the ones a rule of the
	/// thumb's near edges gets the other way about.
	/// </summary>
	/// <remarks><b>Mutations:</b> the vertical test against the thumb's top; the across test against its left; either
	/// direction reversed; a page of more than one.</remarks>
	[TestMethod]
	public void APageClickGoesByTheThumbsFarEdge()
	{
		var screen = Screen.Size;
		Screen.Size = new Point2( 2048, 1536 );

		try
		{
			var (up, thumb) = AVerticalSlider();
			up.SetValue( 50 );
			Assert.AreEqual( (280, 320), (thumb.Rect.Top, thumb.Rect.Bottom) );

			foreach ( var (y, expected) in new[] { (300f, 51), (250f, 51), (320f, 51), (321f, 49), (400f, 49) } )
			{
				up.SetValue( 50 );
				up.LeftClickedAt( 10, y );
				Assert.AreEqual( expected, up.Value, $"down {y}" );
			}

			up.SetValue( 50 );
			up.RightClickedAt( 10, 300 );
			Assert.AreEqual( 51, up.Value, "a right click pages the same way" );

			var (across, bar) = AnAcrossSlider();
			across.SetValue( 50 );
			Assert.AreEqual( (180, 220), (bar.Rect.Left, bar.Rect.Right) );

			foreach ( var (x, expected) in new[] { (200f, 49), (219f, 49), (221f, 51), (300f, 51), (100f, 49) } )
			{
				across.SetValue( 50 );
				across.LeftClickedAt( x, 10 );
				Assert.AreEqual( expected, across.Value, $"across {x}" );
			}
		}
		finally
		{
			Screen.Size = screen;
		}
	}

	/// <summary>
	/// <b>The work-load slider is paged as the original pages it</b>: at the knob's 298 (the thumb 513 to 580) a click at 540,
	/// inside the thumb's band, is above its bottom and pages up to 299, the work load following; one at 600 pages down.
	/// </summary>
	/// <remarks><b>Mutations:</b> the vertical test against the thumb's top.</remarks>
	[TestMethod]
	public void ThePageClickMovesTheWorkLoad()
	{
		var lab = FreshLab();
		var screen = Open( new UI.WindowStack(), lab, ability: 3 );
		var slider = Slider( screen, 0xd2d10 );
		Assert.AreEqual( (513, 580, 298), (slider.Thumb!.Rect.Top, slider.Thumb.Rect.Bottom, slider.Value) );

		slider.LeftClickedAt( 100, 540 );
		Assert.AreEqual( 299, slider.Value );
		Assert.AreEqual( 85, lab.WorkLoad );
		Assert.AreEqual( (512, 579), (slider.Thumb.Rect.Top, slider.Thumb.Rect.Bottom) );

		slider.LeftClickedAt( 100, 600 );
		Assert.AreEqual( 298, slider.Value );
	}

	/// <summary>
	/// <b>The 1000 ms timer sets the five bars again</b> (message <c>0x10</c>): points spent into the rides' project with the
	/// screen open and nobody to say so leave the bar where it was until the timer's second passes, and then it is the new
	/// fill.
	/// </summary>
	/// <remarks><b>Mutations:</b> the timer's fills taken out; the timer owing a tick every pass.</remarks>
	[TestMethod]
	public void TheTimerSetsTheFillsAgain()
	{
		try
		{
			Time.PinWall( 5000 );

			var lab = FreshLab();
			var screen = new UI.ParkResearchScreen( new UI.WindowStack(), lab, () => 3, Most );
			var bar = Bar( screen, 0xd2d01 );
			var before = bar.Units;
			Assert.AreEqual( 552, before );

			Assert.AreEqual( 0, lab.SpendPoints( 500, 0 ).Count( done => done.Kind == 0 ) );
			var now = (100 - lab.PercentLeftOfDearest( 0 )) * 1024 / 100;
			Assert.AreNotEqual( before, now );

			Time.PinWall( 5999 );
			screen.Update();
			Assert.AreEqual( before, bar.Units, "not before the second is out" );

			Time.PinWall( 6000 );
			screen.Update();
			Assert.AreEqual( now, bar.Units );
		}
		finally
		{
			Time.PinWall( null );
		}
	}

	/// <summary>
	/// <b>Message 200 is posted as the screen opens and withdrawn as it closes</b> (<c>FUN_00486b00( 200 )</c>; event
	/// <c>0x14</c>): the advisor's queue holds row 200 while it is up and not after.
	/// </summary>
	/// <remarks><b>Mutations:</b> the post left out; the withdrawal left out.</remarks>
	[TestMethod]
	public void TheAdvisorIsPostedTheScreensLineWhileItIsOpen()
	{
		var kept = Level.Current;

		try
		{
			var lines = new ParkAdvisorMessages( new SettingsFile( "Advisor/Advisor.sam" ) );
			var level = (Level)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject( typeof( Level ) );
			typeof( Level ).GetProperty( nameof( Level.ParkState ) )!.SetValue( level, new ParkState( null ) { AdvisorMessages = lines } );
			Level.Current = level;

			var stack = new UI.WindowStack();
			var screen = new UI.ParkResearchScreen( stack, FreshLab(), () => 3, Most );
			StringAssert.Contains( lines.Census(), "pending=0 ", "built, not yet opened" );

			stack.Open( screen );
			StringAssert.Contains( lines.Census(), "pending=1 " );
			StringAssert.Contains( lines.Census(), "slot=200/" );

			stack.Close( screen );
			StringAssert.Contains( lines.Census(), "pending=0 " );
		}
		finally
		{
			Level.Current = kept;
		}
	}

	/// <summary>
	/// <b>The upgrades' arrows skip a project whose item is not yet researched</b>: of the nine upgrade projects, 37 to 39
	/// need an item not yet researched and 13 and 15 (items 1170 and 1112) not open in a fresh lab (<c>Eligible</c>), so from
	/// project 0 the left arrow goes to 14, the last open one, and not to 39; right goes 8, 10, 14 and then round to 0, not
	/// on to 15 or 37.
	/// </summary>
	/// <remarks><b>Mutations:</b> the left arrow's eligibility test taken out; the right arrow's.</remarks>
	[TestMethod]
	public void TheUpgradeArrowsSkipWhatIsNotYetOpen()
	{
		var lab = FreshLab();
		var screen = Open( new UI.WindowStack(), lab, ability: 3 );
		Assert.AreEqual( 0, lab.Kinds[ParkResearch.Upgrade].CurrentProject );

		Arrow( screen, 0xd2d07, 0xd2d03 ).Clicked!();
		Assert.AreEqual( 14, lab.Kinds[ParkResearch.Upgrade].CurrentProject );

		Arrow( screen, 0xd2d07, 0xd2d04 ).Clicked!();
		Assert.AreEqual( 0, lab.Kinds[ParkResearch.Upgrade].CurrentProject );

		var seen = new List<int>();

		for ( var i = 0; i < 4; ++i )
		{
			Arrow( screen, 0xd2d07, 0xd2d04 ).Clicked!();
			seen.Add( lab.Kinds[ParkResearch.Upgrade].CurrentProject );
		}

		CollectionAssert.AreEqual( new[] { 8, 10, 14, 0 }, seen );
	}

	/// <summary>
	/// <b>Only the effort sliders tell of their thumb's press and release</b> (the 0x801 and 0x802 arms, which the handler
	/// takes after excluding the work-load slider, <c>0xd2d10</c>, from them): the work load's thumb says nothing.
	/// </summary>
	/// <remarks><b>Mutations:</b> the work-load slider's two hooks wired as the efforts' are.</remarks>
	[TestMethod]
	public void OnlyTheEffortSlidersTellOfTheirThumbs()
	{
		var screen = Open( new UI.WindowStack(), FreshLab(), ability: 3 );

		Assert.IsNull( Slider( screen, 0xd2d10 ).ThumbPressed );
		Assert.IsNull( Slider( screen, 0xd2d10 ).ThumbReleased );

		foreach ( var row in UI.ParkResearchScreen.Rows )
		{
			Assert.IsNotNull( Slider( screen, row.Slider ).ThumbPressed, $"{row.Slider:x}" );
			Assert.IsNotNull( Slider( screen, row.Slider ).ThumbReleased, $"{row.Slider:x}" );
		}
	}
}
