using System.Diagnostics;
using Veldrid;

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
	/// Draws both faces. For geometry the camera sits inside - the sky is the whole of it - where
	/// there is no outward side to cull and getting the winding wrong makes it vanish entirely.
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

	public Material( string shaderPath, MaterialFlags flags = MaterialFlags.None )
	{
		Shader = Shader.GetOrCreate( shaderPath );
		Shader.OnRecompile += () => SetupResources( flags );

		Register();
		SetupResources( flags );
	}

	protected Material( string shaderPath, Type uniformBufferType, MaterialFlags flags = MaterialFlags.None )
	{
		Shader = Shader.GetOrCreate( shaderPath );
		Shader.OnRecompile += () => SetupResources( flags );
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

	private void ClearBoundResources()
	{
		return;

		if ( _boundResources.Count > 0 )
		{
			_boundResources.Clear();
		}
	}

	/// <summary>
	/// Binds this material's uniform block. Called once per model per frame, so it is on the
	/// hottest path in the renderer: the array literal it used to box the value into allocated
	/// once per draw, and the deletion it queued ran <see cref="ClearBoundResources"/>, which
	/// returns immediately and has done for as long as it has been in the tree. Together those
	/// were a few hundred pointless allocations a frame.
	/// </summary>
	public void Set<T>( string name, T obj ) where T : unmanaged
	{
		Device.UpdateBuffer( ScratchBuffer, 0, ref obj );
		_boundResources[name] = ScratchBuffer;
	}

	/// <summary>
	/// Binds this material's uniform block like <see cref="Set{T}"/>, but writes it through the
	/// frame's command list, so the write lands between the draws either side of it.
	///
	/// <see cref="Set{T}"/> writes straight to the device, ahead of everything the frame has
	/// recorded, so a material drawn several times in one frame draws every time with the last value
	/// it was given. Nothing in the world is drawn twice with one material, but the interface is:
	/// four player buttons wear the one purple mesh, and set this way all four landed on the last.
	/// </summary>
	public void SetInFrame<T>( string name, T obj ) where T : unmanaged
	{
		Render.CommandList.UpdateBuffer( ScratchBuffer, 0, ref obj );
		_boundResources[name] = ScratchBuffer;
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
			}
		}

		var samplerResource = Samplers[(int)sampler];
		var samplerKey = "s_" + name;

		if ( !_boundResources.TryGetValue( samplerKey, out var existingSampler ) || existingSampler != samplerResource )
		{
			_boundResources[samplerKey] = samplerResource;
			_resourceSetsDirty = true;
		}

		Render.ScheduleDelete( ClearBoundResources );
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
		}

		if ( !_boundResources.TryGetValue( samplerKey, out var existingSampler ) || existingSampler != samplerResource )
		{
			_boundResources[samplerKey] = samplerResource;
			_resourceSetsDirty = true;
		}

		Render.ScheduleDelete( ClearBoundResources );
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
