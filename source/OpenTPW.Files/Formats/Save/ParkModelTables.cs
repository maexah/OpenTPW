namespace OpenTPW;

/// <summary>
/// The two tables of a park file's model record, worked out from the model's own file and the clips its channels
/// run: a flag word a node and a pair a lookup record (FileFormats <c>saves.md</c>, "The node flag words";
/// <c>docs/exe/saves.md</c>, "The rule for a record's two tables").
///
/// <para>
/// The engine keeps these as flags on the running model and packs them as it saves (<c>FUN_00464140</c>). A model
/// here keeps none of them, so they are worked out as the file is written, from what sets each in the engine.
/// </para>
/// </summary>
public static class ParkModelTables
{
	/// <summary>The node is hidden: the node's <c>0x10</c>.</summary>
	public const uint Hidden = 0x1;

	/// <summary>Every child of the node is a childless transform-only node: the node's <c>0x20</c> (<c>0x00462fbc</c>).</summary>
	public const uint HoldsOnlyMarkers = 0x2;

	/// <summary>The mesh was morphed and its face normals are stale, the node's <c>0x10000</c>. Never written here: the engine clears it as it next poses the mesh.</summary>
	public const uint StaleNormals = 0x8;

	/// <summary>A clip bound to a channel has a track for the node: the node's <c>0x40000</c>.</summary>
	public const uint Tracked = 0x20;

	/// <summary>That track is a morph (its flags hold <c>0x1000</c>): the node's <c>0x200000</c>.</summary>
	public const uint Morphed = 0x80;

	/// <summary>That track moves the mesh's texture coordinates (<c>0x10000</c>): the node's <c>0x400000</c>.</summary>
	public const uint TextureMoved = 0x100;

	/// <summary>The node's lookup record has a position (its file flags meet <c>0x30</c>): the node's <c>0x20000000</c> (<c>0x0044a90c</c>).</summary>
	public const uint Placed = 0x200;

	/// <summary>The node is transform-only (its file flags hold <c>0x200</c>), which no hide list hides or shows: the node's <c>0x80000000</c> (<c>0x00462f47</c>).</summary>
	public const uint TransformOnly = 0x400;

	/// <summary>That track places, turns, scales or routes the node (any of <c>0x289</c>): the node's <c>0x100000</c>.</summary>
	public const uint Moved = 0x800;

	/// <summary>The four bits a bound clip's tracks set (<c>FUN_00472d70</c>).</summary>
	public const uint TrackBits = Tracked | Morphed | TextureMoved | Moved;

	/// <summary>A node's file flag for a transform-only node.</summary>
	private const uint FileTransformOnly = 0x200;

	/// <summary>
	/// Each node's word on a model just made, before any clip is bound: the item loader's closing loop over the
	/// nodes (<c>FUN_004629d0</c>, <c>0x00462f40</c>..<c>0x00462fbc</c>) and the lookup records' own pass
	/// (<c>FUN_0044a870</c>). A transform-only node is hidden and marked so in one write.
	/// </summary>
	public static uint[] NodeWordsAtRest( ModelFile model )
	{
		ArgumentNullException.ThrowIfNull( model );

		var nodes = model.Nodes;
		var words = new uint[nodes.Count];
		var children = new int[nodes.Count];
		var otherChildren = new int[nodes.Count];

		for ( var node = 0; node < nodes.Count; ++node )
		{
			if ( nodes[node].ParentIndex is var parent && parent >= 0 && parent < nodes.Count )
				++children[parent];
		}

		for ( var node = 0; node < nodes.Count; ++node )
		{
			if ( (nodes[node].Flags & FileTransformOnly) != 0 )
				words[node] |= TransformOnly | Hidden;

			if ( nodes[node].ParentIndex is var parent && parent >= 0 && parent < nodes.Count
				&& ((nodes[node].Flags & FileTransformOnly) == 0 || children[node] > 0) )
				++otherChildren[parent];
		}

		for ( var node = 0; node < nodes.Count; ++node )
		{
			if ( children[node] > 0 && otherChildren[node] == 0 )
				words[node] |= HoldsOnlyMarkers;

			if ( IsLookup( model, node ) && (nodes[node].IdFlags & PositionFlags) != 0 )
				words[node] |= Placed;
		}

		return words;
	}

	/// <summary>
	/// Marks <paramref name="words"/> as the engine marks a model's nodes when it binds <paramref name="clip"/> to a
	/// channel (<c>FUN_00472d70</c>): each track's node with what the track carries, and, with
	/// <paramref name="hide"/>, each node of the clip's hide list hidden, a transform-only node apart.
	/// </summary>
	public static void Bind( uint[] words, AnimationFile clip, bool hide )
	{
		ArgumentNullException.ThrowIfNull( words );
		ArgumentNullException.ThrowIfNull( clip );

		foreach ( var (node, flags) in clip.Tracks )
		{
			if ( node >= words.Length )
				continue;

			words[node] |= Tracked;

			if ( (flags & 0x1000) != 0 )
				words[node] |= Morphed;

			if ( (flags & 0x10000) != 0 )
				words[node] |= TextureMoved;

			if ( (flags & 0x289) != 0 )
				words[node] |= Moved;
		}

		if ( !hide )
			return;

		foreach ( var node in clip.HideList )
		{
			if ( node < words.Length && (words[node] & TransformOnly) == 0 )
				words[node] |= Hidden;
		}
	}

