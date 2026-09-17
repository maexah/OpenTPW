using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace OpenTPW.Tests;

/// <summary>
/// Which way round a guest is drawn as the camera turns.
///
/// <para>
/// <b>These need no park, no device and no eyesight, and that is the point.</b> Alexah reported that guests
/// "always face opposite of the camera" when it is rotated. Capturing frames could not settle it: at yaw 0
/// and 180 the right answer and the wrong one are IDENTICAL, and at 90 and 270 they differ only by the
/// picture being mirrored - a profile facing the wrong way, which is exactly the thing that is hard to be
/// sure of from a screenshot of a fifteen-pixel figure.
/// </para>
/// <para>
/// <b>The physical invariant settles it instead: turn the camera and the person together and the drawn
/// picture must not change.</b> Someone walking away from you looks the same whichever way you both happen
/// to be pointing. That is true of the real world, needs no knowledge of the art, and is exactly what the
/// arithmetic must reproduce.
/// </para>
/// </summary>
[TestClass]
public class ParkFacingTests
{
	private const int Compass = 8;

	/// <summary>A compass point, clockwise from north: 0 north, 2 east, 4 south, 6 west.</summary>
	private static Vector3 Towards( int clockwise )
	{
		var turn = clockwise / (float)Compass * MathF.Tau;

		return new Vector3( MathF.Sin( turn ), MathF.Cos( turn ), 0f );
	}

	/// <summary>The eleven-bit heading a person walking that way carries - see <see cref="PeepHeading"/>.</summary>
	private static int HeadingTowards( int clockwise )
		=> (0x400 + (clockwise * 0x100)) & (PeepHeading.FullTurn - 1);

	/// <summary>
	/// <b>A person's octant increases clockwise.</b> Measured off the two pieces that produce it rather than
	/// asserted: the cardinals in <see cref="PeepHeading"/> and the fold in
	/// <see cref="ParkWorld.Person.OctantOf"/>.
	/// </summary>
	[TestMethod]
	public void APersonsOctantIncreasesClockwise()
	{
		for ( var clockwise = 0; clockwise < Compass; ++clockwise )
		{
			Assert.AreEqual( clockwise, ParkWorld.Person.OctantOf( HeadingTowards( clockwise ) ),
				$"a person walking {clockwise} eighths clockwise of north" );
		}
	}

	/// <summary>
	/// <b>And the camera's runs the other way.</b> North is 0 for both, but the camera's next step is WEST,
	/// not east. This is not a mistake to be tidied up - it falls out of the orbit camera's yaw - but it is
	/// the reason the two terms must be added, and it is worth a test of its own so that nobody "fixes" one
	/// of the two conventions and silently breaks the sum.
	/// </summary>
	[TestMethod]
	public void TheCamerasOctantRunsTheOppositeWayRound()
	{
		var expected = new[] { 0, 7, 6, 5, 4, 3, 2, 1 };

		for ( var clockwise = 0; clockwise < Compass; ++clockwise )
		{
			Assert.AreEqual( expected[clockwise], ParkGuestSprites.CameraOctant( Towards( clockwise ) ),
				$"a camera looking {clockwise} eighths clockwise of north" );
		}
	}

	/// <summary>
	/// <b>Turning the camera and the person together must change nothing.</b> The invariant the whole thing
	/// rests on, and the one assertion that tells the right arithmetic from the wrong one - both change the
	/// drawn picture by one per eighth of camera rotation, and differ only in which way.
	/// </summary>
	[TestMethod]
	public void TurningTheCameraAndThePersonTogetherChangesNothing()
	{
		for ( var person = 0; person < Compass; ++person )
		{
			for ( var camera = 0; camera < Compass; ++camera )
			{
				var first = ParkGuestSprites.Facing(
					ParkWorld.Person.OctantOf( HeadingTowards( person ) ),
					ParkGuestSprites.CameraOctant( Towards( camera ) ) );

				for ( var together = 1; together < Compass; ++together )
				{
					var turnedPerson = (person + together) % Compass;
					var turnedCamera = (camera + together) % Compass;

					var then = ParkGuestSprites.Facing(
						ParkWorld.Person.OctantOf( HeadingTowards( turnedPerson ) ),
						ParkGuestSprites.CameraOctant( Towards( turnedCamera ) ) );

					Assert.AreEqual( first, then,
						$"a person facing {person} seen by a camera at {camera} is drawn as {first}, but "
						+ $"turning both by {together} eighths draws {then} - the view between them has "
						+ "not changed, so neither may the picture" );
				}
			}
		}
	}

	/// <summary>
	/// <b>And a guest walking away from the camera looks the same wherever that is.</b> The simplest case of
	/// the invariant above, kept separate because it is the one a person can check by eye in a park: stand
	/// behind somebody and you see their back, from any direction.
	/// </summary>
	[TestMethod]
	public void SomebodyWalkingAwayFromTheCameraAlwaysLooksTheSame()
	{
		var away = ParkGuestSprites.Facing(
			ParkWorld.Person.OctantOf( HeadingTowards( 0 ) ), ParkGuestSprites.CameraOctant( Towards( 0 ) ) );

		for ( var clockwise = 1; clockwise < Compass; ++clockwise )
		{
			Assert.AreEqual( away, ParkGuestSprites.Facing(
					ParkWorld.Person.OctantOf( HeadingTowards( clockwise ) ),
					ParkGuestSprites.CameraOctant( Towards( clockwise ) ) ),
				$"walking away from a camera {clockwise} eighths round draws a different picture" );
		}
	}
}
