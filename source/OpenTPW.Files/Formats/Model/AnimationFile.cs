using System.Text;

namespace OpenTPW;

/// <summary>
/// The animation half of the .md2 format - the files that <see cref="ModelFile.IsAnimation"/>
/// flags (mesh table pointer at 0x70 is zero). See the notes on <see cref="ModelFile"/> for how
/// the two kinds relate and for the evidence behind the layout below.
///
/// Layouts marked "confirmed against the engine" below were checked against the original game's
/// own pointer-relocation routine, which walks a freshly loaded .md2 turning every stored file
/// offset into an absolute pointer. What it relocates is a pointer and what it skips is not, and
/// the counts and strides it loops with are the record sizes - so it is a direct statement of
/// the format rather than an inference from the data.
///
/// An animation is a list of TRACKS, each posing one node of the base model over a range of
/// authoring frames. A track can carry several kinds of channel at once - a fountain's water
/// meshes carry vertex morph, UV scroll and a timing scalar together - so the channels are read
/// independently and the ones we don't understand are simply left alone.
///
/// LOCATING THE TRACKS
///
/// The uint at 0x98 points at a 72-byte animation block; that pointer is valid in 1278 of the
/// game's 1279 animation files, and the engine only reads it when the uint at 0x08 is exactly
/// 0xCB - that field is an animation-format version, not the constant it looks like. In the
/// block:
///
///   - uint at +0x08 is the last frame of the animation.
///   - ushort at +0x12 is the track count, uint at +0x2C the offset of the track table.
///
/// The table is track count * 64 bytes and ends exactly where the block begins. That identity
/// is what makes it safe to trust rather than guess at, and it is the guard this class relies
/// on: 1189 of 1279 files satisfy it, and the rest are rejected rather than read.
///
/// TRACK DESCRIPTORS
///
/// Each descriptor is 64 bytes: its own index at +0x00 (checked, must equal its position), a
/// flag word at +0x04 saying which channels it carries, and the target node at +0x14. Every
/// channel bit owns a slot elsewhere in the descriptor, so an unknown channel costs nothing:
///
///     bit          slot                                     tracks  decoded
///     0x00008   rotation, count (ushort) at +0x10, at +0x1C     3039  yes
///     0x01000   vertex morph descriptor at +0x28                1766  yes
///     0x10000   UV animation descriptor at +0x2C                 690  yes
///     0x20000   visibility, count (ushort) at +0x16, at +0x30    2536  yes
///     0x00001   position record at +0x18                          1216  yes
///     0x80|0x100 unidentified, data at +0x20                     644  no
///     0x00200   unidentified, data at +0x24                       71  no
///
/// Every one of those correspondences is exact - across all 1279 files, not one track sets a
/// bit without filling its slot or fills a slot without setting the bit. 0x80 and 0x100 always
/// appear together and share the one slot.
///
/// The eighth pointer at +0x34 that no flag bit owns is the EASING CURVE TABLE, and it is read
/// here now - see RotationTrack.Ease. It is set on 1768 tracks and every one of them is a
/// rotation track (of 3039), so it is an optional extra for rotation rather than a channel in
/// its own right.
///
/// A record is EIGHT BYTES and the ushort at a key's +0x02 indexes them. That stride is what an
/// earlier note here was missing: read at no fixed length the records looked ragged and about a
/// third of them non-monotonic, and the "byte ramp" it quoted (32, 66, 105, 141, 176, 208, 233,
/// 249) turns out to be exactly curve 0 of the advisor's first clip, read at the right stride by
/// luck. Measured at eight over the 1,166 clips whose track table validates: 1,759 of 3,022
/// rotation tracks carry a table and 1,263 do not.
///
/// Bit 0x4000 is a modifier rather than a channel: it makes the +0x28 slot point at a different
/// structure, and the engine's loader branches on it before reading any morph table. Thirty
/// tracks in the game set it, always alongside 0x1000, and they must not be read as morph.
///
/// The target at +0x14 is a USHORT, not a uint - +0x16 holds a separate value and is nonzero on
/// 595 of the game's rotation tracks, so reading 32 bits there yields a garbage node index. It
/// indexes the base model's node list, which is its mesh list for models with no extra
/// hierarchy: Jun_gateM1's two tracks take the gate's door01/door02 meshes from identity to a
/// quarter turn about model Y (up), and Jun_gateM2 is exactly the inverse - a gate swinging
/// open and shut.
///
/// The full node count is the ushort at the model's 0x42, and the meshes are only the first
/// ushort-at-0x44 of them. The engine indexes a node as
///
///     node below meshCount ? meshTable(0x70) + node * 0xA0
///                          : nodeTable(0x74) + (node - meshCount) * 0x58
///
/// so the rest are 88-byte records at header 0x74. 2967 of the game's 2970 animation targets
/// fall inside the node count, against 2272 inside the mesh count, so a target above the mesh
/// count is a real node we have no geometry for rather than a bad read. Callers must
/// range-check and skip those.
///
/// ROTATION (bit 0x8)
///
/// A keyframe is 20 bytes: ushort frame, ushort EASING CURVE ID, then a float quaternion x, y,
/// z, w. That second field was recorded here as "flags (0 or 0xFFFF)", which was wrong in a way
/// worth naming: 0xFFFF does mean "none", but 0 is not a cleared flag - it is curve number nought,
/// the commonest id in the game, so the two values it was seen taking were a real index and a
/// sentinel. Ids reach 100. See RotationTrack.Ease.
///
/// All 3039 rotation tracks in the game decode to strictly ascending frame indices and unit
/// quaternions (within 1e-2), which is the check applied here.
///
/// VERTEX MORPH (bit 0x1000)
///
/// The slot at +0x28 points at a descriptor: ushort record count at +0x02, uint record table
/// offset at +0x0C, and three floats each at +0x14 and +0x20 - the centre and step of the box this
/// track's positions are quantised into, which is the track's own and not its mesh's (see
/// MorphTrack.DecodePosition). Each track has its OWN descriptor and its own channel space, so one
/// animation morphs as many meshes as it has morph tracks - ratraceM1 morphs four (and three of
/// those meshes share a vertex count, so identifying targets by vertex count cannot tell them
/// apart; the target index can). 752 animation files carry readable morph tracks, 360 of them
/// more than one; there are 1766 morph tracks in all, 30 of which set 0x4000 and are skipped.
///
/// A morph track has exactly one channel per vertex of the mesh it targets, plus two trailing
/// channels that aren't vertices - 457 of the 464 tracks whose model resolves satisfy that
/// exactly. The 7 that don't (droidm2 names a 16-vertex mesh but carries 3561 channels) are
/// models whose node list evidently isn't their mesh list, the same caveat the target index
/// carries generally, so callers should check the count and skip a track that fails it rather
/// than morph the wrong mesh into nonsense. The two trailing channels are the corners of the box
/// the vertices span at each keyframe, minimum then maximum - the engine reads them as the
/// animation's bounding box (0x004711d0) - and in all 464 tracks whose model resolves, they sit on
/// the corners of the first keyframe's vertices to within three quantisation steps.
///
/// Each record is: ushort a (keyframes), ushort b (channels), uint p1, uint p2, uint p3, uint
/// reserved. p1 is an array of b channel ids, p2 an array of a ascending frame indices, p3 a
/// block of a*b 4-byte values. Both arrays are padded to a 4-byte boundary, so p2 - p1 is 2*b
/// or 2*b+2, and p3 - p2 is 2*a or 2*a+2. The value block is entry-major: keyframe e of channel
/// slot k lives at p3 + (e * b + k) * 4. Channels the animation doesn't move are still present,
/// in a record holding a single rest keyframe, so sampling every channel reproduces the mesh.
///
/// POSITION (bit 0x1)
///
/// The slot at +0x18 points at a 16-byte record: a uint type, ushort point count, ushort key count,
/// uint -> points (point count * 3 floats) and uint -> keys (key count * 4 bytes, a ushort frame
/// then a ushort that is zero in every file). The type says how the points are joined, and the
/// engine picks its sampler on exactly those bits:
///
///   - bit 0x2 (type 0x12, 785 tracks): a cubic Bezier through segments of four points, three new
///     points per key - so point count is always 3 * (keys - 1) + 1. 0x00474840 evaluates it with
///     the Bezier basis (constants 1, 3, -3, -6 at 0x006fecb8).
///   - bit 0x8 (type 0x18, 431 tracks): straight lines, one point per key - 0x00474bf0.
///   - neither: a Catmull-Rom sampler exists (0x00474cc0) but no file in the game uses it, so it
///     is not read here.
///
/// A point is where the node sits relative to its parent, replacing its authored position - the
/// advisor's body rests at (0, 0.21, -21.2) and his clip 14 raises it there from -73.2. Every one
/// of the 1216 records in the game has the point count its type says; key frames never go
/// backwards, though four tracks repeat a frame.
///
/// VISIBILITY (bit 0x20000)
///
/// A count at +0x16 (the same ushort that is not part of the target) and an array of that many
/// signed shorts at +0x30. Each is a frame number whose sign says what happens from that frame
/// on: above zero the node is shown, zero or below it is hidden. The engine (0x00471860) takes the
/// last entry whose absolute value is at or before the current frame and sets or clears the
/// node's hidden bit, 0x10, from it. All 2536 tracks in the game are ordered that way. It is how
/// the advisor blinks - clip 10 hides his eyes at frame 40 and shows his eyelids, and swaps them
/// back at 44.
///
/// UV ANIMATION (bit 0x10000)
///
/// The slot at +0x2C points at a 20-byte descriptor: uint entry count n at +0x00, uint -> index
/// table at +0x04, uint total component count at +0x08, uint -> duration table at +0x0C, uint ->
/// value table at +0x10. The three tables are contiguous: indices are n * 4 bytes, values are
/// 8 * (total components), durations are n * 4.
///
///   - index table: per entry, ushort first component and ushort component count. UV components
///     are two per coordinate, so an entry covering a whole UV is (2i, 2). A channel that
///     animates every UV of its mesh is the identity (2i, 2) with n == the mesh's vertex order
///     length; one that animates a subset names the components it wants.
///   - value table: per component, a start float and an end float.
///   - duration table: per entry, a ushort pair whose high half is the end frame.
///
/// All 690 UV channels in the game satisfy every one of those invariants - the component counts
/// sum to the stated total, and both table sizes fall exactly where the count says they should.
/// Jun_isleM1 scrolls Post Ripples01's 128 UVs by (-1,-1) over 100 frames, and 104 of the
/// Island mesh's 298 UVs by the same delta, which is the shoreline water lapping the beach.
///
/// COVERAGE
///
/// Of the game's 1279 animation files, 1189 have tracks, and 1183 of those declare at least one
/// channel read here - counted by the flag bits their tracks carry: 768 morph, 686 rotation, 324
/// UV, 455 position and 404 visibility. The other 6 carry only channels we don't decode.
///
/// The remaining 90 hold no animation at all rather than defeating the parser: 89 declare a
/// track count of zero, and 1 has no animation block. The table identity above does not fail
/// on a single file in the game, so nothing is rejected for being unreadable.
///
/// FirstFrame and LastFrame still span only rotation, morph and UV keys, so a file whose tracks move
/// nothing but positions and visibility reads as having no span.
///
/// That was recorded here as 19 files, and it is wrong. Measured 2026-09-15 against the span each file
/// declares in its own animation block, over the 1140 clips under levels/ that carry one: 159 disagree
/// with what the keys cover - 129 where the keys fall short, of which 114 read as no span whatever, and
/// 30 where the keys run past the declared end. Every one of the 114 declares a real length and some are
/// long, the ferries running 600 frames. See DeclaredFirstFrame, which is the span the engine itself
/// answers with and the one to use where a clip's length is wanted rather than its key coverage.
/// </summary>
public class AnimationFile : BaseFormat
{
	/// <summary>
	/// How many keyframes an animation moves through in a second: keyframe numbers are frames at
	/// thirty a second. The engine advances a playing animation by elapsed milliseconds times 0.03
	/// (0x00472f60) and works out a clip's length as frames * 1000 / 30 (0x00474070).
	///
	/// It is a speed, not a frame rate to draw at. The engine's samplers take the frame as a
	/// fraction - its Bezier sampler, 0x00474840, splits it into a segment and a position within
	/// it - so the poses between keyframes are interpolated rather than held, and a pose is the
	/// same function of time whether it is drawn thirty times a second or a hundred and forty-four.
	/// </summary>
	public const float FramesPerSecond = 30f;

