using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OpenTPW.Tests;

/// <summary>
/// A track ride's record written into a park file's track-rides module, and read back by a load -
/// <see cref="ParkTrackRides.Splice"/>, <see cref="ParkTrackRides.Put"/>, <see cref="ParkBumperCars.Written"/> and
/// <see cref="ParkBumperCars.Restore"/>. <c>docs/exe/saves.md</c>, "OpenTPW's writer, a track ride's record".
/// </summary>
[TestClass]
public class ParkTrackRideWriterTests
{
	private const int HotPotHandle = unchecked((int)0xffffff00);

	/// <summary>The original's own bought Hot Pot, shut: the ride chunk and the close of its file, word for word.</summary>
	private static readonly uint[] TheirHotPot =
	[
		3, 52, 52, 0xffffff00, 0x20400, 0x12c00, 0, 0x474, 0x3c, 1, 1, 0x10fe, 0,
		6, 16, 16, 0xffffff00
	];

	private static readonly SavedTrackRide HotPot = new( HotPotHandle, 0x20400, 0x12c00, 0, 1140, 60, 1, 1, 4350, 0 );

	private static byte[] Chunk( int type, params int[] dwords )
	{
		var size = 12 + (dwords.Length * 4);

		return new[] { type, size, size }.Concat( dwords ).SelectMany( BitConverter.GetBytes ).ToArray();
	}

	/// <summary>A body holding a track-rides module of these chunks after its stamp, filler either side.</summary>
	private static byte[] Body( params byte[][] chunks )
	{
		var children = Chunk( 2, 0x7cf, 9, 3, 22, 0 ).Concat( chunks.SelectMany( chunk => chunk ) ).ToArray();
		var root = new[] { 1, 12, 12 + children.Length }.SelectMany( BitConverter.GetBytes );

		return Encoding.ASCII.GetBytes( "RSYS....SYSR" ).Concat( root ).Concat( children )
			.Concat( Encoding.ASCII.GetBytes( "KARTRYLF" ) ).ToArray();
	}

	/// <summary>A ride of the bumper family in a slot with everything a file holds under it: a section, two cars, a rider's record, both lists and the close.</summary>
	private static byte[][] Whole( int slot, int duration = 750, int state = 2 )
	{
		var handle = slot | (-1 << 8);

		return
		[
			Chunk( 3, handle, 0x20400, 0x12c00, 0, 1140, 60, 1, 1, duration, state ),
			Chunk( 4, handle, 9, 0x1800, 0x1800 ),
			Chunk( 5, [handle, .. new int[43 + 5]] ),
			Chunk( 9, handle, 40, 1 ),
			Chunk( 5, [handle, .. new int[43 + 5]] ),
			Chunk( 7, handle, 41 ),
			Chunk( 8, handle, 42 ),
			Chunk( 6, handle )
		];
	}

	private static byte[] ModuleOf( byte[] body )
	{
		var from = body.AsSpan().IndexOf( "SYSR"u8 ) + 4;

		return body[from..body.AsSpan().IndexOf( "KART"u8 )];
	}

	[TestMethod]
	public void ARidesRecordAndCloseAreTheOriginalsBytes()
	{
		CollectionAssert.AreEqual( TheirHotPot.SelectMany( BitConverter.GetBytes ).ToArray(), ParkTrackRides.RideChunks( HotPot ) );
	}

	[TestMethod]
	public void ARidesFiveWordsAreRead()
	{
		var read = new ParkTrackRides( Body( Whole( 0 ) ) );

		Assert.IsNull( read.Problem );
		Assert.AreEqual( HotPot with { Duration = 750, State = 2 }, read.Rides.Single() );
		Assert.AreEqual( 2, read.CarsOf( HotPotHandle ) );
		Assert.AreEqual( 0, read.CarsOf( 1 | (-1 << 8) ), "another slot's" );
	}

	/// <summary>A ride chunk as short as the ones before the five words were known reads them nought, as the loader's reads do.</summary>
	[TestMethod]
	public void AShortRideChunkReadsItsWordsNought()
	{
		var read = new ParkTrackRides( Body( Chunk( 3, HotPotHandle, 1, 2, 5, 1140, 60, 7 ), Chunk( 6, HotPotHandle ) ) );

		Assert.AreEqual( new SavedTrackRide( HotPotHandle, 1, 2, 5, 1140, 60, 7 ), read.Rides.Single() );
	}

