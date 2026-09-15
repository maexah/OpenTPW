namespace OpenTPW;

/// <summary>
/// The animations a thing's model can play, in the twelve roles the engine gives every model.
///
/// <para>
/// <b>An animation id is a role, not a file.</b> A model record carries exactly twelve slots, and
/// <c>TRIGANIM</c>'s first operand picks one of them. Each slot is a letter, and the letter names files
/// sitting beside the model: ids 0 to 11 are <c>C D I L S M E U W B R O</c>, read from a twelve-entry
/// table at <c>0x006fe6bc</c> that the model loader (<c>FUN_00461f10</c>) walks in step with the slots.
/// <b>12 is the sentinel for "no animation"</b>, which is what the engine writes into a slot it clears.
/// </para>
///
/// <para>
/// <b>The obvious reading is refuted.</b> An id is not an index into the numbered <c>&lt;stem&gt;M&lt;n&gt;.md2</c>
/// clips an item happens to ship: of the 72 ride archives whose script names a literal id, every one uses
/// an id at or above the number of such files it ships, and two ship none at all while still triggering
/// ids 0 and 5. <c>M</c> is simply the letter of one role - id 5 - the one whose files carry numbers.
/// </para>
///
/// <para>
/// <b>Numbered first, bare only as a fallback - and that ordering is load-bearing.</b> The engine tries
/// <c>&lt;stem&gt;&lt;letter&gt;&lt;n&gt;.md2</c> from 1 upwards and only falls back to the unnumbered
/// <c>&lt;stem&gt;&lt;letter&gt;.md2</c> when the numbered attempt failed with nothing yet loaded, in which
/// case the role holds exactly one entry. Listing the archive instead would look equivalent and is not:
/// <c>jungle/mamfount</c> ships <c>mamfountm.md2</c> <i>and</i> <c>mamfountm1/m2.md2</c>, so the engine
/// loads two entries there and never reaches the bare file. One archive in the whole game separates the
/// two readings, and a listing-based loader would shift every <c>&lt;parameter&gt;</c> index on it.
/// </para>
///
/// <para>
/// <b>Clips are read directly rather than through <see cref="AnimationFile.TryLoad"/>.</b> That one turns
/// down a clip carrying no morph, rotation or UV track - and 114 of the game's clips are exactly that,
/// carrying position and visibility only. Every one of them declares a real length, some of them long:
/// the ferries run 600 frames, twenty seconds. Loading them the fussy way would silently drop the longest
/// animations in the game and tell their scripts 300ms instead.
/// </para>
/// </summary>
public sealed class RideAnimations
{
	/// <summary>How many roles a model has. The engine's loader walks exactly this many slots.</summary>
	public const int RoleCount = 12;

	/// <summary>
	/// The id meaning "no animation" - the engine's own sentinel, which is <see cref="RoleCount"/> itself
	/// and is what every handler tests for before it touches a slot.
	/// </summary>
	public const int NoRole = RoleCount;

	/// <summary>
	/// The letter each role's files are named with, in id order. Read from the table at <c>0x006fe6bc</c>,
	/// and confirmed against shipped data: every role the eleven things Lost Kingdom places ever name is
	/// shipped by that item's own archive under exactly this letter.
	/// </summary>
	private const string Letters = "CDILSMEUWBRO";

	/// <summary>The clips loaded for each role, in the order the engine numbers them.</summary>
	private readonly AnimationFile[][] _roles;

	/// <summary>Where these came from, for the log and for anything wanting to say so.</summary>
	public string Stem { get; }

	/// <summary>How many clips were loaded across all twelve roles.</summary>
	public int Loaded { get; }

	/// <summary>How many roles hold at least one clip.</summary>
	public int Roles { get; }

	private RideAnimations( string stem, AnimationFile[][] roles )
	{
		Stem = stem;
		_roles = roles;

		foreach ( var role in roles )
		{
			if ( role.Length == 0 )
				continue;

			++Roles;
			Loaded += role.Length;
		}
	}

	/// <summary>The letter a role's files are named with, or nought where the id names no role.</summary>
	public static char LetterFor( int role )
		=> role >= 0 && role < RoleCount ? Letters[role] : '\0';

	/// <summary>How many clips a role holds - what the engine bounds an entry index against.</summary>
	public int EntryCount( int role )
		=> role >= 0 && role < RoleCount ? _roles[role].Length : 0;