	/// <summary>
	/// How long one frame lasts, in milliseconds, as the engine holds it: the <b>32-bit float</b> at
	/// <c>0x006fec08</c> (<c>0x42055555</c>), which is 33.33333206176758 and not the exact 1000/30.
	///
	/// <para>
	/// <b>The difference is not academic, because the engine truncates.</b> It computes a clip's length as
	/// <c>__ftol(frames * this)</c> at <c>0x004733cc</c>-<c>0x004733d6</c>, and <c>__ftol</c>
	/// (<c>0x0067a830</c>) is not a rounding conversion: it saves the x87 control word, <c>OR AH,0xc</c> to
	/// set rounding toward zero, <c>FISTP</c>, and restores it. So the product falls to the integer below.
	/// Where the span is a multiple of three the exact arithmetic lands on a whole millisecond and this one
	/// lands just under it - 600 frames is 19999 here and 20000 by <c>frames * 1000 / 30</c> - and that is
	/// <b>293 of the 1,237 clips under levels/ that declare a span</b>, measured 2026-09-15.
	/// </para>
	/// </summary>
	public const double MillisecondsPerFrame = 33.33333206176758;

	/// <summary>One record of a vertex morph track: some channels, sampled at some frames.</summary>
	public class Track
	{
		public ushort[] ChannelIds { get; init; } = Array.Empty<ushort>();
		public ushort[] FrameIndices { get; init; } = Array.Empty<ushort>();

