using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenTPW.Tests;

/// <summary>
/// Which voices a park's pause takes away, and which it leaves - <c>docs/CLEANUP-PLAN.md</c> item 6.
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
/// <b>WHAT THIS DOES NOT PIN, said plainly because it is most of the feature</b> (and recorded here
/// per <c>docs/VERIFYING.md</c> rule 48, which asks for the mutations expected to SURVIVE). Two
/// mutations survive this class and are known to:
/// </para>
/// <list type="bullet">
/// <item>Deleting <c>Audio.HoldPlaced( GameClock.Paused )</c> from <c>ParkAudio.OnUpdate</c> - the
/// wiring - leaves every test here green. Nothing in the suite drives a park's pause.</item>
/// <item>Emptying the body of <see cref="Audio.HoldPlaced"/> does the same. Both it and
/// <c>Audio.Play</c> need an <b>audio device</b> (<c>Audio.Ready</c>), and a test run has none, so a
/// voice can never reach <c>Audio.Voices</c> for the walk to find.</item>
/// </list>
/// <para>
/// That is not a hole to paper over with a weaker assertion: it is why the item says a unit test here
/// is regression cover and never the proof. What proves it is the capture -
/// <c>~/.cache/tpw-harnesses/screampause.py</c>, which measures the mixer's own output either side of
/// a real park menu, with the game clock proven frozen. The mutation that WOULD fail this class is
/// the one it exists for: changing which voices are selected.
/// </para>
/// <para>
/// The mixer half needs no cover of its own - <see cref="Voice.Pause"/> and its 10 ms ramp have been
/// carrying the advisor since long before this, which is the whole reason item 6 needed no new
/// mechanism. It cannot be reached from here anyway: <c>Voice.MixInto</c> is <c>unsafe</c> and this
/// project does not set <c>AllowUnsafeBlocks</c>.
/// </para>
/// </summary>
[TestClass]
public class VoicePlacementTests
{
	[TestInitialize]
	public void MountTheGame()
	{
		// SoundBank reads through the GLOBAL file system, as SoundCategoryTests does and for the same
		// reason: the game has exactly one, so a test wanting real samples has to put one there.
		FileSystem = GameData.Required();
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
}
