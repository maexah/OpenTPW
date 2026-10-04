using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// A toilet's dirt - <c>docs/exe/ride-operation.md</c>, "A toilet's dirt": a use takes 0.05 of the need's byte off
/// the State of repair in the running park (<c>FUN_004e2440</c>), below 25 the queue turn puts its queuers out
/// (<c>0x005001f0</c>) and the toilet's turn tells its script <c>VAR_WORN</c> (<c>0x004e0d10</c>).
///
/// <para>
/// These read real game files and are skipped where there is no installation - see <see cref="GameData"/>.
/// </para>
/// </summary>
[TestClass]
public class ParkToiletDirtTests
{
	private BaseFileSystem data = null!;

	[TestInitialize]
	public void MountTheGame() => FileSystem = data = GameData.Required();

	/// <summary>The first of Lost Kingdom's three toilets.</summary>
	private const int Toilet = 21;

	private const int BellyBounce = 13;

	private const int Sweep = 40;

	private ParkWorld World()
	{
		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		return new ParkWorld( new SaveReader( stream ).ReadFile() );
	}

	private RideScript ToiletScript()
	{
		using var stream = data.OpenRead( "levels/jungle/features/Toilet.rse" );
		return new RideScript( new RideScriptFile( stream ) );
	}

	private static ParkWorld.NavigatorState StandingStill => new(
		X: 0, Y: 0, VelocityX: 0, VelocityY: 0, TargetX: 0, TargetY: 0,
		Mass: ParkWorld.NavigatorState.DefaultMass, Radius: ParkWorld.NavigatorState.DefaultRadius,
		MaxForce: 0, MaxSpeed: 0, NavMode: 0, CantReachDest: 0, PathFinished: false,
		PathCount: 0, PathTotalCount: 0, PathBufferCount: 0,
		BufferedDistance: 0, TailDistance: 0, TotalDistance: 0, StuckBits: 0 );

	private static Peep Guest( int id, PeepState state, float need, int thing = Toilet )
		=> new( id, new ParkWorld.GuestState(
			State: (int)state, SavedState: (int)PeepState.Deciding, PersonType: 0, Cash: 300,
			ExitLevel: 100, Happiness: 50f, Thirst: 10f, Hunger: 10f, Toilet: need, Vomit: 0f,
			Litter: 0f, MajorDest: thing, QueuePos: 0, PrankeryIndex: 0 ), StandingStill );

	private static float RepairOf( ParkState park, int thing = Toilet )
		=> park.TryObject( thing, out var placed ) ? placed.StateOfRepair : float.NaN;

	/// <summary>One guest uses the toilet at <paramref name="need"/>, through the script's dismissal and the settle-up.</summary>
	private void Use( ParkState park, RideScript script, int id, float need )
	{
		var peep = Guest( id, PeepState.Riding, need );

		// Entering wrote the roll into mQueuePos; nought would be a lost sideshow, which stops before the effects.
		peep.QueuePos = 1;

		Assert.IsTrue( park.TryObject( Toilet, out var toilet ) );

		script.Set( ParkRideOperation.DismissVariable, id );

		var mood = new ParkAdmission( new ParkBalance( "jungle", easyMode: true ), 25 );

		Assert.IsTrue( new ParkRideOperation( park, new Dictionary<int, Peep> { [id] = peep }, mood )
			.Dismiss( script, toilet, tick: 9, new Random( 1 ), catalogue: new ParkItemCatalogue( "jungle", data ) ),
			"the guest should have been let off" );
		Assert.AreEqual( 0f, peep.Toilet, "and their need emptied" );
	}

	/// <summary>
	/// <b>Sixteen uses at a need of 100 take a toilet from 100 to 20, five a use</b>, dirty on the sixteenth and not
	/// on the fifteenth, which leaves exactly 25. The save's own record never moves.
	/// </summary>
	[TestMethod]
	public void SixteenUsesAtFullNeedMakeAToiletDirty()
	{
		var world = World();
		var park = new ParkState( world );
		var script = ToiletScript();

		Assert.AreEqual( 100f, RepairOf( park ), "the save's toilet starts at 100" );

		for ( var use = 1; use <= 16; use++ )
		{
			Use( park, script, 200 + use, need: 100f );

			Assert.AreEqual( 100f - 5f * use, RepairOf( park ), $"after use {use}" );
			Assert.IsTrue( park.TryObject( Toilet, out var now ) );
			Assert.AreEqual( use == 16, ParkState.IsDirty( now ), $"dirty after use {use}" );
		}

		Assert.AreEqual( 100f, world.Objects.Single( thing => thing.ThingId == Toilet ).StateOfRepair,
			"the save's record is not written" );
	}

