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

	/// <summary>The hand is emptied, and every <see cref="ParkPeople"/> a test made goes with it.</summary>
	[TestCleanup]
	public void EmptyTheHand()
	{
		ParkStaffPool.Drop();
		TestRun.DeleteEvery<ParkPeople>();
	}

	/// <summary>
	/// An untouched path cell inside the park, which the original's place-staff click accepts as well:
	/// type 1, not flagged <c>0x40</c>, no track record.
	/// </summary>
	private const int OnMapX = 47, OnMapY = 21;

	private sealed class BankDraw : System.Random
	{
		public override int Next() => 4;
	}

	[DataTestMethod]
	[DataRow( 0, 5 )]
	[DataRow( 1, 6 )]
	[DataRow( 2, 4 )]
	[DataRow( 3, 7 )]
	[DataRow( 4, 8 )]
	public void NewStaffPicturesUseNativeKindsAndCostumes( int candidateKind, int spriteKind )
	{
		// A nonzero second bank distinguishes the candidate byte from a saved staff template.
		// The guard ignores costume0 and draws1; the others truncate costume257 to1.
		var banks = new ParkSpriteBanks( 0, 0, 0 )
		{
			StaffBanks = Enumerable.Range( 4, 5 ).ToDictionary( k => k, _ => 2 )
		};
		var candidate = new ParkStaffPool.Candidate( 1, candidateKind, "Test", 2, candidateKind == 3 ? 0 : 257, 100 );
		var picture = ParkPeople.StaffPicture( candidate, banks, new BankDraw() );
		Assert.IsNotNull( picture );
		Assert.AreEqual( (spriteKind, 1, 0, 0, 255, 1),
			(picture.Value.Type, picture.Value.Bank, picture.Value.SpriteNumber, picture.Value.Frame, picture.Value.Alpha, picture.Value.State) );
	}

	[TestMethod]
	public void MissingStaffBanksRefuseANewPicture()
	{
		var banks = new ParkSpriteBanks( 0, 0, 0 )
		{
			StaffBanks = Enumerable.Range( 4, 5 ).ToDictionary( k => k, _ => 0 )
		};
		foreach ( var kind in Enumerable.Range( 0, 5 ) )
			Assert.IsNull( ParkPeople.StaffPicture( new( 1, kind, "Test", 2, 0, 100 ), banks, new BankDraw() ) );
	}

	[DataTestMethod]
	[DataRow( 0 )]
	[DataRow( 2 )]
	public void FreshParkCanPlaceEveryStaffKindWithoutSavedPeople( int detail )
	{
		var balance = new ParkBalance( "jungle", easyMode: false );
		var catalogue = new ParkItemCatalogue( "jungle", data, instantAction: false );
		var fresh = new FreshPark( "jungle", balance, catalogue, data );
		Assert.AreEqual( 0, fresh.People.Count );
		Assert.AreEqual( 0, fresh.Sprites.Count );
		var state = new ParkState( fresh );
		var banks = ParkSpriteBanks.Read( data, "jungle", detail );
		var people = new ParkPeople( fresh, balance, state: state, catalogue: catalogue, banks: banks );
		var pool = new ParkStaffPool( balance );
		var waiting = pool.Candidates.Count;
		var packed = ParkGuestSprites.BanksToPack( fresh.Sprites, banks ).ToHashSet();
		var models = new[] { 5, 4, 6, 7, 8 };
		var kinds = new[] { 5, 6, 4, 7, 8 };
		Assert.AreEqual( 1, state.Record( 47, 18 ).Type );

		for ( var kind = 0; kind < 5; ++kind )
		{
			var candidate = pool.OfKind( kind ).First();
			ParkStaffPool.Carry( candidate.Id );
			var reply = ParkStaffPool.PlaceCarried( 47, 18 );
			Assert.AreEqual( kind + 1, people.Staff.Count, reply );
			Assert.AreEqual( models[kind], people.Staff[^1].Model );
			Assert.AreEqual( 0, ParkStaffPool.Carrying );
			Assert.IsNull( pool.Find( candidate.Id ) );
			Assert.IsNotNull( people.SpriteFor( people.Staff[^1].ThingId ), "a live animation" );
			Assert.IsTrue( packed.Contains( (kinds[kind], 0) ), "the empty park atlas includes the hire's bank" );
		}
		Assert.AreEqual( waiting - 5, pool.Candidates.Count );
		Assert.AreEqual( 5, people.Staff.Select( s => s.ThingId ).Distinct().Count() );
		Assert.IsTrue( people.Staff.All( s => s.ThingId > 12 ) );
		Assert.AreEqual( 0, fresh.People.Count, "initial state remains immutable and genuinely empty" );
		Assert.AreEqual( 0, fresh.Sprites.Count );
	}

	[DataTestMethod]
	[DataRow( 0, 7 )]
	[DataRow( 2, 8 )]
	public void StaffAtlasReadsActualBanksAndUsablePicturesInAnEmptyPark( int detail, int expected )
	{
		var counts = ParkSpriteBanks.Read( data, "jungle", detail );
		var packed = ParkGuestSprites.BanksToPack( [], counts ).Where( b => b.Type is >= 4 and <= 8 ).ToArray();
		Assert.AreEqual( expected, packed.Length, "three entertainers and four generic staff kinds, with a second mechanic above low detail" );
		foreach ( var (kind, bank) in packed )
		{
			var files = ParkGuestSprites.BanksIn( data, ParkGuestSprites.FolderFor( kind, "jungle" )!, kind );
			System.Console.WriteLine( $"staff kind {kind} bank {bank}: {files[bank]}" );
			Assert.IsTrue( bank < files.Length );
			var sprite = new SpriteBankFile( files[bank] );
			foreach ( var firstPerson in new[] { false, true } )
			{
				var pictures = new SpritePackFile( ParkGuestSprites.PackFor( files[bank], sprite, firstPerson ) );
				Assert.IsTrue( pictures.Pictures.Length > 0 );
			}
		}
	}

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
	/// <b>A hire walks at the speed a rested member settles at</b>, base 140: a mover speed of 18350 and a force of 36700,
	/// whoever else the park holds, as staff are not eased (Q136).
	/// </summary>
	[TestMethod]
	public void AHireWalksAtARestedMembersSpeed()
	{
		var (people, pool) = Park();

		Assert.AreNotEqual( 0, pool.Hire( people, pool.Candidates.Last(), OnMapX, OnMapY ) );

		var hired = people.Staff[^1].Navigator;

		Assert.AreEqual( (18350, 36700), (hired.MaxSpeed, hired.MaxForce) );
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
