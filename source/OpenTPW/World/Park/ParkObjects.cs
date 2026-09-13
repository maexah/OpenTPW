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

	public ParkObjects( string themeName )
	{
		ThemeName = themeName;
		Name = $"{themeName} objects";

		var world = ReadPark( themeName );

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
	/// The park file for a theme, walked, or null where there is nothing to read.
	///
	/// <para>
	/// Only the jungle ships one of these, which is also why Lost Kingdom is the only park an Instant
	/// Action player can start in. The other three themes have no saved park at all, so they get their
	/// ground and their gate and nothing else - which is a fact about the game's data, not a failure, and
	/// is reported as such.
	/// </para>
	/// <para>
	/// This reads the copy that ships beside the level rather than the player's own. Nothing writes a park
	/// back yet, so the two are identical; when saving exists, this is the line that has to start asking
	/// which player is playing.
	/// </para>
	/// </summary>
	private static ParkWorld? ReadPark( string themeName )
	{
		var path = $"levels/{themeName.ToLowerInvariant()}/Easymode.TPWI";

		if ( !FileSystem.FileExists( path ) )
		{
			Log.Info( $"{themeName}: no park file ships with this theme, so nothing is placed in it" );
			return null;
		}

		try
		{
			using var stream = FileSystem.OpenRead( path );
			var world = new ParkWorld( new SaveReader( stream ).ReadFile() );

			if ( world.Problem != null )
				Log.Warning( $"{themeName}: the park file stopped being readable partway - {world.Problem}" );
			else if ( !world.ClosedOnTrailer )
				Log.Warning( $"{themeName}: the park file's world block did not end where it should have" );

			Log.Info( $"{themeName}: park file holds {world.ThingCount} things, {world.Objects.Count} of them objects" );

			return world;
		}
		catch ( Exception e )
		{
			// A park worth looking at without its shops beats no park at all.
			Log.Warning( $"{themeName}: the park file would not read, so the park is empty - {e.Message}" );
			return null;
		}
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

			model.SetTransform( OriginFor( placed, item ), Turn( placed.Angle ) );

			_models.Add( model );
		}
		catch ( Exception e )
		{
			Log.Warning( $"{ThemeName}: '{item.Name}' would not load, so it is missing from ({placed.CellX},{placed.CellY}) - {e.Message}" );
		}
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
	/// So the anchor is the cell's corner, not its middle. The turn, though, has to happen about the
	/// footprint's middle or a rotated item would swing off the ground it was given - hence rotating the
	/// centre and putting it back.
	/// </para>
	/// </summary>
	private static Vector3 OriginFor( ParkWorld.CatalogueObject placed, ParkItemCatalogue.Item item )
	{
		var field = ParkGround.Current?.Heightfield;

		var cellX = field?.CellSizeX ?? DefaultCellSize;
		var cellY = field?.CellSizeY ?? DefaultCellSize;

		// The ground under the anchor corner. The original flattens the land beneath an item as it is
		// built - its .sam has a DontDeformBase key for the exceptions - and nothing here does, so an
		// object on a slope sits at one corner's height rather than being bedded into it.
		var height = field?.HeightAt( placed.CellX, placed.CellY ) ?? 0f;

		var corner = new Vector3( placed.CellX * cellX, placed.CellY * cellY, height );
		var centre = new Vector3( item.Width * cellX * 0.5f, item.Depth * cellY * 0.5f, 0f );

		var turned = (Vector3)System.Numerics.Vector3.Transform( centre.GetSystemVector3(), Turn( placed.Angle ) );

		return corner + centre - turned;
	}

	/// <summary>A cell's size in world units, for the one case where there is no ground to ask.</summary>
	private const float DefaultCellSize = 10f;

	/// <summary>
	/// How far round an object stands, about the world's up axis - which is Z here, where the original's
	/// is Y.
	///
	/// <para>
	/// <b>Which way the original turns is not settled.</b> The saved angles are whole degrees - only 0, 90
	/// and 270 occur in Lost Kingdom - but nothing has been traced that says whether they run clockwise or
	/// anticlockwise, and there is nothing to check it against: the attribute map marks only the entrance,
	/// the roads and the ticket booths, so it says nothing about what an object faces. Every rotated item
	/// in the shipped park has a square footprint, so the choice cannot move one off its own ground either
	/// way - it only decides which side of a toilet the door is on.
	/// </para>
	/// </summary>
	private static Quaternion Turn( int degrees )
		=> Quaternion.CreateFromAxisAngle( System.Numerics.Vector3.UnitZ, degrees * (MathF.PI / 180f) );

	protected override void OnUpdate()
	{
		foreach ( var model in _models )
			model.Update( Time.Delta );
	}
}