	/// <summary>
	/// <b>The wear is the need's truncated byte times 0.05, held to 0..100</b>: 50.9 takes 2.5, and a toilet at 3
	/// used at 100 stops at nought.
	/// </summary>
	[TestMethod]
	public void TheWearIsTheNeedsByteAndStopsAtNought()
	{
		var park = new ParkState( World() );
		var script = ToiletScript();

		Use( park, script, 201, need: 50.9f );
		Assert.AreEqual( 97.5f, RepairOf( park ), "fifty, not 50.9" );

		Use( park, script, 202, need: 0.9f );
		Assert.AreEqual( 97.5f, RepairOf( park ), "a need under one takes nothing" );

		Assert.IsTrue( park.TryObject( Toilet, out var toilet ) );
		park.ReplaceObject( toilet with { StateOfRepair = 3f } );

		Use( park, script, 203, need: 100f );
		Assert.AreEqual( 0f, RepairOf( park ), "held at nought" );
	}

	/// <summary><b>Dirty is a toilet whose State of repair truncates below 25</b>: 24.9 is, 25 is not, and no ride is.</summary>
	[TestMethod]
	public void DirtyIsAToiletBelowTwentyFive()
	{
		var park = new ParkState( World() );

		Assert.IsTrue( park.TryObject( Toilet, out var toilet ) );
		Assert.IsTrue( park.TryObject( BellyBounce, out var ride ) );

		Assert.IsFalse( ParkState.IsDirty( toilet ), "at the save's 100" );
		Assert.IsFalse( ParkState.IsDirty( toilet with { StateOfRepair = 25f } ), "at 25" );
		Assert.IsTrue( ParkState.IsDirty( toilet with { StateOfRepair = 24.9f } ), "at 24.9" );
		Assert.IsTrue( ParkState.IsDirty( toilet with { StateOfRepair = 0f } ), "at nought" );
		Assert.IsFalse( ParkState.IsDirty( ride with { StateOfRepair = 0f } ), "a ride at nought is not a toilet" );
	}

	/// <summary>
	/// <b>No Lost Kingdom toilet declares an excitement</b>, so the arrival's excitement gate (<c>0x004ffc68</c>),
	/// where a dirty toilet would answer 100, lets every arrival through.
	/// </summary>
	[TestMethod]
	public void NoJungleToiletDeclaresAnExcitement()
	{
		var toilets = new ParkItemCatalogue( "jungle", data ).All.Where( item => item.ProvidesRelief ).ToArray();

		Assert.AreEqual( 2, toilets.Length, "the Small Toilet and the Super Toilet" );

		foreach ( var item in toilets )
			Assert.AreEqual( 0, item.ExcitementLevel & 0xff, $"'{item.Name}'" );
	}

	private (PeepBehaviour Behaviour, Dictionary<int, Peep> Guests) Behaviour( ParkWorld world, ParkState state,
		RideScript script )
	{
		var guests = new Dictionary<int, Peep>();
		var admission = new ParkAdmission( new ParkBalance( "jungle", easyMode: true ), world.Economy!.Value.AdmissionFee );

		var behaviour = new PeepBehaviour( world, new Random( 1 ), admission, () => ParkRides.GateIsOpen, state,
			new ParkItemCatalogue( "jungle", data ),
			leaveQueue: ( ride, id ) => ParkRideOperation.LeaveQueue( state, script, ride.ThingId, id ),
			stillQueueing: id => guests.TryGetValue( id, out var peep ) && ParkRideOperation.IsQueueing( peep ) );

		return (behaviour, guests);
	}

	/// <summary>
	/// <b>A dirty toilet's queuers are put out on their own turns</b> (<c>0x005001f0</c>), for
	/// <c>MediumHappinessChange</c>, and a toilet at 25 keeps them.
	/// </summary>
	[TestMethod]
	public void ADirtyToiletPutsItsQueuersOut()
	{
		foreach ( var repair in new[] { 25f, 24.9f } )
		{
			var world = World();
			var state = new ParkState( world );
			var script = ToiletScript();
			var (behaviour, guests) = Behaviour( world, state, script );

			Assert.IsTrue( state.TryObject( Toilet, out var toilet ) );
			state.ReplaceObject( toilet with { StateOfRepair = repair } );

			var queuers = new[] { Guest( 30, PeepState.InQueue, 95f ), Guest( 31, PeepState.InQueue, 95f ) };

			foreach ( var peep in queuers )
			{
				guests[peep.ThingId] = peep;
				peep.QueuePos = state.JoinQueue( Toilet, peep.ThingId );
			}

			foreach ( var peep in queuers )
			{
				var walk = new PeepWalk( peep.Navigator, CellEdge.For( world, ParkPeople.WalkingMode ).Blocked );

				behaviour.Step( peep, walk, playing: null, Sweep );

				if ( repair < ParkState.DirtyBelow )
				{
					Assert.AreEqual( PeepState.Deciding, peep.State, $"guest {peep.ThingId} is put out at {repair}" );
					Assert.AreEqual( 0, peep.MajorDest, $"guest {peep.ThingId} names nothing" );
					Assert.AreEqual( 35f, peep.Happiness, 0.001f, $"guest {peep.ThingId} loses 15" );
					Assert.AreEqual( -1, state.PositionInQueue( Toilet, peep.ThingId ), $"guest {peep.ThingId} is unlinked" );
				}
				else
				{
					Assert.AreEqual( PeepState.InQueue, peep.State, $"guest {peep.ThingId} queues on at {repair}" );
					Assert.AreEqual( 50f, peep.Happiness, 0.001f );
				}
			}
		}
	}

