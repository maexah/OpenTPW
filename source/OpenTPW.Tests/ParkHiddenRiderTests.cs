using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// <b>A rider on a thing without flag bit <c>0x20</c> has no body</b> (Q52): admission tests the thing's
/// <c>RideHandlesSprite</c> bit (<c>0x0050212b</c>) and, without it, destroys the rider's sprite (<c>0x00502147</c>;
/// <c>docs/exe/park-engine.md</c>, the sale's riders). <see cref="ParkPeople.RidesWithoutASprite(int)"/> is the rule, and
/// <see cref="ParkGuestSprites"/> draws and reports by it.
/// </summary>
[TestClass]
public class ParkHiddenRiderTests
{
	private const int BellyBounce = 13, DrinksShop = 16;

	/// <summary>
	/// The stock park with a people of its own; the Drinks Shop's flags written over to <paramref name="shopFlags"/>, so a
	/// thing without the bit stands beside the Belly Bounce, which keeps it.
	/// </summary>
	private static void WithThePark( int shopFlags, Action<ParkPeople, Peep[]> body, Func<int, RideScript?>? scriptFor = null )
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

			Assert.IsTrue( state.TryObject( DrinksShop, out var shop ) );
			Assert.AreEqual( 0x24, shop.Flags, "the stock Drinks Shop keeps its riders' sprites" );
			Assert.IsTrue( state.ReplaceObject( shop with { Flags = (ushort)shopFlags } ) );

			people = new ParkPeople( world, state: state, scriptFor: scriptFor ?? (_ => null), random: new Random( 1 ),
				behaviourRandom: new Random( 2 ), rideRandom: new Random( 3 ), staffRandom: new Random( 4 ) );

