using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;
using System.Text;
using Numerics = System.Numerics;

namespace OpenTPW.Tests;

/// <summary>
/// Where a visitor a walk holds is drawn (<see cref="WalkRiders"/>): the engine's placer <c>FUN_005580a0</c> as its
/// stepper <c>FUN_00557d80</c> hands it each slot (<c>docs/exe/ride-operation.md</c>, "Where a walk's rider is drawn").
/// The places are the ones the original's own saves hold its riders' sprites at (<c>walkriders</c>, sixteen park files).
/// </summary>
[TestClass]
public class WalkRidersTests
{
	/// <summary>A slot whose four node ids differ, so a placement taking the wrong one is seen.</summary>
	private static RideScript.WalkView Slot( RideScript.WalkState state, short action, short flags = 1 )
		=> new( state, action, flags, WalkNode: 1, HeadNode: 7, OffFrom: 9, OffTo: 16, Start: 1000f, Due: 3000f, Facing: 3 );

	/// <summary>
	/// <b>Each state hands the placer its own two nodes</b> (<c>FUN_00557d80</c>'s switch): walking on, the walk node to
	/// the head node, the head in space <c>0x80</c> under action 4 alone; walking off, off-from to off-to, off-from in
	/// <c>0x80</c> under action 4 alone; done, off-to both ends; carried under 3, 5 or 6, the head node both ends in
	/// <c>0x800</c>, under 4 too. Every one walking or done is put on the walk, animation 1.
	/// </summary>
	[TestMethod]
	public void EachStateHandsThePlacerItsOwnTwoNodes()
	{
		const uint walk = RideNodes.WalkSpace, head = RideNodes.HeadSpace;

		foreach ( var (state, action, from, fromSpace, to, toSpace, moving) in new (RideScript.WalkState, short, int, uint, int, uint, bool)[]
		{
			(RideScript.WalkState.WalkingOn, 6, 1, walk, 7, walk, true),
			(RideScript.WalkState.WalkingOn, 4, 1, walk, 7, head, true),
			(RideScript.WalkState.WalkingOff, 6, 9, walk, 16, walk, true),
			(RideScript.WalkState.WalkingOff, 4, 9, head, 16, walk, true),
			(RideScript.WalkState.Done, 4, 16, walk, 16, walk, false),
			(RideScript.WalkState.Done, 1, 16, walk, 16, walk, false),
		} )
		{
			var placed = WalkRiders.Of( Slot( state, action ) );
			var why = $"{state} under action {action}";

			Assert.IsNotNull( placed, why );
			Assert.IsFalse( placed.Value.Hidden, why );
			Assert.AreEqual( (from, fromSpace, to, toSpace, moving), (placed.Value.From, placed.Value.FromSpace, placed.Value.To,
				placed.Value.ToSpace, placed.Value.Moving), why );
			Assert.AreEqual( WalkRiders.WalkAnimation, placed.Value.Animation, why );
			Assert.AreEqual( 3, placed.Value.Facing, why );
		}
	}

	/// <summary>
	/// <b>Carried, the action decides</b> (<c>FUN_005580a0</c>): under 1, 2 or 4 the visitor is hidden
	/// (<c>FUN_004f9ed0</c>); under 3, 5 and 6 they stand on the head node on animations 3, 8 and 5 - the scripts 90, 172
	/// and 140 the original's saves hold its riders under 5 and 6 on; under an action it has no arm for, the placer does
	/// nothing.
	/// </summary>
	[TestMethod]
	public void CarriedTheActionHidesOrStandsTheVisitor()
	{
		foreach ( short action in new short[] { 1, 2, 4 } )
			Assert.IsTrue( WalkRiders.Of( Slot( RideScript.WalkState.Carried, action ) ) is { Hidden: true }, $"action {action}" );

		foreach ( var (action, animation) in new (short, int)[] { (3, 3), (5, 8), (6, 5) } )
		{
			var placed = WalkRiders.Of( Slot( RideScript.WalkState.Carried, action ) );

			Assert.IsTrue( placed is { Hidden: false, Moving: false }, $"action {action}" );
			Assert.AreEqual( (7, RideNodes.WalkSpace, 7, RideNodes.WalkSpace), (placed!.Value.From, placed.Value.FromSpace, placed.Value.To,
				placed.Value.ToSpace), $"action {action}" );
			Assert.AreEqual( animation, placed.Value.Animation, $"action {action}" );
		}

		Assert.AreEqual( 140, new SpriteScript( SpriteScript.None, 0, 0, 0 ) is var five && five.Start( 5 ) ? five.Script : -1 );
		Assert.AreEqual( 172, new SpriteScript( SpriteScript.None, 0, 0, 0 ) is var eight && eight.Start( 8 ) ? eight.Script : -1 );

		Assert.IsNull( WalkRiders.Of( Slot( RideScript.WalkState.Carried, 0 ) ) );
		Assert.IsNull( WalkRiders.Of( Slot( RideScript.WalkState.Carried, 7 ) ) );
		Assert.IsNull( WalkRiders.Of( Slot( RideScript.WalkState.Free, 6 ) ) );
	}

