namespace OpenTPW.Tests;

/// <summary>
/// One park frame for the systems a test made, in <see cref="Level.Update"/>'s order: the park's loop takes the frame's ticks
/// (<see cref="ParkLoop"/>), then the systems do their once-a-frame work, the rides before the people as the level makes them.
/// </summary>
internal static class ParkFrame
{
	public static void Run( ParkPeople? people, ParkRides? rides = null )
	{
		ParkLoop.Run( null, rides, people );
		rides?.Update();
		people?.Update();
	}
}
