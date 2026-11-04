using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace OpenTPW.Tests;

/// <summary>
/// The sounds a ride script starts with <c>ADDOBJ</c> and <c>EVENT</c>, and stops with <c>KILLOBJ</c>, <c>FADEOBJ</c>
/// and its end (<c>docs/exe/audio.md</c>, "A script's sounds"), played with a device stood in as
/// <see cref="VoicePlacementTests"/> stands one in.
/// </summary>
[TestClass]
public class ScriptSoundTests
{
	private const string Theme = "jungle";

	/// <summary>
	/// The class an effect's flags word makes (<c>0x006b6774</c>..<c>0x006b67f2</c>): bit <c>0x4</c> clear is a one-shot
	/// whatever else is set, and with it set <c>0x10</c> wins, then <c>0x2</c> splits the rest by <c>0x400</c> and <c>0x100</c>,
	/// and without <c>0x2</c> <c>0x400</c> is the held chain and anything else the chain of any variation.
	/// </summary>
	[DataTestMethod]
	[DataRow( 0x0, "OneShot", null )]
	[DataRow( 0x8, "OneShot", null )]
	[DataRow( 0x208, "OneShot", null )]
	[DataRow( 0x412, "OneShot", null )]
	[DataRow( 0x6, "Plain", null )]
	[DataRow( 0x206, "Plain", null )]
	[DataRow( 0x406, "Music", null )]
	[DataRow( 0x606, "Music", null )]
	[DataRow( 0x106, "NotBuilt", "RIDE_EFFECT_SOUND_CLASS_6BE330" )]
	[DataRow( 0x416, "NotBuilt", "RIDE_EFFECT_SOUND_CLASS_6BE680" )]
	[DataRow( 0x404, "HeldChain", null )]
	[DataRow( 0x604, "HeldChain", null )]
	[DataRow( 0x504, "HeldChain", null )]
	[DataRow( 0x104, "Chain", null )]
	[DataRow( 0x204, "Chain", null )]
	[DataRow( 0x4, "Chain", null )]
	public void AnEffectsFlagsPickItsVoicesClass( int flags, string kind, string? gap )
	{
		var (made, named) = ParkAudio.ClassOf( flags );

		Assert.AreEqual( kind, made.ToString() );
		Assert.AreEqual( gap, named );
	}

	private static int Word( Opcode opcode ) => unchecked( (int)( 0x80000000u | (uint)opcode ) );

	/// <summary>A whole .RSE file of one variable, as <see cref="RideScriptEffectTests"/> builds one.</summary>
	private static RideScriptFile Build( params int[] body )
	{
		using var memory = new MemoryStream();
		using var writer = new BinaryWriter( memory );

		writer.Write( Encoding.ASCII.GetBytes( "RSSE" ) );
		writer.Write( 0x00010F51 );
		writer.Write( 1 );
		writer.Write( 8 );
		writer.Write( 50 );
		writer.Write( 0 );
		writer.Write( 0 );
		writer.Write( 0 );
		writer.Write( Encoding.ASCII.GetBytes( "Pad Pad Pad Pad " ) );
		writer.Write( body.Length );

		foreach ( var word in body )
			writer.Write( word );

		writer.Write( 0 );

		var name = Encoding.ASCII.GetBytes( "VAR_0\0" );

		writer.Write( name.Length );
		writer.Write( name );
		writer.Flush();

		return new RideScriptFile( new MemoryStream( memory.ToArray() ) );
	}

	private readonly List<Voice> _before = [];

	/// <summary>Runs <paramref name="play"/> with a device stood in and a park's audio up, and takes away every voice it made.</summary>
	private void WithAParksAudio( Action<ParkAudio> play )
		=> FixedRoll.During( 0, dice => WithAParksAudioRolling( play ) );

	/// <summary>
	/// <see cref="WithAParksAudio"/>'s body. Its categories roll 0, the first sample of every list: a roll above a list's
	/// last odds gives no sample (Q723), and global rides 200, 201 and 202 end between 65,527 and 65,534.
	/// </summary>
	private void WithAParksAudioRolling( Action<ParkAudio> play )
	{
		FileSystem = GameData.Required();
		Log ??= new();
		Unimplemented.Forget();

		var ready = typeof( Audio ).GetProperty( nameof( Audio.Ready ) )!;

		Assert.IsFalse( Audio.Ready, "a test run has no audio device" );

		lock ( Audio.Lock )
			_before.AddRange( Audio.Voices );

		ParkAudio? park = null;
		var listener = typeof( Audio ).GetField( "_listener", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static )!;
		var heardFrom = listener.GetValue( null );

		try
		{
			ready.SetValue( null, true );
			park = new ParkAudio( Theme );
			play( park );
		}
		finally
		{
			park?.Delete();
			Entity.ApplyDeletions();

			lock ( Audio.Lock )
				Audio.Voices.RemoveAll( voice => !_before.Contains( voice ) );

			listener.SetValue( null, heardFrom );
			Audio.HoldPlaced( false );
			GameClock.Rebase();
			ready.SetValue( null, false );
			Unimplemented.Forget();
		}
	}

	/// <summary>A park's frame with the world held or not, as <c>Level.Update</c> runs one: the clock, then the park's audio.</summary>
	private static void Frame( ParkAudio park, bool held )
	{
		GameClock.Update( held, GameClock.ParkCatchUp );
		park.Update();
	}

	private static int VoiceCount()
	{
		lock ( Audio.Lock )
			return Audio.Voices.Count;
	}

	private static bool Counted( string what ) => Unimplemented.Summary.Any( entry => entry.What == what );

	/// <summary>The listener put at <paramref name="at"/>, in the world's axes, the height last.</summary>
	private static void HearFrom( Vector3 at ) => Audio.SetListener( at, Vector3.Right );

	/// <summary>The park's draws for its script voices taken from <paramref name="random"/> from now on.</summary>
	private static void DrawFrom( ParkAudio park, Random random )
		=> typeof( ParkAudio ).GetField( "_scriptRandom", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance )!
			.SetValue( park, random );

	/// <summary>
	/// <b>Each type plays in its own category, and a stop takes what its tag names.</b> On the End's model: the global
	/// ambient's 209 looped (flags <c>0x8</c>) on node 1 over its variation's 100; the interface's 186 flat (<c>0x208</c>);
	/// the global rides' 200 of the plain class (<c>0x6</c>) on node 9 over 170, and 202 a one-shot that keeps no record.
	/// The level's ambient holds no 6, so its record is freed, while the global ambient holds a 6. A placed sound of no
	/// node, with no placed thing to stand on, is counted and kept: the level ambient's 177, a held chain, and 201.
	/// </summary>
	[TestMethod]
	public void EachTypePlaysInItsOwnCategoryAndAStopTakesItsTag()
	{
		WithAParksAudio( park =>
		{
			var nodes = RideNodes.Load( "levels/jungle/features/end", "End", (BaseFileSystem)FileSystem, false, [] )!;
			var script = new RideScript( Build(
				Word( Opcode.ADDOBJ ), 8, 1, 209, 1,
				Word( Opcode.ADDOBJ ), 9, -1, 186, 2,
				Word( Opcode.ADDOBJ ), 5, 9, 200, 3,
				Word( Opcode.ADDOBJ ), 4, -1, 6, 4,
				Word( Opcode.ADDOBJ ), 4, -1, 177, 5,
				Word( Opcode.ADDOBJ ), 5, -1, 201, 6,
				Word( Opcode.EVENT ), 5, 10, 202,
				Word( Opcode.END ) ) ) { Effects = new RideEffects(), Nodes = nodes, Id = 903 };

			// In range of both placed starts: 91 from node 1's 209 (its range 100), 125 from node 9's 200 (170).
			HearFrom( new Vector3( 0f, 180f, 0f ) );
			script.Turn( 0f );

			var effects = script.Effects!;

			CollectionAssert.AreEqual( new[] { 6, 5, 3, 2, 1 }, effects.Records.Select( record => record.Tag ).ToArray(),
				"the level ambient's 6 is freed, every other record kept" );
			Assert.AreEqual( 1, effects.Freed );
			Assert.AreEqual( (4, 1, 0, 0, 0), park.ScriptSounds );
			Assert.IsFalse( Counted( "RIDE_EFFECT_SOUND_CLASS_6BDD30" ), "the gates' 177 is a held chain, built" );
			Assert.IsNull( effects.Records[1].Sound, "and with nowhere to stand has no voice" );
			Assert.IsTrue( Counted( "RIDE_EFFECT_SOUND_NO_PLACE" ), "201 has nowhere to stand" );
			Assert.IsNull( effects.Records[0].Sound );

			var looped = effects.Records.Single( record => record.Tag == 1 ).Sound!;
			var flat = effects.Records.Single( record => record.Tag == 2 ).Sound!;
			var plain = effects.Records.Single( record => record.Tag == 3 ).Sound!;

			Assert.IsTrue( looped.Looped && !looped.Plain );
			Assert.AreEqual( new Vector3( 0f, 271f, 61f ), looped.Place, "node 1 at (0.79, 61.63, 271.85) on the model" );
			Assert.AreEqual( 100f, looped.Voice!.Range );
			Assert.IsTrue( looped.Voice.Loop );

			Assert.IsNull( flat.Place, "cat_ui's 186 is flagged 0x200" );
			Assert.IsFalse( flat.Voice!.IsPlaced );

			Assert.IsTrue( plain.Plain && !plain.Looped );
			Assert.AreEqual( new Vector3( 21f, 57f, 16f ), plain.Place );
			Assert.AreEqual( 170f, plain.Voice!.Range );
			Assert.AreEqual( 1, plain.Samples );

			Assert.AreEqual( 4, park.ScriptVoices.Count, "the four started, the event's one-shot among them" );

			var kill = new RideScript( Build( Word( Opcode.KILLOBJ ), 1, Word( Opcode.FADEOBJ ), 3, Word( Opcode.END ) ) );

			// The two stops run on the first script's own list, as the engine's handlers walk the frame's +0xb0.
			script.Effects = null;
			kill.Effects = effects;
			kill.Turn( 0f );

			CollectionAssert.AreEqual( new[] { 6, 5, 2 }, effects.Records.Select( record => record.Tag ).ToArray() );
			Assert.AreEqual( (1, 1), (effects.Stopped, effects.Faded) );
			Assert.AreEqual( (4, 1, 1, 1, 0), park.ScriptSounds );
			Assert.IsTrue( looped.Stopped && plain.Stopped && !flat.Stopped );
			Assert.IsFalse( looped.Voice.Playing, "a kill is a hard stop" );
			Assert.IsTrue( plain.Voice.Ending, "a fade is under way" );

			// And the end of a script stops what is left.
			kill.StopEffects();

			Assert.IsTrue( flat.Stopped );
			Assert.AreEqual( (4, 1, 2, 1, 0), park.ScriptSounds );
		} );
	}

	/// <summary>
	/// <b>A sound on a node stands at the node and is moved only on its script's eighth tick.</b> The End's 201 on its
	/// sound node 10, at (-21.78, 16.59, 57.14) on its model: its height last in the world's axes and each coordinate cut
	/// toward nought; a node the model does not hold frees the record (the engine's "RSSE: Invalid Node ID").
	/// </summary>
	[TestMethod]
	public void ASoundOnANodeStandsThereAndIsMovedOnItsScriptsEighthTick()
	{
		WithAParksAudio( park =>
		{
			var nodes = RideNodes.Load( "levels/jungle/features/end", "End", (BaseFileSystem)FileSystem, false, [] )!;

			var script = new RideScript( Build(
				Word( Opcode.ADDOBJ ), 5, 10, 201, 1,
				Word( Opcode.ADDOBJ ), 5, 40, 201, 2,
				Word( Opcode.END ) ) ) { Effects = new RideEffects(), Nodes = nodes, Id = 904 };

			script.Turn( 0f );

			var record = script.Effects!.Records.Single();

			Assert.AreEqual( 1, record.Tag, "node 40 is not the End's, so its record is freed" );

			var sound = record.Sound!;
			var at = NodePlace;

			Assert.AreEqual( at, sound.Place );
			Assert.AreEqual( at, sound.Voice!.Place );

			var elsewhere = new Vector3( 1f, 2f, 3f );

			park.MoveScriptSound( sound, elsewhere );
			script.SweepEffects( soundsDue: false );

			Assert.AreEqual( elsewhere, sound.Place, "not moved on another tick" );

			script.SweepEffects( soundsDue: true );

			Assert.AreEqual( at, sound.Place, "moved back to its node on its own" );
			Assert.AreEqual( at, sound.Voice.Place );
			Assert.AreEqual( 2, park.ScriptSounds.Moved );
		} );
	}

