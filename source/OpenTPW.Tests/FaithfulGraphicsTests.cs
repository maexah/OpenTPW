using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using OpenTPW.UI;

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
		var wasSaves = SaveFileSystemOrNull;

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

			// The old lines set nothing, and each is named in a warning that gives the line replacing it.
			var warned = new System.Collections.Generic.List<string>();
			Logger.LogDelegate hear = ( severity, text ) =>
			{
				if ( severity == Logger.Level.Warning )
					warned.Add( text );
			};

			GameOptions.Current = new();
			File.WriteAllText( file, "textures original\nground original\ntextures improved\n" );
			Logger.OnLog += hear;

			try { SaveFolder.LoadDisplay(); }
			finally { Logger.OnLog -= hear; }

			Assert.IsFalse( GameOptions.Current.Faithful, "the old lines set nothing" );
			Assert.AreEqual( 2, warned.Count, "one warning each for the two old lines, none for a value they never had" );
			StringAssert.Contains( warned[0], "\"textures original\"" );
			StringAssert.Contains( warned[1], "\"ground original\"" );
			StringAssert.Contains( warned[0], "\"graphics faithful\" replaces it" );

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
			SaveFileSystemOrNull = wasSaves;
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

	/// <summary>
	/// <b>Under the toggle a text's pixels are not copied up before the sampler enlarges them</b> (Q644's sharpening, left
	/// off with the rest): a scale of 2.117 or 3.5 is copied 2 or 3 times without it and once with it, as is 9, which the
	/// ceiling of 4 would take.
	/// </summary>
	/// <remarks><b>Mutations:</b> the toggle not read by <c>PrescaleFor</c>, either overload.</remarks>
	[TestMethod]
	public void UnderTheToggleTheLettersAreNotSharpened()
	{
		Assert.AreEqual( (2, 3, 4), (UiText.PrescaleFor( 2.117f, false ), UiText.PrescaleFor( 3.5f, false ), UiText.PrescaleFor( 9f, false )) );
		Assert.AreEqual( (1, 1, 1), (UiText.PrescaleFor( 2.117f, true ), UiText.PrescaleFor( 3.5f, true ), UiText.PrescaleFor( 9f, true )) );

		var was = GameOptions.Current;

		try
		{
			GameOptions.Current = new() { Faithful = true };
			Assert.AreEqual( 1, UiText.PrescaleFor( 3.5f ), "the toggle as the game holds it" );
			Assert.AreEqual( 1, UiText.Plan( new PixelRect( 0, 0, 2000, 400 ), 100, 15, 0, 4f, 1f, TextAlign.Start, TextAlign.Start ).Prescale );
			GameOptions.Current = new();
			Assert.AreEqual( 3, UiText.PrescaleFor( 3.5f ) );
			Assert.AreEqual( 4, UiText.Plan( new PixelRect( 0, 0, 2000, 400 ), 100, 15, 0, 4f, 1f, TextAlign.Start, TextAlign.Start ).Prescale );
		}
		finally { GameOptions.Current = was; }
	}
}
