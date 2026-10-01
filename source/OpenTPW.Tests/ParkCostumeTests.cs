using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// What a guest wears - <see cref="ParkSpriteBanks"/>, the child an arrival is (<c>FUN_004faec0</c>), a save's children
/// brought within the kid banks loaded, and the settle-up's costume arm (<c>FUN_004fe1e0</c>, <c>0x004fe642</c>..
/// <c>0x004fe6b5</c>; <c>docs/exe/ride-operation.md</c>, "A costume").
/// <para>
/// The children pinned here are Alexah's played Lost Kingdom save, written by the original at high detail: every one of
/// its 296 children wears the bank <see cref="ParkSpriteBanks.ChildOf"/> gives their id over six, and every one of the
/// shipped park's 13 over eight, the count it was saved with. The tests that read the game are skipped where there is
/// no installation - see <see cref="GameData"/>.
/// </para>
/// </summary>
[TestClass]
public class ParkCostumeTests
{
	private BaseFileSystem? data;

	private BaseFileSystem Data() => data ??= FileSystem = GameData.Required();

	/// <summary>Every <see cref="ParkPeople"/> a test made goes with it, out of <see cref="Entity.All"/> and <c>Current</c>.</summary>
	[TestCleanup]
	public void LetThePeopleGo() => TestRun.DeleteEvery<ParkPeople>();

	private const int DrinksShop = 16;

	private const int CostumeShop = 1202;

	/// <summary>Six children, Lost Kingdom's one costume, four balloon colours: the park at medium or high detail.</summary>
	private static readonly ParkSpriteBanks Banks = new( KidBanks: 6, CostumeBanks: 1, BalloonSets: 4 );

	private ParkWorld Park() => new( Payload() );

