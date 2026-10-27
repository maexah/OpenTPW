using System.Text;
using OpenTPW;

// Scratchpad harness. Dumps members out of a .wad using OpenTPW's own WadArchive,
// so Refpack-compressed members come back decompressed instead of being grepped
// through compression tokens.
//
//   wadcat --list <wad>...            list every member with its size
//   wadcat --cat <suffix> <wad>...    print members whose name ends with <suffix>
//   wadcat --id <wad>...              print "Info.Id" and "Name" out of each wad's .sam
//   wadcat --bounds <wad>...          vertex bounds of each .MD2, to tell a world-authored
//                                     mesh (placed at the origin) from a local-origin one
//   wadcat --anim <wad>...            every animation member's tracks, with each target
//                                     resolved to the node name it drives in its base model

if ( args.Length < 2 )
{
	Console.Error.WriteLine( "usage: wadcat --list|--id|--bounds|--meshes|--anim|--field|--heights <wad>... " +
		"| --cat|--dump <suffix> <wad>..." );
	return 1;
}

var mode = args[0];
var takesSuffix = mode is "--cat" or "--dump";
var suffix = takesSuffix ? args[1] : ".sam";
var wads = args.Skip( takesSuffix ? 2 : 1 ).ToArray();

foreach ( var wadPath in wads )
{
	WadArchive archive;

	try
	{
		archive = new WadArchive( wadPath );
	}
	catch ( Exception e )
	{
		Console.Error.WriteLine( $"!! {wadPath}: {e.Message}" );
		continue;
	}

	using ( archive )
	{
		// Whole-archive mode: an animation only makes sense against the model it drives, so this
		// one needs every member in hand before it can say anything.
		if ( mode == "--anim" )
		{
			Anims( archive, wadPath );
			continue;
		}

		foreach ( var member in Members( archive, "" ) )
		{
			switch ( mode )
			{
				case "--list":
					Console.WriteLine( $"{Size( archive, member ),9}  {Path.GetFileName( wadPath )}  {member}" );
					break;

				case "--cat":
					if ( !member.EndsWith( suffix, StringComparison.OrdinalIgnoreCase ) )
						continue;
					Console.WriteLine( $"===== {Path.GetFileName( wadPath )} :: {member} =====" );
					Console.WriteLine( Text( archive, member ) );
					break;

				case "--id":
					if ( !member.EndsWith( ".sam", StringComparison.OrdinalIgnoreCase ) )
						continue;
					Report( wadPath, member, Text( archive, member ) );
					break;

				case "--bounds":
					if ( !member.EndsWith( ".MD2", StringComparison.OrdinalIgnoreCase ) )
						continue;
					Bounds( archive, wadPath, member );
					break;

				case "--meshes":
					if ( !member.EndsWith( ".MD2", StringComparison.OrdinalIgnoreCase ) )
						continue;
					MeshList( archive, member );
					break;

				// Decompressed bytes onto disk, for the things that want reading as binary rather
				// than printing - the attribute map, the landscape.
				case "--dump":
					if ( !member.EndsWith( suffix, StringComparison.OrdinalIgnoreCase ) )
						continue;

					var into = Path.Combine( Directory.GetCurrentDirectory(), Path.GetFileName( member ) );
					File.WriteAllBytes( into, archive.GetFile( member ).GetData() );
					Console.WriteLine( $"{into}  {new FileInfo( into ).Length} bytes" );
					break;

				// Every heightfield VERTEX's height, so that whether the land under a built item is
				// flat can be measured instead of argued about.
				case "--heights":
					if ( !member.EndsWith( ".MD2", StringComparison.OrdinalIgnoreCase ) )
						continue;

					{
						var land = new HeightfieldFile( new MemoryStream( archive.GetFile( member ).GetData() ) );

						Console.WriteLine( $"# {member} {land.CellsX}x{land.CellsY} cells, " +
							$"{land.CellsX + 1}x{land.CellsY + 1} vertices" );

						for ( var vy = 0; vy <= land.CellsY; ++vy )
						{
							for ( var vx = 0; vx <= land.CellsX; ++vx )
								Console.WriteLine( $"{vx} {vy} {land.HeightAt( vx, vy )}" );
						}
					}
					break;

				// Every heightfield cell's texture index and flag word, so what the ground draws can be
				// cross-referenced against what the park's save says is on each cell.
				case "--field":
					if ( !member.EndsWith( ".MD2", StringComparison.OrdinalIgnoreCase ) )
						continue;

					{
						var field = new HeightfieldFile( new MemoryStream( archive.GetFile( member ).GetData() ) );

						Console.WriteLine( $"# {member} {field.CellsX}x{field.CellsY} cell {field.CellSizeX}x{field.CellSizeY} " +
							$"names {string.Join( ",", field.TextureNames )}" );

						for ( var cy = 0; cy < field.CellsY; ++cy )
						{
							for ( var cx = 0; cx < field.CellsX; ++cx )
							{
								var word = field.Cells[(cy * field.CellsX) + cx];
								Console.WriteLine( $"{cx} {cy} {word >> 16} {word & 0xFFFF}" );
							}
						}
					}
					break;
			}
		}
	}
}

