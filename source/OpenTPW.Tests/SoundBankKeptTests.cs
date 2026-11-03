using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// A bank decoded whole is read once for the run and handed out again (Q924), so a Load Park or a Restart Park, which
/// build the park's categories afresh behind the loading screen, do not decode the same sounds a second time. A bank
/// that decodes on demand is not kept: what it has decoded by then would stay in memory.
/// </summary>
[TestClass]
public class SoundBankKeptTests
{
	[TestInitialize]
	public void MountTheGame()
	{
		FileSystem = GameData.Required();
		SoundBank.ForgetKept();
	}

	[TestCleanup]
	public void LetGo() => SoundBank.ForgetKept();

	/// <summary>
	/// The global UI category's banks are all small and decode whole; the second ask for one is the first's object, the
	/// same clips, not a second decode. <b>Mutations:</b> the bank not put in <c>Kept</c>; <c>Kept</c> keyed by the
	/// bank path alone, so the global and a level's same-named bank answer each other.
	/// </summary>
	[TestMethod]
	public void ABankDecodedWholeIsReadOnce()
	{
		var banks = new SoundCategoryFile( "global/sound", "ui" ).Banks;

		Assert.IsTrue( banks.Count > 0, "the UI category names banks" );

		foreach ( var bank in banks )
		{
			var first = SoundBank.Load( "global", bank, keep: true );
			var second = SoundBank.Load( "global", bank, keep: true );

			Assert.IsNotNull( first, $"{bank} ships" );
			Assert.IsTrue( first!.Count > 0, $"{bank} holds samples" );
			Assert.AreSame( first, second, $"{bank}: the second ask decoded the bank again" );
			Assert.AreSame( first[0], second![0], $"{bank}: its first clip is a second decode" );
		}
	}

	/// <summary>
	/// The kids bank decodes 17 MB, over the limit, so it decodes entries on demand and is read afresh each time:
	/// keeping it would keep every clip it had decoded. <b>Mutation:</b> every bank kept, lazy or not.
	/// </summary>
	[TestMethod]
	public void ABankThatDecodesOnDemandIsNotKept()
	{
		var first = SoundBank.Load( "global", "Sound/Kids", keep: true );
		var second = SoundBank.Load( "global", "Sound/Kids", keep: true );

		Assert.IsNotNull( first );
		Assert.AreNotSame( first, second, "a bank decoding on demand was kept, with every clip it has decoded" );
	}

	/// <summary>
	/// Only a park keeps its banks (<c>keep: true</c>); a category built without it, as the lobby's are, decodes its own and
	/// leaves nothing behind. And the lobby, as it starts, lets the kept ones go: the next ask decodes afresh.
	/// <b>Mutations:</b> <c>keep</c> ignored; <see cref="SoundBank.ForgetKept"/> doing nothing.
	/// </summary>
	[TestMethod]
	public void OnlyAParkKeepsAndTheLobbyLetsGo()
	{
		var bank = new SoundCategoryFile( "global/sound", "ui" ).Banks[0];

		Assert.AreNotSame( SoundBank.Load( "global", bank ), SoundBank.Load( "global", bank ), "a bank loaded without keep was kept" );

		var kept = SoundBank.Load( "global", bank, keep: true );

		Assert.AreSame( kept, SoundBank.Load( "global", bank ), "a kept bank is handed to any asker" );

		SoundBank.ForgetKept();

		Assert.AreNotSame( kept, SoundBank.Load( "global", bank, keep: true ), "the lobby's start did not let the kept bank go" );
	}

	/// <summary>
	/// Two banks that share a file name in two folders are two banks: the cache is keyed by the file, not by its name.
	/// <b>Mutation:</b> the key taken as the file's name alone.
	/// </summary>
	[TestMethod]
	public void TwoBanksOfOneNameInTwoFoldersAreTwoBanks()
	{
		var global = SoundBank.Load( "global", "Sound/Ride", keep: true );
		var jungle = SoundBank.Load( "levels/jungle", "Sound/Ride", keep: true );

		Assert.IsNotNull( global );
		Assert.IsNotNull( jungle );
		Assert.AreNotSame( global, jungle, "the global ride bank answered for the jungle's" );
		Assert.AreNotEqual( global!.Count, jungle!.Count, "the two ride banks hold different samples" );
	}

	/// <summary>
	/// A park's sound keeps its banks, so building the park again finds them decoded, and the lobby's start lets them go.
	/// Asked for without <c>keep</c>, a bank comes back as the park's own object while the park's banks are kept, and as
	/// a new one once the lobby has started.
	/// <b>Mutations:</b> a park's categories not asking to keep (<c>ParkAudio</c>'s, <c>ParkAudioScripts</c>'s); the
	/// lobby's constructor not letting go.
	/// </summary>
	[TestMethod]
	public void AParkKeepsItsBanksAndTheLobbyLetsThemGo()
	{
		var ready = typeof( Audio ).GetProperty( nameof( Audio.Ready ) )!;

		Assert.IsFalse( Audio.Ready, "a test run has no audio device" );

		int before;
		lock ( Audio.Lock )
			before = Audio.Voices.Count;

		ready.SetValue( null, true );

		ParkAudio? park = null;
		LobbyAudio? lobby = null;

		try
		{
			park = new ParkAudio( "jungle" );

			// The global ambient's, the level's own rides', the level's ambient the scripts play in, and the staff, kids and
			// UI banks, so a category that stopped asking to keep shows. Measured by dropping flags: sUi is the UI category's
			// alone; Staff is named by both the staff category and the scripts' global rides, and xKids by both the kids
			// category and those rides, so one flag of each pair is redundant and only the pair dropped together shows.
			(string Root, string Bank)[] banks = [("global", "Sound/Ambient"), ("levels/jungle", "Sound/Ride"), ("levels/jungle", "Sound/Ambient"),
				("global", "Sound/Staff"), ("global", "Sound/xKids"), ("global", "Sound/sUi")];
			var kept = banks.Select( bank => SoundBank.Load( bank.Root, bank.Bank ) ).ToArray();

			for ( var i = 0; i < banks.Length; i++ )
			{
				Assert.IsNotNull( kept[i], $"{banks[i]} ships" );
				Assert.AreSame( kept[i], SoundBank.Load( banks[i].Root, banks[i].Bank ), $"the park did not keep {banks[i]}, which it decoded" );
			}

			lobby = new LobbyAudio();

			for ( var i = 0; i < banks.Length; i++ )
				Assert.AreNotSame( kept[i], SoundBank.Load( banks[i].Root, banks[i].Bank ), $"the lobby's start left {banks[i]} kept" );
		}
		finally
		{
			lobby?.Delete();
			park?.Delete();
			Entity.ApplyDeletions();

			lock ( Audio.Lock )
				Audio.Voices.RemoveRange( before, Audio.Voices.Count - before );

			ready.SetValue( null, false );
		}
	}

	/// <summary>A bank the game lacks is not kept as a bank: it is null each time, and the category fills the slot.</summary>
	[TestMethod]
	public void AMissingBankIsNullEachTime()
	{
		Assert.IsNull( SoundBank.Load( "global", "Sound/NoSuchBank", keep: true ) );
		Assert.IsNull( SoundBank.Load( "global", "Sound/NoSuchBank", keep: true ) );
	}
}
