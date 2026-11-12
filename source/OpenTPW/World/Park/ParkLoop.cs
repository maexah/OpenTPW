namespace OpenTPW;

/// <summary>
/// The park's loop: one pass over the 31 ms ticks a frame owes (<see cref="GameClock.TicksDue"/>), each tick taken whole by
/// every system before the next begins, in the original's order (<c>0x0054f4bf</c>..<c>0x0054f8da</c>;
/// <c>docs/exe/park-engine.md</c>, "What the 31 ms tick drives"): the particles (<c>0x0054f4f2</c>), the track tick and the
/// scripts (<c>0x0054f55f</c>, <c>0x0054f56b</c>), the sprites on an even tick (<c>0x0054f5fb</c>), and on every eighth the
/// thing sweep (<c>0x0054f7bb</c>), at most <see cref="ParkPeople.SweepsAFrame"/> a frame, each followed by the sweep's tail:
/// the calendar's compare and its turns and the weather's turn. So a guest's turn sees what the scripts did in the ticks before
/// it and nothing of the ticks after, and a day that turns on a frame's second sweep rolls the objects' rings before its third.
/// </summary>
/// <remarks>
/// Run by <see cref="Level.Update"/> at the top of a park's frame, before the entities' once-a-frame work. The original's
/// flying cars (<c>0x0054f5ef</c>) are not built (Q408), its every-eighth reverb handoff is counted (<see cref="HandTheReverb"/>),
/// and its every-32nd block, the music's level and the crowd's voice, is
/// asked once a frame after the loop (<see cref="ParkAudio"/>), so in a frame owing a 32nd tick and a sweep after it the
/// level counts the guests after that later sweep (Q1120).
/// </remarks>
internal static class ParkLoop
{
	/// <summary>
	/// Runs the frame's ticks through the systems given; any may be absent. A tick's number is worked back from
	/// <see cref="GameClock.Ticks"/>, which already counts this frame's ticks, so the first of them is that many less
	/// <see cref="GameClock.TicksDue"/>, plus one.
	/// </summary>
	/// <param name="calendar">The calendar's compare and its turns for the sweep just run (<see cref="Level.TurnTheCalendar"/>).</param>
	/// <param name="weather">The weather, whose turn follows the compare.</param>
	internal static void Run( ParticleSystem? particles, ParkRides? rides, ParkPeople? people, Action<ParkPeople>? calendar = null,
		ParkWeather? weather = null )
	{
		if ( rides is { IsDeleted: true } )
			rides = null;

		if ( people is { IsDeleted: true } )
			people = null;

		if ( weather is { IsDeleted: true } )
			weather = null;

		var ticks = GameClock.TicksDue;
		var first = GameClock.Ticks - ticks + 1;

		// A frame owing two sweeps or more (a hitch) says each of its ticks, so the order can be read from the log.
		var told = people != null && SweepTicksIn( first, ticks ) >= 2;

		if ( told )
			Log.Info( $"Park loop: a frame of {ticks} ticks, {first} to {first + ticks - 1}, mGameTick {people!.State.GameTick}" );

		// The loop's count of the frame's sweeps, [0x00879064] (0x0054f680).
		var sweeps = 0;
		var dropped = 0;

		for ( var i = 0; i < ticks; ++i )
		{
			var tick = first + i;

			particles?.Tick();
			rides?.Tick( i );

			if ( people == null )
			{
				HandTheReverb( tick );
				continue;
			}

			people.StepSprites( tick );

			var swept = "";

			if ( (tick & (ParkPeople.ThingTickEvery - 1)) == 0 )
			{
				// A fourth sweep in one frame is dropped, not owed - see SweepsAFrame.
				if ( sweeps == ParkPeople.SweepsAFrame )
				{
					++dropped;
					swept = ", sweep dropped";
				}
				else
				{
					++sweeps;
					people.Sweep( tick );
					Tail( people, calendar, weather );
					swept = $", sweep {sweeps} to mGameTick {people.State.GameTick}; calendar {GameCalendar.Now:yyyy-MM-dd HH:mm:ss}"
						+ $" on mGameTick {GameCalendar.Counter}, weather turn {weather?.Turns.ToString() ?? "none"}";
				}
			}

			HandTheReverb( tick );

			if ( told )
				Log.Info( $"Park loop: tick {tick}: particles to {particles?.Steps.ToString() ?? "none"}, scripts to {rides?.Scheduler.Tick.ToString() ?? "none"}{swept}" );
		}

		if ( dropped > 0 )
			people!.Dropped( sweeps, dropped );
	}