		/// <summary>Entry-major: keyframe e of channel slot k is at e * ChannelIds.Length + k.</summary>
		public uint[] RawValues { get; init; } = Array.Empty<uint>();

		public bool IsConstant => FrameIndices.Length <= 1;

		public uint Raw( int keyframe, int slot ) => RawValues[(keyframe * ChannelIds.Length) + slot];
	}

	/// <summary>
	/// One mesh's vertex morph, with its own records and its own channel space - see the notes
	/// on this class. <see cref="ChannelCount"/> is the target mesh's vertex count + 2.
	/// </summary>
	public class MorphTrack
	{
		public int TargetIndex { get; init; }
		public Track[] Records { get; init; } = Array.Empty<Track>();
		public int ChannelCount { get; private set; }

		/// <summary>
		/// The middle of the box this track's positions are quantised into - three floats at
		/// descriptor +0x14. See <see cref="DecodePosition"/>.
		/// </summary>
		public Vector3 Centre { get; init; }

		/// <summary>How far one step of a 10-bit field moves along each axis - three floats at descriptor +0x20.</summary>
		public Vector3 Step { get; init; }

		/// <summary>
		/// A keyframe value as a position in the mesh's own space. The value is three signed
		/// 10-bit fields - X in bits 0..9, Y in 10..19, Z in 20..29, bits 30 and 31 unused - and
		/// each is multiplied by this track's <see cref="Step"/> and added to its
		/// <see cref="Centre"/>, which is how the engine decodes them (0x00470e90, reading the
		/// descriptor's +0x14 and +0x20).
		///
		/// The box is the track's own, not its mesh's. It is usually close to the mesh's bounding
		/// box, but in none of the 464 tracks whose model resolves is it the same. Decoded in the
		/// track's box, the first keyframe lands on the mesh's rest vertices to within one and a
		/// half quantisation steps in 346 of them; decoded in the mesh's bounding box, in 63. It
		/// shows most in the advisor's clip 14, whose antennae are quantised into a box nearly twice
		/// as tall as in his other clips: read in the mesh's box, they came out at a little over half
		/// their height for as long as he rose, then jumped back when his next clip began.
		/// </summary>
		public Vector3 DecodePosition( uint raw )
			=> new( (Field( raw, 0 ) * Step.X) + Centre.X, (Field( raw, 10 ) * Step.Y) + Centre.Y, (Field( raw, 20 ) * Step.Z) + Centre.Z );

		private static int Field( uint raw, int shift )
		{
			var field = (int)((raw >> shift) & 0x3FF);
			return (field & 0x200) != 0 ? field - 1024 : field;
		}

		private (int Track, int Slot)[] _channelLookup = Array.Empty<(int, int)>();

		internal void BuildLookup()
		{
			var maxChannel = -1;
			foreach ( var record in Records )
			{
				foreach ( var id in record.ChannelIds )
					maxChannel = Math.Max( maxChannel, id );
			}

			ChannelCount = maxChannel + 1;

			_channelLookup = new (int, int)[ChannelCount];
			Array.Fill( _channelLookup, (-1, -1) );

			for ( int t = 0; t < Records.Length; ++t )
			{
				var ids = Records[t].ChannelIds;
				for ( int s = 0; s < ids.Length; ++s )
					_channelLookup[ids[s]] = (t, s);
			}
		}

		public bool TryGetChannel( int channelId, out Track? track, out int slot )
		{
			track = null;
			slot = -1;

			if ( channelId < 0 || channelId >= _channelLookup.Length )
				return false;

			var (trackIndex, channelSlot) = _channelLookup[channelId];
			if ( trackIndex < 0 )
				return false;

			track = Records[trackIndex];
			slot = channelSlot;
			return true;
		}
	}

	/// <summary>One node's rotation over time, as a sequence of quaternion keyframes.</summary>
	public class RotationTrack
	{
		public int TargetIndex { get; init; }
		public ushort[] FrameIndices { get; init; } = Array.Empty<ushort>();
		public System.Numerics.Quaternion[] Rotations { get; init; } = Array.Empty<System.Numerics.Quaternion>();

		/// <summary>The id a key carries when it blends straight into the next one.</summary>
		public const ushort NoCurve = 0xFFFF;

		/// <summary>
		/// The easing curve each key blends <i>out of</i>, or <see cref="NoCurve"/> where it blends evenly -
		/// the ushort at key +0x02. See <see cref="Ease"/>; the last key of a track never names one.
		/// </summary>
		public ushort[] CurveIds { get; init; } = Array.Empty<ushort>();