	/// <summary>
	/// <b>The share of the leg is the clock's, at most a thousand</b>: a leg from 1000 to 3000 ms is half walked at 2000,
	/// done at 3000 and held there after, a tenth walked at 1200; a slot not moving stands at nought.
	/// </summary>
	[TestMethod]
	public void TheShareOfTheLegIsTheClocksAtMostAThousand()
	{
		var slot = Slot( RideScript.WalkState.WalkingOn, 6 );
		var placed = WalkRiders.Of( slot )!.Value;

		Assert.AreEqual( 500, WalkRiders.Progress( slot, placed, 2000f ) );
		Assert.AreEqual( 100, WalkRiders.Progress( slot, placed, 1200f ) );
		Assert.AreEqual( 1000, WalkRiders.Progress( slot, placed, 3000f ) );
		Assert.AreEqual( 1000, WalkRiders.Progress( slot, placed, 9000f ) );

		var carried = Slot( RideScript.WalkState.Carried, 6 );

		Assert.AreEqual( 0, WalkRiders.Progress( carried, WalkRiders.Of( carried )!.Value, 2000f ) );
	}

	/// <summary>
	/// <b>The point is A + (B − A) × p / 1000 on all three axes under flag bit 0</b>; without it the height would be the
	/// surface's, which no shipped walk asks for, and nothing is placed. The Gift Shop of Alexah's jungle <c>New Save</c>
	/// held its customer walking on from (465, 275) to (465, 285) at (465, 279.070), 407 thousandths along.
	/// </summary>
	[TestMethod]
	public void ThePointIsAlongTheLineOnAllThreeAxes()
	{
		var placed = WalkRiders.Of( Slot( RideScript.WalkState.WalkingOn, 1 ) )!.Value;
		var point = WalkRiders.Point( placed, new Numerics.Vector3( 10f, 2f, 30f ), new Numerics.Vector3( 20f, 6f, 50f ), 250 );

		Assert.IsNotNull( point );
		Assert.AreEqual( 12.5f, point.Value.X, 1e-5f );
		Assert.AreEqual( 3f, point.Value.Y, 1e-5f );
		Assert.AreEqual( 35f, point.Value.Z, 1e-5f );

		var shop = WalkRiders.Point( placed, new Numerics.Vector3( 465f, 0f, 275f ), new Numerics.Vector3( 465f, 0f, 285f ), 407 )!.Value;

		Assert.AreEqual( 279.070f, shop.Z, 0.0005f );

		Assert.IsNull( WalkRiders.Point( WalkRiders.Of( Slot( RideScript.WalkState.WalkingOn, 1, flags: 0 ) )!.Value,
			new Numerics.Vector3( 10f, 2f, 30f ), new Numerics.Vector3( 20f, 6f, 50f ), 250 ) );
	}

	/// <summary>
	/// <b>The placer queues its animation only where the sprite is neither on it nor has it queued</b>
	/// (<c>FUN_004d4100</c>), and asks nothing of a visitor it hides.
	/// </summary>
	[TestMethod]
	public void TheAnimationIsQueuedOnlyWhereTheSpriteLacksIt()
	{
		var carried = WalkRiders.Of( Slot( RideScript.WalkState.Carried, 6 ) )!.Value;
		var walking = new SpriteScript( SpriteScript.None, 0, 0, 0 );
		walking.Start( WalkRiders.WalkAnimation );

		Assert.AreEqual( 5, WalkRiders.Asked( carried, walking, 0 ) );
		Assert.IsNull( WalkRiders.Asked( carried, walking, 5 ), "already queued" );

		var on = new SpriteScript( SpriteScript.None, 0, 0, 0 );
		on.Start( 5 );

		Assert.IsNull( WalkRiders.Asked( carried, on, 0 ), "already on it" );
		Assert.IsNull( WalkRiders.Asked( carried, null, 0 ), "no sprite" );
		Assert.IsNull( WalkRiders.Asked( WalkRiders.Of( Slot( RideScript.WalkState.Carried, 1 ) )!.Value, walking, 3 ), "hidden" );
		Assert.AreEqual( 1, WalkRiders.Asked( WalkRiders.Of( Slot( RideScript.WalkState.WalkingOff, 6 ) )!.Value, on, 0 ) );
	}

