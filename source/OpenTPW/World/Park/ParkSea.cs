namespace OpenTPW;

/// <summary>
/// The moving sea over the harbour by the gates - the original's <c>WaterMesh</c>, the second of the three "dynamic
/// faces" meshes <c>FUN_0053bfe0</c> makes (<see cref="ParkBuildMarkers"/> is the first). <c>base.MD2</c>'s
/// <c>Line01</c>, drawn by <see cref="ParkTerrain"/>, is the still lake under it; this is a quilt of see-through quads
/// laid six units over it (<c>docs/exe/park-engine.md</c>, "The park's sea").
///
/// <para>
/// <b>The grid.</b> Every frame <c>FUN_0053d380( 16, 19, 20.0, phase )</c> fills 16 columns by 19 rows of points 20
/// apart, the point at x = 20c, z = 20r standing at 1.7 cos s with the normal (sin s, 1, sin s) normalised, where
/// s = (2π/3)(x cos 10° − z sin 10°) − 0.5 × phase (<see cref="Wave"/>), every sine read from the engine's 4,096-entry
/// table. <c>FUN_0053cab0</c> then lays a quad on each cell of two blocks at the corner (<see cref="Blocks"/>), 134 in
/// all, each showing its texture once.
/// </para>
/// <para>
/// <b>Its corner is the theme's <c>WaterStartPos</c></b>, which <c>FUN_004080e0</c> copies from the balance; each of
/// its 110 texture words is drawn once as the park loads, <c>rand() % 3</c> among <c>gby_sur1</c> to <c>gby_sur3</c>,
/// and the second block takes the first 24 again.
/// </para>
/// </summary>
public sealed class ParkSea : ModelEntity
{
	/// <summary>The sea of the park currently loaded, or null outside one or where its theme names no corner.</summary>
	public static ParkSea? Current { get; private set; }

	/// <summary>The grid <c>FUN_0053c3f0</c> asks <c>FUN_0053d380</c> for: 16 columns, 19 rows, 20 apart.</summary>
	public const int Columns = 16;

	/// <inheritdoc cref="Columns"/>
	public const int Rows = 19;

	/// <inheritdoc cref="Columns"/>
	public const float Spacing = 20f;

	/// <summary>How high a point heaves, the 1.7 at <c>0x00700e04</c>.</summary>
	public const float Amplitude = 1.7f;

	/// <summary>
	/// The two blocks of quads <c>FUN_0053c3f0</c> lays (<c>0x0054e2fc</c> on): columns 0 to 9 by rows 0 to 10, then
	/// columns 10 to 13 by rows 0 to 5. Each block counts its texture words from nought.
	/// </summary>
	internal static readonly (int Column, int Row, int Columns, int Rows)[] Blocks = [(0, 0, 10, 11), (10, 0, 4, 6)];

	/// <summary>How many texture words <c>FUN_0053bfe0</c> draws, at <c>0x00871d18</c>: the first block's quads.</summary>
	public const int Words = 110;

	/// <summary>The three textures a quad wears, entries 15 to 17 of the table at <c>0x00763b38</c>.</summary>
	internal static readonly string[] TextureNames = ["gby_sur1", "gby_sur2", "gby_sur3"];

	/// <summary>
	/// What the phase gains a rendered frame, unless the clock is paused: <c>FUN_0053c3f0</c> takes away the -0.4 at
	/// <c>0x00700de8</c> times the clock's rate, which is always 1.0.
	/// </summary>
	public const float PhasePerFrame = 0.4f;

	/// <summary>
	/// How many frames a second the phase counts. <b>The original steps it once a rendered frame</b>, so its sea ran
	/// with its frame rate; this takes 30 a second, the rate the original was measured at (Q486) and the build squares
	/// take (<see cref="ParkBuildMarkers"/>), and runs off <see cref="Time.Delta"/>, so one wave takes 2π / (0.2 × 30) =
	/// 1.05 s at any frame rate here.
	/// </summary>
	public const float FramesPerSecond = 30f;

	/// <summary>
	/// See-through, and clamped. The original grades the quilt by its textures alone: each <c>gby_sur</c> carries an alpha
	/// chunk under header byte 1 set, so it is drawn graded at 16 (<c>docs/exe/textures.md</c>), alpha 67 to 207; here
	/// the shader blends only a vertex carrying <c>FLAG_TRANSLUCENT</c>, so that bit stands for the textures' alpha.
	/// The material word 3 has no <c>0x10</c> or <c>0x20</c>, so the textures do not wrap.
	/// </summary>
	private const uint Flags = 0x2 | LobbyModel.ClampFlag;

