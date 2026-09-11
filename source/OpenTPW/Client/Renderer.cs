using System.Diagnostics;
using Veldrid;
using Veldrid.StartupUtilities;

namespace OpenTPW;

public partial class Renderer
{
	/// <summary>
	/// Frame timing, from a monotonic clock rather than the wall clock.
	///
	/// <c>DateTime.Now</c> can step - an NTP correction or a daylight-saving change moves it,
	/// backwards included - and every frame-rate-independent formula in the project is built on
	/// the delta being a real, positive elapsed time. A negative one turns
	/// <see cref="Time.SmoothingFactor"/> negative, which eases away from its target rather than
	/// toward it. It also reads the local timezone on every call, for a value only ever used as
	/// a difference.
	/// </summary>
	private readonly Stopwatch _frameClock = Stopwatch.StartNew();
	private TimeSpan _lastFrame;

	public CommandList CommandList = null!;

	public Window Window;
	public ImGuiRenderer imGuiRenderer;

	public Action? PreUpdate;
	public Action? OnUpdate;
	public Action? PostUpdate;

	public Action? OnRender;

	public Renderer()
	{
		Window = new( Settings.Default.GameWindowSize.X, Settings.Default.GameWindowSize.Y, "Theme Park World", true );
		Window.OnResized = OnWindowResized;
		Window.Visible = true;

		CreateGraphicsDevice();
		// Swap the buffers so that the screen isn't a mangled mess
		Device.SwapBuffers();
		CreateMultisampledFramebuffer();

		imGuiRenderer = new ImGuiRenderer( Device, Device.MainSwapchain.Framebuffer.OutputDescription, Window.Size.X, Window.Size.Y );
		ModKit.GlobalNamespace.ImGuiManager = imGuiRenderer;
		new Editor( imGuiRenderer, Device );

		CommandList = Device.ResourceFactory.CreateCommandList();
		CreateBlitPipeline();
		OnWindowResized( Window.Size );

		_lastFrame = _frameClock.Elapsed;
	}

	private void CreateMultisampledFramebuffer()
	{
		var colorTextureInfo = TextureDescription.Texture2D(
			(uint)(Screen.Size.X),
			(uint)(Screen.Size.Y),
			1,
			1,
			PixelFormat.B8_G8_R8_A8_UNorm,
			TextureUsage.RenderTarget,
			TextureSampleCount.Count4
		);

		var colorTexture = Device.ResourceFactory.CreateTexture( colorTextureInfo );

		var depthTextureInfo = TextureDescription.Texture2D(
			(uint)(Screen.Size.X),
			(uint)(Screen.Size.Y),
			1,
			1,
			PixelFormat.D32_Float_S8_UInt,
			TextureUsage.DepthStencil,
			TextureSampleCount.Count4
		);

		var depthTexture = Device.ResourceFactory.CreateTexture( depthTextureInfo );

		colorTextureInfo.SampleCount = TextureSampleCount.Count1;
		colorTextureInfo.Usage = TextureUsage.Sampled;

		ResolveColorTexture = Device.ResourceFactory.CreateTexture( colorTextureInfo );

		var framebufferAttachmentInfo = new FramebufferAttachmentDescription( colorTexture, 0 );
		var depthAttachmentInfo = new FramebufferAttachmentDescription( depthTexture, 0 );
		var framebufferDescription = new FramebufferDescription()
		{
			ColorTargets = [framebufferAttachmentInfo],
			DepthTarget = depthAttachmentInfo
		};

		MultisampledFramebuffer = Device.ResourceFactory.CreateFramebuffer( framebufferDescription );
	}

	public Framebuffer MultisampledFramebuffer;
	public Veldrid.Texture ResolveColorTexture;

	private Pipeline _blitPipeline;
	private ResourceSet _blitResourceSet;
	private ResourceLayout _blitResourceLayout;