	/// <summary>
	/// Hides and shows each node <paramref name="clip"/> has a visibility track for, as that track stands at
	/// <paramref name="frame"/> (<c>FUN_00471860</c>, which does not spare a transform-only node as the hide list
	/// does). A track with no entry at or before the frame leaves its node alone.
	/// </summary>
	public static void Show( uint[] words, AnimationFile clip, float frame )
	{
		ArgumentNullException.ThrowIfNull( words );
		ArgumentNullException.ThrowIfNull( clip );

		foreach ( var track in clip.VisibilityTracks )
		{
			if ( track.TargetIndex >= words.Length || track.VisibleAt( frame ) is not { } visible )
				continue;

			words[track.TargetIndex] = visible ? words[track.TargetIndex] & ~Hidden : words[track.TargetIndex] | Hidden;
		}
	}

	/// <summary>
	/// Takes <paramref name="clip"/>'s marks off as the engine does when another clip is started on its channel
	/// (<c>FUN_00472310</c>): the track bits of each node it has a track for, and the hidden bit of each node of its
	/// hide list and of its tracks, a transform-only node's apart.
	/// </summary>
	public static void Unbind( uint[] words, AnimationFile clip )
	{
		ArgumentNullException.ThrowIfNull( words );
		ArgumentNullException.ThrowIfNull( clip );

		void Unhide( int node )
		{
			if ( node < words.Length && (words[node] & TransformOnly) == 0 )
				words[node] &= ~Hidden;
		}

		foreach ( var (node, _) in clip.Tracks )
		{
			if ( node < words.Length )
				words[node] &= ~TrackBits;

			Unhide( node );
		}

		foreach ( var node in clip.HideList )
			Unhide( node );
	}

	/// <summary>
	/// A model's node words as its clips start, kept as the engine keeps them on the model's nodes: begun from a
	/// fresh model or from the words a park file holds, changed by each clip started (<see cref="Started"/>), and
	/// read with each running clip's visibility tracks laid over them (<see cref="Words"/>).
	/// </summary>
	public sealed class Running
	{
		private readonly uint[] _words;
		private readonly AnimationFile?[] _bound;

		/// <summary>A model just made, with nothing bound on any of its <paramref name="channels"/>.</summary>
		public Running( ModelFile model, int channels )
		{
			_words = NodeWordsAtRest( model );
			_bound = new AnimationFile?[Math.Max( channels, 1 )];
		}

		/// <summary>
		/// A model as a park file left it: its words, and the clip its record has each channel running, null for
		/// an idle one. An idle channel's marks are its last clip's, which the file does not name.
		/// </summary>
		public Running( IReadOnlyList<uint> fileWords, IReadOnlyList<AnimationFile?> fileClips )
		{
			ArgumentNullException.ThrowIfNull( fileWords );
			ArgumentNullException.ThrowIfNull( fileClips );

			_words = [.. fileWords];
			_bound = [.. fileClips];
		}

		/// <summary>How many nodes the words are of.</summary>
		public int Count => _words.Length;

		/// <summary>
		/// <paramref name="clip"/> is started on <paramref name="channel"/> and bound (a start without the
		/// caller's <c>0x8</c>; <c>FUN_00472f60</c>): the clip the channel last ran is taken off
		/// (<see cref="Unbind"/>), its visibility tracks first laid down as they stood at
		/// <paramref name="outgoingFrame"/>, which is what a transform-only node keeps of them; the other
		/// channels' clips mark their nodes again; and the new clip is bound, its hide list applied.
		///
		/// <para>
		/// Where the channel has no clip to take off (nothing was started on it, or the file had it idle and does
		/// not name its last clip), every track bit and every mesh's hidden bit is taken off in its place, and the
		/// other channels' clips put their track bits back; marks another idle channel's unnamed clip left go
		/// with them.
		/// </para>
		/// </summary>
		public void Started( int channel, AnimationFile clip, float outgoingFrame )
		{
			ArgumentNullException.ThrowIfNull( clip );

			if ( channel < 0 || channel >= _bound.Length )
				return;

			if ( _bound[channel] is { } outgoing )
			{
				Show( _words, outgoing, outgoingFrame );
				Unbind( _words, outgoing );
			}
			else
			{
				for ( var node = 0; node < _words.Length; ++node )
					_words[node] &= ~((_words[node] & TransformOnly) != 0 ? TrackBits : TrackBits | Hidden);
			}

			_bound[channel] = clip;

			for ( var other = 0; other < _bound.Length; ++other )
			{
				if ( other != channel && _bound[other] is { } standing )
					Bind( _words, standing, hide: false );
			}

			Bind( _words, clip, hide: true );
		}

