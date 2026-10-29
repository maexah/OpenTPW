using System.Numerics;
using Numerics = System.Numerics;

namespace OpenTPW;

/// <summary>
/// The highest point of a coaster's track, Global 0's measure: the largest height of the track's samples, the first left
/// out (<c>Coaster_AnalyseSamples</c> <c>0x0043c450</c>, which stores it at the coaster's <c>+0x110</c>). The samples are
/// rebuilt from the save as <c>Coaster_RebuildTrack</c> (<c>0x0043eb70</c>) makes them: the station's three nodes from the
/// coaster's own model, one node a saved piece, a closing node on the first; a knot on each, its two handles, the
/// Bezier between knots and a sample every 3.0 of chord (<c>docs/exe/ride-operation.md</c>, "A coaster's highest track
/// point").
/// </summary>
/// <remarks>
/// Everything here is in the engine's axes, y up; the park's are z up.
/// </remarks>
public static class ParkCoasterTrack
{
	/// <summary>One knot of the track: where it stands and the way the track runs through it (its length does not matter).</summary>
	public readonly record struct Knot( Numerics.Vector3 Place, Numerics.Vector3 Direction );

	/// <summary>How far apart the samples are along the track, the coaster's <c>+0xac</c> (<c>0x004369b7</c>).</summary>
	public const float Spacing = 3f;

	/// <summary>The step in a segment's Bezier parameter the sampler walks (<c>Coaster_SampleTrack</c> <c>0x00440c10</c>).</summary>
	public const float Step = 0.05f;

	/// <summary>How much of a knot's height difference its handles' distances count (<c>Coaster_KnotDistances</c> <c>0x00432840</c>).</summary>
	public const float HeightWeight = 0.7f;

	/// <summary>
	/// The samples of a chain of knots - <c>Coaster_KnotHandles</c> (<c>0x00432430</c>), then <c>Coaster_SampleTrack</c>. Each
	/// knot's handles lie along its unit direction, out by a third of <paramref name="curviness"/> times the distance to the
	/// next knot and in by as much of the distance to the one before (each distance with its height scaled by
	/// <see cref="HeightWeight"/>, and 1 past either end of the chain). Each segment's cubic Bezier is walked in steps of
	/// <see cref="Step"/>, the chords summed, and a sample put on the chord at every <see cref="Spacing"/>; the first is
	/// the head knot.
	/// </summary>
	/// <remarks>
	/// After a sample the original goes on from the next whole step in the parameter, as this does: it re-evaluates
	/// t + 0.05 - fmod( t, 0.05 ), the same step (<c>0x00441010</c>..<c>0x00441069</c>), and the chord runs on from the sample's
	/// place. Rebuilt from the original's own knots and handles (the dump's three decimals), the first 718 of the Temple Of
	/// Gloom's samples lie within 0.0008 of its (<c>q706c/cmp.py</c>). The one sample the original takes past the last knot,
	/// on the closing curve towards the second node, is left out of the measure and not made here.
	/// </remarks>
	/// <param name="curviness">The coaster type's <c>fCurviness</c> (<c>+8</c>).</param>
	public static List<Numerics.Vector3> Samples( IReadOnlyList<Knot> chain, float curviness )
	{
		var samples = new List<Numerics.Vector3>();

		if ( chain.Count == 0 )
			return samples;

		var outs = new Numerics.Vector3[chain.Count];
		var ins = new Numerics.Vector3[chain.Count];

		for ( var i = 0; i < chain.Count; ++i )
		{
			var place = chain[i].Place;
			var unit = Numerics.Vector3.Normalize( chain[i].Direction );
			var before = i > 0 ? Distance( place, chain[i - 1].Place ) : 1f;
			var after = i + 1 < chain.Count ? Distance( place, chain[i + 1].Place ) : 1f;

			outs[i] = place + (unit * (after * curviness / 3f));
			ins[i] = place - (unit * (before * curviness / 3f));
		}

		var previous = chain[0].Place;
		var along = 0f;
		samples.Add( previous );

		for ( var segment = 0; segment + 1 < chain.Count; ++segment )
		{
			for ( var step = 1; step <= 20; ++step )
			{
				var point = Bezier( chain[segment].Place, outs[segment], ins[segment + 1], chain[segment + 1].Place, step * Step );
				var chord = Numerics.Vector3.Distance( previous, point );

				while ( along + chord >= Spacing )
				{
					previous = Numerics.Vector3.Lerp( previous, point, (Spacing - along) / chord );
					samples.Add( previous );
					chord = Numerics.Vector3.Distance( previous, point );
					along = 0f;
				}

				along += chord;
				previous = point;
			}
		}

		return samples;
	}