	/// <summary>
	/// What copies each finished frame to the window. Made with the renderer rather than when the
	/// game loop starts, as the loading screen presents frames before then.
	/// </summary>
	private void CreateBlitPipeline()
	{
		var layoutDescription = new ResourceLayoutDescription(
			new ResourceLayoutElementDescription( "g_tInput", ResourceKind.TextureReadOnly, ShaderStages.Fragment ),
			new ResourceLayoutElementDescription( "g_sSampler", ResourceKind.Sampler, ShaderStages.Fragment )
		);

		_blitResourceLayout = Device.ResourceFactory.CreateResourceLayout( layoutDescription );

		// Create shader
		var shader = new Shader( "content/shaders/blit.shader" );
		var blitShader = shader.ShaderProgram;

		var pipelineDescription = new GraphicsPipelineDescription(
			BlendStateDescription.SingleAlphaBlend,
			DepthStencilStateDescription.Disabled,
			RasterizerStateDescription.CullNone,
			PrimitiveTopology.TriangleList,
			new ShaderSetDescription(
				Array.Empty<VertexLayoutDescription>(),
				shader.ShaderProgram
			),
			[_blitResourceLayout],
			Device.MainSwapchain.Framebuffer.OutputDescription
		);

		_blitPipeline = Device.ResourceFactory.CreateGraphicsPipeline( pipelineDescription );

		_blitResourceSet = Device.ResourceFactory.CreateResourceSet( new ResourceSetDescription(
			_blitResourceLayout,
			ResolveColorTexture,
			Device.LinearSampler
		) );
	}

	public void Run()
	{
		while ( Window.SdlWindow.Exists )
		{
			FrameProfiler.Wrap( Update );
		}
	}

	private void PreRender()
	{
		foreach ( var shader in Asset.All.OfType<Shader>().Where( x => x.IsDirty ) )
		{
			shader.Recompile();
		}

		CommandList.Begin();
	}

	private void PostRender()
	{
		// Cleared to the sky rather than to black. The sky is geometry, and the lobby's is four
		// layers of cloud on an open dome - about a fifth of it is gaps, with nothing behind them
		// but whatever the frame started as. Black there reads as holes punched in the sky.
		var sky = Level.FogColour;
		DrawScene( "Main Render", OnRender, new RgbaFloat( sky.X, sky.Y, sky.Z, 1f ) );

		Editor.Instance?.Render( CommandList );

		Present();
	}

	/// <summary>
	/// Draws and presents one frame of <paramref name="draw"/> alone, outside the game loop. For the
	/// loading screen, which has to show something while a level is still being built and nothing
	/// else can run. Pumps the window's events as well, so the desktop does not take the game for
	/// hung while it loads.
	/// </summary>
	public void DrawLoadingFrame( Action draw )
	{
		Window.SdlWindow.PumpEvents();

		// Closed mid-load, so there is nothing left to draw into. The game loop finds the same once
		// the load is done, and ends.
		if ( !Window.SdlWindow.Exists )
			return;

		CommandList.Begin();
		DrawScene( "Loading Screen", draw, RgbaFloat.Black );
		Present();

		ProcessDeletionQueue();
	}

	/// <summary>
	/// Draws into the multisampled framebuffer, cleared to <paramref name="clear"/>, and copies the
	/// result to the window. The command list has to have been begun.
	/// </summary>
	private void DrawScene( string name, Action? draw, RgbaFloat clear )
	{
		CommandList.SetFramebuffer( MultisampledFramebuffer ); // Use MSAA framebuffer
		CommandList.SetViewport( 0, new Viewport( 0, 0, MultisampledFramebuffer.Width, MultisampledFramebuffer.Height, 0, 1 ) );
		CommandList.SetFullViewports();
		CommandList.SetFullScissorRects();
		CommandList.ClearDepthStencil( 1 );
		CommandList.ClearColorTarget( 0, clear );

		CommandList.PushDebugGroup( name );
		draw?.Invoke();
		CommandList.PopDebugGroup();

		// Resolve MSAA to non-MSAA texture
		CommandList.ResolveTexture( MultisampledFramebuffer.ColorTargets[0].Target, ResolveColorTexture );

		// Blit to screen
		CommandList.SetFramebuffer( Device.MainSwapchain.Framebuffer );
		CommandList.SetViewport( 0, new Viewport( 0, 0, Device.MainSwapchain.Framebuffer.Width, Device.MainSwapchain.Framebuffer.Height, 0, 1 ) );

		CommandList.SetPipeline( _blitPipeline );
		CommandList.SetGraphicsResourceSet( 0, _blitResourceSet );
		CommandList.Draw( 3, 1, 0, 0 );
	}

