using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeoVeldrid;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace OpenTPW.Tests;

/// <summary>
/// The park's screens - the six management screens and the object windows - as the original keeps them: built onto the
/// park's own layer, not modal, one open at a time (<c>DAT_007c24c8</c>). The gadget answers beside one, a press on its
/// bare frame is the interface's, a left press beside it is kept from the park's idle click, and while it is open only
/// the shortcuts' keys are run. See <c>UiWindow.ParkScreen</c> and <c>docs/exe/park-engine.md</c>, "A park screen is open".
///
/// <para>
/// <b>The frame is the game's own</b>: SDL's events through <see cref="Input.UpdateFrom"/> and the real stack's whole
/// update under the real park front end. No park is loaded, so the hand holds a build tool where it holds anything.
/// </para>
/// </summary>
[TestClass]
public class ParkScreenTests
{
	/// <summary>The middle of the gadget's Buy, Info and camcorder buttons, on the interface's 2048x1536.</summary>
	private static readonly Vector2 Buy = new( 229, 1181 );
	private static readonly Vector2 Info = new( 346, 1192 );
	private static readonly Vector2 Camcorder = new( 261, 1414 );

	/// <summary>The buy screen's button to hire, and the hire screen's button to buy: one point of the layout.</summary>
	private static readonly Vector2 Sibling = new( 1917, 634 );

	/// <summary>The buy screen's bare frame, at its left edge, where none of its controls is.</summary>
	private static readonly Vector2 Frame = new( 200, 500 );

	/// <summary>A point of the park below every screen, which no control is over.</summary>
	private static readonly Vector2 Grass = new( 1100, 1250 );

	private Point2 _screen;
	private Level? _level;
	private float _orbitYaw;
	private Vector3 _orbitLooksAt;

	[TestInitialize]
	public void StartWithNothingHeld()
	{
		Log ??= new();
		FileSystem = GameData.Required();

		_screen = Screen.Size;
		Screen.Size = new Point2( 2048, 1536 );
		_level = Level.Current;
		_orbitYaw = ParkOrbitCameraMode.Yaw;
		_orbitLooksAt = ParkOrbitCameraMode.PointOfInterest;

		Input.ForgetHeldKeys();
		Input.Mouse = new();
		Input.TextCaptured = false;
		Unimplemented.Forget();
		Time.Now = 100f;
		UI.ParkCategoryScreens.Seed();
	}

	[TestCleanup]
	public void PutItAllBack()
	{
		UI.ParkCategoryScreens.Seed();
		ParkBuildMode.Forget();
		ParkCamcorderCameraMode.Forget();
		Camera.SetCameraMode<CameraMode>();
		ParkOrbitCameraMode.Yaw = _orbitYaw;
		ParkOrbitCameraMode.PointOfInterest = _orbitLooksAt;
		Level.Current = _level!;
		Input.ForgetHeldKeys();
		Input.Mouse = new();
		Input.TextCaptured = false;
		Unimplemented.Forget();

		// The stack's own static, which the next test's first frame would otherwise read stale.
		new UI.WindowStack().Update();

		Screen.Size = _screen;
	}

	/// <summary>
	/// <b>The gadget answers beside an open screen, and the screen it opens closes the one that was open</b>: with the
	/// buy screen up, a click on the gadget's Info button leaves one park screen, and it is not the buy screen.
	/// <c>FUN_004a0940</c> tests the game menu alone, and every opener starts with <c>FUN_00485b40</c>.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> the buy screen modal again, and the click never reaches the button; an opener that closes
	/// nothing leaves two screens.
	/// </remarks>
	[TestMethod]
	public void TheGadgetAnswersBesideAScreenAndItsScreenClosesTheOneOpen()
	{
		var stack = APark();

		Click( stack, Buy );
		Assert.AreEqual( "ParkGadget, ParkViewfinder, ParkBuyScreen", Names( stack ) );
		Assert.IsFalse( stack.ModalUp, "the buy screen is not modal" );

		Click( stack, Info );

		Assert.AreEqual( 1, stack.Windows.Count( window => window.ParkScreen ), "one park screen at a time" );
		Assert.IsFalse( stack.Windows.Any( window => window is UI.ParkBuyScreen ), "and the buy screen is the one that closed" );
	}

	/// <summary>
	/// <b>The buy screen asks the advisor for its own line as it opens and takes it back as it closes</b>: the opener
	/// posts message 0xbc on a screen it has just built (<c>FUN_004acc70</c>, <c>0x004ad1e2</c>) and on no other, and the
	/// screen's destroy withdraws it (<c>FUN_004ac270</c>, event 0x14), which empties its slot and, while he says it,
	/// cuts him off on the game's clock.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> no post at the opening; a post as the screen is made, before it is opened, or on tick nought; a post each time an
	/// open screen is shown again or its tab picked; no withdrawal at the close; the tag message's id for either; the
	/// wall clock for the game's.
	/// </remarks>
	[TestMethod]
	public void TheBuyScreenAsksTheAdvisorForItsLineAsItOpensAndTakesItBackAsItCloses()
	{
		using var clock = new SimulationClockScope();
		var stack = APark();
		var lines = new ParkAdvisorMessages( new SettingsFile( "Advisor/Advisor.sam" ) );
		var park = new ParkState( null ) { AdvisorMessages = lines };
		var said = 0;
		int Play( int response, int sample ) { ++said; return 9000; }

		// The park's 22nd day, where the control panel's line scores: the census scores each message on the last tick
		// the advisor was handed, so it names the tick of the post.
		for ( var i = 0; i < 500; ++i )
			park.AdvanceGameTick();

		// The wall clock far past any line's end: a cut is judged on the game's clock, which stands at nought.
		Time.PinWall( 60000 );

		try
		{
			var first = new UI.ParkBuyScreen( stack ) { Park = park };
			StringAssert.Contains( lines.Census(), "pending=0 attempts=0 ", "made, and not yet opened" );
			StringAssert.Contains( lines.Census(), "message=185/score=0/" );
			stack.Open( first );
			StringAssert.Contains( lines.Census(), "pending=1 attempts=0 threshold=25 slot=188/subject=0 " );
			StringAssert.Contains( lines.Census(), "message=185/score=110/", "posted on the park's own tick" );

			// Closed before he says it: the slot alone.
			stack.Close( first );
			StringAssert.Contains( lines.Census(), "pending=0 attempts=0 " );
			StringAssert.Contains( lines.Census(), "message=188/score=1000/line=-1/lastTick=0/said=0/slaps=0" );

			// Opened, said, and closed while he says it.
			var second = new UI.ParkBuyScreen( stack ) { Park = park };
			stack.Open( second );
			lines.Tick( park.GameTick, 0, Play );
			StringAssert.Contains( lines.Census(), "pending=0 attempts=1 " );
			StringAssert.Contains( lines.Census(), "message=188/score=1000/line=0/lastTick=500/said=1/slaps=0" );
			stack.Close( second );
			StringAssert.Contains( lines.Census(), "message=188/score=1000/line=0/lastTick=500/said=0/slaps=1", "cut off" );
			StringAssert.Contains( lines.Census(), "message=284/score=1000/line=-1/lastTick=0/said=0/slaps=0", "the tag message's record is not this one's" );

			// A screen already open asks for nothing more: he has been cut off, so a post would be taken.
			var third = new UI.ParkBuyScreen( stack ) { Park = park };
			stack.Open( third );
			Assert.IsTrue( lines.Withdraw( ParkAdvisorMessages.BuyScreen, 0, () => { } ) );
			StringAssert.Contains( lines.Census(), "pending=0 attempts=1 " );
			third.Shown();
			third.PickTab( 2 );
			StringAssert.Contains( lines.Census(), "pending=0 attempts=1 ", "shown again, and its tab picked" );
			Assert.AreEqual( 1, said );
		}
		finally
		{
			Time.PinWall( null );
		}
	}

