using OpenTPW;
// channelclips (Q574), READ-ONLY: every shipped model that runs more than one animation channel, in all four themes, its clips by role
// sorted as LobbyModel.Pose sorts them (TREE: NodePoser.Drives( clip, meshes ), so posed through the node tree and every mesh placed from it;
// TURN: MeshRotator.Drives and not TREE, so its meshes turned alone; NONE: neither), and the roles its scripts start on each channel
// (TRIGANIM_CH / LOOPANIM_CH name one, every other starter channel 0; a channel or role in a variable is said as such). Then each pair of
// a TURN clip and a TREE clip whose roles the scripts start on two different channels, with the order the channels are posed in
// (RideAnimations.PoseOnto walks 0 upward) and the meshes the two share: a node the TREE clip moves that is the turned mesh, above it,
// or under it. A TURN on the LATER channel overwrites what the tree placed for those meshes; on the EARLIER one the tree takes its turn up
// (MeshRotator.TakeHeld).
// -v adds each start's script word and each turned mesh's parents and what hangs under it.
// args: <data dir> [-v]   (build: dotnet build; run bin/Debug/net10.0/channelclips.dll, the repo's Debug build made first)
// The shipped game (2026-11-12): 294 models, 9 of more than one channel (or a named one), 2 with both kinds of clip (the Inca Totem,
// space's Lunar, whose clips all play on channel 0), 36 pairs on two channels, all the Totem's, 27 sharing a mesh and none crossing one.
OpenTPW.Common.GlobalNamespace.Log = new();
var data = new BaseFileSystem( args[0] );
data.RegisterArchiveHandler( ".wad", path => new WadArchive( path ) );
OpenTPW.Common.GlobalNamespace.FileSystem = data;
var verbose = args.Contains( "-v" );

// Every item: the four catalogues, then every .sam the catalogues do not hold (fixed items: gates, the End, the toilets).
var models = new SortedDictionary<string, (string Dir, string Stem, int Channels, string Source)>( StringComparer.OrdinalIgnoreCase );
foreach ( var theme in new[] { "jungle", "fantasy", "hallow", "space" } )
{
	var catalogue = new ParkItemCatalogue( theme, data, false );
	foreach ( var item in catalogue.All )
		models.TryAdd( $"{item.Directory}/{item.Stem}", (item.Directory, item.Stem, item.AnimationChannels, $"item {item.Id}") );
}
foreach ( var wad in Directory.GetFiles( args[0], "*.wad", SearchOption.AllDirectories ) )
{
	var dir = Path.GetRelativePath( args[0], Path.ChangeExtension( wad, null ) ).Replace( '\\', '/' );
	foreach ( var sam in data.GetFiles( dir ).Where( n => n.EndsWith( ".sam", StringComparison.OrdinalIgnoreCase ) ) )
	{
		var stem = Path.GetFileNameWithoutExtension( sam );
		// An Easy_ .sam is an overlay of the item's own (ParkItemCatalogue reads it over the item's), not a model.
		if ( stem.StartsWith( "Easy_", StringComparison.OrdinalIgnoreCase ) || models.ContainsKey( $"{dir}/{stem}" ) ) continue;
		int channels;
		try { using var s = data.OpenRead( $"{dir}/{stem}.sam" ); channels = new ItemDescriptionFile( s! ).NumSimultAnims; } catch { continue; }
		models[$"{dir}/{stem}"] = (dir, stem, channels, "fixed");
	}
}

