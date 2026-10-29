using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// The park file's writer after an id is given out again (<c>docs/exe/saves.md</c>, "OpenTPW's writer, an id given
/// out again"): the file's thing on a deleted id is gone from the file, and whatever holds the id now is a new thing,
/// written whole as made, the made newest first by their making.
/// </summary>
/// <remarks>
/// <b>Mutations:</b> the writer pairing by id alone (the kept path, the model check, the bought check); the deleted
/// set not handed over; the made sorted by id; the deleted id left in the cell's chain.
/// </remarks>
[TestClass]
public class ParkFileWriterReusedIdTests
{
	private const string Theme = "jungle";
	private const string ShippedPark = "levels/jungle/Easymode.TPWI";
	private const int CrazyApe = 1101, DrinksShop = 16, DrinksScript = 6, Researcher = 8;

	private BaseFileSystem data = null!;
	private BaseFileSystem? oldSaves;
	private string? root;
	private ParkWorld shipped = null!;
	private ParkItemCatalogue catalogue = null!;
	private readonly List<Entity> made = [];

	private string Jungle => Path.Combine( root!, "users", "1Test", "jungle" );

	[TestInitialize]
	public void ReadTheShippedPark()
	{
		Log ??= new();
		data = FileSystem = GameData.Required();
		shipped = Read( data.ReadAllBytes( ShippedPark ) );
		catalogue = new ParkItemCatalogue( Theme, data );

		oldSaves = SaveFileSystemOrNull;
		root = Directory.CreateTempSubdirectory( "opentpw-reused-id-" ).FullName;
		Directory.CreateDirectory( Jungle );
		SaveFileSystem = new BaseFileSystem( root );
		typeof( Players ).GetProperty( nameof( Players.Current ) )!.SetValue( Players.Roster, new Player( 0, "Test", new PlayerFile { InstantAction = true } ) );
		Unimplemented.Forget();
	}

	[TestCleanup]
	public void PutAwayWhatWasMade()
	{
		foreach ( var entity in made )
			entity.Delete();

		made.Clear();
		Entity.ApplyDeletions();
		TestRun.DeleteEvery<ParkPeople>();

		// Without the game the set-up stops before anything is changed.
		if ( root != null )
		{
			typeof( Players ).GetProperty( nameof( Players.Current ) )!.SetValue( Players.Roster, null );
			SaveFileSystemOrNull = oldSaves;
			Directory.Delete( root, true );
		}

		Unimplemented.Forget();
	}

	private static ParkWorld Read( byte[] file )
	{
		using var stream = new MemoryStream( file );
		var reader = new SaveReader( stream );
		return new ParkWorld( reader.ReadFile(), reader.Preamble );
	}

	private static int Times( string gap ) => Unimplemented.Summary.Where( counted => counted.What == gap ).Sum( counted => counted.Times );

	/// <summary>The shipped park running: its people (whose state is the running one) and its scripts.</summary>
	private (ParkPeople People, ParkState State, ParkRides Rides) Park()
	{
		var people = new ParkPeople( shipped );
		var rides = new ParkRides( Theme, shipped, catalogue, data );

		made.Add( rides );

		return (people, people.State, rides);
	}

	/// <summary>A guest the park lets go home at once (the first, or the last), gone and their id freed: the head of the free list.</summary>
	private static int Departed( ParkPeople people, bool last = false )
	{
		var free = people.Peeps.Where( peep => !PeepBehaviour.HeldByAThing( peep ) );
		var guest = (last ? free.Last() : free.First()).ThingId;

		Assert.IsTrue( people.Depart( guest ) );
		people.State.FreeDeletedThings();

		return guest;
	}

	/// <summary>A thing bought by hand, as <c>ParkBuilding.Build</c> leaves one.</summary>
	private ParkWorld.CatalogueObject Buy( ParkState state, ParkRides rides, int itemId, int x, int y )
	{
		Assert.IsTrue( catalogue.TryGet( itemId, out var item ) );

		var id = state.NextThingId();
		var placed = ParkBuilding.Constructed( item, id, x, y, 0, MapStep.CellId( x + item.EntryDeltaX, y + item.EntryDeltaY ),
			MapStep.CellId( x + item.ExitDeltaX, y + item.ExitDeltaY ), ParkWorld.BuiltWhen.At( state.CalendarNow ) );

		state.AddObject( placed );
		ParkBuilding.BindOperation( state, rides, placed, item );
		ParkBuilding.Stamp( state, ParkObjects.FootprintAt( item, x, y, 0 ), x, y );
		state.EnterCell( x, y, id );

		return placed;
	}