	/// <summary>
	/// <b>The scheduler moves a script's sounds on that script's own eighth tick and no other</b> (<c>0x00551a1c</c>: the id
	/// against the tick, as a turn is given). Script 3 takes its turns on ticks 3 and 11; its 201 is put elsewhere after the
	/// first, stays there through ticks 4 to 10, and is back on its node on tick 11.
	/// </summary>
	[TestMethod]
	public void TheSchedulerMovesASoundOnlyOnItsScriptsOwnTick()
	{
		WithAParksAudio( park =>
		{
			var nodes = RideNodes.Load( "levels/jungle/features/end", "End", (BaseFileSystem)FileSystem, false, [] )!;
			var script = new RideScript( Build(
				Word( Opcode.ADDOBJ ), 5, 10, 201, 1,
				Word( Opcode.ENDSLICE ), Word( Opcode.BRANCH ), 0x20000005 ) ) { Effects = new RideEffects(), Nodes = nodes, Id = 3 };
			var scheduler = new RideScriptScheduler();

			scheduler.Add( 3, script );

			for ( var tick = 1; tick <= 3; ++tick )
				scheduler.Advance( 0f );

			var sound = script.Effects!.Records.Single().Sound!;
			var elsewhere = new Vector3( 1f, 2f, 3f );

			Assert.AreEqual( NodePlace, sound.Place, "started on its node on tick 3" );
			park.MoveScriptSound( sound, elsewhere );

			for ( var tick = 4; tick <= 10; ++tick )
			{
				scheduler.Advance( 0f );
				Assert.AreEqual( elsewhere, sound.Place, $"not moved on tick {tick}" );
			}

			scheduler.Advance( 0f );
			Assert.AreEqual( NodePlace, sound.Place, "moved back on tick 11" );
		} );
	}

	/// <summary>
	/// <b>An effect of <c>0x4</c> alone is a chain: a child at once, the next its wait after the last one's start, the one
	/// before playing out, and a stop cuts the newest.</b> The level's <c>cat_rides</c> 100 (the Lava Fountain's and
	/// Eruption's: one variation of eleven samples, volume 68, waits 300 to 700, range 75) and 89 (the Crazy Ape's: volume
	/// 85, waits 500 to 1000, range 100) on the End's node 9. A child is made only once the clock is past the newest one's
	/// time, and a <c>FADEOBJ</c> cuts a chain's newest child as a <c>KILLOBJ</c> does (<c>0x006bcc71</c>).
	/// </summary>
	[TestMethod]
	public void AnEffectOfFourAloneIsAChainOfChildren()
	{
		var nowBefore = Time.Now;

		WithAParksAudio( park =>
		{
			Time.Now = 100f;

			var nodes = RideNodes.Load( "levels/jungle/features/end", "End", (BaseFileSystem)FileSystem, false, [] )!;
			var script = new RideScript( Build(
				Word( Opcode.ADDOBJ ), 3, 9, 100, 40,
				Word( Opcode.ADDOBJ ), 3, 9, 89, 10,
				Word( Opcode.END ) ) ) { Effects = new RideEffects(), Nodes = nodes, Id = 905 };

			script.Turn( 0f );

			var effects = script.Effects!;
			var lava = effects.Records.Single( record => record.Tag == 40 ).Sound!;
			var ape = effects.Records.Single( record => record.Tag == 10 ).Sound!;

			Assert.AreEqual( (2, 0, 0, 0, 0), park.ScriptSounds );
			Assert.IsFalse( Counted( "RIDE_EFFECT_SOUND_CLASS_6BDC60" ) );
			Assert.AreEqual( "chain", lava.Kind );
			Assert.IsTrue( lava.Chain && !lava.Plain && !lava.Looped );
			Assert.AreEqual( 1, lava.Samples, "the first child at once" );
			Assert.AreEqual( ParkAudio.ChannelGain( 68 ), lava.Voice!.Volume, 1e-6f );
			Assert.AreEqual( 75f, lava.Voice.Range );
			Assert.IsFalse( lava.Voice.Loop, "a child plays its one sample once" );
			Assert.AreEqual( new Vector3( 21f, 57f, 16f ), lava.Voice.Place, "where the chain stands, the End's node 9" );
			Assert.AreEqual( ParkAudio.ChannelGain( 85 ), ape.Voice!.Volume, 1e-6f );
			Assert.AreEqual( 100f, ape.Voice.Range );

			var waits = new List<int>();
			var madeAt = 100f;
			var children = new List<Voice> { lava.Voice };

			for ( var turn = 0; turn < 400; ++turn )
			{
				var newest = lava.Voice!;
				var made = lava.Samples;

				waits.Add( lava.Wait );
				Assert.AreEqual( madeAt + (lava.Wait / 1000f), lava.NextAt, 1e-4f, "each child's wait counts from its own start" );

				Time.Now = lava.NextAt;
				park.ServeScriptSounds();
				Assert.AreEqual( made, lava.Samples, "not on the newest child's time itself" );

				Time.Now = lava.NextAt + 0.001f;
				park.ServeScriptSounds();
				Assert.AreEqual( made + 1, lava.Samples, "a child once the clock is past it" );
				Assert.AreNotSame( newest, lava.Voice );
				Assert.IsTrue( newest.Playing && !newest.Ending, "the child before plays out" );
				Assert.AreEqual( ParkAudio.ChannelGain( 68 ), lava.Voice!.Volume, 1e-6f, "each child at the variation's volume" );

				children.Add( lava.Voice );
				madeAt = Time.Now;
			}

			Assert.IsTrue( waits.All( wait => wait >= 300 && wait < 700 ), $"waits {waits.Min()} to {waits.Max()}" );
			Assert.IsTrue( waits.Min() < 320 && waits.Max() > 680, $"waits {waits.Min()} to {waits.Max()} over the variation's range" );
			Assert.IsTrue( ape.Samples > 1, "the Crazy Ape's chain goes on beside it" );

			var kill = new RideScript( Build( Word( Opcode.FADEOBJ ), 40, Word( Opcode.KILLOBJ ), 10, Word( Opcode.END ) ) );
			var lavaNewest = lava.Voice!;
			var apeNewest = ape.Voice!;

			script.Effects = null;
			kill.Effects = effects;
			kill.Turn( 0f );

			Assert.AreEqual( (2, 0, 1, 1, 0), park.ScriptSounds );
			Assert.IsFalse( lavaNewest.Playing, "a chain's fade is a cut" );
			Assert.IsFalse( apeNewest.Playing, "and its kill" );
			Assert.IsTrue( children[^2].Playing, "the child before the newest is not touched" );
			Assert.AreEqual( 0, park.ScriptVoices.Count );

			var counts = (lava.Samples, ape.Samples);

			Time.Now += 10f;
			park.ServeScriptSounds();
			Assert.AreEqual( counts, (lava.Samples, ape.Samples), "a stopped chain makes no child" );
		} );

		Time.Now = nowBefore;
	}

	/// <summary>
	/// <b>An effect of <c>0x404</c> is a held chain: its first child the effect's first variation, each later one drawn
	/// among the zones of the variation before.</b> The level ambient's 177 (the gates' and the first speaker's: nine
	/// variations of one sample, each one's zones naming the other eight over 0 to 100, waits 1000 to 4000, volume 20 to
	/// 100, pitch -4 to 4) and 179 (seven: the first two's zones name 3 to 7 alone, and the third's name 4 to 7 twice
	/// each) on the End's node 9, at the parameter nought. Neither ever repeats the variation before, as a chain of any
	/// variation would, and 179 never goes from its first two to its first two.
	/// </summary>
	[TestMethod]
	public void AnEffectOf404IsAHeldChainWhoseChildrenFollowTheirZones()
	{
		var nowBefore = Time.Now;

		WithAParksAudio( park =>
		{
			Time.Now = 100f;

			var nodes = RideNodes.Load( "levels/jungle/features/end", "End", (BaseFileSystem)FileSystem, false, [] )!;
			var script = new RideScript( Build(
				Word( Opcode.ADDOBJ ), 4, 9, 177, 1,
				Word( Opcode.EVENT ), 4, 9, 179,
				Word( Opcode.END ) ) ) { Effects = new RideEffects(), Nodes = nodes, Id = 906 };

			script.Turn( 0f );

			var gates = script.Effects!.Records.Single( record => record.Tag == 1 ).Sound!;
			var speaker = park.ScriptVoices.Single( sound => sound.Effect == 179 );

			Assert.AreEqual( (2, 0, 0, 0, 0), park.ScriptSounds );
			Assert.IsFalse( Counted( "RIDE_EFFECT_SOUND_CLASS_6BDD30" ) );
			Assert.AreEqual( "held chain", gates.Kind );
			Assert.IsTrue( gates.Chain && gates.Held && !gates.Plain && !gates.Looped );
			Assert.AreEqual( 0, gates.Parameter );
			Assert.AreEqual( (0, 0), (gates.Variation, speaker.Variation), "each first child the effect's first variation" );
			Assert.AreEqual( new Vector3( 21f, 57f, 16f ), gates.Voice!.Place );
			Assert.AreEqual( 100f, gates.Voice.Range );

			var seen177 = new HashSet<int> { gates.Variation };
			var after179 = new Dictionary<int, int[]>();
			var waits = new List<int>();
			var volumes = new HashSet<int>();
			var pitches = new HashSet<int>();

			// Ten thousand turns: the chain is unseeded, and from the third variation 1 and 2 follow one time in five, so
			// the three-to-one bound below fails by chance one run in a hundred at two thousand and in no run at ten thousand,
			// while a doubled zone counted once (two to one) still fails it.
			for ( var turn = 0; turn < 10000; ++turn )
			{
				var (before177, before179) = (gates.Variation, speaker.Variation);
				var made = (gates.Samples, speaker.Samples);

				waits.Add( gates.Wait );
				// A pass a millisecond on as well: the one whose child was due on the first pass's very millisecond had its
				// list emptied there, and makes its child on the next.
				Time.Now = Math.Max( gates.NextAt, speaker.NextAt ) + 0.001f;
				park.ServeScriptSounds();
				Time.Now += 0.001f;
				park.ServeScriptSounds();

				Assert.AreEqual( (made.Item1 + 1, made.Item2 + 1), (gates.Samples, speaker.Samples), "a child each" );
				Assert.AreNotEqual( before177, gates.Variation, "177's zones never name the variation they belong to" );
				seen177.Add( gates.Variation );
				volumes.Add( gates.Kept.Volume );
				pitches.Add( gates.Kept.Pitch );

				after179.TryAdd( before179, new int[7] );
				++after179[before179][speaker.Variation];
			}

			Assert.AreEqual( 9, seen177.Count, "every one of 177's nine" );
			Assert.IsTrue( waits.All( wait => wait >= 1000 && wait < 4000 ), $"waits {waits.Min()} to {waits.Max()}" );
			Assert.IsTrue( volumes.Min() == 20 && volumes.Max() == 99, $"volumes {volumes.Min()} to {volumes.Max()}, a child's own" );
			Assert.IsTrue( pitches.Min() == -4 && pitches.Max() == 3, $"pitches {pitches.Min()} to {pitches.Max()}" );

			foreach ( var from in new[] { 0, 1 } )
				Assert.AreEqual( 0, after179.GetValueOrDefault( from )?.Take( 2 ).Sum() ?? 0, $"179 from {from + 1} only to 3 to 7" );

			// After 179's third, 4 to 7 are named twice each and 1 and 2 once: about two to one.
			var third = after179[2];
			Assert.AreEqual( 0, third[2] );
			Assert.IsTrue( third[3] + third[4] + third[5] + third[6] > 3 * (third[0] + third[1]),
				$"after the third: {string.Join( ",", third )}" );

			// An event's held chain keeps no record: nothing a script asks stops it, and it makes children on.
			script.StopEffects();
			var count = speaker.Samples;

			Time.Now = speaker.NextAt + 0.001f;
			park.ServeScriptSounds();
			Assert.IsTrue( gates.Stopped && !speaker.Stopped );
			Assert.AreEqual( count + 1, speaker.Samples );
		} );

		Time.Now = nowBefore;
	}

