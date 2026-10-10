using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Buffers.Binary;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// A park's <c>scape.omp</c> is read as the level loader reads it, and its particle effect is started in the
/// park's particle system (<c>docs/exe/park.md</c>, "The land's own particles").
/// </summary>
[TestClass]
public class ParkPlacedObjectsTests
{
	private const int WaterFall = 20;

	private ParticleSystem? system;

	[TestInitialize]
	public void Forget()
	{
		Log ??= new();
		Unimplemented.Forget();
	}

	[TestCleanup]
	public void PutAway()
	{
		system?.Shutdown();
		Unimplemented.Forget();
	}

	private static int Times( string what ) => Unimplemented.Summary.Where( entry => entry.What == what ).Sum( entry => entry.Times );

	/// <summary>A file of records <paramref name="size"/> bytes each, every word of record n being n × 100 + its index.</summary>
	private static PlacedObjectsFile Build( uint tag, int count, int size, int claimed = -1, int tail = 8 )
	{
		var data = new byte[12 + count * size + tail];

		BinaryPrimitives.WriteUInt32LittleEndian( data, tag );
		BinaryPrimitives.WriteInt32LittleEndian( data.AsSpan( 4 ), claimed < 0 ? count : claimed );
		BinaryPrimitives.WriteInt32LittleEndian( data.AsSpan( 8 ), size );

		for ( var record = 0; record < count; ++record )
		{
			for ( var word = 0; word < size / 4; ++word )
				BinaryPrimitives.WriteInt32LittleEndian( data.AsSpan( 12 + record * size + word * 4 ), (record + 1) * 100 + word );
		}

		return new PlacedObjectsFile( new MemoryStream( data ) );
	}

	[TestMethod]
	public void ARecordIsItsFirstFifteenWords()
	{
		var file = Build( 0x5f4a424f, 3, 60 );

		Assert.AreEqual( 3, file.Records.Count );
		CollectionAssert.AreEqual( Enumerable.Range( 200, 15 ).ToArray(), file.Records[1].Words );
		Assert.AreEqual( 300, file.Records[2].Type );
		Assert.AreEqual( 301, file.Records[2].Effect );
		Assert.AreEqual( (303, 304, 305), file.Records[2].Place );
	}

	[TestMethod]
	public void ALongerRecordsRestIsSteppedOver()
	{
		var file = Build( 0x5f4a424f, 2, 72 );

		Assert.AreEqual( 2, file.Records.Count );
		Assert.AreEqual( 15, file.Records[1].Words.Length );
		CollectionAssert.AreEqual( Enumerable.Range( 200, 15 ).ToArray(), file.Records[1].Words );
	}

	[TestMethod]
	public void AShorterRecordsRestIsNought()
	{
		var file = Build( 0x5f4a424f, 2, 8 );

		Assert.AreEqual( 2, file.Records.Count );
		CollectionAssert.AreEqual( new[] { 200, 201 }.Concat( new int[13] ).ToArray(), file.Records[1].Words );
	}

	[TestMethod]
	public void AFileThatOpensOnAnotherTagPlacesNothing()
	{
		Assert.AreEqual( 0, Build( 0x4c434e49, 3, 60 ).Records.Count );
		Assert.AreEqual( 0, new PlacedObjectsFile( new MemoryStream( new byte[5] ) ).Records.Count );
	}

	[TestMethod]
	public void ACountPastTheFilesEndStopsAtTheLastWholeRecord()
	{
		Assert.AreEqual( 2, Build( 0x5f4a424f, 2, 60, claimed: 9 ).Records.Count );
	}

	[TestMethod]
	public void ARecordThatEndsTheFileIsRead()
	{
		Assert.AreEqual( 2, Build( 0x5f4a424f, 2, 60, tail: 0 ).Records.Count );
		Assert.AreEqual( 2, Build( 0x5f4a424f, 2, 60, claimed: 3, tail: 59 ).Records.Count );
	}

