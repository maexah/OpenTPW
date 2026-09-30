using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// A guest's balloon - <see cref="Balloon"/>, the settle-up's balloon arm (<c>FUN_004fe1e0</c>, <c>0x004fe6ba</c>..
/// <c>0x004fe78a</c>), boarding's and leaving's halves of the state setter, the needs sweep's countdown and the let-go
/// script (<c>docs/exe/ride-operation.md</c>, "A held balloon").
/// <para>
/// The colours pinned here are Alexah's played Lost Kingdom saves, written by the original: every one of their 29 held
/// balloons is the colour <see cref="Balloon.ColourFor"/> gives its guest's id. The tests that read the shipped park
/// or its catalogue are skipped where there is no installation - see <see cref="GameData"/>.
/// </para>
/// </summary>
[TestClass]
public class ParkBalloonTests
{
	private BaseFileSystem? data;

	private BaseFileSystem Data() => data ??= FileSystem = GameData.Required();

	private const int DrinksShop = 16;

	private const int BalloonShop = 1209, Drinks = 1203;

	/// <summary>The balloon bank's colours: red, green, blue and yellow.</summary>
	private const int Sets = 4;

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

	/// <summary>Guest 7, in <paramref name="state"/>, who won the roll unless <paramref name="queuePos"/> says not.</summary>
	private static Peep Guest( PeepState state = PeepState.Riding, int life = 0, int queuePos = 1 )
		=> new( 7, new ParkWorld.GuestState(
			State: (int)state, SavedState: (int)PeepState.Deciding, PersonType: 0, Cash: 300,
			ExitLevel: 100, Happiness: 50f, Thirst: 50f, Hunger: 50f, Toilet: 10f, Vomit: 0f,
			Litter: 0f, MajorDest: 0, QueuePos: queuePos, PrankeryIndex: 0, RemainingBalloonLife: life ), StandingStill );

	/// <summary>The Drinks Shop the park is saved with, standing in for a shop of <paramref name="item"/>.</summary>
	private ParkWorld.CatalogueObject Shop( int item, int quality = 50 )
		=> Park().Objects.Single( o => o.ThingId == DrinksShop ) with { CatalogueId = item, QualityOfGoods = quality };

	/// <summary>Lets <paramref name="peep"/> off <paramref name="shop"/> through <see cref="ParkRideOperation.Dismiss"/>.</summary>
	private Peep LetOff( Peep peep, ParkWorld.CatalogueObject shop, int sets = Sets )
	{
		var script = Script();

		script.Set( ParkRideOperation.DismissVariable, peep.ThingId );

		Assert.IsTrue( new ParkRideOperation( new ParkState( parkIsClosed: false, visitorsToDate: 0, balance: 1000 ),
				new Dictionary<int, Peep> { [peep.ThingId] = peep }, banks: new ParkSpriteBanks( 6, 1, sets ) )
			.Dismiss( script, shop, tick: 9, new Random( 1 ), catalogue: Catalogue() ),
			"the guest should have been let off" );

		return peep;
	}

	private static int Times( string what ) => Unimplemented.Summary.FirstOrDefault( gap => gap.What == what ).Times;

	/// <summary>
	/// <b>A balloon's colour is its guest's id</b>: the generator reseeded with the id, one draw for the bank and the
	/// second's <c>(r &gt;&gt; 2) % 4</c> for the set. Six of the played saves' holders, one of them walking.
	/// </summary>
	[TestMethod]
	public void ABalloonsColourIsItsGuestsId()
	{
		foreach ( var (id, set) in new[] { (502, 0), (500, 3), (499, 2), (493, 0), (43, 3), (178, 3) } )
			Assert.AreEqual( set, Balloon.ColourFor( id, Sets ), $"guest {id}" );
	}

