using System.Numerics;

namespace OpenTPW;

/// <summary>
/// What the park was built with: the shops, rides, sideshows and scenery standing on its ground, read
/// out of the park's own save file.
///
/// <para>
/// Lost Kingdom ships with eleven of them - a drinks shop, a belly bounce, a jungle spray, three small
/// toilets, a staff room, two security cameras, a litter bin and a round fountain - the things that
/// make it a park somebody laid out.
/// </para>
///
/// <para>
/// <b>Where they are is the whole difficulty, and it is not in any of the obvious places.</b> The save
/// is a serialised memory image - it stores live heap pointers verbatim - so nothing in it can be found
/// by searching, and the positions are reached only by walking the World block from its start. See
/// <see cref="ParkWorld"/>, which does that walking and proves it by ending exactly on the next module's
/// tag.
/// </para>
///
/// <para>
/// The park's <i>fixed</i> items - the gate, the traffic lights and the bus - are in that same list and
/// are skipped here. They carry no position at all, because theirs are baked into their models:
/// see <see cref="ParkFixedItems"/>, which loads them.
/// </para>
/// </summary>
public sealed class ParkObjects : Entity
{
	/// <summary>The theme folder these were loaded from - "jungle", "fantasy", "hallow" or "space".</summary>
	public string ThemeName { get; }

	private readonly List<LobbyModel> _models = [];
	private readonly Dictionary<int, ParkRideHoarding> _hoardings = [];
	private readonly Dictionary<int, SavedThing> _savedHoardings = [];
	internal IEnumerable<string> HoardingCensus() => _hoardings.OrderBy( p => p.Key ).Select( p => p.Value.Census );

	/// <summary>
	/// What one placed thing is made of: the model standing in the park, the twelve animation roles its
	/// own archive ships, and the clip its player last posed.
	///
	/// <para>
	/// The engine keeps all three on the one thing - the model at <c>+0x08</c>, the role table from
	/// <c>+0x1c</c>, the animation players at <c>+0x10</c> - which is why they are held together here
	/// rather than a model in one entity and its clips in another. <see cref="ParkRides"/> reaches the
	/// roles through <see cref="AnimationsFor"/>, so a thing's script and its model are looking at the
	/// same player rather than at two readings of the same files.
	/// </para>
	/// </summary>
	private sealed class Standing( LobbyModel model, RideAnimations animations, int catalogueId,
		IReadOnlyDictionary<string, Texture>? sign = null )
	{
		/// <summary>The name board painted for it, or null where it has none - what its window's preview wears too.</summary>
		public IReadOnlyDictionary<string, Texture>? Sign { get; } = sign;

		/// <summary>
		/// The cells it covers, inclusive at both ends - the engine's rectangle on the model's record,
		/// <c>+0xc0</c> and <c>+0xc4</c> - or null for a fixed item, which is placed on no cell.
		/// </summary>
		public (int Left, int Top, int Right, int Bottom)? Footprint { get; init; }

		/// <summary>The textures its floor was blended into, its own to let go of with it (<see cref="ProceduralFloor.Blend"/>).</summary>
		public IReadOnlyList<Texture> Blended { get; init; } = [];

		/// <summary>How many cells of its floor were blended.</summary>
		public int FloorCells { get; init; }

		/// <summary>The rim vertices the ground moved, and what the model measures against the ground now (<see cref="ProceduralConform.Measure"/>).</summary>
		public int Conformed { get; init; }

		public Func<ProceduralConform.Measurement>? Measure { get; init; }

		public LobbyModel Model { get; } = model;

		public RideAnimations Animations { get; } = animations;

		/// <summary>
		/// Which catalogue item this is: a placed thing's own, and a fixed item's where it was stood with one - nought for a fixed item stood without.
		/// <see cref="ParkRides"/> binds scripts by walking the save's object list, so something stood
		/// without one there would never be given a script; this is how it finds the item to load from.
		/// </summary>
		public int CatalogueId { get; } = catalogueId;

		/// <summary>
		/// The clip posed last on each channel and the start that bound it, so a new start can put back what the
		/// one before it moved. One entry per player, because a sideshow runs a lane on each and each lane changes clip
		/// independently - a single slot here would make lane three's trigger undo lane one's pose.
		/// </summary>
		public RideAnimations.Posed[] Posed { get; } = new RideAnimations.Posed[animations.ChannelCount];
	}

	private readonly Dictionary<int, Standing> _standing = [];

	/// <summary>
	/// The park's objects, for the drawing of the people standing on them - the same arrangement
	/// <see cref="ParkPeople.Current"/> and <see cref="ParkGuestSprites.Current"/> use.
	/// </summary>
	internal static ParkObjects? Current { get; private set; }

	/// <summary>
	/// Where a named node of a placed thing stands in the world, or false when this thing has no model
	/// or no such node.
	/// </summary>
	/// <remarks>
	/// <b>Placed, not loaded.</b> See <see cref="LobbyModel.TryGetPlacedNode"/> for why asking the other
	/// one would quietly ignore both where the thing stands and which way it faces.
	/// </remarks>
	internal bool TryNodeOn( int thingId, string node, out Vector3 world )
	{
		if ( _standing.TryGetValue( thingId, out var standing ) )
			return standing.Model.TryGetPlacedNode( node, out world );

		world = default;

		return false;
	}

	/// <summary>
	/// Where a node of a placed thing, by index, stands as the thing is drawn this frame - on its morphing face, or carried by
	/// its mesh (<see cref="LobbyModel.TryGetDrawnNode"/>) - or <see cref="DrawnNode.Missing"/>.
	/// </summary>
	internal DrawnNode TryDrawnNodeOn( int thingId, int node, out Vector3 world )
	{
		if ( _standing.TryGetValue( thingId, out var standing ) )
			return standing.Model.TryGetDrawnNode( node, out world );

		world = default;

		return DrawnNode.Missing;
	}

	/// <summary>
	/// Where a placed thing's own voice is sounded: the middle of the cells it covers, at its base height
	/// (<c>FUN_00556af0</c>, docs/exe/ride-operation.md, "Where a scream is sounded").
	/// </summary>
	/// <remarks>
	/// <see cref="TryNodeOn"/> wants a node by name, which is right for a rider on a seat and wrong for
	/// a ride's own voice: the engine takes a scream's position from the script's model handle at
	/// <c>+0xc8</c>, not from any node. A fixed item covers no cell here, where the engine's record holds
	/// cell (0,0) alone: it is counted and answers where the model stands.
	/// </remarks>
	internal bool TrySoundPlace( int thingId, out Vector3 world )
	{
		if ( !_standing.TryGetValue( thingId, out var standing ) )
		{
			world = default;

			return false;
		}

		var origin = standing.Model.PlacedOrigin;

		if ( standing.Footprint is { } footprint )
		{
			world = BoxMiddle( footprint, origin.Z );
		}
		else
		{
			Unimplemented.Report( "SOUND_PLACE_NO_CELL_RECTANGLE" );
			world = origin;
		}

		return true;
	}

