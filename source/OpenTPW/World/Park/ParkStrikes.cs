using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenTPW;

/// <summary>
/// The staff HQ's strike records and its look at them as each month turns - thing 1's <c>mForceStrike</c> and
/// five <c>mStrikeLevel</c>, and <c>FUN_00508e70</c> past its training, <c>FUN_00508f70</c> and
/// <c>FUN_00509360</c> (<c>docs/exe/ride-operation.md</c>, "The strike").
///
/// <para>
/// A kind is 0 handyman, 1 mechanic, 2 entertainer, 3 guard, 4 researcher (<see cref="ParkStaffPool.KindFor"/>,
/// the original's <c>FUN_00506490</c>). The staff's own side - the walk to the picket and its end - is
/// <see cref="StaffBehaviour"/>'s, which asks <see cref="IsOnStrike"/> and calls <see cref="EndStrike"/>.
/// </para>
/// <para>
/// <b>Counted, not built.</b> The three posts to the advisor (message <c>0x17</c>: a warning, a strike, a
/// calling-off), who does not speak in a park yet, and the handymen's own cause, the share of the park's
/// cells with their dword set, which nothing here reads.
/// </para>
/// </summary>
public sealed class ParkStrikes
{
	/// <summary>How many kinds of staff keep a record.</summary>
	public const int Kinds = ParkWorld.StaffHqState.Kinds;

	/// <summary>The months since the park's first sweep before which no kind is considered - the <c>0x18</c> of <c>0x00508fbb</c>.</summary>
	public const int MonthsBeforeAnyStrike = 24;

	/// <summary>A month as the look measures it: thirty days, the divisor <c>0x1792F8648000</c> in seconds.</summary>
	public const long SecondsAMonth = 30L * 24 * 60 * 60;

	/// <summary>The level a dispute stops rising at.</summary>
	public const int TopLevel = 4;

	/// <summary>A kind needs more than this many members for their fatigue or unhappiness to be a cause.</summary>
	public const int MembersBeforeACause = 3;

	/// <summary>The average rest or happiness byte under which a kind has a cause.</summary>
	public const int CauseBelow = 15;

	private readonly int[] _level = new int[Kinds];
	private readonly bool[] _onStrike = new bool[Kinds];
	private readonly int[] _stamp = new int[Kinds];

	/// <summary>
	/// <c>mForceStrike</c>: set, the month's look skips its gate and every kind has a cause. Only a save sets it
	/// in the original; here the console's <c>strike</c> does too.
	/// </summary>
	public bool Force { get; set; }

	/// <param name="hq">The save's staff HQ, or null for a record of noughts, as the constructor leaves it (<c>FUN_005089c0</c>).</param>
	public ParkStrikes( ParkWorld.StaffHqState? hq = null )
	{
		if ( hq is not { } saved )
			return;

		Force = saved.ForceStrike != 0;

		for ( var kind = 0; kind < Kinds && kind < (saved.Strikes?.Count ?? 0); ++kind )
		{
			_level[kind] = saved.Strikes![kind].Level;
			_onStrike[kind] = saved.Strikes[kind].OnStrike != 0;
			_stamp[kind] = saved.Strikes[kind].Stamp;
		}
	}

	/// <summary>How far a kind's dispute has gone, 0 to <see cref="TopLevel"/>.</summary>
	public int LevelOf( int kind ) => InRange( kind ) ? _level[kind] : 0;

	/// <summary>The <c>mGameTick</c> of the month's change a kind was last looked at on.</summary>
	public int StampOf( int kind ) => InRange( kind ) ? _stamp[kind] : 0;

	/// <summary>Whether a kind is on strike - <c>FUN_00509990</c>.</summary>
	public bool IsOnStrike( int kind ) => InRange( kind ) && _onStrike[kind];

	/// <summary>Ends a kind's strike, leaving its level - <c>FUN_005099a0</c>, which a striker's turn calls in a park shut and empty.</summary>
	public void EndStrike( int kind )
	{
		if ( !InRange( kind ) || !_onStrike[kind] )
			return;

		_onStrike[kind] = false;

		Log.Info( $"Strikes: kind {kind}'s strike is ended by a park shut and empty, level {_level[kind]}" );
	}

	private static bool InRange( int kind ) => kind is >= 0 and < Kinds;

	/// <summary>
	/// Whole thirty-day months since <c>mGameTick</c> nought - <c>FUN_004f8800</c> over <c>0x1792F8648000</c>: the
	/// tick times <see cref="GameCalendar.Rate"/> over four, in whole seconds, whatever date the calendar began on.
	/// </summary>
	public static int MonthsSinceTheFirstSweep( int tick )
		=> (int)((long)(uint)tick * GameCalendar.Rate / GameCalendar.AdvancesPerSecond / SecondsAMonth);

