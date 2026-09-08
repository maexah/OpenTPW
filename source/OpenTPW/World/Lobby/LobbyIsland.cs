using System.Numerics;

namespace OpenTPW;

public sealed class LobbyIsland : Entity
{
	private readonly ModelEntity?[] _meshEntities;
	private readonly Vector3[] _basePositions;
	private readonly Quaternion[] _baseRotations;
	private readonly MeshAnimator? _animator;

	public LobbyIsland( Vector3 _position, string themeName )
	{
		Position = _position;

		var modelPrefix = themeName[0..3];
		var modelFile = new ModelFile( $"lobby/terrain/{modelPrefix}_isle.md2" );

		var meshCount = modelFile.Meshes.Count;
		_meshEntities = new ModelEntity?[meshCount];
		_basePositions = new Vector3[meshCount];
		_baseRotations = new Quaternion[meshCount];

		for ( int meshIndex = 0; meshIndex < meshCount; ++meshIndex )
		{
			var mesh = modelFile.Meshes[meshIndex];
			var material = new Material<ObjectUniformBuffer>( "content/shaders/test.shader" );
			var textures = new List<Texture>();

			for ( int i = 0; i < 16; ++i )
			{
				if ( mesh.Materials.Length <= i || string.IsNullOrEmpty( mesh.Materials[i].Name ) )
				{
					textures.Add( Texture.Missing );
				}
				else
				{
					var j = mesh.Materials[i];
					textures.Add( new Texture( $"lobby/terrain/textures/{j.Name}.wct", TextureFlags.Repeat ) );
				}
			}

			material.Set( $"Color", [.. textures] );

			var vertices = new List<Vertex>();
			for ( int i = 0; i < mesh.Vertices.Length; ++i )
			{
				vertices.Add( new Vertex()
				{
					Position = new Vector3( mesh.Vertices[i].Position.X, mesh.Vertices[i].Position.Z, mesh.Vertices[i].Position.Y ),
					Normal = mesh.Normals[i],
					TexCoords = mesh.TexCoords[i],
					TexIndex = (int)mesh.Vertices[i].TextureIndex,
					MatFlags = mesh.Materials[(int)mesh.Vertices[i].TextureIndex].Flags
				} );
			}

			var model = new Model( [.. vertices], mesh.Indices, material );
			Matrix4x4.Decompose( mesh.TransformMatrix, out var scl, out var rot, out var pos );

			var position = new Vector3( pos.X, pos.Z, pos.Y - 2.5f );
			var rotation = new Quaternion( rot.X, rot.Z, rot.Y, -rot.W );
			var scale = new Vector3( scl.X, scl.Z, scl.Y );

			_meshEntities[meshIndex] = new ModelEntity()
			{
				Model = model,
				Scale = scale,
				Rotation = rotation,
				Position = position + Position,
			};

			_basePositions[meshIndex] = position + Position;
			_baseRotations[meshIndex] = rotation;
		}

		var animationPath = $"lobby/terrain/{modelPrefix}_isleM1.md2";
		if ( AnimationFile.TryLoad( animationPath, out var animation ) && animation != null )
		{
			_animator = new MeshAnimator( animation );

			var animated = new List<int>();
			foreach ( var track in animation.Tracks )
			{
				if ( !track.IsConstant )
					animated.AddRange( track.ChannelIds.Select( x => (int)x ) );
			}
			animated.Sort();

			Log.Info( $"{modelPrefix}_isleM1: {animation.Tracks.Count} records, {animation.ChannelCount} channels, " +
				$"frames {animation.FirstFrame}..{animation.LastFrame}, animated [{string.Join( ", ", animated )}]" );
		}
		else
		{
			Log.Info( $"{modelPrefix}_isleM1: no readable animation (unsupported variant)" );
		}
	}

	protected override void OnUpdate()
	{
		_animator?.Apply( Time.Now, _meshEntities, _basePositions, _baseRotations );
	}
}
