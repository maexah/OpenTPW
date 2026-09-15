namespace OpenTPW;

/// <summary>
/// The particles and sounds a ride script starts, and the list it keeps them in - the far side of
/// <c>ADDOBJ</c>, <c>EVENT</c> and <c>KILLOBJ</c>.
///
/// <para>
/// <b>This is the engine's own data structure rather than a container chosen here.</b> A script frame
/// carries a doubly linked list at <c>+0xb0</c> whose nodes are 28 bytes
/// (<c>PUSH 0x1c</c> at <c>0x551f5f</c>), laid out <c>+0x00</c> next, <c>+0x04</c> prev, <c>+0x08</c>
/// type, <c>+0x0c</c> the handle the spawn answered, <c>+0x10</c> node, <c>+0x14</c> the node's index on
/// the model, <c>+0x18</c> the tag. The engine also counts every live node in one global
/// (<c>DAT_008791b4</c>), which is what <see cref="Count"/> is.
/// </para>
///
/// <para>
/// <b>What the type operand selects is the subsystem</b>, read out of <c>FUN_005573d0</c>: 1 and 2 spawn
/// particles, 3 to 9 play a sound through one of the named categories - 3 and 4 are a second
/// <c>cat_rides</c> / <c>cat_ambient</c> pair, then <c>cat_rides</c>, <c>cat_kids</c>, <c>cat_staff</c>,
/// <c>cat_ambient</c> and <c>cat_ui</c> - and 10 plays the thing's own custom effect. Anything else is
/// the engine's <c>"RSSE: Unknown object type"</c>, which starts nothing and keeps no record; the
/// guard is an unsigned <c>DEC</c> / <c>CMP 9</c> / <c>JA</c>, so nought and negatives take it too.
/// </para>
///
/// <para>
/// <b>The effect id is deliberately not checked against anything.</b> It is a particle id for types 1
/// and 2 and a sample index for the rest - two unrelated namespaces - and while no shipped script names
/// a particle above 95, that is a fact about the shipped scripts and not a rule: <c>FUN_005573d0</c>
/// tests the id for bit 15 and remaps it through <c>FUN_004145d0</c> when it is set, so the interpreter
/// is written to accept ids this would reject if it bounded them.
/// </para>
///
/// <para>
/// <b>What is deliberately smaller than the original</b>, because the parts it would need are not here:
/// </para>
///
/// <list type="bullet">
/// <item>Nothing is actually drawn or heard. The engine hands the spawn to <c>Particles_Spawn</c> or
/// <c>Sound_PlayEffect</c> and keeps what they answer; this keeps the request and answers a handle of
/// its own, so a world can later start them for real from <see cref="Records"/> without this having
/// guessed at either subsystem's numbering.</item>
/// <item>There is no position. The engine resolves one through <c>FUN_00556b90</c> from the model and
/// the node, and <b>walks this whole list every tick</b> (in <c>FUN_005516b0</c>) to move what is
/// playing as the ride moves. With no model there is nothing to move and nothing to move it
/// relative to.</item>
/// <item>The list has two other writers that are not here: <c>ADDOBJ_EXT</c>, which no shipped script
/// uses at all, and the save-state reader (<c>FUN_005597a0</c>), which rebuilds it from an
/// <c>"OBJ "</c> section - a section none of the 308 shipped scripts carries, because that is written
/// by the save file rather than by the script.</item>
/// </list>
/// </summary>
public sealed class RideEffects
{
	/// <summary>The lowest object type the engine accepts; below this is its "unknown object type".</summary>
	public const int FirstType = 1;

	/// <summary>The highest type, which is the thing's own custom effect.</summary>
	public const int LastType = 10;

	/// <summary>Types 1 and 2 are particles; everything above is a sound. See <see cref="Record.IsParticle"/>.</summary>
	public const int LastParticleType = 2;

	/// <summary>
	/// One of the engine's 28-byte nodes: what was started, and the tag a <c>KILLOBJ</c> matches on.
	/// </summary>
	public sealed class Record
	{
		/// <summary>1 or 2 for a particle, 3 to 10 for a sound - the engine's field <c>+0x08</c>.</summary>
		public int Type { get; init; }

		/// <summary>
		/// Which node of the model it sits on - the engine's field <c>+0x10</c>. <b>-1 is the commonest
		/// value in the whole corpus</b> (277 of 644 <c>ADDOBJ</c>s and 341 of 527 <c>EVENT</c>s) and means
		/// the model's own origin: the engine tests the node for being negative before it looks anything up.
		/// </summary>
		public int Node { get; init; }

		/// <summary>A particle id or a sample index, depending on <see cref="Type"/>, and bounded by neither.</summary>
		public int Effect { get; init; }

		/// <summary>
		/// What a <c>KILLOBJ</c> matches - the engine's field <c>+0x18</c>, taken from <c>ADDOBJ</c>'s
		/// <b>fourth</b> operand. It is a tag and not a duration: the corpus kills 1, 10, 20, 11, 2 and 500,
		/// which are the very values its <c>ADDOBJ</c>s create.
		/// </summary>
		public int Tag { get; init; }

		/// <summary>
		/// What the spawn answered - the engine's field <c>+0x0c</c>, and <b>ours rather than the
		/// original's</b>, since nothing here starts a particle or a sound to get a real one.
		///
		/// <para>
		/// It is settable rather than fixed because the engine writes it back: <c>SETOBJPARAM</c> hands
		/// it to the sound system and stores whatever comes of it - see <see cref="SetParameter"/>.
		/// It is <c>internal</c> rather than <c>private</c> because a containing type cannot reach a
		/// nested type's private setter - only the other way round - so <see cref="RideEffects"/> could
		/// neither create a record nor write one back.
		/// </para>
		/// </summary>
		public int Handle { get; internal set; }

