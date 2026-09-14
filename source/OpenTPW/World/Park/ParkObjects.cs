using System.Numerics;

namespace OpenTPW;

/// <summary>
/// What the park was built with: the shops, rides, sideshows and scenery standing on its ground, read
/// out of the park's own save file.
///
/// <para>
/// Lost Kingdom ships with eleven of them - a drinks shop, a belly bounce, a jungle spray, three small
/// toilets, a staff room, two security cameras, a litter bin and a round fountain. Until now the park
/// was bare ground with a gate on it; these are the things that make it a park somebody laid out.
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
	/// at. <b>That reading is inferred from where they sit</b>; what is measured is that they belong to a
	/// footprint rather than to the ground.
	/// </summary>
	private const int FootprintUsed = 9;

	/// <summary>
	/// The far end of a footprint, which appears exactly once in Lost Kingdom - on the end of the Belly
	/// Bounce away from its queue. One cell is too few to name with any confidence, and it is here
	/// because it is demonstrably part of that ride's twelve-cell footprint, not because "exit" has been
	/// established.
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
	/// "something covers this" index 0 that the river and the fixed roads carry - so the ground built a
	/// grass quad across it at the very heights the floor plate occupies. That is why a shop stood on bare
	/// grass where the original gives it a floor, and it is the same fault the paths had, in a second
	/// place.
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
	public ParkObjects( string themeName, ParkWorld? world )
	{
		ThemeName = themeName;
		Name = $"{themeName} objects";

		if ( world == null )
			return;

		var wanted = world.Objects.Where( item => item.IsPlaced ).ToArray();

		if ( wanted.Length == 0 )
		{
			Log.Info( $"{themeName}: the park file places nothing, so there is only the ground and its fixed items" );
			return;
		}

		var catalogue = new ParkItemCatalogue( themeName );

		foreach ( var item in wanted )
			Place( item, catalogue );

		Log.Info( $"{themeName}: {Placed} of {wanted.Length} objects stand in the park" );
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
			var model = new LobbyModel( $"{item.Directory}/{item.Stem}.MD2", $"{item.Directory}/textures", Vector3.Zero,
				textureOverrides: BuildSign( item ),
				sharedTextureDirectory: $"levels/{ThemeName.ToLowerInvariant()}/sharetex" );

			// Put away whatever building it left behind before it is stood anywhere.
			PoseAsBuilt( model, item );

			var origin = OriginFor( placed.CellX, placed.CellY, placed.Angle );
			var turn = Turn( placed.Angle );

			model.SetTransform( origin, turn );

			_models.Add( model );

			// Where it actually ended up, against where the save says it belongs. The two are worked out
			// from completely separate things - this from the item's own footprint carried through its
			// turn, and the save's from the cells it marks as built on - so when they agree the placement
			// is right for a reason rather than by eye.
			Log.Info( $"{ThemeName}: '{item.Name}' anchored at ({placed.CellX},{placed.CellY}) turned " +
				$"{placed.Angle} covers {FootprintOf( item, origin, turn )}" );
		}
		catch ( Exception e )
		{
			Log.Warning( $"{ThemeName}: '{item.Name}' would not load, so it is missing from ({placed.CellX},{placed.CellY}) - {e.Message}" );
		}
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
	/// and playing none of it at all is what left the Belly Bounce sitting inside an unhatched egg.
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
	private void PoseAsBuilt( LobbyModel model, ParkItemCatalogue.Item item )
	{
		AnimationFile construction;

		try
		{
			using var stream = FileSystem.OpenRead( $"{item.Directory}/{item.Stem}c.md2" );

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
	/// Only rides carry one, and of the eleven objects Lost Kingdom is built with the Belly Bounce is the
	/// only one - but every theme's rides carry them, and the file is always named after the item's own
	/// folder: seventy-eight of seventy-eight across the four themes.
	/// </para>
	///
	/// <para>
	/// <b>A ride's sign does not paint yet, and the reason is known.</b> <see cref="SignFile"/> reads the
	/// layout the gates and the lobby islands use, whose header runs to 0x43DD before the image begins;
	/// every ride's sign is 17,337 bytes, which is 36 short of that, so it is refused as too small and
	/// says so in the log. It is a second variant rather than a broken file - the two differ in the flags
	/// at offsets 4 and 8, which read 0 and 1 on a gate and 1 and 0 on a ride - and 36 is not a whole
	/// number of any record the header is made of, so it wants the original's own loader read rather than
	/// a guess. Until then the Belly Bounce wears the placeholder its artwork ships with, which says
	/// "SIGN1" and "SIGN2" in magenta: that is the game's own art for an unpainted board, and showing it
	/// is more honest than hiding it.
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
	/// boxes, which are node-local and say only how big a mesh is - a distinction that has caught this
	/// work out before.
	/// </para>
	/// <para>
	/// <b>The turn is about the middle of the item's anchor CELL, not the middle of its footprint</b>, and
	/// that is measured rather than chosen - see <see cref="Turn"/>, which settles the direction from the
	/// same evidence. Turning about the footprint's middle leaves the box where it is and spins the item
	/// inside it, which is what this used to do; turning about the anchor cell sweeps the box around that
	/// one cell. Only the second puts the fountain and the staff room on the cells the save marks for them.
	/// </para>
	/// <para>
	/// The size of the footprint drops out of it entirely, which is why it is no longer asked for: whatever
	/// an item's width and depth, its origin corner lies half a cell from its anchor's middle in each axis,
	/// so half a cell is the whole of what has to be turned.
	/// </para>
	/// </summary>
	/// <param name="x">The cell the thing is anchored on, across.</param>
	/// <param name="y">And down.</param>
	/// <param name="angle">How far round it is saved, in whole degrees.</param>
	public static Vector3 OriginFor( int x, int y, int angle )
	{
		var field = ParkGround.Current?.Heightfield;

		var cellX = field?.CellSizeX ?? DefaultCellSize;
		var cellY = field?.CellSizeY ?? DefaultCellSize;

		// The ground under the anchor cell. The original flattens the land beneath an item as it is
		// built - its .sam has a DontDeformBase key for the exceptions - and nothing here does, so an
		// object on a slope would sit at one cell's height rather than being bedded into it. Nothing in
		// Lost Kingdom shows it: the land under all seven of its footprint groups is dead flat, measured
		// from the heightfield's own vertices.
		var height = field?.HeightAt( x, y ) ?? 0f;

		var pivot = new Vector3( (x + 0.5f) * cellX, (y + 0.5f) * cellY, height );
		var toCorner = new Vector3( -0.5f * cellX, -0.5f * cellY, 0f );

		var turned = (Vector3)System.Numerics.Vector3.Transform( toCorner.GetSystemVector3(), Turn( angle ) );

		return pivot + turned;
	}

	/// <summary>A cell's size in world units, for the one case where there is no ground to ask.</summary>
	private const float DefaultCellSize = 10f;

	/// <summary>
	/// Which cells an item actually stands on, as <c>(x0,y0)..(x1,y1)</c>: its footprint's four corners
	/// carried through the very origin and turn the model was handed, then read back as cells.
	///
	/// <para>
	/// It exists to be checked against the cells the <i>save</i> marks as built on - see
	/// <see cref="CoversGround"/> - which is an entirely independent statement of where a thing belongs,
	/// derived from the map rather than from the item. Screenshots cannot settle this: at a pitched
	/// camera a thing further away rides higher up the frame, so "north of" and "south of" read the same
	/// as "nearer" and "further", and two items a cell apart are indistinguishable by eye.
	/// </para>
	/// </summary>
	private static string FootprintOf( ParkItemCatalogue.Item item, Vector3 origin, Quaternion turn )
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
		return $"({(int)MathF.Floor( minX / cellX )},{(int)MathF.Floor( minY / cellY )}).." +
			$"({(int)MathF.Floor( (maxX / cellX) - 0.5f )},{(int)MathF.Floor( (maxY / cellY) - 0.5f )})";
	}

	/// <summary>
	/// How far round an object stands, about the world's up axis - which is Z here, where the original's
	/// is Y.
	///
	/// <para>
	/// <b>The direction is settled, and two independent things settle it.</b> This said for a while that
	/// there was nothing to check it against, which was true only while the save's map cells were being
	/// stepped over. They are read now, and they mark the cells each built thing stands on. The staff
	/// room's two by two is marked at (58,15)..(59,16) and the fountain's three by three at
	/// (57,17)..(59,19), while both are anchored a row beyond that - at (58,16) and (57,19) - and both are
	/// saved at 90 degrees. Only a negative turn puts them there. A positive one lands the fountain on
	/// (55,19)..(57,21) and the staff room on (57,16)..(58,17), which is not a prediction but a
	/// measurement: the game was made to report its own footprints and that is what it reported.
	/// </para>
	/// <para>
	/// The executable agrees, in the one place it states the convention outright. Placing a queue piece it
	/// passes <c>0x168 - angle</c> - 360 minus the stored angle, folded back to 0 - which is this same
	/// negation (FUN_005229e0).
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

	protected override void OnUpdate()
	{
		foreach ( var model in _models )
			model.Update( Time.Delta );
	}
}
