using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// <c>Bumper.WhichTrackType</c> - the field the original's "is this open for business" test keys on, and
/// the last one the ride-choosing arm was waiting for.
///
/// <para>
/// <b>It was identified across the whole catalogue rather than from one item, which is the point.</b> A
/// single ride carrying a plausible number would have proved nothing; what identifies the field is that
/// its values sort the jungle's rides into exactly the groups the code's three branches expect - one car
/// track, one water track, three coasters, and everything else untracked.
/// </para>
/// <para>
/// <b>The file's own comment undercounts its own enum</b>, which is worth pinning so nobody trusts it:
/// <c>Rides.sam</c> says "0=no track, 1=car track, 2=water track" and the data carries a 3 as well.
/// </para>
/// <para>
/// These read real game files and are skipped where there is no installation - see <see cref="GameData"/>.
/// </para>
/// </summary>
[TestClass]
public class ParkTrackTypeTests
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

	/// <summary>Dino Karts runs on a car track, Splish Splash on water, and both say so.</summary>
	[TestMethod]
	public void TheKartsRunOnACarTrackAndTheWaterRideOnWater()
	{
		var byName = Catalogue();
		var karts = byName.TryGet( 1101, out var k ) && k.Name == "Dino Karts" ? k : default;

		// Found by name rather than by number, because the catalogue number is not what is under test.
		var all = Enumerable.Range( 1000, 700 )
			.Select( id => byName.TryGet( id, out var item ) ? item : default )
			.Where( item => item.Id != 0 )
			.ToArray();

		var carTracks = all.Where( i => i.TrackType == ItemDescriptionFile.CarTrack ).ToArray();
		var waterTracks = all.Where( i => i.TrackType == ItemDescriptionFile.WaterTrack ).ToArray();

		Assert.AreEqual( 1, carTracks.Length, "one car track in the jungle" );
		Assert.AreEqual( "Dino Karts", carTracks[0].Name, "and it is the karts" );

		Assert.AreEqual( 1, waterTracks.Length, "one water track" );
		Assert.AreEqual( "Splish Splash", waterTracks[0].Name, "and it is the water ride" );

		_ = karts;
	}

	/// <summary>
	/// The three coasters carry the value the file's own comment never mentions - and they are exactly the
	/// rides that declare no duration, which is why the original computes their excitement another way.
	/// </summary>
	[TestMethod]
	public void EveryCoasterCarriesTheValueTheCommentDoesNotMention()
	{
		var catalogue = Catalogue();

		var coasters = Enumerable.Range( 1000, 700 )
			.Select( id => catalogue.TryGet( id, out var item ) ? item : default )
			.Where( item => item.Id != 0 && item.TrackType == ItemDescriptionFile.CoasterTrack )
			.Select( item => item.Name )
			.OrderBy( name => name )
			.ToArray();

		CollectionAssert.AreEqual( new[] { "Chac Atak", "Gorilla Thrilla", "Temple Of Gloom" }, coasters,
			"the jungle's three coasters, and nothing else, carry track type 3" );
	}

	/// <summary>
	/// Belly Bounce - the park's one ride - is untracked, which is what makes the tracked arms unable to
	/// fire in the shipped park and is worth knowing before reading anything into them.
	/// </summary>
	[TestMethod]
	public void TheOneRideInTheShippedParkIsUntracked()
	{
		Assert.AreEqual( 0, Item( 1100 ).TrackType, "Belly Bounce runs on no track" );

		var catalogue = Catalogue();
		var world = Park();

		foreach ( var placed in world.Objects )
		{
			if ( catalogue.TryGet( placed.CatalogueId, out var item ) )
				Assert.AreEqual( 0, item.TrackType, $"'{item.Name}' stands in this park and is untracked" );
		}
	}

	/// <summary>
	/// The arm itself: a tracked ride whose track is not valid is refused, and the same ride is admitted
	/// once it is. The shipped park cannot exercise this - nothing in it is tracked - so the object is
	/// varied deliberately rather than hoping the data provides a case.
	/// </summary>
	[TestMethod]
	public void ATrackedRideWithAnInvalidTrackIsRefused()
	{
		var ride = Park().Objects.Single( o => o.ThingId == 13 );

		Assert.IsTrue( ParkRideChoice.CanBeOffered( ride, 0, ItemDescriptionFile.CarTrack ),
			"a car track whose track ride is valid is offered" );

		Assert.IsFalse( ParkRideChoice.CanBeOffered( ride with { IsTrackRideValid = 0 }, 0, ItemDescriptionFile.CarTrack ),
			"and is refused when it is not" );

		Assert.IsFalse( ParkRideChoice.CanBeOffered( ride with { IsTrackRideValid = 0 }, 0, ItemDescriptionFile.WaterTrack ),
			"the same holds for a water track" );

		// Untracked and coaster candidates do not consult the flag at all, which is what keeps this arm
		// from quietly becoming "every object needs a valid track ride".
		Assert.IsTrue( ParkRideChoice.CanBeOffered( ride with { IsTrackRideValid = 0 }, 0, 0 ),
			"an untracked ride never asks about a track" );
		Assert.IsTrue( ParkRideChoice.CanBeOffered( ride with { IsTrackRideValid = 0 }, 0, ItemDescriptionFile.CoasterTrack ),
			"nor does a coaster, whose own extra check is the model test this does not reproduce" );
	}
}
