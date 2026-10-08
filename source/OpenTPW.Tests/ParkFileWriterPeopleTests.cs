using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace OpenTPW.Tests;

/// <summary>
/// The park file's writer, the people (<c>docs/exe/saves.md</c>, "OpenTPW's writer, the people"): the thing list put
/// together again round the running park's guests and staff, with the sprite table, the cell chains, the staff lists
/// and the message sets that follow it.
/// </summary>
[TestClass]
public class ParkFileWriterPeopleTests
{
	private const string ShippedPark = "levels/jungle/Easymode.TPWI";

	private const int Researcher = 8;

	private ParkWorld shipped = null!;

	[TestInitialize]
	public void ReadTheShippedPark()
	{
		Log ??= new();
		FileSystem = GameData.Required();
		shipped = Read( FileSystem.ReadAllBytes( ShippedPark ) );
		Unimplemented.Forget();
	}

	[TestCleanup]
	public void Forget()
	{
		TestRun.DeleteEvery<ParkPeople>();
		Unimplemented.Forget();
	}

	private static ParkWorld Read( byte[] file )
	{
		using var stream = new MemoryStream( file );
		var reader = new SaveReader( stream );
		return new ParkWorld( reader.ReadFile(), reader.Preamble );
	}

	private ParkFileWriter.Running Running( ParkPeople people ) => new( shipped.GameTick, shipped.ParkClosed != 0,
		shipped.NumberOfVisitorsToDate, shipped.Economy!.Value.Balance, shipped.Camera.Saved!.Value,
		People: people.Written( Level.WrittenThings( shipped ).Contains ) );

	private int length;

	/// <summary>How long the body is with the file's own people: the length nothing made and nothing gone leaves.</summary>
	private int Carried => ParkFileWriter.Body( shipped, Running( new ParkPeople( shipped ) ) with { People = null } ).Length;

	private ParkWorld Written( ParkPeople people, out ParkWorld.PeopleWritten report )
	{
		var body = ParkFileWriter.Body( shipped, Running( people ), out var done );

		report = done!.Value;
		length = body.Length;

		var written = new ParkWorld( body );

		Assert.IsNull( written.Problem );
		Assert.IsTrue( written.ClosedOnTrailer && written.ClosedOnSpriteTrailer, "the list and the sprite table close" );

		return written;
	}

	private static (int, int, int, int, int, int, int) Key( ParkWorld.Person person ) =>
		(person.ThingId, person.Model, person.RawX, person.RawY, person.Angle, person.SpriteKind, person.SpriteBank);

	/// <summary>Every thing is in the chain of one cell, and a person in the chain of the cell they stand on.</summary>
	private static void AssertTheChainsHold( ParkWorld park )
	{
		var seen = new Dictionary<int, (int X, int Y)>();

		for ( var y = 0; y < ParkWorld.MapSize; ++y )
		{
			for ( var x = 0; x < ParkWorld.MapSize; ++x )
			{
				var chain = park.ChainAt( x, y );
				var models = chain.Select( id => park.Things.Single( thing => thing.ThingId == id ).Model ).ToList();

				foreach ( var id in chain )
					Assert.IsTrue( seen.TryAdd( id, (x, y) ), $"thing {id} is in two chains" );

				// mMapParent, the word after mMapChild: whoever stands ahead in the chain, nought at its head.
				for ( var i = 0; i < chain.Count; ++i )
					Assert.AreEqual( i == 0 ? 0 : chain[i - 1], BitConverter.ToUInt16( park.RecordOf( chain[i] )!, 14 ), $"thing {chain[i]}'s parent" );

				Assert.IsTrue( models.SkipWhile( model => model is 1 or (>= 4 and <= 8) ).All( model => model is not (1 or (>= 4 and <= 8)) ),
					$"at ({x},{y}) a person stands behind a thing that is no person" );
			}
		}

		Assert.AreEqual( park.Things.Count, seen.Count, "every thing is on a cell" );

		foreach ( var person in park.People )
			Assert.AreEqual( (person.CellX, person.CellY), seen[person.ThingId], $"person {person.ThingId}" );
	}

