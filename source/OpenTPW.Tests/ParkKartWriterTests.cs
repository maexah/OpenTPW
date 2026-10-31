using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OpenTPW.Tests;

/// <summary>
/// A bought kart or water ride written whole into a park file's track-rides module: its record, its sections and its
/// close (<see cref="ParkTrackRides.RideChunks(SavedTrackRide)"/>), the record the track table keeps for it
/// (<see cref="ParkTrackRideTable.KartRide"/>) and how the writer takes it (<see cref="ParkTrackRideTable.KartToWrite"/>).
/// <c>docs/exe/saves.md</c>, "OpenTPW's writer, a kart's or water ride's record".
/// </summary>
[TestClass]
public class ParkKartWriterTests
{
	private const int Karts = -4;
	private const int Water = -5;
	private const int Cell = 0xc00;

	/// <summary>The head of Alexah's played Dino Karts, word for word: the ride, its first two sections and the close.</summary>
	private static readonly uint[] TheirKarts =
	[
		3, 52, 52, 0xfffffc00, 0x12000, 0xf000, 6, 0x47e, 0x3c, 0, 4, 0xfffffffd, 2,
		4, 28, 28, 0xfffffc00, 0x10009, 0x12000, 0xe400,
		4, 28, 28, 0xfffffc00, 0x10009, 0x10800, 0xe400,
		6, 16, 16, 0xfffffc00
	];

	private static readonly SavedTrackRide PlayedRide = new( TrackBytes.DinoKartsHandle, 0x12000, 0xf000, 6, TrackBytes.DinoKarts, 60, 0, 4, -3, 2,
		[(0x10009, 0x12000, 0xe400), (0x10009, 0x10800, 0xe400)] );

	[TestInitialize]
	public void StartTheLog() => Log ??= new();

	/// <summary>A counted gap one of these tests reported is not left for the next.</summary>
	[TestCleanup]
	public void ForgetTheGaps() => Unimplemented.Forget();

	private static byte[] Chunk( int type, params int[] dwords )
	{
		var size = 12 + (dwords.Length * 4);

		return new[] { type, size, size }.Concat( dwords ).SelectMany( BitConverter.GetBytes ).ToArray();
	}

	/// <summary>A module holding a stamp and nothing else, as the shipped park's does.</summary>
	private static byte[] EmptyBody() => TrackBytes.Kart( TrackBytes.DinoKartsHandle, TrackBytes.DinoKarts, [] );

	/// <summary>A file whose Dino Karts holds these sections and, where asked, a car with a rider.</summary>
	private static ParkTrackRides FileWith( int[] sections, bool car = false )
	{
		if ( !car )
			return new ParkTrackRides( TrackBytes.Kart( TrackBytes.DinoKartsHandle, TrackBytes.DinoKarts, sections ) );

		var body = TrackBytes.Kart( TrackBytes.DinoKartsHandle, TrackBytes.DinoKarts, sections );
		var close = body.AsSpan().IndexOf( Chunk( 6, TrackBytes.DinoKartsHandle ) );
		var carChunk = Chunk( 5, [TrackBytes.DinoKartsHandle, .. new int[43 + 5]] );
		var grown = body[..close].Concat( carChunk ).Concat( body[close..] ).ToArray();

		BitConverter.TryWriteBytes( grown.AsSpan( 12 + 8, 4 ), BitConverter.ToInt32( body, 12 + 8 ) + carChunk.Length );

		return new ParkTrackRides( grown );
	}

	[TestMethod]
	public void ARideWithSectionsIsWrittenAsTheOriginalsOwnFileHoldsIt()
	{
		CollectionAssert.AreEqual( TheirKarts.SelectMany( BitConverter.GetBytes ).ToArray(), ParkTrackRides.RideChunks( PlayedRide ) );
	}

	/// <summary>The karts (1) and the water ride (2) are written, as the rides of no track are; the coasters (3) are not.</summary>
	[TestMethod]
	public void EveryKindOfTrackButTheCoastersIsWritten()
	{
		CollectionAssert.AreEqual( new[] { true, true, true, false },
			new[] { 0, ItemDescriptionFile.CarTrack, ItemDescriptionFile.WaterTrack, ItemDescriptionFile.CoasterTrack }.Select( Level.WritesTrackKind ).ToArray() );
	}

	[TestMethod]
	public void ARideWithNoSectionsIsStillTheHotPotsBytes()
	{
		var bare = ParkTrackRides.RideChunks( PlayedRide with { Sections = null } );

		Assert.AreEqual( 52 + 16, bare.Length );
		CollectionAssert.AreEqual( ParkTrackRides.RideChunks( PlayedRide with { Sections = [] } ), bare );
	}

