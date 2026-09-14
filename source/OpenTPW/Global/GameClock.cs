namespace OpenTPW;

/// <summary>
/// The game's own clock - the one a park's menu stops.
///
/// <para>
/// This is not <see cref="Time"/>. That clock measures frames and never stops for a menu: the
/// interface, the pointer, the button glints and the camera all run on it, and they have to carry on
/// while the world is held. This one measures how much of the <i>game</i> has happened, and a park's
/// menu, message box or options screen stops it dead.
/// </para>
/// <para>
/// <b>The original keeps one of these as a global object at 0x785970</b>, with GameClock_Pause
/// (0x00402d90) and GameClock_Resume (0x00402db0) over it. It is three layers: a stopwatch over a raw
/// millisecond counter that pause freezes by capturing where it had got to (0x00403030/0x00403050),
/// a second stopwatch on another source that pause stops in lockstep with the first, and a fixed-step
/// latch (0x00402ea0) that makes the clock <i>read</i> a value stepped one tick at a time instead of
/// following real time. Only the first of those matters here, because the latch is for recorded play -
/// the park enters it only while 0x00878128 is set (0x0054f455), which nothing offline ever sets.
/// </para>
/// <para>
/// <b>Pause is nothing but a frozen clock, and that is the whole trick.</b> Neither loop tests a paused
/// flag. Each reads the clock, works out how far behind its last step it is, and steps while it is
/// behind - so a clock that stops reporting new time simply leaves the loop with nothing to do, and
/// every single thing the tick drives stops together without any of them knowing why. That is copied
/// here exactly: <see cref="Paused"/> only zeroes <see cref="Delta"/>, and <see cref="TicksDue"/> then
/// comes out zero by itself.
/// </para>
/// <para>
/// <b>Only a park pauses.</b> The helper the three screens call (0x004092a0) opens with
/// <c>if ([0x00786ba4] == 1)</c>, and every call site tests the same global before calling - the
/// message box (0x0047f251) also wanting the lobby's front end to be absent (0x00f82884 == 0). So
/// in the lobby the clock never stops, whatever is open over it. The lobby holds its advisor instead,
/// which is <see cref="Advisor.Paused"/>, and that is unaffected by any of this.
/// </para>
/// <para>
/// <b>The debug console's <c>pause</c> is a different thing and still works.</b> That one stops
/// <see cref="Time"/> itself so a screenshot is repeatable, and since this clock is driven from
/// <see cref="Time.Delta"/> it stops with it - including <c>step</c>, which steps both.
/// </para>
/// </summary>
public static class GameClock
{
	/// <summary>
	/// One tick: 31 milliseconds, in both scenes.
	///
	/// <para>
	/// <b>The odd number is not a rounding of 30fps, and it is not 31.25 either.</b> The original sets
	/// its step with <c>1000 / rate</c> in whole milliseconds (0x00402ea0), and the park asks for a
	/// rate of 32 (<c>PUSH 0x20</c> at 0x0054f45f), so the step is 1000/32 <i>truncated</i> to 31.
	/// Both loops then add that same <c>0x1f</c> by hand every step - the lobby at 0x0054e780, a park
	/// at 0x0054f4c4 - so the beat is 31ms flat and the game runs a shade over 32 ticks a second.
	/// </para>
	/// </summary>
	public const float TickSeconds = 0.031f;

	/// <summary>
	/// How far behind the lobby will catch up in one frame: 0x1f4 = 500ms, tested at 0x0054e768.
	/// Beyond that the original re-bases its last-stepped time to <c>now - 500</c> and the missed
	/// ticks are simply never run.
	/// </summary>
	public const float LobbyCatchUp = 0.5f;

	/// <summary>
	/// The same for a park: 0x7d0 = 2000ms, tested at 0x0054f493. A park is allowed four times the
	/// lobby's backlog because it has a world to keep consistent - dropping two seconds of ticks
	/// there loses two seconds of everything the tick drives, where in the lobby it loses some
	/// particles.
	/// </summary>
	public const float ParkCatchUp = 2f;

	/// <summary>Whether the world is held - see the class remarks for what that does and does not stop.</summary>
	public static bool Paused { get; private set; }

