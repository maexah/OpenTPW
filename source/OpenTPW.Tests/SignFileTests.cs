using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// Every sign the game ships, read the way the game reads them.
///
/// <para>
/// <b>These exist because the obvious test cannot tell a broken reader from a working one.</b>
/// A reader that demands a 17,373-byte header before it looks at whether the file declares artwork
/// reads the four lobby boards and most of the others that ship a picture, and refuses all sixty-one
/// that do not. Testing it against a lobby sign - the natural choice, and the one the format was worked
/// out on - is green either way. Only walking all of them catches it, so that is what this does.
/// </para>
/// <para>
/// The walk is deliberately a discovery rather than a list of names. A test naming the signs it expects
/// would still pass if the reader lost the ability to find the rest of them, and the counts below are
/// the point of the exercise: far more boards are bare than painted, and a bare board is a normal sign
/// and not a truncated one.
/// </para>
/// <para>
/// These read real game files and are skipped where there is no installation - see <see cref="GameData"/>.
/// </para>
/// </summary>
[TestClass]
public class SignFileTests
{
	[TestInitialize]
	public void MountTheGame()
	{
		FileSystem = GameData.Required();
	}

	/// <summary>The folders a theme keeps free-standing items in - the same four ParkItemCatalogue walks.</summary>
	private static readonly string[] Folders = ["features", "shops", "rides", "sideshow"];

	/// <summary>
	/// What the game actually holds, counted through its own file system rather than off a disk full of
	/// extracted copies: 23 boards with a picture on them and 61 with none.
	/// </summary>
	private const int ExpectedPainted = 23;
	private const int ExpectedBare = 61;
	private const int ExpectedSigns = ExpectedPainted + ExpectedBare;

	/// <summary>
	/// Parsed once and kept. Decoding the painted boards is wavelet work, and three tests each doing it
	/// from scratch would cost more than the rest of the suite put together.
	/// </summary>
	private static (string Path, SignFile Sign)[]? _signs;

	private static (string Path, SignFile Sign)[] Signs()
	{
		return _signs ??= [.. EverySignPath().Select( path => (path, new SignFile( path )) )];
	}

	/// <summary>
	/// Where every sign in the game lives: the lobby's four beside the models that carry their meshes,
	/// and a ride's beside its own model, under whichever of the four item folders it is sold from.
	/// </summary>
	private static List<string> EverySignPath()
	{
		var signs = new List<string>();

		void Collect( string directory )
		{
			foreach ( var entry in Entries( directory, directories: false ) )
			{
				var name = Path.GetFileName( entry );

				if ( !string.IsNullOrEmpty( name ) && name.EndsWith( ".sgn", StringComparison.OrdinalIgnoreCase ) )
					signs.Add( $"{directory}/{name}" );
			}
		}

		Collect( "lobby/terrain" );

		foreach ( var theme in Entries( "levels", directories: true ) )
		{
			var themeName = Path.GetFileName( theme );

			if ( string.IsNullOrEmpty( themeName ) )
				continue;

			foreach ( var folder in Folders )
			{
				foreach ( var item in Entries( $"levels/{themeName}/{folder}", directories: true ) )
				{
					var stem = Path.GetFileName( item );

					if ( !string.IsNullOrEmpty( stem ) )
						Collect( $"levels/{themeName}/{folder}/{stem}" );
				}
			}
		}

		return signs;
	}

	/// <summary>
	/// A theme that does not sell one kind of thing has no folder for it, and asking throws rather than
	/// answering nothing - so this answers nothing, exactly as ParkItemCatalogue does.
	/// </summary>
	private static string[] Entries( string path, bool directories )
	{
		try
		{
			return directories ? FileSystem.GetDirectories( path ) : FileSystem.GetFiles( path );
		}
		catch ( Exception )
		{
			return [];
		}
	}

	/// <summary>
	/// The one that would have caught it: not one sign in the game is refused.
	/// </summary>
	[TestMethod]
	public void EverySignInTheGameReads()
	{
		var signs = Signs();

		var refused = signs.Where( entry => !entry.Sign.IsValid ).Select( entry => entry.Path ).ToArray();

		Assert.AreEqual( 0, refused.Length,
			$"{refused.Length} of {signs.Length} signs would not read, starting with '{refused.FirstOrDefault()}'" );

		// Counted after the reading rather than before it, so a surprise in the count cannot fire first and
		// hide a sign that would not read. A walk that quietly found nothing - or half of them - would
		// otherwise pass every assertion here.
		Assert.AreEqual( ExpectedSigns, signs.Length, "signs found by the walk" );
	}

	/// <summary>
	/// A board with no picture on it is a normal sign, and most of them are.
	///
	/// <para>
	/// The original clears such a board to transparent black and skips the artwork outright, leaving the
	/// ride's name lettered onto nothing with the ride showing through behind it. So a sign is expected
	/// to read with no image at all, and the reader must say valid-with-no-image rather than failing.
	/// </para>
	/// </summary>
	[TestMethod]
	public void MostBoardsCarryNoArtworkAndThatIsNotAFailure()
	{
		var signs = Signs();

		var bare = signs.Count( entry => entry.Sign.IsValid && entry.Sign.Image == null );
		var painted = signs.Count( entry => entry.Sign.IsValid && entry.Sign.Image != null );

		Assert.AreEqual( ExpectedPainted, painted, "boards with a picture on them" );
		Assert.AreEqual( ExpectedBare, bare, "boards with none" );
	}