	/// <summary>
	/// The middle of a cell rectangle in world units, as the engine takes it from the thing's box
	/// (<c>FUN_00466b70</c>): halfway from the least cell's near edge to the greatest cell's far edge.
	/// </summary>
	public static Vector3 BoxMiddle( (int Left, int Top, int Right, int Bottom) footprint, float height )
	{
		var field = ParkGround.Current?.Heightfield;

		var cellX = field?.CellSizeX ?? DefaultCellSize;
		var cellY = field?.CellSizeY ?? DefaultCellSize;

		return new Vector3(
			(footprint.Left + footprint.Right + 1) * 0.5f * cellX,
			(footprint.Top + footprint.Bottom + 1) * 0.5f * cellY,
			height );
	}

	/// <summary>
	/// Every sign painted for an object standing in this park, kept only so that they can be let
	/// go of again. Each is cut from a board rasterised for that one object, so it is in no cache
	/// and nothing else holds it - see <see cref="OnDelete"/>.
	/// </summary>
	private readonly List<IReadOnlyDictionary<string, Texture>> _signs = [];

	/// <summary>How many objects actually stand in the park - for the log, and for anything wanting to check.</summary>
	public int Placed => _models.Count;

	/// <summary>
	/// The body of a built thing's footprint: 35 of Lost Kingdom's cells, and the type that identified
	/// <see cref="ParkWorld.MapCell.Type"/> 4 in the first place by landing exactly on the placed objects.
	/// </summary>
	private const int FootprintBody = 4;

	/// <summary>
	/// The cell of a footprint a thing is used from - eight in Lost Kingdom, and each one sits where it
	/// should: the single cell of each of the three toilets, the drinks shop's counter, the litter bin,
	/// the jungle spray's front, the staff room's door, and the end of the Belly Bounce its queue arrives
	/// at. It is the thing's entrance: the first kind-9 cell of its shape picture (docs/exe/park-engine.md,
	/// "Where a built thing's entry and exit cells come from").
	/// </summary>
	private const int FootprintUsed = 9;

	/// <summary>
	/// The far end of a footprint, which appears exactly once in Lost Kingdom - on the end of the Belly
	/// Bounce away from its queue. It is the thing's exit, the first kind-10 cell of its shape picture
	/// (docs/exe/park-engine.md, "Where a built thing's entry and exit cells come from").
	/// </summary>
	private const int FootprintFar = 10;

	/// <summary>
	/// Whether something built stands on this cell, which is the question <see cref="ParkGround"/> asks in
	/// order to leave it alone. It lives here for the same reason <see cref="ParkPaths.IsPath"/> lives
	/// there: a cell belongs to whatever draws it, and the two must not be able to disagree about which.
	///
	/// <para>
	/// <b>Every item draws its own floor, and that is why the ground must not.</b> The first mesh of an
	/// item's model is a flat plate exactly as wide as its footprint - <c>J_WC</c> under a toilet,
	/// <c>wf_floor</c> under the fountain, <c>js_base</c> under the staff room, <c>cn_floor01</c> under the
	/// drinks shop, <c>jb_floor</c> under the Belly Bounce - lying at the model's own zero, which is the
	/// height the item is stood at.
	/// </para>
	/// <para>
	/// <b>Without this the two fight over the same depth and the grass wins.</b> A footprint cell carries a
	/// real ground texture index in <c>base.MD2</c> - measured, all 44 of them, and not one is the
	/// "something covers this" index 0 that the river and the fixed roads carry - so the ground would build a
	/// grass quad across it at the very heights the floor plate occupies, standing a shop on bare grass
	/// where the original gives it a floor.
	/// </para>
	/// <para>
	/// The three types together are exactly the eleven placed objects' footprints over Lost Kingdom - 44
	/// cells, nothing left over and nothing missing - which is what says this list is complete rather than
	/// merely sufficient.
	/// </para>
	/// </summary>
	public static bool CoversGround( ParkWorld.MapCell cell )
		=> cell.Type is FootprintBody or FootprintUsed or FootprintFar;

	/// <param name="world">
	/// The park's own save, already walked, or null where the theme ships none. It is read once by
	/// <see cref="Level"/> and shared, because the ground and the paths need the same file.
	/// </param>
	/// <param name="catalogue">
	/// Everything this theme can have standing in it, or null where there is no park to place anything in.
	/// It is built once by <see cref="Level"/> and shared with <see cref="ParkRides"/>, because both need
	/// the same items and reading every item's description twice would be careless - the same reason the
	/// park file itself is read once.
	/// </param>
	public ParkObjects( string themeName, IParkInitialState? world, ParkItemCatalogue? catalogue )
	{
		ThemeName = themeName;
		Name = $"{themeName} objects";

		// Before the two bails below, not after them: a park that places nothing still has objects, and
		// leaving Current null there would make the riders' node lookup a silent no-op in exactly the
		// case hardest to notice. ParkPeople sets its own the same way, ahead of its null-park guard.
		Current = this;

		if ( world == null || catalogue == null )
			return;

		if ( world is ParkWorld savedWorld )
		{
			var states = savedWorld.ThingStates( catalogue.ChannelsFor );
			if ( states.Problem == null )
				foreach ( var saved in states.Things ) _savedHoardings[saved.Slot + 1] = saved;
			else Log.Warning( $"Hoarding restore: {states.Problem}" );
		}

		var wanted = world.Objects.Where( item => item.IsPlaced ).ToArray();

		if ( wanted.Length == 0 )
		{
			Log.Info( $"{themeName}: the park file places nothing, so there is only the ground and its fixed items" );
			return;
		}

		foreach ( var item in wanted )
			Place( item, catalogue );

		_savedHoardings.Clear();

		Log.Info( $"{themeName}: {Placed} of {wanted.Length} objects stand in the park, {FloorCellsBlended} floor cells blended into the ground's tiles" );
	}