	/// <summary>
	/// <b>The life is the shop's quality times 255 over 100, truncated, held to 25..255</b>, on the quality's low byte:
	/// a bought shop's 50 gives 127.
	/// </summary>
	[TestMethod]
	public void TheLifeIsTheQualityTimes255OverAHundredHeld()
	{
		foreach ( var (quality, life) in new[] { (0, 25), (9, 25), (10, 25), (11, 28), (50, 127), (100, 255), (255, 255), (300, 112) } )
			Assert.AreEqual( life, Balloon.LifeFor( quality ), $"at quality {quality}" );
	}

	/// <summary><b>The balloon bank has four colours</b>, and the one bank in the game is where the park reads them.</summary>
	[TestMethod]
	public void TheBalloonBankHasFourColours()
		=> Assert.AreEqual( Sets, Balloon.SetsIn( Data() ) );

	/// <summary>
	/// <b>A Balloon Shop hands its winner a balloon in their own colour and a life from its quality</b>, and counts the
	/// event it pushes, not a costume.
	/// </summary>
	[TestMethod]
	public void ABalloonShopGivesABalloonInTheGuestsColourAndALife()
	{
		var events = Times( "SETTLE_UP_BALLOON_EVENT" );

		var peep = LetOff( Guest( life: 3 ), Shop( BalloonShop, quality: 60 ) );

		Assert.IsNotNull( peep.Balloon, "a balloon" );
		Assert.AreEqual( (Balloon.ColourFor( 7, Sets ), Balloon.HeldScript, 255), (peep.Balloon.Sprite.Set,
			peep.Balloon.Sprite.Script, peep.Balloon.Sprite.Alpha), "their colour, on the held script, opaque" );
		Assert.AreEqual( (9 * 8 * 31) + SpriteScript.DefaultInterval, peep.Balloon.Sprite.Due,
			"its first turn an interval after the sweep's sprite clock, sweep 9's game tick at 31 ms" );
		Assert.AreEqual( 153, peep.BalloonLife, "the shop's quality of 60, whatever was left" );
		Assert.AreEqual( events + 1, Times( "SETTLE_UP_BALLOON_EVENT" ), "the event, counted" );
		Assert.AreEqual( ParkSpriteBanks.ChildKind, peep.SpriteKind, "a balloon is not a costume" );
	}

	/// <summary><b>A loser gets nothing</b>: the balloon arm is behind the win roll's gate.</summary>
	[TestMethod]
	public void ALoserGetsNoBalloon()
	{
		var peep = LetOff( Guest( queuePos: 0 ), Shop( BalloonShop ) );

		Assert.AreEqual( (null, 0), (peep.Balloon, peep.BalloonLife) );
	}

	/// <summary>
	/// <b>With no balloon bank a guest is given the life and no balloon</b>, as the original is with no sprite table.
	/// </summary>
	[TestMethod]
	public void WithNoBankTheLifeIsKeptAndNoBalloonShown()
	{
		var peep = LetOff( Guest(), Shop( BalloonShop ), sets: 0 );

		Assert.AreEqual( (null, 127), (peep.Balloon, peep.BalloonLife) );
	}

	/// <summary><b>Boarding anything puts the balloon away and keeps its life</b> (case <c>0x10</c>, no burst).</summary>
	[TestMethod]
	public void BoardingPutsTheBalloonAwayAndKeepsItsLife()
	{
		var peep = Guest( PeepState.EnteringRide, life: 50 );

		peep.Balloon = Balloon.Make( peep.ThingId, Sets, now: 0 );
		peep.SetState( PeepState.Riding, 0, new Random( 1 ) );

		Assert.AreEqual( (null, 50, null), (peep.Balloon, peep.BalloonLife, peep.TakeLetGo()), "put away, not let go" );
	}

	/// <summary>
	/// <b>Leaving a thing brings the balloon back in the same colour, its life as it was</b> (case <c>0xf</c>), won or
	/// lost.
	/// </summary>
	[TestMethod]
	public void LeavingAThingBringsTheBalloonBack()
	{
		foreach ( var won in new[] { 1, 0 } )
		{
			var peep = LetOff( Guest( life: 100, queuePos: won ), Shop( Drinks ) );

			Assert.IsNotNull( peep.Balloon, $"back, the roll {won}" );
			Assert.AreEqual( (Balloon.ColourFor( 7, Sets ), 100), (peep.Balloon.Sprite.Set, peep.BalloonLife) );
		}
	}

