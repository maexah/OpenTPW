using System.Text;

namespace OpenTPW;

/// <summary>
/// The animation half of the .md2 format - the files that <see cref="ModelFile.IsAnimation"/>
/// flags (mesh table pointer at 0x70 is zero). See the notes on <see cref="ModelFile"/> for how
/// the two kinds relate and for the evidence behind the layout below.
///
/// This is vertex animation. An animation has exactly one channel per vertex of the single mesh
/// it drives, plus two trailing channels that aren't vertices - so ChannelCount is that mesh's
/// vertex count + 2, which is how the target mesh is identified. Verified on Bat.MD2 (14 verts
/// -> 16 channels), Bfly_YELL/PINK (15 -> 17) and Jun_isle, whose 171 channels pick out the
/// 169-vertex "Dino" mesh rather than any of its other 14 meshes.
///
/// Channels are sampled sparsely: each track record covers some subset of channels and stores a
/// keyframe per frame index it cares about. Channels the file doesn't animate are still present,
/// in a record holding a single keyframe of rest values, so sampling every channel reproduces
/// the whole mesh. That the moving channels really are vertices was checked independently of the
/// value decode: the 15 channels Jun_isleM1 animates map to vertices with a mean spread of 3.67
/// about their centroid, where 2000 random 15-vertex samples of the same mesh never came below
/// 6.49 - they are one tight cluster, the head.
///
/// Layout, verified against the real game data:
///   - ushort at 0xBA is the track record count, uint at 0xC4 the offset of the record table.
///     Some animation files (the Fan/Hal/Spa lobby islands, for instance) are a different variant
///     that puts other data in those slots - they simply don't load, they must not throw.
///   - Each record is: ushort a (keyframes), ushort b (channels), uint p1, uint p2, uint p3,
///     uint reserved. p1 is an array of b channel ids, p2 an array of a ascending frame indices,
///     p3 a block of a*b 4-byte values. Both arrays are padded to a 4-byte boundary, so
///     p2 - p1 is 2*b or 2*b+2, and p3 - p2 is 2*a or 2*a+2.
///   - The value block is entry-major: the value for keyframe e of channel slot k lives at
///     p3 + (e * b + k) * 4.
///
/// Not every animation file is this kind. Measured over all 1279 animation files in the game:
///
///   - 282 (22%) are the vertex animation above.
///   - 686 (54%) are rotation animations, read by <see cref="RotationTracks"/> below.
///   - The remaining 311 (24%) carry only track kinds we don't read yet (see the flag bits
///     listed on <see cref="ReadTrackTable"/>), and load with no tracks at all.
///
/// ROTATION ANIMATION
///
/// Both kinds share a 72-byte animation block whose offset is the uint at 0x98 - valid in
/// 1278 of the 1279 animation files. In it:
///
///   - uint at +0x08 is the last frame of the animation.
///   - ushort at +0x12 is the track count, uint at +0x2C the offset of the track table. The
///     table is track count * 64 bytes and ends exactly where the block begins, which is what
///     makes it safe to trust: 1189 of 1279 files satisfy that identity, and the ones that
///     don't are rejected rather than read.
///
/// Each 64-byte track descriptor holds its own index at +0x00 and a flag word at +0x04 saying
/// which channels it carries; a channel's data pointer lives in the slot that channel owns, so
/// unknown channels are simply left alone. Bit 0x8 is the rotation channel: ushort at +0x10 is
/// the keyframe count and uint at +0x1C the keyframe offset. That correspondence is exact -
/// across all 1279 files no track sets bit 0x8 without those slots, or fills them without it.
///
/// A rotation keyframe is 20 bytes: ushort frame, ushort flags (0 or 0xFFFF), then a float
/// quaternion x, y, z, w. All 3039 rotation tracks in the game decode to strictly ascending
/// frame indices and unit quaternions (within 1e-2), which is the check this class applies.
///
/// uint at +0x14 is the track's target. For models whose meshes are the whole node list it is
/// the mesh index - Jun_gateM1's two tracks take the gate's door01 and door02 meshes from
/// identity to a quarter turn about model Y (which is up), and Jun_gateM2 is exactly the
/// inverse, so the pair is a gate swinging open and shut. Models with extra hierarchy nodes
/// index past their meshes (Advisor has 25 meshes and reaches index 28), so callers must
/// range-check the target and skip tracks that don't land on a mesh.
/// </summary>
public class AnimationFile : BaseFormat
{
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
	/// One node's rotation over time. <see cref="TargetIndex"/> is an index into the base
	/// model's node list, which is its mesh list for models with no extra hierarchy - see the
	/// notes on this class.
	/// </summary>
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

