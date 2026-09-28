using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// The settle-up's excitement match - <c>FUN_004fdcc0</c>, run as a guest comes off a thing: how near the thing's
/// excitement is to what the guest's kind likes moves their happiness, and the excitement makes them sick by how little
/// hungry they are (<c>docs/exe/ride-operation.md</c>, "The excitement match").
///
/// <para>
/// <b>Every number is the park's own.</b> The Belly Bounce is excitement 40 at its saved settings, and its speed against
/// its starting 60 moves it (62 is 41, 66 is 44, 74 is 49, 75 is 50); the kinds prefer 80, 65, 50, 35, 65, 80, 45 and 80;
/// <c>Standard.sam</c> gives 25, 15 and 5 for a perfect, good and OK ride and 10 for the vomit divisor. Each threshold
/// is met from both sides - gaps of 4 and 5, 14 and 15, 39 and 40 - and the meters start where no clamp can hide a slip.
/// </para>
/// <para>
/// <b>Two things these cannot tell apart.</b> The three rewards are the same numbers as the three mood changes in this
/// park (25, 15 and 5), so which property the match reads is held by review, not by a test. And the item's effects,
/// which follow at once, hold both meters to 0..100 again even when they add nought, so the match's own clamp cannot be
/// seen through a visit: <see cref="BothMetersStopAtAHundred"/> pins where the visit leaves them.
/// </para>
/// <para>
/// These read real game files and are skipped where there is no installation - see <see cref="GameData"/>.
/// </para>
/// </summary>
[TestClass]
public class ParkExcitementMatchTests
{
	private BaseFileSystem data = null!;

	[TestInitialize]
	public void MountTheGame() => FileSystem = data = GameData.Required();

	/// <summary>Thing 13, the Belly Bounce: excitement 40 at its saved settings.</summary>
	private const int BellyBounce = 13;

	/// <summary>Thing 14, the Jungle Spray: a sideshow, excitement 30 at its saved price of 20.</summary>
	private const int JungleSpray = 14;

	/// <summary>Thing 16, the Drinks Shop, which declares effects and no excitement.</summary>
	private const int DrinksShop = 16;

	private ParkWorld Park()
	{
		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		return new ParkWorld( new SaveReader( stream ).ReadFile() );
	}

	private RideScript Script()
	{
		using var stream = data.OpenRead( "levels/jungle/rides/bouncy/bouncy.RSE" );
		return new RideScript( new RideScriptFile( stream ) );
	}

	private static ParkBalance Balance() => new( "jungle", easyMode: true );

	private static ParkWorld.NavigatorState StandingStill => new(
		X: 0, Y: 0, VelocityX: 0, VelocityY: 0, TargetX: 0, TargetY: 0,
		Mass: ParkWorld.NavigatorState.DefaultMass, Radius: ParkWorld.NavigatorState.DefaultRadius,
		MaxForce: 0, MaxSpeed: 0, NavMode: 0, CantReachDest: 0, PathFinished: false,
		PathCount: 0, PathTotalCount: 0, PathBufferCount: 0,
		BufferedDistance: 0, TailDistance: 0, TotalDistance: 0, StuckBits: 0 );

	/// <summary>
	/// Lets a guest of <paramref name="kind"/> off the named thing through the real dismissal, with the park's own
	/// mood constants and, unless <paramref name="withScore"/> is false, its kinds' likings.
	/// </summary>
	private Peep LetOff( int thingId, int kind, float hunger = 80f, float happiness = 50f, float vomit = 0f,
		int queuePos = 1, bool withScore = true, Func<ParkWorld.CatalogueObject, ParkWorld.CatalogueObject>? adjust = null )
	{
		var thing = Park().Objects.Single( o => o.ThingId == thingId );

		if ( adjust != null )
			thing = adjust( thing );

		var peep = new Peep( 7, new ParkWorld.GuestState(
			State: (int)PeepState.Riding, SavedState: (int)PeepState.Deciding, PersonType: kind, Cash: 300,
			ExitLevel: 100, Happiness: happiness, Thirst: 80f, Hunger: hunger, Toilet: 10f, Vomit: vomit,
			Litter: 0f, MajorDest: thingId, QueuePos: queuePos, PrankeryIndex: 0 ), StandingStill );

		var script = Script();

		script.Set( ParkRideOperation.DismissVariable, 7 );

		var balance = Balance();

		Assert.IsTrue( new ParkRideOperation( new ParkState( parkIsClosed: false, visitorsToDate: 0 ),
				new Dictionary<int, Peep> { [7] = peep }, new ParkAdmission( balance, 25 ),
				withScore ? new ParkRideScore( balance ) : null )
			.Dismiss( script, thing, tick: 9, new Random( 1 ), catalogue: new ParkItemCatalogue( "jungle", data ) ),
			"the guest should have been let off" );

		return peep;
	}

