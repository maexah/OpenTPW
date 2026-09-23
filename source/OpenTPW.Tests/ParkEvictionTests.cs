using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// What selling a thing does to the people bound to it - the object destructor's type-10 message, which a
/// guest answers through <c>FUN_004fb360</c> and a member of staff through <c>FUN_00504c70</c>. See
/// <c>docs/exe/park-engine.md</c>, "Selling and the people on it".
///
/// <para>
/// Every guest here is one of Lost Kingdom's own, put into the state under test by hand and given
/// happiness 50, so each number is the balance file's: <c>SmallHappinessChange</c> 5 and
/// <c>MediumHappinessChange</c> 15. The sale goes through <see cref="ParkBuilding.Sell"/>, the path the
/// console and the object window take, so the wiring is tested with the answer.
/// </para>
/// <para>
/// These read real game files and are skipped where there is no installation - see <see cref="GameData"/>.
/// </para>
/// </summary>
[TestClass]
public class ParkEvictionTests
{
	private BaseFileSystem data = null!;

	[TestInitialize]
	public void MountTheGame() => FileSystem = data = GameData.Required();

	private const string Theme = "jungle";

	private const int BellyBounce = 13;

	private const int JungleSpray = 14;

	private const int StaffRoom = 20;

	private const int Guard = 28;

	private const int Researcher = 30;

	private const float Before = 50f;

	/// <summary>A rider's loss: <c>SmallHappinessChange</c>.</summary>
	private const float AfterRiding = 45f;

	/// <summary>A queuer's loss: <c>MediumHappinessChange</c> in <c>FUN_005012f0</c>, then the small one.</summary>
	private const float AfterQueueing = 30f;

	private sealed record Park( ParkWorld World, ParkState State, ParkItemCatalogue Catalogue, ParkPeople People );

	private Park Open()
	{
		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		var world = new ParkWorld( new SaveReader( stream ).ReadFile() );

		var state = new ParkState( world );
		var catalogue = new ParkItemCatalogue( Theme, data );
		var people = new ParkPeople( world, new ParkBalance( Theme, easyMode: true ), gateStatus: null, state,
			catalogue );

		return new( world, state, catalogue, people );
	}

	private static void Close( Park park )
	{
		park.People.Delete();
		Entity.ApplyDeletions();
	}

	private static string Sell( Park park, int thingId )
		=> ParkBuilding.Sell( park.State, park.World, park.Catalogue, null, null, thingId, park.People );

	/// <summary>Puts one of the park's guests into <paramref name="state"/>, bound for <paramref name="thing"/>.</summary>
	private static Peep Bound( Park park, int index, int thing, PeepState state )
	{
		var peep = park.People.Peeps[index];

		peep.MajorDest = thing;
		peep.Happiness = Before;
		peep.SetState( state, tick: 1, new Random( 1 ) );

		return peep;
	}

	/// <summary>Puts one of the park's guests in <paramref name="thing"/>'s queue, standing in it.</summary>
	private static Peep Queueing( Park park, int index, int thing, PeepState state = PeepState.InQueue )
	{
		var peep = Bound( park, index, thing, state );

		peep.QueuePos = park.State.JoinQueue( thing, peep.ThingId );

		return peep;
	}

	/// <summary>
	/// A rider is put off where they stand, loses the small change and goes to deciding. Nothing moves
	/// them: the original writes no position (<c>FUN_004fb360</c>, <c>0x004fb38d</c>..<c>0x004fb4a1</c>).
	/// </summary>
	[TestMethod]
	public void ARiderIsPutOffWhereTheyAreAndLosesTheSmallChange()
	{
		var park = Open();

		try
		{
			var rider = Bound( park, 0, BellyBounce, PeepState.Riding );
			var where = rider.Navigator.Position;

			StringAssert.Contains( Sell( park, BellyBounce ), "sold" );

			Assert.AreEqual( PeepState.Deciding, rider.State, "a rider of a sold thing thinks again" );
			Assert.AreEqual( 0, rider.MajorDest, "and names it no longer" );
			Assert.AreEqual( AfterRiding, rider.Happiness, 0.001f, "and loses SmallHappinessChange, 5" );
			Assert.AreEqual( where, rider.Navigator.Position, "and is not moved" );
		}
		finally
		{
			Close( park );
		}
	}