	[TestMethod]
	public void AMadeKartGoesIntoTheModuleWithItsSectionsInCircuitOrder()
	{
		var body = EmptyBody();
		var spliced = new ParkTrackRides( body ).Splice( body, [TrackBytes.DinoKartsHandle], [PlayedRide] );
		var read = new ParkTrackRides( spliced );

		Assert.IsNull( read.Problem );
		Assert.IsTrue( read.ClosedOnTag );
		Assert.AreEqual( PlayedRide with { Sections = null }, read.Rides.Single() );
		CollectionAssert.AreEqual( new[] { 0x10009, 0x10009 }, read.Sections.Select( section => section.Type ).ToArray() );
		CollectionAssert.AreEqual( new[] { 0x12000, 0x10800 }, read.Sections.Select( section => section.X ).ToArray() );
		Assert.IsTrue( read.Sections.All( section => section.RidesBefore == 1 && section.Y == 0xe400 ), "each after its ride's record" );
	}

	/// <summary>A ride written whole puts its sections right after its record, before its cars and its close.</summary>
	[TestMethod]
	public void ASectionComesBeforeTheCars()
	{
		var body = EmptyBody();
		var car = new SavedTrackCar( TrackBytes.DinoKartsHandle, new byte[SavedTrackCar.Size], 0, 0, 0, 0, 0, 0 );
		var whole = new WrittenTrackRide( PlayedRide, [car], [], [] );
		var chunks = ParkTrackRides.RideChunks( whole );
		var types = new List<int>();

		for ( var at = 0; at < chunks.Length; at += BitConverter.ToInt32( chunks, at + 8 ) )
			types.Add( BitConverter.ToInt32( chunks, at ) );

		CollectionAssert.AreEqual( new[] { 3, 4, 4, 5, 6 }, types );
		Assert.AreEqual( 1, new ParkTrackRides( new ParkTrackRides( body ).SpliceWhole( body, [TrackBytes.DinoKartsHandle], [whole] ) ).Cars.Count );
	}

	[TestMethod]
	public void ABoughtKartHoldsItsTemplatesWords()
	{
		var table = new ParkTrackRideTable();
		var handle = table.Take( Karts );
		var ride = table.KartOf( handle )!;

		Assert.AreEqual( unchecked((int)0xfffffc00), handle );
		Assert.AreEqual( (50, 0, 4, -3, 0, false), (ride.Performance, ride.MeshBase, ride.MeshCount, ride.Duration, ride.State, ride.Placed),
			"0x764178's -4: laps -3, four meshes from 0, performance 50, state 0" );
		Assert.IsNull( table.KartWritten( handle, 0, 1150 ), "not written before the placer has put it down" );

		table.PlaceKart( handle, 24 * Cell, 20 * Cell, 250 );

		Assert.AreEqual( 100, table.KartOf( handle )!.Performance, "the speed word is clamped as Bumper_SetPerformance clamps it" );

		table.KartOf( handle )!.Performance = 60;

		var written = table.KartWritten( handle, 180, 1150 )!.Value;

		Assert.AreEqual( (handle, 24 * Cell, 20 * Cell, 6, 1150, 60, 0, 4, -3, 0), (written.Handle, written.X, written.Y, written.Orientation, written.ItemId,
			written.Performance, written.MeshBase, written.MeshCount, written.Duration, written.State) );
		Assert.AreEqual( 0, written.Sections!.Count, "no section until the circuit's walk lays them" );
		CollectionAssert.AreEqual( new[] { 0, 5, 6, 1 }, new[] { 0, 90, 180, 270 }.Select( angle => table.KartWritten( handle, angle, 1150 )!.Value.Orientation ).ToArray() );
	}

	[TestMethod]
	public void AWaterRideHoldsItsTemplatesWords()
	{
		var table = new ParkTrackRideTable();
		var ride = table.KartOf( table.Take( Water ) )!;

		Assert.AreEqual( (50, 0, 1, -1, 0), (ride.Performance, ride.MeshBase, ride.MeshCount, ride.Duration, ride.State) );
	}

	[TestMethod]
	public void ARideWithNoTemplateHereHasNoKartRecord()
	{
		var table = new ParkTrackRideTable();

		foreach ( var type in new[] { -1, -9, -13 } )
			Assert.IsNull( table.KartOf( table.Take( type ) ), $"BumperType {type}" );
	}