	[TestMethod]
	public void AMadeRideGoesIntoAnEmptyModule()
	{
		var body = Body();
		var spliced = new ParkTrackRides( body ).Splice( body, [], [HotPot] );
		var read = new ParkTrackRides( spliced );

		Assert.IsNull( read.Problem );
		Assert.IsTrue( read.ClosedOnTag, "the root's whole size follows" );
		Assert.AreEqual( HotPot, read.Rides.Single() );
		Assert.AreEqual( 112, ModuleOf( spliced ).Length );
		CollectionAssert.AreEqual( ModuleOf( body )[12..], ModuleOf( spliced )[12..44], "the stamp is carried" );
		CollectionAssert.AreEqual( TheirHotPot.SelectMany( BitConverter.GetBytes ).ToArray(), ModuleOf( spliced )[44..] );
		CollectionAssert.AreEqual( body[..12], spliced[..12], "what lies before the module" );
		CollectionAssert.AreEqual( "KARTRYLF"u8.ToArray(), spliced[^8..], "and after it" );
	}

	[TestMethod]
	public void AGoneRideTakesEveryChunkUnderItsHandle()
	{
		var body = Body( [.. Whole( 0 ), .. Whole( 2 )] );
		var spliced = new ParkTrackRides( body ).Splice( body, [HotPotHandle], [] );
		var read = new ParkTrackRides( spliced );

		Assert.IsNull( read.Problem );
		Assert.AreEqual( 2 | (-1 << 8), read.Rides.Single().Handle );
		Assert.AreEqual( 2, read.CarsOf( 2 | (-1 << 8) ), "the other ride's cars are carried" );
		CollectionAssert.AreEqual( ModuleOf( Body( Whole( 2 ) ) ), ModuleOf( spliced ) );
	}

	/// <summary>The original writes its table slot by slot, so a made ride lies between the rides either side of its slot.</summary>
	[TestMethod]
	public void AMadeRideLiesInItsSlotsPlace()
	{
		var body = Body( [.. Whole( 0 ), .. Whole( 2 )] );
		var between = HotPot with { Handle = 1 | (-1 << 8) };
		var last = HotPot with { Handle = 3 | (-1 << 8) };
		var read = new ParkTrackRides( new ParkTrackRides( body ).Splice( body, [], [last, between] ) );

		Assert.IsNull( read.Problem );
		CollectionAssert.AreEqual( new[] { 0, 1, 2, 3 }, read.Rides.Select( ride => ride.Handle & 0xff ).ToArray() );
		Assert.AreEqual( 0, read.CarsOf( between.Handle ) );
		Assert.AreEqual( 2, read.CarsOf( 2 | (-1 << 8) ) );
	}

	[TestMethod]
	public void ASoldRidesSlotIsTakenByAMadeOne()
	{
		var body = Body( Whole( 0 ) );
		var read = new ParkTrackRides( new ParkTrackRides( body ).Splice( body, [HotPotHandle], [HotPot] ) );

		Assert.AreEqual( HotPot, read.Rides.Single() );
		Assert.AreEqual( 0, read.CarsOf( HotPotHandle ) );
	}

	[TestMethod]
	public void WithNothingMadeOrGoneTheBodyIsItself()
	{
		var body = Body( Whole( 0 ) );

		Assert.AreSame( body, new ParkTrackRides( body ).Splice( body, [], [] ) );
	}

	[TestMethod]
	public void AModuleThatDidNotReadIsRefused()
	{
		var body = Body( Whole( 0 ) );

		BitConverter.TryWriteBytes( body.AsSpan( 12 + 8, 4 ), 4000 );

		var read = new ParkTrackRides( body );

		Assert.IsNotNull( read.Problem );
		Assert.ThrowsException<InvalidOperationException>( () => read.Splice( body, [], [HotPot] ) );
	}