	/// <summary>A park nothing has happened in writes the file's own people: the same things in the same order, each
	/// person where and as the file has them, on the same sprite, in the same chains, lists and sets.</summary>
	[TestMethod]
	public void AParkLeftAloneWritesTheFilesPeople()
	{
		var written = Written( new ParkPeople( shipped ), out var report );

		Assert.AreEqual( (18, 0, 0, 100, 18, 0), (report.Kept, report.Made, report.Gone, report.SpriteSlots,
			report.LiveSprites, report.CellsHeaded) );
		CollectionAssert.AreEqual( shipped.Things.ToList(), written.Things.ToList(), "the list and its order" );
		Assert.AreEqual( Carried, length, "nothing made and nothing gone, so no byte more" );

		foreach ( var (was, now) in shipped.People.Zip( written.People ) )
		{
			Assert.AreEqual( Key( was ), Key( now ) );
			Assert.AreEqual( was.SpriteSlot, now.SpriteSlot, "their own slot" );
			Assert.AreEqual( was.Navigator with { }, now.Navigator, $"person {was.ThingId}'s navigator" );
			Assert.AreEqual( was.Pace, now.Pace );

			if ( was.Guest is { } guest )
			{
				var read = now.Guest!.Value;

				Assert.AreEqual( guest with { PreviousRides = null, PreviousTemporaryRides = null },
					read with { PreviousRides = null, PreviousTemporaryRides = null }, $"guest {was.ThingId}" );
				CollectionAssert.AreEqual( guest.PreviousRides!.ToList(), read.PreviousRides!.ToList() );
			}
			else
				Assert.AreEqual( was.Staff, now.Staff, $"member {was.ThingId}" );
		}

		foreach ( var (was, now) in shipped.Sprites.Zip( written.Sprites ) )
			Assert.AreEqual( (was.Slot, was.Type, was.Bank, was.SpriteNumber, was.Frame, was.Script, was.Pc, was.Alpha, was.State),
				(now.Slot, now.Type, now.Bank, now.SpriteNumber, now.Frame, now.Script, now.Pc, now.Alpha, now.State) );

		for ( var model = 4; model <= 8; ++model )
			CollectionAssert.AreEqual( shipped.StaffList( model ).ToList(), written.StaffList( model ).ToList() );

		for ( var set = 0; set < shipped.MessageSets()!.Count; ++set )
			CollectionAssert.AreEqual( shipped.MessageSets()![set].ToList(), written.MessageSets()![set].ToList(), $"set {set}" );

		AssertTheChainsHold( written );
		Assert.AreEqual( shipped.Camera.Saved, written.Camera.Saved, "the modules after the sets still lie where they are looked for" );
		Assert.IsNull( written.ScriptStates.Problem );
	}