	public List<Track> Tracks { get; } = new();

	public List<RotationTrack> RotationTracks { get; } = new();

	/// <summary>False when this file holds no track we can read - callers must check it.</summary>
	public bool IsValid { get; private set; }

	public int ChannelCount { get; private set; }
	public int FirstFrame { get; private set; }
	public int LastFrame { get; private set; }

	private (int Track, int Slot)[] _channelLookup = Array.Empty<(int, int)>();

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

	public bool TryGetChannel( int channelId, out Track? track, out int slot )
	{
		track = null;
		slot = -1;

		if ( channelId < 0 || channelId >= _channelLookup.Length )
			return false;

		var (trackIndex, channelSlot) = _channelLookup[channelId];
		if ( trackIndex < 0 )
			return false;

		track = Tracks[trackIndex];
		slot = channelSlot;
		return true;
	}

	protected override void ReadFromStream( Stream stream )
	{
		var length = (int)stream.Length;
		var data = new byte[length];
		stream.ReadExactly( data, 0, length );

		if ( length < 0xC8 )
			return;

		var recordCount = BitConverter.ToUInt16( data, 0xBA );
		var tableOffset = BitConverter.ToUInt32( data, 0xC4 );

		// A file can hold either kind of track, so a failure of one is not a failure of the
		// file - only a file with nothing readable in it at all is invalid.
		if ( !TryReadTable( data, tableOffset, recordCount ) )
			Tracks.Clear();

		if ( !ReadTrackTable( data ) )
			RotationTracks.Clear();

		if ( Tracks.Count == 0 && RotationTracks.Count == 0 )
			return;

		var maxChannel = 0;
		var minFrame = int.MaxValue;
		var maxFrame = int.MinValue;

		foreach ( var track in Tracks )
		{
			foreach ( var id in track.ChannelIds )
				maxChannel = Math.Max( maxChannel, id );

			foreach ( var frame in track.FrameIndices )
			{
				minFrame = Math.Min( minFrame, frame );
				maxFrame = Math.Max( maxFrame, frame );
			}
		}

		foreach ( var track in RotationTracks )
		{
			foreach ( var frame in track.FrameIndices )
			{
				minFrame = Math.Min( minFrame, frame );
				maxFrame = Math.Max( maxFrame, frame );
			}
		}

		ChannelCount = Tracks.Count == 0 ? 0 : maxChannel + 1;
		FirstFrame = minFrame == int.MaxValue ? 0 : minFrame;
		LastFrame = maxFrame == int.MinValue ? 0 : maxFrame;

		_channelLookup = new (int, int)[ChannelCount];
		Array.Fill( _channelLookup, (-1, -1) );

		for ( int t = 0; t < Tracks.Count; ++t )
		{
			var ids = Tracks[t].ChannelIds;
			for ( int s = 0; s < ids.Length; ++s )
				_channelLookup[ids[s]] = (t, s);
		}

		IsValid = true;
	}

