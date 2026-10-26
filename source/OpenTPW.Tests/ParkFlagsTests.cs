using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenTPW.Tests;

/// <summary>
/// The gates' flags, <see cref="ParkFlags"/> (<c>docs/exe/park-engine.md</c>, "The gates' flags"). The expected numbers
/// are <c>q491/flagmodel.py</c>'s, the model the original's 384 faces fit to 3e-5 at three winds and three phases. The
/// phase asked, 7.3, and the wind, 139° (the stock park's), are neither nought nor a whole wave, and each point has a
/// column unlike its row, one inside the still part by the pole and one past it, so a wrong lag, a dropped clamp, the
/// axes swapped or a sign turned answers otherwise.
/// </summary>
[TestClass]
public class ParkFlagsTests
{
	private const float Near = 0.001f;

	private static void Close( Vector3 expected, Vector3 actual, string what )
	{
		Assert.AreEqual( expected.X, actual.X, Near, $"{what} x" );
		Assert.AreEqual( expected.Y, actual.Y, Near, $"{what} y" );
		Assert.AreEqual( expected.Z, actual.Z, Near, $"{what} z" );
	}

	[TestMethod]
	public void TheGridWavesPastTheStillPartByThePole()
	{
		var (points, normals) = ParkFlags.Grid( 7.3f );

		// Past 5.4 from the pole t is held at 1; at 3.6 it is two thirds.
		Close( new Vector3( 7.2f, -5.22398f, -0.21536f ), points[4, 2], "[4][2]" );
		Close( new Vector3( 0.49609f, 0.49609f, 0.71259f ), normals[4, 2], "[4][2] normal" );
		Close( new Vector3( 3.6f, -5.95804f, -0.19941f ), points[2, 3], "[2][3]" );
		Close( new Vector3( 0.05095f, 0.05095f, 0.99740f ), normals[2, 3], "[2][3] normal" );

		// The free corner droops 0.02x², 2.33 at 10.8.
		Close( new Vector3( 10.8f, -9.44841f, 0.29468f ), points[6, 4], "[6][4]" );
		Close( new Vector3( 1.8f, -0.00236f, -0.08114f ), points[1, 0], "[1][0]" );
	}

	[TestMethod]
	public void EveryFlagIsLaidAtItsPoleTurnedByTheWindFrontThenBack()
	{
		var corners = ParkFlags.Corners( 7.3f, 139f ).ToArray();
		Assert.AreEqual( 8 * 48 * 4, corners.Length, "eight flags of 48 faces" );

		// Face (flag, row, cell, side), corner: in this engine's axes the original's y and z swap; normals stay in its own.
		(int Flag, int Row, int Cell, int Side, int Corner, Vector3 Position, Vector3 Normal, Vector2 Uv)[] asked =
		[
			(3, 1, 2, 0, 0, new( 446.9748f, 54.8254f, 20.1490f ), new( 0.31030f, 0.34605f, 0.88542f ), new( 0.5f, 0.4125f )),
			(3, 1, 2, 1, 3, new( 446.6423f, 54.4423f, 17.5211f ), new( -0.84452f, 0.47221f, -0.25260f ), new( 0.5f, 0.275f )),
			(0, 0, 0, 1, 0, new( 410.8724f, 39.0777f, 22.0f ), new( -0.65549f, 0f, -0.75520f ), new( 0f, 1f )),
			(6, 3, 5, 0, 2, new( 542.5231f, 45.9345f, 12.5516f ), new( 0.77050f, -0.18129f, 0.61112f ), new( 1f, 0.45f )),
		];

		foreach ( var (flag, row, cell, side, corner, position, normal, uv) in asked )
		{
			var at = (((flag * 48) + (row * 12) + (cell * 2) + side) * 4) + corner;
			var what = $"flag {flag} row {row} cell {cell} side {side} corner {corner}";

			Close( position, corners[at].Position, what );
			Close( normal, corners[at].Normal, what + " normal" );
			Assert.AreEqual( uv.X, corners[at].Uv.X, 1e-5f, what + " u" );
			Assert.AreEqual( uv.Y, corners[at].Uv.Y, 1e-5f, what + " v" );
		}
	}