	/// <summary>
	/// A guest made and a member of staff hired are written whole, ahead of the file's things and newest first, each
	/// on the lowest free sprite slot, at the head of their cell's chain, in the sets their model belongs to, the hire
	/// at the head of their kind's list.
	/// </summary>
	[TestMethod]
	public void AGuestMadeAndAHireAreWrittenWholeAheadOfTheFilesThings()
	{
		var people = new ParkPeople( shipped );
		var guest = people.Admit( 47, 21 );
		var hire = people.Hire( new ParkStaffPool.Candidate( Id: 999, Kind: 4, Name: "Ada Test", Grade: 2, Costume: 0, Wage: 69 ), 47, 21 );
		var member = people.Staff.Single( staff => staff.ThingId == hire );

		Assert.AreEqual( Researcher, member.Model, "the candidate's kind is the researcher's" );
		Assert.IsTrue( guest > 42 && hire > guest );

		var written = Written( people, out var report );
		var free = Enumerable.Range( 1, 99 ).Where( slot => shipped.Sprites.All( sprite => sprite.Slot != slot ) ).Take( 2 ).ToArray();

		Assert.AreEqual( (18, 2, 0, 100, 20), (report.Kept, report.Made, report.Gone, report.SpriteSlots, report.LiveSprites) );
		CollectionAssert.AreEqual( new[] { hire, guest }.Concat( shipped.Things.Select( thing => thing.ThingId ) ).ToList(),
			written.Things.Select( thing => thing.ThingId ).ToList(), "the made first, the newest of them first" );
		Assert.AreEqual( Carried + 533 + 509 + (2 * 0x118) + (2 * 2) + 2, length,
			"a guest's record, a researcher's, two sprites, two ids in set 0xa and one in 0xc" );

		var madeGuest = written.People.Single( person => person.ThingId == guest );
		var madeHire = written.People.Single( person => person.ThingId == hire );
		var peep = people.Guests[guest];

		Assert.AreEqual( (ParkWorld.GuestModel, 47, 21, free[0]), (madeGuest.Model, madeGuest.CellX, madeGuest.CellY, madeGuest.SpriteSlot) );
		Assert.AreEqual( (Researcher, 47, 21, free[1]), (madeHire.Model, madeHire.CellX, madeHire.CellY, madeHire.SpriteSlot) );
		Assert.AreEqual( ((int)peep.State, peep.Cash, peep.ExitLevel, peep.Happiness, peep.Thirst, peep.Hunger, peep.Toilet, peep.PersonType, peep.VisitorNumber, peep.ArrivalDate),
			(madeGuest.Guest!.Value.State, madeGuest.Guest.Value.Cash, madeGuest.Guest.Value.ExitLevel, madeGuest.Guest.Value.Happiness,
			madeGuest.Guest.Value.Thirst, madeGuest.Guest.Value.Hunger, madeGuest.Guest.Value.Toilet, madeGuest.Guest.Value.PersonType,
			madeGuest.Guest.Value.ArrivalIndex, madeGuest.Guest.Value.ArrivalDate) );
		Assert.AreEqual( (ParkWorld.NavigatorState.DefaultMass, ParkWorld.NavigatorState.DefaultRadius, peep.Navigator.MaxSpeed, peep.Navigator.MaxForce),
			(madeGuest.Navigator.Mass, madeGuest.Navigator.Radius, madeGuest.Navigator.MaxSpeed, madeGuest.Navigator.MaxForce), "the navigator's constants" );
		Assert.AreEqual( (peep.Navigator.Position.X, peep.Navigator.Position.Y), (madeGuest.Navigator.X, madeGuest.Navigator.Y) );
		Assert.AreEqual( ("Ada Test", 2, member.Happiness, member.Tiredness, (int)member.Activity),
			(madeHire.Staff!.Value.Name, madeHire.Staff.Value.PayGrade, madeHire.Staff.Value.Happiness, madeHire.Staff.Value.Tiredness, madeHire.Staff.Value.State) );

		var picture = written.Sprites.Single( sprite => sprite.Slot == madeGuest.SpriteSlot );
		var script = people.SpriteFor( guest )!;

		Assert.AreEqual( (ParkSpriteBanks.ChildKind, peep.SpriteBank, script.SpriteNumber, script.Frame, script.Script, script.Pc, 255, 1),
			(picture.Type, picture.Bank, picture.SpriteNumber, picture.Frame, picture.Script, picture.Pc, picture.Alpha, picture.State),
			"the sprite as its constructor leaves one, on the guest's own program" );
		Assert.AreEqual( (475f, 215f), (picture.X, picture.Y), "ten world units to a cell, the cell's middle" );
		Assert.AreEqual( (madeGuest.SpriteKind, madeGuest.SpriteBank), (picture.Type, picture.Bank), "mESPSprite and mSpriteID are the sprite's" );

		// What no reader names, from the record's own bytes: the sprite as its constructor leaves it, and the base's
		// destination, previous place and the navigator's constants.
		var raw = written.SpriteRecordOf( madeGuest.SpriteSlot )!;
		var like = shipped.Sprites.Where( sprite => sprite.Type == picture.Type )
			.OrderByDescending( sprite => (sprite.Bank == picture.Bank && sprite.SpriteNumber == picture.SpriteNumber ? 4 : 0) + (sprite.Set == picture.Set ? 2 : 0) ).First();

		Assert.AreEqual( (madeGuest.SpriteSlot, 0x14, 0, script.Interval, 1f, 1f, BitConverter.ToInt32( shipped.SpriteRecordOf( like.Slot )!, 0xbc )),
			(BitConverter.ToInt32( raw, 4 ), BitConverter.ToInt32( raw, 0x1c ), BitConverter.ToInt32( raw, 0x7c ), BitConverter.ToInt32( raw, 0x80 ),
			BitConverter.ToSingle( raw, 0xa4 ), BitConverter.ToSingle( raw, 0xa8 ), BitConverter.ToInt32( raw, 0xbc )),
			"its slot, the timer, due at once, the interval, the scale and the set byte of the nearest sprite the file holds" );

		var record = written.RecordOf( guest )!;

		Assert.AreEqual( ((peep.Navigator.Target.X >> 8) & 0xffff, (peep.Navigator.Target.Y >> 8) & 0xffff, peep.Navigator.Previous.X, peep.Navigator.Previous.Y),
			((int)BitConverter.ToUInt16( record, 28 ), (int)BitConverter.ToUInt16( record, 30 ), BitConverter.ToInt32( record, 224 ), BitConverter.ToInt32( record, 228 )),
			"mAccurateDest is the target in mX's units; mPrevious the navigator's own" );
		Assert.AreEqual( (peep.NextAnimation, peep.NextInterval, peep.SetDestSuccessfully ? 1 : 0, peep.Thoughts.Last, 0, 0),
			(BitConverter.ToInt32( record, 20 ), BitConverter.ToInt32( record, 24 ), BitConverter.ToInt32( record, 238 ), BitConverter.ToInt32( record, 386 ),
			BitConverter.ToInt32( record, 390 ), BitConverter.ToInt32( record, 406 )) );
		Assert.IsTrue( record.AsSpan( 43, 32 ).ToArray().All( b => b == 0 ) && record.AsSpan( 254, 132 ).ToArray().All( b => b == 0 ),
			"the navigator's force, formation and axes and the event ring are nought, as the constructors leave them" );
		Assert.IsTrue( written.RecordOf( hire )!.AsSpan( 491, 8 ).ToArray().All( b => b == 0 ), "mTimeHired is not written" );

		var chain = written.ChainAt( 47, 21 );

		CollectionAssert.AreEqual( new[] { hire, guest }, chain.Take( 2 ).ToList(), "the newest heads the cell" );
		AssertTheChainsHold( written );

		CollectionAssert.AreEqual( new[] { hire }.Concat( shipped.StaffList( Researcher ) ).ToList(), written.StaffList( Researcher ).ToList(),
			"the hire heads the researchers' list and the file's follow" );
		CollectionAssert.AreEqual( shipped.StaffList( 5 ).ToList(), written.StaffList( 5 ).ToList(), "another kind's list is as it was" );

		var sets = written.MessageSets()!;
		var before = shipped.MessageSets()!;

		CollectionAssert.AreEqual( before[0xa].Concat( new[] { guest, hire } ).Order().ToList(), sets[0xa].ToList(), "everybody's set, in rising id" );
		CollectionAssert.AreEqual( before[0xc].Concat( new[] { hire } ).Order().ToList(), sets[0xc].ToList(), "the staff's" );
		CollectionAssert.AreEqual( before[0x1b].ToList(), sets[0x1b].ToList(), "a researcher is no guard" );
		CollectionAssert.AreEqual( before[0xb].ToList(), sets[0xb].ToList(), "the objects' set is the file's" );
	}

