using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace OpenTPW.Tests;

/// <summary>
/// The sweep's tail in the park's loop (<see cref="ParkLoop"/>): after each sweep the calendar's compare and its turns, as the
/// original's tail compares at the end of every sweep (<c>0x00516695</c> -> <c>0x004d7b3b</c>), then the weather's turn. Driven
/// through Lost Kingdom's file a frame at a time, as <see cref="GameClock"/> hands the loop its ticks.
/// </summary>
[TestClass]
[DoNotParallelize]
public class ParkSweepTailTests
{
	/// <summary>A ride of the stock park, whose day rings the probe counts into.</summary>
	private const int Ride = 13;

	[TestInitialize]
	public void HaveALog() => Log ??= new();

	[TestCleanup]
	public void LeaveTheCalendar() => GameCalendar.Rebase();

	private sealed record Park( ParkState State, ParkPeople People, ParkWeather Weather );

	/// <summary>
	/// The stock park on its own clock, with a weather; <paramref name="probe"/> is asked in every object's turn of every sweep.
	/// </summary>
	private static Park Open( System.Action<int>? probe = null )
	{
		var data = GameData.Required();
		FileSystem = data;

		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		var world = new ParkWorld( new SaveReader( stream ).ReadFile() );
		var state = new ParkState( world );
		GameCalendar.Rebase( state.GameTick, world.Calendar );

		var people = new ParkPeople( world, new ParkBalance( "jungle", easyMode: true ), state: state, scriptFor: id =>
		{
			probe?.Invoke( id );
			return null;
		}, random: new Random( 1 ), behaviourRandom: new Random( 2 ), rideRandom: new Random( 3 ), staffRandom: new Random( 4 ) );

		return new Park( state, people, new ParkWeather( null, drawn: false ) );
	}

	private static void Close( Park? park )
	{
		park?.People.Delete();
		park?.Weather.Delete();
		Entity.ApplyDeletions();
	}

	/// <summary>
	/// One frame of <paramref name="ticks"/> 31 ms ticks through the loop, the calendar turned as the level turns it. The frame
	/// is asked for half a tick over what the clock already holds, so its float remainder never takes a tick off.
	/// </summary>
	private static void Frame( Park park, int ticks, List<(int Tick, bool Day)>? compares = null )
	{
		SimulationClockScope.Frame( (ticks + 0.5f - GameClock.PartialTick) * GameClock.TickSeconds );
		Assert.AreEqual( ticks, GameClock.TicksDue );

		ParkLoop.Run( null, null, park.People, people =>
		{
			Level.TurnTheCalendar( park.State, people, null, _ => { } );
			compares?.Add( (park.State.GameTick, GameCalendar.DayRolled) );
		}, park.Weather );
	}

	/// <summary>
	/// <b>A day that turns on a frame's second sweep rolls the rings before its third</b>: Lost Kingdom's file stands on tick
	/// 755, 2 February 2000, and its first day turns on 761. A frame of 24 ticks runs the sweeps of 760, 761 and 762, each
	/// counting 1 into a ride's takings in its turn; the day's change after 761's sweep puts the first two in the closed day,
	/// and the third is today's. The calendar is compared after each of the three, and turns on the second alone.
	/// </summary>
	/// <remarks><b>Mutations:</b> the compare once a frame after the loop (3 closed, 0 today); the compare before the sweep.</remarks>
	[TestMethod]
	public void ADayTurnedOnAFramesSecondSweepRollsTheRingsBeforeItsThird()
	{
		using var clock = new SimulationClockScope();
		var counted = -1;
		Park? park = null;

		try
		{
			park = Open( id =>
			{
				if ( id != Ride || park!.State.GameTick == counted )
					return;

				counted = park.State.GameTick;
				park.State.RingsFor( Ride ).Takings.Today += 1;
			} );

			Assert.AreEqual( 755, park.State.GameTick, "the stock park's clock" );

			// One sweep a frame to 759, the day unturned.
			while ( park.State.GameTick < 759 )
				Frame( park, 8 );

			var rings = park.State.RingsFor( Ride );
			rings.Takings.Today = 0;
			var entry = rings.Takings.CurrentEntry;
			var compares = new List<(int Tick, bool Day)>();

			Frame( park, 24, compares );

			CollectionAssert.AreEqual( new[] { (760, false), (761, true), (762, false) }, compares, "compared after each sweep" );
			Assert.AreEqual( (entry + 1) % 30, rings.Takings.CurrentEntry, "one day closed" );
			Assert.AreEqual( (2, 1), (rings.Takings.DaysAgo( 0 ), rings.Takings.Today ),
				"760's and 761's takings in the closed day, 762's today" );
		}
		finally
		{
			Close( park );
		}
	}

	/// <summary>
	/// <b>The weather turns once for each sweep run, and not for one dropped</b>: a frame of 64 ticks runs three sweeps and drops
	/// five, and a dry weather's thunder count, one down a turn, falls by three; a frame with no sweep turns it not at all.
	/// </summary>
	/// <remarks><b>Mutations:</b> the weather turned once a frame; turned on a dropped sweep too.</remarks>
	[TestMethod]
	public void TheWeatherTurnsOnceForEachSweepRun()
	{
		using var clock = new SimulationClockScope();
		Park? park = null;

		try
		{
			park = Open();
			park.Weather.DebugQuality( 78 );
			var thunder = park.Weather.Written( null ).ThunderCountdown;

			Frame( park, 64 );
			Assert.AreEqual( (3, 5), (park.People.SweepsRun, park.People.SweepsDropped) );
			Assert.AreEqual( (3, thunder - 3), (park.Weather.Turns, park.Weather.Written( null ).ThunderCountdown ) );

			// Ticks 65 to 68: no sweep falls due.
			Frame( park, 4 );
			Assert.AreEqual( 3, park.Weather.Turns );
		}
		finally
		{
			Close( park );
		}
	}

