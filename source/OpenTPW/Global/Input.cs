using NeoVeldrid;
using NeoVeldrid.Sdl2;

namespace OpenTPW;

public static partial class Input
{
	public static MouseInfo Mouse { get; internal set; } = new();
	public static KeyboardInfo Keyboard { get; internal set; } = new();

	public static float Forward { get; set; }
	public static float Right { get; set; }

	internal static List<InputButton> LastKeysDown { get; set; } = new();
	internal static List<InputButton> KeysDown { get; set; } = new();

	private static CursorTypes _cursorType;
	public static CursorTypes CursorType
	{
		get => _cursorType;
		set => _cursorType = value;
	}

	public static bool IsSystemCursorVisible
	{
		get => Sdl2Window.SdlInstance.ShowCursor( -1 ) == 1;
		set => Sdl2Window.SdlInstance.ShowCursor( value ? 1 : 0 );
	}

	/// <summary>What was typed this frame, as characters - for a text box that has the keyboard.</summary>
	public static string TypedText { get; private set; } = "";

	/// <summary>The keys that went down this frame, a held key's repeats included.</summary>
	public static IReadOnlyList<Key> KeysPressed { get; private set; } = [];

	/// <summary>
	/// The keys that came up this frame, in the order they came up - once for each time a key is let go, however long it
	/// was held. The original's key-up message (0x1000b, UI_PostKey) is the one most of its keys act on.
	/// </summary>
	public static IReadOnlyList<Key> KeysReleased
	{
		get => _keysReleased;
		internal set => Releases = [.. value.Select( key => new KeyRelease( key, Held ) )];
	}

	private static Key[] _keysReleased = [];
	private static KeyRelease[] _releases = [];

	/// <summary>
	/// <see cref="KeysReleased"/>, each with the modifiers held as it came up. The original's window procedure asks
	/// <c>GetKeyState</c> for Shift, Ctrl and Alt at each key-up, just before it posts the key (<c>0x0046bb0b</c>;
	/// <c>docs/exe/scenes.md</c>, "The park Escape route"), so a modifier let go later in the same frame still counts
	/// for the key, and one pressed later does not.
	/// </summary>
	internal static IReadOnlyList<KeyRelease> Releases
	{
		get => _releases;
		private set
		{
			_releases = [.. value];
			_keysReleased = [.. value.Select( release => release.Key )];
		}
	}

	/// <summary>A key let go, and the modifiers held as it came up.</summary>
	internal readonly record struct KeyRelease( Key Key, Modifiers Held )
	{
		/// <summary>Whether no modifier was held - what a row that names none asks for, as the original's Escape rows do.</summary>
		public bool Plain => Held == Modifiers.None;

		/// <summary>Whether Ctrl was held and neither Shift nor Alt was: the modifier byte <c>0x0c</c>.</summary>
		public bool ControlAlone => Held == Modifiers.Control;
	}

	/// <summary>
	/// Set while a text box has the keyboard, so typing into it does not also press what its keys are
	/// bound to - an X in a player's name would otherwise freeze the lobby camera.
	/// </summary>
	public static bool TextCaptured { get; set; }

	public static bool Pressed( InputButton button )
	{
		return PressedFrom( button, KeysDown, LastKeysDown, KeysPressed );
	}

	/// <summary>
	/// Whether this button was pressed this frame: its binding satisfied now, not satisfied last
	/// frame, and one of its own keys among the keys that actually went down.
	/// </summary>
	/// <remarks>
	/// That last test is the half of the original's rule that comparing modifiers does not cover.
	/// The original matches inside its key-down handler, against the key that was just pressed
	/// (FUN_0040c900), while this rebuilds every binding from what is held each frame - so without
	/// it, letting go of Ctrl while C is still down makes the plain-C binding match for the first
	/// time and reads as a fresh press of camcorder mode, and holding C and then pressing Ctrl reads
	/// as a fresh press of Ctrl+C.
	/// A binding that is nothing but modifiers has no key to go down, so Clone is never "pressed" -
	/// it is a held state, and <see cref="Down"/> is the question to ask of it.
	/// </remarks>
	internal static bool PressedFrom( InputButton button, IReadOnlyCollection<InputButton> keysDown,
		IReadOnlyCollection<InputButton> lastKeysDown, IReadOnlyCollection<Key> keysPressed )
	{
		if ( !keysDown.Contains( button ) || lastKeysDown.Contains( button ) )
			return false;

		if ( !Bindings.TryGetValue( button, out var binding ) )
			return false;

		foreach ( var key in binding.Keys )
		{
			if ( keysPressed.Contains( key ) )
				return true;
		}

		return false;
	}

