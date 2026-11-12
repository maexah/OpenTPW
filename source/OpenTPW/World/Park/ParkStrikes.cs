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
/// The three posts, message <c>0x17</c> with the kind and a warning, a strike or a calling-off, go to
/// <see cref="Post"/>, which the park hands to the advisor (<see cref="ParkState.HearStrike"/>).
/// <b>Counted, not built:</b> the handymen's own cause, the share of the park's cells with their dword set, which
/// nothing here reads.
/// </para>
/// </summary>
public sealed class ParkStrikes
{
	/// <summary>How many kinds of staff keep a record.</summary>
	public const int Kinds = ParkWorld.StaffHqState.Kinds;

	/// <summary>The months since the park's first sweep before which no kind is considered - the <c>0x18</c> of <c>0x00508fbb</c>.</summary>
	public const int MonthsBeforeAnyStrike = 24;

	/// <summary>A month as the look measures it: thirty days in seconds, the span of the divisor <c>0x1792F8648000</c>, which is in 100-nanosecond units.</summary>
	public const long SecondsAMonth = 30L * 24 * 60 * 60;

	/// <summary>Message <c>0x17</c>'s second word: a warning, a strike, a dispute called off (<c>FUN_00508f70</c>; 1 is never sent).</summary>
	public const int Warning = 0, Strike = 2, CalledOff = 3;

	/// <summary>The level a dispute stops rising at.</summary>
	public const int TopLevel = 4;

	/// <summary>A kind needs more than this many members for their fatigue or unhappiness to be a cause.</summary>
	public const int MembersBeforeACause = 3;

	/// <summary>The average rest or happiness byte under which a kind has a cause.</summary>
	public const int CauseBelow = 15;

	private readonly int[] _level = new int[Kinds];
	private readonly bool[] _onStrike = new bool[Kinds];
	private readonly int[] _stamp = new int[Kinds];

	private int _force;

	/// <summary>
	/// Message <c>0x17</c>, posted by <c>FUN_00508f70</c> (<c>FUN_0040f6f0</c>, <c>FUN_0040fb10</c>): the kind, then
	/// <see cref="Warning"/>, <see cref="Strike"/> or <see cref="CalledOff"/>, then the tick of the look. Its one
	/// receiver in the original is the advisor (<c>FUN_00599e70</c> joins set <c>0x17</c>); null hears nothing.
	/// </summary>
	public Action<int, int, int>? Post { get; init; }

	/// <summary>
	/// <c>mForceStrike</c>: set, the month's look skips its gate and every kind has a cause. Only a save sets it
	/// in the original; here the console's <c>forcestrike</c> does too, to 1, and a set word keeps its value.
	/// </summary>
	public bool Force
	{
		get => _force != 0;
		set => _force = value ? (_force != 0 ? _force : 1) : 0;
	}

	/// <summary><c>mForceStrike</c> as the save holds it, for the writer.</summary>
	public int ForceWord => _force;

	/// <summary>
	/// The five records as the save holds them, {level, flag, stamp} a kind, for the writer. A flag is kept as 0 or 1,
	/// the only values the game writes (<c>0x005090b2</c>, <c>0x005099a7</c>); the original copies the block raw.
	/// </summary>
	public IReadOnlyList<ParkWorld.StrikeRecord> Records
		=> [.. Enumerable.Range( 0, Kinds )
			.Select( kind => new ParkWorld.StrikeRecord( _level[kind], _onStrike[kind] ? 1 : 0, _stamp[kind] ) )];

	/// <param name="hq">The save's staff HQ, or null for a record of noughts, as the constructor leaves it (<c>FUN_005089c0</c>).</param>
	public ParkStrikes( ParkWorld.StaffHqState? hq = null )
	{
		if ( hq is not { } saved )
			return;

		_force = saved.ForceStrike;

		for ( var kind = 0; kind < Kinds && kind < (saved.Strikes?.Count ?? 0); ++kind )
		{
			_level[kind] = saved.Strikes![kind].Level;
			_onStrike[kind] = saved.Strikes[kind].OnStrike != 0;
			_stamp[kind] = saved.Strikes[kind].Stamp;
		}
	}

	/// <summary>How far a kind's dispute has gone, 0 to <see cref="TopLevel"/>.</summary>
	/// Dead by CODE: only ParkStrikeTests calls it.
	public int LevelOf( int kind ) => InRange( kind ) ? _level[kind] : 0;

	/// <summary>The <c>mGameTick</c> of the month's change a kind was last looked at on.</summary>
	/// Dead by CODE: only ParkStrikeTests calls it.
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
	/// considered (<see cref="Consider"/>). So a strike ends at the first later look to pass that return.
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
			if ( _level[kind] == 1 )
				Send( kind, CalledOff, tick );

			_level[kind] = 0;
			_onStrike[kind] = false;

			return;
		}

		if ( _level[kind] == 0 )
		{
			_level[kind] = 1;

			Send( kind, Warning, tick );

			return;
		}

		// A level outside 0 to 4 falls out of the original's switch with nothing done.
		if ( _level[kind] is < 0 or > TopLevel )
			return;

		_level[kind] = Math.Min( _level[kind] + 1, TopLevel );
		_onStrike[kind] = true;

		Send( kind, Strike, tick );
	}

	private void Send( int kind, int what, int tick )
	{
		Log.Info( $"Strikes: message 0x17 posted for kind {kind}, {what switch { Warning => "a warning", Strike => "a strike", _ => "a calling-off" }} ({what}), on mGameTick {tick}" );

		Post?.Invoke( kind, what, tick );
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
		=> $"force {_force} recs ["
			+ string.Join( ", ", Enumerable.Range( 0, Kinds )
				.Select( kind => $"({_level[kind]}, {(_onStrike[kind] ? 1 : 0)}, {_stamp[kind]})" ) )
			+ "]";
}
