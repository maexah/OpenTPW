namespace OpenTPW;

/// <summary>
/// Everything a theme can have standing in it, indexed by the number a saved park stores to name it.
///
/// <para>
/// A park save says "item 1402 is at cell (55,15)" and nothing more - no path, no name, no size. The
/// number is the item's own <c>Info.Id</c>, declared in the <c>.sam</c> inside its <c>.wad</c>, so the
/// only way to turn one back into a model is to read the theme's items and see which one claims it.
/// That is what this does, once, when a park is built.
/// </para>
///
/// <para>
/// The four folders are the four the save's own feature list names - <c>Features</c>, <c>Shops</c>,
/// <c>Rides</c> and <c>Sideshow</c>. Each <c>.wad</c> in them stands in for a directory of its own name,
/// the same way terrain.wad does, so an item's description is at
/// <c>levels/&lt;theme&gt;/&lt;folder&gt;/&lt;name&gt;/&lt;name&gt;.sam</c>.
/// </para>
///
/// <para>
/// Ids are banded, which is a useful sanity check when reading one: 11xx rides, 12xx shops, 13xx
/// sideshows, 14xx features, 15xx upgrades, and 16xx the fixed items every park has.
/// </para>
/// </summary>
public sealed class ParkItemCatalogue
{
	/// <summary>
	/// One buildable item: what it is called, where its model lives, how much ground it covers, and the
	/// artwork for its name board if it has one - see <see cref="ParkObjects"/>, which paints it.
	/// </summary>
	/// <param name="UiType">Which kind it is - 0 rides, 1 shops, 2 sideshows, 3 features.</param>
	/// <param name="IsChoosable">
	/// Whether a guest may be sent here at all. <b>This is the item file's side of the save's own
	/// "offerable" flag</b>, and the two agree object for object in the shipped park - see
	/// <see cref="ItemDescriptionFile.IsChoosable"/>.
	/// </param>
	public readonly record struct Item( int Id, string Name, string Directory, string Stem, int Width, int Depth,
		string? SignPath, int UiType = ItemDescriptionFile.Feature, bool IsChoosable = false,
		bool ProvidesRelief = false, bool HasQueue = false, bool IsIndoors = false,
		int ExcitementLevel = 0, int AttractionValue = 0, int NewAttractionDecayTime = 0,
		int ThirstEffect = 0, int HungerEffect = 0, int VomitEffect = 0, int HappinessEffect = 0,
		int LitterEffect = 0, int TrackType = 0 );

	private readonly Dictionary<int, Item> _items = [];

	/// <summary>How many items this theme turned out to offer.</summary>
	public int Count => _items.Count;

	/// <summary>
	/// The folders holding free-standing items. <c>upgrades</c> is deliberately not among them: its
	/// contents are tunnels and jumps that attach to a ride rather than stand on their own, and no park
	/// the game ships places one as an object.
	/// </summary>
	private static readonly string[] Folders = ["features", "shops", "rides", "sideshow"];

	/// <summary>
	/// Where the items are read from. A running game passes nothing and gets the one it mounted; a test
	/// passes its own, so that cataloguing a real theme needs no global to have been set - which is the
	/// rule every other park test already follows.
	/// </summary>
	private readonly BaseFileSystem _files;

