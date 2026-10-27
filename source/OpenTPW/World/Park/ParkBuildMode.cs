namespace OpenTPW;

/// <summary>
/// Which build tool the player is holding, and where a run of it was anchored - the original's
/// single current-MODE global <c>DAT_0081ae2c</c> and its anchor pair <c>DAT_0081ede4</c> /
/// <c>DAT_0081ede8</c>.
///
/// <para>
/// <b>It is a MODE, not a tool, and the binary names it so itself:</b> the two functions that write
/// it record the replay actions <c>ACTION_SET_MODE</c> and <c>ACTION_SET_MODE_NR</c>. There are
/// exactly three writers in the whole image, so the list of what writes the mode is closed - and of its values three
/// are built here, <b>1 for path and 3 for queue</b>, fixed by the cursor table that registers
/// <c>c_path.ani</c> and <c>c_queue.ani</c> against them, and <b><c>0x36</c> for the patrol tool</b>.
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

	/// <summary>Fencing a member of staff into a patrol area, the original's mode <c>0x36</c> (<see cref="ParkPatrolTool"/>).</summary>
	public const int Patrol = 0x36;

	/// <summary>Laying a kart ride's track, the original's mode <c>0xb</c> (<see cref="ParkTrackTool"/>).</summary>
	public const int KartTrack = 0xb;

	/// <summary>Laying a water ride's track, the original's mode <c>0x10</c> (<see cref="ParkTrackTool"/>).</summary>
	public const int WaterTrack = 0x10;

	/// <summary>Whether a mode is one of the two track tools.</summary>
	public static bool IsTrack( int mode ) => mode is KartTrack or WaterTrack;

	/// <summary>Which mode is armed, or <see cref="None"/>.</summary>
	public static int Current { get; private set; }

	/// <summary>The object a queue being laid is for, or the member a patrol area is for, or nought. A path needs none.</summary>
	public static int Serves { get; private set; }

	/// <summary>Where this run was anchored, or <b>(-1,-1)</b> for a run that has not started.</summary>
	public static (int X, int Y) Anchor { get; private set; } = (-1, -1);

	/// <summary>Whether a run is part-built, which is what makes the next click a commit rather than an anchor.</summary>
	public static bool Anchored => Anchor.X >= 0;

	/// <summary>
	/// The refusal latch as the last tool's pass left it - the original's <c>DAT_00816d50</c>. Each pass clears it first
	/// (<c>FUN_005234d0</c>, <c>0x005234f2</c>) and its verdicts set it; putting the tool away leaves it
	/// (<c>FUN_0052f200( 0 )</c> clears the placer's <c>DAT_0081b604</c> and <c>d48</c>, never <c>d50</c>), so a followed
	/// member's patrol edges are red after a refused click until the next tool is installed or passes
	/// (<see cref="Arm"/>, <see cref="ParkBuilding.Hold"/>, <see cref="ParkBuildMarkers.QueuePatrolEdges"/>; a park's
	/// start clears it, <c>FUN_0052f050</c>). docs/exe/hud.md, "The patrol edges".
	/// </summary>
	public static bool RefusalLatched { get; set; }

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
		// A change of mode withdraws ten advisor messages (FUN_0052f200, 0x0052f245..0x0052f326); of them only the
		// patrol tool's 0xc1 is posted here, so it is the one withdrawn, which cuts the advisor off where he says it.
		if ( mode != Current && Level.Current?.ParkState?.AdvisorMessages is { } lines )
			lines.Withdraw( ParkAdvisorMessages.PatrolTool, (long)(GameClock.Now * 1000), () => Advisor.Current?.StopSpeaking() );

		// Installing a tool runs a pass under the old mode first, which clears the latch (FUN_0046d5a0, 0x0046d5e8);
		// putting a tool away installs the idle handler, which runs none. The original's install is followed by hover
		// passes over the pointer's cell, whose verdicts can set it again before the click lands; here the latch is
		// not taken from RunPath's own pass over the clicked cell (Q535), so an empty-hand click on one unaffordable cell
		// reads clear where the original reads set.
		if ( mode != None )
			RefusalLatched = false;

		// Putting the tools away writes 0x80 to the track tool's last link step (FUN_0052f200, DAT_00763b00).
		if ( mode == None )
			ParkTrackTool.Forget();

		// Arming the patrol tool over itself sets no cursor in the original (FUN_0052f200 sets one only for modes 3 and
		// 1), so the pointer's refusal stays; any other Arm clears it. Not watched in the original's running game (Q537).
		if ( mode != Patrol || Current != Patrol )
			ParkPatrolTool.PointerRefused = false;

		Current = mode;
		Serves = serves;
		Anchor = (-1, -1);

		return mode switch
		{
			Path => "tool: laying path - click once to anchor, again to lay the run",
			Patrol => $"tool: fencing thing {serves} - click one corner of the patrol area, then the other",
			KartTrack or WaterTrack => $"tool: laying track for thing {serves}",
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

		return mode switch
		{
			Queue => $"tool: laying queue for thing {serves} from ({x},{y}) - click where it should run to",
			KartTrack or WaterTrack => $"tool: laying track (mode 0x{mode:x}) for thing {serves} from ({x},{y}) - click where it should run to",
			_ => $"tool: mode {mode} anchored at ({x},{y})"
		};
	}

	/// <summary>Puts the mode away entirely - the original's <c>FUN_0052f200( 0, 0 )</c>.</summary>
	public static void Disarm() => Arm( None );

	/// <summary>
	/// The commit's refused click: sound <c>0xaf</c> (<see cref="UI.UiSounds.Refused"/>), played once where the preview
	/// flagged a red square, ahead of the commit's mode switch (<c>0x00524a63</c>..<c>0x00524aae</c>), so a placement,
	/// the path tool, the queue tool and the patrol tool share it. It is gated on the flag the mode's button-down slot
	/// sets (<c>DAT_008186d8</c>, read at <c>0x00524a77</c>); a press beside an open park screen never calls that slot,
	/// so the click is refused there in silence.
	/// </summary>
	/// <returns>What the click's answer says of the sound.</returns>
	public static string RefusedSound( bool pressReachedTheTool )
		=> !pressReachedTheTool ? "no sound, the press went to a park screen"
			: UI.UiSounds.Refused() ? "sound 0xaf, a voice" : "sound 0xaf, no voice";

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

	/// <summary>
	/// The ends of the runs laid since the tool anchored, oldest first - the original's pending list at
	/// <c>0x0081b740</c>, count <c>DAT_00820a8c</c>. The first click pushes its cell and every run pushes
	/// its snapped target before it is laid, so while the tool is live the top is the anchor, and Backspace
	/// undoes one run by popping it (<c>FUN_0052fe50</c>).
	/// </summary>
	/// <remarks>
	/// The original's push refuses only past <c>0x400</c> entries, so one entry more lands on the next
	/// global; this refuses at 1,024 exactly.
	/// </remarks>
	public static IReadOnlyList<(int X, int Y)> Pending => _pending;

	private static readonly List<(int X, int Y)> _pending = [];

	private const int PendingLimit = 0x400;

	/// <summary>How many runs have been laid since the tool anchored - the advisor speaks at the third (<c>DAT_008186dc</c>).</summary>
	public static int Clicks { get; set; }

	public static void Push( int x, int y )
	{
		if ( _pending.Count < PendingLimit )
			_pending.Add( (x, y) );
	}

	public static void ClearPending() => _pending.Clear();

	/// <summary>
	/// Takes the last run off the list: its far end, and the end it started from, which becomes the anchor
	/// again. False when only the tool's first cell is left - that one is never popped.
	/// </summary>
	public static bool TryPopRun( out (int X, int Y) end, out (int X, int Y) start )
	{
		end = start = (-1, -1);

		if ( _pending.Count < 2 )
			return false;

		end = _pending[^1];
		_pending.RemoveAt( _pending.Count - 1 );
		start = _pending[^1];

		return true;
	}

	/// <summary>Forgets everything, for a park being torn down - these are statics and outlive a scene.</summary>
	public static void Forget()
	{
		Current = None;
		Serves = 0;
		Anchor = (-1, -1);
		Clicks = 0;
		RefusalLatched = false;
		ParkPatrolTool.PointerRefused = false;
		ParkTrackTool.Forget();
		_pending.Clear();
	}
}
