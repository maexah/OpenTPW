using System.Diagnostics;

namespace OpenTPW;

/// <summary>
/// Optional, self-contained per-frame timing tool. Off unless OPENTPW_PROFILE is set, and costs
/// one branch a frame when off. It times the whole of a frame's <c>Update</c> (events, the world's
/// update, the draw's recording, and the present, which waits on the display's refresh when
/// vertical sync is on), and the frames a second the loop actually ran at. To remove entirely: delete
/// this file and revert the one call site in Renderer.cs's Run() back to a plain `Update();`.
/// </summary>
public static class FrameProfiler
{
	private static readonly bool Enabled = Environment.GetEnvironmentVariable( "OPENTPW_PROFILE" ) is { Length: > 0 } and not "0";
	private const int ReportIntervalFrames = 120;

	private static readonly Stopwatch _stopwatch = new();
	private static readonly Stopwatch _wall = Stopwatch.StartNew();
	private static double _lastReportMs;
	private static int _gen0, _gen1, _gen2;
	private static double _worstMs;
	private static double _sumMs;
	private static int _frameCount;

	public static void Wrap( Action update )
	{
		if ( !Enabled )
		{
			update();
			return;
		}

		_stopwatch.Restart();
		update();
		_stopwatch.Stop();

		var ms = _stopwatch.Elapsed.TotalMilliseconds;
		_sumMs += ms;
		_worstMs = Math.Max( _worstMs, ms );
		_frameCount++;

		if ( _frameCount >= ReportIntervalFrames )
		{
			var wallMs = _wall.Elapsed.TotalMilliseconds;
			var g0 = GC.CollectionCount( 0 );
			var g1 = GC.CollectionCount( 1 );
			var g2 = GC.CollectionCount( 2 );

			Log.Info( $"[FrameProfiler] avg={_sumMs / _frameCount:F2}ms worst={_worstMs:F2}ms over {_frameCount} frames, " +
				$"{_frameCount * 1000.0 / (wallMs - _lastReportMs):F1} fps, gc {g0 - _gen0}/{g1 - _gen1}/{g2 - _gen2}, " +
				$"heap {GC.GetTotalMemory( false ) / 1048576.0:F0} MB, allocated {GC.GetTotalAllocatedBytes() / 1048576.0:F0} MB" );

			_lastReportMs = wallMs;
			_gen0 = g0;
			_gen1 = g1;
			_gen2 = g2;

			_frameCount = 0;
			_sumMs = 0;
			_worstMs = 0;
		}
	}
}
