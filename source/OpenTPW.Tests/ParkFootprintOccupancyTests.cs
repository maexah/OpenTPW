using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenTPW.Tests;

/// <summary>
/// What a click on one cell of a placed thing resolves to - <see cref="ParkPicking.ThingOn"/>, which is
/// what makes a thing the save placed clickable anywhere but its anchor.
///
/// <para>
/// <b>These need no game files.</b> The question is the cell arithmetic, which <see cref="ParkState"/>
/// owns; standing an actual model needs the installation, and the driven run measures that half.
/// </para>
/// <para>
/// <b>A placed thing is on ONE cell's occupancy list, not on every cell it covers</b>. The occupancy
/// links are keyed per THING, so pushing one thing onto twelve cells makes the twelfth overwrite the
/// first and the guest underneath is lost. The save does not do that: measured over the shipped park,
/// all twelve cells of the Belly Bounce carry <c>mParentID</c> 2996 while only (51,23) carries an
/// occupant. The owner is how the other eleven are found.
/// </para>
/// </summary>
[TestClass]
public class ParkFootprintOccupancyTests
{
	private static ParkState Park() => new( parkIsClosed: false, visitorsToDate: 0 );

	private const int Ride = 40;
	private const int Guest = 7;

	/// <summary>
	/// A thing standing at an anchor, with its footprint owned by that anchor's packed cell.
	/// </summary>
	/// <remarks>
	/// <b>The anchor defaults to the footprint's top-left and for a TURNED thing it is not.</b> The
	/// shipped park has two of those - the Staff Room anchored (58,16) covering (58,15)..(59,16), and
	/// the Round Fountain anchored (57,19) covering (57,17)..(59,19) - so the two are passed apart here.
	/// </remarks>
	private static void Place( ParkState park, int left, int top, int right, int bottom, int thingId,
		int anchorX = -1, int anchorY = -1 )
	{
		if ( anchorX < 0 )
			anchorX = left;

		if ( anchorY < 0 )
			anchorY = top;

		park.AddObject( new ParkWorld.CatalogueObject(
			ThingId: thingId, CatalogueId: 1100,
			RawX: anchorX << 8, RawY: anchorY << 8, Angle: 0 ) );

		for ( var y = top; y <= bottom; ++y )
		{
			for ( var x = left; x <= right; ++x )
			{
				park.SetRecord( x, y, park.Record( x, y ) with
				{
					ParentId = (ushort)MapStep.CellId( anchorX, anchorY )
				} );
			}
		}

		// The anchor alone goes on a cell's list, which is what the save does.
		park.EnterCell( anchorX, anchorY, thingId );
	}

	/// <summary>
	/// The whole of Q2: a click anywhere on a thing finds it, not just on the cell it is anchored at.
	/// </summary>
	[TestMethod]
	public void EveryCellOfAFootprintFindsTheThingThatOwnsIt()
	{
		var park = Park();

		Place( park, 51, 23, 53, 26, Ride );

		for ( var y = 23; y <= 26; ++y )
		{
			for ( var x = 51; x <= 53; ++x )
				Assert.AreEqual( Ride, ParkPicking.ThingOn( park, x, y ), $"({x},{y}) should find thing {Ride}" );
		}
	}

	/// <summary>Cells outside it answer nothing, or clicking beside a ride would open it.</summary>
	[TestMethod]
	public void NothingOutsideTheFootprintFindsIt()
	{
		var park = Park();

		Place( park, 51, 23, 53, 26, Ride );

		foreach ( var (x, y) in new[] { (50, 23), (54, 23), (51, 22), (51, 27), (54, 27) } )
			Assert.AreEqual( 0, ParkPicking.ThingOn( park, x, y ), $"({x},{y}) is outside the footprint" );
	}

	/// <summary>
	/// <b>Whoever is STANDING on a cell is found before whoever owns it</b>, so a guest on a ride's
	/// footprint is reached rather than the ride under their feet. The owner is the fallback.
	/// </summary>
	/// <remarks>
	/// <b>Mutation:</b> asking the owner first, or dropping the occupancy test, returns the ride here
	/// and fails - which would make a guest standing on a ride unclickable.
	/// </remarks>
	[TestMethod]
	public void SomebodyStandingOnACellIsFoundBeforeTheThingThatOwnsIt()
	{
		var park = Park();

		Place( park, 51, 23, 53, 26, Ride );
		park.EnterCell( 52, 24, Guest );

		Assert.AreEqual( Guest, ParkPicking.ThingOn( park, 52, 24 ), "the guest is standing there" );
		Assert.AreEqual( Ride, ParkPicking.ThingOn( park, 52, 25 ), "and the cell beside them is still the ride" );

		// And taking the guest off hands the cell back to the ride that owns it, rather than to nobody.
		park.LeaveCell( 52, 24, Guest );
		Assert.AreEqual( Ride, ParkPicking.ThingOn( park, 52, 24 ), "the owner is underneath all along" );
	}

