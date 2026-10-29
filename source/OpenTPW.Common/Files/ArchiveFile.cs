namespace OpenTPW;

/// <summary>
/// A file containing data within an archive
/// </summary>
public abstract class ArchiveFile : ArchiveItem
{
	public abstract byte[] GetData();
}
