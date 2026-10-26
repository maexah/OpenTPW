using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// The shipped game's textures are A4R4G4B4: every channel cut to its top four bits as it is stored, and the nibble
/// read back as <c>n / 15</c> (docs/exe/textures.md, "The texture cache's surface format"). OpenTPW keeps eight unless
/// opentpw.cfg asks for the original's depth, and then <c>TEXTURE32</c> 1 keeps eight, as in the original.
/// </summary>
[TestClass]
public class TextureCutTests
{
	/// <summary>A channel keeps its top four bits, never rounded, and a nibble is widened to <c>n x 17</c>.</summary>
	/// <remarks><b>Mutation:</b> rounding in place of the cut, the nibble widened as <c>n &lt;&lt; 4</c>, or the cut
	/// dropped, fails a pair.</remarks>
	[TestMethod]
	public void AChannelKeepsItsTopFourBitsWidenedByRepeatingThem()
	{
		var pairs = new (byte In, byte Out)[] { (0, 0), (15, 0), (16, 17), (14, 0), (40, 34), (239, 238), (240, 255), (255, 255) };

		foreach ( var (value, cut) in pairs )
			Assert.AreEqual( cut, Texture.CutChannel( value ), $"{value}" );
	}

	/// <summary>
	/// Every level is cut, each the rounded mean of the eight-bit level above's two by two, down to one texel; an odd
	/// side's last column is its own.
	/// </summary>
	/// <remarks><b>Mutation:</b> the levels made from the cut level above, the mean unrounded, the levels stopped short,
	/// or a level left uncut, fails.</remarks>
	[TestMethod]
	public void EveryLevelIsTheMeanOfTheEightBitLevelAboveCut()
	{
		// Three by two: a row of 30, 33, 200 over a row of 31, 33, 100, alpha 255; one byte a texel is spelt out below.
		byte[] Texel( byte v ) => [v, v, v, 255];
		var data = new[] { 30, 33, 200, 31, 33, 100 }.SelectMany( v => Texel( (byte)v ) ).ToArray();

		var levels = Texture.CutLevels( data, 3, 2 ).ToList();

		Assert.AreEqual( 2, levels.Count, "3 x 2, then 1 x 1" );
		Assert.AreEqual( (3, 2), (levels[0].Width, levels[0].Height) );
		CollectionAssert.AreEqual( new byte[] { 17, 34, 204, 17, 34, 102 }, Enumerable.Range( 0, 6 ).Select( i => levels[0].Pixels[i * 4] ).ToArray() );
		Assert.AreEqual( 255, levels[0].Pixels[3], "alpha 255 stays 255" );

		// (30 + 33 + 31 + 33 + 2) / 4 = 32 of the eight-bit texels -> 34; the cut texels' 17, 34, 17, 34 would give 25 -> 17.
		Assert.AreEqual( (1, 1), (levels[1].Width, levels[1].Height) );
		Assert.AreEqual( 34, levels[1].Pixels[0] );
	}

	/// <summary>The game's art is cut unless the game keeps eight bits; OpenTPW's own pixels never are.</summary>
	/// <remarks><b>Mutation:</b> either test in <c>CutsFor</c> dropped or turned round fails.</remarks>
	[TestMethod]
	public void TheGamesArtIsCutUnlessItKeepsEightBits()
	{
		var was = Texture.KeepsEightBits;

		try
		{
			Texture.KeepsEightBits = false;
			Assert.IsTrue( Texture.CutsFor( TextureFlags.Repeat ) );
			Assert.IsFalse( Texture.CutsFor( TextureFlags.OwnPixels | TextureFlags.PointFilter ) );

			Texture.KeepsEightBits = true;
			Assert.IsFalse( Texture.CutsFor( TextureFlags.Repeat ) );
		}
		finally
		{
			Texture.KeepsEightBits = was;
		}
	}