	/// <summary>
	/// Everybody in the queue is put out of it: both happiness changes, their own links gone, the invitation
	/// and the place in the queue cleared - <c>FUN_005012f0</c>, then the small change.
	/// </summary>
	[TestMethod]
	public void EveryQueuerIsPutOutOfTheQueueAndLosesBothChanges()
	{
		var park = Open();

		try
		{
			var head = Queueing( park, 1, BellyBounce );
			var behind = Queueing( park, 2, BellyBounce );

			head.BeenAdmitted = true;

			Assert.AreEqual( 2, park.State.QueueLength( BellyBounce ), "two guests queue before the sale" );
			Assert.AreEqual( 1, behind.QueuePos, "the second stands one back" );

			Sell( park, BellyBounce );

			foreach ( var queuer in new[] { head, behind } )
			{
				Assert.AreEqual( PeepState.Deciding, queuer.State, $"guest {queuer.ThingId} leaves the queue" );
				Assert.AreEqual( 0, queuer.MajorDest, $"guest {queuer.ThingId} names the thing no longer" );
				Assert.AreEqual( AfterQueueing, queuer.Happiness, 0.001f,
					$"guest {queuer.ThingId} loses MediumHappinessChange and then SmallHappinessChange" );
				Assert.AreEqual( 0, queuer.QueuePos, $"guest {queuer.ThingId} has no place in a queue" );
				Assert.IsFalse( queuer.BeenAdmitted, $"guest {queuer.ThingId} holds no invitation" );
				Assert.AreEqual( 0, park.State.NextInQueue( queuer.ThingId ), $"guest {queuer.ThingId} links forward to nobody" );
				Assert.AreEqual( 0, park.State.PreviousInQueue( queuer.ThingId ), $"guest {queuer.ThingId} links back to nobody" );
			}
		}
		finally
		{
			Close( park );
		}
	}

	/// <summary>
	/// "Queueing" is <c>FUN_00502430</c>'s: the four queue states, and a spot animation played from one.
	/// </summary>
	[TestMethod]
	[DataRow( PeepState.SteppingUpQueue )]
	[DataRow( PeepState.BeingAdmitted )]
	[DataRow( PeepState.EnteringRide )]
	public void EachQueueingStateLosesBothChanges( PeepState state )
	{
		var park = Open();

		try
		{
			var queuer = Queueing( park, 1, BellyBounce, state );

			Sell( park, BellyBounce );

			Assert.AreEqual( AfterQueueing, queuer.Happiness, 0.001f, $"{state} is queueing" );
			Assert.AreEqual( PeepState.Deciding, queuer.State );
		}
		finally
		{
			Close( park );
		}
	}

	/// <summary>The spot-animation case: state 8 is judged by the state it was played from.</summary>
	[TestMethod]
	public void ASpotAnimationPlayedInTheQueueCountsAsQueueing()
	{
		var park = Open();

		try
		{
			var queuer = Queueing( park, 1, BellyBounce );

			// Somebody behind them, so they have a link to lose.
			Queueing( park, 2, BellyBounce );

			queuer.SavedState = PeepState.InQueue;
			queuer.SetState( PeepState.PlayingSpotAnimation, tick: 1, new Random( 1 ) );

			Assert.AreNotEqual( 0, park.State.NextInQueue( queuer.ThingId ), "they link to the guest behind" );

			Sell( park, BellyBounce );

			Assert.AreEqual( AfterQueueing, queuer.Happiness, 0.001f, "the saved state decides" );
			Assert.AreEqual( 0, park.State.NextInQueue( queuer.ThingId ), "and they are out of the queue" );
		}
		finally
		{
			Close( park );
		}
	}

	/// <summary>
	/// Only the destination chooses, not the state: a guest walking to it or away from it is stopped too,
	/// and loses only the small change.
	/// </summary>
	[TestMethod]
	[DataRow( PeepState.GoingToRide )]
	[DataRow( PeepState.LeavingRide )]
	[DataRow( PeepState.Wandering )]
	public void AGuestWhoStillNamesItLosesTheSmallChange( PeepState state )
	{
		var park = Open();

		try
		{
			var guest = Bound( park, 3, BellyBounce, state );

			Sell( park, BellyBounce );

			Assert.AreEqual( PeepState.Deciding, guest.State, $"{state} with the thing as destination thinks again" );
			Assert.AreEqual( 0, guest.MajorDest );
			Assert.AreEqual( AfterRiding, guest.Happiness, 0.001f, "and loses SmallHappinessChange alone" );
		}
		finally
		{
			Close( park );
		}
	}

	/// <summary>A guest bound for something else hears the message and does nothing.</summary>
	[TestMethod]
	public void AGuestBoundForSomethingElseIsLeftAlone()
	{
		var park = Open();

		try
		{
			var elsewhere = Queueing( park, 4, JungleSpray );

			Sell( park, BellyBounce );

			Assert.AreEqual( PeepState.InQueue, elsewhere.State, "still queueing for the Jungle Spray" );
			Assert.AreEqual( JungleSpray, elsewhere.MajorDest );
			Assert.AreEqual( Before, elsewhere.Happiness, 0.001f, "and no less happy" );
			Assert.AreEqual( elsewhere.ThingId, park.State.FirstInQueue( JungleSpray ), "and still in its queue" );
		}
		finally
		{
			Close( park );
		}
	}

	/// <summary>
	/// Moving a thing is demolishing it and buying it again, and the demolish tells the park's people too.
	/// </summary>
	[TestMethod]
	public void PickingAThingUpToMoveItPutsItsRiderOff()
	{
		var park = Open();

		try
		{
			var rider = Bound( park, 0, BellyBounce, PeepState.Riding );

			// Onto the staff room's own cell, which refuses, so the thing stays in the hand.
			ParkBuilding.Move( park.State, park.World, park.Catalogue, null, null, BellyBounce, 58, 16,
				people: park.People );

			Assert.AreEqual( PeepState.Deciding, rider.State, "the pickup's demolish puts the rider off" );
			Assert.AreEqual( AfterRiding, rider.Happiness, 0.001f );
		}
		finally
		{
			ParkBuilding.Drop();
			Close( park );
		}
	}

