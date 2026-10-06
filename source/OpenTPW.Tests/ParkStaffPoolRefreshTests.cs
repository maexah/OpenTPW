using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// The staff pool's refresh, <c>FUN_005084f0</c> (<c>docs/exe/park-engine.md</c>, "The staff pool's refresh"): a
/// candidate goes on the first sweep past four times their lifetime, and every <c>TimeBetweenStaffUpdates * 4</c>
/// sweeps the pool is topped up. The numbers are Lost Kingdom's Instant Action keys, as the original loads them: 90,
/// 10 a time, 120; <c>Max</c> 6, 5, 6, 4, 3; <c>Min</c> 1, 1, 1, 1, 0.
/// </summary>
[TestClass]
public class ParkStaffPoolRefreshTests
{
	private ParkStaffPool? _poolBefore;

	[TestInitialize]
	public void MountTheGame()
	{
		Log ??= new();
		FileSystem = GameData.Required();
		_poolBefore = ParkStaffPool.Current;
	}

	[TestCleanup]
	public void PutThePoolBack()
	{
		ParkStaffPool.Drop();
		typeof( ParkStaffPool ).GetProperty( nameof( ParkStaffPool.Current ) )!.SetValue( null, _poolBefore );
	}

	/// <summary>
	/// <b>The edges, in the original's own numbers</b>: its candidate of lifetime 141 made on 361 stood on 925 and went
	/// on 926; its pool marked 722 was topped up on 1083 and not on 1082; a lifetime is the timeout plus a draw
	/// modulo half of it.
	/// </summary>
	/// <remarks><b>Mutations:</b> either compare made at-or-past, or signed; the fours dropped; the lifetime's spread a whole timeout.</remarks>
	[TestMethod]
	public void ACandidateGoesPastFourTimesTheirLifetimeAndThePoolIsDuePastFourTimesItsKey()
	{
		Assert.IsFalse( ParkStaffPool.TimedOut( 925, 361, 141 ), "564 sweeps old is not more than 564" );
		Assert.IsTrue( ParkStaffPool.TimedOut( 926, 361, 141 ) );

		Assert.IsFalse( ParkStaffPool.UpdateIsDue( 1082, 722, 90 ), "360 sweeps on is not more than 360" );
		Assert.IsTrue( ParkStaffPool.UpdateIsDue( 1083, 722, 90 ) );

		// Both compares are unsigned: a mark ahead of the clock is a very long time ago.
		Assert.IsTrue( ParkStaffPool.TimedOut( 5, 10, 141 ) );
		Assert.IsTrue( ParkStaffPool.UpdateIsDue( 5, 10, 90 ) );

		Assert.AreEqual( 120, ParkStaffPool.LifetimeFrom( 120, 0 ) );
		Assert.AreEqual( 179, ParkStaffPool.LifetimeFrom( 120, 59 ) );
		Assert.AreEqual( 120, ParkStaffPool.LifetimeFrom( 120, 60 ) );
	}

	/// <summary>
	/// <b>The draw picks a kind by what each wants</b>, walking them in order with the draw less each one passed; a
	/// kind that wants none is passed and one that wants less than none gives the draw back. The draw is always under
	/// the wants' sum, so it always lands.
	/// </summary>
	/// <remarks><b>Mutations:</b> the walk off by one; the weights not taken off the draw; a kind wanting none picked.</remarks>
	[TestMethod]
	public void TheTopUpsDrawPicksAKindByItsWant()
	{
		int[] want = [5, 2, 4, 2, 1];

		CollectionAssert.AreEqual( new[] { 0, 0, 1, 1, 2, 2, 3, 3, 4 },
			new uint[] { 0, 4, 5, 6, 7, 10, 11, 12, 13 }.Select( draw => ParkStaffPool.PickByWeight( want, draw ) ).ToArray() );

		Assert.AreEqual( 2, ParkStaffPool.PickByWeight( [0, 0, 3], 0 ), "kinds that want none are passed" );
		Assert.AreEqual( 1, ParkStaffPool.PickByWeight( [-1, 2], 0 ), "a kind over its number gives the draw one back" );
	}

