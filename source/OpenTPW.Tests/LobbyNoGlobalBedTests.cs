using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// The island lobby plays no global lobby bed (Q770): the original's island lobby never runs <c>Lobby_Start</c>
/// (<c>0x005dcfe0</c>), the globe page's start, so global effects 2 and 3 do not sound (<c>docs/exe/audio.md</c>).
/// The device is stood in for as <see cref="VoicePlacementTests"/> does, by setting <see cref="Audio.Ready"/> for the
/// test's length; the voice list is read, nothing is opened.
/// </summary>
[TestClass]
public class LobbyNoGlobalBedTests
{
	/// <summary>An opening lobby with nobody playing and no island yet starts no voice at all, whatever the global category holds.</summary>
	/// <remarks><b>Mutation:</b> the global effect 3 bed played from <c>OnUpdate</c> again (a voice appears on the first update).</remarks>
	[TestMethod]
	public void TheOpeningLobbyStartsNoGlobalVoice()
	{
		FileSystem = GameData.Required();

		var ready = typeof( Audio ).GetProperty( nameof( Audio.Ready ) )!;
		Assert.IsFalse( Audio.Ready, "a test run has no audio device, so nothing here should find one" );

		LobbyCameraMode.ForgetIsland();

		int before;
		lock ( Audio.Lock )
			before = Audio.Voices.Count;

		ready.SetValue( null, true );
		LobbyAudio? lobby = null;

		try
		{
			lobby = new LobbyAudio();

			for ( var i = 0; i < 5; i++ )
				lobby.Update();

			lock ( Audio.Lock )
				Assert.AreEqual( before, Audio.Voices.Count, $"voices named {string.Join( ", ", Audio.Voices.Select( v => v.Name ) )}" );
		}
		finally
		{
			lobby?.Delete();
			Entity.ApplyDeletions();

			lock ( Audio.Lock )
				Audio.Voices.RemoveRange( before, Audio.Voices.Count - before );

			ready.SetValue( null, false );
		}
	}
}
