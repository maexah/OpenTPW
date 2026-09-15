using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenTPW.Tests;

/// <summary>
/// The twelve animation roles a thing's model carries, read out of real archives. These read real game
/// files and are skipped where there is no installation - see <see cref="GameData"/>.
///
/// <para>
/// <b>Three of these guard readings that would look right and be wrong.</b> Loading a role by listing an
/// archive rather than probing it in order gives the wrong entry count on exactly one item in the game.
/// Loading clips through <see cref="AnimationFile.TryLoad"/> silently drops the longest animations there
/// are. And taking a clip's length from the keys it happens to carry, rather than from the span it
/// declares, disagrees with the engine on 159 clips.
/// </para>
/// </summary>
[TestClass]
public class RideAnimationsTests
{
	private BaseFileSystem data = null!;

	[TestInitialize]
	public void MountTheGame() => data = GameData.Required();

	private RideAnimations Load( string directory, string stem )
		=> RideAnimations.Load( directory, stem, data );

	/// <summary>
	/// The twelve letters, in the order the engine's table at <c>0x006fe6bc</c> puts them, and nought for
	/// an id that names no role - which includes the sentinel 12 the engine writes into a cleared slot.
	/// </summary>
	[TestMethod]
	public void TheTwelveRolesAreTheLettersTheEngineNamesThemWith()
	{
		var letters = "CDILSMEUWBRO";

		for ( int role = 0; role < RideAnimations.RoleCount; ++role )
			Assert.AreEqual( letters[role], RideAnimations.LetterFor( role ), $"role {role}" );

		Assert.AreEqual( '\0', RideAnimations.LetterFor( RideAnimations.NoRole ), "the sentinel names no role" );
		Assert.AreEqual( '\0', RideAnimations.LetterFor( -1 ) );
	}

	/// <summary>
	/// An item's roles are the ones its own archive ships, and the letters really do land where the table
	/// puts them. The jungle's security camera ships <c>camerac</c>, <c>cameras</c>, <c>cameram</c> and
	/// <c>camerae</c> - and its script names ids 0, 4, 5 and 6, which is C, S, M and E.
	///
	/// <para>
	/// That four-for-four agreement is the letter table confirmed from shipped data, without the
	/// executable: an archive that shipped some other four files would not line up.
	/// </para>
	/// </summary>
	[TestMethod]
	public void AnItemsRolesAreTheOnesItsOwnArchiveShips()
	{
		var camera = Load( "levels/jungle/features/camera", "camera" );

		Assert.AreEqual( 1, camera.EntryCount( 0 ), "C - the construction clip" );
		Assert.AreEqual( 1, camera.EntryCount( 4 ), "S" );
		Assert.AreEqual( 1, camera.EntryCount( 5 ), "M" );
		Assert.AreEqual( 1, camera.EntryCount( 6 ), "E" );

		Assert.AreEqual( 0, camera.EntryCount( 1 ), "D - which it does not ship" );
		Assert.AreEqual( 0, camera.EntryCount( 3 ), "L - which it does not ship" );
		Assert.AreEqual( 4, camera.Roles, "roles holding anything at all" );
	}

	/// <summary>
	/// <b>The numbered run wins, and the bare file beside it is never reached.</b> The engine tries
	/// <c>&lt;stem&gt;&lt;letter&gt;&lt;n&gt;.md2</c> first and only falls back to the unnumbered name
	/// when that run began with nothing.
	///
	/// <para>
	/// <c>mamfount</c> is the one archive in the whole game that separates the two readings: it ships
	/// <c>mamfountm.md2</c> <b>and</b> <c>mamfountm1.md2</c> and <c>mamfountm2.md2</c>. A loader that
	/// listed the archive would find three entries where the engine finds two, and every
	/// <c>&lt;parameter&gt;</c> index into that role would be off by one from there on. 306 archives
	/// cannot tell the two apart; this one can.
	/// </para>
	/// </summary>
	[TestMethod]
	public void ANumberedRunWinsAndTheBareFileBesideItIsNeverReached()
	{
		var mamfount = Load( "levels/jungle/features/mamfount", "mamfount" );

		Assert.AreEqual( 2, mamfount.EntryCount( 5 ),
			"the two numbered clips, and not the bare one sitting beside them" );
	}

