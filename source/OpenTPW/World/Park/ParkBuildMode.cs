namespace OpenTPW;

/// <summary>
/// Which build tool the player is holding, and where a run of it was anchored - the original's
/// single current-MODE global <c>DAT_0081ae2c</c> and its anchor pair <c>DAT_0081ede4</c> /
/// <c>DAT_0081ede8</c>.
///
/// <para>
/// <b>It is a MODE, not a tool, and the binary names it so itself:</b> the two functions that write
/// it record the replay actions <c>ACTION_SET_MODE</c> and <c>ACTION_SET_MODE_NR</c>. There are
/// exactly three writers in the whole image, so the id table is closed - and of those ids only two
/// matter here, <b>1 for path and 3 for queue</b>, fixed by the cursor table that registers
/// <c>c_path.ani</c> and <c>c_queue.ani</c> against them.
/// </para>
///
/// <para>
/// <b>THERE IS NO DRAG.</b> Both drag slots of the original's build-tool interaction mode are bare
/// <c>RET</c> stubs, so a run of path is <b>click to anchor, click to commit</b>: the second click
/// snaps its target to the dominant axis and the line between is laid one cell at a time. The action
/// recorder settles it from the other side - one record per click carrying a single cell, where a
/// real drag would have to record every intermediate cell or a start and an end.
/// </para>
/// </summary>
public static class ParkBuildMode
{
	/// <summary>Nothing armed - the original's mode 0, and what a committed run drops back to.</summary>
	public const int None = 0;

	/// <summary>Laying path, the original's mode 1 (cursor <c>c_path.ani</c>).</summary>
	public const int Path = 1;

	/// <summary>Laying queue, the original's mode 3 (cursor <c>c_queue.ani</c>).</summary>
	public const int Queue = ParkRideChoice.QueueCellType;

	/// <summary>Which mode is armed, or <see cref="None"/>.</summary>
	public static int Current { get; private set; }

	/// <summary>The object a queue being laid is for, or nought. A path needs none.</summary>
	public static int Serves { get; private set; }

	/// <summary>Where this run was anchored, or <b>(-1,-1)</b> for a run that has not started.</summary>
	public static (int X, int Y) Anchor { get; private set; } = (-1, -1);

	/// <summary>Whether a run is part-built, which is what makes the next click a commit rather than an anchor.</summary>
	public static bool Anchored => Anchor.X >= 0;

	/// <summary>
	/// Arms a mode, and <b>clears the anchor</b>.
	///
	/// <para>
	/// <b>Clearing it is the whole difference between the original's two setters</b>, and worth
	/// keeping: the heavyweight one resets the anchor on every call, while the light one used inside
	/// the commit handler deliberately does not - which is how the commit can swap the mode out and
	/// back without destroying the run it is halfway through.
	/// </para>
	/// </summary>
	public static string Arm( int mode, int serves = 0 )
	{
		Current = mode;
		Serves = serves;
		Anchor = (-1, -1);

		return mode switch
		{
			Path => "tool: laying path - click once to anchor, again to lay the run",
			Queue => serves == 0
				? "tool: a queue has to say which thing it serves - `tool queue <thingId>`"
				: $"tool: laying queue for thing {serves} - click once to anchor, again to lay the run",
			_ => "tool: nothing armed"
		};
	}

	/// <summary>
	/// Arms a mode with a run already anchored - the original's light setter <c>FUN_0052f580</c>, called
	/// after something else has written the anchor. It is how a player is handed the queue tool: placing a
	/// queued thing anchors it on the cell the placer laid before the entrance (<c>FUN_0052a050</c>, then
	/// <c>FUN_0052f580( 3, 0 )</c> at <c>0x0052529e</c>), and editing a queue anchors it on the queue's
	/// far end (<c>FUN_00530120</c>, then the same setter).
	/// </summary>
	public static string ArmAt( int mode, int serves, int x, int y )
	{
		Arm( mode, serves );
		Anchor = (x, y);

		return mode == Queue
			? $"tool: laying queue for thing {serves} from ({x},{y}) - click where it should run to"
			: $"tool: mode {mode} anchored at ({x},{y})";
	}

	/// <summary>Puts the mode away entirely - the original's <c>FUN_0052f200( 0, 0 )</c>.</summary>
	public static void Disarm() => Arm( None );

	/// <summary>Remembers where a run starts.</summary>
	public static void AnchorAt( int x, int y ) => Anchor = (x, y);

	/// <summary>
	/// Where a run from the anchor to a clicked cell actually ends: the larger of the two deltas wins
	/// and the other axis is forced back to the anchor's value.
	/// </summary>
	/// <remarks>
	/// <b>The anchor then advances to this snapped target rather than to wherever the run got to</b>,
	/// which is what lets a player lay an L-shaped run click by click.
	/// </remarks>
	public static (int X, int Y) SnapToAxis( int toX, int toY )
	{
		if ( !Anchored )
			return (toX, toY);

		return Math.Abs( toX - Anchor.X ) >= Math.Abs( toY - Anchor.Y )
			? (toX, Anchor.Y)
			: (Anchor.X, toY);
	}

	/// <summary>Forgets everything, for a park being torn down - these are statics and outlive a scene.</summary>
	public static void Forget()
	{
		Current = None;
		Serves = 0;
		Anchor = (-1, -1);
	}
}
