namespace OpenTPW;

public partial class TextureFile
{
	struct ImageDecodeState
	{
		public List<float> DequantizationBuffer;
		public List<float> RowDecodeBuffer;

		/// <summary>
		/// Sized from the indices <c>DecodeChannel</c> reaches, not from the cube of the side. Step 1
		/// writes the dequantization buffer over [0, size*size). Step 2 writes the row buffer up to index
		/// size*size inclusive - in half-scale mode the last row's wavelet lands one past the row - and
		/// step 3's alpha path reads it up to 1.5 * size*size. A 256-pixel texture needs about 650 KB
		/// here.
		/// </summary>
		public ImageDecodeState( int size )
		{
			DequantizationBuffer = Enumerable.Repeat( 0f, size * size ).ToList();
			RowDecodeBuffer = Enumerable.Repeat( 0f, size * size + size * size / 2 ).ToList();
		}
	}
}
