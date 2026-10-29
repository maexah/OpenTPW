using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// What the advisor wears for a line: the pieces a prop switches (<c>FUN_00598ca0</c>), the props and the model
/// slot the built park topics' responses name, and his two models dressed line after line. Those read real game
/// files and are skipped where there is no installation - see <see cref="GameData"/>.
/// </summary>
[TestClass]
public class AdvisorPropsTests
{
	private const string ModelPath = "global/advisor/Advisor.md2";
	private const string ThemeModelPath = "levels/jungle/advisor/Advisor_JUN.md2";

	/// <summary>Lost Kingdom's costume as its <c>Standard.sam</c> writes it: three keys of the eight.</summary>
	private static readonly Dictionary<string, int> JungleKeys = new()
	{
		["ThemeAdvisorCostumes[0].AddOns"] = 4,
		["ThemeAdvisorCostumes[0].SubOffs"] = 19,
		["ThemeAdvisorCostumes[1].SubOffs"] = 20,
	};

	private static (int AddOn, int SubOff)[] Jungle => AdvisorProps.CostumeOf( key => JungleKeys.GetValueOrDefault( key ) );

	/// <summary>The hide list of the clip every line starts on, 14.</summary>
	private static ushort[] Hidden( BaseFileSystem data, string model )
		=> new AnimationFile( new MemoryStream( data.ReadAllBytes( model[..^4] + "m14.MD2" ) ) ).HideList;

	private static (int, bool)[] Switched( int prop ) => AdvisorProps.Switched( prop ).ToArray();

	[TestMethod]
	public void NoughtShowsHisAntennaeAndNothingElse()
		=> CollectionAssert.AreEqual( new[] { (19, true), (20, true) }, Switched( 0 ) );

	[TestMethod]
	public void AHatThatCoversHisAntennaeHidesThemAndIsShown()
	{
		foreach ( var hat in new[] { 1, 2, 4, 5, 6, 7, 9, 10, 11, 13 } )
			CollectionAssert.AreEqual( new[] { (19, false), (20, false), (hat, true) }, Switched( hat ), $"prop {hat}" );
	}

	[TestMethod]
	public void TheSpatulaHidesTheHandThatHoldsIt()
		=> CollectionAssert.AreEqual( new[] { (21, false), (14, true) }, Switched( 14 ) );

	[TestMethod]
	public void AnyOtherPropIsShownAlone()
	{
		foreach ( var prop in new[] { 3, 8, 12, 15 } )
			CollectionAssert.AreEqual( new[] { (prop, true) }, Switched( prop ), $"prop {prop}" );
	}

	[TestMethod]
	public void TheThemesCostumeWithNoPairSwitchesNothing()
	{
		Assert.AreEqual( 0, Switched( AdvisorProps.ThemeCostume ).Length );
		Assert.AreEqual( 0, AdvisorProps.Switched( AdvisorProps.ThemeCostume, new (int, int)[4] ).Count() );
	}

	/// <summary>Four pairs, each key by its own index, nought where the file names none.</summary>
	[TestMethod]
	public void ACostumeIsFourPairsOfTheBalancesKeys()
	{
		CollectionAssert.AreEqual( new[] { (4, 19), (0, 20), (0, 0), (0, 0) }, Jungle );

		var all = AdvisorProps.CostumeOf( key => key switch
		{
			"ThemeAdvisorCostumes[2].AddOns" => 7,
			"ThemeAdvisorCostumes[3].SubOffs" => 9,
			"ThemeAdvisorCostumes[3].AddOns" => 8,
			_ => 0
		} );
		CollectionAssert.AreEqual( new[] { (0, 0), (0, 0), (7, 0), (8, 9) }, all );
	}

