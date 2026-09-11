using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenTPW.UI;
using System.Collections.Generic;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// The interface's 2048x1536 screen put onto a window of any shape. The layout is authored at 4:3,
/// so on a 4:3 window it has to come out exactly as the original draws it, and on any other window
/// it has to keep its shape and stay inside the window - which scaling it by the window's height
/// alone did not: a 1280x1024 window was laid out 1365 pixels wide and ran off both edges.
/// </summary>
[TestClass]
public class VirtualScreenTests
{
	/// <summary>The original's own modes, from _Resolution.sam. All 4:3 but for 1280x1024, which is 5:4.</summary>
	private static readonly (int Width, int Height)[] Original =
	{
		(512, 384), (640, 480), (800, 600), (1024, 768), (1600, 1200), (2048, 1536)
	};

	private static readonly (int Width, int Height)[] Wider =
	{
		(1280, 720), (1366, 768), (1920, 1080), (2560, 1080), (3440, 1440), (3840, 2160)
	};

	private static readonly (int Width, int Height)[] Narrower =
	{
		(1280, 1024), (1600, 1200 + 200), (900, 1200), (600, 800), (512, 512)
	};

	private static IEnumerable<(int Width, int Height)> EverySize
		=> Original.Concat( Wider ).Concat( Narrower );

	private static readonly Anchor[] Across = { Anchor.Left, Anchor.Centre, Anchor.Right };
	private static readonly VerticalAnchor[] Down = { VerticalAnchor.Top, VerticalAnchor.Middle, VerticalAnchor.Bottom };

	private static void At( (int Width, int Height) size ) => Screen.Size = new Point2( size.Width, size.Height );

	[TestCleanup]
	public void PutTheScreenBack() => Screen.Size = new Point2( 1280, 720 );

	/// <summary>
	/// Whatever shape the window is, the whole virtual screen lands inside it and keeps its 4:3
	/// shape. This is the one that fails on the old height-only scale.
	/// </summary>
	[TestMethod]
	public void TheWholeScreenFitsInsideTheWindowAndKeepsItsShape()
	{
		foreach ( var size in EverySize )
		{
			At( size );

			foreach ( var across in Across )
			{
				foreach ( var down in Down )
				{
					var where = $"{size.Width}x{size.Height}, {across}/{down}";
					var rect = VirtualScreen.ToPixels( VirtualScreen.Whole, across, down );

					Assert.IsTrue( rect.Width <= size.Width + 0.01f, $"wider than the window at {where}: {rect.Width}" );
					Assert.IsTrue( rect.Height <= size.Height + 0.01f, $"taller than the window at {where}: {rect.Height}" );
					Assert.IsTrue( rect.X >= -0.01f, $"off the left at {where}: {rect.X}" );
					Assert.IsTrue( rect.Y >= -0.01f, $"off the top at {where}: {rect.Y}" );
					Assert.IsTrue( rect.X + rect.Width <= size.Width + 0.01f, $"off the right at {where}" );
					Assert.IsTrue( rect.Y + rect.Height <= size.Height + 0.01f, $"off the bottom at {where}" );

					// 2048x1536 drawn without distortion, whatever it has been scaled by.
					Assert.AreEqual( 4f / 3f, rect.Width / rect.Height, 0.001f, $"shape at {where}" );
				}
			}
		}
	}

	/// <summary>On a 4:3 window every pin lands in the same place and the layout is the original's.</summary>
	[TestMethod]
	public void AFourThreeWindowIsTheOriginalsLayoutExactly()
	{
		foreach ( var size in Original.Where( size => size.Width * 3 == size.Height * 4 ) )
		{
			At( size );

			var where = $"{size.Width}x{size.Height}";
			Assert.AreEqual( size.Height / 1536f, VirtualScreen.Scale, 1e-6f, $"scale at {where}" );

			foreach ( var across in Across )
				Assert.AreEqual( 0f, VirtualScreen.Offset( across ), 1e-4f, $"offset {across} at {where}" );

			foreach ( var down in Down )
				Assert.AreEqual( 0f, VirtualScreen.OffsetDown( down ), 1e-4f, $"offset {down} at {where}" );

			var rect = VirtualScreen.ToPixels( VirtualScreen.Whole, Anchor.Centre, VerticalAnchor.Middle );

			Assert.AreEqual( 0f, rect.X, 1e-4f, $"left at {where}" );
			Assert.AreEqual( 0f, rect.Y, 1e-4f, $"top at {where}" );
			Assert.AreEqual( size.Width, rect.Width, 1e-3f, $"width at {where}" );
			Assert.AreEqual( size.Height, rect.Height, 1e-3f, $"height at {where}" );
		}
	}