	/// <summary>No shipped detail file sets <c>TEXTURE32</c> to 1; a file saying 1 asks for eight, and 2 does not.</summary>
	/// <remarks><b>Mutation:</b> the value's test made <c>!= 0</c> keeps eight at 2; turned round, the shipped files keep
	/// eight; the key ignored, the written 1 is cut.</remarks>
	[TestMethod]
	public void OnlyTexture32OneKeepsEightBits()
	{
		var folder = Path.Combine( Path.GetTempPath(), $"opentpw-texture32-{System.Guid.NewGuid():N}" );
		Directory.CreateDirectory( folder );

		try
		{
			File.WriteAllText( Path.Combine( folder, "one.sam" ), "GraphicalOptions.TEXTURE32\t\t1\n" );
			File.WriteAllText( Path.Combine( folder, "two.sam" ), "GraphicalOptions.TEXTURE32\t\t2\n" );
			FileSystem = new BaseFileSystem( folder );
			Log = new();

			Assert.IsTrue( Level.Texture32( "one.sam" ) );
			Assert.IsFalse( Level.Texture32( "two.sam" ) );
		}
		finally
		{
			Directory.Delete( folder, true );
		}

		FileSystem = GameData.Required();

		Assert.IsFalse( Level.Texture32( "low.sam" ) );
		Assert.IsFalse( Level.Texture32( "med.sam" ) );
		Assert.IsFalse( Level.Texture32( "high.sam" ) );
	}

	/// <summary>
	/// Eight bits a channel by default, whatever the detail file says (a deviation from the original); at the original's
	/// depth, eight only for <c>TEXTURE32</c> 1, as the original's format <c>2 + TEXTURE32</c>.
	/// </summary>
	/// <remarks><b>Mutation:</b> the original's rule taken always cuts the default; the option ignored keeps eight at
	/// the original's depth; <c>TEXTURE32</c> ignored cuts its 1; the option's default made true cuts the game.</remarks>
	[TestMethod]
	public void EightBitsAreKeptUnlessTheOriginalsDepthIsAsked()
	{
		Assert.IsTrue( Texture.KeepsEightBitsUnder( originalDepth: false, texture32: false ), "default, every shipped file" );
		Assert.IsTrue( Texture.KeepsEightBitsUnder( originalDepth: false, texture32: true ) );
		Assert.IsFalse( Texture.KeepsEightBitsUnder( originalDepth: true, texture32: false ), "the original's own, shipped" );
		Assert.IsTrue( Texture.KeepsEightBitsUnder( originalDepth: true, texture32: true ) );
		Assert.IsFalse( new GameOptions().Faithful, "OpenTPW's default" );
	}

	/// <summary>
	/// The boot wiring (<c>Game.Run</c>): opentpw.cfg's line, read, then the options and the detail file's
	/// <c>TEXTURE32</c> decide the depth. Eight bits by default whatever the detail file says; at <c>original</c> the
	/// detail file decides, and is not even read by default.
	/// </summary>
	/// <remarks><b>Mutation:</b> the arguments swapped, the depth forced to eight, or the detail file read by default fails a step.</remarks>
	[TestMethod]
	public void TheBootDepthFollowsTheConfigAndTheDetailFile()
	{
		var read = 0;
		var was = Texture.KeepsEightBits;

		bool Texture32( bool value ) { ++read; return value; }

		var full = new GameOptions { Faithful = false };
		var original = new GameOptions { Faithful = true };

		try
		{
			Texture.KeepsEightBits = false;
			Assert.IsTrue( Texture.SetDepthFor( full, () => Texture32( false ) ), "the default keeps eight, TEXTURE32 0" );
			Assert.IsTrue( Texture.KeepsEightBits, "and sets it" );
			Assert.IsTrue( Texture.SetDepthFor( full, () => Texture32( true ) ), "the default keeps eight, TEXTURE32 1" );
			Assert.AreEqual( 0, read, "and reads no detail file" );

			Assert.IsFalse( Texture.SetDepthFor( original, () => Texture32( false ) ), "the original's depth with TEXTURE32 0 cuts" );
			Assert.IsFalse( Texture.KeepsEightBits, "and sets that" );
			Assert.IsTrue( Texture.SetDepthFor( original, () => Texture32( true ) ), "the original's depth with TEXTURE32 1 keeps eight" );
			Assert.IsTrue( Texture.KeepsEightBits );
			Assert.AreEqual( 2, read );
		}
		finally { Texture.KeepsEightBits = was; }
	}