	/// <summary>Pair by pair, the add-on shown before the sub-off is hidden, and the prop's own id asked for by nothing.</summary>
	[TestMethod]
	public void TheThemesCostumeShowsEachAddOnAndHidesEachSubOff()
	{
		CollectionAssert.AreEqual( new[] { (4, true), (19, false), (20, false) },
			AdvisorProps.Switched( AdvisorProps.ThemeCostume, Jungle ).ToArray() );
		CollectionAssert.AreEqual( new[] { (3, true), (6, false), (5, true), (2, false) },
			AdvisorProps.Switched( AdvisorProps.ThemeCostume, new[] { (3, 6), (5, 0), (0, 2), (0, 0) } ).ToArray() );

		// A costume is the theme prop's alone: a hat's pieces are the hat's with one to hand.
		CollectionAssert.AreEqual( new[] { (19, false), (20, false), (5, true) }, AdvisorProps.Switched( 5, Jungle ).ToArray() );
		CollectionAssert.AreEqual( new[] { (19, true), (20, true) }, AdvisorProps.Switched( 0, Jungle ).ToArray() );
	}

	/// <summary>The three built responses that wear the theme's costume are said by the park's own model, slot 1.</summary>
	[TestMethod]
	public void TheCostumesLinesAreTheParksOwnModels()
	{
		Assert.AreEqual( 1, Advisor.ThemeSlot );

		foreach ( var response in new[] { 223, 224, 226 } )
			Assert.AreEqual( 1, ParkAdvisorMessages.SlotOf( response ), $"response {response}" );

		foreach ( var response in new[] { 2, 204, 222, 225, 227, 308, 394, 544 } )
			Assert.AreEqual( 0, ParkAdvisorMessages.SlotOf( response ), $"response {response}" );
	}

	/// <summary>
	/// Lost Kingdom's own advisor dressed for a line: the pith helmet on, and the map, the tickets and the
	/// mutant eye away, with a line of no prop taking the helmet off again. It has no antennae to hide.
	/// </summary>
	[TestMethod]
	public void TheThemesModelWearsItsPithHelmet()
	{
		var data = GameData.Required();
		var model = new ModelFile( new MemoryStream( data.ReadAllBytes( ThemeModelPath ) ) );
		var hidden = Hidden( data, ThemeModelPath );
		var shown = new Dictionary<string, bool>();

		void Show( int node, bool visible ) => shown[node < model.Meshes.Count ? model.Meshes[node].Name.TrimEnd( '\0' ).Trim() : $"node {node}"] = visible;

		Assert.AreEqual( -1, model.FindNode( AdvisorProps.LeftAntenna, AdvisorProps.CostumeSpace ) );
		Assert.AreEqual( -1, model.FindNode( AdvisorProps.RightAntenna, AdvisorProps.CostumeSpace ) );
		Assert.AreEqual( "Pith Helmet", model.Meshes[model.FindNode( 4, AdvisorProps.CostumeSpace )].Name.TrimEnd( '\0' ).Trim() );

		AdvisorProps.Wear( model, hidden, AdvisorProps.ThemeCostume, 0, Jungle, Show );
		Assert.IsTrue( shown["Pith Helmet"], "the helmet on" );
		Assert.IsFalse( shown["MAP"] || shown["Ticket"] || shown["Ticket2"] || shown["Mutant Eye"], "nothing else he can hold or wear" );
		Assert.IsFalse( shown.ContainsKey( "Right hand" ) || shown.ContainsKey( "Body" ) || shown.ContainsKey( "Right Eye" ), "his own parts left alone" );

		AdvisorProps.Wear( model, hidden, AdvisorProps.ThemeCostume, 0, null, Show );
		Assert.IsFalse( shown["Pith Helmet"], "no costume to hand, no helmet" );

		AdvisorProps.Wear( model, hidden, AdvisorProps.ThemeCostume, 0, Jungle, Show );
		AdvisorProps.Wear( model, hidden, 0, 0, Jungle, Show );
		Assert.IsFalse( shown["Pith Helmet"], "a line of no prop takes it off" );
		Assert.AreEqual( 0, shown.Count( piece => piece.Value ), "and puts nothing on: the antennae it would show are not this model's" );

		// Ids 1 to 3 are this model's particle nodes in another space: no prop of that number is a piece of his.
		foreach ( var prop in new[] { 1, 2, 3, 5, 6 } )
		{
			shown.Clear();
			AdvisorProps.Wear( model, [], prop, 0, Jungle, Show );
			Assert.AreEqual( 0, shown.Count, $"prop {prop}" );
		}
	}

