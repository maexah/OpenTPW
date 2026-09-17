using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// What the shipped park's save says about the animation each person is playing, checked against the scripts
/// decoded out of <c>testme.exe</c>.
///
/// <para>
/// <b>These are the strongest evidence that the decode is right, and they are worth more than the rest of the
/// sprite tests put together.</b> Everything in <see cref="SpriteScriptTests"/> checks the copied table
/// against itself - it would pass just as happily against a table that had been read out of the wrong address
/// entirely, so long as it were self-consistent. These check it against a file the decode had no part in
/// making: the save records which script each person was on and how far into it, in the executable's own
/// numbering, and those numbers have to land on instructions the decode says exist.
/// </para>
/// <para>
/// <b>And the positions are not arbitrary.</b> Showing a picture is the only thing that ends a turn, so a
/// park saved between turns must have every sprite's counter sitting <i>just past</i> a frame instruction.
/// That is a genuinely falsifiable prediction about a file, and getting the operand widths wrong by even one
/// word would break it.
/// </para>
/// </summary>
[TestClass]
public class ParkSpriteStateTests
{
	private BaseFileSystem data = null!;

	[TestInitialize]
	public void MountTheGame() => data = GameData.Required();

	private const string ShippedPark = "levels/jungle/Easymode.TPWI";

	private ParkWorld World()
	{
		using var stream = new MemoryStream( data.ReadAllBytes( ShippedPark ) );
		return new ParkWorld( new SaveReader( stream ).ReadFile() );
	}

	/// <summary>Only the sprites that belong to a person - the table can hold litter and balloons too.</summary>
	private static ParkWorld.Sprite[] People( ParkWorld world )
	{
		var slots = world.People.Select( person => person.SpriteSlot ).ToHashSet();

		return [.. world.Sprites.Where( sprite => slots.Contains( sprite.Slot ) )];
	}

	/// <summary>
	/// <b>Every person in the park is on a script the decode knows, at a word the decode says is an
	/// instruction.</b> If the operand widths were wrong the saved positions would fall between instructions
	/// and this would say so, person by person.
	/// </summary>
	[TestMethod]
	public void EverySavedPositionLandsOnAnInstruction()
	{
		var people = People( World() );

		Assert.AreEqual( 18, people.Length, "the shipped park's five staff and thirteen guests" );

		foreach ( var sprite in people )
		{
			Assert.IsTrue( SpriteScript.HasInstructionAt( sprite.Script ),
				$"slot {sprite.Slot} is on script {sprite.Script}, which the decode does not know" );

			Assert.IsTrue( SpriteScript.HasInstructionAt( sprite.Pc ),
				$"slot {sprite.Slot} is stopped at word {sprite.Pc}, which is not an instruction" );
		}
	}

	/// <summary>
	/// <b>The park was saved mid-walk, and the walk is the script the decode calls the walk.</b> Sixteen
	/// people on the walking script and two standing - and the two standing are the two the sprite table
	/// already independently says are on set 0.
	/// </summary>
	[TestMethod]
	public void SixteenOfThemAreWalkingAndTwoAreStanding()
	{
		var people = People( World() );

		var walking = people.Count( sprite => sprite.Script == SpriteScript.EntryFor( SpriteScript.Walking ) );
		var standing = people.Count( sprite => sprite.Script == SpriteScript.EntryFor( SpriteScript.Standing ) );

		Assert.AreEqual( 16, walking, "on script 42, the eight-picture walk" );
		Assert.AreEqual( 2, standing, "on script 90, the one-picture stand" );
		Assert.AreEqual( people.Length, walking + standing, "and nobody is on anything else" );
	}

	/// <summary>
	/// <b>The set each person is wearing is the set their script chooses.</b> Two numbers written into the
	/// file at different moments by different code, agreeing - which they can only do if the script decoded
	/// out of the executable is the script the game was actually running.
	/// </summary>
	[TestMethod]
	public void TheSetEachPersonWearsIsTheOneTheirScriptChooses()
	{
		foreach ( var sprite in People( World() ) )
		{
			var expected = sprite.Script == SpriteScript.EntryFor( SpriteScript.Walking ) ? 1 : 0;

			Assert.AreEqual( expected, sprite.Set,
				$"slot {sprite.Slot} is on script {sprite.Script} but is wearing set {sprite.Set}" );
		}
	}

	/// <summary>
	/// <b>Nobody was saved part-way through their preamble.</b> A script chooses its set and shows its first
	/// picture on the same turn, so a counter can only ever come to rest past a frame instruction - never on
	/// the set, and never on the locals write the scripts open with.
	///
	/// <para>
	/// This is the assertion that would fail if the two new fields were read from the wrong offsets and
	/// happened to hold something plausible: a wrong pair would have to land on instruction boundaries
	/// <i>and</i> on frame boundaries <i>and</i> agree with the set, eighteen times over.
	/// </para>
	/// </summary>
	[TestMethod]
	public void EveryCounterRestsJustPastAPicture()
	{
		foreach ( var sprite in People( World() ) )
		{
			var entry = sprite.Script;

			// The words a script's frames occupy: past the locals write and the set, two words apart.
			var firstFrame = entry + 3 + 2;

			Assert.IsTrue( sprite.Pc > entry,
				$"slot {sprite.Slot} is at {sprite.Pc}, at or before its script's first word {entry}" );

			Assert.IsTrue( sprite.Pc >= firstFrame && (sprite.Pc - firstFrame) % 2 == 0,
				$"slot {sprite.Slot} is at {sprite.Pc}, which is not just past a picture of script {entry}" );
		}
	}

	/// <summary>
	/// <b>And the people are saved scattered across the cycle rather than in step.</b> Not a formality: if
	/// the counter were being read from a field that happened to be constant, every assertion above would
	/// still pass and the whole park would walk as one animal.
	/// </summary>
	[TestMethod]
	public void TheWalkersAreScatteredAcrossTheCycle()
	{
		var walkers = People( World() )
			.Where( sprite => sprite.Script == SpriteScript.EntryFor( SpriteScript.Walking ) )
			.Select( sprite => sprite.Pc )
			.ToList();

		Assert.IsTrue( walkers.Distinct().Count() >= 4,
			$"the sixteen walkers stop at only {walkers.Distinct().Count()} distinct words: "
			+ string.Join( ", ", walkers.Distinct().OrderBy( pc => pc ) ) );
	}
}
