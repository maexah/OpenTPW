namespace OpenTPW;

/// <summary>
/// What a thing standing on a cell says about someone trying to walk onto it. The original looks for one
/// particular kind of thing and then asks it two questions, so there are three answers rather than two.
/// </summary>
public enum QueueVerdict
{
	/// <summary>No thing of the kind the original looks for is on the cell, so it carries on asking.</summary>
	NothingThere = 0,

	/// <summary>There is one and it does not mind, which ends the test then and there.</summary>
	LetThemThrough = 1,

	/// <summary>There is one and it is in the way.</summary>
	InTheWay = 2
}

/// <summary>
/// Whether one side of one cell is closed - the original's <c>FUN_004d8750</c>, which is the question
/// <see cref="MapStep.CanStep"/> takes as a parameter and which everything that walks depends on.
///
/// <para>
/// <b>Read the disassembly, not the decompiler, if you ever check this.</b> Decompiled, the body is
/// fifteen calls that appear to take no arguments at all, which reads as impossible. They are
/// <c>thiscall</c> accessors and the decompiler drops <c>ECX</c>. Disassembled, <b>two different cells are
/// being asked</b>: <c>EBP</c> is the cell being left and <c>EDI</c> is the cell being entered, and which
/// of the two each question goes to is most of the meaning. Reading it from the decompiled form loses that
/// completely, and an earlier attempt at this function was committed and reverted for exactly that reason.
/// </para>
/// <para>
/// <b>Fourteen of the fifteen are comparisons of the cell's type</b> against a literal, and the types are
/// already read by <see cref="ParkWorld.MapCell"/>. The fifteenth pair reach a cell's <i>track</i> record,
/// which is a second sub-record every cell of the shipped park carries and which the save reader does not
/// yet parse - so that question is taken as a parameter here, the way the whole edge test is a parameter
/// to <see cref="MapStep"/>.
/// </para>
/// <para>
/// <b>The names given to types are readings, and the numbers are the definition.</b> Only two are firmly
/// established: type 1 is path, by drawing it and getting a connected loop with an avenue to the park
/// entrance, and type 4 is a built object's footprint, by its cells landing on the placed objects. The
/// rest are named for what this function does with them, which is evidence about their role and not proof
/// of their name. Over Lost Kingdom the nine types present are 7 (9,077 cells), 0 (6,875), 2 (240),
/// 1 (78), 30 (66), 4 (35), 9 (8), 3 (4) and 10 (1) - every one inside the set of literals compared
/// against here, with nothing left over.
/// </para>
/// <para>
/// <b><c>true</c> means closed.</b> The four map-boundary guards return it, and leaving the map has to be
/// refused, which fixes the sense of the whole function; the caller treats a non-zero answer as "you may
/// not go that way".
/// </para>
/// </summary>
public sealed class CellEdge
{
	/// <summary>A cell with nothing built on it.</summary>
	public const int Nothing = 0;

	/// <summary>Path - the one type identified by drawing it rather than by inference.</summary>
	public const int Path = 1;

	/// <summary>A built object's footprint - identified by its cells landing on the placed objects.</summary>
	public const int Footprint = 4;

	/// <summary>
	/// The two types the original treats as one when it asks "is this a queue". Lost Kingdom has four cells
	/// of type 3, which are the Belly Bounce's queue, and eight of type 9, which sit on ride ends and on
	/// the drinks shop and litter bin.
	/// </summary>
	public static bool IsQueue( int type ) => type is 3 or 9;

	/// <summary>
	/// The three types the original refuses outright, grouped by one accessor. Type 2 is separately
	/// compared straight afterwards, which is redundant and is kept because the original does it.
	/// </summary>
	public static bool IsSolid( int type ) => type is 5 or 7 or 2;

	/// <summary>
	/// The type of the park approach. Lost Kingdom has 66 such cells, and the fourteen of them carrying
	/// <see cref="ApproachFlag"/> run from (47,10) to (48,16) - straight down to the entrance at (47,17)
	/// and (48,17), which is what identifies them.
	/// </summary>
	public const int Approach = 30;

	/// <summary>
	/// The end of a ride, which is the only source type that reaches the facing test at the very end.
	/// </summary>
	public const int RideEnd = 9;

	/// <summary>
	/// The one cell of Lost Kingdom that carries type 10, at the far end of the Belly Bounce.
	///
	/// <para>
	/// <b>The original tests for it in a place it can never be reached</b>, and that is kept rather than
	/// tidied away. By the time the test is made the cell being left is already known to be path or queue,
	/// so it cannot also be this - the earlier branch has returned. It costs nothing to keep and removing
	/// it would quietly make this function something other than the one in the executable.
	/// </para>
	/// </summary>
	public const int RideFarEnd = 10;

