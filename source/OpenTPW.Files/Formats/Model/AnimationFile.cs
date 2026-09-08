using System.Text;

namespace OpenTPW;

/// <summary>
/// The animation half of the .md2 format - the files that <see cref="ModelFile.IsAnimation"/>
/// flags (mesh table pointer at 0x70 is zero). See the notes on <see cref="ModelFile"/> for how
/// the two kinds relate and for the evidence behind the layout below.
///
/// An animation is a flat list of numbered channels, sampled sparsely: each track record covers
/// some subset of channels and stores a keyframe per frame index it cares about. Channels the
/// file doesn't animate are still present, in a record holding a single keyframe of rest values.
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
/// What a channel drives, and how its 32-bit value decodes, are both still unknown - see
/// MeshAnimator for where those hypotheses are kept.
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

	public List<Track> Tracks { get; } = new();

	/// <summary>False when this file is a variant we can't read - callers must check it.</summary>
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

		if ( !TryReadTable( data, tableOffset, recordCount ) )
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

		ChannelCount = maxChannel + 1;
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
