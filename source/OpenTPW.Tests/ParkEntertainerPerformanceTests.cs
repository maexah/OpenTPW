using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// The entertainer's performance - <c>FUN_004d46d0</c>'s look and state <c>0xe</c>
/// (<c>docs/exe/ride-operation.md</c>, "The entertainer's performance"). These read real game files and are
/// skipped where there is no installation - see <see cref="GameData"/>.
/// </summary>
[TestClass]
public class ParkEntertainerPerformanceTests
{
	private BaseFileSystem data = null!;

	[TestInitialize]
	public void MountTheGame() => FileSystem = data = GameData.Required();

	private const int Entertainer = 27;

	/// <summary>The saved entertainer's grade, 3: 50 sweeps of work and four cells of reach.</summary>
	private const int Grade = 3;

	private ParkWorld Park()
	{
		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		return new ParkWorld( new SaveReader( stream ).ReadFile() );
	}

	private static ParkBalance Balance() => new( "jungle", easyMode: true );

	/// <summary>The saved entertainer on the path at (48,22), walking with a walk that has ended.</summary>
	private (Staff Member, PeepWalk Walk, ParkState State) OnThePath()
	{
		var world = Park();
		var state = new ParkState( world );
		var saved = world.People.Single( person => person.ThingId == Entertainer );
		var member = new Staff( saved.ThingId, saved.Model, saved.Staff!.Value with
		{
			State = (int)StaffActivity.Walking, TimeStartedIdling = 5,
			PatrolBottomLeft = MapStep.CellId( 0, 0 ), PatrolTopRight = MapStep.CellId( 127, 127 )
		}, saved.Navigator );

		member.Happiness = 50f;
		member.Tiredness = 80f;
		member.Navigator.Position = new FixedVector( PeepNavigator.WaypointCentre( 48 ), PeepNavigator.WaypointCentre( 22 ) );
		member.Navigator.Target = member.Navigator.Position;

		return (member, new PeepWalk( member.Navigator, new CellEdge( state.Record, ParkPeople.WalkingMode ).Blocked ), state);
	}

	/// <summary>Answers one value and counts how often it was asked.</summary>
	private sealed class CountedDraw( int value ) : Random
	{
		public int Asked { get; private set; }

		public override int Next()
		{
			++Asked;

			return value;
		}
	}

	private static int Counted( string gap ) => Unimplemented.Summary.FirstOrDefault( entry => entry.What == gap ).Times;

	[TestMethod]
	public void TheSavedEntertainerIsGradeThreeAndTheBalanceGivesItFiftySweepsAndFourCells()
	{
		var (member, _, _) = OnThePath();
		var behaviour = new StaffBehaviour( Balance() );

		Assert.AreEqual( Grade, member.PayGrade );
		CollectionAssert.AreEqual( new[] { 10, 20, 30, 50, 75 },
			Enumerable.Range( 0, 5 ).Select( behaviour.EntertainerWorkDurationAt ).ToArray() );
		CollectionAssert.AreEqual( new[] { 3, 3, 4, 4, 5 },
			Enumerable.Range( 0, 5 ).Select( behaviour.EntertainerActivationDistanceAt ).ToArray() );
	}

	/// <summary>
	/// A draw mod 3 of nought and a guest in reach: the look is asked of the entertainer's own packed cell with the
	/// grade's reach, a second draw is taken, animation <c>0xd</c> is queued for a bank of one group, and the
	/// state and its stamp are written with no setter - the idle stamp and the place are left alone.
	/// </summary>
	[DataTestMethod]
	[DataRow( 0, 3 )]
	[DataRow( 3, 4 )]
	[DataRow( 4, 5 )]
	public void AGuestInReachStartsAPerformanceWhereTheEntertainerStands( int grade, int reach )
	{
		var (member, walk, state) = OnThePath();
		var random = new CountedDraw( 0 );
		(int Cell, int Reach)? looked = null;

		member.PayGrade = grade;

		new StaffBehaviour( Balance(), random, state )
		{
			GuestsNear = ( cell, within ) =>
			{
				looked = (cell, within);

				return 1;
			},
			StateGroupsOf = _ => 1
		}.Step( member, walk, playing: null, tick: 1001 );

		Assert.AreEqual( ((22 * 128) + 48 + 1, reach), looked );
		Assert.AreEqual( StaffActivity.Performing, member.Activity );
		Assert.AreEqual( 1001, member.TimeStartedEntertaining );
		Assert.AreEqual( 0xd, member.NextAnimation );
		Assert.AreEqual( 5, member.TimeStartedIdling, "no setter ran" );
		Assert.AreEqual( 3, random.Asked, "the walking turn's draw for a sound, the look's and the animation's" );
		Assert.AreEqual( (48, 22), member.Navigator.Position.Cell );
		Assert.AreEqual( (48, 22), member.Navigator.Target.Cell, "nowhere to walk to" );
	}

