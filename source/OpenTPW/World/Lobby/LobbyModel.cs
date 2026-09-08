using System.Numerics;

namespace OpenTPW;

/// <summary>
/// Loads a lobby model: one <see cref="ModelEntity"/> per mesh, plus a <see cref="MeshAnimator"/>
/// bound to whichever mesh the model's animations drive, if it has any.
///
/// Animations sit beside the model with an M1, M2, ... suffix. A vertex animation drives
/// exactly one mesh, identified by its vertex count; a rotation animation turns whole meshes,
/// naming each one it drives. A model can have both - see <see cref="AnimationFile"/>.
/// </summary>
public sealed class LobbyModel
{
	public ModelEntity[] Entities { get; }

	/// <summary>Each mesh's placement relative to the model origin it was loaded at.</summary>
	public Vector3[] Offsets { get; }

	public MeshAnimator? Animator { get; }

	public MeshRotator? Rotator { get; }

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

			var rotation = ToWorldSpace( rot );
			_baseRotations[meshIndex] = rotation;

			Entities[meshIndex] = new ModelEntity()
			{
				Model = models[meshIndex],
				Scale = new Vector3( scl.X, scl.Z, scl.Y ),
				Rotation = rotation,
				Position = offset + origin,
			};
		}

		var animations = LoadAnimations( modelPath );

		if ( animations.Length > 0 )
		{
			Animator = BindVertexAnimations( modelPath, animations, modelFile, models, meshVertices );
			Rotator = BindRotationAnimations( modelPath, animations, Entities, _baseRotations );
		}
	}

	/// <summary>Advances whichever animations this model turned out to have.</summary>
	public void Update( float deltaTime )
	{
		Animator?.Update( deltaTime );
		Rotator?.Update( deltaTime );
	}

	/// <summary>
	/// Model space is Y-up and right-handed; the world we draw into swaps Y and Z, which flips
	/// handedness, so a rotation has to be conjugated as well as swizzled to survive the trip.
	/// This is the rotation half of the same mapping the mesh positions go through.
	/// </summary>
	public static Quaternion ToWorldSpace( Quaternion modelSpace )
		=> new( modelSpace.X, modelSpace.Z, modelSpace.Y, -modelSpace.W );

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

	private static AnimationFile[] LoadAnimations( string modelPath )
	{
		var withoutExtension = modelPath[..modelPath.LastIndexOf( '.' )];

		var animations = new List<AnimationFile>();
		for ( int suffix = 1; ; ++suffix )
		{
			if ( !AnimationFile.TryLoad( $"{withoutExtension}M{suffix}.md2", out var loaded ) || loaded == null )
				break;

			animations.Add( loaded );
		}

		return [.. animations];
	}

	private static MeshAnimator? BindVertexAnimations( string modelPath, AnimationFile[] animations,
		ModelFile modelFile, Model[] models, Vertex[][] meshVertices )
	{
		for ( int meshIndex = 0; meshIndex < modelFile.Meshes.Count; ++meshIndex )
		{
			var mesh = modelFile.Meshes[meshIndex];
			if ( !MeshAnimator.Drives( animations[0], mesh ) )
				continue;

			Log.Info( $"{modelPath}: animating '{mesh.Name.TrimEnd( '\0' )}' ({mesh.VertexCount} verts) " +
				$"with {animations.Length} animation(s)" );

			return new MeshAnimator( animations, mesh, models[meshIndex], meshVertices[meshIndex] );
		}

		return null;
	}

	private static MeshRotator? BindRotationAnimations( string modelPath, AnimationFile[] animations,
		ModelEntity[] entities, Quaternion[] baseRotations )
	{
		if ( !MeshRotator.Drives( animations[0], entities.Length ) )
			return null;

		Log.Info( $"{modelPath}: rotating {animations[0].RotationTracks.Count} mesh(es) " +
			$"with {animations.Length} animation(s)" );

		return new MeshRotator( animations, entities, baseRotations );
	}
}
