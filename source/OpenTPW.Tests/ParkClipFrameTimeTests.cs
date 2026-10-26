using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenTPW.Tests;

/// <summary>
/// <b>A clip is posed at the frame's own moment, not the last tick's</b> (Q675): the engine's snapshot is the clock read
/// once a drawn frame (<c>0x00473440</c>) and a clip's frame a float from it (<c>0x00473740</c>), so it moves on every
/// drawn frame; <see cref="ParkRides.NowMilliseconds"/> is the last tick's instant and the part of a tick owed since,
/// short of the next tick's (<c>docs/exe/park.md</c>, "What a drawn frame shows between ticks", difference 7).
/// </summary>
[TestClass]
public class ParkClipFrameTimeTests
{
	/// <summary>A frame of <paramref name="seconds"/> on the park's clock.</summary>
	private static void Frame( float seconds )
	{
		Time.Update( seconds );
		GameClock.Update( paused: false, GameClock.ParkCatchUp );
	}

	/// <summary>The clock with nothing owed: a re-base and the frame it throws away.</summary>
	private static void Rebased()
	{
		Time.Paused = false;
		Time.StepFrames = 0;
		GameClock.Rebase();
		Frame( 0f );
	}

	/// <summary>
	/// Half a tick owed and none due puts now 15 ms past the last tick's instant; 30.9 ms owed puts it 30 ms past, the
	/// part of a tick truncated and never the next tick's instant; a tick due moves the instant on and keeps the part.
	/// </summary>
	[TestMethod]
	public void NowIsTheFramesOwnMoment()
	{
		Rebased();
		var ticks = GameClock.Ticks;

		Frame( 0.0155f );
		Assert.AreEqual( (0, ticks), (GameClock.TicksDue, GameClock.Ticks), "no tick due" );
		Assert.AreEqual( ticks * 31 + 15, ParkRides.NowMilliseconds, "half a tick past the last tick's instant" );

		Frame( 0.0154f );
		Assert.AreEqual( ticks, GameClock.Ticks, "still no tick due" );
		Assert.AreEqual( ticks * 31 + 30, ParkRides.NowMilliseconds, "30.9 ms owed is 30 ms in, short of the next tick's instant" );

		Frame( 0.0156f );
		Assert.AreEqual( ticks + 1, GameClock.Ticks, "a tick due" );
		Assert.AreEqual( (ticks + 1) * 31 + 15, ParkRides.NowMilliseconds, "the next tick's instant and 15.5 ms owed past it" );
	}

	/// <summary>
	/// Past 2^26 ms (tick 2164803; 18.6 hours of ticks), where a float loses whole milliseconds, the frame's moment is
	/// still the tick stamps' own rounding: it never steps back across a tick, starts at the last tick's stamp and stays
	/// at or under the next tick's. Read across 2000 ticks at 2^26 ms (a float's step 8) and at 2^27 ms and past (16).
	/// </summary>
	/// <remarks><b>Mutations:</b> the rounded tick instant and the exact owed part added (steps back from 2^26 ms); the sum
	/// exact in integers (the tick instant is not the stamp the tick's scripts make).</remarks>
	[TestMethod]
	public void NowNeverStepsBackAndKeepsTheTickStampsPastTwoToTheTwentySixMilliseconds()
	{
		foreach ( var first in new[] { 2164800, 4400000 } )
		{
			var previous = 0;

			for ( int ticks = first; ticks < first + 2000; ++ticks )
			{
				var stamp = (int)(ticks * 31f);
				var nextStamp = (int)((ticks + 1) * 31f);

				Assert.AreEqual( stamp, ParkRides.MomentAt( ticks, 0 ), $"tick {ticks}: nothing owed is the tick's own stamp" );

				for ( int owed = 0; owed < 31; ++owed )
				{
					var now = ParkRides.MomentAt( ticks, owed );
					Assert.IsTrue( now >= previous, $"tick {ticks}, {owed} ms owed: {now} after {previous}" );
					Assert.IsTrue( now <= nextStamp, $"tick {ticks}, {owed} ms owed: {now} past the next tick's {nextStamp}" );
					previous = now;
				}

				Assert.AreEqual( ParkRides.MomentAt( ticks, 30 ), ParkRides.MomentAt( ticks, 99 ), "short of the next tick however long is owed" );
			}
		}
	}

