using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeoVeldrid;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace OpenTPW.Tests;

/// <summary>
/// The lobby takes every key on its release, as the original's does (message 0x1000b): the cursor keys move one island
/// each time they are let go, the main Enter is Enter this park, and Escape cancels a flight or opens the menu. The name
/// box takes its Enter on the release too, and the lobby never hears that one. A left press on the lobby's view enters
/// the park on the press; one inside the island panel's outline goes no further. See <c>FrontEnd.LobbyKeys</c>,
/// <c>FrontEnd.ViewPressed</c>, <see cref="UI.IslandPanel.EnterPark"/> and <c>docs/exe/lobby.md</c>, "The lobby's
/// keys act on the release".
///
/// <para>
/// <b>The frame is the stack's own order</b>: every open window's <c>Update</c>, then the stack's <c>Keyboard</c> - so a
/// key read by a window, as the island panel read the cursor keys before, is seen as well as one handed to the scene.
/// Only the pointer half is left out, which the press tests drive through <see cref="UI.WindowStack.ClickAt"/>.
/// </para>
/// <para>
/// <b>What these cannot see.</b> No test builds a lobby, so the islands and the stack are stand-ins made without their
/// constructors - the front end is the real one, over that stack - and SDL's key events are replaced by the lists they
/// fill. The game run shows the rest.
/// </para>
/// </summary>
[TestClass]
public class LobbyKeysOnReleaseTests
{
	private Point2 _screen;
	private readonly List<LobbyIsland> _islands = new();

	[TestInitialize]
	public void StartInOrbit()
	{
		Log ??= new();

		_screen = Screen.Size;
		Screen.Size = new Point2( 2048, 1536 );

		LobbyCameraMode.ForgetIsland();
		LobbyCameraMode.HeldToOneIsland = false;
		IslandIndex = 0;
		Input.ForgetHeldKeys();
	}

	[TestCleanup]
	public void PutTheLobbyBack()
	{
		foreach ( var island in _islands )
			Entity.All.Remove( island );

		LobbyCameraMode.ForgetIsland();
		LobbyCameraMode.HeldToOneIsland = false;
		IslandIndex = 0;
		SetCurrentPlayer( null );

		Input.ForgetHeldKeys();
		Input.Mouse = new();
		Input.TextCaptured = false;

		Screen.Size = _screen;
	}

	/// <summary>
	/// <b>A key's release is kept for the frame it comes up in</b>, once however long the key was held, and a held key's
	/// repeats are presses only.
	/// </summary>
	/// <remarks><b>Mutations:</b> not filling <see cref="Input.KeysReleased"/> from the key-up events turns this red.</remarks>
	[TestMethod]
	public void InputKeepsTheKeysThatCameUpThisFrame()
	{
		Input.UpdateFrom( new Snapshot( Down( Key.Right ) ) );
		CollectionAssert.AreEqual( new[] { Key.Right }, Input.KeysPressed.ToArray(), "the press" );
		Assert.AreEqual( 0, Input.KeysReleased.Count, "nothing let go yet" );

		Input.UpdateFrom( new Snapshot( Down( Key.Right ), Down( Key.Right ) ) );
		Assert.AreEqual( 0, Input.KeysReleased.Count, "a held key's repeats are not releases" );

		Input.UpdateFrom( new Snapshot( Up( Key.Right ), Up( Key.Enter ) ) );
		CollectionAssert.AreEqual( new[] { Key.Right, Key.Enter }, Input.KeysReleased.ToArray(), "both, in the order they came up" );
		Assert.AreEqual( 0, Input.KeysPressed.Count );

		Input.UpdateFrom( new Snapshot() );
		Assert.AreEqual( 0, Input.KeysReleased.Count, "and only for that frame" );
	}