	/// <summary>
	/// <b>Types 6 and 7 play in <c>cat_kids</c> and <c>cat_staff</c>, a one-shot at its variation's own volume.</b> The
	/// toilet's 53 is the kids' (volume 38, range 75) and the global rides' too (100, 100); the staff's 165 (24, 60) is in
	/// no other category. A one-shot is played at its variation's volume through the channel law, not at full.
	/// </summary>
	[TestMethod]
	public void TheKidsAndTheStaffPlayInTheirOwnCategoriesAtTheirVariationsVolume()
	{
		WithAParksAudio( park =>
		{
			var nodes = RideNodes.Load( "levels/jungle/features/end", "End", (BaseFileSystem)FileSystem, false, [] )!;
			HearFrom( new Vector3( 21f, 57f, 0f ) );
			var script = new RideScript( Build(
				Word( Opcode.ADDOBJ ), 6, 9, 53, 1,
				Word( Opcode.ADDOBJ ), 7, 9, 165, 2,
				Word( Opcode.END ) ) ) { Effects = new RideEffects(), Nodes = nodes, Id = 910 };

			script.Turn( 0f );

			var effects = script.Effects!;

			Assert.AreEqual( (2, 0, 0, 0, 0), park.ScriptSounds, "both found in their categories" );

			var kids = effects.Records.Single( record => record.Tag == 1 ).Sound!;
			var staff = effects.Records.Single( record => record.Tag == 2 ).Sound!;

			Assert.AreEqual( "one-shot", kids.Kind );
			Assert.AreEqual( 75f, kids.Voice!.Range, "the kids' 53, not the global rides' over 100" );
			Assert.AreEqual( ParkAudio.ChannelGain( 38 ), kids.Voice.Volume, 1e-6f );
			Assert.AreEqual( "one-shot", staff.Kind );
			Assert.AreEqual( 60f, staff.Voice!.Range );
			Assert.AreEqual( ParkAudio.ChannelGain( 24 ), staff.Voice.Volume, 1e-6f );
		} );
	}

	/// <summary>
	/// <b>A plain voice plays one sample after another, each after its silence, at the volume and pitch its first
	/// drew.</b> The global ambient's 2 (the gulls': volume 49, a pitch of -2 to 2, a wait of 0 to 10000 ms) on the End's
	/// node 9, type 8. Each sample, the first included (<c>FUN_006bc410</c> -> <c>0x006bdf00</c>), is followed by a silence
	/// drawn from its variation's wait; the next is not started before it is over, and is started on the pass that finds
	/// it over. The draws are a seeded generator's, whose first wait is seconds long.
	/// </summary>
	[TestMethod]
	public void APlainVoicesNextSampleComesAfterItsSilenceAtItsFirstsVolume()
	{
		var nowBefore = Time.Now;

		WithAParksAudio( park =>
		{
			Time.Now = 100f;
			DrawFrom( park, new Random( 3 ) );
			HearFrom( new Vector3( 21f, 57f, 0f ) );

			var nodes = RideNodes.Load( "levels/jungle/features/end", "End", (BaseFileSystem)FileSystem, false, [] )!;
			var script = new RideScript( Build( Word( Opcode.ADDOBJ ), 8, 9, 2, 1, Word( Opcode.END ) ) )
				{ Effects = new RideEffects(), Nodes = nodes, Id = 911 };

			script.Turn( 0f );

			var gulls = script.Effects!.Records.Single().Sound!;
			var kept = gulls.Kept;

			Assert.AreEqual( "plain", gulls.Kind );
			Assert.AreEqual( (1, 49), (gulls.Samples, kept.Volume) );
			Assert.IsTrue( gulls.Wait is > 1000 and < 10000, $"the first's silence drawn, {gulls.Wait} ms" );
			Assert.AreEqual( ParkAudio.ChannelGain( 49 ), gulls.Voice!.Volume, 1e-6f, "37 of 127, not full" );

			var waits = new List<int>();

			for ( var sample = 2; sample <= 30; ++sample )
			{
				var last = gulls.Voice!;
				var wait = gulls.Wait;

				park.ServeScriptSounds();
				Assert.AreSame( last, gulls.Voice, "nothing new while a sample plays" );

				last.Stop();
				var ended = Time.Now;

				park.ServeScriptSounds();

				if ( wait > 0 )
				{
					Assert.AreEqual( sample - 1, gulls.Samples, "a silence after it" );
					Assert.AreEqual( ended, gulls.EndedAt, "counted from the pass that found the sample ended" );

					Time.Now = ended + (wait / 1000f) - 0.01f;
					park.ServeScriptSounds();
					Assert.AreEqual( sample - 1, gulls.Samples, "silent to its wait's end" );

					Time.Now = ended + (wait / 1000f);
					park.ServeScriptSounds();
				}

				Assert.AreEqual( sample, gulls.Samples );
				Assert.AreNotSame( last, gulls.Voice );
				Assert.AreEqual( kept, gulls.Kept, "every sample at what the first drew" );
				Assert.AreEqual( ParkAudio.ChannelGain( 49 ), gulls.Voice!.Volume, 1e-6f );
				Assert.AreEqual( ParkCarSounds.Rate( kept.Pitch ), (float)gulls.Voice.Rate, 1e-6f );

				waits.Add( gulls.Wait );
				Time.Now += 0.5f;
			}

			Assert.IsTrue( waits.All( wait => wait is >= 0 and < 10000 ), $"waits {waits.Min()} to {waits.Max()}" );
			Assert.IsTrue( waits.Count( wait => wait > 1000 ) > 10, "most of the silences are seconds long" );
		} );

		Time.Now = nowBefore;
	}

	/// <summary>
	/// <b>A plain voice's later samples never draw the sample before when their variation holds more than two</b>
	/// (<c>FUN_006bc680</c> asked with 1, <c>0x006bde33</c>; Q742): the End's global rides 200 on node 9 (eleven samples), its
	/// pick kept in the voice (<c>+0x46</c>) from its first, which asks for no step. A second voice of the same effect keeps
	/// its own pick.
	/// </summary>
	/// <remarks><b>Mutations:</b> the step not asked; the pick not kept; the first draw not stored.</remarks>
	[TestMethod]
	public void APlainVoicesLaterSamplesNeverDrawTheSampleBeforeFromMoreThanTwo()
	{
		var nowBefore = Time.Now;

		WithAParksAudio( park =>
		{
			Time.Now = 100f;
			DrawFrom( park, new Random( 7 ) );
			HearFrom( new Vector3( 21f, 57f, 0f ) );

			var nodes = RideNodes.Load( "levels/jungle/features/end", "End", (BaseFileSystem)FileSystem, false, [] )!;
			var script = new RideScript( Build( Word( Opcode.ADDOBJ ), 5, 9, 200, 1, Word( Opcode.ADDOBJ ), 5, 9, 200, 2, Word( Opcode.END ) ) )
				{ Effects = new RideEffects(), Nodes = nodes, Id = 912 };

			script.Turn( 0f );

			var voices = script.Effects!.Records.Select( record => record.Sound! ).ToArray();

			Assert.AreEqual( 2, voices.Length );
			Assert.IsTrue( voices.All( voice => voice is { Plain: true } && voice.Samples == 1 ), "two plain voices, each on its first sample" );
			Assert.IsTrue( voices.All( voice => voice.Last >= 0 ), "the first draw asks for no step and stores its pick" );

			var before = voices.Select( voice => voice.Last ).ToArray();
			var stepped = 0;

			for ( var sample = 2; sample <= 120; ++sample )
			{
				foreach ( var voice in voices )
					voice.Voice!.Stop();

				park.ServeScriptSounds();

				foreach ( var voice in voices )
					voice.EndedAt = Time.Now - 100f;

				Time.Now += 100f;
				park.ServeScriptSounds();

				for ( var i = 0; i < voices.Length; ++i )
				{
					var voice = voices[i];

					Assert.AreEqual( sample, voice.Samples, $"voice {i}'s sample {sample}" );

					if ( voice.Category.VariationsOf( voice.Effect )[voice.Variation].Samples > 2 )
					{
						Assert.AreNotEqual( before[i], voice.Last, $"voice {i}, sample {sample}: the sample before is not drawn again" );
						++stepped;
					}

					before[i] = voice.Last;
				}
			}

			Assert.IsTrue( stepped > 200, $"effect 200's lists hold more than two samples ({stepped} of 238 draws)" );
		} );

		Time.Now = nowBefore;
	}

	/// <summary>
	/// <b>Through a park's menu a placed chain's clock runs on and its children are made unheard; none is held to start
	/// with the rest on resume.</b> The original's service is posted each frame, pause or not, on wall time, and its range
	/// test is over x and z alone, which the pause's lifted listener leaves alone: its chains go on making children that
	/// its mixer plays at nought (<c>docs/exe/audio.md</c>, "The pause substitution"). The Lava Fountain's 100 (waits 300
	/// to 700 ms) held for ten seconds: no voice is started, the children are drawn on time, and after the resume the next
	/// child comes at the time the running clock gives it, alone.
	/// </summary>
	[TestMethod]
	public void AHeldParksChainMakesItsChildrenUnheardAndNoneBurstsOnResume()
	{
		var nowBefore = Time.Now;

		WithAParksAudio( park =>
		{
			Time.Now = 100f;

			var nodes = RideNodes.Load( "levels/jungle/features/end", "End", (BaseFileSystem)FileSystem, false, [] )!;
			var script = new RideScript( Build( Word( Opcode.ADDOBJ ), 3, 9, 100, 40, Word( Opcode.END ) ) )
				{ Effects = new RideEffects(), Nodes = nodes, Id = 912 };

			script.Turn( 0f );

			var lava = script.Effects!.Records.Single().Sound!;
			var first = lava.Voice!;

			Frame( park, held: true );

			Assert.IsTrue( Audio.PlacedHeld && first.IsHeld, "the menu holds the chain's child" );

			var voices = VoiceCount();

			for ( var pass = 0; pass < 1000; ++pass )
			{
				Time.Now += 0.01f;
				Frame( park, held: true );
				Assert.IsTrue( lava.NextAt > Time.Now - 0.001f && lava.NextAt <= Time.Now + 0.7f, "its clock runs on" );
			}

			Assert.AreEqual( 1, lava.Samples, "no child's voice started under the hold" );
			Assert.AreEqual( voices, VoiceCount(), "and none waits in the pool" );
			Assert.IsTrue( lava.Unheard is >= 14 and <= 34, $"{lava.Unheard} children in ten seconds of waits of 300 to 700 ms" );
			Assert.IsNull( lava.Voice, "its newest child is an unheard one" );

			var due = lava.NextAt;

			Time.Now = due;
			Frame( park, held: false );

			Assert.IsFalse( first.IsHeld, "the resume lets the held child go" );
			Assert.AreEqual( 1, lava.Samples, "not before the newest child's time" );

			Time.Now = due + 0.001f;
			Frame( park, held: false );

			Assert.AreEqual( 2, lava.Samples, "one child on its time" );
			Assert.AreEqual( voices + 1, VoiceCount(), "and only that one" );
			Assert.IsFalse( lava.Voice!.IsHeld );
		} );

		Time.Now = nowBefore;
	}