	/// <summary><b>With no life left, nothing comes back</b>.</summary>
	[TestMethod]
	public void WithNoLifeNothingComesBack()
		=> Assert.IsNull( LetOff( Guest( life: 0 ), Shop( Drinks ) ).Balloon );

	/// <summary>
	/// <b>Leaving a Balloon Shop does not bring one back</b>: a loser there, who is given none, leaves with none, their
	/// life kept.
	/// </summary>
	[TestMethod]
	public void LeavingABalloonShopBringsNoneBack()
	{
		var peep = LetOff( Guest( life: 100, queuePos: 0 ), Shop( BalloonShop ) );

		Assert.AreEqual( (null, 100), (peep.Balloon, peep.BalloonLife) );
	}

	/// <summary>
	/// <b>The life goes down one a needs sweep on a counting cell, and at nought the balloon is let go</b>: the same
	/// sprite, on the let-go script, handed over once; the life stays at nought.
	/// </summary>
	[TestMethod]
	public void TheLifeCountsDownAndTheBalloonIsLetGoAtNought()
	{
		// Guest 7's sweeps are the ticks whose low two bits are 3.
		var peep = Guest( PeepState.Wandering, life: 2 );
		var held = peep.Balloon = Balloon.Make( peep.ThingId, Sets, now: 0 );

		peep.Tick( 3, onACountingCell: true );

		Assert.AreEqual( (1, held), (peep.BalloonLife, peep.Balloon), "one down, still held" );

		peep.Tick( 7, onACountingCell: true );

		Assert.AreEqual( (0, null), (peep.BalloonLife, peep.Balloon), "nought, and let go" );
		Assert.AreSame( held, peep.TakeLetGo(), "the same balloon, handed over" );
		Assert.AreEqual( SpriteScript.LetGoBalloonEntry, held!.Sprite.Script, "on the let-go script" );
		Assert.IsNull( peep.TakeLetGo(), "once" );

		peep.Tick( 11, onACountingCell: true );

		Assert.AreEqual( 0, peep.BalloonLife, "and it does not go below nought" );
	}

	/// <summary>
	/// <b>The countdown waits on a ride, leaving the park, off a counting cell and off the guest's sweep</b>, and runs with
	/// no balloon showing; each counted sweep counts the thought picker's draw.
	/// </summary>
	[TestMethod]
	public void TheCountdownWaitsWhereTheOriginalsDoes()
	{
		foreach ( var (state, tick, counting, life) in new[] {
			(PeepState.Riding, 3, true, 5), (PeepState.Leaving, 3, true, 5), (PeepState.Wandering, 3, false, 5),
			(PeepState.Wandering, 4, true, 5), (PeepState.Wandering, 3, true, 4) } )
		{
			var peep = Guest( state, life: 5 );
			var draws = Times( "NEEDS_THOUGHT_PICKER" );

			peep.Tick( tick, onACountingCell: counting );

			Assert.AreEqual( life, peep.BalloonLife, $"{state} at {tick} on a counting cell {counting}" );
			Assert.AreEqual( draws + (5 - life), Times( "NEEDS_THOUGHT_PICKER" ), "the draw is counted where it runs" );
		}
	}

	/// <summary><b>A guest is counted on a cleared cell, a path, a queue, a ride's end or its far end</b>, and nowhere else.</summary>
	[TestMethod]
	public void AGuestIsCountedOnTheFiveCellTypes()
	{
		foreach ( var type in new[] { 0, 1, 3, 9, 10 } )
			Assert.IsTrue( Peep.CountsOn( type ), $"type {type}" );

		foreach ( var type in new[] { -1, 2, 4, 5, 6, 7, 8, 11, 30 } )
			Assert.IsFalse( Peep.CountsOn( type ), $"type {type}" );
	}

