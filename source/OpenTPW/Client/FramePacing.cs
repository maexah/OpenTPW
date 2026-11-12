using System.Diagnostics;

namespace OpenTPW;

/// <summary>
/// VSync and the frame limit (Q19), and the measured frame time the log reports for them. <b>OpenTPW's own</b>: the original
/// has no such settings (it presents through DirectDraw and its frame rate is whatever its loop makes), so both are kept in
/// save\opentpw.cfg (<c>vsync</c>, <c>framelimit</c>; <see cref="SaveFolder.LoadDisplay"/>) and read as the game starts.
///
/// <para>
/// VSync on waits for the display's refresh as each frame is presented, as the game always has. The frame limit is a cap of
/// <see cref="LowestLimit"/> frames a second or more, or <see cref="Unlimited"/>; with no <c>framelimit</c> line (a first launch)
/// it is the monitor's refresh rate (<see cref="FirstLimit"/>). With VSync on, a limit at or above the refresh rate leaves the
/// pacing to VSync (<see cref="Limits"/>): two clocks a hair apart would otherwise drop a frame now and then.
/// </para>
/// </summary>
internal sealed class FramePacing
{
	/// <summary>No cap: the frame rate is what the machine makes, or the refresh rate with VSync on.</summary>
	public const int Unlimited = 0;

	/// <summary>No <c>framelimit</c> line has been read: the start takes the monitor's refresh rate (<see cref="FirstLimit"/>).</summary>
	public const int NotChosen = -1;

	/// <summary>The lowest cap: the item's range runs from 30 up to Unlimited.</summary>
	public const int LowestLimit = 30;

	/// <summary>The cap a first launch takes where SDL will not say the monitor's refresh rate (it reports 0). OpenTPW's choice.</summary>
	public const int RefreshUnknown = 60;

	/// <summary>How long a setting is measured before its line is logged (<see cref="Measured"/>).</summary>
	public static readonly TimeSpan MeasureFor = TimeSpan.FromSeconds( 5 );

	/// <summary>The cap a first launch takes: the monitor's refresh rate, at least <see cref="LowestLimit"/>, or <see cref="RefreshUnknown"/> if it is not known.</summary>
	public static int FirstLimit( int? refreshRate )
		=> refreshRate is { } rate && rate > 0 ? Math.Max( rate, LowestLimit ) : RefreshUnknown;

	/// <summary>
	/// The refresh rate of the mode the display is in: under <see cref="DisplayMode.FullScreen"/> the mode it was put into
	/// (<see cref="GameOptions.FullScreenSize"/>, whose rate is the highest the mode list keeps for its size), otherwise, or
	/// where that mode has no size or rate, the desktop's (<paramref name="desktop"/>, 0 if SDL will not say).
	/// </summary>
	public static int RefreshInUse( DisplayMode mode, VideoMode fullScreen, int desktop )
		=> mode == DisplayMode.FullScreen && fullScreen is { Width: > 0, Height: > 0, RefreshRate: > 0 } ? fullScreen.RefreshRate : desktop;

