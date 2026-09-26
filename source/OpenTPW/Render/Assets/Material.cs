using System.Diagnostics;
using NeoVeldrid;

namespace OpenTPW;

[Flags]
public enum MaterialFlags
{
	None = 0,

	DisableDepthTest = 1,
	DisableDepthWrite = 2,

	DisableDepth = DisableDepthTest | DisableDepthWrite,

	/// <summary>
	/// Adds to what is already there instead of blending over it, so black contributes nothing.
	/// Needed by art that carries no alpha channel and relies on a black background being
	/// invisible - the game's own Raindrop.tga is exactly that.
	/// </summary>
	Additive = 4,

	/// <summary>
	/// Draws both faces, as the original draws every face it has - a blade of grass is one quad seen
	/// from either side, and the sky is geometry the camera sits inside, with no outward side to cull.
	/// </summary>
	DisableCulling = 8
}

public partial class Material : Asset
{
	public Shader Shader { get; set; }

	public Type UniformBufferType { get; } = typeof( ObjectUniformBuffer );
	public Pipeline Pipeline { get; private set; } = null!;

	private Dictionary<string, BindableResource> _boundResources = new();

	private ResourceLayout[] _resourceLayouts;

	private ResourceSet[]? _cachedResourceSets;
	private bool _resourceSetsDirty = true;

	/// <summary>
	/// This material's handler on its shader's recompile, kept so that <see cref="Delete"/> can take it
	/// off again. It has to be a field: the subscription is a closure over the material's own flags, so
	/// <c>-=</c> with an equivalent lambda would compare unequal and remove nothing at all.
	///
	/// <para>
	/// A shader outlives every material that ever drew with it - it is cached by path and never
	/// released - so a material that left its handler on would leave a strong reference to itself in
	/// that event for the rest of the run, and a recompile would call <see cref="SetupResources"/> on
	/// a material whose pipeline has been disposed.
	/// </para>
	/// </summary>
	private readonly Action _onShaderRecompiled;

	public Material( string shaderPath, MaterialFlags flags = MaterialFlags.None )
	{
		Shader = Shader.GetOrCreate( shaderPath );

		_onShaderRecompiled = () => SetupResources( flags );
		Shader.OnRecompile += _onShaderRecompiled;

		Register();
		SetupResources( flags );
	}

	protected Material( string shaderPath, Type uniformBufferType, MaterialFlags flags = MaterialFlags.None )
	{
		Shader = Shader.GetOrCreate( shaderPath );

		_onShaderRecompiled = () => SetupResources( flags );
		Shader.OnRecompile += _onShaderRecompiled;

		UniformBufferType = uniformBufferType;

		Register();
		SetupResources( flags );
	}

	private DeviceBuffer ScratchBuffer;

	private static readonly Sampler[] Samplers =
	[
		CreateSampler( SamplerType.Anisotropic ),
		CreateSampler( SamplerType.Linear ),
		CreateSampler( SamplerType.Point ),
		CreateSampler( SamplerType.AnisotropicWrap ),
		CreateSampler( SamplerType.AnisotropicRepeat ),
	];

	private static Sampler CreateSampler( SamplerType type )
	{
		var samplerFilter = type switch
		{
			SamplerType.Anisotropic or SamplerType.AnisotropicWrap or SamplerType.AnisotropicRepeat => SamplerFilter.Anisotropic,
			SamplerType.Linear => SamplerFilter.MinLinear_MagLinear_MipLinear,
			SamplerType.Point => SamplerFilter.MinPoint_MagPoint_MipPoint,
			_ => throw new NotImplementedException()
		};

		var samplerAddressMode = type switch
		{
			SamplerType.Anisotropic or SamplerType.Linear or SamplerType.Point => SamplerAddressMode.Clamp,
			SamplerType.AnisotropicWrap => SamplerAddressMode.Wrap,
			SamplerType.AnisotropicRepeat => SamplerAddressMode.Mirror,
			_ => throw new NotImplementedException()
		};

		var samplerDescription = new SamplerDescription(
			samplerAddressMode,
			samplerAddressMode,
			samplerAddressMode,
			samplerFilter,
			ComparisonKind.Always,
			(type == SamplerType.Anisotropic || type == SamplerType.AnisotropicWrap || type == SamplerType.AnisotropicRepeat) ? 16u : 0u,
			0,
			10,
			0,
			SamplerBorderColor.TransparentBlack
		);

		return Device.ResourceFactory.CreateSampler( samplerDescription );
	}

