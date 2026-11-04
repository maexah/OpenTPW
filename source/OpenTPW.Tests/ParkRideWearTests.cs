using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// A ride's wear by running and its breakdown request: <c>FUN_004df450</c>, <c>FUN_004df670</c> and the third step
/// of <c>FUN_004e0b90</c> - <c>docs/exe/ride-operation.md</c>, "A ride's wear".
///
/// <para>
/// The turns read real game files and are skipped where there is no installation - see <see cref="GameData"/>.
/// </para>
/// </summary>
[TestClass]
public class ParkRideWearTests
{
	private const string Theme = "jungle";

	private const int BellyBounce = 13;

	private const int Toilet = 21;

	/// <summary>The Belly Bounce's tier nought in Instant Action: <c>Easy_Bouncy.sam</c>'s rate over the rides' red line.</summary>
	private static readonly ParkRideWear.Tier Bounce = new( Rate: 3, RedLineSpeed: 60, RedLineCapacity: 5 );

	[TestMethod]
	[DataRow( 1, 1.515f )]
	[DataRow( 2, 1.65f )]
	[DataRow( 4, 1.92f )]
	[DataRow( 5, 2.055f )]
	[DataRow( 10, 2.73f )]
	[DataRow( 15, 2.73f )]
	[DataRow( -3, 1.38f )]
	public void TheBellyBouncesWearIsTheOriginals( int riders, float wear )
		=> Assert.AreEqual( wear, ParkRideWear.Of( speed: 60, riders, maxSpeed: 100, maxCapacity: 10, Bounce ), 0.00001f );

	[TestMethod]
	public void TheOriginalsOwnReadingsAreMet()
	{
		// Read in the running original, to its poller's two places: a Belly Bounce at 100 with two on, and a
		// Hot Pot (eight seats) with one.
		Assert.AreEqual( 98.35, Math.Round( ParkRideWear.Lowered( 100f, ParkRideWear.Of( 60, 2, 100, 10, Bounce ) ), 2 ) );
		Assert.AreEqual( 98.45,
			Math.Round( ParkRideWear.Lowered( 100f, ParkRideWear.Of( 60, 1, 100, 8, Bounce with { RedLineCapacity = 4 } ) ), 2 ) );
	}

	[TestMethod]
	public void ASpeedUnderItsRedLineWearsLess()
	{
		Assert.AreEqual( 1.5f * (0.55f + 0.19f), ParkRideWear.Of( 50, 1, 100, 10, Bounce ), 0.00001f, "under the line" );
		Assert.AreEqual( 1.5f * (0.775f + 0.19f), ParkRideWear.Of( 50, 1, 100, 10, Bounce with { RedLineSpeed = 50 } ),
			0.00001f, "on the line" );
	}

	[TestMethod]
	public void TheCapacitysRedLineIsHeldAgainstATenthOfTheRiders()
	{
		Assert.AreEqual( 1.5f * (0.82f + 0.55f), ParkRideWear.Of( 60, 5, 100, 10, Bounce ), 0.00001f, "five on, a line of five" );
		Assert.AreEqual( 1.5f * (0.82f + 0.775f), ParkRideWear.Of( 60, 5, 100, 10, Bounce with { RedLineCapacity = 0 } ),
			0.00001f, "a line of nought" );
		Assert.AreEqual( 1.5f * (0.82f + 1f), ParkRideWear.Of( 60, 10, 100, 10, Bounce with { RedLineCapacity = 1 } ),
			0.00001f, "ten on, a line of one" );
		Assert.AreEqual( 1.5f * (0.82f + 0.55f), ParkRideWear.Of( 60, 0, 100, 10, Bounce with { RedLineCapacity = 0 } ),
			0.00001f, "nobody on, a line of nought: on the line" );
	}

	[TestMethod]
	public void ALossIsHeldToNoughtAndAHundred()
	{
		Assert.AreEqual( 0f, ParkRideWear.Lowered( 1.5f, 2.0 ) );
		Assert.AreEqual( 100f, ParkRideWear.Lowered( 99f, -2.0 ) );
		Assert.AreEqual( 0f, ParkRideWear.Lowered( 50f, double.NaN ) );
		Assert.AreEqual( 0f, ParkRideWear.Lowered( 50f, ParkRideWear.Of( 60, 1, 0, 10, Bounce ) ), "a most of nought" );
	}

