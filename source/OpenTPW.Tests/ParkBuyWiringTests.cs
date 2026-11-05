using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenTPW.Tests;

/// <summary>
/// A purchase through <see cref="ParkBuilding.Build"/>, not through the pieces it calls (Q1029, from Q211): what the build does
/// for a thing once it stands is bind its script and, for a queued thing, close it (<see cref="ParkBuilding.BindOperation"/>;
/// <c>FUN_004db090</c>, <c>0x004db712</c>). <c>ParkRegionEffectsTests</c> and <c>ParkFileWriterBoughtAndSoldTests</c> call
/// <c>BindOperation</c> themselves, so taking it out of <c>Build</c> left every test green.
/// </summary>
[TestClass]
public class ParkBuyWiringTests
{
	private BaseFileSystem data = null!;
	private ParkWorld shipped = null!;
	private ParkItemCatalogue catalogue = null!;

	[TestInitialize]
	public void ReadTheShippedPark()
	{
		Log ??= new();
		data = GameData.Required();
		FileSystem = data;
		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		shipped = new ParkWorld( new SaveReader( stream ).ReadFile() );
		catalogue = new ParkItemCatalogue( "jungle", data );
		Unimplemented.Forget();
	}

	[TestCleanup]
	public void Forget()
	{
		ParkBuilding.StandThing = null;
		Unimplemented.Forget();
	}

	/// <summary>Buys the first item of the kind at the first cell that takes it, standing it with a stand-in for the model.</summary>
	private (ParkBuilding.Built Built, ParkState State, ParkRides Rides, ParkItemCatalogue.Item Item) BuyFirst( Func<ParkItemCatalogue.Item, bool> kind )
	{
		var state = new ParkState( shipped );
		var rides = new ParkRides( "jungle", shipped, catalogue, data );
		ParkBuilding.StandThing = _ => true;

		foreach ( var item in catalogue.All.Where( i => i.UiType is >= 0 and <= ItemDescriptionFile.Feature && kind( i ) ) )
		for ( var y = 20; y < 108; y += 3 )
		for ( var x = 20; x < 108; x += 3 )
		{
			if ( ParkBuilding.PlacementRefusal( state, item, x, y, 0 ) != null )
				continue;

			var built = ParkBuilding.Build( state, shipped, catalogue, null, rides, item.Id, x, y, 0 );

			if ( built.ThingId != 0 )
				return (built, state, rides, item);
		}

		Assert.Fail( "no cell took any item of the kind" );
		throw new InvalidOperationException();
	}

	/// <summary>A queued thing is bound to its script and starts closed (<c>mCanLoad</c> 0), as the constructor leaves it.</summary>
	[TestMethod]
	public void ABoughtQueuedThingIsBoundAndStartsClosed()
	{
		var (built, state, rides, item) = BuyFirst( i => i.HasQueue && !ParkTrackAddOn.Is( i ) );

		Assert.IsTrue( item.HasQueue );
		Assert.AreNotEqual( 0, rides.ScriptFor( built.ThingId ), $"'{item.Name}' has a script of its own bound" );
		Assert.IsTrue( state.TryObject( built.ThingId, out var thing ) );
		Assert.AreEqual( 0, thing.CanLoad, $"'{item.Name}' is shut until its first queue measure finds its back connected" );
	}

	/// <summary>A thing with no queue is bound too, and is open (<c>mCanLoad</c> 1, the constructor's write).</summary>
	[TestMethod]
	public void ABoughtThingWithNoQueueIsBoundAndStaysOpen()
	{
		var (built, state, rides, item) = BuyFirst( i => !i.HasQueue && !ParkTrackAddOn.Is( i ) && ParkRides.ScriptPathFor( i ) is { } path && data.OpenReadOrNull( path ) != null );

		Assert.IsFalse( item.HasQueue );
		Assert.AreNotEqual( 0, rides.ScriptFor( built.ThingId ), $"'{item.Name}' has a script of its own bound" );
		Assert.IsTrue( state.TryObject( built.ThingId, out var thing ) );
		Assert.AreEqual( 1, thing.CanLoad, $"'{item.Name}' stays open" );
	}
}
