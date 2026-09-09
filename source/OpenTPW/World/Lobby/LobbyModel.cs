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

	/// <summary>One per mesh this model's animations morph - a model can morph several.</summary>
	public MeshAnimator[] Animators { get; } = Array.Empty<MeshAnimator>();

	public MeshRotator? Rotator { get; }

	// Each mesh's orientation, scale and any shear, already converted to world space. Kept
	// separate from Position so an animation can turn a mesh without disturbing it.
	private readonly Matrix4x4[] _linearTransforms;

	/// <param name="scale">
	/// Uniform scale applied to the whole model, including each mesh's offset from the model's
	/// own root - so a mesh further from the root moves closer to it too, rather than just
	/// shrinking in place. Baked into <see cref="_linearTransforms"/> at load time.
	/// </param>
	public LobbyModel( string modelPath, string textureDirectory, Vector3 origin, float scale = 1f )
	{
		var modelFile = new ModelFile( modelPath );
		var meshCount = modelFile.Meshes.Count;

		Entities = new ModelEntity[meshCount];
		Offsets = new Vector3[meshCount];

		_linearTransforms = new Matrix4x4[meshCount];

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

			// The mesh's place in the model's node tree, not just its own transform - a mesh
			// parented to a dummy node stores only its offset from that node. Right-multiplying
			// by a uniform scale here scales the translation too (not just the 3x3 part), so
			// this scales the whole model around its root rather than each mesh in place.
			var world = mesh.WorldTransform * Matrix4x4.CreateScale( scale );

			var offset = new Vector3( world.M41, world.M43, world.M42 );
			Offsets[meshIndex] = offset;

			_linearTransforms[meshIndex] = ToWorldSpace( world );

			Entities[meshIndex] = new ModelEntity()
			{
				Model = models[meshIndex],
				LinearTransform = _linearTransforms[meshIndex],
				Position = offset + origin,
			};
		}

		var animations = LoadAnimations( modelPath );

		if ( animations.Length > 0 )
		{
			Animators = BindVertexAnimations( modelPath, animations, modelFile, models, meshVertices );
			Rotator = BindRotationAnimations( modelPath, animations, Entities, _linearTransforms );
		}
	}

	/// <summary>Advances whichever animations this model turned out to have.</summary>
	public void Update( float deltaTime )
	{
		foreach ( var animator in Animators )
			animator.Update( deltaTime );

		Rotator?.Update( deltaTime );
	}

	/// <summary>
	/// Model space is Y-up; the world we draw into swaps Y and Z, which is the same swizzle the
	/// mesh positions go through. Conjugating the matrix by that swap converts its rotation,
	/// scale and shear together - unlike decomposing to a TRS, which silently drops the shear
	/// and skewed the jungle island's tallest palm trunk sideways by five units.
	///
	/// Only the linear part is converted; the translation is swizzled by the caller.
	/// </summary>
	public static Matrix4x4 ToWorldSpace( Matrix4x4 modelSpace )
		=> new(
			modelSpace.M11, modelSpace.M13, modelSpace.M12, 0,
			modelSpace.M31, modelSpace.M33, modelSpace.M32, 0,
			modelSpace.M21, modelSpace.M23, modelSpace.M22, 0,
			0, 0, 0, 1 );

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

			// The mesh's own orientation lives in its LinearTransform, so this only has to
			// carry the whole model's heading.
			Entities[i].Rotation = rotation;
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

	/// <summary>
	/// One animator per mesh any of these animations morphs or scrolls. The animation names its target
	/// mesh outright, which matters when several meshes share a vertex count - ratrace morphs
	/// three 64-vertex meshes that are otherwise indistinguishable.
	/// </summary>
	private static MeshAnimator[] BindVertexAnimations( string modelPath, AnimationFile[] animations,
		ModelFile modelFile, Model[] models, Vertex[][] meshVertices )
	{
		var targets = animations
			.SelectMany( animation => animation.MorphTracks
				.Where( t => t.TargetIndex >= 0 && t.TargetIndex < modelFile.Meshes.Count
					&& t.ChannelCount == modelFile.Meshes[t.TargetIndex].VertexCount + 2 )
				.Select( t => t.TargetIndex )
				.Concat( animation.UvTracks.Select( t => t.TargetIndex ) ) )
			.Where( index => index >= 0 && index < modelFile.Meshes.Count )
			.Distinct()
			.OrderBy( index => index )
			.ToArray();

		if ( targets.Length == 0 )
			return Array.Empty<MeshAnimator>();

		var animators = new MeshAnimator[targets.Length];

		for ( int i = 0; i < targets.Length; ++i )
		{
			var target = targets[i];
			var mesh = modelFile.Meshes[target];

			animators[i] = new MeshAnimator( animations, target, mesh, models[target], meshVertices[target] );
		}

		var names = string.Join( ", ", targets.Select( t => $"'{modelFile.Meshes[t].Name.TrimEnd( '\0' )}'" ) );
		Log.Info( $"{modelPath}: animating {targets.Length} mesh(es) - {names} - " +
			$"with {animations.Length} animation(s)" );

		return animators;
	}

	private static MeshRotator? BindRotationAnimations( string modelPath, AnimationFile[] animations,
		ModelEntity[] entities, Matrix4x4[] baseTransforms )
	{
		if ( !MeshRotator.Drives( animations[0], entities.Length ) )
			return null;

		Log.Info( $"{modelPath}: rotating {animations[0].RotationTracks.Count} mesh(es) " +
			$"with {animations.Length} animation(s)" );

		return new MeshRotator( animations, entities, baseTransforms );
	}
}
