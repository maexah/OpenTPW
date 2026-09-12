using System.Runtime.InteropServices;
using Veldrid.Sdl2;
using Veldrid.StartupUtilities;

namespace OpenTPW;

/// <summary>
/// Contains code for the instantiation and management of a window, the game editor,
/// ImGUI, inputs, the renderer, and the world itself.
/// </summary>
public class Window
{
	/// <summary>
	/// The smallest the window will go: the smallest mode the original itself runs at. It is mode 1
	/// of its _Resolution.sam and the lowest of the three steps its own options screen offers -
	/// OptionsScreen_Open (0x004a3a30) takes only 0, 1 and 2, which are 512x384, 640x480 and
	/// 800x600. The 400x300 below it ships with an empty Data\Init folder, so there is nothing
	/// further down to inherit.
	/// </summary>
	public static readonly Point2 MinimumSize = new( 512, 384 );

	public static Window Current { get; set; }
	public Sdl2Window SdlWindow { get; private set; }
	public Point2 Size => new Point2( SdlWindow.Width, SdlWindow.Height );

	/// <summary>Whether there is anything to draw into at all - false while the window is minimised.</summary>
	public bool HasArea => SdlWindow.Width > 0 && SdlWindow.Height > 0;

	/// <summary>
	/// The window has been resized. An event rather than the single slot this was, so that whatever
	/// else comes to need the news cannot quietly take the renderer's place by assigning over it.
	/// </summary>
	public event Action<Point2>? Resized;

	public bool Visible
	{
		get => SdlWindow.Visible;
		set => SdlWindow.Visible = value;
	}

	public Window( int width, int height, string title, bool startHidden = false )
	{
		Current ??= this;

		var windowCreateInfo = new WindowCreateInfo()
		{
			WindowWidth = Math.Max( width, MinimumSize.X ),
			WindowHeight = Math.Max( height, MinimumSize.Y ),
			WindowTitle = title,
			X = 32,
			Y = 32,
			WindowInitialState = startHidden ? Veldrid.WindowState.Hidden : Veldrid.WindowState.Normal
		};

		SdlWindow = VeldridStartup.CreateWindow( windowCreateInfo );

		// Resizing while the game runs is new: the original's window procedure (0x0046b600) answers
		// no WM_SIZE, WM_SIZING or WM_GETMINMAXINFO at all, and it works its interface scale out once
		// behind a guard that never runs again. Everything drawn from the window's size here is
		// worked out afresh each frame instead, so there is nothing left to latch.
		SdlWindow.Resizable = true;
		SetMinimumSize( SdlWindow, MinimumSize );

		SdlWindow.Resized += SdlWindow_Resized;
		Screen.UpdateFrom( Size );
	}

	/// <summary>
	/// Puts the window at a size, never below <see cref="MinimumSize"/>. For the debug console, which
	/// drives this to reproduce a size exactly; the desktop is still free to refuse or clamp it.
	/// </summary>
	public void Resize( int width, int height )
	{
		SdlWindow.Width = Math.Max( width, MinimumSize.X );
		SdlWindow.Height = Math.Max( height, MinimumSize.Y );
	}

	private void SdlWindow_Resized()
	{
		Screen.UpdateFrom( Size );
		Resized?.Invoke( Size );
	}

	[UnmanagedFunctionPointer( CallingConvention.Cdecl )]
	private delegate void SDL_SetWindowMinimumSize_t( IntPtr window, int w, int h );

	/// <summary>
	/// Veldrid's SDL binding wraps SDL_SetWindowResizable but has nothing for
	/// SDL_SetWindowMinimumSize, so it is loaded out of the same SDL the binding itself has already
	/// opened. Without it a resizable window can be dragged down to nothing at all, and the game is
	/// left being asked to build render targets with no pixels in them.
	/// </summary>
	private static void SetMinimumSize( Sdl2Window window, Point2 size )
		=> Sdl2Native.LoadFunction<SDL_SetWindowMinimumSize_t>( "SDL_SetWindowMinimumSize" )( window.SdlWindowHandle, size.X, size.Y );
}
