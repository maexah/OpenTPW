namespace OpenTPW.UI;

/// <summary>
/// The statuses of FUN_00485f60 that the door and the queue decide, and their colours from 0x0074fb50;
/// docs/exe/ride-window-door.md. The other object-status arms are not represented here.
/// </summary>
internal static class ParkClosedStatus
{
	internal static int For( IParkInitialState? park, ParkWorld.CatalogueObject ride, int trackType = 0 )
	{
		// These statuses precede the queue's and the door's in FUN_00485f60. Their messages are
		// not built here; a closed ride that has one keeps the display it had.
		if ( ride.State is 1 or 2 or 4 || trackType != 0 )
		{
			if ( ride.CanLoad == 0 )
				Unimplemented.Report( "OBJECT_WINDOW_HIGHER_PRIORITY_STATUS" );

			return 0;
		}

		// 22 for a ride still open, 23 for one closed (0x16 + FUN_004e0440): a queue cut off from the
		// path does not close its ride.
		if ( ride.HasQueuePath && !ParkRideOperation.BackOfQueueConnected( park, ride ) )
			return ride.CanLoad == 0 ? 23 : 22;

		if ( ride.CanLoad != 0 )
			return 0;

		// The exit-disconnected arm (status 24) precedes ordinary closure and is not decoded here.
		Unimplemented.Report( "OBJECT_WINDOW_EXIT_CONNECTION_STATUS" );
		return 1;
	}

	internal static UiColour Colour( int status ) => status switch
	{
		1 => new( 128, 128, 128 ),
		22 or 23 => new( 255, 150, 30 ),
		_ => UiColour.White
	};
}