	[TestMethod]
	public void AnItemFileKeepsEachTiersWearRateAndRedLines()
	{
		var category = new ItemDescriptionFile( "Upgrades[0].WearRate 5\nUpgrades[1].WearRate 3\nUpgrades[2].RedLineSpeed 90\n" );
		var item = new ItemDescriptionFile(
			"Info.Id 1\nUpgrades[1].WearRate 2\nUpgrades[1].RedLineCapacity 7\nUpgrades[2].RedLineCapacity 9\nUpgrades[1].RedLineSpeed 75\n",
			category );

		Assert.AreEqual( (5, 2, 0), (item.WearRateAt( 0 ), item.WearRateAt( 1 ), item.WearRateAt( 2 )) );
		Assert.AreEqual( (0, 75, 90), (item.RedLineSpeedAt( 0 ), item.RedLineSpeedAt( 1 ), item.RedLineSpeedAt( 2 )) );
		Assert.AreEqual( (0, 7, 9), (item.RedLineCapacityAt( 0 ), item.RedLineCapacityAt( 1 ), item.RedLineCapacityAt( 2 )) );
	}

	private sealed record Park( ParkState State, ParkPeople People, Dictionary<int, RideScript> Scripts,
		ParkItemCatalogue Catalogue, ParkRideOperation Operation )
	{
		public RideScript Script => Scripts[BellyBounce];

		public ParkWorld.CatalogueObject Thing( int id = BellyBounce ) => State.Objects.Single( thing => thing.ThingId == id );

		public void Change( Func<ParkWorld.CatalogueObject, ParkWorld.CatalogueObject> how, int id = BellyBounce )
			=> Assert.IsTrue( State.ReplaceObject( how( Thing( id ) ) ) );

		public ParkItemCatalogue.Item Item( int id = BellyBounce )
		{
			Assert.IsTrue( Catalogue.TryGet( Thing( id ).CatalogueId, out var item ) );

			return item;
		}

		public bool Turn( int tick, int id = BellyBounce ) => Operation.WearAndAskToBreak( Scripts[id], Thing( id ), Item( id ), tick );
	}

	private static Park Open( bool instantAction = true )
	{
		var data = FileSystem = GameData.Required();

		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		var world = new ParkWorld( new SaveReader( stream ).ReadFile() );

		var state = new ParkState( world );
		var catalogue = new ParkItemCatalogue( Theme, data, instantAction );
		var scripts = state.Objects.Where( thing => thing.IsVisitable || thing.IsToilet ).ToDictionary( thing => thing.ThingId, thing =>
		{
			using var script = data.OpenRead( "levels/jungle/rides/bouncy/bouncy.RSE" );

			// The word a load puts back, which the file's records hold equal to the thing's operating speed (Q155).
			return new RideScript( new RideScriptFile( script ) ) { Speed = thing.OperatingSpeed > 0 ? thing.OperatingSpeed : 50 };
		} );

		var people = new ParkPeople( world, new ParkBalance( Theme, easyMode: true ), gateStatus: null, state,
			catalogue, id => scripts.GetValueOrDefault( id ) );

		return new( state, people, scripts, catalogue, new ParkRideOperation( state, new Dictionary<int, Peep>() ) );
	}

	private static void Close( Park park )
	{
		park.People.Delete();
		Entity.ApplyDeletions();
	}

	private static void In( Action<Park> test, bool instantAction = true )
	{
		var park = Open( instantAction );

		try
		{
			test( park );
		}
		finally
		{
			Close( park );
		}
	}

	private static int Times( string what ) => Unimplemented.Summary.FirstOrDefault( one => one.What == what ).Times;