	/// <summary>
	/// <b>The hire screen asks the advisor for its own line as it opens and takes it back as it closes</b>: the opener
	/// posts message 0xbd on a screen it has built (<c>FUN_0049bdd0</c>, <c>0x0049c800</c>), and the screen's destroy
	/// withdraws it (<c>FUN_0049b650</c>, event 0x14), which empties its slot and, while he says it, cuts him off on
	/// the game's clock. The buy screen's own button to it closes the buy screen first, so the buy screen's line is
	/// taken back as the hire screen's is asked for.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> no post at the opening; a post as the screen is made, before it is opened, or on tick nought;
	/// a post each time an open screen is shown again; no withdrawal at the close; the buy screen's id for either; the
	/// wall clock for the game's.
	/// </remarks>
	[TestMethod]
	public void TheHireScreenAsksTheAdvisorForItsLineAsItOpensAndTakesItBackAsItCloses()
	{
		using var clock = new SimulationClockScope();
		var stack = APark();
		var lines = new ParkAdvisorMessages( new SettingsFile( "Advisor/Advisor.sam" ) );
		var park = new ParkState( null ) { AdvisorMessages = lines };
		var said = new List<int>();
		int Play( int response, int sample ) { said.Add( sample ); return 9000; }

		// The park's 22nd day, where the control panel's line scores: the census scores each message on the last tick
		// the advisor was handed, so it names the tick of the post.
		for ( var i = 0; i < 500; ++i )
			park.AdvanceGameTick();

		// The wall clock far past any line's end: a cut is judged on the game's clock, which stands at nought.
		Time.PinWall( 60000 );

		try
		{
			var first = new UI.ParkHireScreen( stack, park );
			StringAssert.Contains( lines.Census(), "pending=0 attempts=0 ", "made, and not yet opened" );
			StringAssert.Contains( lines.Census(), "message=185/score=0/" );
			stack.Open( first );
			StringAssert.Contains( lines.Census(), "pending=1 attempts=0 threshold=25 slot=189/subject=0 " );
			StringAssert.Contains( lines.Census(), "message=185/score=110/", "posted on the park's own tick" );

			// Closed before he says it: the slot alone.
			stack.Close( first );
			StringAssert.Contains( lines.Census(), "pending=0 attempts=0 " );
			StringAssert.Contains( lines.Census(), "message=189/score=1000/line=-1/lastTick=0/said=0/slaps=0" );

			// Opened, said, and closed while he says it.
			var second = new UI.ParkHireScreen( stack, park );
			stack.Open( second );
			lines.Tick( park.GameTick, 0, Play );
			StringAssert.Contains( lines.Census(), "pending=0 attempts=1 " );
			StringAssert.Contains( lines.Census(), "message=189/score=1000/line=0/lastTick=500/said=1/slaps=0" );
			stack.Close( second );
			StringAssert.Contains( lines.Census(), "message=189/score=1000/line=0/lastTick=500/said=0/slaps=1", "cut off" );
			StringAssert.Contains( lines.Census(), "message=188/score=1000/line=-1/lastTick=0/said=0/slaps=0", "the buy screen's record is not this one's" );

			// A screen already open asks for nothing more: he has been cut off, so a post would be taken.
			var third = new UI.ParkHireScreen( stack, park );
			stack.Open( third );
			Assert.IsTrue( lines.Withdraw( ParkAdvisorMessages.HireScreen, 0, () => { } ) );
			StringAssert.Contains( lines.Census(), "pending=0 attempts=1 " );
			third.Shown();
			StringAssert.Contains( lines.Census(), "pending=0 attempts=1 ", "shown again" );
			stack.Close( third );

			// From the buy screen by its own button: the buy screen's line is cut off and the hire screen's said.
			var buy = new UI.ParkBuyScreen( stack ) { Park = park };
			stack.Open( buy );
			park.AdvanceGameTick();
			lines.Tick( park.GameTick, 0, Play );
			StringAssert.Contains( lines.Census(), "message=188/score=1000/line=0/lastTick=501/said=1/slaps=0" );
			stack.Close( buy );
			stack.Open( new UI.ParkHireScreen( stack, park ) );
			StringAssert.Contains( lines.Census(), "message=188/score=1000/line=0/lastTick=501/said=0/slaps=1" );
			StringAssert.Contains( lines.Census(), "pending=1 attempts=2 threshold=25 slot=189/subject=0 " );
			park.AdvanceGameTick();
			lines.Tick( park.GameTick, 0, Play );
			StringAssert.Contains( lines.Census(), "message=189/score=1000/line=0/lastTick=502/said=1/slaps=1" );
			CollectionAssert.AreEqual( new[] { 381, 380, 381 }, said );
		}
		finally
		{
			Time.PinWall( null );
		}
	}