	/// <summary>A guard hired joins the guards' set too.</summary>
	[TestMethod]
	public void AGuardHiredJoinsTheGuardsSet()
	{
		var people = new ParkPeople( shipped );
		var hire = people.Hire( new ParkStaffPool.Candidate( Id: 999, Kind: 3, Name: "G", Grade: 0, Costume: 0, Wage: 1 ), 47, 21 );

		Assert.AreEqual( 7, people.Staff.Single( staff => staff.ThingId == hire ).Model );

		var written = Written( people, out _ );

		CollectionAssert.AreEqual( shipped.MessageSets()![0x1b].Concat( new[] { hire } ).Order().ToList(), written.MessageSets()![0x1b].ToList() );
		CollectionAssert.AreEqual( new[] { hire }.Concat( shipped.StaffList( 7 ) ).ToList(), written.StaffList( 7 ).ToList() );

		// The made sprite's +0xbc is the file's own guard's, which is not the one a sprite of no kin is left with.
		var mine = written.SpriteRecordOf( written.People.Single( person => person.ThingId == hire ).SpriteSlot )!;
		var theirs = shipped.SpriteRecordOf( shipped.People.Single( person => person.Model == 7 ).SpriteSlot )!;

		Assert.AreEqual( BitConverter.ToInt32( theirs, 0xbc ), BitConverter.ToInt32( mine, 0xbc ) );
		Assert.AreNotEqual( 1, BitConverter.ToInt32( mine, 0xbc ) );
	}

	/// <summary>A made person takes the lowest free slot, slot 1 when it is free, as the original's search does (<c>FUN_00475a10</c>).</summary>
	[TestMethod]
	public void AMadePersonTakesTheLowestFreeSlot()
	{
		var people = new ParkPeople( shipped );
		var first = shipped.People.Single( person => person.SpriteSlot == 1 );
		var guest = people.Admit( 47, 21 );

		// Whoever holds slot 1 left out of what the park hands over, as one gone.
		var body = ParkFileWriter.Body( shipped, Running( people ) with
		{
			People = [.. people.Written( Level.WrittenThings( shipped ).Contains ).Where( person => person.Person.ThingId != first.ThingId )]
		} );
		var written = new ParkWorld( body );

		Assert.IsNull( written.Problem );
		Assert.AreEqual( 1, written.People.Single( person => person.ThingId == guest ).SpriteSlot );
		Assert.IsFalse( written.StaffList( first.Model ).Contains( first.ThingId ), "and the one gone is off their kind's list" );
	}

