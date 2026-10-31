using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;

namespace OpenTPW.Tests;

/// <summary>
/// When a texture's image view is let go of (Q853): not before the GPU has finished the frame that might have it bound. The
/// device is not in these tests; that leaving a park prints no <c>VUID-vkDestroyImageView-imageView-01026</c> is <c>q853</c>'s run.
/// </summary>
/// <remarks><b>Mutations:</b> <see cref="FenceRetireQueue{TFence}.Poll"/> running an action whose fence is not signalled, or not
/// stopping at the first unsignalled fence; <see cref="FenceRetireQueue{TFence}.FenceForSubmit"/> giving a fence when nothing was
/// asked for, or leaving what was asked for in the list to be handed to the next fence too; reusing a fence without resetting it.</remarks>
[TestClass]
public class FenceRetireQueueTests
{
	private sealed class FakeFence
	{
		public bool Signaled;
		public int Resets;
	}

	private readonly List<FakeFence> _made = [];

	private FenceRetireQueue<FakeFence> Queue() => new(
		() => { var f = new FakeFence(); _made.Add( f ); return f; },
		f => f.Signaled,
		f => { f.Signaled = false; f.Resets++; } );

	[TestMethod]
	public void NothingAskedForNeedsNoFence()
	{
		var queue = Queue();

		Assert.IsNull( queue.FenceForSubmit() );
		Assert.AreEqual( 0, _made.Count );
		Assert.AreEqual( 0, queue.Poll() );
	}

	[TestMethod]
	public void AnActionWaitsForTheNextFramesFence()
	{
		var queue = Queue();
		var ran = 0;

		queue.Schedule( () => ran++ );
		Assert.AreEqual( 1, queue.Pending );
		Assert.AreEqual( 0, queue.Poll(), "no frame was submitted yet: the action has no fence to wait on, so it cannot run" );

		var fence = queue.FenceForSubmit();
		Assert.IsNotNull( fence );
		Assert.AreEqual( 0, queue.Poll(), "the frame is still executing" );
		Assert.AreEqual( 0, ran );
		Assert.AreEqual( 1, queue.Pending );

		fence.Signaled = true;
		Assert.AreEqual( 1, queue.Poll() );
		Assert.AreEqual( 1, ran );
		Assert.AreEqual( 0, queue.Pending );
		Assert.AreEqual( 1, queue.Ran );
	}

	[TestMethod]
	public void AFrameHandsItsFenceOnlyWhatWasAskedForBeforeIt()
	{
		var queue = Queue();
		var order = new List<string>();

		queue.Schedule( () => order.Add( "a" ) );
		var first = queue.FenceForSubmit()!;
		queue.Schedule( () => order.Add( "b" ) );
		queue.Schedule( () => order.Add( "c" ) );
		var second = queue.FenceForSubmit()!;

		Assert.AreNotSame( first, second );

		first.Signaled = true;
		Assert.AreEqual( 1, queue.Poll() );
		CollectionAssert.AreEqual( new[] { "a" }, order, "the second frame's fence is still unsignalled: b and c wait" );

		second.Signaled = true;
		Assert.AreEqual( 2, queue.Poll() );
		CollectionAssert.AreEqual( new[] { "a", "b", "c" }, order );
	}

	[TestMethod]
	public void ALaterFrameDoneDoesNotLetAnEarlierFramesActionsOutOfOrder()
	{
		var queue = Queue();
		var order = new List<string>();

		queue.Schedule( () => order.Add( "a" ) );
		var first = queue.FenceForSubmit()!;
		queue.Schedule( () => order.Add( "b" ) );
		var second = queue.FenceForSubmit()!;

		second.Signaled = true;
		Assert.AreEqual( 0, queue.Poll(), "the queue runs in submission order: the oldest frame is not done, so nothing runs" );
		Assert.AreEqual( 0, order.Count );

		first.Signaled = true;
		Assert.AreEqual( 2, queue.Poll() );
		CollectionAssert.AreEqual( new[] { "a", "b" }, order );
	}

	[TestMethod]
	public void ASignalledFenceIsResetAndUsedAgain()
	{
		var queue = Queue();

		queue.Schedule( () => { } );
		var fence = queue.FenceForSubmit()!;
		fence.Signaled = true;
		queue.Poll();

		Assert.AreEqual( 1, fence.Resets );
		Assert.IsFalse( fence.Signaled, "a fence put back is unsignalled, or the next frame's actions would run at once" );

		queue.Schedule( () => { } );
		Assert.AreSame( fence, queue.FenceForSubmit() );
		Assert.AreEqual( 1, _made.Count, "no second fence was made" );
	}

	[TestMethod]
	public void AnActionAskedForWhileAnotherWaitsKeepsItsOwnFrame()
	{
		var queue = Queue();
		var ran = new List<int>();

		queue.Schedule( () => ran.Add( 1 ) );
		var first = queue.FenceForSubmit()!;
		queue.Schedule( () => ran.Add( 2 ) );

		first.Signaled = true;
		queue.Poll();

		CollectionAssert.AreEqual( new[] { 1 }, ran, "2 was asked for after the first frame was submitted, so it has no fence yet" );
		Assert.AreEqual( 1, queue.Pending );
	}
}
