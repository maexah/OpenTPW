// cliptracks (Q554): every shipped clip's position tracks against its model, or one clip's keys.
//   dotnet run --project tools/harness/dotnet/cliptracks -c Release -- <data dir>
//     the census: tracks, first keys past nought, last keys before the declared span, additive (header 0x4), nodes under a
//     moved node, clips with a route too; lists late.txt, past.txt, offrest.txt (first point off the rest), nested.txt,
//     adds.txt in the working folder. A clip's model is the longest other member of its wad whose name begins the clip's.
//   ... -- <data dir> <clip path in the tree>   one clip's declared span and each position track's keys and samples
//   ... -- <data dir> rot   (Q729) every rotation track whose last key falls before its clip's declared end (none ship;
//     the engine holds a rotation's last key, Anim_BlendRotationKeys 0x00474490, whatever Anim_FindKey's param_5 1
//     names): the counts over every .md2 member of every wad, those whose last key turns away from key 0 (|dot| under 0.99999, or a negative dot, which the original's
//     unflipped blend takes the long way), and the list rotwrap.txt in the working folder. It opens each wad itself and
//     walks its folders, and adds the loose .md2 files, so given the game folder (not Data) it reads the whole corpus:
//     2,129 in the shipped game, 2,123 under Data and the six under USEnglish/Meshes
//   ... -- <game folder> route   (Q573) every clip with a route (a path track) over every wad: its model (the rule above,
//     within the wad's folder), each routed node and every node under it that has a lookup id, with its flags (0x100 a
//     particle's, 0x200 a sound's, 0x800 a walk's), and each ADDOBJ/EVENT of a script in the same wad whose type and node
//     are literals and whose node is found (RideNodes.EffectIndex's space) at or under a routed node; one whose type or
//     node is a variable is counted, not resolved
using OpenTPW;
var dataDir = args[0];
if ( args.Length > 1 && args[1] == "rot" ) { RotScan( dataDir ); return; }
if ( args.Length > 1 && args[1] == "route" ) { RouteScan( dataDir ); return; }
if ( args.Length > 1 ) { var d = new BaseFileSystem( dataDir ); d.RegisterArchiveHandler( ".wad", path => new WadArchive( path ) ); using var cs = d.OpenRead( args[1] )!; var c = new AnimationFile( cs ); Console.WriteLine( $"declared {c.DeclaredFirstFrame}..{c.DeclaredLastFrame} fps {AnimationFile.FramesPerSecond}" ); foreach ( var t in c.PositionTracks ) { Console.WriteLine( $"node {t.TargetIndex} {(t.IsBezier ? "bez" : "lin")} keys {string.Join(",", t.FrameIndices)}" ); foreach ( var f in new[]{0,50,100,150,200,250,300,350,400,450,500} ) Console.Write( $" f{f}:{t.Sample(f).X:F0},{t.Sample(f).Y:F0},{t.Sample(f).Z:F0}" ); Console.WriteLine(); } return; }
var data = new BaseFileSystem( dataDir );
data.RegisterArchiveHandler( ".wad", path => new WadArchive( path ) );
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