	/// <summary>
	/// <b>The gadget's Buy button opens whichever of buy and hire was last left on</b> (<c>FUN_004a0940( 1 )</c> on
	/// <c>DAT_007cb28c</c>): each screen's opener leaves the category on its own screen, whoever asked for it
	/// (<c>FUN_004a0810</c> at <c>0x004acc8f</c> and <c>0x0049bdf5</c>), the gadget's builder puts it back on buy as a
	/// park is entered from the front end, and the button with the hire screen up builds the hire screen again.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> the button always buy; always hire; the hire screen not remembered; the buy screen not
	/// remembered, so hire sticks; a screen remembered as it is made, not as it is opened; the seed leaving hire; a
	/// park loaded from a park seeding too; a park entered from the lobby not seeding; the button with the hire
	/// screen up leaving it as it is.
	/// </remarks>
	[TestMethod]
	public void TheBuyButtonOpensWhicheverOfBuyAndHireWasLastLeftOn()
	{
		var stack = APark();

		void CloseTheScreen() => stack.Close( stack.Windows.Single( window => window.ParkScreen ) );

		Click( stack, Buy );
		Assert.AreEqual( "ParkGadget, ParkViewfinder, ParkBuyScreen", Names( stack ), "a park entered: buy" );
		Assert.AreEqual( UI.ParkCategoryScreens.Buy, UI.ParkCategoryScreens.LeftOn( UI.ParkCategoryScreens.Building ) );

		Click( stack, Sibling );
		Assert.AreEqual( "ParkGadget, ParkViewfinder, ParkHireScreen", Names( stack ), "the buy screen's button to hire" );
		Assert.AreEqual( UI.ParkCategoryScreens.Hire, UI.ParkCategoryScreens.LeftOn( UI.ParkCategoryScreens.Building ) );

		CloseTheScreen();
		Click( stack, Buy );
		Assert.AreEqual( "ParkGadget, ParkViewfinder, ParkHireScreen", Names( stack ), "left on hire, so the button opens hire" );

		// With the hire screen up the opener destroys it and builds another.
		var up = stack.Windows.Single( window => window.ParkScreen );
		Click( stack, Buy );
		Assert.AreEqual( "ParkGadget, ParkViewfinder, ParkHireScreen", Names( stack ) );
		Assert.AreNotSame( up, stack.Windows.Single( window => window.ParkScreen ), "a hire screen built again" );

		Click( stack, Sibling );
		Assert.AreEqual( "ParkGadget, ParkViewfinder, ParkBuyScreen", Names( stack ), "the hire screen's button to buy" );
		CloseTheScreen();
		Click( stack, Buy );
		Assert.AreEqual( "ParkGadget, ParkViewfinder, ParkBuyScreen", Names( stack ), "left on buy again" );

		// A hire screen opened any other way is remembered too, and one made and never opened is not.
		stack.Open( new UI.ParkHireScreen( stack ) );
		CloseTheScreen();
		_ = new UI.ParkBuyScreen( stack );
		Click( stack, Buy );
		Assert.AreEqual( "ParkGadget, ParkViewfinder, ParkHireScreen", Names( stack ) );

		// A park loaded from a park's own menu keeps it; one entered from the front end starts on buy.
		CloseTheScreen();
		UI.ParkCategoryScreens.ParkAsked( fromAPark: true );
		Click( stack, Buy );
		Assert.AreEqual( "ParkGadget, ParkViewfinder, ParkHireScreen", Names( stack ), "kept over a load from the menu" );

		CloseTheScreen();
		UI.ParkCategoryScreens.ParkAsked( fromAPark: false );
		Click( stack, Buy );
		Assert.AreEqual( "ParkGadget, ParkViewfinder, ParkBuyScreen", Names( stack ), "a park entered from the lobby" );
	}

	/// <summary>
	/// <b>The Buy button with the buy screen already up closes and opens nothing</b>: the opener only picks its tab
	/// again (<c>0x004acca0</c>), so the screen that was open is the screen that is open.
	/// </summary>
	/// <remarks><b>Mutations:</b> opening a fresh one every click replaces it.</remarks>
	[TestMethod]
	public void BuyWithTheBuyScreenUpLeavesItAsItIs()
	{
		var stack = APark();

		Click( stack, Buy );
		var first = stack.Windows[^1];

		Click( stack, Buy );

		Assert.AreSame( first, stack.Windows[^1] );
		Assert.AreEqual( "ParkGadget, ParkViewfinder, ParkBuyScreen", Names( stack ) );
	}

	/// <summary>
	/// <b>A left press on a screen's bare frame is the interface's, and one beside it is not</b>: the root is one plain
	/// rectangle that takes a press anywhere on it, as it does a right press, and the park below it is still the park.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> a left press that asks only for a control goes through the frame; a screen that takes every
	/// press, as a modal one does, takes the park's too.
	/// </remarks>
	[TestMethod]
	public void APressOnAScreensBareFrameIsTheInterfacesAndOneBesideItIsNot()
	{
		var stack = APark();
		var buy = new UI.ParkBuyScreen( stack );
		stack.Open( buy );

		Assert.IsNull( buy.Root.HitTest( Frame.X, Frame.Y ), "no control of the buy screen's is there" );

		Pointer( stack, Frame, left: true );
		Assert.IsTrue( UI.WindowStack.PointerTaken, "the bare frame" );
		Pointer( stack, Frame, left: false );

		Pointer( stack, Grass, left: true );
		Assert.IsFalse( UI.WindowStack.PointerTaken, "the park below it" );
		Pointer( stack, Grass, left: false );

		Assert.IsTrue( stack.ClickAt( Frame.X, Frame.Y ), "the console's click reads the frame the same way" );
		Assert.IsFalse( stack.ClickAt( Grass.X, Grass.Y ) );
		Assert.IsTrue( stack.TakesRightPress( Frame.X, Frame.Y ), "and so does the right button" );
		Assert.IsFalse( stack.TakesRightPress( Grass.X, Grass.Y ) );
	}

	/// <summary>
	/// <b>A screen's frame hides the control behind it</b>: a window opened under a park screen takes no press through
	/// the screen's rectangle, and takes its own beside it.
	/// </summary>
	/// <remarks><b>Mutations:</b> a hit test that walks on past the screen's frame presses the control behind it.</remarks>
	[TestMethod]
	public void AScreensFrameHidesTheControlBehindIt()
	{
		var stack = APark();
		var clicks = 0;
		stack.Open( new AWindow( stack, parkScreen: false, new UI.UiRect( 150, 450, 250, 1300 ), () => ++clicks ) );
		stack.Open( new UI.ParkBuyScreen( stack ) );

		stack.ClickAt( Frame.X, Frame.Y );
		Assert.AreEqual( 0, clicks, "through the buy screen's frame: nothing" );

		stack.ClickAt( 200, 1280 );
		Assert.AreEqual( 1, clicks, "below the screen: the control's own" );
	}