	/// <summary>On a cell a thing stands on, a person is chained ahead of it; and one come onto a cell stands ahead
	/// of whoever the file had there, who is there still.</summary>
	[TestMethod]
	public void APersonStandsAheadOfAThingAndANewcomerAheadOfWhoeverStayed()
	{
		var people = new ParkPeople( shipped );
		var thing = shipped.Objects.First( placed => placed.IsPlaced && shipped.ChainAt( placed.CellX, placed.CellY ).SequenceEqual( [placed.ThingId] ) );
		var mover = people.Guests.Values.First();
		var stayer = shipped.People.Last( person => person.Model == ParkWorld.GuestModel && person.ThingId != mover.ThingId );
		var one = ParkWorld.NavigatorState.One;

		mover.Navigator.Position = new FixedVector( (thing.CellX * one) + 7, (thing.CellY * one) + 9 );

		var made = people.Admit( stayer.CellX, stayer.CellY );
		var written = Written( people, out _ );

		CollectionAssert.AreEqual( new[] { mover.ThingId, thing.ThingId }, written.ChainAt( thing.CellX, thing.CellY ).ToList(), "the person, then the thing" );

		var chain = written.ChainAt( stayer.CellX, stayer.CellY ).ToList();

		Assert.AreEqual( made, chain[0], "the newcomer heads the cell" );
		CollectionAssert.AreEqual( shipped.ChainAt( stayer.CellX, stayer.CellY ).Where( id => id != mover.ThingId ).ToList(), chain.Skip( 1 ).ToList(),
			"and whoever stayed follows in the file's order" );
		AssertTheChainsHold( written );
	}

	/// <summary>
	/// A balloon's and a bubble's sprite the file holds are let go and the slots that named them written nought: a
	/// file made here, given a sprite nobody stands on and two guests naming it as their balloon and their bubble.
	/// </summary>
	[TestMethod]
	public void TheFilesBalloonAndBubbleSpritesAreLetGo()
	{
		var first = new ParkPeople( shipped );
		var made = first.Admit( 47, 21 );
		var body = ParkFileWriter.Body( shipped, Running( first ) );
		var held = new ParkWorld( body );
		var slot = held.People.Single( person => person.ThingId == made ).SpriteSlot;
		var holder = held.People.First( person => person.Model == ParkWorld.GuestModel && person.ThingId != made );
		var thinker = held.People.Last( person => person.Model == ParkWorld.GuestModel && person.ThingId != made );

		int At( int thing ) => body.AsSpan().IndexOf( held.RecordOf( thing ) );

		BitConverter.TryWriteBytes( body.AsSpan( At( made ) + 16 ), 0 );
		BitConverter.TryWriteBytes( body.AsSpan( At( holder.ThingId ) + 406 ), slot );
		BitConverter.TryWriteBytes( body.AsSpan( At( thinker.ThingId ) + 390 ), slot );
		TestRun.DeleteEvery<ParkPeople>();

		var doctored = new ParkWorld( body );

		Assert.AreEqual( (0, slot, 19), (doctored.People.Single( person => person.ThingId == made ).SpriteSlot,
			doctored.People.Single( person => person.ThingId == holder.ThingId ).Guest!.Value.BalloonScript, doctored.Sprites.Count) );

		var people = new ParkPeople( doctored );
		var again = new ParkWorld( ParkFileWriter.Body( doctored, new ParkFileWriter.Running( doctored.GameTick, false, 0, 0, doctored.Camera.Saved!.Value,
			People: people.Written( Level.WrittenThings( doctored ).Contains ) ), out var report ) );

		Assert.IsNull( again.Problem );
		Assert.AreEqual( 19, report!.Value.LiveSprites, "the orphan is let go and the guest it was taken from drawn on one slot" );
		Assert.AreEqual( 0, BitConverter.ToInt32( again.RecordOf( holder.ThingId )!, 406 ), "mBalloonScript" );
		Assert.AreEqual( 0, BitConverter.ToInt32( again.RecordOf( thinker.ThingId )!, 390 ), "mThoughtScript" );
		Assert.AreEqual( 19, again.People.Select( person => person.SpriteSlot ).Where( s => s != 0 ).Distinct().Count() );
	}