	/// <summary>
	/// <b>The park asks the cell a guest is linked into</b>: a path in the shipped park counts them, a ride's footprint
	/// does not.
	/// </summary>
	[TestMethod]
	public void TheParkAsksTheCellTheGuestIsLinkedInto()
	{
		var world = Park();
		var people = new ParkPeople( world );
		var peep = people.Guests.Values.First();
		var cells = Enumerable.Range( 0, ParkWorld.MapSize * ParkWorld.MapSize )
			.Select( at => (X: at % ParkWorld.MapSize, Y: at / ParkWorld.MapSize) ).ToArray();
		var path = cells.First( cell => people.State.Record( cell.X, cell.Y ).Type == 1 );
		var footprint = cells.First( cell => people.State.Record( cell.X, cell.Y ).Type == 2 );

		people.State.StandOn( peep.ThingId, path.X, path.Y );
		Assert.IsTrue( people.OnACountingCell( peep ), $"on the path at {path}" );

		people.State.StandOn( peep.ThingId, footprint.X, footprint.Y );
		Assert.IsFalse( people.OnACountingCell( peep ), $"on the footprint at {footprint}" );
	}

	/// <summary><b>Entering the leaving state lets go of the balloon</b> (case <c>0x11</c>), its life kept.</summary>
	[TestMethod]
	public void EnteringTheLeavingStateLetsGo()
	{
		var peep = Guest( PeepState.Wandering, life: 40 );
		var held = peep.Balloon = Balloon.Make( peep.ThingId, Sets, now: 0 );

		peep.SetState( PeepState.Leaving, 0, new Random( 1 ) );

		Assert.AreEqual( (null, 40), (peep.Balloon, peep.BalloonLife) );
		Assert.AreSame( held, peep.TakeLetGo() );
	}

	/// <summary>
	/// <b>A new balloon shows nothing until its first turn, then frame 0 for ever</b>: made hidden with its turn one
	/// interval off, on the held script.
	/// </summary>
	[TestMethod]
	public void ANewBalloonShowsFrameNoughtFromItsFirstTurn()
	{
		var balloon = Balloon.Make( 7, Sets, now: 1000 )!;

		Assert.IsFalse( balloon.Sprite.Shown, "hidden when made" );
		Assert.IsFalse( balloon.Sprite.Step( 1000 + SpriteScript.DefaultInterval ), "not due on the interval itself" );

		for ( var now = 1124; now < 3000; now += 62 )
			balloon.Sprite.Step( now );

		Assert.AreEqual( (true, 0, 255, false), (balloon.Sprite.Shown, balloon.Sprite.Frame, balloon.Sprite.Alpha,
			balloon.Sprite.Ended) );
		Assert.IsNull( Balloon.Make( 7, 0, now: 0 ), "and none with no colours to draw" );
	}

	/// <summary>
	/// <b>A balloon let go shows the burst, frame 1, thirteen turns from alpha 250 down by 20, then hides and is freed on
	/// its next turn</b>, in its own colour - the script at <c>0x0074f4c0</c>.
	/// </summary>
	[TestMethod]
	public void ALetGoBalloonBurstsThirteenTurnsAndIsFreed()
	{
		var balloon = Balloon.Make( 7, Sets, now: 0 )!;
		var set = balloon.Sprite.Set;
		var shown = new List<(int Frame, int Alpha)>();
		var hidden = 0;

		balloon.LetGo();

		for ( var now = 62; !balloon.Sprite.Freed && now < 10000; now += 62 )
		{
			if ( !balloon.Sprite.Step( now ) )
				continue;

			if ( balloon.Sprite.Shown )
				shown.Add( (balloon.Sprite.Frame, balloon.Sprite.Alpha) );
			else
				hidden++;
		}

		CollectionAssert.AreEqual( Enumerable.Range( 0, 13 ).Select( i => (1, 250 - (20 * i)) ).ToArray(), shown.ToArray() );
		Assert.AreEqual( (1, true, true, set), (hidden, balloon.Sprite.Ended, balloon.Sprite.Freed, balloon.Sprite.Set),
			"one hidden turn at the end, then freed, in its colour" );
	}

