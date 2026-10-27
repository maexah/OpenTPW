using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace OpenTPW;

/// <summary>
/// Says so, out loud, when the program reaches something it has not built.
///
/// <para>
/// <b>Why this exists.</b> Places that count what they did not do - <c>RideScript.NotImplemented</c>
/// and <c>IgnoredWrites</c>, <c>PeepBehaviour</c>'s <c>UnansweredState</c>, <c>RideEffects.Unknown</c>,
/// <c>RideState.Refused</c> - tell nobody by counting alone. A counter nobody reads is itself a dead
/// path, and it is the kind that hides other dead paths behind it.
/// </para>
/// <para>
/// <b>It reports each distinct thing ONCE.</b> A ride script takes a turn four times a second, and a
/// guest's behaviour as often, so a bare log at these sites would push several hundred identical
/// lines a minute past everything else on the console - which informs nobody and is how a warning
/// earns the right to be ignored. The first time a gap is reached it is announced; after that the
/// repeat is counted and <see cref="Summary"/> can say how often.
/// </para>
/// <para>
/// <b>A repeat takes no lock.</b> The frame's draw reaches some gaps for every person every frame, so the repeat is
/// one dictionary read and one <see cref="Interlocked.Increment(ref int)"/>; only the first sight of a gap, once per
/// kind a session, takes the lock that makes the announcement happen exactly once.
/// </para>
/// </summary>
public static class Unimplemented
{
	/// <summary>How often one gap has been reached: a class so a repeat can count in place, with no write to the dictionary.</summary>
	private sealed class Counter
	{
		public int Times;
	}

	private static readonly ConcurrentDictionary<string, Counter> _seen = new( StringComparer.Ordinal );

	/// <summary>Held by the first sight of a gap and by <see cref="Forget"/>, never by a repeat.</summary>
	private static readonly object _lock = new();

	/// <summary>
	/// Reports that <paramref name="what"/> was reached and is not built. Safe to call from anywhere,
	/// as often as the caller likes.
	/// </summary>
	/// <param name="what">
	/// What was asked for, named well enough to act on: the instruction, the state, the effect type.
	/// "SETOBJPARAM" tells somebody what to build; "unimplemented" does not.
	/// </param>
	public static void Report( string what )
	{
		if ( string.IsNullOrWhiteSpace( what ) )
			return;

		if ( _seen.TryGetValue( what, out var counter ) )
		{
			Interlocked.Increment( ref counter.Times );

			return;
		}

		bool first;

		lock ( _lock )
		{
			first = !_seen.TryGetValue( what, out counter );

			// Another thread may have registered it between the read above and the lock; that one is the first.
			if ( first )
				_seen[what] = counter = new Counter { Times = 1 };
		}

		// Only the first of each kind reaches the console; the rest are counted. See the class remarks.
		if ( first )
			Log?.Warning( $"not implemented: {what} was reached and does nothing" );
		else
			Interlocked.Increment( ref counter!.Times );
	}

	/// <summary>How many times each distinct gap has been reached this session, most-reached first.</summary>
	public static IReadOnlyList<(string What, int Times)> Summary
		=> _seen
			.Select( entry => (What: entry.Key, Times: Volatile.Read( ref entry.Value.Times )) )
			.OrderByDescending( entry => entry.Times )
			.ThenBy( entry => entry.What, StringComparer.Ordinal )
			.ToList();

	/// <summary>Whether anything at all has been reported.</summary>
	public static bool Any => !_seen.IsEmpty;

	/// <summary>
	/// Forgets everything reported so far, so that one test cannot see another's reports and a run can be
	/// measured from a known-empty start.
	/// </summary>
	public static void Forget()
	{
		lock ( _lock )
			_seen.Clear();
	}
}
