using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// What a park save says about the scripts that were running in it - the module that stops a loaded
/// park building everything in it a second time. These read real game files and are skipped where
/// there is no installation - see <see cref="GameData"/>.
/// </summary>
[TestClass]
public class ParkScriptStateTests
{
	private const string ShippedPark = "levels/jungle/Easymode.TPWI";

	/// <summary>The Belly Bounce: thing 13, catalogue item 1100, and the park's only ride.</summary>
	private const int BellyBounceThing = 13;

	/// <summary>Its script handle, which is what <c>mRideScriptHandle</c> holds on that object.</summary>
	private const int BellyBounceHandle = 3;

	/// <summary>
	/// Where <c>Bouncy.RSE</c>'s construction clip is started - <c>WAITANIM 0 0</c>, role 0 entry 0 -
	/// counted in words. Everything this file is about is the difference between starting at nought,
	/// which runs this, and starting where the save left off, which does not.
	/// </summary>
	private const int ConstructionInstruction = 4;

	private BaseFileSystem data = null!;

	[TestInitialize]
	public void MountTheGame() => data = GameData.Required();

	private ParkWorld Park()
	{
		using var stream = new MemoryStream( data.ReadAllBytes( ShippedPark ) );

		return new ParkWorld( new SaveReader( stream ).ReadFile() );
	}

	private RideScriptFile Script( string path )
	{
		using var stream = new MemoryStream( data.ReadAllBytes( path ) );

		return new RideScriptFile( stream );
	}

	/// <summary>
	/// Whether the file system can offer this at all, treating "not there" and "will not open" alike -
	/// the same helper the other park tests keep, for the same reason.
	/// </summary>
	private bool Has( string path )
	{
		try
		{
			using var stream = data.OpenRead( path );

			return stream != null;
		}
		catch ( System.Exception )
		{
			return false;
		}
	}

	/// <summary>
	/// The module reads, and it reads to the end: every record has to finish on its own
	/// <c>"OBJ "</c> guard, so fourteen records that all close is agreement with the original about
	/// every byte in between rather than a plausible-looking parse of the first one.
	/// </summary>
	[TestMethod]
	public void TheShippedParkSavesOneScriptPerPlacedThing()
	{
		var park = Park();
		var scripts = park.ScriptStates;

		Assert.IsNull( scripts.Problem, "the script module should read without a surprise" );
		Assert.IsTrue( scripts.ClosedOnGuard, "every record should have ended on its object guard" );

		Assert.AreEqual( 14, scripts.Declared, "scripts the module says it holds" );
		Assert.AreEqual( 14, scripts.ByHandle.Count, "and the number actually read back" );

		Assert.AreEqual( park.Objects.Count, scripts.Declared,
			"the park places exactly as many things as it saves scripts for" );
	}

	/// <summary>
	/// The join between the two modules: a saved script is matched to a thing by the handle the
	/// thing's own object record carries. Both sets are read from completely separate parts of the
	/// file, so their agreeing - including the gap at 5, which neither uses - is what makes the
	/// handle the right key rather than a number that merely looks like one.
	/// </summary>
	[TestMethod]
	public void EverySavedScriptIsTheScriptOfSomePlacedThing()
	{
		var park = Park();

		var fromObjects = park.Objects.Select( o => o.RideScript ).OrderBy( h => h ).ToArray();
		var fromScripts = park.ScriptStates.ByHandle.Keys.OrderBy( h => h ).ToArray();

		CollectionAssert.AreEqual( fromObjects, fromScripts,
			"the object records' script handles and the saved scripts' own handles" );

		Assert.AreEqual( BellyBounceHandle,
			park.Objects.Single( o => o.ThingId == BellyBounceThing ).RideScript,
			"the Belly Bounce's script handle" );
	}