	/// <summary>
	/// How long one entry of one role runs, in milliseconds, or nought where that role and entry name
	/// nothing.
	///
	/// <para>
	/// <b>The length is the one the file declares, not the one its keys happen to span.</b> The engine
	/// reads the clip's own frame bounds out of its animation block and multiplies by the float at
	/// <c>0x006fec08</c>, then <b>truncates</b> (<c>0x004733b1</c>-<c>0x004733db</c>) - which is not the
	/// same as dividing by 30, and see <see cref="AnimationFile.MillisecondsPerFrame"/>. Those two
	/// disagree far more often than they look like they would: across the levels 129 clips declare a
	/// longer span than their keys cover and 30 declare a shorter one, so computing it from keys would be
	/// wrong in both directions - see <see cref="AnimationFile.DeclaredLastFrame"/>.
	/// </para>
	/// </summary>
	public int DurationMilliseconds( int role, int entry )
	{
		if ( role < 0 || role >= RoleCount )
			return 0;

		var clips = _roles[role];

		if ( entry < 0 || entry >= clips.Length )
			return 0;

		var clip = clips[entry];
		var frames = clip.DeclaredLastFrame - clip.DeclaredFirstFrame;

		// Multiplied by the engine's own float and truncated, not divided - see
		// AnimationFile.MillisecondsPerFrame for why those are different numbers on a quarter of the
		// game's clips.
		return frames <= 0 ? 0 : (int)(frames * AnimationFile.MillisecondsPerFrame);
	}

	/// <summary>
	/// How long one entry of one role runs in frames, as the channel measures it, or nought where that role
	/// and entry name nothing at all - which the engine treats as an instruction to stop rather than as an
	/// error. See <see cref="DurationMilliseconds"/> for why the span is the declared one.
	/// </summary>
	public float FramesFor( int role, int entry )
	{
		if ( role < 0 || role >= RoleCount )
			return 0f;

		var clips = _roles[role];

		if ( entry < 0 || entry >= clips.Length )
			return 0f;

		var clip = clips[entry];

		return MathF.Max( clip.DeclaredLastFrame - clip.DeclaredFirstFrame, 0 );
	}

	/// <summary>
	/// The animation players this model carries - the engine's array at <c>model+0x10</c>.
	///
	/// <para>
	/// <b>There is one, and the number is ours rather than the engine's.</b> The original takes its channel
	/// count as an argument to the model loader: eight call sites pass 1, ride vehicles pass 5, and the main
	/// thing path reads it out of the thing's own record. Nothing in the file says it, so there is nothing
	/// here to read it from. One is what every instruction this interpreter implements uses - <c>TRIGANIM</c>,
	/// <c>WAITANIM</c> and <c>LOOPANIM</c> all pass a literal nought - and the <c>_CH</c> variants that vary
	/// it are still counted as unimplemented, so widening this now would be a guess in front of a need.
	/// </para>
	/// </summary>
	private readonly AnimTimeControl[] _channels = [new AnimTimeControl()];

	/// <summary>How many players this model has - the engine's <c>model+0x0e</c>.</summary>
	public int ChannelCount => _channels.Length;

	/// <summary>
	/// One player, or null where the index names none. <b>The engine does not check this</b> - every
	/// function that indexes the array multiplies and dereferences, so an out-of-range channel writes past
	/// the allocation. Refusing is the one deliberate departure here, because reproducing it would be a
	/// reading of the bytes rather than of the engine.
	/// </summary>
	public AnimTimeControl? Channel( int index )
		=> index >= 0 && index < _channels.Length ? _channels[index] : null;

