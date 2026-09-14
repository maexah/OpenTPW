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
}