	/// <summary>
	/// <b>The whole point of the module, on the one thing a player noticed.</b> The Belly Bounce's
	/// script opens by starting its construction clip, and the save has it parked twenty instructions
	/// past that - so a park that resumes does not hatch the ride out of its egg again, and a park
	/// that starts at nought does.
	/// </summary>
	[TestMethod]
	public void TheBellyBounceIsSavedPastTheInstructionThatBuildsIt()
	{
		var saved = Park().ScriptStates.For( BellyBounceHandle );

		Assert.IsNotNull( saved, "the Belly Bounce's script should have been saved" );
		Assert.AreEqual( 46, saved!.Value.Position, "where its script was left" );
		Assert.AreEqual( 208, saved.Value.BodyWords, "how long Bouncy.RSE is" );

		Assert.IsTrue( saved.Value.Position > ConstructionInstruction,
			$"word {ConstructionInstruction} starts the construction clip, so a resumed script must be past it" );

		// And that word really is what this claims it is, read out of the script itself rather than
		// asserted: WAITANIM, role 0, entry 0. WAITANIM starts the animation as well as waiting on it.
		var script = Script( "levels/jungle/rides/bouncy/Bouncy.RSE" );
		var building = script.Instructions.Single( i => i.Address == ConstructionInstruction );

		Assert.AreEqual( Opcode.WAITANIM, building.Opcode, "the instruction a fresh start would run" );
		Assert.AreEqual( 0, building.Operands[0].Value, "role 0 - the construction clip" );
		Assert.AreEqual( 0, building.Operands[1].Value, "entry 0" );
	}

	/// <summary>
	/// A saved counter that fell between instructions would not error - <c>RideScript.Step</c> treats
	/// an address it cannot find as the end of the script, so the thing would simply stand there doing
	/// nothing, which is a worse fault than the one being fixed. Every counter in the shipped park
	/// lands on an instruction, and the Belly Bounce's is checked against the real instruction list.
	/// </summary>
	[TestMethod]
	public void EverySavedCounterIsInsideItsOwnScript()
	{
		var scripts = Park().ScriptStates;

		foreach ( var (handle, saved) in scripts.ByHandle )
		{
			Assert.IsTrue( saved.Position >= 0 && saved.Position < saved.BodyWords,
				$"script handle {handle} was saved at word {saved.Position} of {saved.BodyWords}" );
		}

		var bouncy = Script( "levels/jungle/rides/bouncy/Bouncy.RSE" );
		var at = scripts.For( BellyBounceHandle )!.Value.Position;

		Assert.AreEqual( 1, bouncy.Instructions.Count( i => i.Address == at ),
			$"word {at} should be the start of exactly one instruction" );
	}

	/// <summary>
	/// The variables come back too, and they are checked against a part of the file that has nothing
	/// to do with them: slot 2 is <c>VAR_CAPACITY</c> and the object record's own
	/// <c>mOperatingCapacity</c> says the same number. Six agreements across four different kinds of
	/// thing is what identifies this block as the variable array.
	/// </summary>
	[TestMethod]
	public void TheSavedVariablesAgreeWithTheObjectRecords()
	{
		var park = Park();
		var capacitySlot = 2;
		var agreements = 0;

		foreach ( var placed in park.Objects.Where( o => o.OperatingCapacity != 0 ) )
		{
			var saved = park.ScriptStates.For( placed.RideScript );

			Assert.IsNotNull( saved, $"thing {placed.ThingId} should have a saved script" );
			Assert.IsTrue( saved!.Value.Variables.Length > capacitySlot,
				$"thing {placed.ThingId} should have a capacity slot" );

			Assert.AreEqual( placed.OperatingCapacity, saved.Value.Variables[capacitySlot],
				$"thing {placed.ThingId} (catalogue {placed.CatalogueId}): "
				+ "its record's capacity and its script's VAR_CAPACITY" );

			++agreements;
		}

		Assert.AreEqual( 6, agreements,
			"the ride, the sideshow, the drinks shop and the three toilets all declare a capacity" );
	}

