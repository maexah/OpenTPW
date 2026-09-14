namespace OpenTPW;

/// <summary>
/// The park's calendar - what decides which season it is, and how often the weather turns.
///
/// <para>
/// <b>The original has no day counter at all.</b> It keeps an accelerated wall clock - its own source
/// calls it <i>funny time</i> - and reads the date back out of it with FileTimeToSystemTime every world
/// tick (FUN_004f8260). A day advances as an <b>edge</b>: the derived calendar day is compared against
/// the stored one and an event fires when they differ. Nothing accumulates days, which is why there is
/// no "day length" constant anywhere to find.
/// </para>
/// <para>
/// The conversion is <c>funny_seconds = counter * mFunnySecsPerRealSec / 4</c> (FUN_004f8770), where the
/// counter is bumped once per call of FUN_00516380 - the world tick - and the park loop calls that only
/// on every <b>eighth</b> 31ms game tick (<c>TEST byte ptr [0x00877d34],0x7</c> at 0x0054f668, the call
/// at 0x0054f7bb). Eight ticks is 248ms, so the counter runs at about four a second, which is where the
/// otherwise inexplicable divide-by-four comes from.
/// </para>
/// <para>
/// <b>The rate is park data, not a constant.</b> <c>mFunnySecsPerRealSec</c> lives in the world clock's
/// save block and defaults to 15000 (<c>MOV dword ptr [ESI+0x1c],0x3a98</c> at 0x004f7ea9). It has
/// exactly two writers in the whole executable - that default and the save loader - no script opcode, no
/// balance key and no setter, and the clock pointer never escapes into a variable, so nothing else
/// <i>can</i> write it. The one park the game ships does not override it: jungle's Easymode.TPWI holds
/// <c>98 3a 00 00</c> = 15000 in its clock block. So until the clock block is read back off disk, the
/// default is the right answer and <see cref="Rate"/> is where that will arrive.
/// </para>
/// <para>
/// <b>A day is 5.714 seconds, and the missing hundredth is not rounding.</b> 86,400 funny seconds at
/// 15,000 a second is 5.76s <i>nominal</i> - but the counter is fed 248ms per unit while every
/// conversion divides by four as though it were 250ms, so funny time runs about 0.8% fast and nothing
/// anywhere compensates for it. Counting the ticks rather than integrating real seconds is what
/// reproduces that, which is why this counts ticks.
/// </para>
/// <para>
/// What it comes to: a month is about two and a half minutes, a year about half an hour, and
/// <c>Weather.DaysBetweenChanges</c> of 7 is <b>a change of weather roughly every forty seconds</b>.
/// That last figure is why park weather is worth building at all - see <c>ParkWeather</c>.
/// </para>
/// </summary>
public static class GameCalendar
{
	/// <summary>
	/// How many game ticks pass between counter advances: every eighth, from the park loop's own
	/// <c>TEST byte ptr [0x00877d34],0x7</c> at 0x0054f668.
	/// </summary>
	public const int TicksPerAdvance = 8;

	/// <summary>
	/// The divisor in <c>counter * rate / 4</c> (0x004f8792). It is a hard-coded four everywhere the
	/// conversion appears, and it assumes the counter arrives at 250ms - which it does not.
	/// </summary>
	public const int AdvancesPerSecond = 4;

	/// <summary>
	/// Funny seconds per real second - <c>mFunnySecsPerRealSec</c>, the original's own field name,
	/// straight off the constructor's <c>MOV dword ptr [ESI+0x1c],0x3a98</c> at 0x004f7ea9.
	/// </summary>
	public const int DefaultRate = 15000;

	/// <summary>
	/// The most counter advances one frame may run, from <c>CMP EAX,0x3</c> at 0x0054f680.
	///
	/// <para>
	/// The original caps the world clock at three advances per <i>rendered</i> frame, on top of the
	/// tick loop's own catch-up clamp. The effect is one-sided: on a machine that stutters the calendar
	/// loses time and can never gain it, so a slow machine's park runs slow rather than lurching.
	/// </para>
	/// </summary>
	public const int LongestCatchUp = 3;

	/// <summary>
	/// Where the clock starts. This is jungle's saved <c>mFunnyTimeStart</c>, read out of its
	/// Easymode.TPWI clock block, <b>not</b> a proven engine default - nothing traced what the
	/// constructor seeds it with. It only decides which month a park opens in.
	/// </summary>
	public static readonly DateTime Epoch = new( 2000, 1, 1 );