		/// <summary>
		/// Eight bytes per curve, indexed by the id a key names - the table at track descriptor +0x34.
		/// Empty where this track's keys all blend evenly.
		/// </summary>
		public byte[][] Curves { get; init; } = Array.Empty<byte[]>();

		/// <summary>
		/// How many segments a curve's ramp is cut into, as the engine's own constant at
		/// <c>0x006febe4</c>: <b>8.999995231628418</b>, deliberately a hair under nine.
		///
		/// <para>
		/// The shortfall is the point. The engine truncates <c>t * this</c> toward zero to pick a segment,
		/// so an exact nine would send <c>t == 1</c> into a tenth segment that has no upper point to reach
		/// and would answer <c>curve[7]/255</c> instead of 1 - a jump backwards on the very last frame of a
		/// blend. Just under nine keeps <c>t == 1</c> in segment eight, where it lands on 1 exactly.
		/// </para>
		/// </summary>
		private const float Segments = 8.999995231628418f;

		/// <summary>
		/// What each curve byte is worth - the float at <c>0x006febec</c>, which is one 255th. So a byte of
		/// 255 is the whole blend and a byte of 0 is none of it.
		/// </summary>
		private const float ByteScale = 0.003921568859368563f;

		/// <summary>
		/// Where between two keys the pose really is, once the curve the <i>lower</i> key names has had its
		/// say. <paramref name="t"/> is the even fraction between the two keys and the answer replaces it.
		///
		/// <para>
		/// <b>The curve is ten points and nine straight segments.</b> The eight bytes are the inner points;
		/// the first and last are implied, nought before the first byte and one after the last. So the ramp
		/// runs 0 -> curve[0] -> ... -> curve[7] -> 1, and the engine picks its segment by truncating
		/// <c>t * <see cref="Segments"/></c> and lerps across it with what is left over (0x00471c83-0x00471d32).
		/// That implied one at the end is doing real work: the last byte is 255 in only <b>112 of the game's
		/// 12,428 curve entries</b>, so almost every curve climbs to its finish in that ninth segment.
		/// </para>
		///
		/// <para>
		/// <b>A curve need not rise all the way along, and 2,797 of those 12,428 entries do not.</b> One that
		/// dips takes the pose back the way it came for a moment, which is an author's overshoot and not a
		/// bad read - so this reproduces it rather than sorting it. An earlier note here called a third of
		/// these non-monotonic, which was measured before the record length was known; at the true stride of
		/// eight it is about 22%.
		/// </para>
		/// </summary>
		public float Ease( int key, float t )
		{
			if ( key < 0 || key >= CurveIds.Length )
				return t;

			var id = CurveIds[key];

			// A track whose table could not be read keeps every blend even, which is what an id past the
			// end of Curves means. No file in the game is in that state - all 1,759 tracks carrying an id
			// carry a table in range - so this is a guard against bad data rather than a path the game takes.
			if ( id == NoCurve || id >= Curves.Length )
				return t;

			var curve = Curves[id];

			var scaled = t * Segments;
			var segment = (int)scaled;
			var into = scaled - segment;

			float from, to;

			if ( segment <= 0 )
			{
				from = 0f;
				to = curve[0] * ByteScale;
			}
			else if ( segment < curve.Length )
			{
				from = curve[segment - 1] * ByteScale;
				to = curve[segment] * ByteScale;
			}
			else
			{
				from = curve[^1] * ByteScale;
				to = 1f;
			}

			return (from * (1f - into)) + (to * into);
		}

		/// <summary>Rotation at an authoring frame, held at the ends and slerped between keys.</summary>
		public System.Numerics.Quaternion Sample( float frame )
		{
			if ( Rotations.Length == 0 )
				return System.Numerics.Quaternion.Identity;

			if ( Rotations.Length == 1 || frame <= FrameIndices[0] )
				return Rotations[0];

			if ( frame >= FrameIndices[^1] )
				return Rotations[^1];

			var hi = 1;
			while ( hi < FrameIndices.Length && FrameIndices[hi] < frame )
				hi++;

			var lo = hi - 1;
			var span = FrameIndices[hi] - FrameIndices[lo];
			var t = span <= 0 ? 0f : (frame - FrameIndices[lo]) / span;

			// The curve belongs to the key being blended out of, which is why it is the lower one that is
			// asked - the engine reads the id off the key its own search settled on (0x00471c73).
			return System.Numerics.Quaternion.Slerp( Rotations[lo], Rotations[hi], Ease( lo, t ) );
		}
	}

	/// <summary>
	/// One mesh's UV animation: one entry per vertex, each carrying its own run of keyframes.
	///
	/// <para>
	/// <b>An entry is a vertex and its keys, not a run of components between two values.</b> The engine's
	/// sampler (<c>FUN_004745c0</c>) reads the index table as a <i>first key</i> and a <i>key count</i>,
	/// takes one frame number per key from the table at descriptor <c>+0x0c</c> and one (u,v) pair per key
	/// from the one at <c>+0x10</c>, and interpolates between the two keys the frame falls between. It
	/// writes entry <c>e</c> to the node's UV array at <c>(e &gt;&gt; 2) * 0x20 + (e &amp; 3) * 4</c>, which
	/// is four u's then four v's - so the entry index <i>is</i> the vertex index, with no component
	/// arithmetic anywhere.
	/// </para>
	///
	/// <para>
	/// <b>This was read as a two-point ramp, and that is right only when an entry has exactly two keys.</b>
	/// Measured over every clip under levels/ on 2026-09-15: of 29,723 entries, 25,332 have two keys and
	/// 4,391 do not, running as high as 105 - and those fall in <b>289 of the 670 UV tracks, 43%</b>. The
	/// two readings size their tables identically, which is why the old one passed every bounds check it
	/// had: a two-key entry packs so that its first key index is exactly twice its entry index, so
	/// "component 2e, two components" and "key 2e, two keys" address the same bytes. The fountains and the
	/// advisor's fan are the visible casualties - <c>fountainm.md2</c> carries 44 entries of five keys and
	/// <c>fountainc.md2</c> 44 of ten, every one of which was flattened to its first and last.
	/// </para>
	/// </summary>
	public class UvTrack
	{
		public int TargetIndex { get; init; }

