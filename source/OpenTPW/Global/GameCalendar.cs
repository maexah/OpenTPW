namespace OpenTPW;

/// <summary>
/// The park's calendar - what decides which season it is, and how often the weather turns.
///
/// <para>
/// <b>The original has no day counter at all.</b> It keeps an accelerated wall clock - its own source
/// calls it <i>funny time</i> - and reads the date back out of it every world tick. A day advances as
/// an <b>edge</b>: the derived calendar day is compared against the stored one and an event fires when
/// they differ. Nothing accumulates days. See <c>docs/exe/weather.md</c>, the section on the calendar.
/// </para>
/// <para>
/// The conversion is <c>funny_seconds = mGameTick * mFunnySecsPerRealSec / 4</c>, where <c>mGameTick</c> is the park's
/// own clock (<see cref="ParkState.GameTick"/>), one up a thing sweep, and the park loop runs a sweep only on every
/// <b>eighth</b> 31ms game tick. Eight ticks is 248ms, so the clock runs at about four a second.
/// </para>
/// <para>
/// <b>The rate is park data, not a constant.</b> <c>mFunnySecsPerRealSec</c> lives in the world clock's
/// save block and defaults to 15000. The one park the game ships does not override it: jungle's
/// Easymode.TPWI holds 15000 in its clock block. <see cref="Rate"/> is where a park's own value arrives.
/// </para>
/// <para>
/// <b>A day is 5.714 seconds.</b> 86,400 funny seconds at 15,000 a second is 5.76s <i>nominal</i> - but
/// the counter is fed 248ms per unit while every conversion divides by four as though it were 250ms, so
/// funny time runs about 0.8% fast and nothing compensates for it. Counting the ticks rather than
/// integrating real seconds is what reproduces that, which is why this counts ticks.
/// </para>
/// <para>
/// What it comes to: a month is about three minutes, a year about thirty-five minutes, and
/// <c>Weather.DaysBetweenChanges</c> of 7 is <b>a change of weather roughly every forty seconds</b> -
/// see <c>ParkWeather</c>.
/// </para>
/// </summary>
public static class GameCalendar
{
	/// <summary>
	/// The divisor in <c>mGameTick * rate / 4</c> (0x004f8792). It is a hard-coded four everywhere the
	/// conversion appears, and it assumes a sweep arrives every 250ms - which it does not.
	/// </summary>
	public const int AdvancesPerSecond = 4;

	/// <summary>
	/// Funny seconds per real second - <c>mFunnySecsPerRealSec</c>, the original's own field name,
	/// straight off the constructor's <c>MOV dword ptr [ESI+0x1c],0x3a98</c> at 0x004f7ea9.
	/// </summary>
	public const int DefaultRate = 15000;

	/// <summary>
	/// Where the clock starts, <c>mFunnyTimeStart</c>: 2000-01-01 00:00, what the clock constructor seeds it with
	/// (<c>FUN_004f7e80</c>, <c>0x004f7ea0</c>, through <c>SystemTimeToFileTime</c>) and what jungle's
	/// Easymode.TPWI clock block holds. It decides which month a park opens in, and every thing's age is measured
	/// from it (<see cref="ParkState.CalendarNow"/>).
	/// </summary>
	public static readonly DateTime Epoch = new( 2000, 1, 1 );

	/// <summary>Funny seconds per real second: the loaded park's own, <see cref="DefaultRate"/> in a park with no file.</summary>
	public static int Rate { get; private set; } = DefaultRate;

	/// <summary>The park's <c>mGameTick</c> as the calendar last read it (<see cref="Update"/>).</summary>
	public static int Counter { get; private set; }

	/// <summary>
	/// The date and time in the park, the published calendar at <c>0x007ced58</c>: <see cref="Epoch"/> plus
	/// <see cref="Counter"/> × <see cref="Rate"/> / 4 whole seconds (<c>Calendar_Update</c>, <c>0x004f8281</c>..<c>0x004f82f6</c>).
	/// </summary>
	public static DateTime Now { get; private set; } = Epoch;

	/// <summary>How many days have turned since the scene began.</summary>
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
	/// True for the one update on which the calendar month changed - the calendar's second compare, made on its own
	/// rather than inside the day's, which sends message <c>0xc</c> (FUN_004f8260, <c>0x004f83b9</c>).
	/// </summary>
	public static bool MonthRolled { get; private set; }

	/// <summary>
	/// True for the one update on which the calendar year changed - the third compare, message <c>0xd</c>
	/// (FUN_004f8260, <c>0x004f84d0</c>).
	/// </summary>
	public static bool YearRolled { get; private set; }

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
	/// which puts December with September to November. See AGENTS.md rule 11 on why filling a
	/// broken spot in the game's own style beats copying the breakage.
	/// </para>
	/// </summary>
	public static int Season => Math.Clamp( Now.Month / 3, 0, 3 );

