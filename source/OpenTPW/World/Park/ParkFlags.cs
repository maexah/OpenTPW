namespace OpenTPW;

/// <summary>
/// The eight flags on the poles by the park's gates - the original's <c>FlagsMesh</c>, the third of the three "dynamic
/// faces" meshes <c>FUN_0053bfe0</c> makes (<see cref="ParkBuildMarkers"/> and <see cref="ParkSea"/> are the others;
/// <c>docs/exe/park-engine.md</c>, "The gates' flags").
///
/// <para>
/// <b>One grid waves every flag.</b> Every frame <c>FUN_0053d570( 7, 5, 1.8, phase )</c> fills 7 columns by 5 rows 1.8
/// apart (<see cref="Grid"/>), and <c>FUN_0053cda0</c> lays its 6 × 4 cells at each of the eight points, two faces a
/// cell, every corner moved 0.5 off the pole and turned about the vertical by the weather's <c>mWindDir</c>.
/// </para>
/// <para>
/// <b>The points are fixed in the executable</b> (<c>0x00763cf4</c>), whatever the park, so they are laid in every
/// theme; only Lost Kingdom's poles were looked at.
/// </para>
/// </summary>
public sealed class ParkFlags : ModelEntity
{
	/// <summary>The flags of the park currently loaded, or null outside one.</summary>
	public static ParkFlags? Current { get; private set; }

	/// <summary>The grid <c>FUN_0053c3f0</c> asks <c>FUN_0053d570</c> for: 7 columns, 5 rows, 1.8 apart.</summary>
	public const int Columns = 7;

	/// <inheritdoc cref="Columns"/>
	public const int Rows = 5;

	/// <inheritdoc cref="Columns"/>
	public const float Spacing = 1.8f;

	/// <summary>How far <c>FUN_0053cda0</c> moves every corner along the flag, off the pole, before turning it.</summary>
	public const float OffThePole = 0.5f;

	/// <summary>
	/// The eight points of <c>0x00763cf4</c>, in the original's axes (y up), in the order the flags are laid: each x
	/// at z 38.75 and then at z 50.75.
	/// </summary>
	internal static readonly Vector3[] Poles =
	[
		new( 411.25f, 22f, 38.75f ), new( 411.25f, 22f, 50.75f ), new( 451.25f, 22f, 38.75f ), new( 451.25f, 22f, 50.75f ),
		new( 511.25f, 22f, 38.75f ), new( 511.25f, 22f, 50.75f ), new( 551.25f, 22f, 38.75f ), new( 551.25f, 22f, 50.75f )
	];

	/// <summary>
	/// The texture both words wear, entries 18 and 19 of the table at <c>0x00763b38</c>: the SimThemePark logo in its
	/// upper half and Bullfrog Productions' in its lower.
	/// </summary>
	internal const string TextureName = "gte_lgo1";

	/// <summary>Where word 18's flags start down the texture, the SimThemePark half, and word 19's, the Bullfrog half (<c>0x00700dfc</c>).</summary>
	internal const float FirstWordTop = 1f;

	/// <inheritdoc cref="FirstWordTop"/>
	internal const float SecondWordTop = 0.55f;

	/// <summary>How far down the texture a flag's four rows of cells run.</summary>
	internal const float Down = 0.55f;

	/// <summary>Faces a flag: 6 × 4 cells, front and back.</summary>
	public const int FacesPerFlag = (Columns - 1) * (Rows - 1) * 2;

	/// <summary>
	/// Opaque and clamped: <c>gte_lgo1</c>'s header byte 1 is nought and every texel's alpha 255, and the table's word 3
	/// has no <c>0x10</c> or <c>0x20</c>.
	/// </summary>
	private const uint Flags = LobbyModel.ClampFlag;

	/// <summary>
	/// The phase the flags wave at, <c>DAT_00871ed0</c>. The original zeroes it with the sea's and steps the two alike,
	/// so they are equal at every read: this takes <see cref="ParkSea.Phase"/>, and steps its own the sea's way only
	/// where no sea is laid.
	/// </summary>
	public float Phase { get; private set; }

	/// <summary>
	/// The angle the flags stand at, degrees in [0, 360): the weather's <c>mWindDir</c> eased towards (<see cref="WindAfter"/>),
	/// 0 where no weather is running.
	/// </summary>
	public float Wind { get; private set; }