	/// <summary>
	/// <b>A pool run for a thousand sweeps</b>: every opening candidate goes on exactly the first sweep past four times
	/// their lifetime, one held in the hand does not go while held, the pool is topped up on its mark plus 361 and
	/// again 361 on, by ten at the most, no kind taken past its <c>Max</c> by a top-up, a kind the park is full of
	/// given none, and the four kinds with a minimum never left with nobody after one.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> nobody ever dropped; dropped a sweep early or late; the held candidate dropped; no top-up, or
	/// one every sweep; the mark not moved; the budget ignored; a kind topped up past its Max; a full kind topped up;
	/// new candidates marked nought, or given no lifetime.
	/// </remarks>
	[TestMethod]
	public void APoolRunForAThousandSweepsDropsAndTopsUpOnItsMarks()
	{
		const int start = 1000;

		var pool = new ParkStaffPool( new ParkBalance( "jungle", easyMode: true ), gameTick: start );
		var opening = pool.Candidates.ToArray();
		var held = opening[0];
		var left = new Dictionary<int, int>();
		var joined = new List<(int Tick, ParkStaffPool.Candidate Who)>();
		var tick = start;

		pool.Left += id => left[id] = tick;
		pool.Joined += who => joined.Add( (tick, who) );

		Assert.IsTrue( opening.All( person => person.Mark == start && person.Lifetime is >= 120 and <= 179 ),
			"the opening pool is marked with the park's clock and given lifetimes of 120 to 179" );

		ParkStaffPool.Carry( held.Id );

		// The park is full of guards (kind 3) throughout, and employs nobody else, so every other kind is topped up.
		// (A limit of nought met by nobody is not full either, as the original has it, but no shipped balance holds
		// such a limit, 6 being the least, and nothing here can show it.)
		for ( tick = start + 1; tick <= start + 1000; ++tick )
			pool.Sweep( tick, kind => kind == 3 ? pool.MostInPark( 3 ) : 0 );

		foreach ( var person in opening.Skip( 1 ) )
			Assert.AreEqual( start + (person.Lifetime * 4) + 1, left[person.Id], $"{person.Name}, lifetime {person.Lifetime}" );

		Assert.IsFalse( left.ContainsKey( held.Id ), "the one in the hand is not dropped while held" );

		CollectionAssert.AreEqual( new[] { start + 361, start + 722 }, joined.Select( made => made.Tick ).Distinct().ToArray(),
			"topped up 361 and 722 sweeps after its mark, and at no other time" );

		foreach ( var batch in joined.GroupBy( made => made.Tick ) )
		{
			Assert.IsTrue( batch.Count() <= 10 + 4, $"{batch.Count()} joined on {batch.Key}: ten at the most, and the minimums' few" );
			Assert.IsTrue( batch.All( made => made.Who.Mark == batch.Key && made.Who.Lifetime is >= 120 and <= 179 ) );
			Assert.IsFalse( batch.Any( made => made.Who.Kind == 3 ), "the park is full of guards, so none joins" );
		}

		int[] most = [6, 5, 6, 4, 3];

		foreach ( var kind in new[] { 0, 1, 2, 4 } )
		{
			var before = opening.Count( person => person.Kind == kind );

			Assert.IsTrue( joined.Where( made => made.Tick == start + 361 ).Count( made => made.Who.Kind == kind ) <= System.Math.Max( 0, most[kind] - before ),
				$"kind {kind} is not topped up past its Max of {most[kind]}" );
		}

		Assert.AreEqual( start + 722, pool.Mark );

		foreach ( var kind in new[] { 0, 1, 2 } )
			Assert.IsTrue( pool.OfKind( kind ).Any(), $"kind {kind} is never left with nobody after a top-up" );
	}