		/// <summary>Whether this is one of the two particle types rather than one of the eight sound types.</summary>
		public bool IsParticle => Type <= LastParticleType;
	}

	private readonly List<Record> _records = new();

	private int _handle;

	/// <summary>
	/// What is live, newest first - the engine links each new node in at the head, so this is its order.
	/// </summary>
	public IReadOnlyList<Record> Records => _records;

	/// <summary>How many are live, which is the engine's global at <c>DAT_008791b4</c>.</summary>
	public int Count => _records.Count;

	/// <summary>Everything ever started, records and one-shot events alike.</summary>
	public int Started { get; private set; }

	/// <summary>Everything a <c>KILLOBJ</c> has stopped.</summary>
	public int Stopped { get; private set; }

	/// <summary>
	/// Asks for a type the engine does not have. It complains and starts nothing - counted here so that a
	/// script doing it is visible rather than silently doing nothing.
	/// </summary>
	public int Unknown { get; private set; }

	/// <summary>Whether <paramref name="type"/> is one the engine's switch has a case for.</summary>
	public static bool IsKnown( int type ) => type >= FirstType && type <= LastType;

	/// <summary>
	/// <c>ADDOBJ</c>: start something and keep a record of it, so that a later <c>KILLOBJ</c> naming the
	/// same tag can stop it.
	/// </summary>
	public void Add( int type, int node, int effect, int tag )
	{
		if ( !IsKnown( type ) )
		{
			// The engine allocates its node first and then frees it again on this path, having complained
			// twice. The visible outcome is that nothing was started and nothing is left behind.
			++Unknown;
			return;
		}

		++Started;

		_records.Insert( 0, new Record
		{
			Type = type,
			Node = node,
			Effect = effect,
			Tag = tag,
			Handle = ++_handle
		} );
	}

	/// <summary>
	/// <c>EVENT</c>: start something and keep nothing.
	///
	/// <para>
	/// <b>The handler discards the handle</b> - it calls the same worker <c>ADDOBJ</c> does and then
	/// returns without storing what came back - so an <c>EVENT</c>'s particle or sound can never be
	/// stopped by a <c>KILLOBJ</c>, which consults nothing but the record list. That asymmetry is the
	/// whole difference between the two instructions, and it is why 527 uses of <c>EVENT</c> need no
	/// bookkeeping at all.
	/// </para>
	/// </summary>
	public void Trigger( int type, int node, int effect )
	{
		if ( !IsKnown( type ) )
		{
			++Unknown;
			return;
		}

		++Started;
	}

	/// <summary>
	/// <c>KILLOBJ</c>: stop <b>every</b> record whose tag matches, and answer how many that was.
	///
	/// <para>
	/// <b>Every one, not the first.</b> The handler walks the list from the head, and on a match it moves
	/// to the next node <i>before</i> it unlinks and frees the one it matched
	/// (<c>0x552376: MOV EAX,ESI / MOV ESI,[ESI]</c>), then jumps back to the same test. There is no break
	/// anywhere in it. A machine that stopped at the first match would leave later duplicates of a tag
	/// playing for ever, and nothing about the instruction's shape would have shown it.
	/// </para>
	///
	/// <para>
	/// <c>FADEOBJ</c> is the same walk and is deliberately not implemented: it differs only for a sound,
	/// where it calls <c>Sound_StopFading</c> in place of <c>Sound_Stop</c>, and for a particle the two are
	/// byte for byte the same call. With nothing yet sounding there is no difference here to express.
	/// </para>
	/// </summary>
	public int Kill( int tag )
	{
		var killed = _records.RemoveAll( record => record.Tag == tag );

		Stopped += killed;

		return killed;
	}

	/// <summary>
	/// <c>SETOBJPARAM</c>: set a parameter on everything carrying a tag, and answer how many that was.
	///
	/// <para>
	/// It walks this same list and matches on the tag exactly as <see cref="Kill"/> does - <b>the first
	/// operand is a tag and not a slot</b>, which is what the published docs called it - and its type
	/// dispatch has only two cases (byte map at <c>0x5569c4</c>, jump table at <c>0x5569b8</c>): <b>the
	/// two particle types do nothing at all</b>, and the eight sound types hand the record's handle to
	/// the sound system and store back whatever comes of it.
	/// </para>
	///
	/// <para>
	/// <b>With no sound system that answer is nought, and the nought is the engine's own rather than a
	/// stand-in for one.</b> <c>FUN_0051bc40</c> tests two flags that only the sound system's own init
	/// and teardown ever write, and returns 0 when it is down - so the handle is zeroed. The walk, the
	/// two cases and the zero are all the engine's; nothing here is invented.
	/// </para>
	///
	/// <para>
	/// The engine also has a complaint for a record whose type is outside 1..10, after which it carries
	/// on walking. It cannot fire here, because <see cref="Add"/> refuses those types before a record
	/// ever exists - so there is no branch for it rather than a branch that can never be taken.
	/// </para>
	/// </summary>
	public int SetParameter( int tag, int parameter, int value )
	{
		var touched = 0;

		foreach ( var record in _records )
		{
			// A particle carrying the tag is walked past in silence - it is the type that decides, not
			// the tag, so a tag shared between a particle and a sound sets only the sound.
			if ( record.Tag != tag || record.IsParticle )
				continue;

			record.Handle = 0;
			++touched;
		}

		Parameters += touched;

		return touched;
	}

	/// <summary>How many records a <c>SETOBJPARAM</c> has reached, so that doing nothing is visible.</summary>
	public int Parameters { get; private set; }
}
