namespace OpenTPW;

/// <summary>
/// Which of a park's objects a guest may be offered right now - the original's <c>FUN_004dd920</c>, the
/// gate every candidate passes before it is worth scoring at all.
///
/// <para>
/// <b>This is the filter, not the choice.</b> <c>FUN_004fcb10</c> walks the object list, asks this of each
/// candidate, scores the survivors with <c>FUN_004fcc30</c> and takes the best. Only the asking is here;
/// the scoring is a seven-term weighted mean, and a partial one would produce numbers the original never
/// would. Its inputs ARE all identified now - the seven weights are named in the balance file, both
/// lookup tables were read out of the image, the distance divisor is the effects record's short and the
/// newness term is each thing's own build date - so what remains is building it rather than finding it.
/// </para>
/// <para>
/// <b>The track-type arms ARE reproduced, and this said they were not until the field was found.</b> The
/// original refuses a candidate of type 1 or 2 - a car track or a water track - unless
/// <c>mIsTrackRideValid</c> is set. The field is <c>Bumper.WhichTrackType</c> on the item's own
/// description, identified across the whole catalogue rather than from one item: the jungle's
/// <c>Dino Karts</c> is 1, <c>Splish Splash</c> is 2, its three coasters are 3, and every other ride is
/// nought.
/// </para>
/// <para>
/// <b>ONE arm is still not reproduced, and it is a different function rather than a missing field.</b> A
/// type 3 candidate - a coaster - is additionally refused unless <c>FUN_00441970</c> passes, and that is
/// a check on the thing's MODEL and its flags rather than on the item. Nothing here models that, so a
/// coaster is admitted where the original might refuse it. <b>The shipped park contains no coaster</b>,
/// so the arm cannot fire in it either way.
/// </para>
/// </summary>
public static class ParkRideChoice
{
	/// <summary>
	/// How many people may queue per cell of queue before the object stops being offered -
	/// <c>FUN_004dd920</c> compares the queue's length against <c>mQueueSizeInCells</c> shifted left twice.
	/// </summary>
	public const int QueueRoomPerCell = 4;

	/// <summary>
	/// The two states that take an object out of service. <b>Named by their numbers, because what they
	/// mean is not established</b> - the shipped park holds only 0 and 3, so neither value here occurs in
	/// it and nothing about them can be checked against this save.
	/// </summary>
	public const int StateRefusedOne = 1;

	/// <inheritdoc cref="StateRefusedOne"/>
	public const int StateRefusedFour = 4;

	/// <summary>
	/// Whether a guest may be offered this object, given how many people are already queueing for it.
	/// </summary>
	/// <param name="queueLength">
	/// How many are in its queue now. The save leaves every <c>mFirstInQ</c> at nought - nobody has ever
	/// queued in this park - so a park read from the file starts every queue empty and fills it as guests
	/// join.
	/// </param>
	/// <param name="trackType">
	/// The item's <c>Bumper.WhichTrackType</c>, or nought where the catalogue cannot say. A car track or a
	/// water track needs its track ride to be valid before anyone may be sent to it.
	/// </param>
	public static bool CanBeOffered( ParkWorld.CatalogueObject item, int queueLength, int trackType = 0 )
	{
		// A tracked ride whose track is not valid is not open, whatever else is true of it.
		if ( trackType is ItemDescriptionFile.CarTrack or ItemDescriptionFile.WaterTrack
			&& item.IsTrackRideValid == 0 )
			return false;

		// A guest may only be sent somewhere the item's own description says they may - see
		// CatalogueObject.IsVisitable, which is the save's side of Info.IsChoosable.
		if ( !item.IsVisitable )
			return false;

		if ( item.State is StateRefusedOne or StateRefusedFour )
			return false;

		// mCanLoad. The original tests it for non-zero rather than for a particular value, and so does this.
		if ( item.CanLoad == 0 )
			return false;

		// It must have somewhere to be approached from. An unplaced object carries the sentinel entry, and
		// walking to it would send a guest to the corner of the map.
		if ( !item.IsPlaced || item.EntryPos == 0 )
			return false;

		return HasQueueRoom( item, queueLength );
	}

	/// <summary>
	/// Whether the queue has room - <c>queueLength &lt; mQueueSizeInCells * 4</c>.
	/// </summary>
	/// <remarks>
	/// <b>An object with no queue cells has no room, and that is the original's arithmetic rather than a
	/// special case:</b> nought cells times four is nought, and nothing is less than nought. It matters
	/// because the shipped park's Drinks Shop and its three toilets all declare nought queue cells, so this
	/// is the term that decides them - and the two objects that do declare some are the sideshow and the
	/// ride.
	/// </remarks>
	public static bool HasQueueRoom( ParkWorld.CatalogueObject item, int queueLength )
		=> queueLength < item.QueueSizeInCells * QueueRoomPerCell;