	/// <summary><b>Every word of the let-go script is copied</b>, at the address the original keeps it.</summary>
	[TestMethod]
	public void TheLetGoScriptIsCopiedWordForWord()
	{
		foreach ( var at in new[] { 1654, 1655, 1657, 1660, 1664, 1666, 1669 } )
			Assert.IsTrue( SpriteScript.HasInstructionAt( at ), $"word {at}" );

		foreach ( var at in new[] { 1656, 1658, 1665, 1667, 1671 } )
			Assert.IsFalse( SpriteScript.HasInstructionAt( at ), $"word {at} is an operand or not copied" );
	}

	/// <summary>
	/// <b>A standing guest's balloon hangs one up plus the bob, and goes where the last frame's trailing sample was</b>:
	/// (id + phase) % 20 picks the bob's row, row 10 its top, 0.2 times 1.5.
	/// </summary>
	[TestMethod]
	public void AStandingGuestsBalloonHangsOneUpPlusTheBob()
	{
		var at = new FixedVector( 5 * FixedVector.One, 7 * FixedVector.One );

		var (x, y, height, lastX, lastY) = Balloon.Place( at, at, 0.5f, 10f, 10f, thingId: 7, phase: 3, lastX: 1f, lastY: 2f );

		Assert.AreEqual( (1f, 2f, 50f, 70f), (x, y, lastX, lastY), "last frame's place, and this frame's for the next" );
		Assert.AreEqual( 1.3f, height, 0.0001f, "one, and 1.5 x 0.2" );

		var (_, _, low, _, _) = Balloon.Place( at, at, 0.5f, 10f, 10f, thingId: 7, phase: 13, lastX: 0f, lastY: 0f );

		Assert.AreEqual( 1f, low, 0.0001f, "row 0 adds nothing" );
	}

	/// <summary>
	/// <b>A walking guest's balloon trails a third of a sweep and hangs lower by twice the gap</b>; early in a sweep the
	/// trailing sample lies behind where the sweep began.
	/// </summary>
	[TestMethod]
	public void AWalkingGuestsBalloonTrailsAndHangsLower()
	{
		var from = new FixedVector( 0, 0 );
		var to = new FixedVector( FixedVector.One, 0 );

		// At half way the guest is at 5.0; the trail at 0.15 is trunc(9830.4) = 9830 of a cell.
		var (_, _, height, lastX, _) = Balloon.Place( from, to, 0.5f, 10f, 10f, thingId: 0, phase: 0, lastX: 0f, lastY: 0f );

		Assert.AreEqual( 9830 / 65536f * 10f, lastX, 0.0001f, "the trailing sample, truncated" );
		Assert.AreEqual( 1f - ((5f - (9830 / 65536f * 10f)) / 0.5f), height, 0.0001f, "one less twice the gap" );

		// At a tenth of the way the trail is at -0.25 of the sweep, held no nearer than -1.
		var (_, _, _, early, _) = Balloon.Place( from, to, 0.1f, 10f, 10f, thingId: 0, phase: 0, lastX: 0f, lastY: 0f );

		Assert.AreEqual( -2.5f, early, 0.0001f, "behind where the sweep began" );
	}

	/// <summary><b>The bob steps once every eleven placements, round twenty</b>.</summary>
	[TestMethod]
	public void TheBobStepsEveryElevenPlacements()
	{
		var (phase, count) = (19, 0f);

		for ( var i = 0; i < 10; ++i )
			(phase, count) = Balloon.AdvanceBob( phase, count, 1f );

		Assert.AreEqual( 19, phase, "ten placements" );

		(phase, count) = Balloon.AdvanceBob( phase, count, 1f );

		Assert.AreEqual( (0, 0f), (phase, count), "the eleventh steps it, round to nought" );
	}

