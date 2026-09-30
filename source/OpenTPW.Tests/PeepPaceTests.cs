using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// A guest's walking speed - <see cref="Peep.Pace"/>, the first half of <c>FUN_004fa870</c> and <c>FUN_00510190</c>:
/// the three speed words eased a quarter of the way each sweep into the mover's speed and force
/// (<c>docs/exe/ride-operation.md</c>, "Where a WALKING peep is drawn"). Every number was worked out in single
/// precision before it was asserted.
/// <para>
/// The arithmetic runs without the game; the save's four speed words are read from Lost Kingdom's, and skipped where
/// there is no installation - see <see cref="GameData"/>.
/// </para>
/// </summary>
[TestClass]
public class PeepPaceTests
{
	private static ParkWorld.NavigatorState Mover => new(
		X: 0, Y: 0, VelocityX: 0, VelocityY: 0, TargetX: 0, TargetY: 0,
		Mass: ParkWorld.NavigatorState.DefaultMass, Radius: ParkWorld.NavigatorState.DefaultRadius,
		MaxForce: 1, MaxSpeed: 1, NavMode: 0, CantReachDest: 0, PathFinished: true,
		PathCount: 0, PathTotalCount: 0, PathBufferCount: 0,
		BufferedDistance: 0, TailDistance: 0, TotalDistance: 0, StuckBits: 0 );

	private static ParkWorld.GuestState Standing => new(
		State: (int)PeepState.Deciding, SavedState: (int)PeepState.Deciding, PersonType: 0, Cash: 300,
		ExitLevel: 100, Happiness: 50f, Thirst: 0f, Hunger: 0f, Toilet: 0f, Vomit: 0f,
		Litter: 0f, MajorDest: 0, QueuePos: 0, PrankeryIndex: 0 );

	private static Peep Walker( int adjustor, int baseSpeed, float previous, int hurry )
		=> new( 7, Standing, Mover, new ParkWorld.PaceState( adjustor, baseSpeed, previous, hurry ) );

	/// <summary>
	/// <b>A guest settled at their own speed keeps it</b>: at a base of 120, 1.2 is where the ease stands still, and it
	/// makes a speed of 15728 and a force of 31457, the figures the shipped park's settled guests are saved with.
	/// </summary>
	[TestMethod]
	public void ASettledGuestKeepsTheirSpeed()
	{
		var guest = Walker( 0, 120, 1.2f, 0 );

		guest.Pace();

		Assert.AreEqual( 1.2f, guest.PreviousSpeed, "a quarter of the way to where they already are" );
		Assert.AreEqual( (15728, 31457), (guest.Navigator.MaxSpeed, guest.Navigator.MaxForce) );
	}

	/// <summary>
	/// <b>A scoop of ice cream at the stock amount is three hundredths, gone in three sweeps</b> (one a sweep below a
	/// hundred), and the speed it gave eases up to its peak on the second and back down after: 15826, 15867, 15865,
	/// 15831 against the settled 15728.
	/// </summary>
	[TestMethod]
	public void SugarPeaksOnTheSecondSweepAndIsGoneInThree()
	{
		var guest = Walker( 3, 120, 1.2f, 0 );

		var seen = Enumerable.Range( 0, 4 ).Select( _ =>
		{
			guest.Pace();
			return (guest.Navigator.MaxSpeed, guest.Navigator.MaxForce, guest.AdjustorSpeed);
		} ).ToArray();

		CollectionAssert.AreEqual( new[] { (15826, 31653, 2), (15867, 31735, 1), (15865, 31731, 0), (15831, 31663, 0) }, seen );
	}

	/// <summary>
	/// <b>The hurry is summed in with the base</b>: a guest at 120 hurrying at 25 eases toward 1.45, so the first
	/// sweep from 1.2 goes a quarter of the 0.25 up.
	/// </summary>
	[TestMethod]
	public void TheHurryIsSummedInWithTheBase()
	{
		var guest = Walker( 0, 120, 1.2f, Peep.HurryingSpeed );

		guest.Pace();

		Assert.AreEqual( 1.2625f, guest.PreviousSpeed, 0.00001f );
		Assert.AreEqual( 16547, guest.Navigator.MaxSpeed, "1.2625 x 13107.2, truncated" );
	}

