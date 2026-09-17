using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// What Lost Kingdom's thirteen guests were doing when the park was saved. These read real game files and
/// are skipped where there is no installation: see <see cref="GameData"/>.
///
/// <para>
/// A guest's own block is reached by summing the sizes of every field before it, so nothing in it can be
/// found by searching and a map that is one byte out still produces a full set of plausible numbers. That
/// is what these tests are for, and it is why they pin values rather than ranges: the whole table below
/// was measured from this save before any of it was written into a reader, and each row has to come back
/// exactly.
/// </para>
/// </summary>
[TestClass]
public class ParkGuestStateTests
{
	private BaseFileSystem data = null!;

	/// <summary>
	/// The mount is also handed to the global file system, because <see cref="ParkBalance"/> reads its
	/// files through that rather than taking one - the arrangement <see cref="ParkWeatherTests"/> uses.
	/// </summary>
	[TestInitialize]
	public void MountTheGame() => FileSystem = data = GameData.Required();

	private const string ShippedPark = "levels/jungle/Easymode.TPWI";

	/// <summary>
	/// Through a stream of this test's own bytes rather than the global file system, which belongs to a
	/// running game - the same rule the other park tests follow.
	/// </summary>
	private ParkWorld World()
	{
		using var stream = new MemoryStream( data.ReadAllBytes( ShippedPark ) );
		return new ParkWorld( new SaveReader( stream ).ReadFile() );
	}

	private ParkWorld.Person[] Guests( ParkWorld world ) =>
		[.. world.People.Where( person => person.Guest != null )];

	/// <summary>
	/// Every guest in the park, field by field.
	///
	/// <para>
	/// The thing ids are pinned in file order as well as by value, because the record prefix holds the id
	/// of the <i>next</i> thing rather than its own and reading it the other way round is off by one
	/// everywhere while still looking entirely plausible - the mistake that once made the gate come out as
	/// the traffic lights.
	/// </para>
	/// <para>
	/// Cash is the row worth looking at twice. It is not a constant and not a sequence: thirteen guests
	/// carry eleven different amounts, each drawn when that guest was made from the starting money of
	/// their kind. What ties those amounts to the kinds in the third column is the balance file, which
	/// this reader knows nothing about, and which is checked against them separately.
	/// </para>
	/// </summary>
	[TestMethod]
	public void EveryGuestCarriesTheStateTheSaveGaveThem()
	{
		var expected = new (int ThingId, int State, int PersonType, int Cash, int ExitLevel,
			float Thirst, float Hunger, float Toilet)[]
		{
			(42, 2, 5, 684, 142, 36, 18, 13),
			(41, 5, 7, 510,  98, 13, 25, 15),
			(40, 5, 2, 654,  57, 12, 61, 24),
			(39, 2, 6, 340,  40, 35, 40,  3),
			(38, 5, 0, 306,  55, 17, 18, 27),
			(37, 5, 5, 570,  45, 13,  3, 10),
			(36, 5, 1, 450, 121, 38, 22, 12),
			(35, 2, 7, 550,  94, 10,  6,  5),
			(34, 5, 3, 749, 112,  3,  7,  3),
			(33, 3, 1, 535,  70, 18, 34, 19),
			(32, 5, 6, 352,  54, 58, 63, 14),
			(31, 2, 1, 435,  92, 35, 11,  3),
			(29, 2, 2, 570, 148, 39,  1, 20)
		};

		var guests = Guests( World() );

		Assert.AreEqual( expected.Length, guests.Length, "guests in the park" );

		CollectionAssert.AreEqual( expected.Select( row => row.ThingId ).ToArray(),
			guests.Select( guest => guest.ThingId ).ToArray(),
			"the guests, by thing id, in the order the file lists them" );

		for ( var i = 0; i < expected.Length; ++i )
		{
			var row = expected[i];
			var guest = guests[i].Guest!.Value;

			Assert.AreEqual( row.State, guest.State, $"guest {row.ThingId} state" );
			Assert.AreEqual( row.PersonType, guest.PersonType, $"guest {row.ThingId} kind" );
			Assert.AreEqual( row.Cash, guest.Cash, $"guest {row.ThingId} cash" );
			Assert.AreEqual( row.ExitLevel, guest.ExitLevel, $"guest {row.ThingId} exit level" );
			Assert.AreEqual( row.Thirst, guest.Thirst, $"guest {row.ThingId} thirst" );
			Assert.AreEqual( row.Hunger, guest.Hunger, $"guest {row.ThingId} hunger" );
			Assert.AreEqual( row.Toilet, guest.Toilet, $"guest {row.ThingId} toilet" );
		}
	}