	private ParkWorld Saved( ParkPeople people, ParkState state, ParkRides rides, string name )
	{
		Assert.IsNotNull( Level.WritePark( shipped, state, Theme, name, people, null, rides, catalogue ), "the park is written" );

		var written = Read( File.ReadAllBytes( Path.Combine( Jungle, name + ".TPWS" ) ) );

		Assert.IsNull( written.Problem );

		return written;
	}

	/// <summary>
	/// <b>A hire on a departed guest's id is written whole</b>: the guest is gone from the file, the member of staff is
	/// a researcher on that id at the head of the thing list and of the researchers' list.
	/// </summary>
	[TestMethod]
	public void AHireOnADepartedGuestsIdIsWrittenWhole()
	{
		var (people, state, rides) = Park();
		var guest = Departed( people );
		var hire = people.Hire( new ParkStaffPool.Candidate( Id: 999, Kind: 4, Name: "Ada Test", Grade: 2, Costume: 0, Wage: 69 ), 47, 21 );

		Assert.AreEqual( guest, hire, "the hire takes the guest's id" );

		var written = Saved( people, state, rides, "Hired" );
		var person = written.People.Single( thing => thing.ThingId == hire );

		Assert.AreEqual( (Researcher, "Ada Test", 47, 21), (person.Model, person.Staff!.Value.Name, person.CellX, person.CellY) );
		Assert.AreEqual( hire, written.Things[0].ThingId, "made, so at the head" );
		Assert.AreEqual( shipped.People.Count, written.People.Count, "one gone, one made" );
	}

	/// <summary><b>An object bought on a departed guest's id is written whole</b>, and no person is left on the id.</summary>
	[TestMethod]
	public void AnObjectBoughtOnADepartedGuestsIdIsWrittenWhole()
	{
		var (people, state, rides) = Park();
		var guest = Departed( people );
		var ape = Buy( state, rides, CrazyApe, 41, 22 );

		Assert.AreEqual( guest, ape.ThingId );

		var written = Saved( people, state, rides, "Bought" );

		Assert.AreEqual( (CrazyApe, 41, 22), written.Objects.Where( thing => thing.ThingId == guest ).Select( thing => (thing.CatalogueId, thing.CellX, thing.CellY) ).Single() );
		Assert.IsFalse( written.People.Any( person => person.ThingId == guest ) );
		Assert.AreEqual( (guest, guest), (written.Things[0].ThingId, written.FirstObject), "made, at the head of both lists" );
		Assert.AreEqual( 1, written.ScriptStates.HandlesOf( guest ).Count(), "with its script" );
	}

	/// <summary>
	/// <b>A guest arriving on a departed guest's id is a new guest</b>: written as made, at the head of the list,
	/// not over the departed guest's record at its place in the file's; and on the cell the departed guest stood on,
	/// the newest comer, so at the head of its chain ahead of a guest who came before.
	/// </summary>
	[TestMethod]
	public void AGuestArrivingOnADepartedGuestsIdIsWrittenAsMade()
	{
		var (people, state, rides) = Park();
		var leaver = people.Peeps.Last( peep => !PeepBehaviour.HeldByAThing( peep ) );
		var (x, y) = (shipped.People.Single( thing => thing.ThingId == leaver.ThingId ).CellX, shipped.People.Single( thing => thing.ThingId == leaver.ThingId ).CellY);
		var before = people.Admit( x, y );
		var guest = Departed( people, last: true );
		var at = shipped.Things.Select( thing => thing.ThingId ).ToList().IndexOf( guest );
		var arrived = people.Admit( x, y );

		Assert.AreEqual( (leaver.ThingId, guest, 43), (arrived, arrived, before) );
		Assert.IsTrue( at > 0, "the departed guest was not the file's head, or the list's order proves nothing" );

		var written = Saved( people, state, rides, "Arrived" );
		var person = written.People.Single( thing => thing.ThingId == arrived );

		CollectionAssert.AreEqual( new[] { arrived, before }, written.Things.Take( 2 ).Select( thing => thing.ThingId ).ToArray(), "made, so at the head, the newest first" );
		Assert.AreEqual( (ParkWorld.GuestModel, x, y), (person.Model, person.CellX, person.CellY) );
		Assert.AreEqual( arrived, (int)written.Cells[(y * ParkWorld.MapSize) + x].Occupant, "the newest comer heads the cell" );
		Assert.IsTrue( written.People.Single( thing => thing.ThingId == before ).SpriteSlot < person.SpriteSlot,
			"the older made takes the lower free sprite slot" );
		Assert.AreEqual( shipped.People.Count + 1, written.People.Count );
	}

