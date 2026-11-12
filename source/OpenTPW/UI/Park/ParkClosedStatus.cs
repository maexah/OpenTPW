namespace OpenTPW.UI;

/// <summary>
/// An object's status, <c>FUN_00485f60( thing, 2 )</c>, as far as the queue's arm, and each status's UITEXT row
/// (<c>0x0074fbc0</c>) and colour (<c>0x0074fb50</c>); docs/exe/ride-window-door.md, "The statuses behind Q212".
/// </summary>
internal static class ParkClosedStatus
{
	/// <summary>Condemned, mState 4: the ride window hides seven verbs (<c>FUN_004ad4e0</c>).</summary>
	internal const int Condemned = 3;

	/// <summary>A kart or water ride whose circuit is not closed (<c>mIsTrackRideValid</c> nought), UITEXT 386.</summary>
	internal const int TrackIncomplete = 0x19;

	/// <summary>The last of the two track statuses, at which the ride window disables its door.</summary>
	internal const int TrackInvalid = 0x1a;

	/// <summary>The staff the service arms ask about: the park's members, its strikes and mGameTick.</summary>
	internal readonly record struct Staffing( IReadOnlyList<Staff>? Staff, ParkStrikes? Strikes, int Tick )
	{
		internal static Staffing Now => ParkPeople.Current is { } people
			? new( people.Staff, people.Strikes, people.State.GameTick )
			: default;

		internal Staff? ById( int thingId ) => Staff?.FirstOrDefault( member => member.ThingId == thingId );
	}

	internal static int For( IParkInitialState? park, ParkWorld.CatalogueObject ride, int trackType = 0, Staffing? staffing = null )
	{
		var staff = staffing ?? Staffing.Now;

		if ( ride.State == 4 )
			return Condemned;

		if ( trackType is ItemDescriptionFile.CarTrack or ItemDescriptionFile.WaterTrack && ride.IsTrackRideValid == 0 )
			return TrackIncomplete;

		// FUN_00441910 and FUN_00441940 ask the coaster's track record, which is not built: a coaster is taken as complete.
		if ( trackType == ItemDescriptionFile.CoasterTrack )
			Unimplemented.Report( "OBJECT_STATUS_COASTER_TRACK_RECORD" );

		// Waiting for an upgrade (0x004e03e0), the mechanics asked: 5 to 9.
		if ( ride.State == 2 )
			return Served( ride, staff, StaffBehaviour.MechanicModel, 5 );

		// A service requested (+0x64): a toilet asks the handymen, 0xb to 0xf, anything else the mechanics, 0x11 to 0x15.
		if ( ride.RequestedService != 0 )
			return ride.IsToilet
				? Served( ride, staff, StaffBehaviour.HandymanModel, 0xb )
				: Served( ride, staff, StaffBehaviour.MechanicModel, 0x11 );

		// Broken down (0x004e0370): repairs in progress while the assignee is at work, else broken down, assigned or not.
		if ( ride.State == 1 )
			return AtWork( ride, staff ) ? 0x15 : 2;

		// 22 for a ride still open, 23 for one closed (0x16 + FUN_004e0440): a queue cut off from the
		// path does not close its ride.
		if ( ride.HasQueuePath && !ParkRideOperation.BackOfQueueConnected( park, ride ) )
			return ride.CanLoad == 0 ? 23 : 22;

		// DEVIATION: the original asks for the exit-disconnected arm (status 24, FUN_004de5a0) before the
		// closed test, so an open ride with its exit cut off shows 24 there; this returns 0 for an open ride
		// before that arm (docs/exe/ride-window-door.md, "The statuses behind Q212").
		if ( ride.CanLoad != 0 )
			return 0;

		// The exit-disconnected arm (status 24) precedes ordinary closure and is not decoded here.
		Unimplemented.Report( "OBJECT_WINDOW_EXIT_CONNECTION_STATUS" );
		return 1;
	}

	/// <summary>
	/// One service arm from its first status: none of that kind in the park (world <c>mFirstMechanic</c> or
	/// <c>mFirstHandyman</c> nought) the first, the kind on strike (<c>FUN_00509990</c>) the first + 2, nobody assigned
	/// (<c>FUN_004e0220</c>) the first + 1, assigned the first + 3, and + 4 while the assignee is at work.
	/// </summary>
	private static int Served( ParkWorld.CatalogueObject ride, Staffing staff, int model, int first )
	{
		if ( staff.Staff?.Any( member => member.Model == model ) != true )
			return first;

		if ( staff.Strikes?.IsOnStrike( ParkStaffPool.KindFor( model ) ) == true )
			return first + 2;

		if ( Assigned( ride, staff ) == 0 )
			return first + 1;

		return first + 3 + (AtWork( ride, staff ) ? 1 : 0);
	}

	/// <summary>
	/// <c>FUN_004e0220</c>'s answer. DEVIATION: the original forgets a lapsed assignment there and then; this asks
	/// only, and the forgetting is the staff's own turn's (<c>StaffBehaviour.AssignedTo</c>).
	/// </summary>
	private static int Assigned( ParkWorld.CatalogueObject ride, Staffing staff )
		=> ride.AssignedStaff != 0 && StaffBehaviour.KeepsAssignment( ride, staff.Tick, staff.ById )
			? ride.AssignedStaff
			: 0;

	/// <summary>
	/// <c>FUN_004e02f0</c>: an assignee (<c>+0x5e</c>) at work on this thing (<c>FUN_005065d0</c>), a mechanic repairing it
	/// (state <c>0xd</c>) or a handyman cleaning it (state <c>0xb</c>).
	/// </summary>
	private static bool AtWork( ParkWorld.CatalogueObject ride, Staffing staff )
		=> Assigned( ride, staff ) != 0 && staff.ById( ride.AssignedStaff ) is { } member
			&& (member.Model == StaffBehaviour.MechanicModel
				? member.ObjectToRepair == ride.ThingId && member.Activity == StaffActivity.Repairing
				: member.Model == StaffBehaviour.HandymanModel
					&& member.ToiletToClean == ride.ThingId && member.Activity == StaffActivity.Cleaning);

	/// <summary>Whether <c>FUN_004ad4e0</c> sets <c>b_callmech</c> down: the mechanic's statuses, <c>0x11</c> to <c>0x15</c>.</summary>
	internal static bool CallsTheMechanic( int status ) => status is >= 0x11 and <= 0x15;

	/// <summary>The UITEXT row of a status, the table at <c>0x0074fbc0</c> that <c>FUN_004861e0</c> letters; nought for none.</summary>
	internal static int Row( int status ) => status is > 0 and < 0x1b ? Rows[status] : 0;

	private static readonly int[] Rows =
		[0, 365, 366, 367, 368, 372, 370, 371, 369, 373, 374, 378, 376, 377, 375, 379, 380, 384, 382, 383, 381, 385, 388, 389, 390, 386, 387];

	/// <summary>A status's colour, <c>0x0074fb50</c> unpacked red from the low byte (<c>FUN_004861e0</c>).</summary>
	internal static UiColour Colour( int status ) => status switch
	{
		1 => new( 128, 128, 128 ),
		2 or 3 => new( 255, 50, 30 ),
		>= 4 and <= 0x15 => new( 80, 100, 255 ),
		>= 0x16 and <= 0x1a => new( 255, 150, 30 ),
		_ => UiColour.White
	};
}
