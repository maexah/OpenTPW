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
		Assert.AreEqual( 0, Times( "SCAPE_OMP_SOUND_TYPE_3" ) );
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
		Assert.AreEqual( 0, Times( "SCAPE_OMP_SOUND_TYPE_3" ) );
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
		Assert.AreEqual( 0, Times( "SCAPE_OMP_SOUND_TYPE_3" ) );
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
		var heardFrom = Listener.GetValue( null );

		try
		{
			park = new ParkAudio( "jungle" );

			// Between the two, within 97 of each over the ground: both in range, so both are given an output at once.
			Audio.SetListener( new Vector3( 520f, 588f, 30f ), Vector3.Right );

			Assert.AreEqual( 2, ParkPlacedObjects.StartSounds( "jungle", park.StartPlaced ) );
			Assert.AreEqual( 2, Times( "SCAPE_OMP_SOUND_TYPE_1" ) );

			var (fall, river) = (park.Placed[0], park.Placed[1]);

			Assert.AreEqual( (8, 100), (fall.Sound.Effect, fall.Volume) );
			Assert.AreEqual( (7, 50), (river.Sound.Effect, river.Volume) );

			foreach ( var (sound, volume, voice) in park.Placed.Select( placed => (placed.Sound, placed.Volume, placed.Voice) ) )
			{
				Assert.IsNotNull( voice );
				Assert.IsTrue( voice!.Loop );
				Assert.AreEqual( AudioBus.Effects, voice.Bus );
				Assert.AreEqual( sound.Place, voice.Place );
				Assert.AreEqual( 97f, voice.Range );
				Assert.AreEqual( ParkAudio.ChannelGain( volume ), voice.Volume, 1e-6f );
			}

			// The music's level times the options' two defaults, 75 over 60.
			Assert.AreEqual( 0.4125f, ParkAudio.ChannelGain( 100 ), 1e-6f );
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

			Listener.SetValue( null, heardFrom );
			ready.SetValue( null, false );
		}
	}

	/// <summary>The engine's listener, put back after a test that moves it.</summary>
	private static readonly System.Reflection.FieldInfo Listener =
		typeof( Audio ).GetField( "_listener", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static )!;

	/// <summary>Runs <paramref name="play"/> with a device stood in and the jungle's park audio up, and takes away what it made.</summary>
	private void WithTheJunglesAudio( Action<ParkAudio> play )
	{
		FileSystem = GameData.Required();

		var ready = typeof( Audio ).GetProperty( nameof( Audio.Ready ) )!;
		System.Collections.Generic.List<Voice> before;

		lock ( Audio.Lock )
			before = [.. Audio.Voices];

		var heardFrom = Listener.GetValue( null );
		ParkAudio? park = null;

		ready.SetValue( null, true );

		try
		{
			park = new ParkAudio( "jungle" );
			play( park );
		}
		finally
		{
			park?.Delete();
			Entity.ApplyDeletions();

			lock ( Audio.Lock )
				Audio.Voices.RemoveAll( voice => !before.Contains( voice ) );

			Listener.SetValue( null, heardFrom );
			Audio.HoldPlaced( false );
			ready.SetValue( null, false );
		}
	}

	private static void HearFrom( float x, float y, float height = 30f ) => Audio.SetListener( new Vector3( x, y, height ), Vector3.Right );

	/// <summary>
	/// <b>A placed looped voice out of range gives its output up, and in range again is given a fresh one</b>
	/// (<c>0x006bca10</c> -> <c>0x006bca70</c>, <c>0x006bbe40</c>; <c>OneShot_Tick</c> <c>0x006bd800</c> -> <c>0x006bc410</c>):
	/// the fall at (530, 513), range 97, set up with the listener at the world's corner has no output; in range it is given
	/// one; 98 off over the ground it is stopped and let go; 90 off over the ground and 500 up it is in range, the height
	/// left out; exactly 97 off is in range; and each return is a new output from the sample's start, not the old one resumed.
	/// The held draw is not seen here: the shipped effect 8 has one variation of one sample at volume 100, so a fresh draw
	/// at each return gives the same sample and volume.
	/// </summary>
	[TestMethod]
	public void APlacedLoopOutOfRangeReleasesItsOutputAndComesBackWithAFreshOne()
	{
		WithTheJunglesAudio( park =>
		{
			HearFrom( 0f, 0f );
			Assert.AreEqual( 2, ParkPlacedObjects.StartSounds( "jungle", park.StartPlaced ) );

			var fall = park.Placed[0];

			Assert.AreEqual( 8, fall.Sound.Effect );
			Assert.AreEqual( (true, 0, (Voice?)null), (fall.Away, fall.Outputs, fall.Voice), "set up away: no output" );

			HearFrom( 540f, 513f );
			park.ServePlaced( fall, Audio.Listener.Position );

			var first = fall.Voice!;

			Assert.AreEqual( (false, 1, 0), (fall.Away, fall.Outputs, fall.Released), "in range: its first output" );
			Assert.IsTrue( first.Playing && first.Loop );
			Assert.AreEqual( "watfall.mp2", first.Name, true );

			park.ServePlaced( fall, Audio.Listener.Position );
			Assert.AreSame( first, fall.Voice, "in range and sounding: the same output plays on" );

			HearFrom( 530f + 90f, 513f, 500f );
			park.ServePlaced( fall, Audio.Listener.Position );
			Assert.AreSame( first, fall.Voice, "90 off over the ground and 500 up: in range, the height left out" );

			HearFrom( 530f + 98f, 513f );
			park.ServePlaced( fall, Audio.Listener.Position );
			Assert.AreEqual( (true, (Voice?)null, 1), (fall.Away, fall.Voice, fall.Released), "98 off: away, its output given up" );
			Assert.IsFalse( first.Playing, "the output is stopped, not left to play on unheard" );

			park.ServePlaced( fall, Audio.Listener.Position );
			Assert.AreEqual( (1, 1), (fall.Outputs, fall.Released), "away: nothing more" );

			HearFrom( 530f, 513f - 97f );
			park.ServePlaced( fall, Audio.Listener.Position );

			var second = fall.Voice!;

			Assert.AreEqual( (false, 2), (fall.Away, fall.Outputs), "exactly the range off: in range again, a second output" );
			Assert.AreNotSame( first, second, "a fresh output, not the old one resumed" );
			Assert.IsTrue( second.Playing && second.Loop );
			Assert.AreEqual( first.Name, second.Name, "on the sample its set-up drew" );
			Assert.AreEqual( first.Volume, second.Volume, 1e-6f );
		} );
	}

	/// <summary>
	/// The frame's pass serves every one of the land's looped sounds, and the placed voices of no script whose leaving range
	/// is not built are counted each time one leaves while sounding, not once a frame and not while in range: a yawn at the
	/// fall is counted once as the listener leaves, not again while it stays away, and again after it comes back and goes.
	/// </summary>
	[TestMethod]
	public void TheParksPassServesTheLoopsAndCountsTheVoicesThatPlayOn()
	{
		WithTheJunglesAudio( park =>
		{
			HearFrom( 540f, 513f );
			ParkPlacedObjects.StartSounds( "jungle", park.StartPlaced );

			var fall = park.Placed[0];

			Assert.IsTrue( park.Yawn( new Vector3( 530f, 513f, 0f ) ) );

			const string Gap = "PLACED_VOICE_OUT_OF_RANGE_YAWN";

			park.Update();
			Assert.AreEqual( 0, Times( Gap ), "in range: nothing counted" );

			HearFrom( 2000f, 2000f );
			park.Update();
			park.Update();
			Assert.AreEqual( 1, Times( Gap ), "left range while sounding: counted once" );
			Assert.AreEqual( (true, (Voice?)null), (fall.Away, fall.Voice), "the frame's pass gave the fall's output up" );

			HearFrom( 540f, 513f );
			park.Update();
			Assert.AreEqual( 2, fall.Outputs, "and gave it a fresh one on the return" );

			HearFrom( 2000f, 2000f );
			park.Update();
			Assert.AreEqual( 2, Times( Gap ), "left again: counted again" );
		} );
	}

	/// <summary>
	/// <b>Each placed voice of no script whose leaving range is not built is counted under its own kind</b> (Q1416): started at
	/// the fall with the listener there, it is counted once when the listener goes 2,000 off. The gulls' and the sea's
	/// (<c>FOLLOWING</c>) are placed beside the listener wherever it goes, so they are not reached this way.
	/// </summary>
	/// <remarks><b>Mutations:</b> any one kind's <c>PlaysOnOutOfRange</c> call deleted, or given another kind's name.</remarks>
	[DataTestMethod]
	[DataRow( "THUNDER" )]
	[DataRow( "SINGLE_SCREAM" )]
	[DataRow( "PUT_OFF" )]
	[DataRow( "STAFF" )]
	[DataRow( "YAWN" )]
	[DataRow( "CHARGE" )]
	[DataRow( "BUMPER_ENGINE" )]
	public void EachKindOfVoiceThatPlaysOnIsCountedUnderItsOwnName( string kind )
	{
		WithTheJunglesAudio( park =>
		{
			var at = new Vector3( 530f, 513f, 0f );
			HearFrom( 540f, 513f );

			var started = kind switch
			{
				"THUNDER" => park.Thunder( at ),
				"SINGLE_SCREAM" => park.SingleScream( 1, -1, at ),
				"PUT_OFF" => park.PutOff( at ),
				"STAFF" => park.StaffSound( 0xa9, at ),
				"YAWN" => park.Yawn( at ),
				"CHARGE" => park.Charge( at ),
				_ => new ParkCarSounds( new RideScriptScheduler() ).Start( park.Rides!, 194, at ) is not null,
			};
			Assert.IsTrue( started, $"{kind} started" );

			park.CountPlayingOn();
			Assert.AreEqual( 0, Times( $"PLACED_VOICE_OUT_OF_RANGE_{kind}" ), "in range: nothing counted" );

			HearFrom( 2540f, 513f );
			park.CountPlayingOn();
			Assert.AreEqual( 1, Times( $"PLACED_VOICE_OUT_OF_RANGE_{kind}" ), "left range while sounding: counted once" );
		} );
	}

	[TestMethod]
	public void ATypeThreeOfTheGlobalCategoryIsHandedOnWithItsRectangle()
	{
		// The jungle's gulls' own words, and a made one whose words do not divide: -1500 is -1, 2047 is 1.
		var file = Of( [3, 0, 2, 362100, 0, 32100, 80000, 100000, -100000], [3, 0, 9, -1500, 0, 2047, 0, 2047, -1500],
			[3, 1, 4, 0, 0, 0, 0, 1024, 1024] );
		var handed = new System.Collections.Generic.List<ParkPlacedObjects.Following>();

		Assert.AreEqual( 2, ParkPlacedObjects.StartSounds( file, _ => false, sound => { handed.Add( sound ); return true; } ) );

		CollectionAssert.AreEqual( new[]
		{
			new ParkPlacedObjects.Following( 2, 353, 31, 450, -66 ),
			new ParkPlacedObjects.Following( 9, -1, 1, 0, 0 ),
		}, handed, "the level's own is never offered" );
		Assert.AreEqual( 1, Times( "SCAPE_OMP_SOUND_TYPE_3" ) );
		Assert.AreEqual( 0, Times( "SCAPE_OMP_SOUND_TYPE_1" ) );
	}

	[TestMethod]
	public void ATypeThreeRefusedOrWithNobodyToTakeItIsCounted()
	{
		var file = Of( [3, 0, 2, 0, 0, 0, 0, 1024, 1024], [3, 0, 3, 0, 0, 0, 0, 1024, 1024] );

		Assert.AreEqual( 1, ParkPlacedObjects.StartSounds( file, _ => true, sound => sound.Effect == 3 ) );
		Assert.AreEqual( 1, Times( "SCAPE_OMP_SOUND_TYPE_3" ) );

		Unimplemented.Forget();

		Assert.AreEqual( 0, ParkPlacedObjects.StartSounds( file, _ => true ) );
		Assert.AreEqual( 2, Times( "SCAPE_OMP_SOUND_TYPE_3" ) );
	}

	[TestMethod]
	public void EveryParksSeaAndTheJunglesGullsAreHandedOn()
	{
		FileSystem = GameData.Required();

		System.Collections.Generic.List<ParkPlacedObjects.Following> Of( string theme )
		{
			var handed = new System.Collections.Generic.List<ParkPlacedObjects.Following>();

			ParkPlacedObjects.StartSounds( theme, _ => true, sound => { handed.Add( sound ); return true; } );

			return handed;
		}

		// As the original's voices' rectangles stand in its memory.
		CollectionAssert.AreEqual( new[]
		{
			new ParkPlacedObjects.Following( 2, 353, 31, 450, -66 ),
			new ParkPlacedObjects.Following( 3, 372, 21, 567, -174 ),
		}, Of( "jungle" ) );
		CollectionAssert.AreEqual( new[] { new ParkPlacedObjects.Following( 3, 370, 29, 565, -166 ) }, Of( "fantasy" ) );
		CollectionAssert.AreEqual( new[] { new ParkPlacedObjects.Following( 3, 371, 32, 566, -163 ) }, Of( "hallow" ) );
		CollectionAssert.AreEqual( new[] { new ParkPlacedObjects.Following( 3, 373, 31, 568, -164 ) }, Of( "space" ) );
		Assert.AreEqual( 0, Times( "SCAPE_OMP_SOUND_TYPE_3" ) );
	}

	[TestMethod]
	public void AFollowingVoiceStandsAtTheListenersPlaceHeldInsideItsRectangle()
	{
		var gulls = new ParkPlacedObjects.Following( 2, 353, 31, 450, -66 );

		// Inside, it is the listener's own place; outside, the nearest point of the rectangle, at the listener's height.
		Assert.AreEqual( new Vector3( 400f, 10f, 47f ), gulls.Beside( new Vector3( 400f, 10f, 47f ) ) );
		Assert.AreEqual( new Vector3( 400f, 31f, 47f ), gulls.Beside( new Vector3( 400f, 229f, 47f ) ) );
		Assert.AreEqual( new Vector3( 450f, 31f, 5f ), gulls.Beside( new Vector3( 546f, 476f, 5f ) ) );
		Assert.AreEqual( new Vector3( 353f, -66f, 0f ), gulls.Beside( new Vector3( 0f, -500f, 0f ) ) );

		// The same whichever end of a side the record names first.
		Assert.AreEqual( new Vector3( 450f, 31f, 5f ), new ParkPlacedObjects.Following( 2, 450, -66, 353, 31 ).Beside( new Vector3( 546f, 476f, 5f ) ) );
	}

	[TestMethod]
	public void OnlyAnEffectOfThePlainSampleAfterSampleClassFollows()
	{
		FileSystem = GameData.Required();

		var ambient = new SoundCategory( "global", "global/sound", "ambient" );

		// The gulls and the sea are flagged 0x0006; the fall is a loop, and the rain, 0x0606, the class a parameter drives.
		Assert.AreEqual( 0x0006, ambient.FlagsOf( 2 ) );
		Assert.AreEqual( 0x0006, ambient.FlagsOf( 3 ) );
		Assert.AreEqual( 0x0606, ambient.FlagsOf( 33 ) );

		Assert.IsTrue( ParkAudio.PlaysFollowing( ambient, 2 ) );
		Assert.IsTrue( ParkAudio.PlaysFollowing( ambient, 3 ) );
		Assert.IsFalse( ParkAudio.PlaysFollowing( ambient, 8 ) );
		Assert.IsFalse( ParkAudio.PlaysFollowing( ambient, 33 ) );
		Assert.IsFalse( ParkAudio.PlaysFollowing( ambient, 9999 ) );
		Assert.IsFalse( ParkAudio.PlaysFollowing( null, 2 ) );

		// What the voices are made of: the gulls one variation of two samples at 49, a pitch of -2 to 2 and a wait of
		// up to ten seconds; the sea three of one sample, at 33, 55 and 55, each heard a hundred off.
		var gulls = ambient.VariationsOf( 2 ).Single();

		Assert.AreEqual( (2, (49, 49), (-2, 2), 0, 10000, 100f), (gulls.Samples, gulls.Volume, gulls.Pitch, gulls.GapMin, gulls.GapMax, gulls.Range) );
		CollectionAssert.AreEqual( new[] { 33, 55, 55 }, ambient.VariationsOf( 3 ).Select( header => header.Volume.Low ).ToArray() );
		Assert.IsTrue( ambient.VariationsOf( 3 ).All( header => header is { Samples: 1, GapMin: 0, GapMax: 0, Range: 100f } ) );
	}

	private static SoundCategoryFile.Variation Weighing( long weight ) => new( 1, 0, 0, 0, weight, [] );

	[TestMethod]
	public void AFollowingVoicesVariationIsDrawnByWeightAndNeverTheOneBeforeOfThree()
	{
		var random = new Random( 7 );
		var three = new[] { Weighing( 100 ), Weighing( 300 ), Weighing( 600 ) };
		var first = new int[3];

		for ( var draw = 0; draw < 10000; ++draw )
			++first[ParkAudio.AnyVariation( three, -1, random )];

		// A voice's first: by weight alone.
		Assert.IsTrue( first[0] is > 850 and < 1150 && first[1] is > 2800 and < 3200 && first[2] is > 5800 and < 6200, string.Join( ", ", first ) );

		// A later one: the one before is never drawn again, and its draws go to the next round.
		var later = new int[3];

		for ( var draw = 0; draw < 10000; ++draw )
			++later[ParkAudio.AnyVariation( three, 2, random )];

		Assert.AreEqual( 0, later[2] );
		Assert.IsTrue( later[0] is > 6800 and < 7200 && later[1] is > 2800 and < 3200, string.Join( ", ", later ) );

		for ( var draw = 0; draw < 200; ++draw )
			Assert.AreNotEqual( 0, ParkAudio.AnyVariation( three, 0, random ) );

		// Of two the one before may come again; one is that one, and none is none.
		var two = new[] { Weighing( 500 ), Weighing( 500 ) };

		Assert.IsTrue( Enumerable.Range( 0, 200 ).Any( _ => ParkAudio.AnyVariation( two, 0, random ) == 0 ) );
		Assert.IsTrue( Enumerable.Range( 0, 200 ).Any( _ => ParkAudio.AnyVariation( two, 0, random ) == 1 ) );
		Assert.AreEqual( 0, ParkAudio.AnyVariation( [Weighing( 5 )], 0, random ) );
		Assert.AreEqual( -1, ParkAudio.AnyVariation( [], -1, random ) );
	}

	/// <summary>Answers each roll it is given, in turn, whatever the bound.</summary>
	private sealed class Rolls( params long[] rolls ) : Random
	{
		private int _next;

		public override long NextInt64( long maxValue ) => rolls[_next++];
	}

	/// <summary>
	/// <b>A following voice's draw takes the first variation whose running total of weights is at least the roll</b>
	/// (<c>0x006bc86d</c> <c>add</c>, <c>0x006bc870</c> <c>cmp</c>, <c>jae</c>): of weights 3, 5 and 7 the roll 3 is the
	/// first's, 4 and 8 the second's and 9 the third's. Taking one only once the total is past the roll gives the second
	/// at 3 and the third at 8.
	/// </summary>
	[TestMethod]
	public void AFollowingVoicesDrawTakesTheFirstVariationWhoseTotalReachesTheRoll()
	{
		var three = new[] { Weighing( 3 ), Weighing( 5 ), Weighing( 7 ) };

		CollectionAssert.AreEqual( new[] { 0, 0, 1, 1, 2, 2 },
			new long[] { 0, 3, 4, 8, 9, 14 }.Select( roll => ParkAudio.AnyVariation( three, -1, new Rolls( roll ) ) ).ToArray() );
	}

	/// <summary>
	/// The wiring, with a device stood in: the jungle's gulls and sea become two voices of the park's sound that play
	/// one sample after another beside the listener, each at the volume and pitch its first drew, the gulls with a
	/// silence after every sample, the first included (<c>FUN_006bc410</c> -> <c>0x006bdf00</c>).
	/// </summary>
	[TestMethod]
	public void TheJunglesGullsAndSeaPlaySampleAfterSampleBesideTheListener()
	{
		FileSystem = GameData.Required();

		var ready = typeof( Audio ).GetProperty( nameof( Audio.Ready ) )!;
		var now = Time.Now;
		var looksAt = ParkOrbitCameraMode.PointOfInterest;

		Camera.SetCameraMode<ParkOrbitCameraMode>();
		ParkOrbitCameraMode.PointOfInterest = new Vector3( 400f, 60f, 0f );

		System.Collections.Generic.List<Voice> before;

		lock ( Audio.Lock )
			before = [.. Audio.Voices];

		ready.SetValue( null, true );

		ParkAudio? park = null;

		try
		{
			park = new ParkAudio( "jungle" );
			Time.Now = 1000f;

			Assert.AreEqual( 4, ParkPlacedObjects.StartSounds( "jungle", park.StartPlaced, park.StartFollowing ) );
			Assert.AreEqual( 0, Times( "SCAPE_OMP_SOUND_TYPE_3" ) );
			Assert.AreEqual( 2, park.Following.Count );

			var (gulls, sea) = (park.Following[0], park.Following[1]);

			Assert.AreEqual( (2, 3), (gulls.Sound.Effect, sea.Sound.Effect) );
			Assert.IsNull( gulls.Voice, "the first sample starts on the park's next pass" );

			park.Update();

			// The first sample: a silence of 0 to 10 s after it, the variation's volume, a pitch drawn from -2 up to 1.
			Assert.AreEqual( (0, 49, 1), (gulls.Variation, gulls.Kept.Volume, gulls.Samples) );
			Assert.IsTrue( gulls.Wait is >= 0 and < 10000, $"wait {gulls.Wait}" );
			Assert.IsTrue( gulls.Kept.Pitch is >= -2 and <= 1, $"pitch {gulls.Kept.Pitch}" );
			Assert.IsTrue( sea.Kept.Volume is 33 or 55 );
			Assert.AreEqual( (1, 0, 0), (sea.Samples, sea.Kept.Pitch, sea.Wait) );

			foreach ( var following in park.Following )
			{
				var voice = following.Voice!;

				Assert.IsFalse( voice.Loop );
				Assert.AreEqual( AudioBus.Effects, voice.Bus );
				Assert.AreEqual( 100f, voice.Range );
				Assert.AreEqual( ParkAudio.ChannelGain( following.Kept.Volume ), voice.Volume, 1e-6f );
				Assert.AreEqual( ParkCarSounds.Rate( following.Kept.Pitch ), (float)voice.Rate, 1e-6f );

				// Wherever the listener is, the nearest point of the rectangle to it.
				Assert.AreEqual( following.Sound.Beside( Camera.Ears ), voice.Place );
			}

			Assert.AreEqual( 0.2035f, ParkAudio.ChannelGain( 49 ), 1e-6f, "37 of 127 beside the music's 60" );
			Assert.AreEqual( 0.1375f, ParkAudio.ChannelGain( 33 ), 1e-6f, "25 of 127" );

			// While a sample plays nothing new starts, and the voice goes where the listener does.
			var (stood, heardFrom) = (gulls.Voice!.Place, Camera.Ears);

			ParkOrbitCameraMode.PointOfInterest = new Vector3( 500f, 0f, 0f );
			park.Update();

			Assert.AreEqual( (1, 1), (gulls.Samples, sea.Samples) );
			Assert.AreNotEqual( heardFrom, Camera.Ears );
			Assert.AreNotEqual( stood, gulls.Voice!.Place );

			foreach ( var following in park.Following )
				Assert.AreEqual( following.Sound.Beside( Camera.Ears ), following.Voice!.Place );

			var kept = (gulls.Kept, sea.Kept);
			var waited = false;

			for ( var sample = 2; sample <= 40; ++sample )
			{
				var (lastGull, lastSea, variation, wait) = (gulls.Voice!, sea.Voice!, sea.Variation, gulls.Wait);

				lastGull.Stop();
				lastSea.Stop();
				park.Update();

				// The sea has no wait: its next sample at once, of another variation, at the voice's own volume.
				Assert.AreEqual( sample, sea.Samples );
				Assert.AreNotSame( lastSea, sea.Voice );
				Assert.AreNotEqual( variation, sea.Variation, "of three, never the one before" );
				Assert.AreEqual( kept.Item2, sea.Kept );
				Assert.AreEqual( ParkAudio.ChannelGain( kept.Item2.Volume ), sea.Voice!.Volume, 1e-6f );
				Assert.AreEqual( 0, sea.Wait );

				// Each gull, the first included, is followed by its own silence, under ten seconds.
				if ( wait > 100 )
				{
					waited = true;

					Assert.AreSame( lastGull, gulls.Voice, "silent for its wait" );

					Time.Now += (wait / 1000f) - 0.05f;
					park.Update();
					Assert.AreSame( lastGull, gulls.Voice, "to the wait's end" );

					Time.Now += 0.1f;
					park.Update();
				}
				else
				{
					Time.Now += 0.2f;
					park.Update();
				}

				Assert.AreNotSame( lastGull, gulls.Voice );
				Assert.AreEqual( sample, gulls.Samples );
				Assert.AreEqual( sample, sea.Samples, "a pass with a sample playing starts none" );
				Assert.AreEqual( kept.Item1, gulls.Kept, "every sample at what the first drew" );
				Assert.AreEqual( ParkAudio.ChannelGain( 49 ), gulls.Voice!.Volume, 1e-6f );
				Assert.AreEqual( ParkCarSounds.Rate( kept.Item1.Pitch ), (float)gulls.Voice!.Rate, 1e-6f );
				Assert.IsTrue( gulls.Wait is >= 0 and < 10000, $"wait {gulls.Wait}" );
			}

			Assert.IsTrue( waited );

			// A voice's first variation is a draw too: of forty more seas, some start on the 33 and some on a 55.
			for ( var more = 0; more < 40; ++more )
				Assert.IsTrue( park.StartFollowing( sea.Sound ) );

			// And a first gull's silence is drawn: of forty more gulls, every one waits a draw of 0 to 10 s.
			for ( var more = 0; more < 40; ++more )
				Assert.IsTrue( park.StartFollowing( gulls.Sound ) );

			park.Update();

			var firsts = park.Following.Skip( 2 ).Take( 40 ).Select( following => (following.Samples, following.Kept.Volume) ).ToArray();

			Assert.IsTrue( firsts.All( first => first.Samples == 1 ) );
			Assert.IsTrue( firsts.Any( first => first.Volume == 33 ) && firsts.Any( first => first.Volume == 55 ), "both volumes among forty starts" );

			var gullWaits = park.Following.Skip( 42 ).Select( following => (following.Samples, following.Wait) ).ToArray();

			Assert.AreEqual( 40, gullWaits.Length );
			Assert.IsTrue( gullWaits.All( first => first.Samples == 1 && first.Wait is >= 0 and < 10000 ) );
			Assert.IsTrue( gullWaits.Count( first => first.Wait > 1000 ) > 20, "most first silences are seconds long" );

			var voices = park.Following.Select( following => following.Voice! ).ToArray();

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
			Time.Now = now;
			ParkOrbitCameraMode.PointOfInterest = looksAt;
			Camera.SetCameraMode<CameraMode>();
		}
	}

	/// <summary>
	/// <b>Through a park's menu the land's following voices draw their samples on time and let them pass unheard.</b>
	/// The original's service runs on through a pause and its mixer plays them at nought (<c>docs/exe/audio.md</c>, "The
	/// pause substitution"); ours holds a placed voice, so a sample due under the hold starts no voice, which would be
	/// born held and let go with the rest on resume. The jungle's sea, with no silence between its samples: a minute held
	/// starts nothing, and after the resume its next is heard once the unheard one's time is past.
	/// </summary>
	[TestMethod]
	public void AHeldParksFollowingVoicesLetTheirSamplesPassUnheard()
	{
		FileSystem = GameData.Required();

		var ready = typeof( Audio ).GetProperty( nameof( Audio.Ready ) )!;
		var now = Time.Now;
		var looksAt = ParkOrbitCameraMode.PointOfInterest;

		Camera.SetCameraMode<ParkOrbitCameraMode>();
		ParkOrbitCameraMode.PointOfInterest = new Vector3( 400f, 60f, 0f );

		System.Collections.Generic.List<Voice> before;

		lock ( Audio.Lock )
			before = [.. Audio.Voices];

		int Voices()
		{
			lock ( Audio.Lock )
				return Audio.Voices.Count;
		}

		ready.SetValue( null, true );

		ParkAudio? park = null;

		try
		{
			park = new ParkAudio( "jungle" );
			Time.Now = 1000f;

			ParkPlacedObjects.StartSounds( "jungle", park.StartPlaced, park.StartFollowing );

			var sea = park.Following[1];

			Assert.AreEqual( 3, sea.Sound.Effect );

			park.Update();
			Assert.AreEqual( 1, sea.Samples );

			GameClock.Update( paused: true, GameClock.ParkCatchUp );
			park.Update();

			var voices = Voices();

			sea.Voice!.Stop();
			park.Update();

			Assert.AreEqual( (1, 1), (sea.Samples, sea.Unheard), "drawn, and no voice started" );
			Assert.IsNull( sea.Voice );
			Assert.IsTrue( sea.EndedAt > Time.Now, "it lasts as its sample would" );

			for ( var pass = 0; pass < 6000; ++pass )
			{
				Time.Now += 0.01f;
				park.Update();
			}

			Assert.AreEqual( 1, sea.Samples, "a minute held, and not one sample heard" );
			Assert.AreEqual( voices, Voices(), "and none waits in the pool" );
			Assert.IsTrue( sea.Unheard > 1, $"{sea.Unheard} drawn on time" );

			var due = sea.EndedAt!.Value;

			GameClock.Update( paused: false, GameClock.ParkCatchUp );
			park.Update();
			Assert.AreEqual( 1, sea.Samples, "the unheard one's time still to run" );

			Time.Now = due;
			park.Update();

			Assert.AreEqual( 2, sea.Samples, "the next is heard" );
			Assert.IsFalse( sea.Voice!.IsHeld );
		}
		finally
		{
			park?.Delete();
			Entity.ApplyDeletions();

			lock ( Audio.Lock )
				Audio.Voices.RemoveAll( voice => !before.Contains( voice ) );

			Audio.HoldPlaced( false );
			GameClock.Rebase();
			ready.SetValue( null, false );
			Time.Now = now;
			ParkOrbitCameraMode.PointOfInterest = looksAt;
			Camera.SetCameraMode<CameraMode>();
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

	/// <summary>
	/// <c>ParkAudio.OnDelete</c>: a park's sound that ends stops being <see cref="ParkAudio.Current"/> only where it
	/// is the current one. Two are made, so the second is current (the constructor's first line); the first ending
	/// leaves the second current, and the second ending leaves none. Needs no device and none of the game's files:
	/// with no device the constructor loads nothing.
	/// </summary>
	[TestMethod]
	public void AParkThatEndsIsNoLongerTheCurrentSoundAndAnOlderOneEndingLeavesTheNewerCurrent()
	{
		Assert.IsFalse( Audio.Ready, "a test run has no audio device" );

		ParkAudio? older = null;
		ParkAudio? newer = null;

		try
		{
			older = new ParkAudio( "jungle" );
			newer = new ParkAudio( "jungle" );

			Assert.AreSame( newer, ParkAudio.Current, "the one made last is the current one" );

			older.Delete();
			Assert.AreSame( newer, ParkAudio.Current, "an older park ending does not take the newer one's place away" );

			newer.Delete();
			Assert.IsNull( ParkAudio.Current, "the current one ending leaves none" );
		}
		finally
		{
			older?.Delete();
			newer?.Delete();
			Entity.ApplyDeletions();
		}
	}
}