	/// <summary>
	/// <b>Beside an open screen the park's idle click is kept, and what the hand holds is not</b>: the park proc skips a
	/// press while a screen is open (<c>0x00488741</c>), so nothing is opened or picked up; the release still reaches the
	/// mode's button-up slot, which is where a tool commits.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> keeping nothing lets the idle click through; keeping every press stops the armed tool; a flag
	/// never written, or never cleared, reads the wrong way after the screen opens or closes.
	/// </remarks>
	[TestMethod]
	public void BesideAnOpenScreenTheIdleClickIsKeptAndAnArmedToolIsNot()
	{
		var stack = APark();

		Pointer( stack, Grass, left: false );
		Assert.IsFalse( UI.WindowStack.ParkScreenOpen, "no screen yet" );
		Assert.IsFalse( Level.KeptFromThePark( UI.WindowStack.ParkScreenOpen ) );

		var screen = new AWindow( stack, parkScreen: true );
		stack.Open( screen );
		Pointer( stack, Grass, left: false );

		Assert.IsTrue( UI.WindowStack.ParkScreenOpen, "the stack says a screen is open" );
		Assert.IsTrue( Level.KeptFromThePark( UI.WindowStack.ParkScreenOpen ), "idle: the press is kept" );

		ParkBuildMode.Arm( ParkBuildMode.Path );
		Assert.IsFalse( Level.KeptFromThePark( UI.WindowStack.ParkScreenOpen ), "the path tool armed: it is the tool's" );
		ParkBuildMode.Forget();

		stack.Close( screen );
		Pointer( stack, Grass, left: false );
		Assert.IsFalse( UI.WindowStack.ParkScreenOpen, "closed: the park's again" );
	}

	/// <summary>
	/// <b>The park's own frame joins the two</b>: after the stack's update the level reads the left button. With no
	/// screen open a press on the park is a world click; with one open and nothing held it is none, on the screen's bare
	/// frame or beside it; with a tool armed beside the screen it is the tool's, and anchors its run.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> the level's frame not asking whether a screen is open clicks the park beside it; not asking
	/// whether the interface took the press clicks through the frame.
	/// </remarks>
	[TestMethod]
	public void TheParksFrameKeepsAnIdleClickBesideAScreenAndGivesAnArmedToolIts()
	{
		var stack = APark();
		var park = (Level)RuntimeHelpers.GetUninitializedObject( typeof( Level ) );
		typeof( Level ).GetProperty( nameof( Level.Kind ) )!.SetValue( park, Level.Scene.Park );
		typeof( Level ).GetField( "_windows", BindingFlags.Instance | BindingFlags.NonPublic )!.SetValue( park, stack );
		var cell = typeof( ParkPicking ).GetProperty( nameof( ParkPicking.Cell ) )!;
		var cellWas = cell.GetValue( null );
		cell.SetValue( null, (20 * ParkWorld.MapSize) + 47 + 1 );

		var clicks = 0;
		Logger.LogDelegate count = ( _, text ) => clicks += text.Contains( "world click:" ) ? 1 : 0;
		Logger.OnLog += count;

		try
		{
			WorldPress( stack, park, Grass );
			Assert.AreEqual( 1, clicks, "no screen open: the press is the park's" );

			stack.Open( new AWindow( stack, parkScreen: true ) );
			Pointer( stack, Grass, left: false );

			WorldPress( stack, park, Grass );
			Assert.AreEqual( 1, clicks, "beside the open screen, nothing held: kept from the park" );

			WorldPress( stack, park, new Vector2( 800, 450 ) );
			Assert.AreEqual( 1, clicks, "on the screen's bare frame: the interface's" );

			ParkBuildMode.Arm( ParkBuildMode.Queue );
			WorldPress( stack, park, Grass );
			Assert.AreEqual( 2, clicks, "beside the screen with a tool armed: the tool's" );
			Assert.IsTrue( ParkBuildMode.Anchored, "and it anchored its run" );

			WorldPress( stack, park, new Vector2( 800, 450 ) );
			Assert.AreEqual( 2, clicks, "the frame still takes its own, tool or no tool" );
		}
		finally
		{
			Logger.OnLog -= count;
			cell.SetValue( null, cellWas );
		}
	}

	/// <summary>
	/// <b>The game menu closes the open screen as it opens</b> (<c>MenuList_Show</c>, <c>0x00493171</c>), and so does any
	/// window that says it does; a window that does not leaves it.
	/// </summary>
	/// <remarks><b>Mutations:</b> the menu not saying so leaves the screen open under it; every window closing it closes it under the plain one.</remarks>
	[TestMethod]
	public void TheGameMenuClosesTheOpenScreen()
	{
		var stack = APark();
		stack.Open( new AWindow( stack, parkScreen: true ) );
		stack.Open( new AWindow( stack, parkScreen: false ) );
		Assert.AreEqual( "ParkGadget, ParkViewfinder, AWindow, AWindow", Names( stack ), "a plain window leaves it" );

		stack.Open( new UI.GameMenu( stack, [], 10 ) );

		Assert.AreEqual( 0, stack.Windows.Count( window => window.ParkScreen ), "the menu closed the screen" );
		Assert.AreEqual( "ParkGadget, ParkViewfinder, AWindow, GameMenu", Names( stack ) );
	}

	/// <summary>
	/// <b>A screen opening folds the gadget's arm away</b> (<c>FUN_004a25f0( 1 )</c> from <c>FUN_00485b70</c>): the
	/// camcorder button put the arm out, and the buy screen opening brings it in and lifts the button.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> taking the fold out leaves the arm out under the screen; folding on every frame a screen is up
	/// brings it in again the frame after the button put it out.
	/// </remarks>
	[TestMethod]
	public void AScreenOpeningFoldsTheGadgetsArm()
	{
		var stack = APark();
		var gadget = stack.Windows.OfType<UI.ParkGadget>().Single();

		Click( stack, Camcorder );
		ParkGadgetPressTests.Settle( gadget );
		Assert.IsTrue( gadget.ArmOut, "the camcorder button puts the arm out" );

		stack.Open( new UI.ParkBuyScreen( stack ) );
		Pointer( stack, Grass, left: false );
		ParkGadgetPressTests.Settle( gadget );

		Assert.IsFalse( gadget.ArmOut, "the screen opening folds it" );

		Click( stack, Camcorder );
		ParkGadgetPressTests.Settle( gadget );
		Pointer( stack, Grass, left: false );
		ParkGadgetPressTests.Settle( gadget );
		Assert.IsTrue( gadget.ArmOut, "and with the screen still up the button puts it out again, and it stays out" );
	}