	public static bool Down( InputButton button )
	{
		return KeysDown.Contains( button );
	}

	public static bool Released( InputButton button )
	{
		return !KeysDown.Contains( button ) && LastKeysDown.Contains( button );
	}

	/// <summary>
	/// Whether a key of this button's binding came up this frame with exactly the binding's modifiers held: the original's
	/// key-up, which looks its row up by the key let go and the modifiers (<c>FUN_0040c990</c>), once however long the
	/// key was held, its repeats included.
	/// </summary>
	/// <remarks>
	/// Unlike <see cref="Released"/>, a modifier pressed or let go under the held key is no key-up. The modifiers are the
	/// ones held as the key came up (<see cref="Releases"/>).
	/// </remarks>
	public static bool KeyUp( InputButton button )
	{
		return !TextCaptured && KeyUpFrom( button, Releases );
	}

	internal static bool KeyUpFrom( InputButton button, IReadOnlyCollection<KeyRelease> releases )
	{
		if ( !Bindings.TryGetValue( button, out var binding ) )
			return false;

		foreach ( var release in releases )
		{
			if ( release.Held == binding.Modifiers && binding.Keys.Contains( release.Key ) )
				return true;
		}

		return false;
	}

	/// <summary>
	/// Forgets every key currently held, for the windows where the game pumps the desktop's events
	/// and throws them away: a loading screen, a minimised window, and a window without the focus.
	/// </summary>
	/// <remarks>
	/// A key released during one of those never arrives as a key-up, and the held set is only ever
	/// pruned by key-ups, so it would stay held for the rest of the session. A stuck modifier is far
	/// worse than a stuck letter: because a binding's modifiers have to match exactly, one of them
	/// stops every binding that names no modifier from matching at all - which is most of them, the
	/// Escape menu included - with nothing on screen to say why.
	/// </remarks>
	public static void ForgetHeldKeys()
	{
		Keyboard = new KeyboardInfo();
		KeysDown.Clear();
		LastKeysDown.Clear();
		KeysPressed = [];
		Releases = [];
	}

	public struct KeyboardInfo
	{
		public List<Key> KeysDown { get; set; }

		public KeyboardInfo()
		{
			KeysDown = new();
		}

		public KeyboardInfo( List<Key> keysDown )
		{
			KeysDown = keysDown;
		}
	}

	/// <summary>
	/// The modifiers the original tells apart. It asks Windows for the <i>combined</i> virtual keys -
	/// VK_SHIFT, VK_CONTROL and VK_MENU - so the left and right of a pair are one modifier, and the
	/// Windows key is not a modifier at all.
	/// </summary>
	[Flags]
	internal enum Modifiers
	{
		None = 0,
		Shift = 1,
		Control = 2,
		Alt = 4,
	}

	/// <summary>
	/// The keys a binding is about, held together with exactly this set of modifiers. <see cref="Keys"/>
	/// can be empty: a binding that is nothing but modifiers, as Clone is, keeps none.
	/// </summary>
	private readonly record struct Binding( Key[] Keys, Modifiers Modifiers );

	private static Modifiers ModifierFor( Key key ) => key switch
	{
		Key.ShiftLeft or Key.ShiftRight => Modifiers.Shift,
		Key.ControlLeft or Key.ControlRight => Modifiers.Control,
		Key.AltLeft or Key.AltRight => Modifiers.Alt,
		_ => Modifiers.None,
	};

	/// <summary>
	/// Whether Ctrl is held and neither Shift nor Alt is - the original's own test for "Ctrl alone",
	/// a <c>GetAsyncKeyState</c> mask that must equal <c>0x0c</c> exactly (<c>FUN_00486aa0</c>).
	/// </summary>
	public static bool ControlAlone => Held == Modifiers.Control;

	/// <summary>Whether either Shift is held, whatever else is.</summary>
	public static bool ShiftHeld => (Held & Modifiers.Shift) != 0;

	/// <summary>The modifiers held as the frame ends: a click's or a held state's, never a key-up's (<see cref="Releases"/>).</summary>
	internal static Modifiers Held => HeldIn( Keyboard.KeysDown );

	private static Modifiers HeldIn( IReadOnlyCollection<Key> keysDown )
	{
		var held = Modifiers.None;

		foreach ( var key in keysDown )
			held |= ModifierFor( key );

		return held;
	}