	/// <summary>
	/// The Belly Bounce's own slots, spelled out: fourteen variables - the common twelve plus
	/// <c>VAR_TEMP</c> and <c>VAR_SCREAMING</c> - with its capacity and its duration in them.
	/// </summary>
	[TestMethod]
	public void TheBellyBouncesSavedVariablesHoldItsCapacityAndDuration()
	{
		var park = Park();
		var saved = park.ScriptStates.For( BellyBounceHandle )!.Value;
		var ride = park.Objects.Single( o => o.ThingId == BellyBounceThing );

		Assert.AreEqual( 14, saved.Variables.Length, "how many variables Bouncy.RSE declares" );

		Assert.AreEqual( 5, saved.Variables[2], "VAR_CAPACITY" );
		Assert.AreEqual( 30, saved.Variables[3], "VAR_DURATION" );

		Assert.AreEqual( ride.OperatingCapacity, saved.Variables[2], "and the record agrees" );
		Assert.AreEqual( ride.OperatingDuration, saved.Variables[3], "about both" );
	}

	/// <summary>
	/// The OTHER module - what each thing's model was doing - and the constants its walk rests on.
	///
	/// <para>
	/// <b>This is the test that pins them.</b> The module gives no way to step over a record without
	/// knowing how many animation channels the item runs, and every one of the offsets, the 11-dword
	/// channel size and the one-byte cost of an absent slot is load-bearing: get any of them wrong and
	/// the cursor does not land on the module's end. So asserting that it closes is asserting all of
	/// them at once. Mutating the channel count to a flat 1 - the reading that lands 5,786 bytes short -
	/// fails here, because the walk then misses the module's end; elsewhere it degrades quietly into
	/// "restore nothing", which is the frozen-park regression this half exists to prevent.
	/// </para>
	/// </summary>
	[TestMethod]
	public void TheSavedModelStatesReadAndTheirWalkCloses()
	{
		var park = Park();
		var catalogue = new ParkItemCatalogue( "jungle", data );

		var states = park.ThingStates(
			id => catalogue.TryGet( id, out var item ) ? item.AnimationChannels : 1 );

		Assert.IsNull( states.Problem, "the model module should read without a surprise" );
		Assert.IsTrue( states.ClosedOnEnd, "and the walk must land exactly on the module's end" );

		Assert.AreEqual( 161, states.Things.Count, "present records in the shipped park" );

		// Most of them are scenery with nothing to animate, which is why the sentinel is the common case
		// and why "everything restored" would be the wrong thing to assert.
		var channels = states.Things.Sum( t => t.Channels.Length );
		var running = states.Things.Sum( t => t.Channels.Count( c => c.Role != ParkThingStates.NoRole ) );

		Assert.AreEqual( 163, channels, "channels across every record" );
		Assert.AreEqual( 15, running, "and the few that were actually running something" );

		// The Jungle Spray is the one thing that runs more than one, and it is the reason the count has
		// to come from the item rather than being assumed.
		var sideshow = states.For( 1303, 0 );

		Assert.IsNotNull( sideshow, "the Jungle Spray should have a record" );
		Assert.AreEqual( 3, sideshow!.Value.Channels.Length, "its three lanes" );
		Assert.IsTrue( sideshow.Value.Channels.All( c => c.Role == 2 ), "all on role 2" );

		// And two spot checks against what the running game settles into.
		Assert.AreEqual( 5, states.For( 1403, 0 )!.Value.Channels[0].Role, "the Fountain's role" );
		Assert.AreEqual( 2, states.For( 1100, 0 )!.Value.Channels[0].Role, "the Belly Bounce's role" );

		Assert.AreEqual( ParkThingStates.NoRole, states.For( 1411, 0 )!.Value.Channels[0].Role,
			"and the Staff Room really is saved running nothing" );
	}

