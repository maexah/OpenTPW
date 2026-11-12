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
	/// A saved <c>mTimeStartedEntertaining</c> is kept. The shipped park's own reads nought, so the park is written with
	/// the entertainer performing since a stamp that is not, and read back: the record holds it at +503 (FileFormats
	/// <c>saves.md</c>, the entertainer's row: <c>mTimeStartedEntertaining</c> 4, <c>mNext</c> 2), the reader finds it
	/// there, and the member made from the file keeps it.
	/// </summary>
	/// <remarks><b>Mutations:</b> the read at +503 moved (to +505, or the mechanic's guard put on it) or the write moved fails here.</remarks>
	[TestMethod]
	public void ASavedStampIsKept()
	{
		const int stamp = 0x00012345;

		var world = Park();

		Assert.AreEqual( 0, world.People.Single( person => person.ThingId == Entertainer ).Staff!.Value.TimeStartedEntertaining );

		var state = new ParkState( world );
		var people = new ParkPeople( world, Balance(), gateStatus: null, state, new ParkItemCatalogue( "jungle", data ) );

		try
		{
			people.Staff.Single( staff => staff.ThingId == Entertainer ).StartPerforming( 0, stamp );

			var again = new ParkWorld( ParkFileWriter.Body( world, new ParkFileWriter.Running( world.GameTick, false, 0, 0,
				world.Camera.Saved!.Value, People: people.Written( Level.WrittenThings( world ).Contains ) ) ) );

			Assert.IsNull( again.Problem );
			Assert.AreEqual( stamp, BitConverter.ToInt32( again.RecordOf( Entertainer )!, 503 ), "mTimeStartedEntertaining at +503" );

			var written = again.People.Single( person => person.ThingId == Entertainer ).Staff!.Value;

			Assert.AreEqual( (int)StaffActivity.Performing, written.State );
			Assert.AreEqual( stamp, written.TimeStartedEntertaining );
			Assert.AreEqual( stamp, ParkPeople.StaffIn( again ).Single( staff => staff.ThingId == Entertainer ).TimeStartedEntertaining );
		}
		finally
		{
			people.Delete();
			Entity.ApplyDeletions();
		}
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

	/// <summary>
	/// What the sweep itself does for a member of staff, seen on the saved entertainer (thing 27) while carried, whose
	/// behaviour has an empty body, over four sweeps from the park's 755:
	/// <list type="bullet">
	/// <item>the bubble call is made on the member's own sweep in four, <c>(id &amp; 3) == (mGameTick &amp; 3)</c>
	/// (<c>FUN_00505490</c>, <c>0x005054b3</c>..<c>0x005054c0</c>): a bubble 20 sweeps old stays through the sweeps
	/// that are not theirs and is gone from theirs on;</item>
	/// <item>a queued interval is handed to the sprite and cleared, and nought queued leaves the sprite's alone
	/// (<see cref="Peep.NextAnimation"/>): 77 over the saved sprite's 62, kept on every later sweep;</item>
	/// <item>a sprite on a state's script that kept its frames a direction keeps them, the bank not asked: 9, where
	/// the bank's set 4 has 17; one with none and no bank is stood, and stays stood with nothing queued;</item>
	/// <item>the staff pool is told how many of each kind the park employs (<c>FUN_00508000</c>): the save's five
	/// staff are one a kind, so the top-up of the first sweep leaves 1, 1, 1, 1, 1.</item>
	/// </list>
	/// </summary>
	[TestMethod]
	public void ACarriedEntertainerStillTakesTheSweepsOwnTurn()
	{
		var world = Park();
		using var clock = new SimulationClockScope();
		var previousState = ParkState.Current;
		var previousPeople = ParkPeople.Current;
		var previousPool = ParkStaffPool.Current;
		ParkPeople? people = null;

		try
		{
			var folder = ParkGuestSprites.BanksIn( data, "esprites/Jungle/Entertainers", 4 );
			var bank = new SpriteBankFile( new MemoryStream( data.ReadAllBytes(
				folder.Single( file => file.Contains( "SPR_EX", StringComparison.OrdinalIgnoreCase ) ) ) ) );
			SpriteBankFile? given = bank;
			var state = new ParkState( world );

			people = new ParkPeople( world, Balance(), state: state, random: new Random( 1 ),
				behaviourRandom: new Random( 2 ), rideRandom: new Random( 3 ), staffRandom: new Random( 4 ) )
			{
				BankOf = id => id == Entertainer ? given : null
			};

			var start = state.GameTick;
			var pool = new ParkStaffPool( Balance(), gameTick: start - 360 );
			var member = people.Staff.Single( staff => staff.ThingId == Entertainer );
			var sprite = people.SpriteFor( Entertainer )!;
			var group = new SpriteStateGroup( 1, 5, 0, 0 );

			Assert.IsTrue( people.PickUp( Entertainer ) );
			Assert.AreEqual( SpriteScript.DefaultInterval, sprite.Interval, "a saved sprite starts on the default interval, 62" );
			Assert.AreEqual( 17, bank.Sets[4].FramesPerDirection, "SPR_EX's set 4 is the long one" );

			member.NextAnimation = 0;
			member.NextInterval = 77;
			member.Thoughts.Restore( Thoughts.Confused, start - 20, Thoughts.ScriptOf( 0, 9 ) );
			Assert.IsNotNull( member.Thoughts.Bubble );

			var swept = 0;
			var gone = false;

			void Sweep()
			{
				SimulationClockScope.Frame( 8 * GameClock.TickSeconds );
				people!.Update();
				++swept;

				Assert.AreEqual( start + swept, state.GameTick, "one sweep a frame" );

				gone |= (state.GameTick & 3) == (Entertainer & 3);
				Assert.AreEqual( gone, member.Thoughts.Bubble == null, $"the bubble on mGameTick {state.GameTick}" );
				Assert.AreEqual( (77, 0), (sprite.Interval, member.NextInterval), $"the interval on mGameTick {state.GameTick}" );
			}

			// On a state's script with nine frames a direction kept, and a bank that would say 17.
			sprite.StartState( group, 9 );
			Sweep();

			Assert.AreEqual( 9, sprite.FramesPerDirection, "the frames kept are kept: the bank is not asked" );
			Assert.AreEqual( start + 1, pool.Mark, "the pool was topped up on this sweep, 361 past its mark" );
			CollectionAssert.AreEqual( new[] { 1, 1, 1, 1, 1 }, pool.Written().PeopleInCat.ToArray(), "one of each kind employed" );

			// On a state's script with no frames kept and no bank: stood, and nothing queued takes it off the stand.
			given = null;
			sprite.StartState( group, 0 );
			Sweep();

			Assert.IsFalse( sprite.IsOnAState, "off the state's script" );
			Assert.IsTrue( sprite.IsOn( SpriteScript.Standing ), "and standing" );
			Assert.AreEqual( 0, sprite.FramesPerDirection );

			Sweep();
			Sweep();

			Assert.IsNull( member.Thoughts.Bubble, "four sweeps hold the member's own, which took the bubble" );
		}
		finally
		{
			typeof( ParkStaffPool ).GetProperty( nameof( ParkStaffPool.Current ) )!.SetValue( null, previousPool );
			people?.Delete();
			Entity.ApplyDeletions();
			typeof( ParkPeople ).GetProperty( "Current", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic )!.SetValue( null, previousPeople );
			typeof( ParkState ).GetProperty( nameof( ParkState.Current ) )!.SetValue( null, previousState );
		}
	}
}
