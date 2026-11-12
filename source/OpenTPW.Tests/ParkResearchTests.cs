using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenTPW.UI;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// The researched flag the buy list lists by (<c>docs/exe/hud.md</c>, "What the buy list actually filters on"): the
/// save's record for an item it holds, <c>Upgrades[0].CostOfResearch</c> nought for one it lacks. The first tests build
/// a theme of their own on disk; the rest read Lost Kingdom and skip without the game.
/// </summary>
[TestClass]
public class ParkResearchTests
{
	private string _root = null!;

	[TestInitialize]
	public void Setup()
	{
		Log ??= new();
		_root = Path.Combine( Path.GetTempPath(), $"opentpw-research-{System.Guid.NewGuid():N}" );

		Write( "shops/Shops.sam", "Info.WhichUIType 1\n" );
		Write( "shops/free/free.sam", "Info.Id 1201\nInfo.Name \"Free\"\nUpgrades[0].CostOfResearch 0\n" );
		Write( "shops/dear/dear.sam", "Info.Id 1202\nInfo.Name \"Dear\"\nUpgrades[0].CostOfResearch 500\n" );
		Write( "shops/unsaved/unsaved.sam", "Info.Id 1203\nInfo.Name \"Unsaved\"\n" );
		Write( "shops/costly/costly.sam", "Info.Id 1204\nInfo.Name \"Costly\"\nUpgrades[0].CostOfResearch 300\n" );
	}

	[TestCleanup]
	public void Cleanup()
	{
		if ( Directory.Exists( _root ) )
			Directory.Delete( _root, recursive: true );
	}

	private void Write( string path, string text )
	{
		var full = Path.Combine( _root, "levels", "test", path );
		Directory.CreateDirectory( Path.GetDirectoryName( full )! );
		File.WriteAllText( full, text );
	}

	private ParkResearch Research( out ParkItemCatalogue catalogue )
	{
		catalogue = new ParkItemCatalogue( "test", new BaseFileSystem( _root ) );

		// The save says no to a free item and yes to a dear one; it holds nothing for the other two.
		return new ParkResearch(
		[
			new ParkWorld.ObjectControl( 1201, Researched: false, TierResearched: 0 ),
			new ParkWorld.ObjectControl( 1202, Researched: true, TierResearched: 2 ),
		], catalogue );
	}

	[TestMethod]
	public void TheSavesFlagWinsOverTheFile()
	{
		var research = Research( out _ );

		Assert.IsFalse( research.IsResearched( 1201 ), "the save holds it unresearched, though its file costs nothing" );
		Assert.IsTrue( research.IsResearched( 1202 ), "the save holds it researched, though its file costs 500" );
		Assert.AreEqual( 2, research.TierResearched( 1202 ) );
	}

	[TestMethod]
	public void AnItemTheSaveLacksIsSeededFromItsCost()
	{
		var research = Research( out _ );

		Assert.IsTrue( research.IsResearched( 1203 ), "no cost set reads nought: researched" );
		Assert.IsFalse( research.IsResearched( 1204 ), "a cost of 300: not researched" );
		Assert.AreEqual( 2, research.FromSave );
		Assert.AreEqual( 2, research.Seeded );
	}

	[TestMethod]
	public void TheBuyListListsOnlyResearchedItems()
	{
		var research = Research( out var catalogue );

		CollectionAssert.AreEqual( new[] { "Dear", "Unsaved" },
			ParkBuyScreen.Listed( catalogue, research, 1 ).Select( item => item.Name ).Order().ToArray() );
	}

	[TestMethod]
	public void AGoldenTicketRideIsAMysteryUntilThePlayerUnlocksIt()
	{
		var ride = new ParkItemCatalogue.Item( 1105, "Ride", "", "ride", 1, 1, null, GoldenTicketCost: 5 );

		Assert.IsTrue( GoldenTickets.IsMystery( ride, null ), "nobody playing: nothing unlocked" );
		Assert.IsTrue( GoldenTickets.IsMystery( ride, new ushort[] { 1106 } ) );
		Assert.IsFalse( GoldenTickets.IsMystery( ride, new ushort[] { 1105 } ), "unlocked with tickets" );
		Assert.IsFalse( GoldenTickets.IsMystery( ride with { GoldenTicketCost = 0 }, null ), "no ticket cost" );
	}

