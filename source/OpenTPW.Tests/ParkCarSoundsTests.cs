using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// A bumper ride's lead car holds its engine for a go (<see cref="ParkBumperCars.ISounds"/>), and the voice it holds is
/// pitched by a controller its variation names (<see cref="ParkCarSounds"/>). See <c>docs/exe/audio.md</c>, "What an
/// EventMap's slots feed" and "A voice's two controllers".
/// </summary>
[TestClass]
public class ParkCarSoundsTests
{
	[TestInitialize]
	public void StartTheLog() => Log ??= new();

	private const int Centre = 30 * ParkBumperCars.CellUnits + ParkBumperCars.CellUnits / 2;

	/// <summary>Writes down every call, and answers as a live voice would until told its voice has gone.</summary>
	private sealed class Recorder : ParkBumperCars.ISounds
	{
		public readonly List<string> Calls = [];
		public readonly List<(int Ride, int Slot, int Level)> Parameters = [];
		public bool Gone { get; set; }
		private int _next;

		public object? Play( int ride, int slot, int x, int z )
		{
			Calls.Add( $"play {ride:x} slot {slot}" );
			Gone = false;
			return ++_next;
		}

		public bool Move( object voice, int x, int z ) => !Gone;

		public bool SetParameter( object voice, int ride, int slot, int level )
		{
			Parameters.Add( (ride, slot, level) );
			return !Gone;
		}

		public void Fade( object voice ) => Calls.Add( $"fade {voice}" );
	}

	/// <summary>A Hot Pot of four boats, every one with a rider, in its go - the original's measured run.</summary>
	private static (ParkBumperCars Cars, int Handle, Recorder Sounds) AGo()
	{
		var table = new ParkTrackRideTable();
		var handle = table.Take( -1 );
		var cars = table.Cars;
		var sounds = new Recorder();

		cars.Sounds = sounds;
		cars.Place( handle, Centre, Centre );
		cars.OpenForLoading( handle );

		for ( var i = 0; i < 4; ++i )
			cars.Launch( handle );

		for ( var peep = 101; peep < 105; ++peep )
		{
			Assert.IsTrue( cars.Board( handle, peep ) );
			Assert.IsTrue( cars.Fill( handle ) );
		}

		Assert.AreEqual( 0, sounds.Calls.Count, "nothing sounds while the boats load" );

		cars.SetDuration( handle, 25 * ParkBumperCars.TicksPerDurationUnit );
		cars.Start( handle );

		return (cars, handle, sounds);
	}

	/// <summary>
	/// Measured in the original (Q202): the lead boat's handle appears on the go's first tick and is the same handle for
	/// the whole go; when the go ends the boats stay, the unloading arm fades the voice, and the handle goes once the
	/// voice has (8 ticks later there). No other boat ever holds one.
	/// </summary>
	[TestMethod]
	public void TheLeadBoatHoldsOneSoundForTheGoAndFadesItAtTheEnd()
	{
		var (cars, handle, sounds) = AGo();
		var lead = cars.CarsOf( handle ).Single( car => (car.Flags & ParkBumperCars.CarFlags.Lead) != 0 );

		CollectionAssert.AreEqual( new[] { $"play {handle:x} slot {ParkBumperCars.EngineSlot}" }, sounds.Calls,
			"the go's retarget starts the lead's sound, slot 0" );
		Assert.IsNotNull( lead.Voice );
		Assert.IsTrue( cars.CarsOf( handle ).Where( car => car != lead ).All( car => car.Voice is null ), "only the lead" );

		var voice = lead.Voice;

		for ( var tick = 1; tick < 750; ++tick )
		{
			sounds.Parameters.Clear();
			cars.Tick();

			Assert.AreSame( voice, lead.Voice, $"one voice the whole go, tick {tick}" );
			Assert.AreEqual( (handle, ParkBumperCars.SpeedSlot, lead.Speed / 3), sounds.Parameters.Single(),
				$"each step pitches it by slot 10 at the speed / 3, tick {tick}" );
		}

		CollectionAssert.AreEqual( new[] { $"play {handle:x} slot 0" }, sounds.Calls, "no second start though the lead retargets all go" );

		cars.Tick();

		Assert.AreEqual( ParkBumperCars.RideState.Loading, cars.RideOf( handle )!.State );
		Assert.AreEqual( 4, cars.CarCount( handle ), "the boats stay" );
		Assert.AreEqual( "fade 1", sounds.Calls.Last(), "the unloading arm fades it" );
		Assert.AreSame( voice, lead.Voice, "and keeps the handle while the fade runs" );

		sounds.Gone = true;
		cars.Tick();

		Assert.IsNull( lead.Voice, "gone once the voice has" );

		for ( var tick = 0; tick < 100; ++tick )
			cars.Tick();

		Assert.AreEqual( 1, cars.SoundsStarted, "nothing starts while the ride loads" );
	}

	/// <summary>Taking a boat off (<c>FUN_0054ae50</c>) fades its held sound and empties it at once.</summary>
	[TestMethod]
	public void TakingTheLeadOffFadesItsSound()
	{
		var (cars, handle, sounds) = AGo();
		var lead = cars.CarsOf( handle ).Single( car => (car.Flags & ParkBumperCars.CarFlags.Lead) != 0 );

		cars.Empty( handle );

		Assert.AreEqual( "fade 1", sounds.Calls.Last() );
		Assert.IsNull( lead.Voice );
	}