		/// <summary>Where each entry's keys begin in <see cref="Frames"/> and <see cref="Coordinates"/>.</summary>
		public int[] FirstKey { get; init; } = Array.Empty<int>();

		/// <summary>How many keys each entry has - two for most, and up to 105.</summary>
		public int[] KeyCount { get; init; } = Array.Empty<int>();

		/// <summary>One frame number per key, ascending within an entry.</summary>
		public ushort[] Frames { get; init; } = Array.Empty<ushort>();

		/// <summary>One texture coordinate per key.</summary>
		public System.Numerics.Vector2[] Coordinates { get; init; } = Array.Empty<System.Numerics.Vector2>();

		/// <summary>One per vertex this track drives.</summary>
		public int EntryCount => FirstKey.Length;

		/// <summary>The last frame any of this track's keys names - see <see cref="LastFrame"/> on the file.</summary>
		public int LastKeyFrame
		{
			get
			{
				var last = 0;

				foreach ( var frame in Frames )
					last = Math.Max( last, frame );

				return last;
			}
		}

		/// <summary>
		/// Where one entry's vertex has its texture coordinate at <paramref name="frame"/>, interpolated
		/// between the keys it falls between and held at the last one past the end.
		///
		/// <para>
		/// The engine finds the first key whose frame is above the current one and blends from the key
		/// before it, falling back to the final pair with a blend of one when the frame is past them all.
		/// It compares against the <b>truncated</b> frame while blending with the exact one, which is
		/// reproduced here. Its search would index the key before the first if a run ever began above frame
		/// nought; <b>none does</b> - all 29,723 entries in the game start at frame 0 - so clamping there
		/// is a guard against bad data rather than a departure.
		/// </para>
		/// </summary>
		public System.Numerics.Vector2 Sample( int entry, float frame )
		{
			if ( entry < 0 || entry >= FirstKey.Length )
				return System.Numerics.Vector2.Zero;

			var first = FirstKey[entry];
			var count = KeyCount[entry];

			if ( count <= 0 )
				return System.Numerics.Vector2.Zero;

			if ( count == 1 )
				return Coordinates[first];

			// The last pair, blended fully onto its end - what the engine holds when the frame is past
			// every key this entry has.
			var lo = count - 2;
			var t = 1f;

			var current = (int)frame;

			for ( int key = 0; key < count; ++key )
			{
				if ( current >= Frames[first + key] )
					continue;

				lo = Math.Max( key - 1, 0 );

				float from = Frames[first + lo], to = Frames[first + lo + 1];
				t = to > from ? Math.Clamp( (frame - from) / (to - from), 0f, 1f ) : 0f;
				break;
			}

			return System.Numerics.Vector2.Lerp( Coordinates[first + lo], Coordinates[first + lo + 1], t );
		}
	}

	/// <summary>Where a node sits over time - channel 0x1. See the class remarks.</summary>
	public class PositionTrack
	{
		public int TargetIndex { get; init; }

		/// <summary>One per key, ascending, though a key can repeat the frame before it.</summary>
		public ushort[] FrameIndices { get; init; } = Array.Empty<ushort>();

		/// <summary>Three per key after the first for a Bezier track, one per key for a straight one.</summary>
		public System.Numerics.Vector3[] Points { get; init; } = Array.Empty<System.Numerics.Vector3>();

		public bool IsBezier { get; init; }

		/// <summary>The node's position at <paramref name="frame"/>, held at the ends.</summary>
		public System.Numerics.Vector3 Sample( float frame )
		{
			var keys = FrameIndices.Length;
			var last = IsBezier ? 3 * (keys - 1) : keys - 1;

			if ( keys == 0 || Points.Length <= last )
				return System.Numerics.Vector3.Zero;

			if ( frame <= FrameIndices[0] )
				return Points[0];

			if ( frame >= FrameIndices[^1] )
				return Points[last];

			var segment = 0;
			for ( int k = 1; k < keys - 1 && FrameIndices[k] <= frame; ++k )
				segment = k;

			float from = FrameIndices[segment], to = FrameIndices[segment + 1];
			var t = to > from ? (frame - from) / (to - from) : 1f;

			if ( !IsBezier )
				return System.Numerics.Vector3.Lerp( Points[segment], Points[segment + 1], t );

			var i = 3 * segment;
			var u = 1f - t;

			return (Points[i] * (u * u * u)) + (Points[i + 1] * (3f * u * u * t))
				+ (Points[i + 2] * (3f * u * t * t)) + (Points[i + 3] * (t * t * t));
		}
	}

	/// <summary>When a node is shown - channel 0x20000. See the class remarks.</summary>
	public class VisibilityTrack
	{
		public int TargetIndex { get; init; }

		/// <summary>Frame numbers, positive to show from that frame and zero or negative to hide.</summary>
		public short[] Entries { get; init; } = Array.Empty<short>();

		/// <summary>
		/// Whether the node is shown at <paramref name="frame"/>, or null before its first entry -
		/// in which case the engine leaves the node as it was.
		/// </summary>
		public bool? VisibleAt( float frame )
		{
			var current = (int)frame;

			for ( int i = Entries.Length - 1; i >= 0; --i )
			{
				if ( Math.Abs( (int)Entries[i] ) <= current )
					return Entries[i] >= 1;
			}

			return null;
		}
	}

	public List<PositionTrack> PositionTracks { get; } = new();

	public List<VisibilityTrack> VisibilityTracks { get; } = new();

	public List<MorphTrack> MorphTracks { get; } = new();

	public List<UvTrack> UvTracks { get; } = new();

	public List<RotationTrack> RotationTracks { get; } = new();

	/// <summary>False when this file holds no track we can read - callers must check it.</summary>
	public bool IsValid { get; private set; }

	public int FirstFrame { get; private set; }
	public int LastFrame { get; private set; }

