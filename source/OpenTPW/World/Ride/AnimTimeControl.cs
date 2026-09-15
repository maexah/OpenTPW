namespace OpenTPW;

/// <summary>
/// One animation player on a thing's model: which role and entry it is running, when it started, and what
/// is queued behind it.
///
/// <para>
/// <b>The name is the original's own.</b> <c>FUN_00464580</c> is a debug dumper that prints this record
/// field by field - "interesting AnimTimeControl %d/%d to ride instance %s" - and names every one of them:
/// <c>Flags</c>, <c>AnimID</c>, <c>SubAnim</c>, <c>StartAnimTime</c>, <c>AnimTime</c>,
/// <c>NoPauseAnimTime</c>, <c>TotalAnimFrames</c>, <c>AnimFrame</c>, <c>DeferredAnimID</c>,
/// <c>DeferredSubAnim</c> and <c>DeferredFlags</c>. It works its own index out as
/// <c>(channel - model[+0x10]) / 0x38</c>, so the record's size is confirmed by the executable's
/// arithmetic rather than inferred from the fields that were found. The names below are those names.
/// </para>
///
/// <para>
/// <b>A model has an array of these, not one.</b> The count is <c>model+0x0e</c> and the array
/// <c>model+0x10</c>, and the count is not model data at all - it is an argument to the loader
/// (<c>FUN_00461f10</c>), which eight call sites pass 1, ride vehicles pass 5, and the main thing path
/// takes from the thing's own record. The Jungle Spray runs three at once. <b>Nothing in the engine
/// bounds-checks a channel index</b>, so an out-of-range one writes past the allocation; this refuses it
/// instead, which is the one place here that deliberately does not reproduce what the original does.
/// </para>
///
/// <para>
/// <b>Time is a stamp, never an accumulator, and that is load-bearing.</b> The engine keeps the moment the
/// clip started and subtracts it from the moment it is asking about, so continuity across a clip change is
/// expressed by <i>moving the start stamp backwards</i> (<c>FUN_00472bc0</c>): a clip triggered onto a
/// channel that had already overrun begins as far into itself as the old one overshot. A player that
/// accumulated a delta per frame could not express that at all, and would silently drop the overshoot at
/// every transition.
/// </para>
/// </summary>
public sealed class AnimTimeControl
{
	/// <summary>
	/// The elapsed-frames factor, <c>_DAT_006fec0c</c> - written out to the digit because it is
	/// <b>not</b> a thirtieth. Reproducing it as <c>1f / 30f</c> would drift a frame over a long clip.
	/// </summary>
	private const float FramesPerMillisecond = 0.029999999329447746f;

	/// <summary>Loop when the clip ends, rather than holding its last frame - the engine's flag <c>0x1</c>.</summary>
	public const int LoopFlag = 0x1;

	/// <summary>
	/// Start at once and empty the queue - the engine's flag <c>0x2</c>. It is the only path that reaches
	/// the queue-clearing block in <c>FUN_004732a0</c>, which is why "start now" and "forget what was
	/// queued" are one flag rather than two.
	/// </summary>
	public const int StartAtOnceFlag = 0x2;

	/// <summary>
	/// Do not lay the rest pose down on the way in - the engine's <c>0x4</c>, tested as part of
	/// <c>flags &amp; 0xc</c> before it calls the restore.
	/// </summary>
	public const int KeepPoseFlag = 0x4;

	/// <summary>
	/// Do not apply the clip's hide list - the engine's <c>0x8</c>. The idle default passes it, which is
	/// how a model restarting its own <c>M</c> clip avoids putting away nodes an earlier clip revealed.
	/// </summary>
	public const int KeepShownFlag = 0x8;

	/// <summary>
	/// <b>Freeze on frame nought.</b> Not a role: the engine reserves 13 and 14 as instructions to the
	/// channel itself, handled before any role slot is looked at (<c>FUN_00472f60</c>, <c>0x00472fdd</c>).
	/// </summary>
	public const int FreezeAtStart = 13;

	/// <summary>
	/// <b>Hold the last frame.</b> This is how the engine expresses "the animation has finished": rather
	/// than marking a channel done, it re-enters with role 14, which parks the timebase so that the elapsed
	/// frame lands exactly on the total. That is why "finished" is <c>total &lt; elapsed</c>
	/// <b>strictly</b> - a clip pinned at its end is still playing, and is posed once more there.
	/// </summary>
	public const int HoldAtEnd = 14;

	/// <summary>The engine's <c>Flags</c> - see the <c>...Flag</c> constants, plus 0x10 and 0x20 it sets itself.</summary>
	public int Flags { get; private set; }

	/// <summary>The engine's <c>AnimID</c>: which of the twelve roles is playing, or 12 for none.</summary>
	public int AnimID { get; private set; } = RideAnimations.NoRole;

	/// <summary>The engine's <c>SubAnim</c>: which entry of that role.</summary>
	public int SubAnim { get; private set; }

