using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// Leaving a park with something in the hand: <see cref="Level.ForgetPark"/>, the part of
/// <see cref="Level.Unload"/> that empties both hands, among the other park state it lets go of. These read
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
	/// <b>Mutations:</b> leaving the item hand out of <see cref="Level.ForgetPark"/> leaves the Belly Bounce in
	/// it, to go down at the next park's first click.
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
	/// <b>Both hands are emptied when both are full</b>, which this park allows (<c>docs/QUEUE.md</c> Q39): a
	/// candidate taken off the hire screen stays on the cursor while the buy screen fills the other hand.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> letting go of the second hand only when the first was empty, or stopping after the first
	/// drop, carries the candidate into the next park.
	/// </remarks>
	[TestMethod]
	public void LeavingTheParkWithBothHandsFullEmptiesBoth()
	{
		var pool = new ParkStaffPool( new ParkBalance( Theme, easyMode: true ) );
		var candidate = pool.Candidates[0];

		MoveTheBellyBounceIntoTheHand();
		ParkStaffPool.Carry( candidate.Id );

		Assert.AreNotEqual( 0, ParkBuilding.Carrying, "an item in one hand" );
		Assert.AreEqual( candidate.Id, ParkStaffPool.Carrying, "and a candidate in the other" );

		Level.ForgetPark();

		Assert.AreEqual( 0, ParkBuilding.Carrying, "the item hand is empty" );
		Assert.AreEqual( 0, ParkStaffPool.Carrying, "and so is the staff hand" );
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
	/// <b>Mutations:</b> leaving the staff hand out of <see cref="Level.ForgetPark"/> leaves the candidate on the
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
