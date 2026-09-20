using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using System.Numerics;

namespace OpenTPW.Tests;

/// <summary>
/// The clips a model loads for itself, read out of real archives. These read real game files and are
/// skipped where there is no installation - see <see cref="GameData"/>.
///
/// <para>
/// <b>A model's M role is shipped one of two ways and only one of them was ever read.</b>
/// <c>LobbyModel.LoadAnimations</c> probed <c>{stem}M1.md2</c> upwards and stopped at the first miss, so a
/// model shipping a single unnumbered <c>{stem}M.md2</c> animated nothing at all. 197 of the game's 445
/// base models are exactly that shape. The engine is not: <c>FUN_00461f10</c> carries both filename
/// formats, <c>'%s%s%c%d.md2'</c> at <c>0x004623b3</c> and <c>'%s%s%c.md2'</c> at <c>0x004623df</c>.
/// </para>
///
/// <para>
/// <b>These assert the loader, not the archive.</b> Asserting that <c>basem.md2</c> exists and reads would
/// pass just as well with the fallback deleted, which is the shape of test that proves nothing - so every
/// one of these goes through <c>LoadAnimations</c> itself.
/// </para>
/// </summary>
[TestClass]
public class LobbyModelAnimationTests
{
	private BaseFileSystem data = null!;

	/// <summary>
	/// The global file system as well as the local one: <c>LoadAnimations</c> reaches the archives through
	/// <see cref="AnimationFile.TryLoad"/>, which opens against the global. Four other test classes mount
	/// it the same way.
	/// </summary>
	[TestInitialize]
	public void MountTheGame() => FileSystem = data = GameData.Required();

	/// <summary>
	/// <b>Lost Kingdom's terrain ships an animation and it was never loaded.</b> <c>terrain.wad</c> holds
	/// <c>base.md2</c> and <c>basem.md2</c> and no numbered clip at all, so the probe this class is named
	/// for found nothing and the park's water stood still.
	///
	/// <para>
	/// The engine reaches it: <c>FUN_004504c0</c> builds <c>'%s\Terrain'</c> and asks the role loader for
	/// the stem <b><c>"Base"</c></b> (<c>0x0074cf58</c>), falling back to <c>"TestBase"</c>. So this is a
	/// clip the original plays and we did not.
	/// </para>
	///
	/// <para>
	/// <b>The last assertion is the one that decides whether it can be seen.</b> A UV track only binds an
	/// animator where its target is a real mesh of the model, and the terrain has 272 of them; a track
	/// naming something past the end would leave the water as still as no clip at all.
	/// </para>
	/// </summary>
	[TestMethod]
	public void TheTerrainTakesTheBareClipItShipsAndScrollsItsWater()
	{
		var clips = LobbyModel.LoadAnimations( "levels/jungle/terrain/base.MD2" );

		Assert.AreEqual( 1, clips.Length, "the bare basem.md2, which no numbered run ever reaches" );

		var terrain = clips[0];
		var carried = $"rotation {terrain.RotationTracks.Count}, position {terrain.PositionTracks.Count}, " +
			$"morph {terrain.MorphTracks.Count}, UV {terrain.UvTracks.Count}, " +
			$"visibility {terrain.VisibilityTracks.Count}";

		Assert.AreEqual( 11, terrain.UvTracks.Count, $"eleven surfaces scroll - it carries {carried}" );
		Assert.AreEqual( 0, terrain.MorphTracks.Count, $"and nothing morphs - {carried}" );
		Assert.AreEqual( 0, terrain.RotationTracks.Count, $"and nothing turns - {carried}" );

		Assert.AreEqual( 0, terrain.DeclaredFirstFrame, "the span the file declares" );
		Assert.AreEqual( 100, terrain.DeclaredLastFrame );

		var model = new ModelFile( "levels/jungle/terrain/base.MD2" );

		foreach ( var track in terrain.UvTracks )
		{
			Assert.IsTrue( track.TargetIndex >= 0 && track.TargetIndex < model.Meshes.Count,
				$"its UV names mesh {track.TargetIndex}, of {model.Meshes.Count}" );
		}
	}

	/// <summary>
	/// <b>The numbered run still wins, and the bare file beside it is still not taken as well.</b> The
	/// fallback is reached only where the numbered run came back with nothing, which is the engine's own
	/// condition - and <c>mamfount</c> is the one archive in the game that can tell the two readings apart,
	/// shipping <c>mamfountm.md2</c> <b>and</b> <c>mamfountm1.md2</c> and <c>mamfountm2.md2</c>.
	///
	/// <para>
	/// Without that condition this would answer three, and the model would hold a clip the original never
	/// plays. This is the test that fails if the fallback is made unconditional, so it is what keeps the
	/// other one honest.
	/// </para>
	/// </summary>
	[TestMethod]
	public void ANumberedRunWinsAndTheBareFileBesideItIsNotTakenAsWell()
	{
		var clips = LobbyModel.LoadAnimations( "levels/jungle/features/mamfount/mamfount.MD2" );

		Assert.AreEqual( 2, clips.Length, "the two numbered clips, and not the bare one sitting beside them" );
	}

