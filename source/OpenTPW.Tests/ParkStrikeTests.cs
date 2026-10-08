using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// The staff strike - <c>docs/exe/ride-operation.md</c>, "The strike": the staff HQ's records and its look as a
/// month turns (<see cref="ParkStrikes"/>), and the staff's side of it (<see cref="StaffBehaviour"/>'s strike
/// arm, state 4 and state 5).
/// </summary>
[TestClass]
[DoNotParallelize]
public class ParkStrikeTests
{
	private BaseFileSystem data = null!;

	[TestInitialize]
	public void MountTheGame() => FileSystem = data = GameData.Required();

	[TestCleanup]
	public void LetThePeopleGo() => TestRun.DeleteEvery<ParkPeople>();

	private ParkWorld Park()
	{
		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		return new ParkWorld( new SaveReader( stream ).ReadFile() );
	}

	private static ParkBalance Balance() => new( "jungle", easyMode: true );

	private const int Handyman = 25;
	private const int Mechanic = 26;
	private const int Entertainer = 27;
	private const int Guard = 28;
	private const int Researcher = 30;

	/// <summary>The first tick at 24 thirty-day months: 16,589 × 3,750 s is 62,208,750 s, past 62,208,000.</summary>
	private const int FirstTickOfMonth24 = 16589;

	private static int Times( string what ) => Unimplemented.Summary.FirstOrDefault( gap => gap.What == what ).Times;

	/// <summary>Answers its values in order, the last for ever, and counts how often it was asked.</summary>
	private sealed class Draws( params int[] values ) : Random
	{
		public int Asked { get; private set; }

		public override int Next() => values[Math.Min( Asked++, values.Length - 1 )];
	}

	/// <summary>The save's five staff, one a kind.</summary>
	private static Staff[] OneOfEach( ParkWorld world )
		=> world.People.Where( person => person.Staff != null )
			.Select( person => new Staff( person.ThingId, person.Model, person.Staff!.Value, person.Navigator ) )
			.ToArray();

	/// <summary><paramref name="count"/> members of one saved member's kind, each with the rest and happiness given.</summary>
	private static Staff[] Several( ParkWorld world, int thing, int count, float rest, float happiness )
	{
		var saved = world.People.Single( person => person.ThingId == thing );

		return Enumerable.Range( 0, count ).Select( i =>
		{
			var member = new Staff( 100 + i, saved.Model, saved.Staff!.Value, saved.Navigator );

			member.Tiredness = rest;
			member.Happiness = happiness;

			return member;
		} ).ToArray();
	}

	private static string Records( ParkStrikes strikes )
		=> string.Join( " ", Enumerable.Range( 0, ParkStrikes.Kinds )
			.Select( kind => $"{strikes.LevelOf( kind )},{(strikes.IsOnStrike( kind ) ? 1 : 0)},{strikes.StampOf( kind )}" ) );

	private static string All( int level, int flag, int stamp )
		=> string.Join( " ", Enumerable.Repeat( $"{level},{flag},{stamp}", ParkStrikes.Kinds ) );

	/// <summary>The shipped save's staff HQ: no force, and five records of {0, 0, 715} beside its five budgets of nought.</summary>
	[TestMethod]
	public void TheSaveHoldsNoForceAndFiveRecordsStamped715()
	{
		var hq = Park().StaffHq!.Value;

		Assert.AreEqual( 0, hq.ForceStrike );
		CollectionAssert.AreEqual(
			Enumerable.Repeat( new ParkWorld.StrikeRecord( 0, 0, 715 ), 5 ).ToArray(), hq.Strikes!.ToArray() );
		CollectionAssert.AreEqual( new int[5], hq.Budgets.ToArray() );

		var strikes = new ParkStrikes( hq );

		Assert.IsFalse( strikes.Force );
		Assert.AreEqual( All( 0, 0, 715 ), Records( strikes ) );
		Assert.AreEqual( All( 0, 0, 0 ), Records( new ParkStrikes() ), "a fresh park's are noughts" );
	}