	/// <summary>
	/// <b>While a screen is open the game's and the camera's keys are not heard</b>: opening one switches both tables
	/// off (<c>FUN_00485b70</c>, <c>0x00485ccd</c>, <c>0x00485ce6</c>), so Delete leaves the tool armed and the Left arrow
	/// leaves the camera where it looks; closed, both act.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> the build keys not asking puts the tool away under the screen; the camera not asking turns it.
	/// </remarks>
	[TestMethod]
	public void TheGamesAndTheCamerasKeysAreNotHeardWhileAScreenIsOpen()
	{
		var stack = APark();
		var park = (Level)RuntimeHelpers.GetUninitializedObject( typeof( Level ) );
		typeof( Level ).GetProperty( nameof( Level.Kind ) )!.SetValue( park, Level.Scene.Park );
		typeof( Level ).GetField( "_windows", BindingFlags.Instance | BindingFlags.NonPublic )!.SetValue( park, stack );
		Level.Current = park;
		Camera.SetCameraMode<ParkOrbitCameraMode>();

		var screen = new AWindow( stack, parkScreen: true );
		stack.Open( screen );
		ParkBuildMode.Arm( ParkBuildMode.Path );
		var yaw = ParkOrbitCameraMode.Yaw;

		WorldFrame( stack, park, Down( Key.Delete ), Down( Key.Left ) );
		WorldFrame( stack, park, Up( Key.Delete ), Up( Key.Left ) );
		Assert.AreEqual( ParkBuildMode.Path, ParkBuildMode.Current, "Delete under a screen: the tool stays" );
		Assert.AreEqual( yaw, ParkOrbitCameraMode.Yaw, "the Left arrow under a screen: the camera stays" );

		stack.Close( screen );

		WorldFrame( stack, park, Down( Key.Left ) );
		WorldFrame( stack, park, Up( Key.Left ) );
		Assert.AreNotEqual( yaw, ParkOrbitCameraMode.Yaw, "closed: the Left arrow turns the camera" );

		WorldFrame( stack, park, Down( Key.Delete ) );
		WorldFrame( stack, park, Up( Key.Delete ) );
		Assert.AreEqual( ParkBuildMode.None, ParkBuildMode.Current, "and Delete puts the tool away" );
	}

	/// <summary>
	/// <b>The way into first person closes the open park screen</b>: <c>FUN_00481a10</c>, which C and the gadget's
	/// first-person button both reach, calls <c>FUN_00485b40</c> before anything else (<c>0x00481a2b</c>). With a screen
	/// up and a plain window beside it, C let go leaves the viewer in first person, the screen gone and the plain window
	/// where it was; held, nothing closes.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> the call taken out of <c>ParkCamcorderCameraMode.Enter</c> leaves the screen up over first
	/// person; <c>WindowStack.CloseParkScreen</c> closing every window takes the plain one too, and closing the front
	/// window only leaves the screen under it; <c>Level.CloseParkScreen</c> not reaching the stack leaves the screen.
	/// </remarks>
	[DataTestMethod]
	[DataRow( "a park screen" )]
	[DataRow( "buy" )]
	[DataRow( "object window" )]
	public void GoingIntoFirstPersonClosesTheOpenScreen( string which )
	{
		var stack = APark();
		var park = AParkOver( stack );
		Camera.SetCameraMode<ParkOrbitCameraMode>();

		UI.UiWindow screen = which switch
		{
			"buy" => new UI.ParkBuyScreen( stack ),
			"object window" => new UI.ParkObjectWindow( stack, 13 ),
			_ => new AWindow( stack, parkScreen: true )
		};

		stack.Open( screen );
		stack.Open( new AWindow( stack, parkScreen: false ) );

		WorldFrame( stack, park, Down( Key.C ) );
		Assert.IsFalse( ParkCamcorderCameraMode.Active, "held: still in orbit" );
		CollectionAssert.Contains( stack.Windows.ToList(), screen, "and the screen is still up" );

		WorldFrame( stack, park, Up( Key.C ) );
		Assert.IsTrue( ParkCamcorderCameraMode.Active, "let go: in first person" );
		Assert.AreEqual( "ParkGadget, ParkViewfinder, AWindow", Names( stack ), "the screen is gone and the plain window is not" );
		Assert.IsFalse( stack.ParkScreenUp, "no park screen is open" );
	}

	/// <summary>
	/// <b>Going into first person with no screen open closes nothing</b>, and says so: the answer is what the log line
	/// hangs on.
	/// </summary>
	/// <remarks><b>Mutations:</b> <c>CloseParkScreen</c> answering that it closed one whatever was open.</remarks>
	[TestMethod]
	public void GoingIntoFirstPersonWithNoScreenOpenClosesNothing()
	{
		var stack = APark();
		var park = AParkOver( stack );
		stack.Open( new AWindow( stack, parkScreen: false ) );

		Assert.IsFalse( park.CloseParkScreen(), "nothing to close" );
		Assert.AreEqual( "ParkGadget, ParkViewfinder, AWindow", Names( stack ) );

		stack.Open( new AWindow( stack, parkScreen: true ) );
		Assert.IsTrue( park.CloseParkScreen(), "one closed" );
		Assert.AreEqual( "ParkGadget, ParkViewfinder, AWindow", Names( stack ) );
	}