	[TestMethod]
	public void TheFlagsWaveEveryTwentyOfPhase()
	{
		var first = ParkFlags.Corners( 5f, 0f ).ToArray();
		var whole = ParkFlags.Corners( 25f, 0f ).ToArray();
		var half = ParkFlags.Corners( 15f, 0f ).ToArray();

		Assert.IsTrue( first.Zip( whole ).All( pair => (pair.First.Position - pair.Second.Position).Length < 0.01f ), "20 of phase is the whole wave" );
		Assert.IsTrue( first.Zip( half ).Any( pair => (pair.First.Position - pair.Second.Position).Length > 0.9f ), "10 of phase turns the up-and-down wave over" );
	}

	[TestMethod]
	public void TheFlagsWaveAtTheSeasPhaseAndStepTheirOwnOnlyWithNoSea()
	{
		Assert.AreEqual( 31.5f, ParkFlags.PhaseAfter( 31.5f, 2f, 0.5f, paused: false ), "the sea's phase" );
		Assert.AreEqual( 8f, ParkFlags.PhaseAfter( null, 2f, 0.5f, paused: false ), 0.0001f, "0.4 a frame at 30 a second" );
		Assert.AreEqual( 2f, ParkFlags.PhaseAfter( null, 2f, 0.5f, paused: true ), "held while paused" );
	}

	[TestMethod]
	public void AParkOpensWithItsFilesWindAndAChangeTurnsItUnlessHeld()
	{
		Assert.AreEqual( 139f, ParkWeather.StartingWind( 17f, 139f ) );
		Assert.AreEqual( 17f, ParkWeather.StartingWind( 17f, null ), "a fresh park keeps the roll" );

		Assert.AreEqual( 340f, ParkWeather.Turned( 10f, -30, held: null ), "wrapped below nought" );
		Assert.AreEqual( 5f, ParkWeather.Turned( 350f, 15, held: null ), "wrapped past 360" );
		Assert.AreEqual( 90f, ParkWeather.Turned( 350f, 15, held: 90f ), "held" );
	}

	/// <summary>
	/// The eased turn (Q643, a deviation: the original's flags jump). The asked winds straddle 0 and 360 both ways, so a
	/// turn the long way round lands near 185 rather than 5, a dropped wrap at 365, and the factors are neither 0, 1 nor
	/// a half of a half.
	/// </summary>
	[TestMethod]
	public void TheFlagsTurnToANewWindTheShorterWayRound()
	{
		Assert.AreEqual( 5f, ParkFlags.WindAfter( 350f, 20f, 0.5f, paused: false ), 0.0001f, "over 360, wrapped" );
		Assert.AreEqual( 12.5f, ParkFlags.WindAfter( 20f, 350f, 0.25f, paused: false ), 0.0001f, "back past nought" );
		Assert.AreEqual( 109f, ParkFlags.WindAfter( 100f, 145f, 0.2f, paused: false ), 0.0001f, "a fifth of the way" );
		Assert.AreEqual( 335f, ParkFlags.WindAfter( 10f, 300f, 0.5f, paused: false ), 0.0001f, "below nought, wrapped" );

		Assert.AreEqual( 139f, ParkFlags.WindAfter( null, 139f, 0.2f, paused: false ), "a park opens at its file's wind" );
		Assert.AreEqual( 100f, ParkFlags.WindAfter( 100f, 145f, 0.2f, paused: true ), "held while the game is paused" );
		Assert.AreEqual( 0f, ParkFlags.WindAfter( 100f, null, 0.2f, paused: false ), "no weather, no turn" );
	}

	[TestMethod]
	public void AFortyFiveDegreeChangeIsTenShortAfterASecond()
	{
		float? shown = 100f;
		var factor = 1f - System.MathF.Exp( -ParkFlags.TurnRate / 60f );
		for ( var frame = 0; frame < 60; ++frame )
			shown = ParkFlags.WindAfter( shown, 145f, factor, paused: false );

		Assert.AreEqual( 134.96f, shown!.Value, 0.01f, "45 x (1 - e^-1.5) after sixty frames of a sixtieth" );
	}

	[TestMethod]
	public void TheStockParkSavesItsWindAt139Degrees()
	{
		var data = GameData.Required();

		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		var park = new ParkWorld( new SaveReader( stream ).ReadFile() );

		Assert.AreEqual( 7, park.Weather, "mWeather names thing 7" );
		Assert.AreEqual( 139f, park.WindDirection );
	}
}