	/// <summary>
	/// <b>The weather reads the date of its own sweep</b>: a park with no file, its clock at nought, forecasts on its first
	/// sweep and the forecast lands on the sweep whose compare turns its fifth day edge, 93, one sweep a frame
	/// (<c>docs/exe/weather.md</c>, "A park's first days": the original's on 94, by instants, Q529).
	/// </summary>
	/// <remarks><b>Mutations:</b> the weather's turn before the compare, which reads the day before the first edge and lands on
	/// 71.</remarks>
	[TestMethod]
	public void AFreshParksFirstForecastLandsOnTheSweepOfItsDay()
	{
		using var clock = new SimulationClockScope();
		Park? park = null;

		try
		{
			park = Open();
			park.State.SetGameTick( 0 );
			GameCalendar.Rebase();

			var rolled = park.Weather.Quality;
			var landed = 0;

			// A frame of one sweep's ticks at a time, so the sweep it lands on is read; a frame of three is the first test's.
			while ( landed == 0 && park.State.GameTick < 120 )
			{
				Frame( park, ParkPeople.ThingTickEvery );

				if ( park.Weather.Forecast < 0 && park.State.GameTick > 1 )
					landed = park.State.GameTick;
			}

			Assert.AreEqual( 93, landed, $"the forecast landed on sweep {landed}, quality {rolled} to {park.Weather.Quality}" );
		}
		finally
		{
			Close( park );
		}
	}

	/// <summary>An entity that reads the park's clock at its turn in the walk.</summary>
	private sealed class ClockReader( ParkState state ) : Entity
	{
		public int? Seen;

		protected override void OnUpdate() => Seen = state.GameTick;
	}

	/// <summary>
	/// <b>The level's frame runs the park's loop before the entities' walk, and hands the loop the golden tickets</b>
	/// (<c>Level.Update</c>'s world step, run by reflection on a bare level): Lost Kingdom's file with its clock set to 799, a
	/// Full Simulation player and a frame of eight ticks. The sweep on the eighth tick makes 800, a hundredth, which owes the
	/// tickets' test; an entity in the walk reads 800, and the test is run on 800 (<see cref="ParkGoldenTickets.Last"/>).
	/// </summary>
	/// <remarks><b>Mutations:</b> the loop after the walk (the walk reads 799); the tickets' delegate given as <c>_ => { }</c>
	/// (no test run).</remarks>
	[TestMethod]
	public void TheLevelsFrameSweepsBeforeTheWalkAndTestsTheTicketsTheSweepOwed()
	{
		using var clock = new SimulationClockScope();
		var (level, all, people, rides, particles) = (Level.Current, Entity.All, ParkPeople.Current, ParkRides.Current, ParticleSystem.Current);
		var player = typeof( Players ).GetProperty( nameof( Players.Current ) )!;
		var (playing, last) = (player.GetValue( Players.Roster ), typeof( ParkGoldenTickets ).GetProperty( nameof( ParkGoldenTickets.Last ), BindingFlags.Static | BindingFlags.NonPublic )!);
		Park? park = null;

		try
		{
			Entity.All = [];
			typeof( ParkRides ).GetProperty( nameof( ParkRides.Current ) )!.SetValue( null, null );
			typeof( ParticleSystem ).GetProperty( nameof( ParticleSystem.Current ) )!.SetValue( null, null );
			last.SetValue( null, null );
			ParkGoldenTickets.TakeOwed();

			park = Open();
			park.State.SetGameTick( 799 );
			var reader = new ClockReader( park.State );

			var bare = (Level)RuntimeHelpers.GetUninitializedObject( typeof( Level ) );
			typeof( Level ).GetProperty( nameof( Level.Kind ) )!.SetValue( bare, Level.Scene.Park );
			typeof( Level ).GetProperty( nameof( Level.ParkState ) )!.SetValue( bare, park.State );
			typeof( Level ).GetProperty( nameof( Level.ThemeName ) )!.SetValue( bare, "jungle" );
			typeof( Level ).GetProperty( nameof( Level.Balance ) )!.SetValue( bare, new ParkBalance( "jungle", easyMode: false ) );
			Level.Current = bare;
			player.SetValue( Players.Roster, new Player( 0, "Test", new PlayerFile { InstantAction = false } ) );

			SimulationClockScope.Frame( 8.5f * GameClock.TickSeconds );
			Assert.AreEqual( 8, GameClock.TicksDue );
			typeof( Level ).GetMethod( "RunTheWorld", BindingFlags.Instance | BindingFlags.NonPublic )!.Invoke( bare, [] );

			Assert.AreEqual( 800, park.State.GameTick, "one sweep" );
			Assert.AreEqual( 800, reader.Seen, "the walk comes after the sweep" );
			Assert.AreEqual( 800, ParkGoldenTickets.Last?.Tick, "the sweep's tail ran the tickets' test it owed" );
		}
		finally
		{
			Close( park );
			Entity.All.ForEach( entity => entity.Delete() );
			Entity.ApplyDeletions();
			Entity.All = all;
			Level.Current = level!;
			player.SetValue( Players.Roster, playing );
			last.SetValue( null, null );
			ParkGoldenTickets.TakeOwed();
			typeof( ParkPeople ).GetProperty( nameof( ParkPeople.Current ), BindingFlags.Static | BindingFlags.NonPublic )!.SetValue( null, people );
			typeof( ParkRides ).GetProperty( nameof( ParkRides.Current ) )!.SetValue( null, rides );
			typeof( ParticleSystem ).GetProperty( nameof( ParticleSystem.Current ) )!.SetValue( null, particles );
		}
	}
}