	/// <summary>
	/// A window wider than 4:3 is held by its height and a narrower one by its width, which is what
	/// decides whether the room left over is down the sides or above and below.
	/// </summary>
	[TestMethod]
	public void WhicheverSideRunsOutFirstIsWhatHoldsTheScale()
	{
		foreach ( var size in Wider )
		{
			At( size );
			Assert.AreEqual( size.Height / 1536f, VirtualScreen.Scale, 1e-6f, $"held by the height at {size.Width}x{size.Height}" );

			foreach ( var down in Down )
				Assert.AreEqual( 0f, VirtualScreen.OffsetDown( down ), 1e-4f, $"no room above or below at {size.Width}x{size.Height}" );
		}

		foreach ( var size in Narrower )
		{
			At( size );
			Assert.AreEqual( size.Width / 2048f, VirtualScreen.Scale, 1e-6f, $"held by the width at {size.Width}x{size.Height}" );

			foreach ( var across in Across )
				Assert.AreEqual( 0f, VirtualScreen.Offset( across ), 1e-4f, $"no room either side at {size.Width}x{size.Height}" );
		}
	}

	/// <summary>What is drawn at a place and what is pointed at there are the same place.</summary>
	[TestMethod]
	public void PixelsAndVirtualUnitsRoundTrip()
	{
		var rects = new[]
		{
			VirtualScreen.Whole,
			new UiRect( 884, 46, 2048, 193 ),     // a player slot
			new UiRect( 28, 855, 476, 1521 ),     // the island panel
			new UiRect( 500, 233, 1516, 830 )     // a dialog
		};

		foreach ( var size in EverySize )
		{
			At( size );

			foreach ( var rect in rects )
			{
				foreach ( var across in Across )
				{
					foreach ( var down in Down )
					{
						var pixels = VirtualScreen.ToPixels( rect, across, down );

						Assert.AreEqual( rect.Left, VirtualScreen.ToVirtualX( pixels.X, across ), 0.01f,
							$"x at {size.Width}x{size.Height}, {across}" );

						Assert.AreEqual( rect.Top, VirtualScreen.ToVirtualY( pixels.Y, down ), 0.01f,
							$"y at {size.Width}x{size.Height}, {down}" );
					}
				}
			}
		}
	}

	/// <summary>
	/// The lettering is chosen for the size the interface is drawn at, not for the window's height.
	/// A 900x1200 window draws the same interface as a 900x675 one - the room above and below is all
	/// that differs - so it has to be given the same fonts at the same scale. Going by the window's
	/// height would hand the tall one the largest set to put in the smallest buttons.
	/// </summary>
	[TestMethod]
	public void TheFontSetFollowsTheInterfaceAndNotTheWindow()
	{
		At( (900, 675) );
		var wide = (Set: UiFonts.SetIndex, Scale: UiFonts.Scale);

		At( (900, 1200) );

		Assert.AreEqual( wide.Set, UiFonts.SetIndex, "set" );
		Assert.AreEqual( wide.Scale, UiFonts.Scale, 1e-6f, "scale" );

		// And a 4:3 window is still chosen by its own height, as it always was.
		At( (1024, 768) );
		Assert.AreEqual( 3, UiFonts.SetIndex, "1024x768" );
		Assert.AreEqual( 1f, UiFonts.Scale, 1e-6f, "1024x768 scale" );
	}
}