	/// <summary>
	/// An item with no numbered clips takes the bare file, and that role then holds exactly one entry -
	/// the Round Fountain, whose only animation is <c>fountainm.md2</c> with no number on it.
	/// </summary>
	[TestMethod]
	public void AnItemWithNoNumberedClipsTakesTheBareFile()
	{
		var fountain = Load( "levels/jungle/features/fountain", "fountain" );

		Assert.AreEqual( 1, fountain.EntryCount( 5 ), "the bare fountainm.md2" );
		Assert.AreEqual( 1, fountain.EntryCount( 0 ), "and its construction clip" );
	}

	/// <summary>
	/// <b>A clip carrying nothing but positions and visibility still has a length, and a long one.</b>
	/// <see cref="AnimationFile.TryLoad"/> turns those down - it answers "is there an animation here worth
	/// playing" - and 114 of the game's clips are exactly that shape. The ferries are the extreme case:
	/// their first clip declares 600 frames, twenty seconds, and a loader using the fussy route would
	/// have told their script 300 milliseconds instead.
	/// </summary>
	[TestMethod]
	public void AClipWithNoMorphOrRotationStillHasItsDeclaredLength()
	{
		var ferry = Load( "levels/space/features/ferry", "ferry" );

		Assert.AreEqual( 3, ferry.EntryCount( 5 ), "all three of the ferry's clips" );

		// 600 frames at the engine's 1000/30 - see RideAnimations.DurationMilliseconds.
		Assert.AreEqual( 20000, ferry.DurationMilliseconds( 5, 0 ), "twenty seconds, declared in the file" );

		Assert.IsTrue( ferry.DurationMilliseconds( 5, 1 ) > 0, "and the others are not nought either" );
		Assert.IsTrue( ferry.DurationMilliseconds( 5, 2 ) > 0 );
	}

	/// <summary>
	/// A role an archive does not carry is simply empty, which is ordinary data rather than a fault: eight
	/// of the role references the shipped scripts make name a role their own archive has no file for.
	/// </summary>
	[TestMethod]
	public void ARoleAnArchiveDoesNotCarryIsEmpty()
	{
		var spawheel = Load( "levels/space/rides/spawheel", "spawheel" );

		Assert.AreEqual( 0, spawheel.EntryCount( 7 ), "U, which its script names and it does not ship" );
		Assert.IsTrue( spawheel.EntryCount( 5 ) > 0, "but it does ship M, so this is not a broken path" );

		var scentro = Load( "levels/space/rides/scentro", "scentro" );

		Assert.AreEqual( 0, scentro.EntryCount( 11 ), "O, which its script names and it does not ship" );
		Assert.IsTrue( scentro.EntryCount( 0 ) > 0, "but it does ship C" );
	}

	/// <summary>
	/// Asking for something outside a role, or outside the twelve, answers nought rather than throwing -
	/// the bounds the engine tests before it indexes anything.
	/// </summary>
	[TestMethod]
	public void AskingOutsideARoleAnswersNought()
	{
		var camera = Load( "levels/jungle/features/camera", "camera" );

		Assert.AreEqual( 0, camera.DurationMilliseconds( 0, 1 ), "one entry past the only one there is" );
		Assert.AreEqual( 0, camera.DurationMilliseconds( 0, -1 ) );
		Assert.AreEqual( 0, camera.DurationMilliseconds( RideAnimations.NoRole, 0 ), "the sentinel" );
		Assert.AreEqual( 0, camera.DurationMilliseconds( 99, 0 ) );
		Assert.AreEqual( 0, camera.EntryCount( -1 ) );
	}

	/// <summary>
	/// An item shipping nothing at all is not an error. The drinks shop is one: no clips, and a script
	/// that names no animation either.
	/// </summary>
	[TestMethod]
	public void AnItemCanShipNoAnimationsAtAll()
	{
		var none = RideAnimations.None( "nothing" );

		Assert.AreEqual( 0, none.Loaded );
		Assert.AreEqual( 0, none.Roles );

		for ( int role = 0; role < RideAnimations.RoleCount; ++role )
			Assert.AreEqual( 0, none.EntryCount( role ), $"role {role}" );
	}
}