	private static ParkWorld ShippedPark( BaseFileSystem data )
	{
		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		return new ParkWorld( new SaveReader( stream ).ReadFile() );
	}

	[TestMethod]
	public void TheShippedParkHoldsFiftyRecordsTwentySixResearched()
	{
		var records = ShippedPark( GameData.Required() ).ObjectControlRecords;

		Assert.AreEqual( 50, records.Count );
		Assert.AreEqual( 26, records.Count( record => record.Researched ) );
		Assert.AreEqual( 17, records.Count( record => record.TierResearched == 2 ) );
		Assert.IsTrue( records.All( record => record.TierResearched == 0 || record.Researched ), "a tier only where researched" );
	}

	/// <summary>
	/// The control: in the shipped park the save's flag and the file's cost agree item for item, so
	/// this passes whichever rule is read; the tab tests below pass only if the filter is applied at all.
	/// </summary>
	[TestMethod]
	public void InTheShippedParkTheSaveAndTheFilesAgree()
	{
		var data = GameData.Required();
		var catalogue = new ParkItemCatalogue( "jungle", data, instantAction: true );
		var research = new ParkResearch( ShippedPark( data ).ObjectControlRecords, catalogue );

		Assert.AreEqual( 50, research.FromSave );
		Assert.AreEqual( 0, research.Seeded, "the save holds every item Instant Action catalogues" );

		foreach ( var item in catalogue.All )
			Assert.AreEqual( item.ResearchCost == 0, research.IsResearched( item.Id ), item.Name );
	}