	/// <summary>
	/// The reader takes each field from its own place: <c>mForceStrike</c> at 16 and the five records of three
	/// dwords from 22, the budgets after them untouched (FileFormats <c>saves.md</c>, "The staff HQ").
	/// </summary>
	[TestMethod]
	public void TheStrikeFieldsAreReadFromTheirPlace()
	{
		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		var payload = new SaveReader( stream ).ReadFile();

		// The staff HQ's record: five stamps of 715 twelve bytes apart, each after two noughts.
		var found = Enumerable.Range( 0, payload.Length - 110 ).Where( at => Enumerable.Range( 0, 5 ).All( i =>
			BitConverter.ToInt32( payload, at + 22 + (12 * i) ) == 0
			&& BitConverter.ToInt32( payload, at + 26 + (12 * i) ) == 0
			&& BitConverter.ToInt32( payload, at + 30 + (12 * i) ) == 715 ) ).ToList();

		Assert.AreEqual( 1, found.Count, "exactly one record holds the five stamps" );

		BitConverter.GetBytes( 7 ).CopyTo( payload, found[0] + 16 );

		for ( var i = 0; i < 15; ++i )
			BitConverter.GetBytes( 100 + i ).CopyTo( payload, found[0] + 22 + (4 * i) );

		var hq = new ParkWorld( payload ).StaffHq!.Value;

		Assert.AreEqual( 7, hq.ForceStrike );
		CollectionAssert.AreEqual( Enumerable.Range( 0, 5 )
			.Select( i => new ParkWorld.StrikeRecord( 100 + (3 * i), 101 + (3 * i), 102 + (3 * i) ) ).ToArray(),
			hq.Strikes!.ToArray() );
		CollectionAssert.AreEqual( new int[5], hq.Budgets.ToArray() );
		Assert.IsTrue( new ParkStrikes( hq ).Force );
	}

	/// <summary>A saved record is carried whole: its level, its flag and its stamp, and the force.</summary>
	[TestMethod]
	public void ASavedDisputeIsCarriedOn()
	{
		var strikes = new ParkStrikes( new ParkWorld.StaffHqState( new int[5], ForceStrike: 1, Strikes:
		[
			new( 1, 0, 10 ), new( 2, 1, 20 ), new( 3, 0, 30 ), new( 4, 1, 40 ), new( 0, 0, 50 )
		] ) );

		Assert.IsTrue( strikes.Force );
		Assert.AreEqual( "1,0,10 2,1,20 3,0,30 4,1,40 0,0,50", Records( strikes ) );
	}

	/// <summary>
	/// The gate (<c>0x00508e85</c>..<c>0x00508eac</c>): a park shut, or one with a guest inside, is not looked at;
	/// an open and empty one is, and the force passes either.
	/// </summary>
	[DataTestMethod]
	[DataRow( false, 0, false, true )]
	[DataRow( false, 1, false, false )]
	[DataRow( true, 0, false, false )]
	[DataRow( true, 3, false, false )]
	[DataRow( false, 8, true, true )]
	[DataRow( true, 0, true, true )]
	public void TheLookWantsTheParkOpenAndEmptyUnlessForced( bool shut, int guests, bool force, bool looked )
	{
		var world = Park();
		var strikes = new ParkStrikes( world.StaffHq ) { Force = force };

		strikes.Look( 1383, shut, guests, OneOfEach( world ) );

		Assert.AreEqual( All( 0, 0, looked ? 1383 : 715 ), Records( strikes ) );
	}

	/// <summary>The months are thirty days of the park's own seconds from tick nought: 24 of them on tick 16,589.</summary>
	[TestMethod]
	public void TwentyFourMonthsAreUpOnTick16589()
	{
		Assert.AreEqual( 0, ParkStrikes.MonthsSinceTheFirstSweep( 691 ) );
		Assert.AreEqual( 1, ParkStrikes.MonthsSinceTheFirstSweep( 692 ) );
		Assert.AreEqual( 3, ParkStrikes.MonthsSinceTheFirstSweep( 2097 ) );
		Assert.AreEqual( 23, ParkStrikes.MonthsSinceTheFirstSweep( FirstTickOfMonth24 - 1 ) );
		Assert.AreEqual( 24, ParkStrikes.MonthsSinceTheFirstSweep( FirstTickOfMonth24 ) );
	}

