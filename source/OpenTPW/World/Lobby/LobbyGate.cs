using System.Numerics;

namespace OpenTPW;

/// <summary>
/// The gate on a lobby island - the way into that theme's park.
///
/// How much of the gate this model is varies by park. The jungle's is only its two doors and
/// hallow's only its two rails, because the structure they hang in is part of the island model.
/// Fantasy and space have no gateway on their island at all: their gate model is the whole
/// thing - fantasy's worm and its leaf sign, space's hatch and its two screens - which is why
/// those two parks had no front gate until this was built for every island rather than just the
/// jungle.
///
/// Its doors are the clearest example of a rotation animation in the game: Jun_gateM1 swings
/// door01 a quarter turn and door02 back the other way, and Jun_gateM2 is exactly the inverse.
/// The gate is authored in the same model space as the island it belongs to (the island's own
/// static 'gateway' mesh sits right where this one lands), so it needs no placement of ours.
///
/// <para>
/// <b>The park-entry flight plays it, as the original's does.</b> An island holds two animated models, its isle at
/// <c>island+4</c> and this gate at <c>island+8</c> (<c>FUN_005dfa60</c>, from the ISLAND() line's third and fourth
/// names). The camera plays the gate's M1, which opens it, as it finishes swinging round onto it (<c>0x005e06e4</c>),
/// and its M2, which shuts it, when Escape cancels the flight after that (<c>0x005e18ab</c>). See
/// <see cref="LobbyCameraMode.LeaveForPark"/> and <see cref="LobbyCameraMode.CancelLeave"/>; <c>docs/exe/lobby.md</c>,
/// "Escape cancels the fly-in".
/// </para>
///
/// <para>
/// <b>A clip is played once, over the span it declares.</b> The declared span is the engine's length
/// (<see cref="AnimationFile.DeclaredLastFrame"/>): 60 frames for the jungle's and hallow's M1 and M2, 100 for
/// fantasy's and space's, at 30 a second. A clip asked for while another is still playing waits for it - the
/// original's animation channel keeps one clip back and starts it when the playing one ends (<c>0x004732a0</c>) - so a
/// gate shut while it is still opening finishes opening first.
/// </para>
///
/// <para>
/// <b>Two things here are our reading, not the original's measured behaviour.</b> Between those two calls the gate
/// idles shut, and a clip that has ended is held on its last frame. The original's lobby loop may replay a model's M1
/// by itself whenever its channel has finished (<c>FUN_00473c70</c>, unless the instance's <c>+4</c> carries
/// <c>0x8004</c>), and whether a lobby channel holds a finished clip reads <c>+4 &amp; 0x18</c>; the lobby instances'
/// <c>+4</c> is not decoded (<c>docs/QUEUE.md</c> Q63, and the Unsettled list in <c>docs/exe/lobby.md</c>).
/// </para>
///
/// <para>
/// <b>Not every gate is a pair of hinged doors.</b> Measured across all four rather than inferred from one:
/// <c>Jun</c> and <c>Hal</c> turn two meshes, <c>Spa</c> one, the hatch, and <c>Fan</c> none at all but morphs two,
/// because its gate is a worm. <see cref="LobbyModel.Pose"/> poses both halves of a clip, so all four play the same
/// way.
/// </para>
/// </summary>
public sealed class LobbyGate : Entity
{
	/// <summary>M1, the clip that opens the gate - entry 0 of the model's role-M clips.</summary>
	private const int Opening = 0;

	/// <summary>M2, the clip that shuts it again - entry 1, M1 played backwards in every gate the game ships.</summary>
	private const int Shutting = 1;

	private readonly LobbyModel _model;

	/// <summary>The clip playing or held on its last frame, or -1 while none has played.</summary>
	private int _clip = -1;

	/// <summary>How far into <see cref="_clip"/> the gate is, in seconds.</summary>
	private float _played;

