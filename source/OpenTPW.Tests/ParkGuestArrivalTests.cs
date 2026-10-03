using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;

namespace OpenTPW.Tests;

/// <summary>Real arrivals and the decoded constructor's draw order, not a second implementation of its formulas.</summary>
[TestClass]
public class ParkGuestArrivalTests
{
	private BaseFileSystem data = null!;

	[TestInitialize]
	public void MountTheGame()
	{
		FileSystem = data = GameData.Required();
	}

	[TestCleanup]
	public void LetThePeopleGo()
	{
		TestRun.DeleteEvery<ParkPeople>();
		ParkState.ForgetCurrent();
	}

	private sealed class Draws( params int[] values ) : Random
	{
		public int Taken { get; private set; }
		public override int Next()
		{
			Assert.IsTrue( Taken < values.Length, "an unexpected constructor draw" );
			return values[Taken++];
		}
		public override int Next( int maxValue ) => throw new AssertFailedException( "use the raw draw before modulo" );
	}

	private ParkPeople Open( Draws draws )
	{
		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		var world = new ParkWorld( new SaveReader( stream ).ReadFile() );
		return new ParkPeople( world, new ParkBalance( "jungle", easyMode: true ),
			state: new ParkState( world ), random: draws, banks: new ParkSpriteBanks( 6, 3, 4 ) );
	}

	[TestMethod]
	public void TheRealConstructorInitializesEveryDecodedValueInDrawOrder()
	{
		// Speed, exit, kind, cash, thirst, hunger, toilet, discarded, prankery.
		var draws = new Draws( 4, 119, 3, 30, 49, 17, 29, 99, 4 );
		var people = Open( draws );
		var id = people.Admit( 42, 5 );
		var guest = people.Guests[id];

		Assert.AreEqual( 9, draws.Taken );
		Assert.AreEqual( Peep.BaseSpeeds[4], guest.BaseSpeed );
		Assert.AreEqual( 3, guest.PersonType );
		Assert.AreEqual( 179, guest.ExitLevel );
		Assert.AreEqual( 805, guest.Cash );
		Assert.AreEqual( 50f, guest.Happiness );
		Assert.AreEqual( 49f, guest.Thirst );
		Assert.AreEqual( 17f, guest.Hunger );
		Assert.AreEqual( 29f, guest.Toilet );
		Assert.AreEqual( 0f, guest.Vomit );
		Assert.AreEqual( 0f, guest.Litter );
		Assert.AreEqual( 100 + (id & 0xffff) % 3, guest.PrankeryIndex );
		Assert.AreEqual( new ParkSpriteBanks( 6, 3, 4 ).ChildOf( id ), guest.SpriteBank );
	}

	[TestMethod]
	public void LowerEndpointsAndPrankeryEqualityUseTheNextGuestsOwnDraws()
	{
		var draws = new Draws( 0, 0, 0, 0, 50, 100, 30, 0, 5,
			1, 1, 4, 15, 1, 2, 3, 99, 0 );
		var people = Open( draws );
		var first = people.Guests[people.Admit( 42, 5 )];
		var second = people.Guests[people.Admit( 42, 5 )];

		Assert.AreEqual( 18, draws.Taken );
		Assert.AreEqual( 60, first.ExitLevel );
		Assert.AreEqual( 255, first.Cash );
		Assert.AreEqual( 0f, first.Thirst );
		Assert.AreEqual( 0f, first.Hunger );
		Assert.AreEqual( 0f, first.Toilet );
		Assert.AreEqual( 0, first.PrankeryIndex, "5 is not below the shipped likelihood 5" );
		Assert.AreEqual( 50f, second.Happiness );
		Assert.AreEqual( 4, second.PersonType );
		Assert.AreEqual( 750, second.Cash );
		Assert.AreEqual( 61, second.ExitLevel );
		Assert.AreEqual( 100 + (second.ThingId & 0xffff) % 3, second.PrankeryIndex );
	}

	[TestMethod]
	public void TheExceptionalSignedReturnKeepsUnsignedNeedsAndSignedMoneyAndExit()
	{
		var draws = new Draws( int.MinValue, int.MinValue, int.MinValue, int.MinValue,
			int.MinValue, int.MinValue, int.MinValue, int.MinValue, int.MinValue );
		var people = Open( draws );
		var guest = people.Guests[people.Admit( 42, 5 )];

		Assert.AreEqual( 9, draws.Taken );
		Assert.AreEqual( Peep.BaseSpeeds[3], guest.BaseSpeed );
		Assert.AreEqual( 0, guest.PersonType );
		Assert.AreEqual( 52, guest.ExitLevel );
		Assert.AreEqual( 249, guest.Cash );
		Assert.AreEqual( 48f, guest.Thirst );
		Assert.AreEqual( 48f, guest.Hunger );
		Assert.AreEqual( 8f, guest.Toilet );
		Assert.AreEqual( 0, guest.PrankeryIndex );
	}

	[TestMethod]
	public void AForcedKindStillConsumesItsDrawAndCashTruncates()
	{
		var draws = new Draws( 0, 60, 7, 14, 2, 3, 4, 99, 5 );
		var people = Open( draws );
		var guest = people.Guests[people.Admit( 42, 5, personType: 4 )];

		Assert.AreEqual( 9, draws.Taken );
		Assert.AreEqual( 4, guest.PersonType );
		Assert.AreEqual( 742, guest.Cash, "99 percent of 750 truncates toward zero" );
		Assert.AreEqual( 2f, guest.Thirst );
		Assert.AreEqual( 3f, guest.Hunger );
		Assert.AreEqual( 4f, guest.Toilet );
	}
}