	/// <summary>How fast it plays. 1.0 is normal, and the engine seeds every idle restart with exactly that.</summary>
	public float Speed { get; private set; } = 1f;

	/// <summary>The engine's <c>StartAnimTime</c>: the millisecond the clip began, moved backwards to carry an overshoot.</summary>
	public int StartAnimTime { get; private set; }

	/// <summary>The engine's <c>AnimTime</c>: the millisecond this channel has been advanced to.</summary>
	public int AnimTime { get; private set; }

	/// <summary>
	/// The engine's <c>NoPauseAnimTime</c>. It tracks the clock even where <see cref="AnimTime"/> is pinned
	/// by a freeze, so it is the only field that still measures real elapsed time across one - which is why
	/// it is kept rather than dropped as a duplicate. Read at <c>0x004646a1</c>.
	/// </summary>
	public int NoPauseAnimTime { get; private set; }

	/// <summary>The engine's <c>TotalAnimFrames</c>: the span the clip declares, as a float.</summary>
	public float TotalAnimFrames { get; private set; }

	/// <summary>The engine's <c>AnimFrame</c>: how far in this channel has reached, in frames.</summary>
	public float AnimFrame { get; private set; }

	/// <summary>The engine's <c>DeferredAnimID</c>: the role waiting to start when this clip ends, or 12.</summary>
	public int DeferredAnimID { get; private set; } = RideAnimations.NoRole;

	/// <summary>The engine's <c>DeferredSubAnim</c>.</summary>
	public int DeferredSubAnim { get; private set; }

	/// <summary>
	/// The engine's <c>DeferredFlags</c>. <b>It is deliberately left stale when the queue is emptied</b> -
	/// the promotion at <c>0x004738a3</c> resets the role, the entry and the speed and pointedly does not
	/// touch this one, so a later queued clip inherits the flags of the one before it unless it sets its
	/// own. Clearing it here would be a behaviour change dressed up as tidying.
	/// </summary>
	public int DeferredFlags { get; private set; }

	/// <summary>The speed the queued clip will play at - the engine's <c>+0x30</c>, which its dumper does not name.</summary>
	public float DeferredSpeed { get; private set; }

	/// <summary>Whether this channel is running nothing at all.</summary>
	public bool IsIdle => AnimID == RideAnimations.NoRole;

	/// <summary>
	/// Whether the clip has run past its end. <b>Strictly greater</b>, so a channel sitting exactly on its
	/// last frame has not finished - see <see cref="HoldAtEnd"/> for why the engine needs that to be true.
	/// </summary>
	public bool IsFinished => TotalAnimFrames < AnimFrame;

	/// <summary>
	/// Whether a trigger would have to wait for this channel rather than taking it over: the engine's
	/// six-clause test at <c>0x004732c2</c>, minus the two clauses about the incoming role, which
	/// <see cref="RideAnimations"/> applies because they are questions about the role table.
	/// </summary>
	public bool IsBusy => !IsIdle && !IsFinished && (Flags & 0x6) == 0;

	/// <summary>
	/// Moves this channel to <paramref name="now"/> and works out how far into its clip that is.
	///
	/// <para>
	/// <b>The subtraction is unsigned</b>, exactly as the engine's is: it builds a qword whose high half is
	/// explicitly zeroed before converting. A start stamp that sits in the future - which is what the
	/// overshoot carry produces - therefore reads as an enormous positive elapsed rather than a negative
	/// one, and that is what makes the backdate work rather than wrap.
	/// </para>
	///
	/// <para>
	/// A frozen channel (flags <c>0x2</c> or <c>0x4</c>) does not advance: the engine pins
	/// <see cref="AnimTime"/> to the start for a freeze and backdates it so the elapsed frame lands on the
	/// total for a hold, while letting <see cref="NoPauseAnimTime"/> follow the clock either way.
	/// </para>
	/// </summary>
	public void MoveTo( int now )
	{
		if ( (Flags & 0x6) == 0 )
		{
			AnimTime = now;
			NoPauseAnimTime = now;
			AnimFrame = (uint)(AnimTime - StartAnimTime) * Speed * FramesPerMillisecond;

			return;
		}

		NoPauseAnimTime = now;

		if ( (Flags & 0x2) != 0 )
		{
			// Frozen on frame nought.
			AnimTime = StartAnimTime;
			AnimFrame = 0f;

			return;
		}

		// Held on the last frame: the start stamp is pushed back by the clip's whole length, so the
		// arithmetic above would land exactly on the total.
		AnimTime = StartAnimTime - MillisecondsFor( TotalAnimFrames, Speed );
		AnimFrame = TotalAnimFrames;
	}

