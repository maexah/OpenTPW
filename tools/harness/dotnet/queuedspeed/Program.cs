using OpenTPW;
// queuedspeed (Q1062), READ-ONLY: for every model record of the given park files, each channel's saved queued speed (+0x30) beside the speed word
// of the script its thing runs (the divisor 0.5 + 0.01 x word the scheduler writes into it, FUN_00474020) and its queued role: one line a channel,
// then a tally by (queued role set?, script found?, queued speed against the divisor / nought / other).
// args: <data dir> <park file>...   (build: dotnet build; run bin/Debug/net10.0/queuedspeed.dll)
OpenTPW.Common.GlobalNamespace.Log = new();
var data = new BaseFileSystem( args[0] );
data.RegisterArchiveHandler( ".wad", path => new WadArchive( path ) );
OpenTPW.Common.GlobalNamespace.FileSystem = data;
var channelSpeedOf = typeof( OpenTPW.RideScript ).GetMethod( "ChannelSpeedOf", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static )!;
float EngineRate( int word ) => (float)channelSpeedOf.Invoke( null, [word] )!;
var catalogues = new Dictionary<string, ParkItemCatalogue>();
var tally = new SortedDictionary<string, int>();
var examples = new Dictionary<string, string>();
void Count( string key, string example ) { tally[key] = tally.GetValueOrDefault( key ) + 1; examples.TryAdd( key, example ); }
foreach ( var path in args.Skip( 1 ) )
{
	var theme = new[] { "fantasy", "hallow", "space" }.FirstOrDefault( t => path.Contains( $"/{t}/" ) ) ?? "jungle";
	try
	{
		using var stream = File.OpenRead( path );
		var reader = new SaveReader( stream );
		var world = new ParkWorld( reader.ReadFile(), reader.Preamble );
		if ( !catalogues.TryGetValue( theme, out var catalogue ) ) catalogues[theme] = catalogue = new ParkItemCatalogue( theme, data, false );
		var states = world.ThingStates( catalogue.ChannelsFor );
		foreach ( var thing in states.Things )
		{
			var script = thing.ScriptHandle != 0 ? world.ScriptStates.For( thing.ScriptHandle ) : null;
			for ( var i = 0; i < thing.Channels.Length; ++i )
			{
				var c = thing.Channels[i];
				var queued = c.QueuedRole != ParkThingStates.NoRole;
				var divisor = script is { } s ? 0.5f + 0.01f * s.Speed : float.NaN;
				var kind = script is null ? "noscript" : BitConverter.SingleToInt32Bits( c.QueuedSpeed ) == BitConverter.SingleToInt32Bits( EngineRate( script.Value.Speed ) ) ? "=divisor (bit for bit, RideScript.ChannelSpeedOf)" : Math.Abs( c.QueuedSpeed - divisor ) < 1e-4f ? "=divisor (within 1e-4, not the same bits)" : c.QueuedSpeed == 0f ? "=0" : "other";
				var key = $"{(queued ? "queued" : "nothing-queued")} {kind}";
				Count( key, $"{path} slot {thing.Slot} item {thing.CatalogueId} ch {i}" );
				Console.WriteLine( $"{path}\tslot {thing.Slot}\titem {thing.CatalogueId}\tch {i}\t{(queued ? "queued" : "empty")}\tqspeed {c.QueuedSpeed:0.0000}\tspeed {c.Speed:0.0000}\tword {(script is { } w ? w.Speed.ToString() : "-")}\tdivisor {divisor:0.0000}\tqbits {BitConverter.SingleToInt32Bits( c.QueuedSpeed ):x8} enginebits {( script is { } e ? BitConverter.SingleToInt32Bits( EngineRate( e.Speed ) ) : 0 ):x8}\tqflags {c.QueuedFlags:x}\trole {c.Role}" );
			}
		}
	}
	catch ( Exception e ) { Console.WriteLine( $"SKIP {path}: {e.GetType().Name} {e.Message}" ); }
}
Console.WriteLine( "TALLY" );
foreach ( var (k, n) in tally ) Console.WriteLine( $"{k}\t{n}\te.g. {examples[k]}" );
