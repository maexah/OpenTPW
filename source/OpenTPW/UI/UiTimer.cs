namespace OpenTPW.UI;

/// <summary>
/// A control's timer, on real time (<see cref="Time.WallMilliseconds"/>) - <c>FUN_00661fe5</c>, which
/// <c>FUN_006622d2</c> runs for every timer. Nothing is owed until a whole period has gone since the stamp; then one
/// tick is owed for each whole period gone, and the stamp is left the remainder behind now, so a late frame does not
/// move the timer's phase.
/// <para>
/// While the interface holds its timers none runs, and each is stamped afresh as the hold ends
/// (<c>[0x00faa638]</c>, set by <c>FUN_00662411</c> and cleared by <c>FUN_00662420</c>): see
/// <see cref="WindowStack.HoldsTimers"/>.
/// </para>
/// </summary>
internal sealed class UiTimer( long period )
{
	private long _stamp = Time.WallMilliseconds;
	private bool _held;

	/// <summary>How many ticks are owed now; none while <paramref name="held"/>, and none on the frame a hold ends.</summary>
	public int Owed( bool held )
	{
		var now = Time.WallMilliseconds;

		if ( held )
		{
			_held = true;
			return 0;
		}

		if ( _held )
		{
			_held = false;
			_stamp = now;
			return 0;
		}

		var elapsed = now - _stamp;

		if ( elapsed < period )
			return 0;

		_stamp = now - (elapsed % period);

		return (int)(elapsed / period);
	}
}
