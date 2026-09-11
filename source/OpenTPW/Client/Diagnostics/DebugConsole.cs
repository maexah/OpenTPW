using System.Collections.Concurrent;
using System.Globalization;

namespace OpenTPW;

/// <summary>
/// Optional, self-contained command channel on stdin, for driving the game without a human at
/// the keyboard - reproducing a camera angle exactly, forcing an effect that is otherwise random,
/// or reading back frame timings.
///
/// Disabled unless OPENTPW_DEBUG_CONSOLE=1 is set, and costs one boolean test per frame when off.
/// To remove entirely: delete this file, the one call site in Level.Update(), Time.Paused and Time.StepFrames, and
/// the members marked as being for it - LobbyCameraMode's DebugOrbit, DebugSelect and DebugSettle; LobbyWeather's
/// Current, DebugRain, DebugBolt and DebugStrike; LobbyAudio's Muted and State; LobbyFlyer's DebugClosestApproach
/// and DebugClosestSolid; LobbyLightning's DebugAxisDistance and DebugOpacity; and the advisor's Say and State.
///
/// Engine and content: neither. It drives and reads both, and nothing else depends on it.
///
/// Usage:
///
///     mkfifo /tmp/tpw.in
///     OPENTPW_DEBUG_CONSOLE=1 dotnet OpenTPW.dll &lt; /tmp/tpw.in
///     echo "island 2" &gt; /tmp/tpw.in
///
/// Every command answers on stdout with a line beginning "[dbg]", so a caller can wait for the
/// reply rather than guessing how long something took.
/// </summary>
public static class DebugConsole
{
	private static readonly bool Enabled =
		Environment.GetEnvironmentVariable( "OPENTPW_DEBUG_CONSOLE" ) == "1";

	private static readonly ConcurrentQueue<string> _pending = new();
	private static bool _started;

	// A rolling window rather than an average since launch, so `stats` reports what the game is
	// doing now instead of being dragged down by the loading frames.
	private const int Window = 240;
	private static readonly double[] _frames = new double[Window];
	private static int _frameIndex;
	private static int _frameCount;

	/// <summary>Called once per frame. Does nothing at all unless the environment asked for it.</summary>
	public static void Poll()
	{
		if ( !Enabled )
			return;

		// A paused clock reports a zero delta, which would otherwise fill the window with zeroes
		// and have `stats` claim an infinite frame rate.
		if ( !Time.Paused )
			Record( Time.Delta );

		if ( !_started )
			Start();

		while ( _pending.TryDequeue( out var line ) )
			Execute( line );
	}

	private static void Record( float delta )
	{
		_frames[_frameIndex] = delta * 1000.0;
		_frameIndex = (_frameIndex + 1) % Window;
		_frameCount = Math.Min( _frameCount + 1, Window );
	}

	private static void Start()
	{
		_started = true;

		var thread = new Thread( () =>
		{
			// Blocks here for the life of the process; ends on EOF, which is what closing the
			// writing end of the pipe does.
			while ( Console.ReadLine() is { } line )
			{
				if ( !string.IsNullOrWhiteSpace( line ) )
					_pending.Enqueue( line.Trim() );
			}
		} )
		{ IsBackground = true, Name = "OpenTPW debug console" };

		thread.Start();
		Reply( "ready" );
	}

