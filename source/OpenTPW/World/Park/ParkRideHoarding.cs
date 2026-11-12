using System.Numerics;
using V3 = System.Numerics.Vector3;

namespace OpenTPW;

/// <summary>One placed ride's generated panels and mutable material/vertices; docs/exe/ride-hoardings.md.</summary>
internal sealed class ParkRideHoarding : ModelEntity
{
	private readonly int _thingId;
	private readonly RideHoardingState _state;
	private readonly RideHoardingGeometry.Panel[] _panels;
	private readonly Vertex[] _vertices;
	private readonly float[] _heights;
	private int _texture = -1;
	private static readonly string[] TextureNames = ["Closed", "Hoarding", "Condemn", "Upgrade"];
	internal string Census => $"hoarding {_thingId} panels={_panels.Length} texture={TextureNames[_state.Texture]} " +
		$"progress={_state.Progress:F6} active={(_state.Active ? 1 : 0)} flags=0x{_state.Flags:x2} rate={_state.Rate:F1}";

	public ParkRideHoarding( string theme, int thingId, RideHoardingGeometry.Panel[] panels, System.Numerics.Vector2 size,
		RideHoardingState state, Vector3 origin, Quaternion turn )
	{
		_thingId = thingId;
		_state = state;
		_panels = panels;
		Position = origin;
		Rotation = turn;
		Name = $"hoarding {thingId}";
		_heights = new float[panels.Length * 2];
		_vertices = new Vertex[panels.Length * 8];
		var indices = new uint[panels.Length * 12];
		for ( var i = 0; i < panels.Length; ++i )
		{
			_heights[i * 2] = Ground( panels[i].GridA, size, origin, turn );
			_heights[i * 2 + 1] = Ground( panels[i].GridB, size, origin, turn );
			var v = (uint)i * 8;
			uint[] triangles = [v, v + 1, v + 2, v + 1, v + 3, v + 2,
				v + 6, v + 5, v + 4, v + 6, v + 7, v + 5];
			triangles.CopyTo( indices, i * 12 );
		}
		Fill();
		var textures = new Texture[Material.TextureSlots];
		for ( var i = 0; i < textures.Length; ++i )
			textures[i] = i < 4 ? new Texture( $"levels/{theme}/MiscMesh/textures/{TextureNames[i]}.wct" ) : Texture.Missing;
		var material = new Material<ObjectUniformBuffer>( "content/shaders/test.shader", MaterialFlags.None );
		material.Set( "Color", textures );
		Model = new Model( _vertices, indices, material );
		Model.EnableFrequentUpdates( _vertices );
		Opacity = state.Active ? 1f : 0f;
		Log.Info( Census );
		// Each panel's two ends: grid point, then the half height over the model it stands at.
		Log.Info( $"hoarding {thingId} ground " + string.Join( ' ', panels.Select( ( p, i ) => FormattableString.Invariant(
			$"{p.GridA.X},{p.GridA.Y}={_heights[i * 2]:F2}/{p.GridB.X},{p.GridB.Y}={_heights[i * 2 + 1]:F2}" ) ) ) );
	}

	internal static float Ground( System.Numerics.Vector2 grid, System.Numerics.Vector2 size, Vector3 origin, Quaternion turn )
	{
		if ( ParkGround.Current?.Heightfield is not { } field ) return 0f;
		return Ground( field, grid, size, origin, turn );
	}