	/// <summary>The clip kept back until <see cref="_clip"/> ends, or -1 - the channel's one queued clip.</summary>
	private int _queued = -1;

	public LobbyGate( Vector3 _position, string themeName,
		IReadOnlyDictionary<string, Texture>? signTextures = null )
	{
		Position = _position;

		var modelPrefix = themeName[0..3];

		// Same 2.5 unit drop the island's own meshes get - the two share a model space.
		_model = new LobbyModel(
			$"lobby/terrain/{modelPrefix}_gate.md2",
			"lobby/terrain/textures",
			Position - new Vector3( 0, 0, 2.5f ),
			textureOverrides: signTextures );
	}

	/// <summary>Plays M1 once, which opens the gate - see the class remarks.</summary>
	public void Open() => Play( Opening );

	/// <summary>Plays M2 once, which shuts it, after M1 if that is still playing - see the class remarks.</summary>
	public void Shut() => Play( Shutting );

	/// <summary>
	/// How long the engine plays <paramref name="clip"/> for, in seconds: the span it declares, not the one its keys
	/// cover. Hallow's gate clips declare 60 frames and carry keys out to 600.
	/// </summary>
	internal static float PlaySeconds( AnimationFile clip )
		=> Math.Max( clip.DeclaredLastFrame - clip.DeclaredFirstFrame, 0 ) / AnimationFile.FramesPerSecond;

	/// <summary>Whether a clip is still playing rather than held or never started.</summary>
	private bool Playing => _clip >= 0 && _played < PlaySeconds( _model.Clips[_clip] );

	/// <summary>
	/// Starts clip <paramref name="clip"/>, or keeps it back until the one playing ends.
	/// </summary>
	/// <remarks>
	/// A model with no such clip plays nothing. That guard is ours, and dead by CONTENT: every gate
	/// <c>lobby.wad</c> ships has an M1 and an M2. The original's start path does not check the entry against the
	/// role's clip count (<c>FUN_00472f60</c>).
	/// </remarks>
	private void Play( int clip )
	{
		if ( clip >= _model.Clips.Count )
			return;

		if ( Playing )
		{
			_queued = clip;
			return;
		}

		_clip = clip;
		_played = 0f;
	}

	/// <summary>
	/// What the gate is doing, for the debug console, without spaces so that it reads as one field: <c>shut</c> while
	/// nothing has played, otherwise the clip, whether it is playing or held, how far into it, and what is queued -
	/// <c>M1,playing,0.53/2.00,then=M2</c>. A pure getter.
	/// </summary>
	internal string Describe()
	{
		if ( _clip < 0 )
			return "shut";

		var length = PlaySeconds( _model.Clips[_clip] );

		return $"M{_clip + 1},{(Playing ? "playing" : "held")},{_played:F2}/{length:F2}"
			+ (_queued >= 0 ? $",then=M{_queued + 1}" : "");
	}

	/// <summary>
	/// Plays the clip on, and poses nothing while none has played: a rotation key is the orientation a mesh should
	/// hold rather than a turn to add to it, and every one of these clips opens on the orientation its own mesh is
	/// authored with - see <c>MeshRotator.BuildRestInverses</c> - so the model as it was built is the gate shut.
	/// </summary>
	protected override void OnUpdate()
	{
		if ( !Playing )
			return;

		var clip = _model.Clips[_clip];
		var length = PlaySeconds( clip );

		// Time.Delta rather than a per-frame step, so a clip takes as long at any frame rate - CLAUDE.md rule 10.
		_played = MathF.Min( _played + Time.Delta, length );

		// The frame is the clip's own, counted from its declared start; posed at the very end too, so it is held
		// there rather than a step short of it.
		_model.Pose( clip, clip.DeclaredFirstFrame + (_played * AnimationFile.FramesPerSecond) );

		if ( _played < length || _queued < 0 )
			return;

		_clip = _queued;
		_queued = -1;
		_played = 0f;
	}
}
