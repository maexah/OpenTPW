namespace OpenTPW;

/// <summary>
/// The linked arm of SetRandomDest - <c>FUN_004f9490</c>, <c>0x004f95c6</c>..<c>0x004f9916</c>
/// (<c>docs/exe/ride-operation.md</c>, "SetRandomDest", the linked arm): a walk of so many passes over linked
/// cells, which answers the cell a wander is aimed into. Guests and staff share it, as they share the function.
///
/// <para>
/// <b>Each pass stands on one cell and reads that cell's own mask</b>, the person's and then the last one chosen:
/// a slot for each of its four side bits, along that bit's step. Nothing reads a candidate's mask before it is
/// chosen, and no edge is tested; whether the person can get there is the route's question afterwards.
/// </para>
/// <para>
/// <b>The filters each lower the pass's count</b>, which starts as the cell's links: the caller's own (staff
/// who began inside their patrol area lose a slot outside it); from a path cell, a queue or entrance neighbour;
/// on a queue cell, the slot its <c>mDirection</c> names, the count lowered even when that slot was already
/// empty; an exit neighbour. <b>A count below two</b> takes the first slot left in slot order, a step back
/// allowed; <b>two or more</b> start at a draw's low two bits and take the first slot left that does not undo
/// the last step. None left on any pass is the dead end, and the walk so far is dropped.
/// </para>
/// </summary>
internal static class LinkedWander
{
	/// <summary>
	/// The four slots in the original's order, each the bit of the cell being LEFT and that bit's own step:
	/// <c>0x10</c> (0, +1), <c>0x04</c> (+1, 0), <c>0x01</c> (0, -1), <c>0x40</c> (-1, 0). A slot and its
	/// opposite are two apart.
	/// </summary>
	internal static readonly (int Bit, int X, int Y)[] Slots =
		[(0x10, 0, 1), (0x04, 1, 0), (0x01, 0, -1), (0x40, -1, 0)];

	/// <summary>The most passes one call walks, the draw's r % 5 + 1 at its highest (<c>0x004f953b</c>).</summary>
	public const int MostPasses = 5;

	/// <summary>
	/// Walks the passes and answers the cells stepped onto in order, the last of them the one to aim into, or
	/// null at a dead end.
	/// </summary>
	/// <param name="cellAt">The running park's cell at a position.</param>
	/// <param name="passes">The call's draw, r % 5 + 1.</param>
	/// <param name="allowed">The caller's own filter on a slot's cell, run before the others; null passes all.</param>
	/// <remarks>
	/// A last pass that ends on the person's own cell is given one more (<c>0x004f98f5</c>), so the answer is
	/// never the cell they stand on and the cells stepped can number one more than the passes.
	/// </remarks>
	internal static List<(int X, int Y)>? Walk( Func<int, int, ParkWorld.MapCell> cellAt, int x, int y, int passes,
		Random random, Func<int, int, bool>? allowed = null )
	{
		var stepped = new List<(int X, int Y)>( passes + 1 );
		var (atX, atY) = (x, y);
		var reverse = -1;
		var left = passes;

		while ( left-- > 0 )
		{
			var cell = cellAt( atX, atY );
			var count = CellEdge.Links( cell.Neighbours );
			var slots = new (int X, int Y)?[Slots.Length];

			for ( var slot = 0; slot < Slots.Length; ++slot )
			{
				var to = (X: atX + Slots[slot].X, Y: atY + Slots[slot].Y);

				// The original adds the step to the cell id and reads whatever record that names; no shipped
				// mask points off the map, and here such a slot is empty.
				if ( (cell.Neighbours & Slots[slot].Bit) != 0 && ParkState.OnMap( to.X, to.Y ) )
					slots[slot] = to;
			}

			void Drop( int slot )
			{
				slots[slot] = null;
				--count;
			}

			for ( var slot = 0; slot < Slots.Length; ++slot )
			{
				if ( allowed != null && slots[slot] is { } to && !allowed( to.X, to.Y ) )
					Drop( slot );
			}

			if ( cell.Type == CellEdge.Path )
			{
				for ( var slot = 0; slot < Slots.Length; ++slot )
				{
					if ( slots[slot] is { } to && CellEdge.IsQueue( cellAt( to.X, to.Y ).Type ) )
						Drop( slot );
				}
			}

			// A queue cell, not an entrance (FUN_00536320 and not FUN_00536340): the slot its mDirection names.
			if ( CellEdge.IsQueue( cell.Type ) && cell.Type != CellEdge.RideEnd )
			{
				var named = Array.FindIndex( Slots, slot => slot.Bit == cell.Direction );

				if ( named >= 0 )
					Drop( named );
			}

			for ( var slot = 0; slot < Slots.Length; ++slot )
			{
				if ( slots[slot] is { } to && cellAt( to.X, to.Y ).Type == CellEdge.RideFarEnd )
					Drop( slot );
			}

			int chosen;

			if ( count < 2 )
			{
				chosen = Array.FindIndex( slots, slot => slot != null );

				if ( chosen < 0 )
					return null;
			}
			else
			{
				var first = random.Next() & (Slots.Length - 1);

				chosen = first;

				for ( var step = 0; step < Slots.Length; ++step )
				{
					var slot = (first + step) & (Slots.Length - 1);

					if ( slots[slot] == null || slot == reverse )
						continue;

					chosen = slot;

					break;
				}

				// With two or more counted and nothing but the way back, the original takes the slot it started
				// at, empty or not (0x004f98a1). A count that high leaves a second slot, so this is never met.
				if ( slots[chosen] == null )
					return null;
			}

			(atX, atY) = slots[chosen]!.Value;
			stepped.Add( (atX, atY) );
			reverse = (chosen + 2) & (Slots.Length - 1);

			if ( left == 0 && (atX, atY) == (x, y) )
				left = 1;
		}

		return stepped;
	}
}