	/// <summary>
	/// The bit of a cell's flags the original tests on the stricter mode. Over Lost Kingdom it is set on
	/// fourteen cells, all of type <see cref="Approach"/>, forming the avenue down to the park entrance.
	/// </summary>
	public const int ApproachFlag = 1 << 10;

	/// <summary>
	/// The bit that stands for a side of a cell. <b>The same four constants serve both fields</b> the
	/// original reads - it bit-tests them in <c>mNeighbours</c> and compares them for equality against
	/// <c>mDirection</c> - so there is one table rather than two.
	///
	/// <para>
	/// <b>What the bits mean on the map is deliberately not asserted here.</b> Over the shipped park's
	/// paths, the bit that tracks a neighbour towards row zero is <c>0x01</c>; but every question this
	/// function asks of <c>mNeighbours</c> it asks of the cell being <i>entered</i>, about the side facing
	/// the cell being left, and the constants below are exactly that. Since the mask is symmetric across
	/// every one of the park's 65,024 adjacent pairs, no measurement on this park can tell that reading
	/// apart from its mirror image, and the disassembly is what settles it.
	/// </para>
	/// </summary>
	public static int BitFor( StepDirection direction ) => direction switch
	{
		StepDirection.North => 0x10,
		StepDirection.East => 0x40,
		StepDirection.South => 0x01,
		_ => 0x04
	};

	/// <summary>
	/// Whether a track record hands the question to the one its parent names instead of answering it.
	///
	/// <para>
	/// The name is a reading of what the park contains, not of anything the executable says: all 429 cells
	/// of type 12 name a parent, every one of those parents is a cell of a type in <see cref="TrackCounts"/>,
	/// and each parent is named by exactly three of them. Type 17 is grouped with it by the original and
	/// does not occur in this park at all.
	/// </para>
	/// </summary>
	public static bool TrackDefersToParent( int trackType ) => trackType is 12 or 17;

	/// <summary>
	/// Whether a track record is one this test applies to at all. Of these five only 25 occurs in Lost
	/// Kingdom, on 143 cells, none of which names a parent.
	/// </summary>
	public static bool TrackCounts( int trackType ) => trackType is 11 or 13 or 16 or 18 or 25;

	/// <summary>
	/// The bits of a track record's flags that reopen a cell this would otherwise close. Set on exactly
	/// <b>one</b> cell of the shipped park, which is why the branch closes 568 of the 572 it reaches.
	/// </summary>
	public const int TrackOpenFlags = 0xf;

	/// <summary>
	/// Whether a cell's track record closes it - the original's <c>FUN_005363f0</c> deciding whether the
	/// question applies, <c>FUN_00536440</c> deciding whose answer to take, and <c>FUN_0053ad20</c> giving
	/// it.
	///
	/// <para>
	/// A cell that defers answers by its parent's record on both counts: whether the test applies, and
	/// whether the flags reopen it. A deferring cell that names no parent is not closed, which the original
	/// says outright rather than leaving to fall through.
	/// </para>
	/// </summary>
	/// <param name="cellById">
	/// A cell by its number, <b>counted from one</b> as everything in the save is -
	/// <c>id => world.CellAt(MapStep.CellAt(id))</c> is the whole of it.
	/// </param>
	public static bool TrackCloses( ParkWorld.MapCell cell, Func<int, ParkWorld.MapCell> cellById )
	{
		ArgumentNullException.ThrowIfNull( cellById );

		if ( !TrackDefersToParent( cell.TrackType ) )
			return TrackCounts( cell.TrackType ) && (cell.TrackFlags & TrackOpenFlags) == 0;

		if ( cell.TrackParentId == 0 )
			return false;

		var parent = cellById( cell.TrackParentId );

		return TrackCounts( parent.TrackType ) && (parent.TrackFlags & TrackOpenFlags) == 0;
	}

	private readonly Func<int, int, ParkWorld.MapCell> _cellAt;
	private readonly int _mode;
	private readonly Func<ParkWorld.MapCell, bool> _trackCloses;
	private readonly Func<ParkWorld.MapCell, QueueVerdict> _queueAhead;