	/// <summary>
	/// The month's look - <c>FUN_00508e70</c> after its training. Unless <see cref="Force"/> is set it returns when
	/// the park is shut or any guest is inside it. Past that, each kind with a member whose stamp is not this
	/// tick is stamped; one on strike has its strike ended and nothing more, and one not on strike is
	/// considered (<see cref="Consider"/>). So a strike lasts a month.
	/// </summary>
	/// <param name="guestsInside">
	/// The guests on a counting cell (<c>FUN_004fa990</c>). The original reads the analyser's count, which may be
	/// up to five of its timer's units old (<c>FUN_004c7fa0( 5 )</c>); this one is taken as the month turns.
	/// </param>
	/// <param name="staff">Every member of staff in the park.</param>
	public void Look( int tick, bool parkIsClosed, int guestsInside, IReadOnlyCollection<Staff> staff )
	{
		ArgumentNullException.ThrowIfNull( staff );

		if ( !Force && (parkIsClosed || guestsInside != 0) )
		{
			Log.Info( $"Strikes: not considered on mGameTick {tick}, the park {(parkIsClosed ? "shut" : "open")} "
				+ $"with {guestsInside} guests inside" );

			return;
		}

		for ( var kind = 0; kind < Kinds; ++kind )
		{
			var members = staff.Where( member => ParkStaffPool.KindFor( member.Model ) == kind ).ToArray();

			if ( members.Length == 0 || _stamp[kind] == tick )
				continue;

			_stamp[kind] = tick;

			if ( _onStrike[kind] )
				_onStrike[kind] = false;
			else
				Consider( kind, tick, members );

			Log.Info( $"Strikes: kind {kind} looked at on mGameTick {tick}: level {_level[kind]} "
				+ $"flag {(_onStrike[kind] ? 1 : 0)}" );
		}
	}

	/// <summary>
	/// One kind's look - <c>FUN_00508f70</c>. Nothing under <see cref="MonthsBeforeAnyStrike"/> months. With a cause
	/// (<see cref="HasCause"/>): level 0 becomes 1 and a warning is posted; 1, 2 and 3 go one up, 4 stays, each
	/// with the kind on strike and a strike posted. With none: a level of exactly 1 posts a calling-off, and the
	/// level and the strike are both cleared.
	/// </summary>
	private void Consider( int kind, int tick, IReadOnlyCollection<Staff> members )
	{
		if ( MonthsSinceTheFirstSweep( tick ) < MonthsBeforeAnyStrike )
			return;

		if ( !HasCause( kind, members ) )
		{
			// Message 0x17 with (kind, 3), which the advisor alone hears (FUN_0059afc0).
			if ( _level[kind] == 1 )
				Unimplemented.Report( "STRIKE_CALLED_OFF_TO_THE_ADVISOR" );

			_level[kind] = 0;
			_onStrike[kind] = false;

			return;
		}

		if ( _level[kind] == 0 )
		{
			_level[kind] = 1;

			// Message 0x17 with (kind, 0).
			Unimplemented.Report( "STRIKE_WARNING_TO_THE_ADVISOR" );

			return;
		}

		// A level outside 0 to 4 falls out of the original's switch with nothing done.
		if ( _level[kind] is < 0 or > TopLevel )
			return;

		_level[kind] = Math.Min( _level[kind] + 1, TopLevel );
		_onStrike[kind] = true;

		// Message 0x17 with (kind, 2).
		Unimplemented.Report( "STRIKE_TO_THE_ADVISOR" );
	}

	/// <summary>
	/// Whether a kind has cause to strike - <c>FUN_00509360</c>: <see cref="Force"/>; else more than
	/// <see cref="MembersBeforeACause"/> members whose rest bytes average under <see cref="CauseBelow"/> by
	/// unsigned division, or whose happiness bytes do.
	/// </summary>
	/// <remarks>
	/// The handymen have a cause of their own, asked first: the analyser's count of cells with their dword set
	/// over its count of cells of kind 1, 3 or 9, above 0.2 (<c>0x005093c2</c>). What the dword is is not read;
	/// it is counted here and answers as no cause.
	/// </remarks>
	private bool HasCause( int kind, IReadOnlyCollection<Staff> members )
	{
		if ( Force )
			return true;

		if ( kind == 0 )
			Unimplemented.Report( "STRIKE_HANDYMEN_CELL_RATIO" );

		if ( members.Count <= MembersBeforeACause )
			return false;

		var rest = (uint)members.Sum( member => (byte)(int)member.Tiredness );
		var happiness = (uint)members.Sum( member => (byte)(int)member.Happiness );
		var count = (uint)members.Count;

		if ( rest / count < CauseBelow )
		{
			Log.Info( $"Strikes: kind {kind} is striking through fatigue" );

			return true;
		}

		if ( happiness / count < CauseBelow )
		{
			Log.Info( $"Strikes: kind {kind} is striking through unhappiness" );

			return true;
		}

		return false;
	}

	/// <summary>The force and the five records, {level, flag, stamp} a kind, for the console's <c>strikes</c>.</summary>
	public string Describe()
		=> $"force {(Force ? 1 : 0)} recs ["
			+ string.Join( ", ", Enumerable.Range( 0, Kinds )
				.Select( kind => $"({_level[kind]}, {(_onStrike[kind] ? 1 : 0)}, {_stamp[kind]})" ) )
			+ "]";
}
