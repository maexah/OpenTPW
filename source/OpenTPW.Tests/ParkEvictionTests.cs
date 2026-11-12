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
	/// Every queuer the sale reaches is put out of the queue: both happiness changes, their own links gone, the
	/// invitation and the place in the queue cleared - <c>FUN_005012f0</c>, then the small change. For the Belly
	/// Bounce the drain before it has already put out every queuer from the fifth place back, but the nominee and
	/// one in raw state 14, for the medium change alone (<c>docs/exe/ride-operation.md</c>, "The sale's drain").
	/// </summary>
	[TestMethod]
	public void TheTwoQueuersTheDrainLeavesArePutOutOfTheQueueAndLoseBothChanges()
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
	/// Each dock is held at nought, as <c>FUN_004fb4f0</c> and the clamp inlined in <c>FUN_004fea70</c> hold
	/// it: a rider with 3 left and a queuer with 12 both end at nought, not below.
	/// </summary>
	[TestMethod]
	public void AGuestWithLittleHappinessLeftIsHeldAtNought()
	{
		var park = Open();

		try
		{
			var rider = Bound( park, 0, BellyBounce, PeepState.Riding );
			var queuer = Queueing( park, 1, BellyBounce );

			rider.Happiness = 3f;
			queuer.Happiness = 12f;

			Sell( park, BellyBounce );

			Assert.AreEqual( 0f, rider.Happiness, "3 less 5, held at nought" );
			Assert.AreEqual( 0f, queuer.Happiness, "12 less 15, held at nought, less 5, held again" );
		}
		finally
		{
			Close( park );
		}
	}

	/// <summary>
	/// A guest's choice lets go of whatever they named first, chosen or not (<c>FUN_004fcb10</c>,
	/// <c>0x004fcb21</c>) - so a guest who left a ride and then failed to choose is no longer bound to it,
	/// and a sale of it later passes them by, as in the original.
	/// </summary>
	[TestMethod]
	public void AFailedChoiceLetsGoOfWhatTheyNamedBefore()
	{
		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		var world = new ParkWorld( new SaveReader( stream ).ReadFile() );
		var admission = new ParkAdmission( new ParkBalance( Theme, easyMode: true ), world.Economy!.Value.AdmissionFee );

		// A seed whose first roll is the ride arm, and a behaviour with no park, so the choice finds nothing.
		var seed = Enumerable.Range( 0, 100 ).First( candidate => new Random( candidate ).Next() % 3 == 0 );
		var behaviour = new PeepBehaviour( parkIsClosed: false, world.NumberOfVisitorsToDate, new Random( seed ),
			admission );

		var guest = ParkPeople.PeepsIn( world ).First();
		var walk = new PeepWalk( guest.Navigator, CellEdge.For( world, ParkPeople.WalkingMode ).Blocked );

		guest.SetState( PeepState.Deciding, tick: 1, new Random( 1 ) );
		guest.MajorDest = BellyBounce;
		guest.TimeStartedIdling = 0;

		behaviour.Step( guest, walk, playing: null, tick: 100 );

		Assert.AreEqual( PeepState.PlayingSpotAnimation, guest.State, "nothing was chosen, so the empty hand's animation" );
		Assert.AreEqual( PeepState.Deciding, guest.SavedState );
		Assert.AreEqual( 0, guest.MajorDest, "and the ride they named before is let go of" );
	}

	/// <summary>
	/// Which arm each guest takes, which decides the sound: riding is state 16 exactly, queueing is
	/// <c>FUN_00502430</c>'s, anyone else still naming the thing is heading, and a guest naming something else
	/// takes none.
	/// </summary>
	[TestMethod]
	[DataRow( PeepState.Riding, BellyBounce, (int)PeepBehaviour.PutOff.Riding )]
	[DataRow( PeepState.InQueue, BellyBounce, (int)PeepBehaviour.PutOff.Queueing )]
	[DataRow( PeepState.BeingAdmitted, BellyBounce, (int)PeepBehaviour.PutOff.Queueing )]
	[DataRow( PeepState.GoingToRide, BellyBounce, (int)PeepBehaviour.PutOff.Heading )]
	[DataRow( PeepState.Riding, JungleSpray, (int)PeepBehaviour.PutOff.No )]
	public void EachGuestTakesTheArmTheirStateCallsFor( PeepState state, int boundFor, int expected )
	{
		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		var world = new ParkWorld( new SaveReader( stream ).ReadFile() );
		var behaviour = new PeepBehaviour( parkIsClosed: false, world.NumberOfVisitorsToDate, new Random( 1 ) );

		var guest = ParkPeople.PeepsIn( world ).First();

		guest.MajorDest = boundFor;
		guest.SetState( state, tick: 1, new Random( 1 ) );

		Assert.AreEqual( (PeepBehaviour.PutOff)expected, behaviour.ThingRemoved( guest, BellyBounce, tick: 2 ) );
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

	/// <summary>
	/// The rest-area arm answers only when the member's rest area (<c>+0x208</c>) is the thing removed -
	/// <c>FUN_00504c70</c>, <c>docs/exe/park-engine.md</c>, "Selling and the people on it": "Then, when the rest
	/// area is the thing". So with the Belly Bounce sold, a guard resting in the staff room stays in state 3 with
	/// the claim on thing 20, and a researcher on the way there stays in state 2 with the same claim.
	/// </summary>
	[TestMethod]
	public void StaffRestingSomewhereElseKeepTheirRestAreaWhenAnotherThingIsSold()
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

			StringAssert.Contains( Sell( park, BellyBounce ), "sold" );

			Assert.AreEqual( StaffActivity.Resting, resting.Activity, "the guard rests on: the staff room still stands" );
			Assert.AreEqual( StaffRoom, resting.RestArea, "and keeps the claim on it" );
			Assert.AreEqual( StaffActivity.GoingToRest, going.Activity, "the researcher is still on the way" );
			Assert.AreEqual( StaffRoom, going.RestArea, "to the same rest area" );
		}
		finally
		{
			Close( park );
		}
	}

	/// <summary>
	/// The sale's message makes the rider's sound: the kids' effect <c>0x80</c>, always for a rider
	/// (<c>FUN_004faa00</c> at <c>0x004fb3f5</c>), and for nobody who was only walking to the thing. On a thing
	/// whose flags lack <c>0x20</c> admission destroyed the rider's sprite and the eviction makes a new one
	/// whose position is still nought when the sound reads it (<c>0x004fb3cd</c>), so it plays at the world's
	/// origin, (0, 0, 0) - <c>docs/exe/park-engine.md</c>, "Selling and the people on it". One rider and one
	/// guest heading there are therefore one voice, placed at nought, with the put-off's own range of 100.
	/// </summary>
	/// <remarks>
	/// Every visitable thing in Lost Kingdom's save carries <c>0x20</c>, so the Belly Bounce's is taken off by
	/// hand. A device is stood in as <see cref="ParkVoiceRangeTests"/> stands one in. No ground is loaded, so
	/// nothing here can be heard at anybody's feet. <b>Mutation:</b> turning the test that passes by a guest
	/// the message did not choose (<see cref="ParkPeople.ThingRemoved"/>) leaves the sale silent.
	/// </remarks>
	[TestMethod]
	public void ARiderPutOffAThingThatKeepsNoSpriteIsHeardOnceAtTheWorldsOrigin()
	{
		var ready = typeof( Audio ).GetProperty( nameof( Audio.Ready ) )!;

		Assert.IsFalse( Audio.Ready, "a test run has no audio device" );

		System.Collections.Generic.List<Voice> before;

		lock ( Audio.Lock )
			before = [.. Audio.Voices];

		ParkAudio? audio = null;
		Park? park = null;

		try
		{
			ready.SetValue( null, true );
			audio = new ParkAudio( Theme );
			park = Open();

			Assert.IsTrue( park.State.TryObject( BellyBounce, out var bounce ) );
			Assert.AreNotEqual( 0, bounce.Flags & ParkWorld.CatalogueObject.KeepsRidersSpriteFlag,
				"the save's Belly Bounce keeps its riders' sprites" );
			Assert.IsTrue( park.State.ReplaceObject( bounce with
			{
				Flags = (ushort)( bounce.Flags & ~ParkWorld.CatalogueObject.KeepsRidersSpriteFlag )
			} ) );

			var rider = Bound( park, 0, BellyBounce, PeepState.Riding );
			var heading = Bound( park, 3, BellyBounce, PeepState.GoingToRide );

			System.Collections.Generic.List<Voice> quiet;

			lock ( Audio.Lock )
				quiet = [.. Audio.Voices];

			StringAssert.Contains( Sell( park, BellyBounce ), "sold" );

			System.Collections.Generic.List<Voice> heard;

			lock ( Audio.Lock )
				heard = Audio.Voices.Where( voice => !quiet.Contains( voice ) ).ToList();

			Assert.AreEqual( PeepState.Deciding, rider.State, "the rider was put off" );
			Assert.AreEqual( PeepState.Deciding, heading.State, "and so was the guest on the way" );
			Assert.AreEqual( 1, heard.Count, "the rider is heard, and the guest who was only heading there is not" );
			Assert.AreEqual( System.Numerics.Vector3.Zero, heard[0].Place, "at the world's origin, where the new sprite still is" );
			Assert.AreEqual( 100f, heard[0].Range, "the kids' effect 0x80, by its variation's range" );
		}
		finally
		{
			if ( park != null )
				Close( park );

			audio?.Delete();
			Entity.ApplyDeletions();

			lock ( Audio.Lock )
				Audio.Voices.RemoveAll( voice => !before.Contains( voice ) );

			ready.SetValue( null, false );
		}
	}

	/// <summary>
	/// Of several rest areas a tired member of staff claims the nearest, by the squared distance between cells -
	/// across squared plus down squared, each measured from the cell they stand in (<c>FUN_00506910</c>;
	/// <see cref="StaffBehaviour"/>'s <c>GoAndRest</c> states the rule).
	/// </summary>
	/// <remarks>
	/// The guard stands at (39,28) and the park's own staff room at (58,16): 19 across and 12 up, 361 + 144 = 505.
	/// Three copies of it are stood about the guard, each approached from the staff room's own entry, which the
	/// guard can route to: one 2 across and 2 down (4 + 4 = 8), one 1 back and 4 down (1 + 16 = 17), one 4 across
	/// and 1 up (16 + 1 = 17). 8 is the least, so that copy is claimed. Each of the others is the nearest under
	/// one wrong measure: the second where the down distance does not count or is taken away, or where the
	/// column is added rather than subtracted; the third where the across distance does not count; the staff
	/// room itself where the row is added.
	/// </remarks>
	[TestMethod]
	public void ATiredGuardClaimsTheRestAreaNearestBySquaredCellDistance()
	{
		var park = Open();

		try
		{
			Assert.IsTrue( park.State.TryObject( StaffRoom, out var room ) );

			var tired = park.People.Staff.Single( staff => staff.ThingId == Guard );
			var walk = park.People.StaffWalkFor( Guard )!;
			var (x, y) = walk.Position.Cell;

			Assert.AreEqual( (39, 28), (x, y), "where the guard stands in the shipped park" );
			Assert.AreEqual( (58, 16), (room.CellX, room.CellY), "and the staff room, 505 away" );

			var downAndBack = room with { ThingId = park.State.NextThingId(), RawX = (x - 1) << 8, RawY = (y + 4) << 8 };
			var near = room with { ThingId = park.State.NextThingId(), RawX = (x + 2) << 8, RawY = (y + 2) << 8 };
			var acrossAndUp = room with { ThingId = park.State.NextThingId(), RawX = (x + 4) << 8, RawY = (y - 1) << 8 };

			park.State.AddObject( downAndBack );
			park.State.AddObject( near );
			park.State.AddObject( acrossAndUp );

			tired.Tiredness = 0f;
			tired.SetActivity( StaffActivity.Idle, tick: 1 );
			tired.TimeStartedIdling = 0;

			var behaviour = new StaffBehaviour( new ParkBalance( Theme, easyMode: true ), new Random( 7 ), park.State );

			behaviour.Step( tired, walk, playing: null, tick: 500 );

			Assert.AreEqual( StaffActivity.GoingToRest, tired.Activity, "a tired guard sets off to rest" );
			Assert.AreEqual( near.ThingId, tired.RestArea,
				"at the rest area 8 away by squared cells, not at either one 17 away nor the staff room 505 away" );
		}
		finally
		{
			Close( park );
		}
	}

	/// <summary>
	/// Of two rest areas equally near, a tired member of staff claims the first in chain order: the original
	/// follows <c>mNext</c> from <c>mFirstObject</c> and replaces its choice only for one strictly nearer
	/// (<c>FUN_00506910</c>; <see cref="StaffBehaviour"/>'s <c>GoAndRest</c> states the rule).
	/// </summary>
	/// <remarks>
	/// Two copies of the staff room are stood about the guard, one 2 across and 3 down and one 3 across and 2
	/// down: 4 + 9 and 9 + 4, 13 each, against the staff room's own 505. A thing built is linked in at the
	/// chain's head (<c>FUN_00519d80</c>, <see cref="ParkState.AddObject"/>), so the copy added second is met
	/// first, and it is the one claimed.
	/// </remarks>
	[TestMethod]
	public void OfTwoRestAreasEquallyNearTheFirstInChainOrderIsClaimed()
	{
		var park = Open();

		try
		{
			Assert.IsTrue( park.State.TryObject( StaffRoom, out var room ) );

			var tired = park.People.Staff.Single( staff => staff.ThingId == Guard );
			var walk = park.People.StaffWalkFor( Guard )!;
			var (x, y) = walk.Position.Cell;

			var addedFirst = room with { ThingId = park.State.NextThingId(), RawX = (x + 2) << 8, RawY = (y + 3) << 8 };
			var addedSecond = room with { ThingId = park.State.NextThingId(), RawX = (x + 3) << 8, RawY = (y + 2) << 8 };

			park.State.AddObject( addedFirst );
			park.State.AddObject( addedSecond );

			var met = park.State.ObjectsInChainOrder().Where( placed => placed.IsRestArea ).Select( placed => placed.ThingId ).ToArray();

			CollectionAssert.AreEqual( new[] { addedSecond.ThingId, addedFirst.ThingId, StaffRoom }, met,
				"the chain is walked from its head, where the newest thing is linked" );

			tired.Tiredness = 0f;
			tired.SetActivity( StaffActivity.Idle, tick: 1 );
			tired.TimeStartedIdling = 0;

			var behaviour = new StaffBehaviour( new ParkBalance( Theme, easyMode: true ), new Random( 7 ), park.State );

			behaviour.Step( tired, walk, playing: null, tick: 500 );

			Assert.AreEqual( StaffActivity.GoingToRest, tired.Activity, "a tired guard sets off to rest" );
			Assert.AreEqual( addedSecond.ThingId, tired.RestArea,
				"at the first of the two 13 away that the chain gives, not the one met after it" );
		}
		finally
		{
			Close( park );
		}
	}
}