	/// <summary><b>A guest on a sold object's id is written whole</b>, and the object and its script are gone.</summary>
	[TestMethod]
	public void AGuestOnASoldObjectsIdIsWrittenWhole()
	{
		var (people, state, rides) = Park();

		StringAssert.StartsWith( ParkBuilding.Sell( state, shipped, catalogue, null, rides, DrinksShop, people ), "sell: " );
		state.FreeDeletedThings();

		var arrived = people.Admit( 47, 21 );

		Assert.AreEqual( DrinksShop, arrived );

		var written = Saved( people, state, rides, "GuestOnTheShop" );

		Assert.IsFalse( written.Objects.Any( thing => thing.ThingId == DrinksShop ) );
		Assert.AreEqual( ParkWorld.GuestModel, written.People.Single( person => person.ThingId == DrinksShop ).Model );
		Assert.IsFalse( written.ScriptStates.HandlesOf( DrinksShop ).Any(), "the shop's script goes with it" );
		Assert.IsTrue( written.MessageSets()![0xa].Contains( DrinksShop ), "the guest in the set of everybody" );
	}

	/// <summary>
	/// <b>A thing bought on a sold thing's id replaces it</b>: the state hands the file's object over as gone and the
	/// new one as made, and the file holds the new one, its own script and not the shop's.
	/// </summary>
	[TestMethod]
	public void AThingBoughtOnASoldThingsIdReplacesIt()
	{
		var (people, state, rides) = Park();

		StringAssert.StartsWith( ParkBuilding.Sell( state, shipped, catalogue, null, rides, DrinksShop, people ), "sell: " );
		state.FreeDeletedThings();

		var ape = Buy( state, rides, CrazyApe, 41, 22 );

		Assert.AreEqual( DrinksShop, ape.ThingId );

		var kept = state.WrittenObjects( shipped, _ => true, out var bought, out var gone );

		CollectionAssert.AreEqual( new[] { DrinksShop }, gone.ToArray() );
		CollectionAssert.AreEqual( new[] { (DrinksShop, CrazyApe) }, bought.Select( thing => (thing.ThingId, thing.CatalogueId) ).ToArray() );
		Assert.IsFalse( kept.Any( thing => thing.ThingId == DrinksShop ) );

		var written = Saved( people, state, rides, "ApeOnTheShop" );
		var handles = written.ScriptStates.HandlesOf( DrinksShop ).ToArray();

		Assert.AreEqual( (CrazyApe, 41, 22), written.Objects.Where( thing => thing.ThingId == DrinksShop ).Select( thing => (thing.CatalogueId, thing.CellX, thing.CellY) ).Single() );
		Assert.AreEqual( 1, handles.Length );
		Assert.AreNotEqual( DrinksScript, handles[0], "the ape's own script, not the shop's" );
		Assert.AreEqual( 0, Times( "SAVE_PARK_SCRIPT_MADE_SINCE_THE_LOAD" ) );
	}

	/// <summary>
	/// <b>The made are written newest first by their making, not by id</b>: an ape bought (43), the shop sold and its
	/// id given to a second ape (16), so the file's list and its object list open 16, 43, as the original's used list does.
	/// </summary>
	[TestMethod]
	public void TheMadeAreWrittenNewestFirstByTheirMaking()
	{
		var (people, state, rides) = Park();
		var first = Buy( state, rides, CrazyApe, 41, 22 );

		StringAssert.StartsWith( ParkBuilding.Sell( state, shipped, catalogue, null, rides, DrinksShop, people ), "sell: " );
		state.FreeDeletedThings();

		var second = Buy( state, rides, CrazyApe, 45, 22 );

		Assert.AreEqual( (43, DrinksShop), (first.ThingId, second.ThingId) );

		state.WrittenObjects( shipped, _ => true, out var bought, out _ );
		CollectionAssert.AreEqual( new[] { DrinksShop, 43 }, bought.Select( thing => thing.ThingId ).ToArray(), "the state hands them over newest first" );

		var written = Saved( people, state, rides, "TwoApes" );

		CollectionAssert.AreEqual( new[] { DrinksShop, 43 }, written.Things.Take( 2 ).Select( thing => thing.ThingId ).ToArray() );
		Assert.AreEqual( DrinksShop, written.FirstObject );
		Assert.AreEqual( 43, written.Objects.Single( thing => thing.ThingId == DrinksShop ).NextObject );
		Assert.IsTrue( written.Objects.Single( thing => thing.ThingId == 43 ).MeshInstance < written.Objects.Single( thing => thing.ThingId == DrinksShop ).MeshInstance,
			"the older takes the lower model slot" );
	}

