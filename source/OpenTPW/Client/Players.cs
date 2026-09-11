namespace OpenTPW;

/// <summary>
/// The four player slots, and who is playing.
///
/// <para>
/// Players are kept as the original keeps them: a folder each under save\users, named for the slot and the
/// name, with their gms.dat inside - see <see cref="SaveFolder"/> and <see cref="PlayerFile"/>. The slots are
/// filled from those folders as the game starts (0x005c7590, whose one caller is WinMain at 0x0045aa74), so a
/// player made before is waiting in their slot the next time the game starts.
/// </para>
/// <para>
/// There is one set of them for the whole run, <see cref="Roster"/>, as the original makes one in WinMain
/// (0x0045aa5a) and frees it only as the game ends (state 0xc, 0x005502f6). A park's load reads it
/// (0x005accf0), and leaving a park for the lobby keeps whoever is playing: nothing on that path saves them or
/// lets them go.
/// </para>
/// <para>
/// Making a player (0x005c7f40) writes their folder and first gms.dat at once, and picks them (0x005c83b0).
/// Picking a player reads their gms.dat again and takes their options from it, and gives them a park record
/// for every theme they lack. They are saved - gms.dat and then Config.tcf - when Select New Player lets them
/// go and when the game closes with them still playing (0x005c8650), and gms.dat again the moment a new
/// player is handed their first key.
/// </para>
/// <para>
/// <b>Engine and content.</b> Engine: the slots, who is playing, and saving someone and letting them go as one
/// step. There is no game content here, and the original's save\users layout stays behind
/// <see cref="SaveFolder"/> and <see cref="PlayerFile"/>.
/// </para>
/// </summary>
internal sealed class Players
{
	public const int SlotCount = 4;

	/// <summary>The players, for the whole run - see the class remarks.</summary>
	public static Players Roster { get; } = new();

	private readonly Player?[] _slots = new Player?[SlotCount];

	public Player? this[int slot] => slot >= 0 && slot < SlotCount ? _slots[slot] : null;

	/// <summary>Who is playing, or null before anyone has been picked.</summary>
	public Player? Current { get; private set; }

	public int UsedSlots => _slots.Count( player => player != null );

	/// <summary>Fills the slots from save\users - see <see cref="SaveFolder.ScanPlayers"/>.</summary>
	public void Load()
	{
		Array.Clear( _slots );
		Current = null;

		foreach ( var (slot, name, file) in SaveFolder.ScanPlayers() )
			_slots[slot] = new Player( slot, name, file ?? new PlayerFile() );

		if ( UsedSlots > 0 )
			Log.Info( $"Front end: {UsedSlots} saved player(s)" );
	}

	/// <summary>Puts a new player in a slot, writes them out, and makes them the one playing - 0x005c7f40, then 0x005c83b0.</summary>
	public Player Create( int slot, string name, bool instantAction )
	{
		if ( _slots[slot] is { } previous )
			SaveFolder.DeletePlayer( previous.Slot, previous.Name );

		var player = new Player( slot, name, new PlayerFile { InstantAction = instantAction } );
		_slots[slot] = player;

		SaveFolder.CreatePlayer( slot, name, player.File );
		Select( slot );

		return player;
	}

	/// <summary>
	/// Picks a player - 0x005c83b0: their gms.dat is read again, a park record made for each theme they lack,
	/// and their options taken if the file held them whole.
	/// </summary>
	public void Select( int slot )
	{
		if ( this[slot] is not { } player )
			return;

		if ( SaveFolder.LoadPlayer( slot, player.Name ) is { } file )
		{
			player.File = file;

			if ( file.HasOptions )
			{
				GameOptions.Current.Apply( file.Options );
				GameOptions.Current.ApplySound();
			}
		}

		foreach ( var theme in SaveFolder.Themes )
			player.File.Park( theme );

		Current = player;
	}

	/// <summary>Saves whoever is playing and lets them go - 0x005c8650. Nothing is written when nobody is playing.</summary>
	public void SaveAndDeselect()
	{
		if ( Current is not { } player )
			return;

		player.Save();
		SaveFolder.SaveConfig();

		Current = null;
	}

	/// <summary>Deletes the player in a slot, folder and all - the delete box's tick (0x004a61b0, 0x005c7e90).</summary>
	public void Delete( int slot )
	{
		if ( this[slot] is not { } player )
			return;

		SaveFolder.DeletePlayer( slot, player.Name );
		_slots[slot] = null;

		if ( Current == player )
			Current = null;
	}
}

/// <summary>Someone playing: their slot, their name, and what their gms.dat holds.</summary>
internal sealed class Player( int slot, string name, PlayerFile file )
{
	public int Slot { get; } = slot;

	/// <summary>Their name, which is only ever kept in their folder's name.</summary>
	public string Name { get; } = name;

	public PlayerFile File { get; set; } = file;

	/// <summary>Instant Action rather than Full Simulation - the joystick beside their slot.</summary>
	public bool InstantAction => File.InstantAction;

	/// <summary>
	/// Golden keys, which open parks - see <see cref="LobbyIsland.KeysToEnter"/>. PlayerProgress_CountKeys
	/// (0x005af680): a key for every three golden tickets earned, and the keys given outright. A Full
	/// Simulation player is given their first as the slots close (0x005afc30, from FrontEnd_ClosePlayerSlots):
	/// the key the advisor hands over in his tour, "Here's one now to get you started".
	/// </summary>
	public int Keys => File.CountKeys( SaveFolder.IsTheme );

	/// <summary>Gives a key outright and writes the player out straight away, as the original does (0x005afc30, 0x005c8a10).</summary>
	public void AddKey()
	{
		File.KeysGiven++;
		Save();
	}

	public void Save() => SaveFolder.SavePlayer( Slot, Name, File );
}