	/// <summary>
	/// How many guests are actually queueing for this object, by walking the queue itself - the original's
	/// <c>GetPositionInQueue</c> (<c>FUN_004ddf50</c>) counting to the end instead of to a particular
	/// person.
	///
	/// <para>
	/// <b>The walk starts at the object's <c>mFirstInQ</c> and follows each guest's own <c>mQNext</c></b>,
	/// which is a thing handle and not a cell - the field two bytes before it,
	/// <see cref="ParkWorld.CatalogueObject.BackOfQueue"/>, IS a cell, and confusing the two is the trap
	/// that record's own remarks warn about.
	/// </para>
	/// <para>
	/// <b>Every queue in the shipped park measures nought, and that is the park rather than the walk.</b>
	/// Its <c>mFirstInQ</c> is nought on every object because nobody has ever been admitted to it -
	/// <c>mNumberOfVisitorsToDate</c> is nought too - so this can only start returning something once
	/// guests join queues - <b>which they now do</b>, through <c>PeepBehaviour.JoinTheQueue</c>, so this
	/// does return something in a running park. It was built before that, because the field it needed was
	/// the thing blocking it rather than because the park could show it off.
	/// </para>
	/// </summary>
	public static int QueueLength( ParkWorld? park, ParkWorld.CatalogueObject item )
	{
		if ( park == null )
			return 0;

		var guests = new Dictionary<int, ParkWorld.GuestState>();

		foreach ( var person in park.People )
		{
			if ( person.Guest is { } guest )
				guests[person.ThingId] = guest;
		}

		return QueueLengthFrom( item.FirstInQueue,
			id => guests.TryGetValue( id, out var guest ) ? guest.QNext : null, guests.Count );
	}

	/// <summary>
	/// The walk itself, over whatever can answer "who is behind this one" - which is what makes it
	/// testable at all.
	/// </summary>
	/// <remarks>
	/// <b>The shipped park cannot exercise this, so the seam is the test.</b> Every one of its queues is
	/// empty, so any assertion made against it would pass just as happily against a method that returned
	/// nought and walked nothing. Taking the link lookup as a parameter is what lets a real chain - and a
	/// circular one - be put through it.
	/// </remarks>
	/// <param name="nextOf">
	/// The <c>mQNext</c> of a guest, or null for a handle that names nobody - which ends the walk, as a
	/// queue naming a thing that is not a guest does in the original.
	/// </param>
	/// <param name="guests">
	/// How many guests there could be, bounding the walk so a circular chain stops instead of hanging -
	/// the same guard <see cref="Offerable"/> puts on the object list, and for the same reason.
	/// </param>
	public static int QueueLengthFrom( int firstInQueue, Func<int, int?> nextOf, int guests )
	{
		ArgumentNullException.ThrowIfNull( nextOf );

		var length = 0;

		for ( var id = firstInQueue; id != 0 && length <= guests; ++length )
		{
			if ( nextOf( id ) is not { } next )
				break;

			id = next;
		}

		return length;
	}

	/// <summary>
	/// Every object a guest could be offered, walked in the order the original walks them - from the
	/// header's <c>mFirstObject</c> along each object's own <c>mNext</c>, rather than in the order the
	/// reader happens to hold them.
	/// </summary>
	/// <param name="queueLength">
	/// How long each object's queue is, or null to treat every queue as empty - which is what a park
	/// straight out of the file has.
	/// </param>
	/// <param name="trackTypeOf">
	/// Each object's <c>Bumper.WhichTrackType</c>, from the item catalogue, or null where no catalogue is
	/// to hand - which treats every object as untracked.
	/// </param>
	public static List<ParkWorld.CatalogueObject> Offerable( ParkWorld? park,
		Func<ParkWorld.CatalogueObject, int>? queueLength = null,
		Func<ParkWorld.CatalogueObject, int>? trackTypeOf = null )
	{
		var offerable = new List<ParkWorld.CatalogueObject>();

		if ( park == null )
			return offerable;

		var byId = new Dictionary<int, ParkWorld.CatalogueObject>();

		foreach ( var item in park.Objects )
			byId[item.ThingId] = item;

		var seen = 0;

		for ( var id = park.FirstObject; id != 0 && seen <= byId.Count; ++seen )
		{
			if ( !byId.TryGetValue( id, out var item ) )
				break;

			if ( CanBeOffered( item, queueLength?.Invoke( item ) ?? 0, trackTypeOf?.Invoke( item ) ?? 0 ) )
				offerable.Add( item );

			id = item.NextObject;
		}

		return offerable;
	}
}