	/// <summary>
	/// <b>A held Right moves nothing while it is held, and one island when it is let go</b>; Left takes it back, and
	/// again goes round the other way. The keypad's arrows move nothing.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> moving on the press - the island panel's old reading of <see cref="Input.KeysPressed"/>, a held
	/// key's repeats included - moves on every frame of the hold; taking Right off the release moves nothing at all;
	/// Right and Left swapped, or the keypad's arrows taken as the cursor keys, each move the wrong way.
	/// </remarks>
	[TestMethod]
	public void TheCursorKeysMoveOneIslandWhenLetGo()
	{
		var (_, stack, panel) = APlayersLobby( keys: 1 );

		for ( var frame = 0; frame < 5; ++frame )
			Frame( stack, pressed: [Key.Right] );

		Assert.AreEqual( 0, IslandIndex, "held, with its repeats: nothing moves" );

		Frame( stack, released: [Key.Right] );
		Assert.AreEqual( 1, IslandIndex, "let go: one island on" );

		Frame( stack, released: [Key.Left] );
		Assert.AreEqual( 0, IslandIndex, "Left let go: one back" );

		Frame( stack, released: [Key.Left] );
		Assert.AreEqual( 2, IslandIndex, "and Left again goes round the other way, to the last of the three" );

		Frame( stack, released: [Key.Keypad6] );
		Assert.AreEqual( 2, IslandIndex, "the keypad's 6 is another key to the lobby" );

		Frame( stack, released: [Key.Keypad4] );
		Assert.AreEqual( 2, IslandIndex, "and so is its 4" );
		Assert.AreEqual( "IslandPanel", Names( stack ) );
		Assert.IsTrue( stack.Windows.Contains( panel ) );
	}

	/// <summary>
	/// <b>The main Enter enters the park on its release</b>, as the panel's button does: the camera leaves and the panel
	/// closes. Held, it does nothing; the keypad's Enter does nothing at all.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> Enter taken out of <c>FrontEnd.LobbyKeys</c>, or read on the press, or the keypad's Enter
	/// accepted, each turns this red.
	/// </remarks>
	[TestMethod]
	public void TheMainEnterLetGoEntersThePark()
	{
		var (_, stack, _) = APlayersLobby( keys: 1 );

		Frame( stack, pressed: [Key.Enter] );
		Frame( stack, pressed: [Key.Enter] );
		StringAssert.StartsWith( LobbyCameraMode.LeaveDescription(), "leave=No ", "held, Enter enters nothing" );

		Frame( stack, released: [Key.KeypadEnter] );
		StringAssert.StartsWith( LobbyCameraMode.LeaveDescription(), "leave=No ", "the keypad's Enter is another key to the lobby" );

		Frame( stack, released: [Key.Enter] );
		StringAssert.StartsWith( LobbyCameraMode.LeaveDescription(), "leave=Homing ", "let go, it enters the park" );
		Assert.AreEqual( "", Names( stack ), "and the panel closes" );

		Frame( stack, released: [Key.Right] );
		Assert.AreEqual( 0, IslandIndex, "and the island keys wait for the flight" );
	}

	/// <summary>
	/// <b>Enter counts the keys the player holds, not the count the panel last showed</b>: a new player whose first key
	/// the advisor has not yet handed over - the panel still showing none - goes in. With no key, nothing happens.
	/// </summary>
	/// <remarks><b>Mutations:</b> judging by the panel's remembered count refuses the first; not counting at all lets the second in.</remarks>
	[TestMethod]
	public void EnterCountsTheKeysAsTheyStand()
	{
		var (_, stack, _) = APlayersLobby( keys: 0 );

		Frame( stack, released: [Key.Enter] );
		StringAssert.StartsWith( LobbyCameraMode.LeaveDescription(), "leave=No ", "no key, no park" );

		// The key is given as the player is made; the panel shows it only at the advisor's cue.
		Players.Roster.Current!.File.KeysGiven = 1;
		Frame( stack, released: [Key.Enter] );
		StringAssert.StartsWith( LobbyCameraMode.LeaveDescription(), "leave=Homing ", "the key held counts, shown or not" );
	}

	/// <summary>
	/// <b>Escape acts on its release</b>: held during a flight it cancels nothing, and let go it cancels the flight and
	/// gives the panel back. In orbit, let go, it opens the game menu, and let go again it closes it.
	/// </summary>
	/// <remarks><b>Mutations:</b> the lobby's Escape back on the press - <c>EscapeWithoutFocus</c> - cancels on the press.</remarks>
	[TestMethod]
	public void EscapeActsOnTheRelease()
	{
		var (_, stack, _) = APlayersLobby( keys: 1 );

		Frame( stack, released: [Key.Enter] );
		StringAssert.StartsWith( LobbyCameraMode.LeaveDescription(), "leave=Homing " );

		HoldEscape( stack );
		StringAssert.StartsWith( LobbyCameraMode.LeaveDescription(), "leave=Homing ", "held, Escape cancels nothing" );
		Assert.AreEqual( "", Names( stack ), "and opens nothing" );

		Frame( stack, released: [Key.Escape] );
		StringAssert.StartsWith( LobbyCameraMode.LeaveDescription(), "leave=No ", "let go, it cancels the flight" );
		Assert.AreEqual( "IslandPanel", Names( stack ), "and the panel is back, with no menu" );

		HoldEscape( stack );
		Assert.AreEqual( "IslandPanel", Names( stack ), "in orbit, held: no menu yet" );

		Frame( stack, released: [Key.Escape] );
		Assert.AreEqual( "IslandPanel, GameMenu", Names( stack ), "let go: the menu" );

		Frame( stack, released: [Key.Right, Key.Enter] );
		Assert.AreEqual( 0, IslandIndex, "the menu has the keys" );
		StringAssert.StartsWith( LobbyCameraMode.LeaveDescription(), "leave=No " );

		Frame( stack, released: [Key.Escape] );
		Assert.AreEqual( "IslandPanel", Names( stack ), "let go again: the menu closes" );
	}

