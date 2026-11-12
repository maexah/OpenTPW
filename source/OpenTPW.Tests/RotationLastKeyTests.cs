using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;

namespace OpenTPW.Tests;

/// <summary>
/// A rotation track's last key (Q729). The original's key search (<c>Anim_FindKey</c>, <c>0x00470b60</c>) names key 0 as
/// the upper key on a rotation track's last key (<c>0x00470bee</c>), but the blend never reads it:
/// <c>Anim_BlendRotationKeys</c> (<c>0x00474490</c>) pairs key i with key i+1 clamped to the last, so the engine holds the
/// last key, as <see cref="AnimationFile.RotationTrack.Sample"/> does (<c>docs/exe/park.md</c>, <c>FUN_00470b60</c>).
/// </summary>
[TestClass]
public class RotationLastKeyTests
{
	private static readonly Quaternion Turned = Quaternion.CreateFromAxisAngle( System.Numerics.Vector3.UnitZ, MathF.PI / 2f );

	/// <summary>
	/// Past a track's last key the sampler answers the last key, as the engine's clamped blend does (<c>0x00474490</c>): a
	/// wrapping rule gets the identity here, a blend something between.
	/// </summary>
	[TestMethod]
	public void PastTheLastKeyTheLastKeyIsHeld()
	{
		var track = new AnimationFile.RotationTrack
		{
			FrameIndices = [0, 10],
			Rotations = [Quaternion.Identity, Turned],
			CurveIds = [AnimationFile.RotationTrack.NoCurve, AnimationFile.RotationTrack.NoCurve],
		};

		foreach ( var frame in new[] { 10f, 10.5f, 11f, 30f } )
		{
			var q = track.Sample( frame );
			Assert.IsTrue( MathF.Abs( Quaternion.Dot( q, Turned ) ) > 0.99999f, $"frame {frame}: {q}, not the last key {Turned}" );
		}
	}

	/// <summary>
	/// The corpus: every <c>.md2</c> the game ships (each wad opened and walked into its folders, then the loose files, over
	/// the game's folder), 2,129, of which 1,278 declare an end. Their 3,039 rotation tracks all have two keys or more; 2,601 end on their
	/// clip's declared end, 438 past it, and none before it. So no shipped track has a stretch between its last key and its
	/// clip's declared end. The count is a fact about the data only: the engine holds the last key in any case
	/// (<c>0x00474490</c>).
	/// </summary>
	[TestMethod]
	public void NoShippedRotationTrackEndsBeforeItsClipsDeclaredEnd()
	{
		GameData.Required();

		int files = 0, declared = 0, tracks = 0, single = 0, onEnd = 0, pastEnd = 0;
		var before = new List<string>();

		foreach ( var (name, bytes) in Corpus() )
		{
			++files;
			var clip = new AnimationFile( new MemoryStream( bytes ) );
			declared += clip.DeclaredLastFrame > 0 ? 1 : 0;

			foreach ( var track in clip.RotationTracks )
			{
				++tracks;
				single += track.FrameIndices.Length < 2 ? 1 : 0;
				var last = track.FrameIndices[^1];

				if ( last == clip.DeclaredLastFrame )
					++onEnd;
				else if ( last > clip.DeclaredLastFrame )
					++pastEnd;
				else
					before.Add( $"{name} node {track.TargetIndex} last {last} declared {clip.DeclaredLastFrame}" );
			}
		}

		Console.WriteLine( $"md2 {files}, declaring an end {declared}, rotation tracks {tracks}, one key {single}, on the end {onEnd}, past it {pastEnd}, before it {before.Count}" );

		Assert.AreEqual( 2129, files, "every .md2 the game ships" );
		Assert.AreEqual( 1278, declared, "the clips that carry an animation block" );
		Assert.AreEqual( 3039, tracks );
		Assert.AreEqual( 0, single );
		Assert.AreEqual( 2601, onEnd );
		Assert.AreEqual( 438, pastEnd );
		Assert.AreEqual( 0, before.Count, string.Join( "; ", before.Take( 10 ) ) );
	}

	private static IEnumerable<(string Name, byte[] Bytes)> Corpus()
	{
		// The game's folder, not Data alone: the six meshes under USEnglish/Meshes are among the 2,129 (2,123 are under Data).
		var root = Path.GetDirectoryName( Path.TrimEndingDirectorySeparator( GameDir.Data ) )!;

		foreach ( var wad in Directory.GetFiles( root, "*.wad", SearchOption.AllDirectories ).OrderBy( path => path, StringComparer.Ordinal ) )
		{
			using var archive = new WadArchive( wad );
			var stem = Path.GetRelativePath( root, wad ).Replace( '\\', '/' );

			foreach ( var item in Walk( archive.Root, "" ) )
				yield return ($"{stem}:{item.Path}", item.File.GetData());
		}

		foreach ( var loose in Directory.GetFiles( root, "*", SearchOption.AllDirectories )
			.Where( path => path.EndsWith( ".md2", StringComparison.OrdinalIgnoreCase ) ).OrderBy( path => path, StringComparer.Ordinal ) )
			yield return (Path.GetRelativePath( root, loose ), File.ReadAllBytes( loose ));
	}

	private static IEnumerable<(string Path, ArchiveFile File)> Walk( ArchiveDirectory directory, string at )
	{
		foreach ( var item in directory.Children )
		{
			if ( item is ArchiveDirectory sub )
			{
				foreach ( var inner in Walk( sub, $"{at}{sub.Name}/" ) )
					yield return inner;
			}
			else if ( item is ArchiveFile file && file.Name.EndsWith( ".md2", StringComparison.OrdinalIgnoreCase ) )
				yield return ($"{at}{file.Name}", file);
		}
	}
}