			body( people, [.. people.Peeps.Take( 4 )] );
		}
		finally
		{
			people?.Delete();
			Entity.ApplyDeletions();
			typeof( ParkPeople ).GetProperty( "Current", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic )!.SetValue( null, previousPeople );
			typeof( ParkState ).GetProperty( nameof( ParkState.Current ) )!.SetValue( null, previousState );
		}
	}

	private static void Put( Peep peep, int thing, PeepState state )
	{
		peep.MajorDest = thing;
		peep.SetState( state, tick: 1, new Random( 1 ) );
	}

	/// <summary>
	/// <b>Only riding, and only on a thing whose flags lack <c>0x20</c>.</b> The shop's flags are the Crazy Ape's,
	/// <c>0xc</c>, and then <c>0x10c</c> (the coasters', other bits set, <c>0x20</c> clear): its rider has no sprite; the
	/// Belly Bounce's rider (<c>0x12c</c>) and the shop's queuer and walker-on do; and a rider named on a thing the park
	/// does not hold has none, as the writer has it.
	/// </summary>
	/// <remarks>
	/// <b>Mutations (each fails here):</b> the state test dropped (the queuer is hidden); the flag test as
	/// <c>Flags != 0</c> (the shop's rider shows); <c>KeepsRidersSpriteFlag</c> read as <c>0x10</c> (the Bounce's rider,
	/// <c>0x12c</c>, is hidden).
	/// </remarks>
	[TestMethod]
	[DataRow( 0x0c )]
	[DataRow( 0x10c )]
	public void ARiderOnAThingWithoutTheBitHasNoSprite( int shopFlags )
	{
		WithThePark( shopFlags, ( people, peeps ) =>
		{
			Put( peeps[0], DrinksShop, PeepState.Riding );
			Put( peeps[1], BellyBounce, PeepState.Riding );
			Put( peeps[2], DrinksShop, PeepState.InQueue );
			Put( peeps[3], DrinksShop, PeepState.EnteringRide );

			Assert.IsTrue( people.RidesWithoutASprite( peeps[0].ThingId ), "riding a thing without the bit" );
			Assert.IsFalse( people.RidesWithoutASprite( peeps[1].ThingId ), "riding the Belly Bounce, which keeps it" );
			Assert.IsFalse( people.RidesWithoutASprite( peeps[2].ThingId ), "queueing for it" );
			Assert.IsFalse( people.RidesWithoutASprite( peeps[3].ThingId ), "walking on" );
			Assert.IsTrue( people.RidesWithoutASprite( PeepState.Riding, 9999 ), "a thing the park does not hold" );
			Assert.IsFalse( people.RidesWithoutASprite( 9999 ), "nobody" );
		} );
	}

	/// <summary>
	/// <b>A frame draws no body for a rider on a thing without the bit, and the <c>guests</c> census says so</b>
	/// (<see cref="ParkGuestSprites.WriteTheFrame"/>, <see cref="ParkGuestSprites.Census"/>): of four guests, the shop's
	/// rider (flags <c>0xc</c>) is not among the drawn and casts no shadow; the Bounce's rider, the shop's queuer and a
	/// guest left as the file has them are drawn. The census line of the hidden one alone names it.
	/// </summary>
	/// <remarks>
	/// <b>Mutations (each fails here):</b> the <c>RidesWithoutASprite</c> test in the frame deleted (four are drawn); the
	/// census suffix dropped.
	/// </remarks>
	[TestMethod]
	public void AFrameDrawsNoBodyForARiderOnAThingWithoutTheBit()
	{
		InAFrame( null, ( people, peeps ) => { }, ( people, peeps, pool, written ) =>
		{
			var drawn = pool.Drawn.Select( one => one.Thing ).ToHashSet();

			CollectionAssert.AreEquivalent( new[] { peeps[1].ThingId, peeps[2].ThingId, peeps[3].ThingId }, drawn.ToArray(),
				"the shop's rider alone is not drawn" );
			Assert.IsFalse( pool.ShadowsDrawn.Any( shadow => shadow.Thing == peeps[0].ThingId ), "nor their shadow" );

			var lines = pool.Census().ToArray();

			Assert.AreEqual( 1, lines.Count( line => line.Contains( "RIDES WITHOUT A SPRITE" ) ) );
			StringAssert.Contains( lines.Single( line => line.Contains( "RIDES WITHOUT A SPRITE" ) ), $"thing {peeps[0].ThingId,2} " );
		} );
	}

	/// <summary>
	/// <b>A head a ride hangs is still drawn for a rider on a thing without the bit</b>: the hide sits after the head
	/// branches of <see cref="ParkGuestSprites.WriteTheFrame"/>. The shop (flags <c>0xc</c>) runs a script whose
	/// <c>ADDHEAD</c> hangs its rider's head on a head node of the Crazy Ape's model (the Crazy Ape is itself one of the 15
	/// without the bit); the frame writes that head's quad where the node stands and no body for them, beside the three
	/// others' bodies.
	/// </summary>
	/// <remarks>
	/// <b>Mutation (fails here):</b> the hide moved above the two head branches (the head is not written: three quads, all
	/// bodies).
	/// </remarks>
	[TestMethod]
	public void AHeadARideHangsIsStillDrawnForARiderOnAThingWithoutTheBit()
	{
		var data = GameData.Required();
		var monkey = new ParkItemCatalogue( "jungle", data ).All.Single( item => item.Stem == "monkey" );
		var nodes = RideNodes.Load( monkey.Directory, monkey.Stem, data, false, [] )!;
		nodes.PlaceFixed();
		RideScript? hanging = null;
		(OpenTPW.Vector3 At, int Frame, System.Numerics.Vector3 Towards)? hung = null;

		InAFrame( thing => thing == DrinksShop ? hanging : null, ( people, peeps ) =>
		{
			hanging = new RideScript( RideScriptHeadTests.Build(
				RideScriptHeadTests.Word( Opcode.ADDHEAD ), peeps[0].ThingId,
				RideScriptHeadTests.Word( Opcode.END ) ) ) { Nodes = nodes, ThingId = DrinksShop };
			hanging.SizeHeads( 16 );
			hanging.SeedRandom( 1 );
			hanging.Turn( 0f );

			hung = ParkGuestSprites.HeadOnRide( people, peeps[0].ThingId );
			Assert.IsNotNull( hung, "the shop's script holds the rider's head on a posed node" );
		}, ( people, peeps, pool, written ) =>
		{
			CollectionAssert.AreEquivalent( new[] { peeps[1].ThingId, peeps[2].ThingId, peeps[3].ThingId },
				pool.Drawn.Select( one => one.Thing ).ToArray(), "no body for the shop's rider" );
			Assert.AreEqual( 4 + pool.BalloonsDrawn.Count, written.Used, "three bodies and the one head" );

			// The quads are written furthest first; the bodies stand at (300, 200), the head on its node by the origin.
			var at = hung!.Value.At.GetSystemVector3();
			var middles = Enumerable.Range( 0, written.Used )
				.Select( quad => pool.Written.Vertices.AsSpan( quad * 4, 4 ).ToArray()
					.Aggregate( System.Numerics.Vector3.Zero, ( sum, corner ) => sum + corner.Position.GetSystemVector3() ) / 4f )
				.ToArray();

			Assert.AreEqual( 1, middles.Count( middle => System.Numerics.Vector3.Distance( middle, at ) < 5f ),
				$"one quad, the head, at its node {at}: {string.Join( ", ", middles )}" );
		} );
	}

	/// <summary>
	/// The stock park with the shop's flags at <c>0xc</c>, four guests (the shop's rider, the Bounce's rider, the shop's
	/// queuer and one left as the file has them) in one loaded bank, a head bank beside it, and one frame written.
	/// </summary>
	private static void InAFrame( Func<int, RideScript?>? scriptFor, Action<ParkPeople, Peep[]> before,
		Action<ParkPeople, Peep[], ParkGuestSprites, (int Used, int Reach)> after )
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

			WithThePark( 0x0c, ( people, peeps ) =>
			{
				Put( peeps[0], DrinksShop, PeepState.Riding );
				Put( peeps[1], BellyBounce, PeepState.Riding );
				Put( peeps[2], DrinksShop, PeepState.InQueue );

				Camera.SetCameraMode<CameraMode>();
				((CameraMode)cameraField.GetValue( null )!).Position = new OpenTPW.Vector3( 100f, -40f, 7f );

				before( people, peeps );

				var pool = new ParkGuestSprites( "jungle", null );
				pool.Resize( 6 );

				var loaded = typeof( ParkGuestSprites ).GetNestedType( "Loaded", all )!;
				var banks = (System.Collections.IDictionary)typeof( ParkGuestSprites ).GetField( "_banks", all )!.GetValue( pool )!;
				banks[(0, 0)] = Activator.CreateInstance( loaded, bank, pictures );
				banks[ParkGuestSprites.HeadOf( 0, 0 )] = Activator.CreateInstance( loaded, bank, pictures );

				var listed = (System.Collections.Generic.List<(ParkWorld.Person Person, ParkWorld.Sprite Sprite)>)typeof( ParkGuestSprites ).GetField( "_people", all )!.GetValue( pool )!;

				// Every one in the one bank loaded, so that only the rule decides who is drawn.
				foreach ( var peep in peeps )
					(peep.SpriteKind, peep.SpriteBank) = (0, 0);

				foreach ( var peep in peeps )
					listed.Add( (new ParkWorld.Person( peep.ThingId, 0, 0, 0, peep.ThingId, 0, default!, null ),
						new ParkWorld.Sprite( peep.ThingId, 0, 0, 0, 300f, 200f, 0f, 0, 0, 255, 0, 0, 0 )) );

				var written = pool.WriteTheFrame();

				after( people, peeps, pool, written );
			}, scriptFor );
		}
		finally
		{
			cameraField.SetValue( null, cameraBefore );

			for ( var i = 0; i < statics.Length; ++i )
				statics[i].SetValue( null, staticsBefore[i] );
		}
	}
}
