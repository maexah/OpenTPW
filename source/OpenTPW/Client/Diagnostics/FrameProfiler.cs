using System.Diagnostics;

namespace OpenTPW;

/// <summary>
/// Optional, self-contained per-frame timing tool. Disabled by default and costs nothing
/// when off. To remove entirely: delete this file and revert the one call site in
/// Renderer.cs's Run() back to a plain `Update();`.
/// </summary>
public static class FrameProfiler
{
	private const bool Enabled = false;
	private const int ReportIntervalFrames = 120;

	private static readonly Stopwatch _stopwatch = new();
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
			Log.Info( $"[FrameProfiler] avg={_sumMs / _frameCount:F2}ms worst={_worstMs:F2}ms over {_frameCount} frames" );

			_frameCount = 0;
			_sumMs = 0;
			_worstMs = 0;
		}
	}
}
