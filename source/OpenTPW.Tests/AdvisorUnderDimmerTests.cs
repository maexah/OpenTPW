using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenTPW.UI;
using System.Collections.Generic;

namespace OpenTPW.Tests;

/// <summary>
/// <b>The advisor is drawn under a dimmed window</b> (Q18, a deviation at Alexah's request: <c>docs/exe/ui.md</c>, "Pausing
/// him"). While a modal window is shown, the frame's end (<see cref="Level.RenderOverTheWorld"/>) draws the overlays inside
/// the interface's pass, the depth cleared first, just before that window (<see cref="WindowStack.UnderDimmer"/>), and not
/// again on top; with none shown, on top as before. These drive a real <see cref="WindowStack"/> whose windows record their
/// draws; what the dimmed advisor looks like is a photograph of the running game. The one production call,
/// <c>Level.Render</c> handing in its own stack, is not driven here: it is checked by the console's <c>windows</c> census
/// ("overlays under a dimmer"), Q1460's run.
/// </summary>
[TestClass]
public class AdvisorUnderDimmerTests
{
	private sealed class Recorder : UiControl
	{
		private readonly List<string> _drawn;
		private readonly string _name;

		public Recorder( List<string> drawn, string name )
		{
			_drawn = drawn;
			_name = name;
		}

		protected override void OnDraw() => _drawn.Add( _name );
	}

	private sealed class AWindow : UiWindow
	{
		public AWindow( WindowStack stack, List<string> drawn, string name, bool modal, bool keepsTheAdvisorOnTop = false )
			: base( stack )
		{
			Modal = modal;
			KeepsTheAdvisorOnTop = keepsTheAdvisorOnTop;
			Root = new Recorder( drawn, name ) { Rect = new UiRect( 100, 100, 700, 600 ) };
		}
	}

	private sealed class Pointer : Panel
	{
		private readonly List<string> _drawn;

		public Pointer( List<string> drawn ) => _drawn = drawn;

		protected override void OnRender() => _drawn.Add( "pointer" );
	}

	private bool _hidden;
	private Point2 _screen;
	private List<string> _drawn = null!;
	private RootPanel _hud = null!;
	private WindowStack _stack = null!;

	[TestInitialize]
	public void Begin()
	{
		Log ??= new();

		_hidden = RootPanel.Hidden;
		RootPanel.Hidden = false;
		_screen = Screen.Size;
		Screen.Size = new Point2( 2048, 1536 );

		_drawn = new List<string>();
		_hud = new RootPanel();
		_stack = _hud.AddChild( new WindowStack() );
		_hud.Pointer = new Pointer( _drawn );
	}

	[TestCleanup]
	public void End()
	{
		RootPanel.Hidden = _hidden;
		Screen.Size = _screen;
	}

	private AWindow Open( string name, bool modal, bool keepsTheAdvisorOnTop = false )
	{
		var window = new AWindow( _stack, _drawn, name, modal, keepsTheAdvisorOnTop );
		_stack.Open( window );
		return window;
	}

	private void Frame()
		=> Level.RenderOverTheWorld( _hud, _stack, () => _drawn.Add( "depth" ), () => _drawn.Add( "previews" ),
			() => _drawn.Add( "warning" ), () => _drawn.Add( "advisor" ) );

	/// <summary>
	/// A plain window, then a dimmed one over it: the advisor is drawn between them, after a depth clear, and not again on
	/// top; the previews and the warning keep their place after the interface.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> the hook not called in <c>WindowStack.OnRender</c> (the advisor on top, the reported bug); called
	/// after the window's draw (the dimmer under him); the overlays drawn on top as well (drawn twice); the depth clear left
	/// out of the hook; each fails this.
	/// </remarks>
	[TestMethod]
	public void ADimmedWindowIsDrawnOverTheAdvisor()
	{
		Open( "buy", modal: false );
		Open( "menu", modal: true );

		Frame();

		CollectionAssert.AreEqual( new[] { "buy", "depth", "advisor", "menu", "depth", "previews", "warning", "pointer" }, _drawn );
	}