	/// <summary>
	/// <b>The mover takes no more than a speed of two</b>, while the eased speed goes on past it; and <b>neither the
	/// speed nor the force is ever under a hundredth of a cell</b>, 655, whatever the words sum to.
	/// </summary>
	[TestMethod]
	public void TheMoverIsHeldToTwoAndToAHundredthOfACell()
	{
		var fast = Walker( 100, 140, 1.9f, 50 );

		fast.Pace();

		Assert.AreEqual( 2.15f, fast.PreviousSpeed, 0.00001f, "the eased speed itself is not held" );
		Assert.AreEqual( (26214, 52428), (fast.Navigator.MaxSpeed, fast.Navigator.MaxForce), "the mover's is" );

		var still = Walker( 0, 0, 0f, 0 );

		still.Pace();

		Assert.AreEqual( (655, 655), (still.Navigator.MaxSpeed, still.Navigator.MaxForce) );
	}

	/// <summary>
	/// <b>The sugar falls to 99 hundredths of itself, taken as a word</b> (<c>IMUL CX,CX,0x63</c>): one a sweep below a
	/// hundred, and past 661 the product wraps.
	/// </summary>
	[TestMethod]
	public void TheSugarFallsByAHundredthTakenAsAWord()
	{
		foreach ( var (from, to) in new[] { (1, 0), (100, 99), (661, 654), (662, 0), (700, 37) } )
		{
			var guest = Walker( from, 120, 1.2f, 0 );

			guest.Pace();

			Assert.AreEqual( to, guest.AdjustorSpeed, $"from {from}" );
		}
	}

	/// <summary>
	/// <b>An arrival walks off from a standstill</b>: the eased speed starts at nought and the hurry at 25, so the first
	/// sweep gives a quarter of the base and the hurry.
	/// </summary>
	[TestMethod]
	public void AnArrivalsFirstSweepIsAQuarterOfTheWay()
	{
		int[] first = [2785, 3440, 4096, 4751, 5406];

		for ( var i = 0; i < Peep.BaseSpeeds.Length; ++i )
		{
			var guest = Walker( 0, Peep.BaseSpeeds[i], 0f, Peep.HurryingSpeed );

			guest.Pace();

			Assert.AreEqual( first[i], guest.Navigator.MaxSpeed, $"base {Peep.BaseSpeeds[i]}" );
		}
	}

	/// <summary>A guest built from a bare record, as a test builds one, keeps the speed its navigator was given.</summary>
	[TestMethod]
	public void AGuestWithNoSpeedWordsKeepsTheNavigatorsSpeed()
	{
		var guest = new Peep( 7, Standing, Mover );

		guest.Pace();

		Assert.IsFalse( guest.Paced );
		Assert.AreEqual( (1, 1), (guest.Navigator.MaxSpeed, guest.Navigator.MaxForce) );
	}

	// ---- the save ----

