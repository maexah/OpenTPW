namespace OpenTPW;

/// <summary>
/// A park's weather: what it is doing, how often it turns, and the storm it works up to.
///
/// <para>
/// <b>In the original this is a thing, not a subsystem.</b> FUN_0050b360 is the thing-update
/// dispatcher, switching on the type byte at (thing+2), and <b>case 0x0f is weather</b> - so the
/// weather is a model-15 thing in the park's own thing list, 104 bytes of it, updated by
/// FUN_00512880 like any shop or guest. The park save's header field <c>mWeather</c> is that thing's
/// <i>handle</i>, sitting among mMechanicHQ, mResearchLab and mStaffHQ, and not a quality value.
/// </para>
/// <para>
/// <b>Everything follows from one number.</b> <see cref="Quality"/> is 1 to 100 and FUN_00512f00 is
/// the only thing that converts it: a quality inside <c>QualityForRain*</c> makes rain, inside
/// <c>QualityForLightning*</c> arms the lightning, and inside <c>QualityForSnow*</c> would make snow.
/// <b>It is inverted</b> - low quality is bad weather - so the shipped rain band of 0..40 means a
/// quality of 0 is the heaviest rain the game can produce and 40 is the lightest.
/// </para>
/// <para>
/// <b>The beat is a weather tick, not a frame.</b> FUN_00512880 is reached through FUN_00516380,
/// which the park loop gates to every eighth 31ms game tick (0x0054f668) - so weather updates about
/// four times a second, and every countdown below is counted in those. <see cref="GameCalendar"/>
/// advances its counter on exactly that gate, so this takes its beat from there rather than counting
/// ticks a second time.
/// </para>
/// <para>
/// Engine and content: which numbers a theme's balance file gives, and what a park does with them, is
/// content. The sprite pools and the bolt that will draw it are engine - see <c>WeatherSprites</c> -
/// and the sounds go through <see cref="ParkAudio"/> exactly as the lobby's thunder goes through
/// <c>LobbyAudio</c>.
/// </para>
/// <para>
/// <b>Nothing draws yet.</b> <see cref="Drops"/> and <see cref="LastStrike"/> are the whole of what a
/// renderer needs, and they are kept correct here so that sight can follow sound without this
/// changing. The storm is audible in the meantime, which is a real thing to check rather than a
/// placeholder.
/// </para>
/// </summary>
public sealed class ParkWeather : Entity
{
	/// <summary>The one park weather system. There is only ever one, and none in the lobby.</summary>
	internal static ParkWeather? Current { get; private set; }

	/// <summary>
	/// The quality the park's weather is at, 1 to 100, low being bad. Clamped into that range by
	/// FUN_00512f00 before anything reads it, which is also why the snow band of -1/-1 can never be
	/// entered.
	/// </summary>
	public int Quality { get; private set; }

	/// <summary>How many raindrops should be falling, which is what a renderer wants.</summary>
	public int Drops { get; private set; }

	/// <summary>Whether the quality is inside the band that arms lightning.</summary>
	public bool Lightning { get; private set; }

	/// <summary>
	/// Whether what is falling is snow rather than rain. Only hallow can ever set this - its band is
	/// 0..25 where the other three themes disable snow with -1/-1 - and a renderer needs it, because
	/// the two take different art and different fall speeds (0.8 against 2.5).
	/// </summary>
	public bool Snowing { get; private set; }

	/// <summary>Where the last bolt came down - its top, which is also where its thunder sounds.</summary>
	public Vector3 LastStrike { get; private set; }

	/// <summary>The foot of that same bolt, on the ground. Kept because drawing one needs both ends.</summary>
	public Vector3 LastStrikeGround { get; private set; }

	/// <summary>Which way the rain is driven, in degrees, and how hard - both from the quality.</summary>
	public float WindDirection { get; private set; }

	public float WindForce { get; private set; }

	/// <summary>The quality that has been forecast but not yet arrived, or -1 for none.</summary>
	public int Forecast { get; private set; } = -1;

	// Seasons[k], the only per-season numbers there are: an average, how far either side of it an
	// ordinary roll may land, and how often the roll is thrown out for a wholly random quality.
	private readonly int[] _average = new int[Seasons];
	private readonly int[] _tolerance = new int[Seasons];
	private readonly int[] _exceptional = new int[Seasons];

	private const int Seasons = 4;