return 0;

// Every file in the archive, depth first, as a backslash-joined internal path.
static IEnumerable<string> Members( WadArchive archive, string directory )
{
	foreach ( var file in archive.GetFiles( directory ) )
		yield return Join( directory, file );

	foreach ( var child in archive.GetDirectories( directory ) )
		foreach ( var member in Members( archive, Join( directory, child ) ) )
			yield return member;
}

static string Join( string directory, string name ) => directory.Length == 0 ? name : $"{directory}\\{name}";

static int Size( WadArchive archive, string member ) => archive.GetFile( member ).GetData().Length;

static string Text( WadArchive archive, string member ) => Encoding.ASCII.GetString( archive.GetFile( member ).GetData() );

// The two fields that identify a catalogue item, pulled out of its balance file.
static void Report( string wadPath, string member, string text )
{
	string id = null, name = null;

	foreach ( var line in text.Split( '\n' ) )
	{
		var fields = line.Split( [' ', '\t'], StringSplitOptions.RemoveEmptyEntries );

		if ( fields.Length < 2 )
			continue;

		if ( fields[0] == "Info.Id" )
			id ??= fields[1];
		else if ( fields[0] == "Info.Name" || fields[0] == "Name" )
			name ??= string.Join( ' ', fields[1..] );
	}

	if ( id != null || name != null )
		Console.WriteLine( $"{id,8}  {Path.GetFileName( wadPath ),-16} {member,-20} {name}" );
}

// One line per mesh: name, centre and extent. One line each so a region can be picked out with awk -
// the point is to ask what stands at a known world position, such as the ticket booths' cells.
static void MeshList( WadArchive archive, string member )
{
	try
	{
		using var stream = archive.OpenFile( member );
		var model = new ModelFile( stream );

		foreach ( var mesh in model.Meshes )
		{
			var cx = (mesh.BoundsMin.X + mesh.BoundsMax.X) / 2f;
			var cy = (mesh.BoundsMin.Y + mesh.BoundsMax.Y) / 2f;
			var cz = (mesh.BoundsMin.Z + mesh.BoundsMax.Z) / 2f;

			// Bounds are node-local, so they say how big a mesh is and not where it stands. The
			// placement is in the node hierarchy - WorldTransform is the mesh's own transform with
			// every ancestor's applied - and the ground plane is x,z.
			var world = mesh.WorldTransform.Translation;

			// The orientation the mesh is authored with, which is a separate question from where it
			// stands: two heads on the same bench can sit a metre apart and face different ways.
			var turn = System.Numerics.Matrix4x4.Decompose( mesh.WorldTransform, out var scale, out var rotation, out _ )
				? $"{AxisAngle( rotation )}  scale ({scale.X,5:F2},{scale.Y,5:F2},{scale.Z,5:F2})"
				: "(sheared - will not decompose)";

			Console.WriteLine(
				$"{member} world {world.X,9:F1} {world.Y,9:F1} {world.Z,9:F1}  " +
				$"cell {world.X / 10f,6:F1},{world.Z / 10f,6:F1}  " +
				$"local {cx,8:F1} {cy,8:F1} {cz,8:F1}  {mesh.Name,-16} {turn}" );
		}
	}
	catch ( Exception e )
	{
		Console.WriteLine( $"{member} !! {e.Message}" );
	}
}

