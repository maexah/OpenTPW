using System.Runtime.InteropServices;
using Veldrid.Sdl2;

namespace OpenTPW;

/// <summary>How the game fills the screen.</summary>
public enum DisplayMode
{
	/// <summary>A window the desktop manages, which can be dragged to any size.</summary>
	Windowed,

	/// <summary>The display set to a mode of the game's choosing, with the game owning it - see <see cref="Display"/>.</summary>
	FullScreen,

	/// <summary>A borderless window covering the display at whatever mode the desktop is already in.</summary>
	BorderlessFullScreen
}

/// <summary>One mode a display can be put into. The refresh rate is kept only to pick the best of a repeated size.</summary>
public readonly record struct VideoMode( int Width, int Height, int RefreshRate )
{
	/// <summary>As the original names its own modes - UITEXT 341 is "512 x 384".</summary>
	public override string ToString() => $"{Width} x {Height}";
}

/// <summary>
/// The display the window is on: the modes it can be put into, and putting it into one.
///
/// <para>
/// The original does this with DirectDraw in FUN_00563460, and it is worth saying what it did, because
/// one of the three modes here is its and one is not. Asked for full screen it logs
/// <c>Fullscreen %d:%d:%d</c>, takes the display exclusively (SetCooperativeLevel 0x851) and calls
/// SetDisplayMode with a width, a height and a depth - a real mode change, which is what
/// <see cref="DisplayMode.FullScreen"/> is. Otherwise it logs <c>Windowed %d:%d:??</c> - the depth
/// unknown because a window keeps the desktop's - takes the display normally (0x808), and sizes the
/// window round its client rectangle with a clipper. There was no third way in 1999:
/// <see cref="DisplayMode.BorderlessFullScreen"/> is new here, and is the one most people now expect,
/// because it changes no mode and so leaves everything else on the desktop where it was.
/// </para>
/// <para>
/// Veldrid's SDL binding wraps the window states but none of the mode enumeration, so those four calls
/// are loaded out of the SDL it has already opened, the same way the window's minimum size is.
/// </para>
/// </summary>
public static class Display
{
	/// <summary>SDL_DisplayMode. Declared here rather than taken from the binding so the layout is stated where it is used.</summary>
	[StructLayout( LayoutKind.Sequential )]
	private struct SdlDisplayMode
	{
		public uint Format;
		public int Width;
		public int Height;
		public int RefreshRate;
		public IntPtr DriverData;
	}

	[UnmanagedFunctionPointer( CallingConvention.Cdecl )]
	private delegate int SDL_GetWindowDisplayIndex_t( IntPtr window );

	[UnmanagedFunctionPointer( CallingConvention.Cdecl )]
	private delegate int SDL_GetNumDisplayModes_t( int display );

	[UnmanagedFunctionPointer( CallingConvention.Cdecl )]
	private delegate int SDL_GetDisplayMode_t( int display, int index, ref SdlDisplayMode mode );

	[UnmanagedFunctionPointer( CallingConvention.Cdecl )]
	private delegate int SDL_GetDesktopDisplayMode_t( int display, ref SdlDisplayMode mode );

	[UnmanagedFunctionPointer( CallingConvention.Cdecl )]
	private delegate int SDL_SetWindowDisplayMode_t( IntPtr window, ref SdlDisplayMode mode );

	/// <summary>
	/// What to offer when SDL will not say what the display can do - the modes the original itself
	/// lists in _Resolution.sam, which is the one list the game is known to be happy at. Anything
	/// larger than the desktop is dropped from it.
	/// </summary>
	private static readonly VideoMode[] Shipped =
	[
		new( 512, 384, 0 ), new( 640, 480, 0 ), new( 800, 600, 0 ), new( 1024, 768, 0 ),
		new( 1280, 1024, 0 ), new( 1600, 1200, 0 ), new( 2048, 1536, 0 )
	];

	private static T? Load<T>( string name ) where T : Delegate
	{
		try
		{
			return Sdl2Native.LoadFunction<T>( name );
		}
		catch ( Exception e )
		{
			Log.Warning( $"Display: SDL has no {name} - {e.Message}" );
			return null;
		}
	}

	private static int DisplayIndex( Window window )
	{
		var index = Load<SDL_GetWindowDisplayIndex_t>( "SDL_GetWindowDisplayIndex" )?.Invoke( window.SdlWindow.SdlWindowHandle ) ?? 0;
		return index < 0 ? 0 : index;
	}

