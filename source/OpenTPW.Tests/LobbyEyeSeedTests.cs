using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenTPW.Tests;

/// <summary>
/// A lobby's island camera starts from the original's seed (Q749): the render camera <c>[0x00f83778]</c> is built afresh by
/// <c>FrontEnd_Init</c> on every entry to the lobby, with its eye at (500, 100, 500) and its look point at (500, 0, 500) in the
/// original's y-up axes (written by <c>FUN_005d8b50</c>, <c>0x005d8c1b</c>..<c>0x005d8c31</c>), and the island camera's
/// constructor copies both (<c>0x005dfd3c</c>..<c>0x005dfd99</c>; <c>docs/exe/lobby.md</c>, "The orbit's seed"). Ours is z-up, and <see cref="LobbyCameraMode.EyeState"/> reads back in the
/// original's axes.
/// </summary>
[TestClass]
[DoNotParallelize]
public class LobbyEyeSeedTests
{
	/// <summary>
	/// <b>A lobby built after a lobby that had been placed elsewhere stands at the seed, not where the last one was.</b>
	/// <see cref="LobbyCameraMode.DebugSettle"/> leaves the camera unplaced (the state a first lobby is in); the build has to
	/// place it. The exact text pins every axis: a swapped height and depth reads (500, 500, 100), a look point left at the
	/// eye's height reads (500, 100, 500), and an unseeded build reads (0, 0, 0) with <c>placed=False</c>. The orbit is turned
	/// first, so a build that does not zero it reads its leftover.
	/// </summary>
	/// <remarks><b>Mutations:</b> the seed not written (the old build); the eye in the original's axes unconverted
	/// (500, 100, 500 as z-up); the look point's height left at 100; the orbit not zeroed.</remarks>
	[TestMethod]
	public void ABuiltLobbyStartsFromTheRenderCamerasSeed()
	{
		LobbyCameraMode.DebugSettle();
		LobbyCameraMode.DebugOrbit = 3f;

		LobbyCameraMode.BeginLobby();

		StringAssert.StartsWith( LobbyCameraMode.EyeState(),
			"eye (500.000, 100.000, 500.000) look (500.000, 0.000, 500.000) placed=True orbit=0.000" );
	}

	/// <summary>
	/// <b>The seed is ours' z-up form of the original's.</b> The eye over the middle of the four islands at height 100, the look
	/// point on the ground under it.
	/// </summary>
	/// <remarks><b>Mutations:</b> the two axes swapped; the height 10 rather than 100.</remarks>
	[TestMethod]
	public void TheSeedIsTheOriginalsInZUpAxes()
	{
		Assert.AreEqual( new Vector3( 500f, 500f, 100f ), LobbyCameraMode.SeedEye );
		Assert.AreEqual( new Vector3( 500f, 500f, 0f ), LobbyCameraMode.SeedLookAt );
	}
}
