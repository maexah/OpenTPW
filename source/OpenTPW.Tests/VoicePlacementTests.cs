using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;

namespace OpenTPW.Tests;

/// <summary>
/// Which voices a park's pause takes away, and which it leaves - <see cref="Audio.HoldPlaced"/>, and
/// <see cref="ParkAudio"/> calling it as the world is held.
///
/// <para>
/// <b>The rule is not a convenience, it is the original's own reach.</b> A park's pause acts through
/// the LISTENER: it sets <c>DAT_00803ad2</c>, and the per-frame listener update <c>FUN_0051c1d0</c>
/// then replaces the listener's height with 10000.0. A listener cannot reach a sound that was never
/// placed against it, so "has a position" is exactly the set of sounds that mechanism can touch -
/// which is why <see cref="Audio.HoldPlaced"/> selects on <see cref="Voice.IsPlaced"/> and not on a
/// bus, a category or a list of names.
/// </para>
/// <para>
/// <b>Without an audio device.</b> A test run has none, so the tests that play stand in for one by setting
/// <see cref="Audio.Ready"/> for their length. Everything they reach - <see cref="Audio.Play"/>,
/// <see cref="Audio.HoldPlaced"/>, <see cref="Audio.StopAll"/> - works on the voice list alone and opens
/// nothing, and each takes its voices back out of <see cref="Audio.Voices"/> afterwards. The hold is read
/// from <see cref="Voice.IsHeld"/>. What happens to a held voice in the mixer - its 10 ms fade - is not
/// reached: <c>Voice.MixInto</c> is <c>unsafe</c> and this project does not set <c>AllowUnsafeBlocks</c>.
/// </para>
/// </summary>
[TestClass]
public class VoicePlacementTests
{
	/// <summary>Where the placed voices here sound from - a real cell of Lost Kingdom, and any would do.</summary>
	private static readonly Vector3 Somewhere = new( 510f, 230f, 0f );

	/// <summary>The voices a test played, which it takes back out of <see cref="Audio.Voices"/>.</summary>
	private readonly List<Voice> _played = new();

	[TestInitialize]
	public void MountTheGame()
	{
		// SoundBank reads through the GLOBAL file system, as SoundCategoryTests does and for the same
		// reason: the game has exactly one, so a test wanting real samples has to put one there.
		FileSystem = GameData.Required();
	}

	/// <summary>
	/// Runs <paramref name="play"/> as if there were a device, then lets go of the hold, takes back every voice it
	/// played, and leaves the clock as a scene change leaves it.
	/// </summary>
	private void WithADevice( Action play )
	{
		var ready = typeof( Audio ).GetProperty( nameof( Audio.Ready ) )!;

		Assert.IsFalse( Audio.Ready, "a test run has no audio device, so nothing here should find one" );

		ready.SetValue( null, true );

		try
		{
			play();
		}
		finally
		{
			Audio.HoldPlaced( false );

			lock ( Audio.Lock )
				Audio.Voices.RemoveAll( _played.Contains );

			ready.SetValue( null, false );
			GameClock.Rebase();
		}
	}

	/// <summary>Plays <paramref name="clip"/> through <see cref="Audio.Play"/>, somewhere or flat.</summary>
	private Voice Played( AudioClip clip, bool loop, AudioBus bus, Vector3? position )
	{
		var voice = Audio.Play( clip, 1f, loop, 0f, bus, position );

		Assert.IsNotNull( voice, "with a device standing in, Play plays" );

		_played.Add( voice! );

		return voice!;
	}

	/// <summary>
	/// A real sample, because <see cref="AudioClip"/> cannot be built any other way - its constructor
	/// is private and <see cref="AudioClip.Decode"/> is the only road in, which wants real MPEG bytes.
	/// This is the bank the screams themselves come out of.
	/// </summary>
	private static AudioClip AScream()
	{
		var bank = SoundBank.Load( "global", "Sound/Kids" );

		Assert.IsNotNull( bank, "global/Sound/KidsHD.sdt is the bank cat_kids names, and it ships" );
		Assert.AreNotEqual( 0, bank!.Count, "the kids bank decoded to no samples at all" );

		var clip = bank[0];

		Assert.IsNotNull( clip, "the first sample of the kids bank would not decode" );

		return clip!;
	}

