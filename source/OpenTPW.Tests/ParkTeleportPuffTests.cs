using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// The puffs a guest goes onto a ride and comes off it in (<c>FUN_00501db0</c>'s state cases <c>0x10</c> and <c>0xf</c>,
/// <c>0x005020ed</c> and <c>0x00501fce</c>), and a new guest's own (<c>FUN_004faec0</c>, <c>0x004fb26e</c>): particle
/// <c>0x13</c> through <see cref="PersonPuffs"/>, when the thing's descriptor word at <c>+0x100</c>,
/// <c>UsageInfo.RequiresTeleport</c>, is set (<c>docs/exe/park-engine.md</c>, "A person's particles").
/// </summary>
[TestClass]
public class ParkTeleportPuffTests
{
	private BaseFileSystem data = null!;
	private HeightfieldFile field = null!;
	private ParticleSystem? puffs;

	private const int BellyBounce = 13, Drinks = 16;

	[TestInitialize]
	public void MountTheGame()
	{
		FileSystem = data = GameData.Required();

		using var terrain = FileSystem.OpenRead( "levels/jungle/terrain/base.MD2" )!;
		field = new HeightfieldFile( terrain );
		puffs = new ParticleSystem( "Particle/Tp2.plb", 1000 );
		PersonPuffs.Ground = field;
		GameClock.Rebase();
	}

	[TestCleanup]
	public void PutItAway()
	{
		PersonPuffs.Ground = null;
		puffs?.Shutdown();
		puffs = null;
		TestRun.DeleteEvery<ParkPeople>();
	}

	private ParkWorld Park()
	{
		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		return new ParkWorld( new SaveReader( stream ).ReadFile() );
	}

	private ParkItemCatalogue Catalogue() => new( "jungle", data );

	private RideScript Script()
	{
		using var stream = data.OpenRead( "levels/jungle/rides/bouncy/bouncy.RSE" );
		return new RideScript( new RideScriptFile( stream ) );
	}

	/// <summary>A guest on the cell (40.5, 58.5), where the ground is raised, so the height shows in the puff's words.</summary>
	private static Peep Guest( int id, PeepState state )
		=> new( id, new ParkWorld.GuestState(
			State: (int)state, SavedState: (int)PeepState.Deciding, PersonType: 0, Cash: 300,
			ExitLevel: 100, Happiness: 50f, Thirst: 10f, Hunger: 10f, Toilet: 10f, Vomit: 0f,
			Litter: 0f, MajorDest: 0, QueuePos: 0, PrankeryIndex: 0 ),
			new ParkWorld.NavigatorState(
				X: (40 * PeepNavigator.One) + (PeepNavigator.One / 2), Y: (58 * PeepNavigator.One) + (PeepNavigator.One / 2),
				VelocityX: 0, VelocityY: 0, TargetX: 0, TargetY: 0,
				Mass: ParkWorld.NavigatorState.DefaultMass, Radius: ParkWorld.NavigatorState.DefaultRadius,
				MaxForce: 0, MaxSpeed: 0, NavMode: 0, CantReachDest: 0, PathFinished: false,
				PathCount: 0, PathTotalCount: 0, PathBufferCount: 0,
				BufferedDistance: 0, TailDistance: 0, TotalDistance: 0, StuckBits: 0 ) );

	private List<Emitter> Active() => puffs!.Emitters.Where( e => e.Active ).ToList();

	/// <summary>
	/// <b>Three of Lost Kingdom's items set <c>RequiresTeleport</c></b>: the Belly Bounce (1100), Mumbo's (1109) and the
	/// Spider (1113), and no other. The word is descriptor <c>+0x100</c>, the key between <c>ChillsYouOut</c> and
	/// <c>RideHandlesSprite</c> in the original's key table.
	/// </summary>
	/// <remarks><b>Mutations:</b> the key read as another (<c>CannotRide</c>); every item set.</remarks>
	[TestMethod]
	public void ThreeItemsRequireATeleport()
	{
		var set = Catalogue().All.Where( i => i.RequiresTeleport ).Select( i => i.Id ).Order().ToList();

		CollectionAssert.AreEqual( new[] { 1100, 1109, 1113 }, set );
	}

