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

	public ParkRideHoarding( string theme, int thingId, RideHoardingGeometry.Panel[] panels,
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
			_heights[i * 2] = Ground( panels[i].GridA, origin, turn );
			_heights[i * 2 + 1] = Ground( panels[i].GridB, origin, turn );
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
	}

	internal static float Ground( System.Numerics.Vector2 grid, Vector3 origin, Quaternion turn )
	{
		if ( ParkGround.Current?.Heightfield is not { } field ) return 0f;
		return Ground( field, grid, origin, turn );
	}

	internal static float Ground( HeightfieldFile field, System.Numerics.Vector2 grid, Vector3 origin, Quaternion turn )
	{
		var world = V3.Transform( new V3( grid.X * 10, grid.Y * 10, 0 ), turn ) + origin.GetSystemVector3();
		// FUN_00454050 biases positive grid coordinates by 0.1 before truncating.
		var x = (int)(world.X / field.CellSizeX + 0.1f);
		var y = (int)(world.Y / field.CellSizeY + 0.1f);
		return (field.HeightAt( x, y ) - origin.Z) * 0.5f;
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
		for ( var i = 0; i < _panels.Length; ++i )
		{
			var p = _panels[i];
			var (height, lowerV) = _state.Panel( i, _panels.Length );
			for ( var side = 0; side < 2; ++side )
			for ( var corner = 0; corner < 4; ++corner )
			{
				var end = corner & 1;
				var at = end == 0 ? p.A : p.B;
				var upper = corner < 2;
				_vertices[i * 8 + side * 4 + corner] = new Vertex
				{
					Position = new Vector3( at.X, at.Y, _heights[i * 2 + end] + (upper ? height : 0f) ),
					Normal = (Vector3)(side == 0 ? p.Normal : -p.Normal),
					TexCoords = new Vector2( end, upper ? 1f : lowerV ),
					TexIndex = _texture
				};
			}
		}
	}
}
