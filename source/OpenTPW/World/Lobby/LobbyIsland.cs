using System.Numerics;

namespace OpenTPW;

public sealed class LobbyIsland : Entity
{
	private readonly LobbyModel _model;

	public LobbyIsland( Vector3 _position, string themeName )
	{
		Position = _position;

		var modelPrefix = themeName[0..3];

		// An island's meshes sit 2.5 units below the origin it is placed at.
		_model = new LobbyModel(
			$"lobby/terrain/{modelPrefix}_isle.md2",
			"lobby/terrain/textures",
			Position - new Vector3( 0, 0, 2.5f ),
			textureOverrides: BuildSign( modelPrefix, ReadParkName( themeName ) ) );
	}

	/// <summary>
	/// The name this park shows on its sign, read from the lobby script the game ships rather
	/// than from a literal here - the jungle's is "Lost Kingdom", which nothing in the theme name
	/// or the model prefix would tell you.
	///
	/// Each park has a script at the root of lobby.wad named after it, holding one ISLAND() line:
	///
	///     ISLAND(0,"data\lobby\terrain","jun_isle","jun_gate","Lost Kingdom",90.0,12.5)
	///
	/// The name is its fourth quoted field. The rest of the line - the terrain directory, the two
	/// model names, and two numbers that look like a heading and a size - is what Level.cs
	/// currently hardcodes, so this file is worth coming back to.
	/// </summary>
	private static string ReadParkName( string themeName )
	{
		using var stream = FileSystem.OpenRead( $"lobby/{themeName.ToLowerInvariant()}.txt" );

		if ( stream == null )
			return string.Empty;

		using var reader = new StreamReader( stream );

		while ( reader.ReadLine() is { } line )
		{
			// ISLANDFOV and ISLANDCAMERAPOSITION share the prefix but not the bracket.
			if ( !line.TrimStart().StartsWith( "ISLAND(", StringComparison.OrdinalIgnoreCase ) )
				continue;

			var quoted = line.Split( '"' );

			// Quoted fields land on the odd indices; the name is the fourth of them.
			if ( quoted.Length > 7 )
				return quoted[7];
		}

		return string.Empty;
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

	protected override void OnUpdate()
	{
		_model.Update( Time.Delta );
	}
}
