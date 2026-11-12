namespace OpenTPW;

/// <summary>
/// The park's loop: one pass over the 31 ms ticks a frame owes (<see cref="GameClock.TicksDue"/>), each tick taken whole by
/// every system before the next begins, in the original's order (<c>0x0054f4bf</c>..<c>0x0054f8da</c>;
/// <c>docs/exe/park-engine.md</c>, "What the 31 ms tick drives"): the particles (<c>0x0054f4f2</c>), the track tick and the
/// scripts (<c>0x0054f55f</c>, <c>0x0054f56b</c>), the sprites on an even tick (<c>0x0054f5fb</c>), and on every eighth the
/// thing sweep (<c>0x0054f7bb</c>), at most <see cref="ParkPeople.SweepsAFrame"/> a frame. So a guest's turn sees what the
/// scripts did in the ticks before it and nothing of the ticks after.
/// </summary>
/// <remarks>
/// Run by <see cref="Level.Update"/> at the top of a park's frame, before the entities' once-a-frame work. The original's
/// flying cars (<c>0x0054f5ef</c>) are not built (Q408), and its every-32nd block, the music's level and the crowd's voice, is
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
	internal static void Run( ParticleSystem? particles, ParkRides? rides, ParkPeople? people )
	{
		if ( rides is { IsDeleted: true } )
			rides = null;

		if ( people is { IsDeleted: true } )
			people = null;

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
				continue;

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
					swept = $", sweep {sweeps} to mGameTick {people.State.GameTick}";
				}
			}

			if ( told )
				Log.Info( $"Park loop: tick {tick}: particles to {particles?.Steps.ToString() ?? "none"}, scripts to {rides?.Scheduler.Tick.ToString() ?? "none"}{swept}" );
		}

		if ( dropped > 0 )
			people!.Dropped( sweeps, dropped );
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