	/// <summary>Under 24 months a forced look stamps and does nothing else; on the first tick past them it warns.</summary>
	[TestMethod]
	public void NothingIsConsideredBeforeTwentyFourMonths()
	{
		var world = Park();
		var staff = OneOfEach( world );
		var strikes = new ParkStrikes( world.StaffHq ) { Force = true };
		var warned = Times( "STRIKE_WARNING_TO_THE_ADVISOR" );

		strikes.Look( 2097, false, 8, staff );
		Assert.AreEqual( All( 0, 0, 2097 ), Records( strikes ) );

		strikes.Look( FirstTickOfMonth24 - 1, false, 8, staff );
		Assert.AreEqual( All( 0, 0, FirstTickOfMonth24 - 1 ), Records( strikes ) );
		Assert.AreEqual( warned, Times( "STRIKE_WARNING_TO_THE_ADVISOR" ) );

		strikes.Look( FirstTickOfMonth24, false, 8, staff );
		Assert.AreEqual( All( 1, 0, FirstTickOfMonth24 ), Records( strikes ) );
		Assert.AreEqual( warned + 5, Times( "STRIKE_WARNING_TO_THE_ADVISOR" ), "one warning a kind" );
	}

	/// <summary>
	/// The original's run, month by month (<c>q138/orig/a.log</c>): a warning, a strike, the month off, the next
	/// strike a level up, and on to the top level, where it stays. Each strike is posted, counted here.
	/// </summary>
	[TestMethod]
	public void AForcedDisputeWarnsStrikesRestsAMonthAndClimbsToFour()
	{
		var world = Park();
		var staff = OneOfEach( world );
		var strikes = new ParkStrikes( world.StaffHq ) { Force = true };
		var posted = Times( "STRIKE_TO_THE_ADVISOR" );

		(int Tick, int Level, int Flag)[] months =
		[
			(16837, 1, 0), (16843, 2, 1), (17557, 2, 0), (18202, 3, 1), (18916, 3, 0), (19607, 4, 1),
			(20321, 4, 0), (21012, 4, 1)
		];

		foreach ( var (tick, level, flag) in months )
		{
			strikes.Look( tick, false, 8, staff );

			Assert.AreEqual( All( level, flag, tick ), Records( strikes ), $"tick {tick}" );
		}

		Assert.AreEqual( posted + (4 * 5), Times( "STRIKE_TO_THE_ADVISOR" ), "four strikes, five kinds" );
	}

	/// <summary>A kind already stamped with this tick is passed over, and a kind with no member is never stamped.</summary>
	[TestMethod]
	public void AKindIsLookedAtOnceATickAndOnlyWithAMember()
	{
		var world = Park();
		var staff = OneOfEach( world );
		var strikes = new ParkStrikes( world.StaffHq ) { Force = true };

		strikes.Look( 16837, false, 0, staff );
		strikes.Look( 16837, false, 0, staff );
		Assert.AreEqual( All( 1, 0, 16837 ), Records( strikes ), "the second look on the same tick does nothing" );

		var guardsOnly = new ParkStrikes( world.StaffHq ) { Force = true };

		guardsOnly.Look( 16837, false, 0, staff.Where( member => member.ThingId == Guard ).ToArray() );
		Assert.AreEqual( "0,0,715 0,0,715 0,0,715 1,0,16837 0,0,715", Records( guardsOnly ) );
	}

	/// <summary>
	/// The causes with no force (<c>FUN_00509360</c>): more than three members whose rest bytes, or whose
	/// happiness bytes, average under 15. The byte is the float truncated, and the average an unsigned divide.
	/// </summary>
	[DataTestMethod]
	[DataRow( 4, 14.9f, 50f, true )]
	[DataRow( 4, 15f, 50f, false )]
	[DataRow( 4, 50f, 14.9f, true )]
	[DataRow( 4, 50f, 15f, false )]
	[DataRow( 3, 0f, 0f, false )]
	[DataRow( 5, 0f, 100f, true )]
	public void FourTiredOrUnhappyMembersAreACause( int count, float rest, float happiness, bool cause )
	{
		var world = Park();
		var strikes = new ParkStrikes( world.StaffHq );

		strikes.Look( 16837, false, 0, Several( world, Mechanic, count, rest, happiness ) );

		Assert.AreEqual( $"0,0,715 {(cause ? 1 : 0)},0,16837 0,0,715 0,0,715 0,0,715", Records( strikes ) );
	}

