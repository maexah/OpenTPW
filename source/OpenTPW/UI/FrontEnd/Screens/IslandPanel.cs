using Veldrid;

namespace OpenTPW.UI;

/// <summary>
/// The lobby's control panel once someone is playing: the green L in the bottom left, the park's name
/// along the bottom, what the park costs to enter in golden keys, and how many keys the player holds.
///
/// <para>
/// 0x004b8ca0 loads it from the layout stream at 0x00757f60, and 0x004b9340 keeps it in step with
/// the island on show. On the panel, islandlobby.md2, are enter this park (b_entpark), view the
/// online world (b_worldview), and the previous and next island (b_lobleft, b_lobright). Along the
/// bottom is the park's name in font 0 on the purple skin, in (252, 232, 7), reading "Park Name"
/// until there is an island to name.
/// </para>
/// <para>
/// <b>Keys.</b> At the top, s_key.md2 and s_X1.md2 show the park's price: a gold key and a gold "x N"
/// when the player holds enough (parts 0 and N - 1), grey ones when not (parts 1 and N + 4). The price
/// is <see cref="LobbyIsland.KeysToEnter"/>. At the top right, "N x" in font 2 says how many keys the
/// player holds, hidden while that is none. The small gold key beside it is hidden when the panel is
/// built and nothing found shows it again, but screenshots of the original have it beside the count,
/// so here it comes and goes with the count.
/// </para>
/// <para>
/// <b>Sparkles.</b> While the park on show is one the player can afford, twinkles come and go across
/// the price: effect 97, KeySparkle, started in the middle of the screen at the height of the middle
/// of the "x N" (0x004b9340) and spread across both the key and the number. It runs until it is
/// ended - for a park the player cannot afford, or when the panel closes (0x004b8ee0) - and then
/// its last twinkles fade out.
/// </para>
/// <para>
/// The count is not live. The panel remembers the keys it last showed (0x007cc4b8) and only looks
/// again when told to (IslandPanel_Refresh, 0x004b9340): when it is built, whenever it comes into view
/// (message 0x11 to its callback, 0x004b8b70), and on the advisor's cue as he hands a new player their
/// key. The price is judged against that remembered count. The slots put the panel up just before a new
/// player is given their key, so a new player sees every park grey until the key arrives, and then Lost
/// Kingdom and Halloween World turn gold; a returning player sees their keys at once.
/// </para>
/// <para>
/// <b>Enter this park</b> (0x005e1cc0) does nothing at all for a park the player cannot afford. For one
/// they can, it hands over to 0x005e1e30, which closes this panel, plays effect 4 of the global lobby
/// sfx and a burst of particles at the key, and sets the lobby leaving for the park. Entering a park
/// is still to be built, so here an affordable park only says so in the log.
/// </para>
/// <para>
/// <b>View the online world</b> (0x005e1bd0) does nothing unless the game is online, and it never is
/// here - the online world is a dead end for good - so pressing it gets the click and nothing more,
/// as it would in the original offline.
/// </para>
/// <para>
/// The advisor's tour says the cursor keys move between islands as well as the arrows, and they do.
/// </para>
/// </summary>
internal sealed class IslandPanel : UiWindow
{
	private const int ParkNameFont = 0;
	private const int KeyCountFont = 2;

	private static readonly UiColour ParkNameColour = new( 252, 232, 7 );

	private readonly UiControl _price;
	private readonly UiControl _priceNumber;
	private readonly UiControl _held;
	private readonly UiControl _heldNumber;
	private readonly UiControl _parkName;

	/// <summary>The keys the panel last looked at - see the class remarks.</summary>
	private int _keysShown;

	/// <summary>The sparkles across the price while they run, or 0.</summary>
	private int _sparkle;

