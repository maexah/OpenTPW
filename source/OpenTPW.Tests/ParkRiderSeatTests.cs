using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// Where a ride carries the people it has taken aboard.
///
/// <para>
/// <b>Alexah found this by playing: the children never appear on the ride, bouncing - their sprite stays
/// at the front of the queue until the ride is over.</b> Half of that is the original's own behaviour,
/// which is what made it confusing rather than plainly broken: nothing in the engine moves a rider
/// either. All five callers of its "place a person" routine are accounted for - the ride exit, a generic
/// put-down, a wrapper, the handyman's litter arm and dropping a staff member - and not one of them is a
/// rider. A rider's world position legitimately stays where they queued, and the DRAWING puts them on a
/// node of the ride's own model. This build did the first half and never the second.
/// </para>
/// <para>
/// <b>This class pins the join between the two halves</b> - the script's node NUMBER against the model's
/// node NAME - because that is the part that can rot silently. If <c>bouncy.MD2</c> stopped carrying
/// these names, the riders would be looked up, not found, and drawn back at the queue with nothing
/// anywhere saying so.
/// </para>
/// <para>
/// These read real game files and are skipped where there is no installation - see <see cref="GameData"/>.
/// </para>
/// </summary>
[TestClass]
public class ParkRiderSeatTests
{
	/// <summary>The Belly Bounce, the one thing in Lost Kingdom that carries anybody on itself.</summary>
	private const int BellyBounce = 1100;

	/// <summary>How many riders <c>Bouncy.RSE</c> declares room for.</summary>
	private const int Slots = 10;

	private BaseFileSystem data = null!;

	[TestInitialize]
	public void MountTheGame() => FileSystem = data = GameData.Required();

	private ParkItemCatalogue.Item Item( int id )
	{
		Assert.IsTrue( new ParkItemCatalogue( "jungle", data ).TryGet( id, out var item ),
			$"catalogue item {id} should exist" );

		return item;
	}

	/// <summary>The item's own model, read through the mount as every other test reads a game file.</summary>
	private ModelFile ModelOf( ParkItemCatalogue.Item item )
	{
		using var stream = new MemoryStream( data.ReadAllBytes( $"{item.Directory}/{item.Stem}.MD2" ) );

		return new ModelFile( stream );
	}

	/// <summary>
	/// The naming rule itself: node nought is <c>body</c> and the rest count up in two digits.
	/// </summary>
	[TestMethod]
	public void ABounceNodeIsNamedForItsNumber()
	{
		Assert.AreEqual( "body", ParkPeople.BounceNodeName( 0 ), "the first has no number at all" );
		Assert.AreEqual( "body01", ParkPeople.BounceNodeName( 1 ) );
		Assert.AreEqual( "body09", ParkPeople.BounceNodeName( 9 ), "and the last of Bouncy's ten" );
	}

	/// <summary>
	/// <b>The join, and the reason this file exists.</b> Every name the rule produces for Bouncy's ten
	/// slots is a node the shipped model actually carries.
	/// </summary>
	[TestMethod]
	public void EveryBounceNodeTheRuleNamesIsInTheRidesOwnModel()
	{
		var model = ModelOf( Item( BellyBounce ) );

		var named = model.Nodes
			.Select( node => node.Name.Trim() )
			.Where( name => name.Length > 0 )
			.ToHashSet( System.StringComparer.OrdinalIgnoreCase );

		for ( var slot = 0; slot < Slots; ++slot )
		{
			var name = ParkPeople.BounceNodeName( slot );

			Assert.IsTrue( named.Contains( name ),
				$"slot {slot} wants a node called '{name}' and bouncy.MD2 does not carry one - " +
				"riders would be looked up, not found, and drawn back at the queue" );
		}
	}

	/// <summary>
	/// And the anti-vacuity half: the rule does not simply name every node in the model, and the ten it
	/// does name are distinct places rather than one node ten times.
	/// </summary>
	[TestMethod]
	public void TheTenSeatsAreDistinctPlacesAndNotTheWholeModel()
	{
		var model = ModelOf( Item( BellyBounce ) );

		var wanted = Enumerable.Range( 0, Slots )
			.Select( ParkPeople.BounceNodeName )
			.ToHashSet( System.StringComparer.OrdinalIgnoreCase );

		var seats = model.Nodes
			.Where( node => wanted.Contains( node.Name.Trim() ) )
			.ToArray();

		Assert.AreEqual( Slots, seats.Length, "ten names, ten nodes" );

		// Ten different places. A model that named them all at the origin would satisfy the test above
		// and still draw every rider inside one another.
		var places = seats
			.Select( node => (node.WorldTransform.M41, node.WorldTransform.M42, node.WorldTransform.M43) )
			.ToHashSet();

		Assert.AreEqual( Slots, places.Count, "and ten distinct positions" );

		// The model carries a good many nodes the rule does NOT name - so "is it in the model" is a
		// real question rather than one every name would pass.
		Assert.IsTrue( model.Nodes.Count > Slots + 4,
			$"bouncy.MD2 carries {model.Nodes.Count} nodes, of which the rule claims {Slots}" );
	}

	/// <summary>
	/// The seats are off the ground, which is what makes a rider appear ON the ride rather than at its
	/// feet - and is the half a position-only check would miss.
	/// </summary>
	[TestMethod]
	public void TheSeatsAreAboveTheGround()
	{
		var model = ModelOf( Item( BellyBounce ) );

		var wanted = Enumerable.Range( 0, Slots )
			.Select( ParkPeople.BounceNodeName )
			.ToHashSet( System.StringComparer.OrdinalIgnoreCase );

		foreach ( var node in model.Nodes.Where( n => wanted.Contains( n.Name.Trim() ) ) )
		{
			// The model's Z is its up, and LobbyModel swizzles it into the world's when it places a node.
			Assert.IsTrue( node.WorldTransform.M42 > 1f,
				$"'{node.Name.Trim()}' sits at height {node.WorldTransform.M42}, which is on the floor" );
		}
	}
}
