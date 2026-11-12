using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// The coaster's read of the speed word (docs/QUEUE.md Q155): <c>FUN_0043b2b0( ride, speed )</c>, at <c>COAST 8</c> (<c>0x00554aab</c>) and at every
/// due visit (<c>0x005517e1</c>), acts only on a word other than the node's setting (<c>0x0043b2c6</c>), which a new node is made at: 60
/// (<c>FUN_004368f0</c>'s <c>FUN_0043c3d0( node, 0x3c )</c>; docs/exe/ride-operation.md, "A coaster's, a track ride's and an upgraded ride's
/// excitement"). What a change redoes, the cap and the track's run, is not built, so a change is counted (<c>COAST_RIDE_SPEED</c>) and a push of the
/// same word is not. The rule this replaced counted every push at a divisor other than 1, which is every push of a Lost Kingdom coaster
/// made at its <c>InitSpeed</c> of 60, and none at 50.
/// </summary>
[TestClass]
[DoNotParallelize]
public class CoasterSpeedPushTests
{
	private static int Times( string what ) => Unimplemented.Summary.FirstOrDefault( entry => entry.What == what ).Times;

	/// <summary>A push changes the setting only when the word differs from it, and says so; the made setting is 60, not the loader's 50.</summary>
	[TestMethod]
	public void APushChangesTheCoastersSettingOnlyWhenTheWordDiffers()
	{
		var ride = new RideState();

		Assert.AreEqual( 60, ride.Speed, "a node is made at 60" );
		Assert.IsFalse( ride.PushSpeed( 60 ), "60 over 60" );
		Assert.IsTrue( ride.PushSpeed( 50 ), "50 over 60" );
		Assert.AreEqual( 50, ride.Speed );
		Assert.IsFalse( ride.PushSpeed( 50 ), "50 over 50" );
		Assert.IsTrue( ride.PushSpeed( 60 ), "60 over 50" );
		Assert.AreEqual( 60, ride.Speed );
	}

	/// <summary>
	/// Lost Kingdom's three coasters name no speed of their own: each takes <c>Upgrades[0].InitSpeed</c> 60 from <c>rides/Rides.sam</c>, the word the
	/// object constructor pushes into its script, which is the node's made setting.
	/// </summary>
	[TestMethod]
	[DataRow( "coaster1" )]
	[DataRow( "coaster3" )]
	[DataRow( "minecart" )]
	public void ALostKingdomCoasterStartsAtTheNodesMadeSetting( string stem )
	{
		var data = GameData.Required();

		var category = new ItemDescriptionFile( data.OpenRead( "levels/jungle/rides/Rides.sam" ) );
		var item = new ItemDescriptionFile( data.OpenRead( $"levels/jungle/rides/{stem}/{stem}.sam" ), category );

		Assert.AreEqual( RideState.MadeSpeed, item.InitSpeed, stem );
	}

	/// <summary>
	/// A shipped Lost Kingdom coaster script, run by the scheduler from its first tick: at its <c>InitSpeed</c> of 60 its <c>COAST 8</c> and the due
	/// visits change nothing; a word the ride window sets (80, as Alexah's Minecart holds) counts once at the next due visit, and not at the visits after.
	/// </summary>
	[TestMethod]
	[DataRow( "coaster1/Coaster1.RSE" )]
	[DataRow( "coaster3/Coaster3.RSE" )]
	[DataRow( "minecart/Minecart.RSE" )]
	public void AShippedCoasterCountsOnlyTheWordThatChangesItsSetting( string path )
	{
		var data = GameData.Required();

		Unimplemented.Forget();

		var script = new RideScript( new RideScriptFile( data.OpenRead( $"levels/jungle/rides/{path}" ) ) )
		{
			Speed = 60,
			Ride = new RideState()
		};

		var scheduler = new RideScriptScheduler();

		scheduler.Add( 1, script );

		// Script id 1 is due on ticks 1, 9, 17, 25 and 33.
		for ( var tick = 1; tick <= 17; ++tick )
			scheduler.Advance( tick * 31f );

		Assert.IsTrue( script.Initialised, "the script ran its COAST 8" );
		Assert.AreEqual( 0, Times( "COAST_RIDE_SPEED" ), "60 over a node made at 60" );

		script.Speed = 80;

		for ( var tick = 18; tick <= 24; ++tick )
			scheduler.Advance( tick * 31f );

		Assert.AreEqual( 0, Times( "COAST_RIDE_SPEED" ), "no due visit since the word changed" );

		for ( var tick = 25; tick <= 40; ++tick )
			scheduler.Advance( tick * 31f );

		Assert.AreEqual( 1, Times( "COAST_RIDE_SPEED" ), "the visit on tick 25 changed it; 33's pushed the same word" );
		Assert.AreEqual( 80, script.Ride.Speed );

		Unimplemented.Forget();
	}
}
