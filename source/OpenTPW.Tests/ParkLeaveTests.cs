using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// Leaving a park with something in the hand: <see cref="Level.ForgetPark"/>, the part of
/// <see cref="Level.Unload"/> that empties the hand, among the other park state it lets go of. These read
/// the shipped Lost Kingdom and are skipped where there is no installation - see <see cref="GameData"/>.
///
/// <para>
/// <b>The call from <see cref="Level.Unload"/> itself is not reached here</b>, because no test can build a
/// level; the game run is what shows a park left mid-carry opening the next one with an empty hand.
/// </para>
/// </summary>
[TestClass]
public class ParkLeaveTests
{
	private BaseFileSystem data = null!;

	private readonly List<Entity> made = [];

	[TestInitialize]
	public void MountTheGame() => FileSystem = data = GameData.Required();

	[TestCleanup]
	public void PutAwayWhatWasMade()
	{
		ParkBuilding.Drop();
		ParkStaffPool.Drop();
		ParkBuildMode.Forget();

		foreach ( var entity in made )
			entity.Delete();

		made.Clear();
		Entity.ApplyDeletions();
	}

	private const string Theme = "jungle";

	/// <summary>The shipped park's Belly Bounce, anchored (51,23).</summary>
	private const int BellyBounceThing = 13;

	/// <summary>
	/// <b>A moved thing in the hand is let go of as the park goes, and stays sold</b>: the original's park end
	/// runs the move tool's uninstall, which builds nothing and refunds nothing.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> a <see cref="Level.ForgetPark"/> that lets go of nothing, or a let-go that forgets items,
	/// leaves the Belly Bounce in the hand, to go down at the next park's first click.
	/// </remarks>
	[TestMethod]
	public void LeavingTheParkLetsGoOfAMovedThing()
	{
		var state = MoveTheBellyBounceIntoTheHand();
		var balance = state.Balance;
		var standing = state.Objects.Count;

		Level.ForgetPark();

		Assert.AreEqual( 0, ParkBuilding.Carrying, "the hand is empty" );
		Assert.AreEqual( balance, state.Balance, "nothing refunded and nothing charged" );
		Assert.AreEqual( standing, state.Objects.Count, "nothing built" );
		Assert.IsFalse( state.TryObject( BellyBounceThing, out _ ), "and it is still sold" );
	}

	/// <summary>
	/// <b>A worker in the hand is put back down where they stood as the park goes</b>, as the original's park end
	/// runs the place-worker mode's uninstall (<c>0x0046cdc0</c>) - so the hand is empty for the next park.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> a let-go that answers only items and candidates carries the worker's id into the next
	/// park, where the first click would try to put down somebody who is not there.
	/// </remarks>
	[TestMethod]
	public void LeavingTheParkPutsAHeldWorkerBack()
	{
		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		var world = new ParkWorld( new SaveReader( stream ).ReadFile() );
		var people = new ParkPeople( world, new ParkBalance( Theme, easyMode: true ), null, new ParkState( world ) );
		var member = people.Staff[0];

		Assert.IsTrue( people.PickUp( member.ThingId ) );

		Level.ForgetPark();

		Assert.AreEqual( 0, people.CarriedStaff, "nobody is in the hand" );
		Assert.AreEqual( StaffActivity.Idle, member.Activity, "and the worker is back at work" );
	}

	/// <summary>
	/// The shipped park with its Belly Bounce picked up by a move onto the Staff Room's anchor, which refuses it:
	/// sold where it stood, and in the hand.
	/// </summary>
	private ParkState MoveTheBellyBounceIntoTheHand()
	{
		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		var world = new ParkWorld( new SaveReader( stream ).ReadFile() );
		var catalogue = new ParkItemCatalogue( Theme, data );
		var state = new ParkState( world );
		var rides = new ParkRides( Theme, world, catalogue, data );

		made.Add( rides );

		Assert.IsTrue( state.TryObject( BellyBounceThing, out var bounce ) );

		var reply = ParkBuilding.Move( state, world, catalogue, null, rides, BellyBounceThing, 58, 16 );

		Assert.AreEqual( bounce.CatalogueId, ParkBuilding.Carrying, $"in the hand before leaving: {reply}" );

		return state;
	}

	/// <summary>
	/// <b>A candidate on the cursor goes back to the pool as the park goes</b>, as the original's place-staff
	/// uninstall returns one that was never put down.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> a <see cref="Level.ForgetPark"/> that lets go of nothing, or a let-go that forgets
	/// candidates, leaves the candidate on the
	/// cursor, to be hired at the next park's first click; the next park's pool is rolled from the same seed, so
	/// the same id names the same person there.
	/// </remarks>
	[TestMethod]
	public void LeavingTheParkPutsACarriedCandidateBack()
	{
		var pool = new ParkStaffPool( new ParkBalance( Theme, easyMode: true ) );
		var candidate = pool.Candidates[0];
		var waiting = pool.Candidates.ToList();

		StringAssert.Contains( ParkStaffPool.Carry( candidate.Id ), candidate.Name );

		Level.ForgetPark();

		Assert.AreEqual( 0, ParkStaffPool.Carrying, "nobody is on the cursor" );
		CollectionAssert.AreEqual( waiting, pool.Candidates.ToList(), "and the pool is as it was, in its order" );
	}
}