	/// <summary>
	/// A fresh world's lab is filled from the catalogue (<c>FUN_005031a0</c>, <c>FUN_00504580</c>;
	/// <c>docs/exe/park-engine.md</c>, "A fresh world's research lab"). The projects and the control records pinned
	/// here are those of a jungle <c>restart.INTS</c> the original wrote for a new Full Simulation park, on tick
	/// nought, in the file's order.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> an item no buy list shows given a project; the catalogue's order lost; an item's own
	/// project made of kind 4, or with no group; an upgrade's project given the item's own kind, its group or
	/// level nought; the second tier's cost taken for the first's; an item with no upgrade left at tier 0 or 1;
	/// a researched ride with an upgrade left at tier 2; the fresh park handed no project, or tier nought.
	/// </remarks>
	[TestMethod]
	public void AFreshLostKingdomWorldsLabIsTheOriginalsFortyEightProjects()
	{
		var data = GameData.Required();
		FileSystem = data;
		var catalogue = new ParkItemCatalogue( "jungle", data );
		var park = new FreshPark( "jungle", new ParkBalance( "jungle", easyMode: false ), catalogue, data );

		(int Item, int Kind, int Group, int Level, int Cost)[] original =
		[
			(1100, 4, 0, 1, 250), (1140, 0, 3, 0, 700), (1185, 0, 2, 0, 1050), (1190, 0, 4, 0, 1300), (1150, 0, 1, 0, 600), (1106, 0, 1, 0, 550),
			(1108, 0, 2, 0, 500), (1180, 0, 1, 0, 800), (1101, 4, 0, 1, 200), (1109, 0, 2, 0, 700), (1102, 4, 0, 1, 150), (1113, 0, 2, 0, 400),
			(1110, 0, 4, 0, 700), (1170, 4, 0, 1, 150), (1104, 4, 0, 1, 150), (1112, 4, 0, 1, 250), (1160, 0, 3, 0, 800), (1414, 3, 1, 0, 250),
			(1429, 3, 1, 0, 300), (1424, 3, 0, 0, 300), (1423, 3, 1, 0, 300), (1422, 3, 2, 0, 300), (1427, 3, 1, 0, 300), (1426, 3, 1, 0, 300),
			(1400, 3, 1, 0, 350), (1418, 3, 1, 0, 300), (1421, 3, 2, 0, 400), (1430, 3, 2, 0, 300), (1405, 3, 1, 0, 450), (1431, 3, 2, 0, 450),
			(1417, 3, 1, 0, 300), (1433, 3, 1, 0, 300), (1434, 3, 1, 0, 300), (1435, 3, 1, 0, 300), (1407, 3, 1, 0, 350), (1420, 3, 2, 0, 400),
			(1419, 3, 3, 0, 600), (1500, 4, 0, 0, 1000), (1501, 4, 0, 0, 1250), (1502, 4, 0, 0, 1200), (1202, 1, 3, 0, 550), (1212, 1, 1, 0, 450),
			(1211, 1, 2, 0, 550), (1206, 1, 2, 0, 500), (1208, 1, 3, 0, 650), (1309, 2, 2, 0, 700), (1304, 2, 1, 0, 700), (1307, 2, 3, 0, 800),
		];

		CollectionAssert.AreEqual( original.Select( row => new ParkWorld.ResearchProject( row.Item, row.Kind, row.Group, row.Level, row.Cost, 0f ) ).ToArray(),
			park.ResearchProjects.ToArray() );
		CollectionAssert.AreEqual( new[] { 11, 5, 3, 20, 9 }, Enumerable.Range( 0, 5 ).Select( kind => park.ResearchProjects.Count( project => project.Category == kind ) ).ToArray() );

		(int Item, bool Researched, int Tier)[] controls =
		[
			(1100, true, 0), (1140, false, 0), (1185, false, 0), (1190, false, 0), (1150, false, 0), (1106, false, 0), (1108, false, 0),
			(1180, false, 0), (1101, true, 0), (1109, false, 0), (100, true, 0), (1102, true, 0), (1113, false, 0), (1110, false, 0),
			(1170, true, 0), (1104, true, 0), (1112, true, 0), (1160, false, 0), (1414, false, 0), (1428, true, 2), (1429, false, 0),
			(1424, false, 0), (1423, false, 0), (1422, false, 0), (1427, false, 0), (1426, false, 0), (1400, false, 0), (1600, true, 0),
			(1401, true, 2), (1418, false, 0), (1413, true, 2), (1605, true, 0), (1604, true, 0), (1403, true, 2), (1601, true, 0),
			(101, true, 0), (102, true, 0), (1421, false, 0), (1603, true, 0), (1430, false, 0), (1405, false, 0), (1406, true, 2),
			(1602, true, 0), (1431, false, 0), (1417, false, 0), (1433, false, 0), (1434, false, 0), (1435, false, 0), (1411, true, 2),
			(1407, false, 0), (1420, false, 0), (1419, false, 0), (1425, true, 2), (1402, true, 2), (1500, false, 0), (1501, false, 0),
			(1502, false, 0), (1209, true, 2), (1207, true, 2), (1203, true, 2), (1202, false, 0), (1212, false, 0), (1211, false, 0),
			(1206, false, 0), (1208, false, 0), (1309, false, 0), (1304, false, 0), (1303, true, 2), (1307, false, 0), (1305, true, 2),
		];

		CollectionAssert.AreEqual( controls, park.ObjectControlRecords.Select( control => (control.ItemId, control.Researched, control.TierResearched) ).ToArray() );
		Assert.AreEqual( 13, park.ObjectControlRecords.Count( control => control.TierResearched == 2 ) );

		var research = new ParkResearch( park.ObjectControlRecords, catalogue, park.ResearchProjects );
		CollectionAssert.AreEqual( new[] { 11, 5, 3, 20, 9 }, Enumerable.Range( 0, 5 ).Select( research.Unfinished ).ToArray() );
		Assert.AreEqual( 2, research.TierResearched( 1428 ) );
		Assert.AreEqual( 0, research.TierResearched( 1100 ) );
	}

