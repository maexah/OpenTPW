using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace OpenTPW.Tests;

/// <summary>
/// The save's track-rides and coasters modules, the running table of track rides and <c>FUN_00545310</c>'s walk of
/// a ride's track - <see cref="ParkTrackRides"/>, <see cref="ParkCoasters"/> and <see cref="ParkTrackRideTable"/>.
/// <c>docs/exe/ride-operation.md</c>, "A coaster's, a track ride's and an upgraded ride's excitement".
/// </summary>
/// <remarks>
/// The shipped park holds no track ride and no coaster, so the modules that hold them are built here the way
/// FileFormats <c>saves.md</c> lays them out, the Dino Karts from Alexah's played jungle park section for section.
/// The tests that need the shipped park are skipped where there is no installation - see <see cref="GameData"/>.
/// </remarks>
[TestClass]
public class ParkTrackRideTests
{
	private static int TimesReported( string what )
		=> Unimplemented.Summary.FirstOrDefault( entry => entry.What == what ).Times;

	private ParkWorld ShippedPark()
	{
		var data = GameData.Required();
		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );

		return new ParkWorld( new SaveReader( stream ).ReadFile() );
	}

	/// <summary>The shipped park's two modules are the empty ones, each walked to its tag.</summary>
	[TestMethod]
	public void TheShippedParkHoldsNoTrackRideAndNoCoaster()
	{
		var park = ShippedPark();

		Assert.IsNull( park.TrackRides.Problem, "the track-rides module reads" );
		Assert.IsTrue( park.TrackRides.ClosedOnTag, "and lands on its KART tag" );
		Assert.AreEqual( 0, park.TrackRides.Rides.Count, "holding only its stamp" );
		Assert.IsNull( park.Coasters.Problem, "the coasters module reads" );
		Assert.AreEqual( 0, park.Coasters.Count );
		Assert.IsNull( park.Coasters.First );
		Assert.AreEqual( 0, new ParkState( park ).TrackRides.Count, "so the running table starts empty" );
	}

	/// <summary>
	/// A played park's Dino Karts: one ride and its 33 sections, in file order, the station's two straights with
	/// bit 16 set. A module whose root claims four bytes more than it holds does not land on its tag and is refused.
	/// </summary>
	[TestMethod]
	public void ASavedKartsTrackIsReadSectionBySection()
	{
		var read = new ParkTrackRides( TrackBytes.Kart( TrackBytes.DinoKartsHandle, TrackBytes.DinoKarts, TrackBytes.PlayedKarts ) );

		Assert.IsNull( read.Problem );
		Assert.IsTrue( read.ClosedOnTag );
		Assert.AreEqual( 1, read.Rides.Count );

		var ride = read.Rides[0];

		Assert.AreEqual( unchecked((int)0xfffffc00), ride.Handle, "slot 0, BumperType -4" );
		Assert.AreEqual( TrackBytes.DinoKarts, ride.ItemId );
		CollectionAssert.AreEqual( TrackBytes.PlayedKarts, read.Sections.Select( section => section.Type ).ToArray() );
		Assert.IsTrue( read.Sections.All( section => section.Handle == ride.Handle && section.RidesBefore == 1 ) );
		Assert.AreEqual( 33, read.Sections.Select( section => (section.X, section.Y) ).Distinct().Count(), "each on its own cell" );

		var long4 = new ParkTrackRides( TrackBytes.Kart( TrackBytes.DinoKartsHandle, TrackBytes.DinoKarts,
			TrackBytes.PlayedKarts, rootSlack: 4 ) );

		Assert.IsNotNull( long4.Problem, "four bytes too long, it ends on no tag" );
		Assert.IsFalse( long4.ClosedOnTag );
		Assert.AreEqual( 0, long4.Rides.Count, "and a refused module reads no ride" );

		var retagged = TrackBytes.Kart( TrackBytes.DinoKartsHandle, TrackBytes.DinoKarts, TrackBytes.PlayedKarts );
		var tag = retagged.AsSpan().IndexOf( Encoding.ASCII.GetBytes( "KART" ) );

		retagged[tag + 3] = (byte)'X';

		Assert.IsNotNull( new ParkTrackRides( retagged ).Problem, "the right length, closed by the wrong tag" );
	}

	/// <summary>
	/// Each object's <c>MeshInstanceID</c>, the dword before <c>mFlags</c>: the shipped park's fourteen are distinct, the
	/// Belly Bounce's 110. In Alexah's played jungle park Temple Of Gloom's is 330, the number its coasters-module header
	/// holds (<c>q172bsave</c>, read-only).
	/// </summary>
	[TestMethod]
	public void EveryPlacedThingHasItsOwnModelInstance()
	{
		var objects = ShippedPark().Objects;

		Assert.AreEqual( 110, objects.Single( o => o.ThingId == 13 ).MeshInstance );
		Assert.AreEqual( objects.Count, objects.Select( o => o.MeshInstance ).Where( i => i != 0 ).Distinct().Count() );
	}

	/// <summary>A park whose save holds a track ride starts its running table with it, in its saved slot.</summary>
	[TestMethod]
	public void AParkStartsWithItsSavedTrackRides()
	{
		var data = GameData.Required();
		var payload = new SaveReader( new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) ) ).ReadFile();
		var kart = TrackBytes.Kart( TrackBytes.DinoKartsHandle, TrackBytes.DinoKarts, TrackBytes.PlayedKarts );
		var park = new ParkWorld( TrackBytes.WithKart( payload, kart ) );

		Assert.IsNull( park.TrackRides.Problem );
		Assert.AreEqual( 1, park.TrackRides.Rides.Count );

		var state = new ParkState( park );

		Assert.AreEqual( new ParkTrackRideTable.Layout( 12, 6, 1, 33 ), state.TrackRides.LayoutOf( TrackBytes.DinoKartsHandle ) );
		Assert.AreEqual( unchecked((int)0xffffff01), state.TrackRides.Take( -1 ), "so a Hot Pot bought there takes slot 1" );
	}

	/// <summary>
	/// <c>FUN_00545310</c>'s answers: the played Dino Karts' 24 bends and 2 crossings, counted over two passes, halve
	/// to 12 and 1, and its longest run is the six at the list's head. Two passes carry a run from the tail into the
	/// head, so <c>[9, 5, 9]</c> has a run of 2 where one pass gives 1; a list with no bend keeps no run.
	/// </summary>
	[TestMethod]
	public void ATracksLayoutIsWalkedTwiceRoundTheCircuit()
	{
		Assert.AreEqual( new ParkTrackRideTable.Layout( 12, 6, 1, 33 ), ParkTrackRideTable.Walk( TrackBytes.PlayedKarts ) );
		Assert.AreEqual( new ParkTrackRideTable.Layout( 1, 2, 0, 3 ), ParkTrackRideTable.Walk( [9, 5, 9] ),
			"the tail's straight joins the head's" );
		Assert.AreEqual( new ParkTrackRideTable.Layout( 0, 0, 0, 3 ), ParkTrackRideTable.Walk( [9, 9, 9] ),
			"a run no bend ends is never kept" );
		Assert.AreEqual( new ParkTrackRideTable.Layout( 0, 0, 0, 0 ), ParkTrackRideTable.Walk( [] ) );
		Assert.AreEqual( new ParkTrackRideTable.Layout( 1, 3, 3, 4 ), ParkTrackRideTable.Walk( [11, 11, 11, 5] ),
			"a crossing lengthens the run it counts in" );
		Assert.AreEqual( new ParkTrackRideTable.Layout( 1, 3, 0, 4 ), ParkTrackRideTable.Walk( [12, 13, 10, 5] ),
			"and so does a straight with an add-on" );
	}

	/// <summary>
	/// The running table: a saved ride keeps its saved slot, a purchase takes the first free one and a sale lets it
	/// go. A handle whose slot holds another BumperType is stale, answers nothing and frees nothing.
	/// </summary>
	[TestMethod]
	public void ASavedRideKeepsItsSlotAndAPurchaseTakesTheNextFree()
	{
		var table = new ParkTrackRideTable( new ParkTrackRides(
			TrackBytes.Kart( TrackBytes.DinoKartsHandle, TrackBytes.DinoKarts, TrackBytes.PlayedKarts ) ) );

		Assert.AreEqual( 1, table.Count );
		Assert.IsTrue( table.Holds( TrackBytes.DinoKartsHandle ) );
		Assert.AreEqual( new ParkTrackRideTable.Layout( 12, 6, 1, 33 ), table.LayoutOf( TrackBytes.DinoKartsHandle ) );

		var hotPot = table.Take( -1 );

		Assert.AreEqual( unchecked((int)0xffffff01), hotPot, "slot 1, BumperType -1" );
		Assert.AreEqual( new ParkTrackRideTable.Layout( 0, 0, 0, 0 ), table.LayoutOf( hotPot ), "with no track laid" );

		Assert.IsNull( table.LayoutOf( unchecked((int)0xfffffd00) ), "slot 0 holds a -4, not a -3" );
		table.Free( unchecked((int)0xfffffd00) );
		Assert.AreEqual( 2, table.Count, "and a stale handle frees nothing" );

		table.Free( hotPot );
		Assert.AreEqual( 1, table.Count );
		Assert.AreEqual( unchecked((int)0xfffffb01), table.Take( -5 ), "the freed slot is the first free again" );

		table.Free( TrackBytes.DinoKartsHandle );
		Assert.AreEqual( TrackBytes.DinoKartsHandle, table.Take( -4 ), "a Dino Karts bought into the saved one's slot" );
		Assert.AreEqual( new ParkTrackRideTable.Layout( 0, 0, 0, 0 ), table.LayoutOf( TrackBytes.DinoKartsHandle ),
			"has none of the sold one's track" );
	}

	/// <summary>
	/// The loader's refusals (<c>FUN_0054b2f0</c>): a section on a cell its ride's list already holds is ignored, one
	/// for a ride the table does not hold is not laid - so a section saved before its ride's record is lost - and none
	/// is laid past the pool of 0x400.
	/// </summary>
	[TestMethod]
	public void ASectionIsLaidOnceOnItsCellAndOnlyOnARideHeld()
	{
		var table = new ParkTrackRideTable();
		var karts = table.Take( -4 );

		Assert.IsTrue( table.Lay( karts, 9, 0xc00, 0xc00 ) );
		Assert.IsFalse( table.Lay( karts, 5, 0xc00, 0xc00 ), "the same cell again" );
		Assert.IsFalse( table.Lay( unchecked((int)0xfffffc01), 9, 0x1800, 0xc00 ), "slot 1 holds nothing" );
		Assert.AreEqual( new ParkTrackRideTable.Layout( 0, 0, 0, 1 ), table.LayoutOf( karts ) );

		for ( var at = 1; at < ParkTrackRideTable.SectionPool; ++at )
			Assert.IsTrue( table.Lay( karts, 9, at * 0xc00, 0x1800 ) );

		Assert.IsFalse( table.Lay( karts, 9, 0, 0x2400 ), "the pool's 1,024 are laid" );

		var early = new ParkTrackRideTable( new ParkTrackRides( TrackBytes.Kart( TrackBytes.DinoKartsHandle,
			TrackBytes.DinoKarts, TrackBytes.PlayedKarts, sectionsFirst: true ) ) );

		Assert.AreEqual( 1, early.Count, "the ride is seated" );
		Assert.AreEqual( new ParkTrackRideTable.Layout( 0, 0, 0, 0 ), early.LayoutOf( TrackBytes.DinoKartsHandle ),
			"and its sections, read before it, were not laid" );
	}

	/// <summary>A 65th track ride finds no slot: the original reads through a null entry, and here it is counted.</summary>
	[TestMethod]
	public void ASixtyFifthTrackRideIsCounted()
	{
		var table = new ParkTrackRideTable();
		var handles = Enumerable.Range( 0, ParkTrackRideTable.Slots ).Select( _ => table.Take( -1 ) ).ToArray();

		Assert.AreEqual( unchecked((int)0xffffff3f), handles[^1], "the 64th takes slot 63" );

		var full = TimesReported( "TRACK_RIDE_TABLE_FULL" );

		Assert.AreEqual( 0, table.Take( -1 ) );
		Assert.AreEqual( full + 1, TimesReported( "TRACK_RIDE_TABLE_FULL" ) );
	}

	/// <summary>
	/// The placer takes a slot for an item with a <c>Bumper.BumperType</c> and none for one without, and selling the
	/// thing lets its slot go, so the next purchase takes it again.
	/// </summary>
	[TestMethod]
	public void APurchaseTakesASlotAndItsSaleLetsItGo()
	{
		var data = GameData.Required();
		var world = ShippedPark();
		var catalogue = new ParkItemCatalogue( "jungle", data );
		var state = new ParkState( world );

		Assert.IsTrue( catalogue.TryGet( 1140, out var hotPot ) );
		Assert.IsTrue( catalogue.TryGet( 1100, out var bellyBounce ) );

		Assert.AreEqual( 0, ParkBuilding.TakeTrackRide( state, bellyBounce ), "the Belly Bounce has no BumperType" );

		var handle = ParkBuilding.TakeTrackRide( state, hotPot );

		Assert.AreEqual( unchecked((int)0xffffff00), handle, "the shipped park's table is empty: slot 0" );

		var bought = ParkBuilding.Constructed( hotPot, 9000, 20, 12, 0, 0, 0, default, handle );

		Assert.AreEqual( handle, bought.TrackRide, "the constructor stores it" );

		state.AddObject( bought );
		StringAssert.StartsWith( ParkBuilding.Sell( state, world, catalogue, null, null, 9000 ), "sell: '" );

		Assert.IsFalse( state.TrackRides.Holds( handle ), "the sale let the slot go" );
		Assert.AreEqual( handle, ParkBuilding.TakeTrackRide( state, hotPot ), "so the next Hot Pot takes slot 0" );
	}

	/// <summary>
	/// The purchase's own path, through a move whose put-down passes every test and then will not load, as it cannot
	/// without models: the pickup lets the Hot Pot's slot go, the put-down takes one and lets it go again. With every
	/// slot taken, the put-down's take is the one counted.
	/// </summary>
	[TestMethod]
	public void AFailedPutDownLetsItsSlotGo()
	{
		var data = GameData.Required();
		var world = ShippedPark();
		var catalogue = new ParkItemCatalogue( "jungle", data );

		try
		{
			var state = new ParkState( world );

			StringAssert.StartsWith( ParkBuilding.Sell( state, world, catalogue, null, null, 13 ), "sell: '" );

			var handle = state.TrackRides.Take( -1 );

			state.AddObject( new ParkWorld.CatalogueObject( ThingId: 9000, CatalogueId: 1140, RawX: 20 << 8, RawY: 12 << 8,
				Angle: 0, TrackRide: handle ) );

			StringAssert.Contains( ParkBuilding.Move( state, world, catalogue, null, null, 9000, 57, 23, 0 ), "would not load" );
			Assert.AreEqual( 0, state.TrackRides.Count, "nothing holds a slot" );

			var full = new ParkState( world );

			StringAssert.StartsWith( ParkBuilding.Sell( full, world, catalogue, null, null, 13 ), "sell: '" );

			for ( var slot = 0; slot < ParkTrackRideTable.Slots; ++slot )
				full.TrackRides.Take( -4 );

			full.AddObject( new ParkWorld.CatalogueObject( ThingId: 9000, CatalogueId: 1140, RawX: 20 << 8, RawY: 12 << 8,
				Angle: 0 ) );

			var counted = TimesReported( "TRACK_RIDE_TABLE_FULL" );

			StringAssert.Contains( ParkBuilding.Move( full, world, catalogue, null, null, 9000, 57, 23, 0 ), "would not load" );
			Assert.AreEqual( counted + 1, TimesReported( "TRACK_RIDE_TABLE_FULL" ), "the put-down asked for a slot" );
		}
		finally
		{
			ParkBuilding.Drop();
			ParkBuildMode.Forget();
			ParkState.ForgetCurrent();
		}
	}

	/// <summary>
	/// The coasters module of Alexah's played jungle park: one coaster, its header's flags <c>0x101</c>, model instance
	/// 330, handle 1, 38 pieces and no clash, then 510 bytes not decoded. A module holding none must end on its tag.
	/// </summary>
	[TestMethod]
	public void ASavedCoastersHeaderIsRead()
	{
		var read = new ParkCoasters( TrackBytes.Coasters( 1, 0x101, 330, clashes: 0 ) );

		Assert.IsNull( read.Problem );
		Assert.AreEqual( 1, read.Count );
		Assert.AreEqual( new SavedCoaster( 0x101, 330, 1, 38, 0 ), read.First );
		Assert.IsTrue( read.First!.Value.CircuitClosed );
		Assert.AreEqual( 0, read.Unread );
		Assert.IsNotNull( read.For( 330 ) );
		Assert.IsNull( read.For( 331 ) );
		Assert.IsNull( read.For( 0 ), "a thing with no model instance is no coaster's" );

		Assert.AreEqual( 1, new ParkCoasters( TrackBytes.Coasters( 2, 0x101, 330, clashes: 0 ) ).Unread,
			"a second coaster's header cannot be found" );

		var none = Encoding.ASCII.GetBytes( "EMAK" ).Concat( new byte[16] ).Concat( Encoding.ASCII.GetBytes( "SVDA" ) ).ToArray();

		Assert.IsNotNull( new ParkCoasters( none ).Problem, "none, and no SAOC tag after the counts" );
	}
}

