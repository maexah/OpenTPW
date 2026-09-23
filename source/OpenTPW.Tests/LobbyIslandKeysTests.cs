using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenTPW.Tests;

/// <summary>
/// The island keys wait for the park-entry fly-in, and the fly-in does not outlive the lobby: the original's arrow
/// handlers refuse while its camera is leaving (<c>0x005e1ee3</c>), and its leave state is a field of a camera
/// every lobby builds afresh. See <see cref="LobbyCameraMode.Step"/> and <see cref="LobbyCameraMode.ForgetIsland"/>.
///
/// <para>
/// <b>What these cannot see.</b> No test can build a lobby's islands, so a move that is not refused reaches an
/// empty list and goes nowhere; <see cref="LobbyCameraMode.Step"/> answers whether it refused, which is what these
/// ask. The one-line call to <see cref="LobbyCameraMode.IslandKeys"/> from <see cref="LobbyCameraMode.Update"/> is
/// reached only by a running lobby - the game run shows it. The flight itself is stepped by
/// <see cref="LobbyLeaveForParkTests"/>.
/// </para>
/// </summary>
[TestClass]
public class LobbyIslandKeysTests
{
	[TestInitialize]
	public void StartInOrbit()
	{
		// A refusal and a forgotten flight are both logged, and run alone this class is the first to want a logger.
		Log ??= new();

		LobbyCameraMode.ForgetIsland();
	}

	[TestCleanup]
	public void PutTheLobbyBack()
	{
		LobbyCameraMode.ForgetIsland();
		LobbyCameraMode.HeldToOneIsland = false;
		LobbyCameraMode.DebugOrbit = 0f;
	}

	/// <summary>
	/// <b>While the camera is leaving for a park, neither way round moves the island</b> - and before it left,
	/// the same request was taken, so the refusal is the leave's and not something else's.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> taking the leave test out of <see cref="LobbyCameraMode.Step"/> lets both requests through.
	/// </remarks>
	[TestMethod]
	public void TheIslandKeysAreRefusedWhileTheCameraIsLeaving()
	{
		Assert.IsTrue( LobbyCameraMode.Step( 1 ), "in orbit, a move is taken" );

		LobbyCameraMode.LeaveForPark( () => { } );

		Assert.IsFalse( LobbyCameraMode.Step( 1 ), "next is refused while leaving" );
		Assert.IsFalse( LobbyCameraMode.Step( -1 ), "and so is previous" );
		StringAssert.StartsWith( LobbyCameraMode.LeaveDescription(), "leave=Homing ", "and the refusals leave the flight alone" );
	}

	/// <summary>
	/// <b>The bracket keys ask through the same refusal</b>: pressed mid-flight, neither moves the island, where
	/// in orbit both are taken.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> answering either key in <see cref="LobbyCameraMode.IslandKeys"/> with a move of its own
	/// rather than <see cref="LobbyCameraMode.Step"/> - the shape the bug had - lets it through mid-flight.
	/// </remarks>
	[TestMethod]
	public void TheBracketKeysAreRefusedWhileTheCameraIsLeaving()
	{
		Assert.IsTrue( LobbyCameraMode.IslandKeys( next: true, previous: false ), "in orbit, ] is taken" );
		Assert.IsTrue( LobbyCameraMode.IslandKeys( next: false, previous: true ), "and so is [" );
		Assert.IsFalse( LobbyCameraMode.IslandKeys( next: false, previous: false ), "and with neither pressed, nothing moves" );

		LobbyCameraMode.LeaveForPark( () => { } );

		Assert.IsFalse( LobbyCameraMode.IslandKeys( next: true, previous: false ), "] is refused while leaving" );
		Assert.IsFalse( LobbyCameraMode.IslandKeys( next: false, previous: true ), "and so is [" );
	}

	/// <summary>An Instant Action game is held to its island: <see cref="LobbyCameraMode.Step"/> refuses both ways.</summary>
	[TestMethod]
	public void AnInstantActionGameIsStillHeldToItsIsland()
	{
		LobbyCameraMode.HeldToOneIsland = true;

		Assert.IsFalse( LobbyCameraMode.Step( 1 ) );
		Assert.IsFalse( LobbyCameraMode.Step( -1 ) );
	}

	/// <summary>
	/// <b>A lobby that ends mid-flight forgets the flight</b>: the next lobby starts in orbit, with no park waiting
	/// to be asked for, the island keys work again, and a new Enter is taken rather than refused as a second press.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> leaving the leave state, the park still waiting, or the angle out of
	/// <see cref="LobbyCameraMode.ForgetIsland"/> each turns this red.
	/// </remarks>
	[TestMethod]
	public void ALobbyEndedMidFlightForgetsTheFlight()
	{
		LobbyCameraMode.DebugOrbit = 2.6f;
		LobbyCameraMode.LeaveForPark( () => { } );

		Assert.AreEqual( "leave=Homing angle=2.600 radius=0.00 vertical=0.00 waiting=True",
			LobbyCameraMode.LeaveDescription(), "leaving, with a park to ask for" );

		LobbyCameraMode.ForgetIsland();

		Assert.AreEqual( "leave=No angle=0.000 radius=0.00 vertical=0.00 waiting=False",
			LobbyCameraMode.LeaveDescription(), "the lobby built next starts in orbit" );
		Assert.IsTrue( LobbyCameraMode.Step( 1 ), "and its island keys are taken" );

		LobbyCameraMode.DebugOrbit = 1f;
		LobbyCameraMode.LeaveForPark( () => { } );

		StringAssert.StartsWith( LobbyCameraMode.LeaveDescription(), "leave=Homing angle=1.000 ",
			"a new Enter starts a new flight from where the orbit is now" );
	}
}
