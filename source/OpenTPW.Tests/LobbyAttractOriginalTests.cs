using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;

namespace OpenTPW.Tests;

/// <summary>
/// The lobby's attract flight is the original's (Q950): the box its camera wanders in is 300 to 700 across and 50 to 100
/// high (<c>FUN_005dfcd0</c>, <c>docs/exe/lobby.md</c>, "The object"), and with nobody playing one park sounds, the head
/// island's, wherever the camera is (<c>docs/exe/lobby.md</c>, "Island sound is one island at a time").
/// </summary>
[TestClass]
public class LobbyAttractOriginalTests
{
	/// <summary>
	/// <b>The box's corners and middle, each axis on its own roll.</b> A roll of 0 is the box's low edge, 1 its high edge, 0.5 its
	/// centre: X and Y 300 to 700, height 50 to 100. Three different rolls tell the axes apart; the lowered 30-to-60 band gives a
	/// height of 30 at 0 and 60 at 1.
	/// </summary>
	/// <remarks><b>Mutations:</b> the band back at 30 to 60 (centre or extent); the extent taken as a half width (X 400 to 600);
	/// the height rolled with the Y roll.</remarks>
	[TestMethod]
	[DataRow( 0f, 0f, 0f, 300f, 300f, 50f )]
	[DataRow( 1f, 1f, 1f, 700f, 700f, 100f )]
	[DataRow( 0.5f, 0.5f, 0.5f, 500f, 500f, 75f )]
	[DataRow( 0f, 0.5f, 1f, 300f, 500f, 100f )]
	[DataRow( 1f, 0f, 0.25f, 700f, 300f, 62.5f )]
	public void TheBoxIsTheOriginals( float rollX, float rollY, float rollZ, float x, float y, float height )
	{
		var point = LobbyCameraMode.PointInBox( rollX, rollY, rollZ );

		Assert.AreEqual( x, point.X, 0.001f, "X across the lobby" );
		Assert.AreEqual( y, point.Y, 0.001f, "Y across the lobby" );
		Assert.AreEqual( height, point.Z, 0.001f, "the height above it" );
	}

	/// <summary>
	/// <b>The head island's park sounds with nobody playing, whichever island is nearest</b>, and the island on show's
	/// otherwise. The list is given out of order: the head is the lowest <c>Index</c>, not the first handed in.
	/// </summary>
	/// <remarks><b>Mutations:</b> the nearest island's park used while flying (every park at once, one at a time by distance);
	/// the first handed in rather than the lowest index.</remarks>
	[TestMethod]
	public void NobodyPlayingTheHeadIslandsParkSoundsWhereverTheCameraIs()
	{
		var islands = new List<(int, string)> { (2, "Hallow"), (0, "Jungle"), (3, "Space"), (1, "Fantasy") };

		Assert.AreEqual( "Jungle", LobbyAudio.SoundingTheme( true, islands, "Space" ), "flying near Space" );
		Assert.AreEqual( "Jungle", LobbyAudio.SoundingTheme( true, islands, "Fantasy" ), "flying near Fantasy" );
		Assert.AreEqual( "Jungle", LobbyAudio.SoundingTheme( true, islands, null ), "flying with no island picked yet" );
		Assert.AreEqual( "Space", LobbyAudio.SoundingTheme( false, islands, "Space" ), "somebody playing: the island on show" );
		Assert.IsNull( LobbyAudio.SoundingTheme( true, new List<(int, string)>(), null ), "no islands, no park" );
	}
}
