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
/// <b>An id does not count clips.</b> It is not an index into the numbered <c>&lt;stem&gt;M&lt;n&gt;.md2</c>
/// clips an item ships: a script triggers ids 0 and 5 on an item that ships no such file. <c>M</c> is the
/// letter of one role - id 5 - the one whose files carry numbers (docs/exe/park.md, "Item animation: the
/// twelve roles").
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
/// down a clip carrying no morph, rotation or UV track - and 127 of the game's clips are exactly that,
/// 89 of them carrying no track at all. Every one of them declares a real length, some of them long:
/// the ferries run 600 frames, twenty seconds. Loading them the fussy way would silently drop the longest
/// animations in the game and tell their scripts 700ms instead, a missing role's flat second less 300.
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

	private RideAnimations( string stem, AnimationFile[][] roles, int channels )
	{
		Stem = stem;
		_roles = roles;

		// Floored at one exactly as the engine floors it: FUN_004629d0 replaces a nought count with 1 before
		// it ever reaches the loader, so an item that declares none still gets a player.
		_channels = new AnimTimeControl[Math.Max( channels, 1 )];

		for ( var index = 0; index < _channels.Length; ++index )
			_channels[index] = new AnimTimeControl();

		_bound = new Bind[_channels.Length];

		foreach ( var role in roles )
		{
			if ( role.Length == 0 )
				continue;

			++Roles;
			Loaded += role.Length;
		}

		AllClips = [.. roles.SelectMany( clips => clips )];
	}

	/// <summary>
	/// Every clip this thing carries, across all twelve roles, in role and then entry order.
	///
	/// <para>
	/// A model binds its animation players against this rather than against one role, because a channel
	/// may name any of them and a player has to have been built for the mesh before it can be posed.
	/// <b>It is not the numbered <c>M</c> run a lobby model probes for</b>: that is role 5 alone, read
	/// through <see cref="AnimationFile.TryLoad"/>, which turns away the 127 clips carrying no rotation, UV
	/// or readable morph track. Four of the eleven things standing in Lost Kingdom - the Round Fountain, both
	/// Security Cameras and the Drinks Shop - would bind their bare M clip at most, the one the probe falls back
	/// to, and never their C clip.
	/// </para>
	/// </summary>
	public AnimationFile[] AllClips { get; }

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
	/// disagree far more often than they look like they would: some clips declare a longer span than
	/// their keys cover and some a shorter one, so computing it from keys would be wrong in both
	/// directions - the counts are at <see cref="AnimationFile.DeclaredLastFrame"/>.
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
	/// and entry name nothing at all. For a role the model lacks the engine stops the channel rather than
	/// erring; for an entry past a loaded role's count it plays on (see <see cref="Trigger"/>). See
	/// <see cref="DurationMilliseconds"/> for why the span is the declared one.
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
	/// The clip one role and entry names, or null where that pair names nothing at all - what a channel
	/// running them has to be posed from. <see cref="DurationMilliseconds"/> and <see cref="FramesFor"/>
	/// answer about this same clip, and all three have to agree about which one it is, which is why they
	/// are bounded the same way rather than each in its own words.
	/// </summary>
	public AnimationFile? Clip( int role, int entry )
	{
		if ( role < 0 || role >= RoleCount )
			return null;

		var clips = _roles[role];

		return entry >= 0 && entry < clips.Length ? clips[entry] : null;
	}

	/// <summary>
	/// The animation players this model carries - the engine's array at <c>model+0x10</c>.
	///
	/// <para>
	/// <b>The number is the item's own, and its file does say it.</b> The original takes the count as an
	/// argument to the model loader: eight call sites pass 1, ride vehicles pass 5, and the main thing path
	/// hands over <c>thing+0x170</c> (<c>FUN_00413c10</c>), which <c>FUN_004629d0</c> turns into 1 when it is
	/// nought. That field is <c>UsageInfo.NumSimultAnims</c> in the item's own <c>.sam</c> - see
	/// <see cref="ItemDescriptionFile.NumSimultAnims"/>, which carries how it was identified.
	/// </para>
	/// <para>
	/// <b>Nothing in the engine bounds-checks a channel index</b>, so an out-of-range one writes past this
	/// allocation; <see cref="Channel"/> refuses instead, which is the one place here that deliberately does
	/// not reproduce what the original does.
	/// </para>
	/// </summary>
	private readonly AnimTimeControl[] _channels;

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
	/// finished, held or frozen, running a clip its own model no longer carries, asked for a pseudo-role,
	/// or when the caller passes <see cref="AnimTimeControl.StartAtOnceFlag"/>. Otherwise the clip is put in the queue and the answer
	/// is <b>the time still to run on the current clip plus the length of the new one</b>, each truncated
	/// separately - which is why a script that triggers twice in a row is told a longer time the second
	/// time, and why that number stops being <see cref="DurationMilliseconds"/> the moment a park is
	/// running.
	/// </para>
	///
	/// <para>
	/// A role or entry the model does not carry answers a flat second, and the engine reaches that answer by
	/// two different routes: the queue path adds 1000 outright, and the start path stops the channel for a
	/// role it lacks and answers nothing, which <c>FUN_004732a0</c>'s tail turns into 1000. For an entry
	/// past a loaded role's count the engine stops nothing: it plays whatever lies past that role's clips,
	/// with the role standing (<c>0x0047308f</c>). This stops the channel for that too, a deviation no
	/// literal reference reaches; eleven name the entry by a variable, none in Lost Kingdom.
	/// </para>
	/// </summary>
	public int Trigger( int role, int entry, int flags, float speed, int now, int index = 0 )
	{
		if ( Channel( index ) is not { } channel )
			return UnknownLength;

		// Asked as the last frame's advance left it, not brought up to the trigger's moment (0x00473315): the
		// engine advances a placed thing's model once a frame, after the frame's ticks, and so does
		// ParkObjects.Sweep. A clip that ran out since that advance is still busy, so the new one queues, the
		// answer counts the old clip's remainder as the advance left it (0x0047337b), and the next advance
		// promotes it with its start carried back to the old clip's end.
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
	/// Stands a newly built model on the first frame of its construction clip, role nought entry nought, frozen there
	/// until its script plays it - the build path's two triggers on channel 0 (<c>FUN_00463060</c>: role 0 at
	/// <c>0x00463205</c>, then role 13 at once at <c>0x0046321f</c>, after <c>FUN_00472d10</c> found the clip). A model
	/// with no role nought clip is left idle, at rest. Answers whether it was stood.
	/// </summary>
	public bool StandOnBuildClip( int now )
	{
		if ( !Carries( 0, 0 ) )
			return false;

		Trigger( 0, 0, 0, 1f, now );
		Trigger( AnimTimeControl.FreezeAtStart, 0, 0, 1f, now );

		return true;
	}

	/// <summary>
	/// Puts a clip on a channel and answers its length, or a flat second where there is no such clip - the
	/// body of <c>FUN_00472f60</c> and the <c>if (answer == 0) answer = 1000</c> its caller applies.
	/// </summary>
	private int StartOn( AnimTimeControl channel, int role, int entry, int flags, float speed, int now )
	{
		var index = Array.IndexOf( _channels, channel );

		if ( Clip( role, entry ) is { } clip )
		{
			// The model's nodes are marked as the clip's only where the start binds it, which the caller's 0x8 says it
			// does not (0x00473117); the frame the outgoing clip stood on is what its visibility tracks leave behind.
			if ( (flags & AnimTimeControl.KeepShownFlag) == 0 )
				Nodes?.Started( index, clip, channel.IsIdle ? float.MaxValue : channel.AnimFrame );

			// Every start of a clip is a bind, the same clip again included; what it puts back and hides is the drawn
			// side's, by the caller's 0x4 and 0x8 (PoseChannel).
			_bound[index] = new Bind( _bound[index].Start + 1, flags );
		}

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

	/// <summary>
	/// A clip bound to a channel by a start (<c>FUN_00472f60</c>): which start it was, counted on the channel from one,
	/// and the caller's flags, whose <c>0x4</c> and <c>0x8</c> say what the start puts back and hides.
	/// </summary>
	public readonly record struct Bind( int Start, int Flags );

	private readonly Bind[] _bound;

	/// <summary>The last start that bound a clip to a channel, or none (<c>Start</c> nought) where nothing has.</summary>
	public Bind BoundOn( int index ) => index >= 0 && index < _bound.Length ? _bound[index] : default;

	/// <summary>The clip a drawn model or node tree was last posed from on one channel, and the start that bound it.</summary>
	public record struct Posed( AnimationFile? Clip, int Start );

	/// <summary>
	/// Poses <paramref name="target"/> from every channel that is playing, each its clip at the frame it has reached, a
	/// new start first putting back and hiding what the engine's bind does (<see cref="PoseChannel"/>). <paramref name="posed"/>
	/// holds the clip each channel was last posed from, one per channel this caller poses. The engine's sweep walks the
	/// whole array of players (<c>FUN_004735d0</c>): the Jungle Spray runs one per lane.
	/// </summary>
	public void PoseOnto( IChannelPosed target, Posed[] posed )
	{
		for ( var index = 0; index < posed.Length; ++index )
		{
			if ( Channel( index ) is not { } channel || channel.IsIdle )
				continue;

			if ( Clip( channel.AnimID, channel.SubAnim ) is not { } clip )
				continue;

			PoseChannel( target, ref posed[index], clip, channel.AnimFrame, BoundOn( index ) );
		}
	}

	/// <summary>
	/// One channel's clip on <paramref name="target"/> at <paramref name="frame"/>, the frame counted from the start of the
	/// clip. A start the target has not seen, <paramref name="bind"/>, does what the engine's start does
	/// (<c>FUN_00472f60</c>): it puts back what the clip before moved, all but what the incoming clip moves on the same
	/// channel (<c>FUN_00472310</c>; <see cref="NodePoser.Rest"/>), unless the caller passed <c>0x4</c> or <c>0x8</c>, and
	/// then hides the incoming clip's hide list unless it passed <c>0x8</c> (<c>FUN_00472d70</c>, <c>0x00473117</c>), on a
	/// channel that was idle too. A clip changed with no start seen is put back and hidden as an unflagged start's.
	/// <para>
	/// <b>Deviation, the role-12 stop:</b> a start of a role the model lacks restores the outgoing clip at the stop (unless
	/// its flags carry <c>0xc</c>) and parks the channel at role 12, so the next start restores nothing
	/// (<c>0x00473121</c>, <c>0x00472fbb</c>). <see cref="StartOn"/> binds nothing for a null clip and <see cref="PoseOnto"/>
	/// skips the idle channel, so nothing is put back at the stop, the last pose stays drawn, and the next start rests by
	/// its own flags.
	/// </para>
	/// <para>
	/// <b>Deviation, collapsed starts:</b> only the last start on a channel between two poses is seen (<see cref="BoundOn"/>),
	/// where the original applies each start's restore and hide at the start. A trigger and a loop restart in one
	/// <see cref="Advance"/> lose the trigger's restore and hide; a start and a promotion rest the clip before the first
	/// start straight into the promoted one. No Lost Kingdom case is known.
	/// </para>
	/// </summary>
	internal static void PoseChannel( IChannelPosed target, ref Posed posed, AnimationFile clip, float frame, Bind bind = default )
	{
		if ( !ReferenceEquals( clip, posed.Clip ) || bind.Start != posed.Start )
		{
			if ( posed.Clip is { } previous && (bind.Flags & (AnimTimeControl.KeepPoseFlag | AnimTimeControl.KeepShownFlag)) == 0 )
				target.Rest( previous, clip );

			if ( (bind.Flags & AnimTimeControl.KeepShownFlag) == 0 )
				target.Hide( clip );

			posed = new Posed( clip, bind.Start );
		}

		target.Pose( clip, frame );
	}

	/// <summary>
	/// The model's node words as its clips start, for a park file's model record (<see cref="ParkModelTables.Running"/>);
	/// null where nobody keeps them. Set once the channels stand as they began: a load's own starts are not a
	/// clip bound, the file's words already holding them.
	/// </summary>
	public ParkModelTables.Running? Nodes { get; set; }

	/// <summary>The frame each channel stands on, past the end for an idle one: what <see cref="ParkModelTables.Running.Words"/> takes.</summary>
	public float[] NodeFrames() => [.. _channels.Select( channel => channel.IsIdle ? float.MaxValue : channel.AnimFrame )];

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
	/// <b>Two of the engine's five endings are not built here.</b> A finished channel promotes its queue
	/// first, whatever the model's flag word holds. Failing that, <c>model+4 &amp; 0x18</c> chooses: clear,
	/// it replays a looping clip or holds the last frame; set, it stalls, or stops and calls out to the
	/// group that disposes of a ride vehicle. Nothing here sets those bits. The engine sets <c>0x10</c> on a
	/// thing's model whose only clips are role nought's (<c>0x00473f32</c>), and there parks a finished
	/// channel at role 12 with its pose kept, where this holds it on role nought: 22 Lost Kingdom items, each
	/// running one <c>WAITANIM 0, 0</c> and nothing that reads the channel afterwards. What the stall's model
	/// flags change on screen is not decoded.
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

			if ( channel.IsFinished )
			{
				channel.MarkEnded();
				End( channel, now );
			}

			channel.SettleEnded();
		}
	}

	/// <summary>Deals with a channel whose clip has run past its end: its queue, its loop, or a hold.</summary>
	private void End( AnimTimeControl channel, int now )
	{
		if ( channel.HasQueued )
		{
			// The promotion is a full start, so the clip that ran past its end hands its overshoot to this
			// one, clamped to this clip's own length - and it clears three of the four queue fields,
			// leaving the flags stale.
			// The engine binds a promoted clip by the queued flags while its model is in view and adds 0x8 off it
			// (FUN_004735d0's per-frame argument, 0x00473874); this poses every model every frame, so it binds as the
			// queued flags say, in view or not.
			StartOn( channel, channel.DeferredAnimID, channel.DeferredSubAnim, channel.DeferredFlags,
				channel.DeferredSpeed <= 0f ? channel.Speed : channel.DeferredSpeed, now );

			channel.ClearQueue();

			return;
		}

		if ( (channel.Flags & AnimTimeControl.LoopFlag) != 0 )
		{
			// A loop is expressed as starting the same clip again rather than as rewinding it, which is
			// what carries the overshoot across the join. Not the original's: its restart in view passes 0x1 alone
			// (0x00473840..0x00473848), so it puts back and hides as a start does; this passes 0x8 (Q661).
			StartOn( channel, channel.AnimID, channel.SubAnim,
				AnimTimeControl.LoopFlag | AnimTimeControl.KeepShownFlag, channel.Speed, now );

			return;
		}

		// Not looping and nothing waiting: the engine re-enters with the hold pseudo-role, which parks
		// the timebase on the last frame rather than marking the channel done.
		channel.Start( AnimTimeControl.HoldAtEnd, 0, AnimTimeControl.KeepShownFlag, channel.Speed, now, 0f );
	}

	/// <summary>
	/// Reads every role an item ships, beside its model.
	/// </summary>
	/// <param name="directory">The item's own folder, as <see cref="ParkItemCatalogue.Item.Directory"/> gives it.</param>
	/// <param name="stem">Its own name, which its files are all prefixed with.</param>
	/// <param name="files">Where to read from, so a test needs no global file system to have been mounted.</param>
	/// <param name="channels">
	/// How many animation players to give it - the item's own <c>UsageInfo.NumSimultAnims</c>. One is both
	/// the default and what the engine substitutes for nought, so a caller with no item description to hand
	/// gets the same single player it always had.
	/// </param>
	public static RideAnimations Load( string directory, string stem, BaseFileSystem files, int channels = 1 )
	{
		var roles = new AnimationFile[RoleCount][];

		for ( int role = 0; role < RoleCount; ++role )
			roles[role] = LoadRole( directory, stem, Letters[role], files );

		return new RideAnimations( stem, roles, channels );
	}

	/// <summary>An item with nothing beside it - what a thing whose archive ships no clips answers.</summary>
	public static RideAnimations None( string stem = "", int channels = 1 )
		=> new( stem, CreateEmptyRoles(), channels );

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
	/// <see cref="AnimationFile.TryLoad"/> - see the remarks on this class for the 127 clips that one
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

/// <summary>What a thing's players pose: its drawn model (<see cref="LobbyModel"/>) or its node tree (<see cref="NodePoser"/>).</summary>
public interface IChannelPosed
{
	/// <summary>Puts back what <paramref name="outgoing"/> moved, all but what <paramref name="incoming"/> moves.</summary>
	void Rest( AnimationFile outgoing, AnimationFile? incoming );

	/// <summary>Hides <paramref name="incoming"/>'s hide list, as its start binds it (<c>FUN_00472d70</c>); nothing where the target draws nothing.</summary>
	void Hide( AnimationFile incoming ) { }

	/// <summary>Shows <paramref name="clip"/> at <paramref name="frame"/>.</summary>
	void Pose( AnimationFile clip, float frame );
}
