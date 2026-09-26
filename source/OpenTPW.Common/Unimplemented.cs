using System;
using System.Collections.Generic;
using System.Linq;

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
/// </summary>
public static class Unimplemented
{
	private static readonly Dictionary<string, int> _seen = new( StringComparer.Ordinal );

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

		bool first;

		lock ( _lock )
		{
			first = !_seen.TryGetValue( what, out var count );

			_seen[what] = first ? 1 : count + 1;
		}

		// Only the first of each kind reaches the console; the rest are counted. See the class remarks.
		if ( first )
			Log?.Warning( $"not implemented: {what} was reached and does nothing" );
	}

	/// <summary>How many times each distinct gap has been reached this session, most-reached first.</summary>
	public static IReadOnlyList<(string What, int Times)> Summary
	{
		get
		{
			lock ( _lock )
			{
				return _seen
					.Select( entry => (entry.Key, entry.Value) )
					.OrderByDescending( entry => entry.Value )
					.ThenBy( entry => entry.Key, StringComparer.Ordinal )
					.ToList();
			}
		}
	}

	/// <summary>Whether anything at all has been reported.</summary>
	public static bool Any
	{
		get
		{
			lock ( _lock )
				return _seen.Count > 0;
		}
	}

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
