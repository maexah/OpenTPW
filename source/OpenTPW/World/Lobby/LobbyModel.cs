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
	/// Where each named node of the model sits, relative to that same origin.
	///
	/// A model's nodes are not all meshes. The ones that are not mark places rather than occupy them -
	/// a park gate says where its sound belongs with a node called "sound node" - and those are only
	/// reachable now that names are read. Keyed without regard to case, and trimmed, because the names
	/// were authored by hand and a few carry a trailing space.
	/// </summary>
	private readonly Dictionary<string, Vector3> _nodeOffsets = new( StringComparer.OrdinalIgnoreCase );

	/// <summary>The origin the model was loaded at - see <see cref="TryGetNode"/>.</summary>
	private readonly Vector3 _origin;

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
	/// <param name="sharedTextureDirectory">
	/// Where to look for a texture the model names but does not ship - see <see cref="LoadTexture"/>.
	/// Null means look nowhere else, which is what the lobby wants: its models carry their own art.
	/// </param>
	public LobbyModel( string modelPath, string textureDirectory, Vector3 origin, float scale = 1f,
		IReadOnlyDictionary<string, Texture>? textureOverrides = null,
		MaterialFlags materialFlags = MaterialFlags.None,
		string? sharedTextureDirectory = null )
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

			// Which of this mesh's materials had their texture swapped for one built at runtime. Only
			// signs are, and a sign is see-through whatever the model says - see MaterialFlagsFor.
			var substituted = new bool[16];

			for ( int i = 0; i < 16; ++i )
			{
				if ( mesh.Materials.Length <= i || string.IsNullOrEmpty( mesh.Materials[i].Name ) )
					textures.Add( Texture.Missing );
				else if ( textureOverrides != null && textureOverrides.TryGetValue( mesh.Materials[i].Name, out var overridden ) )
				{
					textures.Add( overridden );
					substituted[i] = true;
				}
				else
					textures.Add( LoadTexture( mesh.Materials[i].Name, textureDirectory, sharedTextureDirectory ) );
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
					MatFlags = MaterialFlagsFor( mesh, textures, substituted, (int)mesh.Vertices[i].TextureIndex )
				} );
			}

			meshVertices[meshIndex] = [.. vertices];
			models[meshIndex] = BuildModels( mesh, meshVertices[meshIndex], textures, substituted, materialFlags );

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

		_origin = origin;

		// The same composition and the same Y/Z swizzle the meshes above go through, so a node lands
		// in the world by the rule its model's geometry already landed by.
		foreach ( var node in modelFile.Nodes )
		{
			var name = node.Name.Trim();

			if ( name.Length == 0 )
				continue;

			var placed = node.WorldTransform * Matrix4x4.CreateScale( scale );

			// Assigned rather than added: nothing stops a model naming two nodes the same, and a
			// duplicate is not worth throwing a whole island away for.
			_nodeOffsets[name] = new Vector3( placed.M41, placed.M43, placed.M42 );
		}

		var animations = LoadAnimations( modelPath );

		if ( animations.Length > 0 )
		{
			Animators = BindVertexAnimations( modelPath, animations, modelFile, models, meshVertices );

			// Each mesh's own transform goes along with its composed one: a rotation key is the
			// orientation the mesh holds inside its parent, not the one it ends up with in the
			// model. See MeshRotator.BuildRestInverses.
			Rotator = BindRotationAnimations( modelPath, animations, Entities, _linearTransforms,
				[.. modelFile.Meshes.Select( mesh => ToWorldSpace( mesh.TransformMatrix ) )],
				Offsets, [.. modelFile.Meshes.Select( mesh => mesh.ParentIndex )] );
		}
	}

	/// <summary>
	/// One material's texture: the model's own folder first, then the theme's shared one.
	///
	/// <para>
	/// A buildable item ships only the art unique to it and takes the rest from its theme's
	/// <c>sharetex.wad</c>. A small toilet carries <c>J_wc2</c> and asks besides for <c>J_wc1</c>, a roof
	/// and a side panel, all three of which it shares with the go-karts and half the jungle. Without a
	/// second place to look, most of every item's surfaces draw the game's not-found texture - forty
	/// distinct ones across the eleven objects Lost Kingdom is built with, which is what the park looked
	/// like before this: sand-coloured boxes.
	/// </para>
	///
	/// <para>
	/// Whether a texture is there is asked of the file system rather than discovered by failing to load
	/// it, because <see cref="Texture"/> has a shipped answer for art it cannot find and warns as it uses
	/// it - so trying the item's own folder first and letting it fail would fill the log with exactly the
	/// warnings this removes. It asks <c>GetSize</c> rather than <c>FileExists</c> because that one does
	/// not look inside archives, and every one of these lives in a .wad.
	/// </para>
	///
	/// <para>
	/// It must resolve to a <i>path</i>, never a stream: <see cref="Texture"/> reads a .wct through
	/// Bullfrog's own decoder only on the path constructor, and its stream constructor hands the bytes to
	/// STB instead, which does not know the format.
	/// </para>
	/// </summary>
	private static Texture LoadTexture( string name, string directory, string? shared )
	{
		var path = $"{directory}/{name}.wct";

		if ( shared != null && !Exists( path ) && Exists( $"{shared}/{name}.wct" ) )
			path = $"{shared}/{name}.wct";

		return new Texture( path, TextureFlags.Repeat );
	}

	/// <summary>Whether the file system can offer this path at all - see <see cref="LoadTexture"/>.</summary>
	private static bool Exists( string path )
	{
		try
		{
			return FileSystem.GetSize( path ) > 0;
		}
		catch ( Exception )
		{
			return false;
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

	/// <summary>
	/// Shows or hides one mesh of this model, leaving the rest of it alone.
	///
	/// <para>
	/// The original hides a node by setting bit 0x10 in its flag word, which nothing in the game
	/// data ships with set - it is put there at runtime, mostly by an animation's visibility
	/// channel (see <see cref="AnimationFile.VisibilityTrack"/>). Its node walk tests that bit
	/// after computing the node's matrix and then carries straight on into the node's children, so
	/// a hidden mesh takes nothing else with it; a separate bit, 0x20, is what prunes a subtree.
	/// One entity per mesh means this behaves the same way without having to arrange it.
	/// </para>
	/// </summary>
	public void SetMeshVisible( int mesh, bool visible )
	{
		if ( mesh >= 0 && mesh < Entities.Length )
			Entities[mesh].Opacity = visible ? 1f : 0f;
	}

	/// <summary>
	/// Where the node called <paramref name="name"/> sits in the world, if this model has one.
	///
	/// This is the node's resting place, taken from the origin the model was loaded at. It does not
	/// follow <see cref="SetOrigin"/> or <see cref="SetTransform"/>, and it does not follow a mesh the
	/// animation is turning - only the mesh entities move, and a node has none. That is fine for what
	/// uses it: an island stands still, and the Space antenna's own emitter sits about two units off
	/// the axis it spins about, which against a listener seventy units away is under two degrees.
	/// </summary>
	public bool TryGetNode( string name, out Vector3 position )
	{
		if ( _nodeOffsets.TryGetValue( name.Trim(), out var offset ) )
		{
			position = offset + _origin;
			return true;
		}

		position = default;
		return false;
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
	/// Marks a vertex whose material wants the hard cut-out alpha reference rather than the low one.
	/// Not a bit the file uses - across all 839 static models only the flag word's low byte is ever
	/// set, so the high bits are ours. Must match FLAG_CUTOUT in content/shaders/test.shader.
	///
	/// Marked for cut-out art rather than for gradients, so the hard cut is only ever applied where
	/// a texture has positively been classified. test.shader is shared with the interface, and
	/// <see cref="UiMesh"/> marks every one of its own vertices see-through while having no texture
	/// classification to offer; flagged the other way round, the whole front end fell through to the
	/// cut-out reference and lost the soft edge off every button.
	/// </summary>
	private const uint CutOutAlphaFlag = 0x10000;

	/// <summary>
	/// The see-through bit of a material's flags - see <see cref="ModelFile.MaterialData.IsTranslucent"/>.
	/// </summary>
	private const uint TranslucentFlag = 0x2;

	/// <summary>
	/// The flag word a vertex carries: what the file gave its material, plus
	/// <see cref="CutOutAlphaFlag"/> when that material's texture turns out to be a cut-out mask
	/// rather than a real gradient - see <see cref="Texture.HasGradedAlpha"/>.
	///
	/// The original never reads the model's own see-through bit at draw time; it classifies from
	/// the texture's pixels alone. We keep the model's bit as the gate for whether a surface is
	/// see-through at all, because taking every texture's alpha at face value ate holes in
	/// geometry the game draws whole, and use the pixels only to choose between the two alpha
	/// references - which is the one call the original makes from them.
	/// </summary>
	private static uint MaterialFlagsFor( ModelFile.Mesh mesh, List<Texture> textures, bool[] substituted,
		int material )
	{
		var flags = mesh.Materials[material].Flags;

		// A substituted texture is a sign, and the original makes a sign see-through as it swaps the
		// texture in rather than trusting what the model was authored with: it ORs the bit into the
		// material at 0x00467d00 and 0x00467d60, alongside the marker saying which board this is. A
		// ride's board is authored solid, so without this the 61 signs that carry no artwork letter
		// their name onto black instead of onto the ride showing through behind them.
		//
		// It repairs the painted boards as well. Of the 23 that carry artwork, eight are shaped
		// rather than rectangular - the Bumper Cars, Candy Cabin, Cat Coaster, Ferris Wheel, Tour
		// Ride, both Coasters and the Drip - and reach alpha 0 across 5% to 43% of the board. Their
		// cut-out was being ignored and they were drawn as opaque rectangles. The other fifteen are
		// solid art whose only partly-clear texels are the 1.6% the .wct codec rings around a hard
		// edge, so nothing visible changes for them.
		if ( material < substituted.Length && substituted[material] )
			flags |= TranslucentFlag;

		return material < textures.Count && !textures[material].HasGradedAlpha
			? flags | CutOutAlphaFlag
			: flags;
	}

	/// <summary>
	/// Splits one mesh into its solid half and its see-through half, as two models over the same
	/// vertices - see <see cref="ModelFile.MaterialData.IsTranslucent"/> for which is which.
	///
	/// The split has to be by triangle rather than by mesh, because a mesh can be some of each:
	/// the Space island's antenna is a translucent dish and cone on a solid stalk, and its island
	/// is eight solid materials plus the shoreline ripple. The two halves now ask for the same
	/// pipeline state, as the original's do - it draws cut-out and graded art with identical
	/// render states and changes only the alpha reference - so what the split is still for is
	/// draw order: a graded surface has to blend over finished solid geometry, not into it.
	///
	/// Returns exactly two slots, solid then translucent, either of which may be null when the
	/// mesh turns out to be all of the other - which most meshes are.
	/// </summary>
	private static Model?[] BuildModels( ModelFile.Mesh mesh, Vertex[] vertices,
		List<Texture> textures, bool[] substituted, MaterialFlags materialFlags )
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

			// A substituted sign goes into the see-through half for the same reason it is given the
			// bit in MaterialFlagsFor - it has to blend over finished solid geometry rather than
			// into it, and its own ride is some of what it blends over.
			var seeThrough = material >= 0 && material < mesh.Materials.Length
				&& (mesh.Materials[material].IsTranslucent
					|| (material < substituted.Length && substituted[material]));

			var into = seeThrough ? translucent : solid;

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

		// Nothing in the game is one-sided: the original sets CULLMODE to D3DCULL_NONE once, at
		// 0x0056695c, and never writes that state again in the whole program. Foliage is authored
		// expecting it - a blade of grass is a single quad meant to be seen from behind as well as
		// in front, and a canopy is a dome that showed its dark inside when its near face was cut
		// away.
		//
		// This, rather than the depth change beside it, is what visibly repairs the scene. Frames
		// captured with the clock paused and compared against the branch point: the Fantasy blades
		// change by 5.0% and the Space canopies by 5.6%, both on a noise floor of 0.00%, against
		// 0.4% and 0.6% for the depth change measured the same way.
		var flags = materialFlags | MaterialFlags.DisableCulling;

		return [
			Build( solid, flags ),

			// Depth is written here exactly as it is for the solid half, because the original
			// writes it for every see-through surface too: none of the four state words its
			// texture classifier can produce sets bit 0x800, which is the only ZWRITEENABLE
			// control in the engine (FUN_00567620). The one thing that does turn depth writes off
			// is bit 0x2 of a MESH record's first dword, and across all 839 static models that is
			// set on five mesh records in the entire game, every one of them a 'heightfield' in a
			// base.md2 - none of which is drawn through here.
			//
			// A caller that fades its model still asks for DisableDepthWrite itself and keeps it:
			// a part-transparent surface that writes depth punches a hole through whatever comes
			// after it, which is why LobbyFlyer passes the flag in.
			Build( translucent, flags )
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
		ModelEntity[] entities, Matrix4x4[] baseTransforms, Matrix4x4[] localTransforms,
		Vector3[] offsets, int[] parentIndices )
	{
		if ( !MeshRotator.Drives( animations[0], entities.Length ) )
			return null;

		Log.Info( $"{modelPath}: rotating {animations[0].RotationTracks.Count} mesh(es) " +
			$"with {animations.Length} animation(s)" );

		return new MeshRotator( animations, entities, baseTransforms, localTransforms, offsets, parentIndices );
	}
}
