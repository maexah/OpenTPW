using System.Diagnostics;

namespace OpenTPW;

/// <summary>
/// Times each phase of a level load and says what each one cost.
///
/// <para>
/// The log carries a stamp on every line, but only to the SECOND - and with a U+202F narrow
/// no-break space before the AM/PM, which defeats a naive parse. A stamp that coarse can localise a
/// twenty-second stall to one phase and can do nothing finer, so it cannot tell a phase that takes
/// 900 ms from one that takes 1,400. This reports milliseconds against the phase that spent them,
/// which turns "it hangs after the queues message" into "terrain took 20,800 ms".
/// </para>
///
/// <para>
/// <b>Unlike <see cref="FrameProfiler"/> this is left ON</b>, and that is a deliberate difference
/// rather than an oversight of the rule that puts performance tooling behind a switch. A profiler
/// runs every frame for ever; this runs once per phase per level load - about twenty lines against a
/// load measured in seconds - so it costs nothing measurable. It earns the lines twice over: the
/// loading bar draws the last line logged underneath itself, so a slow load says where it went
/// <i>while it is still on the screen</i>, to the player watching it.
/// </para>
///
/// <para>
/// To remove entirely: delete this file and the <c>load.Mark(...)</c> calls in
/// <see cref="Level.SetupParkEntities"/>.
/// </para>
/// </summary>
internal sealed class LoadTimer
{
	private readonly Stopwatch _sinceStart = Stopwatch.StartNew();
	private readonly Stopwatch _sincePhase = Stopwatch.StartNew();
	private readonly string _what;

	private string _worstPhase = "nothing";
	private double _worstMs;

	public LoadTimer( string what ) => _what = what;

	/// <summary>One phase finished: names what it cost, and starts timing the next.</summary>
	public void Mark( string phase )
	{
		var ms = _sincePhase.Elapsed.TotalMilliseconds;

		if ( ms > _worstMs )
		{
			_worstMs = ms;
			_worstPhase = phase;
		}

		Log.Info( $"[load] {_what}: {phase} took {ms:F0} ms" );
		_sincePhase.Restart();
	}

	/// <summary>The whole load, and which single phase spent the most of it.</summary>
	public void Done()
	{
		Log.Info( $"[load] {_what}: {_sinceStart.Elapsed.TotalMilliseconds:F0} ms in total, " +
			$"the worst phase being {_worstPhase} at {_worstMs:F0} ms" );
	}
}
