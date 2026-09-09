using System.Globalization;

namespace OpenTPW;

/// <summary>
/// A lobby script - one of the plain-text files in lobby.wad that describe the lobby and the
/// four parks in it.
///
/// The original's parser is one function (FUN_005e3210 in the decompile) which splits the file
/// on newlines and strncmps each line against a table of twelve keywords sitting contiguously in
/// the executable at 0x00774ce0:
///
///     ISLANDCAMERAPOSITION(  GLOBERADIUSIN(  GLOBERADIUSOUT(  VERTICALOFFSET(
///     SPINRADIUS(  SPINSPEED(  ISLANDFOV(  SKYCOLOUR(  RAINY(  LIGHTNING(
///     FLYINGMESH(  ISLAND(
///
/// That table is the whole vocabulary - there is nothing else a lobby script can say. Anything
/// else the lobby does is either a model animation or hard-coded.
///
/// Having found a keyword the original scans forward to '(' and then reads field by field,
/// stopping each field at ',', '"' or ')' as appropriate. <see cref="Arguments"/> does the same
/// thing in one pass, which is why one reader serves every directive here.
/// </summary>
public sealed record LobbyScript
{
	/// <summary>A park with no script at all, so a missing file degrades rather than throws.</summary>
	public static readonly LobbyScript Empty = new();

	/// <summary>This park's place in the lobby's running order.</summary>
	public int Index { get; init; }

	/// <summary>The park's name as its ISLAND() line gives it - see LobbyIsland for what's shown.</summary>
	public string ParkName { get; init; } = string.Empty;

	/// <summary>Where this island sits around the lobby globe, in degrees.</summary>
	public float Heading { get; init; }

	/// <summary>How far above the island the lobby camera looks. 12.5 for the jungle, 38 for hallow.</summary>
	public float CameraHeight { get; init; } = 12.5f;

	/// <summary>SKYCOLOUR, as a 0-1 colour. The original stores it as 16.16 fixed point.</summary>
	public Vector3 SkyColour { get; init; } = DefaultSkyColour;

	/// <summary>Whether this park's SKYCOLOUR line was actually there.</summary>
	public bool HasSkyColour { get; init; }

	/// <summary>
	/// RAINY(n). The original copies the *currently selected* island's value into a single global
	/// rain level every frame, so this is not per-island weather so much as weather that follows
	/// whichever park you are looking at. Only hallow sets it, and only to 1.
	/// </summary>
	public int Rainy { get; init; }

	/// <summary>
	/// LIGHTNING(n), which is a probability mask rather than a count or a period. Each frame the
	/// original draws a random number and strikes when
	///
	///     (n &amp; random) == 1
	///
	/// so a strike needs bit 0 of n set and every other bit of the masked value clear: the chance
	/// is 1 in 2^(bits set in n), or never if n is even. Only hallow sets it, to 63 - six bits -
	/// which is a one in sixty-four chance per frame. See <see cref="StrikesPerSecond"/>.
	/// </summary>
	public int Lightning { get; init; }

	/// <summary>Every FLYINGMESH line, in the order the script gives them.</summary>
	public IReadOnlyList<FlyingMesh> FlyingMeshes { get; init; } = Array.Empty<FlyingMesh>();

	/// <summary>
	/// One FLYINGMESH line:
	///
	///     FLYINGMESH("data\lobby\terrain","bfly_PINK",10,200.0,100.0,200.0,1.5)
	///
	/// The original passes the count, the model, the island's own position, the three floats as a
	/// volume to wander, the scale, and a hard-coded 500.0 to its flying-mesh spawner.
	///
	/// The three volume floats are identical in both scripts that use them (200, 100, 200), so
	/// there is no per-park variation in them to preserve, and they are in the original's globe
	/// space - islands there sit on a sphere of radius 475 - rather than the flat arrangement
	/// this draws. They are read and kept for the record; <see cref="LobbyFlyer"/> flies its own
	/// volume. The count and the scale are used as given.
	/// </summary>
	public readonly record struct FlyingMesh( string Directory, string Model, int Count, Vector3 Volume, float Scale )
	{
		/// <summary>
		/// The scale to actually build the model at.
		///
		/// The script's number is not this engine's model scale. The butterflies were sized by
		/// eye long before their script was read, and settled at 0.75 against a script that asks
		/// for 1.5 - so whatever the original means by it, one of its units is half of one of
		/// ours. Taken literally the jungle's butterflies come out as big as its palm trees and
		/// hallow's bats as big as its clock tower, which is what shipping it unconverted looked
		/// like.
		///
		/// Converting rather than hard-coding keeps what the data actually distinguishes: bats
		/// are asked for at 2.5 against the butterflies' 1.5, and their mesh is authored 1.7x
		/// larger again, so they stay markedly the bigger animal.
		/// </summary>
		public float EngineScale => Scale * ScriptScaleToEngine;
	}

	/// <summary>See <see cref="FlyingMesh.EngineScale"/>.</summary>
	private const float ScriptScaleToEngine = 0.5f;

	/// <summary>The sky the lobby falls back to - what Sky was built with before any park asked.</summary>
	public static readonly Vector3 DefaultSkyColour = new( 79 / 255f, 214 / 255f, 255 / 255f );