	/// <summary>A park whose windows are this stack, made the current level.</summary>
	/// <summary>
	/// <b>The hire screen's list picks the row under the pointer, and its skill bar stands at nought until a row is
	/// picked</b>: the list's stream flags are <c>0x291</c>, flag <c>0x80</c> among them, and the bar <c>0x2482</c> is
	/// built at nought (<c>0x0049c335</c>) and set only by message <c>0x401</c>, a pick.
	/// </summary>
	/// <remarks><b>Mutations:</b> the list picking only on a press; no bar; the bar unknown, or full, as it opens.</remarks>
	[TestMethod]
	public void TheHireScreensListPicksTheRowUnderThePointerAndItsSkillBarStandsAtNoughtUntilOne()
	{
		var stack = APark();
		var hire = new UI.ParkHireScreen( stack );

		stack.Open( hire );

		static IEnumerable<UI.UiControl> Under( UI.UiControl control ) => control.Children.Concat( control.Children.SelectMany( Under ) );

		var list = Under( hire.Root ).OfType<UI.UiList>().Single( control => control.Id == 0x248e );
		var bar = Under( hire.Root ).OfType<UI.ParkObjectWindow.UiStatBar>().Single( control => control.Id == 0x2482 );

		Assert.IsTrue( list.SelectsUnderPointer, "flag 0x80" );
		Assert.IsTrue( bar.Known, "drawn as it opens" );
		Assert.AreEqual( 0f, bar.At, "at nought until a row is picked" );
	}

	/// <summary>
	/// <b>The ride window's four condition gauges stand in 10 from their rows' top and bottom and none from their
	/// sides</b> (<c>FUN_00485e30( gauge, 0, 10 )</c> on <c>0x3e16</c>, <c>0x3e18</c>, <c>0x3e19</c> and <c>0x3e17</c>,
	/// <c>0x004adc69</c> to <c>0x004add83</c>): three rows of 45 draw 25 tall and <c>0x3e18</c>'s 44 draws 24, while the
	/// text beside them keeps the whole row.
	/// </summary>
	/// <remarks><b>Mutations:</b> no inset; an inset of 20; the inset across rather than down; the text inset too.</remarks>
	[TestMethod]
	public void TheRideWindowsFourGaugesStandInTenFromTheirRowsTopAndBottom()
	{
		var window = new UI.ParkObjectWindow( APark(), 13 );
		var stats = window.Root.Children.Single( child => child.Id == 0x3e15 );

		UI.UiRect Of( int id ) => stats.Children.Single( child => child.Id == id ).Rect;

		Assert.AreEqual( new UI.UiRect( 1315, 289, 1550, 314 ), Of( 0x3e16 ), "Excitement" );
		Assert.AreEqual( new UI.UiRect( 1315, 341, 1550, 365 ), Of( 0x3e18 ), "Reliability, a row of 44" );
		Assert.AreEqual( new UI.UiRect( 1315, 392, 1550, 417 ), Of( 0x3e19 ), "State of repair" );
		Assert.AreEqual( new UI.UiRect( 1315, 443, 1550, 468 ), Of( 0x3e17 ), "Remaining life" );
		Assert.AreEqual( new UI.UiRect( 876, 279, 1273, 324 ), Of( 0x3e1c ), "the text beside Excitement keeps its row" );
		Assert.IsTrue( new[] { 0x3e16, 0x3e17, 0x3e18, 0x3e19 }.All( id => stats.Children.Single( child => child.Id == id ) is UI.ParkObjectWindow.UiStatBar ) );
	}

	/// <summary>
	/// <b>The hire list picks its first row as it fills, on opening and on a tab</b> (<c>FUN_0066403b</c> at a count of
	/// 1, its <c>0x401</c> posted): the skill bar stands at nought until the screen's next frame takes the pick, then at
	/// the grade of the candidate sorted first by name, not of the one handed over first; the label reads UITEXT 146.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> the hire list not picking; the pick taken nowhere; the label left empty or lettered with the
	/// candidate; the bar set from the candidate added first; the picked wage not kept in the staff costs.
	/// </remarks>
	[TestMethod]
	public void TheHireListPicksItsFirstRowAsItFillsAndTheSkillBarShowsTheRowSortedFirst()
	{
		var stack = APark();
		var park = AParkOver( stack );

		using var stream = FileSystem.OpenRead( "levels/jungle/Easymode.TPWI" );
		var world = new ParkWorld( new SaveReader( stream ).ReadFile() );
		var pool = Level.StaffPoolFor( world, new ParkBalance( "jungle", easyMode: true ), world.GameTick );

		typeof( Level ).GetProperty( nameof( Level.StaffPool ) )!.SetValue( park, pool );
		typeof( Level ).GetProperty( nameof( Level.ParkState ) )!.SetValue( park, new ParkState( parkIsClosed: false, visitorsToDate: 0, balance: 1000 ) );

		// No staff in the park, so the staff costs shown are the picked wage alone.
		var people = typeof( ParkPeople ).GetProperty( "Current", BindingFlags.Static | BindingFlags.NonPublic )!;
		var peopleBefore = people.GetValue( null );
		people.SetValue( null, null );

		// The list's text compare is on plain character values, and equal names stand as handed over.
		ParkStaffPool.Candidate First( int kind ) => pool.OfKind( kind ).OrderBy( person => person.Name, System.StringComparer.Ordinal ).First();

		// A kind whose first candidate in slot order is not the first by name, and is of another grade.
		var kinds = Enumerable.Range( 0, 5 ).Where( kind => pool.OfKind( kind ).Any() ).ToArray();
		var telling = kinds.First( kind => pool.OfKind( kind ).First().Grade != First( kind ).Grade );
		var other = kinds.First( kind => kind != telling && First( kind ).Grade != First( telling ).Grade );

		var hire = new UI.ParkHireScreen( stack, openOn: telling );

		stack.Open( hire );

		static IEnumerable<UI.UiControl> Under( UI.UiControl control ) => control.Children.Concat( control.Children.SelectMany( Under ) );

		var list = Under( hire.Root ).OfType<UI.UiList>().Single( control => control.Id == 0x248e );
		var bar = Under( hire.Root ).OfType<UI.ParkObjectWindow.UiStatBar>().Single( control => control.Id == 0x2482 );
		var label = Under( hire.Root ).Single( control => control.Id == 0x2483 );
		var costs = Under( hire.Root ).Single( control => control.Id == 0x248c );

		Assert.AreEqual( "Skill", label.Text, "UITEXT 146" );
		Assert.AreEqual( First( telling ).Id, list.Selected, "the top row picked as the list filled" );
		Assert.AreEqual( 0f, bar.At, "the pick posted, not yet taken" );

		Pointer( stack, Grass, left: false );
		Assert.AreEqual( UI.ParkHireScreen.SkillUnits( First( telling ).Grade ) / 1024f, bar.At, "taken: the row sorted first" );
		Pointer( stack, Grass, left: false );
		Assert.AreEqual( UI.ParkItemsScreen.Money( First( telling ).Wage ), costs.Text, "its wage in the staff costs, standing frame on frame" );

		// The tabs' ids by kind, the stream's: cleaners, mechanics, entertainers, guards, scientists.
		int[] tabIds = [0x2490, 0x2493, 0x2492, 0x2494, 0x2491];
		var tab = Under( hire.Root ).Single( control => control.Id == tabIds[other] ).Rect;
		Click( stack, new Vector2( (tab.Left + tab.Right) / 2f, (tab.Top + tab.Bottom) / 2f ) );
		Pointer( stack, Grass, left: false );

		Assert.AreEqual( other, hire.Tab );
		Assert.AreEqual( First( other ).Id, list.Selected );
		Assert.AreEqual( UI.ParkHireScreen.SkillUnits( First( other ).Grade ) / 1024f, bar.At, "a tab picks its first row again" );
		Assert.AreEqual( "Skill", label.Text, "and the label stands" );
		Assert.AreEqual( UI.ParkItemsScreen.Money( First( other ).Wage ), costs.Text );

		people.SetValue( null, peopleBefore );
	}

