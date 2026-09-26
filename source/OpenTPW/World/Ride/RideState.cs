namespace OpenTPW;

/// <summary>
/// The ride a script drives, as the <c>COAST</c> opcode reaches it.
///
/// <para>
/// A ride script does not touch the world directly. Everything it does to its own ride goes through
/// one instruction - <c>COAST &lt;op&gt; &lt;arg&gt;</c> - and the engine dispatches that through an
/// eight-entry table at <c>0x556a5c</c>. This type is the far side of those eight ops, and nothing
/// else: it holds a queue, a count of riders, a capacity, whether the ride is open, and whether it is
/// broken. That is the whole of what the twelve shipped coaster scripts ask for.
/// </para>
///
/// <para>
/// <b>Every op below was read out of the binary rather than taken from its name</b>, which was not
/// optional - the names mislead in three places. <c>COAST_SETWORN</c> calls nothing at all, so wear
/// does not exist here either. <c>COAST_GETQUEUE</c> returns the room <i>remaining</i>, not the length
/// of the queue, which inverts the sense of every <c>BRANCH_Z</c> that follows it. And
/// <c>COAST_SETBROKE</c> is not a boolean: <c>FUN_0043b270</c> dispatches on 0, 1 and 2.
/// </para>
///
/// <para>
/// <b>Three things here are deliberately smaller than the original</b>, because the parts they would
/// need do not exist yet, and a guess dressed as a model is worse than an honest gap:
/// </para>
///
/// <list type="bullet">
/// <item>The original's run state is a four-value machine in the low nibble of <c>ride[8]</c> - 1, 2,
/// 4 and 8 - and <c>FUN_00435730</c> guards every transition. Only 4 (open) and 8 (closed) are
/// reachable through <c>COAST</c>, so only those two are modelled; the guards are kept.</item>
/// <item><see cref="SetCapacity"/> keeps the original's clamp at zero but not its two upper clamps,
/// which read a ride record (<c>[ride[4]+0x318]</c>) and a track record
/// (<c>[[ride[0x128]+4]+8]</c>) that nothing here loads.</item>
/// <item><see cref="AddRider"/> guards on the capacity rather than on the original's separate ring
/// size at <c>ride[0xb0]</c>, an allocation this has no equivalent of. The two agree whenever a
/// script asks <see cref="RoomRemaining"/> first, which all twelve shipped scripts do.</item>
/// </list>
/// </summary>
public sealed class RideState
{
	/// <summary>What <c>SETBROKE 0</c> asks for: the bits the engine writes into <c>ride[8] &amp; 0x70</c>.</summary>
	private const int WorkingBits = 0x10;

	/// <summary>What <c>SETBROKE 1</c> asks for. This is the only value a shipped script ever breaks a ride with.</summary>
	private const int BrokenBits = 0x40;

	/// <summary>
	/// What <c>SETBROKE 2</c> asks for. <c>FUN_0043b270</c> accepts it and no shipped script passes it,
	/// so what it means is not established - it is carried rather than named.
	/// </summary>
	private const int ThirdBits = 0x20;

	/// <summary>Riders waiting to get on, filled by <see cref="AddRider"/>; the ring at <c>ride[0xb4]</c>.</summary>
	private readonly Queue<int> _waiting = new();

	/// <summary>
	/// Riders waiting to get off, drained by <see cref="TakeRider"/>. In the original this is a second,
	/// separate ring at <c>ride[0xd4]</c> that <c>ADDPEEP</c> does not fill - the ride's own simulation
	/// does, as cars come back in. See <see cref="FinishRider"/>.
	/// </summary>
	private readonly Queue<int> _finished = new();

	private int _repairBits = WorkingBits;

	/// <summary>
	/// How many riders the ride will hold. <c>SETCAPACITY</c> reaches this through
	/// <c>FUN_0043b330</c> and <c>FUN_0043ba90</c>, whose last act is to copy it into the field
	/// <c>GETQUEUE</c> measures against - so capacity and the room in the queue really are one number,
	/// which reading only the first of those two functions would suggest they are not.
	/// </summary>
	public int Capacity { get; private set; }

	/// <summary>How many riders are waiting to get on.</summary>
	public int Waiting => _waiting.Count;

	/// <summary>
	/// How many riders are on the ride itself. <c>COAST</c> has no op that changes this - in the
	/// original the ride's own simulation does - so with no simulation here it stays at zero, and
	/// <see cref="RoomRemaining"/> answers as though the ride were empty.
	/// </summary>
	public int OnRide { get; private set; }

	/// <summary>
	/// Whether the ride is shut. A ride begins open: the first thing every shipped coaster script does
	/// after <c>INITIALISE</c> is <c>SETCLOSED 1</c>, and the engine only honours a close from the open
	/// state, so a ride that began closed would make that opening sequence do nothing at all.
	/// </summary>
	public bool Closed { get; private set; }

