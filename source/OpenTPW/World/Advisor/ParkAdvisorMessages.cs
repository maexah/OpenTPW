using System.Globalization;

namespace OpenTPW;

/// <summary>
/// Posted park-door messages, category 0, selection mode 2. The eight slots, strict tick threshold,
/// helper recheck and per-message histories follow FUN_0059a550; docs/exe/advisor-park.md, Q90.
/// Other park messages and the polling analyser remain unbuilt.
/// </summary>
internal sealed class ParkAdvisorMessages
{
	internal const int Opened = 128;
	internal const int Closed = 129;
	internal sealed record Topic( int Id, string ScoreKey, int FirstResponse, int FirstSample );
	internal static readonly Topic[] DoorTopics =
	[
		new( Opened, "ParkNowOpen.Score", 308, 342 ),
		new( Closed, "ParkNowClosed.Score", 310, 344 )
	];

	private sealed class History
	{
		public int LastTick;
		public int Line = -1;
		public bool Said;
	}

	private sealed record Pending( Topic Topic, int Score );
	private readonly SettingsFile _settings;
	private readonly Dictionary<int, Topic> _topics;
	private readonly Dictionary<int, History> _histories;
	private readonly Pending?[] _slots = new Pending?[8];
	private long _waitUntil;
	internal int Attempts { get; private set; }
	internal int PendingCount => _slots.Count( slot => slot != null );
	internal int Threshold => Value( "GeneralAdvisor.MinScoreForConsideration" );

	internal ParkAdvisorMessages( SettingsFile settings, IEnumerable<Topic>? topics = null )
	{
		_settings = settings;
		_topics = (topics ?? DoorTopics).ToDictionary( topic => topic.Id );
		_histories = _topics.Keys.ToDictionary( id => id, _ => new History() );
	}

	// Missing or malformed settings are installation errors, not permission to invent scores.
	private int Value( string key ) => int.Parse( _settings[key], CultureInfo.InvariantCulture );

	internal bool PostDoor( bool closed, int gameTick ) => Post( closed ? Closed : Opened, gameTick );

	internal bool Post( int id, int gameTick )
	{
		var topic = _topics[id];
		var score = Value( topic.ScoreKey );
		var history = _histories[id];
		var reason = "queued";
		if ( (uint)history.LastTick >> 2 != 0
			&& unchecked(((uint)gameTick >> 2) - ((uint)history.LastTick >> 2))
				< (uint)Value( "MessageGroups[0].MinTimeSameMessage" ) )
			reason = "cooldown";
		else if ( history.Said && Value( "MessageGroups[0].SayOnlyOnce" ) != 0 )
			reason = "said-once";
		else if ( _slots.Any( slot => slot?.Topic.Id == id ) )
			reason = "duplicate";
		else
		{
			var index = Array.FindIndex( _slots, slot => slot == null );
			if ( index < 0 )
			{
				index = 0;
				for ( var i = 1; i < _slots.Length; ++i )
					if ( _slots[i]!.Score < _slots[index]!.Score ) index = i;
				if ( score <= _slots[index]!.Score ) reason = "priority";
			}
			if ( reason == "queued" ) _slots[index] = new Pending( topic, score );
		}
		Log.Info( $"Park advisor: post message={id} score={score} threshold={Threshold} tick={gameTick} result={reason} pending={PendingCount}" );
		return reason == "queued";
	}

	/// <summary>
	/// One world sweep. Playback returns the response duration (zero when the speaker cannot start).
	/// The helper still succeeds after a zero playback return; its score recheck alone can reject here.
	/// </summary>
	internal void Tick( int gameTick, long milliseconds, Func<int, int, int> play )
	{
		if ( milliseconds < _waitUntil ) return;
		var index = -1;
		for ( var i = 0; i < _slots.Length; ++i )
			if ( _slots[i] is { } slot && (index < 0 || slot.Score > _slots[index]!.Score) ) index = i;
		if ( index < 0 || _slots[index]!.Score <= Threshold ) return;

		var pending = _slots[index]!;
		_slots[index] = null;
		var score = Value( pending.Topic.ScoreKey );
		if ( score < Threshold ) return;

		var history = _histories[pending.Topic.Id];
		var line = (history.Line + 1) % 2;
		var response = pending.Topic.FirstResponse + line;
		var sample = pending.Topic.FirstSample + line;
		var duration = play( response, sample );
		_waitUntil = milliseconds + duration + 1000;
		history.LastTick = gameTick;
		history.Line = line;
		history.Said = true;
		++Attempts;
		Log.Info( $"Park advisor: attempt={Attempts} message={pending.Topic.Id} score={score} threshold={Threshold} response={response} sample={sample} line={line} tick={gameTick} duration={duration} waitUntil={_waitUntil}" );
	}

	internal string Census() => $"park advisor: pending={PendingCount} attempts={Attempts} threshold={Threshold} "
		+ string.Join( " ", _topics.Values.Select( topic =>
			$"message={topic.Id}/score={Value( topic.ScoreKey )}/line={_histories[topic.Id].Line}/lastTick={_histories[topic.Id].LastTick}" ) );
}