	/// <summary>
	/// The fill's rule an item at a time (<c>FUN_005031a0</c>, <c>FUN_00504580</c>): an item whose research costs
	/// anything gives its own project; one that costs nothing gives the first later tier that costs anything, as a
	/// project of kind 4, and is left at the tier before it; a tier costing nothing is passed over.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> the tier test made "not nought" (a negative cost then makes a project); the second tier
	/// never looked at; the tier left one too high or too low; an item of kind 4 with no cost given an upgrade or
	/// a tier; an item of <c>WhichUIType</c> 4 let in, or one of 3 kept out; a cost of nought taken as a project; an
	/// item's own cost below nought given none; a key never taken from the folder's file.
	/// </remarks>
	[TestMethod]
	public void AFreshLabGivesEachItemItsOwnProjectOrItsFirstUpgradeThatCostsAnything()
	{
		ParkItemCatalogue.Item Item( int id, int uiType, int cost, int kind = 0, int group = 0, int first = 0, int second = 0 )
			=> new ParkItemCatalogue.Item( id, $"Item {id}", "", "", 1, 1, null, uiType ) with
			{
				ResearchCost = cost, ResearchCategory = kind, ResearchGroup = group, UpgradeResearchCost1 = first, UpgradeResearchCost2 = second
			};

		var (projects, tiers) = ParkResearch.FreshLab(
		[
			Item( 1, 0, 700, kind: 0, group: 3, first: 100, second: 200 ),
			Item( 2, 0, 0, first: 250, second: 300 ),
			Item( 3, 0, 0, first: 0, second: 300 ),
			Item( 4, 0, 0, first: -5, second: 0 ),
			Item( 5, 4, 900, kind: 2 ),
			Item( 6, 3, 0, kind: 4, first: 80 ),
			Item( 7, 3, 1000, kind: 4 ),
			Item( 8, 3, 0, kind: 3 ),
			Item( 9, 3, -5, kind: 3, group: 1 ),
		] );

		CollectionAssert.AreEqual( new ParkWorld.ResearchProject[]
		{
			new( 1, 0, 3, 0, 700f, 0f ), new( 2, 4, 0, 1, 250f, 0f ), new( 3, 4, 0, 2, 300f, 0f ), new( 7, 4, 0, 0, 1000f, 0f ),
			new( 9, 3, 1, 0, -5f, 0f ),
		}, projects.ToArray() );
		CollectionAssert.AreEquivalent( new[] { (2, 0), (3, 1), (4, 2), (8, 2) }, tiers.Select( pair => (pair.Key, pair.Value) ).ToArray() );

		// An item's file says only what differs from its folder's: each of the lab's keys falls back to it.
		var folder = new ItemDescriptionFile( "Research.Category 2\nResearch.Group 3\nUpgrades[1].CostOfResearch 70\nUpgrades[2].CostOfResearch 90\n" );
		var inherits = new ItemDescriptionFile( "Info.Id 1\n", folder );
		var restates = new ItemDescriptionFile( "Info.Id 2\nResearch.Category 1\nResearch.Group 4\nUpgrades[1].CostOfResearch 0\nUpgrades[2].CostOfResearch 5\n", folder );
		Assert.AreEqual( (2, 3, 70, 90), (inherits.ResearchCategory, inherits.ResearchGroup, inherits.UpgradeResearchCostAt( 1 ), inherits.UpgradeResearchCostAt( 2 )) );
		Assert.AreEqual( (1, 4, 0, 5), (restates.ResearchCategory, restates.ResearchGroup, restates.UpgradeResearchCostAt( 1 ), restates.UpgradeResearchCostAt( 2 )) );
	}

	/// <summary>
	/// The lab's projects are read from the park file (<c>FUN_00502dc0</c>, <c>FUN_00504210</c>): the shipped
	/// Lost Kingdom park holds twenty-four, eleven of a feature's kind, and <see cref="ParkResearch.Unfinished"/>
	/// counts those of a kind whose points are under their cost (<c>FUN_00503b30</c>).
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> the count read from another offset, or all 256 taken; a project's fields each read from
	/// its neighbour's place; a project of any kind counted; one with its points at or over its cost counted; the
	/// cost set against nought.
	/// </remarks>
	[TestMethod]
	public void TheShippedParksLabHoldsTwentyFourProjectsElevenOfThemFeatures()
	{
		var data = GameData.Required();
		var park = ShippedPark( data );
		var projects = park.ResearchProjects;

		Assert.AreEqual( 24, projects.Count );
		Assert.AreEqual( new ParkWorld.ResearchProject( 1140, 0, 3, 0, 700f, 0f ), projects[0] );
		Assert.AreEqual( new ParkWorld.ResearchProject( 1150, 0, 1, 0, 600f, 30f ), projects[1] );
		Assert.AreEqual( new ParkWorld.ResearchProject( 1414, 3, 1, 0, 250f, 7.5f ), projects[8] );
		Assert.AreEqual( new ParkWorld.ResearchProject( 1419, 3, 3, 0, 600f, 0f ), projects[18] );
		Assert.AreEqual( new ParkWorld.ResearchProject( 1307, 2, 3, 0, 800f, 0f ), projects[23] );
		CollectionAssert.AreEqual( new[] { 8, 3, 2, 11 }, Enumerable.Range( 0, 4 ).Select( kind => projects.Count( project => project.Category == kind ) ).ToArray() );
		Assert.IsTrue( projects.All( project => project.Done < project.Cost && project.UpgradeLevel == 0 ) );

		var catalogue = new ParkItemCatalogue( "jungle", data, instantAction: true );
		var research = new ParkResearch( park.ObjectControlRecords, catalogue, projects );
		CollectionAssert.AreEqual( new[] { 8, 3, 2, 11, 0 }, Enumerable.Range( 0, 5 ).Select( research.Unfinished ).ToArray() );
		Assert.AreEqual( 0, new ParkResearch( park.ObjectControlRecords, catalogue ).Unfinished( ItemDescriptionFile.Feature ), "with no projects handed in" );

		// A project is unfinished while its points are under its cost, and is its own kind's alone.
		ParkWorld.ResearchProject Feature( float cost, float done ) => new( 1414, ItemDescriptionFile.Feature, 1, 0, cost, done );
		int Left( params ParkWorld.ResearchProject[] lab ) => new ParkResearch( [], catalogue, lab ).Unfinished( ItemDescriptionFile.Feature );
		Assert.AreEqual( 1, Left( Feature( 250f, 249.5f ) ) );
		Assert.AreEqual( 0, Left( Feature( 250f, 250f ) ), "done at its cost" );
		Assert.AreEqual( 0, Left( Feature( 250f, 300f ) ) );
		Assert.AreEqual( 1, Left( Feature( 0.5f, 0f ) ), "a cost under one point" );
		Assert.AreEqual( 0, Left( Feature( 0f, 0f ) ), "nought is not under nought" );
		Assert.AreEqual( 2, Left( Feature( 250f, 0f ), Feature( 250f, 250f ), Feature( 300f, 7.5f ), new( 1304, ItemDescriptionFile.SideShow, 1, 0, 700f, 9f ), new( 1140, ItemDescriptionFile.Ride, 3, 0, 700f, 0f ) ) );
	}

