using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// Reading TP.ico, the icon the window wears. These read the real file from the installation and are skipped
/// where there is none - see <see cref="GameData"/>.
/// </summary>
[TestClass]
public class IconTests
{
	private IconFile Read()
	{
		// Gates the test on an installation and sets the log the reader reports through.
		GameData.Required();

		var path = GameDir.GetPath( "TP.ico" );

		Assert.IsTrue( File.Exists( path ), $"no TP.ico in {GameDir.Root}" );

		using var stream = File.OpenRead( path );
		return new IconFile( stream );
	}

	/// <summary>
	/// The four images the game ships, measured from each image's own header rather than from the directory -
	/// which is the point, because TP.ico leaves the bit count at zero in all four of its directory entries.
	/// </summary>
	[TestMethod]
	public void TheGamesIconHoldsFourImages()
	{
		var icon = Read();

		CollectionAssert.AreEqual(
			new[] { (32, 32, 4), (32, 32, 8), (32, 32, 24), (64, 64, 8) },
			icon.Images.Select( image => (image.Width, image.Height, image.BitsPerPixel) ).ToArray() );

		foreach ( var image in icon.Images )
			Assert.AreEqual( image.Width * image.Height * 4, image.Pixels.Length, $"{image.Width}x{image.Height} is not RGBA" );
	}

	/// <summary>The largest is the one the window wants, and here it is also the only one of its size.</summary>
	[TestMethod]
	public void TheLargestImageIsTheOneChosen()
	{
		var best = Read().Best;

		Assert.IsNotNull( best );
		Assert.AreEqual( 64, best.Value.Width );
		Assert.AreEqual( 64, best.Value.Height );
	}

	/// <summary>
	/// The AND mask becomes the alpha channel, so the head is cut out of its corners rather than sitting on a
	/// square. The counts are what an independent decoder written before this one reads out of the same file,
	/// so they pin the mask, the row order and the padding together.
	/// </summary>
	[TestMethod]
	public void TheMaskCutsTheHeadOut()
	{
		var icon = Read();

		var opaque = icon.Images.Select( image => image.Pixels.Where( ( _, i ) => i % 4 == 3 ).Count( alpha => alpha == 255 ) );

		CollectionAssert.AreEqual( new[] { 749, 748, 748, 2847 }, opaque.ToArray() );

		var best = icon.Best!.Value;

		Assert.AreEqual( 0, best.Pixels[3], "the top-left corner is outside the head" );
		Assert.AreEqual( 255, best.Pixels[(32 * 64 + 32) * 4 + 3], "the middle of the head is drawn" );
	}
}