	/// <summary>
	/// <b>With nobody playing the lobby's root is hidden, and neither the cursor keys nor Enter reach the camera.</b>
	/// </summary>
	/// <remarks><b>Mutations:</b> taking the shown test out of <c>FrontEnd.LobbyKeys</c> moves the island.</remarks>
	[TestMethod]
	public void WithNobodyPlayingTheKeysReachNothing()
	{
		var (_, stack, _) = APlayersLobby( keys: 1 );
		SetCurrentPlayer( null );

		// Parks that cost nothing, so that only the hidden root keeps Enter from the camera: with a price, Enter this park
		// would refuse a nobody on its own.
		foreach ( var island in _islands )
			SetBacking( island, "KeysToEnter", 0 );

		Frame( stack, released: [Key.Right, Key.Enter] );

		Assert.AreEqual( 0, IslandIndex );
		StringAssert.StartsWith( LobbyCameraMode.LeaveDescription(), "leave=No " );
	}

	/// <summary>
	/// <b>The name box takes Enter on its release, and the lobby does not hear that Enter as well</b> - though the box's
	/// tick opens the island panel in the same frame. Held, it ticks nothing; the keypad's Enter does not tick it.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> the box's Enter back on the press ticks while held; accepting the keypad's Enter ticks on it;
	/// handing the keys to the scene after a box has taken them enters the park on the tick's own Enter.
	/// </remarks>
	[TestMethod]
	public void TheNameBoxTakesItsEnterAndTheLobbyNeverHearsIt()
	{
		var (_, stack, panel) = APlayersLobby( keys: 1 );
		stack.Close( panel );

		var dialog = new ANameBox( stack, panel );
		stack.Open( dialog );

		Frame( stack, pressed: [Key.Enter] );
		Frame( stack, pressed: [Key.Enter] );
		Frame( stack, released: [Key.KeypadEnter] );
		Assert.AreEqual( 0, dialog.Ticks, "held, or the keypad's: no tick" );

		Frame( stack, released: [Key.Enter] );
		Assert.AreEqual( 1, dialog.Ticks, "let go: the tick" );
		Assert.AreEqual( "IslandPanel", Names( stack ), "which gave the panel back" );
		StringAssert.StartsWith( LobbyCameraMode.LeaveDescription(), "leave=No ", "and that Enter was the box's alone" );

		Frame( stack );
		StringAssert.StartsWith( LobbyCameraMode.LeaveDescription(), "leave=No ", "nor is it heard a frame later" );
	}

	/// <summary>
	/// <b>A left press on the lobby's view enters the park, on the press</b>, and so does one on the bare corner of the
	/// island panel's rectangle, outside its L. One inside the L and off the buttons goes no further.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> no view press from the stack; the panel without its outline, so a press on it passes through;
	/// or the root's shown test taken out, each turns this red.
	/// </remarks>
	[TestMethod]
	public void ALeftPressOnTheViewEntersThePark()
	{
		var (_, stack, _) = APlayersLobby( keys: 1 );

		Assert.IsTrue( stack.ClickAt( 45, 1100 ), "inside the L, left of the buttons: the panel's" );
		StringAssert.StartsWith( LobbyCameraMode.LeaveDescription(), "leave=No ", "and it goes no further" );

		// A park that costs nothing, so that only the hidden root keeps the press from it: with a price, Enter this park
		// would refuse a nobody on its own.
		SetCurrentPlayer( null );
		SetBacking( _islands[0], "KeysToEnter", 0 );
		Assert.IsFalse( stack.ClickAt( 1024, 600 ), "the view, with nobody playing" );
		StringAssert.StartsWith( LobbyCameraMode.LeaveDescription(), "leave=No ", "reaches a hidden root: nothing" );

		SetCurrentPlayer( APlayer( keys: 1 ) );
		SetBacking( _islands[0], "KeysToEnter", 1 );
		Assert.IsFalse( stack.ClickAt( 400, 1000 ), "the bare corner of the panel's rectangle is the view's" );
		StringAssert.StartsWith( LobbyCameraMode.LeaveDescription(), "leave=Homing ", "and a press there enters the park" );
		Assert.AreEqual( "", Names( stack ), "the panel closes" );
	}