	/// <summary>
	/// <b>The walk slot holding a visitor is the last in use with their handle</b>: the stepper places every slot in order,
	/// so the later's placement stands; a slot let go holds nobody, and nobody is found for a handle of nought, though a
	/// script can walk one on.
	/// </summary>
	[TestMethod]
	public void TheSlotHoldingAVisitorIsTheLastInUse()
	{
		var script = new RideScript( Build( walkSlots: 4, 0 ) );

		script.RestoreRiders( new SavedScript( 1, 0, 0, [], 0, 0, 0, [], 0, 0, 0, 0, 0, Walk:
		[
			new SavedWalkSlot( 1, 2, 2, 16, 5000, 5700, 42, 6, (short)RideScript.WalkState.Carried, 1, 1 ),
			new SavedWalkSlot( 1, 3, 3, 16, 5000, 5900, 42, 6, (short)RideScript.WalkState.WalkingOff, 1, 4 ),
			new SavedWalkSlot( 1, 4, 4, 16, 5000, 5800, 42, 6, 0, 1, 2 ),
			new SavedWalkSlot( 1, 5, 5, 16, 5000, 5800, 0, 6, (short)RideScript.WalkState.Carried, 1, 2 ),
		] ), reading => reading != 0 ? reading - 5000f : null );

		Assert.IsTrue( script.TryWalkOf( 42, out var view ) );
		Assert.AreEqual( (RideScript.WalkState.WalkingOff, (short)3, (short)4), (view.State, view.HeadNode, view.Facing) );
		Assert.IsFalse( script.TryWalkOf( 43, out _ ) );
		Assert.IsFalse( script.TryWalkOf( 0, out _ ) );
	}

	/// <summary>
	/// <b>A rider the shipped Jungle Spray carries stands where the original put its sprite</b>: Alexah's jungle
	/// <c>New Save</c>'s Jungle Spray on (42, 25) turned 270 held its riders carried under action 6 on lane nodes 2 and 1
	/// with their sprites at (421.995, 265.127) and (421.995, 255.931); its Laughing Hyenas on (30, 27) turned 180, on
	/// node 2 at (295.052, 274.420); its Strength Bird on (41, 29), under action 5 on node 2 at (420.000, 297.500).
	/// </summary>
	[TestMethod]
	public void ACarriedRiderStandsWhereTheOriginalsSaveHoldsTheSprite()
	{
		var data = GameData.Required();

		foreach ( var (directory, stem, cellX, cellY, angle, action, node, x, z) in new (string, string, int, int, int, short, short, float, float)[]
		{
			("levels/jungle/sideshow/junspray", "junspray", 42, 25, 270, 6, 2, 421.995f, 265.127f),
			("levels/jungle/sideshow/junspray", "junspray", 42, 25, 270, 6, 1, 421.995f, 255.931f),
			("levels/jungle/sideshow/hyenas", "hyenas", 30, 27, 180, 6, 2, 295.052f, 274.420f),
			("levels/jungle/sideshow/squark", "squark", 41, 29, 0, 5, 2, 420.000f, 297.500f),
		} )
		{
			var nodes = RideNodes.Load( directory, stem, data, false, [] )!;
			nodes.Place( ParkObjects.OriginFor( cellX, cellY, angle ), angle );

			var slot = new RideScript.WalkView( RideScript.WalkState.Carried, action, 1, WalkNode: 4, HeadNode: node, OffFrom: node,
				OffTo: 4, Start: 0f, Due: 0f, Facing: 0 );
			var placed = WalkRiders.Of( slot )!.Value;

			nodes.Find( placed.From, placed.FromSpace, out var from );
			nodes.Find( placed.To, placed.ToSpace, out var to );

			var at = WalkRiders.Point( placed, from, to, WalkRiders.Progress( slot, placed, 0f ) )!.Value;

			Assert.AreEqual( x, at.X, 0.0005f, $"{stem} node {node}" );
			Assert.AreEqual( z, at.Z, 0.0005f, $"{stem} node {node}" );
		}
	}

	/// <summary>
	/// <b>The Mayan Spinner's first rider stands on its own node, not at the steps</b>: <c>Lookout.RSE</c> walks a rider
	/// on under action 6 to node <c>VAR_ONRIDE</c> plus 2, so the first carried rider of a Spinner on (57, 23) is placed
	/// on node 2 at (581.190, 242.909), on animation 5, facing 1, the way the node points from the step after it arrives.
	/// </summary>
	[TestMethod]
	public void TheSpinnersFirstRiderStandsOnItsOwnNode()
	{
		var data = GameData.Required();
		var nodes = RideNodes.Load( "levels/jungle/rides/lookout", "lookout", data, false, [] )!;
		nodes.Place( ParkObjects.OriginFor( 57, 23, 0 ), 0 );

		var script = new RideScript( Build( walkSlots: 2, Word( Opcode.WALKON ), 42, 1, 2, 2, 16, 6, 1, Word( Opcode.END ) ) ) { Nodes = nodes };

		script.Turn( 0f );
		script.StepTheWalks( 100_000f );

		// The step that finds the rider arrived changes the state alone; the next takes the node's facing.
		script.StepTheWalks( 100_031f );

		Assert.IsTrue( script.TryWalkOf( 42, out var slot ) );
		Assert.AreEqual( RideScript.WalkState.Carried, slot.State );

		var placed = WalkRiders.Of( slot )!.Value;

		nodes.Find( placed.From, placed.FromSpace, out var from );
		nodes.Find( placed.To, placed.ToSpace, out var to );

		var at = WalkRiders.Point( placed, from, to, WalkRiders.Progress( slot, placed, 100_000f ) )!.Value;

		Assert.AreEqual( (2, 5, (short)1), (placed.To, placed.Animation, placed.Facing) );
		Assert.AreEqual( 581.190f, at.X, 0.0005f );
		Assert.AreEqual( 242.909f, at.Z, 0.0005f );
	}