	/// <summary>Whether <c>SETBROKE</c> has put the ride out of action.</summary>
	public bool Broken => _repairBits == BrokenBits;

	/// <summary>
	/// Ops the ride refused: a rider turned away with no room, or a transition the engine's own guards
	/// disallow. Counted rather than hidden, so a test can tell "it did nothing" from "it was not asked".
	/// </summary>
	public int Refused { get; private set; }

	/// <summary>
	/// <c>COAST_GETQUEUE</c>, op 2 - <c>FUN_0043b1f0</c>. The room left, clamped at zero, which is what
	/// the trailing <c>((int)x &lt; 0) - 1 &amp; x</c> in the original computes. <b>This is room
	/// remaining, not queue length</b>, so a script that branches on zero is branching on "full".
	/// </summary>
	public int RoomRemaining()
	{
		var room = Capacity - OnRide - _waiting.Count;

		return room < 0 ? 0 : room;
	}

	/// <summary>
	/// <c>COAST_ADDPEEP</c>, op 1 - <c>FUN_0043b0e0</c>. Puts a rider in the queue if there is room,
	/// and silently declines if there is not, exactly as the original's guarded write does.
	/// </summary>
	public void AddRider( int rider )
	{
		if ( RoomRemaining() <= 0 )
		{
			++Refused;
			return;
		}

		_waiting.Enqueue( rider );
	}

	/// <summary>
	/// <c>COAST_GETPEEP</c>, op 3 - <c>FUN_0043b220</c>. Takes the next rider who has finished, or
	/// returns 0 when there is none. Zero is how the original says "nobody", and the scripts test for
	/// it with the branch that follows.
	/// </summary>
	public int TakeRider() => _finished.Count > 0 ? _finished.Dequeue() : 0;

	/// <summary>
	/// Hands a rider to the queue that <see cref="TakeRider"/> drains - the seam where a ride
	/// simulation will connect. It is not a <c>COAST</c> op: in the original, cars coming back in fill
	/// this ring, and until something does, <see cref="TakeRider"/> correctly answers "nobody" forever.
	/// </summary>
	public void FinishRider( int rider ) => _finished.Enqueue( rider );

	/// <summary>
	/// <c>COAST_SETCLOSED</c>, op 5 - <c>FUN_0043b2f0</c>. Zero opens the ride, anything else shuts it.
	/// Both directions are guarded, and the guards are the point: the engine tests the current state
	/// first and drops the request when it does not fit, so this is a transition rather than a flag.
	/// </summary>
	public void SetClosed( int value )
	{
		var wantClosed = value != 0;

		if ( wantClosed == Closed )
		{
			++Refused;
			return;
		}

		Closed = wantClosed;
	}

	/// <summary>
	/// <c>COAST_SETBROKE</c>, op 4 - <c>FUN_0043b270</c>, which routes to the same state-request
	/// function as <c>SETCLOSED</c> because the two write different fields of one word: the run state
	/// lives in <c>ride[8] &amp; 0xf</c> and this in <c>ride[8] &amp; 0x70</c>. Takes 0, 1 or 2;
	/// anything else is not a request the engine makes.
	/// </summary>
	public void SetBroken( int value )
	{
		switch ( value )
		{
			case 0:
				_repairBits = WorkingBits;
				break;

			case 1:
				_repairBits = BrokenBits;
				break;

			case 2:
				_repairBits = ThirdBits;
				break;

			default:
				// A repair value the engine's own switch has no arm for - unlike the two refusals
				// above, which are its guarded writes working as intended. Only this one is a gap.
				++Refused;
				Unimplemented.Report( $"SETBROKE value {value}" );
				break;
		}
	}

	/// <summary>
	/// <c>COAST_SETCAPACITY</c>, op 6 - <c>FUN_0043b330</c>. Negative values clamp to zero, as the
	/// original's <c>x &amp; (((int)x &lt; 0) - 1)</c> does. The original's two upper clamps are absent
	/// here; see the note on this class.
	/// </summary>
	public void SetCapacity( int value ) => Capacity = value < 0 ? 0 : value;

	/// <summary>
	/// <c>COAST_SETWORN</c>, op 7 - <c>0x00554c07</c>. <b>The shipped engine does nothing with this.</b>
	/// The handler fetches its operand, resolves it, cleans the stack and returns without calling
	/// anything, and twelve scripts use it. It is here so that reading the call site does not suggest
	/// the op was overlooked: wear is maintained somewhere else, or not at all.
	/// </summary>
	public void SetWorn( int value )
	{
		_ = value;
	}
}
