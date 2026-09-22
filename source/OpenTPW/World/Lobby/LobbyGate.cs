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
/// <b>The doors idle shut and swing open once, as the player enters this park</b> - see
/// <see cref="Open"/>. They used to loop open and shut for ever, which was a diagnostic standing in
/// for a park entry that nothing raised yet.
/// </para>
///
/// <para>
/// <b>Not every gate is a pair of hinged doors, and taking the jungle's for the rule is how this was
/// first got wrong.</b> Measured across all four rather than inferred from one: <c>Jun</c> two
/// rotation tracks over frames 0-60, its movement ending at 57; <c>Hal</c> two over 0-600 with the
/// movement ending at 60, so its clip runs ten times past the point its rails stop; <c>Spa</c> one,
/// the hatch, over 0-100 ending at 60; and <c>Fan</c> <b>no rotation at all</b> but two morph tracks
/// over 0-100, because its gate is a worm. Fantasy therefore gets no <see cref="MeshRotator"/>, and a
/// length taken from the rotator alone left it with nought - its park loading with no animation -
/// while dropping the model update would have frozen it outright. See <see cref="SwingSeconds"/>.
/// </para>
///
/// <para>
/// <b>The original does not animate this gate at all, and that is measured rather than assumed.</b>
/// <c>IslandLobby_LeaveForPark</c> (<c>0x005e1e30</c>) sets the lobby leaving,
/// <c>IslandPanel_KeyPuffAndEnterSound</c>, and a UI message 6 to the island panel's own tree
/// (<c>0x007cc4b4</c>) which <c>IslandPanel_Callback</c> does not handle at all, so it is the generic
/// close. Nothing there touches the gate, so the swing is ours, under <c>CLAUDE.md</c> rule 11.
/// </para>
///
/// <para>
/// <b>&gt;&gt;&gt; BUT "THE ORIGINAL IS BLANK AT THIS MOMENT" WAS WRONG, AND THIS COMMENT USED TO SAY
/// IT. &lt;&lt;&lt;</b> The field <c>LeaveForPark</c> sets, <c>+0x14</c>, is <c>param_1[5]</c> in the
/// lobby camera's own update - its <b>state machine</b> - and 1 means "swing round onto the island's
/// heading". The camera then flies in, and once its radius falls below 8 it calls vtable <c>+0x48</c>,
/// which the island lobby overrides with <c>FUN_005e1e50</c>: that sets the scene's choice to <b>2</b>,
/// and the state-3 teardown (<c>0x005d5cf0</c>) <i>returns</i> that field - the documented "choice 2
/// means play a park". So the original is <b>not</b> blank here. It moves the camera for about three
/// seconds and loads the park when the camera arrives, which is built in
/// <see cref="LobbyCameraMode.LeaveForPark"/>. The gate's swing remains a deviation, but it now plays
/// <i>over</i> a move the original really makes rather than standing in for nothing.
/// </para>
/// </summary>
public sealed class LobbyGate : Entity
{
	private readonly LobbyModel _model;

	/// <summary>
	/// The clip that swings the doors open - M1, the first of the pair every gate is authored as.
	/// Null for a gate that ships no clip, which must still let its park be entered.
	/// </summary>
	private readonly AnimationFile? _opening;

	/// <summary>
	/// How long <see cref="_opening"/> takes to finish swinging: its <i>movement's</i> length, not the
	/// clip's - see <see cref="MeshRotator.ClipSeconds"/>. Hallow's carries on for another nine seconds
	/// after its doors have stopped, and playing to that would hold a park entry open on a gate that
	/// had finished moving.
	/// </summary>
	private readonly float _swingSeconds;

	private enum Doors { Shut, Opening, Open }

	private Doors _doors = Doors.Shut;

	/// <summary>How far into the swing the doors are, in seconds.</summary>
	private float _swung;

	/// <summary>Run once, when the doors have finished opening - see <see cref="Open"/>.</summary>
	private Action? _whenOpen;

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