	[TestMethod]
	public void TheCatalogueHoldsTheBellyBouncesTiersInEachMode()
	{
		In( park => CollectionAssert.AreEqual(
			new ParkRideWear.Tier[] { new( 3, 60, 5 ), new( 2, 75, 7 ), new( 1, 90, 9 ) },
			Enumerable.Range( 0, 3 ).Select( park.Item().WearAt ).ToArray(), "Instant Action" ) );

		In( park =>
		{
			CollectionAssert.AreEqual(
				new ParkRideWear.Tier[] { new( 5, 60, 5 ), new( 3, 75, 7 ), new( 2, 90, 9 ) },
				Enumerable.Range( 0, 3 ).Select( park.Item().WearAt ).ToArray(), "Full Simulation" );
			Assert.AreEqual( 0, park.Item( Toilet ).WearAt( 0 ).Rate, "a toilet never wears by running" );
		}, instantAction: false );
	}

	/// <summary>The wear takes the script's speed word (<c>FUN_0055a330</c>, <c>0x004df758</c>) and not the thing's operating speed: the word alone moved to 80, as the console's <c>scriptword</c> does.</summary>
	/// <remarks><b>Mutation:</b> the wear given the thing's operating speed, or the loader's 50.</remarks>
	[TestMethod]
	public void TheWearTakesTheScriptsWordAndNotTheThings() => In( park =>
	{
		var item = park.Item();

		Assert.IsTrue( park.Script.Set( ParkRideOperation.OnRideVariable, 5 ) );
		park.Script.Speed = 80;
		Assert.AreEqual( 60, park.Thing().OperatingSpeed, "the thing's own is not moved" );
		Assert.IsFalse( park.Turn( 64 ) );

		var wear = ParkRideWear.Of( 80, 5, item.MaxSpeed, item.MaxCapacity, item.WearAt( park.Thing().UpgradeLevel ) );

		Assert.AreEqual( 100f - wear, park.Thing().StateOfRepair, 0.0001f );
		Assert.AreNotEqual( 100f - 2.055f, park.Thing().StateOfRepair, 0.01f, "not the 60 the thing holds" );
	} );

	[TestMethod]
	public void ARiddenBounceWearsOnEverySixtyFourthSweep() => In( park =>
	{
		Assert.AreEqual( (100f, 100f), (park.Thing().StateOfRepair, park.Thing().RemainingLife), "the file's" );
		Assert.IsTrue( park.Script.Set( ParkRideOperation.OnRideVariable, 5 ) );
		Assert.AreEqual( 0, park.Script[ParkRideOperation.RunningVariable], "it runs all the time, whatever its script says" );

		foreach ( var tick in new[] { 8, 56, 63, 65, 72, 96 } )
		{
			Assert.IsFalse( park.Turn( tick ) );
			Assert.AreEqual( 100f, park.Thing().StateOfRepair, $"no wear on tick {tick}" );
		}

		Assert.IsFalse( park.Turn( 64 ) );
		Assert.AreEqual( 100f - 2.055f, park.Thing().StateOfRepair, 0.0001f, "five on" );
		Assert.AreEqual( 100f - 0.0411f, park.Thing().RemainingLife, 0.0001f, "a fiftieth of it off the life" );

		Assert.IsTrue( park.Script.Set( ParkRideOperation.OnRideVariable, 2 ) );
		park.Turn( 128 );
		Assert.AreEqual( 100f - 2.055f - 1.65f, park.Thing().StateOfRepair, 0.0001f, "then two on" );
		Assert.AreEqual( 0, park.Script[ParkRideOperation.WornVariable] );
		Assert.AreEqual( 0, park.Script[ParkRideOperation.BreakStatVariable] );
	} );

