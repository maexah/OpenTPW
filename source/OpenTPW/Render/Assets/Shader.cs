using NeoVeldrid;

namespace OpenTPW;

public partial class Shader : Asset
{
	private ShaderInfo shaderInfo;

	public VertexElementDescription[] VertexElements => shaderInfo.Reflection.VertexElements;
	public ResourceLayoutDescription[] ResourceLayouts => shaderInfo.Reflection.ResourceLayouts;
	public NeoVeldrid.Shader[] ShaderProgram => shaderInfo.ShaderProgram;
	public bool IsDirty { get; private set; }

	/// <summary>
	/// Run when this shader has been compiled again, so that whatever was built from it can be built
	/// again too - a <see cref="Material"/> subscribes to rebuild its pipeline.
	///
	/// <para>
	/// Nullable because it genuinely is: nothing is subscribed until a material asks for this shader,
	/// and <see cref="Recompile"/> invokes it as <c>OnRecompile?.Invoke()</c>. It also has
	/// to be, for a material to take its own handler off again - taking the last one off leaves null
	/// behind, and a shader outlives every material that ever drew with it.
	/// </para>
	/// </summary>
	public Action? OnRecompile { get; set; }

	private FileSystemWatcher? watcher;

	/// <summary>
	/// The switch that puts the file watcher on in a build that would not have it, or off in one that would.
	/// </summary>
	public const string WatchVariable = "OPENTPW_SHADER_WATCH";

#if DEBUG
	private const bool DebugBuild = true;
#else
	private const bool DebugBuild = false;
#endif

	/// <summary>
	/// Whether shaders are watched for an edit on disk and compiled again. A player never edits a shader, so a
	/// shipped (Release) build does not watch: that is one file watcher fewer per shader. A Debug build does, as the
	/// shader author needs; <c>OPENTPW_SHADER_WATCH=1</c> turns it on in a Release build and <c>=0</c> off in a Debug one.
	/// </summary>
	public static bool WatchesFiles( string? setting, bool debugBuild )
		=> setting switch
		{
			"1" => true,
			"0" => false,
			_ => debugBuild
		};

	private static bool _unwatchedLogged;

	/// <summary>How many compiled shaders a recompile has let go of, for the log and the tests.</summary>
	public static int Retired { get; private set; }

	internal Shader( string path )
	{
		// Resolved here as well as in GetOrCreate, for the one caller that builds a Shader straight from the
		// constructor - the blit shader, in Renderer.CreateBlitPipeline. Without it the watcher below is
		// handed a directory relative to wherever the game was started from, and throws.
		Path = ContentDir.GetPath( path );
		Register();

		Recompile();
		RequireCompiled( shaderInfo.VertexShader, Path );

		// A resolved path is rooted, so only a root itself has no folder, and a shader is a file in one.
		var directoryName = System.IO.Path.GetDirectoryName( Path )
			?? throw new InvalidOperationException( $"Shader {Path} is not in a folder" );
		var fileName = System.IO.Path.GetFileName( Path );

		if ( !WatchesFiles( Environment.GetEnvironmentVariable( WatchVariable ), DebugBuild ) )
		{
			if ( !_unwatchedLogged )
			{
				_unwatchedLogged = true;
				Log.Info( $"Shaders: files are not watched ({(DebugBuild ? $"{WatchVariable}=0" : "a Release build")}), so an edit on disk is not compiled again ({WatchVariable}=1 turns it on)" );
			}

			return;
		}

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

		var hadOld = shaderInfo.VertexShader != null;
		var rebuilt = OnRecompile != null;

		// Only this shader's materials are given a new pipeline (OnRecompile). The blit pipeline (Renderer.CreateBlitPipeline)
		// subscribes to nothing: an edit of blit.shader compiles it, disposes the old modules under the pipeline that was
		// built from them and goes on drawing with that one - Vulkan allows it - so a blit edit needs a restart to show.
		Replace( ref shaderInfo, () => ShaderCompiler.CompileShader( Path ), info => (info.VertexShader, info.FragmentShader),
			() => OnRecompile?.Invoke(), action => Render.ScheduleDelete( action ) );
		IsDirty = false;

		if ( hadOld )
			Log.Info( $"Shader {System.IO.Path.GetFileName( Path )} compiled again: the old vertex and fragment shaders are queued for disposal ({Retired} let go in all){(rebuilt ? string.Empty : "; nothing built from it is rebuilt, so the edit shows after a restart")}" );
	}

	/// <summary>
	/// A shader that is missing or empty compiles to nothing, which a pipeline built from it would meet as a null reference
	/// with no path; the game stops naming the file instead (its message the file's name, which the failed-start box shows; the exception's <c>FileName</c> and its log line hold the path), in a Release build as in a Debug one, where the shader is first asked for
	/// (the blit shader at the start; a material's in its static initialiser, whose wrapper <c>StartFailure</c> unwraps).
	/// </summary>
	internal static void RequireCompiled( object? compiled, string path )
	{
		if ( compiled == null )
			throw new FileNotFoundException( $"Shader {System.IO.Path.GetFileName( path )} is missing or empty (is the content/shaders folder beside the game?)", path );
	}

	/// <summary>
	/// The order of a recompile: the new compile is stored, <paramref name="notify"/> runs (every material builds its new
	/// pipeline and queues the old one), and then the old pair is queued. The renderer is asked only then, as the first
	/// compile (the blit shader's) is made while the renderer is still being built and has no old pair.
	///
	/// <para>
	/// The order is for tidiness, not safety: NeoVeldrid frees a pipeline when the frames using it have ended but destroys a
	/// shader module at once, and Vulkan lets a module go while pipelines made from it are in use.
	/// </para>
	/// </summary>
	internal static void Replace<T>( ref T current, Func<T> compile, Func<T, (IDisposable? Vertex, IDisposable? Fragment)> halves,
		Action notify, Action<Action> schedule )
	{
		var old = current;
		current = compile();
		notify();

		var (vertex, fragment) = halves( old );
		Retire( vertex, fragment, schedule );
	}

	/// <summary>
	/// Queues each compiled shader that is there for disposal on <paramref name="schedule"/> (the renderer's queue, which
	/// runs once the frame is submitted; nothing waits for the GPU) and counts it.
	/// </summary>
	internal static void Retire( IDisposable? vertex, IDisposable? fragment, Action<Action> schedule )
	{
		foreach ( var shader in new[] { vertex, fragment } )
		{
			if ( shader == null )
				continue;

			Retired++;
			schedule( shader.Dispose );
		}
	}
}