	/// <summary>
	/// A voice given somewhere to sound from is placed - so a pause takes it, as the listener would.
	/// </summary>
	[TestMethod]
	public void AVoiceGivenAPlaceIsPlaced()
	{
		var voice = new Voice( AScream(), 1f, loop: true, 0f, AudioBus.Effects,
			new Vector3( 510f, 230f, 0f ) );

		Assert.IsTrue( voice.IsPlaced,
			"a voice built with a position must be placed - it is what a park's pause selects on" );
	}

	/// <summary>
	/// And one with nowhere is flat, so a pause leaves it. This is the assertion that keeps the music,
	/// the rain and the interface's own sounds audible behind an open menu - the last of those
	/// mattering more than it looks, since the menu holding the world is what plays them.
	/// </summary>
	[TestMethod]
	public void AVoiceWithNoPlaceIsNotPlaced()
	{
		var voice = new Voice( AScream(), 1f, loop: false, 0f, AudioBus.Music, position: null );

		Assert.IsFalse( voice.IsPlaced,
			"a voice built with no position must be flat - the music and the rain are played this way" );
	}

	/// <summary>
	/// The two answers have to DIFFER, which is the anti-vacuity guard: an <c>IsPlaced</c> stuck at
	/// either constant passes one of the two tests above, and a reader skimming a green run would not
	/// see which. Same clip, same everything but the place.
	/// </summary>
	[TestMethod]
	public void ThePlaceIsWhatDecidesIt()
	{
		var clip = AScream();

		var placed = new Voice( clip, 1f, loop: true, 0f, AudioBus.Effects, new Vector3( 1f, 2f, 3f ) );
		var flat = new Voice( clip, 1f, loop: true, 0f, AudioBus.Effects, position: null );

		Assert.AreNotEqual( placed.IsPlaced, flat.IsPlaced,
			"two voices differing only in whether they were given a place must not answer the same" );
	}

	/// <summary>
	/// The origin is a PLACE, not a way of saying "nowhere" - <see cref="Audio.Play"/>'s own remarks
	/// say so, and they are right for a reason worth pinning: (0,0,0) is a real corner of the map, and
	/// a null is what means flat. Getting this backwards would silence the origin's sounds on a pause
	/// and nothing else, which is subtle enough to survive a listen.
	/// </summary>
	[TestMethod]
	public void TheOriginIsAPlaceLikeAnyOther()
	{
		var voice = new Voice( AScream(), 1f, loop: false, 0f, AudioBus.Effects, Vector3.Zero );

		Assert.IsTrue( voice.IsPlaced, "a voice at (0,0,0) is placed there, not unplaced" );
	}

	/// <summary>
	/// <b>A hold takes the placed voices and leaves the flat ones</b>, and letting go gives the placed ones
	/// back where they were: a ride's scream and a one-shot stop behind the menu while the music and the rain
	/// carry on.
	/// </summary>
	/// <remarks>
	/// The flat voices are played first, so a walk that stopped at the first of them would reach neither placed
	/// one; one of each kind loops and one does not, so a walk that picked by looping would hold the wrong ones.
	/// <b>Mutations:</b> emptying <see cref="Audio.HoldPlaced"/>, taking out or loosening its test of
	/// <see cref="Voice.IsPlaced"/>, stopping at the first flat voice, or letting go without resuming, each fail
	/// an assertion here.
	/// </remarks>
	[TestMethod]
	public void AHoldTakesThePlacedVoicesAndLeavesTheFlatOnes()
	{
		var clip = AScream();

		WithADevice( () =>
		{
			var music = Played( clip, loop: true, AudioBus.Music, position: null );
			var rain = Played( clip, loop: true, AudioBus.Effects, position: null );
			var scream = Played( clip, loop: false, AudioBus.Effects, Somewhere );
			var ride = Played( clip, loop: true, AudioBus.Effects, Somewhere + new Vector3( 40f, 0f, 0f ) );

			Assert.IsFalse( scream.IsHeld || ride.IsHeld, "nothing is held before the hold" );

			Audio.HoldPlaced( true );

			Assert.IsTrue( scream.IsHeld, "the placed one-shot is held" );
			Assert.IsTrue( ride.IsHeld, "and so is the placed loop" );
			Assert.IsFalse( music.IsHeld, "the music carries on" );
			Assert.IsFalse( rain.IsHeld, "and so does the rain" );

			Audio.HoldPlaced( false );

			Assert.IsFalse( scream.IsHeld || ride.IsHeld, "letting go gives the placed voices back" );
			Assert.IsTrue( scream.Playing && ride.Playing, "still sounding, from where they were held" );
		} );
	}