	public IslandPanel( FrontEnd frontEnd ) : base( frontEnd )
	{
		Root = new UiControl
		{
			Id = 0x1e0e7,
			Rect = new UiRect( 28, 855, 476, 1521 ),
			Mesh = UiMesh.Get( "islandlobby" )
		};

		Root.Add( new UiButton
		{
			Id = 0x1e0e8,
			Rect = new UiRect( 68, 1114, 221, 1267 ),
			HelpText = 345,
			Mesh = UiMesh.Get( "b_entpark" ),
			Clicked = EnterPark
		} );

		Root.Add( new UiButton
		{
			Id = 0x1e0e9,
			Rect = new UiRect( 68, 946, 221, 1099 ),
			HelpText = 346,
			Mesh = UiMesh.Get( "b_worldview" ),
			Clicked = ViewOnlineWorld
		} );

		_price = Root.Add( new UiControl
		{
			Id = 0x1e0ea,
			Rect = new UiRect( 554, 170, 990, 437 ),
			Mesh = UiMesh.Get( "s_key" )
		} );

		_priceNumber = _price.Add( new UiControl
		{
			Id = 0x1e0eb,
			Rect = new UiRect( 1045, 191, 1405, 416 ),
			Mesh = UiMesh.Get( "s_X1" )
		} );

		_held = Root.Add( new UiControl
		{
			Id = 0x1e0ec,
			Rect = new UiRect( 1688, 68, 1963, 186 )
		} );

		_held.Add( new UiControl
		{
			Id = 0x1e0ed,
			Rect = new UiRect( 1853, 76, 1955, 178 ),
			Mesh = UiMesh.Get( "gkey" ),
			Frame = 3
		} );

		_heldNumber = _held.Add( new UiControl
		{
			Id = 0x1e0ee,
			Rect = new UiRect( 1688, 82, 1829, 172 ),
			Font = KeyCountFont,
			TextAcross = TextAlign.End,
			TextWraps = true
		} );

		_parkName = Root.Add( new UiControl
		{
			Id = 0x1e0ef,
			Rect = new UiRect( 477, 1303, 1571, 1437 ),
			Font = ParkNameFont,
			TextColour = ParkNameColour,
			TextShadow = true,
			Text = "Park Name"
		} );

		Root.Add( new UiButton
		{
			Id = 0x1e0f0,
			Rect = new UiRect( 97, 1299, 250, 1453 ),
			HelpText = 343,
			Mesh = UiMesh.Get( "b_lobleft" ),
			Clicked = () => LobbyCameraMode.Step( -1 )
		} );

		Root.Add( new UiButton
		{
			Id = 0x1e0f1,
			Rect = new UiRect( 260, 1299, 413, 1453 ),
			HelpText = 344,
			Mesh = UiMesh.Get( "b_lobright" ),
			Clicked = () => LobbyCameraMode.Step( 1 )
		} );

		ShowKeys();
	}

	/// <summary>Looks at the player's keys again and shows the count - 0x004b9340.</summary>
	public void ShowKeys()
	{
		_keysShown = FrontEnd.Players.Current?.Keys ?? 0;

		_held.Visible = _keysShown >= 1;
		_heldNumber.Text = $"{_keysShown} x";
	}

	protected internal override void Update()
	{
		if ( FrontEnd.IsFront( this ) && !Input.TextCaptured )
		{
			if ( Input.KeysPressed.Contains( Key.Left ) )
				LobbyCameraMode.Step( -1 );

			if ( Input.KeysPressed.Contains( Key.Right ) )
				LobbyCameraMode.Step( 1 );
		}

		var island = LobbyCameraMode.CurrentIsland;

		_parkName.Text = island?.ParkName ?? "Park Name";

		var price = island?.KeysToEnter ?? 0;
		var affordable = _keysShown >= price;

		_price.Visible = price > 0;
		_price.Frame = affordable ? 0 : 1;
		_priceNumber.Frame = affordable ? price - 1 : price + 4;

		// Not while the options screen has the panel put away. The sparkles are drawn with the panel, so
		// they would not show, but they would still be running.
		ShowSparkle( price > 0 && affordable && !Hidden );
	}

	protected internal override void Shown() => ShowKeys();

	protected internal override void Closed() => ShowSparkle( false );

	/// <summary>Starts the sparkles across the price, or ends them - see the class remarks.</summary>
	private void ShowSparkle( bool show )
	{
		if ( ParticleSystem.Current is not { } particles )
			return;

		if ( !show )
		{
			particles.Kill( _sparkle );
			_sparkle = 0;
			return;
		}

		if ( _sparkle != 0 )
			return;

		var rect = _priceNumber.Rect;
		var middle = ((rect.Bottom - rect.Top) >> 1) + rect.Top;

		_sparkle = particles.Spawn( (int)ParLib.P_EFFECT_KeySparkle, 50000, 0, middle * 75000 / VirtualScreen.Height, _priceNumber.Anchor, this );
	}

	private void EnterPark()
	{
		if ( LobbyCameraMode.CurrentIsland is not { } island || _keysShown < island.KeysToEnter )
			return;

		Log.Info( $"Front end: entering '{island.ParkName}' - entering a park is not built yet" );
	}

	private static void ViewOnlineWorld() { }
}
