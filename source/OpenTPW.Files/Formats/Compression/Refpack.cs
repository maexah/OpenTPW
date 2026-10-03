namespace OpenTPW;

internal sealed partial class Refpack
{
	private byte[] Data { get; set; }

	public Refpack( byte[] data )
	{
		Data = data;
	}

	public List<byte> Decompress( uint expectedSize )
	{
		if ( Data.Length < 5 || Data[0] != 0x10 || Data[1] != 0xFB )
			throw new InvalidDataException( "Unsupported or truncated Refpack header." );
		var size = (Data[2] << 16) | (Data[3] << 8) | Data[4];
		if ( size != expectedSize )
			throw new InvalidDataException( "WAD and Refpack decompressed sizes disagree." );
		var output = new List<byte>();
		IRefpackCommand[] commands =
		{
			new FourByteCommand(), new ThreeByteCommand(), new TwoByteCommand(), new OneByteCommand(), new StopCommand()
		};
		var position = 5;
		while ( position < Data.Length )
		{
			var command = commands.First( candidate => candidate.OpcodeMatches( Data[position] ) );
			if ( command.Length > Data.Length - position )
				throw new InvalidDataException( "Truncated Refpack command." );
			command.Decompress( Data, ref output, position, out var literals );
			position += command.Length + (int)literals;
			if ( output.Count > size )
				throw new InvalidDataException( "Refpack output exceeds its declared size." );
			if ( command.StopAfterFound )
			{
				if ( output.Count != size || position != Data.Length )
					throw new InvalidDataException( "Refpack ended with a size mismatch or trailing bytes." );
				return output;
			}
		}
		throw new InvalidDataException( "Refpack stream has no stop command." );
	}
}