	/// <summary>A guest gone home is left out: their record, their sprite, their place in their cell's chain and in
	/// everybody's set; whoever stood behind them in the chain stands at its head.</summary>
	[TestMethod]
	public void AGuestGoneIsLeftOut()
	{
		var people = new ParkPeople( shipped );
		var gone = shipped.People.First( person => person.Model == ParkWorld.GuestModel );
		var behind = shipped.ChainAt( gone.CellX, gone.CellY ).Where( id => id != gone.ThingId ).ToList();

		Assert.IsTrue( people.Depart( gone.ThingId ) );

		var written = Written( people, out var report );

		Assert.AreEqual( (17, 0, 1, 17), (report.Kept, report.Made, report.Gone, report.LiveSprites) );
		Assert.IsFalse( written.Things.Any( thing => thing.ThingId == gone.ThingId ) );
		Assert.AreEqual( Carried - 533 - 0x118 - 2, length );
		Assert.IsFalse( written.Sprites.Any( sprite => sprite.Slot == gone.SpriteSlot ), "their slot is empty" );
		Assert.IsFalse( written.MessageSets()![0xa].Contains( gone.ThingId ) );
		CollectionAssert.AreEqual( behind, written.ChainAt( gone.CellX, gone.CellY ).ToList() );
		AssertTheChainsHold( written );
	}

	/// <summary>A person who has moved is written on the cell they stand on now, at its chain's head, and off the one they left.</summary>
	[TestMethod]
	public void APersonWhoHasMovedIsChainedWhereTheyStand()
	{
		var people = new ParkPeople( shipped );
		var peep = people.Guests.Values.First();
		var was = shipped.People.Single( person => person.ThingId == peep.ThingId );
		var one = ParkWorld.NavigatorState.One;

		peep.Navigator.Position = new FixedVector( (60 * one) + 100, (70 * one) + 200 );

		var written = Written( people, out _ );
		var now = written.People.Single( person => person.ThingId == peep.ThingId );

		Assert.AreEqual( (60, 70, ((60 * one) + 100) >> 8, ((70 * one) + 200) >> 8), (now.CellX, now.CellY, now.RawX, now.RawY) );
		Assert.AreEqual( peep.ThingId, written.ChainAt( 60, 70 ).First() );
		Assert.IsFalse( written.ChainAt( was.CellX, was.CellY ).Contains( peep.ThingId ) );
		AssertTheChainsHold( written );

		var picture = written.Sprites.Single( sprite => sprite.Slot == now.SpriteSlot );

		Assert.AreEqual( (600f + (1000f / one), 700f + (2000f / one)), (picture.X, picture.Y), "the sprite stands where the navigator does" );
	}

	/// <summary>
	/// A guest on a thing is written deciding where they stand, with no queue links, and counted: the thing's own
	/// record is still the file's.
	/// </summary>
	[TestMethod]
	public void AQueuerIsWrittenDecidingAndCounted()
	{
		var people = new ParkPeople( shipped );
		var peep = people.Guests.Values.First();

		peep.SetState( PeepState.InQueue, 800, new Random( 1 ) );
		peep.MajorDest = shipped.Objects[0].ThingId;
		peep.QueuePos = 3;

		var written = Written( people, out _ );
		var guest = written.People.Single( person => person.ThingId == peep.ThingId ).Guest!.Value;

		Assert.AreEqual( ((int)PeepState.Deciding, (int)PeepState.Deciding, 0, 0, 0, 0), (guest.State, guest.SavedState, guest.MajorDest, guest.QueuePos, guest.QNext, guest.QPrev) );
		Assert.AreEqual( 1, Unimplemented.Summary.Single( gap => gap.What == "SAVE_PARK_GUEST_ON_A_THING" ).Times );
		Assert.IsTrue( written.People.Single( person => person.ThingId == peep.ThingId ).Navigator.PathFinished, "standing, on no route" );
	}

	/// <summary>A handle to a thing the file does not hold is written as nought and counted, and a guest bound for it decides.</summary>
	[TestMethod]
	public void AHandleToAThingNotWrittenIsNoughtAndCounted()
	{
		var people = new ParkPeople( shipped );
		var peep = people.Guests.Values.First();
		var other = people.Guests.Values.Last();
		var held = shipped.Objects[0].ThingId;

		peep.SetState( PeepState.GoingToRide, 800, new Random( 1 ) );
		peep.MajorDest = 500;
		other.SetState( PeepState.GoingToRide, 800, new Random( 1 ) );
		other.MajorDest = held;

		var written = Written( people, out _ );
		var guest = written.People.Single( person => person.ThingId == peep.ThingId ).Guest!.Value;
		var bound = written.People.Single( person => person.ThingId == other.ThingId ).Guest!.Value;

		Assert.AreEqual( ((int)PeepState.Deciding, 0), (guest.State, guest.MajorDest) );
		Assert.AreEqual( ((int)PeepState.GoingToRide, held), (bound.State, bound.MajorDest), "one bound for a thing of the file's is written as they are" );
		Assert.AreEqual( 1, Unimplemented.Summary.Single( gap => gap.What == "SAVE_PARK_HANDLE_TO_AN_UNWRITTEN_THING" ).Times );
	}

