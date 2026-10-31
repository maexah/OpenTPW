using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenTPW.Tests;

/// <summary>
/// Whether a guest put off a sold thing makes the kids' effect <c>0x80</c>, and where - see
/// <see cref="ParkPeople.SoundFor"/>: always for a rider (<c>0x004fb3f5</c>), for a queuer only when their id
/// is a multiple of eight (<c>0x0050133d</c>), and a rider's place by the thing's flag <c>0x20</c>. Needs no game.
/// </summary>
[TestClass]
public class ParkPutOffSoundTests
{
	/// <summary>The Belly Bounce's flags word in Lost Kingdom's save, which carries <c>0x20</c>.</summary>
	private const int BellyBounceFlags = 0x012c;

	/// <summary>A visitable thing without <c>0x20</c>, whose riders' sprites admission destroys.</summary>
	private const int WithoutTheBit = 0x0004;

	private const int Riding = (int)PeepBehaviour.PutOff.Riding;
	private const int Queueing = (int)PeepBehaviour.PutOff.Queueing;
	private const int Heading = (int)PeepBehaviour.PutOff.Heading;
	private const int No = (int)PeepBehaviour.PutOff.No;

	private const int None = (int)ParkPeople.PutOffSound.None;
	private const int Origin = (int)ParkPeople.PutOffSound.Origin;
	private const int Seat = (int)ParkPeople.PutOffSound.Seat;
	private const int Feet = (int)ParkPeople.PutOffSound.Feet;
	private const int Walk = (int)ParkPeople.PutOffSound.Walk;

	[TestMethod]
	[DataRow( Riding, 35, BellyBounceFlags, true, false, Seat )]
	[DataRow( Riding, 35, BellyBounceFlags, false, false, Feet )]
	[DataRow( Riding, 35, BellyBounceFlags, false, true, Walk )]
	[DataRow( Riding, 35, WithoutTheBit, true, false, Origin )]
	[DataRow( Riding, 35, WithoutTheBit, false, false, Origin )]
	[DataRow( Riding, 35, WithoutTheBit, false, true, Origin )]
	[DataRow( Queueing, 32, BellyBounceFlags, false, false, Feet )]
	[DataRow( Queueing, 8, BellyBounceFlags, false, false, Feet )]
	[DataRow( Queueing, 44, BellyBounceFlags, false, false, None )]
	[DataRow( Queueing, 29, BellyBounceFlags, false, false, None )]
	[DataRow( Queueing, 42, WithoutTheBit, false, false, None )]
	[DataRow( Heading, 32, BellyBounceFlags, false, false, None )]
	[DataRow( No, 32, BellyBounceFlags, false, false, None )]
	public void TheSoundFollowsTheArmTheIdAndTheFlag( int how, int guestId, int thingFlags, bool seated, bool walked, int expected )
	{
		Assert.AreEqual( (ParkPeople.PutOffSound)expected,
			ParkPeople.SoundFor( (PeepBehaviour.PutOff)how, guestId, thingFlags, seated, walked ) );
	}

	/// <summary>
	/// <b>A walk's rider is heard where the placer left their sprite</b> (<c>FUN_004fb3c3</c>): hidden by the placer
	/// (<c>FUN_004f9ed0</c> wrote the sprite handle 0), the origin - the old rule, the navigator's feet, is not it;
	/// stood, the point the placer put them at; a seat as before.
	/// </summary>
	[TestMethod]
	public void AWalksRiderIsHeardWhereThePlacerLeftThem()
	{
		var seat = new Vector3( 1f, 2f, 3f );
		var at = new Vector3( 465f, 279.07f, 12f );

		Assert.AreEqual( Vector3.Zero, ParkPeople.PutOffPoint( ParkPeople.PutOffSound.Walk, null, true, at ) );
		Assert.AreEqual( at, ParkPeople.PutOffPoint( ParkPeople.PutOffSound.Walk, null, false, at ) );
		Assert.AreEqual( seat, ParkPeople.PutOffPoint( ParkPeople.PutOffSound.Seat, seat, false, at ) );
		Assert.AreEqual( Vector3.Zero, ParkPeople.PutOffPoint( ParkPeople.PutOffSound.Origin, null, false, at ) );
		Assert.IsNull( ParkPeople.PutOffPoint( ParkPeople.PutOffSound.None, null, false, at ) );
		Assert.IsNull( ParkPeople.PutOffPoint( ParkPeople.PutOffSound.Feet, null, false, at ) );
	}

	/// <summary>
	/// <b>A seat's and a walk's put-off sound is at the sprite's height, the point's less the ground under it</b> (<c>Person_PlaceSprite</c>,
	/// <c>0x004f9e91</c>): a seat at (30, 40, 25) over ground 20 is heard at 5, a walk's point the same; the ground is sampled at the point's own x and y;
	/// the origin and a hidden walk are not lifted by it.
	/// </summary>
	/// <remarks><b>Mutations:</b> the ground not taken off, taken off the wrong arm, or sampled at (y, x) each fails.</remarks>
	[TestMethod]
	public void ASeatsAndAWalksSoundIsAtTheSpritesHeightLessTheGround()
	{
		float Ground( float x, float y ) => x == 30f && y == 40f ? 20f : 1000f;
		var at = new Vector3( 30f, 40f, 25f );

		Assert.AreEqual( new Vector3( 30f, 40f, 5f ), ParkPeople.PutOffPoint( ParkPeople.PutOffSound.Seat, at, false, default, Ground ) );
		Assert.AreEqual( new Vector3( 30f, 40f, 5f ), ParkPeople.PutOffPoint( ParkPeople.PutOffSound.Walk, null, false, at, Ground ) );
		Assert.AreEqual( Vector3.Zero, ParkPeople.PutOffPoint( ParkPeople.PutOffSound.Walk, null, true, at, Ground ) );
		Assert.AreEqual( Vector3.Zero, ParkPeople.PutOffPoint( ParkPeople.PutOffSound.Origin, null, false, at, Ground ) );
		Assert.IsNull( ParkPeople.PutOffPoint( ParkPeople.PutOffSound.Seat, null, false, at, Ground ) );
	}
}