	[TestMethod]
	public void TheSectionsTheWalkLaysAreWritten()
	{
		var table = new ParkTrackRideTable();
		var handle = table.Take( Karts );

		table.PlaceKart( handle, 24 * Cell, 20 * Cell, 60 );
		Assert.IsTrue( table.Lay( handle, 0x10009, 30 * Cell, 25 * Cell ) );
		Assert.IsTrue( table.Lay( handle, 7, 34 * Cell, 25 * Cell ) );

		var sections = table.KartWritten( handle, 0, 1150 )!.Value.Sections!;

		CollectionAssert.AreEqual( new[] { (0x10009, 30 * Cell, 25 * Cell), (7, 34 * Cell, 25 * Cell) }, sections.ToArray() );

		table.ClearSections( handle );
		Assert.AreEqual( 0, table.KartWritten( handle, 0, 1150 )!.Value.Sections!.Count );
	}

	[TestMethod]
	public void ASoldKartsRecordGoesAndItsSlotIsMadeAgain()
	{
		var table = new ParkTrackRideTable();
		var handle = table.Take( Karts );

		table.PlaceKart( handle, 24 * Cell, 20 * Cell, 60 );
		table.Free( handle );
		Assert.IsNull( table.KartOf( handle ) );

		var again = table.Take( Water );

		Assert.AreEqual( 0, again & 0xff, "the slot is the first free" );
		Assert.IsFalse( table.KartOf( again )!.Placed, "a record of the template's, not the sold ride's" );
	}

	/// <summary>A ride the file placed keeps the file's words and place: the object's bind finds it placed and leaves it.</summary>
	[TestMethod]
	public void ALoadedKartKeepsTheFilesWordsAndPlace()
	{
		var table = new ParkTrackRideTable( FileWith( TrackBytes.PlayedKarts ) );
		var ride = table.KartOf( TrackBytes.DinoKartsHandle )!;

		Assert.AreEqual( (60 * Cell, 40 * Cell, true), (ride.X, ride.Y, ride.Placed) );

		table.PlaceKart( TrackBytes.DinoKartsHandle, 1, 2, 77 );

		Assert.AreEqual( (60 * Cell, 40 * Cell, 0), (ride.X, ride.Y, ride.Performance), "the file's, not the placer's" );
		Assert.AreEqual( 33, table.SectionsOf( TrackBytes.DinoKartsHandle ).Count );
	}

	[TestMethod]
	public void AKartMadeSinceTheLoadIsPutIn()
	{
		var table = new ParkTrackRideTable();
		var handle = table.Take( Karts );

		table.PlaceKart( handle, 24 * Cell, 20 * Cell, 60 );

		var (how, ride) = table.KartToWrite( handle, 0, 1150, null )!.Value;

		Assert.AreEqual( ParkTrackRideTable.KartWriting.Made, how );
		Assert.AreEqual( handle, ride!.Value.Handle );
		Assert.IsNull( table.KartToWrite( 0x123, 0, 1150, null ), "a handle that names no kart" );
	}

	/// <summary>The file's carless kart with its sections as the file has them takes the five words over where they lie; one with more is written again.</summary>
	[TestMethod]
	public void AKartTheFileHoldsIsWrittenOverOrAgainBySectionsAlone()
	{
		var file = FileWith( TrackBytes.PlayedKarts );
		var table = new ParkTrackRideTable( file );
		var (how, ride) = table.KartToWrite( TrackBytes.DinoKartsHandle, 0, 1150, file )!.Value;

		Assert.AreEqual( ParkTrackRideTable.KartWriting.Over, how );
		Assert.IsNull( ride!.Value.Sections, "the sections are the file's" );

		// A run laid since: not the file's list.
		Assert.IsTrue( table.Lay( TrackBytes.DinoKartsHandle, 9, 90 * Cell, 90 * Cell ) );

		(how, ride) = table.KartToWrite( TrackBytes.DinoKartsHandle, 0, 1150, file )!.Value;

		Assert.AreEqual( ParkTrackRideTable.KartWriting.Whole, how );
		Assert.AreEqual( 34, ride!.Value.Sections!.Count );
	}

	/// <summary>A kart with cars in the file stays the file's: the cars are the file's and its record is not written over them.</summary>
	[TestMethod]
	public void AKartWithCarsInTheFileStaysTheFiles()
	{
		var file = FileWith( TrackBytes.PlayedKarts, car: true );
		var table = new ParkTrackRideTable( file );

		Assert.AreEqual( 1, file.CarsOf( TrackBytes.DinoKartsHandle ) );

		var (how, ride) = table.KartToWrite( TrackBytes.DinoKartsHandle, 0, 1150, file )!.Value;

		Assert.AreEqual( (ParkTrackRideTable.KartWriting.AsTheFile, null), (how, ride) );
	}
}