	/// <summary>
	/// The largest height of <paramref name="samples"/> but the first, from nought (<c>Coaster_AnalyseSamples</c>,
	/// <c>0x0043c450</c>: samples 1 to n-2 of the original's n, whose last is the one past the closing knot).
	/// </summary>
	public static float Highest( IReadOnlyList<Numerics.Vector3> samples )
	{
		var highest = 0f;

		for ( var i = 1; i < samples.Count; ++i )
			highest = MathF.Max( highest, samples[i].Y );

		return highest;
	}

	/// <summary>
	/// Global 0's walk over the coasters' highest points (<c>Park_HighestCoasterPoint</c>, <c>0x004cc920</c>): the first
	/// taken, each later one where the truncated best so far, as a float, is below it; the best truncated
	/// (<c>0x004cc9c7</c>..<c>0x004cc9e4</c>). Nought with none.
	/// </summary>
	public static int Best( IEnumerable<float> highest )
	{
		var best = 0;
		var any = false;

		foreach ( var height in highest )
		{
			if ( any && !((float)best < height) )
				continue;

			any = true;
			best = (int)height;
		}

		return best;
	}

	private static float Distance( Numerics.Vector3 a, Numerics.Vector3 b )
	{
		var d = a - b;

		return MathF.Sqrt( (d.X * d.X) + (d.Y * HeightWeight * (d.Y * HeightWeight)) + (d.Z * d.Z) );
	}

	/// <summary>The cubic Bezier through a knot, its out handle, the next knot's in handle and that knot (<c>Coaster_BezierPoint</c> <c>0x0043e1b0</c>).</summary>
	public static Numerics.Vector3 Bezier( Numerics.Vector3 from, Numerics.Vector3 outward, Numerics.Vector3 inward, Numerics.Vector3 to, float t )
	{
		var u = 1f - t;

		return (from * (u * u * u)) + (outward * (3f * u * u * t)) + (inward * (3f * u * t * t)) + (to * (t * t * t));
	}
}

/// <summary>
/// A coaster's pylon type, <c>asPylonControls[n]</c> of its <c>coaster.sam</c>, with its model and the three clips that pose
/// its knot here (<c>Coaster_PosePylon</c> <c>0x00430ca0</c>, channels 0 to 2 of five, <c>0x0074ca70</c>): the loft
/// (role <c>L</c>) at the node's loft, the rotate (role <c>R</c>) at its yaw over a whole turn, the incline (role <c>I</c>) at
/// its incline. <c>Coaster_SetupPylonModel</c> (<c>0x00430590</c>) finds its knots: a type with two records flagged
/// <c>0x1000000</c> carries a second rail and one knot, id 2 in space <c>0x400000</c>; with one, a knot for every record in
/// that space, from id 1.
/// </summary>
/// <remarks>
/// The bank's clip (role <c>B</c>) rolls the knot about the way it faces and is not posed for the measure (KnotAt, DirectionAt), which reads the knot's
/// place and facing alone; the drawn pylon poses it (Pose, Morph).
/// </remarks>
public sealed class ParkCoasterPylon
{
	/// <summary>The space a coaster model's track nodes are found in, a station's and a pylon's alike.</summary>
	public const uint TrackSpace = 0x400000;

	/// <summary>The space of a pylon's ends, two on a pylon that stacks.</summary>
	private const uint EndSpace = 0x1000000;