	/// <summary>
	/// The span the file <i>declares</i>, read from its animation block rather than worked out from the
	/// keys - the two integers at block +0x04 and +0x08.
	///
	/// <para>
	/// <b>This is the length the engine uses</b>, and it is not the same number as
	/// <see cref="FirstFrame"/> to <see cref="LastFrame"/>. A ride script asking how long an animation
	/// runs is answered with the declared span times 1000/30 (0x004733b1-0x004733db), so a machine
	/// computing it from keys answers a different question. Across the game's levels the two disagree on
	/// <b>159 clips</b>: 129 declare longer than their keys cover and 30 declare shorter, and of those
	/// 129 there are <b>114 our own reader rejects outright</b> - clips carrying position and visibility
	/// only, which nonetheless declare real and sometimes long lengths (the ferries run 600 frames).
	/// </para>
	/// <para>
	/// <b>Every clip in the game declares a start of nought and an end of at least one</b> - measured over
	/// all 1,140 clips under levels/ that carry an animation block, with no exceptions in either
	/// direction - so there is no such thing as a zero-length clip to guard against.
	/// </para>
	/// <para>
	/// These are filled even when the file carries no track this reader understands, so
	/// <see cref="IsValid"/> being false does not mean there is no length here.
	/// </para>
	/// </summary>
	public int DeclaredFirstFrame { get; private set; }

	/// <summary>The other end of the declared span - see <see cref="DeclaredFirstFrame"/>.</summary>
	public int DeclaredLastFrame { get; private set; }

	public AnimationFile( Stream stream )
	{
		ReadFromStream( stream );
	}

	/// <summary>
	/// Loads an animation, returning false if the path doesn't exist or isn't a readable
	/// animation file. FileSystem.FileExists can't see inside archives, so this opens the
	/// stream and null-checks rather than testing for existence first.
	/// </summary>
	public static bool TryLoad( string path, out AnimationFile? animation )
	{
		animation = null;

		try
		{
			using var stream = FileSystem.OpenRead( path );
			if ( stream == null )
				return false;

			var file = new AnimationFile( stream );
			if ( !file.IsValid )
				return false;

			animation = file;
			return true;
		}
		catch ( Exception e )
		{
			Log.Info( $"Couldn't read animation '{path}': {e.Message}" );
			return false;
		}
	}

	/// <summary>The UV track scrolling a given mesh, or null if this animation doesn't scroll it.</summary>
	public UvTrack? UvTrackFor( int targetIndex )
	{
		foreach ( var track in UvTracks )
		{
			if ( track.TargetIndex == targetIndex )
				return track;
		}

		return null;
	}

	/// <summary>The morph track posing a given mesh, or null if this animation doesn't move it.</summary>
	public MorphTrack? MorphTrackFor( int targetIndex )
	{
		foreach ( var track in MorphTracks )
		{
			if ( track.TargetIndex == targetIndex )
				return track;
		}

		return null;
	}

	protected override void ReadFromStream( Stream stream )
	{
		var length = (int)stream.Length;
		var data = new byte[length];
		stream.ReadExactly( data, 0, length );

		if ( !ReadTrackTable( data ) )
			return;

		if ( MorphTracks.Count == 0 && RotationTracks.Count == 0 && UvTracks.Count == 0 )
			return;

		var minFrame = int.MaxValue;
		var maxFrame = int.MinValue;

		foreach ( var track in RotationTracks )
		{
			foreach ( var frame in track.FrameIndices )
			{
				minFrame = Math.Min( minFrame, frame );
				maxFrame = Math.Max( maxFrame, frame );
			}
		}

		foreach ( var track in MorphTracks )
		{
			foreach ( var record in track.Records )
			{
				foreach ( var frame in record.FrameIndices )
				{
					minFrame = Math.Min( minFrame, frame );
					maxFrame = Math.Max( maxFrame, frame );
				}
			}
		}

		// A UV channel names only the frame each entry finishes on - it always ramps from the
		// start of the animation, so these extend the last frame without ever moving the first.
		//
		// Left out, an animation that scrolls UVs and does nothing else spans no frames at all
		// and so has no duration: fantasy's and hallow's islands are exactly that, and their
		// shoreline water sat frozen while jungle's ran, because jungle's clip happens to morph
		// a Dino alongside the same scroll and took its length from that.
		//
		// This can only ever raise the maximum, never lower it, so no animation that already
		// had a duration can change length. Across the game's 1151 animation files with readable
		// channels, 98 gain a last frame here and all 98 had no span whatsoever before.
		foreach ( var track in UvTracks )
			maxFrame = Math.Max( maxFrame, track.LastKeyFrame );

		FirstFrame = minFrame == int.MaxValue ? 0 : minFrame;
		LastFrame = maxFrame == int.MinValue ? 0 : maxFrame;

		IsValid = true;
	}

	/// <summary>
	/// Walks the animation block's track table and reads the channels we understand from each
	/// descriptor, leaving the rest alone. Returns false when the table can't be located - a
	/// mislocated table would produce confidently wrong animation, so it is rejected instead.
	/// A channel that fails its own validation is skipped without discarding the whole file.
	/// </summary>
	private bool ReadTrackTable( byte[] data )
	{
		if ( data.Length < 0x9C )
			return false;

		var blockOffset = BitConverter.ToUInt32( data, 0x98 );

		if ( blockOffset < 0x9C || blockOffset + 0x30 > data.Length )
			return false;

		// Taken here, before anything below can turn the file away: a clip carrying no track this reader
		// understands still declares a real length, and 114 of the game's clips are exactly that. Read as
		// integers rather than floats - the float reading of these two words gives denormal nonsense
		// (2.1E-43 where the integer is 150), which is how the type was settled rather than assumed.
		DeclaredFirstFrame = BitConverter.ToInt32( data, (int)blockOffset + 0x04 );
		DeclaredLastFrame = BitConverter.ToInt32( data, (int)blockOffset + 0x08 );

		int trackCount = BitConverter.ToUInt16( data, (int)blockOffset + 0x12 );
		var tableOffset = BitConverter.ToUInt32( data, (int)blockOffset + 0x2C );

		if ( trackCount <= 0 || tableOffset < 0x9C )
			return false;

		// The table runs right up to the block it is described by. Any other arrangement means
		// we're reading something that isn't the table.
		if ( tableOffset + (64L * trackCount) != blockOffset )
			return false;

		for ( int i = 0; i < trackCount; ++i )
		{
			var offset = (int)tableOffset + (64 * i);

			if ( BitConverter.ToUInt32( data, offset ) != i )
				return false;

			var flags = BitConverter.ToUInt32( data, offset + 0x04 );
			int target = BitConverter.ToUInt16( data, offset + 0x14 );

			if ( (flags & 0x8) != 0 )
				ReadRotationChannel( data, offset, target );

			// Bit 0x4000 makes the +0x28 slot point at a different structure entirely - the
			// engine's own loader branches on it before touching the morph tables. Only 12
			// tracks in the game set it, but reading them as morph would follow bogus offsets.
			if ( (flags & 0x1000) != 0 && (flags & 0x4000) == 0 )
				ReadMorphChannel( data, BitConverter.ToUInt32( data, offset + 0x28 ), target );

			if ( (flags & 0x10000) != 0 )
				ReadUvChannel( data, BitConverter.ToUInt32( data, offset + 0x2C ), target );

			if ( (flags & 0x1) != 0 )
				ReadPositionChannel( data, BitConverter.ToUInt32( data, offset + 0x18 ), target );

			if ( (flags & 0x20000) != 0 )
				ReadVisibilityChannel( data, offset, target );
		}

		return true;
	}