	/// <summary>
	/// The sea's <c>gby_sur1</c> keeps red 7.4 of its 13.5 once cut, 63% of its texels under 16 storing nought
	/// (<c>q492/census-gby_sur.txt</c>); green 79.6 keeps 76.6.
	/// </summary>
	/// <remarks><b>Mutation:</b> the nibble widened as <c>n &lt;&lt; 4</c> gives red 7.0 and green 72.1; rounding, about 13.5.</remarks>
	[TestMethod]
	public void TheSeasTextureKeepsLittleOfItsRed()
	{
		FileSystem = GameData.Required();
		Log = new();

		var (image, _) = Texture.ReadWct( "generic/dynamic/textures/gby_sur1.wct" );
		var cut = Texture.CutLevels( image.Data, image.Width, image.Height ).First().Pixels;
		var texels = cut.Length / 4;

		double Mean( int c ) => Enumerable.Range( 0, texels ).Average( i => cut[(i * 4) + c] );

		Assert.AreEqual( 7.4, Mean( 0 ), 0.05, "red" );
		Assert.AreEqual( 76.6, Mean( 1 ), 0.05, "green" );
	}

	/// <summary>
	/// The choice <c>Upload</c> makes from the flags a texture was asked for: levels made here for the game's art, none
	/// (the device makes them) for OpenTPW's own pixels or under <c>TEXTURE32</c> 1.
	/// </summary>
	/// <remarks><b>Mutation:</b> <c>LevelsFor</c> reading <c>CutsFor</c> turned round, or ignoring the flags, fails;
	/// <c>Upload</c> and <c>Cut</c> take the same choice from <c>Requested</c>, so a flag dropped at a call site is the
	/// game-only half (<c>ParkFootprintPicture.SquareFlags</c> has its own test).</remarks>
	[TestMethod]
	public void UploadCutsFromTheFlagsItWasAskedFor()
	{
		byte[] data = [200, 100, 50, 255];
		var was = Texture.KeepsEightBits;

		try
		{
			Texture.KeepsEightBits = false;
			Assert.AreEqual( 1, Texture.LevelsFor( data, 1, 1, TextureFlags.None )!.Count() );
			Assert.AreEqual( 204, Texture.LevelsFor( data, 1, 1, TextureFlags.Repeat )!.First().Pixels[0], "200 cut" );
			Assert.IsNull( Texture.LevelsFor( data, 1, 1, TextureFlags.OwnPixels ) );

			Texture.KeepsEightBits = true;
			Assert.IsNull( Texture.LevelsFor( data, 1, 1, TextureFlags.None ) );
		}
		finally
		{
			Texture.KeepsEightBits = was;
		}
	}

	/// <summary>
	/// The sky's colour ramp is read back from the 16-bit surface as <c>n &lt;&lt; 4 | 8</c> a colour channel and alpha 255
	/// (<c>0x00585d82</c>..<c>0x00585da2</c>), not as the art's <c>n x 17</c>.
	/// </summary>
	/// <remarks><b>Mutation:</b> the widening as <c>n x 17</c> (255 for 255, 17 for 16), the 8 dropped, or the alpha
	/// widened as a colour (248), fails.</remarks>
	[TestMethod]
	public void TheSkysRampIsWidenedAsANibbleThenAnEight()
	{
		byte[] ramp = [0, 15, 16, 255, 240, 130, 8, 77];

		CollectionAssert.AreEqual( new byte[] { 8, 8, 24, 255, 248, 136, 8, 255 }, Sky.WidenRamp( ramp ) );
	}
}
