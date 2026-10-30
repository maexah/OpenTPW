namespace OpenTPW;

internal sealed partial class Refpack
{
	/// <summary>The five commands, tried in this order; each is stateless.</summary>
	private static readonly IRefpackCommand[] Commands =
	{
		new FourByteCommand(), new ThreeByteCommand(), new TwoByteCommand(), new OneByteCommand(), new StopCommand()
	};

	private byte[] Data { get; set; }

	public Refpack( byte[] data )
	{
		Data = data;
	}

	private static IRefpackCommand CommandFor( byte firstByte )
	{
		foreach ( var command in Commands )
		{
			if ( command.OpcodeMatches( firstByte ) )
				return command;
		}

		throw new InvalidDataException( "Unrecognised Refpack command." );
	}

	/// <summary>The decompressed bytes, written once into an array of the size the stream's header declares.</summary>
	public byte[] Decompress( uint expectedSize )
	{
		if ( Data.Length < 5 || Data[0] != 0x10 || Data[1] != 0xFB )
			throw new InvalidDataException( "Unsupported or truncated Refpack header." );
		var size = (Data[2] << 16) | (Data[3] << 8) | Data[4];
		if ( size != expectedSize )
			throw new InvalidDataException( "WAD and Refpack decompressed sizes disagree." );
		var output = new byte[size];
		var written = 0;
		var position = 5;
		while ( position < Data.Length )
		{
			var command = CommandFor( Data[position] );
			if ( command.Length > Data.Length - position )
				throw new InvalidDataException( "Truncated Refpack command." );
			command.Decompress( Data, output, ref written, position, out var literals );
			position += command.Length + (int)literals;
			if ( command.StopAfterFound )
			{
				if ( written != size || position != Data.Length )
					throw new InvalidDataException( "Refpack ended with a size mismatch or trailing bytes." );
				return output;
			}
		}
		throw new InvalidDataException( "Refpack stream has no stop command." );
	}
}
