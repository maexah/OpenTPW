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
	/// A load shows the bubble a file holds again, a guest's and a member of staff's, each with its thought and its
	/// time, so it is taken away on the sweep it would have been. Once its person shows none it is let go: its slot
	/// is empty, the record names none and the table holds no bubble.
	/// </summary>
	[TestMethod]
	public void ABubbleTheFileHoldsShowsAgainAfterALoadAndIsLetGoWhenItsPersonShowsNone()
	{
		var first = new ParkPeople( shipped ) { BankAt = Bank };
		var thinker = first.Guests.Values.First();
		var member = first.Staff.First( staff => staff.Model == 8 );

		Assert.IsTrue( thinker.Thoughts.Set( Thoughts.Confused, shipped.GameTick ) );
		Assert.IsTrue( member.Thoughts.Set( 0x14, shipped.GameTick - 3 ) );

		var held = Written( first, out var before );
		var slot = BitConverter.ToInt32( held.RecordOf( thinker.ThingId )!, 390 );
		var staffSlot = BitConverter.ToInt32( held.RecordOf( member.ThingId )!, 390 );

		Assert.AreEqual( (2, 20, Thoughts.SpriteKind, Thoughts.SpriteKind), (before.Bubbles, before.LiveSprites,
			held.Sprites.Single( sprite => sprite.Slot == slot ).Type, held.Sprites.Single( sprite => sprite.Slot == staffSlot ).Type) );
		TestRun.DeleteEvery<ParkPeople>();

		var people = new ParkPeople( held ) { BankAt = Bank };
		var guest = people.Guests[thinker.ThingId].Thoughts;
		var staff = people.Staff.Single( other => other.ThingId == member.ThingId ).Thoughts;

		Assert.AreEqual( (thinker.Thoughts.Bubble, Thoughts.Confused, shipped.GameTick), (guest.Bubble, guest.Last, guest.TimeBubbleShown), "the guest's" );
		Assert.AreEqual( (member.Thoughts.Bubble, 0x14, shipped.GameTick - 3), (staff.Bubble, staff.Last, staff.TimeBubbleShown), "the member's" );
		Assert.IsNotNull( guest.Bubble );
		Assert.IsNotNull( staff.Bubble );
		Assert.IsFalse( Unimplemented.Summary.Any( gap => gap.What == "SAVED_THOUGHT_BUBBLE" ) );

		// The member's is twelve sweeps old three sweeps before the guest's.
		staff.Expire( shipped.GameTick - 3 + Thoughts.BubbleSweeps );
		Assert.IsNotNull( staff.Bubble, "twelve sweeps on it stands" );
		staff.Expire( shipped.GameTick - 3 + Thoughts.BubbleSweeps + 1 );
		guest.Expire( shipped.GameTick - 3 + Thoughts.BubbleSweeps + 1 );
		Assert.IsNull( staff.Bubble, "thirteen on it is gone" );
		Assert.IsNotNull( guest.Bubble );
		guest.Expire( shipped.GameTick + Thoughts.BubbleSweeps + 1 );
		Assert.IsNull( guest.Bubble );

		var again = new ParkWorld( ParkFileWriter.Body( held, new ParkFileWriter.Running( held.GameTick, false, 0, 0, held.Camera.Saved!.Value,
			People: people.Written( Level.WrittenThings( held ).Contains ) ), out var report ) );

		Assert.IsNull( again.Problem );
		Assert.AreEqual( (0, 18, 0), (report!.Value.Bubbles, report.Value.LiveSprites, BitConverter.ToInt32( again.RecordOf( thinker.ThingId )!, 390 )) );
		Assert.IsNull( again.SpriteRecordOf( slot ), "the slot is empty" );
		Assert.IsFalse( again.Sprites.Any( sprite => sprite.Type == Thoughts.SpriteKind ) );
	}

	/// <summary>
	/// A bubble's picture is its script's, so one saved just made, its sprite's <c>+0xb4</c> still nought, shows the
	/// picture it was made for; a slot that holds no bubble shows none. Each of the 22 scripts gives back the
	/// picture it is made for, and a word that starts none gives nothing.
	/// </summary>
	[TestMethod]
	public void ASavedBubblesPictureIsItsScripts()
	{
		for ( var thought = 1; thought <= 22; ++thought )
		{
			var (bank, set, _) = Thoughts.PictureOf( thought );

			Assert.AreEqual( (bank, set), Thoughts.PictureOfScript( Thoughts.ScriptOf( bank, set ) ), $"thought {thought}" );
		}

		foreach ( var word in new[] { 1461, 1463, 1456, 1462 - (17 * 6), 1462 + (22 * 6), 0, -6, 1650 } )
			Assert.IsNull( Thoughts.PictureOfScript( word ), $"word {word}" );

		var first = new ParkPeople( shipped ) { BankAt = Bank };
		var thinker = first.Guests.Values.First();

		var member = first.Staff.First( staff => staff.Model == 7 );

		Assert.IsTrue( thinker.Thoughts.Set( 0x13, shipped.GameTick ) );
		Assert.IsTrue( member.Thoughts.Set( 0x14, shipped.GameTick ) );

		var body = ParkFileWriter.Body( shipped, Running( first ) );
		var held = new ParkWorld( body );
		var slot = BitConverter.ToInt32( held.RecordOf( thinker.ThingId )!, 390 );
		var at = body.AsSpan().IndexOf( held.SpriteRecordOf( slot )! );

		Assert.IsTrue( at > 0 );

		var script = BitConverter.ToInt32( body, at + 0x0c );

		// As the constructor leaves it: at its script's first word, the picture not yet set.
		BitConverter.GetBytes( script ).CopyTo( body, at + 0x08 );
		BitConverter.GetBytes( 0 ).CopyTo( body, at + 0xb4 );
		TestRun.DeleteEvery<ParkPeople>();

		var justMade = new ParkPeople( new ParkWorld( body ) ).Guests[thinker.ThingId].Thoughts;

		Assert.AreEqual( thinker.Thoughts.Bubble, justMade.Bubble );
		Assert.AreNotEqual( (0, 0), justMade.Bubble!.Value );
		TestRun.DeleteEvery<ParkPeople>();

		// The slot holding another kind of sprite is nobody's bubble, a guest's or a member of staff's.
		var staffAt = body.AsSpan().IndexOf( held.SpriteRecordOf( BitConverter.ToInt32( held.RecordOf( member.ThingId )!, 390 ) )! );

		Assert.IsTrue( staffAt > 0 );
		BitConverter.GetBytes( Balloon.SpriteKind ).CopyTo( body, at + 0xac );

		var guestOnly = new ParkPeople( new ParkWorld( body ) );

		Assert.IsNull( guestOnly.Guests[thinker.ThingId].Thoughts.Bubble );
		Assert.AreEqual( member.Thoughts.Bubble, guestOnly.Staff.Single( staff => staff.ThingId == member.ThingId ).Thoughts.Bubble, "the member's stands" );
		TestRun.DeleteEvery<ParkPeople>();
		BitConverter.GetBytes( ParkSpriteBanks.ChildKind ).CopyTo( body, staffAt + 0xac );

		Assert.IsNull( new ParkPeople( new ParkWorld( body ) ).Staff.Single( staff => staff.ThingId == member.ThingId ).Thoughts.Bubble );
	}

	/// <summary>A member of staff's thought and its time are read from the person base, as a guest's are.</summary>
	[TestMethod]
	public void AMemberOfStaffsThoughtIsRead()
	{
		var guard = new ParkPeople( shipped ).Staff.Single( staff => staff.Model == 7 );

		Assert.AreEqual( (18, 180), (guard.Thoughts.Last, guard.Thoughts.TimeBubbleShown), "the shipped guard's" );
		Assert.IsNull( guard.Thoughts.Bubble );

		// So a thought of class 1 makes no bubble until twenty sweeps past the save's.
		Assert.IsFalse( guard.Thoughts.Set( 1, 199 ) );
		Assert.IsTrue( guard.Thoughts.Set( 1, 200 ) );
	}

	private static List<Balloon> BurstingOf( ParkPeople people )
		=> (List<Balloon>)typeof( ParkPeople ).GetField( "_bursting", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance )!.GetValue( people )!;

	private ParkWorld WrittenWithLetGo( ParkWorld from, ParkPeople people, out ParkWorld.PeopleWritten report )
	{
		var written = new ParkWorld( ParkFileWriter.Body( from, new ParkFileWriter.Running( from.GameTick, false, 0, 0, from.Camera.Saved!.Value,
			People: people.Written( Level.WrittenThings( from ).Contains ), LetGo: people.WrittenLetGo() ), out var done ) );

		Assert.IsNull( written.Problem );
		Assert.IsTrue( written.ClosedOnTrailer && written.ClosedOnSpriteTrailer );
		report = done!.Value;

		return written;
	}

	/// <summary>
	/// A balloon let go and still bursting is written on a slot nobody names: on the let-go script past its frame,
	/// with its alpha, inside the fade's loop (the stack's room 19, the loop's start in its last place, a count of
	/// one), state 2 and shown. A load takes it up where it was and it bursts on for the turns its alpha has left;
	/// written again once it is freed, its slot is let go.
	/// </summary>
	[TestMethod]
	public void ABalloonLetGoIsWrittenInsideItsLoopAndBurstsOnAfterALoad()
	{
		var people = new ParkPeople( shipped ) { BankAt = Bank };
		var balloon = Balloon.Make( 20, 4, now: 0 )!;

		(balloon.X, balloon.Y, balloon.Height) = (431.5f, 262.25f, 1.75f);
		balloon.LetGo();

		// Four shown turns: 250, 230, 210 and 190 behind it, 190 the alpha it rests on.
		for ( var turn = 1; turn <= 4; ++turn )
			Assert.IsTrue( balloon.Sprite.Step( turn * 1000 ) );

		Assert.AreEqual( (1657, 190, 1), (balloon.Sprite.Pc, balloon.Sprite.Alpha, balloon.Sprite.Frame) );
		BurstingOf( people ).Add( balloon );

		var written = WrittenWithLetGo( shipped, people, out var report );

		Assert.AreEqual( (19, 0, 1), (report.LiveSprites, report.Balloons, report.LetGo) );
		Assert.IsFalse( Unimplemented.Summary.Any( gap => gap.What == "SAVE_PARK_BALLOON_LET_GO" ) );

		var sprite = written.Sprites.Single( other => other.Type == Balloon.SpriteKind );
		var record = written.SpriteRecordOf( sprite.Slot )!;

		Assert.AreEqual( 11, sprite.Slot, "the lowest slot free: the shipped eighteen hold 1 to 19 but 11" );
		Assert.AreEqual( (SpriteScript.LetGoBalloonEntry, 1657, 190, 1, balloon.Sprite.SpriteNumber, 431.5f, 262.25f, 1.75f),
			(sprite.Script, sprite.Pc, sprite.Alpha, sprite.Frame, sprite.SpriteNumber, sprite.X, sprite.Y, sprite.Height) );
		Assert.AreEqual( (2, 19, 1655, 1, 1, 2), (sprite.State, BitConverter.ToInt32( record, 0x1c ), BitConverter.ToInt32( record, 0x6c ),
			BitConverter.ToInt32( record, 0x78 ), BitConverter.ToInt32( record, 0x114 ), BitConverter.ToInt32( record, 0xbc )) );
		Assert.IsTrue( Enumerable.Range( 0, 19 ).All( place => BitConverter.ToInt32( record, 0x20 + (place * 4) ) == 0 ), "the stack's other places are nought" );
		CollectionAssert.AreEqual( new[] { 1655 }, written.SpriteLoopsOf( sprite.Slot ).ToArray() );
		Assert.IsFalse( written.People.Any( person => person.Guest?.BalloonScript == sprite.Slot ), "nobody names it" );
		TestRun.DeleteEvery<ParkPeople>();

		var loaded = new ParkPeople( written ) { BankAt = Bank };
		var again = loaded.Bursting.Single();

		Assert.AreEqual( (1657, 190, 431.5f, 262.25f, 1.75f, false), (again.Sprite.Pc, again.Sprite.Alpha, again.X, again.Y, again.Height, again.Sprite.Ended) );
		CollectionAssert.AreEqual( new[] { 1655 }, again.Sprite.Loops.ToArray() );
		Assert.IsFalse( loaded.Guests.Values.Any( guest => guest.Balloon != null ), "and nobody holds it" );

		// 170 down to 10 is nine more shown turns, the tenth ends it, the eleventh frees it.
		var turns = 0;

		while ( !again.Sprite.Freed && turns < 40 )
		{
			again.Sprite.Step( ++turns * 1000 );
			Assert.AreEqual( turns <= 9, again.Sprite.Shown, $"turn {turns}" );
		}

		Assert.AreEqual( 11, turns );

		// Written while it waits to be freed it would still be a sprite; freed, it is none, and the file's is let go.
		var after = WrittenWithLetGo( written, loaded, out var last );

		Assert.AreEqual( (18, 0), (last.LiveSprites, last.LetGo) );
		Assert.IsFalse( after.Sprites.Any( other => other.Type == Balloon.SpriteKind ) );
		Assert.IsNull( after.SpriteRecordOf( 11 ) );
	}

	/// <summary>
	/// A balloon just let go is written at its script's first word with an empty stack, and one at its end word in
	/// state 4 and hidden, which a load frees on its first due turn. Handed no list, the writer leaves the file's
	/// own let-go balloons where they lie; a held balloon is never taken for one.
	/// </summary>
	[TestMethod]
	public void ABalloonJustLetGoAndOneEndedAreWrittenAsTheyStand()
	{
		var people = new ParkPeople( shipped ) { BankAt = Bank };
		var holder = people.Guests.Values.First();
		var fresh = Balloon.Make( 20, 4, now: 0 )!;
		var ended = Balloon.Make( 21, 4, now: 0 )!;

		holder.Balloon = Balloon.Make( holder.ThingId, 4, now: 0 );
		fresh.LetGo();
		ended.LetGo();

		for ( var turn = 1; turn <= 14; ++turn )
			ended.Sprite.Step( turn * 1000 );

		Assert.IsTrue( ended.Sprite.Ended && !ended.Sprite.Freed );
		BurstingOf( people ).AddRange( [fresh, ended] );

		var written = WrittenWithLetGo( shipped, people, out var report );

		Assert.AreEqual( (21, 1, 2), (report.LiveSprites, report.Balloons, report.LetGo) );

		var held = written.People.Single( person => person.ThingId == holder.ThingId ).Guest!.Value.BalloonScript;
		var loose = written.Sprites.Where( sprite => sprite.Type == Balloon.SpriteKind && sprite.Slot != held ).OrderBy( sprite => sprite.Slot ).ToArray();

		Assert.AreEqual( (11, 20, 21), (held, loose[0].Slot, loose[1].Slot), "the held balloon first, on the one slot free under 20, then those let go" );
		Assert.AreEqual( (1666, 1666, 2, 20, 0, 1), (loose[0].Script, loose[0].Pc, loose[0].State, BitConverter.ToInt32( written.SpriteRecordOf( 20 )!, 0x1c ),
			BitConverter.ToInt32( written.SpriteRecordOf( 20 )!, 0x78 ), BitConverter.ToInt32( written.SpriteRecordOf( 20 )!, 0x114 )), "just let go" );
		Assert.AreEqual( (1666, 4, 20, 0, 0), (loose[1].Script, loose[1].State, BitConverter.ToInt32( written.SpriteRecordOf( 21 )!, 0x1c ),
			BitConverter.ToInt32( written.SpriteRecordOf( 21 )!, 0x78 ), BitConverter.ToInt32( written.SpriteRecordOf( 21 )!, 0x114 )), "ended" );
		TestRun.DeleteEvery<ParkPeople>();

		var loaded = new ParkPeople( written ) { BankAt = Bank };

		Assert.AreEqual( 2, loaded.Bursting.Count );
		Assert.IsNotNull( loaded.Guests[holder.ThingId].Balloon, "the held one is its guest's" );
		Assert.AreEqual( (false, true), (loaded.Bursting[0].Sprite.Ended, loaded.Bursting[1].Sprite.Ended) );
		loaded.Bursting[1].Sprite.Step( 1000 );
		Assert.IsTrue( loaded.Bursting[1].Sprite.Freed, "freed on its first due turn" );

		// With no list the file's own are left: the same three balloons, the freed one too.
		var left = new ParkWorld( ParkFileWriter.Body( written, new ParkFileWriter.Running( written.GameTick, false, 0, 0, written.Camera.Saved!.Value,
			People: loaded.Written( Level.WrittenThings( written ).Contains ) ), out var kept ) );

		Assert.AreEqual( (21, 0), (kept!.Value.LiveSprites, kept.Value.LetGo) );
		Assert.AreEqual( 3, left.Sprites.Count( sprite => sprite.Type == Balloon.SpriteKind ) );

		// With one, the freed is gone and the other is written again.
		var again = WrittenWithLetGo( written, loaded, out var last );

		Assert.AreEqual( (20, 1, 1), (last.LiveSprites, last.Balloons, last.LetGo) );
		Assert.AreEqual( 2, again.Sprites.Count( sprite => sprite.Type == Balloon.SpriteKind ) );
	}

	/// <summary>A sprite inside more loops than a record's stack holds is refused.</summary>
	[TestMethod]
	public void ASpriteInsideMoreLoopsThanARecordHoldsIsRefused()
	{
		var people = new ParkPeople( shipped ) { BankAt = Bank };
		var letGo = new ParkWorld.WrittenSprite( new ParkWorld.Sprite( 0, Balloon.SpriteKind, 0, 0, 0f, 0f, 0f, 0, 1, 250, 0, 1666, 1657 ),
			Loops: [.. Enumerable.Repeat( 1655, 21 )] );

		Assert.ThrowsException<InvalidOperationException>( () => ParkFileWriter.Body( shipped, Running( people ) with { LetGo = [letGo] } ) );

		var full = new ParkWorld( ParkFileWriter.Body( shipped, Running( people ) with { LetGo = [letGo with { Loops = [.. Enumerable.Range( 1, 20 )] }] } ) );
		var slot = full.Sprites.Single( sprite => sprite.Type == Balloon.SpriteKind ).Slot;

		CollectionAssert.AreEqual( Enumerable.Range( 1, 20 ).ToArray(), full.SpriteLoopsOf( slot ).ToArray(), "twenty fill it, the oldest in its last place" );
		Assert.AreEqual( (0, 20, 1), (BitConverter.ToInt32( full.SpriteRecordOf( slot )!, 0x1c ), BitConverter.ToInt32( full.SpriteRecordOf( slot )!, 0x20 ),
			BitConverter.ToInt32( full.SpriteRecordOf( slot )!, 0x6c )) );
		Assert.AreEqual( 0, full.SpriteLoopsOf( 99 ).Count, "an empty slot has none" );

		// A sprite takes its loops up oldest first and gives them back so.
		CollectionAssert.AreEqual( new[] { 7, 8, 9 }, new SpriteScript( 1666, 1657, 0, 1, loops: [7, 8, 9] ).Loops.ToArray() );

		// A record whose room is no count of twenty gives no loops.
		var body = ParkFileWriter.Body( shipped, Running( people ) with { LetGo = [letGo with { Loops = [1655] }] } );
		var at = body.AsSpan().IndexOf( new ParkWorld( body ).SpriteRecordOf( slot )! );

		foreach ( var room in new[] { 21, -1 } )
		{
			BitConverter.GetBytes( room ).CopyTo( body, at + 0x1c );
			Assert.AreEqual( 0, new ParkWorld( body ).SpriteLoopsOf( slot ).Count, $"room {room}" );
		}
	}

	/// <summary>
	/// A person's own sprite inside a state script's loop is written with its stack - room 19, word 1730 in the
	/// first place, a count of one, state 2 and shown - a kept sprite's and a made one's, and a load goes round the
	/// loop from the frame it was on. One put on another program since is written with its stack empty.
	/// </summary>
	[TestMethod]
	public void APersonsSpriteInsideALoopIsWrittenWithItsStackAndALoadGoesOnRoundIt()
	{
		var people = new ParkPeople( shipped ) { BankAt = Bank };
		var hire = people.Hire( new ParkStaffPool.Candidate( Id: 999, Kind: 4, Name: "Ada Test", Grade: 2, Costume: 0, Wage: 69 ), 47, 21 );
		var entertainer = shipped.People.Single( person => person.Model == 6 ).ThingId;
		var bank = Bank( shipped.People.Single( person => person.Model == 6 ).SpriteKind, shipped.People.Single( person => person.Model == 6 ).SpriteBank )!;
		var group = bank.StateGroups[0];
		var frames = bank.Sets[group.Set - 1].FramesPerDirection;

		Assert.AreEqual( 8, frames );

		(int Room, int First, int Count, int State, int Shown, int Pc, int Frame) Of( ParkWorld park, int thing )
		{
			var record = park.SpriteRecordOf( park.People.Single( person => person.ThingId == thing ).SpriteSlot )!;

			return (BitConverter.ToInt32( record, 0x1c ), BitConverter.ToInt32( record, 0x6c ), BitConverter.ToInt32( record, 0x78 ),
				BitConverter.ToInt32( record, 0x18 ), BitConverter.ToInt32( record, 0x114 ), BitConverter.ToInt32( record, 0x08 ), BitConverter.ToInt32( record, 0xb8 ));
		}

		Assert.AreEqual( (20, 0, 0), (Of( shipped, entertainer ).Room, Of( shipped, entertainer ).First, Of( shipped, entertainer ).Count), "the file's own is inside none" );

		// The kept one shows frames 0 to 5 and the made one 0 to 2.
		var now = 0;

		foreach ( var (thing, turns) in new[] { (entertainer, 6), (hire, 3) } )
		{
			var sprite = people.SpriteFor( thing )!;

			Assert.IsTrue( sprite.StartState( group, frames ) );

			for ( var turn = 0; turn < turns; ++turn )
				Assert.IsTrue( sprite.Step( now += 1000 ) );

			CollectionAssert.AreEqual( new[] { 1730 }, sprite.Loops.ToArray() );
		}

		var written = Written( people, out _ );

		Assert.AreEqual( (19, 1730, 1, 2, 1, 1732, 5), Of( written, entertainer ), "a kept sprite" );
		Assert.AreEqual( (19, 1730, 1, 2, 1, 1732, 2), Of( written, hire ), "a made sprite" );

		// Loaded, each goes on from its frame, round the set and round again, and nothing is counted.
		TestRun.DeleteEvery<ParkPeople>();

		var loaded = new ParkPeople( written ) { BankAt = Bank };

		foreach ( var (thing, from) in new[] { (entertainer, 5), (hire, 2) } )
		{
			var sprite = loaded.SpriteFor( thing )!;

			CollectionAssert.AreEqual( new[] { 1730 }, sprite.Loops.ToArray() );
			Assert.AreEqual( (frames, from), (sprite.FramesPerDirection, sprite.Frame), "the frames a direction are the record's, with no bank asked" );
			Assert.AreEqual( (sprite.Interval, false), (sprite.Due, sprite.Step( sprite.Interval )), "first due an interval past the load, and not on it" );

			var seen = new List<int>();

			for ( var turn = 0; turn < 12; ++turn )
			{
				Assert.IsTrue( sprite.Step( 1000 * (turn + 1) ) );
				seen.Add( sprite.Frame );
			}

			CollectionAssert.AreEqual( Enumerable.Range( from + 1, 12 ).Select( frame => frame % frames ).ToList(), seen, $"thing {thing}" );
			CollectionAssert.AreEqual( new[] { 1730 }, sprite.Loops.ToArray(), "still inside the one loop" );
		}

		Assert.AreEqual( 0, Unimplemented.Summary.Count( gap => gap.What == "SAVED_SPRITE_LOOP_STACK" ) );

		// Put on the standing program, the stack is empty again: the room and the count, the word left as it lies.
		Assert.IsTrue( loaded.SpriteFor( entertainer )!.Start( SpriteScript.Standing ) );

		var after = new ParkWorld( ParkFileWriter.Body( written, new ParkFileWriter.Running( written.GameTick, false, 0, 0, written.Camera.Saved!.Value,
			People: loaded.Written( Level.WrittenThings( written ).Contains ) ) ) );

		Assert.AreEqual( (20, 1730, 0), (Of( after, entertainer ).Room, Of( after, entertainer ).First, Of( after, entertainer ).Count) );
		Assert.AreEqual( (19, 1730, 1), (Of( after, hire ).Room, Of( after, hire ).First, Of( after, hire ).Count), "the other is in its loop still" );
		Assert.AreEqual( 0, new ParkPeople( after ).SpriteFor( entertainer )!.Loops.Count );
	}

	/// <summary>
	/// A person's sprite is written with its drawing flags, the running sprite's local 16 at <c>+0xc4</c>: nought on
	/// one made and not yet run, <c>0x1200</c> once its program has run its first word, and a kept one's as the load
	/// read them, through a new start and through being written standing on a fresh program.
	/// </summary>
	[TestMethod]
	public void APersonsSpriteIsWrittenWithItsDrawingFlagsAndALoadReadsThem()
	{
		static int Flags( ParkWorld park, int thing )
			=> BitConverter.ToInt32( park.SpriteRecordOf( park.People.Single( person => person.ThingId == thing ).SpriteSlot )!, 0xc4 );

		foreach ( var person in shipped.People )
		{
			Assert.AreEqual( 0x1200, Flags( shipped, person.ThingId ), $"the file's thing {person.ThingId}" );
			Assert.AreEqual( 0x1200, shipped.SpriteFlagsOf( person.SpriteSlot ) );
		}

		Assert.AreEqual( 0, shipped.SpriteFlagsOf( 99 ), "an empty slot" );

		// The file with three kept sprites' flags changed, so that what a load reads is told from what a program writes.
		var held = shipped.People.First( person => person.Model == 6 ).ThingId;
		var walker = shipped.People.First( person => person.Guest != null ).ThingId;
		var other = shipped.People.Last( person => person.Guest != null ).ThingId;
		var body = ParkFileWriter.Body( shipped, Running( new ParkPeople( shipped ) { BankAt = Bank } ) );

		TestRun.DeleteEvery<ParkPeople>();

		foreach ( var (thing, value) in new[] { (held, 0x1234), (walker, 0x4321), (other, 0x2468) } )
		{
			var record = new ParkWorld( body ).SpriteRecordOf( shipped.People.Single( person => person.ThingId == thing ).SpriteSlot )!;

			BitConverter.GetBytes( value ).CopyTo( body, body.AsSpan().IndexOf( record ) + 0xc4 );
		}

		var park = new ParkWorld( body );
		var people = new ParkPeople( park ) { BankAt = Bank };

		Assert.AreEqual( (0x1234, 0x4321, 0x2468), (people.SpriteFor( held )!.DrawFlags, people.SpriteFor( walker )!.DrawFlags, people.SpriteFor( other )!.DrawFlags), "a load reads them" );

		// One made: nought as the constructor leaves it, and the program's own once it has run a turn.
		var fresh = people.Hire( new ParkStaffPool.Candidate( Id: 999, Kind: 4, Name: "Ada Test", Grade: 2, Costume: 0, Wage: 69 ), 47, 21 );
		var ran = people.Hire( new ParkStaffPool.Candidate( Id: 998, Kind: 4, Name: "Bea Test", Grade: 2, Costume: 0, Wage: 69 ), 46, 21 );

		Assert.AreEqual( (0, 0), (people.SpriteFor( fresh )!.DrawFlags, people.SpriteFor( ran )!.DrawFlags) );
		Assert.IsTrue( people.SpriteFor( ran )!.Step( 100000 ) );
		Assert.AreEqual( 0x1200, people.SpriteFor( ran )!.DrawFlags );

		// A new start keeps them; a program come round to its first word writes its own, and not a turn sooner;
		// one in the hand is written on a fresh standing program, with them still.
		Assert.IsTrue( people.SpriteFor( walker )!.Start( SpriteScript.Standing ) );
		Assert.IsTrue( people.SpriteFor( other )!.Step( 100000 ) );
		Assert.AreEqual( 0x2468, people.SpriteFor( other )!.DrawFlags, "inside its round still" );

		for ( var turn = 2; turn < 20; ++turn )
			Assert.IsTrue( people.SpriteFor( other )!.Step( 100000 * turn ) );

		Assert.IsFalse( people.SpriteFor( held )!.IsOn( SpriteScript.Standing ), "or the writer makes no fresh program for it" );
		Assert.IsTrue( people.PickUp( held ) );

		var written = new ParkWorld( ParkFileWriter.Body( park, new ParkFileWriter.Running( park.GameTick, false, 0, 0, park.Camera.Saved!.Value,
			People: people.Written( Level.WrittenThings( park ).Contains ) ) ) );

		Assert.AreEqual( 0, Flags( written, fresh ), "made and not run" );
		Assert.AreEqual( 0x1200, Flags( written, ran ), "made and run" );
		Assert.AreEqual( 0x4321, Flags( written, walker ), "kept, started again" );
		Assert.AreEqual( 0x1200, Flags( written, other ), "kept, run" );
		Assert.AreEqual( 0x1234, Flags( written, held ), "kept, written standing" );

		TestRun.DeleteEvery<ParkPeople>();

		var loaded = new ParkPeople( written ) { BankAt = Bank };

		Assert.AreEqual( (0, 0x1200, 0x4321), (loaded.SpriteFor( fresh )!.DrawFlags, loaded.SpriteFor( ran )!.DrawFlags, loaded.SpriteFor( walker )!.DrawFlags) );
	}

	/// <summary>
	/// A sprite's three state words are read from its record: the frames a direction at <c>+0xbc</c> and the lead-in
	/// and the hold at <c>+0xc8</c> and <c>+0xcc</c>, which the state script holds frame 0 by between rounds.
	/// </summary>
	[TestMethod]
	public void ASpritesStateWordsAreReadFromItsRecord()
	{
		var entertainer = shipped.People.Single( person => person.Model == 6 );

		Assert.AreEqual( new ParkWorld.SpriteStateWords( SetByte( shipped, entertainer.SpriteSlot ), 0, 0 ), shipped.SpriteStateWordsOf( entertainer.SpriteSlot ) );
		Assert.AreEqual( default, shipped.SpriteStateWordsOf( 99 ), "an empty slot" );

		// The record as one made on a state whose group ends 2, 1, resting at frame 3 inside the loop.
		var body = ParkFileWriter.Body( shipped, Running( new ParkPeople( shipped ) { BankAt = Bank } ) );
		var at = body.AsSpan().IndexOf( new ParkWorld( body ).SpriteRecordOf( entertainer.SpriteSlot )! );

		foreach ( var (offset, value) in new[] { (0x08, 1732), (0x0c, 1760), (0x1c, 19), (0x6c, 1730), (0x78, 1), (0xb4, 4), (0xb8, 3), (0xbc, 5), (0xc8, 2), (0xcc, 1) } )
			BitConverter.GetBytes( value ).CopyTo( body, at + offset );

		var park = new ParkWorld( body );

		Assert.AreEqual( new ParkWorld.SpriteStateWords( 5, 2, 1 ), park.SpriteStateWordsOf( entertainer.SpriteSlot ) );

		TestRun.DeleteEvery<ParkPeople>();

		var sprite = new ParkPeople( park ).SpriteFor( entertainer.ThingId )!;
		var seen = new List<int>();

		for ( var turn = 0; turn < 10; ++turn )
		{
			Assert.IsTrue( sprite.Step( 1000 * (turn + 1) ) );
			seen.Add( sprite.Frame );
		}

		// Frame 4 ends the round of five; frame 0 is held two turns (the hold + 1); the next round starts at the lead-in.
		CollectionAssert.AreEqual( new[] { 4, 0, 0, 2, 3, 4, 0, 0, 2, 3 }, seen );
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