	/// <summary>
	/// A frame with half a tick owed and no tick due advances a standing thing's clip to the frame's moment: the Round
	/// Fountain's construction clip, started at the last tick's instant, stands 15 ms and 0.45 frames in after the frame,
	/// where a sweep at the last tick's instant leaves it on frame nought. The model is made without its graphics, as
	/// <see cref="ParkBuildClipTests.TheDrawnFountainIsPosedInTheFrameItIsBuilt"/> makes it. Needs the game.
	/// </summary>
	[TestMethod]
	public void AFrameBetweenTicksMovesAClip()
	{
		var data = GameData.Required();
		FileSystem = data;
		Log ??= new();
		var world = new ParkWorld( new SaveReader( new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) ) ).ReadFile() );
		var catalogue = new ParkItemCatalogue( "jungle", data );
		Assert.IsTrue( catalogue.TryGet( 1403, out var item ), "the Round Fountain" );

		using var stream = new MemoryStream( data.ReadAllBytes( $"{item.Directory}/{item.Stem}.md2" ) );
		var file = new ModelFile( stream );
		var entities = Enumerable.Range( 0, file.Meshes.Count ).Select( _ => new ModelEntity() ).ToArray();
		var objects = new ParkObjects( "jungle", null, null );
		var rides = new ParkRides( "jungle", world, catalogue, data, objects );

		try
		{
			var model = (LobbyModel)RuntimeHelpers.GetUninitializedObject( typeof( LobbyModel ) );
			const BindingFlags any = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

			void Set( string field, object value ) => typeof( LobbyModel ).GetField( field, any )!.SetValue( model, value );

			Set( "_file", file );
			Set( "<Entities>k__BackingField", entities );
			Set( "<Offsets>k__BackingField", new OpenTPW.Vector3[file.Meshes.Count] );
			Set( "<Animators>k__BackingField", new MeshAnimator[0] );
			Set( "_nodeParents", file.Nodes.Select( node => node.ParentIndex ).ToArray() );
			Set( "_placedRotation", System.Numerics.Quaternion.Identity );

			var animations = RideAnimations.Load( item.Directory, item.Stem, data, item.AnimationChannels );
			objects.Stand( 43, model, animations );

			Rebased();
			var instant = GameClock.Ticks * 31;
			animations.Trigger( 0, 0, 0, 1f, instant );

			Frame( 0.0155f );
			Assert.AreEqual( 0, GameClock.TicksDue, "no tick due" );
			rides.Update();

			var channel = animations.Channel( 0 )!;
			Assert.AreEqual( instant + 15, channel.AnimTime, "advanced to the frame's moment" );
			Assert.AreEqual( 15 * AnimTimeControl.FramesPerMillisecond, channel.AnimFrame, 0.0001f, "0.45 frames in" );
		}
		finally
		{
			rides.Delete();
			objects.Delete();

			foreach ( var entity in entities )
				entity.Delete();
		}
	}

	/// <summary>A model read from <paramref name="path"/> with only the fields a pose reads, made without its graphics.</summary>
	private static (LobbyModel Model, ModelEntity[] Entities) Ungraphic( string path )
	{
		using var stream = new MemoryStream( FileSystem.ReadAllBytes( path ) );
		var file = new ModelFile( stream );
		var entities = Enumerable.Range( 0, file.Meshes.Count ).Select( _ => new ModelEntity() ).ToArray();
		var model = (LobbyModel)RuntimeHelpers.GetUninitializedObject( typeof( LobbyModel ) );
		const BindingFlags any = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

		void Set( string field, object value ) => typeof( LobbyModel ).GetField( field, any )!.SetValue( model, value );

		Set( "_file", file );
		Set( "<Entities>k__BackingField", entities );
		Set( "<Offsets>k__BackingField", new OpenTPW.Vector3[file.Meshes.Count] );
		Set( "<Animators>k__BackingField", new MeshAnimator[0] );
		Set( "_nodeParents", file.Nodes.Select( node => node.ParentIndex ).ToArray() );
		Set( "_placedRotation", System.Numerics.Quaternion.Identity );

		return (model, entities);
	}

	/// <summary>
	/// A Hot Pot boat in a go is posed at the frame's moment too: its rocking clip (role 5) starts at the frame's moment
	/// half a tick past the last tick's instant, and the next frame, 30 ms in with still no tick due, finds it 15 ms and
	/// 0.45 frames on, where a boat posed at the last tick's instant stands on frame nought both times. The boat is put
	/// in the pool slot by reflection over a model made without its graphics. Needs the game.
	/// </summary>
	[TestMethod]
	public void AHotPotBoatsClipMovesBetweenTicks()
	{
		var data = GameData.Required();
		FileSystem = data;
		Log ??= new();
		var catalogue = new ParkItemCatalogue( "jungle", data );
		Assert.IsTrue( catalogue.TryGet( 1140, out var pot ), "the Hot Pot" );

		var state = new ParkState( parkIsClosed: false, visitorsToDate: 0 );
		var cars = state.TrackRides.Cars;
		var handle = state.TrackRides.Take( -1 );

		cars.Place( handle, 30 * ParkBumperCars.CellUnits, 30 * ParkBumperCars.CellUnits );
		cars.OpenForLoading( handle );

		for ( var car = 0; car < 4; ++car )
			cars.Launch( handle );

		Assert.IsTrue( cars.Board( handle, 101 ) && cars.Fill( handle ) );
		cars.Start( handle );

		var seated = cars.CarsOf( handle ).Single( car => car.Riders.Count > 0 );
		Assert.AreEqual( 5, seated.Animation, "a car with a rider rocks in the go" );

		var (model, entities) = Ungraphic( $"{pot.Directory}/b_car.MD2" );
		var boats = new ParkBumperBoats( "jungle", catalogue );

		try
		{
			const BindingFlags any = BindingFlags.Instance | BindingFlags.NonPublic;
			var boatType = typeof( ParkBumperBoats ).GetNestedType( "Boat", BindingFlags.NonPublic )!;
			var seatType = typeof( ParkBumperBoats ).GetNestedType( "Seat", BindingFlags.NonPublic )!;
			var seats = Activator.CreateInstance( typeof( Dictionary<,> ).MakeGenericType( typeof( int ), seatType ) )!;
			var boat = Activator.CreateInstance( boatType, model, RideAnimations.Load( pot.Directory, "b_car", data ), seats, handle )!;
			var pool = (System.Collections.IDictionary)typeof( ParkBumperBoats ).GetField( "_boats", any )!.GetValue( boats )!;
			pool[seated.Index] = boat;

			// For each other car a boat would be stood with its graphics: those slots are marked as failed instead.
			var failed = (HashSet<int>)typeof( ParkBumperBoats ).GetField( "_failed", any )!.GetValue( boats )!;

			foreach ( var other in cars.CarsOf( handle ).Where( car => car != seated ) )
				failed.Add( other.Index );

			Rebased();
			var instant = GameClock.Ticks * 31;

			Frame( 0.0155f );
			boats.Sync();

			var channel = boats.AnimationsOf( seated.Index )!.Channel( 0 )!;
			Assert.AreEqual( (5, instant + 15, instant + 15, 0f), (channel.AnimID, channel.StartAnimTime, channel.AnimTime, channel.AnimFrame),
				"started at the frame's moment" );

			Frame( 0.0154f );
			Assert.AreEqual( 0, GameClock.TicksDue, "no tick due" );
			boats.Sync();

			Assert.AreEqual( instant + 30, channel.AnimTime, "advanced to the next frame's moment" );
			Assert.AreEqual( 15 * AnimTimeControl.FramesPerMillisecond, channel.AnimFrame, 0.0001f, "0.45 frames in" );
		}
		finally
		{
			boats.Delete();
			ParkState.ForgetCurrent();

			foreach ( var entity in entities )
				entity.Delete();
		}
	}
}