	/// <summary>Nobody in reach, or a draw that is not a multiple of three: the walk, and no second draw.</summary>
	[DataTestMethod]
	[DataRow( 0, 0, 1 )]
	[DataRow( 1, 1, 0 )]
	[DataRow( 2, 1, 0 )]
	public void WithNobodyInReachOrAnotherDrawTheEntertainerWalks( int draw, int guests, int looks )
	{
		var (member, walk, state) = OnThePath();
		var asked = 0;

		new StaffBehaviour( Balance(), new CountedDraw( draw ), state )
		{
			GuestsNear = ( _, _ ) =>
			{
				++asked;

				return guests;
			},
			StateGroupsOf = _ => 1
		}.Step( member, walk, playing: null, tick: 1001 );

		Assert.AreEqual( looks, asked );
		Assert.AreEqual( StaffActivity.Walking, member.Activity );
		Assert.AreEqual( 0, member.TimeStartedEntertaining );
	}

	/// <summary>
	/// The animation by the bank's groups (<c>0x004d47b5</c>): <c>0xd</c> for one, <c>0xd</c> + the draw mod
	/// (groups - 1) for more; none, counted, for a bank with no group.
	/// </summary>
	[DataTestMethod]
	[DataRow( 1, 3, 0xd )]
	[DataRow( 2, 3, 0xd )]
	[DataRow( 3, 3, 0xe )]
	[DataRow( 4, 3, 0xd )]
	[DataRow( 4, 9, 0xd )]
	[DataRow( 4, 6, 0xd )]
	[DataRow( 3, 9, 0xe )]
	[DataRow( 0, 3, 0 )]
	public void TheAnimationIsTheBanksStateByItsGroups( int groups, int draw, int animation )
	{
		var (member, walk, state) = OnThePath();
		var before = Counted( "ENTERTAINER_BANK_WITHOUT_A_STATE_GROUP" );

		member.NextAnimation = 0;

		new StaffBehaviour( Balance(), new CountedDraw( draw ), state )
		{
			GuestsNear = ( _, _ ) => 1, StateGroupsOf = _ => groups
		}.Step( member, walk, playing: null, tick: 1001 );

		Assert.AreEqual( StaffActivity.Performing, member.Activity );
		Assert.AreEqual( animation, member.NextAnimation );
		Assert.AreEqual( groups == 0 ? 1 : 0, Counted( "ENTERTAINER_BANK_WITHOUT_A_STATE_GROUP" ) - before );
	}

