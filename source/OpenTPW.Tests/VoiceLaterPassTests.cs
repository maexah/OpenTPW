using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// A looped voice's second and later passes (Q1166). The original's in-memory voice class, made for a bank whose byte
/// <c>+9</c> has bit 0x1 (<c>0x006ba690</c>), rewinds through <c>0x006c84a0</c>, which for Layer I decodes 0x300 bytes and
/// drops them, so pass 1 plays [0, D) and every later pass [0x300, 0x300 + D) of the decode; a streamed bank's voice rewinds
/// with no skip (<c>0x006c8b10</c>; docs/exe/audio.md, "A looped Layer I voice's later passes start one frame in"). Read from
/// that page, not in Ghidra. A voice is stepped here frame by frame (<see cref="Voice.NextFrame"/>), as its mixer steps it.
/// </summary>
[TestClass]
public class VoiceLaterPassTests
{
	/// <summary>The clip frame each of <paramref name="count"/> output frames plays, -1 once a one-shot has ended.</summary>
	private static List<int> Step( AudioClip clip, bool loop, int count )
	{
		var voice = new Voice( clip, 1f, loop, fadeInSeconds: 0f, AudioBus.Effects, position: null );
		var frames = new List<int>( count );

		for ( int i = 0; i < count; ++i )
			frames.Add( voice.NextFrame() );

		return frames;
	}

	/// <summary>
	/// A looped Layer I clip of an in-memory bank (byte +9 0x01) plays its 1,000 played frames from 0, then starts each later
	/// pass 0x300 bytes in, 384 frames mono and 192 stereo, and plays 1,000 frames from there: the clip keeps those frames
	/// past the played length. A rate that steps past the end carries its overrun into the next pass.
	/// </summary>
	/// <remarks><b>Mutations:</b> every pass from 0 (frame 1,000 reads 0); the skip not divided by the channels (stereo reads
	/// 384); the later pass ending at the played length, not 1,000 past its start (frame 1,999 is not 1,383); the first pass
	/// skipping too (frame 0 reads 384); the frames past the played length not kept (StoredFrames 1,000).</remarks>
	[TestMethod]
	public void ALoopedLayerIClipOfAnInMemoryBankStartsItsLaterPassesOneFrameIn()
	{
		var mono = AudioClip.Decode( "mono", AudioClipTests.SilentLayerI( 5, mpeg1: false, stereo: false ), 2000, bankFlags: 0x01 )!;

		Assert.AreEqual( 1, mono.Layer );
		Assert.AreEqual( 0x01, mono.BankFlags );
		Assert.AreEqual( 1000, mono.Frames, "a pass is the played length" );
		Assert.AreEqual( 384, mono.LaterPassStart );
		Assert.AreEqual( 1384, mono.StoredFrames, "the later passes' frames are kept" );

		var frames = Step( mono, loop: true, 3001 );
		Assert.AreEqual( 0, frames[0], "the first pass starts at 0" );
		Assert.AreEqual( 999, frames[999] );
		Assert.AreEqual( 384, frames[1000], "the second pass starts 384 frames in" );
		Assert.AreEqual( 1383, frames[1999], "and plays 1,000 frames from there" );
		Assert.AreEqual( 384, frames[2000], "the third starts there too" );
		Assert.AreEqual( 1383, frames[2999], "and is as long" );
		Assert.AreEqual( 384, frames[3000] );

		var stereo = AudioClip.Decode( "stereo", AudioClipTests.SilentLayerI( 5, mpeg1: false, stereo: true ), 2000, bankFlags: 0x41 )!;

		Assert.AreEqual( 500, stereo.Frames );
		Assert.AreEqual( 192, stereo.LaterPassStart, "0x300 bytes of stereo 16-bit PCM" );
		Assert.AreEqual( 192, Step( stereo, loop: true, 501 )[500], "byte +9 0x41 has bit 0x1" );

		// At a rate of 3 the first pass's last read is frame 999 and the next lands 2 frames past the end: 384 + 2.
		var fast = new Voice( mono, 1f, loop: true, fadeInSeconds: 0f, AudioBus.Effects, position: null );
		fast.SetRate( 3f );
		var read = Enumerable.Range( 0, 335 ).Select( _ => fast.NextFrame() ).ToList();
		Assert.AreEqual( 999, read[333] );
		Assert.AreEqual( 386, read[334], "the overrun is carried into the next pass" );
	}