	/// <summary>With no dimmed window the advisor stays on top of the interface, as the original draws him.</summary>
	[TestMethod]
	public void WithoutADimmedWindowTheAdvisorIsOnTop()
	{
		Open( "buy", modal: false );

		Frame();

		CollectionAssert.AreEqual( new[] { "buy", "depth", "previews", "warning", "advisor", "pointer" }, _drawn );
	}

	/// <summary>
	/// Two dimmed windows (a message box over the game menu): the advisor goes under the first, once.
	/// </summary>
	/// <remarks><b>Mutation:</b> the hook kept after its first call (the advisor drawn under each dimmer) fails this.</remarks>
	[TestMethod]
	public void UnderTheFirstDimmerOnly()
	{
		Open( "menu", modal: true );
		Open( "box", modal: true );

		Frame();

		CollectionAssert.AreEqual( new[] { "depth", "advisor", "menu", "box", "depth", "previews", "warning", "pointer" }, _drawn );
	}

	/// <summary>
	/// The new player dialog dims the screen but keeps the advisor on top, since he explains it while it is open; a message
	/// box over it still takes him under its own dimmer, between the two.
	/// </summary>
	/// <remarks><b>Mutation:</b> the hook run for every modal window, the dialog's flag not read, fails this.</remarks>
	[TestMethod]
	public void TheNewPlayerDialogLeavesTheAdvisorOnTop()
	{
		Open( "dialog", modal: true, keepsTheAdvisorOnTop: true );

		Frame();

		CollectionAssert.AreEqual( new[] { "dialog", "depth", "previews", "warning", "advisor", "pointer" }, _drawn );

		_drawn.Clear();
		Open( "box", modal: true );

		Frame();

		CollectionAssert.AreEqual( new[] { "dialog", "depth", "advisor", "box", "depth", "previews", "warning", "pointer" }, _drawn );
	}

	/// <summary>The real new player dialog is a dimmed window that keeps him on top.</summary>
	/// <remarks><b>Mutation:</b> the flag left off the dialog fails this.</remarks>
	[TestMethod]
	public void OnlyTheNewPlayerDialogKeepsHimOnTop()
	{
		FileSystem = GameData.Required();

		var frontEnd = (FrontEnd)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject( typeof( FrontEnd ) );
		var dialog = new NewPlayerDialog( _stack, frontEnd, 0 );

		Assert.AreEqual( (true, true), (dialog.Modal, dialog.KeepsTheAdvisorOnTop) );
	}

	/// <summary>
	/// A dimmed window that is hidden or put away draws no dimmer, so it does not take the advisor under it.
	/// </summary>
	/// <remarks><b>Mutation:</b> the hook run for a modal window that is skipped (before the hidden test) fails this.</remarks>
	[TestMethod]
	public void AHiddenDimmedWindowLeavesTheAdvisorOnTop()
	{
		Open( "menu", modal: true ).Hidden = true;

		Frame();

		CollectionAssert.AreEqual( new[] { "depth", "previews", "warning", "advisor", "pointer" }, _drawn );
	}

	/// <summary>
	/// F2's hidden interface draws no window and no dimmer, and the advisor is still drawn, once, on top - and the hook is
	/// let go at the frame's end, so nothing drawn later reaches it.
	/// </summary>
	/// <remarks><b>Mutation:</b> the overlays left out whenever a modal window is open, not when one was drawn, fails this.</remarks>
	[TestMethod]
	public void AHiddenInterfaceStillDrawsTheAdvisor()
	{
		Open( "menu", modal: true );
		RootPanel.Hidden = true;

		Frame();

		CollectionAssert.AreEqual( new[] { "depth", "previews", "warning", "advisor" }, _drawn );
		Assert.IsNull( _stack.UnderDimmer, "the hook is let go after the interface's pass" );
	}
}
