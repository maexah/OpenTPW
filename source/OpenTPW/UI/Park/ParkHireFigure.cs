namespace OpenTPW.UI;

/// <summary>
/// The hire screen's candidate figure, which stands in the portrait <c>0x2484</c>: the picked candidate's own sprite,
/// walking on the spot (<c>FUN_0049ba00</c>; <c>docs/QUEUE.md</c> Q419's notes, from the fork watch, not read here in Ghidra).
///
/// <para>
/// <b>Made only when the candidate changes.</b> <c>FUN_0049ba00</c> returns at once with no candidate, and makes the figure
/// again only when the candidate's role (dword <c>+0</c>) or costume (byte <c>+9</c>) differs from the pair it was last made
/// for (<c>[0x007523e8]</c>, <c>[0x007523ec]</c>); so two candidates of one role and costume share one figure, its walk
/// going on. The screen's destroy (message <c>0x14</c>, <c>0x0049b7b4</c>) frees it and sets both to -1
/// (<see cref="Free"/>). Its one caller is the hire handler (<c>0x0049b97b</c>); this game makes it as a row is picked.
/// </para>
/// <para>
/// <b>What it is.</b> <c>0x00475a10( 0x0074f758, kind, bank, 1, x, y, z )</c> (<c>0x0049bb4a</c>..<c>0x0049bb64</c>): the
/// sprite kind by role from the table at <c>0x0049bb90</c> (<see cref="ParkStaffPool.SpriteKindFor"/>), the bank the
/// costume, set 1, and the script <see cref="SpriteScript.StaffFigureEntry"/>, which shows the walk's eight frames facing 5
/// with draw flag <c>0x40000</c>: no camera term, so direction 5, drawn as direction 3 reflected (<see cref="ParkGuestSprites.Picture"/>).
/// It steps with the park's sprites, every second 31 ms tick, on the interval and the first due time the constructor writes
/// (<c>0x004759c4</c>..<c>0x004759df</c>: due one interval after the make).
/// </para>
/// <para>
/// <b>Where it stands</b> (<see cref="Place"/>): x = (left + (right − left) / 2 − <c>0x3f8</c>) / 1024 and y = (<c>0x300</c> −
/// bottom + (bottom − top) / 4) / 768 in the screen's -1 to 1.
/// </para>
/// <para>
/// <b>Not decoded, so not drawn: its size and its anchor on that point.</b> <c>0x004755d0( handle, 8, 8.0f )</c> writes local 8,
/// and the draw's <c>0x40000</c> arm (<c>0x00589ce4</c>) sizes the quad from it and lays its corners from the picture record
/// (<c>FUN_005890e0</c>), not read through (<c>docs/QUEUE.md</c> Q1513). Rule 4: the figure is made, stepped and censused, and
/// nothing is drawn; <c>STAFF_PORTRAIT_SIZE</c> is counted each time a figure is made.
/// </para>
/// </summary>
internal sealed class ParkHireFigure : UiControl
{
	/// <summary>The pair's value with no figure, <c>[0x007523e8]</c> and <c>[0x007523ec]</c> after the destroy.</summary>
	internal const int None = -1;

	/// <summary>The scale the figure's sprite is handed, local 8 (<c>0x004755d0( handle, 8, 8.0f )</c>): kept, not drawn by.</summary>
	internal const float Scale = 8f;

	private readonly BaseFileSystem? _data;
	private readonly string _theme;
	private readonly ParkSpriteBanks? _banks;

	private SpritePicture[] _pictures = [];
	private SpriteBankFile? _bank;

	/// <param name="data">Where the sprite banks are read from; null where none is mounted, and nothing is drawn.</param>
	/// <param name="theme">The park's theme, whose folder holds the entertainers.</param>
	/// <param name="banks">The banks the park loaded of each kind, which a costume is brought within; null to take it as it is.</param>
	internal ParkHireFigure( BaseFileSystem? data, string theme, ParkSpriteBanks? banks )
	{
		_data = data;
		_theme = theme;
		_banks = banks;
	}

	/// <summary>The role the figure was made for, <c>[0x007523e8]</c>; <see cref="None"/> with none.</summary>
	internal int Role { get; private set; } = None;

	/// <summary>The costume the figure was made for, <c>[0x007523ec]</c>; <see cref="None"/> with none.</summary>
	internal int Costume { get; private set; } = None;

	/// <summary>The sprite kind it wears, by role (<c>0x0049bb90</c>).</summary>
	internal int Kind => ParkStaffPool.SpriteKindFor( Role );

	/// <summary>The bank of that kind whose pictures it shows, or <see cref="None"/> with none loaded.</summary>
	internal int Bank { get; private set; } = None;

	/// <summary>The bank's file name, for the census.</summary>
	internal string BankName { get; private set; } = "";

	/// <summary>How many times a figure has been made on this screen.</summary>
	internal int Made { get; private set; }

	/// <summary>The figure's sprite program, null with no figure.</summary>
	internal SpriteScript? Sprite { get; private set; }

	/// <summary>
	/// <c>FUN_0049ba00</c>: nothing with no candidate; a new figure where the candidate's role or costume is not the pair
	/// the last was made for. Answers whether one was made.
	/// </summary>
	/// <param name="ticks">The game clock's ticks at the make (<see cref="GameClock.Ticks"/>): the new figure comes due one interval after.</param>
	internal bool Show( ParkStaffPool.Candidate? candidate, int ticks )
	{
		if ( candidate is not { } person )
			return false;

		var costume = person.Costume & 0xff;

		if ( person.Kind == Role && costume == Costume )
			return false;

		Free();

		Role = person.Kind;
		Costume = costume;
		Sprite = new SpriteScript( SpriteScript.None, SpriteScript.None, spriteNumber: 1, frame: 0 );
		Sprite.StartAt( SpriteScript.StaffFigureEntry );
		Sprite.ScheduleFrom( (ticks & ~(ParkPeople.SpriteTickEvery - 1)) * ParkPeople.MillisecondsPerTick );
		Made++;

		Load();
		Unimplemented.Report( "STAFF_PORTRAIT_SIZE" );
		Log.Info( $"Hire figure: made for candidate {person.Id}, {Census()}" );

		return true;
	}