	/// <summary>
	/// Lightning strikes per second, converted off the original's per-frame chance.
	///
	/// The test above runs once per frame, so taken literally the storm gets worse the faster
	/// your machine is. At the 25fps the rest of the game's data assumes it comes out as a
	/// strike every 64 frames on average - about one every two and a half seconds - and that is
	/// what this returns, so the storm is the same on any hardware.
	/// </summary>
	public float StrikesPerSecond => (Lightning & 1) == 0
		? 0f
		: MeshAnimator.FramesPerSecond / (1 << System.Numerics.BitOperations.PopCount( (uint)Lightning ));

	/// <summary>
	/// Reads a park's script - lobby/jungle.txt and friends. A missing or unreadable file gives
	/// <see cref="Empty"/> rather than throwing, because this runs during scene setup.
	/// </summary>
	public static LobbyScript Read( string themeName )
	{
		using var stream = FileSystem.OpenRead( $"lobby/{themeName.ToLowerInvariant()}.txt" );

		if ( stream == null )
		{
			Log.Warning( $"No lobby script for '{themeName}' - it gets no name, weather or flyers" );
			return Empty;
		}

		using var reader = new StreamReader( stream );

		var script = Empty;
		var flyers = new List<FlyingMesh>();

		while ( reader.ReadLine() is { } line )
		{
			var trimmed = line.TrimStart();

			// ISLAND( has to be tested against the whole keyword including its bracket, or it
			// also matches ISLANDFOV( and ISLANDCAMERAPOSITION(.
			if ( Arguments( trimmed, "ISLAND", out var island ) && island.Length >= 7 )
			{
				script = script with
				{
					Index = Integer( island[0] ),
					ParkName = island[4],
					Heading = Number( island[5] ),
					CameraHeight = Number( island[6], Empty.CameraHeight )
				};
			}
			else if ( Arguments( trimmed, "FLYINGMESH", out var flyer ) && flyer.Length >= 7 )
			{
				flyers.Add( new FlyingMesh(
					flyer[0],
					flyer[1],
					Integer( flyer[2] ),
					new Vector3( Number( flyer[3] ), Number( flyer[4] ), Number( flyer[5] ) ),
					Number( flyer[6], 1f ) ) );
			}
			else if ( Arguments( trimmed, "SKYCOLOUR", out var sky ) && sky.Length >= 3 )
			{
				script = script with
				{
					HasSkyColour = true,
					SkyColour = new Vector3(
						Integer( sky[0] ) / 255f,
						Integer( sky[1] ) / 255f,
						Integer( sky[2] ) / 255f )
				};
			}
			else if ( Arguments( trimmed, "RAINY", out var rainy ) && rainy.Length >= 1 )
			{
				script = script with { Rainy = Integer( rainy[0] ) };
			}
			else if ( Arguments( trimmed, "LIGHTNING", out var lightning ) && lightning.Length >= 1 )
			{
				script = script with { Lightning = Integer( lightning[0] ) };
			}
		}

		return script with { FlyingMeshes = flyers };
	}

	/// <summary>
	/// Reads a single-number directive out of a line - SPINRADIUS(70) and the rest of lobby.txt's
	/// camera settings.
	/// </summary>
	public static bool TryReadSetting( string line, string name, out float value )
	{
		value = 0f;

		return Arguments( line.TrimStart(), name, out var arguments )
			&& arguments.Length >= 1
			&& float.TryParse( arguments[0], NumberStyles.Float, CultureInfo.InvariantCulture, out value );
	}

	/// <summary>
	/// Splits `NAME(a,"b",c)` into its arguments, with quotes stripped and whitespace trimmed,
	/// or returns false if the line isn't that directive.
	///
	/// Matching includes the opening bracket, the way the original's strncmp lengths do, so
	/// ISLAND does not match ISLANDFOV. A quoted field is taken whole, so a comma or a bracket
	/// inside quotes doesn't split it - none of the shipped scripts do that, but a path field is
	/// the obvious place it could happen.
	/// </summary>
	private static bool Arguments( string line, string name, out string[] arguments )
	{
		arguments = Array.Empty<string>();

		if ( !line.StartsWith( name, StringComparison.OrdinalIgnoreCase )
			|| line.Length <= name.Length
			|| line[name.Length] != '(' )
			return false;

		var fields = new List<string>();
		var field = new System.Text.StringBuilder();
		var quoted = false;

		for ( int i = name.Length + 1; i < line.Length; ++i )
		{
			var c = line[i];

			if ( c == '"' )
			{
				quoted = !quoted;
				continue;
			}

			if ( !quoted && (c == ',' || c == ')') )
			{
				fields.Add( field.ToString().Trim() );
				field.Clear();

				if ( c == ')' )
					break;

				continue;
			}

			field.Append( c );
		}

		// An unterminated line still yields what it managed to read, which is how a truncated
		// script degrades to defaults rather than to nothing.
		if ( field.Length > 0 )
			fields.Add( field.ToString().Trim() );

		arguments = [.. fields];
		return arguments.Length > 0;
	}

	private static int Integer( string text )
		=> int.TryParse( text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value ) ? value : 0;

	/// <summary>Invariant culture because the scripts write 12.5 whatever the machine's locale does.</summary>
	private static float Number( string text, float fallback = 0f )
		=> float.TryParse( text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value ) ? value : fallback;
}