// Every animation in the archive, with each track's target resolved to a node name of the static
// model whose name is a prefix of the animation's. This is the only way to ask what a clip actually
// does: a track names a node by index, and an index means nothing without the model.
static void Anims( WadArchive archive, string wadPath )
{
	var statics = new Dictionary<string, ModelFile>( StringComparer.OrdinalIgnoreCase );
	var animations = new List<string>();

	foreach ( var member in Members( archive, "" ) )
	{
		if ( !member.EndsWith( ".MD2", StringComparison.OrdinalIgnoreCase ) )
			continue;

		try
		{
			using var stream = archive.OpenFile( member );
			var model = new ModelFile( stream );

			if ( model.IsAnimation )
				animations.Add( member );
			else
				statics[Path.GetFileNameWithoutExtension( member )] = model;
		}
		catch ( Exception e )
		{
			Console.WriteLine( $"{member} !! {e.Message}" );
		}
	}

	var wad = Path.GetFileName( wadPath );

	foreach ( var entry in statics.OrderBy( pair => pair.Key ) )
	{
		Console.WriteLine( $"===== {wad} :: {entry.Key}.MD2 - {entry.Value.Meshes.Count} meshes, {entry.Value.Nodes.Count} nodes =====" );

		for ( int i = 0; i < entry.Value.Nodes.Count; ++i )
		{
			var node = entry.Value.Nodes[i];
			var kind = i < entry.Value.Meshes.Count ? "mesh" : "node";

			// Both frames, because a rotation key is authored in the LOCAL one and every placement
			// question is asked in the world one - and the two only agree under a root at identity.
			var local = System.Numerics.Matrix4x4.Decompose( node.LocalTransform, out _, out var localTurn, out _ )
				? AxisAngle( localTurn ) : "(sheared)";
			var world = System.Numerics.Matrix4x4.Decompose( node.WorldTransform, out _, out var worldTurn, out _ )
				? AxisAngle( worldTurn ) : "(sheared)";

			var at = node.WorldTransform.Translation;

			Console.WriteLine( $"    {kind} {i,2}  flags 0x{node.Flags:x4}  parent {node.ParentIndex,3}  {node.Name,-16}" +
				$"  at ({at.X,6:F1},{at.Y,6:F1},{at.Z,6:F1})  local {local}  world {world}" );
		}
	}

	foreach ( var member in animations.OrderBy( name => name, StringComparer.OrdinalIgnoreCase ) )
	{
		var stem = Path.GetFileNameWithoutExtension( member );

		// Longest matching prefix, so JunsprayM1 goes to Junspray and PJunsprayM to PJunspray.
		ModelFile baseModel = statics
			.Where( pair => stem.StartsWith( pair.Key, StringComparison.OrdinalIgnoreCase ) )
			.OrderByDescending( pair => pair.Key.Length )
			.Select( pair => pair.Value )
			.FirstOrDefault();

		string Target( int index )
		{
			if ( baseModel == null )
				return $"#{index}";

			return index < baseModel.Nodes.Count
				? $"{index,2} '{baseModel.Nodes[index].Name.Trim()}'"
				: $"{index,2} (past this model's {baseModel.Nodes.Count} nodes)";
		}

		try
		{
			using var stream = archive.OpenFile( member );
			var animation = new AnimationFile( stream );

			Console.WriteLine( $"--- {wad} :: {member}  frames {animation.FirstFrame}..{animation.LastFrame}  " +
				$"readable {animation.IsValid}  rot {animation.RotationTracks.Count}  morph {animation.MorphTracks.Count}  " +
				$"uv {animation.UvTracks.Count}  pos {animation.PositionTracks.Count}  vis {animation.VisibilityTracks.Count}" );

			foreach ( var track in animation.VisibilityTracks )
				Console.WriteLine( $"      vis   {Target( track.TargetIndex )}  entries [{string.Join( ", ", track.Entries )}]" );

			foreach ( var track in animation.RotationTracks )
			{
				var frames = string.Join( ",", track.FrameIndices );

				// Whether this target's local and world orientations agree. Where they do (a mesh on
				// a root at identity) it cannot matter which frame a key is read in; where they do
				// not, reading a key as a world orientation throws the parent's turn away.
				var frame = "";

				if ( baseModel != null && track.TargetIndex < baseModel.Nodes.Count )
				{
					var node = baseModel.Nodes[track.TargetIndex];

					if ( System.Numerics.Matrix4x4.Decompose( node.LocalTransform, out _, out var localTurn, out _ )
						&& System.Numerics.Matrix4x4.Decompose( node.WorldTransform, out _, out var worldTurn, out _ ) )
					{
						var agree = System.Numerics.Quaternion.Dot( localTurn, worldTurn );
						frame = MathF.Abs( agree ) > 0.9999f
							? "  [local == world]"
							: $"  [LOCAL {AxisAngle( localTurn )} != WORLD {AxisAngle( worldTurn )}]";
					}
				}

				Console.WriteLine( $"      rot   {Target( track.TargetIndex )}  frames [{frames}]{frame}" );

				for ( int k = 0; k < track.Rotations.Length; ++k )
					Console.WriteLine( $"              f{track.FrameIndices[k],4}  {AxisAngle( track.Rotations[k] )}" );
			}

			foreach ( var track in animation.PositionTracks )
			{
				var first = track.Points.Length > 0 ? track.Points[0] : default;
				var last = track.Points.Length > 0 ? track.Points[^1] : default;
				Console.WriteLine( $"      pos   {Target( track.TargetIndex )}  {track.FrameIndices.Length} keys  " +
					$"({first.X:F1},{first.Y:F1},{first.Z:F1}) -> ({last.X:F1},{last.Y:F1},{last.Z:F1})" );
			}

			foreach ( var track in animation.MorphTracks )
				Console.WriteLine( $"      morph {Target( track.TargetIndex )}  {track.Records.Length} records, {track.ChannelCount} channels" );

			foreach ( var track in animation.UvTracks )
				Console.WriteLine( $"      uv    {Target( track.TargetIndex )}  {track.EntryCount} entries" );
		}
		catch ( Exception e )
		{
			Console.WriteLine( $"--- {wad} :: {member} !! {e.Message}" );
		}
	}
}

