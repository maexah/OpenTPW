using Veldrid;
using Veldrid.Sdl2;

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
		get => Sdl2Native.SDL_ShowCursor( -1 ) == 1;
		set => Sdl2Native.SDL_ShowCursor( value ? 1 : 0 );
	}

	/// <summary>What was typed this frame, as characters - for a text box that has the keyboard.</summary>
	public static string TypedText { get; private set; } = "";

	/// <summary>The keys that went down this frame, a held key's repeats included.</summary>
	public static IReadOnlyList<Key> KeysPressed { get; private set; } = [];

	/// <summary>
	/// Set while a text box has the keyboard, so typing into it does not also press what its keys are
	/// bound to - an X in a player's name would otherwise freeze the lobby camera.
	/// </summary>
	public static bool TextCaptured { get; set; }

	public static bool Pressed( InputButton button )
	{
		return KeysDown.Contains( button ) && !LastKeysDown.Contains( button );
	}

	public static bool Down( InputButton button )
	{
		return KeysDown.Contains( button );
	}

	public static bool Released( InputButton button )
	{
		return !KeysDown.Contains( button ) && LastKeysDown.Contains( button );
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

	private static Dictionary<InputButton, Key[]> Bindings = new();

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
		// Build bindings table based on [DefaultKey] attribute values
		//
		foreach ( var key in Enum.GetValues<InputButton>() )
		{
			var attrib = GetAttributeOfType<DefaultKeyAttribute>( key );

			if ( attrib == null )
				continue;

			Bindings.Add( key, attrib.Keys );
		}
	}

	public static void UpdateFrom( InputSnapshot inputSnapshot )
	{
		IsSystemCursorVisible = true;

		var mousePos = new Vector2( inputSnapshot.MousePosition.X, inputSnapshot.MousePosition.Y );
		var mouseInfo = new MouseInfo
		{
			Delta = Mouse.Position - mousePos,
			Position = mousePos,
			Left = inputSnapshot.IsMouseDown( MouseButton.Left ),
			Right = inputSnapshot.IsMouseDown( MouseButton.Right ),
			Wheel = inputSnapshot.WheelDelta
		};

		Mouse = mouseInfo;

		Right = 0;
		Forward = 0;

		var newKeysDown = inputSnapshot.KeyEvents.Where( x => x.Down ).Select( x => x.Key );
		var newKeysUp = inputSnapshot.KeyEvents.Where( x => !x.Down ).Select( x => x.Key );

		Keyboard = new KeyboardInfo(
			Keyboard.KeysDown.Concat( newKeysDown )
				.Distinct()
				.Where( x => !newKeysUp.Contains( x ) )
				.ToList()
		);

		TypedText = new string( [.. inputSnapshot.KeyCharPresses] );
		KeysPressed = [.. newKeysDown];

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

		foreach ( var (button, vkeys) in Bindings )
		{
			bool isPressed = true;

			foreach ( var vkey in vkeys )
			{
				if ( !IsKeyPressed( vkey ) )
					isPressed = false;
			}

			if ( isPressed )
			{
				KeysDown.Add( button );
			}
		}
	}
}
