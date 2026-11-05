using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenTPW.Tests;

/// <summary>
/// A held balloon's per-frame placement - the wiring <see cref="ParkGuestSprites"/>' <c>DrawBalloons</c> makes round
/// <see cref="Balloon.Place"/>: the placed position is written into the balloon and the guest's last x and y, and the shared bob
/// is counted on by <see cref="Balloon.PlacementsPerSecond"/> times the frame's delta. <c>DrawBalloons</c> draws through <c>WriteQuad</c>,
/// which needs an atlas, so these read the placement it calls.
/// </summary>
[TestClass]
public class ParkBalloonPlacementTests
{
	private static Peep Guest( int thingId, float lastX, float lastY )
	{
		var peep = new Peep( thingId, new ParkWorld.GuestState(
			State: (int)PeepState.Deciding, SavedState: (int)PeepState.Deciding, PersonType: 0,
			Cash: 300, ExitLevel: 100, Happiness: 50f, Thirst: 10f, Hunger: 10f, Toilet: 10f,
			Vomit: 0f, Litter: 0f, MajorDest: 0, QueuePos: 0, PrankeryIndex: 0 ),
			new ParkWorld.NavigatorState(
				X: 0, Y: 0, VelocityX: 0, VelocityY: 0, TargetX: 0, TargetY: 0,
				Mass: ParkWorld.NavigatorState.DefaultMass, Radius: ParkWorld.NavigatorState.DefaultRadius,
				MaxForce: 0, MaxSpeed: 0, NavMode: 0, CantReachDest: 0, PathFinished: false,
				PathCount: 0, PathTotalCount: 0, PathBufferCount: 0,
				BufferedDistance: 0, TailDistance: 0, TotalDistance: 0, StuckBits: 0 ) );

		(peep.BalloonLastX, peep.BalloonLastY) = (lastX, lastY);

		return peep;
	}

	/// <summary>
	/// A guest walking from cell (10, 20) to (12, 24), half through the sweep, on cells of 3 by 4: the balloon goes where the LAST frame's
	/// trailing sample was (the guest's last x and y, 111 and 222), the guest's last x and y take this frame's trailing sample (the
	/// walk at 0.15 of the way: (10.3, 20.6) cells, 30.9 and 82.4), and the height is one, less twice the two samples' distance apart
	/// ((2.1 + 5.6) / 0.5 = 15.4), plus 1.5 of row (7 + 5) % 20 of the bob, 0.16.
	/// </summary>
	/// <remarks><b>Mutations:</b> the balloon not written; the guest's last x and y not written, or written the balloon's; the height not written;
	/// the phase passed as nought.</remarks>
	[TestMethod]
	public void APlacedBalloonGoesToTheLastFramesSampleAndThePeepKeepsThisFrames()
	{
		var peep = Guest( 7, 111f, 222f );
		var held = Balloon.Make( 7, 4, 0 )!;

		ParkGuestSprites.PlaceHeld( peep, held, FixedVector.AtCell( 10, 20 ), FixedVector.AtCell( 12, 24 ), 0.5f, 3f, 4f,
			phase: 5, count: 0f, delta: 0f );

		Assert.AreEqual( (111f, 222f), (held.X, held.Y), "where the last frame's trailing sample was" );
		Assert.AreEqual( 1f - 15.4f + 0.24f, held.Height, 1e-3f );
		Assert.AreEqual( 30.9f, peep.BalloonLastX, 1e-3f, "this frame's trailing sample, for the next" );
		Assert.AreEqual( 82.4f, peep.BalloonLastY, 1e-3f );
	}

	/// <summary>
	/// The shared bob counts <see cref="Balloon.PlacementsPerSecond"/> (30) placements a second of the frame's delta, and steps its phase
	/// once each <see cref="Balloon.PlacementsPerBob"/> (11): 0.1 s is 3, so a count of 9 steps the phase and keeps 1 and a count of 7 keeps
	/// 10; a frame of no time moves nothing; 0.8 s is 24 and steps twice.
	/// </summary>
	/// <remarks><b>Mutations:</b> the rate 60 or 1; the delta left out; the phase not advanced; one step at most.</remarks>
	[TestMethod]
	[DataRow( 0.1f, 9f, 5, 6, 1f )]
	[DataRow( 0.1f, 7f, 5, 5, 10f )]
	[DataRow( 0f, 7f, 5, 5, 7f )]
	[DataRow( 0.8f, 0f, 5, 7, 2f )]
	public void TheSharedBobIsCountedOnBySecondsOfFrame( float delta, float count, int phase, int expectedPhase, float expectedCount )
	{
		var peep = Guest( 7, 0f, 0f );
		var held = Balloon.Make( 7, 4, 0 )!;

		var after = ParkGuestSprites.PlaceHeld( peep, held, FixedVector.AtCell( 10, 20 ), FixedVector.AtCell( 12, 24 ), 0.5f, 3f, 4f,
			phase, count, delta );

		Assert.AreEqual( expectedPhase, after.Phase );
		Assert.AreEqual( expectedCount, after.Count, 1e-3f );
	}

	/// <summary>A guest's balloon bobs at the row of the bob table its id and the phase pick, so two phases give two heights.</summary>
	/// <remarks><b>Mutation:</b> the phase passed to <see cref="Balloon.Place"/> being the count, or nought.</remarks>
	[TestMethod]
	public void TheBalloonBobsAtTheSharedPhase()
	{
		float HeightAt( int phase )
		{
			var held = Balloon.Make( 7, 4, 0 )!;

			ParkGuestSprites.PlaceHeld( Guest( 7, 0f, 0f ), held, FixedVector.AtCell( 10, 20 ), FixedVector.AtCell( 10, 20 ), 0.5f, 3f, 4f,
				phase, 0f, 0f );

			return held.Height;
		}

		// A guest standing still has the two samples together: one, plus 1.5 of the row, (7 + phase) % 20 of the bob table.
		Assert.AreEqual( 1f + 1.5f * 0.16f, HeightAt( 5 ), 1e-4f, "row 12" );
		Assert.AreEqual( 1f + 1.5f * 0.2f, HeightAt( 3 ), 1e-4f, "row 10, the table's top" );
	}
}