		/// <summary>
		/// The words as a save takes them: each channel's bound clip's visibility tracks laid over them at the
		/// frame the channel stands on (<paramref name="frames"/>, a channel a frame; past the end for a clip
		/// that has ended). The stale-normals bit of a file's word is kept; none is set.
		/// </summary>
		public uint[] Words( IReadOnlyList<float> frames )
		{
			ArgumentNullException.ThrowIfNull( frames );

			var words = (uint[])_words.Clone();

			for ( var channel = 0; channel < _bound.Length && channel < frames.Count; ++channel )
			{
				if ( _bound[channel] is { } clip )
					Show( words, clip, frames[channel] );
			}

			return words;
		}
	}

	/// <summary>A lookup record has a position where its file flags meet this (<c>0x0044a904</c>).</summary>
	private const uint PositionFlags = 0x30;

	/// <summary>A childless lookup node is posed all the same where its record's file flags meet this (FileFormats <c>models.md</c>, "Which records have a position").</summary>
	private const uint AlwaysPosedFlags = 0x580f00;

	/// <summary>A lookup record's file flag for a head node.</summary>
	private const uint HeadFlag = 0x80;

	/// <summary>The lookup records' shared flags: some record has a position.</summary>
	public const int SharedHasPosition = 0x1;

	/// <summary>The shared flags: the model's head nodes were hidden as its item was loaded (<c>FUN_0044ba60</c>).</summary>
	public const int SharedHeadsHidden = 0x2;

	/// <summary>The shared flags: some record's file flags carry <c>0x2</c>.</summary>
	public const int SharedWalkable = 0x8;

	/// <summary>
	/// Each lookup record's pair on a model with nothing attached: its runtime flags, and <c>-1</c> for the handle
	/// of a record with a position, nought for one without. The flags are <c>0x1</c> it has a position, <c>0x8</c>
	/// it is posed whatever its children (its file flags meet <c>0x580f00</c>, or its item sets
	/// <paramref name="doHeadProcessing"/>), <c>0x10</c> its node's file flags carry <c>0x400</c>, and <c>0x20</c>
	/// its node has no child (<c>FUN_0044a870</c>, <c>FUN_0044aa10</c>).
	/// </summary>
	public static (int Flags, int Handle)[] Lookups( ModelFile model, bool doHeadProcessing )
	{
		ArgumentNullException.ThrowIfNull( model );

		var nodes = model.Nodes;
		var hasChild = new bool[nodes.Count];

		foreach ( var node in nodes )
		{
			if ( node.ParentIndex >= 0 && node.ParentIndex < nodes.Count )
				hasChild[node.ParentIndex] = true;
		}

		var records = new (int Flags, int Handle)[model.LookupCount];

		for ( var record = 0; record < records.Length; ++record )
		{
			var index = model.LookupFirst + record;

			if ( index >= nodes.Count )
			{
				records[record] = (0, 0);
				continue;
			}

			var file = nodes[index].IdFlags;
			var placed = (file & PositionFlags) != 0;

			var flags = (placed ? 0x1 : 0)
				| ((file & AlwaysPosedFlags) != 0 || doHeadProcessing ? 0x8 : 0)
				| ((nodes[index].Flags & 0x400) != 0 ? 0x10 : 0)
				| (hasChild[index] ? 0 : 0x20);

			records[record] = (flags, placed ? -1 : 0);
		}

		return records;
	}

	/// <summary>
	/// The lookup records' shared flags on a bought thing's model with nothing attached:
	/// <see cref="SharedHasPosition"/>, <see cref="SharedWalkable"/>, and <see cref="SharedHeadsHidden"/> where a
	/// record is a head's. An arrival vehicle's model is loaded without the last (the ferry's records read so).
	/// </summary>
	public static int SharedFlags( ModelFile model )
	{
		ArgumentNullException.ThrowIfNull( model );

		var shared = 0;

		for ( var record = 0; record < model.LookupCount && model.LookupFirst + record < model.Nodes.Count; ++record )
		{
			var file = model.Nodes[model.LookupFirst + record].IdFlags;

			if ( (file & PositionFlags) != 0 )
				shared |= SharedHasPosition;

			if ( (file & 0x2) != 0 )
				shared |= SharedWalkable;

			if ( (file & HeadFlag) != 0 )
				shared |= SharedHeadsHidden;
		}

		return shared;
	}

	private static bool IsLookup( ModelFile model, int node )
		=> node >= model.LookupFirst && node < model.LookupFirst + model.LookupCount;
}