	/// <summary>
	/// Releases what this material owns and takes it out of <see cref="Asset.All"/>, once the frame in
	/// progress is done with it.
	///
	/// <para>
	/// <b>Only what it owns.</b> Its layouts and its resource sets are minted here from the shader's
	/// descriptions - see <see cref="CreateResourceLayouts"/> - so they belong to this material and
	/// nothing else holds them. Three things it holds are <i>not</i> its: the <see cref="Shader"/>,
	/// which is cached by path and shared by every material drawn with it; the samplers, which are one
	/// static set for the whole program; and the textures bound into its resource sets, which are
	/// cached by path and shared - disposing a resource set does not touch what the set binds, which is
	/// what makes this safe.
	/// </para>
	/// <para>
	/// The two shared materials refuse, the way <see cref="Texture.Missing"/> does. <see cref="UI"/> is held
	/// by the whole interface, and <see cref="Default"/> is a static any caller may take though none does,
	/// so freeing either would take it from everything holding it.
	/// </para>
	/// </summary>
	public void Delete()
	{
		if ( ReferenceEquals( this, Default ) || ReferenceEquals( this, UI ) )
		{
			Log.Warning( "Material: one of the shared materials was asked to delete itself, which would take it from everything holding it - ignored" );
			return;
		}

		Shader.OnRecompile -= _onShaderRecompiled;

		All.Remove( this );

		// Read out before the queue runs, so what is disposed is what this material held when it was
		// deleted rather than whatever it might be left pointing at.
		var pipeline = Pipeline;
		var scratch = ScratchBuffer;
		var layouts = _resourceLayouts;
		var sets = _cachedResourceSets;
		var rounds = _frameBlocks;

		_boundResources.Clear();

		Render.ScheduleDelete( () =>
		{
			if ( sets != null )
				DestroyResourceSets( sets );

			foreach ( var round in rounds )
			{
				foreach ( var block in round )
				{
					if ( block.Sets is { } blockSets )
						DestroyResourceSets( blockSets );

					block.Buffer.Dispose();
				}

				round.Clear();
			}

			if ( layouts != null )
			{
				foreach ( var layout in layouts )
					layout.Dispose();
			}

			scratch?.Dispose();
			pipeline?.Dispose();
		} );
	}

	/// <summary>
	/// Binds this material's uniform block. Called once per model per frame, so it is on the
	/// hottest path in the renderer and allocates nothing per draw.
	/// </summary>
	public void Set<T>( string name, T obj ) where T : unmanaged
	{
		Device.UpdateBuffer( ScratchBuffer, 0, ref obj );
		_boundResources[name] = ScratchBuffer;
		_drawingBlock = null;
	}

	/// <summary>
	/// Binds a uniform block like <see cref="Set{T}"/>, for a material drawn several times in one frame:
	/// each call writes a block of its own, which the draw that follows binds.
	///
	/// <para>
	/// <see cref="Set{T}"/> has the one block, written straight to the device ahead of everything the
	/// frame records, so a material drawn several times in a frame draws every time with the last value
	/// it was given. Nothing in the world is drawn twice with one material, but the interface is: four
	/// player buttons wear the one purple mesh, and all four would land on the last.
	/// </para>
	/// <para>
	/// Writing that one block through the frame's command list, between the draws, is not reliable either:
	/// with the options screen's two dozen such writes a frame, now and then a draw comes out with the
	/// value before its own. A block for each draw leaves nothing to order.
	/// </para>
	/// <para>
	/// The blocks are kept and handed out again, in the same order, two frames later: there are two
	/// rounds of them, used on alternate frames, so a block is never rewritten while the frame before
	/// might still be drawing with it.
	/// </para>
	/// </summary>
	public void SetInFrame<T>( string name, T obj ) where T : unmanaged
	{
		if ( !_frameEndScheduled )
		{
			_frameEndScheduled = true;
			Render.ScheduleDelete( EndFrame );
		}

		var round = _frameBlocks[_frameRound];

		if ( _frameBlocksUsed == round.Count )
		{
			var description = new BufferDescription( 16 * 128, BufferUsage.UniformBuffer | BufferUsage.Dynamic );
			round.Add( new FrameBlock( Device.ResourceFactory.CreateBuffer( description ) ) );
		}

		var block = round[_frameBlocksUsed++];
		Device.UpdateBuffer( block.Buffer, 0, ref obj );

		if ( block.Sets == null || block.Bindings != _bindings )
		{
			if ( block.Sets is { } stale )
				Render.ScheduleDelete( () => DestroyResourceSets( stale ) );

			_boundResources[name] = block.Buffer;
			block.Sets = CreateResourceSets();
			block.Bindings = _bindings;
		}

		_drawingBlock = block;
	}

