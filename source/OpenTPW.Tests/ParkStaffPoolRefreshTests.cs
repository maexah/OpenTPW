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

		// The park is full of guards (kind 3) throughout, and employs nobody else. A kind is full only once somebody
		// of it is employed, so a limit met by nobody is not: every other kind is topped up though the park asks 0.
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
