using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace OpenTPW.Tests;

[TestClass]
public class ParkAdvisorMessageTests
{
	private static SettingsFile Settings( int open = 26, int close = 26, int threshold = 25 )
		=> new( new MemoryStream( Encoding.ASCII.GetBytes( $"GeneralAdvisor.MinScoreForConsideration {threshold}\n"
			+ $"MessageGroups[0].MinTimeSameMessage 120\nMessageGroups[0].SayOnlyOnce 0\n"
			+ $"ParkNowOpen.Score {open}\nParkNowClosed.Score {close}\n" ) ) );

	private static void Set( SettingsFile settings, string key, int value )
	{
		var index = settings.Entries.FindIndex( pair => pair.Key == key );
		settings.Entries[index] = new SettingsPair( key, value.ToString() );
	}

	[TestMethod]
	public void StockDoorPostsBothMessagesButSpeaksNeitherAndRetainsSlots()
	{
		FileSystem = GameData.Required();
		var messages = new ParkAdvisorMessages( new SettingsFile( "Advisor/Advisor.sam" ) );
		var state = new ParkState( null ) { AdvisorMessages = messages };
		state.SetParkClosed( true );
		state.SetParkClosed( false );
		for ( var i = 0; i < 1000; ++i ) messages.Tick( i, i * 248, (_, _) => throw new Exception( "stock speech" ) );
		Assert.AreEqual( 2, messages.PendingCount );
		Assert.AreEqual( 0, messages.Attempts );
		Assert.IsTrue( messages.Census().Contains( "message=128/score=20/line=-1" ) );
		Assert.IsTrue( messages.Census().Contains( "message=129/score=20/line=-1" ) );
	}

	[TestMethod]
	public void RealStateRoutePostsUnchangedOpenAndClosedWithoutMovingRidesAgain()
	{
		var messages = new ParkAdvisorMessages( Settings() );
		var moved = 0;
		var state = new ParkState( null ) { AdvisorMessages = messages, DoorMoved = _ => ++moved };
		var said = new List<int>();
		int Play( int response, int sample ) { said.Add( sample ); return 0; }
		state.SetParkClosed( false ); // Already open: still posts.
		messages.Tick( 0, 0, Play );
		state.SetParkClosed( true );
		messages.Tick( 1, 1000, Play );
		state.SetParkClosed( true ); // Already closed: still posts.
		messages.Tick( 2, 2000, Play );
		CollectionAssert.AreEqual( new[] { 342, 344, 345 }, said );
		Assert.AreEqual( 1, moved );
	}

	[TestMethod]
	public void EachMessageHasOnePendingCopyAndOppositeDoorDoesNotWithdrawIt()
	{
		var messages = new ParkAdvisorMessages( Settings() );
		Assert.IsTrue( messages.PostDoor( false, 100 ) );
		Assert.IsFalse( messages.PostDoor( false, 100 ) );
		Assert.IsTrue( messages.PostDoor( true, 100 ) );
		Assert.IsFalse( messages.PostDoor( true, 100 ) );
		Assert.AreEqual( 2, messages.PendingCount );
		var said = new List<int>();
		messages.Tick( 100, 0, (r, s) => { said.Add( s ); return 0; } );
		messages.Tick( 104, 1000, (r, s) => { said.Add( s ); return 0; } );
		CollectionAssert.AreEqual( new[] { 342, 344 }, said );
	}

	[DataTestMethod]
	[DataRow( 20, 25, 0 )]
	[DataRow( 25, 25, 0 )]
	[DataRow( 26, 25, 1 )]
	[DataRow( 26, 26, 0 )]
	[DataRow( 27, 26, 1 )]
	public void TickUsesStrictDataThreshold( int score, int threshold, int expected )
	{
		var messages = new ParkAdvisorMessages( Settings( score, threshold: threshold ) );
		messages.PostDoor( false, 100 );
		var calls = 0;
		messages.Tick( 100, 0, (_, _) => { ++calls; return 0; } );
		Assert.AreEqual( expected, calls );
		Assert.AreEqual( 1 - expected, messages.PendingCount );
	}