	/// <summary>
	/// <b>The hire and training screens' account is the analyser's last closed month</b> (<c>FUN_0049bdd0</c>,
	/// <c>FUN_004b2750</c>): in the shipped park cash in 1500, staff costs nought and all costs 13175, so "$ 1500",
	/// "$ 0", "$ 13175", "-$ 11675" as the hire screen opens; a pick adds the candidate's wage to the staff costs and
	/// takes it off the balance; the training screen's staff costs are its budgets, its balance the cash in less them
	/// and the other costs (the original with the mechanics at 75: "-$ 11750", <c>hud.md</c>, Q352's table). A dismissal's
	/// severance goes onto the month's staff costs (<c>FUN_00505790</c>).
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> the cash in and costs rings swapped; the other costs all costs; the balance without the other
	/// costs; the wage not taken off the balance; the training balance without the budgets; the figures read live
	/// rather than as the screen opens; a severance not a staff cost.
	/// </remarks>
	[TestMethod]
	public void TheHireAndTrainingScreensAccountIsTheAnalysersLastClosedMonth()
	{
		var stack = APark();
		var park = AParkOver( stack );

		using var stream = FileSystem.OpenRead( "levels/jungle/Easymode.TPWI" );
		var world = new ParkWorld( new SaveReader( stream ).ReadFile() );
		var pool = Level.StaffPoolFor( world, new ParkBalance( "jungle", easyMode: true ), world.GameTick );
		var state = new ParkState( world );

		typeof( Level ).GetProperty( nameof( Level.StaffPool ) )!.SetValue( park, pool );
		typeof( Level ).GetProperty( nameof( Level.ParkState ) )!.SetValue( park, state );

		var people = typeof( ParkPeople ).GetProperty( "Current", BindingFlags.Static | BindingFlags.NonPublic )!;
		var peopleBefore = people.GetValue( null );
		people.SetValue( null, null );

		static IEnumerable<UI.UiControl> Under( UI.UiControl control ) => control.Children.Concat( control.Children.SelectMany( Under ) );
		string Figures( UI.UiWindow screen, params int[] ids ) => string.Join( " / ", ids.Select( id => Under( screen.Root ).Single( control => control.Id == id ).Text ) );

		var hire = new UI.ParkHireScreen( stack, openOn: 1 );
		stack.Open( hire );

		Assert.AreEqual( "$ 1500 / $ 0 / $ 13175 / -$ 11675", Figures( hire, 0x2489, 0x248c, 0x248b, 0x2487 ), "as it opens" );

		// The month's money moves after the opening; the figures stand.
		state.Deposit( 100 );
		Pointer( stack, Grass, left: false );

		var picked = pool.Candidates.Single( person => person.Id == Under( hire.Root ).OfType<UI.UiList>().Single( control => control.Id == 0x248e ).Selected );
		Assert.AreEqual( $"$ 1500 / $ {picked.Wage} / $ 13175 / -$ {11675 + picked.Wage}", Figures( hire, 0x2489, 0x248c, 0x248b, 0x2487 ), "a pick" );

		stack.Close( hire );

		var shipped = new ParkPeople( world, new ParkBalance( "jungle", easyMode: true ), null, state );

		Assert.AreEqual( "$ 1500 / $ 0 / $ 13175 / -$ 11675", Figures( new UI.ParkStaffCostsScreen( stack ), 0x2e2, 0x2df, 0x2e0, 0x2e4 ), "budgets of nought" );

		shipped.SetTrainingBudget( 1, 75 );
		Assert.AreEqual( "$ 1500 / $ 75 / $ 13175 / -$ 11750", Figures( new UI.ParkStaffCostsScreen( stack ), 0x2e2, 0x2df, 0x2e0, 0x2e4 ), "the mechanics at 75" );

		// A dismissal's severance, one wage, goes onto the month's staff costs and all costs (FUN_00505790).
		var mechanic = shipped.Staff.Single( member => member.Model == 4 );
		var severance = pool.WageFor( ParkStaffPool.KindFor( mechanic.Model ), mechanic.PayGrade );

		Assert.IsTrue( severance > 0 );
		Assert.IsTrue( shipped.Fire( mechanic.ThingId ) );
		Assert.AreEqual( (538 + severance, 538 + severance), (state.MonthStaffCosts.Total, state.MonthCosts.Total), "a severance is a staff cost" );

		// A month closed with staff costs in it: the other costs are all costs less them, on both screens.
		state.Spend( 40 );
		state.TurnTheMonth( null );

		var staffMonth = 538 + severance;

		Assert.AreEqual( $"$ 300 / $ {staffMonth} / $ 40 / -$ {staffMonth + 40 - 300}",
			Figures( new UI.ParkHireScreen( stack, openOn: 1 ), 0x2489, 0x248c, 0x248b, 0x2487 ), "the next month's account" );
		Assert.AreEqual( "$ 300 / $ 75 / $ 40 / $ 185", Figures( new UI.ParkStaffCostsScreen( stack ), 0x2e2, 0x2df, 0x2e0, 0x2e4 ) );

		shipped.Delete();
		Entity.ApplyDeletions();
		people.SetValue( null, peopleBefore );
	}