	/// <summary>
	/// <b>The panel's outline is the original's crossings test over the stream's 23 points</b>: in the L's two arms and
	/// its bottom, out in the corner it leaves bare and outside its rectangle.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> the above-or-level test read as <c>&gt;</c> moves the highest and lowest points; the crossing's
	/// comparison read as <c>&gt;</c> moves the left side.
	/// </remarks>
	[TestMethod]
	public void ThePanelsOutlineIsTheL()
	{
		var outline = (UI.UiPoint[])typeof( UI.IslandPanel ).GetField( "PanelOutline", BindingFlags.Static | BindingFlags.NonPublic )!.GetValue( null )!;

		Assert.AreEqual( 23, outline.Length );

		Assert.IsTrue( UI.UiControl.Encloses( outline, 150, 1000 ), "the upright arm" );
		Assert.IsTrue( UI.UiControl.Encloses( outline, 400, 1400 ), "the foot" );
		Assert.IsTrue( UI.UiControl.Encloses( outline, 150, 1500 ), "the bottom" );
		Assert.IsFalse( UI.UiControl.Encloses( outline, 400, 1000 ), "the bare corner" );
		Assert.IsFalse( UI.UiControl.Encloses( outline, 30, 860 ), "the rounded top-left corner" );
		Assert.IsFalse( UI.UiControl.Encloses( outline, 600, 1400 ), "past the foot" );

		// The upright arm's right side runs from (265,1236) up to (263,961), and the top of the foot from (265,1236) to
		// (356,1242), passing y 1238.3 at x 300.
		Assert.IsTrue( UI.UiControl.Encloses( outline, 262, 1100 ) );
		Assert.IsFalse( UI.UiControl.Encloses( outline, 266, 1100 ) );
		Assert.IsTrue( UI.UiControl.Encloses( outline, 300, 1240 ) );
		Assert.IsFalse( UI.UiControl.Encloses( outline, 300, 1234 ) );

		// On the outline itself the rule decides, worked through by hand: the left side, x 29 from y 1003 to 1364, is
		// out and a unit in is in; the lowest point, (254,1521), is in, and the highest, (157,855), out.
		Assert.IsFalse( UI.UiControl.Encloses( outline, 29, 1100 ), "on the left side" );
		Assert.IsTrue( UI.UiControl.Encloses( outline, 30, 1100 ) );
		Assert.IsTrue( UI.UiControl.Encloses( outline, 254, 1521 ), "the lowest point" );
		Assert.IsFalse( UI.UiControl.Encloses( outline, 157, 855 ), "the highest point" );
	}

	/// <summary>
	/// <b>A left press is the window system's press, never a button that was already down</b>: the frame it is sent in
	/// has one, and the frames the button is held after have none, though the button is down in all of them.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> reading the button's state instead of its press, or not filling the press at all, turns this red.
	/// The stack's own use of it, a real frame's view press, is reached only by a running lobby; the game run shows it.
	/// </remarks>
	[TestMethod]
	public void ALeftPressIsTheOneTheWindowSystemSent()
	{
		Input.UpdateFrom( new LeftHeld( pressedThisFrame: true ) );
		Assert.IsTrue( Input.Mouse.Left );
		Assert.IsTrue( Input.Mouse.LeftWentDown, "the press" );

		Input.UpdateFrom( new LeftHeld( pressedThisFrame: false ) );
		Assert.IsTrue( Input.Mouse.Left, "still held" );
		Assert.IsFalse( Input.Mouse.LeftWentDown, "and no new press" );
	}