	private readonly int _daysOfWarning;
	private readonly int _daysBetweenChanges;
	private readonly int _speedOfChange;
	private readonly int _rainLow;
	private readonly int _rainHigh;
	private readonly int _snowLow;
	private readonly int _snowHigh;
	private readonly int _lightningLow;
	private readonly int _lightningHigh;
	private readonly int _countdownSpeed;
	private readonly int _countdownFrom;
	private readonly int _thunderDistance;
	private readonly int _thunderRandomiser;
	private readonly int _maxRaindrops;

	private int _drops;
	private int _targetDrops;
	private int _lightningCountdown;
	private int _strikesThisPeriod;
	private int _thunderCountdown;
	private int _lastChangedDay;
	private int _forecastDueDay = -1;
	private int _counterSeen;

	private readonly Random _rng = new();

	/// <summary>
	/// What draws it. Both are engine and both are the lobby's too - see <see cref="Rain"/>, and the
	/// original keeping one particle and bolt object for the whole game.
	///
	/// <para>
	/// <b>They are built with the lobby's own numbers, which are very likely wrong for a park and are
	/// deliberately not guessed at.</b> The lobby's drops live in a box forty-four units across,
	/// centred twenty-six ahead of a camera orbiting islands about seventy units wide; a park is twelve
	/// hundred units across. Rather than invent a park's figures, this wires the pair up as they stand
	/// so they can be looked at, and whatever proves wrong becomes a constructor parameter then.
	/// </para>
	/// </summary>
	private readonly Rain _rain = new();

	private readonly Lightning _lightning = new();

	/// <summary>
	/// How far a bolt may come down from the park's edge, and how far its top leans - straight out of
	/// FUN_00512c50, which picks a ground point anywhere across the map (clamped away from the very
	/// edge) and leans the top by up to fifty either way over a height of three hundred.
	/// </summary>
	private const float BoltHeight = 300f;

	private const float BoltLean = 50f;

	/// <summary>The park is 128 cells square at ten units a cell - see <c>ParkGround</c>.</summary>
	private static float MapExtent => OpenTPW.ParkWorld.MapSize * 10f;

	public ParkWeather()
	{
		Current = this;

		var balance = Level.Current?.Balance;

		// Every one of these is a real key in data/levels/Standard.sam, and the fallbacks are the
		// shipped values rather than invented ones - so a missing balance file gives the game's own
		// weather instead of no weather. The names are not guesses: the .sam schema is compiled into
		// the executable as 0x3c-byte descriptors carrying the key name at +4 (AvgWeatherQuality at
		// 0x00741c8c, Seasons at 0x00741d40 with its count of four, MaxRaindrops at 0x00742178), so
		// these spellings were read out of the binary rather than copied off a text file.
		for ( int season = 0; season < Seasons; ++season )
		{
			_average[season] = balance?.Int( $"Seasons[{season}].AvgWeatherQuality", DefaultAverage[season] ) ?? DefaultAverage[season];
			_tolerance[season] = balance?.Int( $"Seasons[{season}].NormalTolerance", DefaultTolerance[season] ) ?? DefaultTolerance[season];
			_exceptional[season] = balance?.Int( $"Seasons[{season}].ChanceForExceptionalWeather", DefaultExceptional[season] ) ?? DefaultExceptional[season];
		}

		_daysOfWarning = Int( balance, "Weather.DaysOfWarning", 4 );
		_daysBetweenChanges = Int( balance, "Weather.DaysBetweenChanges", 7 );
		_speedOfChange = Int( balance, "Weather.SpeedOfChange", 20 );

		_snowLow = Int( balance, "WeatherEffects.QualityForSnowLow", -1 );
		_snowHigh = Int( balance, "WeatherEffects.QualityForSnowHigh", -1 );
		_rainLow = Int( balance, "WeatherEffects.QualityForRainLow", 0 );
		_rainHigh = Int( balance, "WeatherEffects.QualityForRainHigh", 40 );
		_lightningLow = Int( balance, "WeatherEffects.QualityForLightningLow", 0 );
		_lightningHigh = Int( balance, "WeatherEffects.QualityForLightningHigh", 15 );

		_countdownSpeed = Int( balance, "WeatherEffects.LightningCountdownSpeed", 10 );
		_countdownFrom = Int( balance, "WeatherEffects.LightningCountdownFrom", 500 );
		_thunderDistance = Int( balance, "WeatherEffects.ThunderStartsThisFarAway", 10 );
		_thunderRandomiser = Int( balance, "WeatherEffects.ThunderRandomiser", 2 );
		_maxRaindrops = Int( balance, "WeatherEffects.MaxRaindrops", 600 );

		// The original rolls a quality in the constructor and applies it at once (0x00511fc4), so a
		// park opens with weather already decided rather than with a day of nothing.
		Apply( Roll() );

		// The target, not Drops: the ramp has not run yet, so Drops is still nought here and reporting
		// it would say "dry" however hard it is about to rain.
		Log.Info( $"Park weather: quality {Quality}, heading for {_targetDrops} drops, lightning {Lightning}" );
	}

