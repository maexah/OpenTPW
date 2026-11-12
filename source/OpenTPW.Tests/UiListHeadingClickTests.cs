using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenTPW.UI;

namespace OpenTPW.Tests;

/// <summary>
/// A list heading's click sound, picked by the LIST's style bit <c>0x10</c> and not the heading's: the click hook plays on every
/// <c>0x100</c> by its receiver's style (<c>0x00485780</c>, <c>0x004857cb</c>), and a heading tells its list (<c>docs/exe/hud.md</c>,
/// "Which window the click sound is for"). Driven through a real <see cref="WindowStack"/>; needs no game.
/// </summary>
[TestClass]
public class UiListHeadingClickTests
{
	private Point2 _screen;

	[TestInitialize]
	public void OneToOne()
	{
		Log ??= new();
		_screen = Screen.Size;
		Screen.Size = new Point2( 2048, 1536 );
		Input.Mouse = new();
	}

	[TestCleanup]
	public void PutItAllBack()
	{
		Input.Mouse = new();
		new WindowStack().Update();
		Screen.Size = _screen;
		UiSounds.LastClick = null;
	}

	/// <summary>
	/// <b>A heading of a sorted list plays <c>Select3</c> (189); a heading of a list without the flag, and a plain button beside
	/// the list, play <c>BUTTON01</c> (31)</b>. The headings themselves carry no flag of their own.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> the list's flag not read (the sorted heading 31); every list's heading taken for <c>Select3</c> (the
	/// unsorted one 189); any button whose parent is a list (the same); the heading's own <c>Toggles</c> read in its place.
	/// </remarks>
	[TestMethod]
	public void AHeadingClicksBySortedListsStyleNotItsOwn()
	{
		var stack = new WindowStack();
		var window = new TwoLists( stack );
		stack.Open( window );

		int? Click( UiControl control )
		{
			var at = control.Pixels;
			UiSounds.LastClick = null;
			stack.ClickAt( at.X + (at.Width / 2f), at.Y + (at.Height / 2f) );
			return UiSounds.LastClick;
		}

		Assert.IsFalse( window.SortedHeading.Toggles, "the heading has no flag of its own" );
		Assert.AreEqual( 189, Click( window.SortedHeading ), "Select3: the list it tells carries 0x10" );
		Assert.AreEqual( 31, Click( window.PlainHeading ), "BUTTON01: a list without 0x10, as Load Park's 0x81" );
		Assert.AreEqual( 31, Click( window.Beside ), "BUTTON01: a button the window hears" );
	}

	/// <summary>A sorted list and an unsorted one, each with a bare heading, and a button on the window's root.</summary>
	private sealed class TwoLists : UiWindow
	{
		public UiButton SortedHeading { get; }
		public UiButton PlainHeading { get; }
		public UiButton Beside { get; }

		public TwoLists( WindowStack stack ) : base( stack )
		{
			Root = new UiControl { Rect = new UiRect( 0, 0, 1200, 900 ), HoldsChildren = true };

			var sorted = Root.Add( new UiList
			{
				Sorted = true,
				Rect = new UiRect( 100, 100, 500, 600 ),
				RowArea = new UiRect( 100, 200, 500, 600 ),
				Columns = [(100, 500)]
			} );

			var plain = Root.Add( new UiList
			{
				Rect = new UiRect( 600, 100, 1000, 600 ),
				RowArea = new UiRect( 600, 200, 1000, 600 ),
				Columns = [(600, 1000)]
			} );

			sorted.SetSort( 1 );
			plain.SetSort( 1 );
			SortedHeading = sorted.AddHeading( 0, new UiRect( 100, 100, 500, 190 ), "name" );
			PlainHeading = plain.AddHeading( 0, new UiRect( 600, 100, 1000, 190 ), "name" );
			Beside = Root.Add( new UiButton { Rect = new UiRect( 100, 700, 400, 800 ) } );
		}
	}
}