	/// <summary>
	/// Stands something the player has just bought, and answers whether it went up.
	///
	/// <para>
	/// This is the same <see cref="Place"/> the load uses - deliberately, so a bought thing is
	/// stood by the identical rule a saved one is, rather than by a second arrangement that could
	/// drift from it.
	/// </para>
	/// </summary>
	public bool PlaceNow( ParkWorld.CatalogueObject placed, ParkItemCatalogue catalogue )
	{
		var before = _models.Count;

		Place( placed, catalogue );

		return _models.Count > before;
	}

	/// <summary>
	/// Takes something sold back out of the park: its model stops being drawn and its sign is let go
	/// of. Answers whether anything was standing there.
	/// </summary>
	/// <remarks>
	/// <b><see cref="LobbyModel"/> has no teardown of its own</b>, so this deletes the entities it owns
	/// - the same thing <see cref="OnDelete"/> does for the whole park, one thing at a time. The sign
	/// list is deliberately left alone: a sign is cut per object and is let go of with the park, and
	/// chasing which entry belonged to this one would be a second bookkeeping that can disagree with
	/// the first.
	/// </remarks>
	public bool Remove( int thingId )
	{
		if ( !_standing.TryGetValue( thingId, out var standing ) )
			return false;

		if ( _hoardings.Remove( thingId, out var hoarding ) ) hoarding.Delete();
		_standing.Remove( thingId );
		_models.Remove( standing.Model );

		// A LobbyModel owns one ModelEntity per mesh and has no teardown of its own, so the entities
		// are what has to go. Entity.Delete defers to Entity.ApplyDeletions, which the level runs after
		// its own walk - so this is safe from the console's tick, which happens before that walk.
		foreach ( var entity in standing.Model.Entities )
			entity.Delete();

		// Its entities are gone, so nothing draws with its blended floor now.
		foreach ( var texture in standing.Blended )
			texture.Delete();

		if ( standing.Blended.Count > 0 )
			Log.Info( $"thing {thingId}: its {standing.Blended.Count} blended floor textures let go" );

		return true;
	}

	/// <summary>
	/// Stands one object on the ground, or says why it could not.
	/// </summary>
	private void Place( ParkWorld.CatalogueObject placed, ParkItemCatalogue catalogue )
	{
		if ( !catalogue.TryGet( placed.CatalogueId, out var item ) )
		{
			Log.Warning( $"{ThemeName}: nothing in this theme is catalogue number {placed.CatalogueId}, " +
				$"so the object at ({placed.CellX},{placed.CellY}) is missing from the park" );
			return;
		}

		try
		{
			// Items keep only their own art beside their model and share the rest with the whole theme,
			// so the theme's sharetex.wad is the second place to look - see LobbyModel.LoadTexture.
			var sign = BuildSign( item );

			if ( sign != null )
				_signs.Add( sign );

			// The twelve roles this thing's archive ships, read once here and shared with the script that
			// drives it. The model is bound against all of them because an animation player names a role
			// outright - see RideAnimations.AllClips for why probing for a numbered run is not the same set.
			var animations = RideAnimations.Load( item.Directory, item.Stem, FileSystem, item.AnimationChannels );

			// The cells the builder marks, from the anchor; the model of an item with a map offset stands off it, and
			// its floor is blended over the cells the model covers (FloorFootprint).
			var footprint = FootprintAt( item, placed.CellX, placed.CellY, placed.Angle );
			var covered = FloorFootprint( item, placed.CellX, placed.CellY, placed.Angle );
			var shared = $"levels/{ThemeName.ToLowerInvariant()}/sharetex";
			var blended = new List<Texture>();
			var floorCells = 0;

			var origin = ModelOriginFor( item, placed );
			var turn = Turn( placed.Angle );
			var conformed = 0;

			var model = new LobbyModel( $"{item.Directory}/{item.Stem}.MD2", $"{item.Directory}/textures", Vector3.Zero,
				textureOverrides: sign,
				sharedTextureDirectory: shared,
				clips: animations.AllClips,
				opaqueFrames: file => ProceduralBase.ForItem( file, item.Shape ),
				frameTextures: file => BlendFloor( file, item, placed.Angle, covered, shared, blended, out floorCells ),
				stand: file => conformed = StandOnGround( file, item, origin, turn ) );

			// Put away whatever building it left behind before it is stood anywhere.
			PoseAsBuilt( model, item );

			// A track add-on's end caps as the file left them: the original lays each saved node word over the fresh
			// model's node, the walk's last show or hide among them (FileFormats saves.md, "The node flag words"). Only the
			// two caps' words are laid here; the rest of a model's words this game's clips put back themselves.
			if ( IsTrackAddOn( item ) && _savedHoardings.TryGetValue( placed.MeshInstance, out var record )
				&& LoadedCaps( model.Source, record, placed.CatalogueId ).ToArray() is { Length: > 0 } loaded )
			{
				ApplyCaps( model.SetMeshVisible, loaded );

				// A walk-hidden cap's saved word also holds the lock (0x400), which the LobbyModel's rest and hide must spare
				// as ShowAddOnCaps' LockNode does; it is not laid here yet.
				if ( loaded.Any( cap => cap.Hidden ) )
					Unimplemented.Report( "TRACK_CAP_LOCK_LOADED" );

				Log.Info( $"{ThemeName}: '{item.Name}' (thing {placed.ThingId}) stands with its end caps as the file left them: "
					+ string.Join( ", ", AddOnCapSides.Select( side => $"0x{side:x} " + CapState( model, side ) ) ) );
			}

			model.SetTransform( origin, turn );

			_models.Add( model );
			_standing[placed.ThingId] = new Standing( model, animations, placed.CatalogueId, sign )
			{
				Footprint = footprint,
				Blended = blended,
				FloorCells = floorCells,
				Conformed = conformed,
				Measure = () => ProceduralConform.Measure( model.Source, item.Shape, origin.GetSystemVector3(), turn,
					ParkGround.Current?.Heightfield )
			};

			// The original gates on the base vertices FUN_00469a80 counts (0x0046a838) and reads the mesh with the
			// most of them (0x0046a82c). The node count and the first mesh stand in for both: the same mesh in
			// every shipped outline. docs/exe/ride-hoardings.md.
			if ( item.Hoarding is { } outline && model.Source.Nodes.Count > 1 && model.Source.Meshes.Count > 0
				&& ParkState.Current is { } state )
			{
				var mesh = model.Source.Meshes[0];
				var source = mesh.SourceVertices.Select( v => System.Numerics.Vector3.Transform( v.GetSystemVector3(), mesh.WorldTransform ) ).ToArray();
				var panels = RideHoardingGeometry.Build( outline, source );
				if ( panels.Length > 0 )
				{
					var progress = state.BindHoarding( placed.ThingId );
					if ( _savedHoardings.TryGetValue( placed.MeshInstance, out var saved ) && saved.CatalogueId == placed.CatalogueId )
						progress.Restore( saved.HoardingFlags, saved.HoardingProgress );
					_hoardings[placed.ThingId] = new ParkRideHoarding( ThemeName, placed.ThingId, panels, progress, origin, turn );
				}
			}

			// Where it actually ended up, against where the save says it belongs. The two are worked out
			// from completely separate things - this from the item's own footprint carried through its
			// turn, and the save's from the cells it marks as built on - so when they agree the placement
			// is right for a reason rather than by eye.
			Log.Info( $"{ThemeName}: '{item.Name}' anchored at ({placed.CellX},{placed.CellY}) turned " +
				$"{placed.Angle} covers ({footprint.Left},{footprint.Top})..({footprint.Right},{footprint.Bottom})" );
		}
		catch ( Exception e )
		{
			Log.Warning( $"{ThemeName}: '{item.Name}' would not load, so it is missing from ({placed.CellX},{placed.CellY}) - {e.Message}" );
		}
	}