	[TestMethod]
	public void AKeptRidesFiveWordsAreWrittenOver()
	{
		var body = Body( [.. Whole( 0 ), .. Whole( 2 )] );
		var before = (byte[])body.Clone();
		var tracks = new ParkTrackRides( body );
		var running = HotPot with { Performance = 80, MeshBase = 2, MeshCount = 3, Duration = 900, State = 1, X = 7, ItemId = 9 };

		Assert.AreEqual( 1, tracks.Put( body, [running, HotPot with { Handle = 5 | (-1 << 8) }] ), "a ride the file does not hold is skipped" );

		var read = new ParkTrackRides( body );

		Assert.AreEqual( HotPot with { Performance = 80, MeshBase = 2, MeshCount = 3, Duration = 900, State = 1 }, read.Rides[0], "where it stands and its item are the file's" );
		Assert.AreEqual( 750, read.Rides[1].Duration, "the other ride is left" );
		Assert.IsTrue( body.Zip( before ).Select( ( pair, at ) => (pair, at) ).Where( entry => entry.pair.First != entry.pair.Second )
			.All( entry => entry.at is >= 12 + 12 + 32 + 32 and < 12 + 12 + 32 + 52 ), "only the five words change" );
	}

	/// <summary>A section the file holds before its ride's record is not the record: the five words go to the ride chunk alone.</summary>
	[TestMethod]
	public void AKeptRidesWordsGoToItsRideChunkAlone()
	{
		var section = Chunk( 4, HotPotHandle, 9, 0x1800, 0x1800, 0, 0, 0, 0, 0, 0 );
		var body = Body( section, Chunk( 3, HotPotHandle, 0x20400, 0x12c00, 0, 1140, 60, 1, 1, 4350, 0 ), Chunk( 6, HotPotHandle ) );
		var at = body.AsSpan().IndexOf( section );

		Assert.AreEqual( 1, new ParkTrackRides( body ).Put( body, [HotPot with { Duration = 900, State = 1 }] ) );
		CollectionAssert.AreEqual( section, body[at..(at + section.Length)] );
		Assert.AreEqual( (900, 1), (new ParkTrackRides( body ).Rides.Single().Duration, new ParkTrackRides( body ).Rides.Single().State) );
	}

	/// <summary>
	/// The record the running table hands over: the arena's centre less the half cell the laying adds, the
	/// placer's code for the turn, and the record's own performance, mesh words, duration and state.
	/// </summary>
	[TestMethod]
	public void TheRunningRideHandsOverItsRecord()
	{
		var table = new ParkTrackRideTable();
		var handle = table.Take( -1 );

		Assert.AreEqual( HotPotHandle, handle );

		table.Cars.Place( handle, 0x20a00, 0x13200 );
		table.Cars.SetPerformance( handle, 60 );

		Assert.AreEqual( HotPot, table.Cars.Written( handle, 0, 1140 ) );
		CollectionAssert.AreEqual( new[] { 0, 5, 6, 1 }, new[] { 0, 90, 180, 270 }.Select( angle => table.Cars.Written( handle, angle, 1140 )!.Value.Orientation ).ToArray() );

		table.Cars.OpenForLoading( handle );
		table.Cars.SetDuration( handle, 750 );

		Assert.AreEqual( (750, 1), (table.Cars.Written( handle, 0, 1140 )!.Value.Duration, table.Cars.Written( handle, 0, 1140 )!.Value.State) );
		Assert.IsNull( table.Cars.Written( 7 | (-1 << 8), 0, 1140 ), "a handle the table does not hold" );
	}

	/// <summary>A load lays the file's performance, mesh words, duration and state over the ride its template made (<c>FUN_00543560</c>).</summary>
	[TestMethod]
	public void ALoadTakesTheRecordsFiveWords()
	{
		Log ??= new();

		var saved = new ParkTrackRides( Body( Chunk( 3, HotPotHandle, 0x20400, 0x12c00, 0, 1140, 80, 2, 3, 750, 1 ), Chunk( 6, HotPotHandle ) ) );
		var ride = new ParkTrackRideTable( saved ).Cars.RideOf( HotPotHandle )!;

		Assert.AreEqual( (80, 2, 3, 750, ParkBumperCars.RideState.Loading), (ride.Performance, ride.MeshBase, ride.MeshCount, ride.Duration, ride.State) );
		Assert.AreEqual( 8 + ((12 - 8) * 80 / 100), ride.Thrust, "the performance is set, not stored" );
	}
}
