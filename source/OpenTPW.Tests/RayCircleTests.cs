using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenTPW.Tests;

/// <summary>
/// Solving how far along a direction a circle is touched.
///
/// <para>
/// The interesting test here is the one that pins an answer which is <i>geometrically wrong</i>. The
/// original's scaling of the square root does not match the scaling of what it is added to, so a ray fired
/// at a circle one cell in radius whose centre is four cells away comes back at about four rather than three.
/// That is reproduced deliberately, so the test asserts the number the original produces and says plainly
/// that it is not the textbook answer.
/// </para>
/// </summary>
[TestClass]
public class RayCircleTests
{
	private const int Cell = FixedVector.One;

	/// <summary>
	/// <b>Missing is a negative value, and that is the only reason its caller works.</b> The original tests
	/// the answer against <c>0x7fffffff</c>, which it can never be, so that test is dead - and the check
	/// straight after it catches this because it is less than nothing.
	/// </summary>
	[TestMethod]
	public void MissingIsNegativeWhichIsWhatMakesTheCallersDeadTestHarmless()
	{
		Assert.AreEqual( int.MinValue, RayCircle.Missed );
		Assert.IsTrue( RayCircle.Missed < 0, "the caller's real guard is 'not greater than zero'" );
		Assert.AreNotEqual( 0x7fffffff, RayCircle.Missed, "which is the value the original looks for" );
	}

	/// <summary>A line passing well to one side of the circle never touches it.</summary>
	[TestMethod]
	public void ALineThatPassesWideMisses()
	{
		var answer = RayCircle.Along(
			FixedVector.Zero, new FixedVector( Cell, 0 ), FixedVector.AtCell( 0, 4 ), Cell );

		Assert.AreEqual( RayCircle.Missed, answer );
	}

	/// <summary>
	/// A line fired straight at a circle touches it - and the number is the original's, not the one the
	/// geometry says.
	///
	/// <para>
	/// Centre four cells away, radius one cell: the near edge is three cells off, and the answer is 261888,
	/// which is 3.996 cells. The whole calculation is pinned rather than the intent, because the intent is
	/// not established.
	/// </para>
	/// </summary>
	[TestMethod]
	public void AimedStraightAtItTheAnswerIsTheOriginalsNotTheGeometrys()
	{
		var answer = RayCircle.Along(
			FixedVector.Zero, new FixedVector( Cell, 0 ), FixedVector.AtCell( 4, 0 ), Cell );

		Assert.AreEqual( 261888, answer );

		Assert.AreNotEqual( 3 * Cell, answer,
			"three cells is where the near edge actually is, and this is not that" );

		Assert.IsTrue( answer > 0 && answer <= 4 * Cell, "it does land between the ray and the far side" );
	}

	/// <summary>Pointing the other way answers with a distance behind you, which its caller throws out.</summary>
	[TestMethod]
	public void PointingAwayGivesSomethingBehindYou()
	{
		var answer = RayCircle.Along(
			FixedVector.Zero, new FixedVector( -Cell, 0 ), FixedVector.AtCell( 4, 0 ), Cell );

		Assert.IsTrue( answer < 0, "behind the ray, which the caller rejects with its 'not greater' test" );
		Assert.AreNotEqual( RayCircle.Missed, answer, "but it is a real root, not a miss" );
	}

	/// <summary>Standing inside the circle still gives an answer, one root each side of you.</summary>
	[TestMethod]
	public void StandingInsideItStillSolves()
	{
		var answer = RayCircle.Along(
			FixedVector.Zero, new FixedVector( Cell, 0 ), FixedVector.AtCell( 0, 0 ), 2 * Cell );

		Assert.AreNotEqual( RayCircle.Missed, answer, "a line through the middle always meets it" );
	}

	/// <summary>A circle of no size is never touched, however straight the aim.</summary>
	[TestMethod]
	public void ACircleOfNoSizeIsNeverTouched()
	{
		var answer = RayCircle.Along(
			FixedVector.Zero, new FixedVector( Cell, 0 ), FixedVector.AtCell( 4, 0 ), 0 );

		Assert.AreEqual( RayCircle.Missed, answer,
			"the discriminant has to be strictly greater than nothing, so grazing counts as missing" );
	}
}
