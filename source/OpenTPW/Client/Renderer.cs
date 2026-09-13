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
		Window.Resized += OnWindowResized;

		// While it is still hidden, so the window is never shown under the icon the desktop hands out to
		// a program that has named none.
		GameIcon.Apply( Window );

		Window.Visible = true;

		// Only one pointer on screen. The game draws its own - the themed sprite in Cursor.cs, out
		// of the same four-frame strips the original drew - and the original hid the desktop's while
		// it did so, with ShowCursor(0) at 0x00489de1 in its UI_Init. Showing both puts two pointers
		// up, the drawn one always a frame and a present behind the one the window manager
		// composites. Set once: SDL keeps this, and nothing else in the game asks for it back.
		Input.IsSystemCursorVisible = false;

		CreateGraphicsDevice();
		// Swap the buffers so that the screen isn't a mangled mess
		Device.SwapBuffers();
		CreateMultisampledFramebuffer( Screen.Size );

		imGuiRenderer = new ImGuiRenderer( Device, Device.MainSwapchain.Framebuffer.OutputDescription, Window.Size.X, Window.Size.Y );
		ModKit.GlobalNamespace.ImGuiManager = imGuiRenderer;
		new Editor( imGuiRenderer, Device );

		CommandList = Device.ResourceFactory.CreateCommandList();
		CreateBlitPipeline();

		_lastFrame = _frameClock.Elapsed;
	}

	private void CreateMultisampledFramebuffer( Point2 size )
	{
		var colorTextureInfo = TextureDescription.Texture2D(
			(uint)(size.X),
			(uint)(size.Y),
			1,
			1,
			PixelFormat.B8_G8_R8_A8_UNorm,
			TextureUsage.RenderTarget,
			TextureSampleCount.Count4
		);

		_colorTarget = Device.ResourceFactory.CreateTexture( colorTextureInfo );

		var depthTextureInfo = TextureDescription.Texture2D(
			(uint)(size.X),
			(uint)(size.Y),
			1,
			1,
			PixelFormat.D32_Float_S8_UInt,
			TextureUsage.DepthStencil,
			TextureSampleCount.Count4
		);

		_depthTarget = Device.ResourceFactory.CreateTexture( depthTextureInfo );

		colorTextureInfo.SampleCount = TextureSampleCount.Count1;
		colorTextureInfo.Usage = TextureUsage.Sampled;

		ResolveColorTexture = Device.ResourceFactory.CreateTexture( colorTextureInfo );

		var framebufferAttachmentInfo = new FramebufferAttachmentDescription( _colorTarget, 0 );
		var depthAttachmentInfo = new FramebufferAttachmentDescription( _depthTarget, 0 );
		var framebufferDescription = new FramebufferDescription()
		{
			ColorTargets = [framebufferAttachmentInfo],
			DepthTarget = depthAttachmentInfo
		};

		MultisampledFramebuffer = Device.ResourceFactory.CreateFramebuffer( framebufferDescription );
	}

	public Framebuffer MultisampledFramebuffer;
	public Veldrid.Texture ResolveColorTexture;

	/// <summary>
	/// What <see cref="MultisampledFramebuffer"/> is drawn into. Veldrid does not dispose a
	/// framebuffer's attachments along with it, so they are held here to be let go of by hand:
	/// at 1080p the pair is around a hundred megabytes, and a window being dragged would otherwise
	/// leak that much again every time it settled on a new size.
	/// </summary>
	private Veldrid.Texture _colorTarget = null!;
	private Veldrid.Texture _depthTarget = null!;

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

		// Overwritten, not blended. This draw is a copy of the finished frame, and the swapchain
		// image it copies into is never cleared - it still holds the frame presented two or three
		// swaps ago. Alpha blending here takes only the new frame's own alpha and lets the rest of
		// that stale image through, and the finished frame does carry alpha below one wherever
		// anything was drawn over it: the interface, the cursor, the flyers, the rain and the cut
		// edges of foliage all blend with SourceAlpha/InverseSourceAlpha, which applies to the
		// alpha channel as well as to colour and leaves it at srcA^2 + dstA(1 - srcA). So whatever
		// moved was followed by faint copies of where it had been.
		var pipelineDescription = new GraphicsPipelineDescription(
			BlendStateDescription.SingleOverrideBlend,
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

		// Resized mid-load, which the loading screen itself also answers - see LoadingScreen.Draw.
		ApplyResize();

		if ( !Window.HasArea )
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

		// Before anything is drawn, and with no command list open - see ApplyResize.
		ApplyResize();

		// Minimised: there is nothing to draw into, and no swapchain to wait on either, so without
		// the pause here the loop would spin as fast as the processor allows for as long as the
		// window stays down. The clock is not advanced, so the world takes up where it left off
		// rather than lurching forward by however long the window was away.
		if ( !Window.HasArea )
		{
			Thread.Sleep( 16 );
			return;
		}

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

	/// <summary>
	/// Which graphics API the game draws with. Vulkan on Linux and Windows; Metal on macOS, which has no
	/// Vulkan driver of its own - asking for Vulkan there fails inside the native loader, before any of this
	/// code could say why.
	///
	/// <para>
	/// Windows stays on Vulkan rather than Direct3D 11 deliberately. The shaders are written as Vulkan GLSL,
	/// and Vulkan is the path exercised here every day, so a report from Windows lands on the same code Linux
	/// runs. Direct3D is only a cross-compile away - <see cref="ShaderCompiler"/> already maps it to HLSL -
	/// but nothing would be testing it, and an untested third path is worth less than a second tested one.
	/// </para>
	/// </summary>
	private static GraphicsBackend Backend => OperatingSystem.IsMacOS() ? GraphicsBackend.Metal : GraphicsBackend.Vulkan;

	private void CreateGraphicsDevice()
	{
		var options = new GraphicsDeviceOptions()
		{
			// Vulkan's clip space has Y running down the screen where Metal's runs up it. This asks Vulkan
			// for Metal's direction - Veldrid gets it by giving the viewport a negative height - so that one
			// set of shaders, and the pixel-to-clip-space arithmetic the interface does, mean the same thing
			// on both.
			PreferStandardClipSpaceYDirection = true,

			// Both are natively 0-to-1 in depth, which is what System.Numerics builds - see
			// Camera.CalcViewProjMatrix. Only OpenGL would need converting, and OpenGL is not offered.
			PreferDepthRangeZeroToOne = true,

			SwapchainDepthFormat = null,
			SwapchainSrgbFormat = false,
			SyncToVerticalBlank = true,
			HasMainSwapchain = true
		};

		// Built by hand rather than through VeldridStartup.CreateGraphicsDevice, so that the options above
		// are the ones actually used. It is the same swapchain VeldridStartup would build, and
		// GetSwapchainSource already answers every window system the game runs on, an NSWindow included.
		var swapchain = new SwapchainDescription(
			VeldridStartup.GetSwapchainSource( Window.SdlWindow ),
			(uint)Window.Size.X,
			(uint)Window.Size.Y,
			options.SwapchainDepthFormat,
			options.SyncToVerticalBlank,
			options.SwapchainSrgbFormat );

		Device = Backend switch
		{
			GraphicsBackend.Metal => GraphicsDevice.CreateMetal( options, swapchain ),
			_ => GraphicsDevice.CreateVulkan( options, swapchain )
		};
	}

	/// <summary>The size the window has become, until the render targets have been built for it.</summary>
	private Point2? _resizedTo;

	/// <summary>
	/// SDL raises this from inside the event pump, once for every size a window passes through as it
	/// is dragged. Building a swapchain and a set of multisampled targets for each of those would be
	/// tens of megabytes of allocation a frame, so only the newest size is kept and the frame builds
	/// it once - see <see cref="ApplyResize"/>.
	/// </summary>
	private void OnWindowResized( Point2 newSize ) => _resizedTo = newSize;

	/// <summary>
	/// Builds the swapchain and the render targets for the size the window has become, if it has.
	/// Called from the frame after the events have been pumped and before anything is drawn, so
	/// there is no command list open and nothing part way through a frame that used the old targets.
	/// </summary>
	private void ApplyResize()
	{
		if ( _resizedTo is not { } size )
			return;

		_resizedTo = null;

		// Minimised rather than resized. Veldrid will not build a texture or a swapchain with no
		// pixels in it, and the window will say so again on its way back up.
		if ( size.X <= 0 || size.Y <= 0 )
			return;

		Device.MainSwapchain.Resize( (uint)size.X, (uint)size.Y );

		// The GPU may still be reading the targets the last frame was drawn into: nothing fences
		// between SwapBuffers and here, so letting go of them now is a use-after-free that Vulkan is
		// entitled to fault on. A resize is rare enough to afford waiting for the device to finish.
		Device.WaitForIdle();

		MultisampledFramebuffer?.Dispose();
		ResolveColorTexture?.Dispose();
		_colorTarget?.Dispose();
		_depthTarget?.Dispose();
		_blitResourceSet?.Dispose();

		CreateMultisampledFramebuffer( size );

		// The blit set names the resolve texture, so it is built again with it.
		_blitResourceSet = Device.ResourceFactory.CreateResourceSet( new ResourceSetDescription(
			_blitResourceLayout,
			ResolveColorTexture,
			Device.LinearSampler
		) );

		imGuiRenderer?.WindowResized( size.X, size.Y );
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