	[TestMethod]
	public void AnEmptyAShutABrokenOrAStoppedRideDoesNotWear() => In( park =>
	{
		park.Turn( 64 );
		Assert.AreEqual( 100f, park.Thing().StateOfRepair, "nobody on" );

		park.Script.Set( ParkRideOperation.OnRideVariable, 3 );

		park.Change( thing => thing with { CanLoad = 0 } );
		park.Turn( 64 );
		Assert.AreEqual( 100f, park.Thing().StateOfRepair, "its door shut" );

		park.Change( thing => thing with { CanLoad = 1, State = ParkRideChoice.StateRefusedOne } );
		park.Turn( 64 );
		Assert.AreEqual( 100f, park.Thing().StateOfRepair, "broken down" );

		park.Change( thing => thing with { State = 0, Flags = (ushort)(thing.Flags & ~ItemDescriptionFile.RunsContinuouslyFlag) } );
		park.Turn( 64 );
		Assert.AreEqual( 100f, park.Thing().StateOfRepair, "not running, and not one that always runs" );

		park.Script.Set( ParkRideOperation.RunningVariable, 1 );
		park.Turn( 64 );
		Assert.AreEqual( 100f - 1.785f, park.Thing().StateOfRepair, 0.0001f, "running, three on" );

		park.Change( thing => thing with { UpgradeLevel = 1 } );
		park.Turn( 128 );
		Assert.AreEqual( 100f - 1.785f - 1f * (0.64f + 0.37f), park.Thing().StateOfRepair, 0.0001f,
			"its second tier: a rate of 2, and 60 under the line of 75" );
	} );

	[TestMethod]
	public void FullSimulationsBounceWearsAtTheRidesOwnRate() => In( park =>
	{
		park.Script.Set( ParkRideOperation.OnRideVariable, 5 );
		park.Turn( 64 );
		Assert.AreEqual( 100f - 2.5f * 1.37f, park.Thing().StateOfRepair, 0.0001f );
	}, instantAction: false );

	[TestMethod]
	public void ARideUnderTwentyIsToldItIsWornAndUnderTenTheAdvisorIsToldOnce() => In( park =>
	{
		park.Script.Set( ParkRideOperation.OnRideVariable, 5 );

		park.Change( thing => thing with { StateOfRepair = 22.5f } );
		park.Turn( 64 );
		Assert.AreEqual( (20.445f, 0), (MathF.Round( park.Thing().StateOfRepair, 3 ), park.Script[ParkRideOperation.WornVariable]), "20 is not under 20" );

		park.Turn( 128 );
		Assert.AreEqual( (18.39f, 1), (MathF.Round( park.Thing().StateOfRepair, 3 ), park.Script[ParkRideOperation.WornVariable]) );

		var advisor = park.State.AdvisorMessages = new ParkAdvisorMessages( new SettingsFile( "Advisor/Advisor.sam" ) );

		park.Change( thing => thing with { StateOfRepair = 12.5f } );
		park.Turn( 192 );
		Assert.AreEqual( 0, advisor.PendingCount, "10 is not under 10" );

		park.Change( thing => thing with { StateOfRepair = 11.5f } );
		park.Turn( 256 );
		Assert.IsTrue( advisor.Census().Contains( $"pending=1 attempts=0 threshold=25 slot=66/subject={BellyBounce} " ),
			"9 is, and 9 and the wear is 10 or more: " + advisor.Census() );

		park.Turn( 320 );
		Assert.AreEqual( 1, advisor.PendingCount, "told once: 7 and the wear is under 10" );

		var said = new List<int>();

		var heard = new List<(int Group, int Subject)>();

		advisor.Tick( 321, 0, ( _, sample ) => { said.Add( sample ); return 0; }, ( group, subject ) => heard.Add( (group, subject) ) );
		CollectionAssert.AreEqual( new[] { 191 }, said );
		CollectionAssert.AreEqual( new[] { (68, (int)BellyBounce) }, heard, "the said line's event: its response's group and the ride" );

		park.Change( thing => thing with { StateOfRepair = 11.5f } );
		park.Turn( 384 );
		Assert.AreEqual( 0, advisor.PendingCount, "refused: said 63 sweeps before, under the group's 480" );

		park.Change( thing => thing with { StateOfRepair = 11.5f } );
		park.Turn( 832 );
		Assert.AreEqual( 1, advisor.PendingCount, "(832 >> 2) - (321 >> 2) is 128" );

		park.Change( thing => thing with { StateOfRepair = 7.39f } );
		Assert.AreEqual( 0, park.Script[ParkRideOperation.BreakStatVariable], "7 is not nought" );
	} );