	/// <summary>The corner, in this engine's axes: the theme's <c>WaterStartPos</c> with its Y and Z swapped.</summary>
	public Vector3 Corner { get; private set; }

	/// <summary>The corner as the log and the console print it, "(380, -180, -5)".</summary>
	public string CornerText => FormattableString.Invariant( $"({Corner.X:0.##}, {Corner.Y:0.##}, {Corner.Z:0.##})" );

	/// <summary>How far the sea's phase runs before it starts again: 4π, where the wave's angle (half the phase back) has turned once.</summary>
	public const float SeaPeriod = 4f * MathF.PI;

	/// <summary>
	/// How far <see cref="FlagsPhase"/> runs before it starts again: 20, where <see cref="ParkFlags.Grid"/>'s D has fallen
	/// 12, the joint period of its waves of 3 and 4.
	/// </summary>
	public const float FlagsPeriod = 20f;

	/// <summary>
	/// The sea's phase, <c>DAT_00871f60</c>, wrapped at <see cref="SeaPeriod"/>.
	///
	/// <para>
	/// <b>A deviation (Q504).</b> The original's phase, and the flags' <c>DAT_00871ed0</c> beside it, are one float32
	/// each that grows without bound and is stepped alike, so they are equal at every read. Ours gains 12 a second off
	/// <see cref="Time.Delta"/>, so unwrapped, at a high refresh rate, its step would round against the float's spacing
	/// after hours (at 144 Hz from about 1.5 h; stopped at about 48 h, where the original's 0.4 a frame holds to about
	/// 12 h and stops at about 194 h). The sea repeats every 4π of phase and the flags every 20, with no common period,
	/// so the two are kept apart, both stepped by <see cref="Advance"/> from the same frame: this one wrapped at 4π and
	/// <see cref="FlagsPhase"/> at 20. Each draws the wave the original's one phase would, at any age of the park.
	/// </para>
	/// </summary>
	public float Phase { get; private set; }

	/// <summary>
	/// The flags' phase, <c>DAT_00871ed0</c>, stepped beside <see cref="Phase"/> and wrapped at <see cref="FlagsPeriod"/>
	/// (the deviation said at <see cref="Phase"/>); <see cref="ParkFlags"/> waves at it.
	/// </summary>
	public float FlagsPhase { get; private set; }

	/// <summary>Each quad's texture, 0 to 2 for <c>gby_sur1</c> to <c>gby_sur3</c>, as drawn at load.</summary>
	public IReadOnlyList<int> TextureWords => _words;

	/// <summary>The folder the textures came from: <c>stexture</c> at low texture quality, else <c>textures</c>.</summary>
	public string Folder { get; }

	private readonly int[] _words;
	private readonly Vertex[] _vertices;

	/// <summary>The four corners of every quad as last laid, in <see cref="Quads"/>' order. Read by the tests.</summary>
	internal IReadOnlyList<Vertex> Vertices => _vertices;

	public ParkSea( Vector3 waterStartPos, bool smallTextures, Random random )
		: this( CornerOf( waterStartPos ), smallTextures ? "stexture" : "textures", DrawWords( random ) )
	{
		var textures = new Texture[Material.TextureSlots];
		for ( var slot = 0; slot < textures.Length; ++slot )
			textures[slot] = slot < TextureNames.Length ? Load( $"generic/dynamic/{Folder}/{TextureNames[slot]}.wct" ) : Texture.Missing;

		var elements = Elements( _vertices.Length / 4 );

		// Both faces drawn, where the original culls (render-states.md, "Which draws the test covers"; Q613).
		var material = new Material<ObjectUniformBuffer>( "content/shaders/test.shader", MaterialFlags.DisableCulling );
		material.Set( "Color", textures );

		TranslucentModel = new Model( _vertices, elements, material );
		TranslucentModel.EnableFrequentUpdates( _vertices );

		Log.Info( $"Sea: {_vertices.Length / 4} quads at {CornerText} from {Folder}, words {Census()}" );
		Current = this;
	}

	/// <summary>The sea's data without its model, which needs the renderer: what the constructor above lays first, and what the tests lay.</summary>
	internal ParkSea( Vector3 corner, string folder, int[] words )
	{
		Name = "park sea";

		Corner = corner;
		Folder = folder;
		_words = words;
		_vertices = new Vertex[Quads().Count() * 4];
		Fill();
	}