	/// <summary>A uniform block <see cref="SetInFrame{T}"/> hands out, and the resource sets binding it with the textures as they were when the sets were made.</summary>
	private sealed class FrameBlock( DeviceBuffer buffer )
	{
		public DeviceBuffer Buffer { get; } = buffer;

		public ResourceSet[]? Sets { get; set; }

		/// <summary>Which of the material's bindings <see cref="Sets"/> were made from - see <see cref="_bindings"/>.</summary>
		public int Bindings { get; set; } = -1;
	}

	/// <summary>The two rounds of blocks - see <see cref="SetInFrame{T}"/>.</summary>
	private readonly List<FrameBlock>[] _frameBlocks = [new(), new()];

	private int _frameRound;
	private int _frameBlocksUsed;
	private bool _frameEndScheduled;

	/// <summary>The block the next draw binds, or null when it binds the material's own.</summary>
	private FrameBlock? _drawingBlock;

	/// <summary>Counts changes to what the material binds, so a block can tell its resource sets are out of date.</summary>
	private int _bindings;

	/// <summary>Once the frame is submitted: the other round is up next.</summary>
	private void EndFrame()
	{
		_frameEndScheduled = false;
		_frameBlocksUsed = 0;
		_frameRound = 1 - _frameRound;
		_drawingBlock = null;
	}

	/// <param name="sampler">How the textures are sampled - wrapping, unless the caller knows better.</param>
	public void Set( string name, Texture[] texture, SamplerType sampler = SamplerType.AnisotropicWrap )
	{
		for ( int i = 0; i < texture.Length; i++ )
		{
			var key = name + $"{i}";
			var resource = texture[i].NativeTexture;

			if ( !_boundResources.TryGetValue( key, out var existing ) || existing != resource )
			{
				_boundResources[key] = resource;
				_resourceSetsDirty = true;
				_bindings++;
			}
		}

		var samplerResource = Samplers[(int)sampler];
		var samplerKey = "s_" + name;

		if ( !_boundResources.TryGetValue( samplerKey, out var existingSampler ) || existingSampler != samplerResource )
		{
			_boundResources[samplerKey] = samplerResource;
			_resourceSetsDirty = true;
			_bindings++;
		}
	}

	public void Set( string name, Texture texture )
	{
		var resource = texture.NativeTexture;
		var samplerResource = Samplers[(int)texture.SamplerType];
		var samplerKey = "s_" + name;

		if ( !_boundResources.TryGetValue( name, out var existing ) || existing != resource )
		{
			_boundResources[name] = resource;
			_resourceSetsDirty = true;
			_bindings++;
		}

		if ( !_boundResources.TryGetValue( samplerKey, out var existingSampler ) || existingSampler != samplerResource )
		{
			_boundResources[samplerKey] = samplerResource;
			_resourceSetsDirty = true;
			_bindings++;
		}
	}

	internal ResourceLayout[] CreateResourceLayouts()
	{
		return Shader.ResourceLayouts.Select( x => Device.ResourceFactory.CreateResourceLayout( x ) ).ToArray();
	}