	/// <summary>
	/// A placed thing's model stood on the ground as the original makes it (flag <c>0x8</c>, <c>0x00463329</c>): the floor's rim vertices
	/// on the terrain's corners, the meshes the lookup records mark <c>0x10000</c> draped, and the nine track meshes the records mark
	/// <c>0x8</c> interpolated along their cell lines (<see cref="ProceduralConform"/>). Answers how many vertices moved.
	/// </summary>
	internal static int StandOnGround( ModelFile file, ParkItemCatalogue.Item item, Vector3 origin, Quaternion turn )
		=> ParkGround.Current?.Heightfield is { } field ? StandOnGround( file, item, origin, turn, field ) : 0;

	/// <summary><see cref="StandOnGround(ModelFile, ParkItemCatalogue.Item, Vector3, Quaternion)"/> on the ground it is given.</summary>
	internal static int StandOnGround( ModelFile file, ParkItemCatalogue.Item item, Vector3 origin, Quaternion turn, HeightfieldFile field )
	{
		var at = origin.GetSystemVector3();
		var moved = ProceduralConform.Conform( file, item.Shape, at, turn, field );

		moved += ProceduralConform.Drape( file, ProceduralConform.LookupDraped( file ), at, turn, field );

		return moved + ProceduralConform.Interpolate( file, item.Shape, at, turn, field );
	}

	/// <summary>
	/// For the console's <c>rim</c>: each standing thing's listed rim vertices and how many of them the drawn model has on a terrain
	/// corner (<see cref="ProceduralConform.Measure"/>), and the largest distance of one from its corner's height.
	/// </summary>
	internal IEnumerable<string> ConformCensus()
	{
		foreach ( var (thingId, standing) in _standing.OrderBy( pair => pair.Key ) )
		{
			if ( standing.Measure?.Invoke() is not { } measured || (measured.Listed == 0 && measured.Interpolated == 0) )
				continue;

			var name = Level.Current?.Catalogue is { } catalogue && catalogue.TryGet( standing.CatalogueId, out var item ) ? item.Name : $"item {standing.CatalogueId}";

			yield return $"thing {thingId,3} '{name}' base vertices {measured.Listed} on a corner {measured.OnCorner} largest off {measured.LargestOff:F4} moved {standing.Conformed}"
				+ (measured.Interpolated > 0 ? $" interpolated {measured.Interpolated} listed largest rise off {measured.LargestRiseOff:F4}" : "");
		}
	}

	/// <summary>
	/// The cells a placed item's floor is stamped and blended over: the rectangle the model covers (<c>FUN_00467030</c>'s
	/// <c>+0xc0</c>/<c>+0xc4</c>, made from the place <c>FUN_004dd500</c> builds from <c>mTopLeft</c>), so the footprint at the
	/// item's <see cref="ModelCell"/>. It is the anchor's own footprint except for an item with a map offset.
	/// </summary>
	public static (int Left, int Top, int Right, int Bottom) FloorFootprint( ParkItemCatalogue.Item item, int cellX, int cellY, int angle )
	{
		var (modelX, modelY) = ModelCell( item, cellX, cellY, angle );

		return FootprintAt( item, modelX, modelY, angle );
	}

	/// <summary>The floor cells of the things standing blended into the ground's tiles (<see cref="BlendFloor"/>).</summary>
	public int FloorCellsBlended => _standing.Values.Sum( standing => standing.FloorCells );

	/// <summary>
	/// The mesh a model's floor table is read from, or -1 where it blends none: a model exported with a procedural base (header
	/// <c>0x8</c>) and a floor found, or the header's <c>0x80</c>, every cell, on the root.
	/// </summary>
	internal static int FloorMeshOf( ModelFile file, ItemShape shape )
		=> (file.HeaderFlags & 0x8) == 0 ? -1
			: ProceduralBase.FloorMesh( file, shape, ProceduralBase.ParkGrid, out _ ) ?? ((file.HeaderFlags & 0x80) != 0 ? file.RootNode : -1);

	/// <summary>
	/// A placed item's floor blended into the ground's tiles (<see cref="ProceduralFloor.Blend"/>), as the model maker does
	/// for a thing stood on the land (<c>FUN_00463060</c>'s flag <c>0x8</c>) where the compositor is on and the loader built
	/// the item's footprint table: a model exported with a procedural base (header <c>0x8</c>), loaded with its picture, and a
	/// floor found (or the header's <c>0x80</c>, every cell, on the root). None elsewhere, where the floor draws its own texture.
	/// </summary>
	private static IReadOnlyDictionary<uint, Texture> BlendFloor( ModelFile file, ParkItemCatalogue.Item item, int angle,
		(int Left, int Top, int Right, int Bottom) footprint, string shared, List<Texture> made, out int cells )
	{
		cells = 0;

		if ( ParkGround.Current is not { } ground || item.Shape is not { } shape || FloorMeshOf( file, shape ) is not (>= 0 and var mesh) )
			return new Dictionary<uint, Texture>();

		var everyCell = (file.HeaderFlags & 0x80) != 0;

		var blended = ProceduralFloor.Blend( file, mesh, shape, shape.Width, shape.Depth, everyCell, angle, footprint, ground,
			name => LobbyModel.TexturePath( name, $"{item.Directory}/textures", shared ), made, out cells );

		if ( cells > 0 )
			Log.Info( $"'{item.Name}': {cells} floor cells blended into the ground's tiles" );

		return blended;
	}

