namespace OpenTPW.UI;

/// <summary>
/// The front end's four player slots, and who is playing.
///
/// A player lasts until the game closes: nothing is saved yet, so every launch starts with four empty
/// slots - the case the original meets when its save folder holds nobody (Players_CountUsedSlots,
/// 0x005c7bb0).
/// </summary>
internal sealed class Players
{
	public const int SlotCount = 4;

	private readonly Player?[] _slots = new Player?[SlotCount];

	public Player? this[int slot] => slot >= 0 && slot < SlotCount ? _slots[slot] : null;

	/// <summary>Who is playing, or null before anyone has been picked.</summary>
	public Player? Current { get; private set; }

	public int UsedSlots => _slots.Count( player => player != null );

	/// <summary>Puts a new player in a slot and makes them the one playing - 0x005c7f40, then 0x005c83b0.</summary>
	public Player Create( int slot, string name, bool instantAction )
	{
		var player = new Player( name, instantAction );

		_slots[slot] = player;
		Current = player;

		return player;
	}

	public void Select( int slot ) => Current = this[slot];
}

/// <summary>Someone playing: their name, their game mode, and their golden keys.</summary>
internal sealed class Player( string name, bool instantAction )
{
	public string Name { get; } = name;

	/// <summary>Instant Action rather than Full Simulation - the joystick beside their slot.</summary>
	public bool InstantAction { get; } = instantAction;

	/// <summary>
	/// Golden keys, which open parks - see <see cref="LobbyIsland.KeysToEnter"/>.
	///
	/// The original works a player's keys out (0x005af680) as a third of a count of flags it keeps -
	/// four of the player's own, six for each park played, two more - plus a count of keys given
	/// outright. A new player has none of either, and is given their first as the slots close
	/// (0x005afc30, from FrontEnd_ClosePlayerSlots): the key the advisor hands over in his tour, "Here's
	/// one now to get you started".
	/// </summary>
	public int Keys { get; set; }
}