	/// <summary>
	/// <b>A park loaded from a save starts with the save's pool, and a park with no save rolls one.</b> Lost Kingdom's
	/// sixteen, with the marks and lifetimes the file gives them and the pool's mark of 722, run from the save's 755:
	/// each goes on the sweep the original drops them on and the pool is topped up on 1083 and 1444, as the
	/// original's own log has it (<c>docs/exe/park-engine.md</c>, "The staff pool's refresh").
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> the save's pool not used, by the pool or by the level's hand-off; the empty records taken too;
	/// the pool marked with the park's clock; a candidate marked with the park's clock, or given a rolled lifetime; the
	/// name not the record's row; every wage a grade 2's; every name a researcher's; the drops walked from the last.
	/// </remarks>
	[TestMethod]
	public void ALoadedParkStartsWithTheSavesPool()
	{
		using var stream = FileSystem.OpenRead( "levels/jungle/Easymode.TPWI" );
		var world = new ParkWorld( new SaveReader( stream ).ReadFile() );
		var pool = Level.StaffPoolFor( world, new ParkBalance( "jungle", easyMode: true ), world.GameTick );
		var saved = pool.Candidates.ToArray();
		var left = new List<int>();
		var joined = new List<int>();
		var tick = world.GameTick;

		Assert.AreEqual( 16, saved.Length );
		Assert.AreEqual( 722, pool.Mark );
		CollectionAssert.AreEqual( new[] { 3, 4, 4, 2, 3 }, Enumerable.Range( 0, 5 ).Select( kind => pool.OfKind( kind ).Count() ).ToArray() );
		Assert.AreEqual( (4, 2, 0, 361, 146), (saved[0].Kind, saved[0].Grade, saved[0].Costume, saved[0].Mark, saved[0].Lifetime) );
		Assert.AreEqual( (2, 3, 2, 361, 143), (saved[15].Kind, saved[15].Grade, saved[15].Costume, saved[15].Mark, saved[15].Lifetime) );
		Assert.AreEqual( new StringFile( "Language/English/RESEARCHER_NAMES.str" )[33], saved[0].Name, "the name is the record's row of its kind's table" );
		Assert.AreEqual( pool.WageFor( 4, 2 ), saved[0].Wage );

		// Every one of the sixteen is paid by their own kind and grade and named from their own kind's table.
		string[] tables = ["HANDYMAN_NAMES", "MECHANIC_NAMES", "ENTERTAINER_NAMES", "GUARD_NAMES", "RESEARCHER_NAMES"];
		var records = world.StaffPool.Where( record => record.Valid ).ToArray();

		Assert.AreEqual( 16, records.Length );
		Assert.IsTrue( records.Select( record => record.PayGrade ).Distinct().Count() > 1 && records.Select( record => record.Type ).Distinct().Count() == 5,
			"the save's pool holds every kind and more than one grade" );

		for ( var n = 0; n < records.Length; ++n )
		{
			Assert.AreEqual( pool.WageFor( records[n].Type, records[n].PayGrade ), saved[n].Wage, $"candidate {n}'s wage" );
			Assert.AreEqual( new StringFile( $"Language/English/{tables[records[n].Type]}.str" )[records[n].Name], saved[n].Name, $"candidate {n}'s name" );
		}

		Assert.AreNotEqual( pool.WageFor( 1, 2 ), saved.First( person => person is { Kind: 1, Grade: 1 } ).Wage, "a grade-1 mechanic is not paid as grade 2" );

		var went = new List<int>();

		pool.Left += id => { left.Add( tick ); went.Add( id ); };
		pool.Joined += _ => joined.Add( tick );

		for ( tick = world.GameTick + 1; tick <= 1444; ++tick )
			pool.Sweep( tick, _ => 0 );

		CollectionAssert.AreEqual( new[] { 858, 858, 926, 934, 946, 1066, 1239, 1259, 1263, 1279, 1327, 1355, 1383, 1387, 1387, 1403 },
			left.Take( 16 ).ToArray(), "the save's sixteen go when the original's do" );
		CollectionAssert.AreEqual( new[] { saved[13].Id, saved[14].Id }, went.Take( 2 ).ToArray(), "two on one sweep go lower slot first" );
		CollectionAssert.AreEqual( new[] { 1083, 1444 }, joined.Distinct().ToArray() );
		Assert.AreEqual( 10, joined.Count( on => on == 1083 ), "ten wanted and ten the most a top-up adds" );

		// With no save, the opening pool is rolled and marked with the clock it was made on.
		var fresh = Level.StaffPoolFor( null, new ParkBalance( "jungle", easyMode: true ), 0 );

		Assert.AreEqual( 22, fresh.Candidates.Count );
		Assert.AreEqual( 0, fresh.Mark );
		Assert.IsTrue( fresh.Candidates.All( person => person.Mark == 0 ) );
	}