	[TestMethod]
	public void ARideWornToNoughtAsksToBreakDownOnceAndLosesFiveOfItsLife() => In( park =>
	{
		park.Change( thing => thing with { StateOfRepair = 0.9f, RemainingLife = 98f } );

		Assert.IsFalse( park.Turn( 9 ), "only on a turn the clock's low three bits are nought" );
		Assert.IsTrue( park.Turn( 8 ) );
		Assert.AreEqual( (1, 93f), (park.Script[ParkRideOperation.BreakStatVariable], park.Thing().RemainingLife) );

		Assert.IsFalse( park.Turn( 16 ), "its script already told" );
		Assert.AreEqual( 93f, park.Thing().RemainingLife );
	} );

	[TestMethod]
	public void ARideWithNoLifeLeftAsksToBreakDownAndLosesNoMore() => In( park =>
	{
		park.Change( thing => thing with { RemainingLife = 0.5f } );

		Assert.IsTrue( park.Turn( 8 ) );
		Assert.AreEqual( (1, 0.5f, 100f),
			(park.Script[ParkRideOperation.BreakStatVariable], park.Thing().RemainingLife, park.Thing().StateOfRepair) );
	} );

	[TestMethod]
	public void ALifeUnderFiveIsHeldAtNought() => In( park =>
	{
		park.Change( thing => thing with { StateOfRepair = 0f, RemainingLife = 3f } );

		Assert.IsTrue( park.Turn( 8 ) );
		Assert.AreEqual( 0f, park.Thing().RemainingLife );
	} );

	[TestMethod]
	public void AToiletAShutRideAndABrokenOneAskForNoBreakdown() => In( park =>
	{
		park.Change( thing => thing with { StateOfRepair = 0f }, Toilet );
		Assert.IsFalse( park.Turn( 8, Toilet ), "a toilet" );
		Assert.AreEqual( 0, park.Scripts[Toilet][ParkRideOperation.BreakStatVariable] );

		park.Change( thing => thing with { StateOfRepair = 0f, CanLoad = 0 } );
		Assert.IsFalse( park.Turn( 8 ), "its door shut" );

		park.Change( thing => thing with { CanLoad = 1, State = ParkRideChoice.StateRefusedOne } );
		Assert.IsFalse( park.Turn( 8 ), "broken down" );
		Assert.AreEqual( (0, 100f), (park.Script[ParkRideOperation.BreakStatVariable], park.Thing().RemainingLife) );
	} );

	[TestMethod]
	public void TheRidesOwnTurnWearsItAndAsksItToBreak() => In( park =>
	{
		park.Script.Set( ParkRideOperation.OnRideVariable, 4 );
		park.People.TakeTheRidesTurns( thingTick: 64 );
		Assert.AreEqual( 100f - 1.92f, park.Thing().StateOfRepair, 0.0001f, "the turn wears it" );

		park.Change( thing => thing with { StateOfRepair = 0.5f } );
		park.People.TakeTheRidesTurns( thingTick: 72 );
		Assert.AreEqual( 1, park.Script[ParkRideOperation.BreakStatVariable], "and asks it to break" );
		Assert.AreEqual( 0.5f, park.Thing().StateOfRepair, "and the rest of the turn keeps the wear" );
		Assert.AreEqual( 100f - 0.0384f - 5f, park.Thing().RemainingLife, 0.0001f );
	} );

	[TestMethod]
	public void ARideWhoseLastLifeTheAskTakesIsCondemnedOnTheSameTurn() => In( park =>
	{
		park.Change( thing => thing with { StateOfRepair = 0f, RemainingLife = 3f } );
		Assert.IsTrue( park.Script.Set( ParkRideOperation.BrokenVariable, 1 ) );

		park.People.TakeTheRidesTurns( thingTick: 72 );

		Assert.AreEqual( (ParkRideChoice.StateRefusedFour, 0f), (park.Thing().State, park.Thing().RemainingLife),
			"the turn's second half reads the life the first half left" );
	} );
}