	/// <summary>
	/// The lab's five kinds' efforts and outputs are the park file's, and a fresh world's are the original's own
	/// (<c>FUN_00503330</c>; its fresh Lost Kingdom park's file holds 100, 15, 30, 15, 10 and 49, 6, 14, 6, 4).
	/// <see cref="ParkResearch.Rate"/> is how much of the lab's work a kind gets from the researchers on the staff
	/// (<c>FUN_00503980</c>): the output times their share, in whole per cent, of the most a park's could be.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> a kind's record read from its neighbour's place, or as twelve or sixteen bytes; the output
	/// read where the effort is; an effort not held to 100, or held to 99; the share of the efforts not taken in
	/// whole per cent before the work load; the work load not divided by a hundred; the rate's share not a whole
	/// number's, or rounded up; the rate of another kind; no most answering anything but nought.
	/// </remarks>
	[TestMethod]
	public void TheLabsKindsOutputsAreTheFilesAndAFreshWorldsAndAKindsRateIsTheStaffsShareOfIt()
	{
		var data = GameData.Required();
		FileSystem = data;
		var shipped = ShippedPark( data );
		CollectionAssert.AreEqual( new ParkWorld.ResearchKind[] { new( 100, 49, 1 ), new( 15, 6, 20 ), new( 30, 14, 22 ), new( 25, 11, 8 ), new( 0, 0 ) }, shipped.ResearchKinds.ToArray() );

		var catalogue = new ParkItemCatalogue( "jungle", data );
		var fresh = new FreshPark( "jungle", new ParkBalance( "jungle", easyMode: false ), catalogue, data );
		CollectionAssert.AreEqual( new ParkWorld.ResearchKind[] { new( 100, 49, 4 ), new( 15, 6, 41 ), new( 30, 14, 46 ), new( 15, 6, 17 ), new( 10, 4, 0 ) }, fresh.ResearchKinds.ToArray() );
		StringAssert.EndsWith( fresh.Census(), "the kinds' efforts [100,15,30,15,10] and outputs [49,6,14,6,4]" );

		// The shipped park's efforts make the shipped park's outputs, so the rule is the file's own.
		CollectionAssert.AreEqual( shipped.ResearchKinds.Select( kind => (kind.Effort, kind.Output) ).ToArray(),
			ParkResearch.FreshKinds( [100, 15, 30, 25, 0], 85 ).Select( kind => (kind.Effort, kind.Output) ).ToArray() );
		CollectionAssert.AreEqual( new ParkWorld.ResearchKind[] { new( 100, 28 ), new( 100, 28 ), new( 100, 28 ) }, ParkResearch.FreshKinds( [100, 250, -1], 85 ).Select( kind => kind with { CurrentProject = -1, HighestGroup = 1 } ).ToArray(), "an effort is held to 100, a negative one too: the compare is unsigned" );
		CollectionAssert.AreEqual( new ParkWorld.ResearchKind[] { new( 99, 49 ), new( 100, 50 ) }, ParkResearch.FreshKinds( [99, 100], 100 ).Select( kind => kind with { CurrentProject = -1, HighestGroup = 1 } ).ToArray() );
		CollectionAssert.AreEqual( new ParkWorld.ResearchKind[] { new( 0, 0 ), new( 0, 0 ) }, ParkResearch.FreshKinds( [0, 0], 85 ).Select( kind => kind with { CurrentProject = -1, HighestGroup = 1 } ).ToArray(), "no effort at all" );
		CollectionAssert.AreEqual( new ParkWorld.ResearchKind[] { new( 1, 28 ), new( 2, 56 ) }, ParkResearch.FreshKinds( [1, 2], 85 ).Select( kind => kind with { CurrentProject = -1, HighestGroup = 1 } ).ToArray(), "33 and 66 per cent of 85, each a whole number's" );

		var research = new ParkResearch( shipped.ObjectControlRecords, catalogue, shipped.ResearchProjects, shipped.ResearchKinds );
		// Instant Action's Lost Kingdom: six researchers of the top grade's 20 are the most, 120.
		Assert.AreEqual( 1, research.Rate( ItemDescriptionFile.SideShow, 12, 120 ), "the shipped park's one researcher of grade 2: 14 x 10 / 100" );
		Assert.AreEqual( 0, research.Rate( ItemDescriptionFile.SideShow, 9, 120 ), "of grade 1: 14 x 7 / 100" );
		Assert.AreEqual( 0, research.Rate( ItemDescriptionFile.SideShow, 6, 120 ) );
		Assert.AreEqual( 0, research.Rate( ItemDescriptionFile.SideShow, 0, 120 ), "no researcher" );
		Assert.AreEqual( 1, research.Rate( ItemDescriptionFile.SideShow, 10, 120 ), "8.33 per cent is 8, and 14 x 8 is over a hundred" );
		Assert.AreEqual( 14, research.Rate( ItemDescriptionFile.SideShow, 120, 120 ) );
		Assert.AreEqual( 15, research.Rate( ItemDescriptionFile.SideShow, 131, 120 ), "more than the most is not held" );
		Assert.AreEqual( 4, research.Rate( ItemDescriptionFile.Ride, 12, 120 ), "49 x 10 / 100" );
		Assert.AreEqual( 0, research.Rate( 4, 120, 120 ), "a kind given no effort" );
		Assert.AreEqual( 0, research.Rate( ItemDescriptionFile.SideShow, 12, 0 ), "no most to set them against" );
		Assert.AreEqual( 0, research.Rate( 5, 12, 120 ) );
		Assert.AreEqual( 0, new ParkResearch( shipped.ObjectControlRecords, catalogue, shipped.ResearchProjects ).Rate( ItemDescriptionFile.SideShow, 120, 120 ), "with no kinds handed in" );
	}

