using OpenTPW;
using OpenTPW.Common;
// dotnet/hfsamplers (Q822): a park heightfield's cells under the original's height samplers, against OpenTPW's.
//   hfsamplers <data dir> <path in data>...      e.g. levels/jungle/terrain/base.MD2
// Per file: the cells the load flags 0x800 (FUN_004504c0: the two triangles across (x+1,z)-(x,z+1), the cross products of
// their unit edges, not made unit, their dot under 0.9999), the bend of the cells it leaves unflagged, and, on a 10 x 10 grid of points in every cell, how far
// FUN_004527f0 (0x800 cells split by 0x4, the rest the near-corner plane), FUN_00452560 (split by 0x4 on every cell),
// HeightfieldFile.HeightAtWorld (bilinear), ScapeHeight (the loader's flags, so it matches FUN_004527f0 by construction) and CameraHeight answer apart; and the cells
// whose 0x2 (the pick's and the drawing's split) and 0x4 (the heights' split) name different diagonals.
var fs = new BaseFileSystem( args[0] );
fs.RegisterArchiveHandler( ".wad", path => new WadArchive( path ) );

foreach ( var path in args.Skip( 1 ) )
{
	var field = new HeightfieldFile( new MemoryStream( fs.ReadAllBytes( path ) ) );
	int cells = 0, ground = 0, flagged = 0, flaggedGround = 0, flaggedGround4 = 0, bentUnflagged = 0;
	int bilinearOff = 0, scapeOff = 0, cameraOff = 0, sameBits = 0, sameBitsBent = 0, groundSameBits = 0;
	float maxUnflaggedBend = 0f, maxBilinear = 0f, maxScape = 0f, maxCamera = 0f, maxBend = 0f;
	var holeBits = new Dictionary<int, int>();
	int groundBent = 0, groundBentUnflagged = 0, groundBilinearOff = 0, groundCameraOff = 0;
	float groundMaxBend = 0f, groundMaxUnflaggedBend = 0f, groundMaxBilinear = 0f, groundMaxCamera = 0f;

	for ( var y = 0; y < field.CellsY; ++y )
	for ( var x = 0; x < field.CellsX; ++x )
	{
		++cells;
		var flags = field.FlagsAt( x, y );
		var isGround = (flags & 0x1) == 0;
		var (h00, h10, h01, h11) = (field.HeightAt( x, y ), field.HeightAt( x + 1, y ), field.HeightAt( x, y + 1 ), field.HeightAt( x + 1, y + 1 ));
		var bend = MathF.Abs( h11 - h10 - h01 + h00 );
		if ( Environment.GetEnvironmentVariable( "HFS_DUMP" ) is { Length: > 0 } dump )
			File.AppendAllText( dump, $"{x} {y} {flags:x4}\n" );
		var load800 = LoadFlags( h00, h10, h01, h11, field.CellSizeX, field.CellSizeY );
		var split4 = (flags & 0x4) != 0;

		if ( isGround ) ++ground; else holeBits[flags & 0x6] = holeBits.GetValueOrDefault( flags & 0x6 ) + 1;
		if ( load800 ) { ++flagged; if ( isGround ) { ++flaggedGround; if ( split4 ) ++flaggedGround4; } }
		else if ( bend > 0f ) { ++bentUnflagged; maxUnflaggedBend = MathF.Max( maxUnflaggedBend, bend ); }
		maxBend = MathF.Max( maxBend, bend );

		if ( ((flags & 0x2) != 0) == split4 )
		{
			++sameBits;
			if ( isGround ) ++groundSameBits;
			if ( bend > 0f ) ++sameBitsBent;
		}

		float worstBilinear = 0f, worstScape = 0f, worstCamera = 0f;

		for ( var j = 0; j < 10; ++j )
		for ( var i = 0; i < 10; ++i )
		{
			var (u, v) = ((i + 0.5f) / 10f, (j + 0.5f) / 10f);
			var (wx, wy) = ((x + u) * field.CellSizeX, (y + v) * field.CellSizeY);
			var triangle = Triangle( h00, h10, h01, h11, u, v, split4 );
			var original = load800 ? triangle : h00 + ((h10 - h00) * u) + ((h01 - h00) * v);

			worstBilinear = MathF.Max( worstBilinear, MathF.Abs( field.HeightAtWorld( wx, wy ) - original ) );
			worstScape = MathF.Max( worstScape, MathF.Abs( field.ScapeHeight( wx, wy ) - original ) );
			worstCamera = MathF.Max( worstCamera, MathF.Abs( field.CameraHeight( wx, wy ) - original ) );
		}

		if ( worstBilinear > 0.001f ) ++bilinearOff;
		if ( worstScape > 0.001f ) ++scapeOff;
		if ( worstCamera > 0.001f ) ++cameraOff;
		if ( isGround )
		{
			if ( bend > 0f ) ++groundBent;
			if ( bend > 0f && !load800 ) { ++groundBentUnflagged; groundMaxUnflaggedBend = MathF.Max( groundMaxUnflaggedBend, bend ); }
			if ( worstBilinear > 0.001f ) ++groundBilinearOff;
			if ( worstCamera > 0.001f ) ++groundCameraOff;
			(groundMaxBend, groundMaxBilinear, groundMaxCamera) = (MathF.Max( groundMaxBend, bend ), MathF.Max( groundMaxBilinear, worstBilinear ), MathF.Max( groundMaxCamera, worstCamera ));
		}

		(maxBilinear, maxScape, maxCamera) = (MathF.Max( maxBilinear, worstBilinear ), MathF.Max( maxScape, worstScape ), MathF.Max( maxCamera, worstCamera ));
	}

	Console.WriteLine( $"{path}: {field.CellsX} x {field.CellsY} = {cells} cells, {ground} ground; most bend {maxBend:0.###}" );
	Console.WriteLine( $"  load 0x800: {flagged} cells, {flaggedGround} ground, {flaggedGround4} of those with 0x4; unflagged yet bent {bentUnflagged} (most {maxUnflaggedBend:0.######})" );
	Console.WriteLine( $"  holes' 0x2|0x4 bits: {string.Join( ", ", holeBits.OrderBy( p => p.Key ).Select( p => $"0x{p.Key:x}={p.Value}" ) )}" );
	Console.WriteLine( $"  0x2 and 0x4 name different diagonals (equal bits): {sameBits} cells ({groundSameBits} ground, {sameBitsBent} bent)" );
	Console.WriteLine( $"  against FUN_004527f0, cells off by over 0.001: bilinear {bilinearOff} (most {maxBilinear:0.###}), ScapeHeight {scapeOff} (most {maxScape:0.###}), CameraHeight {cameraOff} (most {maxCamera:0.###})" );
	Console.WriteLine( $"  ground only: bent {groundBent} (most {groundMaxBend:0.###}), bent yet unflagged {groundBentUnflagged} (most {groundMaxUnflaggedBend:0.######}); off FUN_004527f0 by over 0.001: bilinear {groundBilinearOff} (most {groundMaxBilinear:0.###}), CameraHeight {groundCameraOff} (most {groundMaxCamera:0.###})" );
}

