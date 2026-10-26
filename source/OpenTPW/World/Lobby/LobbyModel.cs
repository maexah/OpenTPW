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
public sealed class LobbyModel : IChannelPosed
{
	public ModelEntity[] Entities { get; }

	/// <summary>Each mesh's placement relative to the model origin it was loaded at.</summary>
	public Vector3[] Offsets { get; }

	/// <summary>
	/// Where each named node of the model sits, relative to that same origin.
	///
	/// A model's nodes are not all meshes. The ones that are not mark places rather than occupy them -
	/// a park gate says where its sound belongs with a node called "sound node" - and those are only
	/// reachable by name. Keyed without regard to case, and trimmed, because the names
	/// were authored by hand and a few carry a trailing space.
	/// </summary>
	private readonly Dictionary<string, Vector3> _nodeOffsets = new( StringComparer.OrdinalIgnoreCase );

	// The file, for TryGetDrawnNode.
	private readonly ModelFile _file;
	internal ModelFile Source => _file;

	/// <summary>The origin the model was loaded at - see <see cref="TryGetNode"/>.</summary>
	private readonly Vector3 _origin;

	// Each node's parent, so a change of clip finds the meshes under the nodes it moved.
	private readonly int[] _nodeParents = Array.Empty<int>();

	// The model's own path, for the log.
	private readonly string _modelPath;

	// Whether the model's header word 0x30 carries 0x4: a clip's keys are composed with each node's own (the coaster parts).
	private readonly bool _additive;

	/// <summary>
	/// How far this model reaches from its own origin, after scaling - a loose bounding radius
	/// taken from the mesh bounds the animation decoder already relies on, so it covers every
	/// pose a vertex animation can put the model in rather than just its rest one.
	///
	/// Used to decide how close to the camera something can get before it has to fade, so that a
	/// bat and a butterfly fade over their own size rather than a shared guess.
	/// </summary>
	public float Radius { get; }

	/// <summary>
	/// The box this model's meshes occupy, in its own space, about its own origin.
	///
	/// <para>
	/// <b><see cref="Radius"/> is not a substitute for it, and using one where the other belongs is a
	/// real mistake.</b> Radius is a distance from the ORIGIN, so a model whose origin sits off to one
	/// side - or which carries one far-flung mesh among eight - reports a radius far larger than the
	/// bulk of it, and anything sized by that shrinks to nothing while sitting off-centre. A box gives
	/// a CENTRE as well as a size, which is what the engine itself fits a preview by: it reads the
	/// model's box and takes <c>(max + min) / 2</c> and <c>max - min</c> (<c>FUN_004689f0</c>).
	/// </para>
	/// </summary>
	public Vector3 BoundsMin { get; private set; } = new( float.MaxValue, float.MaxValue, float.MaxValue );

	public Vector3 BoundsMax { get; private set; } = new( float.MinValue, float.MinValue, float.MinValue );

	/// <summary>Whether any mesh contributed to <see cref="BoundsMin"/> - false for a model with none.</summary>
	/// Dead by CODE: nothing calls it.
	public bool HasBounds => BoundsMax.X >= BoundsMin.X;

	/// <summary>
	/// Each mesh's own box, in the same order as <see cref="Entities"/>, about the model's origin.
	///
	/// <para>
	/// <b>The whole model's box is not a substitute when only some of it is drawn.</b> A ride hides
	/// its building meshes once it is up (<c>ParkObjects.PoseAsBuilt</c>), and those still sit in
	/// <see cref="BoundsMin"/> - so a caller framing the model by the whole box aims below what is
	/// actually visible and leaves it riding high. Boxing only the meshes being drawn needs their
	/// bounds one at a time, which is what this is for.
	/// </para>
	/// </summary>
	/// Dead by CODE: nothing calls it.
	public IReadOnlyList<(Vector3 Low, Vector3 High)> MeshBoxes => _meshBoxes;

	private readonly List<(Vector3 Low, Vector3 High)> _meshBoxes = [];

	/// <summary>One per mesh this model's animations morph - a model can morph several.</summary>
	public MeshAnimator[] Animators { get; } = Array.Empty<MeshAnimator>();

	public MeshRotator? Rotator { get; }

	/// <summary>
	/// Poses the whole node tree for a clip that moves nodes about - see <see cref="NodePoser"/>. Made by the first such
	/// clip posed (<see cref="Poser"/>), so a model nobody poses through <see cref="Pose"/> makes none: the advisor's,
	/// which <see cref="AdvisorModel"/> poses itself.
	/// </summary>
	private NodePoser? _poser;

	/// <summary>The scale the model was loaded at, which a posed node's place is built with as its rest one was.</summary>
	private readonly float _scale;

	/// <summary>
	/// The clips this model plays, in the order the engine probes them - M1, M2, and so on.
	///
	/// <para>
	/// Kept so that a caller can sequence them itself through <see cref="Pose"/>, rather than looping
	/// through them with <see cref="Update"/>. <see cref="LobbyGate"/> is the one that does: its gate idles
	/// shut, plays M1 as the park-entry flight homes onto it, and plays M2 when Escape cancels the flight,
	/// queued behind M1 if that is still playing.
	/// </para>
	/// </summary>
	public IReadOnlyList<AnimationFile> Clips { get; } = Array.Empty<AnimationFile>();