	/// <summary>
	/// A spell is the stamp's sweep and WorkDuration more: a turn of work on each of the 51 sweeps after the
	/// stamp, (6 - grade) x 0.025 of rest and x 0.01 of mood, and on the 51st the end's sound, counted, and the
	/// decide again in the same turn - here, with the guests gone on a multiple of four, the stand.
	/// </summary>
	[TestMethod]
	public void ASpellIsFiftyOneTurnsOfWorkThenTheDecideAgain()
	{
		var (member, walk, state) = OnThePath();
		var guests = 1;
		var sounded = new List<(int Thing, int Effect)>();
		var behaviour = new StaffBehaviour( Balance(), new CountedDraw( 0 ), state )
		{
			GuestsNear = ( _, _ ) => guests, StateGroupsOf = _ => 1, Sound = ( who, effect ) => sounded.Add( (who.ThingId, effect) )
		};

		behaviour.Step( member, walk, playing: null, tick: 1001 );

		Assert.AreEqual( 80f, member.Tiredness, "the start costs nothing" );
		CollectionAssert.AreEqual( new[] { (member.ThingId, 0xa4) }, sounded, "the walking turn's draw of nought" );
		sounded.Clear();

		member.NextAnimation = 0;
		guests = 0;

		for ( var tick = 1002; tick <= 1051; ++tick )
		{
			behaviour.Step( member, walk, playing: null, tick );

			Assert.AreEqual( StaffActivity.Performing, member.Activity, $"mGameTick {tick}" );
			Assert.AreEqual( 80f - ((tick - 1001) * 0.075f), member.Tiredness, 0.001f );
		}

		Assert.AreEqual( 0, sounded.Count, "a performing turn sounds nothing" );
		Assert.AreEqual( 0, member.NextAnimation );

		behaviour.Step( member, walk, playing: null, tick: 1052 );

		Assert.AreEqual( (1, 2), member.Sounds, "the end's sound takes no draw: the one was the walking turn's" );

		Assert.AreEqual( StaffActivity.Idle, member.Activity );
		Assert.AreEqual( 0, member.TimeStartedIdling, "idle from anything but a walk stamps nought" );
		Assert.AreEqual( SpriteScript.Standing, member.NextAnimation );
		Assert.AreEqual( 80f - 3.825f, member.Tiredness, 0.001f );
		Assert.AreEqual( 50f - 1.53f, member.Happiness, 0.001f );
		CollectionAssert.AreEqual( new[] { (member.ThingId, StaffBehaviour.PerformanceEndSound) }, sounded );
		Assert.AreEqual( 0x87, StaffBehaviour.PerformanceEndSound );
		Assert.AreEqual( 1001, member.TimeStartedEntertaining );
	}

	/// <summary>With a guest still in reach at the end, the same turn starts another spell on a fresh stamp.</summary>
	[TestMethod]
	public void ASpellEndingBesideAGuestRunsStraightIntoAnother()
	{
		var (member, walk, state) = OnThePath();
		var stampsAtTheEndsSound = new List<int>();
		var behaviour = new StaffBehaviour( Balance(), new CountedDraw( 0 ), state )
		{
			GuestsNear = ( _, _ ) => 1, StateGroupsOf = _ => 1,
			Sound = ( who, effect ) =>
			{
				if ( effect == StaffBehaviour.PerformanceEndSound )
					stampsAtTheEndsSound.Add( who.TimeStartedEntertaining );
			}
		};

		for ( var tick = 1001; tick <= 1051; ++tick )
			behaviour.Step( member, walk, playing: null, tick );

		Assert.AreEqual( 1001, member.TimeStartedEntertaining );

		member.NextAnimation = 0;
		behaviour.Step( member, walk, playing: null, tick: 1052 );

		CollectionAssert.AreEqual( new[] { 1001 }, stampsAtTheEndsSound, "the end sounds before the decide starts the next spell" );

		Assert.AreEqual( StaffActivity.Performing, member.Activity );
		Assert.AreEqual( 1052, member.TimeStartedEntertaining );
		Assert.AreEqual( 0xd, member.NextAnimation, "queued again, so the script starts over" );
	}

	/// <summary>
	/// A saved <c>mTimeStartedEntertaining</c> is kept. The shipped park's own reads nought, so the read at +503
	/// is pinned by no file here.
	/// </summary>
	[TestMethod]
	public void ASavedStampIsKept()
	{
		var saved = Park().People.Single( person => person.ThingId == Entertainer );

		Assert.AreEqual( 0, saved.Staff!.Value.TimeStartedEntertaining );
		Assert.AreEqual( 640, new Staff( saved.ThingId, saved.Model,
			saved.Staff.Value with { TimeStartedEntertaining = 640 }, saved.Navigator ).TimeStartedEntertaining );
	}

