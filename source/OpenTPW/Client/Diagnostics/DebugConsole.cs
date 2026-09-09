using System.Collections.Concurrent;
using System.Globalization;

namespace OpenTPW;

/// <summary>
/// Optional, self-contained command channel on stdin, for driving the game without a human at
/// the keyboard - reproducing a camera angle exactly, forcing an effect that is otherwise random,
/// or reading back frame timings.
///
/// Disabled unless OPENTPW_DEBUG_CONSOLE=1 is set, and costs one boolean test per frame when off.
/// To remove entirely: delete this file, the one call site in Level.Update(), and the four
/// members on LobbyCameraMode and LobbyWeather marked as being for it.
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

			case "stats":
				Reply( Stats() );
				break;

			case "state":
				Reply( State() );
				break;

			case "quit":
				Reply( "quitting" );
				Environment.Exit( 0 );
				break;

			default:
				Reply( $"unknown command '{command}' - island/orbit/freeze/unfreeze/settle/strike/rain/stats/state/quit" );
				break;
		}
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
			+ $"rainy={script?.Rainy} lightning={script?.Lightning} "
			+ $"strikes/s={script?.StrikesPerSecond:F3} flyers={script?.FlyingMeshes.Count}";
	}

	private static void Reply( string message )
	{
		Console.Out.WriteLine( $"[dbg] {message}" );
		Console.Out.Flush();
	}
}
