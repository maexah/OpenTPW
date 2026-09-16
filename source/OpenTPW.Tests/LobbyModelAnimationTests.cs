using Microsoft.VisualStudio.TestTools.UnitTesting;

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
}