	/// <summary>
	/// The three seams the restore is built from, exercised directly rather than through a park.
	///
	/// <para>
	/// <b>Each needs coverage of its own.</b> Binding a park asserts where the counter
	/// ended up, which a working <c>ResumeAt</c> and a broken <c>SeedVariable</c> would both satisfy:
	/// twelve of the fourteen saved variable arrays are nought except slots 2 and 3, and those two are
	/// exactly what <c>VAR_CAPACITY</c> and <c>VAR_DURATION</c> are written with a moment earlier, so
	/// the seeding is provably inert for them. Replacing the whole of <c>SeedVariable</c> with
	/// <c>return true</c> fails here.
	/// </para>
	/// </summary>
	[TestMethod]
	public void TheThreeSeamsOfTheRestoreEachDoTheirOwnJob()
	{
		var script = new RideScript( Script( "levels/jungle/rides/bouncy/Bouncy.RSE" ) );

		Assert.AreEqual( 0, script.Position, "a fresh script starts at its own beginning" );
		Assert.IsFalse( script.IsNamed, "and has not run the NAME it opens with" );

		// TakeDeclaredName: reads word 0's operand without running anything.
		Assert.IsTrue( script.TakeDeclaredName(), "Bouncy.RSE opens with a NAME" );
		Assert.IsTrue( script.IsNamed, "so the script knows its name" );
		Assert.AreNotEqual( string.Empty, script.Name, "and the name is not empty" );
		Assert.AreEqual( 0, script.Position, "and taking it moved nothing" );

		// SeedVariable: writes by slot, and answers whether the slot exists.
		Assert.IsTrue( script.SeedVariable( 5, 4242 ), "slot 5 is one of Bouncy's fourteen" );
		Assert.AreEqual( 4242, script.Variables[5], "and the value landed in it" );
		Assert.IsFalse( script.SeedVariable( 14, 1 ), "slot 14 is past the end of them" );
		Assert.IsFalse( script.SeedVariable( -1, 1 ), "and a negative slot is refused" );

		// ResumeAt: takes an instruction boundary and refuses anything else.
		Assert.IsTrue( script.ResumeAt( 46 ), "46 is where the save left this script" );
		Assert.AreEqual( 46, script.Position, "so that is where it starts" );

		Assert.IsFalse( script.ResumeAt( 47 ), "47 is inside the WAIT at 46, not an instruction" );
		Assert.AreEqual( 46, script.Position, "and a refusal must not move it" );

		Assert.IsFalse( script.ResumeAt( 100000 ), "and neither is a position past the body" );
		Assert.AreEqual( 46, script.Position, "still unmoved" );
	}

	/// <summary>
	/// Resuming steps over the <c>NAME</c> every one of these scripts opens with, and a name is not
	/// decoration - <c>FINDSCRIPTRAND</c> looks another script up by it. So the first instruction of
	/// each of them has to be a <c>NAME</c> for <c>RideScript.TakeDeclaredName</c> to have anything to
	/// read, and this is the check that would notice if one of them were not.
	/// </summary>
	[TestMethod]
	public void EveryPlacedThingsScriptOpensWithItsName()
	{
		var catalogue = new ParkItemCatalogue( "jungle", data );
		var park = Park();
		var seen = 0;

		foreach ( var placed in park.Objects )
		{
			if ( !catalogue.TryGet( placed.CatalogueId, out var item ) )
				continue;

			var path = $"{item.Directory}/{item.Stem}.RSE";

			if ( !Has( path ) )
				continue;

			var script = Script( path );
			var first = script.Instructions.FirstOrDefault( i => i.Address == 0 );

			Assert.IsNotNull( first, $"{path} should have an instruction at word 0" );
			Assert.AreEqual( Opcode.NAME, first!.Opcode, $"{path} should open by naming itself" );

			++seen;
		}

		Assert.IsTrue( seen >= 10, $"only {seen} of the park's scripts were reachable to check" );
	}
}