	/// <summary>The average is the sum over the count, whole: 14, 14, 14 and 17 make 59, 14 a member.</summary>
	[TestMethod]
	public void TheAverageIsAWholeDivide()
	{
		var world = Park();
		var members = Several( world, Guard, 4, 14f, 50f );

		members[3].Tiredness = 17.9f;

		var strikes = new ParkStrikes( world.StaffHq );

		strikes.Look( 16837, false, 0, members );
		Assert.AreEqual( 1, strikes.LevelOf( 3 ) );

		members[3].Tiredness = 18f;

		var none = new ParkStrikes( world.StaffHq );

		none.Look( 16837, false, 0, members );
		Assert.AreEqual( 0, none.LevelOf( 3 ), "60 over 4 is 15" );
	}

	/// <summary>
	/// With no cause the level and the strike are cleared, and a level of exactly 1 posts the calling-off first.
	/// </summary>
	[DataTestMethod]
	[DataRow( 1, 0, 1 )]
	[DataRow( 2, 0, 0 )]
	[DataRow( 4, 0, 0 )]
	[DataRow( 0, 0, 0 )]
	public void WithNoCauseTheDisputeIsOverAndAWarningIsCalledOff( int level, int flag, int calledOff )
	{
		var world = Park();
		var strikes = new ParkStrikes( new ParkWorld.StaffHqState( new int[5], 0,
			Enumerable.Repeat( new ParkWorld.StrikeRecord( level, flag, 715 ), 5 ).ToArray() ) );
		var before = Times( "STRIKE_CALLED_OFF_TO_THE_ADVISOR" );

		strikes.Look( 16837, false, 0, OneOfEach( world ) );

		Assert.AreEqual( All( 0, 0, 16837 ), Records( strikes ) );
		Assert.AreEqual( before + (calledOff * 5), Times( "STRIKE_CALLED_OFF_TO_THE_ADVISOR" ) );
	}

	/// <summary>The handymen's own cause, the cell ratio, is counted each time it would be asked, and never with the force.</summary>
	[TestMethod]
	public void TheHandymensCellRatioIsCounted()
	{
		var world = Park();
		var staff = OneOfEach( world );
		var before = Times( "STRIKE_HANDYMEN_CELL_RATIO" );

		new ParkStrikes( world.StaffHq ).Look( 16837, false, 0, staff );
		Assert.AreEqual( before + 1, Times( "STRIKE_HANDYMEN_CELL_RATIO" ) );

		new ParkStrikes( world.StaffHq ) { Force = true }.Look( 16837, false, 0, staff );
		Assert.AreEqual( before + 1, Times( "STRIKE_HANDYMEN_CELL_RATIO" ) );
	}

	/// <summary>Ending a strike clears the flag and keeps the level (<c>FUN_005099a0</c>).</summary>
	[TestMethod]
	public void EndingAStrikeKeepsItsLevel()
	{
		var strikes = new ParkStrikes( new ParkWorld.StaffHqState( new int[5], 0,
			Enumerable.Repeat( new ParkWorld.StrikeRecord( 3, 1, 715 ), 5 ).ToArray() ) );

		strikes.EndStrike( 2 );

		Assert.AreEqual( "3,1,715 3,1,715 3,0,715 3,1,715 3,1,715", Records( strikes ) );
	}

	/// <summary>
	/// The month's change reaches the look through the park's people, with the park's clock, its door and its
	/// guests inside: the shipped park shut, or with one guest on a path, keeps its stamps; open with nobody
	/// inside, as it is saved, it is stamped with the clock.
	/// </summary>
	[TestMethod]
	public void TheMonthsChangeLooksAtStrikes()
	{
		var world = Park();
		var state = new ParkState( world );
		var people = new ParkPeople( world, Balance(), null, state );
		var guest = people.Peeps[0];
		var outside = guest.Navigator.Position;

		Assert.AreEqual( 0, people.GateGuestCensus, "the shipped park is saved with every guest outside" );

		state.SetParkClosed( true );
		people.TrainTheStaff();
		Assert.AreEqual( All( 0, 0, 715 ), Records( people.Strikes ), "shut" );

		state.SetParkClosed( false );
		guest.Navigator.Position = FixedVector.AtCell( 48, 22 );
		Assert.AreEqual( 1, people.GateGuestCensus );
		people.TrainTheStaff();
		Assert.AreEqual( All( 0, 0, 715 ), Records( people.Strikes ), "a guest inside" );

		guest.Navigator.Position = outside;
		people.TrainTheStaff();
		Assert.AreEqual( All( 0, 0, 755 ), Records( people.Strikes ), "open and empty" );
		Assert.AreEqual( 755, state.GameTick );
	}

