using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Numerics;

namespace OpenTPW.Tests;

using Vector3 = System.Numerics.Vector3;

/// <summary>
/// The camera's direction as a boat's rider sees it - <see cref="ParkBumperBoats.TowardsCamera(System.Numerics.Vector3, Quaternion, Matrix4x4)"/>,
/// what <c>FUN_0044b510</c> composes before it picks a head picture: the boat's turn undone, the map's z-up swapped for the
/// model's y-up, then the seat node's own rotation undone.
/// </summary>
[TestClass]
public class ParkBoatTowardsCameraTests
{
	private static readonly Quaternion Boat = Quaternion.CreateFromAxisAngle( Vector3.UnitZ, -MathF.PI / 2 );

	private static readonly Matrix4x4 Seat = Matrix4x4.CreateRotationY( MathF.PI / 2 );

	private static void AreClose( Vector3 expected, Vector3 actual )
		=> Assert.IsTrue( Vector3.Distance( expected, actual ) < 1e-5f, $"expected {expected}, got {actual}" );

	/// <summary>
	/// A boat turned a quarter (its yaw put in as <c>-yaw</c> about z, as <c>Put</c> does) and a seat turned a quarter about y, with
	/// the camera at (0, -2, 3) from the seat in the world: the boat's turn undone gives (2, 0, 3); y and z swapped,
	/// (2, 3, 0); the seat's turn undone, (0, 3, 2).
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> the boat's turn applied instead of undone; no swap of y and z; the seat's turn applied instead of undone;
	/// the unusable seat's direction lost.
	/// </remarks>
	[TestMethod]
	public void TheCameraIsTurnedBackByTheBoatThenSwappedThenTurnedBackBySeat()
	{
		AreClose( new Vector3( 0, 3, 2 ), ParkBumperBoats.TowardsCamera( new Vector3( 0, -2, 3 ), Boat, Seat ) );
	}

	/// <summary>A seat whose rotation cannot be undone leaves the direction as the model has it: swapped, turned back by the boat, no more.</summary>
	[TestMethod]
	public void ASeatThatCannotBeUndoneLeavesTheModelsDirection()
	{
		AreClose( new Vector3( 2, 3, 0 ), ParkBumperBoats.TowardsCamera( new Vector3( 0, -2, 3 ), Boat, new Matrix4x4() ) );
	}
}
