using System.Numerics;

namespace OpenTPW;

/// <summary>
/// Loads a lobby model: one <see cref="ModelEntity"/> per mesh, plus a <see cref="MeshAnimator"/>
/// bound to whichever mesh the model's animations drive, if it has any.
///
/// Animations sit beside the model with an M1, M2, ... suffix and each drives exactly one mesh,
/// identified by its vertex count - see <see cref="AnimationFile"/>.
/// </summary>
public sealed class LobbyModel
{
	public ModelEntity[] Entities { get; }

	/// <summary>Each mesh's placement relative to the model origin it was loaded at.</summary>
	public Vector3[] Offsets { get; }

	public MeshAnimator? Animator { get; }

	private readonly Quaternion[] _baseRotations;

	public LobbyModel( string modelPath, string textureDirectory, Vector3 origin )
	{
		var modelFile = new ModelFile( modelPath );
		var meshCount = modelFile.Meshes.Count;

		Entities = new ModelEntity[meshCount];
		Offsets = new Vector3[meshCount];

		_baseRotations = new Quaternion[meshCount];

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
					textures.Add( Texture.Missing );
				else
					textures.Add( new Texture( $"{textureDirectory}/{mesh.Materials[i].Name}.wct", TextureFlags.Repeat ) );
			}

			material.Set( "Color", [.. textures] );

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

			models[meshIndex] = new Model( [.. vertices], mesh.Indices, material );
			meshVertices[meshIndex] = [.. vertices];

			Matrix4x4.Decompose( mesh.TransformMatrix, out var scl, out var rot, out var pos );

			var offset = new Vector3( pos.X, pos.Z, pos.Y );
			Offsets[meshIndex] = offset;

			var rotation = new Quaternion( rot.X, rot.Z, rot.Y, -rot.W );
			_baseRotations[meshIndex] = rotation;

			Entities[meshIndex] = new ModelEntity()
			{
				Model = models[meshIndex],
				Scale = new Vector3( scl.X, scl.Z, scl.Y ),
				Rotation = rotation,
				Position = offset + origin,
			};
		}

		Animator = LoadAnimations( modelPath, modelFile, models, meshVertices );
	}

	/// <summary>Moves every mesh of this model, keeping their relative placement.</summary>
	public void SetOrigin( Vector3 origin )
	{
		for ( int i = 0; i < Entities.Length; ++i )
			Entities[i].Position = Offsets[i] + origin;
	}

	/// <summary>Moves and turns the whole model about its origin.</summary>
	public void SetTransform( Vector3 origin, Quaternion rotation )
	{
		for ( int i = 0; i < Entities.Length; ++i )
		{
			var offset = System.Numerics.Vector3.Transform( Offsets[i].GetSystemVector3(), rotation );

			Entities[i].Position = (Vector3)offset + origin;
			Entities[i].Rotation = rotation * _baseRotations[i];
		}
	}

	private static MeshAnimator? LoadAnimations( string modelPath, ModelFile modelFile, Model[] models, Vertex[][] meshVertices )
	{
		var withoutExtension = modelPath[..modelPath.LastIndexOf( '.' )];

		var animations = new List<AnimationFile>();
		for ( int suffix = 1; ; ++suffix )
		{
			if ( !AnimationFile.TryLoad( $"{withoutExtension}M{suffix}.md2", out var loaded ) || loaded == null )
				break;

			animations.Add( loaded );
		}

		if ( animations.Count == 0 )
			return null;

		for ( int meshIndex = 0; meshIndex < modelFile.Meshes.Count; ++meshIndex )
		{
			var mesh = modelFile.Meshes[meshIndex];
			if ( !MeshAnimator.Drives( animations[0], mesh ) )
				continue;

			Log.Info( $"{modelPath}: animating '{mesh.Name.TrimEnd( '\0' )}' ({mesh.VertexCount} verts) " +
				$"with {animations.Count} animation(s)" );

			return new MeshAnimator( [.. animations], mesh, models[meshIndex], meshVertices[meshIndex] );
		}

		Log.Info( $"{modelPath}: {animations[0].ChannelCount} animation channels match no mesh" );
		return null;
	}
}
