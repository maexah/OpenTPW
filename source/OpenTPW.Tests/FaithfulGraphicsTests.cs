using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;

namespace OpenTPW.Tests;

/// <summary>
/// The faithful graphics toggle (<see cref="GameOptions.Faithful"/>, Q645): one line of opentpw.cfg, off by default, that
/// turns every improvement to the original's look off at once. The texture depth's and the ground's own readers are
/// <c>TextureCutTests</c>' and <c>GroundTileSizeTests</c>'; the buy and hire screens', <c>ParkListScreenRewriteTests</c>'.
/// </summary>
[TestClass]
public class FaithfulGraphicsTests
{
	/// <summary>
	/// opentpw.cfg's <c>graphics</c> line: <c>faithful</c> sets the toggle, <c>improved</c> or any other word clears it, the
	/// two lines it replaces (<c>textures original</c>, <c>ground original</c>) are read no more, and the file written says
	/// which, read back the same.
	/// </summary>
	/// <remarks><b>Mutation:</b> the line not read, read as any value, written as the other word, or an old line still
	/// setting it, fails a step.</remarks>
	[TestMethod]
	public void TheToggleIsKeptInOpenTpwCfg()
	{
		var folder = Path.Combine( Path.GetTempPath(), $"opentpw-graphics-{System.Guid.NewGuid():N}" );
		Directory.CreateDirectory( folder );
		var was = GameOptions.Current;
		var wasSaves = SaveFileSystem;

		try
		{
			Log = new();
			SaveFileSystem = new BaseFileSystem( folder );
			var file = Path.Combine( folder, "opentpw.cfg" );

			Assert.IsFalse( new GameOptions().Faithful, "OpenTPW's default" );

			GameOptions.Current = new();
			File.WriteAllText( file, "display windowed\ngraphics faithful\n" );
			SaveFolder.LoadDisplay();
			Assert.IsTrue( GameOptions.Current.Faithful, "faithful" );

			File.WriteAllText( file, "graphics improved\n" );
			SaveFolder.LoadDisplay();
			Assert.IsFalse( GameOptions.Current.Faithful, "improved" );

			// Only the word "faithful" asks for it.
			foreach ( var other in new[] { "original", "yes", "1", "faithfully" } )
			{
				GameOptions.Current.Faithful = true;
				File.WriteAllText( file, $"graphics {other}\n" );
				SaveFolder.LoadDisplay();
				Assert.IsFalse( GameOptions.Current.Faithful, $"\"{other}\" is not \"faithful\"" );
			}

			GameOptions.Current = new();
			File.WriteAllText( file, "textures original\nground original\n" );
			SaveFolder.LoadDisplay();
			Assert.IsFalse( GameOptions.Current.Faithful, "the old lines set nothing" );

			GameOptions.Current = new() { Faithful = true };
			SaveFolder.SaveDisplay();
			StringAssert.Contains( File.ReadAllText( file ), "\ngraphics faithful\n" );
			GameOptions.Current = new();
			SaveFolder.LoadDisplay();
			Assert.IsTrue( GameOptions.Current.Faithful, "written faithful, read back" );

			GameOptions.Current = new();
			SaveFolder.SaveDisplay();
			StringAssert.Contains( File.ReadAllText( file ), "\ngraphics improved\n" );
		}
		finally
		{
			GameOptions.Current = was;
			SaveFileSystem = wasSaves;
			Directory.Delete( folder, true );
		}
	}

	/// <summary>
	/// <b>A pump made under the toggle is drawn picture by picture</b>: at rate 2, five passes into the run its frame is 2
	/// (5 × 28834 = 2.2 frames), and that is the part drawn, unmoved; made without it, the part drawn is <c>fluid</c>, moved
	/// by the scroll. The logic is the same in both.
	/// </summary>
	/// <remarks><b>Mutations:</b> the pump not reading the toggle; the faithful pump still drawing part 0 or still moved.</remarks>
	[TestMethod]
	public void AFaithfulPumpIsDrawnPictureByPicture()
	{
		var was = GameOptions.Current;

		try
		{
			UI.ResearchPump Run( bool faithful )
			{
				GameOptions.Current = new() { Faithful = faithful };
				var pump = new UI.ResearchPump();
				pump.SetRate( 2 );

				for ( var pass = 0; pass < 37 + 5; ++pass )
					pump.Pass();

				return pump;
			}

			var stepped = Run( true );
			var smooth = Run( false );

			Assert.AreEqual( (false, true), (stepped.Smooth, smooth.Smooth) );
			Assert.AreEqual( (UI.ResearchPump.PumpState.Running, 2, 5f * 28834), (stepped.State, stepped.Frame, stepped.Scroll), "the same logic" );
			Assert.AreEqual( (smooth.State, smooth.Frame, smooth.Scroll), (stepped.State, stepped.Frame, stepped.Scroll) );

			Assert.AreEqual( (2, 0f), (stepped.DrawnPart, stepped.Shift()), "the frame itself, unmoved" );
			StringAssert.EndsWith( stepped.ToString(), " stepped" );
			Assert.AreEqual( 0, smooth.DrawnPart, "fluid" );
			Assert.AreEqual( -(5f * 28834 / 65536) / 31, smooth.Shift(), 1e-6f, "moved by the scroll" );
		}
		finally { GameOptions.Current = was; }
	}

	/// <summary>
	/// <b>Under the toggle the flags take the wind straight</b>, as the original's do: from 10 degrees to a wind of 100 in
	/// one frame, and the shorter way round through nought from 350 to 20; without it they take
	/// <see cref="Time.SmoothingFactor"/> of the turn rate. The lettering is 1, the original's, under it and 1.2 without.
	/// </summary>
	/// <remarks><b>Mutations:</b> the factor 1 not taken under the toggle; the eased factor taken under it; the lettering
	/// not reading it.</remarks>
	[TestMethod]
	public void UnderTheToggleTheFlagsJumpAndTheLettersAreTheOriginals()
	{
		Assert.AreEqual( 1f, ParkFlags.TurnFactor( true ) );
		Assert.AreEqual( 100f, ParkFlags.WindAfter( 10f, 100f, ParkFlags.TurnFactor( true ), false ) );
		Assert.AreEqual( 20f, ParkFlags.WindAfter( 350f, 20f, ParkFlags.TurnFactor( true ), false ), 1e-4f );
		Assert.AreEqual( Time.SmoothingFactor( ParkFlags.TurnRate ), ParkFlags.TurnFactor( false ) );

		var was = GameOptions.Current;

		try
		{
			GameOptions.Current = new() { Faithful = true };
			Assert.AreEqual( 1f, UI.UiFonts.ParkScreenLettering );
			GameOptions.Current = new();
			Assert.AreEqual( 1.2f, UI.UiFonts.ParkScreenLettering );
		}
		finally { GameOptions.Current = was; }
	}
}