	/// <summary>
	/// Leaves an item looking the way it does once it has finished being built, rather than the way
	/// it looked while it was going up.
	///
	/// <para>
	/// An item is put up by a one-shot clip named after itself with a <c>c</c> on the end, and that
	/// clip decides what is on screen as it plays: the Belly Bounce arrives as an egg, which splits
	/// at frame 94 to let the dinosaur out, drops its shell at 131, and raises its fence, its posts
	/// and its two sign boards over the frames after that. The engine plays it when the player
	/// builds the thing and never again - a park loaded from a save restores the state each object
	/// settled into instead - so <b>the last frame of that clip is what a built item looks like</b>,
	/// and playing none of it at all would leave the Belly Bounce sitting inside an unhatched egg.
	/// </para>
	///
	/// <para>
	/// Only the clip's visibility is taken, which is the part that says what exists. Across all four
	/// themes 966 of these tracks end with their mesh shown and 57 end with it hidden, and every one
	/// of the 57 is something the building of the item threw away: this egg and its shell, a witch's
	/// frog, a monkey's crate and the shards of it, puffs of smoke, and the beams and flashes a ride
	/// only shows while it is running. Of the eleven objects Lost Kingdom is built with, the Belly
	/// Bounce is the only one that hides anything at all.
	/// </para>
	///
	/// <para>
	/// The clip is read directly rather than through <see cref="AnimationFile.TryLoad"/>, which
	/// answers "is there an animation here worth playing" and rejects one carrying visibility and
	/// nothing else - six items in the game ship exactly that.
	/// </para>
	/// </summary>
	internal void PoseAsBuilt( LobbyModel model, ParkItemCatalogue.Item item )
	{
		AnimationFile construction;

		try
		{
			using var stream = FileSystem.OpenReadOrNull( $"{item.Directory}/{item.Stem}c.md2" );

			if ( stream == null )
				return;

			construction = new AnimationFile( stream );
		}
		catch ( Exception )
		{
			// An item with no construction clip is simply there the moment it is placed, and has
			// nothing left over to put away.
			return;
		}

		if ( construction.VisibilityTracks.Count == 0 )
			return;

		// A clip's last frame spans only the keys that pose it, and a visibility entry can sit past
		// all of them - bricks keys nothing at all and still switches a snail on at frame 45 - so
		// the end of the build is the later of the two.
		var end = (float)construction.LastFrame;

		foreach ( var track in construction.VisibilityTracks )
		{
			foreach ( var entry in track.Entries )
				end = MathF.Max( end, MathF.Abs( entry ) );
		}

		var leftBehind = 0;

		foreach ( var track in construction.VisibilityTracks )
		{
			if ( track.VisibleAt( end ) is not bool visible )
				continue;

			model.SetMeshVisible( track.TargetIndex, visible );

			if ( !visible )
				++leftBehind;
		}

		if ( leftBehind > 0 )
			Log.Info( $"{ThemeName}: '{item.Name}' finished being built, so {leftBehind} of its meshes are put away" );
	}

	/// <summary>
	/// A ride's name board, painted the same way the park gate's is.
	///
	/// <para>
	/// A ride that has a board ships the artwork for it as a <c>.sgn</c> beside its model and names two
	/// materials for the two halves of it, <c>sign1</c> and <c>sign2</c>. It ships neither of those
	/// textures: they fall through to the theme's shared archive, where <c>sign1.wct</c> and
	/// <c>sign2.wct</c> are magenta placeholders that read, in so many words, "SIGN1" and "SIGN2". The
	/// lettering is meant to be painted on at load out of the ride's own name, which is what this does.
	/// </para>
	///
	/// <para>
	/// <b>Not the original's lettering (Q399).</b> This letters the <c>.sam</c>'s <c>Name</c>; the original
	/// letters the item's two <c>OBJECT_NAMES</c> rows (FileFormats <c>sgn.md</c>), which in English Lost
	/// Kingdom differ only for Dino Karts ("DinoKarts", on one line), and scales each character's width by the
	/// font record's width percent, which <c>SignFile</c> does not read.
	/// </para>
	///
	/// <para>
	/// Only rides carry one, and of the eleven objects Lost Kingdom is built with the Belly Bounce is the
	/// only one - but every theme's rides carry them, and the file is always named after the item's own
	/// folder: seventy-eight of seventy-eight across the four themes.
	/// </para>
	///
	/// <para>
	/// <b>Most rides ship no artwork for their board, and are meant not to.</b> 61 of the 84 signs in the
	/// game leave the flag at offset 8 clear, and the original answers that by clearing the board to
	/// transparent black and skipping the picture entirely (0x005ecd09, 0x005ecd18) - the name is lettered
	/// onto nothing and floats with the ride showing through behind it. A board with a picture carries the
	/// artwork's own 36-byte header - twelve bytes of width, height and type, four dequantisation floats and
	/// the two chunk sizes - and a bare board does not, which is the whole of the difference.
	/// </para>
	///
	/// <para>
	/// Returning null leaves the model's own textures in place, which is the right answer both for that
	/// case and for the nine items in ten that have no board at all.
	/// </para>
	/// </summary>
	private IReadOnlyDictionary<string, Texture>? BuildSign( ParkItemCatalogue.Item item )
	{
		if ( item.SignPath == null || string.IsNullOrEmpty( item.Name ) )
			return null;

		if ( !SignTexture.TryBuild( item.SignPath, item.Name, out var left, out var right ) )
			return null;

		Log.Info( $"{ThemeName}: painted '{item.Name}' onto its own sign" );

		return new Dictionary<string, Texture>( StringComparer.OrdinalIgnoreCase )
		{
			["sign1"] = left!,
			["sign2"] = right!
		};
	}

	/// <summary>
	/// Where a model has to be put so that its footprint covers the cells the save gives it.
	///
	/// <para>
	/// An item is authored with <b>its footprint's corner at its own origin</b>: a one-cell toilet's floor
	/// runs from 0 to 10 in both ground axes, the three-by-three fountain's from 0 to 30, the two-by-two
	/// staff room's from 0 to 20. That was read off the models' node transforms rather than their bounding
	/// boxes, which are node-local and say only how big a mesh is.
	/// </para>
	/// <para>
	/// <b>The turn is about the middle of the item's anchor CELL, not the middle of its footprint</b>, and
	/// that is measured rather than chosen - see <see cref="Turn"/>, which settles the direction from the
	/// same evidence. Turning about the footprint's middle leaves the box where it is and spins the item
	/// inside it; turning about the anchor cell sweeps the box around that
	/// one cell. Only the second puts the fountain and the staff room on the cells the save marks for them.
	/// </para>
	/// <para>
	/// The size of the footprint drops out of it entirely, which is why it is not asked for: whatever
	/// an item's width and depth, its origin corner lies half a cell from its anchor's middle in each axis,
	/// so half a cell is the whole of what has to be turned.
	/// </para>
	/// </summary>
	/// <param name="x">The cell the thing is anchored on, across.</param>
	/// <param name="y">And down.</param>
	/// <param name="angle">How far round it is saved, in whole degrees.</param>
	public static Vector3 OriginFor( int x, int y, int angle ) => OriginFor( x, y, angle, ParkGround.Current?.Heightfield );