	/// <summary>
	/// Whether this button's binding is satisfied - every key of it held, and the modifiers held
	/// being <i>exactly</i> the ones it asks for.
	/// </summary>
	/// <remarks>
	/// This is the held state as the frame ends, which <see cref="Down"/>, <see cref="Pressed"/> and
	/// <see cref="Released"/> are built on. A key-up is judged at its own moment instead (<see cref="KeyUp"/>).
	/// The original compares a binding's modifier field for equality and not for containment, so a
	/// binding that names no modifier only fires when none is held. That is what keeps Ctrl+C from
	/// meaning Close Park and camcorder mode at once, and Ctrl+S from meaning both staff shortcuts.
	/// Keys that are not modifiers do not count against a binding: walking with W while pressing C
	/// still enters camcorder mode.
	/// </remarks>
	internal static bool BindingMatches( InputButton button, IReadOnlyCollection<Key> keysDown )
	{
		if ( !Bindings.TryGetValue( button, out var binding ) )
			return false;

		if ( HeldIn( keysDown ) != binding.Modifiers )
			return false;

		foreach ( var key in binding.Keys )
		{
			if ( !keysDown.Contains( key ) )
				return false;
		}

		return true;
	}

	private static Dictionary<InputButton, Binding> Bindings = new();

	static Input()
	{
		static T? GetAttributeOfType<T>( Enum enumVal ) where T : System.Attribute
		{
			var type = enumVal.GetType();
			var memInfo = type.GetMember( enumVal.ToString() );
			var attributes = memInfo[0].GetCustomAttributes( typeof( T ), false );
			return (attributes.Length > 0) ? (T)attributes[0] : null;
		}

		//
		// Build bindings table based on [DefaultKey] attribute values, splitting each one into the
		// modifiers it wants held and the keys it is actually about. A binding that is nothing but
		// modifiers - Clone is Ctrl on its own - keeps no keys, and so means "these modifiers, and
		// only these, are down", which is what holding Ctrl to clone asks for.
		//
		foreach ( var key in Enum.GetValues<InputButton>() )
		{
			var attrib = GetAttributeOfType<DefaultKeyAttribute>( key );

			if ( attrib == null )
				continue;

			var modifiers = Modifiers.None;
			var keys = new List<Key>();

			foreach ( var vkey in attrib.Keys )
			{
				var modifier = ModifierFor( vkey );

				if ( modifier != Modifiers.None )
					modifiers |= modifier;
				else
					keys.Add( vkey );
			}

			Bindings.Add( key, new Binding( [.. keys], modifiers ) );
		}
	}

	public static void UpdateFrom( InputSnapshot inputSnapshot )
	{
		var mousePos = new Vector2( inputSnapshot.MousePosition.X, inputSnapshot.MousePosition.Y );
		var mouseInfo = new MouseInfo
		{
			Delta = Mouse.Position - mousePos,
			Position = mousePos,
			Left = inputSnapshot.IsMouseDown( MouseButton.Left ),
			Right = inputSnapshot.IsMouseDown( MouseButton.Right ),
			LeftWentDown = inputSnapshot.MouseEvents.Any( e => e.MouseButton == MouseButton.Left && e.Down ),
			RightWentDown = inputSnapshot.MouseEvents.Any( e => e.MouseButton == MouseButton.Right && e.Down ),
			Wheel = inputSnapshot.WheelDelta
		};

		Mouse = mouseInfo;

		Right = 0;
		Forward = 0;

		var newKeysDown = inputSnapshot.KeyEvents.Where( x => x.Down ).Select( x => x.Key );

		// The frame's key events in the order they came, so that each release is given the modifiers held at that
		// moment and not the ones left as the frame ends.
		var held = new List<Key>( Keyboard.KeysDown );
		var releases = new List<KeyRelease>();

		foreach ( var keyEvent in inputSnapshot.KeyEvents )
		{
			if ( keyEvent.Down )
			{
				if ( !held.Contains( keyEvent.Key ) )
					held.Add( keyEvent.Key );
			}
			else
			{
				held.Remove( keyEvent.Key );
				releases.Add( new KeyRelease( keyEvent.Key, HeldIn( held ) ) );
			}
		}

		Keyboard = new KeyboardInfo( held );

		TypedText = new string( [.. inputSnapshot.KeyCharPresses] );
		KeysPressed = [.. newKeysDown];
		Releases = releases;

		bool IsKeyPressed( Key k ) => Keyboard.KeysDown.Contains( k );

		if ( IsKeyPressed( Key.A ) )
			Right -= 1;
		if ( IsKeyPressed( Key.D ) )
			Right += 1;
		if ( IsKeyPressed( Key.W ) )
			Forward += 1;
		if ( IsKeyPressed( Key.S ) )
			Forward -= 1;
		
		LastKeysDown = [.. KeysDown];
		KeysDown.Clear();

		if ( TextCaptured )
			return;

		foreach ( var button in Bindings.Keys )
		{
			if ( BindingMatches( button, Keyboard.KeysDown ) )
				KeysDown.Add( button );
		}
	}
}
