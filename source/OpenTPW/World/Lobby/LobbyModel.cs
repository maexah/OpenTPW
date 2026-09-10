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

	/// <summary>
	/// How far this model reaches from its own origin, after scaling - a loose bounding radius
	/// taken from the mesh bounds the animation decoder already relies on, so it covers every
	/// pose a vertex animation can put the model in rather than just its rest one.
	///
	/// Used to decide how close to the camera something can get before it has to fade, so that a
	/// bat and a butterfly fade over their own size rather than a shared guess.
	/// </summary>
	public float Radius { get; }

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
	/// <param name="textureOverrides">
	/// Textures to use in place of the .wct a material names, keyed by material name. The park
	/// signs are built at runtime rather than loaded from disk - see <see cref="SignTexture"/>.
	/// </param>
	/// <param name="materialFlags">
	/// Applied to every mesh's material. Anything that will be faded with <see cref="SetOpacity"/>
	/// wants <see cref="MaterialFlags.DisableDepthWrite"/> here, or it punches a hole through
	/// whatever is drawn after it for as long as it is part-transparent.
	/// </param>
	public LobbyModel( string modelPath, string textureDirectory, Vector3 origin, float scale = 1f,
		IReadOnlyDictionary<string, Texture>? textureOverrides = null,
		MaterialFlags materialFlags = MaterialFlags.None )
	{
		var modelFile = new ModelFile( modelPath );
		var meshCount = modelFile.Meshes.Count;

		Entities = new ModelEntity[meshCount];
		Offsets = new Vector3[meshCount];

		_linearTransforms = new Matrix4x4[meshCount];

		var models = new Model?[meshCount][];
		var meshVertices = new Vertex[meshCount][];

		for ( int meshIndex = 0; meshIndex < meshCount; ++meshIndex )
		{
			var mesh = modelFile.Meshes[meshIndex];
			var textures = new List<Texture>();

			for ( int i = 0; i < 16; ++i )
			{
				if ( mesh.Materials.Length <= i || string.IsNullOrEmpty( mesh.Materials[i].Name ) )
					textures.Add( Texture.Missing );
				else if ( textureOverrides != null && textureOverrides.TryGetValue( mesh.Materials[i].Name, out var overridden ) )
					textures.Add( overridden );
				else
					textures.Add( new Texture( $"{textureDirectory}/{mesh.Materials[i].Name}.wct", TextureFlags.Repeat ) );
			}

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

			meshVertices[meshIndex] = [.. vertices];
			models[meshIndex] = BuildModels( mesh, meshVertices[meshIndex], textures, materialFlags );

			// The mesh's place in the model's node tree, not just its own transform - a mesh
			// parented to a dummy node stores only its offset from that node. Right-multiplying
			// by a uniform scale here scales the translation too (not just the 3x3 part), so
			// this scales the whole model around its root rather than each mesh in place.
			var world = mesh.WorldTransform * Matrix4x4.CreateScale( scale );

			var offset = new Vector3( world.M41, world.M43, world.M42 );
			Offsets[meshIndex] = offset;

			// Furthest bound corner from the mesh's own origin, plus how far that origin sits
			// from the model's. Loose, but never under.
			var reach = MathF.Max( mesh.BoundsMin.Length, mesh.BoundsMax.Length ) * scale;
			Radius = MathF.Max( Radius, offset.Length + reach );

			_linearTransforms[meshIndex] = ToWorldSpace( world );

			Entities[meshIndex] = new ModelEntity()
			{
				Model = models[meshIndex][0],
				TranslucentModel = models[meshIndex][1],
				LinearTransform = _linearTransforms[meshIndex],
				Position = offset + origin,
			};
		}

		var animations = LoadAnimations( modelPath );

		if ( animations.Length > 0 )
		{
			Animators = BindVertexAnimations( modelPath, animations, modelFile, models, meshVertices );
			Rotator = BindRotationAnimations( modelPath, animations, Entities, _linearTransforms, Offsets,
				[.. modelFile.Meshes.Select( mesh => mesh.ParentIndex )] );
		}
	}

	/// <summary>Advances whichever animations this model turned out to have.</summary>
	public void Update( float deltaTime )
	{
		foreach ( var animator in Animators )
			animator.Update( deltaTime );

		Rotator?.Update( deltaTime );
	}

	/// <summary>Freezes every morph animation this model has on its rest pose - see <see cref="MeshAnimator.Pause"/>.</summary>
	public void Pause()
	{
		foreach ( var animator in Animators )
			animator.Pause();
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

	/// <summary>
	/// Fades the whole model, 0 to 1 - see <see cref="ModelEntity.Opacity"/>. At 0 none of its
	/// meshes are drawn at all.
	/// </summary>
	public void SetOpacity( float opacity )
	{
		foreach ( var entity in Entities )
			entity.Opacity = opacity;
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

			// The mesh's own orientation lives in its LinearTransform, so this only has to
			// carry the whole model's heading.
			Entities[i].Rotation = rotation;
		}
	}

	/// <summary>
	/// Splits one mesh into its solid half and its see-through half, as two models over the same
	/// vertices - see <see cref="ModelFile.MaterialData.IsTranslucent"/> for which is which.
	///
	/// The split has to be by triangle rather than by mesh, because a mesh can be some of each:
	/// the Space island's antenna is a translucent dish and cone on a solid stalk, and its island
	/// is eight solid materials plus the shoreline ripple. Whether depth is written is a property
	/// of the pipeline, so the two halves cannot share one.
	///
	/// Returns exactly two slots, solid then translucent, either of which may be null when the
	/// mesh turns out to be all of the other - which most meshes are.
	/// </summary>
	private static Model?[] BuildModels( ModelFile.Mesh mesh, Vertex[] vertices,
		List<Texture> textures, MaterialFlags materialFlags )
	{
		var solid = new List<uint>( mesh.Indices.Length );
		var translucent = new List<uint>();

		for ( int i = 0; i + 2 < mesh.Indices.Length; i += 3 )
		{
			// Every vertex of a triangle carries the same material - materials own contiguous runs
			// of the vertex order, so a triangle never straddles two - which makes the first
			// corner enough to place it.
			var corner = mesh.Indices[i];
			var material = corner < vertices.Length ? vertices[corner].TexIndex : 0;

			var into = material >= 0 && material < mesh.Materials.Length
				&& mesh.Materials[material].IsTranslucent ? translucent : solid;

			into.Add( mesh.Indices[i] );
			into.Add( mesh.Indices[i + 1] );
			into.Add( mesh.Indices[i + 2] );
		}

		Model? Build( List<uint> indices, MaterialFlags flags )
		{
			if ( indices.Count == 0 )
				return null;

			var material = new Material<ObjectUniformBuffer>( "content/shaders/test.shader", flags );
			material.Set( "Color", [.. textures] );

			return new Model( vertices, [.. indices], material );
		}

		return [
			Build( solid, materialFlags ),

			// A see-through surface that writes depth punches a hole through whatever is drawn
			// after it, which is the whole reason these are separated out.
			Build( translucent, materialFlags | MaterialFlags.DisableDepthWrite )
		];
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
		ModelFile modelFile, Model?[][] models, Vertex[][] meshVertices )
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

			// Both halves of a split mesh share one vertex array, so both need the animator's
			// writes - the Space island is animated and split, and so is every shoreline ripple.
			animators[i] = new MeshAnimator( animations, target, mesh,
				[.. models[target].OfType<Model>()], meshVertices[target] );
		}

		var names = string.Join( ", ", targets.Select( t => $"'{modelFile.Meshes[t].Name.TrimEnd( '\0' )}'" ) );
		Log.Info( $"{modelPath}: animating {targets.Length} mesh(es) - {names} - " +
			$"with {animations.Length} animation(s)" );

		return animators;
	}

	private static MeshRotator? BindRotationAnimations( string modelPath, AnimationFile[] animations,
		ModelEntity[] entities, Matrix4x4[] baseTransforms, Vector3[] offsets, int[] parentIndices )
	{
		if ( !MeshRotator.Drives( animations[0], entities.Length ) )
			return null;

		Log.Info( $"{modelPath}: rotating {animations[0].RotationTracks.Count} mesh(es) " +
			$"with {animations.Length} animation(s)" );

		return new MeshRotator( animations, entities, baseTransforms, offsets, parentIndices );
	}
}
