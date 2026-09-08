using System.Text;

namespace OpenTPW;

/// <summary>
/// The animation half of the .md2 format - the files that <see cref="ModelFile.IsAnimation"/>
/// flags (mesh table pointer at 0x70 is zero). See the notes on <see cref="ModelFile"/> for how
/// the two kinds relate and for the evidence behind the layout below.
///
/// An animation is a list of TRACKS, each posing one node of the base model over a range of
/// authoring frames. A track can carry several kinds of channel at once - a fountain's water
/// meshes carry vertex morph, UV scroll and a timing scalar together - so the channels are read
/// independently and the ones we don't understand are simply left alone.
///
/// LOCATING THE TRACKS
///
/// The uint at 0x98 points at a 72-byte animation block; that pointer is valid in 1278 of the
/// game's 1279 animation files. In the block:
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
///     bit 0x00008  rotation    count (ushort) at +0x10, keyframes at +0x1C
///     bit 0x01000  vertex morph                         descriptor at +0x28
///     bit 0x10000  UV animation                         descriptor at +0x2C
///     bit 0x20000  timing scalar                        16.16 fixed value at +0x30
///     bit 0x00001  unidentified                         data at +0x18
///
/// That correspondence is exact: across all 1279 files no track sets one of those bits without
/// filling its slot, or fills a slot without setting the bit.
///
/// The target at +0x14 is a USHORT, not a uint - +0x16 holds a separate value and is nonzero on
/// 595 of the game's rotation tracks, so reading 32 bits there yields a garbage node index. It
/// indexes the base model's node list, which is its mesh list for models with no extra
/// hierarchy: Jun_gateM1's two tracks take the gate's door01/door02 meshes from identity to a
/// quarter turn about model Y (up), and Jun_gateM2 is exactly the inverse - a gate swinging
/// open and shut. Models with extra nodes index past their meshes (Advisor has 25 meshes and
/// reaches index 28), so callers must range-check the target and skip what doesn't land.
///
/// ROTATION (bit 0x8)
///
/// A keyframe is 20 bytes: ushort frame, ushort flags (0 or 0xFFFF), then a float quaternion
/// x, y, z, w. All 3039 rotation tracks in the game decode to strictly ascending frame indices
/// and unit quaternions (within 1e-2), which is the check applied here.
///
/// VERTEX MORPH (bit 0x1000)
///
/// The slot at +0x28 points at a 16-byte descriptor: ushort record count at +0x02, uint record
/// table offset at +0x0C. Each track has its OWN descriptor and its own channel space, so one
/// animation morphs as many meshes as it has morph tracks - ratraceM1 morphs four (and three of
/// those meshes share a vertex count, so identifying targets by vertex count cannot tell them
/// apart; the target index can). 768 animation files carry morph tracks, 368 of them more than
/// one, 1766 tracks in total.
///
/// A morph track has exactly one channel per vertex of the mesh it targets, plus two trailing
/// channels that aren't vertices - 457 of the 465 tracks whose model resolves satisfy that
/// exactly. The 8 that don't (droidm2 names a 16-vertex mesh but carries 3561 channels) are
/// models whose node list evidently isn't their mesh list, the same caveat the target index
/// carries generally, so callers should check the count and skip a track that fails it rather
/// than morph the wrong mesh into nonsense.
///
/// Each record is: ushort a (keyframes), ushort b (channels), uint p1, uint p2, uint p3, uint
/// reserved. p1 is an array of b channel ids, p2 an array of a ascending frame indices, p3 a
/// block of a*b 4-byte values. Both arrays are padded to a 4-byte boundary, so p2 - p1 is 2*b
/// or 2*b+2, and p3 - p2 is 2*a or 2*a+2. The value block is entry-major: keyframe e of channel
/// slot k lives at p3 + (e * b + k) * 4. Channels the animation doesn't move are still present,
/// in a record holding a single rest keyframe, so sampling every channel reproduces the mesh.
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
/// Of the game's 1279 animation files, 1164 (91%) carry at least one channel read here: 768
/// morph, 686 rotation, 324 UV. 25 have a readable track table but only channels we don't
/// decode, and 90 fail the table identity above and are rejected.
/// </summary>
public class AnimationFile : BaseFormat
{
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