	/// <summary>A voice that has gone answers nought, which empties the car's +0x20; its next retarget starts another.</summary>
	[TestMethod]
	public void AVoiceThatHasGoneIsStartedAgainAtTheNextRetarget()
	{
		var (cars, handle, sounds) = AGo();
		var lead = cars.CarsOf( handle ).Single( car => (car.Flags & ParkBumperCars.CarFlags.Lead) != 0 );

		sounds.Gone = true;
		cars.Tick();

		Assert.IsNull( lead.Voice, "the step's nought empties it" );

		for ( var tick = 0; tick < 700 && lead.Voice is null; ++tick )
			cars.Tick();

		Assert.IsNotNull( lead.Voice, "a later retarget starts it again" );
		Assert.AreEqual( 2, sounds.Calls.Count( call => call.StartsWith( "play" ) ) );
	}

	/// <summary>
	/// The go's end fades the engine every unloading tick while the step still pitches it each tick (the order of
	/// <c>TrackRides_Tick</c>'s first pass): the pitch must not stop the fade, or the engine sounds on after the go
	/// (seen in the game, Q202 run 1). A device is stood in as <see cref="ParkScreamChainTests"/> does.
	/// </summary>
	[TestMethod]
	public void AFadingEngineFadesOnThoughTheStepPitchesIt()
	{
		FileSystem = GameData.Required();

		var ready = typeof( Audio ).GetProperty( nameof( Audio.Ready ) )!;

		Assert.IsFalse( Audio.Ready, "a test run has no audio device" );

		List<Voice> before;

		lock ( Audio.Lock )
			before = [.. Audio.Voices];

		ready.SetValue( null, true );

		ParkAudio? park = null;

		try
		{
			park = new ParkAudio( "jungle" );

			var rides = park.Rides!;
			var held = new ParkCarSounds.Held
			{
				Effect = 194, Header = rides.VariationsOf( 194 )[0], Keys = [(byte)rides.ParameterOf( 194 ), 16, 0, 0]
			};

			held.Voice = Audio.Play( rides.PickFrom( 194, 0 ), 0.68f, loop: true, position: Vector3.Zero )!;
			Assert.IsNotNull( held.Voice );

			var sounds = new ParkCarSounds( new RideScriptScheduler() );

			// Parameter 16, the jungle bumper's slot 10, is the first controller's: it applies.
			sounds.Set( held, 16, 30 );

			Assert.AreEqual( -24 + 30 * 60 / 100, held.Pitch, "the step's 30 pitched it" );
			Assert.IsFalse( held.Voice.Ending );

			sounds.Fade( held );
			sounds.Set( held, 16, 60 );

			Assert.AreEqual( -24 + 60 * 60 / 100, held.Pitch, "the next step pitched it again" );
			Assert.IsTrue( held.Voice.Ending, "and it is still fading" );
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

	/// <summary>
	/// <c>FUN_006d2820</c>'s tables read one step past the pitch: 2^((p ± 1) / 96), checked against the table's own
	/// floats at <c>0x00782f40</c> and <c>0x00783540</c>.
	/// </summary>
	[TestMethod]
	public void APitchIsAFrequencyFromTheTablesOneStepOn()
	{
		Assert.AreEqual( 1f, ParkCarSounds.Rate( 0 ) );
		Assert.AreEqual( 1.0145449638f, ParkCarSounds.Rate( 1 ), 1e-6f, "table entry 1, 0x00782f44" );
		Assert.AreEqual( 1.1978249549f, ParkCarSounds.Rate( 24 ), 1e-5f, "0x00782fa0" );
		Assert.AreEqual( 0.9856629967f, ParkCarSounds.Rate( -1 ), 1e-6f, "0x00783544" );
		Assert.AreEqual( 0.8348469734f, ParkCarSounds.Rate( -24 ), 1e-5f, "0x007835a0" );
	}

	/// <summary>The engine's controller: speed / 3 over -24..36, so a boat at rest is -24 and one at 247 is +25.</summary>
	[TestMethod]
	public void TheSpeedDrivesThePitchOverTheVariationsRange()
	{
		Assert.AreEqual( -24, ParkCarSounds.Controlled( 0, (-24, 36) ) );
		Assert.AreEqual( 25, ParkCarSounds.Controlled( 247 / 3, (-24, 36) ) );
		Assert.AreEqual( 36, ParkCarSounds.Controlled( 100, (-24, 36) ) );
		Assert.AreEqual( 68, ParkCarSounds.Controlled( 50, (68, 68) ) );
	}

	/// <summary>
	/// Jungle's engine, effect 194: one variation whose first controller answers to parameter 16 and drives the pitch
	/// (mask 2) over -24..36, at volume 68 - and the bumper's <c>EventMap</c> names 16 at slot 10 (<c>docs/exe/park.md</c>).
	/// </summary>
	[TestMethod]
	public void JunglesEngineIsPitchedByParameterSixteen()
	{
		FileSystem = GameData.Required();

		var rides = new SoundCategoryFile( "levels/jungle/Sound", "rides" );

		Assert.IsTrue( rides.IsValid );

		var engine = rides.ReadVariations()[rides.IndexOf( 194 )].Single();

		Assert.AreEqual( 16, engine.FirstKey );
		Assert.AreEqual( 2, engine.GapByParameter, "bit 2, the pitch" );
		Assert.AreEqual( 0, engine.SecondKey );
		Assert.AreEqual( (-24, 36), engine.Pitch );
		Assert.AreEqual( (68, 68), engine.Volume );
		Assert.AreEqual( 0, rides.Effects[rides.IndexOf( 194 )].ParameterId, "no zones to choose, so no key 0" );
	}
}