	/// <summary>The space of a pylon's centre, whose height bounds its loft.</summary>
	private const uint CentreSpace = 0x800000;

	/// <summary>A clip and how a node's value moves it along: its share is the value times the multiplier plus the add, held between the caps.</summary>
	private readonly record struct Channel( AnimationFile? Clip, float StartCap, float EndCap, float Multiplier, float Add )
	{
		public float Share( float value, float offset = 0f )
			=> Math.Clamp( (value * Multiplier) + Add + offset, StartCap, MathF.Max( StartCap, EndCap ) );
	}

	private readonly ModelFile _model;
	private readonly Channel _loft;
	private readonly Channel _rotate;
	private readonly Channel _incline;
	private readonly Channel _bank;
	private readonly int _knot;

	/// <summary>The type's <c>fMinHeight</c> and <c>fMaxHeight</c> (<c>+0x68</c>, <c>+0x6c</c>), the first raised to <see cref="Lowest"/>.</summary>
	public float MinHeight { get; }

	public float MaxHeight { get; }

	/// <summary>The centre's height at the loft's two ends (<c>+0xbc</c>, <c>+0xc0</c>): 10 and 100 on the Minecart's.</summary>
	public float Lowest { get; }

	public float Highest { get; }

	/// <summary>How many knots each of its nodes holds on the first rail; only one is built.</summary>
	public int Knots { get; }

	private ParkCoasterPylon( ModelFile model, RideAnimations clips, SettingsFile sam, int index )
	{
		_model = model;

		// A key the file leaves out, or sets to nought, keeps the constructor's default (0x0042fd90, 0x0042eb70): every
		// clip's caps 0 and 1, multiplier 1 and add nought; heights 10 and 10.
		float Float( string key, float otherwise )
			=> float.TryParse( sam[$"asPylonControls[{index}].{key}"], System.Globalization.NumberStyles.Float,
				System.Globalization.CultureInfo.InvariantCulture, out var value ) && value != 0f ? value : otherwise;

		Channel For( string name, int role )
			=> new( clips.Clip( role, 0 ), Float( $"f{name}AnimStartCap", 0f ), Float( $"f{name}AnimEndCap", 1f ),
				Float( $"f{name}AnimMultiplier", 1f ), Float( $"f{name}AnimAdd", 0f ) );

		_loft = For( "Loft", LoftRole );
		_rotate = For( "Rotate", RotateRole );
		_incline = For( "Incline", InclineRole );
		_bank = For( "Bank", BankRole );

		// Two ends make a pylon that stacks, with one knot, id 2; one end, a knot for each track record from id 1.
		var ends = Records( EndSpace ).Count();
		var stacks = ends >= 2;

		Knots = stacks ? 1 : Records( TrackSpace ).Count();
		_knot = ends == 0 ? -1 : model.FindNode( stacks ? 2 : 1, TrackSpace );

		// The centre's height with the loft's channel at the start of its range and at its end (0x00430590).
		var centre = Records( CentreSpace ).DefaultIfEmpty( -1 ).First();
		Lowest = centre < 0 ? 0f : Pose( centre, _loft.Share( 0f ), 0f, 0f ).Place.Y;
		Highest = centre < 0 ? 0f : Pose( centre, _loft.Share( 1f ), 0f, 0f ).Place.Y;
		MinHeight = MathF.Max( Float( "fMinHeight", 10f ), Lowest );
		MaxHeight = Float( "fMaxHeight", 10f );
	}

	/// <summary>The pylon type <paramref name="index"/> of the coaster item in <paramref name="directory"/>, or null where its files do not read.</summary>
	public static ParkCoasterPylon? Load( string directory, BaseFileSystem files, int index = 0 )
	{
		using var samStream = files.OpenReadOrNull( $"{directory}/coaster.sam" );

		if ( samStream == null )
			return null;

		var sam = new SettingsFile( samStream );
		var stem = sam[$"asPylonControls[{index}].pcMeshFilename"]?.Trim().Trim( '"' );

		if ( string.IsNullOrEmpty( stem ) )
			return null;

		using var modelStream = files.OpenReadOrNull( $"{directory}/{stem}.MD2" );

		if ( modelStream == null )
			return null;

		return new ParkCoasterPylon( new ModelFile( modelStream ), RideAnimations.Load( directory, stem, files ), sam, index ) { Stem = stem };
	}