	/// <summary>
	/// Forces a quality, for DebugConsole. Everything follows from it, so this is the one lever needed.
	///
	/// <para>
	/// <b>It also holds, and that is not cosmetic.</b> Applying a quality without clearing the schedule
	/// leaves the next forecast to land within a cycle - about forty real seconds - and quietly replace
	/// what was forced. The first probe run measured exactly that: it asked for quality 0, and twenty
	/// seconds later was measuring a delivered 32, which gives 120 drops and no lightning. Both of its
	/// failures were that one race. So this re-bases the change schedule and drops any pending forecast,
	/// giving a forced quality a full <c>DaysBetweenChanges</c> to be observed in.
	/// </para>
	/// <para>
	/// The original has a field this may well be for - <c>mOverridden</c> at +0x64, saved and loaded but
	/// never read by anything traced - but <b>what it does is not established</b>, so this is a debug
	/// convenience rather than a claim about it.
	/// </para>
	/// </summary>
	internal void DebugQuality( int quality )
	{
		Apply( quality );

		Forecast = -1;
		_forecastDueDay = -1;
		_lastChangedDay = GameCalendar.Days;
	}

	/// <summary>
	/// Puts a bolt down now rather than waiting on the countdown, for DebugConsole.
	///
	/// <para>
	/// <paramref name="ground"/> aims it. Without one a bolt lands anywhere across the map, as the
	/// original's does - and a park is twelve hundred units across while the camera sees a few hundred
	/// of it, so most strikes are out of frame. That is faithful, and it makes checking that a bolt is
	/// <i>drawn</i> a lottery: the first attempt landed at x 887 with nothing on screen past x 600.
	/// Aiming turns that into one deterministic shot.
	/// </para>
	/// </summary>
	internal void DebugStrike( Vector3? ground = null ) => PlaceBolt( ground );

	/// <summary>
	/// How many bolts have come down since the park opened, for DebugConsole.
	///
	/// Cumulative, where <c>mLightningNumThisPeriod</c> is reset every time the lightning disarms - a
	/// count that goes backwards cannot be measured against. Nothing but the console reads it.
	/// </summary>
	internal int DebugStrikes { get; private set; }

	/// <summary>
	/// The live bolt, so DebugConsole can report what it is doing - the same accessor
	/// <see cref="LobbyWeather"/> has, and for the same reason.
	///
	/// <para>
	/// A strike leaves nothing on screen to measure if it is not being drawn, and a counter that only
	/// says it fired cannot tell a bolt that never started from one that ran and drew nothing.
	/// <c>Flash</c> and <c>DebugOpacity</c> separate those: opacity is set immediately before the quad
	/// is uploaded, so a non-zero one puts the fault downstream of the wiring.
	/// </para>
	/// </summary>
	internal Lightning DebugBolt => _lightning;

	private static readonly int[] DefaultAverage = [75, 80, 90, 50];
	private static readonly int[] DefaultTolerance = [25, 20, 10, 15];
	private static readonly int[] DefaultExceptional = [10, 10, 5, 10];

	private static int Int( ParkBalance? balance, string key, int fallback )
		=> balance?.Int( key, fallback ) ?? fallback;

	protected override void OnDelete()
	{
		ParkAudio.Current?.StopRain();

		if ( Current == this )
			Current = null;
	}

	/// <summary>
	/// Runs one weather tick for each advance the calendar has made, so the weather keeps the
	/// original's four-a-second beat however fast or slow the frames are coming.
	/// </summary>
	protected override void OnUpdate()
	{
		var counter = GameCalendar.Counter;

		while ( _counterSeen < counter )
		{
			_counterSeen++;
			Tick();
		}
	}

	/// <summary>One weather tick - the body of FUN_00512880.</summary>
	private void Tick()
	{
		Deliver();
		Schedule();
		StepRain();
		Strike();
		Thunder();
	}

	/// <summary>
	/// A forecast that has come due is applied and cleared. The original keeps the pending quality in
	/// the thing's own forecast record at +0x20..+0x2c and tests the flag byte at +0x2c first thing.
	/// </summary>
	private void Deliver()
	{
		if ( Forecast < 0 || GameCalendar.Days < _forecastDueDay )
			return;

		Apply( Forecast );

		Forecast = -1;
		_forecastDueDay = -1;
		_lastChangedDay = GameCalendar.Days;
	}

