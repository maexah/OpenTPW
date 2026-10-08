namespace OpenTPW;

/// <summary>
/// What a person last thought and the bubble over them - the thought half of the person base's history block
/// (<c>+0x30</c>): <c>mLastThought</c>, the bubble's sprite (<c>+0xb4</c>) and <c>mTimeBubbleShown</c>
/// (<c>+0xbc</c>). Guests and staff both carry one (<c>docs/exe/ride-operation.md</c>, "Thoughts and their
/// pictures").
///
/// <para>
/// <b>The bubble is a world sprite of kind 9</b>, "thoughts", from the two banks in <see cref="Folder"/>. Each of
/// the 22 thought scripts sets one picture set, shows frame 0 and loops, so the bubble here is the set alone and
/// runs no script; the original's shows from its script's first turn, a sprite interval after it is made.
/// </para>
/// </summary>
public sealed class Thoughts
{
	/// <summary>The sprite kind the table at <c>0x00763f88</c> names "thoughts".</summary>
	public const int SpriteKind = 9;

	/// <summary>Where the two thought banks are, <c>SPR_TB</c> then <c>SPR_TC</c>, generic to every theme.</summary>
	public const string Folder = "esprites/Generic/Thoughts";

	/// <summary>How far above the person the bubble is placed each frame - <c>[0x0075c804]</c>, <c>FUN_004fa030</c>.</summary>
	public const float Lift = 2.5f;

	/// <summary>How many sweeps a thought's class holds off the next bubble, per class - <c>0x0050c039</c>.</summary>
	public const int SweepsPerClass = 20;

	/// <summary>How many sweeps past its making a bubble must be before a needs turn takes it away - <c>FUN_0050be40</c>.</summary>
	public const int BubbleSweeps = 12;

	/// <summary>Confused: a guest at a dead end, and one refused as stranded (<c>0x004f9deb</c>, <c>0x004f950e</c>).</summary>
	public const int Confused = 0x11;

	/// <summary>
	/// SetThought's switch, <c>FUN_0050be80</c>: each thought's picture and class, thought 1 first. The picture
	/// packs the bank in its high bits and the set in its low four, so 16 to 21 are <c>SPR_TC</c>'s sets 0 to 5.
	/// </summary>
	private static readonly (int Picture, int Class)[] Table =
	[
		(8, 1), (10, 1), (12, 1), (13, 1), (21, 0), (9, 0), (14, 1), (5, 3), (0, 3), (1, 3), (3, 3),
		(4, 0), (2, 0), (6, 1), (7, 0), (11, 0), (15, 0), (16, 3), (17, 2), (18, 0), (19, 0), (20, 0)
	];

	/// <summary>
	/// The script a bubble of a picture is made on, as a word of the sprite scripts' array, which a park file names
	/// it by. SetThought's 22 are six words apart from <c>0x0074f190</c>: set the picture, show frame 0, jump back.
	/// They set pictures 0 to 15 in order, then 21, then 16 to 20.
	/// </summary>
	public static int ScriptOf( int bank, int set )
	{
		var picture = (bank << 4) | set;

		return FirstScript + (WordsAScript * (picture < 16 ? picture : picture == 21 ? 16 : picture + 1));
	}

	/// <summary>
	/// How far into its script a bubble that has shown its frame rests, in words: past the two instructions that
	/// set the picture and show it, on the jump back. Every saved bubble but one just made rests there.
	/// </summary>
	public const int ShownAt = 4;

	private const int FirstScript = 1462;

	private const int WordsAScript = 6;

	/// <summary>The thought last set, shown or not - <c>mLastThought</c>. Nought before any.</summary>
	public int Last { get; private set; }

	/// <summary>The park's clock when the bubble last made was made - <c>mTimeBubbleShown</c>.</summary>
	public int TimeBubbleShown { get; private set; }

	/// <summary>The bubble showing: its bank of the kind and its set there. Null with none.</summary>
	public (int Bank, int Set)? Bubble { get; private set; }