	/// <summary>
	/// <b>The minimums' round takes the mechanics first, then the handymen, the entertainers, the guards and the
	/// researchers</b> (<c>FUN_00508170</c>), and names each kind that has fewer than its minimum between the park and
	/// the pool and is not full.
	/// </summary>
	/// <remarks><b>Mutations:</b> no minimums pass; the handymen first; a full kind named; a kind at its minimum named.</remarks>
	[TestMethod]
	public void TheMinimumsRoundNamesTheShortKindsMechanicsFirst()
	{
		int[] have = [0, 0, 1, 0, 0];
		int[] minimum = [1, 1, 1, 1, 0];
		bool[] full = [false, false, false, true, false];

		CollectionAssert.AreEqual( new[] { 1, 0 },
			ParkStaffPool.ShortOfTheirMinimum( kind => have[kind], minimum, full ).ToArray(),
			"the mechanics and then the handymen; the entertainers have theirs, the guards are full, the researchers need none" );

		// A top-up of ten into an empty pool that wants twenty-four leaves some kind with nobody about one time in
		// five by the draw alone; the round after it makes that up, whatever the draws were. Forty pools, each given
		// one sweep long after it was made, which times out everybody in it and tops it up from empty.
		var balance = new ParkBalance( "jungle", easyMode: true );

		for ( var seed = 1; seed <= 40; ++seed )
		{
			var pool = new ParkStaffPool( balance, seed, gameTick: 0 );

			pool.Sweep( 5000, _ => 0 );
			Assert.IsTrue( pool.Candidates.All( person => person.Mark == 5000 ), "everybody in it was made by this top-up" );

			foreach ( var kind in new[] { 0, 1, 2, 3 } )
				Assert.IsTrue( pool.OfKind( kind ).Any(), $"seed {seed}: kind {kind} has its minimum of one after a top-up" );
		}
	}

	/// <summary>
	/// <b>A kind with more candidates than its <c>Max</c> takes from the total wanted</b>: the sum is signed, as the
	/// original's is. The pool rolled for a fresh Lost Kingdom park holds 22 against Maxes of 24 with one kind over
	/// its own, so its first top-up, 361 sweeps on and before anybody's time is up, adds two and not three, as the
	/// game run measured.
	/// </summary>
	/// <remarks><b>Mutations:</b> a kind over its Max wanting nought.</remarks>
	[TestMethod]
	public void AKindOverItsMaxTakesFromTheTotalWanted()
	{
		var pool = new ParkStaffPool( new ParkBalance( "jungle", easyMode: true ), gameTick: 0 );
		int[] most = [6, 5, 6, 4, 3];
		var have = Enumerable.Range( 0, 5 ).Select( kind => pool.OfKind( kind ).Count() ).ToArray();
		var joined = 0;

		Assert.IsTrue( Enumerable.Range( 0, 5 ).Any( kind => have[kind] > most[kind] ), $"some kind is over its Max ({string.Join( ",", have )})" );
		Assert.AreEqual( 2, most.Sum() - have.Sum(), "the wants' signed sum" );

		pool.Joined += _ => ++joined;

		for ( var tick = 1; tick <= 361; ++tick )
			pool.Sweep( tick, _ => 0 );

		Assert.AreEqual( 361, pool.Mark, "topped up on 361" );
		Assert.AreEqual( 2, joined, "by the signed sum" );
	}

	/// <summary>
	/// <b>The minimums' round counts the staff the park employs with the candidates in the pool</b>
	/// (<c>0x0050819d</c>): with one guard employed the guards have their minimum of one, so a top-up whose draws
	/// gave the pool no guard leaves it with none. Forty pools topped up from empty, as the test above does it; with
	/// nobody employed every one of them is given a guard.
	/// </summary>
	/// <remarks><b>Mutations:</b> the round counting the pool alone.</remarks>
	[TestMethod]
	public void TheMinimumsRoundCountsTheStaffEmployed()
	{
		var balance = new ParkBalance( "jungle", easyMode: true );
		var without = 0;

		for ( var seed = 1; seed <= 40; ++seed )
		{
			var pool = new ParkStaffPool( balance, seed, gameTick: 0 );

			pool.Sweep( 5000, kind => kind == 3 ? 1 : 0 );

			if ( !pool.OfKind( 3 ).Any() )
				++without;
		}

		Assert.IsTrue( without > 0, "with a guard employed, a pool whose draws made no guard is not given one" );
	}