	/// <summary>
	/// <b>Every person's saved speed and force are their saved eased speed, truncated</b>: the person base's words at
	/// 32, 34, 220 and 236 read in their places, every base one of the five and every hurry 0, 25 or 50. Staff
	/// included: the mover is every person's.
	/// </summary>
	[TestMethod]
	public void EverySavedMoverIsItsEasedSpeedTruncated()
	{
		var data = GameData.Required();

		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		var world = new ParkWorld( new SaveReader( stream ).ReadFile() );

		Assert.AreEqual( 18, world.People.Count );

		foreach ( var person in world.People )
		{
			var pace = person.Pace!.Value;
			var speed = System.Math.Min( pace.PreviousSpeed, Peep.MostSpeed );

			Assert.AreEqual( System.Math.Max( 655, (int)(speed * Peep.MaxSpeedPerSpeed) ), person.Navigator.MaxSpeed, $"{person.ThingId}'s speed" );
			Assert.AreEqual( System.Math.Max( 655, (int)(speed * Peep.MaxForcePerSpeed) ), person.Navigator.MaxForce, $"{person.ThingId}'s force" );
			CollectionAssert.Contains( Peep.BaseSpeeds, pace.BaseSpeed, $"{person.ThingId}'s base" );
			CollectionAssert.Contains( new[] { 0, 25, 50 }, pace.PurposeSpeed, $"{person.ThingId}'s hurry" );
		}

		// And the sugar's word is nought on every one: nobody in the shipped park has eaten an ice cream.
		Assert.IsTrue( world.People.All( person => person.Pace!.Value.AdjustorSpeed == 0 ) );
	}

	/// <summary>
	/// <b>The sugar's word is read at 32 and the hurry's at 236</b>: nought on everyone in the shipped park, which a
	/// reader at the wrong offset could read too, so guest 42's record is given an ice cream's 7 and a hurry of 25
	/// first, and the loaded guest carries both.
	/// </summary>
	[TestMethod]
	public void TheSugarAndTheHurryAreReadInTheirPlaces()
	{
		var data = GameData.Required();
		var bytes = new SaveReader( new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) ) ).ReadFile();
		var guest42 = new ParkWorld( bytes ).People.Single( person => person.ThingId == 42 );
		var saved = guest42.Guest!.Value;

		// Guest 42's record, found by its own cash at 414, mExitLevel at 418 and kind at 468.
		var record = Enumerable.Range( 0, bytes.Length - 533 ).Single( at =>
			System.BitConverter.ToInt32( bytes, at + 414 ) == saved.Cash && System.BitConverter.ToInt32( bytes, at + 418 ) == saved.ExitLevel
			&& bytes[at + 468] == saved.PersonType );

		System.BitConverter.GetBytes( (ushort)7 ).CopyTo( bytes, record + 32 );
		System.BitConverter.GetBytes( (ushort)25 ).CopyTo( bytes, record + 236 );

		var world = new ParkWorld( bytes );
		var patched = world.People.Single( person => person.ThingId == 42 ).Pace!.Value;

		Assert.AreEqual( (7, 60, guest42.Pace!.Value.PreviousSpeed, 25),
			(patched.AdjustorSpeed, patched.BaseSpeed, patched.PreviousSpeed, patched.PurposeSpeed) );

		var guest = ParkPeople.PeepsIn( world ).Single( peep => peep.ThingId == 42 );

		Assert.AreEqual( (7, 60, 25, true), (guest.AdjustorSpeed, guest.BaseSpeed, guest.PurposeSpeed, guest.Paced), "handed to the guest" );
	}

	/// <summary>
	/// <b>A guest loaded settled walks at the speed they were saved with</b>: a first sweep leaves eight of the shipped
	/// park's thirteen guests, those settled in single precision, exactly where the file had them.
	/// </summary>
	[TestMethod]
	public void ALoadedSettledGuestKeepsTheSavedSpeed()
	{
		var data = GameData.Required();

		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		var world = new ParkWorld( new SaveReader( stream ).ReadFile() );

		var settled = 0;

		foreach ( var guest in ParkPeople.PeepsIn( world ) )
		{
			var saved = (guest.Navigator.MaxSpeed, guest.Navigator.MaxForce);
			var before = guest.PreviousSpeed;

			guest.Pace();

			if ( guest.PreviousSpeed != before )
				continue;

			settled++;
			Assert.AreEqual( saved, (guest.Navigator.MaxSpeed, guest.Navigator.MaxForce), $"guest {guest.ThingId}" );
		}

		Assert.AreEqual( 8, settled, "the base-140 pair are settled only at a wider precision, and three at base 100 are still easing" );
	}
}