	/// <summary>Reads the bank the figure wears: the kind's banks in the original's order, the costume brought within those the park loaded.</summary>
	private void Load()
	{
		Bank = None;
		BankName = "";
		_bank = null;
		_pictures = [];

		if ( _data is not { } data || ParkGuestSprites.FolderFor( Kind, _theme ) is not { } folder )
			return;

		try
		{
			var files = ParkGuestSprites.BanksIn( data, folder, Kind );

			// <b>A deviation, at Alexah's word</b> (docs/DECISIONS.md, "A hired costume is brought within the banks loaded"):
			// the original draws bank base + costume of one flat table, so a costume past its kind's banks shows the next
			// kind's pictures; here it is brought within the kind's, as a hire is (ParkPeople.StaffPicture).
			var bank = _banks is { } loaded && loaded.CountOf( Kind ) > 0 ? loaded.Reduce( Kind, Costume ) : Costume;

			if ( bank < 0 || bank >= files.Length )
			{
				Log.Warning( $"Hire figure: sprite kind {Kind} has no bank {bank} in {folder}" );
				return;
			}

			var file = new SpriteBankFile( new MemoryStream( data.ReadAllBytes( files[bank] ) ) );
			var pack = new SpritePackFile( new MemoryStream( data.ReadAllBytes( ParkGuestSprites.PackFor( files[bank], file, firstPerson: false ) ) ) );

			Bank = bank;
			BankName = Path.GetFileNameWithoutExtension( files[bank] );
			_bank = file;
			_pictures = pack.Pictures;
		}
		catch ( Exception e )
		{
			Log.Warning( $"Hire figure: sprite kind {Kind}'s bank would not load - {e.Message}" );
		}
	}

	/// <summary>
	/// Steps the figure as the park steps its sprites (<c>FUN_00475360</c>): on the newest sprite tick of the loop's
	/// <paramref name="ticks"/>, at its 31 ms a tick.
	/// </summary>
	internal void Step( int ticks )
	{
		var tick = ticks & ~(ParkPeople.SpriteTickEvery - 1);

		Sprite?.Step( tick * ParkPeople.MillisecondsPerTick );
	}

	/// <summary>The picture the figure shows now, and whether it is drawn reflected; null before its script has shown a frame.</summary>
	internal (int Index, bool Mirrored, SpritePicture Picture)? Showing()
	{
		if ( Sprite is not { Shown: true } sprite || _bank is not { } bank )
			return null;

		var sets = bank.Sets;
		var set = sprite.Set;

		if ( set < 0 || set >= sets.Length )
			return null;

		// Flag 0x40000: sector 0, no camera term (0x005421cc), so the facing local alone is the direction.
		var index = ParkGuestSprites.Picture( sets[set], sprite.Frame, sprite.Facing & 7, out var mirrored );

		return index >= 0 && index < _pictures.Length ? (index, mirrored, _pictures[index]) : null;
	}

	/// <summary>
	/// Where the figure stands for the portrait's <paramref name="rect"/>, in the screen's -1 to 1, y up
	/// (<c>docs/QUEUE.md</c> Q419's note of 2026-11-12).
	/// </summary>
	internal static (float X, float Y) Place( UiRect rect )
		=> ((rect.Left + (rect.Width / 2) - 0x3f8) / 1024f, (0x300 - rect.Bottom + (rect.Height / 4)) / 768f);

	/// <summary>That point on the virtual screen, down positive.</summary>
	internal static (float X, float Y) OnVirtualScreen( UiRect rect )
	{
		var (x, y) = Place( rect );

		return (1024f + (x * 1024f), 768f - (y * 768f));
	}

	/// <summary>The screen's destroy: the figure freed and the pair set to -1 (<c>0x0049b7b4</c>).</summary>
	internal void Free()
	{
		Role = None;
		Costume = None;
		Bank = None;
		BankName = "";
		Sprite = null;
		_bank = null;
		_pictures = [];
	}

	/// <summary>What the figure is and shows: its pair, kind, bank, the program's place and the picture, for the log and the console.</summary>
	internal string Census()
	{
		if ( Sprite is not { } sprite )
			return $"hire figure: none, made {Made}";

		var showing = Showing() is { } shown
			? $"picture {shown.Index} {shown.Picture.Width}x{shown.Picture.Height} origin {shown.Picture.OriginX},{shown.Picture.OriginY}{(shown.Mirrored ? " mirrored" : "")}"
			: "no picture";
		var (x, y) = Place( Rect );
		var (vx, vy) = OnVirtualScreen( Rect );

		return $"hire figure: role {Role} costume {Costume}, kind {Kind} bank {Bank} {BankName}, script {sprite.Script} pc {sprite.Pc} "
			+ $"flags 0x{sprite.DrawFlags:x} set {sprite.Set} frame {sprite.Frame} facing {sprite.Facing} turns {sprite.Turns}, {showing}, "
			+ $"at ({x:0.0000},{y:0.0000}) = virtual ({vx:0},{vy:0}), made {Made}";
	}
}
