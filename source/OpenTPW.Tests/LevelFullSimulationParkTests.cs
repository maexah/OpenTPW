using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>The level's actual mode decision must create a fresh world for Full Simulation.</summary>
[TestClass]
public class LevelFullSimulationParkTests
{
	[TestCleanup]
	public void PutTheRosterBack() => SetCurrentPlayer( null );

	[TestMethod]
	public void FullSimulationCreatesTwelveThingsWithRegularLoans()
	{
		FileSystem = GameData.Required();
		SetCurrentPlayer( new Player( 0, "Test", new PlayerFile { InstantAction = false } ) );
		var balance = new ParkBalance( "jungle", easyMode: Level.InstantAction );
		var catalogue = new ParkItemCatalogue( "jungle", instantAction: Level.InstantAction );
		var park = Level.CreatePark( "jungle", balance, catalogue )!;
		Assert.IsInstanceOfType( park, typeof( FreshPark ) );
		Assert.IsNull( park.Save );
		Assert.AreEqual( 12, park.ThingCount );
		CollectionAssert.AreEqual( Enumerable.Range( 1, 12 ).ToArray(), park.Things.Select( t => t.ThingId ).ToArray() );
		CollectionAssert.AreEqual( new[] { 9, 10, 11, 12, 13, 14, 15, 16, 17, 19, 3, 3 }, park.Things.Select( t => t.Model ).ToArray() );
		Assert.AreEqual( 11, park.ParkGates );
		Assert.AreEqual( 12, park.TrafficLights );
		Assert.AreEqual( 2, park.Objects.Count );
		Assert.IsTrue( park.Objects.All( o => !o.IsPlaced ) );
		Assert.AreEqual( 0, park.People.Count );
		Assert.AreEqual( 1, park.ParkClosed );
		Assert.AreEqual( 0, park.WorldState );
		Assert.AreEqual( 16384, park.Cells.Count );
		Assert.AreEqual( 10, park.Cells.Count( c => c.Type == 1 ) );
		Assert.AreEqual( 6991, park.Cells.Count( c => c.Type == 0 ) );
		// Oracle from all 16,384 cells of the copied original jungle restart.INTS, not from FreshPark.
		// Only random path art is normalized: base 10/2 and alternate 20/19 share geometry.
		var normalized = park.Cells.Select( c => c with { TileIndex = c.TileIndex == 20 ? 10 : c.TileIndex == 19 ? 2 : c.TileIndex } );
		var hash = System.Convert.ToHexString( System.Security.Cryptography.SHA256.HashData(
			System.Text.Json.JsonSerializer.SerializeToUtf8Bytes( normalized ) ) );
		Assert.AreEqual( "CE6D735C46A27C7FB16458974BEE6F1DED0CE55598161A2C0534091A8150D7B2", hash );
		var economy = park.Economy!.Value;
		Assert.AreEqual( 20, economy.AdmissionFee );
		Assert.AreEqual( 50000, economy.Balance );
		CollectionAssert.AreEqual( new[] { 20, 20, 20, 20, 23, 22, 18, 21 }, economy.Loans.Select( l => l.AprPercent ).ToArray() );
		CollectionAssert.AreEqual( new[] { 3651, 1825, 912, 365, 922, 1282, 2320, 2749 }, economy.Loans.Select( l => l.MonthlyRepayment ).ToArray() );
		CollectionAssert.AreEqual( new[] { 0, 0, 0, 1, 0, 0, 0, 0 }, economy.Loans.Select( l => l.Available ).ToArray() );
		var state = new ParkState( park );
		Assert.AreEqual( 13, state.NextThingId() );
		CollectionAssert.AreEqual( new[] { 12, 11 }, state.ObjectsInChainOrder().Select( o => o.ThingId ).ToArray() );
	}

	[TestMethod]
	public void InstantActionAndConsoleKeepTheShippedPark()
	{
		FileSystem = GameData.Required();
		foreach ( var player in new Player?[] { null, new( 0, "Test", new PlayerFile { InstantAction = true } ) } )
		{
			SetCurrentPlayer( player );
			Assert.IsTrue( Level.InstantAction );
			var park = Level.CreatePark( "jungle", new ParkBalance( "jungle", easyMode: Level.InstantAction ),
				new ParkItemCatalogue( "jungle", instantAction: Level.InstantAction ) )!;
			Assert.IsInstanceOfType( park, typeof( ParkWorld ) );
			Assert.IsTrue( park.Objects.Count > 2 );
			Assert.IsTrue( park.Economy!.Value.Loans.All( l => l.AprPercent == 0 ) );
		}
	}

	[TestMethod]
	public void CatalogueEmittersFillTheFirstUnnamedSlots()
	{
		FileSystem = GameData.Required();
		var library = new ParticleLibraryFile( "Particle/Tp2.plb" );
		var first = System.Array.FindIndex( library.Effects, effect => effect.Name.Length == 0 );
		Assert.AreEqual( 101, first );
		new ParkItemCatalogue( "jungle" ).RegisterParticleEffects( library );
		Assert.AreEqual( "Smoke", library.Effects[101].Name );
		Assert.AreEqual( "BeamUp", library.Effects[102].Name );
		Assert.AreEqual( "", library.Effects[103].Name );
	}

	[TestMethod]
	public void InitialBoundaryRequiresItsTerminatingPoint()
	{
		FileSystem = GameData.Required();
		var rows = string.Join( "\n", Enumerable.Range( 0, 64 ).Select( at =>
			$"HoardingClicks[{at}].X 0\nHoardingClicks[{at}].Y 0" ) );
		using var stream = new System.IO.MemoryStream( System.Text.Encoding.ASCII.GetBytes( rows ) );
		Assert.ThrowsException<System.IO.InvalidDataException>( () => FreshParkBoundary.Apply(
			new ParkWorld.MapCell[16384], new ParkBalance( "jungle" ), new SettingsFile( stream ) ) );
	}

	private static void SetCurrentPlayer( Player? player )
		=> typeof( Players ).GetProperty( nameof( Players.Current ) )!.SetValue( Players.Roster, player );
}
