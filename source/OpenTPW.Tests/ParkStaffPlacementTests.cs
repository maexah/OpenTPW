using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// Putting a candidate from the hire screen down in the park: the click that
/// <see cref="ParkStaffPool.PlaceCarried"/> answers, and <see cref="ParkStaffPool.Hire"/>, the body it
/// shares with the console's <c>hire</c>. These read the shipped Lost Kingdom and are skipped where there
/// is no installation - see <see cref="GameData"/>.
///
/// <para>
/// <b>Off the map is the one refusal a cell earns here</b>, so that is the refusal these use; the
/// original's cell rule is counted, not built (<c>STAFF_PLACEMENT_CELL_RULE</c>). The cell a worker goes
/// up on is one that rule accepts too, so these hold once it is built. The hand and the pool are static,
/// and each test empties the hand after itself.
/// </para>
/// </summary>
[TestClass]
public class ParkStaffPlacementTests
{
	private BaseFileSystem data = null!;

	[TestInitialize]
	public void MountTheGame() => FileSystem = data = GameData.Required();

	[TestCleanup]
	public void EmptyTheHand() => ParkStaffPool.Drop();

	/// <summary>
	/// An untouched path cell inside the park, which the original's place-staff click accepts as well:
	/// type 1, not flagged <c>0x40</c>, no track record.
	/// </summary>
	private const int OnMapX = 47, OnMapY = 21;

	private (ParkPeople People, ParkStaffPool Pool) Park()
	{
		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		var world = new ParkWorld( new SaveReader( stream ).ReadFile() );
		var balance = new ParkBalance( "jungle", easyMode: true );
		var state = new ParkState( world );

		var cell = ParkState.CellFor( world, OnMapX, OnMapY );
		Assert.AreEqual( 1, cell.Type, "the landing cell is path" );
		Assert.AreEqual( 0, cell.Flags & ParkPathBuilding.OutsideThePark, "inside the park" );
		Assert.AreEqual( 0, cell.TrackType, "with no track record" );

		return (new ParkPeople( world, balance, null, state ), new ParkStaffPool( balance ));
	}

	/// <summary>
	/// <b>A drop the park refuses keeps the candidate on the cursor and in the pool</b>, as the original's
	/// place-staff click leaves its mode installed, and the next click that lands hires the same person.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> taking the candidate out of the pool and emptying the hand before the hire loses
	/// them from both; emptying the hand on a refusal leaves the pool whole and the cursor empty; taking
	/// them and putting them back on a refusal moves them to the end of the pool, and of their tab.
	/// </remarks>
	[TestMethod]
	public void ADropTheParkRefusesKeepsTheCandidateOnTheCursorAndInThePool()
	{
		var (people, pool) = Park();
		var candidate = pool.Candidates[0];
		var waiting = pool.Candidates.ToList();
		var staff = people.Staff.Count;

		StringAssert.Contains( ParkStaffPool.Carry( candidate.Id ), candidate.Name );

		var refused = ParkStaffPool.PlaceCarried( -1, -1 );

		Assert.AreEqual( candidate.Id, ParkStaffPool.Carrying, $"still on the cursor: {refused}" );
		Assert.AreEqual( candidate, pool.Find( candidate.Id ), "still in the pool" );
		CollectionAssert.AreEqual( waiting, pool.Candidates.ToList(), "the pool as it was, in its order" );
		Assert.AreEqual( staff, people.Staff.Count, "and nobody was hired" );
		StringAssert.Contains( refused, "cannot be put down at (-1,-1)" );

		// The refusal was the cell's: the same person, on the next click, goes up.
		var hired = ParkStaffPool.PlaceCarried( OnMapX, OnMapY );

		StringAssert.Contains( hired, $"hired {candidate.Name} as thing " );
		Assert.AreEqual( staff + 1, people.Staff.Count, hired );
	}

	/// <summary>
	/// <b>A drop that lands hires the candidate, takes them out of the pool and empties the hand</b> -
	/// the one moment the pool loses anybody.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> a hire that never takes the candidate leaves them in the pool; one that keeps the
	/// hand full after a hire leaves them on the cursor.
	/// </remarks>
	[TestMethod]
	public void ADropThatLandsHiresTheCandidateAndEmptiesTheHand()
	{
		var (people, pool) = Park();
		var candidate = pool.Candidates[0];
		var waiting = pool.Candidates.Count;

		ParkStaffPool.Carry( candidate.Id );

		var reply = ParkStaffPool.PlaceCarried( OnMapX, OnMapY );
		var member = people.Staff[^1];

		Assert.AreEqual( 0, ParkStaffPool.Carrying, $"the hand is empty: {reply}" );
		Assert.IsNull( pool.Find( candidate.Id ), "they left the pool" );
		Assert.AreEqual( waiting - 1, pool.Candidates.Count, "and only they did" );
		Assert.AreEqual( ParkStaffPool.ModelFor( candidate.Kind ), member.Model, "the new worker is of their kind" );
		StringAssert.Contains( reply, $"as thing {member.ThingId} at ({OnMapX},{OnMapY})" );
	}

	/// <summary>
	/// <b>The body the place-staff click and the console's <c>hire</c> share refuses the same way</b>:
	/// nought, and the candidate still waiting. This drives that body; the console's own case is not
	/// driven here.
	/// </summary>
	/// <remarks>
	/// <b>Mutation:</b> taking the candidate whatever the hire answered loses them here.
	/// </remarks>
	[TestMethod]
	public void AHireTheParkRefusesLeavesTheCandidateWaiting()
	{
		var (people, pool) = Park();
		var candidate = pool.Candidates.Last();
		var waiting = pool.Candidates.Count;

		Assert.AreEqual( 0, pool.Hire( people, candidate, ParkWorld.MapSize, OnMapY ) );
		Assert.AreEqual( candidate, pool.Find( candidate.Id ), "still waiting" );
		Assert.AreEqual( waiting, pool.Candidates.Count );

		Assert.AreNotEqual( 0, pool.Hire( people, candidate, OnMapX, OnMapY ), "and the park takes them on the map" );
		Assert.IsNull( pool.Find( candidate.Id ) );
	}
}
