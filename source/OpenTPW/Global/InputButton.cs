using NeoVeldrid;

namespace OpenTPW;

/// <summary>
/// Every key the game binds, with the default each one ships on.
///
/// <para>
/// <b>Nine of these forty-one have a production listener; the other thirty-two are declared, bound,
/// rebindable in the options screen and consumed by nothing.</b> Counted 2026-09-17 rather than estimated,
/// and recorded here once so that it is not rediscovered a screen at a time. The nine that work are
/// <see cref="HideUI"/>, <see cref="Menu"/>, <see cref="ToggleHelpBar"/>, <see cref="FreezeCamera"/>,
/// <see cref="NextIsland"/>, <see cref="PreviousIsland"/>, <see cref="RotateLeft"/>,
/// <see cref="RotateRight"/> and <see cref="CamcorderMode"/>.
/// </para>
/// <para>
/// <b>This is not a defect list.</b> Most of the thirty-two wait on features that do not exist yet - there
/// is no building, no research and no time control to speed up - and they are declared ahead of those on
/// purpose, because the binding is what the options screen offers and the original ships all of them. The
/// ones worth noticing are the pairs whose feature <i>does</i> now exist:
/// <see cref="OpenPark"/> and <see cref="ClosePark"/>, which have had something to talk to since the gate
/// began taking commands.
/// </para>
/// <para>
/// <b>Two counting traps, both of which have caught somebody.</b> A plain search finds
/// <see cref="OpenPark"/> in production code, but the only hit is prose inside a doc comment saying it is
/// unconsumed. And <see cref="Clone"/> cannot be consumed by <c>Input.Pressed</c> at all whatever is wired
/// to it: it is a modifier-only binding, and a modifier never registers as a press - it has to be asked
/// with <c>Down</c>. So it is unreachable for a different reason than the rest, and deleting it as "dead"
/// would be wrong.
/// </para>
/// </summary>
public enum InputButton
{
	/// <summary>
	/// Open Park (Ctrl + O)
	/// Opens the park.
	/// </summary>
	[DefaultKey( Key.ControlLeft, Key.O )]
	OpenPark,

	/// <summary>
	/// Close Park (Ctrl + C)
	/// Closes the park.
	/// </summary>
	[DefaultKey( Key.ControlLeft, Key.C )]
	ClosePark,

	/// <summary>
	/// Toggle Help Bar (Ctrl + H)
	/// Turns the help bar on or off.
	/// </summary>
	[DefaultKey( Key.ControlLeft, Key.H )]
	ToggleHelpBar,

	/// <summary>
	/// Menu (Esc)
	/// Accesses the menu or cancels the current action.
	/// </summary>
	[DefaultKey( Key.Escape )]
	Menu,

	/// <summary>
	/// Buy Attractions (B)
	/// Buys attractions for the park.
	/// </summary>
	[DefaultKey( Key.B )]
	BuyAttractions,

	/// <summary>
	/// Hire Staff (H)
	/// Hires staff for the park.
	/// </summary>
	[DefaultKey( Key.H )]
	HireStaff,

	/// <summary>
	/// Park Info (I)
	/// Shows park information or status.
	/// </summary>
	[DefaultKey( Key.I )]
	ParkInfo,

	/// <summary>
	/// All Attractions (A)
	/// Shows all attractions in the park.
	/// </summary>
	[DefaultKey( Key.A )]
	AllAttractions,

	/// <summary>
	/// All Staff (S)
	/// Shows all staff in the park.
	/// </summary>
	[DefaultKey( Key.S )] 
	AllStaff,

	/// <summary>
	/// All Visitors (V)
	/// Shows all visitors in the park.
	/// </summary>
	[DefaultKey( Key.V )]
	AllVisitors,

	/// <summary>
	/// Financial Info (F)
	/// Shows financial information of the park.
	/// </summary>
	[DefaultKey( Key.F )] 
	FinancialInfo,

	/// <summary>
	/// Loans (L)
	/// Manages loans.
	/// </summary>
	[DefaultKey( Key.L )]
	Loans,

	/// <summary>
	/// Staff Budgets (T)
	/// Manages staff training budgets.
	/// </summary>
	[DefaultKey( Key.T )]
	StaffBudgets,

	/// <summary>
	/// Entry Price (G)
	/// Sets or changes the entry price for the park.
	/// </summary>
	[DefaultKey( Key.G )]
	EntryPrice,

	/// <summary>
	/// Research (R)
	/// Manages park research.
	/// </summary>
	[DefaultKey( Key.R )]
	Research,

	/// <summary>
	/// Map Screen (Space)
	/// Opens the map screen.
	/// </summary>
	[DefaultKey( Key.Space )]
	MapScreen,

