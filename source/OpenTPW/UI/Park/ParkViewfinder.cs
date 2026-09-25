namespace OpenTPW.UI;

/// <summary>
/// The camcorder's viewfinder: the frame first person is watched through, and the eject button that
/// leaves it.
///
/// <para>
/// <b>Where it comes from.</b> Its own compiled layout stream, 0x0074fa98, which FUN_00489f50 loads
/// onto a page of its own when a park's interface is assembled. Two controls: the surround
/// <c>f_viewfinder</c> across (170,170)-(1878,1366), and a button wearing <c>b_eject</c> in the far
/// bottom-right corner at (1916,1404)-(2018,1506), carrying UIHELPTEXT row 468 - <i>"Left-click to
/// exit camcorder mode (C)"</i>. A screenshot of the original confirms both: at 800x600 that button's
/// rectangle lands on (748,548)-(788,588), which is exactly where the eject icon sits, and the
/// surround's corner brackets fall on its rect.
/// </para>
/// <para>
/// <b>It is the other half of <see cref="ParkGadget"/>, not an addition to it.</b> The same screenshot
/// shows the management gadget is <i>absent</i> in first person - the corner where it lives is empty
/// park. So the two windows swap: this one is put away unless camcorder mode is running, and the
/// gadget is put away while it is. Both use <see cref="UiWindow.Hidden"/>, which
/// <see cref="WindowStack"/> honours for drawing and for the pointer alike, so a hidden window neither
/// paints nor takes a click.
/// </para>
/// <para>
/// <b>Why the eject button matters beyond parity.</b> Camcorder mode steers the view from where the
/// pointer is, so a control it owns has to be somewhere the resulting turn is wanted. The original
/// puts this one deep in the right-hand band, where reaching for it pans the view right - which is
/// what a camcorder does. That is the difference between this button and the gadget's: panning toward
/// the thing you are reaching for is the mode working, and panning away from a management panel you
/// only wanted to click was the mode fighting you.
/// </para>
/// <para>
/// <b>Engine and content.</b> The window, the frame and the pointer are engine. Which controls first
/// person has, where they sit and what the button does is this file.
/// </para>
/// </summary>
internal sealed class ParkViewfinder : UiWindow
{
	public ParkViewfinder( WindowStack stack ) : base( stack )
	{
		// Not modal and not pausing: the park runs while it is being filmed, and a pausing window here
		// would stop the clock, the calendar and every model with it.
		Root = new UiControl
		{
			Id = 0x89db52,
			Rect = new UiRect( 170, 170, 1878, 1366 ),
			Mesh = UiMesh.Get( "f_viewfinder" )
		};

		// A sibling of the surround in the stream rather than a child of it, and it has to stay one:
		// the button is outside the surround's rectangle, so nesting it would leave it anchoring on its
		// own anyway - and it belongs to the far corner, not to the frame.
		Root.Add( new UiButton
		{
			Id = 0x89db53,
			Rect = new UiRect( 1916, 1404, 2018, 1506 ),
			HelpText = 468,
			Mesh = UiMesh.Get( "b_eject" ),
			Clicked = ParkCamcorderCameraMode.Leave
		} );

		Hidden = true;
	}

	/// <summary>
	/// Up only in first person. The mode is asked every frame rather than told, because all four ways
	/// out of it - this button, the C key, Escape and a right click (<see cref="RightClicked"/>) - go through
	/// <see cref="ParkCamcorderCameraMode"/> itself, and a second record of whether it is running could
	/// only ever disagree with the first.
	/// </summary>
	protected internal override void Update() => Hidden = !ParkCamcorderCameraMode.Active;

	/// <summary>
	/// What answers a right click whose press lands on the view, as the stack asks at the press
	/// (<see cref="WindowStack.ViewRightClick"/>): in first person the view is this window's own full-screen
	/// layer, under the frame, which takes no pointer, and beside the eject button, which drops a right
	/// click; outside it the view is the park's own layer, which answers no click.
	/// </summary>
	/// <remarks>
	/// <b>Not the original's while a park screen is open over first person.</b> Entering first person
	/// closes the one open (<c>FUN_00485b40</c>) and hides the park's layer; here the screen stays and
	/// takes a right press on its body (<c>docs/QUEUE.md</c> Q122).
	/// </remarks>
	internal static Action? RightClickAnswer() => ParkCamcorderCameraMode.Active ? RightClicked : null;

	/// <summary>
	/// The layer's answer to a right click: with the Options switch "RMB cancel" on, leave first person,
	/// the way out Escape takes (<c>0x00488aa1</c>; <c>docs/exe/hud.md</c>, "Four ways out of camcorder
	/// mode"). First person is asked again at the click, as the original reads its camera flags again
	/// there.
	/// </summary>
	internal static void RightClicked()
	{
		if ( !ParkCamcorderCameraMode.Active || !GameOptions.Current.RmbCancel )
			return;

		ParkCamcorderCameraMode.Leave();
		Log.Info( "Right click: out of first person" );
	}
}
