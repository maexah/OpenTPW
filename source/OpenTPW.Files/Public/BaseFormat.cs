namespace OpenTPW;

public class BaseFormat
{
	/// <summary>
	/// Read the format from a file within the TPW's internal file system
	/// </summary>
	protected virtual void ReadFromFile( string path )
	{
		using var fileStream = FileSystem.OpenRead( path );
		ReadFromStream( fileStream );
	}

	/// <summary>
	/// Read the format from a stream.
	/// </summary>
	protected virtual void ReadFromStream( Stream stream )
	{
	}

	/// <summary>
	/// The stream's bytes, as many as its length says, read from where it stands; the stream is closed after. A
	/// reader that keeps the whole file takes them in its constructor, so its buffer is never unset.
	/// </summary>
	internal static byte[] ReadAll( Stream stream )
	{
		var length = (int)stream.Length;
		var bytes = new byte[length];
		stream.ReadAtLeast( bytes, length, throwOnEndOfStream: false );
		stream.Close();
		return bytes;
	}

	/// <summary>A file's bytes from the TPW's internal file system (<see cref="ReadAll(Stream)"/>).</summary>
	internal static byte[] ReadAll( string path )
	{
		using var fileStream = FileSystem.OpenRead( path );
		return ReadAll( fileStream );
	}
}
