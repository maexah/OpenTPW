using System.Numerics;

namespace OpenTPW;

public sealed class LobbyIsland : Entity
{
	private readonly MeshAnimator? _animator;

	public LobbyIsland( Vector3 _position, string themeName )
	{
		Position = _position;

		var modelPrefix = themeName[0..3];
		var modelFile = new ModelFile( $"lobby/terrain/{modelPrefix}_isle.md2" );

		var meshCount = modelFile.Meshes.Count;
		var models = new Model[meshCount];
		var meshVertices = new Vertex[meshCount][];

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
			models[meshIndex] = model;
			meshVertices[meshIndex] = [.. vertices];
			Matrix4x4.Decompose( mesh.TransformMatrix, out var scl, out var rot, out var pos );

			var position = new Vector3( pos.X, pos.Z, pos.Y - 2.5f );
			var rotation = new Quaternion( rot.X, rot.Z, rot.Y, -rot.W );
			var scale = new Vector3( scl.X, scl.Z, scl.Y );

			_ = new ModelEntity()
			{
				Model = model,
				Scale = scale,
				Rotation = rotation,
				Position = position + Position,
			};
		}

		// A model can ship several animations, suffixed M1, M2, ... - all driving one mesh.
		var animations = new List<AnimationFile>();
		for ( int suffix = 1; ; ++suffix )
		{
			if ( !AnimationFile.TryLoad( $"lobby/terrain/{modelPrefix}_isleM{suffix}.md2", out var loaded ) || loaded == null )
				break;

			animations.Add( loaded );
		}

		if ( animations.Count == 0 )
		{
			Log.Info( $"{modelPrefix}_isle: no readable animations" );
		}
		else
		{
			// An animation drives exactly one mesh, identified by its vertex count.
			for ( int meshIndex = 0; meshIndex < meshCount; ++meshIndex )
			{
				var mesh = modelFile.Meshes[meshIndex];
				if ( !MeshAnimator.Drives( animations[0], mesh ) )
					continue;

				_animator = new MeshAnimator( [.. animations], mesh, models[meshIndex], meshVertices[meshIndex] );

				Log.Info( $"{modelPrefix}_isle: animating '{mesh.Name.TrimEnd( '\0' )}' ({mesh.VertexCount} verts) " +
					$"with {animations.Count} animation(s)" );
				break;
			}

			if ( _animator == null )
				Log.Info( $"{modelPrefix}_isle: {animations[0].ChannelCount} channels match no mesh" );
		}
	}

	protected override void OnUpdate()
	{
		_animator?.Update( Time.Delta );
	}
}