	/// <summary>
	/// <b>A ride being sold still answers for its walk</b>: the sale unlinks the object before its riders are put off
	/// (<see cref="ParkBuilding"/>, then <see cref="ParkPeople.ThingRemoved"/>), so the walk holding a rider is found
	/// through the thing going, not the park's objects, and the put-off sound plays where the placer left them
	/// (<c>FUN_004faa00</c>; run 7 review, the desk run that heard it at the feet).
	/// </summary>
	/// <remarks><b>Mutations:</b> the thing going not asked (the walk looked for among the park's objects alone).</remarks>
	[TestMethod]
	public void ARideBeingSoldStillAnswersForItsWalk()
	{
		var data = GameData.Required();
		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		var world = new ParkWorld( new SaveReader( stream ).ReadFile() );
		using var clock = new SimulationClockScope();
		var previousState = ParkState.Current;
		var previousPeople = ParkPeople.Current;
		ParkPeople? people = null;

		try
		{
			var state = new ParkState( world );
			var script = new RideScript( Build( walkSlots: 2, 0 ) );

			people = new ParkPeople( world, state: state, scriptFor: id => id == 13 ? script : null, random: new Random( 1 ),
				behaviourRandom: new Random( 2 ), rideRandom: new Random( 3 ), staffRandom: new Random( 4 ) );

			script.RestoreRiders( new SavedScript( 1, 0, 0, [], 0, 0, 0, [], 0, 0, 0, 0, 0, Walk:
			[
				new SavedWalkSlot( 1, 2, 2, 16, 5000, 5700, 29, 1, (short)RideScript.WalkState.Carried, 1, 0 ),
				default,
			] ), reading => reading != 0 ? reading - 5000f : null );

			Assert.IsTrue( people.TryWalkOf( 29, out _, out _ ), "held while the ride stands" );

			state.RemoveObject( 13 );

			Assert.IsFalse( people.TryWalkOf( 29, out _, out _ ), "the park's objects no longer name the ride" );
			Assert.IsTrue( people.TryWalkOf( 29, out var holder, out _, goingThing: 13 ) && holder == script, "the thing going does" );
			Assert.AreEqual( true, ParkGuestSprites.WalkedOnRide( people, 29, 13 )?.Hidden, "action 1 hides the rider: heard at the origin" );
		}
		finally
		{
			people?.Delete();
			Entity.ApplyDeletions();
			typeof( ParkPeople ).GetProperty( "Current", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic )!.SetValue( null, previousPeople );
			typeof( ParkState ).GetProperty( nameof( ParkState.Current ) )!.SetValue( null, previousState );
		}
	}