	/// <summary>
	/// Begins <paramref name="role"/> entry <paramref name="entry"/> on this channel.
	/// <paramref name="frames"/> is the span the clip declares, and nought where there is no clip - which
	/// is a real case, not a guard: the engine parks the channel at role 12 and answers nothing.
	/// </summary>
	/// <param name="carry">
	/// How far past its end the outgoing clip had run, in frames, for the new one to start that far in. The
	/// engine clamps it to the new clip's own length, so a channel left alone for a minute does not skip a
	/// short clip entirely.
	/// </param>
	public void Start( int role, int entry, int flags, float speed, int now, float frames, float carry = 0f )
	{
		Flags = (flags & LoopFlag) != 0 ? Flags | LoopFlag : Flags & ~LoopFlag;
		Flags = (flags & KeepPoseFlag) != 0 ? Flags | 0x20 : Flags & ~0x20;

		if ( role is FreezeAtStart or HoldAtEnd )
		{
			// The pseudo-roles say what to do with the clip already loaded rather than naming one.
			Flags |= role == FreezeAtStart ? 0x2 : 0x14;

			// Both keep the start stamp at the moment of the freeze and move the OTHER end: a freeze puts
			// the clip's own time there too, and a hold pushes it a whole clip into the past so that the
			// elapsed frame works out at exactly the total.
			StartAnimTime = now;
			NoPauseAnimTime = now;

			if ( role == FreezeAtStart )
			{
				AnimTime = now;
				AnimFrame = 0f;
			}
			else
			{
				AnimTime = now - MillisecondsFor( TotalAnimFrames, Speed );
				AnimFrame = TotalAnimFrames;
			}

			return;
		}

		// A clip the model does not carry stops the channel rather than leaving it running - the engine
		// restores the rest pose and writes the sentinel, and answers nothing at all.
		if ( frames <= 0f )
		{
			AnimID = RideAnimations.NoRole;
			SubAnim = 0;
			Speed = 1f;
			StartAnimTime = 0;
			AnimTime = 0;
			NoPauseAnimTime = 0;
			TotalAnimFrames = 0f;
			AnimFrame = 0f;

			return;
		}

		Flags &= ~0x6;

		SubAnim = entry;
		AnimID = role;
		Speed = speed;
		TotalAnimFrames = frames;

		if ( carry <= 0f )
		{
			StartAnimTime = now;
			AnimTime = now;
			NoPauseAnimTime = now;
		}
		else
		{
			// Clamped to this clip's own length before it is turned back into milliseconds, so the most an
			// overshoot can do is start the new clip at its own end.
			var into = MillisecondsFor( MathF.Min( carry, TotalAnimFrames ), Speed );

			AnimTime = now;
			StartAnimTime = now - into;
			NoPauseAnimTime = now - into;
		}

		AnimFrame = (uint)(AnimTime - StartAnimTime) * Speed * FramesPerMillisecond;
	}

	/// <summary>
	/// Puts a clip behind the one playing. <b>Four fields and nothing else</b> - the engine writes the
	/// queue without touching the running clip's role, timing or flags, so a trigger onto a busy channel
	/// cannot disturb what it is waiting for.
	/// </summary>
	public void Queue( int role, int entry, int flags, float speed )
	{
		DeferredFlags = flags;
		DeferredAnimID = role;
		DeferredSubAnim = entry;
		DeferredSpeed = speed;
	}

	/// <summary>
	/// Empties the queue - the role, the entry and the speed, leaving <see cref="DeferredFlags"/> alone
	/// because the engine does.
	/// </summary>
	public void ClearQueue()
	{
		DeferredAnimID = RideAnimations.NoRole;
		DeferredSubAnim = 0;
		DeferredSpeed = 0f;
	}

	/// <summary>Whether something is waiting behind the clip that is playing.</summary>
	public bool HasQueued => DeferredAnimID != RideAnimations.NoRole;

	/// <summary>
	/// How much of the running clip is still to come, in milliseconds, truncated as the engine truncates.
	/// <b>It is not divided by <see cref="Speed"/></b>, though the engine divides by it everywhere else -
	/// <c>0x0047337b</c> multiplies and converts with no <c>FDIV</c> between them. That is a shipped
	/// inconsistency, and dividing here would change every wait computed on a channel running at anything
	/// other than normal speed.
	/// </summary>
	public int RemainingMilliseconds()
		=> MillisecondsFor( TotalAnimFrames - AnimFrame );

	/// <summary>
	/// Frames to milliseconds the engine's way: times the float at <c>0x006fec08</c>, divided by the
	/// channel's speed, then truncated toward zero by <c>__ftol</c>, which sets the rounding mode itself
	/// rather than trusting the one in force. See <see cref="AnimationFile.MillisecondsPerFrame"/>.
	///
	/// <para>
	/// <b>The default of 1 is not a convenience.</b> Every conversion the engine makes about <i>when</i>
	/// something happens divides by the speed - the freeze, the hold and the overshoot carry all do
	/// (<c>FMUL</c> then <c>FDIV [ESI+0xc]</c>) - but <see cref="RemainingMilliseconds"/> does not, and
	/// takes this default so that the omission is visible at the call site rather than hidden here.
	/// </para>
	/// </summary>
	private static int MillisecondsFor( float frames, float speed = 1f )
		=> frames <= 0f || speed <= 0f ? 0 : (int)(frames * AnimationFile.MillisecondsPerFrame / speed);
}
