using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// A clock reading a script keeps in a variable, moved across a load onto the clock it runs on, as
/// <c>ParkRides.Resume</c> moves the struct's own deadlines (docs/exe/park.md, "What a kept <c>GETTIME</c> reading is").
/// Each case puts a jungle script where Alexah's jungle saves left it, with its reading on the save's clock, and moves
/// it as a load does. These read real game files and are skipped where there is no installation.
/// </summary>
[TestClass]
public class RideScriptKeptReadingsTests
{
	private BaseFileSystem data = null!;

	[TestInitialize]
	public void MountTheGame() => data = FileSystem = GameData.Required();

	/// <summary>The shipped park's clock, the save's moment.</summary>
	private const int Saved = 114_374_804;

	/// <summary>The load's moment on the clock the scripts run on.</summary>
	private const int Loaded = 1_000;

	/// <summary>What <c>ParkRides.Moved</c> does to a reading: keeps its distance from the save's moment.</summary>
	private static int Move( int reading ) => unchecked(reading - Saved + Loaded);

	private RideScript Script( string path, int position, params (string Name, int Value)[] variables )
	{
		using var stream = data.OpenRead( path );
		var script = new RideScript( new RideScriptFile( stream ) );

		Assert.IsTrue( script.ResumeAt( position ), $"word {position} of {path}" );

		foreach ( var (name, value) in variables )
			Assert.IsTrue( script.Set( name, value ), name );

		return script;
	}

	/// <summary>Turns on the scheduler's every-eighth-tick beat, 248 ms apart, from the load up to <paramref name="until"/>.</summary>
	private static void RunTo( RideScript script, float until )
	{
		for ( var now = (float)Loaded; now <= until; now += 248f )
			script.Turn( now );
	}

	/// <summary>
	/// <b>A loaded Mumbo with nobody boarding starts its go 9.5 s after the load</b>, as the engine does: saved on its
	/// branch back into the boarding loop (word 62) with <c>VAR_STARTNOW</c> 9,500 ms past the save's moment, it is
	/// still looping at 9.3 s and has left the loop for its empty go (word 65 on) by 9.8 s.
	/// </summary>
	[TestMethod]
	public void ALoadedMumboStartsItsGoWhenItsSaveSaid()
	{
		var mumbo = Script( "levels/jungle/rides/mumbo/Mumbo.RSE", 62,
			("VAR_STARTNOW", Saved + 9_500), ("VAR_SPACELEFT", 4), ("VAR_CAPACITY", 4) );

		Assert.AreEqual( 1, mumbo.MoveKeptReadings( Move ), "one reading kept" );
		Assert.AreEqual( Loaded + 9_500, mumbo["VAR_STARTNOW"], "moved by its distance from the save" );

		RunTo( mumbo, Loaded + 9_300 );
		Assert.IsTrue( mumbo.Position is >= 20 and <= 62, $"still boarding at 9.3 s, word {mumbo.Position}" );

		RunTo( mumbo, Loaded + 9_800 );
		Assert.IsTrue( mumbo.Position > 62, $"gone by 9.8 s, word {mumbo.Position}" );
	}

	/// <summary>
	/// <b>A loaded Monkey starts its go 7.3 s after the load</b>: saved on its branch at word 85, its deadline 7,300 ms
	/// past the save's moment; <c>VAR_RUNNING</c> is set by the go's first word (88), not before.
	/// </summary>
	[TestMethod]
	public void ALoadedMonkeyStartsItsGoWhenItsSaveSaid()
	{
		var monkey = Script( "levels/jungle/rides/monkey/Monkey.rse", 85,
			("VAR_STARTNOW", Saved + 7_300), ("VAR_SPACELEFT", 4), ("VAR_CAPACITY", 4) );

		monkey.MoveKeptReadings( Move );

		RunTo( monkey, Loaded + 7_100 );
		Assert.AreEqual( 0, monkey["VAR_RUNNING"], "not running at 7.1 s" );

		RunTo( monkey, Loaded + 7_600 );
		Assert.AreEqual( 1, monkey["VAR_RUNNING"], "running by 7.6 s" );
	}