	/// <summary>
	/// <b>A dirty toilet's turn writes <c>VAR_WORN</c> 1, by name, and a clean one's writes nothing</b>
	/// (<c>0x004e0d10</c>). <c>Toilet.rse</c> declares the variable.
	/// </summary>
	[TestMethod]
	public void ADirtyToiletsTurnTellsItsScript()
	{
		var park = new ParkState( World() );
		var script = ToiletScript();
		var operation = new ParkRideOperation( park, new Dictionary<int, Peep>() );

		Assert.IsTrue( script.IndexOf( ParkRideOperation.WornVariable ) >= 0, "Toilet.rse declares VAR_WORN" );

		Assert.IsFalse( operation.TellTheWorn( script, Toilet ), "a clean toilet is told nothing" );
		Assert.AreEqual( 0, script[ParkRideOperation.WornVariable] );

		Assert.IsTrue( park.TryObject( Toilet, out var toilet ) );
		park.ReplaceObject( toilet with { StateOfRepair = 20f } );

		Assert.IsTrue( operation.TellTheWorn( script, Toilet ), "a dirty one is" );
		Assert.AreEqual( 1, script[ParkRideOperation.WornVariable] );

		Assert.IsFalse( operation.TellTheWorn( script, BellyBounce ), "a ride is not a toilet" );
	}

	/// <summary>One frame through both clocks, as a level runs them.</summary>
	private static void Frame( float seconds )
	{
		Time.Update( seconds );
		GameClock.Update( paused: false, GameClock.ParkCatchUp );
	}

	/// <summary>Runs the park until <paramref name="sweeps"/> more thing sweeps have been taken.</summary>
	private static void RunSweeps( ParkPeople people, int sweeps )
	{
		var until = GameClock.Ticks / ParkPeople.ThingTickEvery + sweeps;

		for ( var frame = 0; frame < 600 && GameClock.Ticks / ParkPeople.ThingTickEvery < until; ++frame )
		{
			Frame( 1f / 60f );
			people.Update();
		}

		Assert.IsTrue( GameClock.Ticks / ParkPeople.ThingTickEvery >= until, "the sweeps ran" );
	}

	/// <summary>
	/// <b>The park's own sweep wears the toilet and tells its script</b>: a guest let off a toilet at 26 in the
	/// rides' turns leaves it at 21, the sweep going on over the replaced record, and the toilet's next turn writes
	/// <c>VAR_WORN</c>.
	/// </summary>
	[TestMethod]
	public void TheParksSweepWearsTheToiletAndTellsItsScript()
	{
		var world = World();
		var state = new ParkState( world );
		var script = ToiletScript();

		var people = new ParkPeople( world, new ParkBalance( "jungle", easyMode: true ), gateStatus: null, state,
			new ParkItemCatalogue( "jungle", data ), scriptFor: id => id == Toilet ? script : null );

		try
		{
			Assert.IsTrue( state.TryObject( Toilet, out var toilet ) );
			state.ReplaceObject( toilet with { StateOfRepair = 26f } );

			var user = people.Peeps.First( guest => guest.ExitLevel > 0 );

			user.MajorDest = Toilet;
			user.Toilet = 100f;
			user.SetState( PeepState.Riding, tick: 1, new Random( 1 ) );
			user.QueuePos = 1;
			script.Set( ParkRideOperation.DismissVariable, user.ThingId );

			Time.Paused = false;
			Time.StepFrames = 0;
			GameClock.Rebase();
			Frame( 0f );

			RunSweeps( people, 1 );

			Assert.AreEqual( 21f, RepairOf( state ), "the use took five in the park's own sweep" );
			Assert.AreEqual( 0f, user.Toilet, "and emptied the guest's need" );

			RunSweeps( people, 1 );

			Assert.AreEqual( 1, script[ParkRideOperation.WornVariable], "the toilet's next turn tells its script" );
		}
		finally
		{
			people.Delete();
			Entity.ApplyDeletions();
		}
	}
}