static void RotScan( string dataDir )
{
	var data = new BaseFileSystem( dataDir );
	data.RegisterArchiveHandler( ".wad", path => new WadArchive( path ) );
	int members = 0, threw = 0, withBlock = 0, clipsWithRot = 0, tracks = 0, multi = 0, before = 0, beforeTurns = 0, beforeNeg = 0, equalEnd = 0, past = 0;
	var clipsBefore = new HashSet<string>(); var clipsTurns = new HashSet<string>(); var list = new List<string>();
	// Every .md2 in the tree: each wad opened on its own and walked into its folders, then the loose files.
	var corpus = new List<(string Name, Func<byte[]> Read)>();
	foreach ( var wad in Directory.GetFiles( dataDir, "*.wad", SearchOption.AllDirectories ).OrderBy( w => w, StringComparer.Ordinal ) )
	{
		var archive = new WadArchive( wad );
		var stem = Path.GetRelativePath( dataDir, wad ).Replace( '\\', '/' );
		void Walk( ArchiveDirectory d, string at )
		{
			foreach ( var item in d.Children )
			{
				if ( item is ArchiveDirectory sub ) Walk( sub, $"{at}{sub.Name}/" );
				else if ( item is ArchiveFile f && f.Name.EndsWith( ".md2", StringComparison.OrdinalIgnoreCase ) ) corpus.Add( ($"{stem}:{at}{f.Name}", f.GetData) );
			}
		}
		Walk( archive.Root, "" );
	}
	foreach ( var loose in Directory.GetFiles( dataDir, "*", SearchOption.AllDirectories ).Where( n => n.EndsWith( ".md2", StringComparison.OrdinalIgnoreCase ) ).OrderBy( n => n, StringComparer.Ordinal ) )
		corpus.Add( (Path.GetRelativePath( dataDir, loose ), () => File.ReadAllBytes( loose )) );
	foreach ( var (name, read) in corpus )
	{
		++members;
		AnimationFile clip;
		try { using var s = new MemoryStream( read() ); clip = new AnimationFile( s ); } catch { ++threw; continue; }
		if ( clip.DeclaredLastFrame > 0 ) ++withBlock;
		if ( clip.RotationTracks.Count == 0 ) continue;
		++clipsWithRot;
		foreach ( var t in clip.RotationTracks )
		{
			++tracks;
			if ( t.FrameIndices.Length < 2 ) continue;
			++multi;
			var last = t.FrameIndices[^1];
			if ( last == clip.DeclaredLastFrame ) ++equalEnd;
			if ( last > clip.DeclaredLastFrame ) ++past;
			if ( last >= clip.DeclaredLastFrame ) continue;
			++before; clipsBefore.Add( name );
			var dot = System.Numerics.Quaternion.Dot( t.Rotations[^1], t.Rotations[0] );
			var turns = MathF.Abs( dot ) < 0.99999f || dot < 0f;
			if ( turns ) { ++beforeTurns; clipsTurns.Add( name ); if ( dot < 0f ) ++beforeNeg; }
			list.Add( $"{name} node {t.TargetIndex} keys {t.FrameIndices.Length} first {t.FrameIndices[0]} last {last} decl {clip.DeclaredFirstFrame}..{clip.DeclaredLastFrame} dot {dot:F5}{(turns ? " TURNS" : "")}" );
		}
	}
	Console.WriteLine( $"md2 members {members}, threw {threw}, with a declared end {withBlock}, clips with rotation {clipsWithRot}, rotation tracks {tracks}, of 2+ keys {multi}" );
	Console.WriteLine( $"last key = declared end {equalEnd}; past it {past}; before it {before} in {clipsBefore.Count} clips" );
	Console.WriteLine( $"of those, last key turned from key 0 {beforeTurns} in {clipsTurns.Count} clips (negative dot {beforeNeg})" );
	foreach ( var c in clipsTurns.OrderBy( c => c, StringComparer.Ordinal ) ) Console.WriteLine( $"turns: {c}" );
	File.WriteAllLines( "rotwrap.txt", list );
}

