using NeoVeldrid;

namespace OpenTPW;

partial class Renderer
{
	private List<Action> _deletionQueue = [];

	private FenceRetireQueue<Fence>? _retireQueueField;

	private FenceRetireQueue<Fence> _retireQueue => _retireQueueField ??= new(
		() => Device.ResourceFactory.CreateFence( false ),
		fence => fence.Signaled,
		fence => Device.ResetFence( fence ) );

	public void ScheduleDelete( Action action )
	{
		_deletionQueue.Add( action );
	}

	/// <summary>
	/// What <see cref="ScheduleDelete"/> cannot be trusted with: a texture, which NeoVeldrid does not hold for a frame still executing
	/// on the GPU. Runs <paramref name="action"/> once the next frame submitted has finished (<see cref="FenceRetireQueue{TFence}"/>).
	/// </summary>
	public void ScheduleRetire( Action action )
	{
		_retireQueue.Schedule( action );
	}

	/// <summary>The retire queue's counts, for the console's <c>mem</c>.</summary>
	internal (int Ran, int Pending) RetireCounts => (_retireQueue.Ran, _retireQueue.Pending);

	private void ProcessDeletionQueue()
	{
		foreach ( var item in _deletionQueue )
		{
			item.Invoke();
		}

		_deletionQueue.Clear();
		_retireQueue.Poll();
	}
}