	/// <summary>
	private static void Sweep( ParkPeople people )
	{
		var from = people.State.GameTick;

		for ( var frame = 0; frame < 10 && people.State.GameTick == from; ++frame )
		{
			Time.Update( 0.1f );
			GameClock.Update( paused: false, GameClock.ParkCatchUp );
			people.Update();
		}

		Assert.AreEqual( from + 1, people.State.GameTick, "one sweep" );
	}

	/// <summary>
	/// The running park: two forced months past the 24 put every kind on strike, and with the gate's status
	/// reading open the park's own staff set off and stand on strike; with it reading anything else nobody
	/// goes. A park then shut with a guest still inside keeps its strike.
	/// </summary>
	[DataTestMethod]
	[DataRow( 1, true )]
	[DataRow( 0, false )]
	public void TheParksOwnStaffGoOnStrikeWhileTheGateIsOpen( int gate, bool strikes )
	{
		var world = Park();
		var state = new ParkState( world );
		var people = new ParkPeople( world, Balance(), () => gate, state, random: new Random( 3 ),
			behaviourRandom: new Random( 4 ), rideRandom: new Random( 5 ), staffRandom: new Random( 6 ) );

		Time.Paused = false;
		Time.StepFrames = 0;
		GameClock.Rebase();
		Time.Update( 0f );
		GameClock.Update( paused: false, GameClock.ParkCatchUp );

		state.SetGameTick( 16842 );
		people.Strikes.Force = true;
		people.TrainTheStaff();
		Sweep( people );
		people.TrainTheStaff();

		Assert.AreEqual( All( 2, 1, 16843 ), Records( people.Strikes ) );

		var picketing = new HashSet<int>();

		for ( var sweep = 0; sweep < 400 && people.Staff.Count( member => member.Activity == StaffActivity.OnStrike ) < 2; ++sweep )
		{
			Sweep( people );

			foreach ( var member in people.Staff )
			{
				if ( member.Activity is StaffActivity.GoingOnStrike or StaffActivity.OnStrike )
					picketing.Add( member.ThingId );
			}
		}

		if ( !strikes )
		{
			Assert.AreEqual( 0, picketing.Count, "the gate does not read open" );

			return;
		}

		Assert.IsTrue( people.Staff.Count( member => member.Activity == StaffActivity.OnStrike ) >= 2,
			$"on strike or on the way: {string.Join( ", ", picketing )}" );
		Assert.IsTrue( people.GateGuestCensus > 0, "guests are inside by now" );

		state.SetParkClosed( true );
		Sweep( people );
		Sweep( people );

		Assert.AreEqual( All( 2, 1, 16843 ), Records( people.Strikes ), "shut with guests inside, the strike stands" );
		Assert.IsTrue( people.Staff.Count( member => member.Activity == StaffActivity.OnStrike ) >= 2 );
	}

	/// <summary>One member on the path at (48,22), walking the park's own cells, in the state given.</summary>
	private (Staff Member, PeepWalk Walk, ParkState State) OnThePath( int thing, StaffActivity activity, bool shut = false )
	{
		var world = Park();
		var state = new ParkState( world );
		var saved = world.People.Single( person => person.ThingId == thing );
		var member = new Staff( saved.ThingId, saved.Model, saved.Staff!.Value with
		{
			State = (int)activity, TimeStartedIdling = 0
		}, saved.Navigator );

		member.Happiness = 50f;
		member.Tiredness = 80f;
		member.Navigator.Position = new FixedVector( PeepNavigator.WaypointCentre( 48 ), PeepNavigator.WaypointCentre( 22 ) );
		member.Navigator.Target = member.Navigator.Position;

		PeepWalk walk = shut
			? new PeepWalk( member.Navigator, ( _, _, _ ) => true )
			: new PeepWalk( member.Navigator, new CellEdge( state.Record, ParkPeople.WalkingMode ).Blocked );

		return (member, walk, state);
	}