	/// <summary>
	/// An item shipping only the bare file takes it, and this is the case that is not the terrain - the
	/// security camera's whole animation is <c>cameram.md2</c> with no number on it.
	/// </summary>
	[TestMethod]
	public void AnItemShippingOnlyTheBareClipTakesIt()
	{
		var clips = LobbyModel.LoadAnimations( "levels/jungle/features/camera/camera.MD2" );

		Assert.AreEqual( 1, clips.Length, "the bare cameram.md2" );
		Assert.AreEqual( 1, clips[0].MorphTracks.Count, "the camera moves by morphing one mesh" );
	}

	/// <summary>
	/// <b>And the honest limit of all this: a bare clip that carries no track at all still animates
	/// nothing.</b> Lost Kingdom's queue is the case - <c>queendm.md2</c> exists, and is empty.
	///
	/// <para>
	/// 37 of the 197 bare-only clips are this shape, the same as the two <c>lights.RSE</c> clips already on
	/// record that declare ten frames and drive nothing. <see cref="AnimationFile.TryLoad"/> answers "is
	/// there an animation here worth playing" and rightly says no, so the queue is untouched by this and
	/// should not be expected to move. Space ships real queue clips; the jungle does not.
	/// </para>
	/// </summary>
	[TestMethod]
	public void AnEmptyBareClipIsStillNothingToPlay()
	{
		Assert.IsTrue( data.GetSize( "levels/jungle/queue/queendm.md2" ) > 0,
			"the file is there to be found" );

		Assert.AreEqual( 0, LobbyModel.LoadAnimations( "levels/jungle/queue/queend.MD2" ).Length,
			"but it carries no track of any kind, so there is nothing to play" );
	}

	/// <summary>The four lobby gates, by the prefix each theme's models are named with.</summary>
	private static readonly string[] Gates = ["Jun", "Fan", "Hal", "Spa"];

	/// <summary>The five kinds of track a clip can carry, for a message that has to say what it found.</summary>
	private static string Shape( AnimationFile clip )
		=> $"rotation {clip.RotationTracks.Count}, position {clip.PositionTracks.Count}, " +
			$"morph {clip.MorphTracks.Count}, UV {clip.UvTracks.Count}, " +
			$"visibility {clip.VisibilityTracks.Count}, frames {clip.FirstFrame}-{clip.LastFrame}";

	/// <summary>
	/// <b>Every one of the four gates ships an opening clip that moves something</b> - which is what lets
	/// <see cref="LobbyGate"/> idle on clip 0 and play through it as a park is entered, rather than
	/// looping the pair for ever.
	///
	/// <para>
	/// <b>Across all four, and that is the point rather than thoroughness for its own sake.</b> The
	/// jungle's gate is the model this code was written against and is the one that cannot catch a
	/// mistake: it is two hinged doors, so it makes "a gate is a rotation animation" look like a rule.
	/// It is not one. Fantasy and space have no gateway on their island at all, so their gate model is
	/// the whole structure - a worm, a hatch - and it need not turn to open.
	/// </para>
	/// </summary>
	[TestMethod]
	public void EveryIslandGateShipsAnOpeningClipThatMovesSomething()
	{
		foreach ( var prefix in Gates )
		{
			var clips = LobbyModel.LoadAnimations( $"lobby/terrain/{prefix}_gate.md2" );

			Assert.IsTrue( clips.Length >= 2,
				$"{prefix}_gate is authored as a pair - M1 opens, M2 shuts - and ships {clips.Length}" );

			var opening = clips[0];
			var moves = opening.RotationTracks.Count + opening.MorphTracks.Count + opening.UvTracks.Count;

			Assert.AreNotEqual( 0, moves,
				$"{prefix}_gateM1 has to move something to open the gate - it carries {Shape( opening )}" );

			// Where it does turn, "opens" has to be a movement rather than a name: the doors must end
			// somewhere other than where they start.
			if ( opening.RotationTracks.Count > 0 )
			{
				Assert.IsTrue( opening.RotationTracks.Any( track => track.Rotations.Length >= 2
						&& MathF.Abs( Quaternion.Dot( track.Rotations[0], track.Rotations[^1] ) ) < 0.9999f ),
					$"{prefix}_gateM1 should leave its doors somewhere other than shut - {Shape( opening )}" );
			}
		}
	}