	/// <summary>The Belly Bounce run at <paramref name="speed"/> against its starting 60, its duration its starting 30.</summary>
	private static Func<ParkWorld.CatalogueObject, ParkWorld.CatalogueObject> AtSpeed( int speed )
		=> thing => thing with { OperatingSpeed = speed, OperatingDuration = 30 };

	/// <summary>What the Belly Bounce's excitement is at <paramref name="speed"/>, so a test states the gap it stands on.</summary>
	private int BellyBounceAt( int speed )
	{
		var thing = AtSpeed( speed )( Park().Objects.Single( o => o.ThingId == BellyBounce ) );
		var item = new ParkItemCatalogue( "jungle", data ).All.Single( i => i.Id == thing.CatalogueId );

		return ParkRideScore.ExcitementOf( thing, item );
	}

	/// <summary>
	/// The four keys are the balance file's, not fallbacks: an absent key reads nought (and the divisor one), so a
	/// misspelt key could not pass here.
	/// </summary>
	[TestMethod]
	public void TheFourKeysComeFromTheBalanceFile()
	{
		var mood = new ParkAdmission( Balance(), 25 );

		Assert.AreEqual( 25, mood.PerfectRide );
		Assert.AreEqual( 15, mood.GoodRide );
		Assert.AreEqual( 5, mood.OKRide );
		Assert.AreEqual( 10, mood.RideVomitDivisor );
	}

	/// <summary>Four off is perfect: the Belly Bounce at 62 is 41, and kind 6 likes 45.</summary>
	[TestMethod]
	public void FourOffIsAPerfectRide()
	{
		Assert.AreEqual( 41, BellyBounceAt( 62 ) );

		Assert.AreEqual( 75f, LetOff( BellyBounce, kind: 6, adjust: AtSpeed( 62 ) ).Happiness, 0.001f,
			"fifty and the perfect ride's twenty-five" );
	}

	/// <summary>Kind 3 likes 35 and the Belly Bounce is 40: five off is good, not perfect.</summary>
	[TestMethod]
	public void FiveOffIsAGoodRide()
	{
		var peep = LetOff( BellyBounce, kind: 3 );

		Assert.AreEqual( 65f, peep.Happiness, 0.001f, "fifty and the good ride's fifteen" );
		Assert.AreEqual( 4f, peep.Vomit, 0.001f, "forty over ten, once, at a hunger of eighty" );
	}

	/// <summary>Fourteen off is good: the Belly Bounce at 74 is 49, and kind 3 likes 35.</summary>
	[TestMethod]
	public void FourteenOffIsAGoodRide()
	{
		Assert.AreEqual( 49, BellyBounceAt( 74 ) );

		Assert.AreEqual( 65f, LetOff( BellyBounce, kind: 3, adjust: AtSpeed( 74 ) ).Happiness, 0.001f );
	}

	/// <summary>Fifteen off is OK, not good: the Belly Bounce at 75 is 50, and kind 1 likes 65.</summary>
	[TestMethod]
	public void FifteenOffIsAnOKRide()
	{
		Assert.AreEqual( 50, BellyBounceAt( 75 ) );

		Assert.AreEqual( 55f, LetOff( BellyBounce, kind: 1, adjust: AtSpeed( 75 ) ).Happiness, 0.001f,
			"fifty and the OK ride's five" );
	}

	/// <summary>Thirty-nine off is still OK: the Belly Bounce at 62 is 41, and kind 0 likes 80.</summary>
	[TestMethod]
	public void ThirtyNineOffIsAnOKRide()
	{
		Assert.AreEqual( 55f, LetOff( BellyBounce, kind: 0, adjust: AtSpeed( 62 ) ).Happiness, 0.001f );
	}

	/// <summary>Kind 0 likes 80: forty off cheers them not at all, and the ride makes them just as sick.</summary>
	[TestMethod]
	public void FortyOffIsNothingButStillSickens()
	{
		var peep = LetOff( BellyBounce, kind: 0 );

		Assert.AreEqual( 50f, peep.Happiness, 0.001f, "forty off is past the OK ride" );
		Assert.AreEqual( 4f, peep.Vomit, 0.001f, "the sickness does not care how well it suited them" );
	}

