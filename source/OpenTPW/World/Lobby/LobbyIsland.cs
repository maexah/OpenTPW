using System.Globalization;
using System.Numerics;

namespace OpenTPW;

public sealed class LobbyIsland : Entity
{
	/// <summary>
	/// Where the lobby camera looks while this island is the one on show: the island itself,
	/// raised by the height its script asks for. The four parks differ a lot here - 12.5 for the
	/// jungle against 38 for hallow - so this is not something the camera can assume.
	/// </summary>
	public Vector3 CameraTarget => Position + (Vector3.Up * Script.CameraHeight);

	/// <summary>This island's place in the lobby's running order, taken from its script.</summary>
	public int Index => Script.Index;

	/// <summary>
	/// Everything this park's script says - its weather included. Read once here and then read
	/// back by whatever needs it; <see cref="LobbyWeather"/> takes the rain, lightning and sky
	/// off whichever island the camera is currently on, which is how the original does it too.
	/// </summary>
	public LobbyScript Script { get; }

	/// <summary>The park's name as the player sees it, painted on the gate's sign.</summary>
	public string ParkName { get; }

	/// <summary>
	/// What this park is called in the data - "Jungle", "Hallow" and so on. Not what the player
	/// is shown (see <see cref="ParkName"/>); this is the folder its models, script and sound
	/// banks are under.
	/// </summary>
	public string ThemeName { get; }

	/// <summary>
	/// What each park is actually called.
	///
	/// A park's ISLAND() line does carry a name, but for three of the four it is the backend one
	/// - "Fantasy", "Halloween", "Space" - and only the jungle's happens to be what the player is
	/// shown. The displayed names are rows of <c>THEMENAMES.str</c>, looked up by index rather than
	/// by text (docs/exe/lobby.md, "Park names and the locale tables"), which
	/// <c>ParkFixedItems.ParkDisplayName</c> already reads for the gate's sign. This table still holds
	/// them by hand, and a park outside these four falls back to whatever its script says.
	///
	/// All four are two words, which is what puts one on each of the sign's two panels.
	/// </summary>
	private static readonly Dictionary<string, string> DisplayNames = new( StringComparer.OrdinalIgnoreCase )
	{
		["Jungle"] = "Lost Kingdom",
		["Fantasy"] = "Wonder Land",
		["Hallow"] = "Halloween World",
		["Space"] = "Space Zone"
	};

	/// <summary>
	/// How many golden keys it takes to enter this park: Keys.CostToEnter in the park's own
	/// levels\&lt;park&gt;\global.sam - one for the jungle and hallow, three for fantasy, five for space.
	///
	/// The lobby panel asks the park's global.sam for it (0x005b11a0, through 0x005b11c0, which loads
	/// data\levels\%s\global.sam) and shows it with a numbers mesh that runs from "x 1" to "x 5" -
	/// which is also exactly the range the four parks' values cover. The advisor's tour agrees: the
	/// one key he gives a new player opens Halloween World and Lost Kingdom and no more.
	/// </summary>
	public int KeysToEnter { get; }

	/// <summary>
	/// Whether the park's global.sam loaded. The original keeps a record per park that stands only while it does
	/// (0x005b09c0 through 0x005b11c0), and Enter this park refuses a park without one (0x005e1da3). The original looks
	/// again each time; this is read once, with the price.
	/// </summary>
	public bool GlobalLoaded { get; }

	/// <summary>
	/// This island's gate, which is the way into its park and swings open as the player goes through.
	/// </summary>
	public LobbyGate Gate { get; }

	private readonly LobbyModel _model;

	/// <summary>
	/// The two sign panels painted for this island, kept only so that they can be let go of again.
	/// They are built from pixels rather than loaded by path, so they are in no cache and nothing
	/// else holds them: the gate is handed the same two and draws with them, but does not own them.
	/// </summary>
	private readonly IReadOnlyDictionary<string, Texture>? _sign;