	/// <summary>
	/// How much game time this frame is worth, in seconds: zero while <see cref="Paused"/>. Anything
	/// belonging to the world reads this rather than <see cref="Time.Delta"/>, and so stops when the
	/// world does.
	/// </summary>
	public static float Delta { get; private set; }

	/// <summary>
	/// How much game time has passed altogether. It carries on across a scene change, as the original's
	/// clock does - it is a global built at boot and never rebuilt - so it is the age of the session's
	/// play, not of the park.
	/// </summary>
	public static float Now { get; private set; }

	/// <summary>
	/// How many 31ms ticks have come due this frame. Usually 0 or 1; more after a long frame, and 0
	/// for every frame of a pause.
	///
	/// <para>
	/// Every system that runs on the tick reads this same count, rather than one of them draining a
	/// queue. The original instead runs a single loop that calls each system once per tick
	/// (0x0054f4bf onwards: the particles, the thing engine, the schedulers, the music level), which
	/// is the same thing while no two of them depend on the order they see each other in. <b>When
	/// there is a simulation that does</b> - guests reacting to what rides did in the same tick -
	/// <b>this has to become one loop calling each system per tick</b>, not a count each of them
	/// loops over separately.
	/// </para>
	/// </summary>
	public static int TicksDue { get; private set; }

	/// <summary>
	/// Ticks run since the game started. The original keeps this too, incrementing 0x00877d34 once
	/// per step of the park's loop. Reported by the debug console's <c>state</c>, which is how a
	/// pause can be checked without looking at pixels.
	/// </summary>
	public static int Ticks { get; private set; }

	/// <summary>Game time owed but not yet worth a whole tick.</summary>
	private static float _owed;

	/// <summary>
	/// Whether the next frame's elapsed time is the one a <see cref="Rebase"/> is throwing away - see
	/// there for why a scene must not be billed for its own loading.
	/// </summary>
	private static bool _rebased;

	/// <summary>
	/// Drops the backlog and any pause, as entering a scene does.
	///
	/// <para>
	/// The original re-bases its own tick baselines on entering a park - 0x0054ed7c reads the clock
	/// three times in a row and writes it into all three of them - so the seconds a level spent
	/// loading are not owed as ticks the moment the first frame runs. Without this the catch-up cap
	/// would still bound it, but a park would open by running its full two seconds of backlog in one
	/// frame.
	/// </para>
	/// </summary>
	public static void Rebase()
	{
		_owed = 0f;
		TicksDue = 0;
		Paused = false;
		_rebased = true;
	}

	/// <summary>
	/// One frame of game time, driven by <see cref="Level.Update"/> before anything reads it.
	/// </summary>
	/// <param name="paused">Whether the world is held this frame.</param>
	/// <param name="catchUp">
	/// The most backlog this scene will work through in one frame - <see cref="LobbyCatchUp"/> or
	/// <see cref="ParkCatchUp"/>.
	/// </param>
	public static void Update( bool paused, float catchUp )
	{
		Paused = paused;

		// Continuous game time takes the clamped frame, because what reads it integrates straight -
		// a model's animation walks forward by however much it is handed, and after a stall it would
		// jump by the whole stall. That is what Time.LongestFrame is for and it still applies here.
		Delta = paused ? 0f : Time.Delta;
		Now += Delta;

		// The tick backlog takes the unclamped frame instead, because this is where the original
		// bounds a long frame: not by shortening it, but by refusing more than catchUp of arrears.
		// Clamping what is owed is the same thing as the original re-basing its last-stepped time to
		// now - cap (0x0054e770, 0x0054f49b) - either way the ticks older than the cap are dropped
		// rather than run late. Fed the clamped frame this cap could never be reached at all.
		//
		// The frame straight after a Rebase is thrown away whole: it is the one that spans the load,
		// and billing the new scene for the seconds it spent building itself is the very thing the
		// re-base is for. Clearing the arrears alone would not do it - they are already nil - because
		// what is owed arrives with the next frame, not before it.
		var elapsed = paused || _rebased ? 0f : Time.RawDelta;
		_rebased = false;

		_owed = MathF.Min( _owed + elapsed, catchUp );

		TicksDue = (int)(_owed / TickSeconds);
		_owed -= TicksDue * TickSeconds;
		Ticks += TicksDue;
	}
}
