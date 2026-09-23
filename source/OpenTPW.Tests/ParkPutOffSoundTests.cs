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

	[TestMethod]
	[DataRow( Riding, 35, BellyBounceFlags, true, Seat )]
	[DataRow( Riding, 35, BellyBounceFlags, false, Feet )]
	[DataRow( Riding, 35, WithoutTheBit, true, Origin )]
	[DataRow( Riding, 35, WithoutTheBit, false, Origin )]
	[DataRow( Queueing, 32, BellyBounceFlags, false, Feet )]
	[DataRow( Queueing, 8, BellyBounceFlags, false, Feet )]
	[DataRow( Queueing, 44, BellyBounceFlags, false, None )]
	[DataRow( Queueing, 29, BellyBounceFlags, false, None )]
	[DataRow( Queueing, 42, WithoutTheBit, false, None )]
	[DataRow( Heading, 32, BellyBounceFlags, false, None )]
	[DataRow( No, 32, BellyBounceFlags, false, None )]
	public void TheSoundFollowsTheArmTheIdAndTheFlag( int how, int guestId, int thingFlags, bool seated, int expected )
	{
		Assert.AreEqual( (ParkPeople.PutOffSound)expected,
			ParkPeople.SoundFor( (PeepBehaviour.PutOff)how, guestId, thingFlags, seated ) );
	}
}
