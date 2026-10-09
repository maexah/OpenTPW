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
	/// holds no record for is not written here: <see cref="MadeObjectRecord"/> makes its record.
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

			PutObject( body, _records[index].At, thing );

			++written;
		}

		return written;
	}

	/// <summary>The fields of an object's record that follow the running park, each where <see cref="ReadCatalogueObject"/> reads it.</summary>
	private static void PutObject( byte[] body, int start, CatalogueObject thing )
	{
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
	}

	/// <summary>
	/// An object bought since the load, as a park file holds one (<c>docs/exe/saves.md</c>, "OpenTPW's writer, a
	/// thing bought and a thing sold"): the park's own record, and the two lines of its name, the item's rows of
	/// <c>OBJECT_NAMES</c> (<c>FUN_00413020</c>, <c>FUN_004130b0</c>).
	/// </summary>
	public sealed record MadeObject( CatalogueObject Object, string NameA, string NameB );

	/// <summary>The objects bought and sold since the load, for <see cref="PutPeople"/>: each made one's whole record, newest first, and the ids gone.</summary>
	internal sealed record ObjectEdits( IReadOnlyList<(int Id, byte[] Record)> Made, IReadOnlySet<int> Gone );

	/// <summary>How many characters each line of an object's name holds, its terminator among them.</summary>
	private const int NameLength = 33;

	/// <summary>The six rings' places in an object's record, each of <see cref="DayRing.Size"/> bytes.</summary>
	private static readonly int[] RingsAt = [228, 361, 498, 635, 768, 901];

	/// <summary>What the object constructor starts the three floats at <c>+0x44</c>, <c>+0x48</c> and <c>+0x4c</c> at (<c>FUN_004db090</c>, <c>0x004db36f</c>).</summary>
	private const float ConstructedFloat = 100f;

	/// <summary>
	/// A whole record for an object the file does not hold, as the object constructor <c>FUN_004db090</c> and the
	/// serialiser <c>FUN_004db7d0</c> leave one: the thing's place at the middle of its anchor cell, its angle,
	/// item and date, <paramref name="meshHandle"/>, its flags, the two lines of its name, its script, its track
	/// ride, <c>mState</c> (nought for one a guest may be offered and 3 for any other, <c>0x004db4fb</c>),
	/// <c>mTopLeft</c>, its two ends, <c>mIsTrackRideValid</c> 1, six rings of thirty days, the running fields
	/// (<see cref="PutObject"/>) and 100 in the float no reader here names. Its links are
	/// <see cref="PutPeople"/>'s to write.
	///
	/// <para>
	/// <b><c>mTopLeft</c> is the anchor cell's id unless the record names one</b>: the constructor adds a pair of
	/// the item's description that nothing here reads (<c>+0x168</c>), nought for every object of seven park
	/// files but two (345 of 349).
	/// </para>
	/// </summary>
	public static byte[] MadeObjectRecord( MadeObject made, int meshHandle )
	{
		var thing = made.Object;
		var record = new byte[RecordSizes[CatalogueObjectModel]];
		var cellX = thing.RawX >> 8;
		var cellY = thing.RawY >> 8;

		PutInt32( record, 4, CatalogueObjectModel );
		PutUInt16( record, 8, (ushort)((cellX << 8) | 0x80) );
		PutUInt16( record, 10, (ushort)((cellY << 8) | 0x80) );
		PutInt32( record, 16, thing.Angle );
		PutUInt16( record, 20, (ushort)thing.CatalogueId );

		var built = thing.Built;
		int[] stamp = [built.Year, built.Month, built.Day, built.DayOfWeek, built.Hour, built.Minute, built.Second, built.Millisecond];

		for ( var i = 0; i < stamp.Length; ++i )
			PutInt32( record, 22 + (i * 4), stamp[i] );

		PutInt32( record, 54, meshHandle );
		PutUInt16( record, 58, thing.Flags );

		for ( var i = 0; i < NameLength - 1; ++i )
		{
			PutUInt16( record, 60 + (i * 4), i < made.NameA.Length ? made.NameA[i] : (ushort)0 );
			PutUInt16( record, 62 + (i * 4), i < made.NameB.Length ? made.NameB[i] : (ushort)0 );
		}

		PutInt32( record, 192, thing.RideScript );
		PutInt32( record, 196, thing.TrackRide );
		PutInt32( record, 200, thing.IsVisitable ? 0 : 3 );
		PutUInt16( record, 204, thing.TopLeft != 0 ? thing.TopLeft : (ushort)((cellY * MapSize) + cellX + 1) );
		PutUInt16( record, 206, thing.EntryPos );
		PutUInt16( record, 218, thing.ExitPos );
		PutInt32( record, 222, 1 );

		foreach ( var ring in RingsAt )
			PutInt32( record, ring + 4, DayRing.Length );

		PutObject( record, 0, thing );
		PutInt32( record, 1066, BitConverter.SingleToInt32Bits( ConstructedFloat ) );
		record[1098] = unchecked((byte)thing.UpgradeLevel);

		return record;
	}

	/// <summary>Where <c>mNext</c>, an object's link in the object list, sits in its record.</summary>
	private const int ObjectNextAt = 208;

	/// <summary>
	/// Writes each item's standing count and first-build stamp over its object control in <paramref name="body"/>
	/// (FileFormats <c>saves.md</c>, "The object controls": <c>+0x18</c> and <c>+0x1c</c>), for the items
	/// <paramref name="built"/> answers; an item the file holds no control for is left out.
	/// </summary>
	/// <returns>How many controls were written over.</returns>
	internal int PutControls( byte[] body, IReadOnlyDictionary<int, (int Standing, uint FirstBuilt)> built )
	{
		var written = 0;

		for ( var i = 0; i < ObjectControlRecords.Count && _controlsAt >= 0; ++i )
		{
			if ( !built.TryGetValue( ObjectControlRecords[i].ItemId, out var now ) )
				continue;

			var at = _controlsAt + (i * ObjectControlSize);

			PutInt32( body, at + 0x18, now.Standing );
			PutInt32( body, at + 0x1c, unchecked((int)now.FirstBuilt) );
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