	/// <summary>
	/// A member of staff resting in a sold rest area is put out and stands idle, and one on the way there
	/// gives it up - <c>FUN_00504c70</c>. With no other rest area in the park, the resting one claims none.
	/// </summary>
	[TestMethod]
	public void StaffRestingInASoldRestAreaAreSentAway()
	{
		var park = Open();

		try
		{
			var resting = park.People.Staff.Single( staff => staff.ThingId == Guard );
			var going = park.People.Staff.Single( staff => staff.ThingId == Researcher );

			resting.RestArea = StaffRoom;
			resting.SetActivity( StaffActivity.Resting, tick: 1 );
			going.RestArea = StaffRoom;
			going.SetActivity( StaffActivity.GoingToRest, tick: 1 );

			StringAssert.Contains( Sell( park, StaffRoom ), "sold" );

			Assert.AreEqual( StaffActivity.Idle, resting.Activity, "the resting guard stands up" );
			Assert.AreEqual( 0, resting.RestArea, "and with nowhere else to rest claims nothing" );
			Assert.AreEqual( StaffActivity.Idle, going.Activity, "the researcher on the way stops" );
			Assert.AreEqual( 0, going.RestArea, "and gives the claim up" );
		}
		finally
		{
			Close( park );
		}
	}

	/// <summary>
	/// Put out of a sold rest area with another standing, a resting member of staff claims the nearest and
	/// is routed to its entry - but stays idle, which is the original's own arm (<c>0x00504d8f</c>).
	/// </summary>
	[TestMethod]
	public void StaffPutOutClaimTheNearestOtherRestAreaAndStayIdle()
	{
		var park = Open();

		try
		{
			// A second staff room: the first's record where the Jungle Spray stands, approached from the
			// spray's entry, which guests reach. A bought one would carry no rest-area bit.
			Assert.IsTrue( park.State.TryObject( StaffRoom, out var room ) );
			Assert.IsTrue( park.State.TryObject( JungleSpray, out var spray ) );

			var other = room with
			{
				ThingId = park.State.NextThingId(),
				RawX = spray.RawX, RawY = spray.RawY,
				EntryPos = spray.EntryPos,
				ExitPos = spray.ExitPos
			};

			park.State.AddObject( other );

			var resting = park.People.Staff.Single( staff => staff.ThingId == Guard );

			resting.RestArea = StaffRoom;
			resting.SetActivity( StaffActivity.Resting, tick: 1 );

			Sell( park, StaffRoom );

			Assert.AreEqual( StaffActivity.Idle, resting.Activity, "put out, they stand idle" );
			Assert.AreEqual( other.ThingId, resting.RestArea, "claiming the other rest area" );
			Assert.AreEqual( (spray.EntryCellX, spray.EntryCellY), resting.Navigator.Target.Cell, "routed to its entry" );
		}
		finally
		{
			Close( park );
		}
	}

	/// <summary>
	/// A tired member of staff is sent to a rest area standing now, never to one sold: the search walks the
	/// park's live objects, as <c>FUN_00506910</c> walks <c>mFirstObject</c>, not the save.
	/// </summary>
	/// <remarks>
	/// The second rest area stands only in the running park, so a search of the save finds the sold one
	/// alone - and its entry, an entrance cell the sale left bare, cannot be routed to, so that search ends
	/// with nobody resting. The one standing is where they must go.
	/// </remarks>
	[TestMethod]
	public void ATiredMemberOfStaffIsSentToARestAreaStandingNotToOneSold()
	{
		var park = Open();

		try
		{
			Assert.IsTrue( park.State.TryObject( StaffRoom, out var room ) );
			Assert.IsTrue( park.State.TryObject( JungleSpray, out var spray ) );

			var other = room with
			{
				ThingId = park.State.NextThingId(),
				RawX = spray.RawX, RawY = spray.RawY,
				EntryPos = spray.EntryPos,
				ExitPos = spray.ExitPos
			};

			park.State.AddObject( other );

			Sell( park, StaffRoom );

			var tired = park.People.Staff.Single( staff => staff.ThingId == Guard );

			tired.Tiredness = 0f;
			tired.SetActivity( StaffActivity.Idle, tick: 1 );
			tired.TimeStartedIdling = 0;

			var behaviour = new StaffBehaviour( new ParkBalance( Theme, easyMode: true ), new Random( 7 ), park.State );
			var walk = park.People.StaffWalkFor( Guard )!;

			behaviour.Step( tired, walk, playing: null, tick: 500 );

			Assert.AreEqual( StaffActivity.GoingToRest, tired.Activity, "a tired guard sets off to rest" );
			Assert.AreEqual( other.ThingId, tired.RestArea, "at the rest area standing, not the one sold" );
		}
		finally
		{
			Close( park );
		}
	}
}