	/// <param name="cellAt"><see cref="ParkWorld.CellAt"/>, or a stand-in for a test.</param>
	/// <param name="mode">
	/// The original's fourth argument, which it names nothing and which is passed 0, 1, 2, a field of the
	/// thing doing the asking, and a global, depending on the caller. <b>Only the numbers are known</b>, so
	/// they are not given names here. What differs is visible below: on 2 the approach stops being freely
	/// passable, a thing standing on the cell ahead gets a say, and a path-to-open-ground step is allowed
	/// outright; on 1 a path may be left for anything that is not path, queue or footprint.
	/// </param>
	/// <param name="trackCloses">
	/// Whether the cell's <i>track</i> record closes it. Not modelled: see the class remarks.
	///
	/// <para>
	/// <b>Measured rather than guessed at, and the measurement is why this is small.</b> Every one of Lost
	/// Kingdom's 16,384 cells carries a track record; 572 of them carry a type this branch acts on, and of
	/// those it would close 568. But those cells are not park geography - drawn out they are nested
	/// rectangular frames two cells thick, mirrored across the map, and <b>only two of the 572 touch a path
	/// cell at all</b>, with 24 inside the path network's bounding box. So left out this answers no, which
	/// is wrong for 568 cells and reachable by a walker at two of them.
	/// </para>
	/// </param>
	/// <param name="queueAhead">
	/// What a thing standing on the cell ahead says. Not modelled: it needs the per-cell thing lists the
	/// engine keeps and this project does not. Left out, it answers <see cref="QueueVerdict.NothingThere"/>.
	/// </param>
	public CellEdge( Func<int, int, ParkWorld.MapCell> cellAt, int mode,
		Func<ParkWorld.MapCell, bool>? trackCloses = null,
		Func<ParkWorld.MapCell, QueueVerdict>? queueAhead = null )
	{
		_cellAt = cellAt ?? throw new ArgumentNullException( nameof( cellAt ) );
		_mode = mode;
		_trackCloses = trackCloses ?? (_ => false);
		_queueAhead = queueAhead ?? (_ => QueueVerdict.NothingThere);
	}

	/// <summary>
	/// Whether this side of this cell is closed. The shape fits <see cref="MapStep.CanStep"/>'s parameter,
	/// which is the whole point of it.
	/// </summary>
	public bool Blocked( int x, int y, StepDirection direction )
	{
		// The boundary is tested before anything is read from the map, so the edge of the map closes every
		// cell on it whatever is built there.
		if ( MapStep.LeavesTheMap( x, y, direction ) )
			return true;

		var (toX, toY) = MapStep.Beyond( x, y, direction );

		var from = _cellAt( x, y );
		var to = _cellAt( toX, toY );

		// Walking onto a path is allowed, unless leaving a queue - a queue is left by its own rules below.
		if ( to.Type == Path && !IsQueue( from.Type ) )
			return false;

		// Within one object's footprint, movement is free; onto one from outside it is not.
		if ( from.Type == Footprint && to.Type == Footprint )
			return false;

		if ( to.Type == Footprint )
			return true;

		// The approach is open on every mode but the strict one.
		if ( to.Type == Approach && _mode != 2 )
			return false;

		if ( _mode == 2 )
		{
			if ( (to.Flags & ApproachFlag) != 0 )
				return false;

			if ( to.Type == RideEnd )
			{
				switch ( _queueAhead( to ) )
				{
					case QueueVerdict.LetThemThrough:
						return false;
					case QueueVerdict.InTheWay:
						return true;
				}
			}
		}

		if ( IsSolid( to.Type ) )
			return true;

		// The original asks this separately having already refused it above. Kept as it stands.
		if ( to.Type == 2 )
			return true;

		// Joining or leaving a queue is the one case decided by which cells say they adjoin.
		if ( IsQueue( to.Type ) )
		{
			if ( from.Type != Path && !IsQueue( from.Type ) )
				return true;

			return (to.Neighbours & BitFor( direction )) == 0;
		}

		if ( IsQueue( from.Type ) && to.Type == Path )
			return (to.Neighbours & BitFor( direction )) == 0;

		if ( _trackCloses( to ) )
			return true;

		// Anyone not standing on path or queue has already been let through by this point.
		if ( from.Type != Path && !IsQueue( from.Type ) )
			return false;

		if ( _mode == 1 && from.Type == Path
			&& to.Type != Path && !IsQueue( to.Type ) && to.Type != Footprint )
			return false;

		if ( _mode == 2 && from.Type == Path && to.Type == Nothing )
			return false;

		// What is left is stepping off the end of a ride, which may only be done the way it faces.
		if ( from.Type != RideFarEnd && from.Type != RideEnd )
			return true;

		if ( to.Type != Nothing )
			return true;

		return from.Direction != BitFor( direction );
	}
}