	public ParkItemCatalogue( string themeName, BaseFileSystem? files = null )
	{
		_files = files ?? FileSystem;

		var theme = themeName.ToLowerInvariant();
		var unreadable = 0;

		foreach ( var folder in Folders )
		{
			var path = $"levels/{theme}/{folder}";

			string[] directories;

			try
			{
				directories = _files.GetDirectories( path );
			}
			catch ( Exception e )
			{
				// A theme without one of these folders is not broken - it simply sells nothing of that
				// kind - so this reports and carries on.
				Log.Info( $"{themeName}: no {folder} to catalogue - {e.Message}" );
				continue;
			}

			// The folder's own description, which every item in it inherits from. An item's file says only
			// what DIFFERS from this, so reading an item without it misses which kind the item is, whether
			// a guest may choose it, and whether it has a queue - none of which most items restate.
			var category = TryReadCategory( path, folder );

			foreach ( var directory in directories )
			{
				var stem = Path.GetFileName( directory );

				if ( string.IsNullOrEmpty( stem ) )
					continue;

				if ( !TryRead( $"{path}/{stem}", stem, out var item, category ) )
				{
					++unreadable;
					continue;
				}

				// Last one wins, and says so. Nothing in the shipped themes collides, so a collision is
				// worth hearing about rather than resolving quietly.
				if ( _items.TryGetValue( item.Id, out var existing ) )
					Log.Warning( $"{themeName}: items '{existing.Stem}' and '{stem}' both claim catalogue number {item.Id}" );

				_items[item.Id] = item;
			}
		}

		Log.Info( $"{themeName}: catalogued {Count} items" + (unreadable > 0 ? $", and {unreadable} would not read" : "") );
	}

	/// <summary>
	/// Reads one item's description, or answers false if that directory does not hold one - which is not
	/// an error worth a line of its own, because these folders can hold things that are not items.
	/// </summary>
	private bool TryRead( string directory, string stem, out Item item, ItemDescriptionFile? category = null )
	{
		item = default;

		try
		{
			using var stream = _files.OpenRead( $"{directory}/{stem}.sam" );
			var description = new ItemDescriptionFile( stream, category );

			if ( description.Id <= 0 )
				return false;

			// A ride keeps the artwork for its name board beside its model, always under the item's own
			// name - seventy-eight of the seventy-eight that have one, across all four themes.
			var sign = $"{directory}/{stem}.sgn";

			item = new Item( description.Id, description.Name, directory, stem,
				description.FootprintWidth, description.FootprintDepth,
				Exists( sign ) ? sign : null,
				description.WhichUIType, description.IsChoosable, description.ProvidesRelief,
				description.HasQueue, description.IsIndoors, description.ExcitementLevel,
				description.AttractionValue, description.NewAttractionDecayTime,
				description.ThirstEffect, description.HungerEffect, description.VomitEffect,
					description.HappinessEffect, description.LitterEffect, description.TrackType );

			return true;
		}
		catch ( Exception )
		{
			return false;
		}
	}

	/// <summary>
	/// The folder's own description - <c>rides/Rides.sam</c> and its three siblings - whose values every
	/// item in that folder inherits. Null where the folder has none, which leaves each item standing on
	/// its own file alone.
	/// </summary>
	/// <remarks>
	/// <b>The name is asked for in the folder's own case and found in the file's.</b> The folders are
	/// lower case and the files are not - <c>sideshow/SideShow.sam</c>, <c>rides/Rides.sam</c> - so this
	/// leans on the same Windows-style case-insensitive matching every other path in the game does. If
	/// that ever stopped working the symptom would be quiet: every item would read as unchoosable, because
	/// almost none of them restate it.
	/// </remarks>
	private ItemDescriptionFile? TryReadCategory( string path, string folder )
	{
		try
		{
			using var stream = _files.OpenRead( $"{path}/{folder}.sam" );

			return new ItemDescriptionFile( stream );
		}
		catch ( Exception e )
		{
			Log.Info( $"no category description for '{folder}', so its items stand on their own files - {e.Message}" );

			return null;
		}
	}

	/// <summary>
	/// Whether the file system can offer this path at all. It asks for a size rather than using
	/// <c>FileExists</c>, which does not look inside archives - and every one of these lives in a .wad.
	/// </summary>
	private bool Exists( string path )
	{
		try
		{
			return _files.GetSize( path ) > 0;
		}
		catch ( Exception )
		{
			return false;
		}
	}

	/// <summary>The item that calls itself <paramref name="id"/>, if this theme has one.</summary>
	public bool TryGet( int id, out Item item ) => _items.TryGetValue( id, out item );
}
