namespace OpenTPW;

/// <summary>
/// The game's own clock - the one a park's menu stops.
///
/// <para>
/// This is not <see cref="Time"/>. That clock measures frames and never stops for a menu: the
/// interface, the pointer, the button glints and the camera all run on it, and they have to carry on
/// while the world is held. This one measures how much of the <i>game</i> has happened, and a park's
/// menu, message box, options screen or map stops it dead.
/// </para>
/// <para>
/// <b>Pause is nothing but a frozen clock.</b> Neither of the original's tick loops tests a paused
/// flag. Each reads the clock (a global object at 0x785970), works out how far behind its last step it
/// is, and steps while it is behind - so a clock that stops reporting new time leaves the loop with
/// nothing to do, and everything the tick drives stops together. That is copied here:
/// <see cref="Paused"/> only zeroes <see cref="Delta"/>, and <see cref="TicksDue"/> then comes out zero
/// by itself. The original's fixed-step latch, which is for recorded play, is not reproduced.
/// </para>
/// <para>
/// A pause also does what a clock cannot: it holds the advisor's voice, and it lifts the audio
/// listener 10,000 units up, which silences every placed voice that has a range
/// (<see cref="Audio.HoldPlaced"/>). It does not freeze the interface's own timers: no screen modelled
/// here asks the original for that.
/// </para>
/// <para>
/// <b>Only a park pauses.</b> In the lobby the clock never stops, whatever is open over it; the lobby
/// holds its advisor instead, which is <see cref="Advisor.Paused"/>. The original's pause-permission
/// gate and what keeps the lobby from pausing are in <c>docs/exe/weather.md</c>, "Pause, and why the
/// calendar cannot roll while paused".
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
	///
	/// <para>
	/// <b>A capped frame runs 64 ticks here and 65 in the engine, a deliberate deviation kept by
	/// Alexah's decision.</b> The engine steps while <c>now</c> is past its last stepped time, adding 31
	/// to that (<c>0x0054f4ad</c>, <c>0x0054f4c4</c>), so each tick runs at the START of its 31 ms and
	/// the count is rounded up; <see cref="Update"/> runs one when a whole 31 ms is owed, at its end, and
	/// rounds down. Both re-base to <c>now - cap</c>, so the engine ends each capped frame one tick further
	/// on (the lobby: 17 against 16); matching it would move the phase of
	/// every tick and the interpolation that reads <see cref="PartialTick"/>
	/// (<c>docs/exe/park.md</c>, difference 6).
	/// </para>
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
	/// A park's systems take these ticks in one loop, each tick through every system before the next
	/// (<see cref="ParkLoop"/>, the original's from 0x0054f4bf), so a guest's turn sees what the rides did in
	/// the ticks before it. The lobby's one system, the particles, loops over the count alone.
	/// </para>
	/// </summary>
	public static int TicksDue { get; private set; }

	/// <summary>
	/// Ticks run since the game started, never reset. The original's own count, 0x00877d34, goes up
	/// once per step of the park's loop and is reset to zero on state entry (<c>docs/exe/boot.md</c>).
	/// Reported by the debug console's <c>state</c>, which is how a pause can be checked without looking
	/// at pixels.
	/// </summary>
	public static int Ticks { get; private set; }

	/// <summary>Game time owed but not yet worth a whole tick.</summary>
	private static float _owed;

	/// <summary>
	/// How far through the tick that has not yet come due this frame is, from nought to just under one.
	///
	/// <para>
	/// <b>This is the frame-level remainder, and on its own it is not what anything draws with.</b> The
	/// original interpolates at three different rates off one clock sample - placed objects every 31ms,
	/// particles every 62ms and <b>peeps every 248ms</b> - each dividing by its own reciprocal constant
	/// against its own baseline (<c>0x0054fa0d</c>, <c>0x0054fa38</c>, <c>0x0054fa5c</c>). So a consumer
	/// composes this with its own beat rather than reading it as a fraction of anything; see
	/// <see cref="ParkPeople.ThingTickFraction"/>, which is the 248ms one.
	/// </para>
	/// <para>
	/// A held world leaves this where it stands, because <see cref="Update"/> adds nothing to
	/// <see cref="_owed"/> while paused - so a paused park holds its interpolation still rather than
	/// snapping anybody, which is what pausing should look like.
	/// </para>
	/// </summary>
	public static float PartialTick => _owed / TickSeconds;

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

		var backlog = _owed + elapsed;
		_owed = MathF.Min( backlog, catchUp );

		TicksDue = (int)(_owed / TickSeconds);

		if ( backlog > catchUp )
			Log?.Info( $"clock: a {backlog * 1000f:F0} ms backlog capped at {catchUp * 1000f:F0} ms, {TicksDue} ticks run" );
		_owed -= TicksDue * TickSeconds;
		Ticks += TicksDue;
	}
}
