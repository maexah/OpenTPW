using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenTPW.Files;

namespace OpenTPW.Tests;

/// <summary>
/// A sound effect's loudness is its variation's volume as the original's mixer channel holds it: a whole number of
/// 127, the volume times the sound effects' 75 over a hundred rounded to even, beside the music's 60
/// (<c>docs/exe/audio.md</c>, "A channel's volume"). The numbers are the running original's own channels'.
/// </summary>
[TestClass]
public class ParkChannelVolumeTests
{
	[TestMethod]
	[DataRow( 100, 75 )]   // the waterfall
	[DataRow( 50, 38 )]    // the river: 37.5 goes up to the even
	[DataRow( 14, 10 )]    // a guest put off: 10.5 goes down to the even
	[DataRow( 47, 35 )]    // every scream
	[DataRow( 34, 26 )]    // 25.5 goes up to the even
	[DataRow( 24, 18 )]
	[DataRow( 16, 12 )]
	[DataRow( 0, 0 )]
	public void AVolumeIsAWholeNumberOf127RoundedToEven( int volume, int channel )
	{
		Assert.AreEqual( channel / 60f * ParkAudio.MusicVolume, ParkAudio.ChannelGain( volume ), 1e-6f );
	}

	[TestMethod]
	public void TheMusicsOwnHundredIsItsSixty()
	{
		Assert.AreEqual( 75, ParkAudio.EffectsGroupLevel );
		Assert.AreEqual( 60, ParkAudio.MusicGroupLevel );
		Assert.AreEqual( ParkAudio.MusicVolume * 75f / 60f, ParkAudio.ChannelGain( 100 ), 1e-6f );
	}

	/// <summary>
	/// A parameter moves a playing sample's volume only where one of its variation's own two controllers is on the
	/// volume and keyed by it (<c>0x006bbb80</c>).
	/// </summary>
	[TestMethod]
	public void AParameterIsOnTheVolumeOnlyByAControllerOfTheVariationsOwn()
	{
		var plain = new SoundCategoryFile.Variation { Volume = (47, 47) };
		var first = plain with { FirstKey = 6, FirstMask = 1 };
		var second = plain with { SecondKey = 6, SecondMask = 1 };
		var pitch = plain with { FirstKey = 6, FirstMask = 2, SecondKey = 6, SecondMask = 4 };
		var other = plain with { FirstKey = 7, FirstMask = 1 };

		Assert.IsFalse( ParkAudio.NamesOnVolume( plain, 6 ) );
		Assert.IsTrue( ParkAudio.NamesOnVolume( first, 6 ) );
		Assert.IsTrue( ParkAudio.NamesOnVolume( second, 6 ) );
		Assert.IsFalse( ParkAudio.NamesOnVolume( pitch, 6 ), "a controller on the pitch or the wait is not on the volume" );
		Assert.IsFalse( ParkAudio.NamesOnVolume( other, 6 ), "nor one keyed by another parameter" );
		Assert.IsFalse( ParkAudio.NamesOnVolume( plain with { SecondKey = 7, SecondMask = 1 }, 6 ), "the second controller's key is read too" );
	}

	/// <summary>
	/// No shipped scream names its parameter on its volume, and each holds one volume, 47: so a script's level
	/// moves no scream's loudness in any park.
	/// </summary>
	[TestMethod]
	public void EveryShippedScreamVariationHoldsFortySevenAndNoControllerOnIt()
	{
		FileSystem = GameData.Required();

		var kids = new SoundCategory( "global", "global/sound", "kids" );
		var seen = 0;

		for ( var effect = 0x47; effect <= 0x5a; ++effect )
		{
			foreach ( var header in kids.VariationsOf( effect ) )
			{
				Assert.AreEqual( (47, 47), header.Volume, $"effect {effect}" );
				Assert.IsFalse( ParkAudio.NamesOnVolume( header, kids.ParameterOf( effect ) ), $"effect {effect}" );
				++seen;
			}
		}

		Assert.AreEqual( 32, seen, "the kids' screams, effects 71 to 90" );
	}
}