	/// <summary>The shipped file names the costume the test's keys do.</summary>
	[TestMethod]
	public void LostKingdomsBalanceNamesThePithHelmet()
	{
		var before = FileSystemOrNull;
		FileSystem = GameData.Required();

		try
		{
			var balance = new ParkBalance( "jungle", easyMode: true );

			CollectionAssert.AreEqual( Jungle, AdvisorProps.CostumeOf( key => balance.Int( key ) ) );
		}
		finally
		{
			FileSystemOrNull = before;
		}
	}

	[TestMethod]
	public void ARidesLinesNameTheHardHatAndTheDoorsTheTicketHat()
	{
		foreach ( var response in Enumerable.Range( 194, 3 ).Concat( Enumerable.Range( 198, 9 ) ) )
			Assert.AreEqual( (5, 0), ParkAdvisorMessages.PropsOf( response ), $"response {response}" );

		foreach ( var response in Enumerable.Range( 308, 4 ) )
			Assert.AreEqual( (9, 0), ParkAdvisorMessages.PropsOf( response ), $"response {response}" );

		foreach ( var response in new[] { 193, 197, 207, 307, 312, 394 } )
			Assert.AreEqual( (0, 0), ParkAdvisorMessages.PropsOf( response ), $"response {response}" );
	}

	/// <summary>His model's pieces by name after each of three lines: a ride's, the door's, the lobby's.</summary>
	[TestMethod]
	public void HisModelIsDressedLineAfterLine()
	{
		var data = GameData.Required();
		var model = new ModelFile( new MemoryStream( data.ReadAllBytes( ModelPath ) ) );
		var shown = new Dictionary<string, bool>();

		void Show( int node, bool visible ) => shown[model.Meshes[node].Name.TrimEnd( '\0' ).Trim()] = visible;

		var hidden = Hidden( data, ModelPath );

		AdvisorProps.Wear( model, hidden, 5, 0, null, Show );
		Assert.IsTrue( shown["Hardhat"], "the hard hat on" );
		Assert.IsFalse( shown["Left Antennae"] || shown["Right Antennae"], "his antennae under it" );
		Assert.IsFalse( shown["Ticket Hat"] || shown["Spatula"] || shown["Bowler_BTie"], "no other add-on" );
		Assert.IsFalse( shown.ContainsKey( "Right hand" ), "his hands left alone" );

		AdvisorProps.Wear( model, hidden, 9, 0, null, Show );
		Assert.IsTrue( shown["Ticket Hat"], "the ticket hat on" );
		Assert.IsFalse( shown["Hardhat"], "the hard hat off again" );
		Assert.IsFalse( shown["Left Antennae"] || shown["Right Antennae"], "his antennae still away" );

		AdvisorProps.Wear( model, hidden, 0, 0, null, Show );
		Assert.IsTrue( shown["Left Antennae"] && shown["Right Antennae"], "his antennae back" );
		Assert.AreEqual( 0, shown.Count( piece => piece.Value && piece.Key.Contains( "Hat" ) ), "no hat" );
		Assert.IsFalse( shown["Hardhat"] || shown["Ticket Hat"], "neither hat" );

		AdvisorProps.Wear( model, hidden, 13, 14, null, Show );
		Assert.IsTrue( shown["Fast Food Hat"] && shown["Spatula"], "both props of a row with two" );
		Assert.IsFalse( shown["Right hand"], "the hand that holds the spatula" );
	}

	/// <summary>Every clip of his hides the same ten pieces as it starts: the model's add-ons, ids under 19.</summary>
	[TestMethod]
	public void EveryClipHidesTheTenAddOns()
	{
		var data = GameData.Required();
		var model = new ModelFile( new MemoryStream( data.ReadAllBytes( ModelPath ) ) );
		var addOns = Enumerable.Range( 0, model.Nodes.Count )
			.Where( node => model.Nodes[node].Id is > 0 and < AdvisorProps.FirstOwnPart ).ToArray();

		Assert.AreEqual( 10, addOns.Length );

		for ( var clip = 1; clip <= 15; ++clip )
		{
			var animation = new AnimationFile( new MemoryStream( data.ReadAllBytes( $"global/advisor/Advisorm{clip}.MD2" ) ) );
			CollectionAssert.AreEquivalent( addOns, animation.HideList.Select( node => (int)node ).ToArray(), $"clip {clip}" );
		}
	}
}