	/// <summary>
	/// <b>Through a park's menu a plain voice's samples are drawn on time and let pass unheard.</b> The gulls' 2 on the
	/// End's node 9: a sample that ends under the hold is followed by an unheard one, which lasts as its sample would at
	/// its pitch; after the resume the next sample comes once that one's end and its silence are past, and is heard. A
	/// flat one, <c>cat_ui</c>'s 180 (<c>0x206</c>), is not held, and goes on being heard.
	/// </summary>
	[TestMethod]
	public void AHeldParksPlainVoiceLetsItsSamplesPassUnheard()
	{
		var nowBefore = Time.Now;

		WithAParksAudio( park =>
		{
			Time.Now = 100f;

			var nodes = RideNodes.Load( "levels/jungle/features/end", "End", (BaseFileSystem)FileSystem, false, [] )!;
			var script = new RideScript( Build(
				Word( Opcode.ADDOBJ ), 8, 9, 2, 1,
				Word( Opcode.ADDOBJ ), 9, 9, 180, 2,
				Word( Opcode.END ) ) ) { Effects = new RideEffects(), Nodes = nodes, Id = 913 };

			HearFrom( new Vector3( 21f, 57f, 0f ) );
			script.Turn( 0f );

			var gulls = script.Effects!.Records.Single( record => record.Tag == 1 ).Sound!;
			var flat = script.Effects!.Records.Single( record => record.Tag == 2 ).Sound!;

			Assert.IsTrue( flat.Plain && flat.Place is null );

			Frame( park, held: true );

			flat.Voice!.Stop();
			Frame( park, held: true );
			Time.Now += flat.Wait / 1000f;
			Frame( park, held: true );

			Assert.AreEqual( (2, 0), (flat.Samples, flat.Unheard), "a flat voice's next sample is heard under the hold" );
			Assert.IsFalse( flat.Voice!.IsHeld );

			var voices = VoiceCount();

			// The sample ends as the hold begins: the next is due once the first's silence is over.
			gulls.Voice!.Stop();
			Frame( park, held: true );
			Time.Now += gulls.Wait / 1000f;
			Frame( park, held: true );

			Assert.AreEqual( (1, 1), (gulls.Samples, gulls.Unheard), "drawn, and no voice started" );
			Assert.AreEqual( voices, VoiceCount() );
			Assert.IsNull( gulls.Voice );
			Assert.IsTrue( gulls.EndedAt > Time.Now, "an unheard sample lasts as its sample would" );

			for ( var pass = 0; pass < 6000; ++pass )
			{
				Time.Now += 0.01f;
				Frame( park, held: true );
			}

			Assert.AreEqual( 1, gulls.Samples, "a minute held, and not one sample heard" );
			Assert.AreEqual( voices, VoiceCount() );
			Assert.IsTrue( gulls.Unheard > 1, $"{gulls.Unheard} drawn on time" );

			var due = gulls.EndedAt!.Value + (gulls.Wait / 1000f);

			if ( due > Time.Now )
			{
				Frame( park, held: false );
				Assert.AreEqual( 1, gulls.Samples, "the unheard one's end and silence still to come" );
			}

			Time.Now = MathF.Max( due, Time.Now );
			Frame( park, held: false );

			Assert.AreEqual( 2, gulls.Samples, "the next is heard" );
			Assert.IsFalse( gulls.Voice!.IsHeld );
			Assert.AreEqual( voices + 1, VoiceCount() );
		} );

		Time.Now = nowBefore;
	}

	/// <summary>
	/// <b>An effect of <c>0x406</c> is the music's class, and <c>SETOBJPARAM</c>'s parameter picks its zones.</b> The bus's
	/// own <c>ADDOBJ 8 1 6 1</c> and <c>SETOBJPARAM 1 20 0</c> on its model: the global ambient's 6, key 20, first its first
	/// variation at a pitch of -24 (variation 1's controller is key 21, which nothing sets, so its value is nought) that
	/// every later sample keeps, though variations 5 to 7 hold a pitch of nought. At 0 the idles 1 to 4, never the one
	/// before; at 75 the rev, 5, then 6 again and again; back at 0, 7 and then the idles. A set of key 21 on an idle applies
	/// its pitch at once (<c>0x006bbbe0</c>). The handle stays while the voice lives (<c>0x006b6620</c>); past every zone the voice is parked at <c>0x7f</c> and a set to anything else wakes it.
	/// </summary>
	[TestMethod]
	public void AnEffectOf406IsTheMusicsClassWhoseParameterPicksItsZones()
	{
		WithAParksAudio( park =>
		{
			var nodes = RideNodes.Load( "levels/jungle/features/bus", "bus", (BaseFileSystem)FileSystem, false, [] )!;
			var script = new RideScript( Build(
				Word( Opcode.ADDOBJ ), 8, 1, 6, 1,
				Word( Opcode.SETOBJPARAM ), 1, 20, 0,
				Word( Opcode.END ) ) ) { Effects = new RideEffects(), Nodes = nodes, Id = 907 };

			// Beside the bus's sound node, (-183, -284) with the model unplaced.
			HearFrom( new Vector3( -180f, -280f, 0f ) );
			script.Turn( 0f );

			var record = script.Effects!.Records.Single();
			var bus = record.Sound!;
			var rate = MathF.Pow( 2f, -25f / 96f );

			Assert.AreEqual( "music", bus.Kind );
			Assert.IsTrue( bus.Music && bus.Follows && !bus.Plain && !bus.Chain );
			Assert.IsFalse( Counted( "RIDE_EFFECT_SOUND_CLASS_6BE610" ) );
			Assert.AreEqual( (20, 0, 0, 1), (bus.Key, bus.Parameter, bus.Variation, bus.Samples) );
			Assert.AreEqual( -24, bus.Kept.Pitch );
			Assert.AreEqual( rate, (float)bus.Voice!.Rate, 1e-4f, "22050 x 2^(-25/96) = 18408, the original's frequency" );
			Assert.AreEqual( 100f, bus.Voice.Range );
			Assert.AreNotEqual( 0, record.Handle, "the set answers the handle of a voice that lives" );
			Assert.AreEqual( 1, park.ScriptParameterSets );

			int Next()
			{
				bus.Voice!.Stop();
				park.ServeScriptSounds();
				Assert.AreEqual( -24, bus.Kept.Pitch, "a later sample keeps the first's pitch" );
				Assert.AreEqual( rate, (float)bus.Voice.Rate, 1e-4f );
				return bus.Variation;
			}

			void Set( int id, int value )
				=> Assert.IsTrue( park.SetScriptSoundParameter( bus, id, value ), "the voice lives" );

			var idles = new HashSet<int>();

			for ( var sample = 0; sample < 200; ++sample )
			{
				var before = bus.Variation;
				var next = Next();

				Assert.IsTrue( next is >= 0 and <= 3 && next != before, $"{before + 1} then {next + 1} at 0" );
				idles.Add( next );
			}

			Assert.AreEqual( 4, idles.Count, "every one of the four idles" );

			Set( 20, 75 );
			Assert.AreEqual( 75, bus.Parameter );
			Assert.AreEqual( 4, Next(), "the rev" );

			for ( var sample = 0; sample < 5; ++sample )
				Assert.AreEqual( 5, Next(), "the run, and again" );

			Set( 20, 0 );
			Assert.AreEqual( 6, Next(), "the slowing" );
			Assert.IsTrue( Next() is >= 0 and <= 3, "and an idle" );

			// A set past slot 0 (key 21, the idle's controller of its pitch) applies at once: 50 x 60 / 100 - 24 = 6, and
			// the parameter is left as it was.
			var applies = park.ScriptParameterApplies;
			Set( 21, 50 );
			Assert.AreEqual( (0, 50, 6, applies + 1), (bus.Parameter, bus.Values[1], bus.Kept.Pitch, park.ScriptParameterApplies) );
			Assert.AreEqual( MathF.Pow( 2f, 7f / 96f ), (float)bus.Voice!.Rate, 1e-4f );

			// No idle's zone holds 101: parked at 0x7f, and nothing more plays until a set to anything else.
			Set( 20, 101 );
			var samples = bus.Samples;

			bus.Voice!.Stop();
			park.ServeScriptSounds();
			park.ServeScriptSounds();
			Assert.IsTrue( bus.Parked );
			Assert.AreEqual( (ParkAudio.ParkedParameter, samples), (bus.Parameter, bus.Samples) );

			Set( 20, ParkAudio.ParkedParameter );
			park.ServeScriptSounds();
			Assert.AreEqual( samples, bus.Samples, "a set to 0x7f leaves it parked" );

			Set( 20, 0 );
			park.ServeScriptSounds();
			Assert.IsFalse( bus.Parked );
			Assert.AreEqual( samples + 1, bus.Samples, "woken by the set, it plays" );

			// Stopped, the voice is gone and the set answers nought.
			script.StopEffects();
			Assert.IsFalse( park.SetScriptSoundParameter( bus, 20, 75 ) );
		} );
	}

	/// <summary>
	/// <b>A voice out of range makes nothing next</b> (<c>FUN_006b62b0</c> ticks only a voice in range, <c>FUN_006bca10</c>):
	/// the bus's music started far off holds its first variation through a set to 75, and in range plays it, then the rev;
	/// cut as it leaves, it draws nothing until it is back, and then plays the same variation afresh. The Lava Fountain's
	/// chain 100 makes no child away, its child before playing out, and one at once on its return, not one for each wait
	/// missed. The staff's held chain 188 started away makes its first child, the effect's first, once in range. The range
	/// is over the ground alone, and a chain's is its newest child's: 188's variation 4 holds 85 where the others hold 90.
	/// </summary>
	[TestMethod]
	public void AVoiceOutOfRangeMakesNothingNext()
	{
		var nowBefore = Time.Now;
		var far = new Vector3( 1000f, 1000f, 0f );

		WithAParksAudio( park =>
		{
			Time.Now = 100f;
			HearFrom( far );

			var busNodes = RideNodes.Load( "levels/jungle/features/bus", "bus", (BaseFileSystem)FileSystem, false, [] )!;
			var busScript = new RideScript( Build(
				Word( Opcode.ADDOBJ ), 8, 1, 6, 1,
				Word( Opcode.SETOBJPARAM ), 1, 20, 75,
				Word( Opcode.END ) ) ) { Effects = new RideEffects(), Nodes = busNodes, Id = 908 };

			busScript.Turn( 0f );

			var bus = busScript.Effects!.Records.Single().Sound!;
			var at = bus.Place!.Value;

			Assert.AreEqual( (0, 0, 75, true), (bus.Samples, bus.Variation, bus.Parameter, bus.Away), "started away: v1 held, nothing played" );
			Assert.IsNull( bus.Voice );

			for ( var pass = 0; pass < 5; ++pass )
			{
				Time.Now += 1f;
				park.ServeScriptSounds();
			}

			Assert.AreEqual( (0, 0), (bus.Samples, bus.Variation), "nothing drawn while away, though the parameter is 75" );

			HearFrom( at + new Vector3( 10f, 0f, 0f ) );
			park.ServeScriptSounds();
			Assert.AreEqual( (1, 0, false), (bus.Samples, bus.Variation, bus.Away), "in range: the held v1 plays" );
			Assert.IsTrue( bus.Voice!.Playing );

			bus.Voice.Stop();
			park.ServeScriptSounds();
			Assert.AreEqual( (2, 4), (bus.Samples, bus.Variation), "then the rev, by the zones at 75" );

			var cut = bus.Voice!;

			HearFrom( far );
			park.ServeScriptSounds();
			Assert.IsFalse( cut.Playing, "leaving range cuts the sample" );
			Assert.IsTrue( bus.Away );

			for ( var pass = 0; pass < 5; ++pass )
			{
				Time.Now += 1f;
				park.ServeScriptSounds();
			}

			Assert.AreEqual( (2, 4), (bus.Samples, bus.Variation), "and nothing is drawn away" );

			HearFrom( at );
			park.ServeScriptSounds();
			Assert.AreEqual( (3, 4, false), (bus.Samples, bus.Variation, bus.Away), "back in range, the rev afresh, not the run" );
			Assert.AreNotSame( cut, bus.Voice );
			Assert.IsTrue( bus.Voice!.Playing );

			// Ended in range, and away before its silence is over: nothing drawn.
			bus.Voice!.Stop();
			HearFrom( far );

			for ( var pass = 0; pass < 5; ++pass )
			{
				park.ServeScriptSounds();
				Time.Now += 1f;
			}

			Assert.AreEqual( (3, 4, true), (bus.Samples, bus.Variation, bus.Away), "nothing drawn in a silence run out away" );

			// Over the ground alone: a listener 5000 above is in range, and the edge is the range itself.
			Assert.IsTrue( ParkAudio.InRange( bus, at + new Vector3( 0f, 0f, 5000f ) ) );
			Assert.IsTrue( ParkAudio.InRange( bus, at + new Vector3( 60f, 80f, 0f ) ) );
			Assert.IsFalse( ParkAudio.InRange( bus, at + new Vector3( 60.5f, 80f, 0f ) ) );

			// The Lava Fountain's chain on the End's node 9, (21, 57).
			var endNodes = RideNodes.Load( "levels/jungle/features/end", "End", (BaseFileSystem)FileSystem, false, [] )!;
			var lavaScript = new RideScript( Build(
				Word( Opcode.ADDOBJ ), 3, 9, 100, 40,
				Word( Opcode.ADDOBJ ), 7, 9, 188, 41,
				Word( Opcode.END ) ) ) { Effects = new RideEffects(), Nodes = endNodes, Id = 909 };

			HearFrom( new Vector3( 21f, 57f, 0f ) );
			lavaScript.Turn( 0f );

			var lava = lavaScript.Effects!.Records.Single( record => record.Tag == 40 ).Sound!;
			var staff = lavaScript.Effects!.Records.Single( record => record.Tag == 41 ).Sound!;

			Assert.AreEqual( 1, lava.Samples );

			var before = lava.Voice!;

			HearFrom( far );

			for ( var pass = 0; pass < 10; ++pass )
			{
				Time.Now = lava.NextAt + 1f;
				park.ServeScriptSounds();
			}

			Assert.AreEqual( (1, true), (lava.Samples, lava.Away), "no child away" );
			Assert.IsTrue( before.Playing, "the child before plays out" );

			HearFrom( new Vector3( 21f, 57f, 0f ) );
			park.ServeScriptSounds();
			Assert.AreEqual( (2, false), (lava.Samples, lava.Away), "one child at once in range again" );
			park.ServeScriptSounds();
			Assert.AreEqual( 2, lava.Samples, "and not one for each wait missed" );

			Time.Now = lava.NextAt + 0.001f;
			park.ServeScriptSounds();
			Assert.AreEqual( 3, lava.Samples, "then each after its wait" );

			// 188 started away: its first child, the effect's first variation, once in range.
			var staffNodes = RideNodes.Load( "levels/jungle/features/end", "End", (BaseFileSystem)FileSystem, false, [] )!;
			var staffScript = new RideScript( Build(
				Word( Opcode.ADDOBJ ), 7, 9, 188, 42,
				Word( Opcode.END ) ) ) { Effects = new RideEffects(), Nodes = staffNodes, Id = 910 };

			HearFrom( far );
			staffScript.Turn( 0f );

			var late = staffScript.Effects!.Records.Single().Sound!;

			Assert.AreEqual( (0, -1, true), (late.Samples, late.Variation, late.Away) );

			HearFrom( new Vector3( 21f, 57f, 0f ) );
			park.ServeScriptSounds();
			Assert.AreEqual( (1, 0), (late.Samples, late.Variation), "the first child, the effect's first" );

			// A chain's range is its newest child's: 188's variation 4 holds 85, the others 90.
			var place = staff.Place!.Value;

			staff.Variation = 3;
			Assert.IsFalse( ParkAudio.InRange( staff, place + new Vector3( 87f, 0f, 0f ) ) );
			staff.Variation = 0;
			Assert.IsTrue( ParkAudio.InRange( staff, place + new Vector3( 87f, 0f, 0f ) ) );
		} );

		Time.Now = nowBefore;
	}

