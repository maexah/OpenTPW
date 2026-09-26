using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenTPW.UI;

namespace OpenTPW.Tests;

/// <summary>
/// Putting a window away, and the fact that a window's own <c>Update</c> cannot undo it.
///
/// <para>
/// <b>Putting a window away cannot be done with <c>Hidden</c>.</b> The front end puts windows away
/// when the options screen opens - the original's message 6. But <c>WindowStack</c> updates every window
/// it holds, hidden ones included, with no guard; and two of a park's windows assign <c>Hidden</c> afresh
/// in their own <c>Update</c> from conditions that say nothing about the options screen. So a gadget put
/// away through <c>Hidden</c> would come back one frame later, drawn under the options screen's dimmed
/// backdrop.
/// </para>
/// <para>
/// <b>The obvious fix is wrong and is worth recording.</b> Not updating hidden windows would stop the
/// clobber - and would permanently strand <c>ParkViewfinder</c>, which is constructed hidden and relies on
/// its own <c>Update</c> to bring itself back when first person starts. So the two meanings are two
/// flags.
/// </para>
/// </summary>
[TestClass]
public class WindowPutAwayTests
{
	/// <summary>
	/// A window that writes <see cref="UiWindow.Hidden"/> every update, the way <c>ParkGadget</c> and
	/// <c>ParkViewfinder</c> both do.
	///
	/// <para>
	/// <b>It is given no stack and no root, and both omissions are deliberate.</b> <see cref="UiWindow"/>'s
	/// constructor does nothing but assign the stack to a property, and nothing here reads it - while
	/// building a real <c>WindowStack</c> would call <c>UiFonts.Preload</c>, which fetches bitmap fonts off
	/// a file system these tests do not mount, and would fail for a reason with nothing to do with what is
	/// being tested. Leaving <c>Root</c> unset is the same argument: this never draws.
	/// </para>
	/// </summary>
	private sealed class ClobberingWindow : UiWindow
	{
		public ClobberingWindow() : base( null! )
		{
		}

		public int Updates { get; private set; }

		protected internal override void Update()
		{
			++Updates;
			Hidden = false;
		}
	}

	/// <summary>
	/// The window decides <c>Hidden</c>; the front end decides <c>PutAway</c>; and the first cannot reach
	/// the second.
	///
	/// <para>
	/// <b>The second and third assertions are the anti-vacuity guard, and without them this proves
	/// nothing.</b> A window whose <c>Update</c> never ran, or which never actually wrote <c>Hidden</c>,
	/// would satisfy the first assertion perfectly while the bug sat untouched - so the test insists the
	/// clobber really happened before it claims to have survived it.
	/// </para>
	/// </summary>
	[TestMethod]
	public void AWindowsOwnUpdateCannotUndoBeingPutAway()
	{
		var window = new ClobberingWindow { Hidden = true, PutAway = true };

		window.Update();

		Assert.IsTrue( window.PutAway, "a window's own update must not clear what the front end put away" );
		Assert.AreEqual( 1, window.Updates, "the update has to have run, or nothing was tested" );
		Assert.IsFalse( window.Hidden, "and it really did write Hidden, which is the clobber this survives" );
	}

	/// <summary>
	/// The two flags are independent: being put away is not being hidden, and a window that hides itself is
	/// not one the front end has put away. That distinction is what lets the options screen give back only
	/// the windows it took, rather than showing one that had hidden itself for reasons of its own.
	/// </summary>
	[TestMethod]
	public void HidingYourselfIsNotTheSameAsBeingPutAway()
	{
		var itself = new ClobberingWindow { Hidden = true };
		var frontEnd = new ClobberingWindow { PutAway = true };

		Assert.IsFalse( itself.PutAway, "a window that hid itself has not been put away" );
		Assert.IsFalse( frontEnd.Hidden, "and one that was put away has not hidden itself" );
	}
}