	/// <summary>
	/// The start's choice: with no <c>framelimit</c> line read (a first launch) the limit becomes <see cref="FirstLimit"/> of
	/// <paramref name="refreshRate"/>, which the next write of opentpw.cfg keeps; a limit read is left alone. Answers the line
	/// the start logs. VSync is the device's own flag, made from <see cref="GameOptions.VSync"/> as the device is built
	/// (<c>Renderer.CreateGraphicsDevice</c>), and is not set here.
	/// </summary>
	public static string Start( GameOptions options, int refreshRate )
	{
		var first = options.FrameLimit == NotChosen;

		if ( first )
			options.FrameLimit = FirstLimit( refreshRate );

		var limit = Clamp( options.FrameLimit );

		return $"Frames: vsync {(options.VSync ? "on" : "off")}, {(limit > 0 ? $"limit {limit}" : "unlimited")}"
			+ $"{(first ? $" (no framelimit line, so the monitor's refresh rate{(refreshRate > 0 ? "" : $", which SDL does not say, so {RefreshUnknown}")})" : "")}, "
			+ $"refresh {refreshRate}: {Who( options.VSync, limit, refreshRate )}";
	}

	private static string Who( bool vsync, int limit, int refreshRate )
		=> Limits( vsync, limit, refreshRate ) ? "the limiter paces" : vsync ? "VSync paces" : "nothing paces";

	/// <summary>A cap as opentpw.cfg's number gives it: none or below none is Unlimited, anything else at least <see cref="LowestLimit"/>.</summary>
	public static int Clamp( int limit ) => limit <= 0 ? Unlimited : Math.Max( limit, LowestLimit );

	/// <summary>
	/// Whether the limiter paces the frames: a cap is set, and VSync is off or the cap is below the refresh rate (with VSync on
	/// the display already holds the frames to its rate). A refresh rate of nothing (not known) leaves the cap to the limiter.
	/// </summary>
	public static bool Limits( bool vsync, int limit, int refreshRate )
		=> limit > 0 && !(vsync && refreshRate > 0 && limit >= refreshRate);

	/// <summary>One frame's share of a second at <paramref name="limit"/> frames a second.</summary>
	public static TimeSpan Period( int limit ) => TimeSpan.FromTicks( TimeSpan.TicksPerSecond / limit );

	/// <summary>
	/// When the frame after one due at <paramref name="due"/> and begun at <paramref name="now"/> is due. Counted on from when it
	/// was due, so the cap holds its rate however late within a period a frame begins (counted from <paramref name="now"/>, every
	/// period would stretch by that lateness); a frame a whole period or more late starts the count again from now, rather than
	/// letting the next frames run uncapped to catch up. <see cref="Time.NextBeat"/>'s rule, on a <see cref="TimeSpan"/>: a
	/// float's seconds lose the millisecond after a few hours.
	/// </summary>
	public static TimeSpan NextDue( TimeSpan due, TimeSpan period, TimeSpan now )
		=> now - due < period ? due + period : now + period;

	/// <summary>
	/// Waits until <paramref name="due"/> on <paramref name="clock"/>: sleeps a millisecond at a time while more than
	/// <see cref="SpinUnder"/> is left (a sleep can overrun by a millisecond or so), then yields until it is reached.
	/// </summary>
	public static void WaitUntil( TimeSpan due, Func<TimeSpan> clock, Action<int> sleep )
	{
		for ( var left = due - clock(); left > TimeSpan.Zero; left = due - clock() )
		{
			if ( left > SpinUnder )
				sleep( 1 );
			else
				Thread.Yield();
		}
	}

	/// <summary>Below this the wait yields rather than sleeps.</summary>
	public static readonly TimeSpan SpinUnder = TimeSpan.FromMilliseconds( 2 );

	/// <summary>The words of the measured line (<see cref="Measured"/>): the settings, and what the frames took.</summary>
	public static string Line( bool vsync, int limit, int refreshRate, int frames, TimeSpan over, TimeSpan shortest, TimeSpan longest )
	{
		var cap = limit > 0 ? $"limit {limit}" : "unlimited";
		var who = Who( vsync, limit, refreshRate );
		var average = frames > 0 ? over.TotalMilliseconds / frames : 0;

		return $"Frames: vsync {(vsync ? "on" : "off")}, {cap}, refresh {refreshRate} ({who}): {frames} frames in {over.TotalSeconds:F2} s, "
			+ $"{average:F2} ms a frame (shortest {shortest.TotalMilliseconds:F2}, longest {longest.TotalMilliseconds:F2})";
	}

	private readonly Func<TimeSpan> _clock;
	private readonly Action<int> _sleep;

	/// <summary>The refresh rate of the mode the display is in (<see cref="RefreshInUse"/>), 0 if SDL would not say; <see cref="UseRefresh"/> changes it.</summary>
	public int RefreshRate { get; private set; }

	/// <summary>When the next frame may begin, while the limiter paces.</summary>
	private TimeSpan? _due;

	/// <summary>The settings being measured, and what has been measured of them since <see cref="_since"/>.</summary>
	private (bool VSync, int Limit)? _measuring;
	private TimeSpan _since;
	private int _frames;
	private TimeSpan _shortest, _longest;
	private bool _logged;

	/// <summary>The line logged once <see cref="MeasureFor"/> has been measured at a setting, until the setting changes.</summary>
	public string? Measured { get; private set; }

	public FramePacing( int refreshRate, Func<TimeSpan> clock, Action<int> sleep )
	{
		RefreshRate = Math.Max( refreshRate, 0 );
		_clock = clock;
		_sleep = sleep;
	}

	/// <summary>A pacing on the real clock and <see cref="Thread.Sleep(int)"/>.</summary>
	public static FramePacing OnRealClock( int refreshRate )
	{
		var clock = Stopwatch.StartNew();
		return new FramePacing( refreshRate, () => clock.Elapsed, Thread.Sleep );
	}

	/// <summary>
	/// At the top of each frame: waits for the cap where the limiter paces (<see cref="Limits"/>). The options are read every
	/// frame, so a limit changed while the game runs paces from the next frame.
	/// </summary>
	public void Wait( GameOptions options )
	{
		var limit = Clamp( options.FrameLimit );

		if ( Limits( options.VSync, limit, RefreshRate ) )
		{
			// The first frame paced is due as it begins.
			if ( _due is { } due )
				WaitUntil( due, _clock, _sleep );

			var now = _clock();
			_due = NextDue( _due ?? now, Period( limit ), now );
		}
		else
			_due = null;
	}

	/// <summary>
	/// Counts a frame's time, from the start of the frame before to the start of this one with the wait in it, into the measured
	/// line; a change of the settings starts the measuring again.
	/// </summary>
	public void Count( GameOptions options, TimeSpan frame ) => Measure( options.VSync, Clamp( options.FrameLimit ), frame );

	/// <summary>
	/// The display was put into another mode (<see cref="Display.Apply"/>): the limiter compares against
	/// <paramref name="refreshRate"/> from the next frame, its count starts again, and the frames are measured again.
	/// </summary>
	public void UseRefresh( int refreshRate )
	{
		RefreshRate = Math.Max( refreshRate, 0 );
		_due = null;
		_logged = false;
		Measured = null;

		if ( _measuring is { } setting )
			Restart( setting );
	}

	/// <summary>The frames stop being measured until the next one (a minimised window, whose frames draw nothing), and the limiter's count starts again.</summary>
	public void Interrupt()
	{
		_due = null;

		if ( _measuring is { } setting )
			Restart( setting );
	}

	private void Measure( bool vsync, int limit, TimeSpan lastFrame )
	{
		if ( _measuring != (vsync, limit) )
		{
			_logged = false;
			Measured = null;
			Restart( (vsync, limit) );
			return;
		}

		if ( _logged )
			return;

		++_frames;
		_shortest = _frames == 1 || lastFrame < _shortest ? lastFrame : _shortest;
		_longest = lastFrame > _longest ? lastFrame : _longest;

		var over = _clock() - _since;

		if ( over < MeasureFor )
			return;

		Measured = Line( vsync, limit, RefreshRate, _frames, over, _shortest, _longest );
		_logged = true;
		Log.Info( Measured );
	}

	private void Restart( (bool VSync, int Limit) setting )
	{
		_measuring = setting;
		_since = _clock();
		_frames = 0;
		_shortest = _longest = TimeSpan.Zero;
	}
}