	/// <summary><see cref="OriginFor(int, int, int)"/> over a given ground, or none.</summary>
	internal static Vector3 OriginFor( int x, int y, int angle, HeightfieldFile? field )
	{
		var cellX = field?.CellSizeX ?? DefaultCellSize;
		var cellY = field?.CellSizeY ?? DefaultCellSize;

		// The ground at the cell's near corner; a placed object's model takes its footprint's height instead
		// (ModelOriginFor).
		var height = field?.HeightAt( x, y ) ?? 0f;

		var pivot = new Vector3( (x + 0.5f) * cellX, (y + 0.5f) * cellY, height );
		var toCorner = new Vector3( -0.5f * cellX, -0.5f * cellY, 0f );

		var turned = (Vector3)System.Numerics.Vector3.Transform( toCorner.GetSystemVector3(), Turn( angle ) );

		return pivot + turned;
	}

	/// <summary>
	/// The cell an object's model stands on: its <c>mTopLeft</c>, which the object's model is made on
	/// (<c>FUN_004dd500</c>, <c>0x004dd5d3</c>, for every placed object at every angle; the turn about the cell is
	/// the same for every item, <see cref="OriginFor"/>). That is the anchor for an item with no map offset, and a row
	/// or a column off it for one with: the Huge Hollow Rock and the Dino Karts Tunnel. Taken from the item's offset
	/// (<see cref="ParkBuilding.TopLeftFor"/>), which a file's <c>mTopLeft</c> is on every placed object measured
	/// (<c>docs/exe/saves.md</c>, "<c>mTopLeft</c>").
	/// </summary>
	public static (int X, int Y) ModelCell( ParkItemCatalogue.Item item, int cellX, int cellY, int angle )
	{
		var id = ParkBuilding.TopLeftFor( item, cellX, cellY, angle ) - 1;

		return (id & (ParkWorld.MapSize - 1), id / ParkWorld.MapSize);
	}

	/// <summary>
	/// Where an object's model and its nodes stand: <see cref="OriginFor(int, int, int)"/> its <see cref="ModelCell"/>, at
	/// the height its footprint gives it (<see cref="BaseHeight"/>).
	/// </summary>
	public static Vector3 ModelOriginFor( ParkItemCatalogue.Item item, ParkWorld.CatalogueObject placed )
		=> ModelOriginFor( item, placed, ParkGround.Current?.Heightfield );

	/// <summary><see cref="ModelOriginFor(ParkItemCatalogue.Item, ParkWorld.CatalogueObject)"/> over a given ground, or none.</summary>
	internal static Vector3 ModelOriginFor( ParkItemCatalogue.Item item, ParkWorld.CatalogueObject placed, HeightfieldFile? field )
	{
		var (x, y) = ModelCell( item, placed.CellX, placed.CellY, placed.Angle );
		var origin = OriginFor( x, y, placed.Angle, field );

		return field == null ? origin : origin with { Z = BaseHeight( item.Shape, origin, placed.Angle, field ) ?? origin.Z };
	}

	/// <summary>
	/// The height the model maker stands a placed object at (<c>FUN_00466d70</c>; <c>docs/exe/park-engine.md</c>, "Where a
	/// placed object's model stands"): halfway between the lowest and the highest ground vertex at the corners of the cells its
	/// <c>Info.Shape</c> fills (a kind whose low four bits are not nought), each carried through the model's place and
	/// turn. Null where the item has no shape or the shape fills no cell, which leaves the ground under the model's cell.
	/// </summary>
	internal static float? BaseHeight( ItemShape? shape, Vector3 origin, int angle, HeightfieldFile field )
		=> FootprintHeights( shape, origin, angle, field ) is var (low, high) ? (low + high) * 0.5f : null;

	/// <summary>
	/// The lowest and the highest ground vertex at the corners of the cells an item's shape fills (the two
	/// <see cref="BaseHeight"/> halves), each corner carried through the model's place and turn; null where the shape fills no cell.
	/// </summary>
	internal static (float Low, float High)? FootprintHeights( ItemShape? shape, Vector3 origin, int angle, HeightfieldFile field )
	{
		if ( shape == null )
			return null;

		var turn = Turn( angle );
		float low = float.MaxValue, high = float.MinValue;

		for ( var row = 0; row < shape.Depth; ++row )
		{
			for ( var column = 0; column < shape.Width; ++column )
			{
				if ( (shape.KindAt( column, row ) & 0xf) == 0 )
					continue;

				for ( var corner = 0; corner < 4; ++corner )
				{
					var local = new System.Numerics.Vector3( (column + (corner & 1)) * field.CellSizeX,
						(row + (corner >> 1)) * field.CellSizeY, 0f );
					var at = System.Numerics.Vector3.Transform( local, turn ) + origin.GetSystemVector3();
					// The vertex, a tenth added and cut, as FUN_00454050 does.
					var height = field.HeightAt( (int)((at.X / field.CellSizeX) + 0.1f), (int)((at.Y / field.CellSizeY) + 0.1f) );

					low = MathF.Min( low, height );
					high = MathF.Max( high, height );
				}
			}
		}

		return low > high ? null : (low, high);
	}

	/// <summary>A cell's size in world units, for the one case where there is no ground to ask.</summary>
	private const float DefaultCellSize = 10f;