	/// <summary>The roles of the pylon's loft, rotate and incline clips (<c>0x0074ca70</c>: 3, 10, 2).</summary>
	private const int LoftRole = 3;

	private const int RotateRole = 10;

	private const int InclineRole = 2;

	private const int BankRole = 9;

	/// <summary>
	/// Whether its knot can be posed here: one knot, found; the nodes the rotate and incline clips turn unturned in the file,
	/// so that the additive key (<c>Anim_ComposeAdditiveTurn</c> <c>0x00470d10</c>) is the turn; the loft clip turning nothing and the turning clips moving
	/// nothing; and the knot where the yaw does not move it, over the middle of its cell.
	/// </summary>
	public bool Posable
	{
		get
		{
			if ( Knots != 1 || _knot < 0 || (_loft.Clip?.RotationTracks.Count ?? 0) != 0 )
				return false;

			foreach ( var clip in new[] { _rotate.Clip, _incline.Clip } )
			{
				if ( clip == null )
					continue;

				if ( clip.PositionTracks.Count != 0 || clip.PathTracks.Count != 0
					|| clip.RotationTracks.Any( track => !IsUnturned( _model.Nodes[track.TargetIndex].LocalTransform ) ) )
					return false;
			}

			var rest = KnotAt( 0f );
			var turned = Pose( _knot, 0f, 0.3f, 0f ).Place;

			return MathF.Abs( rest.X - CellMiddle ) < 1e-3f && MathF.Abs( rest.Z - CellMiddle ) < 1e-3f
				&& Numerics.Vector3.Distance( rest, turned ) < 1e-3f;
		}
	}

	/// <summary>The middle of a ground cell from its corner, ten units a cell.</summary>
	private const float CellMiddle = 5f;

	/// <summary>Where the knot stands in the pylon's model at <paramref name="loft"/>: the loft clip at the loft's share of its length.</summary>
	public Numerics.Vector3 KnotAt( float loft ) => Pose( _knot, _loft.Share( loft ), 0f, 0f ).Place;

	/// <summary>
	/// The way the knot faces at <paramref name="yaw"/> and <paramref name="incline"/>: its z axis with the rotate clip at
	/// the yaw's share of a turn (<c>Coaster_KnotPass</c> <c>0x00430ed0</c>, <c>0x004310b1</c>: yaw / 2π, its whole turns
	/// taken off) and the incline clip at <c>incline / 2π × fInclineAnimMultiplier + 0.5</c>, each held to its caps. The
	/// Minecart's turns once round over 100 frames, and pitches from 45° up to 45° down over frames 1 to 19 of 20.
	/// </summary>
	public Numerics.Vector3 DirectionAt( float yaw, float incline )
	{
		var turns = yaw / (2f * MathF.PI);
		turns -= MathF.Floor( turns );

		return Pose( _knot, _loft.Share( 0f ), _rotate.Share( turns ), _incline.Share( incline / (2f * MathF.PI), 0.5f ) ).Facing;
	}

	/// <summary>
	/// A rotation track's turn at <paramref name="frame"/> as the engine blends it (<c>Anim_BlendRotationKeys</c> <c>0x00474490</c>): held at the ends,
	/// the lower key's easing (<see cref="AnimationFile.RotationTrack.Ease"/>), then <see cref="Slerp"/>.
	/// </summary>
	private static Quaternion Turn( AnimationFile.RotationTrack track, float frame )
	{
		var frames = track.FrameIndices;

		if ( track.Rotations.Length == 1 || frame <= frames[0] )
			return track.Rotations[0];

		if ( frame >= frames[^1] )
			return track.Rotations[^1];

		var hi = 1;

		while ( hi < frames.Length && frames[hi] < frame )
			++hi;

		var lo = hi - 1;
		var span = frames[hi] - frames[lo];

		return Slerp( track.Rotations[lo], track.Rotations[hi], track.Ease( lo, span <= 0 ? 0f : (frame - frames[lo]) / span ) );
	}