	/// <summary>
	/// Starts <paramref name="role"/> entry <paramref name="entry"/>, or queues it behind whatever is
	/// already running, and answers how long the caller must wait in milliseconds -
	/// <c>FUN_004732a0</c> whole.
	///
	/// <para>
	/// <b>A trigger does not always start anything.</b> The channel is taken over only when it is idle,
	/// finished, frozen, running a clip its own model no longer carries, or when the caller passes
	/// <see cref="AnimTimeControl.StartAtOnceFlag"/>. Otherwise the clip is put in the queue and the answer
	/// is <b>the time still to run on the current clip plus the length of the new one</b>, each truncated
	/// separately - which is why a script that triggers twice in a row is told a longer time the second
	/// time, and why that number stops being <see cref="DurationMilliseconds"/> the moment a park is
	/// running.
	/// </para>
	///
	/// <para>
	/// A role or entry the model does not carry answers a flat second, and the engine reaches that answer by
	/// two different routes: the queue path adds 1000 outright, and the start path stops the channel and
	/// answers nothing, which <c>FUN_004732a0</c>'s tail turns into 1000.
	/// </para>
	/// </summary>
	public int Trigger( int role, int entry, int flags, float speed, int now, int index = 0 )
	{
		if ( Channel( index ) is not { } channel )
			return UnknownLength;

		// The engine advances every channel once per frame from a snapshot of the game clock, outside the
		// fixed-step loop the scripts run in (FUN_0044e410 at 0054fa96). Nothing here poses anything yet, so
		// the only thing the timebase decides is whether this channel counts as busy - and bringing it up to
		// the asking moment first is what makes that question mean the same thing it means in the original.
		channel.MoveTo( now );

		var pseudo = role is AnimTimeControl.FreezeAtStart or AnimTimeControl.HoldAtEnd;
		var free = pseudo || !channel.IsBusy || !Carries( channel.AnimID, channel.SubAnim );

		if ( !free && (flags & AnimTimeControl.StartAtOnceFlag) == 0 )
		{
			channel.Queue( role, entry, flags, speed );

			// Truncated apart from each other, not added and then converted once.
			var remaining = channel.RemainingMilliseconds();

			return Carries( role, entry )
				? remaining + DurationMilliseconds( role, entry )
				: remaining + UnknownLength;
		}

		// Only the start-at-once path reaches the queue clear, which is why the two are one flag.
		if ( (flags & AnimTimeControl.StartAtOnceFlag) != 0 )
			channel.ClearQueue();

		return StartOn( channel, role, entry, flags, speed, now );
	}

	/// <summary>
	/// Puts a clip on a channel and answers its length, or a flat second where there is no such clip - the
	/// body of <c>FUN_00472f60</c> and the <c>if (answer == 0) answer = 1000</c> its caller applies.
	/// </summary>
	private int StartOn( AnimTimeControl channel, int role, int entry, int flags, float speed, int now )
	{
		// Where the outgoing clip had already run past its end, the new one starts that far in rather than
		// at nought, so a chain of clips does not lose a fraction of a frame at every join.
		var carry = !channel.IsIdle && channel.IsFinished
			? channel.AnimFrame - channel.TotalAnimFrames
			: 0f;

		channel.Start( role, entry, flags, speed, now, FramesFor( role, entry ), carry );

		return Carries( channel.AnimID, channel.SubAnim )
			? DurationMilliseconds( channel.AnimID, channel.SubAnim )
			: UnknownLength;
	}

	/// <summary>Whether this model actually has a clip for that role and entry.</summary>
	private bool Carries( int role, int entry )
		=> entry >= 0 && entry < EntryCount( role );

	/// <summary>
	/// What the engine answers for a role or entry no clip backs - the <c>ADD ESI,0x3e8</c> at
	/// <c>0x004733e7</c> and the flat <c>1000</c> its sibling path returns.
	/// </summary>
	public const int UnknownLength = 1000;

	/// <summary>
	/// Empties a channel's queue - <c>FLUSHANIM</c>, which is <c>FUN_00473270</c> and writes the sentinel
	/// into the <i>queued</i> role only. <b>It stops nothing</b>: the clip that is running plays out, and
	/// the instruction answers nothing at all.
	/// </summary>
	public void Flush( int index = 0 )
		=> Channel( index )?.ClearQueue();

