using System.Numerics;

namespace OpenTPW;

/// <summary>
/// A park's fixed items: the gate standing over the entrance and the traffic lights at the two
/// pedestrian crossings. They are "fixed" in the original's own sense - every park has them, in the
/// same place, and the player can neither build nor remove them.
///
/// <para>
/// <b>They carry no position in the save.</b> Their records in the saved thing list hold the sentinel
/// 128/128 that every unplaced thing holds, which is what makes them look absent. The positions are
/// baked into the models instead: each mesh's node transform is already in world coordinates, exactly
/// as <see cref="ParkTerrain"/>'s <c>base.MD2</c> is, so these load at the origin rather than being
/// placed. Their <c>.sam</c> says as much - <c>Info.DontApplyOffset 1</c>, "this is a fixed item whose
/// animation should be played relative to world (0,0), not the object pos".
/// </para>
///
/// <para>
/// That the models really are world-positioned is checked against a file that knows nothing about
/// them, <c>Standard.sam</c>: the gate's two door meshes sit at world x 470 and 490, bracketing the
/// two entrance cells <c>EntranceA</c> (47,17) and <c>EntranceB</c> (48,17), and the four traffic
/// lights land on <c>CrossingParkSideA/B</c> (47,9),(48,9) and <c>CrossingBSSideA/B</c> (47,5),(48,5)
/// - four out of four. A mesh bounding box would <i>not</i> have shown this: bounds are node-local and
/// say only how big a mesh is, never where it stands. See ParkFixedItemsTests.
/// </para>
///
/// <para>
/// <b>Only two of the six fixed items are here, and deliberately.</b> The theme also ships bus, ferry,
/// seaplane and end - and none of them is scenery: the bus sits at cell (29.7,-11.5) and the ferry at
/// (90.0,-17.6), both off the map on negative depth, the seaplane off it on negative x, and end.wad is
/// three aircraft 59 units in the air. They are vehicles parked at their spawns, driven by the
/// <c>.RSE</c> scripts whose runtime is not built (see <see cref="Ride"/>), and standing them still on
/// the ground would be worse than leaving them out.
/// </para>
///
/// <para>
/// The gate's doors animate without anything asked of them: <see cref="LobbyModel"/> looks for
/// companions with an M1, M2... suffix and the archive holds <c>gatesm1</c>, <c>gatesm2</c> and
/// <c>gatesm3</c>, which match case-insensitively. That is the opposite of the terrain's own
/// <c>basem.MD2</c>, whose bare <c>m</c> nothing picks up.
/// </para>
/// </summary>
public sealed class ParkFixedItems : Entity
{
	/// <summary>The theme folder these were loaded from - "jungle", "fantasy", "hallow" or "space".</summary>
	public string ThemeName { get; }

	private readonly List<LobbyModel> _models = [];

	/// <summary>
	/// The items to load, by the name of both the archive and the model inside it, and whether that
	/// model carries the park's name board. Only the gate does; the lights name no sign material, so
	/// offering them one would be noise.
	/// </summary>
	private static readonly (string Name, bool CarriesSign)[] Items = [("gates", true), ("lights", false)];

	public ParkFixedItems( string themeName )
	{
		ThemeName = themeName;
		Name = $"{themeName} fixed items";

		var features = $"levels/{themeName.ToLowerInvariant()}/features";

		foreach ( var (item, carriesSign) in Items )
		{
			// Each .wad stands in for a directory of its own name, the same way terrain.wad gives the
			// terrain its terrain/ paths.
			var directory = $"{features}/{item}";

			try
			{
				_models.Add( new LobbyModel(
					$"{directory}/{item}.MD2",
					$"{directory}/textures",
					Vector3.Zero,
					textureOverrides: carriesSign ? BuildSign( directory, themeName ) : null ) );
			}
			catch ( Exception e )
			{
				// A theme missing one of these is a broken installation, but the park is still worth
				// looking at without it - and saying which one is missing beats an empty entrance.
				Log.Warning( $"{themeName}: fixed item '{item}' would not load, so it is missing from the park - {e.Message}" );
			}
		}
	}

	/// <summary>
	/// The gate's name board, painted the same way the lobby paints the one on its islands: the
	/// artwork comes out of the gate's own <c>.sgn</c> and the lettering is rasterised over it, then cut
	/// into the two 128x128 panels the gateway mesh draws as 'sign1' and 'sign2'.
	///
	/// <para>
	/// Null leaves the model's own textures in place, which is what a theme wants when the paint fails:
	/// hallow ships real sign1.wct and sign2.wct beside its model, and the other three ship none, so
	/// those would fall back to the not-found texture instead. Overrides are taken ahead of the ones on
	/// disk, so this wins wherever it succeeds.
	/// </para>
	/// </summary>
	private static IReadOnlyDictionary<string, Texture>? BuildSign( string directory, string themeName )
	{
		var parkName = ParkDisplayName( themeName );

		if ( string.IsNullOrEmpty( parkName ) )
			return null;

		if ( !SignTexture.TryBuild( $"{directory}/gates.sgn", parkName, out var left, out var right ) )
			return null;

		Log.Info( $"{themeName}: painted '{parkName}' onto the park gate's sign" );

		return new Dictionary<string, Texture>( StringComparer.OrdinalIgnoreCase )
		{
			["sign1"] = left!,
			["sign2"] = right!
		};
	}

	/// <summary>
	/// What a park is called on its own gate, read from the shipped strings rather than written out
	/// here: <c>Language/English/THEMENAMES.str</c> holds exactly four entries, and they are the four
	/// park names.
	///
	/// <para>
	/// The order is the one below and was read out of the file's own header rather than assumed - the
	/// four records declare lengths 12, 15, 11 and 10 with their spaces at index 4, 9, 6 and 5, which
	/// only "Lost Kingdom", "Halloween World", "Wonder Land" and "Space Zone" fit. BFST stores indices
	/// into MBToUni.dat rather than text, so no plain search of the file shows this.
	/// </para>
	///
	/// <para>
	/// <see cref="LobbyIsland"/> keeps its own hardcoded copy of these four names. That is worth
	/// replacing with this read, but it is the lobby's to change and not this entity's errand.
	/// </para>
	/// </summary>
	private static string ParkDisplayName( string themeName )
	{
		var index = Array.FindIndex( ThemeOrder, name => string.Equals( name, themeName, StringComparison.OrdinalIgnoreCase ) );

		if ( index < 0 )
			return string.Empty;

		try
		{
			var names = new StringFile( "Language/English/THEMENAMES.str" );

			return index < names.Entries.Length ? names[index] : string.Empty;
		}
		catch ( Exception e )
		{
			Log.Warning( $"{themeName}: THEMENAMES.str would not read, so the gate keeps a blank sign - {e.Message}" );
			return string.Empty;
		}
	}

	/// <summary>The order THEMENAMES.str lists the parks in - see <see cref="ParkDisplayName"/>.</summary>
	private static readonly string[] ThemeOrder = ["jungle", "hallow", "fantasy", "space"];

	protected override void OnUpdate()
	{
		// Game time, not frame time - see GameClock.
		foreach ( var model in _models )
			model.Update( GameClock.Delta );
	}
}
