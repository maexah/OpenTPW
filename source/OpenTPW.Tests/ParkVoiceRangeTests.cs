using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace OpenTPW.Tests;

/// <summary>
/// A placed voice's range where it is given none of its own: its variation's, the float at <c>+0x22</c> of the
/// header (<c>docs/exe/audio.md</c>, "A voice's range, and the rectangle it follows the listener in").
/// </summary>
[TestClass]
public class ParkVoiceRangeTests
{
	private static readonly string[] Themes = ["jungle", "fantasy", "hallow", "space"];

	/// <summary>Every category the game ships, as (the folder its banks are relative to, its maps' folder, its name).</summary>
	private static IEnumerable<(string Root, string Maps, string Name)> Categories()
	{
		foreach ( var name in new[] { "staff", "rides", "ambient", "ui", "kids", "globallobbysfx" } )
			yield return ("global", "global/sound", name);

		yield return ("global", "global/Speech", "speech");

		foreach ( var theme in Themes )
		{
			foreach ( var name in new[] { "locallobbysfx", "locallobbymusic", "rides", "ambient" } )
				yield return ($"levels/{theme}", $"levels/{theme}/Sound", name);

			yield return ($"levels/{theme}", $"levels/{theme}/Speech", "speech");
			yield return ($"levels/{theme}", $"levels/{theme}/Music", "music");
		}
	}

	[TestMethod]
	public void EveryShippedVariationsRangeIsOneOfEighteenNumbers()
	{
		FileSystem = GameData.Required();

		var spread = new SortedDictionary<float, int>();
		var files = 0;

		foreach ( var (_, maps, name) in Categories() )
		{
			var file = new SoundCategoryFile( maps, name );
			var headers = file.ReadVariations();

			Assert.IsTrue( file.IsValid, $"{maps}/{name}" );
			Assert.AreEqual( file.Effects.Count, headers.Count, $"{maps}/{name} walks to its end" );
			++files;

			foreach ( var variation in headers.SelectMany( effect => effect ) )
				spread[variation.Range] = spread.GetValueOrDefault( variation.Range ) + 1;
		}

		Assert.AreEqual( 31, files );
		Assert.AreEqual( 1595, spread.Values.Sum() );
		CollectionAssert.AreEqual(
			new[] { 0f, 0.1f, 10f, 20f, 30f, 45f, 50f, 60f, 70f, 75f, 80f, 85f, 90f, 95f, 100f, 150f, 170f, 1500f },
			spread.Keys.ToArray() );
		Assert.AreEqual( 645, spread[0f], "the speech, flagged to have no place" );
		Assert.AreEqual( 738, spread[100f] );
		Assert.AreEqual( 34, spread[95f] );
	}

	[TestMethod]
	public void ARangeIsTheVariationsOwnAndNoneForAnEffectWithNoPlace()
	{
		FileSystem = GameData.Required();

		var kids = new SoundCategory( "global", "global/sound", "kids" );
		var staff = new SoundCategory( "global", "global/sound", "staff" );
		var ambient = new SoundCategory( "global", "global/sound", "ambient" );

		// The four held screams and the sixteen single ones are 95 in every variation.
		for ( var effect = 71; effect <= 90; ++effect )
			for ( var variation = 0; variation < kids.VariationsOf( effect ).Count; ++variation )
				Assert.AreEqual( 95f, kids.RangeOf( effect, variation ), $"kids {effect} variation {variation}" );

		Assert.AreEqual( 4, kids.VariationsOf( 71 ).Count );
		Assert.AreEqual( 100f, kids.RangeOf( 0x80, 0 ), "a guest put off" );
		Assert.AreEqual( 100f, kids.RangeOf( 0x7e, 0 ), "a yawn" );
		Assert.AreEqual( 70f, kids.RangeOf( 131, 0 ) );
		Assert.AreEqual( 75f, staff.RangeOf( 0xa9, 0 ) );
		Assert.AreEqual( 90f, staff.RangeOf( 0x87, 0 ) );

		// The staff's 188 is the one global effect whose variations differ: its fourth is 85 among six of 90.
		CollectionAssert.AreEqual( new float?[] { 90f, 90f, 90f, 85f, 90f, 90f, 90f },
			Enumerable.Range( 0, 7 ).Select( variation => staff.RangeOf( 188, variation ) ).ToArray() );
		Assert.AreEqual( 1500f, ambient.RangeOf( 32, 0 ), "thunder, from a bolt's top" );
		Assert.AreEqual( 100f, ambient.RangeOf( 8, 0 ), "the waterfall's own variation; its placed record gives it 97" );

		// Rain is flagged 0x0606 and the crowd's voice 0x0606: bit 0x200, a voice with no place and no range.
		Assert.AreEqual( 0x200, ambient.FlagsOf( 33 ) & SoundCategory.NoPlaceFlag );
		Assert.AreEqual( 100f, ambient.VariationsOf( 33 )[0].Range, "the file still holds one" );
		Assert.IsNull( ambient.RangeOf( 33, 0 ) );
		Assert.IsNull( kids.RangeOf( 91, 0 ) );

		Assert.IsNull( kids.RangeOf( 71, 4 ), "a variation past the effect's last" );
		Assert.IsNull( kids.RangeOf( 71, -1 ) );
		Assert.IsNull( kids.RangeOf( 9999, 0 ), "an effect the category has not got" );
	}

