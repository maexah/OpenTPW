using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// The debug console's two guest instruments: <c>admit</c>, a guest made inside the park already deciding
/// (<see cref="ParkPeople.AdmitInside"/>), and <c>send</c>, a guest sent to a thing as if they had chosen it
/// (<see cref="PeepBehaviour.SendAsChosen"/>). What they must prove is that only the choice is skipped: the sent
/// guest walks and joins the queue through the guest's own states.
///
/// <para>
/// These read real game files and are skipped where there is no installation - see <see cref="GameData"/>.
/// </para>
/// </summary>
[TestClass]
public class ParkConsoleGuestTests
{
	private BaseFileSystem data = null!;

	[TestInitialize]
	public void MountTheGame() => FileSystem = data = GameData.Required();

	/// <summary>Thing 14, the stock park's Jungle Spray.</summary>
	private const int JungleSpray = 14;

	private ParkWorld World()
	{
		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		return new ParkWorld( new SaveReader( stream ).ReadFile() );
	}

	private ParkPeople People( ParkWorld world, ParkState state )
		=> new( world, new ParkBalance( "jungle", easyMode: true ), null, state, new ParkItemCatalogue( "jungle", data ) );

	private static void Done( ParkPeople people )
	{
		people.Delete();
		Entity.ApplyDeletions();
	}

	/// <summary>
	/// <b>A guest made by <c>admit</c> has come through the gate</b>: deciding, paid, numbered a visitor, of the kind
	/// asked for, on the cell asked for - where <see cref="ParkPeople.Admit"/> leaves one at the gate, unpaid.
	/// </summary>
	[TestMethod]
	public void AnAdmittedGuestStandsInsideDeciding()
	{
		var world = World();
		var state = new ParkState( world );
		var people = People( world, state );

		try
		{
			var id = people.AdmitInside( 55, 30, personType: 5 );
			var guest = people.Peeps.Single( peep => peep.ThingId == id );

			Assert.AreEqual( PeepState.Deciding, guest.State, "through the gate and deciding" );
			Assert.IsTrue( guest.PaidAdmission, "paid" );
			Assert.AreEqual( state.VisitorsToDate, guest.VisitorNumber, "the latest visitor" );
			Assert.AreEqual( 5, guest.PersonType, "the kind asked for" );
			Assert.AreEqual( (55, 30), people.WalkFor( id )!.Position.Cell, "where it was asked for" );
		}
		finally
		{
			Done( people );
		}
	}

	/// <summary>
	/// <b>A sent guest is aimed at the thing's back of queue and is going to it</b>, as the ride arm of Deciding
	/// leaves a guest who chose it: <see cref="Peep.MajorDest"/> the thing, the walk's target the middle of its
	/// back-of-queue cell.
	/// </summary>
	[TestMethod]
	public void ASentGuestIsGoingToTheThingsBackOfQueue()
	{
		var world = World();
		var state = new ParkState( world );
		var people = People( world, state );

		try
		{
			var id = people.AdmitInside( 55, 30, personType: 0 );

			Assert.IsNull( people.SendAsChosen( id, JungleSpray ), "the Spray can be reached from (55,30)" );

			var guest = people.Peeps.Single( peep => peep.ThingId == id );
			var spray = state.Objects.Single( thing => thing.ThingId == JungleSpray );
			var (back, _) = ParkRideChoice.QueueCellsFor( world, spray );

			Assert.AreEqual( PeepState.GoingToRide, guest.State, "going to it" );
			Assert.AreEqual( JungleSpray, guest.MajorDest, "named as what they chose" );
			Assert.AreEqual( MapStep.CellAt( back ), guest.Navigator.Target.Cell, "aimed at the back of its queue" );
		}
		finally
		{
			Done( people );
		}
	}

	/// <summary>
	/// <b>From there it is the guest's own path</b>: a saved guest set deciding, sent to the Spray and then only
	/// stepped, walks there and joins its queue. Nothing but <see cref="PeepBehaviour.Step"/> runs after the send.
	/// </summary>
	[TestMethod]
	public void ASentGuestWalksThereAndJoinsTheQueue()
	{
		var world = World();
		var state = new ParkState( world );
		var behaviour = new PeepBehaviour( world, new Random( 1 ),
			new ParkAdmission( new ParkBalance( "jungle", easyMode: true ), world.Economy!.Value.AdmissionFee ),
			() => ParkRides.GateIsOpen, state, new ParkItemCatalogue( "jungle", data ) );

		var blocked = CellEdge.For( world, ParkPeople.WalkingMode ).Blocked;
		var guest = ParkPeople.PeepsIn( world ).First( peep => !PeepBehaviour.HeldByAThing( peep.State ) );
		var walk = new PeepWalk( guest.Navigator, blocked );

		guest.SetState( PeepState.Deciding, 0, new Random( 1 ) );

		Assert.IsNull( behaviour.SendAsChosen( guest, walk, JungleSpray, tick: 1 ), "sent" );

		var joined = false;

		for ( var tick = 2; tick < 3000 && !joined; ++tick )
		{
			behaviour.Step( guest, walk, playing: null, tick );
			joined = guest.MajorDest == JungleSpray && PeepBehaviour.HeldByAThing( guest.State );
		}

		Assert.IsTrue( joined, $"never joined the Spray's queue: {guest.State}, major {guest.MajorDest}" );
		Assert.IsTrue( state.QueueLength( JungleSpray ) > 0, "and the Spray counts them" );
	}

	/// <summary>
	/// <b>Only a guest deciding or wandering is sent, and only to a thing the park has</b>: a guest in a queue keeps
	/// their place and their thing, and a thing id that names nothing leaves the guest as they were.
	/// </summary>
	[TestMethod]
	public void SendRefusesAHeldGuestAndANamelessThing()
	{
		var world = World();
		var state = new ParkState( world );
		var people = People( world, state );

		try
		{
			var id = people.AdmitInside( 55, 30, personType: 0 );
			var guest = people.Peeps.Single( peep => peep.ThingId == id );

			Assert.IsNotNull( people.SendAsChosen( id, 9999 ), "no thing 9999" );
			Assert.AreEqual( PeepState.Deciding, guest.State, "left deciding" );
			Assert.AreEqual( 0, guest.MajorDest, "naming nothing" );

			guest.SetState( PeepState.InQueue, 1, new Random( 1 ) );

			Assert.IsNotNull( people.SendAsChosen( id, JungleSpray ), "a queuer is not sent" );
			Assert.AreEqual( PeepState.InQueue, guest.State, "and stays queued" );

			Assert.IsNotNull( people.SendAsChosen( 9999, JungleSpray ), "no guest 9999" );
		}
		finally
		{
			Done( people );
		}
	}
}