static void RouteScan( string gameDir )
{
	int wads = 0, routeClips = 0, noModel = 0, idNodes = 0, effectOps = 0, unresolved = 0, onRoute = 0, scripts = 0;
	var flagged = new Dictionary<uint, int>();
	foreach ( var wad in Directory.GetFiles( gameDir, "*.wad", SearchOption.AllDirectories ).OrderBy( w => w, StringComparer.Ordinal ) )
	{
		++wads;
		var archive = new WadArchive( wad );
		var stem = Path.GetRelativePath( gameDir, wad ).Replace( '\\', '/' );
		var md2 = new List<(string Dir, string Name, ArchiveFile File)>(); var rse = new List<(string Name, ArchiveFile File)>();
		void Walk( ArchiveDirectory d, string at )
		{
			foreach ( var item in d.Children )
			{
				if ( item is ArchiveDirectory sub ) Walk( sub, $"{at}{sub.Name}/" );
				else if ( item is ArchiveFile f && f.Name.EndsWith( ".md2", StringComparison.OrdinalIgnoreCase ) ) md2.Add( (at, f.Name, f) );
				else if ( item is ArchiveFile r && r.Name.EndsWith( ".rse", StringComparison.OrdinalIgnoreCase ) ) rse.Add( ($"{at}{r.Name}", r) );
			}
		}
		Walk( archive.Root, "" );
		var ops = new List<(string Script, int Word, Opcode Op, int Type, int Node)>();
		foreach ( var (name, file) in rse )
		{
			RideScriptFile script;
			try { using var s = new MemoryStream( file.GetData() ); script = new RideScriptFile( s ); } catch { continue; }
			++scripts;
			foreach ( var i in script.Instructions.Where( i => i.Opcode is Opcode.ADDOBJ or Opcode.EVENT ) )
			{
				++effectOps;
				if ( i.Operands.Count < 3 || i.Operands[0].Kind != RideOperandKind.Literal || i.Operands[1].Kind != RideOperandKind.Literal ) { ++unresolved; Console.WriteLine( $"variable {stem}:{name} @{i.Address} {i.Opcode}" ); continue; }
				ops.Add( (name, i.Address, i.Opcode, i.Operands[0].Value, i.Operands[1].Value) );
			}
		}
		foreach ( var (dir, name, file) in md2 )
		{
			AnimationFile clip;
			try { using var s = new MemoryStream( file.GetData() ); clip = new AnimationFile( s ); } catch { continue; }
			if ( clip.PathTracks.Count == 0 ) continue;
			++routeClips;
			ModelFile? model = null; string? modelName = null;
			foreach ( var cand in md2.Where( m => m.Dir == dir && !string.Equals( m.Name, name, StringComparison.OrdinalIgnoreCase ) && name.StartsWith( Path.GetFileNameWithoutExtension( m.Name ), StringComparison.OrdinalIgnoreCase ) ).OrderByDescending( m => m.Name.Length ) )
			{
				try { using var s = new MemoryStream( cand.File.GetData() ); var m = new ModelFile( s ); if ( m.IsAnimation || m.Nodes.Count == 0 ) continue; model = m; modelName = cand.Name; break; } catch { }
			}
			if ( model == null ) { ++noModel; Console.WriteLine( $"route {stem}:{dir}{name}: no model" ); continue; }
			var under = new bool[model.Nodes.Count];
			foreach ( var t in clip.PathTracks ) if ( t.TargetIndex >= 0 && t.TargetIndex < under.Length ) under[t.TargetIndex] = true;
			for ( var changed = true; changed; )
			{
				changed = false;
				for ( var n = 0; n < under.Length; ++n )
					if ( !under[n] && model.Nodes[n].ParentIndex is var p && p >= 0 && p < under.Length && under[p] ) { under[n] = true; changed = true; }
			}
			var tracked = string.Join( ",", clip.PathTracks.Select( t => t.TargetIndex < model.Nodes.Count ? $"{t.TargetIndex}:{model.Nodes[t.TargetIndex].Name}" : $"{t.TargetIndex}:?" ) );
			Console.WriteLine( $"route {stem}:{dir}{name} model {modelName} routed {tracked} nodes under {under.Count( u => u )}" );
			for ( var n = 0; n < under.Length; ++n )
			{
				if ( !under[n] || model.Nodes[n].Id < 0 ) continue;
				++idNodes;
				var f = model.Nodes[n].IdFlags & 0xf00;
				flagged[f] = flagged.GetValueOrDefault( f ) + 1;
				Console.WriteLine( $"  id node {n} {model.Nodes[n].Name} id {model.Nodes[n].Id} flags 0x{model.Nodes[n].IdFlags:x}" );
			}
			foreach ( var (script, word, op, type, node) in ops )
			{
				var at = model.FindNode( node, type <= 2 ? 0x100u : 0x200u );
				if ( at < 0 || !under[at] ) continue;
				++onRoute;
				Console.WriteLine( $"  ON ROUTE {script} @{word} {op} type {type} node {node} -> {at} {model.Nodes[at].Name}" );
			}
		}
	}
	Console.WriteLine( $"wads {wads}, scripts {scripts}, ADDOBJ/EVENT {effectOps} (type or node a variable {unresolved}); clips with a route {routeClips} (no model {noModel}); id'd nodes at or under a route {idNodes} ({string.Join( ", ", flagged.OrderBy( k => k.Key ).Select( k => $"0x{k.Key:x} {k.Value}" ) )}); effect starts on a route {onRoute}" );
}
