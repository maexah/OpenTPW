using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenTPW.Tests;

/// <summary>
/// The lobby's one-frame inversion after a bolt (0x005e110b, <c>docs/exe/lobby.md</c>, "The lobby's bolt inverts the screen for
/// one frame") is not built, so each lobby strike counts <c>LOBBY_BOLT_FLASH</c> (rule 4, Q1050).
/// </summary>
[TestClass]
[DoNotParallelize]
public class LobbyBoltFlashCountTests
{
	/// <summary>A lobby weather without its rain, which needs a renderer: its bolt draws nothing.</summary>
	private static LobbyWeather BoltOnly()
	{
		var weather = (LobbyWeather)RuntimeHelpers.GetUninitializedObject( typeof( LobbyWeather ) );

		weather.StrikeBolt = ( _, _ ) => { };

		return weather;
	}

	private static int Times( string what ) => Unimplemented.Summary.FirstOrDefault( entry => entry.What == what ).Times;

	/// <summary>
	/// <b>Each lobby strike is counted once, and a park's strike is not</b>: three strikes read 3, and a park's bolt (which has no
	/// flash) leaves the count where it was.
	/// </summary>
	/// <remarks><b>Mutations:</b> the report removed (0); the report put in <c>Lightning.Strike</c> (the park's strike counts too); the report put in <c>OnUpdate</c> (once a frame).</remarks>
	[TestMethod]
	public void EachLobbyStrikeIsCountedAndAParksIsNot()
	{
		Unimplemented.Forget();
		var lobby = BoltOnly();
		var park = new ParkWeather( null, drawn: false );

		try
		{
			lobby.DebugStrike();
			lobby.DebugStrike();
			lobby.DebugStrike();

			Assert.AreEqual( 3, Times( "LOBBY_BOLT_FLASH" ) );

			park.DebugStrike( new Vector3( 500f, 500f, 0f ) );

			Assert.AreEqual( 3, Times( "LOBBY_BOLT_FLASH" ), "a park's bolt never sets the flash" );
		}
		finally
		{
			park.Delete();
			Unimplemented.Forget();
		}
	}
}