	/// <summary>
	/// <b>Enter this park refuses a park whose global.sam would not load</b>, however little it costs, as the original
	/// refuses a park it has no record for (0x005e1da3) - but only after letting an Instant Action player straight in.
	/// </summary>
	/// <remarks><b>Mutations:</b> no record test enters the free park; the test before Instant Action refuses it.</remarks>
	[TestMethod]
	public void EnterRefusesAParkWhoseGlobalWouldNotLoad()
	{
		var (_, stack, _) = APlayersLobby( keys: 1 );
		SetBacking( _islands[0], "KeysToEnter", 0 );
		SetBacking( _islands[0], "GlobalLoaded", false );

		Frame( stack, released: [Key.Enter] );
		StringAssert.StartsWith( LobbyCameraMode.LeaveDescription(), "leave=No ", "no global.sam, no park, though it is free" );

		SetCurrentPlayer( new Player( 0, "Test", new PlayerFile { InstantAction = true } ) );
		Frame( stack, released: [Key.Enter] );
		StringAssert.StartsWith( LobbyCameraMode.LeaveDescription(), "leave=Homing ", "an Instant Action player goes straight in" );
	}

	/// <summary>
	/// <b>The name box takes the first of Enter and Escape to come up</b>: Escape let go and then Enter in one frame
	/// cancels the dialog and ticks nothing.
	/// </summary>
	/// <remarks><b>Mutations:</b> Enter looked for before Escape, whatever the order, ticks the dialog.</remarks>
	[TestMethod]
	public void TheNameBoxTakesItsKeysInTheOrderTheyCameUp()
	{
		var (_, stack, panel) = APlayersLobby( keys: 1 );
		stack.Close( panel );

		var dialog = new ANameBox( stack, panel );
		stack.Open( dialog );

		Frame( stack, released: [Key.Escape, Key.Enter] );

		Assert.AreEqual( 1, dialog.Cancels, "Escape came up first" );
		Assert.AreEqual( 0, dialog.Ticks, "and the Enter after it ticked nothing" );
	}

	/// <summary>
	/// Holds Escape down for a few frames, its repeats included, without letting it go - its binding newly down on the
	/// first frame and held after, as <see cref="Input.UpdateFrom"/> has it, so a handler on the press would fire.
	/// </summary>
	private static void HoldEscape( UI.WindowStack stack )
	{
		for ( var frame = 0; frame < 3; ++frame )
		{
			Input.LastKeysDown = frame == 0 ? [] : [InputButton.Menu];
			Input.KeysDown = [InputButton.Menu];
			Frame( stack, pressed: [Key.Escape] );
		}

		Input.LastKeysDown = [InputButton.Menu];
		Input.KeysDown = [];
	}

	/// <summary>One frame of the stack with these keys gone down and come up: every window's update, then the keys.</summary>
	private static void Frame( UI.WindowStack stack, Key[]? pressed = null, Key[]? released = null )
	{
		typeof( Input ).GetProperty( nameof( Input.KeysPressed ) )!.SetValue( null, pressed ?? [] );
		Input.KeysReleased = released ?? [];

		foreach ( var window in stack.Windows.ToArray() )
			window.Update();

		typeof( UI.WindowStack ).GetMethod( "Keyboard", BindingFlags.Instance | BindingFlags.NonPublic )!.Invoke( stack, [] );

		typeof( Input ).GetProperty( nameof( Input.KeysPressed ) )!.SetValue( null, Array.Empty<Key>() );
		Input.KeysReleased = [];
	}

	/// <summary>
	/// The island the camera is on. It outlives a lobby, so each test puts it back to the first: a test left on another
	/// would hand the next one its island.
	/// </summary>
	private static int IslandIndex
	{
		get => (int)IslandIndexProperty.GetValue( null )!;
		set => IslandIndexProperty.SetValue( null, value );
	}

	private static PropertyInfo IslandIndexProperty => typeof( LobbyCameraMode ).GetProperty( "IslandIndex", BindingFlags.Static | BindingFlags.NonPublic )!;

	/// <summary>
	/// A lobby with a Full Simulation player holding <paramref name="keys"/> keys, the island panel up, and three islands
	/// in the running order, each costing one key, the first on show. The panel is shown no keys, as a new player's is.
	/// </summary>
	private (UI.FrontEnd FrontEnd, UI.WindowStack Stack, UI.IslandPanel Panel) APlayersLobby( int keys )
	{
		FileSystem = GameData.Required();

		_islands.Add( AnIsland( 0, "Lost Kingdom", "Jungle" ) );
		_islands.Add( AnIsland( 1, "Halloween World", "Hallow" ) );
		_islands.Add( AnIsland( 2, "Wonder Land", "Fantasy" ) );
		Entity.All.AddRange( _islands );

		typeof( LobbyCameraMode ).GetProperty( nameof( LobbyCameraMode.CurrentIsland ) )!.SetValue( null, _islands[0] );

		var stack = (UI.WindowStack)RuntimeHelpers.GetUninitializedObject( typeof( UI.WindowStack ) );
		SetField( stack, "_windows", new List<UI.UiWindow>() );

		// The real front end, which hands the stack its own key and press handlers; with someone playing it opens the
		// island panel, as a lobby entered by a player does.
		SetCurrentPlayer( APlayer( keys ) );
		var frontEnd = new UI.FrontEnd( stack );
		var panel = (UI.IslandPanel)typeof( UI.FrontEnd ).GetField( "_islandPanel", BindingFlags.Instance | BindingFlags.NonPublic )!.GetValue( frontEnd )!;

		// Shown no keys, as a new player's panel is until the advisor's cue.
		SetField( panel, "_keysShown", 0 );

		return (frontEnd, stack, panel);
	}