	/// <summary>
	/// Through the categories, as a park plays them: the global ambient's River (effect 7, <c>Sound\Ambient</c>, byte +9 0x01)
	/// starts its later passes 384 frames in; jungle's <c>RideHighPass</c> (rides effect 80, <c>Sound\sRide</c>, 0x02, Layer I)
	/// and the UI's <c>WinOneShot</c> (effect 186, <c>Sound\sUi</c>, 0x02, Layer II), both looped effects of streamed banks,
	/// start every pass at 0. Across the global and Lost Kingdom categories every clip's later passes start 0x300 bytes in
	/// exactly when it is Layer I and its bank's byte has bit 0x1 (at the stream's rate: the five 44,100 Hz clips, none of them
	/// in a looped effect, start 192 frames in at 22,050), it keeps that many frames more, and its pass is its played length;
	/// Layer II clips of in-memory banks start at 0.
	/// </summary>
	/// <remarks><b>Mutations:</b> the category not handing its banks their flags (River at 0); the flag tested as byte +9 equal to
	/// 1 (the 0x41 banks' clips at 0, the count short); the layer left out (Layer II clips of in-memory banks skip); every bank
	/// skipping (RideHighPass at 384).</remarks>
	[TestMethod]
	public void AStreamedBanksLoopAndALayerIIClipStartEveryPassAtNought()
	{
		FileSystem = GameData.Required();
		SoundBank.ForgetKept();

		try
		{
			var river = new SoundCategory( "global", "global/sound", "ambient" ).SampleAt( 7, 0, 0, out _ )!;
			Assert.AreEqual( "River.mp2", river.Name );
			Assert.AreEqual( (1, 0x01, 384), (river.Layer, river.BankFlags, river.LaterPassStart) );
			Assert.AreEqual( 384, Step( river, loop: true, river.Frames + 1 )[river.Frames], "the river's second pass" );

			var high = new SoundCategory( "levels/jungle", "levels/jungle/Sound", "rides" ).SampleAt( 80, 0, 0, out _ )!;
			Assert.AreEqual( "RideHighPass.mp", high.Name );
			Assert.AreEqual( (1, 0x02, 0), (high.Layer, high.BankFlags, high.LaterPassStart) );
			Assert.AreEqual( high.Frames, high.StoredFrames, "a streamed bank's clip keeps nothing past its played length" );
			Assert.AreEqual( 0, Step( high, loop: true, high.Frames + 1 )[high.Frames], "a streamed bank's second pass" );

			var win = new SoundCategory( "global", "global/sound", "ui" ).SampleAt( 186, 0, 0, out _ )!;
			Assert.AreEqual( "WinOneShot.mp2", win.Name );
			Assert.AreEqual( (2, 0x02, 0), (win.Layer, win.BankFlags, win.LaterPassStart) );
			Assert.AreEqual( 0, Step( win, loop: true, win.Frames + 1 )[win.Frames] );

			var options = new EnumerationOptions { RecurseSubdirectories = true, MatchCasing = MatchCasing.CaseInsensitive };
			var maps = Directory.EnumerateFiles( Path.Combine( GameDir.Data, "global" ), "cat_*BANK.map", options )
				.Concat( Directory.EnumerateFiles( Path.Combine( GameDir.Data, "levels", "jungle" ), "cat_*BANK.map", options ) );
			var skipping = 0;
			var layerTwoInMemory = 0;
			var wrong = new List<string>();

			foreach ( var map in maps )
			{
				var folder = Path.GetRelativePath( GameDir.Data, Path.GetDirectoryName( map )! ).Replace( '\\', '/' );
				var root = folder.StartsWith( "global", StringComparison.OrdinalIgnoreCase ) ? "global" : "levels/jungle";
				var file = new SoundCategoryFile( folder, Path.GetFileName( map )[4..^8] );

				for ( int b = 0; b < file.Banks.Count; ++b )
				{
					var bank = SoundBank.Load( root, file.Banks[b], flags: file.BankFlags[b] )!;
					using var stream = FileSystem.OpenReadOrNull( bank.Path )!;
					using var archive = new SdtArchive( stream );

					for ( int i = 0; i < bank.Count; ++i )
					{
						var clip = bank[i];

						if ( clip == null )
							continue;

						var entry = archive.soundFiles[i];
						var skips = clip.Layer == 1 && (file.BankFlags[b] & 0x1) != 0;
						var start = skips ? (int)((long)(0x300 / (2 * clip.Channels)) * Audio.SampleRate / entry.SampleRate) : 0;
						var played = (int)((long)(entry.DecodedSize / (2 * clip.Channels)) * Audio.SampleRate / entry.SampleRate);

						if ( clip.LaterPassStart != start || clip.StoredFrames != clip.Frames + start || clip.Frames != played )
							wrong.Add( $"{bank.Path}:{clip.Name} start {clip.LaterPassStart} stored {clip.StoredFrames} frames {clip.Frames}" );

						skipping += skips ? 1 : 0;
						layerTwoInMemory += clip.Layer == 2 && (file.BankFlags[b] & 0x1) != 0 ? 1 : 0;
					}
				}
			}

			Assert.AreEqual( 0, wrong.Count, string.Join( "; ", wrong.Take( 5 ) ) );
			Assert.AreEqual( SkippingClips, skipping, "Layer I clips of in-memory banks, a bank named by two categories counted twice" );
			Assert.AreEqual( LayerTwoInMemory, layerTwoInMemory, "Layer II clips of in-memory banks, which start at 0" );
		}
		finally
		{
			SoundBank.ForgetKept();
		}
	}

