using OpenTPW;
using OpenTPW.Common;
using System.Text.Json;
// lib/normdump (Q657): every mesh of a model, its file normal table (+0x64) and OpenTPW's computed per-vertex normals.
//   normdump <data dir> <path in data> <out.json>
var fs = new BaseFileSystem( args[0] );
fs.RegisterArchiveHandler<WadArchive>( ".wad" );
var model = new ModelFile( new MemoryStream( fs.ReadAllBytes( args[1] ) ) );
var list = new List<object>();
foreach ( var m in model.Meshes )
{
	list.Add( new {
		name = m.Name.TrimEnd( '\0' ).Trim(),
		order = m.VertexOrder.Length,
		faces = m.FaceNormalIndices.Select( x => (int)x ).ToArray(),
		table = m.FaceNormals.Select( v => new[] { v.X, v.Y, v.Z } ).ToArray(),
		computed = m.Normals.Select( v => new[] { v.X, v.Y, v.Z } ).ToArray(),
		indices = m.Indices.Select( x => (int)x ).ToArray(),
	} );
}
File.WriteAllText( args[2], JsonSerializer.Serialize( list ) );
Console.WriteLine( $"{model.Meshes.Count} meshes" );