	/// <summary>
	/// Staff Locator (Ctrl + S)
	/// Locates staff in the park.
	/// </summary>
	[DefaultKey( Key.ControlLeft, Key.S )]
	StaffLocator,

	/// <summary>
	/// Visitor Locator (Ctrl + V)
	/// Locates visitors in the park.
	/// </summary>
	[DefaultKey( Key.ControlLeft, Key.V )]
	VisitorLocator,

	/// <summary>
	/// Send Email Postcard (Ctrl + P)
	/// Sends an E-Mail postcard.
	/// </summary>
	[DefaultKey( Key.ControlLeft, Key.P )]
	SendEmailPostcard,

	/// <summary>
	/// Camcorder Mode (C)
	/// Enables camcorder mode, click to desired location.
	/// </summary>
	[DefaultKey( Key.C )]
	CamcorderMode,

	/// <summary>
	/// Rotate Left (Left Arrow)
	/// Rotates the camera left.
	/// </summary>
	[DefaultKey( Key.Left )]
	RotateLeft,

	/// <summary>
	/// Rotate Right (Right Arrow)
	/// Rotates the camera right.
	/// </summary>
	[DefaultKey( Key.Right )]
	RotateRight,

	/// <summary>
	/// Scroll Left (Numpad 4)
	/// Scrolls the view left.
	/// </summary>
	[DefaultKey( Key.Keypad4 )]
	ScrollLeft,

	/// <summary>
	/// Scroll Right (Numpad 6)
	/// Scrolls the view right.
	/// </summary>
	[DefaultKey( Key.Keypad6 )]
	ScrollRight,

	/// <summary>
	/// Zoom In (Up Arrow)
	/// Zooms the camera in.
	/// </summary>
	[DefaultKey( Key.Up )]
	ZoomIn,

	/// <summary>
	/// Zoom Out (Down Arrow)
	/// Zooms the camera out.
	/// </summary>
	[DefaultKey( Key.Down )]
	ZoomOut,

	/// <summary>
	/// Home (Home)
	/// Returns to Park Gates View.
	/// </summary>
	[DefaultKey( Key.Home )]
	Home,

	/// <summary>
	/// Turn Blueprint Left (Comma)
	/// Turns the blueprint left.
	/// </summary>
	[DefaultKey( Key.Comma )]
	TurnBlueprintLeft,

	/// <summary>
	/// Turn Blueprint Right (Full stop)
	/// Turns the blueprint right.
	/// </summary>
	[DefaultKey( Key.Period )]
	TurnBlueprintRight,

	/// <summary>
	/// Clone (Hold Ctrl and left-click)
	/// Copies an object (hold key and left-click the object).
	/// </summary>
	[DefaultKey( Key.ControlLeft )]
	Clone,

	/// <summary>
	/// Delete (Backspace)
	/// Deletes queues/tracks etc.
	/// </summary>
	[DefaultKey( Key.BackSpace )]
	Delete,

	/// <summary>
	/// Clear (Delete)
	/// Clears land.
	/// </summary>
	[DefaultKey( Key.Delete )]
	Clear,

	/// <summary>
	/// Time Speed Up (Numpad +)
	/// Speeds up time.
	/// </summary>
	[DefaultKey( Key.KeypadPlus )]
	TimeSpeedUp,

	/// <summary>
	/// Time Slow Down (Numpad -)
	/// Slows down time.
	/// </summary>
	[DefaultKey( Key.KeypadMinus )]
	TimeSlowDown,

	/// <summary>
	/// Time Reset (Numpad *)
	/// Resets time to normal.
	/// </summary>
	[DefaultKey( Key.KeypadMultiply )]
	TimeReset,

	/// <summary>
	/// Pause (P)
	/// Pauses or unpauses the game.
	/// </summary>
	[DefaultKey( Key.P )]
	Pause,

	/// <summary>
	/// Help (F1)
	/// Plays advisor help speech for the current screen/action.
	/// </summary>
	[DefaultKey( Key.F1 )]
	Help,

	/// <summary>
	/// Freeze Camera (X)
	/// Stops the lobby camera orbiting, so a model can be watched while it animates.
	/// </summary>
	[DefaultKey( Key.X )]
	FreezeCamera,

	/// <summary>
	/// Hide UI (F2)
	/// Hides the whole HUD so a screenshot shows the scene with nothing in front of it.
	/// </summary>
	[DefaultKey( Key.F2 )]
	HideUI,

	/// <summary>
	/// Previous Island ([)
	/// Moves the lobby camera around to the park before this one.
	/// </summary>
	[DefaultKey( Key.BracketLeft )]
	PreviousIsland,

	/// <summary>
	/// Next Island (])
	/// Moves the lobby camera around to the park after this one.
	/// </summary>
	[DefaultKey( Key.BracketRight )]
	NextIsland,
}