	/// <summary>
	/// <b>A script's one-shot starts only in range, and is dropped by its time</b> (<c>0x006bd800</c>, <c>0x006bcb60</c>):
	/// the toilet's kids 51 (one sample of 3251 ms, range 70) on the End's node 9. Started away it plays nothing and is
	/// dropped 250 ms after its length has passed; started near, it is cut away and starts from the top on its return,
	/// and its drop lets a sample still playing play out; ended by itself it is let go. The global ambient's 209, looped,
	/// is never dropped by time, and starts afresh on its return.
	/// </summary>
	[TestMethod]
	public void AScriptsOneShotStartsOnlyInRangeAndIsDroppedByItsTime()
	{
		var nowBefore = Time.Now;
		var far = new Vector3( 1000f, 1000f, 0f );
		var near = new Vector3( 21f, 57f, 0f );

		WithAParksAudio( park =>
		{
			var nodes = RideNodes.Load( "levels/jungle/features/end", "End", (BaseFileSystem)FileSystem, false, [] )!;
			ParkAudio.ScriptVoice Start( int type, int node, int effect, int id )
			{
				new RideScript( Build( Word( Opcode.EVENT ), type, node, effect, Word( Opcode.END ) ) )
					{ Effects = new RideEffects(), Nodes = nodes, Id = id }.Turn( 0f );

				return park.ScriptVoices.Last( sound => sound.Effect == effect );
			}

			Time.Now = 200f;
			HearFrom( far );

			var away = Start( 6, 9, 51, 911 );

			Assert.AreEqual( (0, true), (away.Samples, away.Away), "started away: nothing plays" );
			Assert.IsNull( away.Voice );
			Assert.AreEqual( 203.501f, away.DropAt, 0.0005f, "its start, 250 ms and the record's 3251 ms" );

			Time.Now = 203.5f;
			park.ServeScriptSounds();
			Assert.IsTrue( park.ScriptVoices.Contains( away ), "kept until its time" );

			Time.Now = 203.502f;
			park.ServeScriptSounds();
			Assert.IsFalse( park.ScriptVoices.Contains( away ), "dropped once it has passed, never played" );
			Assert.AreEqual( (1, 0), (park.ScriptOneShotsDropped, away.Samples) );

			HearFrom( near );
			Time.Now = 300f;

			var cut = Start( 6, 9, 51, 912 );
			var first = cut.Voice!;

			Assert.AreEqual( 1, cut.Samples );
			Assert.IsTrue( first.Playing );

			HearFrom( far );
			park.ServeScriptSounds();
			Assert.IsFalse( first.Playing, "leaving range cuts it" );
			Assert.IsTrue( cut.Away );

			HearFrom( near );
			Time.Now = 302f;
			park.ServeScriptSounds();
			Assert.AreEqual( (2, false), (cut.Samples, cut.Away), "in range again: from the top" );
			Assert.AreNotSame( first, cut.Voice );

			var again = cut.Voice!;

			Time.Now = cut.DropAt + 0.001f;
			park.ServeScriptSounds();
			Assert.IsFalse( park.ScriptVoices.Contains( cut ), "dropped by its time while it plays" );
			Assert.IsTrue( again.Playing, "and its sample plays out" );
			Assert.AreEqual( 2, park.ScriptOneShotsDropped );

			var ended = Start( 6, 9, 51, 913 );

			ended.Voice!.Stop();
			park.ServeScriptSounds();
			Assert.IsFalse( park.ScriptVoices.Contains( ended ), "ended by itself in range: let go" );
			Assert.AreEqual( 2, park.ScriptOneShotsDropped, "not by its time" );

			// The global ambient's 209, looped (0x8), on node 1 at (0, 271), over 100.
			HearFrom( far );

			var looped = Start( 8, 1, 209, 914 );

			Assert.IsTrue( looped.Looped );
			Time.Now += 60f;
			park.ServeScriptSounds();
			Assert.AreEqual( (0, true), (looped.Samples, looped.Away), "held away, and never dropped by time" );
			Assert.IsTrue( park.ScriptVoices.Contains( looped ) );

			HearFrom( new Vector3( 0f, 271f, 0f ) );
			park.ServeScriptSounds();
			Assert.AreEqual( 1, looped.Samples );
			Assert.IsTrue( looped.Voice!.Loop && looped.Voice.Playing );

			var loop = looped.Voice;

			HearFrom( far );
			park.ServeScriptSounds();
			Assert.IsFalse( loop.Playing, "cut away" );

			HearFrom( new Vector3( 0f, 271f, 0f ) );
			park.ServeScriptSounds();
			Assert.AreEqual( 2, looped.Samples, "afresh on its return" );
			Assert.IsTrue( looped.Voice!.Playing );
		} );

		Time.Now = nowBefore;
	}

	/// <summary>The park's draws of a one-shot's volume and pitch taken from <paramref name="random"/> from now on.</summary>
	private static void DrawOneShotsFrom( ParkAudio park, Random random )
		=> typeof( ParkAudio ).GetField( "_screamRandom", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance )!
			.SetValue( park, random );

	/// <summary>
	/// <b>A one-shot started late in its sample is refused, and let go</b> (<c>0x006bc410</c>, <c>0x006bc534</c>..
	/// <c>0x006bc55d</c>; <c>0x006bd8fa</c>): kids 132 (one sample of 2681 ms, a pitch of -5 to 5) on the End's node 9, cut
	/// away and back 2.0 s after its set-up, 681 ms before its sample's end: no channel, its volume and pitch drawn all the
	/// same, and gone on the next pass, not by its time. The toilet's kids 51 (3251 ms) back 2.0 s after, 1251 ms before its
	/// end, starts from the top. The time is counted round its length, and a flat one is never refused.
	/// </summary>
	[TestMethod]
	public void AOneShotStartedLateInItsSampleIsRefusedAndLetGo()
	{
		var nowBefore = Time.Now;
		var far = new Vector3( 1000f, 1000f, 0f );
		var near = new Vector3( 21f, 57f, 0f );

		WithAParksAudio( park =>
		{
			var nodes = RideNodes.Load( "levels/jungle/features/end", "End", (BaseFileSystem)FileSystem, false, [] )!;
			ParkAudio.ScriptVoice Start( int effect, int id )
			{
				new RideScript( Build( Word( Opcode.EVENT ), 6, 9, effect, Word( Opcode.END ) ) )
					{ Effects = new RideEffects(), Nodes = nodes, Id = id }.Turn( 0f );

				return park.ScriptVoices.Last( sound => sound.Effect == effect );
			}

			HearFrom( near );
			Time.Now = 400f;

			var late = Start( 132, 921 );

			Assert.AreEqual( (2681, 1), (late.LengthMilliseconds, late.Samples) );

			var first = late.Voice!;

			HearFrom( far );
			park.ServeScriptSounds();
			Assert.IsTrue( late.Away );

			var header = late.Category.VariationsOf( 132 )[late.Variation];

			Assert.AreNotEqual( header.Pitch.Low, header.Pitch.High, "a pitch drawn, so a draw is seen" );

			var twin = new Random( 5 );

			DrawOneShotsFrom( park, new Random( 5 ) );
			ParkAudio.Controlled( header, 1, 0, 0, twin );
			var pitch = ParkAudio.Controlled( header, 2, 0, 0, twin );

			HearFrom( near );
			Time.Now = 402f;
			park.ServeScriptSounds();
			Assert.AreEqual( (1, true), (late.Samples, late.Refused), "no channel, 2000 ms in and 681 left" );
			Assert.AreSame( first, late.Voice );
			Assert.IsFalse( first.Playing );
			Assert.AreEqual( pitch, park.OneShotPitch, "its volume and pitch drawn before the refusal" );
			Assert.IsTrue( park.ScriptVoices.Contains( late ) );

			park.ServeScriptSounds();
			Assert.IsFalse( park.ScriptVoices.Contains( late ), "let go on the next pass" );
			Assert.AreEqual( (1, 0), (park.ScriptOneShotsRefused, park.ScriptOneShotsDropped), "as 0x10, not by its time" );

			// Back 2.0 s in, 1251 ms are left: from the top.
			Time.Now = 500f;

			var early = Start( 51, 922 );

			HearFrom( far );
			park.ServeScriptSounds();
			HearFrom( near );
			Time.Now = 502f;
			park.ServeScriptSounds();
			Assert.AreEqual( (2, false), (early.Samples, early.Refused) );
			Assert.IsTrue( early.Voice!.Playing );

			// Its position is counted round its length: 5751 ms after its set-up is 2500 ms into its sample.
			Assert.IsFalse( ParkAudio.LateStart( early ) );
			Time.Now = 500f + 5.751f;
			Assert.IsTrue( ParkAudio.LateStart( early ) );
			Time.Now = 500f + 3.251f + 1.0f;
			Assert.IsFalse( ParkAudio.LateStart( early ), "1000 ms in, not past it" );

			// One heard flat (0x200) has no place: never refused.
			Time.Now = 600f;

			var flat = Start( 51, 923 );

			flat.Place = null;
			Time.Now = 602.5f;
			Assert.IsFalse( ParkAudio.LateStart( flat ), "a flat one-shot starts wherever it is in its sample" );
		} );

		Time.Now = nowBefore;
	}