	private void Present()
	{
		CommandList.End();

		Device.SubmitCommands( CommandList );
		Device.SwapBuffers();
	}

	private void Update()
	{
		var now = _frameClock.Elapsed;
		float deltaTime = (float)(now - _lastFrame).TotalSeconds;
		_lastFrame = now;

		InputSnapshot inputSnapshot = Window.SdlWindow.PumpEvents();

		// Closed while those events were pumped - the window's own close button, or the desktop asking the
		// game to go. SDL has already destroyed the window, so there is nothing left to draw into and
		// Present would throw trying to acquire an image from a swapchain whose window has gone. The loop's
		// own test ends the game on its next turn, which is where whoever is playing is saved. The loading
		// screen stops for the same reason - see DrawLoadingFrame.
		if ( !Window.SdlWindow.Exists )
			return;

		Time.Update( deltaTime );
		Input.UpdateFrom( inputSnapshot );

		if ( Input.Pressed( InputButton.EditorToggle ) )
			Editor.Instance.shouldRender = !Editor.Instance.shouldRender;
		if ( Editor.Instance.shouldRender )
			Editor.Instance.UpdateFrom( inputSnapshot );

		PreRender();
		PreUpdate?.Invoke();

		OnUpdate?.Invoke();

		PostRender();
		PostUpdate?.Invoke();

		ProcessDeletionQueue();
	}

	private void CreateGraphicsDevice()
	{
		var options = new GraphicsDeviceOptions()
		{
			PreferStandardClipSpaceYDirection = true,
			PreferDepthRangeZeroToOne = true,
			SwapchainDepthFormat = null,
			SwapchainSrgbFormat = false,
			SyncToVerticalBlank = true,
			HasMainSwapchain = true
		};

		var swapchainSource = VeldridStartup.GetSwapchainSource( Window.SdlWindow );
		Device = GraphicsDevice.CreateVulkan( swapchainDescription: new SwapchainDescription( swapchainSource, (uint)(Window.Size.X), (uint)(Window.Size.Y), options.SwapchainDepthFormat, options.SyncToVerticalBlank, options.SwapchainSrgbFormat ), options: options );
	}

	public void OnWindowResized( Point2 newSize )
	{
		var dpiScale = 1.0f;
		Device.MainSwapchain.Resize( (uint)(newSize.X * dpiScale), (uint)(newSize.Y * dpiScale) );

		// Cleanup old MSAA resources
		MultisampledFramebuffer?.Dispose();
		ResolveColorTexture?.Dispose();

		// Recreate MSAA resources
		CreateMultisampledFramebuffer();

		// Recreate blit resources since they depend on the framebuffer
		_blitResourceSet?.Dispose();
		_blitResourceSet = Device.ResourceFactory.CreateResourceSet( new ResourceSetDescription(
			_blitResourceLayout,
			ResolveColorTexture,
			Device.LinearSampler
		) );

		imGuiRenderer?.WindowResized( newSize.X, newSize.Y );
	}

	public void ImmediateSubmit( Action<CommandList> action )
	{
		var commandList = Device.ResourceFactory.CreateCommandList();
		commandList.Begin();

		action( commandList );

		commandList.End();
		Device.SubmitCommands( commandList );

		ScheduleDelete( commandList.Dispose );
	}
}