	private static ParkStrikes Striking( params int[] kinds )
		=> new( new ParkWorld.StaffHqState( new int[5], 0, Enumerable.Range( 0, 5 )
			.Select( kind => new ParkWorld.StrikeRecord( 2, kinds.Contains( kind ) ? 1 : 0, 16843 ) ).ToArray() ) );

	/// <summary>
	/// The decide's first arm (<c>0x00506a4d</c>): the kind on strike and the gate open, four draws aim the member
	/// into the strike area - a cell across and a byte inside it, a cell down and a byte inside it - the mark is
	/// set, and with a route the state is 4 at the hurry's speed.
	/// </summary>
	[DataTestMethod]
	[DataRow( Handyman, 0 )]
	[DataRow( Mechanic, 1 )]
	[DataRow( Entertainer, 2 )]
	[DataRow( Guard, 3 )]
	[DataRow( Researcher, 4 )]
	public void AMemberWhoseKindIsOnStrikeSetsOffForTheStrikeArea( int thing, int kind )
	{
		var (member, walk, state) = OnThePath( thing, StaffActivity.Idle );

		// The idle turn's draw for a sound, then the four: 40 + 7 mod 6, 0x80, 9 + 5 mod 1, 0x40.
		var random = new Draws( 1, 7, 0x180, 5, 0x1240 );
		var behaviour = new StaffBehaviour( Balance(), random, state )
		{
			Strikes = Striking( kind ), GateStatus = () => ParkRides.GateIsOpen
		};

		behaviour.Step( member, walk, playing: null, tick: 16850 );

		Assert.AreEqual( StaffActivity.GoingOnStrike, member.Activity );
		Assert.IsTrue( member.SettingOffForTheStrike );
		Assert.AreEqual( 5, random.Asked );
		Assert.AreEqual( new FixedVector(
			(41 * PeepNavigator.One) + (0x80 * (PeepNavigator.One / 256)),
			(9 * PeepNavigator.One) + (0x40 * (PeepNavigator.One / 256)) ), member.Navigator.Target );
		Assert.AreEqual( Staff.HurryingSpeed, member.PurposeSpeed );
		Assert.AreEqual( 9, member.NextAnimation );
		Assert.AreEqual( (40, 9, 6, 1), behaviour.StrikeArea );
	}

	/// <summary>Another kind's strike, a gate not reading open, and no records at all each leave the member to the kind's own choice.</summary>
	[DataTestMethod]
	[DataRow( 0, 1, true )]
	[DataRow( 3, 0, true )]
	[DataRow( 3, 2, true )]
	[DataRow( 3, -1, true )]
	[DataRow( 3, 1, false )]
	public void NoStrikeWalkForAnotherKindOrAShutGate( int kindOnStrike, int gate, bool records )
	{
		var (member, walk, state) = OnThePath( Guard, StaffActivity.Idle );
		var behaviour = new StaffBehaviour( Balance(), new Draws( 1 ), state )
		{
			Strikes = records ? Striking( kindOnStrike ) : null, GateStatus = () => gate
		};

		behaviour.Step( member, walk, playing: null, tick: 16850 );

		Assert.AreNotEqual( StaffActivity.GoingOnStrike, member.Activity );
		Assert.IsFalse( member.SettingOffForTheStrike );
	}

	/// <summary>
	/// With no route to the strike area the arm answers nothing and the kind's own choice follows, the mark left
	/// set as the original leaves it (<c>0x00506b19</c>).
	/// </summary>
	[TestMethod]
	public void WithNoRouteToTheStrikeAreaTheMemberCarriesOn()
	{
		var (member, walk, state) = OnThePath( Guard, StaffActivity.Idle, shut: true );
		var random = new Draws( 1 );
		var behaviour = new StaffBehaviour( Balance(), random, state )
		{
			Strikes = Striking( 3 ), GateStatus = () => ParkRides.GateIsOpen
		};

		behaviour.Step( member, walk, playing: null, tick: 16851 );

		Assert.AreEqual( StaffActivity.Idle, member.Activity );
		Assert.IsTrue( member.SettingOffForTheStrike );
		Assert.IsTrue( random.Asked >= 5, "the four draws were taken" );
	}