	/// <summary>
	/// <b>The drawing finds the ride that holds a rider wherever it stands in the park's list</b>: the walk, the head a
	/// walk hung and the bounce seat (<see cref="ParkPeople.TryWalkOf"/>, <see cref="ParkPeople.TryHeadOf"/>,
	/// <see cref="ParkPeople.TrySeatOf"/>) each look through every placed object, and are asked of every guest every
	/// frame. The ride is made the first, a middle and the last of the stock park's objects in turn, and each answers each time.
	/// </summary>
	/// <remarks><b>Mutations:</b> any of the three loops starting at the second object or stopping before the last.</remarks>
	[TestMethod]
	public void TheLookupsFindTheHoldingRideAtAnyPlaceInTheList()
	{
		var data = GameData.Required();
		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		var world = new ParkWorld( new SaveReader( stream ).ReadFile() );
		using var clock = new SimulationClockScope();
		var previousState = ParkState.Current;
		var previousPeople = ParkPeople.Current;
		ParkPeople? people = null;

		try
		{
			var state = new ParkState( world );
			var things = state.Objects.Select( thing => thing.ThingId ).ToArray();
			Assert.IsTrue( things.Length >= 3, "the control: a list with a first, a middle and a last" );

			var (holding, script) = (0, (RideScript?)null);

			people = new ParkPeople( world, state: state, scriptFor: id => id == holding ? script : null, random: new Random( 1 ),
				behaviourRandom: new Random( 2 ), rideRandom: new Random( 3 ), staffRandom: new Random( 4 ) );

			RideScript Holding( SavedScript saved, bool model = false )
			{
				var made = new RideScript( Build( bounceSlots: 2, walkSlots: 2, [0] ) );

				if ( model )
					made.Nodes = RideNodes.Load( "levels/jungle/Rides/tvsim", "tvsim", data, true, [] )!;

				made.RestoreRiders( saved, reading => reading != 0 ? reading - 5000f : null );

				return made;
			}

			var walk = Holding( new SavedScript( 1, 0, 0, [], 0, 0, 0, [], 0, 0, 0, 0, 0, Walk:
			[
				new SavedWalkSlot( 1, 2, 2, 16, 5000, 5700, 29, 6, (short)RideScript.WalkState.Carried, 1, 0 ),
				default,
			] ) );
			var heads = Holding( new SavedScript( 1, 0, 0, [], 0, 0, 0, [], 0, 0, 0, 0, 0, Walk:
			[
				new SavedWalkSlot( 1, 3, 3, 1, 5000, 5700, 29, 4, (short)RideScript.WalkState.Carried, 1, 0 ),
				default,
			] ), model: true );
			var seats = Holding( new SavedScript( 1, 0, 0, [], 0, 0, 0, [], 0, 0, 0, 0, 0,
				Bounce: [new SavedBounceSlot( 29, 2, 9000, 8000 ), default] ) );

			foreach ( var place in new[] { 0, things.Length / 2, things.Length - 1 } )
			{
				holding = things[place];

				script = walk;
				Assert.IsTrue( people.TryWalkOf( 29, out var walker, out _ ) && walker == walk, $"the walk, ride at place {place} of {things.Length}" );
				Assert.IsFalse( people.TryWalkOf( 30, out _, out _ ), "and not another guest's" );

				script = heads;
				Assert.IsTrue( people.TryHeadOf( 29, out var hung, out var node ) && hung == heads && node == 3, $"the head, ride at place {place}" );
				Assert.IsFalse( people.TryHeadOf( 30, out _, out _ ), "and not another guest's" );

				script = seats;
				Assert.IsTrue( people.TrySeatOf( 29, out var ride, out var seat ) && ride == holding && seat == 2, $"the seat, ride at place {place}" );
				Assert.IsFalse( people.TrySeatOf( 30, out _, out _ ), "and not another guest's" );
			}

			holding = 0;
			Assert.IsFalse( people.TryWalkOf( 29, out _, out _ ), "the control: no ride holds the guest when none is named" );
		}
		finally
		{
			people?.Delete();
			Entity.ApplyDeletions();
			typeof( ParkPeople ).GetProperty( "Current", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic )!.SetValue( null, previousPeople );
			typeof( ParkState ).GetProperty( nameof( ParkState.Current ) )!.SetValue( null, previousState );
		}
	}

	/// <summary>
	/// <b>A sweep hands a carried guest the placer's animation, and frees a hidden one's bubble</b>
	/// (<c>FUN_004f9e60</c>, <c>FUN_004f9ed0</c>): in the stock park, guest 29 riding thing 13 carried under action 6 is on
	/// animation 5 (script 140) after eight sweeps; carried under action 1, the bubble a thought gave them on the sweep
	/// before is gone after one, which the needs turn's expiry would not take for twelve.
	/// </summary>
	[DataTestMethod]
	[DataRow( (short)6 )]
	[DataRow( (short)1 )]
	public void ASweepHandsACarriedGuestThePlacersAnimation( short action )
	{
		var data = GameData.Required();
		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		var world = new ParkWorld( new SaveReader( stream ).ReadFile() );
		using var clock = new SimulationClockScope();
		var previousState = ParkState.Current;
		var previousPeople = ParkPeople.Current;
		ParkPeople? people = null;

		try
		{
			var state = new ParkState( world );
			var script = new RideScript( Build( walkSlots: 2, 0 ) );

			people = new ParkPeople( world, state: state, scriptFor: id => id == 13 ? script : null, random: new Random( 1 ),
				behaviourRandom: new Random( 2 ), rideRandom: new Random( 3 ), staffRandom: new Random( 4 ) );

			// After the people are made, which puts the park file's own riders back into the scripts.
			script.RestoreRiders( new SavedScript( 1, 0, 0, [], 0, 0, 0, [], 0, 0, 0, 0, 0, Walk:
			[
				new SavedWalkSlot( 1, 2, 2, 16, 5000, 5700, 29, action, (short)RideScript.WalkState.Carried, 1, 0 ),
				default,
			] ), reading => reading != 0 ? reading - 5000f : null );

			var guest = people.Peeps.Single( peep => peep.ThingId == 29 );
			guest.SetState( PeepState.Riding, 0, new Random( 1 ) );

			var sprite = people.SpriteFor( 29 );
			Assert.IsNotNull( sprite );
			Assert.IsFalse( sprite.IsOn( 5 ), "not on it before" );
			Assert.IsTrue( people.TryWalkOf( 29, out var holder, out _ ) && holder == script );

			Assert.IsTrue( guest.Thoughts.Set( 1, state.GameTick ), "a bubble to free" );

			for ( var sweep = 0; sweep < (action == 6 ? 8 : 1); ++sweep )
			{
				SimulationClockScope.Frame( 8 * GameClock.TickSeconds );
				people.Update();
			}

			if ( action == 6 )
			{
				Assert.IsTrue( sprite.IsOn( 5 ), $"script {sprite.Script}" );
				Assert.AreEqual( 140, sprite.Script );
			}
			else
			{
				Assert.IsNull( guest.Thoughts.Bubble, "the hidden rider's bubble is freed" );
			}
		}
		finally
		{
			people?.Delete();
			Entity.ApplyDeletions();
			typeof( ParkPeople ).GetProperty( "Current", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic )!.SetValue( null, previousPeople );
			typeof( ParkState ).GetProperty( nameof( ParkState.Current ) )!.SetValue( null, previousState );
		}
	}