	/// <summary>
	/// How fast the flags turn to a new wind, per second, through <see cref="Time.SmoothingFactor"/>: a 45 degree change
	/// is 10 degrees short after a second and 2 after two.
	///
	/// <para>
	/// <b>A deviation, at Alexah's word (Q643).</b> The original's flags take <c>mWindDir</c> straight every
	/// frame (<c>FUN_0053c3f0</c> through <c>FUN_00519590</c>), so they jump by up to 45 degrees at each change of
	/// weather (<c>FUN_00512f00</c>). This eases the turn, on by default; the rate is chosen, not measured, as the
	/// original has none.
	/// </para>
	/// </summary>
	internal const float TurnRate = 1.5f;

	/// <summary>The wind last seen in the weather, to log each change once.</summary>
	private float? _seen;

	/// <summary>The folder the texture came from: <c>stexture</c> at low texture quality, else <c>textures</c>.</summary>
	public string Folder { get; }

	private readonly Vertex[] _vertices;

	public ParkFlags( bool smallTextures )
	{
		Name = "park flags";

		Folder = smallTextures ? "stexture" : "textures";

		var textures = new Texture[Material.TextureSlots];
		textures[0] = Load( $"generic/dynamic/{Folder}/{TextureName}.wct" );
		for ( var slot = 1; slot < textures.Length; ++slot )
			textures[slot] = Texture.Missing;

		_vertices = new Vertex[Poles.Length * FacesPerFlag * 4];
		Fill();

		// Culled: the back of each cell is a face of its own, wound the other way, as the original lays them.
		var material = new Material<ObjectUniformBuffer>( "content/shaders/test.shader" );
		material.Set( "Color", textures );

		Model = new Model( _vertices, ParkSea.Elements( Poles.Length * FacesPerFlag ), material );
		Model.EnableFrequentUpdates( _vertices );

		Log.Info( $"Flags: {Poles.Length} flags of {FacesPerFlag} faces, {TextureName} from {Folder}" );
		Current = this;
	}

	/// <summary>The flags, the texture the small copy where <paramref name="textureQuality"/> is 0 or 1, as the sea's.</summary>
	public static ParkFlags For( int textureQuality ) => new( ParkSea.SmallTextures( textureQuality ) );

	private static Texture Load( string path )
	{
		try
		{
			return new Texture( path );
		}
		catch ( Exception e )
		{
			Log.Warning( $"Flags: {path} would not load - {e.Message}" );
			return Texture.Missing;
		}
	}

	/// <summary>
	/// <c>FUN_0053d570</c>'s grid at a phase, in the original's axes, [column, row]: the point at x = 1.8c stands at
	/// y = 0.6 t cos(πD/2) − 1.8r − 0.02x² and z = 0.3 t cos(2πD/3), where D = √(x² + (1.8r + 9)²) − 0.6 × phase is
	/// its distance from a point 9 over the flag's top at the pole, less the wave, and t = min( x / 5.4, 1 ); its normal
	/// is (t sin(2πD/3), t sin(2πD/3), 1) normalised. The sums are taken in the order the original takes them.
	/// </summary>
	internal static (Vector3[,] Points, Vector3[,] Normals) Grid( float phase )
	{
		const float k = 6.2831855f * 0.33333334f;
		var points = new Vector3[Columns, Rows];
		var normals = new Vector3[Columns, Rows];

		var top = 0f;
		for ( var r = 0; r < Rows; ++r )
		{
			var below = top - (Rows * Spacing);
			var belowSquared = below * below;
			var lag = phase * 0.6f;
			const float span = (Columns - 4) * Spacing;

			var x = 0f;
			for ( var c = 0; c < Columns; ++c )
			{
				var d = (float)Math.Sqrt( ((double)x * x) + belowSquared ) - lag;
				var a = k * d;
				var b = 6.2831855f * d * 0.25f;
				var t = Math.Min( x / span, 1f );

				var z = ParkSea.Cosine( a ) * t * 0.3f;
				var y = ((t + t) * ParkSea.Cosine( b ) * 0.3f) + top - (x * x * 0.02f);
				var s = ParkSea.Sine( a ) * t;
				var length = (float)Math.Sqrt( ((double)s * s * 2) + 1 );

				points[c, r] = new Vector3( x, y, z );
				normals[c, r] = new Vector3( s / length, s / length, 1f / length );
				x += Spacing;
			}

			top -= Spacing;
		}

		return (points, normals);
	}

