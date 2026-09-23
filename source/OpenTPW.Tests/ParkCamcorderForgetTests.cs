using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Runtime.CompilerServices;

namespace OpenTPW.Tests;

/// <summary>
/// Leaving a park lets go of the edge test the camcorder walked it with - <see cref="Level.ForgetPark"/>,
/// through <see cref="ParkCamcorderCameraMode.Forget"/>. The edge test holds its park's save, and the next
/// park replaces it only at its first step on the ground, so without this a park left after a walk stays in
/// memory for as long as the next one goes unwalked.
///
/// <para>
/// <b>The question is asked of the garbage collector</b>, not of the fields: the edge test's delegate holds
/// the save through the closures <see cref="CellEdge.For"/> builds, so a field set to null proves nothing
/// about whether the save can go. The save is a stand-in, <c>new ParkWorld( [] )</c>, which the cache keys
/// on by identity alone, so these need no installation.
/// </para>
/// <para>
/// <c>Step</c> handing <see cref="ParkCamcorderCameraMode.EdgeTest"/> the park on show is
/// <see cref="ParkCamcorderWalkTests.TheWalkStopsAtARidesFootprint"/>'s, which walks with a stand-in level.
/// </para>
/// </summary>
[TestClass]
public class ParkCamcorderForgetTests
{
	[TestCleanup]
	public void ForgetTheWalk() => ParkCamcorderCameraMode.Forget();

	/// <summary>
	/// <b>A park walked in and then left is collected once the next park is up</b>, before anyone walks in
	/// that one - and the next park's first walk builds an edge test of its own.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> <see cref="ParkCamcorderCameraMode.Forget"/> keeping the park it was built for, or
	/// keeping the delegate, or <see cref="Level.ForgetPark"/> not calling it at all, each keep the first park
	/// alive.
	/// </remarks>
	[TestMethod]
	public void LeavingTheParkLetsGoOfTheSaveTheCamcorderWalkedIn()
	{
		var first = WalkIn();

		Collect();
		Assert.IsTrue( first.IsAlive, "the control: the camcorder holds the park it has walked in" );

		Level.ForgetPark();

		var second = new ParkWorld( [] );

		Collect();
		Assert.IsFalse( first.IsAlive, "the park left is held by nothing of the camcorder's" );

		Assert.IsNotNull( ParkCamcorderCameraMode.EdgeTest( second ), "and the next park's walk builds its own" );
		Assert.AreSame( second, ParkCamcorderCameraMode.EdgeTestPark );
	}

	/// <summary>
	/// A park, walked in, known only by a weak reference. Not inlined, and so its own frame: a debug build
	/// keeps a method's locals alive until it returns, which would keep the park alive whatever the camcorder
	/// did.
	/// </summary>
	[MethodImpl( MethodImplOptions.NoInlining )]
	private static WeakReference WalkIn()
	{
		var park = new ParkWorld( [] );

		Assert.IsNotNull( ParkCamcorderCameraMode.EdgeTest( park ), "walking in builds the park's edge test" );

		return new WeakReference( park );
	}

	private static void Collect()
	{
		GC.Collect();
		GC.WaitForPendingFinalizers();
		GC.Collect();
	}
}
