using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeoVeldrid;
using OpenTPW.UI;

namespace OpenTPW.Tests;

/// <summary>
/// The hit test passes over a disabled button and everything under it, as the original's skips a control flagged
/// <c>0x2</c> (<c>FUN_0065db25</c>; <c>docs/exe/hud.md</c>, "The hit test's order").
/// </summary>
[TestClass]
public class DisabledButtonHitTests
{
	private Point2 _screen;

	[TestInitialize]
	public void OnAVirtualSizedScreen()
	{
		_screen = Screen.Size;
		Screen.Size = new Point2( 2048, 1536 );
	}

	[TestCleanup]
	public void PutTheScreenBack() => Screen.Size = _screen;

	/// <summary>
	/// <b>A disabled button and its child are passed over to the panel behind</b>; enabled, the child answers
	/// first and the button around it next, as before.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> the hit test reading no <c>SkipsPointer</c> answers with the child; the button skipping only itself
	/// (its children still tested) answers with the child too.
	/// </remarks>
	[TestMethod]
	public void ADisabledButtonIsPassedOverWithEverythingUnderIt()
	{
		var panel = new UiControl { Rect = new UiRect( 0, 0, 1000, 1000 ), StopsPointer = true };
		var button = panel.Add( new UiButton { Rect = new UiRect( 100, 100, 400, 400 ), HelpText = 343 } );
		var inside = button.Add( new UiControl { Rect = new UiRect( 150, 150, 250, 250 ), StopsPointer = true } );

		Assert.AreSame( inside, panel.HitTest( 200, 200 ), "enabled: the child in front" );
		Assert.AreSame( button, panel.HitTest( 300, 300 ), "enabled: the button off its child" );

		button.Enabled = false;

		Assert.AreSame( panel, panel.HitTest( 200, 200 ), "disabled: its child is skipped with it" );
		Assert.AreSame( panel, panel.HitTest( 300, 300 ), "disabled: the button itself is skipped" );
		Assert.AreSame( panel, panel.HitTest( 600, 600 ), "off the button, the panel either way" );

		var bare = new UiControl { Rect = new UiRect( 0, 0, 1000, 1000 ) };
		bare.Add( new UiButton { Rect = new UiRect( 100, 100, 400, 400 ), Enabled = false } );
		Assert.IsNull( bare.HitTest( 300, 300 ), "with nothing behind that takes it, nothing: the press goes on past" );
	}
}
