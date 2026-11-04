using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace OpenTPW.Tests;

/// <summary>
/// A guest's put-off, yawn and till are heard at their x and y and the height nought on raised ground, as a member of staff's
/// voice is (Q847; <c>docs/exe/audio.md</c>, "The height of a person's sound"): the sprite's <c>+0x8c</c> is nought for anyone the
/// walk places (<c>0x004f9ffd</c>), so the sound is not at the ground's height. The stock park's plateau at cells (40..64, 58) stands
/// 20.0, where a guest made by the console's <c>admit</c> stands at (405, 585); the guests of the shipped park walk on flat ground,
/// where nought and the ground are the same, so only here can a site that takes the ground be told from one that does not.
/// </summary>
/// <remarks>
/// Each test puts the real heightfield under <see cref="ParkGround.Current"/> (the entity is not built: nothing here draws) and
/// reads the voice the game's own audio started. Skipped where there is no installation - see <see cref="GameData"/>.
/// </remarks>
[TestClass]
public class ParkGuestSoundHeightTests
{
	private BaseFileSystem data = null!;

	[TestInitialize]
	public void MountTheGame() => FileSystem = data = GameData.Required();

	private const string Theme = "jungle";

	private const int BellyBounce = 13;

	/// <summary>The shipped park's guest whose id divides by 16 (and by 8): the one that yawns and is put off at the kids' effect.</summary>
	private const int Sixteen = 32;

	private const float PlateauX = 405f;

	private const float PlateauY = 585f;

	private sealed record Park( ParkWorld World, ParkState State, ParkItemCatalogue Catalogue, ParkPeople People );

	private Park Open()
	{
		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		var world = new ParkWorld( new SaveReader( stream ).ReadFile() );
		var state = new ParkState( world );
		var catalogue = new ParkItemCatalogue( Theme, data );
		var people = new ParkPeople( world, new ParkBalance( Theme, easyMode: true ), gateStatus: null, state, catalogue );

		return new( world, state, catalogue, people );
	}

	/// <summary>
	/// Runs <paramref name="body"/> with the park's real heightfield as the ground, an audio that starts voices, and guest
	/// <see cref="Sixteen"/> pinned to the plateau; hands it the park, the guest and the voices started since.
	/// </summary>
	private void OnThePlateau( Action<Park, Peep, Func<System.Collections.Generic.List<Voice>>> body )
	{
		var ready = typeof( Audio ).GetProperty( nameof( Audio.Ready ) )!;
		var current = typeof( ParkGround ).GetProperty( nameof( ParkGround.Current ) )!;
		var heightfield = typeof( ParkGround ).GetProperty( nameof( ParkGround.Heightfield ) )!;
		var groundBefore = current.GetValue( null );

		Assert.IsFalse( Audio.Ready, "a test run has no audio device" );

		System.Collections.Generic.List<Voice> before;

		lock ( Audio.Lock )
			before = [.. Audio.Voices];

		ParkAudio? audio = null;
		Park? park = null;

		try
		{
			using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/terrain/base.MD2" ) );
			var field = new HeightfieldFile( stream );
			var ground = (ParkGround)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject( typeof( ParkGround ) );

			heightfield.SetValue( ground, field );
			current.SetValue( null, ground );

			Assert.AreEqual( 20.0f, ParkGuestSprites.StandsOn( field, PlateauX, PlateauY ), 0.001f, "the control: the plateau stands 20.0" );

			ready.SetValue( null, true );
			audio = new ParkAudio( Theme );
			park = Open();

			var guest = park.People.Peeps.Single( peep => peep.ThingId == Sixteen );

			Assert.IsTrue( park.People.PinGuest( Sixteen, PlateauX, PlateauY, field.CellSizeX, field.CellSizeY ), "the guest walks and is stood on the plateau" );

			body( park, guest, () =>
			{
				lock ( Audio.Lock )
					return Audio.Voices.Where( voice => !before.Contains( voice ) ).ToList();
			} );
		}
		finally
		{
			if ( park != null )
			{
				park.People.Delete();
				Entity.ApplyDeletions();
			}

			audio?.Delete();
			Entity.ApplyDeletions();

			lock ( Audio.Lock )
				Audio.Voices.RemoveAll( voice => !before.Contains( voice ) );

			ready.SetValue( null, false );
			current.SetValue( null, groundBefore );
		}
	}

	private static void AssertHeardOnThePlateau( System.Collections.Generic.IEnumerable<Voice> voices, string what )
	{
		var places = voices.Select( voice => voice.Place ).ToList();

		Assert.AreEqual( 1, places.Count, $"{what}: one voice" );
		Assert.AreEqual( PlateauX, places[0]!.Value.X, 0.01f, $"{what}: x" );
		Assert.AreEqual( PlateauY, places[0]!.Value.Y, 0.01f, $"{what}: y" );
		Assert.AreEqual( 0f, places[0]!.Value.Z, 0.0001f, $"{what}: nought, not the plateau's 20.0" );
	}

	/// <summary>A yawn (spot animation 4, an id divisible by 16) is heard at the guest's x and y and nought.</summary>
	/// <remarks><b>Mutation:</b> <see cref="PeepBehaviour.PlaySpotAnimation"/> sounding at the ground's height fails here.</remarks>
	[TestMethod]
	public void AGuestsYawnOnRaisedGroundIsHeardAtNought()
	{
		OnThePlateau( ( park, guest, voices ) =>
		{
			Assert.IsTrue( PeepBehaviour.Yawns( PeepBehaviour.SpotBored, guest.ThingId ), "the control: this guest yawns" );

			park.People.Behaviour.PlaySpotAnimation( guest, PeepBehaviour.SpotBored, tick: 1 );

			AssertHeardOnThePlateau( voices(), "the yawn" );
		} );
	}