	/// <summary>
	/// <b>A guest let off a ride that teleports them goes in a puff at their feet</b> (no walk is handed over here, so they stay where they are; <see cref="TheExitPuffStandsOnTheExitCell"/> puts them down first);
	/// one let off a shop (the Drinks Shop, whose item does not) goes in none.
	/// </summary>
	/// <remarks><b>Mutations:</b> no puff; the puff for every item; the puff at the cell's corner.</remarks>
	[TestMethod]
	public void AGuestComesOffATeleportingRideInAPuff()
	{
		var park = Park();
		var bounce = park.Objects.Single( o => o.ThingId == BellyBounce );
		var drinks = park.Objects.Single( o => o.ThingId == Drinks );

		foreach ( var (ride, puffed) in new[] { (drinks, false), (bounce, true) } )
		{
			var peep = Guest( 7, PeepState.Riding );
			var script = Script();
			script.Set( ParkRideOperation.DismissVariable, 7 );
			var before = Active().Count;
			var (x, height, z) = PersonPuffs.PlaceOn( field, peep.Navigator.Position );

			Assert.IsTrue( new ParkRideOperation( new ParkState( parkIsClosed: false, visitorsToDate: 0 ),
				new Dictionary<int, Peep> { [7] = peep } )
				.Dismiss( script, ride, tick: 9, new Random( 1 ), catalogue: Catalogue() ), "let off" );

			var now = Active();
			Assert.AreEqual( puffed ? before + 1 : before, now.Count, puffed ? "a puff" : "no puff" );

			if ( puffed )
				Assert.AreEqual( (0x13, x >> 4, height >> 4, z >> 4), (now[^1].Effect, now[^1].X, now[^1].Y, now[^1].Z), "at their feet" );
		}

		Assert.AreNotEqual( 0, PersonPuffs.PlaceOn( field, Guest( 7, PeepState.Riding ).Navigator.Position ).Height, "on raised ground" );
	}

	/// <summary>
	/// <b>The puff is sent up after the guest is put down on the exit</b>, where they now stand, not where they stood on the
	/// ride: case <c>0xf</c> stamps the place first (<c>FUN_004fa030( 0 )</c>, then <c>FUN_004fa470</c>).
	/// </summary>
	/// <remarks><b>Mutations:</b> the puff sent before <c>PutDownAtTheExit</c>.</remarks>
	[TestMethod]
	public void TheExitPuffStandsOnTheExitCell()
	{
		var park = Park();
		var bounce = park.Objects.Single( o => o.ThingId == BellyBounce );
		var peep = Guest( 7, PeepState.Riding );
		var script = Script();
		script.Set( ParkRideOperation.DismissVariable, 7 );
		var before = Active().Count;
		var walk = new PeepWalk( peep.Navigator, ( _, _, _ ) => true );

		Assert.IsTrue( new ParkRideOperation( new ParkState( parkIsClosed: false, visitorsToDate: 0 ),
			new Dictionary<int, Peep> { [7] = peep } )
			.Dismiss( script, bounce, tick: 9, new Random( 1 ), id => id == 7 ? walk : null, catalogue: Catalogue() ), "let off" );

		var (x, height, z) = PersonPuffs.PlaceOn( field, new FixedVector(
			ParkRidePoint.Fixed( bounce.ExitCellX, 127 ), ParkRidePoint.Fixed( bounce.ExitCellY, 127 ) ) );
		var now = Active();

		Assert.AreEqual( before + 1, now.Count, "a puff" );
		Assert.AreEqual( (0x13, x >> 4, height >> 4, z >> 4), (now[^1].Effect, now[^1].X, now[^1].Y, now[^1].Z), "on the exit cell" );

		// The original's own words for the same puff: the emitter's raw place (33597, 0, 16957) = (524.953, 0, 264.953) for
		// 37 of 37 Belly Bounce exit puffs polled in q900/orig/a-poll.log (the guest's sprite at (524.961, 0, 264.961)),
		// 3 under the cell's middle in x and in z.
		Assert.AreEqual( (33597, 0, 16957), (now[^1].X, now[^1].Y, now[^1].Z), "the original's raw words" );
	}

	/// <summary>
	/// <b>A guest taken off the queue onto a teleporting ride goes in a puff</b> where they stand; onto one that does not,
	/// in none; and a caller that names no ride (the admission's own tests) makes none.
	/// </summary>
	/// <remarks><b>Mutations:</b> no puff; the puff for every item; a puff when no ride is named.</remarks>
	[TestMethod]
	public void AGuestGoesOntoATeleportingRideInAPuff()
	{
		var park = Park();
		var bounce = park.Objects.Single( o => o.ThingId == BellyBounce );
		var shop = park.Objects.Single( o => o.ThingId == Drinks );

		foreach ( var (ride, named, puffed) in new[] { (shop, true, false), (bounce, false, false), (bounce, true, true) } )
		{
			var state = new ParkState( parkIsClosed: false, visitorsToDate: 0 );
			state.JoinQueue( ride.ThingId, 7 );
			var peep = Guest( 7, PeepState.EnteringRide );
			var before = Active().Count;
			var (x, height, z) = PersonPuffs.PlaceOn( field, peep.Navigator.Position );

			Assert.IsTrue( new ParkRideOperation( state, new Dictionary<int, Peep> { [7] = peep } )
				.CompleteAdmission( Script(), ride.ThingId, tick: 5, new Random( 1 ), named ? ride : null, named ? Catalogue() : null ), "admitted" );

			var now = Active();
			Assert.AreEqual( puffed ? before + 1 : before, now.Count, $"{ride.ThingId} named {named}" );

			if ( puffed )
				Assert.AreEqual( (0x13, x >> 4, height >> 4, z >> 4), (now[^1].Effect, now[^1].X, now[^1].Y, now[^1].Z), "where they stand" );
		}
	}
}