		_opening = _model.Clips.Count > 0 ? _model.Clips[0] : null;
		_swingSeconds = SwingSeconds( _model.Rotator?.ClipSeconds( 0 ), _model.Animators.Length > 0, _opening );
	}

	/// <summary>Whether these doors have been asked to open and have not finished - see <see cref="Open"/>.</summary>
	public bool IsOpening => _doors == Doors.Opening;

	/// <summary>
	/// How long this gate's opening clip should be played for, in seconds.
	///
	/// <para>
	/// <b>Not every gate swings.</b> The jungle's and hallow's are hinged doors driven by rotation, but
	/// fantasy's gate is a worm and space's a hatch, and a model whose opening clip turns nothing gets
	/// no <see cref="MeshRotator"/> at all - so taking the length from the rotator alone left those
	/// gates with no opening to play and let the park load at once.
	/// </para>
	///
	/// <para>
	/// A rotation gate is measured by its <i>movement</i> rather than its clip, because hallow's carries
	/// on for another nine seconds after its doors have stopped. Anything else is played over the span
	/// its clip declares. A clip that drives nothing this model can play is no opening at all, and
	/// answers nought so that the park stays reachable.
	/// </para>
	/// </summary>
	/// <param name="rotationSeconds">
	/// How long the opening clip turns for, or null where this model has no <see cref="MeshRotator"/>
	/// because that clip turns nothing.
	/// </param>
	/// <param name="morphs">Whether the model has an animator the clip can move instead.</param>
	internal static float SwingSeconds( float? rotationSeconds, bool morphs, AnimationFile? opening )
	{
		if ( opening == null )
			return 0f;

		if ( rotationSeconds is { } turning )
			return turning;

		if ( !morphs )
			return 0f;

		return Math.Max( opening.LastFrame - opening.FirstFrame, 0 ) / AnimationFile.FramesPerSecond;
	}

	/// <summary>
	/// Swings the doors open, once, and runs <paramref name="whenOpen"/> the moment they finish.
	///
	/// <para>
	/// The callback is how a park entry waits for the gate without the front end having to tick a panel
	/// it has already closed: this is an <see cref="Entity"/>, so it keeps being updated after the
	/// island panel has gone. Asking a gate that is already opening, or already open, does nothing -
	/// the button behind it can be pressed twice before a frame ends.
	/// </para>
	/// </summary>
	public void Open( Action? whenOpen = null )
	{
		// A gate shipping no clip, or one with nothing that turns in it, cannot be allowed to make the
		// player wait for ever: its park still has to be reachable.
		if ( _opening == null || _swingSeconds <= 0f )
		{
			whenOpen?.Invoke();
			return;
		}

		if ( _doors != Doors.Shut )
			return;

		_doors = Doors.Opening;
		_swung = 0f;
		_whenOpen = whenOpen;
	}

	/// <summary>
	/// Nothing is posed while the doors idle, and that is not an omission: a rotation key is the
	/// orientation a mesh should hold rather than a turn to add, and every one of these clips opens on
	/// the orientation its own mesh is authored with - see <c>MeshRotator.BuildRestInverses</c> - so
	/// <b>the model as it was built is the gate shut</b>. Posing frame 0 each frame would compute the
	/// transforms it already has.
	/// </summary>
	protected override void OnUpdate()
	{
		if ( _doors != Doors.Opening )
			return;

		// Time.Delta rather than a per-frame step, so the swing takes as long on any frame rate -
		// CLAUDE.md rule 10.
		_swung = MathF.Min( _swung + Time.Delta, _swingSeconds );

		_model.Pose( _opening!, _opening!.FirstFrame + (_swung * AnimationFile.FramesPerSecond) );

		if ( _swung < _swingSeconds )
			return;

		// Held where the swing ended - the pose above leaves the doors open - and whoever is waiting
		// told exactly once.
		_doors = Doors.Open;

		var finished = _whenOpen;
		_whenOpen = null;

		finished?.Invoke();
	}
}