	/// <summary>
	/// Which cells an item actually stands on, inclusive at both ends: its footprint's four corners
	/// carried through the very origin and turn the model was handed, then read back as cells.
	///
	/// <para>
	/// It exists to be checked against the cells the <i>save</i> marks as built on - see
	/// <see cref="CoversGround"/> - which is an entirely independent statement of where a thing belongs,
	/// derived from the map rather than from the item. Screenshots cannot settle this: at a pitched
	/// camera a thing further away rides higher up the frame, so "north of" and "south of" read the same
	/// as "nearer" and "further", and two items a cell apart are indistinguishable by eye.
	/// </para>
	/// <para>
	/// <b>It answers in numbers</b>, not only for the line below: anything that builds, moves or sells
	/// needs the same answer - which cells to mark, and which to give back.
	/// </para>
	/// </summary>
	public static (int Left, int Top, int Right, int Bottom) FootprintOf( ParkItemCatalogue.Item item, Vector3 origin, Quaternion turn )
	{
		var field = ParkGround.Current?.Heightfield;

		var cellX = field?.CellSizeX ?? DefaultCellSize;
		var cellY = field?.CellSizeY ?? DefaultCellSize;

		float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;

		// The footprint's four corners in the item's own space, where it is authored with that footprint
		// running from its origin out to its width and depth.
		for ( var corner = 0; corner < 4; ++corner )
		{
			var local = new Vector3(
				(corner is 1 or 2 ? item.Width : 0) * cellX,
				(corner is 2 or 3 ? item.Depth : 0) * cellY, 0f );

			var at = (Vector3)System.Numerics.Vector3.Transform( local.GetSystemVector3(), turn ) + origin;

			minX = MathF.Min( minX, at.X );
			minY = MathF.Min( minY, at.Y );
			maxX = MathF.Max( maxX, at.X );
			maxY = MathF.Max( maxY, at.Y );
		}

		// Half a cell in from each far edge before rounding, so a box that ends exactly on a boundary
		// names the cell it fills rather than the empty one it touches.
		return (
			(int)MathF.Floor( minX / cellX ),
			(int)MathF.Floor( minY / cellY ),
			(int)MathF.Floor( (maxX / cellX) - 0.5f ),
			(int)MathF.Floor( (maxY / cellY) - 0.5f ) );
	}

	/// <summary>
	/// Which cells an item would stand on if it were anchored at a cell and turned that far - the same
	/// answer as <see cref="FootprintOf"/>, for a caller holding a position rather than a transform.
	/// </summary>
	/// <remarks>
	/// Anything deciding whether a thing may be built somewhere has the cell and the angle and not the
	/// origin, so composing the two here keeps the pairing of <see cref="OriginFor"/> with
	/// <see cref="Turn"/> in one place: turning about the footprint's middle instead of the anchor cell's
	/// spins an item inside its own box.
	/// </remarks>
	public static (int Left, int Top, int Right, int Bottom) FootprintAt( ParkItemCatalogue.Item item, int cellX, int cellY, int angle )
		=> FootprintOf( item, OriginFor( cellX, cellY, angle ), Turn( angle ) );

	/// <summary>
	/// How far round an object stands, about the world's up axis - which is Z here, where the original's
	/// is Y.
	///
	/// <para>
	/// <b>The direction is settled by the save's own map cells</b>, which mark the cells each built thing
	/// stands on. The staff room's two by two is marked at (58,15)..(59,16) and the fountain's three by three at
	/// (57,17)..(59,19), while both are anchored a row beyond that - at (58,16) and (57,19) - and both are
	/// saved at 90 degrees. Only a negative turn puts them there. A positive one lands the fountain on
	/// (55,19)..(57,21) and the staff room on (57,16)..(58,17), which is not a prediction but a
	/// measurement: the game was made to report its own footprints and that is what it reported.
	/// </para>
	/// <para>
	/// <b>The executable's <c>0x168 - angle</c> does not bear on it.</b> The constant sits at the
	/// <i>queue's</i> own call site (FUN_005229e0), so what it says is that a piece of queue turns the
	/// <b>opposite</b> way to a built thing - it is the difference between the two, not a convention they
	/// share. See <see cref="ParkQueues"/>, which applies it. The footprint cells above are the whole of
	/// the evidence for this one, and they are enough on their own.
	/// </para>
	/// <para>
	/// Those two are the only rotated things in the shipped park whose footprint is bigger than one cell,
	/// so 180 and 270 follow the rule the two 90s establish rather than being measured in their own right.
	/// The three toilets are saved at 270 and are one cell each, where every direction agrees - there it
	/// decides which way the door faces and nothing else.
	/// </para>
	/// </summary>
	public static Quaternion Turn( int degrees )
		=> Quaternion.CreateFromAxisAngle( System.Numerics.Vector3.UnitZ, -degrees * (MathF.PI / 180f) );

	/// <summary>
	/// Takes in a thing the park built somewhere else, so that it is swept along with the rest.
	///
	/// <para>
	/// The fixed items are why this exists. <b>The engine does not read them from the save's placements at
	/// all</b> - <c>FUN_005156a0</c> searches the item descriptions by name for "gates" and "lights" and
	/// constructs each one itself - but what it constructs is an ordinary catalogue object, through the very
	/// same constructor a built thing goes through (<c>FUN_004db090</c>), which binds its <c>.RSE</c> and
	/// gives it an animation player like anything else. <see cref="ParkFixedItems"/> finds those two the same
	/// way, by name, and hands them here.
	/// </para>
	///
	/// <para>
	/// <b>They must not come through <see cref="Place"/>.</b> Their positions are baked into their models
	/// rather than kept in the save, so placing them would move them off the entrance and the crossings, and
	/// the gate's board carries the <i>park's</i> name rather than an item's. Only the sweep is shared: the
	/// model and its sign stay owned by whoever built them, which is why neither <c>_models</c> nor the sign
	/// list is touched here.
	/// </para>
	/// </summary>
	public void Stand( int thingId, LobbyModel model, RideAnimations animations, int catalogueId = 0 )
		=> _standing[thingId] = new Standing( model, animations, catalogueId );

	/// <summary>
	/// The animation roles a placed thing's archive ships, or null where nothing of that id stands here -
	/// what <see cref="ParkRides"/> binds to a script instead of reading the same clips a second time.
	/// </summary>
	public RideAnimations? AnimationsFor( int thingId )
		=> _standing.TryGetValue( thingId, out var standing ) ? standing.Animations : null;

	/// <summary>
	/// The model a placed thing is standing as, or null where nothing of that id stands here. It is the
	/// model the park is drawing, not a copy.
	/// </summary>
	internal LobbyModel? ModelFor( int thingId )
		=> _standing.TryGetValue( thingId, out var standing ) ? standing.Model : null;

	/// <summary>An add-on's two end caps, by their lookup records' hide words: 1 the way back, 0x10 the way on.</summary>
	public static readonly uint[] AddOnCapSides = [0x01, 0x10];

	/// <summary>An add-on of a kart or water ride's track (<c>AddOn.UpgradeType</c> 1, a ride named): its cells carry the
	/// track's add-on flag, and the walk shows and hides its end caps.</summary>
	public static bool IsTrackAddOn( ParkItemCatalogue.Item item ) => item.AddOnUpgradesId != 0 && item.AddOnUpgradeType == 1;