	/// <summary>
	/// The engine's slerp (<c>Anim_SlerpOnSineTable</c> <c>0x004742a0</c>): within 0.001 of one key, the two mixed straight; otherwise each weight the
	/// sine of its share of the angle between them over the sine of the whole, every sine read from the engine's table
	/// (<see cref="RideNodes.Table"/>, the angle rounded to its step), which puts the blend on whole steps of the table and
	/// the turn on whole multiples of π/1024. The result is normalised when it is made a matrix (<c>Anim_QuatToMatrix3</c> <c>0x00474120</c>).
	/// </summary>
	/// <remarks>Two keys within 0.001 of opposite take another arm, which no pylon's clip reaches; counted, and blended here by <see cref="Quaternion.Slerp"/>.</remarks>
	public static Quaternion Slerp( Quaternion from, Quaternion to, float t )
	{
		var dot = Quaternion.Dot( from, to );

		if ( !(dot + 1f > 0.001f) )
		{
			Unimplemented.Report( "ANIM_SLERP_OPPOSITE_KEYS" );
			return Quaternion.Slerp( from, to, t );
		}

		float before, after;

		if ( 1f - dot <= 0.001f )
		{
			before = 1f - t;
			after = t;
		}
		else
		{
			var angle = MathF.Acos( dot );
			var whole = Sine( angle );

			before = Sine( (1f - t) * angle ) / whole;
			after = Sine( angle * t ) / whole;
		}

		return Quaternion.Normalize( new Quaternion(
			(to.X * after) + (from.X * before), (to.Y * after) + (from.Y * before),
			(to.Z * after) + (from.Z * before), (to.W * after) + (from.W * before) ) );
	}

	/// <summary>The engine's sine of an angle: its table at the angle times the table's scale, rounded to the nearest.</summary>
	private static float Sine( float angle )
		=> RideNodes.Table( (int)Math.Round( (double)(angle * TableScale), MidpointRounding.ToEven ) );

	/// <summary>The table's entries a radian, 4096 × 0.15915494 as a float (<c>FUN_004708d0</c>).</summary>
	private const float TableScale = 4096 * 0.15915494f;

	/// <summary>
	/// The loft that stands a pylon's knot <paramref name="height"/> above its ground (<c>Coaster_LoftForHeight</c> <c>0x00433790</c>, a pylon on the
	/// ground): the height held to <see cref="MinHeight"/> and <see cref="MaxHeight"/>, its share of
	/// <see cref="Lowest"/> to <see cref="Highest"/>, held to 0 and 1.
	/// </summary>
	public float LoftFor( float height ) => LoftShare( height, MinHeight, MaxHeight, Lowest, Highest );

	/// <summary><see cref="LoftFor"/> of a pylon whose heights are given: held to <paramref name="minHeight"/> and <paramref name="maxHeight"/>, then its share of <paramref name="lowest"/> to <paramref name="highest"/>, held to 0 and 1.</summary>
	internal static float LoftShare( float height, float minHeight, float maxHeight, float lowest, float highest )
	{
		height = Math.Clamp( height, minHeight, MathF.Max( minHeight, maxHeight ) );

		return highest == lowest ? 0f : Math.Clamp( (height - lowest) / (highest - lowest), 0f, 1f );
	}