	/// <summary>
	/// Runs <paramref name="body"/> with a stock park's people whose Mayan Spinner's script (the <c>Lookout</c> model on
	/// (57, 23), its players loaded) holds <paramref name="slots"/>, the clock at 10 ticks and a bit; the statics put back.
	/// </summary>
	private static void WithTheSpinnersWalks( SavedWalkSlot[] slots, Action<ParkPeople, RideAnimations, RideNodes> body )
	{
		const string Lookout = "levels/jungle/rides/lookout";

		var data = GameData.Required();
		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		var world = new ParkWorld( new SaveReader( stream ).ReadFile() );
		using var clock = new SimulationClockScope();
		var previousState = ParkState.Current;
		var previousPeople = ParkPeople.Current;
		ParkPeople? people = null;

		try
		{
			var players = RideAnimations.Load( Lookout, "lookout", data, 1 );
			var nodes = RideNodes.Load( Lookout, "lookout", data, false, players.AllClips )!;
			nodes.Place( ParkObjects.OriginFor( 57, 23, 0 ), 0 );

			var script = new RideScript( Build( walkSlots: slots.Length, 0 ) ) { Nodes = nodes, Animations = players };
			var state = new ParkState( world );

			people = new ParkPeople( world, state: state, scriptFor: id => id == 13 ? script : null, random: new Random( 1 ),
				behaviourRandom: new Random( 2 ), rideRandom: new Random( 3 ), staffRandom: new Random( 4 ) );

			script.RestoreRiders( new SavedScript( 1, 0, 0, [], 0, 0, 0, [], 0, 0, 0, 0, 0, Walk: slots ),
				reading => reading != 0 ? reading - 5000f : null );

			SimulationClockScope.Frame( 0.3125f );
			Assert.AreEqual( 10, GameClock.Ticks, "ten ticks and 2.5 ms owed" );

			body( people, players, nodes );
		}
		finally
		{
			people?.Delete();
			Entity.ApplyDeletions();
			typeof( ParkPeople ).GetProperty( "Current", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic )!.SetValue( null, previousPeople );
			typeof( ParkState ).GetProperty( nameof( ParkState.Current ) )!.SetValue( null, previousState );
		}
	}