	/// <summary>
	/// Every corner of every face, as <c>FUN_0053cda0</c> lays them flag by flag, row by row, cell by cell, the front
	/// before the back: the front's corners [c+1][r], [c][r], [c+1][r+1], [c][r+1] with their normals negated, the
	/// back's [c][r], [c+1][r], [c][r+1], [c+1][r+1]. Each is moved <see cref="OffThePole"/> along x, turned about y by
	/// the wind (x' = x cos a − z sin a, z' = x sin a + z cos a) and set on its pole; U runs c/6 from the pole and V
	/// falls 0.55/4 a row from the word's top. Positions are in this engine's axes, normals in the original's (the
	/// shader swaps them).
	/// </summary>
	internal static IEnumerable<(Vector3 Position, Vector3 Normal, Vector2 Uv)> Corners( float phase, float windDegrees )
	{
		var (points, normals) = Grid( phase );
		var angle = windDegrees * 0.0174533f;
		var (sin, cos) = (ParkSea.Sine( angle ), ParkSea.Cosine( angle ));

		Vector3 Turn( Vector3 p ) => new( (cos * p.X) - (sin * p.Z), p.Y, (sin * p.X) + (cos * p.Z) );

		const float across = 1f / (Columns - 1);
		const float down = Down / (Rows - 1);

		for ( var flag = 0; flag < Poles.Length; ++flag )
		{
			var pole = Poles[flag];
			var v = flag % 2 == 0 ? FirstWordTop : SecondWordTop;

			(Vector3, Vector3, Vector2) Corner( int c, int r, float sign, float u, float vv )
			{
				var turned = Turn( points[c, r] + new Vector3( OffThePole, 0f, 0f ) ) + pole;
				return (new Vector3( turned.X, turned.Z, turned.Y ), Turn( normals[c, r] * sign ), new Vector2( u, vv ));
			}

			for ( var r = 0; r < Rows - 1; ++r )
			{
				var u = 0f;
				for ( var c = 0; c < Columns - 1; ++c )
				{
					yield return Corner( c + 1, r, -1f, u + across, v );
					yield return Corner( c, r, -1f, u, v );
					yield return Corner( c + 1, r + 1, -1f, u + across, v - down );
					yield return Corner( c, r + 1, -1f, u, v - down );

					yield return Corner( c, r, 1f, u, v );
					yield return Corner( c + 1, r, 1f, u + across, v );
					yield return Corner( c, r + 1, 1f, u, v - down );
					yield return Corner( c + 1, r + 1, 1f, u + across, v - down );

					u += across;
				}

				v -= down;
			}
		}
	}

	/// <summary>The phase this frame: the sea's where one is laid, else this one stepped as the sea's is.</summary>
	internal static float PhaseAfter( float? sea, float phase, float seconds, bool paused )
		=> sea ?? ParkSea.Advance( phase, seconds, paused );

	/// <summary>
	/// The flags' angle this frame, eased from <paramref name="shown"/> towards the weather's <paramref name="wind"/> the
	/// shorter way round by <paramref name="factor"/> (<see cref="Time.SmoothingFactor"/> of <see cref="TurnRate"/>), and
	/// wrapped to [0, 360). Taken straight where nothing has been shown yet, so a park opens with its flags at the
	/// file's wind; held while the game is paused, as the wave's phase is; 0 with no weather.
	/// </summary>
	internal static float WindAfter( float? shown, float? wind, float factor, bool paused )
	{
		if ( wind is not { } target )
			return 0f;

		if ( shown is not { } from )
			return target;

		if ( paused )
			return from;

		var turn = ((((target - from) % 360f) + 540f) % 360f) - 180f;
		return (((from + (turn * factor)) % 360f) + 360f) % 360f;
	}

	/// <summary>Stands the flags at the weather's wind at once, for DebugConsole's <c>flags wind W</c>, whose frames are compared with the original's.</summary>
	internal void DebugSnap() => _shown = null;

	private float? _shown;

	protected override void OnUpdate()
	{
		Phase = PhaseAfter( ParkSea.Current?.Phase, Phase, Time.Delta, GameClock.Paused );

		var wind = ParkWeather.Current?.WindDirection;
		if ( wind != _seen )
		{
			var now = wind?.ToString( "0.###" ) ?? "none";
			Log.Info( $"Flags: the wind {(_seen is { } was ? $"moves from {was:0.###} to {now}" : $"is {now}")}, the flags at {Wind:0.###}" );
			_seen = wind;
		}

		Wind = WindAfter( _shown, wind, Time.SmoothingFactor( TurnRate ), GameClock.Paused );
		_shown = wind is null ? null : Wind;

		Fill();
		Model?.UpdateVertices( _vertices );
	}

	private void Fill()
	{
		var at = 0;
		foreach ( var (position, normal, uv) in Corners( Phase, Wind ) )
		{
			_vertices[at++] = new Vertex
			{
				Position = position,
				Normal = normal,
				TexCoords = uv,
				TexIndex = 0,
				MatFlags = Flags
			};
		}
	}

	protected override void OnDelete()
	{
		base.OnDelete();

		if ( Current == this )
			Current = null;
	}
}