	/// <summary>
	/// <b>A tabbed screen fills its list once as it opens, once again a tab, and not for the tab already down</b>: each
	/// opener fills its list and then puts its tab down with <c>FUN_0066a295</c>, which does not reach the tab handler
	/// that fills on a click. The original's hire and buy fills were hit once each at an opening and once at a tab, and
	/// not at a click on the tab already down (<c>q364/orig</c>).
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> putting the tab down tells the handler, so the opening fills twice; a tab's click fills nothing;
	/// a click on the tab already down fills again.
	/// </remarks>
	[TestMethod]
	[DataRow( "hire", "Hire list: added " )]
	[DataRow( "buy", "Buy list: tab " )]
	[DataRow( "items", "Items list: tab " )]
	[DataRow( "staff", "Staff list: kind " )]
	public void ATabbedScreenFillsItsListOnceAsItOpensOnceATabAndNotForTheTabAlreadyDown( string which, string fill )
	{
		var stack = APark();
		AParkOver( stack );

		var fills = 0;
		void Count( Logger.Level severity, string line ) => fills += line.StartsWith( fill ) ? 1 : 0;

		Logger.OnLog += Count;

		try
		{
			UI.UiWindow screen = which switch
			{
				"hire" => new UI.ParkHireScreen( stack ),
				"buy" => new UI.ParkBuyScreen( stack ),
				"items" => new UI.ParkItemsScreen( stack ),
				_ => new UI.ParkStaffScreen( stack )
			};

			stack.Open( screen );
			Pointer( stack, Grass, left: false );

			Assert.AreEqual( 1, fills, "the opening fills once" );

			static IEnumerable<UI.UiControl> Under( UI.UiControl control ) => control.Children.Concat( control.Children.SelectMany( Under ) );

			var tabs = Under( screen.Root ).OfType<UI.UiRadioGroup>().Single();
			var tab = tabs.Children.OfType<UI.UiButton>().First( option => option.Id != tabs.Selected ).Rect;

			Click( stack, new Vector2( (tab.Left + tab.Right) / 2f, (tab.Top + tab.Bottom) / 2f ) );
			Pointer( stack, Grass, left: false );

			Assert.AreEqual( 2, fills, "and a tab once more" );

			Click( stack, new Vector2( (tab.Left + tab.Right) / 2f, (tab.Top + tab.Bottom) / 2f ) );
			Pointer( stack, Grass, left: false );

			Assert.AreEqual( 2, fills, "and the tab already down not at all" );
		}
		finally
		{
			Logger.OnLog -= Count;
		}
	}

	private static Level AParkOver( UI.WindowStack stack )
	{
		var park = (Level)RuntimeHelpers.GetUninitializedObject( typeof( Level ) );
		typeof( Level ).GetProperty( nameof( Level.Kind ) )!.SetValue( park, Level.Scene.Park );
		typeof( Level ).GetField( "_windows", BindingFlags.Instance | BindingFlags.NonPublic )!.SetValue( park, stack );
		Level.Current = park;

		return park;
	}

	/// <summary>A park's interface: the real front end over a real stack, its gadget and viewfinder up.</summary>
	private static UI.WindowStack APark()
	{
		var stack = new UI.WindowStack();

		_ = new UI.ParkFrontEnd( stack, "jungle" );

		return stack;
	}

	/// <summary>One frame with the pointer at a point of the screen and the left button down or up.</summary>
	private static void Pointer( UI.WindowStack stack, Vector2 at, bool left )
	{
		Input.UpdateFrom( new Snapshot( [], at, left, pressed: left && !Input.Mouse.Left ) );
		stack.Update();
	}

	/// <summary>A real click: the pointer there, the button down for a frame, then up.</summary>
	private static void Click( UI.WindowStack stack, Vector2 at )
	{
		Pointer( stack, at, left: false );
		Pointer( stack, at, left: true );
		Pointer( stack, at, left: false );
	}

	/// <summary>A left press and its release as two of the game's frames: the stack's update, then the level's reading of the park's buttons.</summary>
	private static void WorldPress( UI.WindowStack stack, Level park, Vector2 at )
	{
		var worldClick = typeof( Level ).GetMethod( "WorldClick", BindingFlags.Instance | BindingFlags.NonPublic )!;

		foreach ( var down in new[] { true, false } )
		{
			Pointer( stack, at, down );
			worldClick.Invoke( park, [] );
		}
	}

	/// <summary>One frame with these key events and the pointer on the grass.</summary>
	private static void Keys( UI.WindowStack stack, params KeyEvent[] keys )
	{
		Input.UpdateFrom( new Snapshot( keys, Grass ) );
		stack.Update();
	}

	/// <summary>One frame as <c>Level.Update</c> orders it: the input, the stack, the build keys, then the camera.</summary>
	private static void WorldFrame( UI.WindowStack stack, Level park, params KeyEvent[] keys )
	{
		Keys( stack, keys );
		typeof( Level ).GetMethod( "GameTableKeys", BindingFlags.Instance | BindingFlags.NonPublic )!.Invoke( park, [] );
		Camera.Update();
	}

	private static string Names( UI.WindowStack stack ) => string.Join( ", ", stack.Windows.Select( window => window.GetType().Name ) );

	private static KeyEvent Down( Key key ) => new( key, true, ModifierKeys.None );

	private static KeyEvent Up( Key key ) => new( key, false, ModifierKeys.None );

	/// <summary>A bare window, a park screen or not, with one control that fills it when it is given a click to answer.</summary>
	private sealed class AWindow : UI.UiWindow
	{
		public AWindow( UI.WindowStack stack, bool parkScreen, UI.UiRect? rect = null, System.Action? clicked = null ) : base( stack )
		{
			Root = new UI.UiControl { Rect = rect ?? new UI.UiRect( 600, 300, 1000, 600 ), Clicked = clicked };
			ParkScreen = parkScreen;
		}
	}

	/// <summary>
	/// SDL's events for one frame: the keys, where the pointer is, whether the left button is down, and whether its press
	/// was sent this frame.
	/// </summary>
	private sealed class Snapshot( KeyEvent[] keys, Vector2 at, bool left = false, bool pressed = false ) : InputSnapshot
	{
		public IReadOnlyList<KeyEvent> KeyEvents => keys;
		public IReadOnlyList<MouseEvent> MouseEvents => pressed ? [new MouseEvent( MouseButton.Left, true )] : [];
		public IReadOnlyList<char> KeyCharPresses => [];
		public System.Numerics.Vector2 MousePosition => new( at.X, at.Y );
		public float WheelDelta => 0f;
		public bool IsMouseDown( MouseButton button ) => button == MouseButton.Left && left;
	}
}