	/// <summary>
	/// <b>Each start of a one-shot draws its variation's wait, which puts its drop back</b> (<c>0x006bc4f4</c>..
	/// <c>0x006bc4fa</c>, the voice's <c>+0x3c</c>, read by <c>0x006bcb60</c>): the Monkey's 82 (level rides, one sample of
	/// 1695 ms, a wait of 1000 ms both ways) on the End's node 9 is dropped at its set-up, 250 ms, its length and the wait,
	/// not a second sooner.
	/// </summary>
	[TestMethod]
	public void AOneShotsStartDrawsItsWaitWhichPutsItsDropBack()
	{
		var nowBefore = Time.Now;

		WithAParksAudio( park =>
		{
			var nodes = RideNodes.Load( "levels/jungle/features/end", "End", (BaseFileSystem)FileSystem, false, [] )!;

			HearFrom( new Vector3( 21f, 57f, 0f ) );
			Time.Now = 500f;
			new RideScript( Build( Word( Opcode.EVENT ), 3, 9, 82, Word( Opcode.END ) ) )
				{ Effects = new RideEffects(), Nodes = nodes, Id = 931 }.Turn( 0f );

			var monkey = park.ScriptVoices.Last( sound => sound.Effect == 82 );
			var header = monkey.Category.VariationsOf( 82 )[monkey.Variation];

			Assert.AreEqual( ("one-shot", 1695, 1000, 1000), (monkey.Kind, monkey.LengthMilliseconds, header.GapMin, header.GapMax) );
			Assert.AreEqual( 1, monkey.Samples );
			Assert.AreEqual( 500f + 0.25f + 1.695f + 1f, monkey.DropAt, 0.0005f );

			Time.Now = 502.5f;
			park.ServeScriptSounds();
			Assert.IsTrue( park.ScriptVoices.Contains( monkey ), "past its length and 250 ms, inside the wait" );

			Time.Now = 502.946f;
			park.ServeScriptSounds();
			Assert.IsFalse( park.ScriptVoices.Contains( monkey ) );
			Assert.AreEqual( 1, park.ScriptOneShotsDropped );
		} );

		Time.Now = nowBefore;
	}

	/// <summary>
	/// <b>Under a park's menu a one-shot that comes into range passes unheard</b> (<see cref="ParkAudio"/>'s
	/// <c>Unheard</c>; the original's lifted listener, <c>0x0051c1c0</c>): kids 51 started away, then the listener brought in
	/// under the hold: no voice, one sample counted, its end held; cut on leaving range and unheard again on its return;
	/// let go at its sample's end; nothing sounds when the hold is lifted.
	/// </summary>
	[TestMethod]
	public void UnderAMenuAOneShotComingIntoRangePassesUnheard()
	{
		var nowBefore = Time.Now;
		var far = new Vector3( 1000f, 1000f, 0f );
		var near = new Vector3( 21f, 57f, 0f );

		WithAParksAudio( park =>
		{
			var nodes = RideNodes.Load( "levels/jungle/features/end", "End", (BaseFileSystem)FileSystem, false, [] )!;

			HearFrom( far );
			Time.Now = 600f;
			new RideScript( Build( Word( Opcode.EVENT ), 6, 9, 51, Word( Opcode.END ) ) )
				{ Effects = new RideEffects(), Nodes = nodes, Id = 941 }.Turn( 0f );

			var kids = park.ScriptVoices.Last( sound => sound.Effect == 51 );

			Assert.IsTrue( kids.Away );

			var voices = VoiceCount();

			Audio.HoldPlaced( true );
			HearFrom( near );
			Time.Now = 600.1f;
			park.ServeScriptSounds();
			Assert.IsNull( kids.Voice, "no voice born held" );
			Assert.AreEqual( (1, 1, voices), (kids.Samples, kids.Unheard, VoiceCount()) );
			Assert.AreEqual( 600.1f + kids.Clip!.Duration / ParkCarSounds.Rate( park.OneShotPitch ), kids.UnheardUntil!.Value, 0.0005f );

			HearFrom( far );
			park.ServeScriptSounds();
			Assert.AreEqual( (true, (float?)null), (kids.Away, kids.UnheardUntil), "cut on leaving range" );

			HearFrom( near );
			Time.Now = 600.2f;
			park.ServeScriptSounds();
			Assert.AreEqual( (2, 2), (kids.Samples, kids.Unheard), "unheard again in range" );

			Time.Now = kids.UnheardUntil!.Value - 0.001f;
			park.ServeScriptSounds();
			Assert.IsTrue( park.ScriptVoices.Contains( kids ), "as playing until its sample's end" );

			Time.Now = kids.UnheardUntil!.Value + 0.001f;
			park.ServeScriptSounds();
			Assert.IsFalse( park.ScriptVoices.Contains( kids ), "let go at its end" );
			Assert.AreEqual( 0, park.ScriptOneShotsDropped, "not by its time" );

			Audio.HoldPlaced( false );
			Assert.AreEqual( voices, VoiceCount(), "nothing sounds on the resume" );
		} );

		Time.Now = nowBefore;
	}

	/// <summary>
	/// <b>A one-shot keeps its sample through every return, and draws its volume and pitch afresh at each</b>
	/// (<c>0x006bbf60</c>'s <c>+0xc</c>; <c>FUN_006bc410</c>): kids 49 (eight samples) on the End's node 9, cut and back ten
	/// times before its drop; each start plays the first's sample at the pitch a seeded generator gives it, and its drop
	/// stays where its set-up put it.
	/// </summary>
	[TestMethod]
	public void AOneShotKeepsItsSampleAndDrawsAfreshAtEachReturn()
	{
		var nowBefore = Time.Now;
		var far = new Vector3( 1000f, 1000f, 0f );
		var near = new Vector3( 21f, 57f, 0f );

		WithAParksAudio( park =>
		{
			var nodes = RideNodes.Load( "levels/jungle/features/end", "End", (BaseFileSystem)FileSystem, false, [] )!;

			HearFrom( near );
			Time.Now = 700f;
			new RideScript( Build( Word( Opcode.EVENT ), 6, 9, 49, Word( Opcode.END ) ) )
				{ Effects = new RideEffects(), Nodes = nodes, Id = 951 }.Turn( 0f );

			var kids = park.ScriptVoices.Last( sound => sound.Effect == 49 );
			var headers = kids.Category.VariationsOf( 49 );
			var header = headers[kids.Variation];
			var name = kids.Voice!.Name;
			var dropAt = kids.DropAt;

			Assert.AreEqual( 8, headers.Sum( variation => variation.Samples ), "eight samples to draw among" );
			Assert.AreNotEqual( header.Pitch.Low, header.Pitch.High, "a pitch drawn, so a kept one is seen" );

			// Every return inside its length and 250 ms, before its drop.
			var step = (kids.LengthMilliseconds / 1000f + 0.2f) / 11f;

			for ( var turn = 1; turn <= 10; ++turn )
			{
				HearFrom( far );
				Time.Now = 700f + (turn - 0.5f) * step;
				park.ServeScriptSounds();

				var twin = new Random( turn );

				DrawOneShotsFrom( park, new Random( turn ) );
				ParkAudio.Controlled( header, 1, 0, 0, twin );
				var pitch = ParkAudio.Controlled( header, 2, 0, 0, twin );

				HearFrom( near );
				Time.Now = 700f + turn * step;
				park.ServeScriptSounds();
				Assert.AreEqual( (turn + 1, name), (kids.Samples, kids.Voice!.Name), $"return {turn}: the first's sample" );
				Assert.AreEqual( pitch, park.OneShotPitch, $"return {turn}: its pitch drawn afresh" );
				Assert.AreEqual( (double)ParkCarSounds.Rate( pitch ), kids.Voice.Rate, 1e-5 );
				Assert.AreEqual( dropAt, kids.DropAt, $"return {turn}: its drop kept" );
			}
		} );

		Time.Now = nowBefore;
	}

	/// <summary>
	/// <b>A one-shot let go is neither stopped nor moved by its script after</b> (<c>0x006bb9b3</c>..<c>0x006bb9c7</c>, the
	/// handle masked): kids 51 near on the End's node 9, dropped by its time with its sample playing; a <c>KILLOBJ</c>'s stop
	/// and the eighth-tick move find nothing, and the sample plays out where it is.
	/// </summary>
	[TestMethod]
	public void AOneShotLetGoIsNeitherStoppedNorMovedAfter()
	{
		var nowBefore = Time.Now;

		WithAParksAudio( park =>
		{
			var nodes = RideNodes.Load( "levels/jungle/features/end", "End", (BaseFileSystem)FileSystem, false, [] )!;

			HearFrom( new Vector3( 21f, 57f, 0f ) );
			Time.Now = 800f;
			new RideScript( Build( Word( Opcode.EVENT ), 6, 9, 51, Word( Opcode.END ) ) )
				{ Effects = new RideEffects(), Nodes = nodes, Id = 961 }.Turn( 0f );

			var kids = park.ScriptVoices.Last( sound => sound.Effect == 51 );
			var voice = kids.Voice!;
			var place = kids.Place;

			Time.Now = kids.DropAt + 0.001f;
			park.ServeScriptSounds();
			Assert.IsFalse( park.ScriptVoices.Contains( kids ) );
			Assert.IsTrue( voice.Playing );

			var counts = park.ScriptSounds;

			park.MoveScriptSound( kids, place!.Value + new Vector3( 5f, 0f, 0f ) );
			park.StopScriptSound( kids, fade: false );
			Assert.AreEqual( counts, park.ScriptSounds, "neither moved nor stopped" );
			Assert.AreEqual( place, kids.Place );
			Assert.IsTrue( voice.Playing, "its sample plays out" );
		} );

		Time.Now = nowBefore;
	}

	/// <summary>
	/// <b>A voice in range again draws its silence, volume and pitch afresh, as a first start does</b> (<c>0x006bdf50</c>,
	/// <c>FUN_006bc410</c>, whose slot <c>+0x48</c> draws the wait before the volume and the pitch). The gulls (global
	/// ambient 2: volume 49, a pitch of -2 to 2, a wait of 0 to 10000 ms) on the End's node 9: the sample ends in range,
	/// the listener leaves in its silence and comes back a minute on. The held sample plays again at the wait, volume and
	/// pitch a seeded generator gives a first, and the next comes only once a silence counted from that sample's own end
	/// is over.
	/// </summary>
	[TestMethod]
	public void AVoiceInRangeAgainDrawsItsSilenceVolumeAndPitchAfresh()
	{
		var nowBefore = Time.Now;
		var near = new Vector3( 21f, 57f, 0f );
		const int seed = 5;

		WithAParksAudio( park =>
		{
			Time.Now = 100f;
			DrawFrom( park, new Random( 1 ) );
			HearFrom( near );

			var nodes = RideNodes.Load( "levels/jungle/features/end", "End", (BaseFileSystem)FileSystem, false, [] )!;
			var script = new RideScript( Build( Word( Opcode.ADDOBJ ), 8, 9, 2, 1, Word( Opcode.END ) ) )
				{ Effects = new RideEffects(), Nodes = nodes, Id = 916 };

			script.Turn( 0f );

			var gulls = script.Effects!.Records.Single().Sound!;
			var header = gulls.Category.VariationsOf( gulls.Effect )[0];
			var first = gulls.Kept;

			Assert.AreEqual( (1, 0), (gulls.Samples, gulls.Variation) );

			// The sample ends in range, and in its silence the listener leaves.
			gulls.Voice!.Stop();
			park.ServeScriptSounds();
			Assert.IsTrue( gulls.EndedAt is not null && gulls.Wait > 0, "in its silence" );

			HearFrom( new Vector3( 1000f, 1000f, 0f ) );
			park.ServeScriptSounds();
			Time.Now += 60f;
			park.ServeScriptSounds();
			Assert.AreEqual( (1, true), (gulls.Samples, gulls.Away), "nothing drawn away" );

			// Back: the draws of a first start, in its order.
			var probe = new Random( seed );
			var wait = ParkScreams.GapMilliseconds( header, probe );
			var kept = ParkAudio.HeldSample( header, true, first, 0, 0, probe );

			Assert.AreNotEqual( first.Pitch, kept.Pitch, "the seed draws another pitch than the first's" );
			Assert.IsTrue( wait > 1000, $"the seed draws a silence of seconds, {wait} ms" );

			DrawFrom( park, new Random( seed ) );
			HearFrom( near );
			park.ServeScriptSounds();

			Assert.AreEqual( (2, 0, false), (gulls.Samples, gulls.Variation, gulls.Away), "the held sample afresh" );
			Assert.AreEqual( (wait, kept), (gulls.Wait, gulls.Kept) );
			Assert.AreEqual( ParkCarSounds.Rate( kept.Pitch ), (float)gulls.Voice!.Rate, 1e-6f );

			// Its silence counts from its own end, not from the end of the sample before the leaving.
			Time.Now += 1f;
			gulls.Voice.Stop();

			var ended = Time.Now;

			park.ServeScriptSounds();
			Assert.AreEqual( 2, gulls.Samples );
			Assert.AreEqual( ended, gulls.EndedAt );

			Time.Now = ended + (wait / 1000f) - 0.01f;
			park.ServeScriptSounds();
			Assert.AreEqual( 2, gulls.Samples, "silent to its wait's end" );

			Time.Now = ended + (wait / 1000f);
			park.ServeScriptSounds();
			Assert.AreEqual( 3, gulls.Samples );
			Assert.AreEqual( kept, gulls.Kept, "the next at what the return drew" );
		} );

		Time.Now = nowBefore;
	}