	/// <summary>
	/// <b>A saved guest's balloon fields are read from 406, 495, 430 and 434, and the balloon is the sprite its slot
	/// names only when that sprite is a balloon</b> (FileFormats <c>saves.md</c>). The shipped park holds none, so a copy
	/// of its payload points one guest at another's sprite, and then makes that sprite a balloon.
	/// </summary>
	[TestMethod]
	public void ASavedBalloonIsTheSpriteItsSlotNames()
	{
		var payload = Payload();
		var guests = new ParkWorld( payload ).People.Where( p => p.Guest is { } ).ToArray();
		var (holder, other) = (guests[0], guests[1]);
		var guest = holder.Guest!.Value;
		var at = RecordAt( payload, p => I32( payload, p + 414 ) == guest.Cash && I32( payload, p + 505 ) == guest.State
			&& payload[p + 468] == guest.PersonType && I32( payload, p + 418 ) == guest.ExitLevel );

		Put( payload, at + 406, other.SpriteSlot );
		Put( payload, at + 495, 99 );
		BitConverter.GetBytes( 12.5f ).CopyTo( payload, at + 430 );
		BitConverter.GetBytes( 34.25f ).CopyTo( payload, at + 434 );

		var read = new ParkWorld( payload ).People.Single( p => p.ThingId == holder.ThingId ).Guest!.Value;

		Assert.AreEqual( (other.SpriteSlot, 99, 12.5f, 34.25f), (read.BalloonScript, read.RemainingBalloonLife,
			read.LastPosX, read.LastPosY), "each from its own place" );
		Assert.AreEqual( guest.BeenAdmitted, read.BeenAdmitted, "and the field after the slot untouched" );

		var peep = new ParkPeople( new ParkWorld( payload ) ).Guests[holder.ThingId];

		Assert.AreEqual( (null, 99, 12.5f, 34.25f), (peep.Balloon, peep.BalloonLife, peep.BalloonLastX, peep.BalloonLastY),
			"a person's sprite is not taken for a balloon" );

		// Now make that sprite a balloon: kind 10, set 2, on the held script where the saves keep theirs.
		var sprite = new ParkWorld( payload ).Sprites.Single( s => s.Slot == other.SpriteSlot );
		var record = RecordAt( payload, 0x118, p => I32( payload, p + 0xac ) == sprite.Type && I32( payload, p + 0xb4 ) == sprite.SpriteNumber
			&& BitConverter.ToSingle( payload, p + 0x88 ) == sprite.X && BitConverter.ToSingle( payload, p + 0x90 ) == sprite.Y
			&& I32( payload, p + 0x0c ) == sprite.Script && I32( payload, p + 0x08 ) == sprite.Pc );

		Put( payload, record + 0xac, Balloon.SpriteKind );
		Put( payload, record + 0xb0, 0 );
		Put( payload, record + 0xb4, 2 );
		Put( payload, record + 0x0c, Balloon.HeldScript );
		Put( payload, record + 0x08, Balloon.HeldScript + 2 );

		var linked = new ParkPeople( new ParkWorld( payload ) ).Guests[holder.ThingId].Balloon;

		Assert.IsNotNull( linked, "the balloon its slot names" );
		Assert.AreEqual( (2, Balloon.HeldScript, Balloon.HeldScript + 2, sprite.X, sprite.Y),
			(linked.Sprite.Set, linked.Sprite.Script, linked.Sprite.Pc, linked.X, linked.Y), "where the save left it" );
	}