/// <summary>The bytes of the two save modules these tests build, laid out as FileFormats <c>saves.md</c> gives them.</summary>
internal static class TrackBytes
{
	public const int DinoKarts = 1150;

	public const int DinoKartsHandle = unchecked((int)0xfffffc00);

	/// <summary>
	/// Alexah's played Dino Karts' 33 sections, in circuit order from the station, the first two straights with bit 16.
	/// </summary>
	public static readonly int[] PlayedKarts =
	[
		0x10009, 0x10009, 9, 12, 12, 9, 5, 10, 10, 8, 11, 9, 7, 10, 6, 9, 5, 10, 10, 8, 9, 12, 9, 9, 7, 10, 6, 8, 5,
		9, 7, 10, 6
	];

	private static byte[] Chunk( int type, params int[] dwords )
	{
		var size = 12 + (dwords.Length * 4);

		return new[] { type, size, size }.Concat( dwords ).SelectMany( BitConverter.GetBytes ).ToArray();
	}

	/// <summary>
	/// A payload holding a track-rides module of one ride and its sections: after the <c>SYSR</c> tag, the root, the
	/// stamp, the ride, each section, the close and the <c>KART</c> tag, filler either side.
	/// </summary>
	/// <param name="rootSlack">Bytes the root claims beyond what it holds.</param>
	/// <param name="sectionsFirst">Whether the sections come before the ride's record rather than after it.</param>
	public static byte[] Kart( int handle, int itemId, int[] sections, int rootSlack = 0, bool sectionsFirst = false )
	{
		var children = new List<byte>();
		var ride = Chunk( 3, handle, 60 * 0xc00, 40 * 0xc00, 0, itemId, 0, 0, 0, 0, 0 );

		children.AddRange( Chunk( 2, 0x7cf, 9, 3, 22, 0 ) );

		if ( !sectionsFirst )
			children.AddRange( ride );

		// Each section on a cell of its own, as the game's own lists hold them.
		for ( var at = 0; at < sections.Length; ++at )
			children.AddRange( Chunk( 4, handle, sections[at], (20 + at) * 0xc00, 40 * 0xc00 ) );

		if ( sectionsFirst )
			children.AddRange( ride );

		children.AddRange( Chunk( 6, handle ) );

		var root = new[] { 1, 12, 12 + children.Count + rootSlack }.SelectMany( BitConverter.GetBytes );

		return Encoding.ASCII.GetBytes( "RSYS....SYSR" ).Concat( root ).Concat( children )
			.Concat( Encoding.ASCII.GetBytes( "KARTRYLF" ) ).ToArray();
	}

