namespace OpenTPW;

/// <summary>
/// The twelve variable names every <b>ride</b> script declares, in the order it declares them.
///
/// <para>
/// <b>Measured, not assumed.</b> Bouncy, Monkey, Mumbo, Wateride, Spider and Inca God all declare exactly
/// these twelve first and in this order, then append their own - <c>VAR_SCREAMING</c> on Belly Bounce,
/// <c>VAR_BOATCOUNT</c> on the water ride, <c>VAR_SPACELEFT</c> / <c>VAR_STARTNOW</c> / <c>VAR_COUNT</c>
/// on several. So for a ride script these values really are its indices.
/// </para>
/// <para>
/// <b>But a <c>.RSE</c> is not always a ride script, and those declare none of this set.</b> A ride's
/// archive can hold companions - <c>child.RSE</c> in <c>monkey.wad</c> declares only <c>VAR_TEMP</c>,
/// <c>effects.RSE</c> in <c>mumbo.wad</c> declares <c>VAR_TEMP</c> and <c>VAR_RAND</c>, and
/// <c>EventMap.RSE</c> in <c>wateride.wad</c> declares <c>VAR_EVT0</c> to <c>VAR_EVT9</c> and a
/// <c>VAR_PAR0</c>. A script numbers its variables in the order IT declares them, so treat this as a list
/// of names to look up rather than a layout to index with: use <c>RideScript.IndexOf</c> or the string
/// indexer, as <see cref="ParkRides"/> does for the gate.
/// </para>
/// </summary>
public enum RideVariables : int
{
	VAR_LETMEON,
	VAR_LETMEOFF,
	VAR_CAPACITY,
	VAR_DURATION,
	VAR_BREAKSTAT,
	VAR_ONRIDE,
	VAR_RIDECLOSED,
	VAR_BROKEN,
	VAR_WORN,
	VAR_RUNNING,
	VAR_PAD,
	VAR_PARAM
}