	/// <summary>
	/// Reads the rotation channel of every track in the animation block's track table, and
	/// leaves the other channels alone. The flag bits at descriptor +0x04, and the slot each
	/// one owns, counted over all 1279 animation files:
	///
	///     0x00008  ushort count at +0x10, data at +0x1C  rotation - what we read
	///     0x00001                         data at +0x18  3039 tracks
	///     0x01000                         data at +0x28  1058 tracks
	///     0x10000                         data at +0x2C   704 tracks
	///     0x20000                         data at +0x30  2178 tracks
	///
	/// Returns false without having read anything usable if the table can't be located or any
	/// track fails to validate - a mislocated table would produce confidently wrong rotations.
	/// </summary>
	private bool ReadTrackTable( byte[] data )
	{
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
			if ( (flags & 0x8) == 0 )
				continue;

			int keyCount = BitConverter.ToUInt16( data, offset + 0x10 );
			var target = BitConverter.ToUInt32( data, offset + 0x14 );
			var keysAt = BitConverter.ToUInt32( data, offset + 0x1C );

			if ( keyCount <= 0 || keysAt < 0x9C || keysAt + (20L * keyCount) > data.Length )
				return false;

			var frames = new ushort[keyCount];
			var rotations = new System.Numerics.Quaternion[keyCount];

			for ( int k = 0; k < keyCount; ++k )
			{
				var entry = (int)keysAt + (20 * k);

				frames[k] = BitConverter.ToUInt16( data, entry );
				if ( k > 0 && frames[k] <= frames[k - 1] )
					return false;

				rotations[k] = new System.Numerics.Quaternion(
					BitConverter.ToSingle( data, entry + 4 ),
					BitConverter.ToSingle( data, entry + 8 ),
					BitConverter.ToSingle( data, entry + 12 ),
					BitConverter.ToSingle( data, entry + 16 ) );

				if ( Math.Abs( rotations[k].Length() - 1f ) > 0.01f )
					return false;
			}

			RotationTracks.Add( new RotationTrack
			{
				TargetIndex = (int)target,
				FrameIndices = frames,
				Rotations = rotations
			} );
		}

		return true;
	}

	private bool TryReadTable( byte[] data, uint tableOffset, int recordCount )
	{
		if ( recordCount <= 0 || recordCount > 4096 )
			return false;

		if ( tableOffset < 0xB8 || tableOffset + (20L * recordCount) > data.Length )
			return false;

		for ( int i = 0; i < recordCount; ++i )
		{
			var offset = (int)tableOffset + (20 * i);

			int keyframes = BitConverter.ToUInt16( data, offset );
			int channels = BitConverter.ToUInt16( data, offset + 2 );
			uint channelIdsAt = BitConverter.ToUInt32( data, offset + 4 );
			uint frameIndicesAt = BitConverter.ToUInt32( data, offset + 8 );
			uint valuesAt = BitConverter.ToUInt32( data, offset + 12 );

			if ( keyframes <= 0 || channels <= 0 )
				return false;

			// Both id and frame arrays are padded up to a 4-byte boundary.
			var idBytes = frameIndicesAt - channelIdsAt;
			var frameBytes = valuesAt - frameIndicesAt;

			if ( idBytes != 2 * channels && idBytes != (2 * channels) + 2 )
				return false;

			if ( frameBytes != 2 * keyframes && frameBytes != (2 * keyframes) + 2 )
				return false;

			if ( channelIdsAt < 0xB8 || valuesAt + (4L * keyframes * channels) > data.Length )
				return false;

			var ids = new ushort[channels];
			for ( int c = 0; c < channels; ++c )
				ids[c] = BitConverter.ToUInt16( data, (int)channelIdsAt + (2 * c) );

			var frames = new ushort[keyframes];
			for ( int f = 0; f < keyframes; ++f )
				frames[f] = BitConverter.ToUInt16( data, (int)frameIndicesAt + (2 * f) );

			var values = new uint[keyframes * channels];
			for ( int v = 0; v < values.Length; ++v )
				values[v] = BitConverter.ToUInt32( data, (int)valuesAt + (4 * v) );

			Tracks.Add( new Track
			{
				ChannelIds = ids,
				FrameIndices = frames,
				RawValues = values
			} );
		}

		return true;
	}
}