	/// <summary>
	/// A walker is written on their route: the navigator's counts, distances, target and the waypoints it holds, each
	/// leg beside the waypoint it starts from; a person who has walked no new route keeps the file's waypoints.
	/// </summary>
	[TestMethod]
	public void AWalkersRouteIsWritten()
	{
		var people = new ParkPeople( shipped );
		var peep = people.Guests.Values.First();
		var idle = people.Guests.Values.Last();
		var blocked = CellEdge.For( shipped, ParkPeople.WalkingMode ).Blocked;
		var path = Enumerable.Range( 0, ParkWorld.MapSize * ParkWorld.MapSize )
			.Select( at => (X: at % ParkWorld.MapSize, Y: at / ParkWorld.MapSize) )
			.Where( cell => shipped.CellAt( cell.X, cell.Y ).Type == 1 ).ToList();

		peep.Navigator.Position = new FixedVector( PeepNavigator.WaypointCentre( path[0].X ), PeepNavigator.WaypointCentre( path[0].Y ) );

		var far = path.Last( cell => peep.Navigator.NavigateTo(
			new FixedVector( PeepNavigator.WaypointCentre( cell.X ), PeepNavigator.WaypointCentre( cell.Y ) ), blocked, addCurrent: false )
			&& peep.Navigator.Waypoints.Count >= 3 );

		Assert.IsTrue( peep.Navigator.NavigateTo(
			new FixedVector( PeepNavigator.WaypointCentre( far.X ), PeepNavigator.WaypointCentre( far.Y ) ), blocked, addCurrent: false ) );

		var navigator = peep.Navigator;
		var written = Written( people, out _ );
		var read = written.People.Single( person => person.ThingId == peep.ThingId ).Navigator;
		var subpath = written.SubpathOf( peep.ThingId );

		Assert.AreEqual( (navigator.Cursor, navigator.TotalWaypoints, navigator.BufferedWaypoints, navigator.BufferedDistance, navigator.TailDistance,
			navigator.TotalDistance, navigator.Target.X, navigator.Target.Y, navigator.Finished),
			(read.PathCount, read.PathTotalCount, read.PathBufferCount, read.BufferedDistance, read.TailDistance, read.TotalDistance,
			read.TargetX, read.TargetY, read.PathFinished) );
		Assert.IsTrue( navigator.Waypoints.Count >= 3 && navigator.LegLengths.Count == navigator.Waypoints.Count - 1 );

		for ( var i = 0; i < navigator.Waypoints.Count; ++i )
			Assert.AreEqual( (navigator.Waypoints[i].X, navigator.Waypoints[i].Y), (subpath[i].X, subpath[i].Y), $"waypoint {i}" );

		for ( var i = 0; i < navigator.LegLengths.Count; ++i )
			Assert.AreEqual( navigator.LegLengths[i], subpath[i].Leg, $"leg {i}" );

		Assert.AreEqual( 0, idle.Navigator.Waypoints.Count, "nobody has routed the other guest since the load" );
		CollectionAssert.AreEqual( shipped.SubpathOf( idle.ThingId ).ToList(), written.SubpathOf( idle.ThingId ).ToList(), "the file's waypoints are left" );
	}

	/// <summary>A member of staff in the hand is written idle and standing: the original puts the hand's thing down before it writes.</summary>
	[TestMethod]
	public void AMemberInTheHandIsWrittenIdle()
	{
		var people = new ParkPeople( shipped );
		var member = people.Staff.First( staff => staff.Activity == StaffActivity.Walking );

		Assert.IsTrue( people.PickUp( member.ThingId ) );
		Assert.AreEqual( StaffActivity.Held, member.Activity );

		var written = Written( people, out var report );
		var read = written.People.Single( person => person.ThingId == member.ThingId );

		Assert.AreEqual( ((int)StaffActivity.Idle, true, 0, 0), (read.Staff!.Value.State, read.Navigator.PathFinished, read.Navigator.VelocityX, read.Navigator.PathBufferCount) );
		Assert.AreEqual( 18, report.LiveSprites, "and drawn" );
	}