	private static Player APlayer( int keys ) => new( 0, "Test", new PlayerFile { KeysGiven = keys } );

	private static LobbyIsland AnIsland( int index, string name, string theme )
	{
		var script = (LobbyScript)RuntimeHelpers.GetUninitializedObject( typeof( LobbyScript ) );
		SetBacking( script, "Index", index );

		var island = (LobbyIsland)RuntimeHelpers.GetUninitializedObject( typeof( LobbyIsland ) );
		SetBacking( island, "Script", script );
		SetBacking( island, "ParkName", name );
		SetBacking( island, "ThemeName", theme );
		SetBacking( island, "KeysToEnter", 1 );
		SetBacking( island, "GlobalLoaded", true );

		return island;
	}

	/// <summary>A window with a name box, standing in for the new player dialog: its tick closes it and gives the panel back.</summary>
	private sealed class ANameBox : UI.UiWindow
	{
		private readonly UI.IslandPanel _panel;

		public ANameBox( UI.WindowStack stack, UI.IslandPanel panel ) : base( stack )
		{
			_panel = panel;
			Root = new UI.UiControl { Rect = new UI.UiRect( 500, 500, 1500, 1000 ) };
			Focus = Root.Add( new UI.UiEdit { Rect = new UI.UiRect( 600, 600, 1400, 700 ) } );
			Modal = true;
		}

		public int Ticks { get; private set; }

		public int Cancels { get; private set; }

		protected internal override void Accept()
		{
			++Ticks;
			Stack.Close( this );
			Stack.Open( _panel );
		}

		protected internal override void Cancel()
		{
			++Cancels;
			Stack.Close( this );
		}
	}

	/// <summary>SDL's key events for one frame, and nothing else.</summary>
	private class Snapshot( params KeyEvent[] keys ) : InputSnapshot
	{
		public IReadOnlyList<KeyEvent> KeyEvents => keys;
		public virtual IReadOnlyList<MouseEvent> MouseEvents => [];
		public IReadOnlyList<char> KeyCharPresses => [];
		public System.Numerics.Vector2 MousePosition => System.Numerics.Vector2.Zero;
		public float WheelDelta => 0f;
		public virtual bool IsMouseDown( MouseButton button ) => false;
	}

	/// <summary>A frame with the left button down, and whether the window system sent its press this frame.</summary>
	private sealed class LeftHeld( bool pressedThisFrame ) : Snapshot
	{
		public override IReadOnlyList<MouseEvent> MouseEvents
			=> pressedThisFrame ? [new MouseEvent( MouseButton.Left, true )] : [];

		public override bool IsMouseDown( MouseButton button ) => button == MouseButton.Left;
	}

	private static KeyEvent Down( Key key ) => new( key, true, ModifierKeys.None );

	private static KeyEvent Up( Key key ) => new( key, false, ModifierKeys.None );

	private static string Names( UI.WindowStack stack ) => string.Join( ", ", stack.Windows.Select( window => window.GetType().Name ) );

	private static void SetCurrentPlayer( Player? player )
		=> typeof( Players ).GetProperty( nameof( Players.Current ) )!.SetValue( Players.Roster, player );

	private static void SetBacking( object target, string property, object? value )
		=> SetField( target, $"<{property}>k__BackingField", value );

	private static void SetField( object target, string field, object? value )
	{
		for ( var type = target.GetType(); type != null; type = type.BaseType )
		{
			if ( type.GetField( field, BindingFlags.Instance | BindingFlags.NonPublic ) is { } info )
			{
				info.SetValue( target, value );
				return;
			}
		}

		throw new MissingFieldException( target.GetType().Name, field );
	}
}