	/// <summary>
	/// The sea a park's balance asks for, or none where its theme names no <c>WaterStartPos</c>. The textures are the
	/// small copies where <paramref name="textureQuality"/> is 0 or 1 (<c>FUN_0054dcc0</c>, read by
	/// <c>FUN_0053d790</c>: the detail file's <c>GraphicalOptions.TEXTUREQUALITY</c>, there being no
	/// <c>dialog.tcf</c> in the install to answer instead).
	/// </summary>
	public static ParkSea? For( ParkBalance balance, int textureQuality )
	{
		if ( balance["WaterStartPos.X"] is null || balance["WaterStartPos.Y"] is null || balance["WaterStartPos.Z"] is null )
		{
			Log.Warning( "Sea: the balance names no WaterStartPos, so no sea is laid over the harbour" );
			return null;
		}

		return new ParkSea( balance.Vector( "WaterStartPos", Vector3.Zero ), SmallTextures( textureQuality ), new Random() );
	}

	/// <summary>
	/// The triangles of <paramref name="quads"/> quads: <c>FUN_0053e530</c> writes each of the sea's faces as (2, 1, 0)
	/// and (1, 2, 3) of its four corners, so a quad folds along the line from [c+1][r] to [c][r+1].
	/// </summary>
	internal static uint[] Elements( int quads )
	{
		var elements = new uint[quads * 6];
		for ( uint quad = 0, at = 0; quad < quads; ++quad )
		{
			var corner = quad * 4;
			elements[at++] = corner + 2;
			elements[at++] = corner + 1;
			elements[at++] = corner;
			elements[at++] = corner + 1;
			elements[at++] = corner + 2;
			elements[at++] = corner + 3;
		}

		return elements;
	}

	/// <summary>Whether a texture quality takes the small copies: 0 or 1.</summary>
	internal static bool SmallTextures( int textureQuality ) => textureQuality is 0 or 1;

	/// <summary>The corner in this engine's axes: the original's Y is up and its Z is this engine's Y, the swap every model's vertices take.</summary>
	internal static Vector3 CornerOf( Vector3 waterStartPos ) => new( waterStartPos.X, waterStartPos.Z, waterStartPos.Y );

	/// <summary>
	/// The phase after <paramref name="seconds"/>: 0.4 a frame at <see cref="FramesPerSecond"/>, nothing while paused,
	/// wrapped into [0, <paramref name="period"/>) (<see cref="SeaPeriod"/> or <see cref="FlagsPeriod"/>).
	/// </summary>
	internal static float Advance( float phase, float seconds, bool paused, float period )
		=> Wrap( paused ? phase : phase + (PhasePerFrame * FramesPerSecond * seconds), period );

	/// <summary><paramref name="phase"/> in [0, <paramref name="period"/>).</summary>
	internal static float Wrap( float phase, float period )
	{
		var wrapped = phase % period;
		return wrapped < 0f ? wrapped + period : wrapped;
	}

	private static Texture Load( string path )
	{
		try
		{
			return new Texture( path );
		}
		catch ( Exception e )
		{
			Log.Warning( $"Sea: {path} would not load - {e.Message}" );
			return Texture.Missing;
		}
	}

	/// <summary>
	/// The 110 words, each <c>rand() % 3</c> as <c>FUN_0053bfe0</c> draws them. The C library's sequence is not
	/// reproduced: which quad wears which is the original's random draw too.
	/// </summary>
	internal static int[] DrawWords( Random random )
	{
		var words = new int[Words];
		for ( var i = 0; i < words.Length; ++i )
			words[i] = random.Next( 3 );

		return words;
	}

	/// <summary>
	/// Every quad as <c>FUN_0053cab0</c> lays it: block by block, row by row, column by column, with the index of the
	/// texture word it wears, counted from nought in each block.
	/// </summary>
	internal static IEnumerable<(int Column, int Row, int Word)> Quads()
	{
		foreach ( var (column, row, columns, rows) in Blocks )
		{
			var word = 0;

			for ( var r = 0; r < rows; ++r )
			for ( var c = 0; c < columns; ++c )
				yield return (column + c, row + r, word++);
		}
	}

	/// <summary>The engine's sine of an angle, the table entry nearest it (<c>FUN_004708d0</c>, rounded as the x87 rounds).</summary>
	internal static float Sine( float radians )
		=> RideNodes.Table( (int)MathF.Round( radians * (4096 * 0.15915494f), MidpointRounding.ToEven ) );

