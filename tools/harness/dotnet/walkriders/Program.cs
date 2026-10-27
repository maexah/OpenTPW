using OpenTPW;
// walkriders (Q585), READ-ONLY: every walk slot in use in the given park files beside its rider's saved sprite - the place
// FUN_005580a0 puts it, the facing it gives it, or no sprite at all - and the walkers' sprite facing against the octant of
// their angle. What the original's placer did is read off its own saves; RideNodes gives the nodes at rest.
// args: <data dir> <park file>...  |  <data dir> --objects <park file>  |  <data dir> --nodes <item dir> <stem> <cellX> <cellY> <angle> <space hex> <id>...   (build: dotnet build; run bin/Debug/net10.0/walkriders.dll)
OpenTPW.Common.GlobalNamespace.Log = new();
var data = new BaseFileSystem( args[0] );
data.RegisterArchiveHandler<WadArchive>( ".wad" );
OpenTPW.Common.GlobalNamespace.FileSystem = data;
if ( args.Length > 1 && args[1] == "--nodes" )
{
	// --nodes <item dir> <stem> <cellX> <cellY> <angle> <space hex> <id>...: each node's place at rest, model axes (y up).
	var at = RideNodes.Load( args[2], args[3], data, false, [] )!;
	at.Place( ParkObjects.OriginFor( int.Parse( args[4] ), int.Parse( args[5] ), int.Parse( args[6] ) ), int.Parse( args[6] ) );
	var space = Convert.ToUInt32( args[7], 16 );
	foreach ( var id in args.Skip( 8 ).Select( int.Parse ) )
	{
		var e = at.Find( id, space, out var node );
		at.FindFacing( id, space, out var facing );
		Console.WriteLine( $"node {id} {e} ({node.X:0.000},{node.Y:0.000},{node.Z:0.000}) facing {facing}" );
	}
	return;
}
if ( args.Length > 2 && args[1] == "--objects" )
{
	// --objects <park file>: each object's thing id, catalogue id, cell and turn.
	using var file = File.OpenRead( args[2] );
	var read = new SaveReader( file );
	foreach ( var o in new ParkWorld( read.ReadFile(), read.Preamble ).Objects )
		Console.WriteLine( $"thing {o.ThingId} item {o.CatalogueId} cell ({o.RawX >> 8},{o.RawY >> 8}) angle {o.Angle}" );
	return;
}
var catalogues = new Dictionary<string, ParkItemCatalogue>();
var tally = new SortedDictionary<string, (int n, int agree)>();
void Count( string key, bool ok ) { var c = tally.GetValueOrDefault( key ); tally[key] = (c.n + 1, c.agree + (ok ? 1 : 0)); }
foreach ( var path in args.Skip( 1 ) )
{
	var theme = new[] { "fantasy", "hallow", "space" }.FirstOrDefault( t => path.Contains( $"/{t}/" ) ) ?? "jungle";
	using var stream = File.OpenRead( path );
	var reader = new SaveReader( stream );
	var world = new ParkWorld( reader.ReadFile(), reader.Preamble );
	if ( !catalogues.TryGetValue( theme, out var catalogue ) ) catalogues[theme] = catalogue = new ParkItemCatalogue( theme, data, false );
	var name = $"{Path.GetFileName( Path.GetDirectoryName( path ) )}/{Path.GetFileName( path )}";
	var sprites = world.Sprites.ToDictionary( s => s.Slot );
	var riders = new HashSet<int>();
	foreach ( var handle in world.ScriptStates.Order )
	{
		if ( world.ScriptStates.For( handle ) is not { } script || script.Walk is not { Length: > 0 } walk ) continue;
		var thing = world.Objects.FirstOrDefault( o => o.ThingId == script.Thing );
		if ( thing.ThingId != script.Thing || !catalogue.TryGet( thing.CatalogueId, out var item ) ) continue;
		var nodes = RideNodes.Load( item.Directory, item.Stem, data, item.DoHeadProcessing, [] );
		if ( nodes is null ) continue;
		nodes.Place( ParkObjects.OriginFor( thing.RawX >> 8, thing.RawY >> 8, thing.Angle ), thing.Angle );
		for ( var i = 0; i < walk.Length; ++i )
		{
			var s = walk[i];
			if ( s.State == 0 ) continue;
			riders.Add( s.Handle );
			var person = world.People.FirstOrDefault( p => p.ThingId == s.Handle );
			var has = person.ThingId == s.Handle && person.SpriteSlot != 0 && sprites.ContainsKey( person.SpriteSlot );
			var sprite = has ? sprites[person.SpriteSlot] : default;
			var hiddenByRule = s.State == 2 && s.Action is 1 or 2 or 4;
			var key = $"state {s.State} action {s.Action}";
			string at = "";
			if ( s.State == 2 && s.Action is 3 or 5 or 6 )
			{
				var e = nodes.Find( s.HeadNode, RideNodes.WalkSpace, out var node );
				var dx = has ? sprite.X - node.X : float.NaN; var dz = has ? sprite.Y - node.Z : float.NaN;
				var placed = has && MathF.Abs( dx ) < 0.01f && MathF.Abs( dz ) < 0.01f;
				Count( key + " sprite on the head node", placed );
				Count( key + " sprite faces the slot", has && sprite.Facing == s.Facing );
				at = $"node {s.HeadNode} {e} ({node.X:0.000},{node.Y:0.000},{node.Z:0.000}) sprite ({sprite.X:0.000},{sprite.Height:0.000},{sprite.Y:0.000}) d ({dx:0.000},{dz:0.000})";
			}
			else if ( hiddenByRule )
			{
				Count( key + " no sprite", !has );
			}
			else if ( s.State is 1 or 3 or 4 )
			{
				var (aId, aSpace, bId, bSpace) = s.State == 1
					? (s.WalkNode, RideNodes.WalkSpace, s.HeadNode, s.Action == 4 ? RideNodes.HeadSpace : RideNodes.WalkSpace)
					: s.State == 3 ? (s.OffFrom, s.Action == 4 ? RideNodes.HeadSpace : RideNodes.WalkSpace, s.OffTo, RideNodes.WalkSpace)
					: (s.OffTo, RideNodes.WalkSpace, s.OffTo, RideNodes.WalkSpace);
				nodes.Find( aId, aSpace, out var a ); nodes.Find( bId, bSpace, out var b );
				// On the line from A to B, x and z, at some share t (the save's clock is not read here).
				var len2 = (b.X - a.X) * (b.X - a.X) + (b.Z - a.Z) * (b.Z - a.Z);
				var t = len2 > 0 ? ((sprite.X - a.X) * (b.X - a.X) + (sprite.Y - a.Z) * (b.Z - a.Z)) / len2 : 0f;
				var px = a.X + (b.X - a.X) * t; var pz = a.Z + (b.Z - a.Z) * t;
				var off = MathF.Sqrt( (sprite.X - px) * (sprite.X - px) + (sprite.Y - pz) * (sprite.Y - pz) );
				Count( key + " sprite on the leg", has && off < 0.01f && t >= -0.001f && t <= 1.001f );
				Count( key + " sprite faces the slot", has && sprite.Facing == s.Facing );
				at = $"leg {aId}->{bId} ({a.X:0.000},{a.Z:0.000})->({b.X:0.000},{b.Z:0.000}) t {t:0.000} off {off:0.0000} sprite ({sprite.X:0.000},{sprite.Height:0.000},{sprite.Y:0.000})";
			}
			Console.WriteLine( $"  {name} '{item.Name}' ({item.Directory} {item.Stem}) cell ({thing.RawX >> 8},{thing.RawY >> 8}) angle {thing.Angle} slot {i} state {s.State} action {s.Action} handle {s.Handle} "
				+ $"slot facing {s.Facing} sprite {(has ? $"slot {sprite.Slot} facing {sprite.Facing} set {sprite.Set} frame {sprite.Frame} script {sprite.Script}" : "NONE")} {at}" );
		}
	}
	foreach ( var person in world.People )
	{
		if ( person.Guest is null || riders.Contains( person.ThingId ) || person.SpriteSlot == 0 || !sprites.TryGetValue( person.SpriteSlot, out var sp ) ) continue;
		Count( "walkers: sprite facing == OctantOf( angle )", sp.Facing == person.Facing );
	}
}
foreach ( var c in tally ) Console.WriteLine( $"{c.Key}: {c.Value.agree} of {c.Value.n}" );