	private byte[] Payload()
	{
		using var stream = new MemoryStream( Data().ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		return new SaveReader( stream ).ReadFile();
	}

	private RideScript Script()
	{
		using var stream = Data().OpenRead( "levels/jungle/rides/bouncy/bouncy.RSE" );
		return new RideScript( new RideScriptFile( stream ) );
	}

	private ParkItemCatalogue Catalogue() => new( "jungle", Data() );

	private static ParkWorld.NavigatorState StandingStill => new(
		X: 0, Y: 0, VelocityX: 0, VelocityY: 0, TargetX: 0, TargetY: 0,
		Mass: ParkWorld.NavigatorState.DefaultMass, Radius: ParkWorld.NavigatorState.DefaultRadius,
		MaxForce: 0, MaxSpeed: 0, NavMode: 0, CantReachDest: 0, PathFinished: false,
		PathCount: 0, PathTotalCount: 0, PathBufferCount: 0,
		BufferedDistance: 0, TailDistance: 0, TotalDistance: 0, StuckBits: 0 );

	/// <summary>Guest <paramref name="id"/>, riding, wearing <paramref name="kind"/> and <paramref name="bank"/>.</summary>
	private static Peep Guest( int id, int kind, int bank, int queuePos = 1 )
		=> new( id, new ParkWorld.GuestState(
			State: (int)PeepState.Riding, SavedState: (int)PeepState.Deciding, PersonType: 0, Cash: 300,
			ExitLevel: 100, Happiness: 50f, Thirst: 50f, Hunger: 50f, Toilet: 10f, Vomit: 0f,
			Litter: 0f, MajorDest: 0, QueuePos: queuePos, PrankeryIndex: 0 ), StandingStill )
		{
			SpriteKind = kind,
			SpriteBank = bank
		};

	/// <summary>Lets <paramref name="peep"/> off a Costume Shop through <see cref="ParkRideOperation.Dismiss"/>.</summary>
	private Peep LetOff( Peep peep, Random random, int item = CostumeShop )
	{
		var script = Script();
		var shop = Park().Objects.Single( o => o.ThingId == DrinksShop ) with { CatalogueId = item };

		script.Set( ParkRideOperation.DismissVariable, peep.ThingId );

		Assert.IsTrue( new ParkRideOperation( new ParkState( parkIsClosed: false, visitorsToDate: 0, balance: 1000 ),
				new Dictionary<int, Peep> { [peep.ThingId] = peep }, banks: Banks )
			.Dismiss( script, shop, tick: 9, random, catalogue: Catalogue() ),
			"the guest should have been let off" );

		return peep;
	}

	/// <summary>Every draw it is asked for, answered from a list and counted.</summary>
	private sealed class Draws( params int[] answers ) : Random
	{
		public int Taken { get; private set; }

		public override int Next()
		{
			var answer = answers.Length == 0 ? 0 : answers[Taken % answers.Length];

			Taken++;

			return answer;
		}
	}

	private static int Times( string what ) => Unimplemented.Summary.FirstOrDefault( gap => gap.What == what ).Times;

	/// <summary>
	/// <b>The detail file caps the kid banks</b> at two, four, six or eight for <c>NUMKIDS</c> 0, 1, 2 or more, unsigned
	/// (<c>FUN_0041a9d0</c>): not the files' own comment's four, six and eight.
	/// </summary>
	[TestMethod]
	public void TheDetailCapsTheKidBanks()
	{
		foreach ( var (numKids, cap) in new[] { (0, 2), (1, 4), (2, 6), (3, 8), (4, 8), (-1, 8) } )
			Assert.AreEqual( cap, ParkSpriteBanks.KidCap( numKids ), $"NUMKIDS {numKids}" );
	}

	/// <summary>
	/// <b>Lost Kingdom draws over six children, one costume and four balloon colours at medium detail</b>, eight
	/// children with the cap lifted, two at low.
	/// </summary>
	[TestMethod]
	public void LostKingdomsBanksAreCounted()
	{
		Assert.AreEqual( Banks, ParkSpriteBanks.Read( Data(), "jungle", numKids: 2 ) );
		Assert.AreEqual( 8, ParkSpriteBanks.Read( Data(), "jungle", numKids: 3 ).KidBanks, "all eight banks" );
		Assert.AreEqual( (2, 1), (ParkSpriteBanks.Read( Data(), "jungle", numKids: 0 ) is var low ? (low.KidBanks, low.StaffCap) : default),
			"low detail: two children, one bank a staff folder" );
	}

	/// <summary>
	/// <b>A guest's child is their id's</b>: the generator reseeded with it, one draw, <c>(r &gt;&gt; 2) %</c> the banks.
	/// The played park's first children on each bank, over six; two guests there in costume, who would go back to 0 and 4.
	/// </summary>
	[TestMethod]
	public void AGuestsChildIsTheirIds()
	{
		foreach ( var (id, child) in new[] { (523, 0), (520, 1), (522, 2), (521, 3), (524, 4), (526, 5), (213, 0), (181, 4) } )
			Assert.AreEqual( child, Banks.ChildOf( id ), $"guest {id}" );

		Assert.AreEqual( 0, new ParkSpriteBanks( 0, 0, 0 ).ChildOf( 523 ), "and bank nought with none" );
	}

	/// <summary>
	/// <b>The shipped park's thirteen children are their ids' over eight</b>, the count it was saved with, and each person's
	/// <c>mESPSprite</c> and <c>mSpriteID</c> (file 37 and 246) are their saved sprite's kind and bank.
	/// </summary>
	[TestMethod]
	public void TheShippedChildrenAreTheirIdsOverEight()
	{
		var park = Park();
		var sprites = park.Sprites.ToDictionary( sprite => sprite.Slot );
		var eight = Banks with { KidBanks = 8 };
		var guests = park.People.Where( person => person.Guest != null ).ToArray();

		Assert.AreEqual( 13, guests.Length );

		foreach ( var person in park.People )
			Assert.AreEqual( (sprites[person.SpriteSlot].Type, sprites[person.SpriteSlot].Bank), (person.SpriteKind, person.SpriteBank),
				$"person {person.ThingId}" );

		foreach ( var person in guests )
			Assert.AreEqual( eight.ChildOf( person.ThingId ), person.SpriteBank, $"guest {person.ThingId}" );
	}

	/// <summary>
	/// <b>A load brings a saved child within the banks loaded</b> (<c>0x004f93a6</c>): the shipped park's guests 33, 35 and
	/// 29, saved on banks 6 and 7, come in on 0 and 1 at six; the rest keep theirs.
	/// </summary>
	[TestMethod]
	public void ALoadBringsASavedChildWithinTheBanks()
	{
		var park = Park();
		var people = new ParkPeople( park, banks: Banks );

		foreach ( var (id, bank) in new[] { (33, 0), (35, 1), (29, 1), (41, 0), (37, 2), (42, 4), (40, 5) } )
			Assert.AreEqual( (ParkSpriteBanks.ChildKind, bank), (people.Guests[id].SpriteKind, people.Guests[id].SpriteBank), $"guest {id}" );

		Assert.AreEqual( 7, new ParkPeople( park ).Guests[35].SpriteBank, "with no counts, as saved" );
	}

	/// <summary>
	/// <b>A staff folder loads one bank at <c>NUMKIDS</c> 0 and two otherwise</b> (<c>FUN_0041aa40</c>); the handymen,
	/// mechanics, guards and researchers are brought within it, the entertainers are not.
	/// </summary>
	[TestMethod]
	public void TheStaffFoldersAreCappedToo()
	{
		var low = Banks with { StaffCap = ParkSpriteBanks.StaffCapFor( 0 ) };

		Assert.AreEqual( (1, 2, 2), (ParkSpriteBanks.StaffCapFor( 0 ), ParkSpriteBanks.StaffCapFor( 1 ), ParkSpriteBanks.StaffCapFor( 2 )) );
		Assert.AreEqual( (0, 0, 0, 0, 2), (low.Reduce( 5, 1 ), low.Reduce( 6, 1 ), low.Reduce( 7, 1 ), low.Reduce( 8, 1 ), low.Reduce( 4, 2 )),
			"each capped kind within one; an entertainer as saved" );
		Assert.AreEqual( 1, Banks.Reduce( 6, 1 ), "two banks at medium" );

		// The shipped park's mechanic wears the second mechanics' bank, SPR_OM, which low detail does not load.
		var park = Park();
		var mechanic = park.People.Single( p => p.SpriteKind == 6 );
		var sprite = park.Sprites.Single( s => s.Slot == mechanic.SpriteSlot );

		Assert.AreEqual( 1, sprite.Bank, "saved on the second" );
		Assert.AreEqual( (6, 0), ParkGuestSprites.LookOf( new ParkPeople( park, banks: low ), low, mechanic, sprite ), "drawn on the first" );
		Assert.IsTrue( ParkGuestSprites.BanksToPack( park.Sprites, low ).Contains( (6, 0) )
			&& !ParkGuestSprites.BanksToPack( park.Sprites, low ).Contains( (6, 1) ), "and only the first packed" );
	}

	/// <summary>
	/// <b>Each kind is brought within its own banks</b>: a costume's within the costumes, and a kind no guest wears left
	/// as it is.
	/// </summary>
	[TestMethod]
	public void EachKindIsBroughtWithinItsOwnBanks()
	{
		var three = Banks with { CostumeBanks = 3 };

		Assert.AreEqual( (1, 1, 0, 7), (three.Reduce( ParkSpriteBanks.ChildKind, 7 ), three.Reduce( ParkSpriteBanks.CostumeKind, 4 ),
			Banks.Reduce( ParkSpriteBanks.CostumeKind, 3 ), Banks.Reduce( 4, 7 )) );
	}

	/// <summary><b>An arrival is the child their id gives</b>, drawn in it; with no counts, bank nought.</summary>
	[TestMethod]
	public void AnArrivalIsTheirIdsChild()
	{
		var people = new ParkPeople( Park(), banks: Banks );
		var arrival = people.Guests[people.Admit( 55, 30 )];

		Assert.AreEqual( (ParkSpriteBanks.ChildKind, Banks.ChildOf( arrival.ThingId )), (arrival.SpriteKind, arrival.SpriteBank) );

		var bare = new ParkPeople( Park() );
		var plain = bare.Guests[bare.Admit( 55, 30 )];

		Assert.AreEqual( 0, plain.SpriteBank, "no counts, no child but the first" );
	}

	/// <summary>
	/// <b>A Costume Shop dresses a child in its costume</b>: kind 2, a bank drawn over the one costume - one draw, taken
	/// all the same - and the event counted.
	/// </summary>
	[TestMethod]
	public void ACostumeShopDressesAChild()
	{
		var events = Times( "SETTLE_UP_COSTUME_EVENT" );
		var random = new Draws( 12345 );

		var peep = LetOff( Guest( 523, ParkSpriteBanks.ChildKind, 0 ), random );

		Assert.AreEqual( (ParkSpriteBanks.CostumeKind, 0), (peep.SpriteKind, peep.SpriteBank), "in the costume" );
		Assert.AreEqual( 1, random.Taken, "the costume's one draw" );
		Assert.AreEqual( events + 1, Times( "SETTLE_UP_COSTUME_EVENT" ), "the event, counted" );
	}

	/// <summary>
	/// <b>The costume's bank is <c>(r &gt;&gt; 2) %</c> the costume banks</b>, where a theme has more than one.
	/// </summary>
	[TestMethod]
	public void TheCostumesBankIsTheDrawOverTheBanks()
	{
		var script = Script();
		var shop = Park().Objects.Single( o => o.ThingId == DrinksShop ) with { CatalogueId = CostumeShop };
		var peep = Guest( 523, ParkSpriteBanks.ChildKind, 0 );

		script.Set( ParkRideOperation.DismissVariable, peep.ThingId );

		// 17 is 4 once shifted, which is 1 over three; unshifted it would be 2, unreduced 4, over the six kid banks 4.
		Assert.IsTrue( new ParkRideOperation( new ParkState( parkIsClosed: false, visitorsToDate: 0, balance: 1000 ),
				new Dictionary<int, Peep> { [peep.ThingId] = peep }, banks: Banks with { CostumeBanks = 3 } )
			.Dismiss( script, shop, tick: 9, new Draws( 17 ), catalogue: Catalogue() ), "let off" );

		Assert.AreEqual( (ParkSpriteBanks.CostumeKind, 1), (peep.SpriteKind, peep.SpriteBank) );
	}

	/// <summary>
	/// <b>A second visit gives the child back</b>, the one their id gives, with no event and no draw from the ride turn.
	/// </summary>
	[TestMethod]
	public void ASecondVisitGivesTheChildBack()
	{
		var events = Times( "SETTLE_UP_COSTUME_EVENT" );
		var random = new Draws();

		var peep = LetOff( Guest( 524, ParkSpriteBanks.CostumeKind, 0 ), random );

		Assert.AreEqual( (ParkSpriteBanks.ChildKind, 4), (peep.SpriteKind, peep.SpriteBank), "guest 524's child, bank 4" );
		Assert.AreEqual( (0, events), (random.Taken, Times( "SETTLE_UP_COSTUME_EVENT" )), "no draw, no event" );
	}

	/// <summary>
	/// <b>Anything but a costume is dressed</b>: the test is "not exactly 2" (<c>0x004fe64a</c>), so even a kind no guest
	/// wears is.
	/// </summary>
	[TestMethod]
	public void AnythingButACostumeIsDressed()
		=> Assert.AreEqual( ParkSpriteBanks.CostumeKind, LetOff( Guest( 523, 1, 0 ), new Draws() ).SpriteKind );

	/// <summary><b>A loser keeps what they wear</b>: the arm is behind the win roll's gate. And a Drinks Shop dresses nobody.</summary>
	[TestMethod]
	public void ALoserAndADrinkChangeNothing()
	{
		var lost = LetOff( Guest( 523, ParkSpriteBanks.ChildKind, 0, queuePos: 0 ), new Draws() );
		var drank = LetOff( Guest( 523, ParkSpriteBanks.ChildKind, 0 ), new Draws(), item: 1203 );

		Assert.AreEqual( (ParkSpriteBanks.ChildKind, ParkSpriteBanks.ChildKind), (lost.SpriteKind, drank.SpriteKind) );
	}

	/// <summary>
	/// <b>A guest is drawn in what they wear now</b>, a costume included; anybody the simulation does not run, in the
	/// saved sprite, a child brought within the banks.
	/// </summary>
	[TestMethod]
	public void AGuestIsDrawnInWhatTheyWear()
	{
		var park = Park();
		var people = new ParkPeople( park, banks: Banks );
		var person = park.People.Single( p => p.ThingId == 35 );
		var sprite = park.Sprites.Single( s => s.Slot == person.SpriteSlot );

		Assert.AreEqual( (0, 1), ParkGuestSprites.LookOf( people, Banks, person, sprite ), "saved on 7, drawn on 1" );

		people.Guests[35].SpriteKind = ParkSpriteBanks.CostumeKind;
		people.Guests[35].SpriteBank = 0;

		Assert.AreEqual( (2, 0), ParkGuestSprites.LookOf( people, Banks, person, sprite ), "in costume" );
		Assert.AreEqual( (0, 1), ParkGuestSprites.LookOf( null, Banks, person, sprite ), "no simulation: the saved, within six" );
		Assert.AreEqual( (0, 7), ParkGuestSprites.LookOf( null, null, person, sprite ), "and with no counts, as saved" );
	}

	/// <summary>
	/// <b>With the counts every child and costume bank is packed</b>, the staff's as worn and the balloons', and a saved
	/// child's seventh or eighth bank is not; without them, what is worn.
	/// </summary>
	[TestMethod]
	public void EveryChildAndCostumeBankIsPacked()
	{
		var sprites = Park().Sprites;
		var packed = ParkGuestSprites.BanksToPack( sprites, Banks ).ToHashSet();

		foreach ( var bank in Enumerable.Range( 0, 6 ) )
			Assert.IsTrue( packed.Contains( (0, bank) ), $"child {bank}" );

		Assert.IsTrue( packed.Contains( (2, 0) ) && packed.Contains( (Balloon.SpriteKind, 0) ), "the costume and the balloons" );
		Assert.IsFalse( packed.Contains( (0, 6) ) || packed.Contains( (0, 7) ), "not the seventh or eighth child" );
		Assert.IsTrue( sprites.Where( s => s.Type is > 2 and < 9 ).All( s => packed.Contains( (s.Type, s.Bank + s.BankOffset) ) ), "the staff's" );

		var worn = ParkGuestSprites.BanksToPack( sprites, null ).ToHashSet();

		Assert.IsTrue( worn.Contains( (0, 7) ) && !worn.Contains( (0, 1) ), "without counts, the save's own" );
	}
}
