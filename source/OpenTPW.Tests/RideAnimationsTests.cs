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
/// declares, disagrees with the engine on 164 clips.
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

	/// <summary>How big a file is, or nought where there is none - <see cref="LobbyModel"/> asks this way too.</summary>
	private long SizeOf( string path )
	{
		try
		{
			return data.GetSize( path );
		}
		catch ( System.Exception )
		{
			return 0;
		}
	}

	/// <summary>
	/// <b>What a model has to bind its animation players against is every role, and the numbered run it
	/// would find by itself is not that.</b> A player has to exist for a mesh before that mesh can be
	/// posed, and an animation player names a role outright - so a model built from one role's clips
	/// cannot pose any of the others.
	///
	/// <para>
	/// The security camera is the case that shows it, and it is also the thing posing is verified
	/// against: <b>every clip it ships is a bare <c>&lt;stem&gt;&lt;letter&gt;.md2</c></b> and not one of
	/// them is numbered, so a model probing <c>cameraM1.md2</c> upwards finds nothing; what
	/// <see cref="LobbyModel"/> does on its own then falls back to the bare <c>cameram.md2</c> and reaches
	/// role M alone, while the thing in fact carries four clips across four roles.
	/// </para>
	/// </summary>
	[TestMethod]
	public void EveryRolesClipsAreWhatAModelBindsAgainstAndTheNumberedRunIsNotThem()
	{
		var camera = Load( "levels/jungle/features/camera", "camera" );

		Assert.AreEqual( camera.Loaded, camera.AllClips.Length, "as many as were loaded across the roles" );
		Assert.AreEqual( 4, camera.AllClips.Length, "C, S, M and E" );

		// Role and then entry order, so the clips can be walked alongside the roles that name them.
		Assert.AreSame( camera.Clip( 0, 0 ), camera.AllClips[0], "C is the first role that holds anything" );
		Assert.AreSame( camera.Clip( 6, 0 ), camera.AllClips[^1], "and E the last" );

		Assert.IsTrue( SizeOf( "levels/jungle/features/camera/cameram.md2" ) > 0,
			"it ships its main clip under the bare name" );

		Assert.AreEqual( 0, SizeOf( "levels/jungle/features/camera/cameram1.md2" ),
			"and ships no numbered one, so probing for a numbered run binds nothing at all" );
	}

	/// <summary>
	/// The clip a role and entry name is the same clip whose length those very arguments are answered
	/// for. They are three separate walks of the role table - <see cref="RideAnimations.Clip"/>,
	/// <see cref="RideAnimations.FramesFor"/> and <see cref="RideAnimations.DurationMilliseconds"/> - and
	/// an animation player is posed from the first while having been timed by the other two, so a
	/// disagreement between them would pose one clip for another clip's duration.
	/// </summary>
	[TestMethod]
	public void TheClipARoleNamesIsTheOneItsLengthIsAnsweredFor()
	{
		var camera = Load( "levels/jungle/features/camera", "camera" );
		var main = camera.Clip( 5, 0 );

		Assert.IsNotNull( main, "the camera ships an M clip" );

		var declared = (float)(main!.DeclaredLastFrame - main.DeclaredFirstFrame);

		Assert.AreEqual( declared, camera.FramesFor( 5, 0 ), 0.001f, "the span the player is given" );
		Assert.AreEqual( (int)(declared * AnimationFile.MillisecondsPerFrame), camera.DurationMilliseconds( 5, 0 ),
			"and the milliseconds the script is told" );

		Assert.IsNull( camera.Clip( 5, 1 ), "there is no second entry of that role" );
		Assert.IsNull( camera.Clip( 1, 0 ), "nor any D role at all" );
		Assert.IsNull( camera.Clip( RideAnimations.NoRole, 0 ), "nor anything behind the sentinel" );
	}

	/// <summary>
	/// <b>The security camera moves by morphing a mesh, and not by turning one.</b> It runs a cycle for
	/// ever with no peeps and no ride state, which is what makes it the thing posing is verified
	/// against - so what it is actually driven by had to be found out rather than assumed. Its main clip
	/// carries one morph track and nothing else whatever: no rotation, no position, no UV, no visibility.
	///
	/// <para>
	/// That settles a scope question rather than being a curiosity. Posing applies rotation, morph, UV and
	/// visibility and deliberately does <b>not</b> apply position, because a position key is parent-local
	/// exactly as a rotation key is and <see cref="LobbyModel"/> composes no per-mesh node tree to put one
	/// back into. The camera carries no position track at all, so that gap provably cannot reach it.
	/// </para>
	///
	/// <para>
	/// <b>The last assertion is the one that decides whether it can move at all.</b> A model binds a morph
	/// animator only where the track's channel count is its mesh's vertex count plus two. The engine's own
	/// rule is recorded as plus two only where the track descriptor's bit <c>0x2</c> is set and plus one
	/// otherwise, where ours adds two unconditionally - and that difference has never been measured. If it
	/// bit here no animator would be bound, and a camera standing still would look exactly like posing
	/// being broken rather than like one clip being read a channel short.
	/// </para>
	/// </summary>
	[TestMethod]
	public void TheSecurityCameraMovesByMorphingOneMesh()
	{
		var camera = Load( "levels/jungle/features/camera", "camera" );
		var main = camera.Clip( 5, 0 )!;

		var carried = $"rotation {main.RotationTracks.Count}, position {main.PositionTracks.Count}, " +
			$"morph {main.MorphTracks.Count}, UV {main.UvTracks.Count}, visibility {main.VisibilityTracks.Count}";

		Assert.AreEqual( 1, main.MorphTracks.Count, $"one mesh morphs - it carries {carried}" );
		Assert.AreEqual( 0, main.RotationTracks.Count, $"and nothing turns - it carries {carried}" );
		Assert.AreEqual( 0, main.PositionTracks.Count,
			$"and nothing slides, so the tracks this branch does not pose are none of its business - {carried}" );

		var model = new ModelFile( new System.IO.MemoryStream(
			data.ReadAllBytes( "levels/jungle/features/camera/camera.md2" ) ) );

		var track = main.MorphTracks[0];

		Assert.IsTrue( track.TargetIndex >= 0 && track.TargetIndex < model.Meshes.Count,
			$"its morph names mesh {track.TargetIndex}, of {model.Meshes.Count}" );

		// Cast because a mesh's vertex count is unsigned where a channel count is not: this assertion boxes
		// both, and a boxed uint is never equal to a boxed int however equal the two numbers are. The gate
		// in LobbyModel.BindVertexAnimations compares them numerically and is not affected.
		Assert.AreEqual( (int)model.Meshes[track.TargetIndex].VertexCount + 2, track.ChannelCount,
			"an animator is bound only where these agree" );
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
	/// <c>&lt;parameter&gt;</c> index into that role would be off by one from there on. Of the 306
	/// level archives, this is the only one that can tell the two apart.
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
	/// playing" - and it rejects 127 of the game's clips that declare a span: 89 with no tracks at all,
	/// and 38 with no rotation, UV or readable morph track. The ferries are the extreme case:
	/// their first clip declares 600 frames, twenty seconds, and a loader using the fussy route would
	/// have told their script 700 milliseconds instead, the flat second a missing role answers less 300.
	/// </summary>
	[TestMethod]
	public void AClipWithNoMorphOrRotationStillHasItsDeclaredLength()
	{
		var ferry = Load( "levels/space/features/ferry", "ferry" );

		Assert.AreEqual( 3, ferry.EntryCount( 5 ), "all three of the ferry's clips" );

		// 600 frames times the engine's own float, truncated - a millisecond under twenty seconds. See
		// AnimationFile.MillisecondsPerFrame for why that is not 600 * 1000 / 30.
		Assert.AreEqual( 19999, ferry.DurationMilliseconds( 5, 0 ), "twenty seconds bar a millisecond, declared in the file" );

		Assert.IsTrue( ferry.DurationMilliseconds( 5, 1 ) > 0, "and the others are not nought either" );
		Assert.IsTrue( ferry.DurationMilliseconds( 5, 2 ) > 0 );
	}

	/// <summary>
	/// <b>A length is truncated from the engine's own float, not divided by thirty.</b> The engine
	/// multiplies the declared span by the 32-bit float at <c>0x006fec08</c> - 33.33333206176758, not the
	/// exact 1000/30 - and converts with <c>__ftol</c>, which sets rounding toward zero before it stores.
	/// So wherever the span is a multiple of three the product lands just under a whole millisecond and
	/// the answer is one less than dividing would give.
	///
	/// <para>
	/// It is not a rounding curiosity: it moves <b>293 of the 1,237 clips under levels/ that declare a
	/// span</b>, and every one of those millisecond errors would have gone into a script's own deadline
	/// through <c>TRIGANIM</c> and <c>WAITANIM</c>.
	/// </para>
	/// </summary>
	[TestMethod]
	public void ALengthIsTruncatedFromTheEnginesFloatRatherThanDivided()
	{
		var ferry = Load( "levels/space/features/ferry", "ferry" );

		Assert.AreEqual( 19999, ferry.DurationMilliseconds( 5, 0 ), "600 frames, as the engine counts them" );

		// What dividing would have answered for the very same clip - kept here so the difference is
		// stated rather than implied, and so the constant cannot quietly go back to 1000/30.
		Assert.AreEqual( 20000, 600 * 1000 / (int)AnimationFile.FramesPerSecond, "what dividing gives" );

		Assert.AreEqual( 19999, (int)(600 * AnimationFile.MillisecondsPerFrame), "and what the engine's float gives" );

		// A span that is not a multiple of three lands in the same place either way, which is why this
		// went unnoticed: the fountain's fifty frames agree exactly.
		var fountain = Load( "levels/jungle/features/fountain", "fountain" );

		Assert.AreEqual( 1666, fountain.DurationMilliseconds( 5, 0 ), "fifty frames, where the two readings agree" );
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
	/// An item shipping nothing at all is not an error. No shipped shop is one - the Drinks Shop ships a
	/// C and an M clip - so this builds one with <see cref="RideAnimations.None"/>.
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