			return System.Numerics.Quaternion.Slerp( Rotations[lo], Rotations[hi], t );
		}
	}

	/// <summary>
	/// One mesh's UV animation: a set of entries, each sliding some run of UV components from a
	/// start value to an end value by its own end frame. See the notes on this class.
	/// </summary>
	public class UvTrack
	{
		public int TargetIndex { get; init; }

		/// <summary>First UV component each entry drives - two components per coordinate.</summary>
		public ushort[] FirstComponent { get; init; } = Array.Empty<ushort>();
		public ushort[] ComponentCount { get; init; } = Array.Empty<ushort>();

		/// <summary>Index into <see cref="Values"/> where each entry's block begins.</summary>
		public int[] ValueOffset { get; init; } = Array.Empty<int>();

		/// <summary>Per entry: its ComponentCount start floats, then that many end floats.</summary>
		public float[] Values { get; init; } = Array.Empty<float>();

		public ushort[] EndFrame { get; init; } = Array.Empty<ushort>();

		public int EntryCount => FirstComponent.Length;
	}

	public List<MorphTrack> MorphTracks { get; } = new();

	public List<UvTrack> UvTracks { get; } = new();

	public List<RotationTrack> RotationTracks { get; } = new();

	/// <summary>False when this file holds no track we can read - callers must check it.</summary>
	public bool IsValid { get; private set; }

	public int FirstFrame { get; private set; }
	public int LastFrame { get; private set; }

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

	/// <summary>
	/// A keyframe value is a vertex position quantised into three signed 10-bit fields -
	/// X in bits 0..9, Y in 10..19, Z in 20..29, with bits 30 and 31 unused. Each field spans
	/// the owning mesh's bounding box, so -512 maps to the box minimum and +511 to its maximum.
	///
	/// Verified by decoding the rest keyframe of every channel of Jun_isleM1 and comparing
	/// against the 169 vertices of the Dino mesh it animates: R^2 = 0.999997 or better per
	/// axis, max error 0.028 units, which is just the 10-bit quantisation step.
	/// </summary>
	public static Vector3 DecodePosition( uint raw, Vector3 boundsMin, Vector3 boundsMax )
	{
		return new Vector3(
			Component( raw, 0, boundsMin.X, boundsMax.X ),
			Component( raw, 10, boundsMin.Y, boundsMax.Y ),
			Component( raw, 20, boundsMin.Z, boundsMax.Z ) );
	}

	private static float Component( uint raw, int shift, float min, float max )
	{
		var field = (int)((raw >> shift) & 0x3FF);
		if ( (field & 0x200) != 0 )
			field -= 1024;

		var centre = (min + max) * 0.5f;
		return centre + (field * (max - min) / 1023f);
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

			if ( (flags & 0x1000) != 0 )
				ReadMorphChannel( data, BitConverter.ToUInt32( data, offset + 0x28 ), target );

			if ( (flags & 0x10000) != 0 )
				ReadUvChannel( data, BitConverter.ToUInt32( data, offset + 0x2C ), target );
		}

		return true;
	}

	private void ReadRotationChannel( byte[] data, int descriptor, int target )
	{
		int keyCount = BitConverter.ToUInt16( data, descriptor + 0x10 );
		var keysAt = BitConverter.ToUInt32( data, descriptor + 0x1C );

		if ( keyCount <= 0 || keysAt < 0x9C || keysAt + (20L * keyCount) > data.Length )
			return;

		var frames = new ushort[keyCount];
		var rotations = new System.Numerics.Quaternion[keyCount];

		for ( int k = 0; k < keyCount; ++k )
		{
			var entry = (int)keysAt + (20 * k);

			frames[k] = BitConverter.ToUInt16( data, entry );
			if ( k > 0 && frames[k] <= frames[k - 1] )
				return;

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
			Rotations = rotations
		} );
	}

	private void ReadMorphChannel( byte[] data, uint descriptorAt, int target )
	{
		if ( descriptorAt < 0x9C || descriptorAt + 0x10 > data.Length )
			return;

		int recordCount = BitConverter.ToUInt16( data, (int)descriptorAt + 0x02 );
		var tableOffset = BitConverter.ToUInt32( data, (int)descriptorAt + 0x0C );

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

		var track = new MorphTrack { TargetIndex = target, Records = records };
		track.BuildLookup();

		MorphTracks.Add( track );
	}

	private void ReadUvChannel( byte[] data, uint descriptorAt, int target )
	{
		if ( descriptorAt < 0x9C || descriptorAt + 0x14 > data.Length )
			return;

		long entryCount = BitConverter.ToUInt32( data, (int)descriptorAt + 0x00 );
		var indicesAt = BitConverter.ToUInt32( data, (int)descriptorAt + 0x04 );
		long componentTotal = BitConverter.ToUInt32( data, (int)descriptorAt + 0x08 );
		var durationsAt = BitConverter.ToUInt32( data, (int)descriptorAt + 0x0C );
		var valuesAt = BitConverter.ToUInt32( data, (int)descriptorAt + 0x10 );

		if ( entryCount <= 0 || entryCount > 65535 || componentTotal <= 0 )
			return;

		// The three tables are contiguous and exactly sized by the two counts. Anything else
		// means this isn't a UV descriptor.
		if ( indicesAt + (4 * entryCount) != valuesAt )
			return;

		if ( valuesAt + (8 * componentTotal) != durationsAt )
			return;

		if ( indicesAt < 0x9C || durationsAt + (4 * entryCount) > data.Length )
			return;

		var count = (int)entryCount;
		var first = new ushort[count];
		var components = new ushort[count];
		var valueOffset = new int[count];
		var endFrame = new ushort[count];

		var running = 0;
		for ( int i = 0; i < count; ++i )
		{
			first[i] = BitConverter.ToUInt16( data, (int)indicesAt + (4 * i) );
			components[i] = BitConverter.ToUInt16( data, (int)indicesAt + (4 * i) + 2 );

			// Each entry stores its start values then its end values, so its block is twice
			// its component count.
			valueOffset[i] = running * 2;
			running += components[i];

			endFrame[i] = BitConverter.ToUInt16( data, (int)durationsAt + (4 * i) + 2 );
		}

		if ( running != componentTotal )
			return;

		var values = new float[componentTotal * 2];
		for ( int v = 0; v < values.Length; ++v )
			values[v] = BitConverter.ToSingle( data, (int)valuesAt + (4 * v) );

		UvTracks.Add( new UvTrack
		{
			TargetIndex = target,
			FirstComponent = first,
			ComponentCount = components,
			ValueOffset = valueOffset,
			Values = values,
			EndFrame = endFrame
		} );
	}
}