	/// <summary>
	/// A coasters module whose first coaster has these header fields and 38 pieces, then the 510 undecoded bytes the
	/// played park's has, between the <c>EMAK</c> and <c>SAOC</c> tags.
	/// </summary>
	public static byte[] Coasters( int count, int flags, int meshInstance, int clashes )
		=> Encoding.ASCII.GetBytes( "EMAK" ).Concat( CoastersModule( count, flags, meshInstance, clashes ) )
			.Concat( Encoding.ASCII.GetBytes( "SAOCSVDA" ) ).ToArray();

	/// <summary>The module alone, from its four counts to the last undecoded byte.</summary>
	public static byte[] CoastersModule( int count, int flags, int meshInstance, int clashes )
	{
		var header = new byte[32];

		BitConverter.GetBytes( flags ).CopyTo( header, 0 );
		BitConverter.GetBytes( (short)19 ).CopyTo( header, 0x08 );
		BitConverter.GetBytes( (short)meshInstance ).CopyTo( header, 0x0a );
		BitConverter.GetBytes( (short)1 ).CopyTo( header, 0x0c );
		BitConverter.GetBytes( (short)38 ).CopyTo( header, 0x16 );
		BitConverter.GetBytes( clashes ).CopyTo( header, 0x1c );

		return new[] { count, count, 0, count + 1 }.SelectMany( BitConverter.GetBytes )
			.Concat( header ).Concat( new byte[(38 * 0x22) + 510] ).ToArray();
	}

