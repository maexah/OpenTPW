// cliptracks (Q554): every shipped clip's position tracks against its model, or one clip's keys.
//   dotnet run --project tools/harness/dotnet/cliptracks -c Release -- <data dir>
//     the census: tracks, first keys past nought, last keys before the declared span, additive (header 0x4), nodes under a
//     moved node, clips with a route too; lists late.txt, past.txt, offrest.txt (first point off the rest), nested.txt,
//     adds.txt in the working folder. A clip's model is the longest other member of its wad whose name begins the clip's.
//   ... -- <data dir> <clip path in the tree>   one clip's declared span and each position track's keys and samples
using OpenTPW;
var dataDir = args[0];
if ( args.Length > 1 ) { var d = new BaseFileSystem( dataDir ); d.RegisterArchiveHandler<WadArchive>( ".wad" ); using var cs = d.OpenRead( args[1] )!; var c = new AnimationFile( cs ); Console.WriteLine( $"declared {c.DeclaredFirstFrame}..{c.DeclaredLastFrame} fps {AnimationFile.FramesPerSecond}" ); foreach ( var t in c.PositionTracks ) { Console.WriteLine( $"node {t.TargetIndex} {(t.IsBezier ? "bez" : "lin")} keys {string.Join(",", t.FrameIndices)}" ); foreach ( var f in new[]{0,50,100,150,200,250,300,350,400,450,500} ) Console.Write( $" f{f}:{t.Sample(f).X:F0},{t.Sample(f).Y:F0},{t.Sample(f).Z:F0}" ); Console.WriteLine(); } return; }
var data = new BaseFileSystem( dataDir );
data.RegisterArchiveHandler<WadArchive>( ".wad" );
int addRot = 0, withPath = 0, threw = 0, invalid = 0, clips = 0, clipsWithPos = 0, tracks = 0, firstAfter0 = 0, lastBeforeEnd = 0, lastBeforeEndBez = 0, additive = 0, firstMatchesRest = 0, firstOffRest = 0, nestedMoved = 0, alsoRot = 0, notMesh = 0, unreadBase = 0, keyFrameBeyondEnd = 0;
var offRest = new List<string>(); var late = new List<string>(); var past = new List<string>(); var nested = new List<string>(); var adds = new HashSet<string>();
foreach ( var wad in Directory.GetFiles( dataDir, "*.wad", SearchOption.AllDirectories ) )
{
	var dir = Path.GetRelativePath( dataDir, Path.ChangeExtension( wad, null ) ).Replace( '\\', '/' );
	var members = data.GetFiles( dir ).Select( Path.GetFileName ).Where( n => n!.EndsWith( ".md2", StringComparison.OrdinalIgnoreCase ) ).ToList();
	foreach ( var member in members )
	{
		AnimationFile clip;
		try { using var s = data.OpenRead( $"{dir}/{member}" ); if ( s == null ) continue; clip = new AnimationFile( s ); } catch { ++threw; continue; }
		// IsValid is false on a clip with no rotation, morph or UV track, which RideAnimations still loads and poses: the
		// eight position-only clips (looM1/2, bugstvE/S, phantomS/E, tvsime/s) are counted here (review of run 6).
		if ( !clip.IsValid ) { ++invalid; if ( clip.PositionTracks.Count == 0 ) continue; } else ++clips;
		if ( clip.PositionTracks.Count == 0 ) continue;
		++clipsWithPos;
		// base: the longest other member whose name (less .md2) starts this clip's name and reads as a model
		ModelFile? model = null;
		foreach ( var cand in members.Where( n => !string.Equals( n, member, StringComparison.OrdinalIgnoreCase ) && member!.StartsWith( Path.GetFileNameWithoutExtension( n )!, StringComparison.OrdinalIgnoreCase ) ).OrderByDescending( n => n!.Length ) )
		{
			try { using var s = data.OpenRead( $"{dir}/{cand}" ); var m = new ModelFile( s! ); if ( m.IsAnimation || m.Nodes.Count == 0 ) continue; model = m; break; } catch { }
		}
		if ( model == null ) ++unreadBase;
		bool add = model != null && (model.HeaderFlags & 4) != 0;
		var movers = new HashSet<int>( clip.PositionTracks.Select( t => t.TargetIndex ).Concat( clip.RotationTracks.Select( t => t.TargetIndex ) ).Concat( clip.PathTracks.Select( t => t.TargetIndex ) ) );
		if ( clip.PathTracks.Count > 0 ) { ++withPath; Console.WriteLine( $"path+pos {dir}/{member}: path nodes {string.Join(",", clip.PathTracks.Select(t=>t.TargetIndex))} pos nodes {string.Join(",", clip.PositionTracks.Select(t=>t.TargetIndex))}" ); }
		if ( model != null && (model.HeaderFlags & 4) != 0 ) addRot += clip.RotationTracks.Count;
		foreach ( var t in clip.PositionTracks )
		{
			++tracks;
			if ( add ) { ++additive; adds.Add( $"{dir}/{member}" ); }
			var name = $"{dir}/{member} node {t.TargetIndex}";
			if ( t.FrameIndices[0] > 0 ) { ++firstAfter0; if ( late.Count < 400 ) late.Add( $"{name} first {t.FrameIndices[0]} decl {clip.DeclaredFirstFrame}..{clip.DeclaredLastFrame}" ); }
			if ( t.FrameIndices[^1] < clip.DeclaredLastFrame ) { ++lastBeforeEnd; if ( t.IsBezier ) ++lastBeforeEndBez; if ( past.Count < 2000 ) past.Add( $"{name} last {t.FrameIndices[^1]} decl {clip.DeclaredLastFrame} {(t.IsBezier ? "bez" : "lin")} keys {t.FrameIndices.Length}" ); }
			if ( t.FrameIndices[^1] > clip.DeclaredLastFrame ) ++keyFrameBeyondEnd;
			if ( clip.RotationTracks.Any( r => r.TargetIndex == t.TargetIndex ) ) ++alsoRot;
			if ( model != null )
			{
				if ( t.TargetIndex >= model.Meshes.Count ) ++notMesh;
				if ( t.TargetIndex < model.Nodes.Count )
				{
					var rest = model.Nodes[t.TargetIndex].LocalTransform.Translation;
					var d = System.Numerics.Vector3.Distance( rest, t.Points[0] );
					if ( d < 0.05f ) ++firstMatchesRest; else { ++firstOffRest; if ( offRest.Count < 5000 ) offRest.Add( $"{name} rest {rest} first {t.Points[0]} d {d:F2}{(add ? " ADD" : "")}" ); }
					for ( int p = model.Nodes[t.TargetIndex].ParentIndex, g = 0; p >= 0 && g < 200; p = model.Nodes[p].ParentIndex, ++g )
						if ( movers.Contains( p ) ) { ++nestedMoved; if ( nested.Count < 5000 ) nested.Add( $"{name} under moved {p}" ); break; }
				}
			}
		}
	}
}
Console.WriteLine( $"threw {threw}, invalid {invalid}, clips {clips}, with position {clipsWithPos}, tracks {tracks}, base unread {unreadBase}" );
Console.WriteLine( $"clips with position and path {withPath}; rotation tracks in additive clips {addRot}" );
Console.WriteLine( $"first key after 0: {firstAfter0}; last key before declared end: {lastBeforeEnd} (bezier {lastBeforeEndBez}); key past declared end: {keyFrameBeyondEnd}" );
Console.WriteLine( $"additive (&4): {additive} in {adds.Count} clips; first point = rest local {firstMatchesRest}, off {firstOffRest}; target not a mesh {notMesh}; also rotated {alsoRot}; under a moved ancestor {nestedMoved}" );
File.WriteAllLines( "late.txt", late ); File.WriteAllLines( "past.txt", past ); File.WriteAllLines( "offrest.txt", offRest ); File.WriteAllLines( "nested.txt", nested ); File.WriteAllLines( "adds.txt", adds );
