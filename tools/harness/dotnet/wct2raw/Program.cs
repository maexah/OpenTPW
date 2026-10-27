// wct2raw <in.wct|in.sgn> <out.rgba> : decode a .wct (or a .sgn's artwork) with OpenTPW.Files; write width, height, RGBA.
using OpenTPW;
// wct2raw --batch <list> <outdir> : each line of list a .wct path; writes <outdir>/<n>.rgba (the line's number), "FAIL" lines on stderr.
if ( args[0] == "--batch" )
{
	var lines = File.ReadAllLines( args[1] );
	Parallel.For( 0, lines.Length, i =>
	{
		try
		{
			using var fs = File.OpenRead( lines[i] ); var d = new TextureFile( fs ).Data;
			using var o = File.Create( Path.Combine( args[2], $"{i}.rgba" ) );
			o.Write( BitConverter.GetBytes( d.Width ) ); o.Write( BitConverter.GetBytes( d.Height ) ); o.Write( d.Data );
		}
		catch ( Exception e ) { Console.Error.WriteLine( $"FAIL {i} {lines[i]} {e.Message}" ); }
	} );
	return;
}
if ( args[0] == "--sweep" )
{
	int files = 0, meshes = 0, over = 0, most = 0;
	foreach ( var f in File.ReadAllLines( args[1] ) )
	{
		try { using var fs = File.OpenRead( f ); var m = new ModelFile( fs ); ++files;
			foreach ( var mesh in m.Meshes ) { ++meshes; var used = mesh.Materials.Length; most = Math.Max( most, used ); if ( used > 16 ) { ++over; Console.WriteLine( $"{f} {mesh.Name} {used}" ); } } }
		catch ( Exception e ) { Console.WriteLine( $"{f} FAIL {e.Message}" ); }
	}
	Console.WriteLine( $"files {files} meshes {meshes} over16 {over} most {most}" );
	return;
}
using var s = File.OpenRead( args[0] );
TextureData data;
if ( args[0].EndsWith( ".md2", StringComparison.OrdinalIgnoreCase ) )
{
	var m = new ModelFile( s );
	foreach ( var mesh in m.Meshes ) Console.WriteLine( $"{mesh.Name}: " + string.Join( ", ", mesh.Materials.Select( ( x, i ) => $"[{i}] {x.Name} 0x{x.Flags:x}" ).Where( t => !t.Contains( "]  0x" ) ) ) );
	return;
}
if ( args[0].EndsWith( ".sgn", StringComparison.OrdinalIgnoreCase ) )
{
	var sign = new SignFile( s );
	Console.WriteLine( $"sgn valid {sign.IsValid} image {(sign.Image is null ? "none" : "yes")}" );
	if ( sign.Image is not { } image ) return;
	data = image;
}
else data = new TextureFile( s ).Data;
using var o = File.Create( args[1] );
o.Write( BitConverter.GetBytes( data.Width ) ); o.Write( BitConverter.GetBytes( data.Height ) ); o.Write( data.Data );
Console.WriteLine( $"{args[0]}: {data.Width}x{data.Height}" );