	/// <summary>
	/// The sickness counts whole twenties of how little hungry the guest is, the hunger truncated first. A hunger of 80.9
	/// counts as 80, one twenty short of starving, so forty over ten once; rounded it would be 81, and nothing at all.
	/// </summary>
	[TestMethod]
	public void TheSicknessTruncatesTheHunger()
	{
		Assert.AreEqual( 4f, LetOff( BellyBounce, kind: 0, hunger: 80.9f ).Vomit, 0.001f, "80.9 is 80: one twenty" );
		Assert.AreEqual( 0f, LetOff( BellyBounce, kind: 0, hunger: 81f ).Vomit, 0.001f, "81 is under a whole twenty" );
	}

	/// <summary>
	/// A guest who is not hungry at all counts five twenties and one at hunger 1 four; the excitement goes over the
	/// divisor in whole numbers too, so the Belly Bounce's 44 at speed 66 counts as 4. A hunger of 0.9 truncates to
	/// nought where rounding would make it 1.
	/// </summary>
	[TestMethod]
	public void ALessHungryGuestIsSickerInWholeSteps()
	{
		Assert.AreEqual( 44, BellyBounceAt( 66 ) );

		Assert.AreEqual( 20f, LetOff( BellyBounce, kind: 0, hunger: 0.9f, adjust: AtSpeed( 66 ) ).Vomit, 0.001f,
			"hunger 0.9 is nought: five twenties of 44 over ten, which is 4" );
		Assert.AreEqual( 16f, LetOff( BellyBounce, kind: 0, hunger: 1f, adjust: AtSpeed( 66 ) ).Vomit, 0.001f,
			"hunger 1 is 99 over twenty, four" );
	}

	/// <summary>
	/// Where the visit leaves both meters when the match would carry them past a hundred: at a hundred. The effects
	/// that follow hold them again, so this is the visit's end state and not the match's own clamp.
	/// </summary>
	[TestMethod]
	public void BothMetersStopAtAHundred()
	{
		var peep = LetOff( BellyBounce, kind: 3, happiness: 95f, vomit: 98f );

		Assert.AreEqual( 100f, peep.Happiness, 0.001f, "ninety-five and fifteen" );
		Assert.AreEqual( 100f, peep.Vomit, 0.001f, "ninety-eight and four" );
	}

	/// <summary>
	/// A sideshow is matched too: the Jungle Spray is 30 and kind 3 likes 35, a good ride, before its winner's cheer.
	/// </summary>
	[TestMethod]
	public void TheSideshowIsMatchedBeforeItsWinnerCheers()
	{
		var peep = LetOff( JungleSpray, kind: 3 );

		Assert.AreEqual( 84f, peep.Happiness, 0.001f, "fifty, the good ride's fifteen and log2(50/20) x 15, nineteen" );
		Assert.AreEqual( 3f, peep.Vomit, 0.001f, "thirty over ten, once, at a hunger of eighty" );
		Assert.AreEqual( 330, peep.Cash, "charged twenty and paid its fifty" );
	}

	/// <summary>A thing with no excitement - every shop - leaves the guest to its own effects alone.</summary>
	[TestMethod]
	public void AThingWithNoExcitementDoesNothing()
	{
		var peep = LetOff( DrinksShop, kind: 3 );

		Assert.AreEqual( 55f, peep.Happiness, 0.001f, "the drink's own five and no more" );
		Assert.AreEqual( 10f, peep.Vomit, 0.001f, "the drink's own ten and no more" );
	}

	/// <summary>The match sits behind the settle-up's gate: a visit that gave nothing never reaches it.</summary>
	[TestMethod]
	public void AVisitThatGaveNothingNeverReachesTheMatch()
	{
		var peep = LetOff( BellyBounce, kind: 3, queuePos: 0 );

		Assert.AreEqual( 35f, peep.Happiness, 0.001f, "only the lost visit's fifteen off" );
		Assert.AreEqual( 0f, peep.Vomit, 0.001f, "and no sickness" );
	}

	/// <summary>Without the kinds' likings the match does not run, rather than guessing a liking.</summary>
	[TestMethod]
	public void WithNoLikingsThereIsNoMatch()
	{
		var peep = LetOff( BellyBounce, kind: 3, withScore: false );

		Assert.AreEqual( 50f, peep.Happiness, 0.001f );
		Assert.AreEqual( 0f, peep.Vomit, 0.001f );
	}
}