	/// <summary>
	/// <b>A park holds its placed sounds while its clock is held</b>, and lets them go when it runs again -
	/// <see cref="ParkAudio"/>'s update asks for the hold from <see cref="GameClock.Paused"/>, which a park's
	/// menu sets. The same frame with the clock running is the control.
	/// </summary>
	/// <remarks>
	/// The park's audio is built before the device stands in, so it loads nothing and has nothing of its own to
	/// play; its update still asks for the hold first. <b>Mutations:</b> taking the call out of that update, or
	/// passing it anything but <see cref="GameClock.Paused"/>, fails an assertion here.
	/// </remarks>
	[TestMethod]
	public void AParkHoldsItsPlacedSoundsWhileItsClockIsHeld()
	{
		var clip = AScream();
		var park = new ParkAudio( "jungle" );

		try
		{
			WithADevice( () =>
			{
				var scream = Played( clip, loop: false, AudioBus.Effects, Somewhere );

				GameClock.Update( paused: false, GameClock.ParkCatchUp );
				park.Update();

				Assert.IsFalse( scream.IsHeld, "a running park leaves its screams alone" );

				GameClock.Update( paused: true, GameClock.ParkCatchUp );
				park.Update();

				Assert.IsTrue( scream.IsHeld, "a held park holds them" );

				GameClock.Update( paused: false, GameClock.ParkCatchUp );
				park.Update();

				Assert.IsFalse( scream.IsHeld, "and running again lets them go" );
			} );
		}
		finally
		{
			park.Delete();
			Entity.ApplyDeletions();
		}
	}

	/// <summary>
	/// <b>A voice started while the world is held is born held if it is placed, and plays if it is flat</b> - so
	/// a scream that starts behind the menu waits for it, and the menu's own clicks, which are flat, are heard.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> <see cref="Audio.Play"/> not holding a placed voice born during a hold, or holding
	/// every voice born during one, fails an assertion here.
	/// </remarks>
	[TestMethod]
	public void AVoiceStartedDuringAHoldIsBornHeldOnlyIfPlaced()
	{
		var clip = AScream();

		WithADevice( () =>
		{
			Audio.HoldPlaced( true );

			var scream = Played( clip, loop: false, AudioBus.Effects, Somewhere );
			var click = Played( clip, loop: false, AudioBus.Effects, position: null );

			Assert.IsTrue( scream.IsHeld, "a placed voice started during the hold is born held" );
			Assert.IsFalse( click.IsHeld, "a flat one plays" );
		} );
	}

	/// <summary>
	/// <b>A scene that ends while the world is held does not carry the hold into the next</b>: a placed voice
	/// the next scene starts is heard.
	/// </summary>
	/// <remarks><b>Mutations:</b> <see cref="Audio.StopAll"/> not letting go of the hold fails here.</remarks>
	[TestMethod]
	public void ASceneEndingWhileHeldDoesNotCarryTheHold()
	{
		var clip = AScream();

		WithADevice( () =>
		{
			Audio.HoldPlaced( true );
			Audio.StopAll( 0f );

			var next = Played( clip, loop: false, AudioBus.Effects, Somewhere );

			Assert.IsFalse( next.IsHeld, "the next scene's first placed voice plays" );
		} );
	}
}