	/// <summary>
	/// The park is saved a moment after it opened, and every guest says so the same way.
	///
	/// <para>
	/// <b>Why a plain range check would be worthless here, and this is the part worth keeping.</b> The
	/// needs are floats and the obvious test is <c>0 &lt;= need &lt;= 100</c> - but a small integer read
	/// as a float is a denormal of about <c>1e-43</c>, which passes that check comfortably. A reader
	/// pointed a few bytes off the real block therefore still produces needs that look perfectly legal.
	/// What does not survive being wrong is a value that is <i>specific</i>: happiness being exactly the
	/// 50 a guest is constructed with, a saved state of exactly 6, and needs that are whole numbers
	/// rather than merely small ones.
	/// </para>
	/// <para>
	/// The last two assertions are the anti-vacuity guard. Illness, litter, destination and queue
	/// position are all zero on all thirteen, which is what arriving should look like - but a column of
	/// zeros agrees with a misread block just as readily, so the needs that are <i>not</i> uniform have
	/// to be seen to vary.
	/// </para>
	/// </summary>
	[TestMethod]
	public void EveryGuestHasJustArrivedAndNoneHasChosenAnything()
	{
		var guests = Guests( World() );

		foreach ( var person in guests )
		{
			var guest = person.Guest!.Value;
			var where = $"guest {person.ThingId}";

			Assert.AreEqual( 50f, guest.Happiness, $"{where} should carry the happiness a guest is made with" );
			Assert.AreEqual( ParkWorld.GuestState.Deciding, guest.SavedState,
				$"{where} should have the state a guest is constructed in saved behind them" );

			CollectionAssert.Contains( new[] { 2, 3, 5 }, guest.State,
				$"{where} is in state {guest.State}, and a park this new should hold only guests heading "
				+ "for the gate, waiting for it to open, or coming through it" );

			Assert.IsTrue( guest.PersonType is >= 0 and < ParkWorld.GuestState.PersonTypes,
				$"{where} is kind {guest.PersonType}, which is not one the balance file describes" );

			Assert.AreEqual( 0f, guest.Illness, $"{where} illness" );
			Assert.AreEqual( 0f, guest.Litter, $"{where} litter carried" );
			Assert.AreEqual( 0, guest.MajorDest, $"{where} should not have chosen anywhere to go yet" );
			Assert.AreEqual( 0, guest.QueuePos, $"{where} should not be standing in a queue" );

			foreach ( var (need, name) in new[]
			{
				(guest.Happiness, "happiness"), (guest.Thirst, "thirst"), (guest.Hunger, "hunger"),
				(guest.Toilet, "toilet"), (guest.Illness, "illness"), (guest.Litter, "litter")
			} )
			{
				Assert.IsTrue( need is >= 0f and <= 100f, $"{where} {name} is {need}, outside 0..100" );
				Assert.AreEqual( MathF.Round( need ), need,
					$"{where} {name} is {need}, which is not the whole number every need in this save is - "
					+ "a fractional one means the block is being read at the wrong offset" );
			}
		}

		Assert.IsTrue( guests.Select( person => person.Guest!.Value.Cash ).Distinct().Count() >= 8,
			"the guests should not all be carrying the same money, or this test proves nothing" );
		Assert.IsTrue( guests.Select( person => person.Guest!.Value.Thirst ).Distinct().Count() >= 8,
			"the guests should not all be equally thirsty, or this test proves nothing" );
	}

	/// <summary>
	/// Staff have no guest block, and this is the control for every test above.
	///
	/// <para>
	/// The five kinds of staff share the same 390-byte person base and then carry a staff base and their
	/// own fields where a guest carries theirs. Their records are close enough in size - 509 to 513
	/// against a guest's 533 - that reading a guest's block off one would not run past the end and would
	/// quietly return numbers. Nothing here reads them, so nothing here can misreport them.
	/// </para>
	/// </summary>
	[TestMethod]
	public void OnlyGuestsCarryAGuestBlock()
	{
		var world = World();

		Assert.AreEqual( 18, world.People.Count, "people in the park" );
		Assert.AreEqual( 13, world.People.Count( person => person.Guest != null ), "guests among them" );

		foreach ( var person in world.People )
		{
			Assert.AreEqual( person.Model == 1, person.Guest != null,
				$"thing {person.ThingId} is model {person.Model}, so it should "
				+ (person.Model == 1 ? "carry" : "have no") + " guest block" );
		}
	}