	private void ReadPositionChannel( byte[] data, uint recordAt, int target )
	{
		if ( recordAt < 0x9C || recordAt + 16 > data.Length )
			return;

		var record = (int)recordAt;
		var type = BitConverter.ToUInt32( data, record );
		int pointCount = BitConverter.ToUInt16( data, record + 4 );
		int keyCount = BitConverter.ToUInt16( data, record + 6 );
		var pointsAt = BitConverter.ToUInt32( data, record + 8 );
		var keysAt = BitConverter.ToUInt32( data, record + 12 );

		var bezier = (type & 0x2) != 0;
		var straight = !bezier && (type & 0x8) != 0;

		// The engine's third sampler, for neither bit, is used by nothing in the game.
		if ( keyCount <= 0 || (!bezier && !straight) )
			return;

		if ( pointCount != (bezier ? (3 * (keyCount - 1)) + 1 : keyCount) )
			return;

		if ( pointsAt < 0x9C || pointsAt + (12L * pointCount) > data.Length
			|| keysAt < 0x9C || keysAt + (4L * keyCount) > data.Length )
			return;

		var frames = new ushort[keyCount];
		for ( int k = 0; k < keyCount; ++k )
		{
			frames[k] = BitConverter.ToUInt16( data, (int)keysAt + (4 * k) );
			if ( k > 0 && frames[k] < frames[k - 1] )
				return;
		}

		var points = new System.Numerics.Vector3[pointCount];
		for ( int i = 0; i < pointCount; ++i )
		{
			var at = (int)pointsAt + (12 * i);
			points[i] = new System.Numerics.Vector3(
				BitConverter.ToSingle( data, at ),
				BitConverter.ToSingle( data, at + 4 ),
				BitConverter.ToSingle( data, at + 8 ) );
		}

		PositionTracks.Add( new PositionTrack { TargetIndex = target, FrameIndices = frames, Points = points, IsBezier = bezier } );
	}

	private void ReadVisibilityChannel( byte[] data, int descriptor, int target )
	{
		int count = BitConverter.ToUInt16( data, descriptor + 0x16 );
		var entriesAt = BitConverter.ToUInt32( data, descriptor + 0x30 );

		if ( count <= 0 || entriesAt < 0x9C || entriesAt + (2L * count) > data.Length )
			return;

		var entries = new short[count];
		for ( int i = 0; i < count; ++i )
			entries[i] = BitConverter.ToInt16( data, (int)entriesAt + (2 * i) );

		VisibilityTracks.Add( new VisibilityTrack { TargetIndex = target, Entries = entries } );
	}

	private void ReadRotationChannel( byte[] data, int descriptor, int target )
	{
		int keyCount = BitConverter.ToUInt16( data, descriptor + 0x10 );
		var keysAt = BitConverter.ToUInt32( data, descriptor + 0x1C );

		if ( keyCount <= 0 || keysAt < 0x9C || keysAt + (20L * keyCount) > data.Length )
			return;

		var frames = new ushort[keyCount];
		var rotations = new System.Numerics.Quaternion[keyCount];
		var curveIds = new ushort[keyCount];

		for ( int k = 0; k < keyCount; ++k )
		{
			var entry = (int)keysAt + (20 * k);

			frames[k] = BitConverter.ToUInt16( data, entry );
			if ( k > 0 && frames[k] <= frames[k - 1] )
				return;

			curveIds[k] = BitConverter.ToUInt16( data, entry + 2 );

			rotations[k] = new System.Numerics.Quaternion(
				BitConverter.ToSingle( data, entry + 4 ),
				BitConverter.ToSingle( data, entry + 8 ),
				BitConverter.ToSingle( data, entry + 12 ),
				BitConverter.ToSingle( data, entry + 16 ) );

			if ( Math.Abs( rotations[k].Length() - 1f ) > 0.01f )
				return;
		}

		RotationTracks.Add( new RotationTrack
		{
			TargetIndex = target,
			FrameIndices = frames,
			Rotations = rotations,
			CurveIds = curveIds,
			Curves = ReadCurves( data, BitConverter.ToUInt32( data, descriptor + 0x34 ), curveIds )
		} );
	}

	/// <summary>
	/// The easing curves this track's keys name, out of the eighth pointer at descriptor +0x34 - eight
	/// bytes each, indexed by the id rather than by the key, which is why they are read as a table.
	///
	/// <para>
	/// <b>An id is not a key number, though it very often looks like one.</b> Of the game's 12,428 keys that
	/// name a curve, 9,358 name the one whose id matches their own position and <b>3,070 do not</b> - so
	/// deriving the curve from the key index would be right four times in five and quietly wrong the rest.
	/// Ids run as high as 100, well past any key count, and the table is sized here by the highest one the
	/// keys actually ask for.
	/// </para>
	///
	/// <para>
	/// No table means no easing rather than a rejected track: the caller keeps the rotation either way, and
	/// 1,263 of the game's 3,022 rotation tracks have none - every key on those carries
	/// <see cref="RotationTrack.NoCurve"/>, all 9,051 of them, so the two facts never disagree.
	/// </para>
	/// </summary>
	private static byte[][] ReadCurves( byte[] data, uint tableAt, ushort[] curveIds )
	{
		var highest = -1;

		foreach ( var id in curveIds )
		{
			if ( id != RotationTrack.NoCurve )
				highest = Math.Max( highest, id );
		}

		if ( highest < 0 || tableAt < 0x9C || tableAt + (8L * (highest + 1)) > data.Length )
			return Array.Empty<byte[]>();

		var curves = new byte[highest + 1][];

		for ( int id = 0; id <= highest; ++id )
		{
			curves[id] = new byte[8];
			Array.Copy( data, (int)tableAt + (8 * id), curves[id], 0, 8 );
		}

		return curves;
	}

