using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// What an item's own description says about whether a guest may come to it - the data behind the
/// decision the park's guests make (<see cref="ParkRideChooser"/>).
///
/// <para>
/// <b>An item's file is an OVERRIDE, not a whole description.</b> Each folder carries the defaults for
/// its kind and an item says only what differs, so <c>Bouncy.sam</c> never states that it is a ride, that
/// people may use it, or that it has a queue - all three come from <c>rides/Rides.sam</c>. Reading an item
/// alone gets those wrong silently, which is why the catalogue reads both.
/// </para>
/// <para>
/// <b>The last test is the one worth having.</b> It checks the item files against the SAVE - two sources
/// that know nothing about each other - and asserts they agree object for object about who may be
/// visited. Nothing about that agreement is arranged: the save carries a bit in a flags byte at file
/// offset 58, and the item files carry a keyword in a text file inside a different archive.
/// </para>
/// <para>
/// These read real game files and are skipped where there is no installation - see <see cref="GameData"/>.
/// </para>
/// </summary>
[TestClass]
public class ParkItemDecisionTests
{
	private BaseFileSystem data = null!;

	[TestInitialize]
	public void MountTheGame() => FileSystem = data = GameData.Required();

	private ParkItemCatalogue Catalogue() => new( "jungle", data );

	private ParkWorld Park()
	{
		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		return new ParkWorld( new SaveReader( stream ).ReadFile() );
	}

	private ParkItemCatalogue.Item Item( int id )
	{
		Assert.IsTrue( Catalogue().TryGet( id, out var item ), $"catalogue item {id} should exist" );

		return item;
	}

	/// <summary>
	/// The four kinds, by the game's own numbering - and each item takes it from the folder it lives in,
	/// because not one of the jungle's items states it.
	/// </summary>
	[TestMethod]
	public void EachItemInheritsItsKindFromTheFolderItLivesIn()
	{
		Assert.AreEqual( 0, Item( 1100 ).UiType, "Belly Bounce is a ride" );
		Assert.AreEqual( 1, Item( 1203 ).UiType, "the Drinks Shop is a shop" );
		Assert.AreEqual( 2, Item( 1303 ).UiType, "Jungle Spray is a sideshow" );
		Assert.AreEqual( 3, Item( 1402 ).UiType, "a Small Toilet is a feature" );
		Assert.AreEqual( 3, Item( 1411 ).UiType, "and so is the Staff Room" );
	}

	/// <summary>
	/// <b>The toilet is the interesting one.</b> Features default to nobody choosing them, and the toilet
	/// overrides itself back - which is the whole reason the category has to be read before the item
	/// rather than instead of it.
	/// </summary>
	[TestMethod]
	public void FeaturesAreNotChosenExceptTheToiletWhichOverridesItself()
	{
		Assert.IsTrue( Item( 1402 ).IsChoosable, "a Small Toilet says 'People CAN use this'" );
		Assert.IsTrue( Item( 1402 ).ProvidesRelief, "and it is what relieves a guest who needs one" );

		foreach ( var id in new[] { 1403, 1406, 1411, 1413 } )
		{
			Assert.IsFalse( Item( id ).IsChoosable, $"item {id} is a feature nobody chooses" );
			Assert.IsFalse( Item( id ).ProvidesRelief, $"item {id} is not a toilet" );
		}

		// And the three that never say so inherit it, which is the half a single-file read would miss.
		foreach ( var id in new[] { 1100, 1203, 1303 } )
			Assert.IsTrue( Item( id ).IsChoosable, $"item {id} inherits 'choosable' from its folder" );
	}