	/// <summary>
	/// <b>Where the walk holds a visitor, they are drawn there</b> (<see cref="ParkGuestSprites.WalkedOnRide"/>, through
	/// <c>NodeAt</c>): on the Mayan Spinner (<c>Lookout</c>, on (57, 23)), a rider carried under action 6 on node 2 stands at
	/// (581.190, 242.909) and, as the park's axes have it, the model's y is their height; a rider carried under action 1 is
	/// hidden; a rider walking on is a share of the way from node 1 to node 2 by the park's clock (10.08 ticks of 31 ms of a
	/// 700 ms leg); and when the drum (role 6) has risen the carried rider has risen with it, the node's stored matrix
	/// where its clip left it. Nobody the walk does not hold is placed by it.
	/// </summary>
	/// <remarks>
	/// <b>Mutations (each fails here):</b> <c>( point.X, point.Y, point.Z )</c> for <c>( point.X, point.Z, point.Y )</c>; the
	/// hidden arm returning the slot's placement; <c>NodeAt</c> without <c>Follow</c> or with <c>carried: false</c>; the
	/// clock in seconds (<c>* 1000f</c> dropped); the facing dropped.
	/// </remarks>
	[TestMethod]
	public void AWalkHoldingAVisitorPlacesThemOnTheirNodeAndTheDrumCarriesThem()
	{
		WithTheSpinnersWalks(
		[
			new SavedWalkSlot( 1, 2, 2, 16, 5000, 5700, 900, 6, (short)RideScript.WalkState.Carried, 1, 5 ),
			new SavedWalkSlot( 1, 2, 2, 16, 5000, 5700, 901, 1, (short)RideScript.WalkState.Carried, 1, 3 ),
			new SavedWalkSlot( 1, 2, 2, 16, 5000, 5700, 902, 6, (short)RideScript.WalkState.WalkingOn, 1, 6 ),
		], ( people, players, nodes ) =>
		{
			nodes.Find( 1, RideNodes.WalkSpace, out var walkNode );
			nodes.Find( 2, RideNodes.WalkSpace, out var headNode );

			var carried = ParkGuestSprites.WalkedOnRide( people, 900 );

			Assert.IsTrue( carried is { Hidden: false } );
			Assert.AreEqual( (short)5, carried!.Value.Facing );
			Assert.AreEqual( 581.190f, carried.Value.At.X, 0.001f );
			Assert.AreEqual( 242.909f, carried.Value.At.Y, 0.001f, "the model's z is the park's y" );
			Assert.AreEqual( headNode.Y, carried.Value.At.Z, 0.001f, "the model's y is the park's height" );

			var hidden = ParkGuestSprites.WalkedOnRide( people, 901 );

			Assert.IsTrue( hidden is { Hidden: true } );
			Assert.AreEqual( (short)3, hidden!.Value.Facing );

			// A leg of 0 to 700 ms at 10 ticks and 2.5 ms of 31 ms ticks: ( 310 + 2.5 ) * 1000 / 700 thousandths of the way.
			var now = ((GameClock.Ticks * 31f) + (GameClock.PartialTick * 31f));
			var share = (int)(now * 1000f / 700f);
			var walking = ParkGuestSprites.WalkedOnRide( people, 902 );

			Assert.AreEqual( 446, share, "the clock the test stands the walk at" );
			Assert.IsTrue( walking is { Hidden: false } );
			Assert.AreEqual( (short)6, walking!.Value.Facing );
			Assert.AreEqual( walkNode.X + ((headNode.X - walkNode.X) * share / 1000f), walking.Value.At.X, 0.01f );
			Assert.AreEqual( walkNode.Z + ((headNode.Z - walkNode.Z) * share / 1000f), walking.Value.At.Y, 0.01f );
			Assert.AreEqual( walkNode.Y + ((headNode.Y - walkNode.Y) * share / 1000f), walking.Value.At.Z, 0.01f );

			Assert.IsNull( ParkGuestSprites.WalkedOnRide( people, 903 ), "a visitor no walk holds" );
			Assert.IsNull( ParkGuestSprites.WalkedOnRide( null, 900 ) );

			// The drum (role 6) has risen 20 units in two seconds, and the rider with it.
			players.Trigger( 6, 0, AnimTimeControl.StartAtOnceFlag, 1f, 0 );
			players.Advance( 2000 );

			var risen = ParkGuestSprites.WalkedOnRide( people, 900 )!.Value;

			Assert.AreEqual( 581.190f, risen.At.X, 0.001f, "it rises, it does not turn" );
			Assert.AreEqual( headNode.Y + 20.11f, risen.At.Z, 0.01f, "20.379 less the node's 0.269 at rest, plus its own" );
		} );
	}

