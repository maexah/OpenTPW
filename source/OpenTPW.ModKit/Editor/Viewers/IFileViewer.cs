using NeoVeldrid;

namespace OpenTPW.ModKit;

internal interface IFileViewer
{
	TextureView GetIcon();
	void DrawPreview();
}