	/// <summary>
	/// The wiring, with a device stood in as <see cref="VoicePlacementTests"/> stands one in: each of the park's
	/// placed voices is started with its variation's range, and a placed voice asked for without it has none.
	/// </summary>
	[TestMethod]
	public void TheParksPlacedVoicesAreStartedWithTheirVariationsRange()
	{
		FileSystem = GameData.Required();

		var ready = typeof( Audio ).GetProperty( nameof( Audio.Ready ) )!;

		Assert.IsFalse( Audio.Ready, "a test run has no audio device" );

		List<Voice> before;

		lock ( Audio.Lock )
			before = [.. Audio.Voices];

		ParkAudio? park = null;

		Voice Last()
		{
			lock ( Audio.Lock )
			{
				var voice = Audio.Voices[^1];

				Assert.IsFalse( before.Contains( voice ), "a voice was started" );
				Audio.Voices.Remove( voice );

				return voice;
			}
		}

		try
		{
			ready.SetValue( null, true );
			park = new ParkAudio( "jungle" );

			var at = new Vector3( 515f, 235f, 0f );

			park.Thunder( at + new Vector3( 0f, 0f, 300f ) );
			Assert.AreEqual( 1500f, Last().Range );

			Assert.IsTrue( park.SingleScream( 1, -1, at ) );
			Assert.AreEqual( 100f, Last().Range, "effect 0x69" );

			Assert.IsTrue( park.SingleScream( 1, 90, at ) );
			Assert.AreEqual( 95f, Last().Range, "effect 0x4d" );

			Assert.IsTrue( park.PutOff( at ) );
			Assert.AreEqual( 100f, Last().Range );

			Assert.IsTrue( park.Yawn( at ) );
			Assert.AreEqual( 100f, Last().Range );

			Assert.IsTrue( park.StaffSound( 0xa9, at ) );
			Assert.AreEqual( 75f, Last().Range );

			Assert.IsTrue( park.StaffSound( 0x87, at ) );
			Assert.AreEqual( 90f, Last().Range );

			Assert.IsTrue( park.Scream( 7, 1, 50, at ) );

			var child = Last();

			Assert.IsTrue( child.IsPlaced );
			Assert.AreEqual( 95f, child.Range, "a held scream's child" );

			// A placed voice the category is asked for without its range has none: the lobby's are this project's own.
			var kids = new SoundCategory( "global", "global/sound", "kids" );

			Assert.IsNull( kids.Play( 0x80, 0.5f, respectDelay: false, position: at )!.Range );
			Last();
			Assert.AreEqual( 100f, kids.Play( 0x80, 0.5f, respectDelay: false, position: at, ownRange: true )!.Range );
			Last();
			Assert.AreEqual( 40f, kids.Play( 0x80, 0.5f, respectDelay: false, position: at, range: 40f, ownRange: true )!.Range,
				"a range of the voice's own comes first" );
			Last();
			Assert.IsNull( kids.Play( 0x80, 0.5f, respectDelay: false, ownRange: true )!.Range, "a flat voice has none" );
			Last();
		}
		finally
		{
			park?.Delete();
			Entity.ApplyDeletions();

			lock ( Audio.Lock )
				Audio.Voices.RemoveAll( voice => !before.Contains( voice ) );

			ready.SetValue( null, false );
		}
	}
}