	/// <summary>
	/// The shop sold, kept in the file because no catalogue is handed to say it can be written (the same gap as a tracked
	/// ride's, <c>SAVE_PARK_OBJECT_SOLD</c>), and its id freed: the park written by <c>Level.WritePark</c>.
	/// </summary>
	private (ParkPeople People, ParkState State, ParkRides Rides) KeptSoldShop()
	{
		var park = Park();

		StringAssert.StartsWith( ParkBuilding.Sell( park.State, shipped, catalogue, null, park.Rides, DrinksShop, park.People ), "sell: " );
		park.State.FreeDeletedThings();

		return park;
	}

	/// <summary>
	/// <b>A guest on the id of a sold object the file keeps is left out, counted, and the file still reads</b>: the shop
	/// stays in the file (a deviation: the original writes the guest and drops the shop), the guest who took its id is
	/// not written, and the write is not refused for "thing 16 is model 3 in the file and 1 in the park".
	/// </summary>
	/// <remarks><b>Mutation:</b> the person filter in <c>Level.WritePark</c> removed (the write throws); the kept ids left among
	/// the deleted ones (the write throws).</remarks>
	[TestMethod]
	public void AGuestOnAKeptSoldObjectsIdIsLeftOut()
	{
		var (people, state, rides) = KeptSoldShop();
		var arrived = people.Admit( 47, 21 );

		Assert.AreEqual( DrinksShop, arrived );
		Unimplemented.Forget();
		Assert.IsNotNull( Level.WritePark( shipped, state, Theme, "GuestOnTheKeptShop", people, null, rides, null ), "the park is written" );

		var written = Read( File.ReadAllBytes( Path.Combine( Jungle, "GuestOnTheKeptShop.TPWS" ) ) );

		Assert.IsNull( written.Problem );
		Assert.IsTrue( written.Objects.Any( thing => thing.ThingId == DrinksShop ), "the shop is kept" );
		Assert.IsFalse( written.People.Any( person => person.ThingId == DrinksShop ), "the guest is left out" );
		Assert.AreEqual( shipped.People.Count, written.People.Count, "and nobody else" );
		Assert.AreEqual( (1, 1), (Times( "SAVE_PARK_OBJECT_SOLD" ), Times( "SAVE_PARK_ID_KEPT_FOR_A_SOLD_THING" )) );
	}

	/// <summary>
	/// <b>An object bought on the id of a sold object the file keeps is left out, and the file still reads</b>: the file's
	/// shop stays and the ape is not written (with no catalogue nothing bought is).
	/// </summary>
	/// <remarks><b>Mutation:</b> the kept sold ids not held back from the deleted ids handed to the writer (the write throws).</remarks>
	[TestMethod]
	public void AnObjectBoughtOnAKeptSoldObjectsIdIsLeftOut()
	{
		var (people, state, rides) = KeptSoldShop();
		var ape = Buy( state, rides, CrazyApe, 41, 22 );

		Assert.AreEqual( DrinksShop, ape.ThingId );
		Unimplemented.Forget();
		Assert.IsNotNull( Level.WritePark( shipped, state, Theme, "ApeOnTheKeptShop", people, null, rides, null ), "the park is written" );

		var written = Read( File.ReadAllBytes( Path.Combine( Jungle, "ApeOnTheKeptShop.TPWS" ) ) );

		Assert.IsNull( written.Problem );
		Assert.AreEqual( shipped.Objects.Single( thing => thing.ThingId == DrinksShop ).CatalogueId, written.Objects.Single( thing => thing.ThingId == DrinksShop ).CatalogueId, "the file's shop, not the ape" );
		Assert.AreEqual( shipped.Objects.Count, written.Objects.Count );
		Assert.AreEqual( 1, Times( "SAVE_PARK_OBJECT_SOLD" ), "the shop counted kept; with no catalogue nothing bought is written either" );
	}
}