	/// <summary>
	/// A sprite read part-way through a state's script has no frames a direction: the park reads them from the
	/// bank again on its first sweep, and with no bank the sprite is stood, counted.
	/// </summary>
	[DataTestMethod]
	[DataRow( true )]
	[DataRow( false )]
	public void ASpriteSavedOnAStateScriptGetsItsFramesFromTheBankOrStands( bool packed )
	{
		var world = Park();
		using var clock = new SimulationClockScope();
		var previousState = ParkState.Current;
		var previousPeople = ParkPeople.Current;
		ParkPeople? people = null;

		try
		{
			var folder = ParkGuestSprites.BanksIn( data, "esprites/Jungle/Entertainers", 4 );
			var bank = new SpriteBankFile( new MemoryStream( data.ReadAllBytes(
				folder.Single( file => file.Contains( "SPR_EX", StringComparison.OrdinalIgnoreCase ) ) ) ) );
			var before = Counted( "SPRITE_STATE_ANIMATION_NOT_STARTED" );

			people = new ParkPeople( world, Balance(), state: new ParkState( world ), random: new Random( 1 ),
				behaviourRandom: new Random( 2 ), rideRandom: new Random( 3 ), staffRandom: new Random( 4 ) )
			{
				BankOf = id => packed && id == Entertainer ? bank : null
			};

			var sprite = people.SpriteFor( Entertainer )!;

			sprite.StartState( new SpriteStateGroup( 1, 5, 0, 0 ), 0 );
			Assert.AreEqual( 17, bank.Sets[4].FramesPerDirection, "SPR_EX's set 4 is the long one" );

			SimulationClockScope.Frame( 8 * GameClock.TickSeconds );
			people.Update();

			Assert.AreEqual( packed ? 17 : 0, sprite.FramesPerDirection );

			// The saved entertainer is walking, so the walk's own animation follows in the same sweep either way.
			Assert.IsFalse( sprite.IsOnAState );
			Assert.AreEqual( packed ? 0 : 1, Counted( "SPRITE_STATE_ANIMATION_NOT_STARTED" ) - before );
		}
		finally
		{
			people?.Delete();
			Entity.ApplyDeletions();
			typeof( ParkPeople ).GetProperty( "Current", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic )!.SetValue( null, previousPeople );
			typeof( ParkState ).GetProperty( nameof( ParkState.Current ) )!.SetValue( null, previousState );
		}
	}

	/// <summary>
	/// The stock park left alone, seeded: the entertainer performs, every start has a guest within four cells
	/// each way, and the sprite is then on the bank's state script - set 4 of
	/// <c>Jungle\Entertainers</c>' first bank, its eight frames a direction.
	/// </summary>
	[TestMethod]
	public void InTheStockParkTheEntertainerPerformsOnTheBanksOwnAnimation()
	{
		var world = Park();
		using var clock = new SimulationClockScope();
		var previousState = ParkState.Current;
		var previousPeople = ParkPeople.Current;
		ParkPeople? people = null;

		try
		{
			var state = new ParkState( world );
			var folder = ParkGuestSprites.BanksIn( data, "esprites/Jungle/Entertainers", 4 );
			var bank = new SpriteBankFile( new MemoryStream( data.ReadAllBytes( folder[0] ) ) );

			people = new ParkPeople( world, Balance(), state: state, random: new Random( 1 ),
				behaviourRandom: new Random( 2 ), rideRandom: new Random( 3 ), staffRandom: new Random( 4 ) )
			{
				BankOf = id => id == Entertainer ? bank : null
			};

			var member = people.Staff.Single( staff => staff.ThingId == Entertainer );
			var starts = 0;
			var stamp = 0;
			var onTheScript = 0;

			for ( var sweep = 0; sweep < 900; ++sweep )
			{
				SimulationClockScope.Frame( 8 * GameClock.TickSeconds );
				people.Update();

				if ( member.Activity != StaffActivity.Performing )
					continue;

				var (x, y) = member.Navigator.Position.Cell;
				var sprite = people.SpriteFor( Entertainer )!;

				if ( member.TimeStartedEntertaining != stamp )
				{
					stamp = member.TimeStartedEntertaining;
					++starts;

					Assert.AreEqual( state.GameTick, stamp );
					Assert.IsTrue( people.GuestsNear( (y * 128) + x + 1, 4 ) > 0, $"a guest in reach on {stamp}" );
				}

				Assert.AreEqual( 1760, sprite.Script, $"mGameTick {state.GameTick}" );
				Assert.AreEqual( 4, sprite.Set );
				Assert.AreEqual( 8, sprite.FramesPerDirection );
				Assert.IsTrue( sprite.Frame is >= 0 and < 8, $"frame {sprite.Frame}" );
				++onTheScript;
			}

			Assert.IsTrue( starts > 0, "the entertainer performed" );
			Assert.IsTrue( onTheScript >= 51 );
		}
		finally
		{
			people?.Delete();
			Entity.ApplyDeletions();
			typeof( ParkPeople ).GetProperty( "Current", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic )!.SetValue( null, previousPeople );
			typeof( ParkState ).GetProperty( nameof( ParkState.Current ) )!.SetValue( null, previousState );
		}
	}
}