	/// <summary>
	/// <b>A frame draws the visitors a walk holds where and how it places them</b> (<see cref="ParkGuestSprites.WriteTheFrame"/>):
	/// of three visitors, the one carried under action 1 is not drawn; the one carried under action 6 on node 2 is drawn
	/// at the walk's point and facing 0, where the control with no walk is drawn at its own place and facing; and a second
	/// carried under 6 facing 2 is drawn another picture than the first.
	/// </summary>
	/// <remarks>
	/// <b>Mutations (each fails here):</b> <c>if ( walked is { Hidden: true } ) continue;</c> deleted; <c>walked?.Facing ??</c>
	/// dropped from the picture; <c>walked?.At ??</c> dropped from the centre.
	/// </remarks>
	[TestMethod]
	public void AFrameDrawsAWalkedVisitorWhereAndHowTheWalkPlacesThem()
	{
		var data = GameData.Required();
		var bankFile = ParkGuestSprites.BanksIn( data, ParkGuestSprites.FolderFor( 0, "jungle" )!, 0 )[0];
		var bank = new SpriteBankFile( new MemoryStream( data.ReadAllBytes( bankFile ) ) );
		var pictures = Enumerable.Range( 0, 1024 ).Select( i => new ParkGuestSprites.Region( 0.25f, 0.1f, 0.5f, 0.3f, i + 1, 50, 22, 42 ) ).ToArray();

		var all = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public;
		var cameraField = typeof( Camera ).GetField( "CameraMode", all )!;
		var cameraBefore = cameraField.GetValue( null );
		var statics = new[] { typeof( ParkBumperBoats ), typeof( ParkGround ), typeof( ParkObjects ) }
			.Select( type => type.GetProperty( "Current", all )! ).ToArray();
		var staticsBefore = statics.Select( property => property.GetValue( null ) ).ToArray();

		try
		{
			foreach ( var property in statics )
				property.SetValue( null, null );

			WithTheSpinnersWalks(
			[
				new SavedWalkSlot( 1, 2, 2, 16, 5000, 5700, 901, 1, (short)RideScript.WalkState.Carried, 1, 3 ),
				new SavedWalkSlot( 1, 2, 2, 16, 5000, 5700, 902, 6, (short)RideScript.WalkState.Carried, 1, 0 ),
				new SavedWalkSlot( 1, 2, 2, 16, 5000, 5700, 903, 6, (short)RideScript.WalkState.Carried, 1, 2 ),
			], ( people, players, nodes ) =>
			{
				Camera.SetCameraMode<CameraMode>();
				((CameraMode)cameraField.GetValue( null )!).Position = new OpenTPW.Vector3( 100f, -40f, 7f );

				var pool = new ParkGuestSprites( "jungle", null );
				pool.Resize( 6 );

				var loaded = typeof( ParkGuestSprites ).GetNestedType( "Loaded", all )!;
				((System.Collections.IDictionary)typeof( ParkGuestSprites ).GetField( "_banks", all )!.GetValue( pool )!)[(0, 0)] = Activator.CreateInstance( loaded, bank, pictures );

				var drawn = (System.Collections.Generic.List<(ParkWorld.Person Person, ParkWorld.Sprite Sprite)>)typeof( ParkGuestSprites ).GetField( "_people", all )!.GetValue( pool )!;

				foreach ( var thing in new[] { 900, 901, 902, 903 } )
					drawn.Add( (new ParkWorld.Person( thing, 0, 0, 0, thing, 0, default!, null ),
						new ParkWorld.Sprite( thing, 0, 0, 0, 300f, 200f, 0f, 0, 0, 255, 0, 0, 0 )) );

				var (used, _) = pool.WriteTheFrame();
				var seen = pool.Drawn.ToDictionary( one => one.Thing );

				Assert.AreEqual( 3, used, "the visitor carried under action 1 is not drawn" );
				Assert.IsFalse( seen.ContainsKey( 901 ) );

				var placed = ParkGuestSprites.WalkedOnRide( people, 902 )!.Value;

				Assert.AreEqual( placed.At.X, seen[902].Origin.X, 0.001f, "at the walk's point" );
				Assert.AreEqual( placed.At.Y, seen[902].Origin.Y, 0.001f );
				Assert.AreEqual( placed.At.Z, seen[902].Origin.Z, 0.001f );
				Assert.AreNotEqual( placed.At.X, seen[900].Origin.X, 1f, "the control stands at its own place" );
				Assert.AreNotEqual( seen[902].Width, seen[903].Width, "facing 0 and facing 2 are other pictures" );
			} );
		}
		finally
		{
			cameraField.SetValue( null, cameraBefore );

			for ( var i = 0; i < statics.Length; ++i )
				statics[i].SetValue( null, staticsBefore[i] );
		}
	}

	private static int Word( Opcode opcode ) => unchecked( (int)(0x80000000u | (uint)opcode) );

	/// <summary>A script declaring <paramref name="walkSlots"/> walk slots, two variables, and <paramref name="body"/>.</summary>
	private static RideScriptFile Build( int walkSlots, params int[] body ) => Build( 0, walkSlots, body );

	private static RideScriptFile Build( int bounceSlots, int walkSlots, int[] body )
	{
		using var memory = new MemoryStream();
		using var writer = new BinaryWriter( memory );

		writer.Write( Encoding.ASCII.GetBytes( "RSSE" ) );
		writer.Write( 0x00010F51 );
		writer.Write( 2 );
		writer.Write( 8 );
		writer.Write( 50 );
		writer.Write( 0 );
		writer.Write( bounceSlots );
		writer.Write( walkSlots );
		writer.Write( Encoding.ASCII.GetBytes( "Pad Pad Pad Pad " ) );
		writer.Write( body.Length );

		foreach ( var word in body )
			writer.Write( word );

		writer.Write( 0 );

		for ( var i = 0; i < 2; ++i )
		{
			var name = Encoding.ASCII.GetBytes( $"VAR_{i}\0" );

			writer.Write( name.Length );
			writer.Write( name );
		}

		writer.Flush();

		return new RideScriptFile( new MemoryStream( memory.ToArray() ) );
	}
}