	/// <summary>
	/// <b>A new candidate's name is drawn again while one in the pool has it</b>, fifteen draws at the most
	/// (<c>FUN_00507580</c> on <c>FUN_005083f0</c>): forty opening pools of twenty-two, five or six to a kind from 35
	/// names each, and no pool holds a name twice. Drawn once, about one kind in four would.
	/// </summary>
	/// <remarks><b>Mutations:</b> the name drawn once; the look made among the kind's own candidates only is not told apart here.</remarks>
	[TestMethod]
	public void ANewCandidatesNameIsDrawnAgainWhileItIsInUse()
	{
		var balance = new ParkBalance( "jungle", easyMode: true );

		for ( var seed = 1; seed <= 40; ++seed )
		{
			var pool = new ParkStaffPool( balance, seed, gameTick: 0 );
			var twice = pool.Candidates.GroupBy( person => person.Name ).Where( name => name.Count() > 1 ).Select( name => name.Key ).ToArray();

			Assert.AreEqual( 22, pool.Candidates.Count );
			Assert.AreEqual( 0, twice.Length, $"seed {seed}: {string.Join( ", ", twice )} held twice" );
		}
	}

	/// <summary>
	/// <b>The hire screen's list follows the pool while it is open</b>: a candidate of the kind shown who joins gets a
	/// row, one of another kind does not, and one whose time runs out loses theirs (<c>FUN_00481550</c>).
	/// </summary>
	/// <remarks><b>Mutations:</b> the list deaf to a candidate joining, or to one leaving; every kind's rows added; a closed screen
	/// left following the pool.</remarks>
	[TestMethod]
	public void TheHireListFollowsThePoolWhileItIsOpen()
	{
		var levelBefore = Level.Current;
		var screenBefore = Screen.Size;

		Screen.Size = new Point2( 2048, 1536 );

		var pool = new ParkStaffPool( new ParkBalance( "jungle", easyMode: true ), gameTick: 0 );
		var level = (Level)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject( typeof( Level ) );
		typeof( Level ).GetProperty( nameof( Level.StaffPool ) )!.SetValue( level, pool );
		Level.Current = level;

		try
		{
			var stack = new UI.WindowStack();
			var screen = new UI.ParkHireScreen( stack );
			var list = (UI.UiList)typeof( UI.ParkHireScreen )
				.GetField( "_list", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic )!.GetValue( screen )!;
			var kind = (int)typeof( UI.ParkHireScreen )
				.GetField( "_kind", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic )!.GetValue( screen )!;

			stack.Open( screen );

			int[] Shown() => list.Rows.Select( row => row.Id ).OrderBy( id => id ).ToArray();
			int[] InThePool() => pool.OfKind( kind ).Select( person => person.Id ).OrderBy( id => id ).ToArray();

			CollectionAssert.AreEqual( InThePool(), Shown(), "the list opens on the pool's candidates of its kind" );

			var changes = 0;

			for ( var tick = 1; tick <= 1100; ++tick )
			{
				var before = pool.Candidates.Count;

				pool.Sweep( tick, _ => 0 );

				if ( pool.Candidates.Count == before )
					continue;

				++changes;
				CollectionAssert.AreEqual( InThePool(), Shown(), $"after the pool changed on sweep {tick}" );
			}

			Assert.IsTrue( changes > 10, "the pool dropped and topped up many times in 1100 sweeps" );

			// Closed, it follows nothing: the pool changes and the list it left does not.
			stack.Close( screen );

			var rows = Shown();

			for ( var tick = 1101; tick <= 2200; ++tick )
				pool.Sweep( tick, _ => 0 );

			CollectionAssert.AreNotEqual( rows, InThePool(), "the pool went on changing" );
			CollectionAssert.AreEqual( rows, Shown(), "and the closed screen's list did not" );
		}
		finally
		{
			Level.Current = levelBefore!;
			Screen.Size = screenBefore;
		}
	}
}