	/// <summary>
	/// <b>An arrival vehicle's nodes stand where its file holds them</b>, as the original's bus voice came at its body's
	/// place, (296, 5, -114): the bus's sound node 1 there, its root <c>Line02</c>'s (480, 0, 170) kept, where a thing
	/// stood at its cells has its root's place replaced by theirs (here cell (0, 0), with no ground).
	/// </summary>
	[TestMethod]
	public void AnArrivalVehicleStandsWhereItsFileHoldsIt()
	{
		var data = GameData.Required();
		var bus = RideNodes.Load( "levels/jungle/features/bus", "bus", data, false, [] )!;
		var other = RideNodes.Load( "levels/jungle/features/bus", "bus", data, false, [] )!;

		ParkRides.Stand( bus, 1600, ParkObjects.OriginFor( 0, 0, 0 ), 0 );
		ParkRides.Stand( other, 1417, ParkObjects.OriginFor( 0, 0, 0 ), 0 );

		Assert.AreEqual( NodeEnd.Posed, bus.Find( 1, RideNodes.SoundSpace, out var fixedAt ) );
		Assert.AreEqual( 296.27f, fixedAt.X, 0.01f );
		Assert.AreEqual( 5.67f, fixedAt.Y, 0.01f );
		Assert.AreEqual( -114.16f, fixedAt.Z, 0.01f );

		other.Find( 1, RideNodes.SoundSpace, out var celled );
		Assert.IsTrue( celled.X < 0f && celled.Z < 0f, $"at its cells the root's (480, 0, 170) is gone: {celled}" );
	}

	/// <summary>
	/// <b>Through a park's menu a held chain's children are drawn on their zones and made unheard; none is held to start
	/// with the rest on resume.</b> The global staff's 188, the Staff Room's (<c>ADDOBJ 7 -1 188 1</c>, whose two heaviest
	/// variations wait 100 to 500 ms), on the End's node 9, held for ten seconds: no voice is started, each child is
	/// drawn on time among the zones of the one before at the parameter nought, and after the resume the next child
	/// comes at the time the running clock gives it, alone. The original's service ticks it through a pause and its
	/// mixer plays the children at nought (<c>docs/exe/audio.md</c>, "The pause substitution").
	/// </summary>
	[TestMethod]
	public void AHeldParksHeldChainMakesItsChildrenUnheardAndNoneBurstsOnResume()
	{
		var nowBefore = Time.Now;

		WithAParksAudio( park =>
		{
			Time.Now = 100f;

			var nodes = RideNodes.Load( "levels/jungle/features/end", "End", (BaseFileSystem)FileSystem, false, [] )!;
			var script = new RideScript( Build( Word( Opcode.ADDOBJ ), 7, 9, 188, 1, Word( Opcode.END ) ) )
				{ Effects = new RideEffects(), Nodes = nodes, Id = 914 };

			script.Turn( 0f );

			var staff = script.Effects!.Records.Single().Sound!;
			var headers = staff.Category.VariationsOf( 188 );
			var longest = headers.Max( header => Math.Max( header.GapMin, header.GapMax ) ) / 1000f;
			var first = staff.Voice!;

			Assert.AreEqual( "held chain", staff.Kind );
			Assert.AreEqual( 0, staff.Variation, "its first child the effect's first variation" );

			Frame( park, held: true );

			Assert.IsTrue( Audio.PlacedHeld && first.IsHeld, "the menu holds the chain's child" );

			var voices = VoiceCount();
			var steps = 0;

			for ( var pass = 0; pass < 1000; ++pass )
			{
				var before = staff.Variation;

				Time.Now += 0.01f;
				Frame( park, held: true );
				Assert.IsTrue( staff.NextAt > Time.Now - 0.001f && staff.NextAt <= Time.Now + longest, "its clock runs on" );

				if ( staff.Variation == before )
					continue;

				++steps;
				Assert.IsTrue( headers[before].Zones.Any( zone => zone.Variation == staff.Variation && zone.Low <= 0 && zone.High >= 0 ),
					$"{before + 1} to {staff.Variation + 1} under the hold, by the zones" );
			}

			Assert.AreEqual( 1, staff.Samples, "no child's voice started under the hold" );
			Assert.AreEqual( voices, VoiceCount(), "and none waits in the pool" );
			Assert.IsTrue( staff.Unheard >= Math.Max( 2, (int)(10f / longest) ),
				$"{staff.Unheard} children in ten seconds of waits up to {longest:0.0} s, {steps} changes of variation" );
			Assert.IsNull( staff.Voice, "its newest child is an unheard one" );

			var due = staff.NextAt;

			Time.Now = due;
			Frame( park, held: false );

			Assert.IsFalse( first.IsHeld, "the resume lets the held child go" );
			Assert.AreEqual( 1, staff.Samples, "not before the newest child's time" );

			Time.Now = due + 0.001f;
			Frame( park, held: false );

			Assert.AreEqual( 2, staff.Samples, "one child on its time" );
			Assert.AreEqual( voices + 1, VoiceCount(), "and only that one" );
			Assert.IsFalse( staff.Voice!.IsHeld );
		} );

		Time.Now = nowBefore;
	}

	/// <summary>
	/// <b>Through a park's menu a music voice's samples are drawn on its zones and let pass unheard.</b> The bus's 6 at
	/// the parameter 75, on its model: its first sample ends as the hold begins, and the rev (5) follows unheard, then the
	/// run (6) again and again, each lasting as its sample would at the first's pitch; no voice is started. After the
	/// resume the next sample comes once the unheard one's end is past, and is heard.
	/// </summary>
	[TestMethod]
	public void AHeldParksMusicVoiceLetsItsSamplesPassUnheard()
	{
		var nowBefore = Time.Now;

		WithAParksAudio( park =>
		{
			Time.Now = 100f;

			var nodes = RideNodes.Load( "levels/jungle/features/bus", "bus", (BaseFileSystem)FileSystem, false, [] )!;
			var script = new RideScript( Build(
				Word( Opcode.ADDOBJ ), 8, 1, 6, 1,
				Word( Opcode.SETOBJPARAM ), 1, 20, 75,
				Word( Opcode.END ) ) ) { Effects = new RideEffects(), Nodes = nodes, Id = 915 };

			HearFrom( new Vector3( -180f, -280f, 0f ) );
			script.Turn( 0f );

			var bus = script.Effects!.Records.Single().Sound!;

			Assert.AreEqual( (1, 0, 75), (bus.Samples, bus.Variation, bus.Parameter) );
			Assert.IsNotNull( bus.Place );

			Frame( park, held: true );

			var voices = VoiceCount();

			bus.Voice!.Stop();
			Frame( park, held: true );

			Assert.AreEqual( (1, 1, 4), (bus.Samples, bus.Unheard, bus.Variation), "the rev drawn, and no voice started" );
			Assert.IsNull( bus.Voice );
			Assert.AreEqual( voices, VoiceCount() );
			Assert.IsTrue( bus.EndedAt > Time.Now, "an unheard sample lasts as its sample would" );

			for ( var pass = 0; pass < 3000; ++pass )
			{
				Time.Now += 0.01f;
				Frame( park, held: true );
			}

			Assert.AreEqual( 1, bus.Samples, "thirty seconds held, and not one sample heard" );
			Assert.AreEqual( voices, VoiceCount() );
			Assert.IsTrue( bus.Unheard > 2, $"{bus.Unheard} drawn on time" );
			Assert.AreEqual( 5, bus.Variation, "the run, again and again" );

			Time.Now = MathF.Max( bus.EndedAt!.Value + (bus.Wait / 1000f), Time.Now );
			Frame( park, held: false );

			Assert.AreEqual( 2, bus.Samples, "the next is heard" );
			Assert.IsFalse( bus.Voice!.IsHeld );
			Assert.AreEqual( voices + 1, VoiceCount() );
		} );

		Time.Now = nowBefore;
	}

	/// <summary>
	/// <b>The research screen's two loops take the work load's parameters</b> (<c>docs/exe/hud.md</c>, "The research
	/// screen's sounds"). Opened over a fresh lab with one researcher of ability 3, the knob's round trip sets 298, so
	/// <c>298 × 100 / 1024</c> = 29: <c>0x9a</c> (the music's class, key 18) takes 29 on slot 1 (key 12, its volume),
	/// <c>29 × 77 / 100 + 8</c> = 30, and the rates' mean, nought, on slot 0; <c>0x9b</c> (key 13 on slots 0 and 1) takes 29
	/// on both, a volume of <c>29 × 18 / 100 + 2</c> = 7 and a pitch of <c>29 × 16 / 100 - 8</c> = -4. Both flat, the first
	/// sample of each its effect's first variation. At the mean nought <c>0x9a</c>'s next is its second variation (zone 0 to
	/// 0). Of ability 60 the limit is past the slider's top: the knob dragged to 1023 is 99, and <c>0x9b</c>'s next is its
	/// second variation (zone 81 to 100), <c>0x9a</c>'s mean 15 its first. The screen's close stops both.
	/// </summary>
	/// <remarks><b>Mutations:</b> the loops started after the knob is set; a set of slot 0 applying; the work load over
	/// 1000; the apply's volume from slot 2; the mean over four.</remarks>
	[TestMethod]
	public void TheResearchScreensLoopsTakeTheWorkLoad()
	{
		WithAParksAudio( park =>
		{
			var stack = new UI.WindowStack();
			var screen = new UI.ParkResearchScreen( stack, ParkResearchScreenTests.FreshLab( (BaseFileSystem)FileSystem ), () => 3, 60, park );
			var loop = park.ScriptVoices.Single( sound => sound.Effect == 0x9a );
			var work = park.ScriptVoices.Single( sound => sound.Effect == 0x9b );

			Assert.AreEqual( ("the interface", 9), (loop.Owner, loop.Type) );
			Assert.IsNull( loop.Place, "flat" );
			Assert.IsTrue( loop.Music && work.Music );
			CollectionAssert.AreEqual( new[] { 0, 29, 0, 0 }, loop.Values );
			CollectionAssert.AreEqual( new[] { 29, 29, 0, 0 }, work.Values );
			Assert.AreEqual( ((30, 0), (7, -4), 0, 0), (loop.Kept, work.Kept, loop.Variation, work.Variation) );
			Assert.AreEqual( ParkAudio.ChannelGain( 30 ), loop.Voice!.Volume, 1e-5f );
			Assert.AreEqual( MathF.Pow( 2f, -5f / 96f ), (float)work.Voice!.Rate, 1e-4f );

			loop.Voice.Stop();
			work.Voice.Stop();
			park.ServeScriptSounds();
			Assert.AreEqual( (1, 0, (30, 0)), (loop.Variation, work.Variation, loop.Kept), "the mean nought's zone, and the volume kept" );

			stack.Open( screen );
			stack.Close( screen );
			Assert.IsTrue( loop.Stopped && work.Stopped, "the close stops both" );

			var all = new UI.ParkResearchScreen( new UI.WindowStack(), ParkResearchScreenTests.FreshLab( (BaseFileSystem)FileSystem ), () => 60, 60, park );
			var allLoop = park.ScriptVoices.Single( sound => sound.Effect == 0x9a && !sound.Stopped );
			var allWork = park.ScriptVoices.Single( sound => sound.Effect == 0x9b && !sound.Stopped );
			var knob = all.Root.Children.OfType<UI.UiSlider>().Single( slider => slider.Id == 0xd2d10 );

			Assert.AreEqual( 15, allLoop.Parameter, "(48 + 6 + 14 + 6 + 4) / 5" );
			knob.SetValue( 1023 );
			knob.Moved!();
			CollectionAssert.AreEqual( new[] { 99, 99, 0, 0 }, allWork.Values );
			Assert.AreEqual( (99 * 18 / 100 + 2, 99 * 16 / 100 - 8), allWork.Kept );

			allLoop.Voice!.Stop();
			allWork.Voice!.Stop();
			park.ServeScriptSounds();
			Assert.AreEqual( (0, 1), (allLoop.Variation, allWork.Variation) );
		} );
	}