	/// <summary>
	/// The routes this model carries, in the model's own space - empty for all but twenty-four of the
	/// game's models. The bus, the ferry and the seaplane each have one; the haunted house has four.
	///
	/// <para>
	/// Held for the same reason as <see cref="Clips"/>: what drives a thing along one of these is its
	/// script, which reads a per-frame scalar out of an animation and samples the route with it, so
	/// both halves have to be reachable from the thing rather than from the file.
	/// </para>
	/// </summary>
	public IReadOnlyList<ModelFile.ModelPath> Paths { get; } = Array.Empty<ModelFile.ModelPath>();

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
	/// <param name="clips">
	/// The animations to bind players for, or null to probe for this model's own numbered run.
	///
	/// <para>
	/// A thing standing in a park is handed the clips its twelve roles carry, because the animation
	/// player driving it may name any of them and a player has to exist for a mesh before that mesh can
	/// be posed. Probing instead would bind against role 5 alone, read through the loader that turns away
	/// every clip carrying position and visibility only - and four of the eleven things Lost Kingdom
	/// places, three of them meant to move for ever, would bind their bare M clip at most and never their C clip. See
	/// <see cref="RideAnimations.AllClips"/>.
	/// </para>
	/// </param>
	/// <param name="opaqueFrames">
	/// The frames whose materials the original's loader sets <c>0x800</c> on, drawn opaque whatever their texture's
	/// alpha (<see cref="ProceduralBase"/>), or null for a model it loads without flag <c>0x2000</c>.
	/// </param>
	/// <param name="frameTextures">
	/// Textures to draw in place of a frame's own, keyed by <see cref="ModelFile.MaterialData.FrameOffset"/>: a built
	/// thing's floor blended into the ground's tiles (<see cref="ProceduralFloor"/>), each the model's own, as the
	/// original's model keeps its own frame table. Null for none.
	/// </param>
	/// <param name="vertexNormals">
	/// The normal each mesh's vertex is lit by, given the mesh and the vertex, in place of the loader's: the terrain's
	/// meshes on land outside the park (<see cref="TerrainHalfLight"/>). Null for the loader's own.
	/// </param>
	public LobbyModel( string modelPath, string textureDirectory, Vector3 origin, float scale = 1f,
		IReadOnlyDictionary<string, Texture>? textureOverrides = null,
		MaterialFlags materialFlags = MaterialFlags.None,
		string? sharedTextureDirectory = null,
		IReadOnlyList<AnimationFile>? clips = null,
		Func<ModelFile, IReadOnlySet<uint>>? opaqueFrames = null,
		Func<ModelFile, IReadOnlyDictionary<uint, Texture>>? frameTextures = null,
		Func<ModelFile.Mesh, int, Vector3>? vertexNormals = null )
	{
		var modelFile = new ModelFile( modelPath );
		_file = modelFile;
		_modelPath = modelPath;
		_additive = (modelFile.HeaderFlags & 0x4) != 0;
		var opaque = opaqueFrames?.Invoke( modelFile ) ?? NoFrames;
		var framed = frameTextures?.Invoke( modelFile );
		var meshCount = modelFile.Meshes.Count;

		var supplied = clips is { Count: > 0 };
		var animations = supplied ? clips!.ToArray() : LoadAnimations( modelPath );
		var wrapping = WrapFrames( modelFile, animations );

		Entities = new ModelEntity[meshCount];
		Offsets = new Vector3[meshCount];

		_linearTransforms = new Matrix4x4[meshCount];

		var models = new Model?[meshCount][];
		var meshVertices = new Vertex[meshCount][];

		// The materials whose texture's alpha is drawn solid by 0x800, for the log below.
		var alphaMadeOpaque = new List<string>();

		for ( int meshIndex = 0; meshIndex < meshCount; ++meshIndex )
		{
			var mesh = modelFile.Meshes[meshIndex];
			var textures = new List<Texture>();

			// Which of this mesh's materials had their texture swapped for one built at runtime. Only
			// signs are, and a sign is see-through whatever the model says - see MaterialFlagsFor.
			var substituted = new bool[Material.TextureSlots];

			for ( int i = 0; i < Material.TextureSlots; ++i )
			{
				if ( mesh.Materials.Length <= i || string.IsNullOrEmpty( mesh.Materials[i].Name ) )
					textures.Add( Texture.Missing );
				else if ( framed != null && framed.TryGetValue( mesh.Materials[i].FrameOffset, out var blended ) )
					textures.Add( blended );
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
					Normal = vertexNormals?.Invoke( mesh, i ) ?? mesh.Normals[i],
					TexCoords = mesh.TexCoords[i],
					TexIndex = (int)mesh.Vertices[i].TextureIndex,
					MatFlags = MaterialFlagsFor( mesh, textures, substituted, (int)mesh.Vertices[i].TextureIndex, opaque, wrapping )
				} );
			}

			meshVertices[meshIndex] = [.. vertices];
			models[meshIndex] = BuildModels( mesh, meshVertices[meshIndex], textures, substituted, materialFlags, opaque );

			for ( int i = 0; i < mesh.Materials.Length && i < textures.Count; ++i )
			{
				if ( !string.IsNullOrEmpty( mesh.Materials[i].Name ) && !substituted[i] )
					NoteTextureWay( textures[i], Wraps( mesh.Materials[i], wrapping ) );

				if ( !string.IsNullOrEmpty( mesh.Materials[i].Name ) && !substituted[i]
					&& textures[i].Alpha != TextureAlpha.Opaque && MadeOpaque( mesh.Materials[i], opaque ) )
					alphaMadeOpaque.Add( $"{mesh.Name.TrimEnd( '\0' )}:{mesh.Materials[i].Name} {textures[i].Alpha}" );
			}

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

			// And the box this mesh's OWN bounds make about its place.
			//
			// <b>Not the isotropic +/- reach the radius uses.</b> reach is the distance to the furthest
			// bounding CORNER, so a box built from it pads a mesh at the model's origin by that much in every
			// direction, and a preview sized by it draws the thing far too small.
			//
			// <b>The mesh's own orientation is applied, by carrying all eight corners through the very
			// transform its geometry is drawn with.</b> Leaving it out does not merely loosen the box,
			// it MOVES it, and a box that is off by a constant gives a centre that is off by a constant -
			// the centre a ride preview spins about.
			//
			// The corners go in swizzled but UNSCALED: a mesh's bounds are model space, Y-up, while this
			// box lives in the world's Z-up, and `world` already folded the scale into the linear part.
			var linear = ToWorldSpace( world );
			var low = new Vector3( float.MaxValue, float.MaxValue, float.MaxValue );
			var high = new Vector3( float.MinValue, float.MinValue, float.MinValue );

			for ( var corner = 0; corner < 8; ++corner )
			{
				var pick = new Vector3(
					(corner & 1) == 0 ? mesh.BoundsMin.X : mesh.BoundsMax.X,
					(corner & 2) == 0 ? mesh.BoundsMin.Z : mesh.BoundsMax.Z,
					(corner & 4) == 0 ? mesh.BoundsMin.Y : mesh.BoundsMax.Y );

				var at = (Vector3)System.Numerics.Vector3.Transform( pick.GetSystemVector3(), linear ) + offset;

				low = new Vector3( MathF.Min( low.X, at.X ), MathF.Min( low.Y, at.Y ), MathF.Min( low.Z, at.Z ) );
				high = new Vector3( MathF.Max( high.X, at.X ), MathF.Max( high.Y, at.Y ), MathF.Max( high.Z, at.Z ) );
			}

			// Kept per mesh as well as folded into the whole - see MeshBoxes for why a caller that
			// draws only some of the meshes cannot use the whole model's box.
			_meshBoxes.Add( (low, high) );

			BoundsMin = new Vector3(
				MathF.Min( BoundsMin.X, low.X ),
				MathF.Min( BoundsMin.Y, low.Y ),
				MathF.Min( BoundsMin.Z, low.Z ) );

			BoundsMax = new Vector3(
				MathF.Max( BoundsMax.X, high.X ),
				MathF.Max( BoundsMax.Y, high.Y ),
				MathF.Max( BoundsMax.Z, high.Z ) );

			_linearTransforms[meshIndex] = linear;

