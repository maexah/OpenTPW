namespace OpenTPW;

/// <summary>
/// The lobby's sky: a cube turned inside out around everything, painted one flat colour.
///
/// That colour is a park's SKYCOLOUR - the original copies the currently selected island's into
/// its renderer every frame, so the sky belongs to whichever park you are looking at rather than
/// to a place in the world. <see cref="LobbyWeather"/> is what feeds it.
/// </summary>
public class Sky : ModelEntity
{
	private Texture? _texture;
	private readonly byte[] _pixel = [79, 214, 255, 255];

	/// <summary>
	/// The sky's colour, 0-1 per channel. Written straight into the one pixel the sky is made of
	/// rather than by building a new texture, which at this call rate would leak - see
	/// <see cref="Texture.UpdatePixels"/>.
	/// </summary>
	public Vector3 Colour
	{
		get => new( _pixel[0] / 255f, _pixel[1] / 255f, _pixel[2] / 255f );
		set
		{
			var r = (byte)(value.X.Clamp( 0f, 1f ) * 255f);
			var g = (byte)(value.Y.Clamp( 0f, 1f ) * 255f);
			var b = (byte)(value.Z.Clamp( 0f, 1f ) * 255f);

			if ( r == _pixel[0] && g == _pixel[1] && b == _pixel[2] )
				return;

			_pixel[0] = r;
			_pixel[1] = g;
			_pixel[2] = b;

			_texture?.UpdatePixels( _pixel );
		}
	}

	public override void Spawn()
	{
		base.Spawn();

		_texture = new Texture( _pixel, 1, 1 );

		var material = new Material<ObjectUniformBuffer>( "content/shaders/unlit.shader" );
		material.Set( "Color", _texture );

		Model = Primitives.Cube.GenerateModel( material );
		Scale = new Vector3( -10000f );
	}
}