	/// <summary>
	/// State 4 (<c>FUN_005056e0</c>): thought <c>0x15</c> on every turn, and the walk's end, here a walk that
	/// cannot be made, clears the mark and stands the member on strike with the hurry off.
	/// </summary>
	[TestMethod]
	public void TheWalkToThePicketThinksOfItEveryTurnAndEndsOnStrike()
	{
		var (member, walk, state) = OnThePath( Guard, StaffActivity.Idle );
		var behaviour = new StaffBehaviour( Balance(), new Draws( 1, 7, 0x180, 5, 0x1240 ), state )
		{
			Strikes = Striking( 3 ), GateStatus = () => ParkRides.GateIsOpen
		};

		behaviour.Step( member, walk, playing: null, tick: 16850 );
		Assert.AreEqual( StaffActivity.GoingOnStrike, member.Activity );
		Assert.AreNotEqual( 0x15, member.Thoughts.Last );

		var from = member.Navigator.Position;

		behaviour.Step( member, walk, playing: null, tick: 16851 );
		Assert.AreEqual( StaffActivity.GoingOnStrike, member.Activity );
		Assert.AreEqual( 0x15, member.Thoughts.Last );
		Assert.AreNotEqual( from, member.Navigator.Position, "a step of the walk" );
		Assert.IsTrue( member.SettingOffForTheStrike );

		var tick = 16852;

		while ( member.Activity == StaffActivity.GoingOnStrike && tick < 16852 + 2000 )
			behaviour.Step( member, walk, playing: null, tick++ );

		Assert.AreEqual( StaffActivity.OnStrike, member.Activity );
		Assert.AreEqual( (41, 9), member.Navigator.Position.Cell, "inside the strike area" );
		Assert.IsFalse( member.SettingOffForTheStrike );
		Assert.AreEqual( 0, member.PurposeSpeed );
		Assert.AreEqual( 0x13, member.NextAnimation );

		// A walk that fails ends the same way.
		var (stuck, shutWalk, shutState) = OnThePath( Guard, StaffActivity.GoingOnStrike, shut: true );

		stuck.SettingOffForTheStrike = true;
		stuck.Navigator.Target = new FixedVector( PeepNavigator.WaypointCentre( 41 ), PeepNavigator.WaypointCentre( 9 ) );

		new StaffBehaviour( Balance(), new Draws( 1 ), shutState ).Step( stuck, shutWalk, playing: null, tick: 16851 );

		Assert.AreEqual( StaffActivity.OnStrike, stuck.Activity );
		Assert.IsFalse( stuck.SettingOffForTheStrike );
		Assert.AreEqual( 0x15, stuck.Thoughts.Last );
	}

	/// <summary>
	/// A striker's turn on the spot (<c>FUN_00506300</c>): one draw, and on its low three bits nought the facing
	/// moves by the draw's low byte less 128; a sum past <c>0x7ff</c>, a turn below nought among them, is held at
	/// <c>0x7ff</c>. No sound is drawn for.
	/// </summary>
	[DataTestMethod]
	[DataRow( 0x400, 0x08, 0x388 )]
	[DataRow( 0x400, 0xf8, 0x478 )]
	[DataRow( 0x400, 0x1f8, 0x478 )]
	[DataRow( 0x400, 0x09, 0x400 )]
	[DataRow( 0x400, 0x07, 0x400 )]
	[DataRow( 0x010, 0x08, 0x7ff )]
	[DataRow( 0x7f0, 0xf8, 0x7ff )]
	[DataRow( 0x078, 0x00, 0x7ff )]
	[DataRow( 0x080, 0x00, 0x000 )]
	public void AStrikerTurnsOnTheSpot( int heading, int draw, int turned )
	{
		var (member, walk, state) = OnThePath( Guard, StaffActivity.OnStrike );
		var random = new Draws( draw );
		var sounded = 0;
		var behaviour = new StaffBehaviour( Balance(), random, state )
		{
			Strikes = Striking( 3 ), GateStatus = () => ParkRides.GateIsOpen, Sound = ( _, _ ) => ++sounded
		};

		walk.Heading = heading;
		behaviour.Step( member, walk, playing: null, tick: 17000 );

		Assert.AreEqual( turned, walk.Heading );
		Assert.AreEqual( 1, random.Asked );
		Assert.AreEqual( (0, 0, 0), (sounded, member.Sounds.Draws, member.Sounds.Played) );
		Assert.AreEqual( StaffActivity.OnStrike, member.Activity );
	}