	/// <summary>
	/// The sweep's tail as it bears on the date: the calendar's compare and the day's, month's and year's turns, then the
	/// golden tickets the sweep owed (<paramref name="calendar"/>; the original's tail <c>FUN_004d7b20</c>, which its sweep
	/// calls last, <c>0x00516695</c>, and which compares at <c>0x004d7b3b</c>), then the weather's turn.
	/// </summary>
	/// <remarks>
	/// The original's weather, thing 7, takes its turn in the sweep's walk, before the tail (<c>FUN_00512880</c> from
	/// <c>FUN_0050b360</c>'s arm <c>0xf</c>), but it reads the date of the sweep's own <c>mGameTick</c> as it goes
	/// (<c>FUN_004f8690</c> at <c>0x005128cd</c> and <c>0x005129be</c>, <c>FUN_004f8770</c> for the season) and hears none of
	/// the compare's messages (it joins sets 1 and 2 alone, <c>0x00511fa9</c>, <c>0x00511fb8</c>). Ours reads the live tick's
	/// calendar for its schedule (<see cref="ParkState.CalendarNow"/>) and the season the compare sets
	/// (<see cref="GameCalendar.Season"/>), so it turns after the compare and sees the same date; and after the advisor's turn, thing 3's, which reads nothing of the weather (docs/exe/weather.md,
	/// "The calendar"). But the original's tail reads the turned weather: its arrival manager sizes a load by this sweep's
	/// drops (<c>0x004cf60e</c>), where ours runs in <see cref="ParkPeople.Sweep"/> before this turn and reads the drops one
	/// turn behind (Q1165). And the original skips the walk, its weather with it, in world state 4, the park's end
	/// (<c>0x0051639e</c>), while the tail still runs; ours turns the weather whatever the state: gate it there when Q404
	/// builds the end.
	/// </remarks>
	private static void Tail( ParkPeople people, Action<ParkPeople>? calendar, ParkWeather? weather )
	{
		calendar?.Invoke( people );
		weather?.Turn();
	}

	/// <summary>
	/// The every-eighth step's reverb handoff, <c>FUN_0055a470</c> (<c>0x0054f828</c>, after the sweep and run on a step whose
	/// sweep was dropped too; <c>docs/exe/audio.md</c>, "The reverb"): the ride the camera is on hands its script's level, or the
	/// level's <c>Sound.Reverb</c>, to the reverb preset holder (<c>FUN_0051c8d0</c>). Reverb is cut (no backend has a slot),
	/// so the handoff is counted, <c>CAMERA_RIDE_REVERB</c>, on each such step while the camera is in a ride's view
	/// (<see cref="ParkRideViewCameraMode.Active"/>, the End's alone in this build). Not counted with no ride under the camera,
	/// though the original then hands <c>Sound.Reverb</c> (0 in Lost Kingdom), as Q864 asks.
	/// </summary>
	internal static void HandTheReverb( int tick )
	{
		if ( (tick & (ParkPeople.ThingTickEvery - 1)) == 0 && ParkRideViewCameraMode.Active )
			Unimplemented.Report( "CAMERA_RIDE_REVERB" );
	}

	/// <summary>How many of the <paramref name="ticks"/> ticks from <paramref name="first"/> fall on the thing sweep's beat.</summary>
	internal static int SweepTicksIn( int first, int ticks )
	{
		var count = 0;

		for ( var tick = first; tick < first + ticks; ++tick )
		{
			if ( (tick & (ParkPeople.ThingTickEvery - 1)) == 0 )
				++count;
		}

		return count;
	}
}