	/// <summary>
	/// Every sign names two fonts and a file for each, painted or not.
	///
	/// <para>
	/// This is what makes a bare board worth reading at all: the lettering is the whole of its content.
	/// The two font records are read unconditionally by the engine, with no count anywhere in the file,
	/// and all 84 shipped signs fill both - including the three that set both line modes to zero, whose
	/// font records name a font the sign then inks with nothing.
	/// </para>
	/// </summary>
	[TestMethod]
	public void EverySignNamesTwoFontsAndAFileForEach()
	{
		var wrong = Signs()
			.Where( entry => entry.Sign.IsValid )
			.Where( entry => entry.Sign.Fonts.Count != 2
				|| entry.Sign.Fonts.Any( font => string.IsNullOrEmpty( font.FileName ) ) )
			.Select( entry => entry.Path )
			.ToArray();

		Assert.AreEqual( 0, wrong.Length,
			$"{wrong.Length} signs do not name two fonts with a file each, starting with '{wrong.FirstOrDefault()}'" );
	}

	/// <summary>
	/// A painted board decodes at the size its chunks are encoded at, which is not the size it states.
	///
	/// <para>
	/// The header of every painted sign says 256x128, and that is true of the artwork - but the chunks
	/// are encoded at the wavelet's aligned 256x256, and only the top half of the decoded image carries
	/// the board. The two readings cannot be told apart by the colour chunk, which inflates to 98304
	/// bytes either way, but the alpha chunk settles it at 65536: that is 256*256, not 256*128.
	/// </para>
	/// </summary>
	[TestMethod]
	public void APaintedBoardDecodesAtTheSizeItsChunksAreEncodedAt()
	{
		var painted = Signs().FirstOrDefault( entry => entry.Sign.IsValid && entry.Sign.Image != null );

		Assert.IsNotNull( painted.Path, "no painted board was found to check" );

		var image = painted.Sign.Image!.Value;

		Assert.AreEqual( SignFile.Size, image.Width, $"{painted.Path} decoded width" );
		Assert.AreEqual( SignFile.Size, image.Height, $"{painted.Path} decoded height" );
	}

	/// <summary>
	/// A painted board is either solid art or a deliberate cut-out, and there is nothing in between.
	///
	/// <para>
	/// A material whose texture was substituted is forced see-through, because that is the only way a
	/// ride's bare board can show the ride behind it rather than filling with black - see
	/// <c>LobbyModel.MaterialFlagsFor</c>, and the original ORs the same bit in as it swaps the
	/// texture. That reaches the boards that <b>do</b> carry artwork too, which the world's shader would
	/// otherwise draw with their alpha ignored, since it only honours alpha on a see-through material. So
	/// what their alpha holds decides whether that is right for them.
	/// </para>
	/// <para>
	/// It is. Fifteen of the twenty-three are solid art carrying 1.6% partly-clear texels
	/// that never go below alpha 77 - the same figure on every one of them, unrelated artwork
	/// included, which is the <c>.wct</c> codec's ringing around a hard edge rather than anything
	/// authored. The other eight reach alpha <b>0</b> across 5% to 43% of the board: they are shaped
	/// art - the Bumper Cars, Candy Cabin, Cat Coaster, Ferris Wheel and the rest - that without the bit
	/// draws as an opaque rectangle with the cut-out it was authored with ignored.
	/// </para>
	/// <para>
	/// Only the rows the board is cut from are measured. The image decodes into a 256x256 buffer
	/// because that is the size its chunks are encoded at, but just the top <see cref="SignFile.ArtworkHeight"/>
	/// of them are ever drawn, and what the unused remainder holds does not matter.
	/// </para>
	/// </summary>
	[TestMethod]
	public void APaintedBoardIsEitherSolidOrADeliberateCutOut()
	{
		// Solid art rings a couple of per cent of partly-clear texels around its edges and never
		// reaches alpha 0; shaped art does reach 0, over a far larger share. A board sitting between
		// the two would be artwork quietly fading out, which nothing in the game does.
		const double RimFraction = 2.0;

		var between = new List<string>();
		var solid = 0;
		var cutOut = 0;

		foreach ( var (path, sign) in Signs() )
		{
			if ( sign.Image is not { } image )
				continue;

			var lowest = 255;
			var partial = 0;
			var drawn = 0;

			for ( int y = 0; y < SignFile.ArtworkHeight && y < image.Height; ++y )
			{
				var row = y * image.Width * 4;

				for ( int x = 0; x < image.Width; ++x )
				{
					var alpha = image.Data[row + (x * 4) + 3];

					lowest = Math.Min( lowest, alpha );
					++drawn;

					if ( alpha < 255 )
						++partial;
				}
			}

			var fraction = 100.0 * partial / drawn;

			if ( lowest == 0 )
				++cutOut;
			else if ( fraction <= RimFraction )
				++solid;
			else
				between.Add( $"{Path.GetFileName( path )} min={lowest} under255={fraction:F1}%" );
		}

		Assert.AreEqual( 0, between.Count,
			$"{between.Count} painted boards are neither solid nor a cut-out: {string.Join( " | ", between.Take( 25 ) )}" );

		Assert.AreEqual( 15, solid, "painted boards that are solid but for the codec's rim" );
		Assert.AreEqual( 8, cutOut, "painted boards shaped by a real cut-out" );
	}
}
