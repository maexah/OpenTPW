namespace OpenTPW;

// Dead by CODE: nothing implements it. An archive reads its bytes in its constructor, never through this.
public interface IFileFormat
{
	/// <summary>
	/// Reads the format's data from a given stream
	/// </summary>
	/// <param name="stream"></param>
	void ReadFromStream( Stream stream );
}