	/// <summary>The engine's cosine: the sine of the angle less −π/2 (<c>0x00700df0</c>).</summary>
	internal static float Cosine( float radians ) => Sine( radians - -1.5707964f );

	/// <summary>
	/// The wave's angle at a grid point x across and z along the grid, in the original's axes:
	/// (2π × 1/3)(x cos 10° − z sin 10°) − 0.5 × phase (<c>0x00700dd4</c> × <c>0x00700e00</c>, ten times
	/// <c>0x00700dd0</c>, <c>0x00700de4</c>), the two tens' sine and cosine from the table.
	/// </summary>
	internal static float Wave( float x, float z, float phase )
	{
		const float k = 6.2831855f * 0.33333334f;
		var sin = Sine( 0.017453292f * 10f );
		var cos = Cosine( 0.017453292f * 10f );

		return (x * cos * k) + (z * -(k * sin)) - (phase * 0.5f);
	}

	/// <summary>How high a point stands over the corner: 1.7 cos s.</summary>
	internal static float Height( float s ) => Cosine( s ) * Amplitude;

	/// <summary>
	/// A point's normal, (sin s, 1, sin s) normalised, in the original's Y-up form as <see cref="ParkGround.NormalAt"/>
	/// gives the ground's (the shader swaps it).
	/// </summary>
	internal static Vector3 Normal( float s )
	{
		var sin = Sine( s );
		return new Vector3( sin, 1f, sin ).Normal;
	}

	/// <summary>Where the point of column <paramref name="column"/> and row <paramref name="row"/> stands, in this engine's axes.</summary>
	internal static Vector3 Point( Vector3 corner, int column, int row, float phase )
	{
		var (x, z) = (column * Spacing, row * Spacing);
		return corner + new Vector3( x, z, Height( Wave( x, z, phase ) ) );
	}

	protected override void OnUpdate()
	{
		Phase = Advance( Phase, Time.Delta, GameClock.Paused, SeaPeriod );
		FlagsPhase = Advance( FlagsPhase, Time.Delta, GameClock.Paused, FlagsPeriod );

		Fill();
		TranslucentModel?.UpdateVertices( _vertices );
	}

	/// <summary>Lays every quad's four corners at the phase now: [c][r], [c+1][r], [c][r+1], [c+1][r+1], UV (0,0), (1,0), (0,1), (1,1).</summary>
	private void Fill()
	{
		var vertex = 0;

		foreach ( var (column, row, word) in Quads() )
		{
			var texture = _words[word];

			_vertices[vertex++] = Laid( column, row, new Vector2( 0f, 0f ), texture );
			_vertices[vertex++] = Laid( column + 1, row, new Vector2( 1f, 0f ), texture );
			_vertices[vertex++] = Laid( column, row + 1, new Vector2( 0f, 1f ), texture );
			_vertices[vertex++] = Laid( column + 1, row + 1, new Vector2( 1f, 1f ), texture );
		}
	}

	private Vertex Laid( int column, int row, Vector2 uv, int texture ) => new()
	{
		Position = Point( Corner, column, row, Phase ),
		Normal = Normal( Wave( column * Spacing, row * Spacing, Phase ) ),
		TexCoords = uv,
		TexIndex = texture,
		MatFlags = Flags
	};

	/// <summary>
	/// Lowers or raises the quilt's corner, the debug console's <c>sea y</c>: how the lake under it is measured alone,
	/// as the original's was with its corner held under <c>Line01</c> (Q486).
	/// </summary>
	public void SetHeight( float height ) => Corner = Corner with { Z = height };

	/// <summary>
	/// Puts the phase somewhere, the debug console's <c>sea phase</c>: a paused park shows the sea and the flags at any
	/// point of their waves. <paramref name="phase"/> is the original's one phase, so each of the two takes it wrapped
	/// at its own period.
	/// </summary>
	public void SetPhase( float phase )
	{
		Phase = Wrap( phase, SeaPeriod );
		FlagsPhase = Wrap( phase, FlagsPeriod );
	}

	/// <summary>The census of the texture words, how many wear each of the three, for the log and the console.</summary>
	public string Census() => string.Join( " ", Enumerable.Range( 0, TextureNames.Length )
		.Select( t => $"{TextureNames[t]}:{_words.Count( w => w == t )}" ) );

	protected override void OnDelete()
	{
		base.OnDelete();

		if ( Current == this )
			Current = null;
	}
}