	/// <summary>
	/// Rolls the next quality and publishes it as a forecast, once the park is far enough past the
	/// last change.
	///
	/// <para>
	/// <b>The warning is real and it is why there are two numbers.</b> FUN_00512880 schedules at
	/// <c>lastChanged + (DaysBetweenChanges - DaysOfWarning)</c> - 0x0051295c subtracts one from the
	/// other and clamps at zero - and the thing then applies it <c>DaysOfWarning</c> days later, so the
	/// full cycle comes to <c>DaysBetweenChanges</c>. With the shipped 7 and 4 the weather is decided
	/// three days ahead and arrives four days after that.
	/// </para>
	/// <para>
	/// Whether the original ever showed that warning to the player - an advisor line, a forecast panel -
	/// is <b>not established</b>; only that it schedules this way.
	/// </para>
	/// </summary>
	private void Schedule()
	{
		if ( Forecast >= 0 )
			return;

		var lead = Math.Max( _daysBetweenChanges - _daysOfWarning, 0 );

		if ( GameCalendar.Days < _lastChangedDay + lead )
			return;

		Forecast = Roll();
		_forecastDueDay = GameCalendar.Days + _daysOfWarning;
	}

	/// <summary>
	/// A quality for the season, from FUN_00512e10: usually the season's average give or take its
	/// tolerance, but every so often a wholly random one instead.
	/// </summary>
	private int Roll()
	{
		var season = GameCalendar.Season;

		if ( _rng.Next( 100 ) < _exceptional[season] )
			return _rng.Next( 100 );

		var tolerance = _tolerance[season];

		return tolerance > 0
			? _average[season] + _rng.Next( tolerance * 2 ) - tolerance
			: _average[season];
	}

	/// <summary>
	/// Turns a quality into weather - FUN_00512f00, the only thing in the original that writes the
	/// drop target or the lightning flag.
	///
	/// <para>
	/// <b>Snow is real, and hallow snows.</b> Three of the four themes disable it with a band of -1/-1 -
	/// which is not a hack but the compiled schema's own legal "never", its declared bound being -1..100 -
	/// so for jungle, space and fantasy this branch never runs. <b>Hallow's band is 0..25</b>, and a
	/// quality is clamped to 1..100, so anything from 1 to 25 lands in it.
	/// </para>
	/// <para>
	/// <b>Which is also why the coin flip below is not dead code.</b> Hallow's snow reaches 25 and its
	/// rain starts at 15, so a quality of 15 to 25 qualifies as both, and the original picks between
	/// them at random rather than preferring either.
	/// </para>
	/// </summary>
	private void Apply( int quality )
	{
		Quality = Math.Clamp( quality, 1, 100 );

		var snowing = Quality >= _snowLow && Quality <= _snowHigh;
		var raining = Quality >= _rainLow && Quality <= _rainHigh;

		Lightning = Quality >= _lightningLow && Quality <= _lightningHigh;

		if ( !Lightning )
			_strikesThisPeriod = 0;

		// Both bands could overlap, and the original tosses a coin rather than preferring either. In
		// hallow they do overlap, at a quality of 15 to 25 - see the remarks above.
		if ( snowing && raining )
		{
			if ( _rng.Next( 2 ) == 0 )
				raining = false;
			else
				snowing = false;
		}

		Snowing = snowing;

		if ( snowing )
			_targetDrops = Intensity( Quality, _snowLow, _snowHigh );
		else if ( raining )
			_targetDrops = Intensity( Quality, _rainLow, _rainHigh );
		else
			// Outside every band there is nothing to fall. The original's own branches each write the
			// target, and nothing writes it when neither matches - which leaves the ramp below to take
			// the drops down and stop the sound. Read as "dry", which is the only sense it can have.
			_targetDrops = 0;

		// Wind comes straight off the quality too: the worse it is, the harder it blows, and the
		// direction wanders by up to forty-five degrees a change.
		WindForce = Math.Clamp( (100 - Quality) * 0.01f, 0f, 1f );
		WindDirection = (WindDirection + _rng.Next( 90 ) - 45 + 360f) % 360f;
	}

	/// <summary>
	/// How many drops a quality asks for inside a band. <b>It is inverted</b>: a quality at the bottom
	/// of the band is the heaviest weather and one at the top is the lightest, which is what makes a
	/// low quality bad weather.
	/// </summary>
	private int Intensity( int quality, int low, int high )
	{
		if ( high <= low )
			return 0;

		var intensity = (quality - low) * 100 / (high - low);

		return Math.Clamp( (100 - intensity) * _maxRaindrops / 100, 0, _maxRaindrops );
	}

