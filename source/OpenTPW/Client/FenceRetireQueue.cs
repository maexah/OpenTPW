namespace OpenTPW;

/// <summary>
/// Things to let go of once the GPU has finished the frame they were asked for in (Q853). <see cref="Renderer"/>'s deletion
/// queue runs right after a frame is presented, with the frame's commands still executing, so an image view destroyed there
/// can still be bound by a descriptor set that frame draws with (the Khronos layer's <c>VUID-vkDestroyImageView-imageView-01026</c>).
/// Here an action waits for the fence of the next frame submitted: one queue runs submissions in order, so that fence signalling
/// means every frame up to it is done.
/// </summary>
/// <typeparam name="TFence">The device's fence; the queue only asks the three delegates about it.</typeparam>
internal sealed class FenceRetireQueue<TFence>( Func<TFence> newFence, Func<TFence, bool> isSignaled, Action<TFence> reset )
	where TFence : class
{
	private readonly List<Action> _asked = [];
	private readonly Queue<(TFence Fence, List<Action> Actions)> _waiting = new();
	private readonly Stack<TFence> _spare = new();

	/// <summary>Actions let go of so far.</summary>
	public int Ran { get; private set; }

	/// <summary>Actions asked for and not run yet: those not yet handed a fence and those whose frame is not done.</summary>
	public int Pending => _asked.Count + _waiting.Sum( entry => entry.Actions.Count );

	/// <summary>Asks for <paramref name="action"/> to run once the next frame submitted has finished on the GPU.</summary>
	public void Schedule( Action action ) => _asked.Add( action );

	/// <summary>
	/// A frame is about to be submitted: the fence to submit it with, or null when nothing was asked for (almost every frame,
	/// which then makes and waits for nothing). What was asked for since the last frame waits on this fence.
	/// </summary>
	public TFence? FenceForSubmit()
	{
		if ( _asked.Count == 0 )
			return null;

		var fence = _spare.Count > 0 ? _spare.Pop() : newFence();
		_waiting.Enqueue( (fence, [.. _asked]) );
		_asked.Clear();

		return fence;
	}

	/// <summary>Runs the actions whose frame is done, oldest first, and stops at the first frame that is not; returns how many ran.</summary>
	public int Poll()
	{
		var ran = 0;

		while ( _waiting.TryPeek( out var entry ) && isSignaled( entry.Fence ) )
		{
			_waiting.Dequeue();
			reset( entry.Fence );
			_spare.Push( entry.Fence );

			foreach ( var action in entry.Actions )
				action();

			ran += entry.Actions.Count;
		}

		Ran += ran;
		return ran;
	}
}