	/// <summary>An add-on's end caps as a park file's node words leave them: each side's cap node and whether its word's
	/// hidden bit (<c>0x1</c>) is set. A side with no cap, or no word, is left out.</summary>
	public static IEnumerable<(uint Side, int Node, bool Hidden)> SavedCaps( ModelFile file, IReadOnlyList<uint> words )
	{
		foreach ( var side in AddOnCapSides )
		{
			if ( file.FindNodeByHide( side, 1 ) is var node and >= 0 && node < words.Count )
				yield return (side, node, (words[node] & 1) != 0);
		}
	}

	private static string CapState( LobbyModel model, uint side )
		=> model.CapNode( side ) is var node and >= 0 ? (model.Entities[node].Opacity > 0f ? "shown" : "hidden") : "none";

	/// <summary>Each cap a walk's <paramref name="bits"/> leave: a side's cap hidden where its bit is set.</summary>
	public static IEnumerable<(uint Side, int Node, bool Hidden)> CapsFor( ModelFile file, int bits )
	{
		foreach ( var side in AddOnCapSides )
		{
			if ( file.FindNodeByHide( side, 1 ) is var node and >= 0 )
				yield return (side, node, (bits & side) != 0);
		}
	}

	/// <summary>The caps a loaded add-on's saved record lays: its node words' hidden bits, only for the record of this
	/// catalogue item. None where the record is another item's or holds no words.</summary>
	public static IEnumerable<(uint Side, int Node, bool Hidden)> LoadedCaps( ModelFile file, SavedThing record, int catalogueId )
		=> record.CatalogueId == catalogueId && record.NodeWords is { } words ? SavedCaps( file, words ) : [];

	/// <summary>Shows each cap not hidden and hides the others, through <paramref name="setVisible"/> (node, visible).</summary>
	public static void ApplyCaps( Action<int, bool> setVisible, IEnumerable<(uint Side, int Node, bool Hidden)> caps )
	{
		foreach ( var (_, node, hidden) in caps )
			setVisible( node, !hidden );
	}

	/// <summary>
	/// An add-on's end caps as a walk leaves them (<c>0x0052af54</c>..<c>0x0052b18c</c>): each cap whose side's bit is in
	/// <paramref name="bits"/> hidden (<c>| 0x80000010</c>), the other shown (<c>&amp; ~0x10</c>), and its node words the
	/// same for a save. Answers whether the thing stands here.
	/// </summary>
	public bool ShowAddOnCaps( int thingId, int bits )
	{
		if ( ModelFor( thingId ) is not { } model )
			return false;

		var caps = CapsFor( model.Source, bits ).ToArray();

		ApplyCaps( model.SetMeshVisible, caps );

		foreach ( var (_, node, hidden) in caps )
		{
			model.LockNode( node );
			AnimationsFor( thingId )?.Nodes?.SetHidden( node, hidden );
		}

		Log.Info( $"track: the walk leaves add-on thing {thingId}'s end caps "
			+ string.Join( ", ", AddOnCapSides.Select( side => $"0x{side:x} " + CapState( model, side ) ) ) + $" (bits 0x{bits:x})" );

		return true;
	}

	/// <summary>
	/// The name board painted for a placed thing, or null where it has none. The park owns the textures
	/// and lets go of them when it ends.
	/// </summary>
	internal IReadOnlyDictionary<string, Texture>? SignFor( int thingId )
		=> _standing.TryGetValue( thingId, out var standing ) ? standing.Sign : null;

	/// <summary>
	/// Brings every thing's animation player up to <paramref name="now"/> and shows whatever each one has
	/// reached.
	///
	/// <para>
	/// <b>Once per frame, after the script ticks, which is the order the engine uses.</b> Its channels are
	/// advanced in the scene draw - a model on screen from its scene-node callback, advanced and posed, the
	/// rest by the sweep <c>FUN_0044e410( 3 )</c>, advanced unposed - past the back edge of the 31ms
	/// catch-up loop the scripts run inside, off a clock snapshot taken once for the whole frame.
	/// Advancing and posing are one function there (<c>FUN_004735d0</c>), which is why they are one here
	/// and why this is driven from <see cref="ParkRides"/> rather than from this entity's own update.
	/// </para>
	///
	/// <para>
	/// <b>Nothing standing in a park keeps a clock of its own.</b> Looping whatever numbered clips sit
	/// beside a model is neither what its script asks for nor anything the engine does - so a thing whose
	/// player is idle holds the pose its construction left it in,
	/// and only a script moves it. That is the engine's own behaviour: its idle default never fires on a
	/// placed thing's model (docs/exe/park.md, "Two gates").
	/// </para>
	/// </summary>
	public void Sweep( int now )
	{
		foreach ( var standing in _standing.Values )
		{
			standing.Animations.Advance( now );

			// Every player, not only the first: a sideshow runs one per lane - the Jungle Spray declares three in
			// UsageInfo.NumSimultAnims and triggers a lane onto each. The frame is counted from the start of the
			// player's own clip - see MeshRotator.Pose for why that is not the clip's first keyed frame.
			standing.Animations.PoseOnto( standing.Model, standing.Posed );
		}
	}

	/// <summary>
	/// Poses one standing thing from its players as they stand, advancing nothing: a thing built after this frame's
	/// <see cref="Sweep"/> (a placing click is dealt out after it) is drawn as its clips hold it from its first frame,
	/// not as <see cref="PoseAsBuilt"/> left it.
	/// </summary>
	public void PoseNow( int thingId )
	{
		if ( _standing.TryGetValue( thingId, out var standing ) )
			standing.Animations.PoseOnto( standing.Model, standing.Posed );
	}

	/// <summary>
	/// Lets go of every sign painted for this park's objects, for the reason
	/// LobbyIsland.OnDelete gives: a material leaves the textures bound into it alone because they
	/// are usually cached by path and shared, and these are neither.
	/// </summary>
	protected override void OnDelete()
	{
		foreach ( var hoarding in _hoardings.Values ) hoarding.Delete();
		_hoardings.Clear();
		_savedHoardings.Clear();
		if ( Current == this )
			Current = null;

		foreach ( var sign in _signs )
		{
			foreach ( var panel in sign.Values )
				panel.Delete();
		}

		_signs.Clear();

		foreach ( var standing in _standing.Values )
		{
			foreach ( var texture in standing.Blended )
				texture.Delete();
		}
	}

}