	internal ResourceSet[] CreateResourceSets()
	{
		Debug.Assert( _resourceLayouts != null );

		List<ResourceSetDescription> resourceSetDescriptions = new();

		for ( int i = 0; i < Shader.ResourceLayouts.Length; i++ )
		{
			ResourceLayoutDescription resourceLayout = Shader.ResourceLayouts[i];
			var sortedBoundResources = new List<BindableResource>();

			foreach ( var resource in resourceLayout.Elements )
			{
				if ( _boundResources.TryGetValue( resource.Name, out var boundResource ) )
				{
					sortedBoundResources.Add( boundResource );
				}
				else
				{
					throw new Exception( $"{resource.Name} wasn't bound at draw time!" );
				}
			}

			var resourceSetDescription = new ResourceSetDescription()
			{
				Layout = _resourceLayouts[i],
				BoundResources = [.. sortedBoundResources]
			};

			resourceSetDescriptions.Add( resourceSetDescription );
		}

		return resourceSetDescriptions.Select( x => Device.ResourceFactory.CreateResourceSet( x ) ).ToArray();
	}

	internal void GetOrCreateResourceSet( out ResourceSet[] resourceSets )
	{
		if ( _drawingBlock is { Sets: { } blockSets } )
		{
			resourceSets = blockSets;
			return;
		}

		if ( _resourceSetsDirty || _cachedResourceSets == null )
		{
			var oldResourceSets = _cachedResourceSets;

			_cachedResourceSets = CreateResourceSets();
			_resourceSetsDirty = false;

			// Mark the previous set for death, once the GPU is done with this frame
			if ( oldResourceSets != null )
				Render.ScheduleDelete( () => DestroyResourceSets( oldResourceSets ) );
		}

		resourceSets = _cachedResourceSets;
	}

	private static void DestroyResourceSets( ResourceSet[] resourceSets )
	{
		if ( resourceSets == null )
		{
			Log.Warning( $"Resource sets were marked for death, but are already dead - we can't kill what's already dead!" );
			return;
		}

		foreach ( var item in resourceSets )
		{
			item.Dispose();
		}
	}

	private void SetupResources( MaterialFlags flags )
	{
		var vertexLayout = new VertexLayoutDescription( Vertex.VertexElementDescriptions );

		//
		// Create resource layout - but only from what we're using/need
		//
		_resourceLayouts ??= CreateResourceLayouts();

		//
		// Create pipeline
		//
		var pipelineDescription = new GraphicsPipelineDescription()
		{
			BlendState = flags.HasFlag( MaterialFlags.Additive )
				? BlendStateDescription.SingleAdditiveBlend
				: BlendStateDescription.SingleAlphaBlend,

			DepthStencilState = new DepthStencilStateDescription(
				!flags.HasFlag( MaterialFlags.DisableDepthTest ),
				!flags.HasFlag( MaterialFlags.DisableDepthWrite ),
				flags.HasFlag( MaterialFlags.DisableDepthTest | MaterialFlags.DisableDepthWrite ) ? ComparisonKind.Always : ComparisonKind.Less
			),

			RasterizerState = new RasterizerStateDescription(
				flags.HasFlag( MaterialFlags.DisableCulling ) ? FaceCullMode.None : FaceCullMode.Back,
				PolygonFillMode.Solid,
				FrontFace.Clockwise,
				true,
				false
			),

			PrimitiveTopology = PrimitiveTopology.TriangleList,
			ResourceLayouts = [.. _resourceLayouts],
			ShaderSet = new ShaderSetDescription( [vertexLayout], Shader.ShaderProgram ),
			Outputs = Render.MultisampledFramebuffer.OutputDescription
		};

		Pipeline = Device.ResourceFactory.CreateGraphicsPipeline( pipelineDescription );

		var bufferDescription = new BufferDescription( 16 * 128, BufferUsage.UniformBuffer | BufferUsage.Dynamic );
		ScratchBuffer = Device.ResourceFactory.CreateBuffer( bufferDescription );
	}
}

public class Material<T>( string shaderPath, MaterialFlags flags = MaterialFlags.None ) : Material( shaderPath, typeof( T ), flags );