	/// <summary>A member resting inside a rest area is written with no sprite, as the files hold one; a member bound
	/// for a rest area the file does not hold is written idle.</summary>
	[TestMethod]
	public void ARestingMemberHasNoSpriteAndOneBoundForAnUnwrittenRestAreaIsIdle()
	{
		var people = new ParkPeople( shipped );
		var resting = people.Staff[0];
		var bound = people.Staff[1];
		var room = shipped.Objects.First( thing => thing.IsRestArea ).ThingId;

		resting.RestArea = room;
		resting.SetActivity( StaffActivity.Resting, 800 );
		bound.RestArea = 500;
		bound.SetActivity( StaffActivity.GoingToRest, 800 );

		var written = Written( people, out var report );
		var rested = written.People.Single( person => person.ThingId == resting.ThingId );
		var stood = written.People.Single( person => person.ThingId == bound.ThingId );

		Assert.AreEqual( ((int)StaffActivity.Resting, room, 0), (rested.Staff!.Value.State, rested.Staff.Value.RestArea, rested.SpriteSlot) );
		Assert.AreEqual( ((int)StaffActivity.Idle, 0), (stood.Staff!.Value.State, stood.Staff.Value.RestArea) );
		Assert.AreNotEqual( 0, stood.SpriteSlot );
		Assert.AreEqual( 17, report.LiveSprites );
	}

	/// <summary>No balloon and no thought bubble is written: each is counted, the guest's slots are nought and the
	/// file's sprites for them are let go.</summary>
	[TestMethod]
	public void ABalloonIsCountedAndNotWritten()
	{
		var people = new ParkPeople( shipped );
		var peep = people.Guests.Values.First();

		peep.Balloon = Balloon.Make( peep.ThingId, 4, now: 0 );
		peep.BalloonLife = 40;

		var written = Written( people, out var report );
		var guest = written.People.Single( person => person.ThingId == peep.ThingId ).Guest!.Value;

		Assert.AreEqual( (0, 40, 18), (guest.BalloonScript, guest.RemainingBalloonLife, report.LiveSprites) );
		Assert.AreEqual( 1, Unimplemented.Summary.Single( gap => gap.What == "SAVE_PARK_BALLOON" ).Times );
	}

	/// <summary>When every slot is taken the table grows by fifty, as the original's does.</summary>
	[TestMethod]
	public void AFullSpriteTableGrowsByFifty()
	{
		var people = new ParkPeople( shipped );

		for ( var i = 0; i < 82; ++i )
			people.Admit( 47, 21 );

		var written = Written( people, out var report );

		Assert.AreEqual( (150, 100), (report.SpriteSlots, report.LiveSprites), "99 slots hold 99 sprites; the hundredth person takes slot 100" );
		Assert.AreEqual( 100, written.Sprites.Max( sprite => sprite.Slot ) );
		Assert.AreEqual( 100, written.People.Select( person => person.SpriteSlot ).Distinct().Count(), "each on a slot of their own" );
		Assert.AreEqual( 82, written.ChainAt( 47, 21 ).Count( id => id > 42 ) );
	}

	/// <summary>The written park is one OpenTPW's own people are built from again: the same people, each where they stood.</summary>
	[TestMethod]
	public void TheWrittenPeopleLoadAgain()
	{
		var people = new ParkPeople( shipped );
		var guest = people.Admit( 47, 21 );

		var again = new ParkPeople( Written( people, out _ ) );

		Assert.AreEqual( 14, again.Guests.Count );
		Assert.AreEqual( people.Guests[guest].Navigator.Position, again.Guests[guest].Navigator.Position );
		Assert.AreEqual( (people.Guests[guest].VisitorNumber, people.Guests[guest].Cash), (again.Guests[guest].VisitorNumber, again.Guests[guest].Cash) );
		Assert.IsNotNull( again.SpriteFor( guest ), "on the sprite the file gives them" );
	}

	/// <summary>The writer refuses people it cannot write: an id twice, one past the ids a file can name, one off the map.</summary>
	[TestMethod]
	public void PeopleThatCannotBeWrittenAreRefused()
	{
		var people = new ParkPeople( shipped ).Written( _ => true );
		var first = people[0];

		ParkFileWriter.Running With( params ParkWorld.WrittenPerson[] extra ) => new( shipped.GameTick, false, 0, 0,
			shipped.Camera.Saved!.Value, People: [.. people, .. extra] );

		Assert.ThrowsException<InvalidOperationException>( () => ParkFileWriter.Body( shipped, With( first ) ), "twice" );
		Assert.ThrowsException<InvalidOperationException>( () => ParkFileWriter.Body( shipped,
			With( first with { Person = first.Person with { ThingId = ParkWorld.HighestThingId + 1 } } ) ), "past the last id" );
		Assert.ThrowsException<InvalidOperationException>( () => ParkFileWriter.Body( shipped,
			With( first with { Person = first.Person with { ThingId = 900, RawX = 128 << 8 } } ) ), "off the map" );
		Assert.ThrowsException<InvalidOperationException>( () => ParkFileWriter.Body( shipped,
			With( first with { Person = first.Person with { ThingId = shipped.Objects[0].ThingId } } ) ), "an object's id" );
	}
}