	/// <summary>The mode the desktop is in, or nothing if SDL will not say.</summary>
	public static VideoMode? Desktop( Window window )
	{
		var get = Load<SDL_GetDesktopDisplayMode_t>( "SDL_GetDesktopDisplayMode" );

		if ( get == null )
			return null;

		var mode = new SdlDisplayMode();
		return get( DisplayIndex( window ), ref mode ) == 0 ? new VideoMode( mode.Width, mode.Height, mode.RefreshRate ) : null;
	}

	/// <summary>
	/// Every size the display can be set to, smallest first, with one entry per size: a display lists
	/// the same size once per refresh rate, and the interface has no use for the difference, so the
	/// highest rate of each size is the one kept. Sizes below the window's own minimum are dropped,
	/// as the game will not go there anyway.
	/// </summary>
	public static IReadOnlyList<VideoMode> Modes( Window window )
	{
		var found = new Dictionary<(int, int), VideoMode>();
		var count = Load<SDL_GetNumDisplayModes_t>( "SDL_GetNumDisplayModes" );
		var read = Load<SDL_GetDisplayMode_t>( "SDL_GetDisplayMode" );

		if ( count != null && read != null )
		{
			var display = DisplayIndex( window );

			for ( int i = 0; i < count( display ); ++i )
			{
				var mode = new SdlDisplayMode();

				if ( read( display, i, ref mode ) != 0 )
					continue;

				Keep( new VideoMode( mode.Width, mode.Height, mode.RefreshRate ) );
			}
		}

		if ( found.Count == 0 )
		{
			// Nothing to ask, so the original's own list, less anything the desktop cannot show.
			var desktop = Desktop( window );

			foreach ( var mode in Shipped )
			{
				if ( desktop is not { } size || (mode.Width <= size.Width && mode.Height <= size.Height) )
					Keep( mode );
			}
		}

		return found.Values
			.OrderBy( mode => mode.Width )
			.ThenBy( mode => mode.Height )
			.ToArray();

		void Keep( VideoMode mode )
		{
			if ( mode.Width < Window.MinimumSize.X || mode.Height < Window.MinimumSize.Y )
				return;

			if ( !found.TryGetValue( (mode.Width, mode.Height), out var already ) || already.RefreshRate < mode.RefreshRate )
				found[(mode.Width, mode.Height)] = mode;
		}
	}

	/// <summary>
	/// Puts the window into <paramref name="mode"/>, asking the display for <paramref name="fullScreen"/>
	/// first if the mode is the one that owns the display. Anything SDL refuses is reported and left
	/// alone rather than retried, so a display that will not take a mode leaves the game where it was.
	/// </summary>
	public static void Apply( Window window, DisplayMode mode, VideoMode fullScreen )
	{
		var sdl = window.SdlWindow;

		// Out of any full screen first: a display cannot be given a new mode while it is being held
		// at the old one, and SDL only reads the window's mode as it enters full screen.
		sdl.WindowState = Veldrid.WindowState.Normal;

		switch ( mode )
		{
			case DisplayMode.FullScreen:
				SetFullScreenMode( window, fullScreen );
				sdl.WindowState = Veldrid.WindowState.FullScreen;
				break;

			case DisplayMode.BorderlessFullScreen:
				sdl.WindowState = Veldrid.WindowState.BorderlessFullScreen;
				break;

			default:
				break;
		}

		Log.Info( $"Display: {mode}{(mode == DisplayMode.FullScreen ? $" at {fullScreen}" : "")}, window {window.Size.X}x{window.Size.Y}" );
	}

	private static void SetFullScreenMode( Window window, VideoMode wanted )
	{
		if ( wanted.Width <= 0 || wanted.Height <= 0 )
			return;

		var set = Load<SDL_SetWindowDisplayMode_t>( "SDL_SetWindowDisplayMode" );

		if ( set == null )
			return;

		// Format and driver data left at zero, which asks SDL for the closest mode it has.
		var mode = new SdlDisplayMode { Width = wanted.Width, Height = wanted.Height, RefreshRate = wanted.RefreshRate };

		if ( set( window.SdlWindow.SdlWindowHandle, ref mode ) != 0 )
			Log.Warning( $"Display: the display would not take {wanted}, so full screen uses whatever mode it is in" );
	}
}