	/// <summary>
	/// <b>A loaded gift shop's idle timer runs out when its save said</b>: <c>VAR_TIMER1</c> 4,000 ms past the save's
	/// moment, standing at the top of its loop (word 25); at 3.8 s it still holds the moved deadline, and by 4.3 s the
	/// shop has passed it and written a fresh one (word 67 or 82).
	/// </summary>
	[TestMethod]
	public void ALoadedGiftShopIdlesWhenItsSaveSaid()
	{
		var shop = Script( "levels/jungle/shops/giftshop/giftshop.RSE", 25, ("VAR_TIMER1", Saved + 4_000) );

		shop.MoveKeptReadings( Move );
		Assert.AreEqual( Loaded + 4_000, shop["VAR_TIMER1"], "moved" );

		RunTo( shop, Loaded + 3_800 );
		Assert.AreEqual( Loaded + 4_000, shop["VAR_TIMER1"], "not yet due at 3.8 s" );

		RunTo( shop, Loaded + 4_300 );
		Assert.AreNotEqual( Loaded + 4_000, shop["VAR_TIMER1"], "passed and written again by 4.3 s" );
	}

	/// <summary>
	/// <b>A pair a turn split is moved too.</b> A Mumbo saved on its <c>SUB</c> (word 30), right after
	/// <c>GETTIME VAR_TEMP</c>, holds the save's moment in <c>VAR_TEMP</c> and in its result register; both move with
	/// the deadline, so the test that follows still finds 9.5 s to go rather than a deadline long past.
	/// </summary>
	[TestMethod]
	public void APairATurnSplitIsMovedWhole()
	{
		var mumbo = Script( "levels/jungle/rides/mumbo/Mumbo.RSE", 30,
			("VAR_STARTNOW", Saved + 9_500), ("VAR_TEMP", Saved), ("VAR_SPACELEFT", 4), ("VAR_CAPACITY", 4) );

		mumbo.RestoreStacks( [.. mumbo.Stack], mumbo.Stack.Count - 1, 0, Saved );

		Assert.AreEqual( 3, mumbo.MoveKeptReadings( Move ), "the deadline, VAR_TEMP and the register" );
		Assert.AreEqual( Loaded, mumbo["VAR_TEMP"], "VAR_TEMP on this clock" );
		Assert.AreEqual( Loaded, mumbo.Result, "and the register" );

		RunTo( mumbo, Loaded + 100 );
		Assert.IsTrue( mumbo.Position is >= 20 and <= 62, $"still boarding, word {mumbo.Position}" );
	}

	/// <summary>Standing anywhere but on that <c>SUB</c>, <c>VAR_TEMP</c> is no reading and stays as it is.</summary>
	[TestMethod]
	public void AnUnsplitTempIsLeftAlone()
	{
		var mumbo = Script( "levels/jungle/rides/mumbo/Mumbo.RSE", 62, ("VAR_STARTNOW", Saved + 9_500), ("VAR_TEMP", 7) );

		mumbo.MoveKeptReadings( Move );

		Assert.AreEqual( 7, mumbo["VAR_TEMP"] );
	}

	/// <summary>A deadline never written is nought, and stays nought, the engine's "passed".</summary>
	[TestMethod]
	public void ANoughtIsNoReading()
	{
		var mumbo = Script( "levels/jungle/rides/mumbo/Mumbo.RSE", 62 );

		Assert.AreEqual( 0, mumbo.MoveKeptReadings( Move ) );
		Assert.AreEqual( 0, mumbo["VAR_STARTNOW"] );
	}

	/// <summary>
	/// <b>The walk finds exactly what Q181's sweep found</b>, over all 308 scripts and their 197 distinct bodies:
	/// <c>VAR_STARTNOW</c> in 51 bodies, <c>VAR_TIMER1</c> in 2 and <c>VAR_ENDTIME</c> in 1, and nothing else.
	/// </summary>
	[TestMethod]
	public void TheWalkFindsTheThreeKeptReadingsAndNoOther()
	{
		var bodies = new Dictionary<string, string[]>();

		foreach ( var (_, file) in RideScriptTests.Scripts() )
		{
			var body = string.Join( ";", file.Instructions.Select( instruction =>
				$"{instruction.Opcode} {string.Join( ",", instruction.Operands )}" ) );

			bodies[body] = [.. RideScript.KeptReadings( file ).Select( slot => file.VariableNames[slot] )];
		}

		Assert.AreEqual( 197, bodies.Count, "distinct bodies" );

		var count = bodies.Values.SelectMany( names => names ).GroupBy( name => name )
			.ToDictionary( group => group.Key, group => group.Count() );

		CollectionAssert.AreEquivalent( new[] { "VAR_STARTNOW", "VAR_TIMER1", "VAR_ENDTIME" }, count.Keys.ToArray() );
		Assert.AreEqual( 51, count["VAR_STARTNOW"] );
		Assert.AreEqual( 2, count["VAR_TIMER1"] );
		Assert.AreEqual( 1, count["VAR_ENDTIME"] );
	}
}