	/// <summary>
	/// <b>A guest who goes home takes their balloon with them</b>: deleted, not let go, as at the original's bus.
	/// </summary>
	[TestMethod]
	public void AGuestGoingHomeTakesTheBalloon()
	{
		var people = new ParkPeople( Park() );
		var peep = people.Guests.Values.First( p => !PeepBehaviour.HeldByAThing( p.State ) );

		peep.Balloon = Balloon.Make( peep.ThingId, Sets, now: 0 );

		Assert.IsTrue( people.Depart( peep.ThingId ), "gone home" );
		Assert.AreEqual( 0, people.Bursting.Count, "nothing bursting" );
	}

	/// <summary>
	/// <b>In the running park a balloon's life goes down on its guest's own sweeps, and at nought it bursts and is freed</b>:
	/// a guest made on the path at (55,30), given a balloon and two sweeps of life. The shipped park's own guests all
	/// stand on the gateway's approach, cells of type 30, where the original does not count them.
	/// </summary>
	[TestMethod]
	public void InTheParkTheLifeRunsOutAndTheBalloonBursts()
	{
		var world = Park();
		var people = new ParkPeople( world, new ParkBalance( "jungle", easyMode: true ), null, new ParkState( world ),
			random: new Random( 1 ), banks: new ParkSpriteBanks( 6, 1, Sets ) );

		EnterPark();

		var peep = people.Guests[people.Admit( 55, 30 )];
		var held = peep.Balloon = Balloon.Make( peep.ThingId, Sets, now: 0 );

		// Its sprite takes its turns with everybody's: a sweep later it is showing its one picture.
		peep.BalloonLife = 100;
		Sweep( people );

		Assert.AreEqual( (true, 0, Balloon.HeldScript), (held!.Sprite.Shown, held.Sprite.Frame, held.Sprite.Script),
			"stepped as it was held" );

		peep.BalloonLife = 2;

		for ( var sweep = 0; sweep < 12 && peep.BalloonLife > 0; ++sweep )
		{
			Assert.IsTrue( people.OnACountingCell( peep ), $"guest {peep.ThingId} stands where they count, sweep {sweep}" );
			Sweep( people );
		}

		Assert.AreEqual( (0, null), (peep.BalloonLife, peep.Balloon), "run out, and let go" );
		Assert.AreSame( held, people.Bursting.Single(), "bursting where it was" );

		for ( var sweep = 0; sweep < 12 && people.Bursting.Count > 0; ++sweep )
			Sweep( people );

		Assert.AreEqual( (0, true), (people.Bursting.Count, held.Sprite.Freed), "and freed" );
	}

	/// <summary>One frame, through both clocks, as <c>ParkTickTests</c> runs one.</summary>
	private static void Frame( float seconds )
	{
		Time.Update( seconds );
		GameClock.Update( paused: false, GameClock.ParkCatchUp );
	}

	private static void EnterPark()
	{
		Time.Paused = false;
		Time.StepFrames = 0;
		GameClock.Rebase();
		Frame( 0f );
	}

	private static void Sweep( ParkPeople people )
	{
		var from = people.State.GameTick;

		for ( var frame = 0; frame < 10 && people.State.GameTick == from; ++frame )
		{
			Frame( 0.1f );
			people.Update();
		}

		Assert.AreEqual( from + 1, people.State.GameTick, "one sweep" );
	}

	/// <summary>The one offset in the payload where <paramref name="matches"/> holds.</summary>
	private static int RecordAt( byte[] payload, Func<int, bool> matches ) => RecordAt( payload, 1100, matches );

	/// <summary>The one offset, among those with a record of <paramref name="size"/> bytes after it, where <paramref name="matches"/> holds.</summary>
	private static int RecordAt( byte[] payload, int size, Func<int, bool> matches )
	{
		var found = Enumerable.Range( 0, payload.Length - size ).Where( matches ).ToList();

		Assert.AreEqual( 1, found.Count, "exactly one record answers to those fields" );

		return found[0];
	}

	private static int I32( byte[] payload, int at ) => BitConverter.ToInt32( payload, at );

	private static void Put( byte[] payload, int at, int value ) => BitConverter.GetBytes( value ).CopyTo( payload, at );
}