	/// <summary>
	/// The ground under one grid endpoint (<paramref name="grid"/> of an outline <paramref name="size"/> cells), half its height
	/// over the model's (<c>FUN_00454190</c>). The endpoint is carried into the model's inclusive cell rectangle, <c>+0xc0</c> its
	/// first cell and <c>+0xc4</c> its last (<c>FUN_00467030</c>, <c>0x004670c1</c>), so at no turn <c>x0 + trunc( gx * (w - 1) / w
	/// + 0.1 )</c> (<c>FUN_00454050</c>): the last grid line reads the last cell's near vertex. docs/exe/ride-hoardings.md.
	/// </summary>
	internal static float Ground( HeightfieldFile field, System.Numerics.Vector2 grid, System.Numerics.Vector2 size, Vector3 origin, Quaternion turn )
	{
		// The outline's corners through the turn give the rectangle; the outline is the footprint's size in every shipped item.
		var at = origin.GetSystemVector3();
		var cell = new V3( field.CellSizeX, field.CellSizeY, 1f );
		V3 Cells( float gx, float gy ) => (V3.Transform( new V3( gx * cell.X, gy * cell.Y, 0 ), turn ) + at) / cell;
		var a = Cells( 0, 0 ); var b = Cells( size.X, size.Y );
		var c = Cells( size.X, 0 ); var d = Cells( 0, size.Y );
		var left = MathF.Round( MathF.Min( MathF.Min( a.X, b.X ), MathF.Min( c.X, d.X ) ) );
		var top = MathF.Round( MathF.Min( MathF.Min( a.Y, b.Y ), MathF.Min( c.Y, d.Y ) ) );
		var width = MathF.Round( MathF.Max( MathF.Max( a.X, b.X ), MathF.Max( c.X, d.X ) ) ) - left;
		var depth = MathF.Round( MathF.Max( MathF.Max( a.Y, b.Y ), MathF.Max( c.Y, d.Y ) ) ) - top;
		var p = Cells( grid.X, grid.Y );
		// Across the rectangle from its first cell to its last, not to the cell past it: x0 + (x - x0) * (w - 1) / w.
		// The turned arms are read as the same carry into the turned rectangle (not checked against FUN_00454050, Q1525).
		var x = width > 0 ? left + (p.X - left) * (width - 1) / width : left;
		var y = depth > 0 ? top + (p.Y - top) * (depth - 1) / depth : top;
		return (field.HeightAt( (int)(x + 0.1f), (int)(y + 0.1f) ) - origin.Z) * 0.5f;
	}

	protected override void OnUpdate()
	{
		// GameClock.Delta is Time.Delta gated by park windows. Debug pause/step also affects Time.Delta.
		// The original uses a scaled engine clock; this engine currently exposes only its normal rate.
		var changed = _state.Advance( GameClock.Delta );
		if ( changed || _texture != _state.Texture )
		{
			Fill();
			Model?.UpdateVertices( _vertices );
		}
		Opacity = _state.Active ? 1f : 0f;
	}

	private void Fill()
	{
		_texture = _state.Texture;
		Fill( _vertices, _panels, _heights, _state );
	}

	/// <summary>
	/// Writes the eight vertices of each panel (two sides of four corners, the upper two first) from the progress state:
	/// the upper corners stand the panel's height over the ground at their end, the lower ones on it with the V
	/// <see cref="RideHoardingState.Panel"/> slides up, and every vertex wears the state's texture.
	/// </summary>
	internal static void Fill( Vertex[] vertices, RideHoardingGeometry.Panel[] panels, float[] heights, RideHoardingState state )
	{
		for ( var i = 0; i < panels.Length; ++i )
		{
			var p = panels[i];
			var (height, lowerV) = state.Panel( i, panels.Length );
			for ( var side = 0; side < 2; ++side )
			for ( var corner = 0; corner < 4; ++corner )
			{
				var end = corner & 1;
				var at = end == 0 ? p.A : p.B;
				var upper = corner < 2;
				vertices[i * 8 + side * 4 + corner] = new Vertex
				{
					Position = new Vector3( at.X, at.Y, heights[i * 2 + end] + (upper ? height : 0f) ),
					Normal = (Vector3)(side == 0 ? p.Normal : -p.Normal),
					TexCoords = new Vector2( end, upper ? 1f : lowerV ),
					TexIndex = state.Texture
				};
			}
		}
	}
}
