namespace OpenTPW;

/// <summary>
/// The staff pool and the arrival timer as a park file's writer takes them (<c>docs/exe/saves.md</c>, "OpenTPW's
/// writer, the staff pool and the arrival timer").
/// </summary>
public sealed partial class ParkWorld
{
	/// <summary>How many kinds of staff the pool counts: the five <c>mPeopleInCat</c> and <c>mStopProducing</c> pairs.</summary>
	private const int PoolKinds = 5;

	/// <summary>Where the staff pool's first record sits in the body; -1 where the walk never reached it.</summary>
	public int StaffPoolAt { get; private set; } = -1;

	/// <summary>Where the arrival block's first field, <c>mArrivalRate</c>, sits in the body; -1 where the walk never reached it.</summary>
	public int ArrivalAt { get; private set; } = -1;

	/// <summary>
	/// <c>mPeopleInCat</c>: how many of each kind the park employed as the pool last counted them, by kind
	/// (<c>FUN_00508000</c>, which the top-up calls).
	/// </summary>
	public IReadOnlyList<int> StaffPoolPeopleInCat { get; private set; } = [];

	/// <summary><c>mStopProducing</c>: whether that count had reached the kind's <c>Max&lt;Kind&gt;InPark</c>.</summary>
	public IReadOnlyList<bool> StaffPoolStopProducing { get; private set; } = [];

	/// <summary>
	/// The staff pool as it is written: the candidate each of the 32 slots holds, or null for an empty one; the
	/// five counts and flags of the last top-up; and the pool's own mark.
	/// </summary>
	public sealed record WrittenStaffPool( IReadOnlyList<StaffCandidate?> Slots, IReadOnlyList<int> PeopleInCat,
		IReadOnlyList<bool> StopProducing, int TimeSig );

	/// <summary>
	/// Writes the staff pool over the file's in <paramref name="body"/>, a copy of <see cref="Body"/>. A slot that
	/// holds a candidate is written whole, valid and not on the pointer; an empty one loses its <c>mValid</c> and
	/// keeps the rest of its bytes, as the original's own slot does when its candidate goes
	/// (<c>FUN_005084f0</c>). <c>mOpeningStaffPoolGenerated</c> is left as the file's.
	/// </summary>
	/// <exception cref="InvalidOperationException">The walk never reached the pool, or the pool is not 32 slots and five kinds.</exception>
	internal void PutStaffPool( byte[] body, WrittenStaffPool pool )
	{
		if ( StaffPoolAt < 0 )
			throw new InvalidOperationException( "the park file it was loaded from was not read as far as its staff pool" );

		if ( pool.Slots.Count != PoolRecords || pool.PeopleInCat.Count != PoolKinds || pool.StopProducing.Count != PoolKinds )
			throw new InvalidOperationException( "a staff pool is 32 slots and five kinds" );

		for ( var slot = 0; slot < PoolRecords; ++slot )
		{
			var at = StaffPoolAt + slot * PoolRecordSize;

			if ( pool.Slots[slot] is not { } candidate )
			{
				body[at + 0x0a] = 0;
				continue;
			}

			PutInt32( body, at, candidate.Type );
			PutInt32( body, at + 4, candidate.Name );
			body[at + 8] = (byte)candidate.PayGrade;
			body[at + 9] = (byte)candidate.SubType;
			body[at + 0x0a] = 1;
			body[at + 0x0b] = 0;
			PutInt32( body, at + 0x0c, candidate.TimeSig );
			PutInt32( body, at + 0x10, candidate.TimeoutTime );
		}

		var tail = StaffPoolAt + PoolRecords * PoolRecordSize;

		for ( var kind = 0; kind < PoolKinds; ++kind )
		{
			PutInt32( body, tail + kind * 5, pool.PeopleInCat[kind] );
			body[tail + kind * 5 + 4] = (byte)(pool.StopProducing[kind] ? 1 : 0);
		}

		PutInt32( body, tail + PoolCountsSize, pool.TimeSig );
	}

	/// <summary>
	/// Writes the arrival timer's mark, the count still to drop and whether a load is held over the file's in
	/// <paramref name="body"/>, a copy of <see cref="Body"/>. <c>mArrivalRate</c>, <c>mTargetVehicleCapacity</c> and
	/// <c>mGatesOpen</c> are left as the file's: nothing here runs them. <paramref name="currentVehicle"/> is the
	/// header's <c>mCurrentArrivalVehicle</c>, the thing of the vehicle that is current; null leaves the file's.
	/// </summary>
	/// <exception cref="InvalidOperationException">The walk never reached the block.</exception>
	internal void PutArrival( byte[] body, int timeSig, int peopleOnBus, bool offloading, int? currentVehicle = null )
	{
		if ( ArrivalAt < 0 )
			throw new InvalidOperationException( "the park file it was loaded from was not read as far as its arrival block" );

		PutInt32( body, ArrivalAt + 4, timeSig );
		PutInt32( body, ArrivalAt + 0x0c, peopleOnBus );
		body[ArrivalAt + 0x10] = (byte)(offloading ? 1 : 0);

		if ( currentVehicle is { } vehicle )
			Put16( body, HeaderAt + HeaderFieldAt( CurrentArrivalVehicleField ), vehicle );
	}
}