	[TestMethod]
	public void LostKingdomsTabsListWhatTheOriginalShowed()
	{
		var data = GameData.Required();
		var catalogue = new ParkItemCatalogue( "jungle", data, instantAction: true );
		var research = new ParkResearch( ShippedPark( data ).ObjectControlRecords, catalogue );

		// Which items, in name order: the order they stand in is the list's sorted insert (ParkBuyGoldenTicketTests).
		string[] Tab( int tab ) => ParkBuyScreen.Listed( catalogue, research, tab ).Select( item => item.Name ).Order().ToArray();

		// q178b/orig/s06-s09.png, the original under Proton.
		CollectionAssert.AreEqual( new[] { "Aztec Mayhem", "Belly Bounce", "Crazy Ape", "Rocky Racers" }, Tab( 0 ) );
		CollectionAssert.AreEqual( new[] { "Balloon Shop", "Burger Shop", "Drinks Shop" }, Tab( 1 ) );
		CollectionAssert.AreEqual( new[] { "Jungle Spray", "Strength Bird" }, Tab( 2 ) );
	}

	/// <summary>
	/// <b>The buy list leaves out a ride's upgrade</b> (<c>AddOn.UpgradesId</c>, <c>+0x284</c>, not nought; the skip at
	/// <c>0x004aaffc</c>), however researched: with every Lost Kingdom item marked researched, as a Full Simulation park
	/// with 1500 and 1501 finished holds them, the rides tab lists DinoKarts but not the Dino Karts Jump (750) or
	/// Tunnel (900), and no tab lists any add-on.
	/// </summary>
	/// <remarks><b>Mutations:</b> the <c>+0x284</c> test dropped (the Jump and Tunnel listed).</remarks>
	[TestMethod]
	public void TheBuyListLeavesOutAResearchedRideUpgrade()
	{
		var data = GameData.Required();
		var catalogue = new ParkItemCatalogue( "jungle", data );
		var research = new ParkResearch( catalogue.All.Select( item => new ParkWorld.ObjectControl( item.Id, Researched: true, TierResearched: 2 ) ), catalogue );

		var addOns = catalogue.All.Where( item => item.AddOnUpgradesId != 0 ).ToArray();
		CollectionAssert.IsSubsetOf( new[] { (1500, 750), (1501, 900) }, addOns.Select( item => (item.Id, item.BuildPrice) ).ToArray(),
			"the Jump and the Tunnel are the add-ons this proves on, researched" );
		Assert.IsTrue( addOns.All( item => research.IsResearched( item.Id ) && item.UiType == ItemDescriptionFile.Ride ),
			"every add-on researched and on the rides tab, so only the +0x284 test keeps it off" );

		var rides = ParkBuyScreen.Listed( catalogue, research, ItemDescriptionFile.Ride ).Select( item => item.Id ).ToArray();
		CollectionAssert.DoesNotContain( rides, 1500 );
		CollectionAssert.DoesNotContain( rides, 1501 );
		Assert.IsTrue( rides.Any( id => catalogue.TryGet( id, out var item ) && ParkObjectNames.NameOf( item ) == "DinoKarts" ), "the ride they fit is listed" );

		for ( var tab = 0; tab < 4; ++tab )
			Assert.IsFalse( ParkBuyScreen.Listed( catalogue, research, tab ).Any( item => item.AddOnUpgradesId != 0 ), $"tab {tab}" );
	}

