namespace OpenTPW;

/// <summary>
/// A park as it is being <i>played</i>, which is a different thing from the file it was loaded out of.
///
/// <para>
/// <see cref="ParkWorld"/> describes a <b>file</b> and is deliberately immutable - it says what the save
/// held, and nothing that happens afterwards may change it. This is the other half: the numbers and the
/// cells that a running park moves. It is seeded from the save once, when the park opens, and owned by
/// the <see cref="Level"/>.
/// </para>
/// <para>
/// <b>It exists because four separate features were each waiting on it, not as a tidying.</b> Two
/// workarounds in the tree said so outright: <c>PeepBehaviour.Takings</c> and
/// <c>PeepBehaviour.VisitorsToDate</c> each carried a number that belongs to the park, each documented at
/// its own site as living there only because the park had nowhere to keep it. Both are folded in here.
/// The same gap blocked litter on cells and the admission gate's per-cell occupancy.
/// </para>
/// <para>
/// <b>What is deliberately NOT here.</b> No ride state and no per-object dirt. Those want a ride that
/// operates, and nothing operates one yet - a layer built for a consumer that does not exist is the
/// speculative kind this project does not add. The cells below are the opposite case: they are read by
/// the handyman's litter search, which is built next.
/// </para>
/// </summary>
public sealed class ParkState
{
	/// <summary>
	/// One cell as the running park holds it. Only the three fields anything can change are here: the rest
	/// of a cell - its type, its tile, its neighbours - is scenery the save describes and play does not
	/// move, so it stays on <see cref="ParkWorld.MapCell"/> and is not copied.
	/// </summary>
	public struct RuntimeCell
	{
		/// <summary>How much has been dropped here - <c>mLitter</c>.</summary>
		public int Litter;

		/// <summary>The handyman who has claimed this cell's litter, or nought - <c>mLitterCollector</c>.</summary>
		public ushort LitterCollector;

		/// <summary>The thing standing on this cell - see <see cref="ParkWorld.MapCell.Occupant"/>.</summary>
		public ushort Occupant;
	}

	private readonly RuntimeCell[] _cells;

	/// <summary>
	/// Seeded from the save. A null park gives an empty, open one - the same answer
	/// <see cref="PeepBehaviour"/> already gives for a null park, and for the same reason: a park with
	/// nothing loaded is not a park whose gates are shut.
	/// </summary>
	public ParkState( ParkWorld? park )
	{
		Balance = park?.Economy?.Balance ?? 0;
		VisitorsToDate = park?.NumberOfVisitorsToDate ?? 0;
		ParkIsClosed = park is not null && park.ParkClosed != 0;

		_cells = new RuntimeCell[ParkWorld.MapSize * ParkWorld.MapSize];

		if ( park == null )
			return;

		for ( var i = 0; i < _cells.Length && i < park.Cells.Count; ++i )
		{
			var cell = park.Cells[i];

			_cells[i] = new RuntimeCell
			{
				Litter = cell.Litter,
				LitterCollector = cell.LitterCollector,
				Occupant = cell.Occupant
			};
		}
	}

	/// <summary>
	/// The two facts on their own, for a test that has no park to load - the arrangement
	/// <see cref="PeepBehaviour"/> already has, and it exists for the same reason: the only park that can
	/// be loaded is saved open, so every branch that turns on a shut park would otherwise be unreachable.
	/// </summary>
	public ParkState( bool parkIsClosed, int visitorsToDate, int balance = 0 )
	{
		ParkIsClosed = parkIsClosed;
		VisitorsToDate = visitorsToDate;
		Balance = balance;
		_cells = new RuntimeCell[ParkWorld.MapSize * ParkWorld.MapSize];
	}

	/// <summary>
	/// What the park is worth now - the balance the save was left with, moved by everything since.
	///
	/// <para>
	/// <b>This is one number where it used to be two.</b> The interface added the save's balance to a
	/// running total held on the behaviours, because nothing could move the saved one. It can now, which
	/// is what <see cref="Take"/> does - and that matches the original, where taking a fee adds it
	/// straight onto <c>mBalance</c> (<c>FUN_004d0600</c>).
	/// </para>
	/// </summary>
	public int Balance { get; private set; }

	/// <summary>
	/// What has been taken at the gate since the park opened, kept beside <see cref="Balance"/> rather
	/// than folded into it because it answers a different question - the balance is a position and this is
	/// a flow. The original keeps both too, adding a fee to <c>mBalance</c> and to
	/// <c>mProfitThisYear</c> alike.
	/// </summary>
	public int Takings { get; private set; }

	/// <summary>
	/// How many guests this park has ever admitted, counting on from what the save recorded - the
	/// original's <c>world + 0x1da714</c>, moved in exactly one place, by a guest finishing at the gate.
	/// </summary>
	public int VisitorsToDate { get; private set; }

	/// <summary>Whether the park is shut to visitors, as the save left it - <b>zero is open</b>.</summary>
	public bool ParkIsClosed { get; }

	/// <summary>
	/// Takes an admission fee: onto the balance and onto the running total alike, which is the one place
	/// the two move together.
	/// </summary>
	public void Take( int fee )
	{
		Balance += fee;
		Takings += fee;
	}

	/// <summary>Admits one guest and hands back which visitor they are, counting from one.</summary>
	public int Admit() => ++VisitorsToDate;

	/// <summary>
	/// The runtime cell at a grid position, by reference so a caller can change it in place. Off the map
	/// throws rather than returning a default: a default would be silently writable and the write would
	/// go nowhere, which is the kind of no-op this project has been bitten by.
	/// </summary>
	public ref RuntimeCell CellAt( int x, int y )
	{
		if ( x < 0 || y < 0 || x >= ParkWorld.MapSize || y >= ParkWorld.MapSize )
			throw new ArgumentOutOfRangeException( nameof( x ), $"({x},{y}) is off a {ParkWorld.MapSize} square map" );

		return ref _cells[(y * ParkWorld.MapSize) + x];
	}

	/// <summary>Whether a grid position is on the map at all, for a caller that would rather ask than catch.</summary>
	public static bool OnMap( int x, int y )
		=> x >= 0 && y >= 0 && x < ParkWorld.MapSize && y < ParkWorld.MapSize;

	/// <summary>How many cells hold litter, which is what a park's cleanliness comes to.</summary>
	public int LitteredCells
	{
		get
		{
			var count = 0;

			foreach ( var cell in _cells )
			{
				if ( cell.Litter != 0 )
					++count;
			}

			return count;
		}
	}
}