	/// <summary>A park payload with its track-rides module, from the <c>SYSR</c> tag to the <c>KART</c> tag, replaced by the one in these bytes.</summary>
	public static byte[] WithKart( byte[] payload, byte[] kart )
	{
		var span = payload.AsSpan();
		var from = span.IndexOf( Encoding.ASCII.GetBytes( "SYSR" ) ) + 4;
		var to = span.IndexOf( Encoding.ASCII.GetBytes( "KART" ) );
		var module = kart.AsSpan();
		var start = module.IndexOf( Encoding.ASCII.GetBytes( "SYSR" ) ) + 4;
		var end = module.IndexOf( Encoding.ASCII.GetBytes( "KART" ) );

		return payload[..from].Concat( kart[start..end] ).Concat( payload[to..] ).ToArray();
	}

	/// <summary>A park payload with its coasters module, the 16 bytes after the <c>EMAK</c> tag, replaced by this one.</summary>
	public static byte[] WithCoasters( byte[] payload, byte[] module )
	{
		var at = payload.AsSpan().IndexOf( Encoding.ASCII.GetBytes( "EMAK" ) ) + 4;

		Assert.AreEqual( "SAOC", Encoding.ASCII.GetString( payload, at + 16, 4 ), "the park holds no coaster" );

		return payload[..at].Concat( module ).Concat( payload[(at + 16)..] ).ToArray();
	}
}