	/// <summary>Funny seconds per real second. Park data; the shipped park uses <see cref="DefaultRate"/>.</summary>
	public static int Rate { get; private set; } = DefaultRate;

	/// <summary>The world-tick counter, bumped once per <see cref="TicksPerAdvance"/> game ticks.</summary>
	public static int Counter { get; private set; }

	/// <summary>The date and time in the park, derived rather than accumulated.</summary>
	public static DateTime Now { get; private set; } = Epoch;

	/// <summary>How many whole days have passed since the park opened.</summary>
	public static int Days { get; private set; }

	/// <summary>
	/// True for the one update on which the calendar day changed.
	///
	/// The original works the same way - it compares the derived day against <c>mDayAtLastUpdate</c>
	/// and fires an event on the difference (FUN_004f8260, day compare at 0x004f8321) - so anything
	/// paced in days watches for this edge rather than counting elapsed time itself.
	/// </summary>
	public static bool DayRolled { get; private set; }

	/// <summary>
	/// Which of the four <c>Seasons[]</c> rows applies, 0 to 3.
	///
	/// <para>
	/// The original is <c>wMonth / 3</c> on the <b>raw, 1-based</b> SYSTEMTIME month, signed-divided at
	/// 0x004f87e7 with no bounds check anywhere in FUN_00512e10. So January and February are season 0,
	/// March to May season 1, and so on - and <b>December gives 4, one entry past a four-entry table.</b>
	/// </para>
	/// <para>
	/// <b>That out-of-range read is a real defect in the original and is deliberately not reproduced.</b>
	/// Index 4 lands on 0x007854f4, which is the Seasons array's own element-count slot, and then reads
	/// <c>DaysOfWarning</c> and <c>DaysBetweenChanges</c> as though they were a tolerance and a chance -
	/// so December's weather comes from three numbers that are not weather at all. This clamps instead,
	/// which puts December with September to November. See the note in the memory on why filling a
	/// broken spot in the game's own style beats copying the breakage.
	/// </para>
	/// </summary>
	public static int Season => Math.Clamp( Now.Month / 3, 0, 3 );

	private static int _ticksAtStart;
	private static int _dayAtLastUpdate;

	/// <summary>
	/// Starts the calendar over, as entering a park does - the original zeroes its counter at park
	/// init (<c>MOV dword ptr [EBP + 0x1da70c],EBX</c> at 0x00515865, in the same function that loads
	/// the balance file).
	/// </summary>
	/// <param name="rate">
	/// Funny seconds per real second, for when a park's saved clock block is read. Nothing reads it
	/// yet, so every caller passes the default.
	/// </param>
	public static void Rebase( int rate = DefaultRate )
	{
		Rate = rate < 1 ? DefaultRate : rate;
		Counter = 0;
		Days = 0;
		DayRolled = false;
		Now = Epoch;

		_ticksAtStart = GameClock.Ticks;
		_dayAtLastUpdate = Epoch.Day;
	}

	/// <summary>
	/// Brings the calendar up to whatever <see cref="GameClock"/> has counted, and says whether the day
	/// turned. Driven from <see cref="Level.Update"/> after the clock, and only in a park - the
	/// original's counter is advanced from the park loop and nowhere else, so the lobby has no calendar.
	///
	/// <para>
	/// Because it is derived from <see cref="GameClock.Ticks"/>, a paused park stops the calendar for
	/// free: no ticks come due, so no counter advances and the date cannot move. That is exactly how the
	/// original behaves, and it was worth proving rather than assuming - the park loop reads its "now"
	/// through 0x00402d70, which is a thunk onto the pause-aware accessor FUN_004030d0, and Game_Pause
	/// freezes the very object it reads (<c>MOV ECX,0x785970</c> at both 0x004092c3 and 0x0054f47a).
	/// </para>
	/// </summary>
	public static void Update()
	{
		var wanted = (GameClock.Ticks - _ticksAtStart) / TicksPerAdvance;
		var behind = wanted - Counter;

		if ( behind <= 0 )
		{
			DayRolled = false;
			return;
		}

		Counter += Math.Min( behind, LongestCatchUp );

		// counter * rate / 4, in whole seconds and in that order, so the truncation falls where the
		// original's 64-bit multiply-then-divide puts it.
		var seconds = (long)Counter * Rate / AdvancesPerSecond;

		Now = Epoch.AddSeconds( seconds );

		DayRolled = Now.Day != _dayAtLastUpdate;

		if ( !DayRolled )
			return;

		_dayAtLastUpdate = Now.Day;
		Days++;
	}
}