	[DataTestMethod]
	[DataRow( 24, 0 )]
	[DataRow( 25, 1 )]
	public void HelperRechecksScoreInclusivelyAndConsumesRejectedSlot( int changedScore, int expected )
	{
		var settings = Settings();
		var messages = new ParkAdvisorMessages( settings );
		messages.PostDoor( false, 100 );
		Set( settings, "ParkNowOpen.Score", changedScore );
		messages.Tick( 100, 0, (_, _) => 0 );
		Assert.AreEqual( expected, messages.Attempts );
		Assert.AreEqual( 0, messages.PendingCount );
		Set( settings, "ParkNowOpen.Score", 26 );
		Assert.AreEqual( expected == 0, messages.PostDoor( false, 100 ) );
	}

	[TestMethod]
	public void PerMessageCooldownUsesShiftedParkSweepsAndIncludesBoundary()
	{
		var messages = new ParkAdvisorMessages( Settings() );
		messages.PostDoor( false, 101 );
		messages.Tick( 101, 0, (_, _) => 0 );
		Assert.IsFalse( messages.PostDoor( false, 579 ) );
		Assert.IsTrue( messages.PostDoor( true, 102 ), "opposite door has its own stamp" );
		Assert.IsTrue( messages.PostDoor( false, 580 ), "(580 >> 2) - (101 >> 2) == 120" );
	}

	[TestMethod]
	public void EarlyStampDoesNotArmCooldownAndCategoryValueIsRead()
	{
		var settings = Settings();
		Set( settings, "MessageGroups[0].MinTimeSameMessage", 2 );
		var messages = new ParkAdvisorMessages( settings );
		messages.PostDoor( false, 3 );
		messages.Tick( 3, 0, (_, _) => 0 );
		Assert.IsTrue( messages.PostDoor( false, 4 ) );
		messages.Tick( 4, 1000, (_, _) => 0 );
		Assert.IsFalse( messages.PostDoor( false, 11 ) );
		Assert.IsTrue( messages.PostDoor( false, 12 ) );
	}

	[TestMethod]
	public void RotationIsSeparateWrapsAndZeroPlaybackStillAdvancesHistory()
	{
		var messages = new ParkAdvisorMessages( Settings() );
		var said = new List<int>();
		var responses = new List<int>();
		for ( var i = 0; i < 3; ++i )
		{
			messages.PostDoor( false, 100 + i * 480 );
			messages.Tick( 100 + i * 480, i * 2000, (r, s) => { responses.Add( r ); said.Add( s ); return 0; } );
			messages.PostDoor( true, 101 + i * 480 );
			messages.Tick( 101 + i * 480, i * 2000 + 1000, (r, s) => { responses.Add( r ); said.Add( s ); return 0; } );
		}
		CollectionAssert.AreEqual( new[] { 342, 344, 343, 345, 342, 344 }, said );
		CollectionAssert.AreEqual( new[] { 308, 310, 309, 311, 308, 310 }, responses );
	}

	[TestMethod]
	public void ResponseWaitIncludesDurationPlusOneSecond()
	{
		var messages = new ParkAdvisorMessages( Settings() );
		messages.PostDoor( false, 100 );
		messages.PostDoor( true, 100 );
		messages.Tick( 100, 50, (_, _) => 2000 );
		messages.Tick( 500, 3049, (_, _) => throw new Exception( "still waiting" ) );
		Assert.AreEqual( 1, messages.PendingCount );
		messages.Tick( 500, 3050, (_, _) => 0 );
		Assert.AreEqual( 2, messages.Attempts );
	}

	[TestMethod]
	public void HigherPriorityWinsAndEqualScoresKeepSlotOrder()
	{
		foreach ( var close in new[] { 26, 30 } )
		{
			var messages = new ParkAdvisorMessages( Settings( close: close ) );
			messages.PostDoor( false, 100 );
			messages.PostDoor( true, 100 );
			var said = 0;
			messages.Tick( 100, 0, (_, s) => { said = s; return 0; } );
			Assert.AreEqual( close == 26 ? 342 : 344, said );
		}
	}

