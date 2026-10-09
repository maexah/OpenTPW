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
		// Seeded: a hire put down thinks "very happy" on one draw in sixteen, and that bubble is a sprite of the file's.
		var people = new ParkPeople( shipped, staffRandom: new Random( 1 ) );
		var guest = people.Admit( 47, 21 );
		var hire = people.Hire( new ParkStaffPool.Candidate( Id: 999, Kind: 4, Name: "Ada Test", Grade: 2, Costume: 0, Wage: 69 ), 47, 21 );
		var member = people.Staff.Single( staff => staff.ThingId == hire );

		Assert.AreEqual( Researcher, member.Model, "the candidate's kind is the researcher's" );
		Assert.IsTrue( guest > 42 && hire > guest );

		var written = Written( people, out var report );
		var free = Enumerable.Range( 1, 99 ).Where( slot => shipped.Sprites.All( sprite => sprite.Slot != slot ) ).Take( 2 ).ToArray();

		Assert.AreEqual( 0, report.Bubbles, "this seed's hire has no thought, or the counts below prove nothing" );
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
		Assert.AreEqual( ParkWorld.StaffState.FileTimeOf( people.State.CalendarNow ), BitConverter.ToInt64( written.RecordOf( hire )!, 491 ),
			"mTimeHired is the park's calendar as the hire was made" );

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
	/// A guest on a thing is written as they are: their state, their thing, their place and their links in its
	/// queue, on the route and the sprite they have.
	/// </summary>
	[TestMethod]
	public void AQueuerIsWrittenAsTheyAre()
	{
		var people = new ParkPeople( shipped );
		var peep = people.Guests.Values.First();
		var behind = people.Guests.Values.Last();
		var thing = shipped.Objects.First( placed => placed.IsVisitable ).ThingId;

		foreach ( var queuer in new[] { peep, behind } )
		{
			queuer.SetState( PeepState.InQueue, 800, new Random( 1 ) );
			queuer.MajorDest = thing;
			people.State.JoinQueue( thing, queuer.ThingId );
		}

		peep.QueuePos = 3;
		peep.BeenAdmitted = true;
		peep.QueueMoveDelay = 4;

		var written = Written( people, out _ );
		var guest = written.People.Single( person => person.ThingId == peep.ThingId ).Guest!.Value;
		var second = written.People.Single( person => person.ThingId == behind.ThingId ).Guest!.Value;

		Assert.AreEqual( ((int)PeepState.InQueue, thing, 3, behind.ThingId, 0, 1, 4),
			(guest.State, guest.MajorDest, guest.QueuePos, guest.QNext, guest.QPrev, guest.BeenAdmitted, guest.QueueMoveDelay) );
		Assert.AreEqual( ((int)PeepState.InQueue, thing, 0, peep.ThingId), (second.State, second.MajorDest, second.QNext, second.QPrev) );
		Assert.IsFalse( Unimplemented.Summary.Any( gap => gap.What == "SAVE_PARK_GUEST_ON_A_THING" ) );
		Assert.AreNotEqual( 0, written.People.Single( person => person.ThingId == peep.ThingId ).SpriteSlot, "a queuer keeps their sprite" );
	}

	/// <summary>
	/// A rider keeps their sprite on a thing that keeps its riders' (<c>mFlags</c> <c>0x20</c>) and is written with
	/// none on a thing that does not, as admission leaves them; and a file with two such people reads back.
	/// </summary>
	[TestMethod]
	public void ARiderIsWrittenWithTheSpriteTheirThingKeeps()
	{
		var people = new ParkPeople( shipped );
		var guests = people.Guests.Values.Take( 3 ).ToList();
		var keeps = shipped.Objects.First( placed => (placed.Flags & ParkWorld.CatalogueObject.KeepsRidersSpriteFlag) != 0 ).ThingId;
		var keepsNone = shipped.Objects.First( placed => placed.IsPlaced && (placed.Flags & ParkWorld.CatalogueObject.KeepsRidersSpriteFlag) == 0 ).ThingId;

		foreach ( var (guest, thing) in guests.Zip( new[] { keeps, keepsNone, keepsNone } ) )
		{
			guest.SetState( PeepState.Riding, 800, new Random( 1 ) );
			guest.MajorDest = thing;
		}

		var written = Written( people, out var report );

		int Slot( Peep guest ) => written.People.Single( person => person.ThingId == guest.ThingId ).SpriteSlot;

		Assert.AreNotEqual( 0, Slot( guests[0] ) );
		Assert.AreEqual( (0, 0), (Slot( guests[1] ), Slot( guests[2] )) );
		Assert.AreEqual( 16, report.LiveSprites, "eighteen people, two with no sprite" );

		foreach ( var guest in guests )
			Assert.AreEqual( (int)PeepState.Riding, written.People.Single( person => person.ThingId == guest.ThingId ).Guest!.Value.State );

		// Slot nought is nobody's: the park's sprites are taken over two people who hold it.
		Assert.AreEqual( 16, ParkGuestSprites.Taken( written ).Count );
	}

	/// <summary>A guest on a thing the file does not hold is written deciding where they stand, with no queue links, and counted.</summary>
	[TestMethod]
	public void AGuestOnAThingNotWrittenDecidesAndIsCounted()
	{
		var people = new ParkPeople( shipped );
		var peep = people.Guests.Values.First();

		peep.SetState( PeepState.InQueue, 800, new Random( 1 ) );
		peep.MajorDest = 500;
		peep.QueuePos = 3;

		var written = Written( people, out _ );
		var guest = written.People.Single( person => person.ThingId == peep.ThingId ).Guest!.Value;

		Assert.AreEqual( ((int)PeepState.Deciding, (int)PeepState.Deciding, 0, 0, 0, 0), (guest.State, guest.SavedState, guest.MajorDest, guest.QueuePos, guest.QNext, guest.QPrev) );
		Assert.AreEqual( 1, Unimplemented.Summary.Single( gap => gap.What == "SAVE_PARK_GUEST_ON_A_THING" ).Times );
		Assert.AreEqual( 1, Unimplemented.Summary.Single( gap => gap.What == "SAVE_PARK_HANDLE_TO_AN_UNWRITTEN_THING" ).Times );
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

	/// <summary>The game's own sprite bank by its kind and its number among the kind's, as the drawing packs them.</summary>
	private static SpriteBankFile? Bank( int kind, int bank )
	{
		if ( ParkGuestSprites.FolderFor( kind, "Jungle" ) is not { } folder )
			return null;

		var files = ParkGuestSprites.BanksIn( FileSystem, folder, kind );

		if ( bank < 0 || bank >= files.Length )
			return null;

		using var stream = FileSystem.OpenRead( files[bank] );

		return new SpriteBankFile( stream );
	}

	private int[] FreeSlots( ParkWorld park, int count ) =>
		[.. Enumerable.Range( 1, 99 ).Where( slot => park.Sprites.All( sprite => sprite.Slot != slot ) ).Take( count )];

	private static int SetByte( ParkWorld park, int slot ) => BitConverter.ToInt32( park.SpriteRecordOf( slot )!, 0xbc );

	/// <summary>
	/// A held balloon is written as a sprite of its own on the lowest free slot, which the guest's
	/// <c>mBalloonScript</c> names: kind 10, bank 0, the script, the set and the frame it is on, where it was last
	/// placed, and its own set's frames a direction at <c>+0xbc</c>. Nobody else's slot is set and nothing is counted.
	/// </summary>
	[TestMethod]
	public void AHeldBalloonIsWrittenAsItsOwnSprite()
	{
		var people = new ParkPeople( shipped ) { BankAt = Bank };
		var peep = people.Guests.Values.First();
		var balloon = Balloon.Make( peep.ThingId, 4, now: 0 )!;

		(balloon.X, balloon.Height, balloon.Y) = (431.5f, 1.125f, 222.25f);
		balloon.Sprite.Interval = 0x4a;
		balloon.Sprite.Step( 1000 );
		Assert.AreEqual( Balloon.HeldScript + 2, balloon.Sprite.Pc, "its script has shown its frame, or the program counter below proves nothing" );
		peep.Balloon = balloon;
		peep.BalloonLife = 40;

		var written = Written( people, out var report );
		var slot = FreeSlots( shipped, 1 )[0];
		var colour = Balloon.ColourFor( peep.ThingId, 4 );

		foreach ( var person in written.People.Where( person => person.Guest != null ) )
			Assert.AreEqual( person.ThingId == peep.ThingId ? slot : 0, person.Guest!.Value.BalloonScript, $"mBalloonScript of {person.ThingId}" );

		Assert.AreEqual( (40, 19, 1, 0, 0), (written.People.Single( person => person.ThingId == peep.ThingId ).Guest!.Value.RemainingBalloonLife,
			report.LiveSprites, report.Balloons, report.Bubbles, report.UnmatchedSpriteSets) );
		Assert.AreEqual( Carried + 0x118, length, "one sprite more" );

		var sprite = written.Sprites.Single( one => one.Slot == slot );

		Assert.AreEqual( (Balloon.SpriteKind, 0, colour, 0, Balloon.HeldScript, Balloon.HeldScript + 2, 255),
			(sprite.Type, sprite.Bank, sprite.SpriteNumber, sprite.Frame, sprite.Script, sprite.Pc, sprite.Alpha) );
		Assert.AreEqual( (431.5f, 1.125f, 222.25f), (sprite.X, sprite.Height, sprite.Y), "where it was last placed" );

		var raw = written.SpriteRecordOf( slot )!;

		Assert.AreEqual( (slot, 0x4a, 1f, Bank( Balloon.SpriteKind, 0 )!.Sets[colour].FramesPerDirection, 2),
			(BitConverter.ToInt32( raw, 4 ), BitConverter.ToInt32( raw, 0x80 ), BitConverter.ToSingle( raw, 0xa4 ), SetByte( written, slot ), SetByte( written, slot )),
			"its slot, its interval, the scale, and two frames a direction: whole and burst" );
		Assert.IsFalse( Unimplemented.Summary.Any( gap => gap.What.StartsWith( "SAVE_PARK_BALLOON" ) || gap.What == "SAVE_PARK_THOUGHT_BUBBLE" ) );

		// The written park's balloon is the guest's again on a load.
		TestRun.DeleteEvery<ParkPeople>();

		var again = new ParkPeople( written ).Guests[peep.ThingId].Balloon;

		Assert.IsNotNull( again );
		Assert.AreEqual( (colour, 431.5f, 222.25f), (again.Sprite.Set, again.X, again.Y) );
	}

	/// <summary>
	/// A balloon the file holds for a guest who holds it still keeps its slot and is written over where it lies; one
	/// whose guest holds none now is let go, and its slot is the next sprite's to take.
	/// </summary>
	[TestMethod]
	public void ABalloonTheFileHoldsKeepsItsSlotAndOneGoneIsLetGo()
	{
		var first = new ParkPeople( shipped ) { BankAt = Bank };
		var keeper = first.Guests.Values.First();
		var loser = first.Guests.Values.Last();

		keeper.Balloon = Balloon.Make( keeper.ThingId, 4, now: 0 );
		loser.Balloon = Balloon.Make( loser.ThingId, 4, now: 0 );

		var held = Written( first, out _ );
		var slots = FreeSlots( shipped, 2 );
		var order = new[] { keeper.ThingId, loser.ThingId }.Order().ToArray();
		int Named( ParkWorld park, int thing ) => park.People.Single( person => person.ThingId == thing ).Guest!.Value.BalloonScript;

		Assert.AreEqual( (slots[0], slots[1]), (Named( held, order[0] ), Named( held, order[1] )), "the lowest free slots, the lower id first" );
		TestRun.DeleteEvery<ParkPeople>();

		var people = new ParkPeople( held ) { BankAt = Bank };

		Assert.IsNotNull( people.Guests[keeper.ThingId].Balloon );
		people.Guests[keeper.ThingId].Balloon!.X = 99.5f;
		people.Guests[loser.ThingId].Balloon = null;

		var again = new ParkWorld( ParkFileWriter.Body( held, new ParkFileWriter.Running( held.GameTick, false, 0, 0, held.Camera.Saved!.Value,
			People: people.Written( Level.WrittenThings( held ).Contains ) ), out var report ) );

		Assert.IsNull( again.Problem );
		Assert.AreEqual( (Named( held, keeper.ThingId ), 0, 19, 1), (Named( again, keeper.ThingId ), Named( again, loser.ThingId ), report!.Value.LiveSprites, report.Value.Balloons) );
		Assert.AreEqual( 99.5f, again.Sprites.Single( sprite => sprite.Slot == Named( again, keeper.ThingId ) ).X );
		Assert.AreEqual( 1, again.Sprites.Count( sprite => sprite.Type == Balloon.SpriteKind ) );
		Assert.IsNull( again.SpriteRecordOf( Named( held, loser.ThingId ) ), "the slot is empty" );
	}

	/// <summary>
	/// A thought bubble is written as a sprite of its own on the slot the person's <c>mThoughtScript</c> names, a
	/// guest's and a member of staff's alike: kind 9, bank 0, its picture, the picture's script four words in, over
	/// the person at 2.5, and bank 0's set 0's frames a direction at <c>+0xbc</c> whatever the picture.
	/// </summary>
	[TestMethod]
	public void AThoughtBubbleIsWrittenAsItsOwnSprite()
	{
		var people = new ParkPeople( shipped ) { BankAt = Bank };
		var peep = people.Guests.Values.First();
		var member = people.Staff[0];

		Assert.IsTrue( peep.Thoughts.Set( Thoughts.Confused, shipped.GameTick ) && member.Thoughts.Set( 0x14, shipped.GameTick ) );
		Assert.AreEqual( ((0, 15), (1, 2)), (peep.Thoughts.Bubble!.Value, member.Thoughts.Bubble!.Value), "a blue question mark and a pink sleeper of the second bank" );

		var written = Written( people, out var report );
		var slots = FreeSlots( shipped, 2 );
		var lower = peep.ThingId < member.ThingId;

		Assert.AreEqual( (19 + 1, 0, 2, 0), (report.LiveSprites, report.Balloons, report.Bubbles, report.UnmatchedSpriteSets) );

		// The scripts' own words: 0x0074f2f8 sets picture 15, and 0x0074f358, the twentieth, picture 18.
		foreach ( var (thing, slot, picture, script, position) in new[]
		{
			(peep.ThingId, slots[lower ? 0 : 1], 15, 1552, peep.Navigator.Position),
			(member.ThingId, slots[lower ? 1 : 0], 18, 1576, member.Navigator.Position)
		} )
		{
			var record = written.RecordOf( thing )!;

			Assert.AreEqual( (slot, shipped.GameTick), (BitConverter.ToInt32( record, 390 ), BitConverter.ToInt32( record, 394 )), $"mThoughtScript and mTimeBubbleShown of {thing}" );

			var sprite = written.Sprites.Single( one => one.Slot == slot );

			Assert.AreEqual( (Thoughts.SpriteKind, 0, picture, 0, script, script + 4, 255),
				(sprite.Type, sprite.Bank, sprite.SpriteNumber, sprite.Frame, sprite.Script, sprite.Pc, sprite.Alpha), $"the bubble of {thing}" );
			Assert.AreEqual( (position.X * 10f / FixedVector.One, 2.5f, position.Y * 10f / FixedVector.One), (sprite.X, sprite.Height, sprite.Y) );
			Assert.AreEqual( (1, Bank( Thoughts.SpriteKind, 0 )!.Sets[0].FramesPerDirection), (SetByte( written, slot ), SetByte( written, slot )) );
		}

		Assert.AreEqual( 0, written.People.Single( person => person.ThingId == people.Guests.Values.Last().ThingId ).Guest!.Value.ThoughtScript );
		Assert.IsFalse( Unimplemented.Summary.Any( gap => gap.What == "SAVE_PARK_THOUGHT_BUBBLE" ) );
	}

	/// <summary>
	/// The 22 thought scripts are six words apart from word 1462 (<c>0x0074f190</c>) and set these pictures, read
	/// from the executable's own words: 0 to 15 in order, then 21, then 16 to 20. The played saves' bubbles pair the
	/// same way (script 1558 with picture 21, 1564 with 16, 1576 with 18).
	/// </summary>
	[TestMethod]
	public void AThoughtsScriptIsTheOneThatSetsItsPicture()
	{
		int[] sets = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 21, 16, 17, 18, 19, 20];

		for ( var script = 0; script < sets.Length; ++script )
			Assert.AreEqual( 1462 + (6 * script), Thoughts.ScriptOf( sets[script] >> 4, sets[script] & 0xf ), $"picture {sets[script]}" );

		for ( var thought = 1; thought <= 22; ++thought )
		{
			var (bank, set, _) = Thoughts.PictureOf( thought );

			Assert.IsTrue( Thoughts.ScriptOf( bank, set ) is >= 1462 and <= 1462 + (6 * 21) );
		}
	}

	/// <summary>
	/// A bubble the file holds for a person who shows one still keeps its slot and takes the new picture; and a slot
	/// a guest's <c>mBalloonScript</c> names that holds no balloon is not written over: the balloon goes on a slot of
	/// its own.
	/// </summary>
	[TestMethod]
	public void AKeptBubbleKeepsItsSlotAndASlotOfAnotherKindIsNotWrittenOver()
	{
		var first = new ParkPeople( shipped ) { BankAt = Bank };
		var thinker = first.Guests.Values.First();
		var holder = first.Guests.Values.Last();

		Assert.IsTrue( thinker.Thoughts.Set( Thoughts.Confused, shipped.GameTick ) );

		var body = ParkFileWriter.Body( shipped, Running( first ) );
		var held = new ParkWorld( body );
		var slot = BitConverter.ToInt32( held.RecordOf( thinker.ThingId )!, 390 );
		var own = held.People.Single( person => person.ThingId == thinker.ThingId ).SpriteSlot;

		Assert.AreEqual( FreeSlots( shipped, 1 )[0], slot );

		// The holder's record is made to name the thinker's own sprite as its balloon.
		BitConverter.TryWriteBytes( body.AsSpan( body.AsSpan().IndexOf( held.RecordOf( holder.ThingId ) ) + 406 ), own );
		TestRun.DeleteEvery<ParkPeople>();

		var doctored = new ParkWorld( body );
		var people = new ParkPeople( doctored ) { BankAt = Bank };

		Assert.IsNull( people.Guests[holder.ThingId].Balloon, "a slot that holds no balloon gives none at a load" );
		people.Guests[holder.ThingId].Balloon = Balloon.Make( holder.ThingId, 4, now: 0 );
		Assert.IsTrue( people.Guests[thinker.ThingId].Thoughts.Set( 1, doctored.GameTick + 100 ) );

		var again = new ParkWorld( ParkFileWriter.Body( doctored, new ParkFileWriter.Running( doctored.GameTick, false, 0, 0, doctored.Camera.Saved!.Value,
			People: people.Written( Level.WrittenThings( doctored ).Contains ) ), out var report ) );

		Assert.IsNull( again.Problem );
		Assert.AreEqual( (slot, 8, 1, 1), (BitConverter.ToInt32( again.RecordOf( thinker.ThingId )!, 390 ),
			again.Sprites.Single( sprite => sprite.Slot == slot ).SpriteNumber, report!.Value.Bubbles, report.Value.Balloons), "the same slot, thought 1's picture" );

		var balloon = again.People.Single( person => person.ThingId == holder.ThingId ).Guest!.Value.BalloonScript;

		Assert.IsTrue( balloon != own && balloon != slot && balloon != 0 );
		Assert.AreEqual( (2, 2), (BitConverter.ToInt32( doctored.SpriteRecordOf( own )!, 0x18 ), BitConverter.ToInt32( again.SpriteRecordOf( own )!, 0x18 )),
			"and is the file's record still, not one made anew" );
		Assert.AreEqual( (ParkSpriteBanks.ChildKind, Balloon.SpriteKind),
			(again.Sprites.Single( sprite => sprite.Slot == own ).Type, again.Sprites.Single( sprite => sprite.Slot == balloon ).Type), "the thinker's own sprite is theirs still" );
	}

	/// <summary>
	/// A bubble the file holds over somebody who shows none now is let go: its slot is empty, the record names none
	/// and the table holds no bubble. A load here makes no bubble again, so the loaded park's people show none.
	/// </summary>
	[TestMethod]
	public void ABubbleTheFileHoldsIsLetGoWhenItsPersonShowsNone()
	{
		var first = new ParkPeople( shipped ) { BankAt = Bank };
		var thinker = first.Guests.Values.First();

		Assert.IsTrue( thinker.Thoughts.Set( Thoughts.Confused, shipped.GameTick ) );

		var held = Written( first, out var before );
		var slot = BitConverter.ToInt32( held.RecordOf( thinker.ThingId )!, 390 );

		Assert.AreEqual( (1, 19, Thoughts.SpriteKind), (before.Bubbles, before.LiveSprites, held.Sprites.Single( sprite => sprite.Slot == slot ).Type) );
		TestRun.DeleteEvery<ParkPeople>();

		var people = new ParkPeople( held ) { BankAt = Bank };

		Assert.IsNull( people.Guests[thinker.ThingId].Thoughts.Bubble );

		var again = new ParkWorld( ParkFileWriter.Body( held, new ParkFileWriter.Running( held.GameTick, false, 0, 0, held.Camera.Saved!.Value,
			People: people.Written( Level.WrittenThings( held ).Contains ) ), out var report ) );

		Assert.IsNull( again.Problem );
		Assert.AreEqual( (0, 18, 0), (report!.Value.Bubbles, report.Value.LiveSprites, BitConverter.ToInt32( again.RecordOf( thinker.ThingId )!, 390 )) );
		Assert.IsNull( again.SpriteRecordOf( slot ), "the slot is empty" );
		Assert.IsFalse( again.Sprites.Any( sprite => sprite.Type == Thoughts.SpriteKind ) );
	}

	/// <summary>A balloon let go and still bursting is nobody's, is not written, and is counted.</summary>
	[TestMethod]
	public void ABurstingBalloonIsCountedAndNotWritten()
	{
		var people = new ParkPeople( shipped ) { BankAt = Bank };
		var bursting = (List<Balloon>)typeof( ParkPeople ).GetField( "_bursting", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance )!.GetValue( people )!;

		bursting.Add( Balloon.Make( 20, 4, now: 0 )! );

		Written( people, out var report );

		Assert.AreEqual( (18, 0), (report.LiveSprites, report.Balloons) );
		Assert.AreEqual( 1, Unimplemented.Summary.Single( gap => gap.What == "SAVE_PARK_BALLOON_LET_GO" ).Times );
	}

	/// <summary>
	/// A made sprite's <c>+0xbc</c> is the frames a direction of the set it is made on, which for a person is their
	/// bank's set 0: nothing is left to copy and nothing unmatched. A kept sprite keeps the file's until a state's
	/// animation starts on it, which writes its own set's.
	/// </summary>
	[TestMethod]
	public void ASpritesSetByteIsItsBanksAndAStateAnimationsAfterOne()
	{
		var people = new ParkPeople( shipped ) { BankAt = Bank };
		var guest = people.Admit( 47, 21 );
		var hire = people.Hire( new ParkStaffPool.Candidate( Id: 999, Kind: 4, Name: "Ada Test", Grade: 2, Costume: 0, Wage: 69 ), 47, 21 );
		var entertainer = shipped.People.Single( person => person.Model == 6 );
		var bank = Bank( entertainer.SpriteKind, entertainer.SpriteBank )!;
		var group = bank.StateGroups[0];

		// The made guest walks: the set at +0xb4 is the walk's, eight frames a direction, and +0xbc is still set 0's.
		var walking = people.SpriteFor( guest )!;

		walking.Start( SpriteScript.Walking );
		walking.Step( 1_000_000 );
		Assert.AreEqual( 8, Bank( ParkSpriteBanks.ChildKind, people.Guests[guest].SpriteBank )!.Sets[walking.Set].FramesPerDirection,
			"the made guest is on a set of another count, or the next assertions prove nothing" );

		var written = Written( people, out var report );
		int Of( ParkWorld park, int thing ) => SetByte( park, park.People.Single( person => person.ThingId == thing ).SpriteSlot );

		Assert.AreEqual( 0, report.UnmatchedSpriteSets );
		Assert.AreEqual( (Bank( ParkSpriteBanks.ChildKind, people.Guests[guest].SpriteBank )!.Sets[0].FramesPerDirection, 1), (Of( written, guest ), Of( written, guest )), "a child stands on one frame a direction" );
		Assert.AreEqual( (Bank( 8, 0 )!.Sets[0].FramesPerDirection, 4), (Of( written, hire ), Of( written, hire )), "a researcher on four" );
		Assert.AreEqual( Of( shipped, entertainer.ThingId ), Of( written, entertainer.ThingId ), "a kept sprite keeps the file's" );

		var performing = bank.Sets[group.Set - 1].FramesPerDirection;

		Assert.AreNotEqual( performing, Of( shipped, entertainer.ThingId ), "the group's set has another count, or this proves nothing" );
		Assert.IsTrue( people.SpriteFor( entertainer.ThingId )!.StartState( group, performing ) );
		Assert.IsTrue( people.SpriteFor( hire )!.StartState( group, 6 ) );

		var after = Written( people, out _ );

		Assert.AreEqual( performing, Of( after, entertainer.ThingId ), "a state's animation leaves its own set's" );
		Assert.AreEqual( 6, Of( after, hire ), "over a made sprite's too" );
	}

	/// <summary>
	/// The rule the made sprites are written by holds for every sprite of the shipped park: each person's
	/// <c>+0xbc</c> is the frames a direction of their bank's set 0.
	/// </summary>
	[TestMethod]
	public void EveryShippedSpritesSetByteIsItsBanksSetNoughts()
	{
		Assert.AreEqual( 18, shipped.Sprites.Count );

		foreach ( var sprite in shipped.Sprites )
			Assert.AreEqual( Bank( sprite.Type, sprite.Bank )!.Sets[0].FramesPerDirection, SetByte( shipped, sprite.Slot ), $"slot {sprite.Slot}, kind {sprite.Type} bank {sprite.Bank}" );
	}

	/// <summary>
	/// <c>mTimeHired</c> is read as a <c>FILETIME</c>: the shipped four were made on tick 15 and the researcher on 648,
	/// at 3,750 seconds of the calendar a tick. A hire is stamped with the park's calendar, the file's keep theirs, and
	/// a load reads both back.
	/// </summary>
	[TestMethod]
	public void TimeHiredIsTheParksCalendarAsAMemberIsMade()
	{
		var start = new DateTime( 2000, 1, 1 );

		CollectionAssert.AreEquivalent(
			new[] { start.AddSeconds( 15 * 3750 ), start.AddSeconds( 15 * 3750 ), start.AddSeconds( 15 * 3750 ), start.AddSeconds( 15 * 3750 ), start.AddSeconds( 648 * 3750 ) },
			shipped.People.Where( person => person.Staff != null ).Select( person => ParkWorld.StaffState.HiredWhenOf( person.Staff!.Value.TimeHired )!.Value ).ToList() );
		Assert.AreEqual( 0x1bf546e1d0a1900, ParkWorld.StaffState.FileTimeOf( start.AddSeconds( 15 * 3750 ) ) );
		Assert.IsNull( ParkWorld.StaffState.HiredWhenOf( -1 ) );

		var people = new ParkPeople( shipped );
		var hire = people.Hire( new ParkStaffPool.Candidate( Id: 999, Kind: 4, Name: "Ada Test", Grade: 2, Costume: 0, Wage: 69 ), 47, 21 );
		var stamp = ParkWorld.StaffState.FileTimeOf( start.AddSeconds( shipped.GameTick * 3750L ) );

		Assert.AreEqual( stamp, people.Staff.Single( member => member.ThingId == hire ).TimeHired );

		var written = Written( people, out _ );

		foreach ( var person in written.People.Where( person => person.Staff != null ) )
		{
			Assert.AreEqual( person.ThingId == hire ? stamp : shipped.People.Single( one => one.ThingId == person.ThingId ).Staff!.Value.TimeHired,
				person.Staff!.Value.TimeHired, $"mTimeHired of {person.ThingId}" );
		}

		TestRun.DeleteEvery<ParkPeople>();
		Assert.AreEqual( stamp, new ParkPeople( written ).Staff.Single( member => member.ThingId == hire ).TimeHired, "and a load reads it back" );
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
