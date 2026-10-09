using System.Buffers.Binary;

namespace OpenTPW;

public sealed partial class ParkWorld
{
	/// <summary>
	/// Writes each object as it runs over its record in <paramref name="body"/>, a copy of the payload this was
	/// read from (<c>docs/exe/saves.md</c>, "OpenTPW's writer, the objects"): its door (<c>mCanLoad</c>), the member
	/// of staff assigned and the tick they were, the queue's back cell, size and head, the guest being loaded, the
	/// six day rings and the two counts between them, the operating three, the goods' cost, quality, chance and
	/// ingredient, the price, the two repair floats, the service request and the two totals, each where
	/// <see cref="ReadCatalogueObject"/> reads it.
	///
	/// <para>
	/// The rest of a record stays the file's: where the object stands, its item, its model and its script, its
	/// flags, <c>mState</c>, its entry and exit, its link in the object list and its upgrade. An object the file
	/// holds no record for is not written.
	/// </para>
	/// </summary>
	/// <returns>How many records were written over.</returns>
	internal int PutObjects( byte[] body, IReadOnlyList<CatalogueObject> objects )
	{
		var written = 0;

		foreach ( var thing in objects )
		{
			var index = _records.FindIndex( record => record.Id == thing.ThingId && record.Model == CatalogueObjectModel );

			if ( index < 0 )
				continue;

			var start = _records[index].At;

			PutUInt16( body, start + 210, thing.AssignedStaff );
			PutUInt16( body, start + 212, thing.BackOfQueue );
			PutInt32( body, start + 214, thing.CanLoad );
			PutUInt16( body, start + 220, thing.FirstInQueue );

			if ( thing.Rings is { } rings )
				PutRings( body, start + 228, rings );

			body[start + 1034] = unchecked((byte)thing.OperatingCapacity);
			body[start + 1035] = unchecked((byte)thing.OperatingDuration);
			PutInt32( body, start + 1036, thing.OperatingSpeed );
			PutUInt16( body, start + 1040, thing.PersonBeingLoaded );
			PutInt32( body, start + 1042, thing.CostOfGoods );
			PutInt32( body, start + 1046, thing.QualityOfGoods );
			PutInt32( body, start + 1050, thing.ChanceOfWinning );
			PutInt32( body, start + 1054, thing.PricePerUse );
			PutInt32( body, start + 1058, thing.AmountOfSpecialIngredient );
			PutInt32( body, start + 1062, thing.QueueSizeInCells );
			PutInt32( body, start + 1070, BitConverter.SingleToInt32Bits( thing.RemainingLife ) );
			PutInt32( body, start + 1074, BitConverter.SingleToInt32Bits( thing.StateOfRepair ) );
			PutInt32( body, start + 1078, thing.RequestedService );
			PutInt32( body, start + 1082, thing.TimeMarkedForMaintenance );
			PutInt32( body, start + 1086, thing.TotalCosts );
			PutInt32( body, start + 1090, thing.TotalTakings );

			++written;
		}

		return written;
	}

	/// <summary>The six rings and the two counts, in <see cref="ReadRings"/>'s order.</summary>
	private static void PutRings( byte[] body, int at, ObjectRings rings )
	{
		PutRing( body, ref at, rings.Costs );
		PutRing( body, ref at, rings.Takings );
		PutInt32( body, at, rings.NumCustomers );
		at += 4;
		PutRing( body, ref at, rings.Customers );
		PutInt32( body, at, rings.NumWalkAways );
		at += 4;
		PutRing( body, ref at, rings.WalkAways );
		PutRing( body, ref at, rings.Served );
		PutRing( body, ref at, rings.Satisfaction );
	}

	/// <summary>
	/// One ring, in <see cref="ReadRing"/>'s order. Its <c>mNumEntries</c> is left the file's: the record's size
	/// holds thirty days whatever it says.
	/// </summary>
	private static void PutRing( byte[] body, ref int at, DayRing ring )
	{
		PutInt32( body, at, ring.CurrentEntry );
		body[at + 8] = ring.WrappedAround ? (byte)1 : (byte)0;
		PutInt32( body, at + 9, ring.Today );

		for ( var i = 0; i < DayRing.Length && i < ring.Days.Count; ++i )
			PutInt32( body, at + 13 + (i * 4), ring.Days[i] );

		at += DayRing.Size;
	}
}
