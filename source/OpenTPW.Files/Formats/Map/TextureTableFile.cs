using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace OpenTPW;

/// <summary>
/// A theme's texture correspondence table - the plain-text <c>.tct</c> inside its <c>terrain.wad</c>,
/// named after the theme (<c>Jungle.tct</c>). It says which texture a tile index draws, which is the
/// one thing a park's paths need and cannot get anywhere else: the ground takes its names from the
/// model's own frame table, but a path tile is named only here.
///
/// <para>
/// The shape is a section name alone on a line, then <c>index</c>, a tab, and a file name. Lines
/// starting <c>#</c> are comments, and the jungle's include a whole commented-out <c>#Water</c>
/// section - so a reader that ignored the <c>#</c> would invent a section the game does not have.
/// </para>
///
/// <para>
/// <b>The names end <c>.tga</c> and the files beside them are <c>.wct</c></b>, which is the same
/// translation the ground already makes. It is deliberately not done here: this reports what the file
/// says, and the consumer swaps the extension.
/// </para>
///
/// <para>
/// Two details are the file's own and neither is a parsing fault. Jungle's <c>PathTex</c> lists
/// <c>jpa_ctr1</c> at both 13 and 14, and the original author says why in a comment at the top of the
/// file - <i>"14 has be put back as a dummy texture - maybe fix later ?"</i>. And <c>QueueTex</c>'s
/// names are not in numeric order: 0 is <c>jpa_que4</c> and 2 is <c>jpa_que1</c>. So an index is a
/// position in this table and nothing may be inferred from the name at it.
/// </para>
/// </summary>
public sealed class TextureTableFile
{
	/// <summary>Every section the file declares, in the order it declares them.</summary>
	public IReadOnlyList<string> SectionNames => _order;

	private readonly List<string> _order = [];

	private readonly Dictionary<string, Dictionary<int, string>> _sections =
		new( StringComparer.OrdinalIgnoreCase );

	public TextureTableFile( Stream stream )
	{
		using var memory = new MemoryStream();
		stream.CopyTo( memory );

		var text = Encoding.ASCII.GetString( memory.GetBuffer(), 0, (int)memory.Length );
		Dictionary<int, string>? section = null;

		foreach ( var raw in text.Split( '\n' ) )
		{
			var line = raw.Trim();

			if ( line.Length == 0 || line[0] == '#' )
				continue;

			// A row is "<index><whitespace><name>"; anything else on its own is a section heading. Split
			// on whitespace rather than on a tab alone, so a file spaced differently still reads.
			var parts = line.Split( [' ', '\t'], 2, StringSplitOptions.RemoveEmptyEntries );

			if ( parts.Length == 2 && int.TryParse( parts[0], out var index ) )
			{
				// A row before any heading has nothing to belong to. Keep reading rather than throwing:
				// the rest of the file is still worth having.
				section?.TryAdd( index, parts[1].Trim() );
				continue;
			}

			if ( _sections.TryGetValue( line, out var existing ) )
			{
				section = existing;
				continue;
			}

			section = [];
			_sections[line] = section;
			_order.Add( line );
		}
	}

	/// <summary>
	/// What a section calls the texture at an index, or empty where it names none. Empty rather than a
	/// throw because a park may legitimately never use half the table, and a missing row should cost one
	/// tile rather than the whole surface.
	/// </summary>
	public string NameFor( string section, int index )
		=> _sections.TryGetValue( section, out var rows ) && rows.TryGetValue( index, out var name )
			? name
			: string.Empty;

	/// <summary>Every row of a section, by index, or empty where there is no such section.</summary>
	public IReadOnlyDictionary<int, string> Rows( string section )
		=> _sections.TryGetValue( section, out var rows )
			? rows
			: new Dictionary<int, string>();
}
