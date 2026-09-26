namespace OpenTPW;

/// <summary>
/// The three things that draw a park's cells, laid again together after a cell has changed.
///
/// <para>
/// <b>They divide the map between them and the division is by cell, so they have to be rebuilt as
/// one.</b> <see cref="ParkGround"/> draws every cell nothing else owns and asks
/// <see cref="ParkPaths.IsPath"/>, <see cref="ParkQueues.IsQueue"/> and
/// <see cref="ParkObjects.CoversGround"/> which ones to leave alone. Rebuild the ground on its own and
/// it stops drawing grass on a cell the moment the overlay calls it a path - while the paths, built
/// once at load, still draw nothing there. <b>The result is a hole in the park exactly where the new
/// walkway belongs.</b>
/// </para>
/// <para>
/// <b>One statement of it, for the reason <see cref="ParkPaths.IsPath"/> is shared rather than
/// repeated:</b> a cell belongs to whatever draws it, and two call sites that can be updated
/// separately are two that can disagree.
/// </para>
/// <para>
/// <b>The order is the order they are first built in</b> (<see cref="Level"/>): the ground reads the
/// heightfield out of <c>base.MD2</c> and publishes it, and both the paths and the queues stand on
/// that same grid by asking <see cref="ParkGround.Current"/> for it. The heightfield outlives a
/// rebuild, so the order matters less than it did at load - but keeping it means a rebuild and a load
/// cannot diverge.
/// </para>
/// </summary>
public static class ParkSurfaces
{
	/// <summary>
	/// Lays the ground, the paths and the queues again. Safe to call outside a park, where all three
	/// are null and nothing happens.
	/// </summary>
	/// <remarks>
	/// <b>Whole surfaces, not cells.</b> Each of the three is one model - or, for a queue, one model a
	/// cell - so there is no per-cell edit to make; see <see cref="ParkGround.Rebuild"/>, which carries
	/// the argument for why eight thousand cells is cheap enough on the rare frame a player builds
	/// something. <b>That argument is about a discrete edit and does not extend to a dragged run:</b>
	/// a rebuild re-reads <c>base.MD2</c>, so calling this once a cell along a drag would re-parse the
	/// model once a cell. A drag has to commit its whole run and then call this once.
	/// </remarks>
	public static void Rebuild()
	{
		ParkGround.Current?.Rebuild();
		ParkPaths.Current?.Rebuild();
		ParkQueues.Current?.Rebuild();
	}
}
