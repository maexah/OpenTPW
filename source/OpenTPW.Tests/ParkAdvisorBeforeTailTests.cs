using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// The sweep's tail comes after the advisor's turn (<c>FUN_00516380</c> walks the things, the advisor among them, and
/// then calls <c>FUN_004d7b20</c> at <c>0x00516695</c>: the arrival manager, then the staff pool at <c>0x004d7b30</c>;
/// <c>docs/exe/advisor-park.md</c>, "The advisor's turn in the sweep"). Row 17 (<c>0x0059cfe0</c>) counts the pool's
/// researchers to hire, so a pool that changes on the tick of its poll shows which turn came first.
/// </summary>
[TestClass]
public class ParkAdvisorBeforeTailTests
{
	private const int Researchers = 4;

	/// <summary>
	/// <b>A researcher the top-up makes on the poll's tick is not yet counted</b>: the shipped park's pool, marked 722,
	/// with its three researchers taken off, is topped up on 1083, the first sweep more than 360 past its mark. Row 17
	/// polled on that sweep counts nobody to hire and scores nought; the pool's turn after him then holds researchers.
	/// </summary>
	/// <remarks><b>Mutations:</b> the staff pool's turn (alone, or with the arrivals') before the advisor's.</remarks>
	[TestMethod]
	public void ARecruitTheTopUpMakesOnThePollsTickIsCountedOnlyAfterIt()
	{
		Run( ( pool, last ) =>
		{
			foreach ( var person in pool.OfKind( Researchers ).ToList() )
				Assert.IsTrue( pool.Expire( person.Id ) );

			Assert.AreEqual( 722, pool.Mark, "the shipped park's pool mark" );
			return 1083;
		}, ( messages, pool ) =>
		{
			Assert.AreEqual( 1083, pool.Mark, "the pool was topped up on this sweep" );
			Assert.IsTrue( pool.OfKind( Researchers ).Any(), "and the top-up made researchers" );
			Assert.AreEqual( "no-score", messages.PollAnswer( ParkAdvisorMessages.HireMoreResearchers ) );
		} );
	}

	/// <summary>
	/// <b>A researcher whose time runs out on the poll's tick is still counted</b>: the pool left only the researcher
	/// whose time runs out last (marked 722, lifetime 151: out on 1327, the first sweep more than 604 past the mark),
	/// topped up on 1083 and the top-up's researchers taken off. Row 17 polled on 1327 counts the one and posts; the
	/// pool's turn after him times the researcher out.
	/// </summary>
	/// <remarks><b>Mutations:</b> the staff pool's turn (alone, or with the arrivals') before the advisor's.</remarks>
	[TestMethod]
	public void AResearcherWhoTimesOutOnThePollsTickIsStillCounted()
	{
		Run( ( pool, last ) =>
		{
			foreach ( var person in pool.OfKind( Researchers ).Where( person => person.Id != last.Id ).ToList() )
				Assert.IsTrue( pool.Expire( person.Id ) );

			Assert.AreEqual( (722, 151), (last.Mark, last.Lifetime), "the shipped park's last researcher to time out" );
			Assert.AreEqual( 1327, last.Mark + last.Lifetime * 4 + 1 );
			return 1327;
		}, ( messages, pool ) =>
		{
			Assert.IsFalse( pool.OfKind( Researchers ).Any(), "the pool's turn timed the researcher out" );
			Assert.AreEqual( "post", messages.PollAnswer( ParkAdvisorMessages.HireMoreResearchers ) );
		}, topUpFirst: true );
	}

	/// <summary>
	/// The shipped Lost Kingdom park with the stock advisor file and the q422 runs' key 1 (one month is enough for row
	/// 17), its saved pool, a sweep onto the tick <paramref name="arrange"/> answers with the poll cursor on row 17, then
	/// <paramref name="check"/>. With <paramref name="topUpFirst"/> the park is first swept on 1083, where the pool tops
	/// up, and the top-up's researchers are taken off.
	/// </summary>
	private static void Run( Func<ParkStaffPool, ParkStaffPool.Candidate, int> arrange, Action<ParkAdvisorMessages, ParkStaffPool> check,
		bool topUpFirst = false )
	{
		var data = GameData.Required();
		FileSystem = data;

		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		var world = new ParkWorld( new SaveReader( stream ).ReadFile() );
		var balance = new ParkBalance( "jungle", easyMode: true );

		var settings = new SettingsFile( "Advisor/Advisor.sam" );
		var index = settings.Entries.FindIndex( pair => pair.Key == "StaffHireResearchers3.MonthsIntoGameForConsideration" );
		settings.Entries[index] = new SettingsPair( settings.Entries[index].Key, "1" );
		var cursor = new ParkAdvisorMessages.PollCursor();
		var messages = new ParkAdvisorMessages( settings, cursor: cursor );

		using var clock = new SimulationClockScope();
		var state = new ParkState( world ) { AdvisorMessages = messages };
		var people = new ParkPeople( world, balance, state: state );
		var pool = new ParkStaffPool( balance, gameTick: world.GameTick, saved: world );

		try
		{
			Assert.AreEqual( 755, state.GameTick, "the shipped park's clock" );
			var researchers = pool.OfKind( Researchers ).OrderBy( person => person.Mark + person.Lifetime * 4 ).ToList();
			Assert.AreEqual( 3, researchers.Count, "the shipped park's pool holds three researchers" );
			var last = researchers[^1];

			if ( topUpFirst )
			{
				var before = pool.OfKind( Researchers ).Select( person => person.Id ).ToHashSet();
				state.SetGameTick( 1082 );
				cursor.Row = 0;
				people.Sweep( 0 );
				Assert.AreEqual( 1083, pool.Mark );
				foreach ( var person in pool.OfKind( Researchers ).Where( person => !before.Contains( person.Id ) ).ToList() )
					Assert.IsTrue( pool.Expire( person.Id ) );
			}

			var on = arrange( pool, last );
			state.SetGameTick( on - 1 );
			cursor.Row = 17;
			people.Sweep( 0 );

			Assert.AreEqual( on, state.GameTick );
			Assert.AreEqual( 1, ParkStrikes.MonthsSinceTheFirstSweep( on ), "one month, the key's" );
			check( messages, pool );
		}
		finally
		{
			ParkStaffPool.ForgetCurrent();
			people.Delete();
			Entity.ApplyDeletions();
		}
	}
}
