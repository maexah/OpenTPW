namespace OpenTPW;

/// <summary>
/// Which items the running park has researched, and to which upgrade tier - the researched flag and tier of each
/// of <c>CControlManager</c>'s records (<c>+0x10</c>, <c>+0x14</c>), which the buy list asks before it lists an item
/// (<c>0x004ab023</c>; <c>docs/exe/hud.md</c>, "What the buy list actually filters on").
///
/// <para>
/// <b>At a park load the save's word is the flag</b>: world setup seeds every catalogued item researched when its
/// <c>Upgrades[0].CostOfResearch</c> is nought (<c>0x004d3e92</c>), the save's <c>mObjectControls</c> is laid over
/// all of it (<c>0x005181e7</c>), and an item the save has no record of is seeded again (<c>FUN_00415140</c>). So an
/// item the save holds keeps the save's flag, whatever its file's cost.
/// </para>
///
/// <para>
/// <b>Research completing is not built</b> (<c>FUN_00504630</c>, which sets the flag and the tier): this game has no
/// research, and nothing here changes after the load. The points a researcher would hand the lab are counted where
/// the original hands them, <c>RESEARCH_POINTS_TO_THE_LAB</c> in <see cref="StaffBehaviour"/>.
/// </para>
/// </summary>
public sealed class ParkResearch
{
	private readonly Dictionary<int, (bool Researched, int Tier)> _items = [];

	/// <summary>How many items came from the save's records, and how many were seeded from their files.</summary>
	public int FromSave { get; }

	/// <inheritdoc cref="FromSave"/>
	public int Seeded { get; }

	/// <param name="records">The save's records, <see cref="ParkWorld.ObjectControlRecords"/>.</param>
	public ParkResearch( IEnumerable<ParkWorld.ObjectControl> records, ParkItemCatalogue catalogue )
	{
		foreach ( var record in records )
		{
			if ( _items.TryAdd( record.ItemId, (record.Researched, record.TierResearched) ) )
				++FromSave;
		}

		foreach ( var item in catalogue.All )
		{
			if ( _items.TryAdd( item.Id, (item.ResearchCost == 0, 0) ) )
				++Seeded;
		}

		Log.Info( $"Research: {_items.Values.Count( entry => entry.Researched )} of {_items.Count} items researched, "
			+ $"{FromSave} from the save's records and {Seeded} seeded from their files" );
	}

	/// <summary>Whether the item is researched - the flag the buy list lists by. An item nobody knows is not.</summary>
	public bool IsResearched( int itemId ) => _items.TryGetValue( itemId, out var entry ) && entry.Researched;

	/// <summary>The upgrade tier researched, 0 to 2 (<c>+0x14</c>); nothing reads it yet.</summary>
	public int TierResearched( int itemId ) => _items.TryGetValue( itemId, out var entry ) ? entry.Tier : 0;
}
