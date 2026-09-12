using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// How the advisor's blink is written down, and the asymmetry in it that can leave his eyes shut.
///
/// A clip's visibility keys are state that outlives the clip: they switch a mesh on or off from a
/// frame onwards and leave it that way. His eyelids carry a key at frame 0, so entering any clip
/// re-establishes them. His eyes do not, so entering a clip leaves them however the last one ended.
/// A line cut off inside a blink therefore strands them hidden, and the next line inherits that
/// until its own first blink - which is what <see cref="AdvisorModel.OpenEyes"/> exists to prevent.
///
/// These read real game files and are skipped where there is no installation - see
/// <see cref="GameData"/>.
/// </summary>
[TestClass]
public class AdvisorBlinkTests
{
	/// <summary>His model and his clips, exactly as <see cref="AdvisorModel"/> names them.</summary>
	private const string ModelPath = "global/advisor/Advisor.md2";

	private const int Clips = 15;

	private BaseFileSystem data = null!;

	[TestInitialize]
	public void MountTheGame() => data = GameData.Required();

	/// <summary>
	/// Read through the file system this test mounted rather than the global one, which belongs to a
	/// running game and which no test should need to have been set.
	/// </summary>
	private ModelFile ReadModel() => new( new MemoryStream( data.ReadAllBytes( ModelPath ) ) );

	private AnimationFile ReadClip( int clip )
		=> new( new MemoryStream( data.ReadAllBytes( $"global/advisor/Advisorm{clip}.MD2" ) ) );

	/// <summary>By name rather than by index, so the test says which mesh it means.</summary>
	private static int MeshNamed( ModelFile model, string name )
	{
		for ( int i = 0; i < model.Meshes.Count; ++i )
		{
			if ( string.Equals( model.Meshes[i].Name.TrimEnd( '\0' ).Trim(), name, StringComparison.OrdinalIgnoreCase ) )
				return i;
		}

		return -1;
	}

	private static AnimationFile.VisibilityTrack? TrackFor( AnimationFile clip, int mesh )
		=> clip.VisibilityTracks.FirstOrDefault( track => track.TargetIndex == mesh );

	/// <summary>
	/// The asymmetry itself. Clip 3 blinks at frames 80 to 83: its eyelid track reads [0, 80, -84]
	/// and its eye track [-80, 84]. At frame 0 the eyelid answers "hidden" and the eye answers
	/// nothing at all, which is the whole of the bug.
	/// </summary>
	[TestMethod]
	public void AnEyelidBaselinesItselfAndAnEyeDoesNot()
	{
		var model = ReadModel();

		var eye = MeshNamed( model, "Right Eye" );
		var eyelid = MeshNamed( model, "ShutEye R" );

		Assert.AreNotEqual( -1, eye, "no mesh called 'Right Eye'" );
		Assert.AreNotEqual( -1, eyelid, "no mesh called 'ShutEye R'" );

		var clip = ReadClip( 3 );

		var eyeTrack = TrackFor( clip, eye );
		var eyelidTrack = TrackFor( clip, eyelid );

		Assert.IsNotNull( eyeTrack, "clip 3 should blink his right eye" );
		Assert.IsNotNull( eyelidTrack, "clip 3 should show his right eyelid" );

		Assert.AreEqual( false, eyelidTrack!.VisibleAt( 0 ),
			$"the eyelid should be put away at frame 0, from [{string.Join( ",", eyelidTrack.Entries )}]" );

		Assert.IsNull( eyeTrack!.VisibleAt( 0 ),
			$"the eye has no key at frame 0, so it keeps whatever it had, from [{string.Join( ",", eyeTrack.Entries )}]" );
	}

	/// <summary>
	/// Why this only ever bites a line that was interrupted: every clip that blinks ends with his
	/// eyes showing again, so a clip allowed to play out always hands the next one a working face.
	/// </summary>
	[TestMethod]
	public void EveryClipThatBlinksEndsWithHisEyesShowing()
	{
		var model = ReadModel();
		var eye = MeshNamed( model, "Right Eye" );

		Assert.AreNotEqual( -1, eye, "no mesh called 'Right Eye'" );

		var blinked = 0;

		for ( int number = 1; number <= Clips; ++number )
		{
			var clip = ReadClip( number );
			var track = TrackFor( clip, eye );

			if ( track == null )
				continue;

			++blinked;

			Assert.AreEqual( true, track.VisibleAt( clip.LastFrame ),
				$"clip {number} leaves his eye hidden at its last frame ({clip.LastFrame}), "
				+ $"from [{string.Join( ",", track.Entries )}]" );
		}

		Assert.IsTrue( blinked >= 10, $"only {blinked} of {Clips} clips blink - the data has moved" );
	}

	/// <summary>
	/// The eyelid's frame-0 key is not a quirk of one clip: every clip carrying eyelid keys opens
	/// with one, which is why the eyelids alone come back by themselves.
	/// </summary>
	[TestMethod]
	public void EveryEyelidTrackOpensWithAFrameZeroKey()
	{
		var model = ReadModel();
		var eyelid = MeshNamed( model, "ShutEye R" );

		Assert.AreNotEqual( -1, eyelid, "no mesh called 'ShutEye R'" );

		var found = 0;

		for ( int number = 1; number <= Clips; ++number )
		{
			var track = TrackFor( ReadClip( number ), eyelid );

			if ( track == null )
				continue;

			++found;

			Assert.AreEqual( false, track.VisibleAt( 0 ),
				$"clip {number}'s eyelid track does not put it away at frame 0, "
				+ $"from [{string.Join( ",", track.Entries )}]" );
		}

		Assert.IsTrue( found >= 10, $"only {found} of {Clips} clips move the eyelid - the data has moved" );
	}
}