	// The clock's mMonthAtLastUpdate (1-based here), mDayAtLastUpdate and the year at +0x18.
	private static int _dayAtLastUpdate;
	private static int _monthAtLastUpdate;
	private static int _yearAtLastUpdate;

	// The running park's year, kept for the next park's Rebase when that park is loaded in place (CarryTheYear).
	private static int? _carriedYear;

	/// <summary>
	/// Puts the calendar on a park's clock as the park is entered: <paramref name="gameTick"/> is the park's
	/// <c>mGameTick</c>, nought with no file and the file's own after a load (755 in Lost Kingdom's Easymode.TPWI).
	///
	/// <para>
	/// The compares start where the original's do. Park init seeds the month and the year from tick nought
	/// (<c>FUN_004f85a0</c>, called at <c>0x00515886</c> just after <c>mGameTick</c> is zeroed at 0x00515865), and the
	/// constructor leaves the day at -1 (<c>0x004f7ebd</c>), so a park with no file turns a day on its first sweep. A
	/// load then reads the file's month and day over them (<c>Calendar_Serialise</c>, <c>0x004f7f30</c>) and its rate,
	/// but not the year, so a park saved in another year turns a year on its first sweep. A park loaded over a running
	/// park keeps that park's year (<see cref="CarryTheYear"/>).
	/// </para>
	/// </summary>
	/// <param name="saved">The loaded file's clock block, or null for a park with no file.</param>
	public static void Rebase( int gameTick = 0, ParkWorld.CalendarBlock? saved = null )
	{
		// A deviation: a file rate under 1, which none of the game's files holds, is taken as 15000; the original
		// multiplies by the file's rate as it lies (0x004f8288), so a rate of nought would hold its date.
		Rate = saved is { FunnySecsPerRealSec: >= 1 } ? saved.FunnySecsPerRealSec : DefaultRate;
		Counter = gameTick;
		Now = DateOf( gameTick );
		Days = 0;
		DayRolled = false;
		MonthRolled = false;
		YearRolled = false;

		var seed = DateOf( 0 );
		_monthAtLastUpdate = saved is null ? seed.Month : saved.MonthAtLastUpdate + 1;
		_dayAtLastUpdate = saved?.DayAtLastUpdate ?? -1;
		_yearAtLastUpdate = _carriedYear ?? seed.Year;
		_carriedYear = null;
	}

	/// <summary>
	/// Keeps the running park's year for the park about to be built over it - Load Park's row, Alt+L, Restart Park -
	/// which the original loads into the running park, whose year at <c>+0x18</c> no load writes. Spent by the next
	/// <see cref="Rebase"/>.
	/// </summary>
	public static void CarryTheYear() => _carriedYear = _yearAtLastUpdate;

	/// <summary>
	/// Reads the date of the park's clock and sets the day's, the month's and the year's edges, as
	/// <c>Calendar_Update</c> (<c>0x004f8260</c>) does. Driven from <see cref="Level.Update"/> once a frame, after the
	/// frame's sweeps, and only in a park, so the lobby has no calendar. A frame with no sweep compares nothing; a paused
	/// park sweeps nothing, so its date stands. The sweeps dropped past three a frame
	/// (<see cref="ParkPeople.SweepsAFrame"/>) are dropped from the date with them, as the original's are.
	/// <para>
	/// A deviation: the original compares at the tail of every sweep, so a frame of two or three sweeps whose day turns
	/// before the last turns it there; here it turns once the last has run (<see cref="Level.Update"/> says why).
	/// </para>
	/// </summary>
	public static void Update( int gameTick )
	{
		DayRolled = false;
		MonthRolled = false;
		YearRolled = false;

		if ( gameTick == Counter )
			return;

		Counter = gameTick;
		Now = DateOf( gameTick );

		DayRolled = Now.Day != _dayAtLastUpdate;
		MonthRolled = Now.Month != _monthAtLastUpdate;
		YearRolled = Now.Year != _yearAtLastUpdate;

		_dayAtLastUpdate = Now.Day;
		_monthAtLastUpdate = Now.Month;
		_yearAtLastUpdate = Now.Year;

		if ( DayRolled )
			Days++;
	}

	/// <summary>
	/// The date of a park tick: <c>mFunnyTimeStart</c> plus <c>tick × rate / 4</c> whole seconds, the tick taken unsigned and
	/// the truncation where the original's 64-bit multiply-then-divide puts it.
	/// </summary>
	private static DateTime DateOf( int gameTick )
		=> Epoch.AddSeconds( (long)(uint)gameTick * Rate / AdvancesPerSecond );
}
