using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenTPW.UI;

namespace OpenTPW.Tests;

/// <summary>
/// <b>A modal screen up takes the wheel wherever the pointer is</b> (Q930; <c>docs/exe/hud.md</c>, "The wheel over a modal screen"). The notch goes to the
/// control under the pointer and only the park layer's procedure turns it into a zoom, which the original's park never hears under the Load Park
/// screen, the Save Park screen or the game menu. <see cref="WindowStack.WheelTaken"/> is what the park camera reads, so these drive a real
/// <see cref="WindowStack"/> a frame at a time and read it.
/// </summary>
[TestClass]
public class ModalWheelTests
{
	private Point2 _screen;
	private WindowStack _stack = null!;

	[TestInitialize]
	public void OneToOne()
	{
		Log ??= new();

		_screen = Screen.Size;
		Screen.Size = new Point2( 2048, 1536 );
		Input.Mouse = new();
		Time.PinWall( 0 );

		_stack = new WindowStack();
	}

	[TestCleanup]
	public void PutItAllBack()
	{
		Input.Mouse = new();
		Time.PinWall( null );
		new WindowStack().Update();
		Screen.Size = _screen;
	}

	/// <summary>One frame with the wheel turned and the pointer where it is.</summary>
	private bool Taken( float wheel, float x, float y, long now )
	{
		Time.PinWall( now );
		Input.Mouse = new() { Position = new Vector2( x, y ), Delta = new Vector2( 1, 0 ), Wheel = wheel };
		_stack.Update();
		return WindowStack.WheelTaken;
	}

	/// <summary>
	/// A wheel turned with a modal screen up is taken at the screen's bare frame, over a control on it (its exit cross), and on the bare park outside it;
	/// the same places with no modal up leave the wheel to the camera. Values a rule of "only over the screen's own rectangle" or "only over a control"
	/// gets wrong: the place outside every rectangle, and the frame between the controls.
	/// </summary>
	/// <remarks><b>Mutations:</b> the modal clause left out of <c>WindowStack.OnUpdate</c> (the park zooms behind every screen); the clause taken only when
	/// a control is hit (the frame and the park outside zoom); the clause taken without <c>ModalUp</c> (the control at the end fails).</remarks>
	[TestMethod]
	public void AModalScreenTakesTheWheelEverywhere()
	{
		var screen = new AModal( _stack, modal: true );
		_stack.Open( screen );

		Assert.IsTrue( Taken( -1f, 300, 300, 1000 ), "the screen's bare frame" );
		Assert.IsTrue( Taken( 1f, 560, 100, 1100 ), "the exit cross" );
		Assert.IsTrue( Taken( -1f, 1900, 1400, 1200 ), "the bare park outside the screen's rectangle" );
		Assert.IsFalse( Taken( 0f, 300, 300, 1300 ), "no notch, nothing taken" );

		screen.Hidden = true;
		Assert.IsFalse( Taken( -1f, 1900, 1400, 1400 ), "a modal screen put away takes nothing" );
	}

	[TestMethod]
	public void WithoutAModalTheWheelIsTheCameras()
	{
		_stack.Open( new AModal( _stack, modal: false ) );

		Assert.IsFalse( Taken( -1f, 300, 300, 1000 ), "over a plain window's bare frame" );
		Assert.IsFalse( Taken( 1f, 560, 100, 1100 ), "over a control of a plain window" );
		Assert.IsFalse( Taken( -1f, 1900, 1400, 1200 ), "on the bare park" );
	}

	/// <summary>
	/// <b>A park screen takes the wheel over its rectangle alone</b> (Q231; <c>docs/exe/hud.md</c>, "The wheel over a park screen"): over its
	/// bare frame and over a control on it the notch is the screen's, so the camera does not zoom; beside it, on the bare park, the camera
	/// zooms as with none; a screen put away, and the full-screen view's cover over the stack, take nothing.
	/// </summary>
	/// <remarks><b>Mutations:</b> the park screen clause left out of <c>WindowStack.OnUpdate</c> (the frame zooms); the clause taken with
	/// no test of the rectangle (the park beside zooms no more); the cover not asked.</remarks>
	[TestMethod]
	public void AParkScreenTakesTheWheelOverItsRectangleAlone()
	{
		var screen = new AParkScreen( _stack );
		_stack.Open( screen );

		Assert.IsTrue( Taken( -1f, 300, 300, 1000 ), "the screen's bare frame" );
		Assert.IsTrue( Taken( 1f, 560, 100, 1100 ), "a control on the screen" );
		Assert.IsFalse( Taken( -1f, 1900, 1400, 1200 ), "the bare park beside the screen" );

		_stack.Cover( true );
		Assert.IsFalse( Taken( -1f, 300, 300, 1300 ), "under the cover" );
		_stack.Cover( false );

		screen.Hidden = true;
		Assert.IsFalse( Taken( -1f, 300, 300, 1400 ), "a screen put away" );
	}

	private sealed class AParkScreen : UiWindow
	{
		public AParkScreen( WindowStack stack ) : base( stack )
		{
			ParkScreen = true;
			Root = new UiControl { Rect = new UiRect( 100, 80, 700, 600 ), HoldsChildren = true };
			Root.Add( new UiControl { Rect = new UiRect( 540, 80, 580, 120 ) } );
		}
	}

	private sealed class AModal : UiWindow
	{
		public AModal( WindowStack stack, bool modal ) : base( stack )
		{
			Modal = modal;
			Root = new UiControl { Rect = new UiRect( 100, 100, 700, 600 ), HoldsChildren = true };
			Root.Add( new UiControl { Rect = new UiRect( 540, 80, 580, 120 ) } );
		}
	}
}