// A quaternion as a turn a human can read. Quaternions negate to the same rotation, so the
// hemisphere is normalised first or half the keys report the long way round.
static string AxisAngle( System.Numerics.Quaternion q )
{
	if ( q.W < 0 )
		q = System.Numerics.Quaternion.Negate( q );

	var w = Math.Clamp( q.W, -1f, 1f );
	var angle = 2f * MathF.Acos( w ) * 180f / MathF.PI;
	var sine = MathF.Sqrt( Math.Max( 0f, 1f - (w * w) ) );

	if ( sine < 1e-4f )
		return "  0.0 deg (no turn)";

	return $"{angle,6:F1} deg about ({q.X / sine,5:F2},{q.Y / sine,5:F2},{q.Z / sine,5:F2})";
}

// Where a model's geometry actually sits. A mesh authored in world coordinates spans the park;
// one authored about its own origin sits near zero and has to be placed.
static void Bounds( WadArchive archive, string wadPath, string member )
{
	try
	{
		using var stream = archive.OpenFile( member );
		var model = new ModelFile( stream );

		// An animation file carries no static meshes, so it has no bounds to report. Saying so beats
		// printing the empty min/max, which comes out as +/- float.MaxValue and reads like real numbers.
		if ( model.Meshes.Count == 0 )
		{
			Console.WriteLine( $"{Path.GetFileName( wadPath ),-14} {member,-16}   (animation - no static meshes)" );
			return;
		}

		// Plain floats: OpenTPW has a Vector3 of its own and System.Numerics has another, and
		// which one a mesh's bounds are in is beside the point here.
		float minX = float.MaxValue, minY = float.MaxValue, minZ = float.MaxValue;
		float maxX = float.MinValue, maxY = float.MinValue, maxZ = float.MinValue;

		foreach ( var mesh in model.Meshes )
		{
			minX = Math.Min( minX, mesh.BoundsMin.X );
			minY = Math.Min( minY, mesh.BoundsMin.Y );
			minZ = Math.Min( minZ, mesh.BoundsMin.Z );
			maxX = Math.Max( maxX, mesh.BoundsMax.X );
			maxY = Math.Max( maxY, mesh.BoundsMax.Y );
			maxZ = Math.Max( maxZ, mesh.BoundsMax.Z );
		}

		float centreX = (minX + maxX) / 2f, centreY = (minY + maxY) / 2f, centreZ = (minZ + maxZ) / 2f;

		Console.WriteLine( $"{Path.GetFileName( wadPath ),-14} {member,-16} {model.Meshes.Count,3} meshes" );
		Console.WriteLine( $"    min ({minX,9:F1},{minY,9:F1},{minZ,9:F1})  max ({maxX,9:F1},{maxY,9:F1},{maxZ,9:F1})" );
		Console.WriteLine( $"    centre ({centreX,8:F1},{centreY,8:F1},{centreZ,8:F1})  -> cell ({centreX / 10f,5:F1},{centreY / 10f,5:F1}) if world-authored" );
	}
	catch ( Exception e )
	{
		Console.WriteLine( $"{Path.GetFileName( wadPath ),-14} {member,-16} !! {e.Message}" );
	}
}
