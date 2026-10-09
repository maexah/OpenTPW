using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenTPW.Tests;

/// <summary>
/// Which of eight ways a walk slot faces (<see cref="RideNodes.Octant"/>): the rule alone, with no game file.
/// </summary>
[TestClass]
public class RideNodeFacingTests
{
	/// <summary>
	/// trunc( 10.5 − 4θ/π ) mod 8, θ = atan2( z, x ): along z is nought, and the octants count on towards x.
	/// </summary>
	[TestMethod]
	[DataRow( 0.0, 1.0, 0 )]
	[DataRow( 1.0, 1.0, 1 )]
	[DataRow( 1.0, 0.0, 2 )]
	[DataRow( 1.0, -1.0, 3 )]
	[DataRow( 0.0, -1.0, 4 )]
	[DataRow( -1.0, -1.0, 5 )]
	[DataRow( -1.0, 0.0, 6 )]
	[DataRow( -1.0, 1.0, 7 )]
	// The Jungle Spray's three lanes from its entrance, as the original walked them: 7, 0 and 1.
	[DataRow( -9.196, 7.161, 7 )]
	[DataRow( 0.0, 7.161, 0 )]
	[DataRow( 9.196, 7.161, 1 )]
	// An octant's edge is half an octant either side of its middle.
	[DataRow( 0.40, 1.0, 0 )]
	[DataRow( 0.42, 1.0, 1 )]
	[DataRow( -0.40, 1.0, 0 )]
	[DataRow( -0.42, 1.0, 7 )]
	public void ADirectionOnTheGroundIsOneOfEightWays( double x, double z, int octant )
		=> Assert.AreEqual( octant, RideNodes.Octant( x, z ) );

	/// <summary>A direction that is no number, as a matrix never stored gives, is nought; and so is no direction at all.</summary>
	[TestMethod]
	public void NoDirectionIsNought()
	{
		Assert.AreEqual( 0, RideNodes.Octant( double.NaN, double.NaN ) );
		Assert.AreEqual( 2, RideNodes.Octant( 0.0, 0.0 ), "atan2( 0, 0 ) is nought, which is along x" );
	}

	/// <summary>A walker faces along the leg, whatever the height between its ends.</summary>
	[TestMethod]
	public void AWalkerFacesAlongTheLeg()
	{
		var from = new System.Numerics.Vector3( 525.127f, 0.984f, 300.844f );

		Assert.AreEqual( 7, RideNodes.Facing( from, new System.Numerics.Vector3( 515.931f, 9f, 308.005f ) ) );
		Assert.AreEqual( 3, RideNodes.Facing( new System.Numerics.Vector3( 515.931f, 0.984f, 308.005f ), from ) );
	}
}
