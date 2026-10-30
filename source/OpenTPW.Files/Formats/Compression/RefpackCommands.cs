namespace OpenTPW;

partial class Refpack
{
	internal interface IRefpackCommand
	{
		bool StopAfterFound => false;
		int Length { get; }

		void Decompress( byte[] data, byte[] output, ref int written, int offset, out uint skipAhead );
		bool OpcodeMatches( byte firstByte );
	}

	public static void DecompressData( byte[] data, byte[] output, ref int written, int offset, int opcodeLength, uint proceedingDataLength, uint referencedDataLength, uint referencedDataOffset )
	{
		if ( (long)offset + opcodeLength + proceedingDataLength > data.Length )
			throw new InvalidDataException( "Truncated Refpack literals." );
		var afterLiterals = written + (long)proceedingDataLength;

		if ( referencedDataLength > 0 && (referencedDataOffset == 0 || referencedDataOffset > afterLiterals) )
			throw new InvalidDataException( "Refpack backreference precedes the output." );
		if ( afterLiterals + referencedDataLength > output.Length )
			throw new InvalidDataException( "Refpack output exceeds its declared size." );

		Array.Copy( data, offset + opcodeLength, output, written, proceedingDataLength );
		written += (int)proceedingDataLength;

		// Overlap is valid: each copied byte can become the source of the next byte.
		for ( var i = 0; i < referencedDataLength; ++i, ++written )
			output[written] = output[written - (int)referencedDataOffset];
	}

	internal class FourByteCommand : IRefpackCommand
	{
		public int Length => 4;

		public void Decompress( byte[] data, byte[] output, ref int written, int offset, out uint skipAhead )
		{
			var proceedingDataLength = (uint)((data[offset] & 0x03));
			var referencedDataLength = (uint)(((data[offset] & 0x0C) << 6) + data[offset + 3] + 5);
			var referencedDataDistance = (uint)(((data[offset] & 0x10) << 12) + (data[offset + 1] << 8) + data[offset + 2] + 1);
			skipAhead = proceedingDataLength;

			DecompressData( data, output, ref written, offset, Length, proceedingDataLength, referencedDataLength, referencedDataDistance );
		}

		public bool OpcodeMatches( byte firstByte ) => (firstByte & 0xE0) == 0xC0;
	}

	internal class ThreeByteCommand : IRefpackCommand
	{
		public int Length => 3;

		public void Decompress( byte[] data, byte[] output, ref int written, int offset, out uint skipAhead )
		{
			var proceedingDataLength = (uint)((data[offset + 1] & 0xC0) >> 6);
			var referencedDataLength = (uint)((data[offset] & 0x3F) + 4);
			var referencedDataDistance = (uint)(((data[offset + 1] & 0x3F) << 8) + data[offset + 2] + 1);
			skipAhead = proceedingDataLength;

			DecompressData( data, output, ref written, offset, Length, proceedingDataLength, referencedDataLength, referencedDataDistance );
		}

		public bool OpcodeMatches( byte firstByte ) => (firstByte & 0xC0) == 0x80;
	}

	internal class TwoByteCommand : IRefpackCommand
	{
		public int Length => 2;

		public void Decompress( byte[] data, byte[] output, ref int written, int offset, out uint skipAhead )
		{
			var proceedingDataLength = (uint)((data[offset] & 0x03));
			var referencedDataLength = (uint)(((data[offset] & 0x1C) >> 2) + 3);
			var referencedDataDistance = (uint)(((data[offset] & 0x60) << 3) + data[offset + 1] + 1);
			skipAhead = proceedingDataLength;

			DecompressData( data, output, ref written, offset, Length, proceedingDataLength, referencedDataLength, referencedDataDistance );
		}
		public bool OpcodeMatches( byte firstByte ) => (firstByte & 0x80) == 0;
	}

	internal class OneByteCommand : IRefpackCommand
	{
		public int Length => 1;

		public void Decompress( byte[] data, byte[] output, ref int written, int offset, out uint skipAhead )
		{
			var dataAtOffset = data[offset];
			var proceedingDataLength = (uint)(((dataAtOffset & 0x1F) + 1) << 2);

			skipAhead = proceedingDataLength;
			DecompressData( data, output, ref written, offset, Length, proceedingDataLength, 0, 0 );
		}

		public bool OpcodeMatches( byte firstByte ) => ((firstByte & 0x1F) + 1) << 2 <= 0x70 && (firstByte & 0xE0) == 0xE0;
	}

	internal class StopCommand : IRefpackCommand
	{
		public int Length => 1;
		public bool StopAfterFound => true;

		public void Decompress( byte[] data, byte[] output, ref int written, int offset, out uint skipAhead )
		{
			var proceedingDataLength = (uint)((data[offset] & 0x03));
			skipAhead = proceedingDataLength;
			DecompressData( data, output, ref written, offset, Length, proceedingDataLength, 0, 0 );
		}

		public bool OpcodeMatches( byte firstByte ) => ((firstByte & 0x1F) + 1) << 2 > 0x70 && (firstByte & 0xE0) == 0xE0;
	}
}
