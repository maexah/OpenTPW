
namespace OpenTPW;

// Dead by CODE: nothing implements or names it.
public interface IBaseFileSystem
{
	string GetAbsolutePath( string relativePath );
	string[] GetDirectories( string relativePath );
	string[] GetFiles( string relativePath );
	Stream OpenRead( string relativePath );
	byte[] ReadAllBytes( string relativePath );
	string ReadAllText( string relativePath );
	void RegisterArchiveHandler( string extension, Func<string, IArchive> open );
}