	/// <summary>A queuer whose id divides by eight, put off by the thing's sale, is heard at their feet: x and y and nought.</summary>
	/// <remarks><b>Mutation:</b> the <see cref="PutOffSound.Feet"/> arm of <see cref="ParkPeople.ThingRemoved"/> sounding at the ground's height fails here.</remarks>
	[TestMethod]
	public void AQueuersPutOffOnRaisedGroundIsHeardAtNought()
	{
		OnThePlateau( ( park, guest, voices ) =>
		{
			Assert.IsTrue( park.State.TryObject( BellyBounce, out var bounce ) );

			guest.MajorDest = BellyBounce;
			guest.SetState( PeepState.InQueue, tick: 1, new Random( 1 ) );
			guest.QueuePos = park.State.JoinQueue( BellyBounce, guest.ThingId );
			park.People.PinGuest( Sixteen, PlateauX, PlateauY, 10f, 10f );

			Assert.AreEqual( ParkPeople.PutOffSound.Feet,
				ParkPeople.SoundFor( PeepBehaviour.PutOff.Queueing, guest.ThingId, bounce.Flags, seated: false ), "the control: the kids' sound at their feet" );

			var heard = new System.Collections.Generic.List<OpenTPW.Vector3>();
			park.People.PutOffHeard = heard.Add;
			park.People.ThingRemoved( bounce );

			CollectionAssert.AreEquivalent( new[] { guest.ThingId },
				park.People.Peeps.Where( peep => peep.State == PeepState.Deciding && peep.ThingId == Sixteen ).Select( peep => peep.ThingId ).ToArray(),
				"the queuer was put off" );

			var here = voices().Where( voice => voice.Place is { } place && Math.Abs( place.X - PlateauX ) < 0.01f && Math.Abs( place.Y - PlateauY ) < 0.01f ).ToList();

			AssertHeardOnThePlateau( here, "the put-off" );
			Assert.IsTrue( heard.Contains( new OpenTPW.Vector3( PlateauX, PlateauY, 0f ) ), "the seam's place is the same" );
		} );
	}

	/// <summary>A guest put out of a closed ride's queue (the turn-away) is heard at their feet: x and y and nought.</summary>
	/// <remarks><b>Mutation:</b> <see cref="ParkPeople.PutOffAtTheirFeet"/> sounding at the ground's height fails here.</remarks>
	[TestMethod]
	public void AGuestTurnedAwayOnRaisedGroundIsHeardAtNought()
	{
		OnThePlateau( ( park, guest, voices ) =>
		{
			Assert.IsTrue( park.State.TryObject( BellyBounce, out var bounce ) );

			guest.MajorDest = BellyBounce;
			guest.SetState( PeepState.InQueue, tick: 1, new Random( 1 ) );
			park.People.PinGuest( Sixteen, PlateauX, PlateauY, 10f, 10f );

			ParkPeople.PutOffAtTheirFeet( guest, bounce );

			AssertHeardOnThePlateau( voices(), "the turn-away" );
		} );
	}

	/// <summary>The paying guest's till is asked for at their x and y and nought.</summary>
	/// <remarks><b>Mutation:</b> <see cref="ParkRideOperation.TillPlace"/> at the ground's height fails here.</remarks>
	[TestMethod]
	public void AGuestsTillOnRaisedGroundIsHeardAtNought()
	{
		OnThePlateau( ( park, guest, voices ) =>
		{
			var operation = new ParkRideOperation( park.State, park.People.Peeps.ToDictionary( peep => peep.ThingId ) );
			var at = operation.TillPlace( guest );

			Assert.AreEqual( new OpenTPW.Vector3( PlateauX, PlateauY, 0f ), at!.Value );
			Assert.IsTrue( ParkAudio.Current!.Charge( at.Value ), "and the game's till starts there" );
			AssertHeardOnThePlateau( voices(), "the till" );
		} );
	}

	/// <summary>A ride's settle-up sounds the till at the pinned guest's x and y and nought, through the game's own <c>Charge</c> and the default place.</summary>
	/// <remarks><b>Mutation:</b> <see cref="ParkRideOperation"/>'s till site taking <see cref="ParkGuestSprites.StandsOn"/> for the height fails here. No walk is handed in, so the guest is not put down at the exit and the pin holds.</remarks>
	[TestMethod]
	public void ARidesSettleUpOnRaisedGroundSoundsTheTillAtNought()
	{
		OnThePlateau( ( park, guest, voices ) =>
		{
			guest.SetState( PeepState.Riding, tick: 1, new Random( 1 ) );

			var ride = park.World.Objects.Single( o => o.ThingId == BellyBounce ) with { PricePerUse = 20 };
			using var stream = data.OpenRead( "levels/jungle/rides/bouncy/bouncy.RSE" );
			var script = new RideScript( new RideScriptFile( stream ) );

			script.Set( ParkRideOperation.DismissVariable, Sixteen );

			var operation = new ParkRideOperation( park.State, park.People.Peeps.ToDictionary( peep => peep.ThingId ) );

			Assert.IsTrue( operation.Dismiss( script, ride, tick: 9, new Random( 1 ) ), "the guest is let off" );
			AssertHeardOnThePlateau( voices(), "the ride's till" );
		} );
	}
}