	/// <summary>
	/// Brings every channel up to <paramref name="now"/> and deals with any that has reached the end of its
	/// clip - the timebase half of <c>FUN_004735d0</c>.
	///
	/// <para>
	/// <b>Three of the engine's five endings are unreachable here, and that is a fact about this program
	/// rather than a simplification.</b> Which ending a finished channel gets is chosen by two bits of the
	/// model's own flag word (<c>model+4 &amp; 0x18</c>): with them clear it promotes the queue, replays a
	/// looping clip or holds the last frame, and with them set it stalls or stops and calls out to the
	/// group that disposes of a ride vehicle. Nothing in this program sets those bits, because nothing here
	/// takes a thing out of a park - so the first three are the whole of what can happen, and the other two
	/// are named here rather than silently dropped.
	/// </para>
	/// </summary>
	public void Advance( int now )
	{
		// A model with no clips at all is never ticked: the engine tests model+0xa8, the count of clips
		// loaded across the twelve roles, before it touches a channel - and this is that count.
		if ( Loaded == 0 )
			return;

		foreach ( var channel in _channels )
		{
			if ( channel.IsIdle )
				continue;

			channel.MoveTo( now );

			if ( !channel.IsFinished )
				continue;

			if ( channel.HasQueued )
			{
				// The promotion is a full start, so the overshoot carry applies to it exactly as it would
				// to a trigger - and it clears three of the four queue fields, leaving the flags stale.
				StartOn( channel, channel.DeferredAnimID, channel.DeferredSubAnim,
					channel.DeferredFlags | AnimTimeControl.KeepShownFlag,
					channel.DeferredSpeed <= 0f ? channel.Speed : channel.DeferredSpeed, now );

				channel.ClearQueue();

				continue;
			}

			if ( (channel.Flags & AnimTimeControl.LoopFlag) != 0 )
			{
				// A loop is expressed as starting the same clip again rather than as rewinding it, which is
				// what carries the overshoot across the join.
				StartOn( channel, channel.AnimID, channel.SubAnim,
					AnimTimeControl.LoopFlag | AnimTimeControl.KeepShownFlag, channel.Speed, now );

				continue;
			}

			// Not looping and nothing waiting: the engine re-enters with the hold pseudo-role, which parks
			// the timebase on the last frame rather than marking the channel done.
			channel.Start( AnimTimeControl.HoldAtEnd, 0, AnimTimeControl.KeepShownFlag, channel.Speed, now, 0f );
		}
	}

	/// <summary>
	/// Reads every role an item ships, beside its model.
	/// </summary>
	/// <param name="directory">The item's own folder, as <see cref="ParkItemCatalogue.Item.Directory"/> gives it.</param>
	/// <param name="stem">Its own name, which its files are all prefixed with.</param>
	/// <param name="files">Where to read from, so a test needs no global file system to have been mounted.</param>
	public static RideAnimations Load( string directory, string stem, BaseFileSystem files )
	{
		var roles = new AnimationFile[RoleCount][];

		for ( int role = 0; role < RoleCount; ++role )
			roles[role] = LoadRole( directory, stem, Letters[role], files );

		return new RideAnimations( stem, roles );
	}

	/// <summary>An item with nothing beside it - what a thing whose archive ships no clips answers.</summary>
	public static RideAnimations None( string stem = "" )
		=> new( stem, CreateEmptyRoles() );

	private static AnimationFile[][] CreateEmptyRoles()
	{
		var roles = new AnimationFile[RoleCount][];

		for ( int role = 0; role < RoleCount; ++role )
			roles[role] = [];

		return roles;
	}

	/// <summary>
	/// One role's clips: the numbered run first, and the bare file only if that run began with nothing.
	/// </summary>
	private static AnimationFile[] LoadRole( string directory, string stem, char letter, BaseFileSystem files )
	{
		var clips = new List<AnimationFile>();

		// The numbered run, from one upwards, stopping at the first gap - which is what the engine does,
		// and what makes a gap invisible rather than skipped. No shipped archive has one: across 306
		// archives every numbered run starts at 1 and none is broken.
		for ( int number = 1; number <= NumberedCeiling; ++number )
		{
			if ( Read( $"{directory}/{stem}{letter}{number}.md2", files ) is not { } numbered )
				break;

			clips.Add( numbered );
		}

		// Only where the numbered run found nothing at all. An item shipping both keeps its numbered ones
		// and never reaches this, which is the mamfount case in the remarks on this class.
		if ( clips.Count == 0 && Read( $"{directory}/{stem}{letter}.md2", files ) is { } bare )
			clips.Add( bare );

		return [.. clips];
	}

	/// <summary>
	/// A stop on the numbered walk, so that a file system answering yes to everything cannot spin. The
	/// longest run the game ships is six.
	/// </summary>
	private const int NumberedCeiling = 64;

	/// <summary>
	/// One clip, or null where there is none. Read straight rather than through
	/// <see cref="AnimationFile.TryLoad"/> - see the remarks on this class for the 114 clips that one
	/// turns away.
	/// </summary>
	private static AnimationFile? Read( string path, BaseFileSystem files )
	{
		try
		{
			using var stream = files.OpenRead( path );

			if ( stream == null )
				return null;

			return new AnimationFile( stream );
		}
		catch ( Exception )
		{
			// A clip that will not read leaves the role shorter rather than taking the item down with it.
			return null;
		}
	}
}