// FUN_004504c0's test (0x0045075d): each triangle's two edges made unit vectors (0x43a9a0), crossed and NOT normalised
// (0x004506c0, 0x00450725), the two crosses' dot (0x43a970) under 0.9999.
static bool LoadFlags( float h00, float h10, float h01, float h11, float dx, float dz )
{
	var a = Face( new( dx, h10 - h00, 0f ), new( 0f, h01 - h00, dz ) );
	var b = Face( new( -dx, h01 - h11, 0f ), new( 0f, h10 - h11, -dz ) );
	return System.Numerics.Vector3.Dot( a, b ) < 0.9999f;
}

static System.Numerics.Vector3 Face( System.Numerics.Vector3 p, System.Numerics.Vector3 q )
	=> System.Numerics.Vector3.Cross( System.Numerics.Vector3.Normalize( q ), System.Numerics.Vector3.Normalize( p ) );

// FUN_004527f0's split arms (0x004528f8): 0x4 clear cuts (x,z)-(x+1,z+1), set cuts (x+1,z)-(x,z+1).
static float Triangle( float h00, float h10, float h01, float h11, float u, float v, bool other )
{
	if ( !other )
		return v <= u ? h00 + ((h10 - h00) * u) + ((h11 - h10) * v) : h00 + ((h11 - h01) * u) + ((h01 - h00) * v);

	return u + v >= 1f ? h11 + ((h11 - h01) * (u - 1f)) + ((h11 - h10) * (v - 1f)) : h00 + ((h10 - h00) * u) + ((h01 - h00) * v);
}