	[TestMethod]
	public void EightSlotsReplaceOnlyTheFirstLowestWithStrictlyHigherPriority()
	{
		var settings = Settings( 20 );
		var topics = Enumerable.Range( 1, 10 ).Select( id => new ParkAdvisorMessages.Topic( id,
			id == 10 ? "ParkNowClosed.Score" : "ParkNowOpen.Score", 308, 342 ) ).ToArray();
		var messages = new ParkAdvisorMessages( settings, topics );
		for ( var id = 1; id <= 8; ++id ) Assert.IsTrue( messages.Post( id, 100 ) );
		Assert.IsFalse( messages.Post( 9, 100 ), "equal cannot replace" );
		Assert.IsTrue( messages.Post( 10, 100 ), "26 replaces the first 20" );
		Assert.AreEqual( 8, messages.PendingCount );
		messages.Tick( 100, 0, (_, _) => 0 );
		Assert.AreEqual( 7, messages.PendingCount );
		Assert.IsTrue( messages.Post( 1, 100 ), "first slot was replaced" );
		Assert.IsFalse( messages.Post( 2, 100 ), "other low slots remain" );
	}

	[TestMethod]
	public void WorldUpdateTicksMessagesOnTheIncrementedParkClock()
	{
		using var clock = new SimulationClockScope();
		FileSystem = GameData.Required();
		var state = new ParkState( null ) { AdvisorMessages = new ParkAdvisorMessages( Settings() ) };
		for ( var i = 0; i < 100; ++i ) state.AdvanceGameTick();
		var people = new ParkPeople( null, state: state );
		try
		{
			state.SetParkClosed( false );
			Time.Paused = false;
			Time.StepFrames = 0;
			GameClock.Rebase();
			Time.Update( 0 );
			GameClock.Update( false, GameClock.ParkCatchUp );
			for ( var i = 0; i < 9; ++i )
			{
				Time.Update( .031f );
				GameClock.Update( false, GameClock.ParkCatchUp );
				people.Update();
			}
			Assert.AreEqual( 1, state.AdvisorMessages.Attempts );
			Assert.IsTrue( state.AdvisorMessages.Census().Contains( "message=128/score=26/line=0/lastTick=101" ) );
		}
		finally
		{
			people.Delete();
			Entity.ApplyDeletions();
			Time.Paused = false;
			GameClock.Rebase();
		}
	}

	[TestMethod]
	public void ParkResponseDurationIncludesTheEndingClipTwiceOverall()
	{
		// A chain of 2400 ms already includes its 600 ms ending clip. The response adds it again.
		Assert.AreEqual( 4000, Advisor.ParkResponseDuration( 2400, 600 ) );
	}

	[TestMethod]
	public void CatchUpSweepsShareOneResponseClockAndDoNotConsumeTheNextMessage()
	{
		using var clock = new SimulationClockScope();
		FileSystem = GameData.Required();
		var state = new ParkState( null ) { AdvisorMessages = new ParkAdvisorMessages( Settings() ) };
		var people = new ParkPeople( null, state: state );
		try
		{
			state.SetParkClosed( false );
			state.SetParkClosed( true );
			SimulationClockScope.Frame( 2f );
			people.Update();
			Assert.AreEqual( 3, state.GameTick, "64 ticks owe eight world sweeps, and a frame runs three" );
			Assert.AreEqual( 1, state.AdvisorMessages.Attempts, "all three sweeps see one instant, so the one-second wait cannot expire" );
			Assert.AreEqual( 1, state.AdvisorMessages.PendingCount );
			Assert.IsTrue( state.AdvisorMessages.Census().Contains( "message=128/score=26/line=0/lastTick=1" ) );
		}
		finally
		{
			people.Delete();
			Entity.ApplyDeletions();
		}
	}

	[TestMethod]
	public void CategorySayOncePreventsSecondPosting()
	{
		var settings = Settings();
		Set( settings, "MessageGroups[0].SayOnlyOnce", 1 );
		var messages = new ParkAdvisorMessages( settings );
		messages.PostDoor( false, 100 );
		messages.Tick( 100, 0, (_, _) => 0 );
		Assert.IsFalse( messages.PostDoor( false, 580 ) );
	}
}