			Entities[meshIndex] = new ModelEntity()
			{
				Model = models[meshIndex][0],
				TranslucentModel = models[meshIndex][1],
				LinearTransform = _linearTransforms[meshIndex],
				Position = offset + origin,
			};
		}

		if ( opaqueFrames != null || alphaMadeOpaque.Count > 0 )
			Log.Info( $"{modelPath}: {opaque.Count} frame(s) set 0x800, {alphaMadeOpaque.Count} material(s) whose texture's alpha is drawn opaque - {string.Join( ", ", alphaMadeOpaque )}" );

		_origin = origin;
		_placedOrigin = origin;

		// The same composition and the same Y/Z swizzle the meshes above go through, so a node lands
		// in the world by the rule its model's geometry already landed by.
		for ( var index = 0; index < modelFile.Nodes.Count; ++index )
		{
			var node = modelFile.Nodes[index];
			var name = node.Name.Trim();

			if ( name.Length == 0 )
				continue;


			var placed = node.WorldTransform * Matrix4x4.CreateScale( scale );

			// Assigned rather than added: nothing stops a model naming two nodes the same, and a
			// duplicate is not worth throwing a whole island away for.
			_nodeOffsets[name] = new Vector3( placed.M41, placed.M43, placed.M42 );
		}

		Clips = animations;
		Paths = modelFile.Paths;
		_nodeParents = [.. modelFile.Nodes.Select( node => node.ParentIndex )];

		_scale = scale;

		if ( animations.Length > 0 )
		{
			Animators = BindVertexAnimations( modelPath, animations, modelFile, models, meshVertices );

			// Each mesh's own transform goes along with its composed one: a rotation key is the
			// orientation the mesh holds inside its parent, not the one it ends up with in the
			// model. See MeshRotator.BuildRestInverses.
			Rotator = BindRotationAnimations( modelPath, animations, supplied, Entities, _linearTransforms,
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
	/// distinct ones across the eleven objects Lost Kingdom is built with.
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
		=> new( TexturePath( name, directory, shared ), TextureFlags.Repeat );

	/// <summary>Where a material's texture is read from: the model's own folder, else the shared one where only it has the file.</summary>
	internal static string TexturePath( string name, string directory, string? shared )
	{
		var path = $"{directory}/{name}.wct";

		if ( shared != null && !Exists( path ) && Exists( $"{shared}/{name}.wct" ) )
			path = $"{shared}/{name}.wct";

		return path;
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
	/// scale and shear together - unlike decomposing to a TRS, which silently drops the shear.
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

	/// <summary>
	/// Where a node stands now, rather than where it stood when the model was loaded - the same
	/// composition <see cref="Place"/> gives every mesh, so a node lands by the rule the geometry
	/// around it lands by.
	/// </summary>
	/// <remarks>
	/// <b>This exists because <see cref="TryGetNode"/> is the wrong answer for anything the park
	/// places.</b> That one adds <c>_origin</c>, the origin the model was LOADED at, and a park object
	/// is loaded at <see cref="Vector3.Zero"/> and only afterwards moved with
	/// <see cref="SetTransform"/>. So it would hand back an offset about the world origin and ignore
	/// the thing's heading entirely - which happens to look like a plain offset and is not one. A ride
	/// turned 90 degrees would have its riders drawn in the wrong place, and nothing about the number
	/// would say so.
	/// </remarks>
	public bool TryGetPlacedNode( string name, out Vector3 position )
	{
		if ( !_nodeOffsets.TryGetValue( name.Trim(), out var offset ) )
		{
			position = default;
			return false;
		}

		var turned = System.Numerics.Vector3.Transform( offset.GetSystemVector3(), _placedRotation );

		position = (Vector3)turned + _placedOrigin;

		return true;
	}

	/// <summary>
	/// Where a node stands as the model is drawn this frame, rather than at rest as <see cref="TryGetPlacedNode"/> answers:
	/// a node anchored on a face of its parent mesh stands on that face while a morph poses the mesh (the engine's pose
	/// walk, <c>FUN_0044ab90</c>, <c>0x0044abf2</c>, and <c>FUN_0044b040</c>), and any other node goes where its nearest
	/// mesh ancestor is drawn, as it rests in that mesh. Answers which of the two it took.
	/// </summary>
	/// <remarks>
	/// The second is OpenTPW's: the engine stores every node's posed matrix, where this answers a node that is not a mesh
	/// from its rest place inside its nearest mesh ancestor as drawn, so a clip that turns or moves such a node itself
	/// (bouncy's <c>camera</c>, the volcano's <c>lavarock1</c>-<c>5</c>, the lookout's <c>Dummy01</c>) does not carry it
	/// here, though <see cref="NodePoser"/> carries the meshes under it.
	/// </remarks>
	public DrawnNode TryGetDrawnNode( int index, out Vector3 position )
	{
		position = default;

		if ( index < 0 || index >= _file.Nodes.Count )
			return DrawnNode.Missing;

		var node = _file.Nodes[index];
		var parent = node.ParentIndex;

		if ( node.Face is { } face && (node.IdFlags & 0x40040) != 0 && parent >= 0 && parent < Entities.Length
			&& Animators.FirstOrDefault( animator => animator.TargetIndex == parent ) is { Morphing: true } animator
			&& ModelFile.PointOnFace( _file.Meshes[parent], face, animator.SourcePosition ) is { } local )
		{
			position = (Vector3)System.Numerics.Vector3.Transform( Swizzle( local.GetSystemVector3() ), Entities[parent].ModelMatrix );
			return DrawnNode.OnAFace;
		}

		if ( !DrawnInMesh( _file, Entities, index, out position ) )
			return TryGetPlacedNode( node.Name, out position ) ? DrawnNode.AtRest : DrawnNode.Missing;

		return DrawnNode.OnAMesh;
	}

	/// <summary>
	/// Where node <paramref name="index"/> is drawn as it rests inside its nearest mesh ancestor, as
	/// <paramref name="entities"/> draw that mesh; false where it has none.
	/// </summary>
	internal static bool DrawnInMesh( ModelFile file, ModelEntity[] entities, int index, out Vector3 position )
	{
		position = default;

		var node = file.Nodes[index];

		// The nearest ancestor that is a mesh: the first meshes are the first nodes. Guarded against a parent chain that loops.
		var mesh = node.ParentIndex;

		for ( var guard = file.Nodes.Count; mesh >= entities.Length && guard > 0; --guard )
			mesh = file.Nodes[mesh].ParentIndex;

		if ( mesh < 0 || mesh >= entities.Length || !Matrix4x4.Invert( file.Meshes[mesh].WorldTransform, out var unplaced ) )
			return false;

		var within = System.Numerics.Vector3.Transform( node.WorldTransform.Translation, unplaced );

		position = (Vector3)System.Numerics.Vector3.Transform( Swizzle( within ), entities[mesh].ModelMatrix );
		return true;
	}

	/// <summary>A point in the file's axes (y up) as a mesh's vertices are built, z up.</summary>
	private static System.Numerics.Vector3 Swizzle( System.Numerics.Vector3 file ) => new( file.X, file.Z, file.Y );

	/// <summary>Where this model was last put - see <see cref="SetTransform"/>.</summary>
	/// <remarks>
	/// Not <c>_origin</c>, which is where it was LOADED: a park object loads at the world origin and is
	/// moved afterwards, so the loaded one is nought for everything the park places. Same distinction
	/// <see cref="TryGetPlacedNode"/> turns on.
	/// </remarks>
	public Vector3 PlacedOrigin => _placedOrigin;

	/// <summary>How this model was last turned - see <see cref="SetTransform"/>.</summary>
	public Quaternion PlacedRotation => _placedRotation;

	/// <summary>Moves every mesh of this model, keeping their relative placement.</summary>
	/// Dead by CODE: nothing calls it.
	public void SetOrigin( Vector3 origin )
	{
		_placedOrigin = origin;
		_placedRotation = Quaternion.Identity;

		Place();
	}

	/// <summary>Moves and turns the whole model about its origin.</summary>
	public void SetTransform( Vector3 origin, Quaternion rotation )
	{
		_placedOrigin = origin;
		_placedRotation = rotation;

		Place();

		// The mesh's own orientation lives in its LinearTransform, so this only has to
		// carry the whole model's heading.
		foreach ( var entity in Entities )
			entity.Rotation = rotation;
	}

	// Where this model was last put. Kept because an animation moves meshes about, and putting one back
	// means putting it where the model was placed rather than where it was loaded - see Rest.
	private Vector3 _placedOrigin;
	private Quaternion _placedRotation = Quaternion.Identity;

	/// <summary>Every mesh at the offset it was built with, about wherever the model was last put.</summary>
	private void Place() => Place( Entities, Offsets, _placedRotation, _placedOrigin );

	/// <summary>
	/// Each mesh <paramref name="only"/> marks (every one where it is null) at the offset it was built with, turned by
	/// <paramref name="rotation"/> about <paramref name="origin"/>.
	/// </summary>
	internal static void Place( ModelEntity[] entities, Vector3[] offsets, Quaternion rotation, Vector3 origin, bool[]? only = null )
	{
		for ( int i = 0; i < entities.Length; ++i )
		{
			if ( only != null && (i >= only.Length || !only[i]) )
				continue;

			var offset = System.Numerics.Vector3.Transform( offsets[i].GetSystemVector3(), rotation );

			entities[i].Position = (Vector3)offset + origin;
		}
	}

	/// <summary>
	/// Where node <paramref name="node"/> stands in the model's own axes as the node tree last posed it, or null where no
	/// clip has posed the tree: for the debug console's <c>vehicles</c>.
	/// </summary>
	internal Matrix4x4? Posed( int node ) => _poser != null && node >= 0 && node < _poser.Count ? _poser.World( node ) : null;

	/// <summary>The node tree's poser, made by the first clip that moves a node (<see cref="NodePoser.Drives"/>).</summary>
	private NodePoser Poser()
	{
		if ( _poser == null )
		{
			_poser = new NodePoser( _file );

			Log.Info( $"{_modelPath}: posing {Clips.Count( NodePoser.Drives )} of {Clips.Count} clip(s) over the node tree, " +
				$"{Clips.Sum( clip => clip.PositionTracks.Count )} position track(s), {Clips.Sum( clip => clip.PathTracks.Count )} route track(s)" );
		}

		return _poser;
	}

	/// <summary>
	/// Each mesh <paramref name="only"/> marks (every one where it is null) where <paramref name="poser"/> last put its
	/// node: its turn as <see cref="ToWorldSpace"/> converts it at <paramref name="scale"/>, and its place swizzled, turned
	/// by <paramref name="rotation"/> about <paramref name="origin"/>, as <see cref="Place"/> puts the rest pose.
	/// </summary>
	internal static void PlacePosed( ModelEntity[] entities, NodePoser poser, float scale, Quaternion rotation, Vector3 origin,
		bool[]? only = null )
	{
		var scaling = Matrix4x4.CreateScale( scale );

		for ( int i = 0; i < entities.Length && i < poser.Count; ++i )
		{
			if ( only != null && (i >= only.Length || !only[i]) )
				continue;

			var world = poser.World( i ) * scaling;
			var offset = System.Numerics.Vector3.Transform( Swizzle( world.Translation ), rotation );

			entities[i].LinearTransform = ToWorldSpace( world );
			entities[i].Position = (Vector3)offset + origin;
		}
	}

	/// <summary>
	/// The meshes a change from <paramref name="outgoing"/> puts back: each node it turns, moves or routes that is a mesh,
	/// and every mesh under one, of the first <paramref name="meshCount"/> nodes (<c>FUN_00472310</c> restores the outgoing
	/// clip's nodes alone, <c>0x0047244b</c>, and the pose walk composes what hangs off them again).
	/// </summary>
	internal static bool[] MeshesRested( AnimationFile outgoing, IReadOnlyList<int> parents, int meshCount )
	{
		var driven = new HashSet<int>( outgoing.RotationTracks.Select( track => track.TargetIndex )
			.Concat( outgoing.PositionTracks.Select( track => track.TargetIndex ) )
			.Concat( outgoing.PathTracks.Select( track => track.TargetIndex ) ) );

		var rested = new bool[meshCount];

		for ( var mesh = 0; mesh < meshCount; ++mesh )
		{
			// Up the chain from the mesh itself. The guard is against a parent chain that loops back on itself.
			for ( int at = mesh, guard = parents.Count; at >= 0 && at < parents.Count && guard >= 0; at = parents[at], --guard )
			{
				if ( driven.Contains( at ) )
				{
					rested[mesh] = true;
					break;
				}
			}
		}

		return rested;
	}

	/// <summary>
	/// Shows <paramref name="animation"/> at <paramref name="frame"/> and leaves it there, without any
	/// clock of this model's own - for a caller holding an animation player that says which clip is
	/// running and how far into it the model has reached.
	///
	/// <para>
	/// <b>One clip, every kind of track it carries.</b> The engine poses a clip by walking its track list
	/// once and dispatching on each track's kind (<c>FUN_004721f0</c> into <c>FUN_00471860</c>), so the
	/// turning, the morphing, the scrolling and the switching on and off of a model all come from the same
	/// clip at the same frame. One clock for both halves is what keeps a model's rotation and its morph
	/// on the same clip.
	/// </para>
	///
	/// <para>
	/// <b>A clip carrying position or route tracks is posed by <see cref="NodePoser"/></b>, rotation, position and route
	/// together over the whole node tree, so a node a key or a route moves carries what hangs off it, turned by its parents
	/// (the End's planes and their propellers, the seaplane's propellers as it turns along its route); every other clip
	/// turns its meshes through <see cref="Rotator"/>.
	/// </para>
	/// </summary>
	public void Pose( AnimationFile animation, float frame )
	{
		if ( NodePoser.Drives( animation ) )
		{
			// Every mesh from the one tree. Not the original's, whose tree holds every channel's turns: a clip that only
			// turns, on a channel posed before this one, is drawn at rest until it is posed again, the next frame. No Lost
			// Kingdom model plays one on a channel below a clip that moves nodes (the Inca Totem's are channels 1 to 3).
			var poser = Poser();

			poser.Pose( animation, frame );
			PlacePosed( Entities, poser, _scale, _placedRotation, _placedOrigin );
		}
		else
		{
			// FUN_00470d10 composes an additive model's rotation key with the node's own; not decoded, and MeshRotator would
			// replace the turn. The 49 such clips are the coaster pylons' and their kin's, and no source loads a pylon model.
			if ( _additive && animation.RotationTracks.Count > 0 )
				Unimplemented.Report( "ANIM_ADDITIVE_ROTATION" );

			Rotator?.Pose( animation, frame );
		}

		// A scale track sets a node's axis lengths after its turn (Anim_PoseTrack 0x00471d8d); only the advisor poses them
		// (AdvisorModel.Pose). 128 of the game's 644 are in Lost Kingdom's wads: 11 the advisor's, which AdvisorModel poses, and 117 in its shops, rides, sideshows and features.
		if ( animation.ScaleTracks.Count > 0 )
			Unimplemented.Report( "ANIM_SCALE_TRACK" );

		// Held at whatever the last entry before this frame said, and left alone entirely before the
		// first - which is what VisibleAt answers null for, and what the engine does by simply not
		// writing the node's flag.
		foreach ( var track in animation.VisibilityTracks )
		{
			if ( track.VisibleAt( frame ) is bool visible )
				SetMeshVisible( track.TargetIndex, visible );
		}

		foreach ( var animator in Animators )
			animator.Pose( animation, frame );
	}

	/// <summary>
	/// Where a percentage along <paramref name="route"/> puts a thing, in the route's own space (<c>Anim_PoseTrack</c>,
	/// <c>0x00471f5b</c>..<c>0x00472037</c>; <c>docs/exe/park.md</c>, "Clip content").
	///
	/// <para>
	/// The percentage is taken plus 1000, modulo 100, and times the route's segments and 0.01; the whole part is the
	/// segment and the rest how far into it. A Bezier route has a segment for each three points and a straight one a
	/// segment for each point, <b>less one unless the route is closed</b> (type bit <c>0x1</c>), so an open route's last
	/// segment ends on its point 3n-3 and never runs back to point 0: the bus's 45 points make 14 segments and the
	/// seaplane's and the ferry's 33 make 10.
	/// </para>
	/// </summary>
	internal static System.Numerics.Vector3 SampleRoute( ModelFile.ModelPath route, float percent )
	{
		if ( !RouteSegment( route, percent, out var points, out var b, out var t ) )
			return System.Numerics.Vector3.Zero;

		// Route_SampleStraight, the straight lerp; only space's slide is a straight route. Not the original's: its sampler
		// has no wrap (docs/exe/park.md, "Route_SampleStraight"), where this wraps point b + 1 on a closed straight route;
		// no model ships one, so nothing reaches it.
		if ( !route.IsBezier )
			return System.Numerics.Vector3.Lerp( points[b], points[(b + 1) % points.Length], t );

		// Route_SampleBezier, the cubic Bezier over points 3k to 3k + 3, which wraps on a closed route.
		var (p0, p1, p2, p3) = RouteWindow( points, b );
		var u = 1f - t;

		return (p0 * (u * u * u)) + (p1 * (3f * u * u * t))
			+ (p2 * (3f * u * t * t)) + (p3 * (t * t * t));
	}

	/// <summary>
	/// Which way the route runs at a percentage along it, unscaled: the Bezier's first derivative
	/// (<c>Route_BezierTangent</c>), or a straight route's step from one point to the next (<c>Route_StraightTangent</c>). What a
	/// route key's facing (<c>0x400</c>) turns a node by (<see cref="NodePoser"/>).
	/// </summary>
	internal static System.Numerics.Vector3 RouteTangent( ModelFile.ModelPath route, float percent )
	{
		if ( !RouteSegment( route, percent, out var points, out var b, out var t ) )
			return System.Numerics.Vector3.Zero;

		if ( !route.IsBezier )
			return points[(b + 1) % points.Length] - points[b];

		var (p0, p1, p2, p3) = RouteWindow( points, b );

		return (3f * (p1 - p0)) + (6f * t * (p0 - (2f * p1) + p2)) + (3f * t * t * ((3f * p1) - p0 - (3f * p2) + p3));
	}

	/// <summary>
	/// The segment a percentage falls in, as its first point's index, and how far into it. False for a route with no
	/// points, or a Bezier one too short for a segment.
	/// </summary>
	private static bool RouteSegment( ModelFile.ModelPath route, float percent, out System.Numerics.Vector3[] points,
		out int first, out float t )
	{
		// The points are this project's own Vector3, not System.Numerics' - a model file's are, where an animation
		// file's position track spells System.Numerics out - so they are converted rather than mixed.
		points = [.. route.Points.Select( point => point.GetSystemVector3() )];
		first = 0;
		t = 0f;

		var segments = route.IsBezier ? points.Length / 3 : points.Length;

		if ( (route.Type & 0x1) == 0 )
			--segments;

		if ( segments <= 0 )
			return false;

		// 0x006febf0 and 0x006febf8: -1000 and 100. Below -1000 the engine's remainder is negative and so is its
		// segment; ours is wrapped back onto the route. No shipped key is below -0.02.
		var wrapped = (float)((percent + 1000.0) % 100.0);

		if ( wrapped < 0f )
			wrapped += 100f;

		var travelled = wrapped * 0.01f * segments;
		var segment = Math.Min( (int)travelled, segments - 1 );

		t = travelled - segment;
		first = route.IsBezier ? 3 * segment : segment;

		return true;
	}

	/// <summary>A Bezier segment's four points from <paramref name="first"/>, wrapped round the route.</summary>
	private static (System.Numerics.Vector3, System.Numerics.Vector3, System.Numerics.Vector3, System.Numerics.Vector3)
		RouteWindow( System.Numerics.Vector3[] points, int first )
		=> (points[first % points.Length], points[(first + 1) % points.Length],
			points[(first + 2) % points.Length], points[(first + 3) % points.Length]);

	/// <summary>
	/// Puts back what <paramref name="outgoing"/> moved, which the engine's start does unless its caller passed <c>0x4</c>
	/// or <c>0x8</c> (<c>FUN_00472f60</c>) - see <see cref="MeshRotator.Rest"/> for the mask that decides how much of it -
	/// and shows again what the restore shows (<see cref="ShownAtRest"/>): the seaplane's take-off hides it, and its next
	/// landing shows it again. The incoming clip's hide list is the bind's, <see cref="Hide"/>.
	/// </summary>
	/// <param name="incoming">
	/// The clip about to play, whose own channels the engine leaves as <paramref name="outgoing"/> left them - see
	/// <see cref="NodePoser.Rest"/>.
	/// </param>
	/// <remarks>
	/// The caller, <see cref="RideAnimations.PoseChannel"/>, rests at a start it has not seen, by the start's flags; a
	/// queued clip's promotion, which the engine binds with <c>0x8</c> added off the screen (<c>0x00473874</c>), is bound
	/// here as in view, and a loop's restart, which the engine binds in view (<c>0x00473848</c>), shows and hides nothing
	/// here (Q661).
	/// </remarks>
	public void Rest( AnimationFile outgoing, AnimationFile? incoming = null )
	{
		Rotator?.Rest( outgoing );

		foreach ( var animator in Animators )
			animator.Rest();

		RestMeshes( Entities, Offsets, _nodeParents, _poser, _scale, _placedRotation, _placedOrigin, outgoing, incoming );

		foreach ( var (node, visible) in ShownAtRest( outgoing, node => IsTransformOnly( _file, node ) ) )
			SetMeshVisible( node, visible );
	}

	/// <summary>
	/// Hides <paramref name="incoming"/>'s hide list, as the engine's start binds it unless its caller passed <c>0x8</c>
	/// (<c>FUN_00472d70</c>, <c>0x00473117</c>), on an idle channel too: a bought Fries Shop's construction puts its mesh 10 away.
	/// </summary>
	public void Hide( AnimationFile incoming )
	{
		foreach ( var (node, visible) in HiddenAtBind( incoming, node => IsTransformOnly( _file, node ) ) )
			SetMeshVisible( node, visible );
	}

	/// <summary>Whether a node is transform-only (its file flags hold <c>0x200</c>), which no hide list or restore touches.</summary>
	internal static bool IsTransformOnly( ModelFile file, int node ) => node >= 0 && node < file.Nodes.Count && (file.Nodes[node].Flags & 0x200) != 0;

	/// <summary>
	/// What a change of clip by an unflagged start does to which nodes are hidden, in the order the engine does it
	/// (<c>FUN_00472f60</c>): the restore's showing (<see cref="ShownAtRest"/>), then the bind's hiding
	/// (<see cref="HiddenAtBind"/>). Each node with whether it is left shown.
	/// </summary>
	internal static IEnumerable<(int Node, bool Visible)> ShownAtChange( AnimationFile outgoing, AnimationFile? incoming,
		Func<int, bool> transformOnly )
		=> ShownAtRest( outgoing, transformOnly ).Concat( incoming is null ? [] : HiddenAtBind( incoming, transformOnly ) );

	/// <summary>
	/// The restore's showing: every node <paramref name="outgoing"/> has a track of any kind for and every node of its hide
	/// list is shown again (<c>Anim_RestoreOutgoing</c>: the tracks' clear at <c>0x00472486</c>, the list's at
	/// <c>0x00472401</c>); a transform-only node is spared (<c>docs/exe/park.md</c>, "The per-clip hide list").
	/// </summary>
	internal static IEnumerable<(int Node, bool Visible)> ShownAtRest( AnimationFile outgoing, Func<int, bool> transformOnly )
	{
		foreach ( var node in outgoing.Tracks.Select( track => track.Node ).Concat( outgoing.HideList.Select( node => (int)node ) ) )
		{
			if ( !transformOnly( node ) )
				yield return (node, true);
		}
	}

	/// <summary>The bind's hiding: every node of <paramref name="incoming"/>'s hide list (<c>FUN_00472d70</c>) but a transform-only one.</summary>
	internal static IEnumerable<(int Node, bool Visible)> HiddenAtBind( AnimationFile incoming, Func<int, bool> transformOnly )
	{
		foreach ( int node in incoming.HideList )
		{
			if ( !transformOnly( node ) )
				yield return (node, false);
		}
	}

	/// <summary>
	/// The places half of <see cref="Rest"/>: the node tree's nodes <paramref name="outgoing"/> moved put back, all but
	/// those <paramref name="incoming"/> moves (<see cref="NodePoser.Rest"/>), and then only the meshes it moved and what
	/// hangs off them placed again (<see cref="MeshesRested"/>), from the tree where there is one and from the rest
	/// offsets where there is none, as the engine puts back only the outgoing clip's nodes: what a clip on another channel
	/// moved stays.
	/// </summary>
	internal static void RestMeshes( ModelEntity[] entities, Vector3[] offsets, IReadOnlyList<int> nodeParents, NodePoser? poser,
		float scale, Quaternion rotation, Vector3 origin, AnimationFile outgoing, AnimationFile? incoming )
	{
		poser?.Rest( outgoing, incoming );

		var rested = MeshesRested( outgoing, nodeParents, entities.Length );

		if ( poser != null )
			PlacePosed( entities, poser, scale, rotation, origin, rested );
		else
			Place( entities, offsets, rotation, origin, rested );
	}

	/// <summary>
	/// Marks a vertex whose material wants the hard cut-out alpha reference rather than the low one.
	/// Not a bit the file uses - across all 839 static models only the flag word's low byte is ever
	/// set, so the high bits are ours. Must match FLAG_CUTOUT in content/shaders/test.shader.
	///
	/// Marked for cut-out art rather than for gradients, so the hard cut is only ever applied where
	/// a texture says so. test.shader is shared with the interface, and <see cref="UiMesh"/> marks
	/// every one of its own vertices see-through while handing no texture's alpha on; flagged the
	/// other way round, the whole front end would fall through to the cut-out reference and lose the
	/// soft edge off every button.
	/// </summary>
	private const uint CutOutAlphaFlag = 0x10000;

	/// <summary>
	/// The shader's see-through bit, test.shader's FLAG_TRANSLUCENT, and the file's bit of the same value
	/// (<see cref="ModelFile.MaterialData.IsTranslucent"/>): set where <see cref="SeeThrough"/> says, cleared elsewhere.
	/// </summary>
	private const uint TranslucentFlag = 0x2;

	private static readonly IReadOnlySet<uint> NoFrames = new HashSet<uint>();

	/// <summary>The material word's bit the original draws opaque whatever the texture holds (<c>FUN_00581790</c>).</summary>
	private const uint OpaqueBit = 0x800;

	/// <summary>Whether a material carries <c>0x800</c>: its file's word, or its frame set by the loader.</summary>
	private static bool MadeOpaque( ModelFile.MaterialData material, IReadOnlySet<uint> opaque )
		=> (material.Flags & OpaqueBit) != 0 || opaque.Contains( material.FrameOffset );

	/// <summary>
	/// Marks a vertex whose texture is sampled clamped to its edge. Must match FLAG_CLAMP in content/shaders/test.shader;
	/// a high bit of ours, as <see cref="CutOutAlphaFlag"/> is.
	/// </summary>
	internal const uint ClampFlag = 0x20000;

	/// <summary>
	/// Marks a vertex drawn white, its texture at its own brightness, whatever the light: its material is unlit
	/// (<see cref="ModelFile.MaterialData.IsUnlit"/>, <c>DrawList_AddMaterialFaces</c> <c>0x00580530</c>). Must match
	/// FLAG_UNLIT in content/shaders/test.shader; a high bit of ours, as <see cref="CutOutAlphaFlag"/> is.
	/// </summary>
	internal const uint UnlitFlag = 0x80000;

	/// <summary>
	/// A frame's wrap bits: <c>0x10</c> ORs <c>0x10000</c> into each of its textures' flags, <c>0x20</c>
	/// <c>0x20000</c> (<c>FUN_00575e10</c>), and either one sets the stage's ADDRESS to WRAP where every other
	/// texture is CLAMPed (<c>FUN_00581790</c>, <c>0x005678cf</c>; docs/exe/render-states.md).
	/// </summary>
	internal const uint WrapBits = 0x30;

	/// <summary>
	/// The frames whose textures wrap: those the file gives a wrap bit, and every frame a material of a mesh with a
	/// UV track names, which the loader ORs <c>0x8030</c> into before the textures load (<c>0x004626fb</c>).
	/// </summary>
	internal static IReadOnlySet<uint> WrapFrames( ModelFile modelFile, IEnumerable<AnimationFile> animations )
	{
		var scrolled = animations
			.SelectMany( animation => animation.UvTracks.Select( track => track.TargetIndex ) )
			.Where( index => index >= 0 && index < modelFile.Meshes.Count )
			.ToHashSet();

		var frames = new HashSet<uint>();

		for ( int m = 0; m < modelFile.Meshes.Count; ++m )
		{
			foreach ( var material in modelFile.Meshes[m].Materials )
			{
				if ( material.FrameOffset == 0 || ((material.Flags & WrapBits) == 0 && !scrolled.Contains( m )) )
					continue;

				if ( frames.Add( material.FrameOffset ) && (material.Flags & WrapBits) == 0 )
					Log.Info( $"{modelFile.Meshes[m].Name.TrimEnd( '\0' )}:{material.Name} wraps by its mesh's UV track alone" );
			}
		}

		return frames;
	}

	/// <summary>Whether a material's texture wraps: its frame is one of <see cref="WrapFrames"/>, or its own word has a wrap bit.</summary>
	internal static bool Wraps( ModelFile.MaterialData material, IReadOnlySet<uint> wrapping )
		=> (material.Flags & WrapBits) != 0 || wrapping.Contains( material.FrameOffset );

	/// <summary>Each texture path loaded so far, and whether a material sampled it wrapped (bit 1) or clamped (bit 2).</summary>
	private static readonly Dictionary<string, int> TextureWays = new( StringComparer.OrdinalIgnoreCase );

	/// <summary>
	/// Counts a texture one model wraps and another clamps. The original keeps one texture object per folder and name
	/// (<c>FUN_00575870</c>) and ORs the wrap bits into it for good, so there every user of such a texture wraps while
	/// it is loaded; here each material keeps its own way (<c>TEXTURE_WRAP_SHARED</c>).
	/// </summary>
	private static void NoteTextureWay( Texture texture, bool wraps )
	{
		if ( string.IsNullOrEmpty( texture.Path ) )
			return;

		lock ( TextureWays )
		{
			TextureWays.TryGetValue( texture.Path, out var ways );
			var now = ways | (wraps ? 1 : 2);
			TextureWays[texture.Path] = now;

			if ( now == 3 && ways != 3 )
			{
				Log.Info( $"{texture.Path}: wrapped by one model and clamped by another" );
				Unimplemented.Report( "TEXTURE_WRAP_SHARED" );
			}
		}
	}

	/// <summary>
	/// Whether a material's alpha is tested and blended: its texture has an alpha (<see cref="Texture.Alpha"/>) and the
	/// material is not <c>0x800</c>, as the original's state word gives it (<c>FUN_00581790</c>; docs/exe/render-states.md):
	/// the material's <c>0x2</c> plays no part. The loader sets <c>0x800</c> on a built thing's floor, whose clear border
	/// is painted grass (<see cref="ProceduralBase"/>). A substituted texture is a sign, see-through whatever it holds.
	/// </summary>
	internal static bool SeeThrough( ModelFile.Mesh mesh, List<Texture> textures, bool[] substituted, int material,
		IReadOnlySet<uint>? opaque = null )
	{
		if ( material < 0 || material >= textures.Count || material >= mesh.Materials.Length )
			return false;

		if ( material < substituted.Length && substituted[material] )
			return true;

		if ( textures[material].Alpha == TextureAlpha.Opaque )
			return false;

		return !MadeOpaque( mesh.Materials[material], opaque ?? NoFrames );
	}

	/// <summary>
	/// The flags a model's materials draw with: the caller's, culled, as the original draws a model. Its CULLMODE stays
	/// none because it culls every model face itself, keeping those whose screen corners wind clockwise
	/// (<c>Face_KeptByWinding</c>, <c>0x00581320</c>; <c>render-states.md</c>, "The software back-face test").
	/// A face seen from both sides is authored twice, turned opposite ways and lit each way: of the Sun God's brazier
	/// plates the lit tops are drawn from above, not the dark undersides that come first in the file.
	/// </summary>
	internal static MaterialFlags DrawFlags( MaterialFlags asked ) => asked & ~MaterialFlags.DisableCulling;

	/// <summary>
	/// The flag word a vertex carries: what the file gave its material with the see-through bit taken from
	/// <see cref="SeeThrough"/>, plus <see cref="CutOutAlphaFlag"/> where its texture's alpha is not graded,
	/// <see cref="ClampFlag"/> where its texture does not wrap (<see cref="Wraps"/>) and <see cref="UnlitFlag"/> where the
	/// material is drawn unlit.
	/// </summary>
	internal static uint MaterialFlagsFor( ModelFile.Mesh mesh, List<Texture> textures, bool[] substituted,
		int material, IReadOnlySet<uint>? opaque = null, IReadOnlySet<uint>? wrapping = null )
	{
		var flags = mesh.Materials[material].Flags & ~TranslucentFlag;

		if ( mesh.Materials[material].IsUnlit )
			flags |= UnlitFlag;

		if ( !Wraps( mesh.Materials[material], wrapping ?? NoFrames ) )
			flags |= ClampFlag;

		if ( !SeeThrough( mesh, textures, substituted, material, opaque ) )
			return flags;

		flags |= TranslucentFlag;

		// A substituted texture is a sign, and the original makes a sign see-through as it swaps the
		// texture in rather than trusting what the model was authored with: it ORs the bit into the
		// material at 0x00467d00 and 0x00467d60, alongside the marker saying which board this is. A
		// ride's board is authored solid, so without this the 61 signs that carry no artwork letter
		// their name onto black instead of onto the ride showing through behind them.
		//
		// It keeps the painted boards' cut-outs as well. Of the 23 that carry artwork, eight are shaped
		// rather than rectangular - the Bumper Cars, Candy Cabin, Cat Coaster, Ferris Wheel, Tour
		// Ride, both Coasters and the Drip - and reach alpha 0 across 5% to 43% of the board; without
		// the bit they draw as opaque rectangles. The other fifteen are
		// solid art whose only partly-clear texels are the 1.6% the .wct codec rings around a hard
		// edge, so nothing visible changes for them.
		return textures[material].Alpha != TextureAlpha.Graded
			? flags | CutOutAlphaFlag
			: flags;
	}

	/// <summary>
	/// Splits one mesh into its solid half and its see-through half, as two models over the same
	/// vertices - see <see cref="SeeThrough"/> for which is which.
	///
	/// The split has to be by triangle rather than by mesh, because a mesh can be some of each:
	/// the Space island's antenna is a translucent dish and cone on a solid stalk, and its island
	/// is eight solid materials plus the shoreline ripple. The two halves ask for the same
	/// pipeline state, as the original's do - it draws cut-out and graded art with identical
	/// render states and changes only the alpha reference - so what the split is still for is
	/// draw order: a graded surface has to blend over finished solid geometry, not into it.
	///
	/// Returns exactly two slots, solid then translucent, either of which may be null when the
	/// mesh turns out to be all of the other - which most meshes are.
	/// </summary>
	private static Model?[] BuildModels( ModelFile.Mesh mesh, Vertex[] vertices,
		List<Texture> textures, bool[] substituted, MaterialFlags materialFlags, IReadOnlySet<uint> opaque )
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
			var seeThrough = SeeThrough( mesh, textures, substituted, material, opaque );

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

			// Wrap, and the clamped twin Material.Set binds beside it for the vertices carrying ClampFlag.
			material.Set( "Color", [.. textures] );

			return new Model( vertices, [.. indices], material );
		}

		var flags = DrawFlags( materialFlags );

		return [
			Build( solid, flags ),

			// Depth is written here exactly as it is for the solid half, because the original
			// writes it for every see-through surface too: none of the four state words its
			// texture classifier can produce sets bit 0x800, which is the only ZWRITEENABLE
			// control in the engine (FUN_00567620). The one thing that does turn depth writes off
			// is bit 0x2 of a MESH record's first dword, and across all 839 static models that is
			// set on five mesh records in the entire game, every one of them an empty 'heightfield' record
			// in a base.md2, so nothing drawn here has it.
			//
			// A caller that fades its model still asks for DisableDepthWrite itself and keeps it:
			// a part-transparent surface that writes depth punches a hole through whatever comes
			// after it, which is why LobbyFlyer passes the flag in.
			Build( translucent, flags )
		];
	}

	/// <summary>
	/// The clips a model's M role ships: the numbered run first, and the bare <c>{stem}M.md2</c> only
	/// where that run found nothing at all.
	///
	/// <para>
	/// <b>The bare file is not a stand-in for a missing clip - it is the other way the same role is
	/// shipped</b>, and the engine reads both. <c>FUN_00461f10</c> is the loader that probes a role, which
	/// its own two format strings settle: <c>'%s%s%c%d.md2'</c> at <c>0x004623b3</c> for the numbered run
	/// and <c>'%s%s%c.md2'</c> at <c>0x004623df</c> for the bare file. <b>197 of the game's 445 base models
	/// ship only the bare one</b>.
	/// </para>
	///
	/// <para>
	/// <b>Only where the numbered run came back empty</b>, because one archive in the game separates the
	/// two readings: <c>jungle/mamfount</c> ships <c>mamfountm.md2</c> <i>and</i> <c>mamfountm1/m2.md2</c>,
	/// and the engine loads the numbered pair and never reaches the bare file. Taking both would give it a
	/// third clip the original never plays. <see cref="RideAnimations"/> reads a role under exactly this
	/// rule already.
	/// </para>
	///
	/// <para>
	/// <b>These still go through <see cref="AnimationFile.TryLoad"/></b>, unlike the clips
	/// <see cref="RideAnimations"/> reads straight, and that is measured rather than assumed: of the 197
	/// bare-only clips, <b>not one</b> carries position and visibility alone, so the check that rejects
	/// those never fires here. 160 carry a rotation, morph or UV track, and the remaining 37 are empty -
	/// no track of any kind - so they load and move nothing, the same shape as the two <c>lights.RSE</c>
	/// clips that declare ten frames and drive nothing.
	/// </para>
	/// </summary>
	/// <remarks>
	/// Internal rather than private only so that it can be tested: a <see cref="LobbyModel"/> cannot be
	/// built without a graphics device, and asserting the shipped files instead would pass whether or not
	/// the fallback below is there at all.
	/// </remarks>
	internal static AnimationFile[] LoadAnimations( string modelPath )
	{
		var withoutExtension = modelPath[..modelPath.LastIndexOf( '.' )];

		var animations = new List<AnimationFile>();

		// Stopping at the first gap is what the engine does, which makes a gap invisible rather than
		// skipped. No shipped archive has one.
		for ( int suffix = 1; ; ++suffix )
		{
			if ( !AnimationFile.TryLoad( $"{withoutExtension}M{suffix}.md2", out var loaded ) || loaded == null )
				break;

			animations.Add( loaded );
		}

		if ( animations.Count == 0
			&& AnimationFile.TryLoad( $"{withoutExtension}M.md2", out var bare ) && bare != null )
			animations.Add( bare );

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

	/// <param name="anyClip">
	/// Whether to look past the first clip for one that turns a mesh.
	///
	/// <para>
	/// A model probing for its own run is asked about its first clip only, which is what the lobby's gates
	/// and islands are tuned against. A model handed a thing's twelve
	/// roles is asked about all of them, because there is no first: a channel names a role outright, and a
	/// model whose turning lives in a role other than the one that happens to sort first would otherwise
	/// get no rotator at all and stand still whatever its script did.
	/// </para>
	/// </param>
	private static MeshRotator? BindRotationAnimations( string modelPath, AnimationFile[] animations,
		bool anyClip, ModelEntity[] entities, Matrix4x4[] baseTransforms, Matrix4x4[] localTransforms,
		Vector3[] offsets, int[] parentIndices )
	{
		var drives = anyClip
			? animations.Any( animation => MeshRotator.Drives( animation, entities.Length ) )
			: MeshRotator.Drives( animations[0], entities.Length );

		if ( !drives )
			return null;

		Log.Info( $"{modelPath}: rotating {animations[0].RotationTracks.Count} mesh(es) " +
			$"with {animations.Length} animation(s)" );

		return new MeshRotator( animations, entities, baseTransforms, localTransforms, offsets, parentIndices );
	}
}

/// <summary>How <see cref="LobbyModel.TryGetDrawnNode"/> found a node.</summary>
public enum DrawnNode
{
	/// <summary>No node of that name.</summary>
	Missing,

	/// <summary>On a face of its parent mesh, as a morph poses it.</summary>
	OnAFace,

	/// <summary>Where its nearest mesh ancestor is drawn.</summary>
	OnAMesh,

	/// <summary>At rest: it hangs from no mesh.</summary>
	AtRest
}