	/// <summary>
	/// Walks the drops toward what the quality asked for, a <c>SpeedOfChange</c> at a time, and drives
	/// the rain loop's level from where they have got to.
	///
	/// <para>
	/// Within one step of a target it stops moving and, when that target is nothing, stops the rain
	/// outright - which is FUN_00512880's own shape: the near-enough test at 0x00512a5e either falls
	/// through to the stop or skips the level call entirely.
	/// </para>
	/// </summary>
	private void StepRain()
	{
		if ( _targetDrops < 0 )
			return;

		if ( Math.Abs( _drops - _targetDrops ) < _speedOfChange )
		{
			// Already there, and there is somewhere to be: leave it entirely alone, level included.
			if ( _targetDrops != 0 )
				return;

			_drops = 0;
			Drops = 0;

			_rain.Level = 0f;
			ParkAudio.Current?.StopRain();
			return;
		}

		_drops += _drops < _targetDrops ? _speedOfChange : -_speedOfChange;
		_drops = Math.Clamp( _drops, 0, _maxRaindrops );

		Drops = _drops;

		// How hard it is raining, as a fraction of the hardest the balance file allows. The drawn
		// count is the renderer's own business and is NOT the same number - it draws Level of its own
		// pool, which is the lobby's nine hundred. Matching the two exactly is a thing to do once
		// there is a reason to believe the park's pool size, rather than now.
		var level = _drops / (float)_maxRaindrops;

		_rain.Level = level;
		ParkAudio.Current?.SetRainLevel( level );
	}

	/// <summary>
	/// Counts down to the next bolt and puts one somewhere in the park.
	///
	/// <para>
	/// The countdown falls by <c>LightningCountdownSpeed</c> a weather tick from a start of
	/// <c>rand % LightningCountdownFrom</c>, so with the shipped 10 and 500 a storm flashes every
	/// twenty-five weather ticks on average - about six seconds.
	/// </para>
	/// </summary>
	private void Strike()
	{
		if ( !Lightning )
		{
			_strikesThisPeriod = 0;
			return;
		}

		_lightningCountdown -= _countdownSpeed;

		if ( _lightningCountdown >= 0 )
			return;

		PlaceBolt();

		_lightningCountdown = _rng.Next( Math.Max( _countdownFrom, 1 ) );
	}

	/// <summary>
	/// Puts one bolt somewhere in the park and arms its thunder. Split out from the countdown so the
	/// debug console can force a strike without having to arm the weather first.
	/// </summary>
	/// <param name="at">
	/// Where to put its foot, or null to let it fall anywhere as the original does. Only the debug
	/// console passes one - see <see cref="DebugStrike"/>.
	/// </param>
	private void PlaceBolt( Vector3? at = null )
	{
		// A ground point anywhere across the map, and a top leaning up to fifty either way over three
		// hundred of height. The original clamps only the ground point, and only away from the very
		// edge (FUN_00512c50).
		var groundX = Math.Clamp( at?.X ?? (float)_rng.NextDouble() * MapExtent, 1f, MapExtent );
		var groundY = Math.Clamp( at?.Y ?? (float)_rng.NextDouble() * MapExtent, 1f, MapExtent );

		float Lean() => ((float)_rng.NextDouble() * 2f - 1f) * BoltLean;

		// The original is Y-up; this engine is Z-up, so its three hundred of height is a Z here - the
		// same swap the bolt over the lobby already makes.
		LastStrikeGround = new Vector3( groundX, groundY, 0f );
		LastStrike = new Vector3( groundX + Lean(), groundY + Lean(), BoltHeight );

		_lightning.Strike( LastStrikeGround, LastStrike );

		// Thunder follows the flash, and closes in as the storm builds: the delay is the distance less
		// however many bolts this period has already thrown, so the first is about ten weather ticks
		// behind its thunder and later ones follow almost at once.
		if ( _thunderCountdown < 1 )
			_thunderCountdown = _rng.Next( Math.Max( _thunderRandomiser, 1 ) )
				+ Math.Max( _thunderDistance - _strikesThisPeriod, 0 );

		_strikesThisPeriod++;
		DebugStrikes++;
	}

	/// <summary>
	/// Sounds the thunder for a bolt that flashed a while ago - at the bolt's own top, because the
	/// original plays it as a placed sound rather than a flat one.
	/// </summary>
	private void Thunder()
	{
		if ( _thunderCountdown < 1 )
			return;

		_thunderCountdown--;

		if ( _thunderCountdown != 0 )
			return;

		ParkAudio.Current?.Thunder( LastStrike );
	}
}