	private static void Execute( string line )
	{
		var parts = line.Split( ' ', StringSplitOptions.RemoveEmptyEntries );
		var command = parts[0].ToLowerInvariant();

		float Argument( int index, float fallback = 0f )
			=> parts.Length > index
				&& float.TryParse( parts[index], NumberStyles.Float, CultureInfo.InvariantCulture, out var value )
					? value
					: fallback;

		switch ( command )
		{
			case "island":
				LobbyCameraMode.DebugSelect( (int)Argument( 1 ) );
				Reply( $"island {LobbyCameraMode.CurrentIsland?.Index} '{LobbyCameraMode.CurrentIsland?.ParkName}'" );
				break;

			case "orbit":
				LobbyCameraMode.DebugOrbit = Argument( 1 );
				Reply( $"orbit {LobbyCameraMode.DebugOrbit:F3}" );
				break;

			case "freeze":
				LobbyCameraMode.Paused = true;
				Reply( "frozen" );
				break;

			// `freeze` stops the camera orbiting but leaves the world running, which is enough to
			// hold a composition and no use at all for comparing two builds: the ocean keeps
			// scrolling, the sky keeps drifting and the Dino keeps moving, so no two frames match.
			// `pause` stops the clock itself, and then a screenshot is repeatable.
			case "pause":
				Time.Paused = true;
				Reply( "paused" );
				break;

			case "resume":
				Time.Paused = false;
				Reply( "resumed" );
				break;

			// Runs the world on for a counted number of fixed-size frames and stops again, so a
			// caller can put the lobby at exactly the same point every run: pause, pick an island,
			// step, shoot. Poll `state` for stepping=0 to know it has finished.
			case "step":
				Time.Paused = true;
				Time.StepFrames = parts.Length > 1 ? (int)Argument( 1 ) : 1;
				Reply( $"stepping {Time.StepFrames}" );
				break;

			case "unfreeze":
				LobbyCameraMode.Paused = false;
				Reply( "running" );
				break;

			case "settle":
				// Drops the camera straight onto where it is headed, so a screenshot taken next
				// frame is the same every run instead of depending on how long the ease had.
				LobbyCameraMode.DebugSettle();
				Reply( "settled" );
				break;

			case "strike":
				LobbyWeather.Current?.DebugStrike();
				Reply( "strike" );
				break;

			case "rain":
				if ( LobbyWeather.Current != null )
					LobbyWeather.Current.DebugRain = parts.Length > 1 ? Argument( 1 ) : null;

				Reply( $"rain {LobbyWeather.Current?.DebugRain?.ToString( "F2" ) ?? "script"}" );
				break;

			case "near":
				if ( parts.Length > 1 && parts[1] == "reset" )
				{
					LobbyFlyer.DebugClosestApproach = float.MaxValue;
					LobbyFlyer.DebugClosestSolid = float.MaxValue;
				}

				Reply( Near() );
				break;

			case "stats":
				Reply( Stats() );
				break;

			case "state":
				Reply( State() );
				break;

			case "volume":
				if ( parts.Length > 1 )
					Audio.MasterVolume = Argument( 1, Audio.MasterVolume );

				Reply( $"volume={Audio.MasterVolume:0.00}" );
				break;

			case "mute":
				if ( LobbyAudio.Current != null )
					LobbyAudio.Current.Muted = parts.Length < 2 || parts[1] != "0";

				Reply( $"muted={LobbyAudio.Current?.Muted}" );
				break;

			case "sound":
				Reply( LobbyAudio.Current?.State() ?? "no lobby audio" );
				break;

			case "speech":
				// Auditions one of the 641 global speech samples, ducking the rest of the mix
				// exactly as a real line would. This is how Advisor's first-launch sample
				// gets confirmed - there is no way to read it out of the executable.
				if ( Advisor.Current == null )
					Reply( "no advisor" );
				else if ( parts.Length > 1 )
					Advisor.Current.Say( (int)Argument( 1, 1 ) );
				else
					Advisor.Current.Hush();

				Reply( Advisor.Current?.State() ?? "no advisor" );
				break;

			case "advisor":
				Reply( Advisor.Current?.State() ?? "no advisor" );
				break;

			case "greet":
				// Replays the front-end greeting the way the lobby gives it on arrival with no saved
				// players, so its timing and ducking can be captured at a moment of the caller's choosing.
				UI.FrontEndLines.Greet( usedSlots: 0 );
				Reply( Advisor.Current?.State() ?? "no advisor" );
				break;

			case "duck":
				Audio.Duck( parts.Length > 1 ? Argument( 1, 1f ) : 1f, 0.3f );
				Reply( $"duck={Audio.DuckLevel:0.00}" );
				break;

			case "quit":
				Reply( "quitting" );
				Environment.Exit( 0 );
				break;

			default:
				Reply( $"unknown command '{command}' - island/orbit/freeze/unfreeze/pause/resume/step/settle/strike/rain/near/stats/state/volume/mute/sound/speech/advisor/greet/duck/quit" );
				break;
		}
	}

	/// <summary>
	/// How near things have come to the camera, which is otherwise very hard to catch: a flyer
	/// passing close is a fraction of a second and a bolt landing on the camera is a one in
	/// sixty-four roll.
	///
	/// "closest" says close passes are actually happening, so a clean "solid" number means the
	/// fade caught them rather than that nothing came near. Both are in multiples of the flyer's
	/// own radius, which is what the fade band is written in.
	/// </summary>
	private static string Near()
	{
		static string Radii( float value ) => value == float.MaxValue ? "-" : value.ToString( "F2" );

		var bolt = LobbyWeather.Current?.DebugBolt;

		var boltDistance = bolt == null || bolt.DebugAxisDistance == float.MaxValue
			? "-"
			: bolt.DebugAxisDistance.ToString( "F1" );

		return $"near flyers closest={Radii( LobbyFlyer.DebugClosestApproach )}r "
			+ $"closestSolid={Radii( LobbyFlyer.DebugClosestSolid )}r "
			+ $"bolt axisDist={boltDistance} opacity={bolt?.DebugOpacity ?? 0f:F2}";
	}

	private static string Stats()
	{
		if ( _frameCount == 0 )
			return "stats no frames yet";

		var window = _frames.Take( _frameCount ).ToArray();
		var sorted = window.Order().ToArray();

		var mean = window.Average();
		var p99 = sorted[Math.Min( sorted.Length - 1, (int)(sorted.Length * 0.99) )];

		var entities = Entity.All.Count;
		var models = Asset.All.OfType<Model>().Count();

		return $"stats fps={1000.0 / mean:F1} mean={mean:F2}ms p99={p99:F2}ms worst={sorted[^1]:F2}ms "
			+ $"frames={_frameCount} entities={entities} models={models}";
	}

	private static string State()
	{
		var island = LobbyCameraMode.CurrentIsland;
		var script = island?.Script;

		return $"state island={island?.Index} name='{island?.ParkName}' "
			+ $"orbit={LobbyCameraMode.DebugOrbit:F3} paused={LobbyCameraMode.Paused} "
			+ $"clock={(Time.Paused ? "paused" : "running")} stepping={Time.StepFrames} "
			+ $"rainy={script?.Rainy} lightning={script?.Lightning} "
			+ $"strikes/s={script?.StrikesPerSecond:F3} flyers={script?.FlyingMeshes.Count}";
	}

	private static void Reply( string message )
	{
		Console.Out.WriteLine( $"[dbg] {message}" );
		Console.Out.Flush();
	}
}