	private const int SkippingClips = 2769;
	private const int LayerTwoInMemory = 3;

	/// <summary>
	/// A one-shot of a clip that keeps its later passes' frames plays its played length and stops, as one of a clip that
	/// keeps none does: the frames kept past it are a loop's alone.
	/// </summary>
	/// <remarks><b>Mutations:</b> the one-shot played to the frames kept (frame 1,000 reads 1,000); <see cref="AudioClip.Frames"/>
	/// counted from what is stored (1,384, and a <see cref="AudioClip.Duration"/> 17 ms long).</remarks>
	[TestMethod]
	public void AOneShotPlaysItsPlayedLengthAndNoMore()
	{
		var data = AudioClipTests.SilentLayerI( 5, mpeg1: false, stereo: false );
		var kept = AudioClip.Decode( "kept", data, 2000, bankFlags: 0x01 )!;
		var plain = AudioClip.Decode( "plain", data, 2000 )!;

		Assert.AreEqual( 1384, kept.StoredFrames );
		Assert.AreEqual( (1000, 0, 1000), (plain.Frames, plain.LaterPassStart, plain.StoredFrames), "a clip of no bank keeps nothing more" );
		Assert.AreEqual( plain.Frames, kept.Frames );
		Assert.AreEqual( plain.Duration, kept.Duration );

		var frames = Step( kept, loop: false, 1002 );
		Assert.AreEqual( 999, frames[999] );
		Assert.AreEqual( -1, frames[1000], "the one-shot ends at its played length" );
		CollectionAssert.AreEqual( Enumerable.Range( 0, 1000 ).ToList(), frames.Take( 1000 ).ToList() );
	}

	/// <summary>
	/// A looped Layer I voice of a bank whose byte +9 has neither 0x1 nor 0x2 (the two <c>Sound\sfUi</c> records' 0x04) is of a
	/// class not read, so its start counts <c>LOOPED_LAYER_I_VOICE_OF_BANK_FLAGS_04</c> (rule 4, Q1640); a one-shot of it, and a
	/// loop of a 0x01, 0x41, 0x02 or 0x42 bank, count nothing.
	/// </summary>
	/// <remarks><b>Mutations:</b> the count left out (0 counted); the loop not asked (the one-shot counts); 0x41 or 0x42 taken as
	/// unread (byte tested whole, not by its two low bits).</remarks>
	[TestMethod]
	public void ALoopedLayerIVoiceOfABankOfAnUnreadClassIsCounted()
	{
		const string Prefix = "LOOPED_LAYER_I_VOICE_OF_BANK_FLAGS_";
		static int Counted() => Unimplemented.Summary.Where( entry => entry.What.StartsWith( Prefix, StringComparison.Ordinal ) ).Sum( entry => entry.Times );
		static int CountedOf( string what ) => Unimplemented.Summary.FirstOrDefault( entry => entry.What == what ).Times;

		var ready = typeof( Audio ).GetProperty( nameof( Audio.Ready ) )!;
		Assert.IsFalse( Audio.Ready, "a test run has no audio device" );

		int before;
		lock ( Audio.Lock )
			before = Audio.Voices.Count;

		ready.SetValue( null, true );

		try
		{
			var data = AudioClipTests.SilentLayerI( 5, mpeg1: false, stereo: false );
			var all = Counted();
			var ours = CountedOf( Prefix + "04" );

			Assert.IsNotNull( Audio.Play( AudioClip.Decode( "sfui", data, 2000, bankFlags: 0x04 ), loop: true ) );
			Assert.AreEqual( ours + 1, CountedOf( Prefix + "04" ), "a looped Layer I voice of a 0x04 bank" );

			Audio.Play( AudioClip.Decode( "sfui", data, 2000, bankFlags: 0x04 ), loop: false );

			foreach ( var flags in new[] { 0x01, 0x41, 0x02, 0x42 } )
				Audio.Play( AudioClip.Decode( "known", data, 2000, bankFlags: flags ), loop: true );

			Assert.AreEqual( all + 1, Counted(), "only the 0x04 bank's loop is counted" );
		}
		finally
		{
			lock ( Audio.Lock )
				Audio.Voices.RemoveRange( before, Audio.Voices.Count - before );

			ready.SetValue( null, false );
		}
	}
}