	/// <summary>
	/// <b>A TURNED thing, whose anchor is not the corner of its own footprint.</b> The owner written on
	/// each cell is the cell the thing STANDS on, and <see cref="ParkPicking.ThingOn"/> matches it
	/// against each object's own position, so keying either side on the footprint's top-left leaves a
	/// turned thing findable nowhere at all.
	/// </summary>
	/// <remarks>
	/// <b>A square-on thing cannot tell its anchor from its footprint's corner</b>, because the two
	/// coincide. The shipped park's turned things do not: the Staff Room is anchored (58,16) and covers
	/// (58,15)..(59,16). Those are its real numbers.
	/// </remarks>
	[TestMethod]
	public void ATurnedThingIsFoundAcrossItsWholeFootprint()
	{
		var park = Park();

		Place( park, 58, 15, 59, 16, Ride, anchorX: 58, anchorY: 16 );

		for ( var y = 15; y <= 16; ++y )
		{
			for ( var x = 58; x <= 59; ++x )
				Assert.AreEqual( Ride, ParkPicking.ThingOn( park, x, y ), $"({x},{y}) should find thing {Ride}" );
		}
	}

	/// <summary>
	/// <b>Taking a thing off a cell it was never on must not cost it the links it holds elsewhere.</b>
	/// <see cref="ParkState.LeaveCell"/> ends by forgetting the thing's own next and previous whether or
	/// not it found it on the cell asked about, so a caller that sweeps a footprint reaches the anchor
	/// last with nothing left to relink - and puts nought into the head instead of promoting whoever
	/// stood behind it.
	/// </summary>
	/// <remarks>
	/// <see cref="ParkBuilding"/>'s sell leaves the anchor and nothing else: a sweep of the whole footprint
	/// loses a guest for a TURNED thing, whose anchor is not the corner the sweep starts from.
	/// </remarks>
	[TestMethod]
	public void LeavingACellTheThingIsNotOnKeepsWhoeverStandsBehindIt()
	{
		var park = Park();

		Place( park, 58, 15, 59, 16, Ride, anchorX: 58, anchorY: 16 );
		park.EnterCell( 58, 16, Guest );

		// The guest went on last, so the ride is behind them on the same cell's list.
		Assert.AreEqual( Guest, ParkPicking.ThingOn( park, 58, 16 ), "the guest is standing on the anchor" );

		// The corner of the footprint, which is not the cell the ride is on.
		park.LeaveCell( 58, 15, Ride );
		park.LeaveCell( 58, 16, Guest );

		Assert.AreEqual( Ride, ParkPicking.ThingOn( park, 58, 16 ),
			"the ride is still on its anchor after being taken off a cell it was never on" );
	}

	/// <summary>
	/// <b>The real <see cref="ParkBuilding.Stamp"/>, over a TURNED thing.</b> Every case above this
	/// one writes the owner itself, so a <see cref="ParkBuilding.Stamp"/> that keyed it on the footprint's
	/// corner instead of the anchor would pass them all: they never call it (<c>docs/VERIFYING.md</c> rule
	/// 114). The numbers are the shipped Staff Room's own: anchored (58,16), covering
	/// (58,15)..(59,16), so the anchor is not the corner.
	/// </summary>
	[TestMethod]
	public void TheStampedFootprintOfATurnedThingIsFoundEverywhere()
	{
		var park = Park();

		park.AddObject( new ParkWorld.CatalogueObject(
			ThingId: Ride, CatalogueId: 1100, RawX: 58 << 8, RawY: 16 << 8, Angle: 90 ) );

		ParkBuilding.Stamp( park, (58, 15, 59, 16), 58, 16 );
		park.EnterCell( 58, 16, Ride );

		for ( var y = 15; y <= 16; ++y )
		{
			for ( var x = 58; x <= 59; ++x )
				Assert.AreEqual( Ride, ParkPicking.ThingOn( park, x, y ), $"({x},{y}) should find thing {Ride}" );
		}
	}

	/// <summary>
	/// <b>Selling a turned thing hands its anchor back to whoever was standing under it.</b>
	/// </summary>
	/// <remarks>
	/// <b>The guest goes on FIRST and the thing is built over them</b>, which is both the realistic order
	/// and the only one that can see the fault: it leaves the thing at the head of the cell's list with
	/// the guest behind it, so taking the thing off has to promote the guest. Stood the other way up the
	/// guest is already the head, and a sweep that leaves the wrong cell changes nothing observable -
	/// the mutation survives and the test reads as proof while proving nothing.
	/// </remarks>
	[TestMethod]
	public void UnstampingATurnedThingHandsItsAnchorBackToTheGuestUnderIt()
	{
		var park = Park();

		park.AddObject( new ParkWorld.CatalogueObject(
			ThingId: Ride, CatalogueId: 1100, RawX: 58 << 8, RawY: 16 << 8, Angle: 90 ) );

		park.EnterCell( 58, 16, Guest );
		ParkBuilding.Stamp( park, (58, 15, 59, 16), 58, 16 );
		park.EnterCell( 58, 16, Ride );

		Assert.AreEqual( Ride, park.CellAt( 58, 16 ).Occupant, "the thing was built over the guest" );

		ParkBuilding.Unstamp( park, (58, 15, 59, 16), 58, 16, Ride );

		Assert.AreEqual( Guest, park.CellAt( 58, 16 ).Occupant,
			"and taking it away leaves the guest standing there, rather than nobody" );
	}

	/// <summary>
	/// A cell naming an owner that is no longer in the park answers nothing - what selling leaves
	/// behind for the instant before its footprint is cleared to bare ground.
	/// </summary>
	[TestMethod]
	public void ACellWhoseOwnerHasGoneFindsNothing()
	{
		var park = Park();

		Place( park, 51, 23, 53, 26, Ride );

		park.RemoveObject( Ride );

		Assert.AreEqual( 0, ParkPicking.ThingOn( park, 52, 25 ),
			"nothing in the park stands at that anchor any more" );
	}
}