	public LobbyIsland( Vector3 _position, string themeName )
	{
		Position = _position;

		ThemeName = themeName;
		(KeysToEnter, GlobalLoaded) = ReadKeysToEnter( themeName );

		var modelPrefix = themeName[0..3];
		Script = LobbyScript.Read( themeName );

		ParkName = DisplayNames.TryGetValue( themeName, out var displayName )
			? displayName
			: Script.ParkName;

		// Offered to both models below: the sign panels are meshes of the island for the jungle
		// and hallow, but of the gate for fantasy and space, and whichever model names sign1 and
		// sign2 is the one that takes them.
		var sign = _sign = BuildSign( modelPrefix, ParkName );

		// An island's meshes sit 2.5 units below the origin it is placed at.
		_model = new LobbyModel(
			$"lobby/terrain/{modelPrefix}_isle.md2",
			"lobby/terrain/textures",
			Position - new Vector3( 0, 0, 2.5f ),
			textureOverrides: sign );

		// An island brings its gate with it, the way a park's script names both models on one
		// line. Kept rather than dropped because the park-entry flight opens it, and Escape shuts it
		// again - see <see cref="LobbyGate"/>.
		Gate = new LobbyGate( Position, themeName, sign );

		// ...and whatever its script says flies around it: ten of each butterfly for the jungle,
		// fifty bats for hallow, nothing at all for the other two.
		foreach ( var mesh in Script.FlyingMeshes )
			LobbyFlyer.Spawn( mesh, this );
	}

	/// <summary>
	/// Lets go of the sign panels this island painted.
	/// </summary>
	/// <remarks>
	/// The models and materials go with the entities that hold them, but a material leaves the
	/// textures bound into it alone - those are normally cached by path and shared between scenes.
	/// These are not: they are cut from a board rasterised for this island alone, so every lobby
	/// build left two behind.
	/// </remarks>
	protected override void OnDelete()
	{
		if ( _sign == null )
			return;

		foreach ( var panel in _sign.Values )
			panel.Delete();
	}

	/// <summary>
	/// The sign's two panels, or null to leave the model's placeholder textures in place.
	///
	/// The .sgn sits beside the model that carries the sign meshes and is named after it, which
	/// is the island for the jungle and hallow but the gate for fantasy and space - so both are
	/// tried rather than assuming either.
	/// </summary>
	private static IReadOnlyDictionary<string, Texture>? BuildSign( string modelPrefix, string parkName )
	{
		if ( string.IsNullOrEmpty( parkName ) )
			return null;

		foreach ( var model in new[] { "isle", "gate" } )
		{
			if ( !SignTexture.TryBuild( $"lobby/terrain/{modelPrefix}_{model}.sgn", parkName, out var left, out var right ) )
				continue;

			Log.Info( $"{modelPrefix}: painted '{parkName}' onto the {modelPrefix}_{model} sign" );

			return new Dictionary<string, Texture>( StringComparer.OrdinalIgnoreCase )
			{
				["sign1"] = left!,
				["sign2"] = right!
			};
		}

		Log.Warning( $"{modelPrefix}: no usable sign file - the sign keeps its placeholder texture" );
		return null;
	}

	private static (int Keys, bool Loaded) ReadKeysToEnter( string themeName )
	{
		SettingsFile global;

		try
		{
			global = new SettingsFile( $"/levels/{themeName.ToLowerInvariant()}/global.sam" );
		}
		catch ( Exception e )
		{
			Log.Warning( $"{themeName}: global.sam would not load - {e.Message}; Enter this park will refuse it" );
			return (0, false);
		}

		if ( int.TryParse( global["Keys.CostToEnter"], NumberStyles.Integer, CultureInfo.InvariantCulture, out var keys ) )
			return (keys, true);

		Log.Warning( $"{themeName}: no Keys.CostToEnter - the park costs nothing to enter" );
		return (0, true);
	}

	/// <summary>
	/// Where a named node of this island's model sits in the world, if it has one - see
	/// <see cref="LobbyModel.TryGetNode"/>. This is how a place marked in the model, rather than a
	/// place written down here, is found.
	/// </summary>
	public bool TryGetNode( string name, out Vector3 position ) => _model.TryGetNode( name, out position );

	protected override void OnUpdate()
	{
		_model.Update( Time.Delta );
	}
}