	/// <summary>
	/// <b>A gate is played for as long as it actually moves, and the two are not the same number.</b>
	/// A park entry waits for its gate, so the wait has to come from the movement and not from the file:
	/// hallow's M1 shuts the gate again and swings it open the other way over another nine seconds, and
	/// playing to <see cref="AnimationFile.LastFrame"/> would hold the player in the lobby watching a
	/// gate that had already stopped.
	///
	/// <para>
	/// This goes through <c>LobbyGate.SwingSeconds</c>, which is what the gate itself takes its length
	/// from, rather than asserting the files - the same reason the tests above go through
	/// <c>LoadAnimations</c>. The rotation bound is asserted only for the gates that rotate, because
	/// <c>MeshRotator.MotionEnd</c> walks rotation tracks and answers <c>LastFrame</c> where there are
	/// none, which is a measurement of nothing.
	/// </para>
	/// </summary>
	[TestMethod]
	public void EveryGateIsPlayedForAsLongAsItMoves()
	{
		// Measured off the shipped archives, gate by gate: rotation tracks, morph tracks, the frame the
		// clip ends on, and the frame its MOVEMENT ends on.
		(string Gate, int Rotation, int Morph, int LastFrame, int MotionEnd)[] measured =
		[
			("Jun", 2, 0,  60,  57),
			("Hal", 2, 0, 600,  60),
			("Spa", 1, 0, 100,  60),
			("Fan", 0, 2, 100, 100)
		];

		foreach ( var (gate, rotation, morph, lastFrame, motionEnd) in measured )
		{
			var opening = LobbyModel.LoadAnimations( $"lobby/terrain/{gate}_gate.md2" )[0];

			Assert.AreEqual( rotation, opening.RotationTracks.Count, $"{gate}_gateM1 - {Shape( opening )}" );
			Assert.AreEqual( morph, opening.MorphTracks.Count, $"{gate}_gateM1 - {Shape( opening )}" );
			Assert.AreEqual( lastFrame, opening.LastFrame, $"{gate}_gateM1 - {Shape( opening )}" );

			// Hallow is the one that matters: its clip runs ten times past the point its rails stop,
			// so a gate played to LastFrame would hold the player in the lobby for twenty seconds.
			// Fantasy's 100 is MotionEnd answering LastFrame because it walks rotation tracks and
			// there are none - a measurement of nothing, which is why the rule below does not use it.
			Assert.AreEqual( motionEnd, MeshRotator.MotionEnd( opening, opening ),
				$"{gate}_gateM1's movement ends here, in a clip running to {opening.LastFrame}" );
		}
	}

	/// <summary>
	/// <b>The rule <see cref="LobbyGate"/> takes its opening length from</b>, which is the part the
	/// measurements above only justify. A turning gate is played for as long as it turns; one that
	/// turns nothing but morphs - fantasy's worm - is played over its clip's own span; and a clip that
	/// moves nothing this model can play is no opening at all, so the park stays reachable.
	///
	/// <para>
	/// <b>Written because the shape tests are hollow against the fix.</b> Taking the length from the
	/// rotator alone left fantasy's gate with nought and let its park load with no animation, and every
	/// assertion about the files stays green through that mistake. This one does not.
	/// </para>
	/// </summary>
	[TestMethod]
	public void AGateThatTurnsNothingIsStillPlayedForAsLongAsItMorphs()
	{
		var fantasy = LobbyModel.LoadAnimations( "lobby/terrain/Fan_gate.md2" )[0];
		var jungle = LobbyModel.LoadAnimations( "lobby/terrain/Jun_gate.md2" )[0];

		Assert.AreEqual( 100f / AnimationFile.FramesPerSecond,
			LobbyGate.SwingSeconds( null, morphs: true, fantasy ), 0.001f,
			"fantasy turns nothing, so its worm is played over the span its clip declares" );

		Assert.AreEqual( 57f / AnimationFile.FramesPerSecond,
			LobbyGate.SwingSeconds( 57f / AnimationFile.FramesPerSecond, morphs: false, jungle ), 0.001f,
			"a turning gate is played for as long as it turns" );

		Assert.AreEqual( 0f, LobbyGate.SwingSeconds( null, morphs: false, jungle ), 0.001f,
			"a clip that moves nothing is no opening, and must not hold the park up" );

		Assert.AreEqual( 0f, LobbyGate.SwingSeconds( null, morphs: true, null ), 0.001f,
			"and neither is no clip at all" );
	}
}
