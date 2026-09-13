using ImGuiNET;
using OpenTPW.ModKit;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.Reflection;
using NeoVeldrid;

namespace OpenTPW;
public partial class Editor
{
	public static Editor Instance { get; private set; }

	public ImGuiRenderer imguiRenderer { get; private set; }
	public GraphicsDevice graphicsDevice { get; private set; }

	private List<BaseTab> tabs = new();

	public bool shouldRender = false;

	public static ImFontPtr MonospaceFont { get; private set; }
	public static ImFontPtr SansSerifFont { get; private set; }

	public Editor( ImGuiRenderer imguiRenderer, GraphicsDevice graphicsDevice )
	{
		Instance ??= this;

		this.imguiRenderer = imguiRenderer;
		this.graphicsDevice = graphicsDevice;

		InitIO();

		tabs.AddRange( new BaseTab[] {
			new DemoWindow(),
			new FileBrowser()
		} );

		tabs.ForEach( x => x.ImGuiRenderer = this.imguiRenderer );
	}

	private void InitIO()
	{
		var io = ImGui.GetIO();

		io.ConfigFlags |= ImGuiConfigFlags.DockingEnable | ImGuiConfigFlags.IsSRGB;
		ImGui.LoadIniSettingsFromDisk( ImGui.GetIO().IniFilename ); // https://github.com/mellinoe/veldrid/issues/410

		// io.Fonts.Clear();
		// io.Fonts.AddFontFromFileTTF( "C:\\Windows\\Fonts\\segoeui.ttf", 16f );
		// io.Fonts.Build();
		// imguiRenderer.RecreateFontDeviceTexture( graphicsDevice );
	}

	private void DrawMenuBar()
	{
		ImGui.BeginMainMenuBar();

		foreach ( var tab in tabs )
		{
			var editorMenuAttribute = tab.GetType().GetCustomAttribute<EditorMenuAttribute>();
			if ( editorMenuAttribute == null )
				continue;

			var splitPath = editorMenuAttribute.Path.Split( '/' );

			if ( ImGui.BeginMenu( splitPath[0] ) )
			{
				for ( int i = 1; i < splitPath.Length; i++ )
				{
					string? item = splitPath[i];
					bool active = ImGui.MenuItem( item );

					if ( i == splitPath.Length - 1 && active )
						tab.visible = !tab.visible;
				}

				ImGui.EndMenu();
			}
		}

		ImGui.EndMainMenuBar();
	}

	public void UpdateFrom( InputSnapshot inputSnapshot )
	{
		imguiRenderer.Update( 1 / 60f, inputSnapshot );

		DrawMenuBar();

		// The leading id is new in ImGui 1.90, which took a dockspace id in front of the viewport it had
		// taken before. Zero asks ImGui for the id it would have derived from that viewport itself, which
		// is the docking this had when the id was not a parameter at all.
		ImGui.DockSpaceOverViewport( 0, ImGui.GetMainViewport(), ImGuiDockNodeFlags.PassthruCentralNode );

		tabs.ForEach( tab =>
		{
			if ( tab.visible )
				tab.Draw();
		} );
	}

	public async Task<string> CacheThumbnailAsync( string path )
	{
		if ( !path.EndsWith( ".wct" ) )
			return "";

		var thumbnailPath = FileSystem.GetRelativePath( path );
		thumbnailPath = Path.ChangeExtension( thumbnailPath, ".png" );
		thumbnailPath = "/thumbcache" + thumbnailPath;

		await Task.Run( () =>
		{
			var textureFile = new TextureFile( path );
			var t = textureFile.Data;

			var pixelData = new Rgba32[t.Width * t.Height];

			for ( int i = 0; i < t.Width * t.Height; ++i )
			{
				var tInd = i * 4;
				pixelData[i] = new Rgba32( t.Data[tInd], t.Data[tInd + 1], t.Data[tInd + 2], t.Data[tInd + 3] );
			}

			using var img = Image.LoadPixelData<Rgba32>( pixelData, t.Width, t.Height );
			using var writeStream = CacheFileSystem.OpenWrite( thumbnailPath );
			img.Mutate( x => x.Resize( 16, 16 ) );
			img.SaveAsPng( writeStream );

			Log.Info( $"Cached to {thumbnailPath}" );
		} );

		return thumbnailPath;
	}

	public void Render( CommandList commandList ) 
	{
		imguiRenderer.Render( graphicsDevice, commandList );
	}
}