int multi = 0, withBoth = 0, pairs = 0, laterTurns = 0, laterShared = 0, earlierShared = 0;
var reached = new List<string>();
var animOps = new HashSet<Opcode> { Opcode.TRIGANIM, Opcode.TRIGANIM_CH, Opcode.LOOPANIM, Opcode.LOOPANIM_CH, Opcode.TRIGWAITANIM, Opcode.WAITANIM };
foreach ( var (key, (dir, stem, declared, source)) in models )
{
	// The scripts beside the model: the item's own and any other in its folder (companions).
	var starts = new List<(string Channel, string Role, string Entry, string Where)>();
	foreach ( var rse in data.GetFiles( dir ).Where( n => n.EndsWith( ".rse", StringComparison.OrdinalIgnoreCase ) ) )
	{
		RideScriptFile file;
		try { using var s = data.OpenRead( $"{dir}/{Path.GetFileName( rse )}" ); file = new RideScriptFile( s! ); } catch { continue; }
		string Lit( RideOperand o ) => o.Kind == RideOperandKind.Variable ? $"var:{(o.Value >= 0 && o.Value < file.VariableNames.Count ? file.VariableNames[o.Value] : o.Value.ToString())}" : o.Value.ToString();
		foreach ( var ins in file.Instructions )
		{
			if ( !animOps.Contains( ins.Opcode ) || ins.Operands.Count < 2 ) continue;
			var channel = ins.Opcode switch { Opcode.TRIGANIM_CH => Lit( ins.Operands[3] ), Opcode.LOOPANIM_CH => Lit( ins.Operands[2] ), _ => "0" };
			starts.Add( (channel, Lit( ins.Operands[0] ), Lit( ins.Operands[1] ), $"{Path.GetFileName( rse )}@{ins.Address} {ins.Opcode}") );
		}
	}
	var namedChannels = starts.Select( s => s.Channel ).Distinct().ToList();
	if ( declared <= 1 && namedChannels.All( c => c == "0" ) ) continue;
	++multi;

	ModelFile model;
	try { using var s = data.OpenRead( $"{dir}/{stem}.md2" ); if ( s == null ) { Console.WriteLine( $"{key}: no model" ); continue; } model = new ModelFile( s ); } catch ( Exception e ) { Console.WriteLine( $"{key}: model unread {e.Message}" ); continue; }
	var meshes = model.Meshes.Count;
	var animations = RideAnimations.Load( dir, stem, data, declared );
	var parents = model.Nodes.Select( n => n.ParentIndex ).ToArray();

	string Kind( AnimationFile c ) => NodePoser.Drives( c, meshes ) ? "TREE" : MeshRotator.Drives( c, meshes ) ? "TURN" : "NONE";
	var roleKinds = new SortedDictionary<int, List<(int Entry, string Kind, AnimationFile Clip)>>();
	for ( var role = 0; role < 12; ++role )
		for ( var entry = 0; animations.Clip( role, entry ) is { } clip; ++entry )
			( roleKinds.TryGetValue( role, out var l ) ? l : roleKinds[role] = [] ).Add( (entry, Kind( clip ), clip) );

	var kinds = roleKinds.Values.SelectMany( l => l ).Select( e => e.Kind ).ToHashSet();
	var both = kinds.Contains( "TREE" ) && kinds.Contains( "TURN" );
	if ( both ) ++withBoth;
	Console.WriteLine( $"{key} ({source}): {declared} channel(s), {meshes} meshes of {model.Nodes.Count} nodes; roles " +
		string.Join( " ", roleKinds.Select( r => $"{r.Key}:{string.Join( ",", r.Value.Select( e => e.Kind ) )}" ) ) );
	Console.WriteLine( "  starts: " + string.Join( "; ", starts.Select( s => $"ch {s.Channel} clip {s.Role}.{s.Entry}" ).Distinct() ) );
	if ( verbose ) foreach ( var s in starts ) Console.WriteLine( $"    {s.Where}: ch {s.Channel} clip {s.Role}.{s.Entry}" );
	if ( !both ) continue;

	bool Under( int node, int ancestor ) { for ( int at = node, g = parents.Length; at >= 0 && at < parents.Length && g >= 0; at = parents[at], --g ) if ( at == ancestor ) return true; return false; }

	if ( verbose )
	{
		foreach ( var m in roleKinds.Values.SelectMany( l => l ).Where( e => e.Kind == "TURN" ).SelectMany( e => e.Clip.RotationTracks ).Select( r => r.TargetIndex ).Where( m => m >= 0 && m < meshes ).Distinct().Order() )
		{
			var chain = new List<int>();
			for ( int at = parents[m], g = parents.Length; at >= 0 && at < parents.Length && g >= 0; at = parents[at], --g ) chain.Add( at );
			var under = Enumerable.Range( 0, parents.Length ).Where( n => n != m && Under( n, m ) ).ToList();
			Console.WriteLine( $"    turned mesh {m} '{model.Nodes[m].Name}': parents {string.Join( " < ", chain.Select( p => $"{p} '{model.Nodes[p].Name}'" ) )}; under it {(under.Count == 0 ? "nothing" : string.Join( ",", under ))}" );
		}
	}


	// Every literal (channel, role) pair; a variable one is said and not paired.
	var literal = starts.Where( s => int.TryParse( s.Channel, out _ ) && int.TryParse( s.Role, out _ ) && int.TryParse( s.Entry, out _ ) )
		.Select( s => (Channel: int.Parse( s.Channel ), Role: int.Parse( s.Role ), Entry: int.Parse( s.Entry )) ).Distinct().ToList();
	foreach ( var odd in starts.Where( s => !int.TryParse( s.Channel, out _ ) || !int.TryParse( s.Role, out _ ) || !int.TryParse( s.Entry, out _ ) ).Select( s => $"ch {s.Channel} clip {s.Role}.{s.Entry}" ).Distinct() )
		Console.WriteLine( $"  NOT PAIRED (a variable): {odd}" );
	foreach ( var t in literal )
	foreach ( var n in literal )
	{
		if ( t.Channel == n.Channel || !roleKinds.TryGetValue( t.Role, out var tl ) || !roleKinds.TryGetValue( n.Role, out var nl ) ) continue;
		foreach ( var turn in tl.Where( e => e.Kind == "TURN" && e.Entry == t.Entry ) )
		foreach ( var tree in nl.Where( e => e.Kind == "TREE" && e.Entry == n.Entry ) )
		{
			++pairs;
			var later = t.Channel > n.Channel;
			if ( later ) ++laterTurns;
			var moved = tree.Clip.Tracks.Select( k => k.Node ).ToHashSet();
			var turned = turn.Clip.RotationTracks.Where( r => r.TargetIndex >= 0 && r.TargetIndex < meshes ).Select( r => r.TargetIndex ).Distinct().ToList();
			// A node the tree moves that is a turned mesh, above it, or under it.
			var shared = turned.Where( m => moved.Any( x => Under( m, x ) || Under( x, m ) ) ).ToList();
			if ( shared.Count > 0 ) { if ( later ) { ++laterShared; reached.Add( $"{key} turn role {t.Role}.{turn.Entry} ch {t.Channel} after tree role {n.Role}.{tree.Entry} ch {n.Channel}" ); } else ++earlierShared; }
			Console.WriteLine( $"  PAIR turn role {t.Role}.{turn.Entry} on ch {t.Channel} {(later ? "AFTER" : "before")} tree role {n.Role}.{tree.Entry} on ch {n.Channel}: " +
				$"turned meshes {string.Join( ",", turned )}; tree moves {string.Join( ",", moved.Order() )}; shared {(shared.Count == 0 ? "none" : string.Join( ",", shared ))}" );
		}
	}
}
Console.WriteLine( $"TALLY models {models.Count}; more than one channel or a named one {multi}; with TREE and TURN clips {withBoth}; " +
	$"turn/tree pairs on two channels {pairs}, the turn on the later channel {laterTurns}; sharing a mesh chain: later {laterShared}, earlier {earlierShared}" );
foreach ( var r in reached ) Console.WriteLine( $"REACHED {r}" );