	/// <summary>
	/// A node of the pylon's model posed with the loft clip at <paramref name="loft"/> of its length, and the rotate and
	/// incline clips' turns at <paramref name="rotate"/> and <paramref name="incline"/> of theirs in place of the turns of the
	/// nodes they name: where it stands and its z axis, composed from the node up to the root.
	/// </summary>
	private (Numerics.Vector3 Place, Numerics.Vector3 Facing) Pose( int node, float loft, float rotate, float incline )
	{
		var poser = new NodePoser( _model );

		if ( _loft.Clip != null )
			poser.Pose( _loft.Clip, FrameAt( _loft.Clip, loft ) );

		var turns = Turns( rotate, incline, null );

		var world = Matrix4x4.Identity;

		for ( var at = node; at >= 0 && at < _model.Nodes.Count; at = _model.Nodes[at].ParentIndex )
		{
			var local = poser.Local( at );

			if ( turns.TryGetValue( at, out var turn ) )
			{
				var place = local.Translation;
				local = Matrix4x4.CreateFromQuaternion( turn );
				local.Translation = place;
			}

			world *= local;
		}

		return (world.Translation, Numerics.Vector3.Normalize( Numerics.Vector3.TransformNormal( Numerics.Vector3.UnitZ, world ) ));
	}

	/// <summary>
	/// The turn each node the rotate, incline and (where given) bank clips turn takes at those shares of the clips.
	/// </summary>
	private Dictionary<int, Quaternion> Turns( float rotate, float incline, float? bank )
	{
		var turns = new Dictionary<int, Quaternion>();

		var clips = new List<(AnimationFile? Clip, float Share)> { (_rotate.Clip, rotate), (_incline.Clip, incline) };

		if ( bank is { } rolled )
			clips.Add( (_bank.Clip, rolled) );

		foreach ( var (clip, share) in clips )
		{
			foreach ( var track in clip?.RotationTracks ?? [] )
			{
				if ( track.Rotations.Length > 0 )
					turns[track.TargetIndex] = Turn( track, FrameAt( clip!, share ) );
			}
		}

		return turns;
	}

	/// <summary>
	/// Poses a pylon's model the way <c>Coaster_PosePylon</c> (<c>0x00430ca0</c>) sets its clips: the loft clip at <paramref name="loft"/>,
	/// the rotate clip at the yaw's share of a turn, the incline clip at <c>incline / 2π × fInclineAnimMultiplier + 0.5</c> and the bank
	/// clip likewise at the bank, each rotation key taken as the node's turn (the nodes they turn are unturned in the file, so the additive
	/// key is the turn). A bank clip that turns a node the file turns is left unposed and counted.
	/// </summary>
	internal void Pose( NodePoser poser, float loft, float yaw, float incline, float bank )
	{
		if ( _loft.Clip != null )
			poser.Pose( _loft.Clip, FrameAt( _loft.Clip, _loft.Share( loft ) ) );

		var turns = yaw / (2f * MathF.PI);
		turns -= MathF.Floor( turns );

		var bankable = (_bank.Clip?.RotationTracks ?? []).All( track => track.Rotations.Length == 0 || IsUnturned( _model.Nodes[track.TargetIndex].LocalTransform ) );

		if ( !bankable )
			Unimplemented.Report( "COASTER_PYLON_BANK_TURNED_NODE" );

		foreach ( var (node, turn) in Turns( _rotate.Share( turns ), _incline.Share( incline / (2f * MathF.PI), 0.5f ),
			bankable ? _bank.Share( bank / (2f * MathF.PI), 0.5f ) : null ) )
			poser.Turn( node, turn );
	}

	/// <summary>
	/// Morphs a made pylon's meshes as <c>Coaster_PosePylon</c> sets its clips (<c>0x00430ca0</c>): the loft, incline and bank clips' vertex
	/// keys, offsets added to the model's rest (<see cref="MeshAnimator.PoseAdditive"/>), each at the share the same arithmetic gives
	/// <see cref="Pose"/> and <see cref="KnotAt"/>.
	/// </summary>
	internal void Morph( LobbyModel model, float loft, float incline, float bank )
	{
		var poses = MorphPoses( loft, incline, bank );

		foreach ( var animator in model.Animators )
			animator.PoseAdditive( poses );
	}