	/// <summary>
	/// The numbers an item carries, each from the file that states it: the ride overrides its own
	/// excitement and attraction, the shop quenches thirst and not hunger. The guests' decision
	/// (<see cref="ParkRideScore"/>) weighs the excitement, the thirst and the hunger.
	/// </summary>
	[TestMethod]
	public void TheDecisionNumbersComeFromTheItemFilesThemselves()
	{
		var ride = Item( 1100 );

		Assert.AreEqual( 40, ride.ExcitementLevel, "Belly Bounce overrides its excitement" );
		Assert.AreEqual( 25, ride.AttractionValue, "and its attraction value" );
		Assert.AreEqual( 60, ride.NewAttractionDecayTime, "but inherits how long it stays new" );
		Assert.IsTrue( ride.HasQueue, "and that rides are queued for" );

		var shop = Item( 1203 );

		Assert.AreEqual( 40, shop.ThirstEffect, "the Drinks Shop quenches thirst" );
		Assert.AreEqual( 0, shop.HungerEffect, "and does nothing at all for hunger, being a drink" );
		Assert.IsFalse( shop.HasQueue, "shops are not queued for" );

		// The rest of the effect block, which the original applies to five guest meters when somebody
		// finishes using a thing. The file states each one's meter in its own comment column - "how much
		// thirst to deduct", "how much vomit to add" - and the two DEDUCT while the three ADD, which is
		// the same asymmetry the engine's arithmetic has.
		Assert.AreEqual( 10, shop.VomitEffect, "a drink adds to how sick a guest feels" );
		Assert.AreEqual( 5, shop.HappinessEffect, "and cheers them up a little" );
		Assert.AreEqual( 50, shop.LitterEffect, "and leaves them holding a great deal of litter" );

		// Anti-vacuity, and it is the half that gives the three above any meaning: the block is a SHOP
		// thing. The ride declares none of it, so these are read values rather than a constant every item
		// happens to carry.
		Assert.AreEqual( 0, ride.VomitEffect, "the Belly Bounce declares no effect block" );
		Assert.AreEqual( 0, ride.HappinessEffect, "the Belly Bounce declares no effect block" );
		Assert.AreEqual( 0, ride.LitterEffect, "the Belly Bounce declares no effect block" );

		Assert.AreEqual( 35, Item( 1303 ).ExcitementLevel, "Jungle Spray sets its own excitement" );
	}

	/// <summary>
	/// <b>The cross-source check.</b> The item files and the saved park agree, object for object, about
	/// who a guest may be sent to - a keyword in a text file inside one archive against a bit at file
	/// offset 58 of a record in another. Neither knows about the other.
	/// </summary>
	/// <remarks>
	/// The counts are asserted as well as the agreement, because "they agree" is satisfied vacuously if
	/// both are false everywhere - which is exactly what a failure of the category read would look like.
	/// </remarks>
	[TestMethod]
	public void TheItemFilesAndTheSavedParkAgreeAboutWhoMayBeVisited()
	{
		var catalogue = Catalogue();
		var world = Park();
		var checkedAgainst = 0;

		foreach ( var placed in world.Objects )
		{
			if ( !catalogue.TryGet( placed.CatalogueId, out var item ) )
				continue;

			++checkedAgainst;

			Assert.AreEqual( item.IsChoosable, placed.IsVisitable,
				$"'{item.Name}' (catalogue {placed.CatalogueId}, thing {placed.ThingId}): the item file says " +
				$"choosable={item.IsChoosable} and the save says offerable={placed.IsVisitable}" );

			Assert.AreEqual( item.ProvidesRelief, placed.IsToilet,
				$"'{item.Name}': the item file says relief={item.ProvidesRelief} and the save says toilet={placed.IsToilet}" );
		}

		Assert.AreEqual( world.Objects.Count, checkedAgainst, "every object in the park is in the catalogue" );

		// Anti-vacuity: the agreement above is worthless unless both answers actually occur.
		Assert.AreEqual( 6, world.Objects.Count( o => o.IsVisitable ), "six objects may be visited" );
		Assert.AreEqual( 3, world.Objects.Count( o => o.IsToilet ), "three of them are toilets" );
		Assert.IsTrue( world.Objects.Count( o => !o.IsVisitable ) >= 6, "and a good many may not" );
	}
}
