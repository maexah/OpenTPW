using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenTPW.Tests;

/// <summary>
/// <see cref="Rotation.LookAt"/> has one job: answer with a rotation that faces the direction it was
/// handed. This checks that it does, for the ordinary directions and for the one that needs a special
/// case.
///
/// <para>
/// <b>That special case was wrong for as long as it existed.</b> Looking straight back along
/// <c>-Forward</c> leaves the axis of the turn undefined, so the code turns about the up axis instead -
/// but it built the quaternion with <c>W = MathF.PI</c>, where a half turn needs <c>W = cos(pi/2) = 0</c>.
/// The result was not a unit quaternion at all and came back about eighty degrees off.
/// </para>
/// <para>
/// Nothing caught it because nothing had ever looked exactly that way: the lobby and park orbit cameras
/// both place the eye above what they look at, so their direction always carries a height term and is
/// never exactly horizontal. A first-person camera's is, whenever it is not pitched - which is how this
/// surfaced. The near-miss case below is the one that camera actually produces, where a quarter turn
/// leaves a residue of about 4e-8 in Y.
/// </para>
/// </summary>
[TestClass]
public class RotationTests
{
	[TestMethod]
	public void LookAtFacesTheDirectionItWasGiven()
	{
		var directions = new[]
		{
			new Vector3( 1f, 0f, 0f ),              // straight along Forward - the identity case
			new Vector3( -1f, 0f, 0f ),             // straight back - the special case that was wrong
			new Vector3( -1f, -0.00000004f, 0f ),   // what a first-person camera yawed a quarter turn gives
			new Vector3( 0f, 1f, 0f ),
			new Vector3( 0f, -1f, 0f ),
			new Vector3( 1f, 1f, 0f ),
			new Vector3( 0.3f, -0.7f, 0.2f ),
		};

		foreach ( var direction in directions )
		{
			var wanted = direction.Normal;
			var facing = Rotation.LookAt( direction ).Forward;

			Assert.AreEqual( wanted.X, facing.X, 0.001f, $"{direction}: X" );
			Assert.AreEqual( wanted.Y, facing.Y, 0.001f, $"{direction}: Y" );
			Assert.AreEqual( wanted.Z, facing.Z, 0.001f, $"{direction}: Z" );
		}
	}

	/// <summary>
	/// <see cref="Rotation.From(float, float, float)"/> takes any angle, and a whole turn more or less is the same
	/// rotation. Thirty whole turns carry each angle past 180 radians (about 10,313 degrees), so a clamp to 180 either
	/// way cuts it short if it is applied after the angles become radians; 270 degrees is a quarter turn back, so the
	/// same clamp applied before makes it a half turn.
	/// </summary>
	[TestMethod]
	public void FromTakesAnyAngle()
	{
		const float Turns = 30 * 360f;

		for ( int axis = 0; axis < 3; ++axis )
		{
			TurnsAlike( Angles( axis, 90f ), Angles( axis, 90f + Turns ), axis, "a quarter turn and thirty turns more" );
			TurnsAlike( Angles( axis, -90f ), Angles( axis, 270f ), axis, "270 degrees" );
		}
	}

	private static Vector3 Angles( int axis, float degrees )
		=> new( axis == 0 ? degrees : 0f, axis == 1 ? degrees : 0f, axis == 2 ? degrees : 0f );

	private static void TurnsAlike( Vector3 wanted, Vector3 given, int axis, string what )
	{
		var a = Rotation.From( wanted.X, wanted.Y, wanted.Z );
		var b = Rotation.From( given.X, given.Y, given.Z );

		foreach ( var v in new[] { Vector3.Forward, Vector3.Up, Vector3.Left } )
		{
			var x = a * v;
			var y = b * v;

			Assert.AreEqual( x.X, y.X, 0.001f, $"axis {axis}, {what}: {v} X" );
			Assert.AreEqual( x.Y, y.Y, 0.001f, $"axis {axis}, {what}: {v} Y" );
			Assert.AreEqual( x.Z, y.Z, 0.001f, $"axis {axis}, {what}: {v} Z" );
		}
	}
}