	/// <summary>
	/// Where the park's guests are actually going - and the answer explains what a player sees at the gate.
	///
	/// <para>
	/// <b>All seven guests coming through the gate are walking to the exact centre of one of its two
	/// entrance cells.</b> That is why they stop in the gateway rather than passing under it: the cell
	/// centre <i>is</i> their destination, and what should happen next - choosing somewhere inside the park
	/// and setting off again - is the <c>Deciding</c> hub, which is not built. Until it is, a guest who
	/// arrives stands in the archway.
	/// </para>
	/// <para>
	/// <b>The two halves of this come from files that know nothing about each other</b>, which is what makes
	/// it worth pinning: the entrance cells are text a developer typed into the balance file, and the
	/// targets are 16.16 fixed-point coordinates inside a saved park. Neither was used to derive the other,
	/// so seven targets landing dead on two named cell centres cannot be an artefact of the reader.
	/// </para>
	/// <para>
	/// The five heading <i>for</i> the gate are the control: they stop four cells short of it, which is why
	/// they are seen queueing in front rather than inside.
	/// </para>
	/// </summary>
	[TestMethod]
	public void TheGuestsComingThroughTheGateAreHeadingForItsTwoEntranceCells()
	{
		var balance = new ParkBalance( "jungle" );
		var one = ParkWorld.NavigatorState.One;

		var entranceY = balance.Int( "FixedItemInfo.EntranceAPosY", -1 );

		Assert.AreEqual( 17, entranceY, "the balance file's entrance row" );
		Assert.AreEqual( 47, balance.Int( "FixedItemInfo.EntranceAPosX", -1 ), "entrance A" );
		Assert.AreEqual( 48, balance.Int( "FixedItemInfo.EntranceBPosX", -1 ), "entrance B" );
		Assert.AreEqual( entranceY, balance.Int( "FixedItemInfo.EntranceBPosY", -1 ),
			"both entrance cells are on one row" );

		var entering = 0;
		var heading = 0;

		foreach ( var person in Guests( World() ) )
		{
			var guest = person.Guest!.Value;
			var nav = person.Navigator;
			var where = $"guest {person.ThingId}";

			// The centre of a cell, which is where the pathfinder walks to - cell * One + One / 2.
			var cellX = nav.TargetX / one;
			var cellY = nav.TargetY / one;

			if ( guest.State == 5 )
			{
				++entering;

				Assert.AreEqual( entranceY, cellY, $"{where} is entering, so should be walking to the gate row" );
				Assert.IsTrue( cellX == 47 || cellX == 48,
					$"{where} is entering but is walking to cell ({cellX},{cellY}), not an entrance cell" );

				// Dead centre, not merely inside: the half is what says this is a pathfinder's cell centre.
				Assert.AreEqual( (cellX * one) + (one / 2), nav.TargetX, $"{where} across" );
				Assert.AreEqual( (cellY * one) + (one / 2), nav.TargetY, $"{where} down" );
			}
			else if ( guest.State == 2 )
			{
				++heading;

				Assert.IsTrue( cellY < entranceY,
					$"{where} is heading for the gate and should stop short of it, not at row {cellY}" );
			}
		}

		Assert.AreEqual( 7, entering, "guests coming through the gate" );
		Assert.AreEqual( 5, heading, "guests still heading for it" );
	}

	/// <summary>
	/// The money a guest arrived with, against the money their kind is supposed to start with.
	///
	/// <para>
	/// This is the strongest thing said about the guest block anywhere, because the two halves come from
	/// places that know nothing about each other. <c>mCash</c> and <c>mPersonType</c> are offsets derived
	/// by summing field sizes out of the executable; the amounts and the spread are text a developer
	/// typed into a balance file. Neither was used to work out the other, so them agreeing thirteen times
	/// out of thirteen cannot be an artefact of the offsets - if the block were being read a few bytes
	/// out, the kind and the money would both be wrong and would have no reason to remain consistent.
	/// </para>
	/// <para>
	/// The second loop is the control, and it is what stops the first from being a formality. The eight
	/// bands overlap heavily, so a good many wrong pairings still land inside a band; what must not
	/// happen is that <i>every</i> guest survives being matched to the wrong kind. If that ever became
	/// true the check would have stopped discriminating, and this says so rather than passing quietly.
	/// </para>
	/// </summary>
	[TestMethod]
	public void EachGuestsCashIsWithinTheirKindsBandInTheBalanceFile()
	{
		var balance = new ParkBalance( "jungle" );
		var spread = balance.Int( "PeepInfo.StartingCashVarPc", -1 );

		Assert.AreEqual( 15, spread, "the balance file's spread either way on a guest's starting money" );

		var guests = Guests( World() );

		bool Within( int cash, int type )
		{
			var start = balance.Int( $"PeepTypes[{type}].StartingCash", -1 );

			Assert.IsTrue( start > 0, $"PeepTypes[{type}].StartingCash should be in the balance file" );

			return cash >= start * (100 - spread) / 100f && cash <= start * (100 + spread) / 100f;
		}

		foreach ( var person in guests )
		{
			var guest = person.Guest!.Value;

			Assert.IsTrue( Within( guest.Cash, guest.PersonType ),
				$"guest {person.ThingId} is kind {guest.PersonType}, which starts with "
				+ $"{balance.Int( $"PeepTypes[{guest.PersonType}].StartingCash", -1 )} give or take {spread}%, "
				+ $"and they carry {guest.Cash}" );
		}

		var misfits = guests.Count( person =>
			!Within( person.Guest!.Value.Cash,
				(person.Guest!.Value.PersonType + 1) % ParkWorld.GuestState.PersonTypes ) );

		Assert.IsTrue( misfits > 0,
			"matching every guest to the next kind along should put at least one outside its band, "
			+ "or the bands are too wide for this check to mean anything" );
	}
}
