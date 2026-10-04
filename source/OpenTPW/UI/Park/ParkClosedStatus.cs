namespace OpenTPW.UI;

/// <summary>
/// Closed statuses from FUN_00485f60 and colours from 0x0074fb50; docs/exe/ride-window-door.md.
/// The other object-status arms are not represented here.
/// </summary>
internal static class ParkClosedStatus
{
	internal static int For( IParkInitialState? park, ParkWorld.CatalogueObject ride, int trackType = 0 )
	{
		if ( ride.CanLoad != 0 )
			return 0;

		// These statuses precede ordinary closure in FUN_00485f60. Their detailed messages are
		// not built here; retain the existing display rather than overwrite them with CLOSED.
		if ( ride.State is 1 or 2 or 4 || trackType != 0 )
		{
			Unimplemented.Report( "OBJECT_WINDOW_HIGHER_PRIORITY_STATUS" );
			return 0;
		}

		if ( ride.HasQueuePath && !ParkRideOperation.BackOfQueueConnected( park, ride ) )
			return 23;

		// The exit-disconnected arm (status 24) precedes ordinary closure and is not decoded here.
		Unimplemented.Report( "OBJECT_WINDOW_EXIT_CONNECTION_STATUS" );
		return 1;
	}

	internal static UiColour Colour( int status ) => status switch
	{
		1 => new( 128, 128, 128 ),
		23 => new( 255, 150, 30 ),
		_ => UiColour.White
	};
}
