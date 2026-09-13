using System.Text;

namespace OpenTPW;

/// <summary>
/// One buildable item's description - the <c>.sam</c> that sits inside the item's own <c>.wad</c>, which
/// the file itself calls a "Theme Park 2 Ride Description File" whether it describes a ride, a shop, a
/// sideshow or a litter bin.
///
/// <para>
/// This exists rather than a plain <see cref="SettingsFile"/> because the part that matters here is the
/// part that file cannot reach. <c>Info.Shape</c> is not a value but a block - a row of dashes, an ASCII
/// picture of the item's footprint, another row of dashes - and <see cref="SAMParser"/> reads a value as
/// the single word after the key, so it comes back as "---" with the picture left behind as unparsed
/// junk. The scalars are read the same way the parser would; only the block needs its own handling.
/// </para>
///
/// <para>
/// <b>The footprint is the grid's bounding box, not the cells drawn in it.</b> That is worth stating
/// because the picture invites the opposite reading: <c>4x4rock</c> draws fourteen stars inside a four by
/// four box, <c>5x5rck</c> twenty-three inside five by five, and <c>ground</c> draws none at all - yet
/// every one of the jungle's seventy items has a <c>.hmp</c> whose length says width times height, all
/// seventy, including the three that would otherwise disagree.
/// </para>
/// </summary>
public sealed class ItemDescriptionFile
{
	/// <summary>
	/// The catalogue number, which is what a saved park stores to say which item is standing somewhere.
	/// They are banded: 11xx rides, 12xx shops, 13xx sideshows, 14xx features, 15xx upgrades and 16xx the
	/// fixed items that every park has.
	/// </summary>
	public int Id { get; private set; }

	/// <summary>What the item is called, as the game shows it - "Small Toilet", "Belly Bounce".</summary>
	public string Name { get; private set; } = string.Empty;

	/// <summary>How many cells across the item stands, and how many deep.</summary>
	public int FootprintWidth { get; private set; } = 1;

	public int FootprintDepth { get; private set; } = 1;

	public ItemDescriptionFile( Stream stream )
	{
		using var reader = new StreamReader( stream, Encoding.ASCII );
		Read( reader.ReadToEnd() );
	}

	public ItemDescriptionFile( string text ) => Read( text );

	private void Read( string text )
	{
		var lines = text.Split( '\n' );

		var width = 0;
		var depth = 0;

		for ( var i = 0; i < lines.Length; ++i )
		{
			var line = lines[i].TrimEnd( '\r' );
			var key = KeyOf( line );

			switch ( key )
			{
				case "Info.Id":
					if ( int.TryParse( ValueOf( line ), out var id ) )
						Id = id;
					break;

				case "Info.Name":
					Name = Quoted( line );
					break;

				// The original's own escape hatch, and in the whole jungle only the park gate uses it: its
				// picture is a single cell but it really stands six by three over the entrance. Where these
				// are present they win, because the game's own loader takes them in preference.
				case "Info.EngineFootprintWidthOverride":
					if ( int.TryParse( ValueOf( line ), out var overrideWidth ) && overrideWidth > 0 )
						FootprintWidth = overrideWidth;
					break;

				case "Info.EngineFootprintHeightOverride":
					if ( int.TryParse( ValueOf( line ), out var overrideDepth ) && overrideDepth > 0 )
						FootprintDepth = overrideDepth;
					break;

				case "Info.Shape":
					ReadShape( lines, i, out width, out depth );
					break;
			}
		}

		// The picture only fills in what no override has already claimed, so an item carrying one keeps it.
		if ( width > 0 && !HasOverride( text, "Info.EngineFootprintWidthOverride" ) )
			FootprintWidth = width;

		if ( depth > 0 && !HasOverride( text, "Info.EngineFootprintHeightOverride" ) )
			FootprintDepth = depth;
	}

	/// <summary>
	/// The footprint picture: the lines between the two rows of dashes that follow the key. Width is the
	/// longest row and depth the number of rows, both of the box the picture is drawn in rather than of
	/// the marks inside it.
	/// </summary>
	private static void ReadShape( string[] lines, int keyLine, out int width, out int depth )
	{
		width = 0;
		depth = 0;

		var at = keyLine + 1;

		while ( at < lines.Length && lines[at].Trim() != Fence )
			++at;

		for ( ++at; at < lines.Length && lines[at].Trim() != Fence; ++at )
		{
			var row = lines[at].TrimEnd( '\r', '\n', ' ', '\t' );

			// Blank rows inside the fence are layout, not footprint - counting them would make an item
			// deeper than it is.
			if ( row.Length == 0 )
				continue;

			width = Math.Max( width, row.Length );
			++depth;
		}
	}

	/// <summary>The row of dashes that opens and closes a block.</summary>
	private const string Fence = "---";

	private static bool HasOverride( string text, string key )
		=> text.Contains( key, StringComparison.OrdinalIgnoreCase );

	/// <summary>The key a line declares, or empty if it declares none - a comment, a blank, or part of a block.</summary>
	private static string KeyOf( string line )
	{
		var trimmed = line.TrimStart();

		if ( trimmed.Length == 0 || trimmed[0] == '#' )
			return string.Empty;

		var end = 0;

		while ( end < trimmed.Length && !char.IsWhiteSpace( trimmed[end] ) )
			++end;

		return trimmed[..end];
	}

	/// <summary>
	/// The first word after the key. Values carry an inline comment more often than not - the shipped
	/// files write things like <c>Info.IsChoosable 1 People CAN use this</c> - so everything past the
	/// first word is dropped rather than parsed.
	/// </summary>
	private static string ValueOf( string line )
	{
		var parts = line.Split( [' ', '\t'], StringSplitOptions.RemoveEmptyEntries );

		return parts.Length > 1 ? parts[1] : string.Empty;
	}

	/// <summary>
	/// What is between the first pair of quotes on the line. Names have spaces in them, which is exactly
	/// what the ordinary one-word read cannot carry: "Small Toilet" comes back as "Small.
	/// </summary>
	private static string Quoted( string line )
	{
		var open = line.IndexOf( '"' );

		if ( open < 0 )
			return string.Empty;

		var close = line.IndexOf( '"', open + 1 );

		return close > open ? line[(open + 1)..close] : string.Empty;
	}
}