	/// <summary>
	/// The strike's two ends. The kind's flag cleared by the month's look: the striker walks for the park
	/// entrance's cell. The park shut with nobody inside: the striker's own turn clears the flag and walks; shut
	/// with a guest inside, or open and empty, the picket stands.
	/// </summary>
	[DataTestMethod]
	[DataRow( false, false, 0, true )]
	[DataRow( true, true, 0, true )]
	[DataRow( true, true, 1, false )]
	[DataRow( true, false, 0, false )]
	public void TheStrikeEndsWithTheFlagOrAParkShutAndEmpty( bool flag, bool shut, int guests, bool leaves )
	{
		var (member, walk, state) = OnThePath( Guard, StaffActivity.OnStrike );
		var strikes = Striking( flag ? [3] : [] );
		var behaviour = new StaffBehaviour( Balance(), new Draws( 1 ), state )
		{
			Strikes = strikes, GateStatus = () => ParkRides.GateIsOpen, GuestsInside = () => guests
		};

		state.SetParkClosed( shut );
		behaviour.Step( member, walk, playing: null, tick: 17558 );

		Assert.AreEqual( leaves ? StaffActivity.Walking : StaffActivity.OnStrike, member.Activity );
		Assert.AreEqual( flag && !leaves, strikes.IsOnStrike( 3 ) );
		Assert.AreEqual( 2, strikes.LevelOf( 3 ), "the level is kept" );

		if ( leaves )
		{
			Assert.AreEqual( new FixedVector( PeepNavigator.WaypointCentre( 47 ), PeepNavigator.WaypointCentre( 17 ) ),
				member.Navigator.Target );
			Assert.AreEqual( (47, 17), behaviour.EntranceA );
		}
	}

	/// <summary>A striker with no route to the park entrance stands idle, stamped nought.</summary>
	[TestMethod]
	public void AStrikerWithNoRouteBackStandsIdle()
	{
		var (member, walk, state) = OnThePath( Guard, StaffActivity.OnStrike, shut: true );
		var behaviour = new StaffBehaviour( Balance(), new Draws( 1 ), state ) { Strikes = Striking() };

		behaviour.Step( member, walk, playing: null, tick: 17558 );

		Assert.AreEqual( StaffActivity.Idle, member.Activity );
		Assert.AreEqual( 0, member.TimeStartedIdling );
	}

	/// <summary>
	/// The console's clock writes the calendar's counter with the park's, so the next advance reads the date of
	/// that tick and a write into another month is a month's change.
	/// </summary>
	[TestMethod]
	public void WritingTheCalendarsCounterIsAMonthsChange()
	{
		Time.Paused = false;
		Time.StepFrames = 0;
		GameClock.Rebase();
		GameCalendar.Rebase();
		Time.Update( 0f );
		GameClock.Update( false, GameClock.ParkCatchUp );
		GameCalendar.Update();

		GameCalendar.SetCounter( 16836 );

		Assert.AreEqual( 16836, GameCalendar.Counter );

		var turned = false;

		for ( var frame = 0; frame < 600 && GameCalendar.Counter == 16836; ++frame )
		{
			Time.Update( 1f / 60f );
			GameClock.Update( false, GameClock.ParkCatchUp );
			GameCalendar.Update();
			turned |= GameCalendar.MonthRolled;
		}

		Assert.AreEqual( 16837, GameCalendar.Counter );
		Assert.AreEqual( new DateTime( 2001, 12, 31 ), GameCalendar.Now.Date );
		Assert.IsTrue( turned, "January 2000 to December 2001 is a month's change" );

		GameCalendar.Rebase();
	}
}
