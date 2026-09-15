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
	/// reads the clip's own frame bounds out of its animation block and multiplies by 1000/30
	/// (<c>0x004733b1</c>-<c>0x004733db</c>, against the float 33.3333 at <c>0x006fec08</c>). Those two
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

		return frames <= 0 ? 0 : frames * 1000 / (int)AnimationFile.FramesPerSecond;
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