	/// <summary>The clips <see cref="Morph"/> poses and the frame of each: the loft, incline and bank clips at their shares of their own length.</summary>
	internal List<(AnimationFile Clip, float Frame)> MorphPoses( float loft, float incline, float bank )
	{
		var poses = new List<(AnimationFile, float)>();

		foreach ( var (channel, share) in new[] { (_loft, _loft.Share( loft )), (_incline, _incline.Share( incline / (2f * MathF.PI), 0.5f )),
			(_bank, _bank.Share( bank / (2f * MathF.PI), 0.5f )) } )
		{
			if ( channel.Clip != null )
				poses.Add( (channel.Clip, FrameAt( channel.Clip, share )) );
		}

		return poses;
	}

	/// <summary>The clips the pylon is posed by (loft, rotate, incline, bank): what a model made for it is given, which finds none beside the file by its own naming.</summary>
	public IReadOnlyList<AnimationFile> Clips => new[] { _loft.Clip, _rotate.Clip, _incline.Clip, _bank.Clip }.OfType<AnimationFile>().ToArray();

	/// <summary>The pylon's model file name without its extension, in the coaster's folder.</summary>
	public string Stem { get; private init; } = string.Empty;

	/// <summary>The model's node the knot is: the one the track passes through.</summary>
	public int KnotNode => _knot;

	/// <summary>
	/// The node of the type's named pylon part <paramref name="which"/> (1 to 4: the type flags <c>0x400</c>, <c>0x800</c>, <c>0x1000</c>
	/// and <c>0x2000</c>), or -1 where its model has none: the last of the first four lookup records in space <c>0x80000</c> whose id is <paramref name="which"/>
	/// (<c>Coaster_SetupPylonModel</c>, <c>0x004307ce</c>..<c>0x00430833</c>: records 1 to 4 walked in order, the later overwriting).
	/// </summary>
	public int NamedNode( int which )
		=> Records( 0x80000 ).Take( 4 ).Where( node => _model.Nodes[node].Id == which ).DefaultIfEmpty( -1 ).Last();

	/// <summary>The name of node <paramref name="node"/> in the pylon's model, for the console's census.</summary>
	public string NodeName( int node ) => node >= 0 && node < _model.Nodes.Count ? _model.Nodes[node].Name : "-";

	/// <summary>
	/// The yaw whose rotate clip faces the knot along <paramref name="level"/> (a direction on the ground, in the engine's axes): the
	/// angle between the knot's heading at yaw 0 and the wanted one, taken the way a small yaw turns it.
	/// </summary>
	public float YawFacing( Numerics.Vector3 level )
	{
		static float Heading( Numerics.Vector3 v ) => MathF.Atan2( v.X, v.Z );

		var at0 = Heading( DirectionAt( 0f, 0f ) );
		var small = Heading( DirectionAt( 0.2f, 0f ) ) - at0;

		small -= MathF.Round( small / (2f * MathF.PI) ) * 2f * MathF.PI;

		var wanted = Heading( level ) - at0;
		var yaw = (small >= 0f ? wanted : -wanted) % (2f * MathF.PI);

		return yaw < 0f ? yaw + (2f * MathF.PI) : yaw;
	}

	/// <summary>A channel's place at <paramref name="share"/> of its clip: the share of its declared length, from its start.</summary>
	private static float FrameAt( AnimationFile clip, float share )
		=> clip.DeclaredFirstFrame + ((clip.DeclaredLastFrame - clip.DeclaredFirstFrame) * share);

	/// <summary>The model's lookup records whose flags share a bit with <paramref name="space"/>, in their order (<c>FUN_0044b2b0</c>, <c>FUN_0044b270</c>).</summary>
	private IEnumerable<int> Records( uint space )
		=> Enumerable.Range( 0, _model.Nodes.Count ).Where( node => _model.Nodes[node].Id >= 0 && (_model.Nodes[node].IdFlags & space) != 0 );

	private static bool IsUnturned( Matrix4x4 local )
		=> Matrix4x4.Decompose( local, out _, out var turn, out _ ) && MathF.Abs( MathF.Abs( turn.W ) - 1f ) < 1e-5f;
}
