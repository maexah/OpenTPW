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

		Assert.AreEqual( 0, Times( "SCAPE_OMP_SOUND_TYPE_1" ), "the sounds are StartSounds' to hand on or count" );
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
		Assert.AreEqual( 0, Times( "SCAPE_OMP_SOUND_TYPE_1" ) );
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
		Assert.AreEqual( 2, Times( "SCAPE_OMP_SOUND_TYPE_3" ) );
	}

	/// <summary>A file of whole records, each given as its words.</summary>
	private static PlacedObjectsFile Of( params int[][] records )
	{
		var data = new byte[12 + records.Length * 60];

		BinaryPrimitives.WriteUInt32LittleEndian( data, 0x5f4a424f );
		BinaryPrimitives.WriteInt32LittleEndian( data.AsSpan( 4 ), records.Length );
		BinaryPrimitives.WriteInt32LittleEndian( data.AsSpan( 8 ), 60 );

		for ( var record = 0; record < records.Length; ++record )
		{
			for ( var word = 0; word < records[record].Length; ++word )
				BinaryPrimitives.WriteInt32LittleEndian( data.AsSpan( 12 + record * 60 + word * 4 ), records[record][word] );
		}

		return new PlacedObjectsFile( new MemoryStream( data ) );
	}

	[TestMethod]
	public void ATypeOneOfTheGlobalCategoryIsHandedOnWithItsPlaceAndRange()
	{
		// The jungle's waterfall's own words: category 0, effect 8, x 543000, the height 0, z 525500, and 50000.
		var file = Of( [1, 0, 8, 543000, 0, 525500, 50000], [3, 0, 2, 361500, 0, 31750, 0, 99000, -99000] );
		var handed = new System.Collections.Generic.List<ParkPlacedObjects.Sound>();

		Assert.AreEqual( 1, ParkPlacedObjects.StartSounds( file, sound => { handed.Add( sound ); return true; } ) );

		// x and z are the ground's two axes here and the height the third; the range is the word doubled.
		Assert.AreEqual( new ParkPlacedObjects.Sound( 8, new Vector3( 530f, 513f, 0f ), 97f ), handed.Single() );
		Assert.AreEqual( 0, Times( "SCAPE_OMP_SOUND_TYPE_1" ) );
	}

	[TestMethod]
	public void ASoundsPlaceAndRangeAreDividedTowardNought()
	{
		var file = Of( [1, 0, 5, -1500, 2047, 3071, 1535] );
		var handed = new System.Collections.Generic.List<ParkPlacedObjects.Sound>();

		ParkPlacedObjects.StartSounds( file, sound => { handed.Add( sound ); return true; } );

		// -1500 / 1024 is -1, not the -2 a shift gives; 2047 is 1, 3071 is 2; 1535 doubled is 3070, 2.
		Assert.AreEqual( new ParkPlacedObjects.Sound( 5, new Vector3( -1f, 2f, 1f ), 2f ), handed.Single() );
	}

	[TestMethod]
	public void ASoundOfTheLevelsOwnCategoryAndOneRefusedAreCounted()
	{
		var file = Of( [1, 1, 181, 0, 0, 0, 10000], [1, 0, 8, 0, 0, 0, 50000], [1, 0, 7, 0, 0, 0, 50000] );
		var asked = new System.Collections.Generic.List<int>();

		Assert.AreEqual( 1, ParkPlacedObjects.StartSounds( file, sound => { asked.Add( sound.Effect ); return sound.Effect == 7; } ) );

		CollectionAssert.AreEqual( new[] { 8, 7 }, asked, "the level's own is never offered" );
		Assert.AreEqual( 2, Times( "SCAPE_OMP_SOUND_TYPE_1" ) );
	}

	[TestMethod]
	public void TheJunglesWaterfallAndRiverAreHandedOnAndItsLevelsTwoCounted()
	{
		FileSystem = GameData.Required();

		var handed = new System.Collections.Generic.List<ParkPlacedObjects.Sound>();

		Assert.AreEqual( 2, ParkPlacedObjects.StartSounds( "jungle", sound => { handed.Add( sound ); return true; } ) );

		// As the original's voices stand in its memory: the fall at (530, 0, 513) and the river at (511, 0, 664), each 97.
		CollectionAssert.AreEqual( new[]
		{
			new ParkPlacedObjects.Sound( 8, new Vector3( 530f, 513f, 0f ), 97f ),
			new ParkPlacedObjects.Sound( 7, new Vector3( 511f, 664f, 0f ), 97f ),
		}, handed );

		Assert.AreEqual( 2, Times( "SCAPE_OMP_SOUND_TYPE_1" ) );
	}

	[TestMethod]
	public void OnlyALoopedEffectWithSomethingToPlayIsPlayedPlaced()
	{
		FileSystem = GameData.Required();

		var ambient = new SoundCategory( "global", "global/sound", "ambient" );

		// The fall and the river are flagged 0x0008; the gulls, 0x0006, are a type 3's and not a loop.
		Assert.AreEqual( 0x0008, ambient.FlagsOf( 8 ) );
		Assert.AreEqual( 0x0008, ambient.FlagsOf( 7 ) );
		Assert.AreEqual( 0x0006, ambient.FlagsOf( 2 ) );

		Assert.IsTrue( ParkAudio.PlaysPlaced( ambient, 8 ) );
		Assert.IsTrue( ParkAudio.PlaysPlaced( ambient, 7 ) );
		Assert.IsFalse( ParkAudio.PlaysPlaced( ambient, 2 ) );
		Assert.IsFalse( ParkAudio.PlaysPlaced( ambient, 9999 ) );
		Assert.IsFalse( ParkAudio.PlaysPlaced( null, 8 ) );

		// The jungle's own ambient 182 is flagged looped and has no variation: nothing to play.
		var level = new SoundCategory( "levels/jungle", "levels/jungle/Sound", "ambient" );

		Assert.IsTrue( level.IsValid );
		Assert.AreEqual( 0x0008, level.FlagsOf( 182 ) );
		Assert.AreEqual( 0, level.VariationsOf( 182 ).Count );
		Assert.IsFalse( ParkAudio.PlaysPlaced( level, 182 ) );
	}

	/// <summary>
	/// The wiring, with a device stood in as <see cref="VoicePlacementTests"/> stands one in: the jungle's fall and
	/// river become two looped voices of the park's sound, each at its place with its range, the river at half
	/// the fall's volume, and a park that ends takes them with it.
	/// </summary>
	[TestMethod]
	public void TheJunglesFallAndRiverAreLoopedVoicesWithTheirRange()
	{
		FileSystem = GameData.Required();

		var ready = typeof( Audio ).GetProperty( nameof( Audio.Ready ) )!;

		System.Collections.Generic.List<Voice> before;

		lock ( Audio.Lock )
			before = [.. Audio.Voices];

		ready.SetValue( null, true );

		ParkAudio? park = null;

		try
		{
			park = new ParkAudio( "jungle" );

			Assert.AreEqual( 2, ParkPlacedObjects.StartSounds( "jungle", park.StartPlaced ) );
			Assert.AreEqual( 2, Times( "SCAPE_OMP_SOUND_TYPE_1" ) );

			var (fall, river) = (park.Placed[0], park.Placed[1]);

			Assert.AreEqual( (8, 100), (fall.Sound.Effect, fall.Volume) );
			Assert.AreEqual( (7, 50), (river.Sound.Effect, river.Volume) );

			foreach ( var (sound, volume, voice) in park.Placed )
			{
				Assert.IsNotNull( voice );
				Assert.IsTrue( voice!.Loop );
				Assert.AreEqual( AudioBus.Effects, voice.Bus );
				Assert.AreEqual( sound.Place, voice.Place );
				Assert.AreEqual( 97f, voice.Range );
				Assert.AreEqual( volume / 100f * ParkAudio.PlacedVolume, voice.Volume, 1e-6f );
			}

			// The music's level times the options' two defaults, 75 over 60.
			Assert.AreEqual( 0.4125f, ParkAudio.PlacedVolume, 1e-6f );
			Assert.AreEqual( "watfall.mp2", fall.Voice!.Name, true );

			// A second start of the same effect is another voice: the loop holds no delay against it.
			Assert.IsTrue( park.StartPlaced( fall.Sound ) );
			Assert.IsNotNull( park.Placed[2].Voice );

			var voices = park.Placed.Select( placed => placed.Voice! ).ToArray();

			park.Delete();
			Entity.ApplyDeletions();
			park = null;

			foreach ( var voice in voices )
				Assert.IsTrue( voice.Ending, "a park that ends fades its land's sounds out" );
		}
		finally
		{
			park?.Delete();
			Entity.ApplyDeletions();

			lock ( Audio.Lock )
				Audio.Voices.RemoveAll( voice => !before.Contains( voice ) );

			ready.SetValue( null, false );
		}
	}

	/// <summary>A park under the orbit camera is heard from halfway between the eye and the point it looks at.</summary>
	[TestMethod]
	public void TheOrbitCamerasEarsAreHalfwayToThePointItLooksAt()
	{
		var looksAt = ParkOrbitCameraMode.PointOfInterest;

		try
		{
			ParkOrbitCameraMode.PointOfInterest = new Vector3( 530f, 513f, 0f );

			var mode = new ParkOrbitCameraMode();

			Assert.AreNotEqual( mode.Position, ParkOrbitCameraMode.PointOfInterest );
			Assert.AreEqual( (mode.Position + ParkOrbitCameraMode.PointOfInterest) * 0.5f, mode.Ears );
			Assert.AreEqual( mode.Position, ((CameraMode)mode).Position );
			Assert.AreEqual( new Vector3( 1f, 2f, 3f ), new CameraMode { Position = new Vector3( 1f, 2f, 3f ) }.Ears, "any other camera is heard from its eye" );
		}
		finally
		{
			ParkOrbitCameraMode.PointOfInterest = looksAt;
		}
	}
}