	private void ReadMorphChannel( byte[] data, uint descriptorAt, int target )
	{
		if ( descriptorAt < 0x9C || descriptorAt + 0x2C > data.Length )
			return;

		int recordCount = BitConverter.ToUInt16( data, (int)descriptorAt + 0x02 );
		var tableOffset = BitConverter.ToUInt32( data, (int)descriptorAt + 0x0C );

		float Float( int at ) => BitConverter.ToSingle( data, (int)descriptorAt + at );
		var centre = new Vector3( Float( 0x14 ), Float( 0x18 ), Float( 0x1C ) );
		var step = new Vector3( Float( 0x20 ), Float( 0x24 ), Float( 0x28 ) );

		if ( !float.IsFinite( centre.X + centre.Y + centre.Z + step.X + step.Y + step.Z ) )
			return;

		if ( recordCount <= 0 || recordCount > 4096 )
			return;

		if ( tableOffset < 0x9C || tableOffset + (20L * recordCount) > data.Length )
			return;

		var records = new Track[recordCount];

		for ( int i = 0; i < recordCount; ++i )
		{
			var offset = (int)tableOffset + (20 * i);

			int keyframes = BitConverter.ToUInt16( data, offset );
			int channels = BitConverter.ToUInt16( data, offset + 2 );
			uint channelIdsAt = BitConverter.ToUInt32( data, offset + 4 );
			uint frameIndicesAt = BitConverter.ToUInt32( data, offset + 8 );
			uint valuesAt = BitConverter.ToUInt32( data, offset + 12 );

			if ( keyframes <= 0 || channels <= 0 )
				return;

			// Both id and frame arrays are padded up to a 4-byte boundary.
			var idBytes = frameIndicesAt - channelIdsAt;
			var frameBytes = valuesAt - frameIndicesAt;

			if ( idBytes != 2 * channels && idBytes != (2 * channels) + 2 )
				return;

			if ( frameBytes != 2 * keyframes && frameBytes != (2 * keyframes) + 2 )
				return;

			if ( channelIdsAt < 0x9C || valuesAt + (4L * keyframes * channels) > data.Length )
				return;

			var ids = new ushort[channels];
			for ( int c = 0; c < channels; ++c )
				ids[c] = BitConverter.ToUInt16( data, (int)channelIdsAt + (2 * c) );

			var frames = new ushort[keyframes];
			for ( int f = 0; f < keyframes; ++f )
				frames[f] = BitConverter.ToUInt16( data, (int)frameIndicesAt + (2 * f) );

			var values = new uint[keyframes * channels];
			for ( int v = 0; v < values.Length; ++v )
				values[v] = BitConverter.ToUInt32( data, (int)valuesAt + (4 * v) );

			records[i] = new Track
			{
				ChannelIds = ids,
				FrameIndices = frames,
				RawValues = values
			};
		}

		var track = new MorphTrack { TargetIndex = target, Records = records, Centre = centre, Step = step };
		track.BuildLookup();

		MorphTracks.Add( track );
	}

	private void ReadUvChannel( byte[] data, uint descriptorAt, int target )
	{
		if ( descriptorAt < 0x9C || descriptorAt + 0x14 > data.Length )
			return;

		long entryCount = BitConverter.ToUInt32( data, (int)descriptorAt + 0x00 );
		var indicesAt = BitConverter.ToUInt32( data, (int)descriptorAt + 0x04 );
		long keyTotal = BitConverter.ToUInt32( data, (int)descriptorAt + 0x08 );
		var framesAt = BitConverter.ToUInt32( data, (int)descriptorAt + 0x0C );
		var valuesAt = BitConverter.ToUInt32( data, (int)descriptorAt + 0x10 );

		if ( entryCount <= 0 || entryCount > 65535 || keyTotal <= 0 )
			return;

		// The three tables are contiguous and exactly sized by the two counts: an index pair per entry,
		// then a coordinate pair per key, then a frame per key. Anything else is not a UV descriptor.
		if ( indicesAt + (4 * entryCount) != valuesAt )
			return;

		if ( valuesAt + (8 * keyTotal) != framesAt )
			return;

		if ( indicesAt < 0x9C || framesAt + (2 * keyTotal) > data.Length )
			return;

		var count = (int)entryCount;
		var firstKey = new int[count];
		var keyCount = new int[count];

		var running = 0;
		for ( int i = 0; i < count; ++i )
		{
			firstKey[i] = BitConverter.ToUInt16( data, (int)indicesAt + (4 * i) );
			keyCount[i] = BitConverter.ToUInt16( data, (int)indicesAt + (4 * i) + 2 );

			// Runs are packed end to end in the order the entries name them, which is what lets the
			// total stand in for a bound on every one of them.
			if ( firstKey[i] != running )
				return;

			running += keyCount[i];

			if ( running > keyTotal )
				return;
		}

		if ( running != keyTotal )
			return;

		var total = (int)keyTotal;
		var frames = new ushort[total];
		var coordinates = new System.Numerics.Vector2[total];

		for ( int key = 0; key < total; ++key )
		{
			frames[key] = BitConverter.ToUInt16( data, (int)framesAt + (2 * key) );

			coordinates[key] = new System.Numerics.Vector2(
				BitConverter.ToSingle( data, (int)valuesAt + (8 * key) ),
				BitConverter.ToSingle( data, (int)valuesAt + (8 * key) + 4 ) );
		}

		UvTracks.Add( new UvTrack
		{
			TargetIndex = target,
			FirstKey = firstKey,
			KeyCount = keyCount,
			Frames = frames,
			Coordinates = coordinates
		} );
	}
}
