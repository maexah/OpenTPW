using System.Runtime.InteropServices;
using Veldrid;

namespace OpenTPW;

public class Model : Asset
{
	public DeviceBuffer VertexBuffer { get; private set; } = null!;
	public DeviceBuffer? IndexBuffer { get; private set; } = null;

	public Material Material { get; private set; }
	public bool IsIndexed { get; private set; }

	private uint indexCount;
	private uint vertexCount;

	public Model( Vertex[] vertices, uint[] indices, Material material )
	{
		Material = material;
		IsIndexed = true;

		SetupMesh( vertices, indices );

		Register();
	}

	public Model( Vertex[] vertices, Material material )
	{
		Material = material;
		IsIndexed = false;

		SetupMesh( vertices );

		Register();
	}

	private void SetupMesh( Vertex[] vertices )
	{
		var factory = Device.ResourceFactory;
		var vertexStructSize = (uint)Marshal.SizeOf( typeof( Vertex ) );
		vertexCount = (uint)vertices.Length;

		VertexBuffer = factory.CreateBuffer(
			new BufferDescription( vertexCount * vertexStructSize, BufferUsage.VertexBuffer )
		);

		Device.UpdateBuffer( VertexBuffer, 0, vertices );
	}

	private void SetupMesh( Vertex[] vertices, uint[] indices )
	{
		SetupMesh( vertices );

		var factory = Device.ResourceFactory;
		indexCount = (uint)indices.Length;

		IndexBuffer = factory.CreateBuffer(
			new BufferDescription( indexCount * sizeof( uint ), BufferUsage.IndexBuffer )
		);

		Device.UpdateBuffer( IndexBuffer, 0, indices );
	}

	/// <summary>
	/// Moves this model's vertices into memory the CPU can write to directly, for a mesh that is
	/// going to be rewritten every frame.
	///
	/// Veldrid only has a fast path for a buffer it has been told is dynamic: that memory stays
	/// mapped, and an update is a memcpy. For any other buffer an update means fetching a staging
	/// buffer, recording a copy into a fresh command buffer, submitting it and waiting on a
	/// fence - per call. That measured at about 0.12ms each here, so the lobby's seventy animated
	/// flyers were spending 16.7ms a frame, or 100fps, on nothing but telling the driver their
	/// wings had moved. Marking those buffers dynamic gets all of it back.
	///
	/// Call this before the first update, with the data the buffer already holds - the new
	/// buffer starts empty, so it has to be given the current contents or the mesh renders as
	/// garbage until whatever drives it gets round to its first write.
	/// </summary>
	internal void EnableFrequentUpdates( Vertex[] current )
	{
		if ( VertexBuffer.Usage.HasFlag( BufferUsage.Dynamic ) )
			return;

		var old = VertexBuffer;

		VertexBuffer = Device.ResourceFactory.CreateBuffer(
			new BufferDescription( old.SizeInBytes, BufferUsage.VertexBuffer | BufferUsage.Dynamic ) );

		Device.UpdateBuffer( VertexBuffer, 0, current );

		Render.ScheduleDelete( old.Dispose );
	}

	/// <summary>Re-uploads vertex data, for meshes driven by a vertex animation.</summary>
	internal void UpdateVertices( Vertex[] vertices )
	{
		Device.UpdateBuffer( VertexBuffer, 0, vertices );
	}

	/// <summary>
	/// Re-uploads only the first <paramref name="count"/> vertices.
	///
	/// For a mesh that is a pool of sprites most of which are usually unused - the lobby weather
	/// is one - uploading the whole buffer every frame spends several
	/// hundred kilobytes saying that nothing changed past the part that did.
	/// </summary>
	internal void UpdateVertices( Vertex[] vertices, int count )
	{
		count = Math.Clamp( count, 0, vertices.Length );

		if ( count == 0 )
			return;

		var size = (uint)(count * Marshal.SizeOf<Vertex>());
		Device.UpdateBuffer( VertexBuffer, 0, ref vertices[0], size );
	}

	internal void Draw()
	{
		var commandList = Render.CommandList;

		commandList.SetVertexBuffer( 0, VertexBuffer );
		commandList.SetPipeline( Material.Pipeline );

		Material.GetOrCreateResourceSet( out var resourceSets );

		for ( uint i = 0; i < resourceSets.Length; ++i )
			commandList.SetGraphicsResourceSet( i, resourceSets[i] );

		if ( IsIndexed )
		{
			commandList.SetIndexBuffer( IndexBuffer, IndexFormat.UInt32 );

			commandList.DrawIndexed(
				indexCount: indexCount,
				instanceCount: 1,
				indexStart: 0,
				vertexOffset: 0,
				instanceStart: 0
			);
		}
		else
		{
			commandList.Draw( vertexCount );
		}
	}
}
