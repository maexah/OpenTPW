using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenTPW.Tests;

/// <summary>
/// The status box <see cref="Level"/> draws over the topmost window (Q1029, from Q211): the overlay pass draws the box of the
/// last window that is neither hidden nor put away, and of no other (<c>docs/exe/hud.md</c>). The windows are stand-ins that
/// count the draws.
/// </summary>
[TestClass]
public class LevelActiveWarningTests
{
	private class Box : UI.UiWindow, UI.IStatusBox
	{
		public int Drawn;

		public Box( UI.WindowStack stack ) : base( stack ) => Root = new UI.UiControl();

		public void DrawStatus() => ++Drawn;
	}

	private sealed class Plain : UI.UiWindow
	{
		public Plain( UI.WindowStack stack ) : base( stack ) => Root = new UI.UiControl();
	}

	[TestMethod]
	public void OnlyTheTopmostShownWindowsBoxIsDrawn()
	{
		var stack = new UI.WindowStack();
		Box under = new( stack ), over = new( stack ), hidden = new( stack ) { Hidden = true }, away = new( stack ) { PutAway = true };

		Level.DrawActiveWarning( new List<UI.UiWindow> { under, over, hidden, away } );

		Assert.AreEqual( (0, 1, 0, 0), (under.Drawn, over.Drawn, hidden.Drawn, away.Drawn), "the last window neither hidden nor put away" );

		Level.DrawActiveWarning( new List<UI.UiWindow> { under } );
		Assert.AreEqual( 1, under.Drawn, "a lone window" );
	}

	[TestMethod]
	public void AWindowWithNoBoxOverOneWithAnotherDrawsNone()
	{
		var stack = new UI.WindowStack();
		var under = new Box( stack );

		Level.DrawActiveWarning( new List<UI.UiWindow> { under, new Plain( stack ) } );
		Level.DrawActiveWarning( [] );

		Assert.AreEqual( 0, under.Drawn, "the topmost window is not one with a box, so the covered box stays unseen" );
	}

	/// <summary>The two real windows draw their box through the interface <see cref="Level"/> looks for.</summary>
	[TestMethod]
	public void TheRideAndShopWindowsAreStatusBoxes()
	{
		Assert.IsTrue( typeof( UI.IStatusBox ).IsAssignableFrom( typeof( UI.ParkObjectWindow ) ) );
		Assert.IsTrue( typeof( UI.IStatusBox ).IsAssignableFrom( typeof( UI.ParkShopWindow ) ) );
	}

	/// <summary>The level hands its own stack's windows to it (the frame's overlay pass calls the private method).</summary>
	[TestMethod]
	public void TheLevelDrawsTheBoxOfItsOwnStacksTopWindow()
	{
		Screen.Size = new Point2( 2048, 1536 );
		var level = (Level)RuntimeHelpers.GetUninitializedObject( typeof( Level ) );
		var stack = new UI.WindowStack();
		var window = new Box( stack );
		stack.Open( window );
		typeof( Level ).GetField( "_windows", BindingFlags.Instance | BindingFlags.NonPublic )!.SetValue( level, stack );

		typeof( Level ).GetMethod( "DrawActiveWarning", BindingFlags.Instance | BindingFlags.NonPublic, System.Type.EmptyTypes )!.Invoke( level, null );

		Assert.AreEqual( 1, window.Drawn );

		typeof( Level ).GetField( "_windows", BindingFlags.Instance | BindingFlags.NonPublic )!.SetValue( level, null );
		typeof( Level ).GetMethod( "DrawActiveWarning", BindingFlags.Instance | BindingFlags.NonPublic, System.Type.EmptyTypes )!.Invoke( level, null );

		Assert.AreEqual( 1, window.Drawn, "no stack, no draw" );
	}
}