	/// <summary>
	/// <b>An item's name is its two <c>OBJECT_NAMES</c> lines joined</b> (<c>0x004146b0</c>): a space between, none
	/// where the second is empty, and a first line's closing hyphen cut with the second joined straight on.
	/// </summary>
	/// <remarks><b>Mutations:</b> a space always; the hyphen kept.</remarks>
	[TestMethod]
	public void AnItemsNameIsItsTwoLinesJoined()
	{
		Assert.AreEqual( "Crazy Ape", ParkObjectNames.Joined( "Crazy", "Ape" ) );
		Assert.AreEqual( "DinoKarts", ParkObjectNames.Joined( "DinoKarts", "" ) );
		Assert.AreEqual( "Overpath", ParkObjectNames.Joined( "Over-", "path" ) );
		Assert.AreEqual( " Ape", ParkObjectNames.Joined( "", "Ape" ), "%s%s%s with the space, as the original joins it" );
	}

	/// <summary>
	/// <b>The park's lists letter the <c>OBJECT_NAMES</c> name, not the <c>.sam</c>'s</b> (<c>0x00412fb0</c>):
	/// Dino Karts is "DinoKarts" (rows 2 and 3; the original's buy list, <c>q701/side-by-side.png</c>), and the
	/// Loudspeaker is "Speaker: Wild Beasts" (row 61; <c>docs/exe/saves.md</c>, the Loudspeaker bought in the original).
	/// </summary>
	/// <remarks><b>Mutations:</b> the <c>.sam</c>'s <c>Info.Name</c> lettered.</remarks>
	[TestMethod]
	public void TheListsLetterTheObjectNamesRows()
	{
		var data = GameData.Required();
		FileSystem = data;

		var catalogue = new ParkItemCatalogue( "jungle", data, instantAction: true );

		Assert.IsTrue( catalogue.TryGet( 1150, out var karts ) );
		Assert.IsTrue( catalogue.TryGet( 1417, out var speaker ) );
		Assert.AreEqual( ("Dino Karts", "Loudspeaker"), (karts.Name, speaker.Name), "the .sam's names" );
		Assert.AreEqual( "DinoKarts", ParkObjectNames.NameOf( karts ) );
		Assert.AreEqual( "Speaker: Wild Beasts", ParkObjectNames.NameOf( speaker ) );
	}
}
