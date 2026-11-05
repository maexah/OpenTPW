using System;
using System.IO;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenTPW.Tests;

/// <summary>
/// What a guest made by <see cref="ParkPeople.Admit"/> starts with (Q1029, from Q211): the prankery index from the guest's
/// id and the balance's likelihood, and the cash drawn from the type's starting cash (<c>FUN_004faec0</c>;
/// <c>docs/exe/guest-arrivals.md</c>). The shipped balance cannot tell either rule from a wrong one (every id is under
/// 65,536 and every starting cash is positive), so the balance is set and the id counter pushed over the line.
/// </summary>
[TestClass]
public class ParkArrivalStartTests
{
	[TestCleanup]
	public void LetThePeopleGo() => TestRun.DeleteEvery<ParkPeople>();

	private static ParkPeople People( ParkBalance balance )
	{
		FileSystem = GameData.Required();
		using var stream = new MemoryStream( FileSystem.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		var world = new ParkWorld( new SaveReader( stream ).ReadFile() );

		return new ParkPeople( world, balance, random: new Random( 1 ), banks: new ParkSpriteBanks( 6, 1, 4 ) );
	}

	private static void NextIdIs( ParkPeople people, int id )
	{
		ParkState.ForgetCurrent();
		typeof( ParkPeople ).GetField( "_nextThingId", BindingFlags.Instance | BindingFlags.NonPublic )!.SetValue( people, id );
	}

	/// <summary>A prankster's index is 100 plus the id's low sixteen bits mod 3: id 0x10001 is 101, where the whole id would make it 102.</summary>
	[TestMethod]
	public void ThePranksterIndexTakesTheIdsLowSixteenBits()
	{
		var balance = new ParkBalance( "jungle" );
		balance.Set( "PeepInfo.PrankeryLikelihood", "100" );
		var people = People( balance );

		NextIdIs( people, 0x10001 );
		var wrapped = people.Guests[people.Admit( 55, 30 )];
		Assert.AreEqual( 0x10001, wrapped.ThingId );
		Assert.AreEqual( 101, wrapped.PrankeryIndex );

		NextIdIs( people, 0x20002 );
		Assert.AreEqual( 100 + 2 % 3, people.Guests[people.Admit( 56, 30 )].PrankeryIndex );

		NextIdIs( people, 4 );
		Assert.AreEqual( 101, people.Guests[people.Admit( 57, 30 )].PrankeryIndex );
	}

	/// <summary>With a likelihood of nought nobody is a prankster, whatever the draw.</summary>
	[TestMethod]
	public void ALikelihoodOfNoughtMakesNoPranksters()
	{
		var balance = new ParkBalance( "jungle" );
		balance.Set( "PeepInfo.PrankeryLikelihood", "0" );
		var people = People( balance );

		for ( var n = 0; n < 8; ++n )
			Assert.AreEqual( 0, people.Guests[people.Admit( 55 + n, 30 )].PrankeryIndex );
	}

	/// <summary>A type with a negative starting cash (no shipped one has) is given no cash rather than debt: the draw's percentage of it is clamped at nought.</summary>
	[TestMethod]
	public void ANegativeStartingCashIsClampedAtNought()
	{
		var balance = new ParkBalance( "jungle" );
		balance.Set( "PeepTypes[2].StartingCash", "-1000" );
		var people = People( balance );

		var guest = people.Guests[people.Admit( 55, 30, personType: 2 )];

		Assert.AreEqual( 2, guest.PersonType );
		Assert.AreEqual( 0, guest.Cash );
	}

	/// <summary>The cash is the drawn percentage (100 +/- StartingCashVarPc) of the type's starting cash, whole, so a positive one is never clamped.</summary>
	[TestMethod]
	public void AGuestsCashIsAPercentageOfTheTypesStartingCash()
	{
		var balance = new ParkBalance( "jungle" );
		balance.Set( "PeepTypes[2].StartingCash", "1000" );
		balance.Set( "PeepInfo.StartingCashVarPc", "15" );
		var people = People( balance );

		for ( var n = 0; n < 6; ++n )
		{
			var cash = people.Guests[people.Admit( 55 + n, 30, personType: 2 )].Cash;
			Assert.IsTrue( cash is >= 850 and <= 1150 && cash % 10 == 0, $"cash {cash}" );
		}
	}
}