	/// <summary>Whether a number is one of the 22 thoughts the switch knows.</summary>
	public static bool Known( int thought ) => thought >= 1 && thought <= Table.Length;

	/// <summary>A known thought's bank and set, and its class.</summary>
	public static (int Bank, int Set, int Class) PictureOf( int thought )
	{
		var (picture, wait) = Table[thought - 1];

		return (picture >> 4, picture & 0xf, wait);
	}

	/// <summary>
	/// SetThought, <c>FUN_0050be80( thought, 0 )</c>. The thought is stored first. In a first-person view nothing
	/// more happens. Otherwise the old bubble is freed, and a new one is made only when the park's clock is at
	/// least the last bubble's time plus <see cref="SweepsPerClass"/> times the thought's class, unsigned; a
	/// thought the switch does not know frees the old bubble and makes none.
	/// </summary>
	/// <param name="gameTick">The park's own clock, <c>mGameTick</c>.</param>
	/// <param name="firstPerson">Whether the view is first person (<c>gui_CameraFlags &amp; 0x16</c>).</param>
	/// <returns>Whether a bubble was made.</returns>
	public bool Set( int thought, int gameTick, bool firstPerson = false )
	{
		Last = thought;

		if ( firstPerson )
			return false;

		Bubble = null;

		if ( !Known( thought ) )
			return false;

		var (bank, set, wait) = PictureOf( thought );

		if ( (uint)(TimeBubbleShown + (wait * SweepsPerClass)) > (uint)gameTick )
			return false;

		TimeBubbleShown = gameTick;
		Bubble = (bank, set);

		return true;
	}

	/// <summary>
	/// A needs turn's last call, <c>FUN_0050be40</c>: a bubble made more than <see cref="BubbleSweeps"/> sweeps
	/// ago is freed, unsigned. A person's needs turn is one sweep in four, so a bubble lasts 13 to 16.
	/// </summary>
	public void Expire( int gameTick )
	{
		if ( (uint)(TimeBubbleShown + BubbleSweeps) < (uint)gameTick )
			Bubble = null;
	}

	/// <summary>What a save kept: the thought and the bubble's time. The bubble's own sprite is not restored.</summary>
	public void Restore( int last, int timeBubbleShown )
	{
		Last = last;
		TimeBubbleShown = timeBubbleShown;
	}

	/// <summary>
	/// The thought a guest's needs pick - <c>FUN_004fc8a0</c>, in its order, each need truncated to a byte: hungry
	/// and thirsty both above 90, 3; hungry above 99, 1; thirsty above 99, 2; the toilet above 99, 4; illness
	/// above 99, <c>0xe</c>; happiness above 99, 9, or below 10, <c>0xb</c>. Null picks nothing.
	/// </summary>
	/// <remarks>
	/// Between illness and happiness the original asks the park analyser for the guest's region and thinks 7,
	/// too messy, where its litter share is above 0.2 (<c>0x004fc985</c>..<c>0x004fc9eb</c>). This park keeps no
	/// litter, so that arm is counted and never taken. Thought 4 on a guest whose id's low four bits are nought
	/// also plays effect <c>0x85</c> (<c>0x004fca43</c>), counted.
	/// </remarks>
	public static int? PickFromNeeds( Peep peep )
	{
		var (hunger, thirst) = ((byte)(int)peep.Hunger, (byte)(int)peep.Thirst);

		if ( hunger > 90 && thirst > 90 )
			return 3;

		if ( hunger > 99 )
			return 1;

		if ( thirst > 99 )
			return 2;

		if ( (byte)(int)peep.Toilet > 99 )
		{
			if ( (peep.ThingId & 0xf) == 0 )
				Unimplemented.Report( "NEEDS_THOUGHT_4_SOUND" );

			return 4;
		}

		if ( (byte)(int)peep.Vomit > 99 )
			return 0xe;

		Unimplemented.Report( "NEEDS_THOUGHT_LITTER_SHARE" );

		var happiness = (byte)(int)peep.Happiness;

		return happiness > 99 ? 9 : happiness < 10 ? 0xb : null;
	}
}