	[TestMethod]
	public void OnlyTheJunglePlacesAParticle()
	{
		var data = GameData.Required();

		foreach ( var (theme, records) in new[] { ("jungle", 7), ("fantasy", 4), ("hallow", 4), ("space", 4) } )
		{
			using var stream = data.OpenRead( ParkPlacedObjects.PathFor( theme ) );
			var file = new PlacedObjectsFile( stream );

			Assert.AreEqual( records, file.Records.Count, theme );
			Assert.AreEqual( theme == "jungle" ? 1 : 0, file.Records.Count( record => record.Type == PlacedObjectsFile.ParticleType ), theme );
			Assert.IsTrue( file.Records.All( record => record.Type is 1 or 2 or 3 ), theme );
		}
	}

	[TestMethod]
	public void TheJunglesWaterfallIsStartedWhereTheShippedParksEmitterStands()
	{
		FileSystem = GameData.Required();
		system = new ParticleSystem( "Particle/Tp2.plb", 1000 );

		Assert.AreEqual( 1, ParkPlacedObjects.Start( "jungle", system ) );

		var running = system.Emitters.Where( emitter => emitter.Active ).ToArray();

		Assert.AreEqual( 1, running.Length );
		Assert.AreEqual( WaterFall, running[0].Effect );

		// The place in the emitter's own units, 64 to a park unit: the shipped park's own emitter reads (532, 4, 526).
		Assert.AreEqual( (545100 >> 4, 5000 >> 4, 538700 >> 4), (running[0].X, running[0].Y, running[0].Z) );
		Assert.AreEqual( (532, 4, 526), (running[0].X >> 6, running[0].Y >> 6, running[0].Z >> 6) );
		Assert.IsNull( running[0].Aim );

		Assert.AreEqual( 4, Times( "SCAPE_OMP_SOUND_TYPE_1" ) );
		Assert.AreEqual( 2, Times( "SCAPE_OMP_SOUND_TYPE_3" ) );
		Assert.AreEqual( 0, Times( "PARK_PARTICLE_NOT_STARTED" ) );

		// It is endless and throws a particle a tick out at this density, each living 25 to 30 ticks.
		for ( var tick = 0; tick < 200; ++tick )
			system.Tick();

		Assert.IsTrue( running[0].Active );
		Assert.IsTrue( running[0].Count is >= 24 and <= 31, $"{running[0].Count} particles" );
	}

	[TestMethod]
	public void AParkWithNoParticleRecordStartsNone()
	{
		FileSystem = GameData.Required();
		system = new ParticleSystem( "Particle/Tp2.plb", 1000 );

		Assert.AreEqual( 0, ParkPlacedObjects.Start( "fantasy", system ) );
		Assert.IsFalse( system.Emitters.Any( emitter => emitter.Active ) );
		Assert.AreEqual( 3, Times( "SCAPE_OMP_SOUND_TYPE_1" ) );
		Assert.AreEqual( 1, Times( "SCAPE_OMP_SOUND_TYPE_3" ) );
	}

	[TestMethod]
	public void ASystemThatRefusesIsCountedAndNoSystemStartsNothing()
	{
		FileSystem = GameData.Required();
		system = new ParticleSystem( "Particle/Tp2.plb", 1000 );

		for ( var slot = 0; slot < ParticleSystem.EmitterCount; ++slot )
			Assert.AreNotEqual( 0, system.Spawn( WaterFall, 0, 0, 0 ) );

		Assert.AreEqual( 0, ParkPlacedObjects.Start( "jungle", system ) );
		Assert.AreEqual( 1, Times( "PARK_PARTICLE_NOT_STARTED" ) );

		Unimplemented.Forget();

		Assert.AreEqual( 0, ParkPlacedObjects.Start( "jungle", null ) );
		Assert.AreEqual( 0, Times( "PARK_PARTICLE_NOT_STARTED" ) );
		Assert.AreEqual( 4, Times( "SCAPE_OMP_SOUND_TYPE_1" ) );
	}
}
