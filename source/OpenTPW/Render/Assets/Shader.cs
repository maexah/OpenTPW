using Veldrid;
using Vortice.Direct3D11;
using Vortice.Win32;

namespace OpenTPW;

public partial class Shader : Asset
{
	private ShaderInfo shaderInfo;

	public VertexElementDescription[] VertexElements => shaderInfo.Reflection.VertexElements;
	public ResourceLayoutDescription[] ResourceLayouts => shaderInfo.Reflection.ResourceLayouts;
	public Veldrid.Shader[] ShaderProgram => shaderInfo.ShaderProgram;
	public bool IsDirty { get; private set; }
	public Action OnRecompile { get; set; }

	private FileSystemWatcher watcher;

	internal Shader( string path )
	{
		// Resolved here as well as in GetOrCreate, for the one caller that builds a Shader straight from the
		// constructor - the blit shader, in Renderer.CreateBlitPipeline. Without it the watcher below is
		// handed a directory relative to wherever the game was started from, which is where it threw.
		Path = ContentDir.GetPath( path );
		Register();

		Recompile();

		var directoryName = System.IO.Path.GetDirectoryName( Path );
		var fileName = System.IO.Path.GetFileName( Path );

		watcher = new FileSystemWatcher( directoryName, fileName );

		// Deliberately excludes NotifyFilters.LastAccess: Recompile() calls IsFileReady(),
		// which opens the file for read - watching LastAccess would make that read itself
		// re-dirty the shader, forcing a full recompile every single frame.
		watcher.NotifyFilter = NotifyFilters.Attributes
							 | NotifyFilters.CreationTime
							 | NotifyFilters.DirectoryName
							 | NotifyFilters.FileName
							 | NotifyFilters.LastWrite
							 | NotifyFilters.Security
							 | NotifyFilters.Size;

		watcher.EnableRaisingEvents = true;
		watcher.Changed += OnWatcherChanged;
	}

	public static bool IsFileReady( string path )
	{
		try
		{
			using ( FileStream inputStream = File.OpenRead( path ) )
				return inputStream.Length > 0;
		}
		catch ( Exception )
		{
			return false;
		}
	}

	private void OnWatcherChanged( object sender, FileSystemEventArgs e )
	{
		IsDirty = true;
	}

	public void Recompile()
	{
		if ( !IsFileReady( Path ) )
			return;

		shaderInfo = ShaderCompiler.CompileShader( Path );
		OnRecompile?.Invoke();
		IsDirty = false;
	}
}