	/// <summary>
	/// <b>An effort thumb held plays <c>0x9c</c>, its volume and pitch the effort</b>: the plain class, keys 10 (volume, 2 to
	/// 15) and 11 (pitch, -24 to 36). Pressed at the rides' 100 it is <c>15</c> and <c>36</c>; dragged to 50, <c>8</c> and
	/// <c>6</c>, each set applied at once; let go, faded. A drag with no thumb held sets nothing.
	/// </summary>
	/// <remarks><b>Mutations:</b> the parameters swapped; the press not playing; the release cutting rather than fading.</remarks>
	[TestMethod]
	public void AnEffortThumbHeldPlaysItsSound()
	{
		WithAParksAudio( park =>
		{
			var screen = new UI.ParkResearchScreen( new UI.WindowStack(), ParkResearchScreenTests.FreshLab( (BaseFileSystem)FileSystem ), () => 3, 60, park );
			var rides = screen.Root.Children.OfType<UI.UiSlider>().Single( slider => slider.Id == 0xd2d0e );

			rides.SetValue( 60 );
			rides.Moved!();
			Assert.IsFalse( park.ScriptVoices.Any( sound => sound.Effect == 0x9c ), "no thumb held" );

			rides.SetValue( 100 );
			rides.ThumbPressed!();
			var effort = park.ScriptVoices.Single( sound => sound.Effect == 0x9c );

			Assert.IsTrue( effort.Plain );
			CollectionAssert.AreEqual( new[] { 0, 100, 100, 0 }, effort.Values );
			Assert.AreEqual( (15, 36), effort.Kept );
			Assert.AreEqual( MathF.Pow( 2f, 37f / 96f ), (float)effort.Voice!.Rate, 1e-4f );

			rides.SetValue( 50 );
			rides.Moved!();
			Assert.AreEqual( (8, 6), effort.Kept );
			Assert.AreEqual( ParkAudio.ChannelGain( 8 ), effort.Voice.Volume, 1e-5f );

			var faded = park.ScriptSounds.Faded;
			rides.ThumbReleased!();
			Assert.AreEqual( (true, faded + 1), (effort.Stopped, park.ScriptSounds.Faded) );
		} );
	}

	/// <summary>The End's sound node 10 on its model, in whole units of the world, the height last.</summary>
	private static readonly Vector3 NodePlace = new( -21f, 57f, 16f );

	/// <summary>
	/// <b>The stock park's gates' held chain comes back from <c>Easymode.TPWI</c> under its handle</b>
	/// (<c>docs/exe/audio.md</c>, "A park file's voices"): the jungle ambient's 177 at (450, 0, 160), with its saved
	/// child's sample, variation 8's first, kept and played as the chain is first in range at a variation the zones of
	/// variation 8 draw (never 8), its own variation drawn again, and no flag <c>0x20</c>, so the first time its list is
	/// empty its child is the effect's first variation, and the second time it goes on from its own. The file's other
	/// nine are counted by class: four one-shots, three plain voices, two of the music's class.
	/// </summary>
	[TestMethod]
	public void TheShippedParksGatesChainIsPutBackWithItsChild()
	{
		var nowBefore = Time.Now;

		WithAParksAudio( park =>
		{
			Time.Now = 100f;
			HearFrom( new Vector3( 450f, 160f, 0f ) );

			var put = park.PutBackFileVoices( ShippedVoices() );

			Assert.AreEqual( 1, put.Count );

			var gates = put[0x012e0055];
			var headers = gates.Category.VariationsOf( 177 );
			var saved = gates.Category.SampleAt( 177, 7, 0, out var length );

			Assert.AreEqual( ("held chain", 177, 4, "the park file"), (gates.Kind, gates.Effect, gates.Type, gates.Owner) );
			Assert.AreEqual( new Vector3( 450f, 160f, 0f ), gates.Place );
			Assert.IsTrue( park.ScriptVoices.Contains( gates ) );
			Assert.IsFalse( gates.ListEmpty || gates.FirstChildTaken, "its child kept, and no 0x20" );
			Assert.AreSame( saved, gates.KeptChild, "variation 8's sample" );
			Assert.AreNotEqual( 7, gates.KeptChildVariation, "the child's variation one of variation 8's zones, which never name 8" );
			Assert.AreEqual( 100f + 0.25f + (float)length.TotalSeconds, gates.NextAt, 1e-4f, "its set-up's clock, 250 ms and the sample's length" );
			Assert.AreEqual( 0, gates.Samples );
			Assert.IsFalse( Counted( "PARK_FILE_SOUND_VOICE_SCREAM" ) || Counted( "PARK_FILE_SOUND_VOICE_NO_CATEGORY" ) );
			Assert.AreEqual( 4, Times( "PARK_FILE_SOUND_VOICE_ONE_SHOT" ) );
			Assert.AreEqual( 3, Times( "PARK_FILE_SOUND_VOICE_PLAIN" ) );
			Assert.AreEqual( 2, Times( "PARK_FILE_SOUND_VOICE_MUSIC_CLASS" ) );

			park.ServeScriptSounds();

			Assert.AreEqual( 1, gates.Samples, "the kept child as the chain is in range" );
			Assert.AreEqual( saved!.Name, gates.Voice!.Name );
			Assert.AreEqual( headers[gates.KeptChildVariation].Range, gates.Voice.Range, "over its own variation's range" );
			Assert.IsNull( gates.KeptChild );

			park.ServeScriptSounds();
			Assert.AreEqual( 1, gates.Samples, "nothing more before its time" );

			var own = gates.Variation;

			Time.Now = gates.NextAt + 0.001f;
			park.ServeScriptSounds();

			Assert.AreEqual( 2, gates.Samples );
			Assert.AreNotEqual( own, gates.Variation, "the next among the zones of the chain's own variation" );

			Time.Now = gates.NextAt;
			park.ServeScriptSounds();

			Assert.IsTrue( gates.ListEmpty, "pruned on the millisecond it was due, with no child after it" );
			Assert.AreEqual( 2, gates.Samples );

			Time.Now += 0.001f;
			park.ServeScriptSounds();

			Assert.AreEqual( (3, 0), (gates.Samples, gates.Variation), "the first child from an empty list: the effect's first" );
			Assert.IsTrue( gates.FirstChildTaken );

			Time.Now = gates.NextAt;
			park.ServeScriptSounds();
			Time.Now += 0.001f;
			park.ServeScriptSounds();

			Assert.AreEqual( 4, gates.Samples );
			Assert.AreNotEqual( 0, gates.Variation, "the second time it goes on from its own variation" );
		} );

		Time.Now = nowBefore;
	}

	/// <summary>
	/// <b>Each put-back draws the chain's own variation again</b>, by weight among all nine of 177's
	/// (<c>0x006bbfc5</c>), and its kept child's among the zones of the saved eighth, which never name it.
	/// </summary>
	[TestMethod]
	public void APutBackChainDrawsItsVariationsAgain()
	{
		WithAParksAudio( park =>
		{
			var voices = ShippedVoices();
			var own = new HashSet<int>();
			var child = new HashSet<int>();

			for ( var time = 0; time < 60; ++time )
			{
				var gates = park.PutBackFileVoices( voices )[0x012e0055];

				own.Add( gates.Variation );
				child.Add( gates.KeptChildVariation );
			}

			Assert.IsTrue( own.Count >= 5, $"the chain's own drawn among all: {string.Join( ",", own )}" );
			Assert.IsTrue( child.Count >= 4 && !child.Contains( 7 ), $"the child's among variation 8's zones: {string.Join( ",", child )}" );
		} );
	}

	/// <summary>
	/// <b>The kept child sounds over its own variation's range, not its chain's</b>: the Staff Room's 188 (its place 5 in
	/// <c>cat_staff</c>), whose fourth variation reaches 85 and the other six 90, put back until the chain's own variation
	/// and its child's differ in range.
	/// </summary>
	[TestMethod]
	public void AKeptChildSoundsOverItsOwnVariationsRange()
	{
		WithAParksAudio( park =>
		{
			var voices = new ParkVoices( ParkVoicesTests.Body( (0x01000003, 6, true, 5, 5) ) );
			ParkAudio.ScriptVoice? staff = null;

			for ( var time = 0; time < 300 && staff is null; ++time )
			{
				var put = park.PutBackFileVoices( voices )[0x01000003];
				var ranges = put.Category.VariationsOf( 188 );

				if ( ranges[put.Variation].Range != ranges[put.KeptChildVariation].Range )
					staff = put;
			}

			Assert.IsNotNull( staff, "a chain whose own variation and its child's differ in range" );
			HearFrom( new Vector3( 12f, 56f, 0f ) );
			park.ServeScriptSounds();

			var headers = staff.Category.VariationsOf( 188 );

			Assert.AreEqual( 1, staff.Samples );
			Assert.AreEqual( headers[staff.KeptChildVariation].Range, staff.Voice!.Range );
		} );
	}

	/// <summary>
	/// <b>A held chain of the screams, or of a category no script plays in, is counted and not put back</b>: the kids'
	/// 73 is <see cref="ParkScreams"/>' (its place 15 in <c>cat_kids</c>), and the speech is no script's.
	/// </summary>
	[TestMethod]
	public void AScreamsChainAndASpeechChainAreCountedNotPutBack()
	{
		WithAParksAudio( park =>
		{
			var voices = new ParkVoices( ParkVoicesTests.Body( (0x01000003, 6, true, 3, 15), (0x01000004, 6, false, 4, 0) ) );

			Assert.IsNull( voices.Problem );
			Assert.AreEqual( 2, voices.Saved.Count );
			Assert.AreEqual( 0, park.PutBackFileVoices( voices ).Count );
			Assert.AreEqual( (1, 1), (Times( "PARK_FILE_SOUND_VOICE_SCREAM" ), Times( "PARK_FILE_SOUND_VOICE_NO_CATEGORY" )) );
		} );
	}

	/// <summary>
	/// <b>A record a load put back takes the voice the file kept under its handle</b>, and its <c>KILLOBJ</c> stops it, as
	/// the Loudspeaker's and the Staff Room's records name theirs: not a particle's record of the same handle, nor a sound's
	/// of a handle no voice was put back under.
	/// </summary>
	[TestMethod]
	public void ARestoredRecordTakesTheVoiceItsHandleNamesAndItsKillStopsIt()
	{
		WithAParksAudio( park =>
		{
			var put = park.PutBackFileVoices( ShippedVoices() );
			var gates = put[0x012e0055];
			var script = new RideScript( Build( Word( Opcode.KILLOBJ ), 10, Word( Opcode.END ) ) ) { Effects = new RideEffects(), Id = 16 };

			// Restored newest first, so the particle's record is met first and the sound's last.
			script.Effects!.Restore( [new SavedEffect( 4, 0x012e0055, -1, -1, 10 ), new SavedEffect( 4, 0x0abc0001, -1, -1, 11 ),
				new SavedEffect( 1, 0x012e0055, -1, -1, 12 )] );

			var other = new RideScript( Build( Word( Opcode.END ) ) ) { Effects = new RideEffects(), Id = 17 };

			other.Effects!.Restore( [new SavedEffect( 4, 0x012e0055, -1, -1, 10 )] );

			Assert.AreEqual( 12, script.Effects.Records[0].Tag );
			Assert.AreEqual( 1, script.TakeRestoredSounds( put ) );
			Assert.AreEqual( 0, other.TakeRestoredSounds( put ), "a voice another record has taken" );
			Assert.IsNull( other.Effects.Records[0].Sound );
			Assert.AreEqual( (16, "script 16"), (gates.Script, gates.Owner) );
			Assert.AreSame( gates, script.Effects.Records.Single( record => record.Tag == 10 ).Sound );
			Assert.IsTrue( script.Effects.Records.Where( record => record.Tag != 10 ).All( record => record.Sound is null ) );
			Assert.AreEqual( 0, script.TakeRestoredSounds( put ), "taken once" );

			Assert.AreEqual( 0, park.FileVoicesLetGo.Count );

			script.Turn( 0f );

			Assert.IsTrue( gates.Stopped );
			Assert.IsFalse( park.ScriptVoices.Contains( gates ) );
			CollectionAssert.AreEquivalent( new[] { 0x012e0055 }, park.FileVoicesLetGo.ToArray(), "what a save leaves out" );
		} );
	}

	/// <summary>The stock park's voice list.</summary>
	private static ParkVoices ShippedVoices()
	{
		using var stream = new MemoryStream( FileSystem.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		var reader = new SaveReader( stream );

		return new ParkWorld( reader.ReadFile(), reader.Preamble ).Voices;
	}

	private static int Times( string what ) => Unimplemented.Summary.FirstOrDefault( entry => entry.What == what ).Times;
}
