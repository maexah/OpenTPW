using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text;

namespace OpenTPW;

/// <summary>
/// .md2 files in this game come in two kinds, sharing the same 4-byte magic (0x1CD15D46)
/// and the same 0xDD/0xCB constants at 0x04/0x08. They are distinguished by the mesh table
/// pointer at 0x70: a static mesh always has one, an animation file always has zero.
///
/// Determined by testing all 2401 .md2 files present in the real game data (using a harness
/// that reused this project's own WadArchive/BaseFileSystem/ModelFile code), not by reading
/// a handful of files by hand:
///
///   - Static meshes (1122 files, 47%) - what the parser below implements. Real offsets at
///     0x50 (textureListOffset), 0x54 (frameListOffset) and 0x70 (meshPtr), plus embedded
///     ASCII texture names. All but 2 parse correctly.
///
///   - Animation files (1279 files, 53%) - all three of those offsets are zero. These are
///     NOT meshes and must not be parsed as such; doing so is what used to throw
///     EndOfStreamException/ArgumentOutOfRangeException on half the game's .md2 files.
///
/// Evidence that the second kind is animation data for a base model rather than geometry:
///   - 1274 of the 1279 sit next to a static-mesh .md2 whose name is a prefix of their own
///     (droid.MD2 -> droidc/droidi/droidm1/droidm2.MD2). The 5 that don't are named
///     SCALE.MD2, ROTATE.MD2, FLY.MD2, anim.MD2 and scatM1.md2.
///   - They contain no float32 array anywhere that reproduces the model bounding box stored
///     at 0x80, under any layout tried (AoS triples or the grouped-by-4 SoA packing the
///     static path uses) - i.e. they carry no vertex positions at all.
///   - They contain no texture names, consistent with inheriting the base model's materials.
///   - They are frequently much larger than the model they accompany (droidc.MD2 is 110 KB
///     against droid.MD2's 17 KB), which rules out LOD or collision geometry.
///
/// The animation format is decoded - see AnimationFile. In short: one channel per vertex of
/// the mesh it drives (plus two trailing non-vertex channels), sparse keyframes per channel,
/// and each keyframe value is a vertex position packed into three signed 10-bit fields
/// spanning that mesh's bounding box.
///
/// Useful samples: /lobby/terrain/Jun_isleM1.MD2 and Jun_isleM2.MD2, which both animate the
/// 169-vertex "Dino" mesh of Jun_isle.MD2.
/// </summary>
public partial class ModelFile : BaseFormat
{
	public List<Mesh> Meshes { get; private set; } = new();

	/// <summary>
	/// Every node in the model - its meshes first, in the same order as <see cref="Meshes"/>, then
	/// the transform-only nodes after them. See <see cref="ResolveHierarchy"/> for the layout.
	///
	/// Most models never need this: <see cref="Mesh.WorldTransform"/> already has the tree baked
	/// in. It is for a model whose animations turn a node that has no geometry of its own - the
	/// advisor's head and arms are three such nodes, with his face and hands hanging off them.
	/// </summary>
	public List<Node> Nodes { get; private set; } = new();

	/// <summary>One node of the model's tree.</summary>
	public class Node
	{
		/// <summary>The node's own transform, relative to <see cref="ParentIndex"/>.</summary>
		public Matrix4x4 LocalTransform { get; set; } = Matrix4x4.Identity;

		/// <summary>
		/// <see cref="LocalTransform"/> with every ancestor's applied, which is where the node actually
		/// sits in the model - the same composition <see cref="Mesh.WorldTransform"/> carries, and
		/// worked out in the same pass.
		///
		/// It matters for the nodes that are not meshes, because those are the ones that mark a place
		/// rather than occupy one: a local transform alone says nothing about where "ant_emitter" is
		/// until the antenna, the body and the island it hangs off have all been applied.
		/// </summary>
		public Matrix4x4 WorldTransform { get; set; } = Matrix4x4.Identity;

		public int ParentIndex { get; set; } = -1;

		/// <summary>
		/// What the node is called - "ant_emitter", "sound node", "1stperson" - or empty when its
		/// record names nothing.
		///
		/// This is the same word of the record a mesh's name has always come from (+0x54); the
		/// transform-only nodes are simply the ones it was never read for. It is the only place the
		/// file says what a node is <i>for</i>: the id table gives a node a number and a capability
		/// flag - see <see cref="ReadNodeIds"/> - but never a meaning.
		/// </summary>
		public string Name { get; set; } = string.Empty;

		/// <summary>
		/// The record's flag word. 0x200 marks a transform-only node, and the engine hides a node
		/// by setting 0x10 at runtime - no node in the game data ships with it set.
		/// </summary>
		public uint Flags { get; set; }

		/// <summary>
		/// The number the engine looks this node up by when dressing a character, or -1 when the
		/// node has none. See <see cref="ReadNodeIds"/>.
		/// </summary>
		public int Id { get; set; } = -1;

		/// <summary>The flag word of this node's id record; 0 when it has none.</summary>
		public uint IdFlags { get; set; }
	}

	/// <summary>
	/// True when this file is animation data for a separate base model rather than a mesh.
	/// <see cref="Meshes"/> is empty in that case - see the notes on this class.
	/// </summary>
	public bool IsAnimation { get; private set; }

	public ModelFile( Stream stream )
	{
		ReadFromStream( stream );
	}

	public ModelFile( string path )
	{
		ReadFromFile( path );
	}

	public struct Vertex
	{
		public Vector3 Position { get; set; }
		public uint TextureIndex { get; set; }
	}

	public class Mesh
	{
		public string Name { get; set; }
		public uint VertexOffset { get; set; }
		public uint UvOffset { get; set; }
		public uint VertCnt { get; set; }
		public uint MaterialOffset { get; set; }
		public uint FaceOffset { get; set; }
		public uint FaceCount { get; set; }
		public uint VertexCount { get; set; }
		public uint VertexOrderLen { get; set; }
		public uint VertexOrderOffset { get; set; }
		public Vertex[] Vertices { get; set; }
		public uint[] Indices { get; set; }
		public Vector2[] TexCoords { get; set; }
		/// <summary>This mesh's own transform, relative to its parent node.</summary>
		public Matrix4x4 TransformMatrix { get; set; }

		/// <summary>
		/// <see cref="TransformMatrix"/> with every ancestor's transform applied, which is where
		/// the mesh actually belongs in the model. See the hierarchy notes on this class.
		/// </summary>
		public Matrix4x4 WorldTransform { get; set; }

		/// <summary>Index into the model's node list, or -1 for a root. See this class's notes.</summary>
		public int ParentIndex { get; set; } = -1;
		public MaterialData[] Materials { get; set; }

		public Vector3[] Normals { get; set; }

		/// <summary>
		/// Bounding box of this mesh. Not the box its animations quantise vertex positions into -
		/// each morph track carries its own - see AnimationFile.MorphTrack.DecodePosition.
		/// </summary>
		public Vector3 BoundsMin { get; set; }
		public Vector3 BoundsMax { get; set; }

		/// <summary>Maps each entry of <see cref="Vertices"/> back to a source vertex index.</summary>
		public ushort[] VertexOrder { get; set; } = Array.Empty<ushort>();
	}

	public struct FrameData
	{
		public uint Value;
		public uint MustBeZero;
		public ushort Pad;
		public ushort willBe1;
		public uint FrameNameOff;
	}

	public class MaterialData
	{
		public uint FrameOffset;
		public ushort a;
		public ushort b;
		public ushort StartIndex;
		public ushort EndIndex;
		public uint Pad;

		public string Name;

		/// <summary>
		/// The material's flag word, read from the first four bytes of its texture-list entry - so
		/// it belongs to the texture within this model rather than to the material alone, and two
		/// materials naming the same texture share it. Bit 0 is set on every material in the game.
		/// See <see cref="IsTranslucent"/> for the only bit we act on.
		/// </summary>
		public uint Flags;

		/// <summary>
		/// Whether this material is meant to be drawn see-through, from bit 0x2.
		///
		/// Not something the engine tells us outright - it is never read back in the decompile
		/// where a render state is chosen - so it is established from the data. Of the 246 material
		/// uses in lobby.wad, 232 name a texture that ships in the same wad; across those the bit
		/// agrees with the texture header's own alpha-channel byte in 224 cases and disagrees in
		/// eight. Compare against that byte and not against the bit depth: sen_ant1 is stored
		/// 32-bit but declares no alpha channel, so the two questions give different answers.
		///
		/// That tightness is a property of the lobby, not a general one, and it matters as soon as
		/// anything past the lobby is drawn. Across all 12,773 resolvable material uses in the
		/// game, the bit still implies an alpha channel 87% of the time, but only 56% of the
		/// textures that carry one set it - a great deal of the game's 32-bit ground and path art
		/// is drawn opaque. It is an authoring decision, not a restatement of the texture format.
		///
		/// The eight read as authoring choices rather than noise. Six are textures that carry an
		/// alpha channel and are drawn opaque anyway - the Space hoarding's four sign faces and
		/// the two Box meshes sharing sfl_cnr3. The other two set the bit over a texture with no
		/// alpha channel at all: base.md2's Sea, the lobby's own water surface, and the Space
		/// antenna's stalk.
		///
		/// 61 of the 246 carry the bit, and they are mostly not glass. The ripple ring on all four
		/// islands, the Sea, and the antenna's cone are the genuinely see-through ones; the rest is
		/// cut-out art that needs its alpha tested rather than blended - every palm frond
		/// (jtr_plm1), the Fantasy grass blades and sunflower, the Hallow bushes and tree, the
		/// gate rails and fences, the butterflies and the bats. Anything acting on this bit has to
		/// serve both, which is why the shader still discards near-zero alpha.
		///
		/// Bit 0 is the only one set on every material. The engine ORs 0x8030 into this word itself
		/// for the materials of any mesh carrying a UV animation, but 0x10 and 0x20 are not a pair:
		/// they occur alone 33 and 36 times respectively. What either selects, and what the 0x40
		/// seen on just two materials means, is not known.
		/// </summary>
		public bool IsTranslucent => (Flags & 0x2) != 0;
	}

	protected override void ReadFromStream( Stream stream )
	{
		using ( BinaryReader reader = new BinaryReader( stream, Encoding.ASCII, true ) )
		{
			//
			// Animation files carry no mesh table. Detect them up front rather than letting
			// the mesh parse below run off the end of the stream.
			//
			reader.BaseStream.Seek( 0x70, SeekOrigin.Begin );
			uint meshTableOffset = reader.ReadUInt32();

			if ( meshTableOffset == 0 )
			{
				IsAnimation = true;
				return;
			}

			if ( meshTableOffset >= stream.Length )
				throw new InvalidDataException( $"Mesh table offset {meshTableOffset} is past the end of this {stream.Length} byte file" );

			reader.BaseStream.Seek( 0x50, SeekOrigin.Begin );
			uint off2 = reader.ReadUInt32();

			reader.BaseStream.Seek( 0x36, SeekOrigin.Begin );
			ushort frameCount = reader.ReadUInt16();

			reader.BaseStream.Seek( off2 + (8 * frameCount), SeekOrigin.Begin );

			List<string> textures = new();
			for ( int i = 0; i < frameCount; i++ )
			{
				var texName = Encoding.ASCII.GetString( reader.ReadBytes( 20 ) );
				textures.Add( texName );
			}

			// Meshes are only the first meshCnt of nodeCnt nodes; the rest are transform-only
			// nodes in a second table. See ResolveHierarchy.
			reader.BaseStream.Seek( 0x42, SeekOrigin.Begin );
			ushort nodeCnt = reader.ReadUInt16();

			reader.BaseStream.Seek( 0x44, SeekOrigin.Begin );
			ushort meshCnt = reader.ReadUInt16();

			reader.BaseStream.Seek( 0x74, SeekOrigin.Begin );
			uint nodePtr = reader.ReadUInt32();

			reader.BaseStream.Seek( 0x50, SeekOrigin.Begin );
			uint textureListOffset = reader.ReadUInt32();

			reader.BaseStream.Seek( 0x54, SeekOrigin.Begin );
			uint frameListOffset = reader.ReadUInt32();

			reader.BaseStream.Seek( frameListOffset, SeekOrigin.Begin );
			List<FrameData> frameData = new();
			for ( int i = 0; i < frameCount; ++i )
			{
				frameData.Add( new()
				{
					Value = reader.ReadUInt32(),
					MustBeZero = reader.ReadUInt32(),
					Pad = reader.ReadUInt16(),
					willBe1 = reader.ReadUInt16(),
					FrameNameOff = reader.ReadUInt32()
				} );
			}

			reader.BaseStream.Seek( 0x70, SeekOrigin.Begin );
			uint meshPtr = reader.ReadUInt32();

			reader.BaseStream.Seek( meshPtr, SeekOrigin.Begin );

			Meshes = new List<Mesh>( meshCnt );

			for ( int meshIdx = 0; meshIdx < meshCnt; meshIdx++ )
			{
				reader.BaseStream.Seek( meshPtr + (160 * meshIdx), SeekOrigin.Begin );

				reader.BaseStream.Seek( 16, SeekOrigin.Current ); // Skip initial mesh data

				// Read mat4
				Matrix4x4 transformMatrix;

				{
					var ma = new Vector4( reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle() );
					var mb = new Vector4( reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle() );
					var mc = new Vector4( reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle() );
					var md = new Vector4( reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle() );
					transformMatrix = new Matrix4x4(
						ma.X, ma.Y, ma.Z, ma.W,
						mb.X, mb.Y, mb.Z, mb.W,
						mc.X, mc.Y, mc.Z, mc.W,
						md.X, md.Y, md.Z, md.W
					);
				}

				uint texIndex = reader.ReadUInt32();
				uint nOff = reader.ReadUInt32();
				ushort vertexCount = reader.ReadUInt16();
				ushort materialCount = reader.ReadUInt16();
				ushort faceCount = reader.ReadUInt16();
				ushort vertexOrderLength = reader.ReadUInt16();
				uint vertexOffset = reader.ReadUInt32();
				_ = reader.ReadUInt32();
				uint uvOffset = reader.ReadUInt32();
				uint materialOffset = reader.ReadUInt32();
				uint faceOffset = reader.ReadUInt32();
				// The mesh's bounding box lives in here. It is not what animation files
				// quantise vertex positions into - every morph track carries a box of its own -
				// see AnimationFile.MorphTrack.DecodePosition.
				reader.BaseStream.Seek( 4, SeekOrigin.Current );
				var boundsMin = new Vector3( reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle() );
				var boundsMax = new Vector3( reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle() );
				reader.BaseStream.Seek( 4, SeekOrigin.Current );

				uint vertexOrderOffset = reader.ReadUInt32();
				reader.BaseStream.Seek( 8, SeekOrigin.Current ); // Skip _38 and _39

				reader.BaseStream.Seek( nOff, SeekOrigin.Begin );
				string name = "";
				char c;

				do
				{
					c = reader.ReadChar();
					name += c;
				} while ( c != '\0' );

				if ( materialOffset + (20 * (long)materialCount) > stream.Length )
					throw new InvalidDataException( $"Mesh {meshIdx} wants {materialCount} materials at offset {materialOffset}, which runs past the end of this {stream.Length} byte file" );

				reader.BaseStream.Seek( materialOffset, SeekOrigin.Begin );
				List<MaterialData> materials = new();

				for ( int i = 0; i < materialCount; ++i )
				{
					materials.Add( new()
					{
						FrameOffset = reader.ReadUInt32(),
						a = reader.ReadUInt16(),
						b = reader.ReadUInt16(),
						StartIndex = reader.ReadUInt16(),
						EndIndex = reader.ReadUInt16(),
						Pad = reader.ReadUInt32()
					} );
				}

				foreach ( var material in materials )
				{
					// A FrameOffset of 0 is a sentinel for "no texture" - it isn't a real
					// offset into the frame table (real offsets start at textureListOffset),
					// so there's no frame/texture name to resolve for this material.
					if ( material.FrameOffset == 0 )
					{
						material.Name = string.Empty;
						continue;
					}

					// start at texIdOffset, divide by 8 to get index
					uint frameId = (material.FrameOffset - textureListOffset) / 8;
					var frame = frameData[(int)frameId];

					reader.BaseStream.Seek( frame.FrameNameOff, SeekOrigin.Begin );
					var texName = Encoding.ASCII.GetString( reader.ReadBytes( 20 ) );
					var texture = Path.GetFileNameWithoutExtension( texName );

					material.Name = texture;

					reader.BaseStream.Seek( material.FrameOffset, SeekOrigin.Begin );
					material.Flags = reader.ReadUInt32();
				}

				Meshes.Add( new Mesh
				{
					Name = name,
					VertCnt = materialCount,
					UvOffset = uvOffset,
					VertexOffset = vertexOffset,
					MaterialOffset = materialOffset,
					FaceOffset = faceOffset,
					FaceCount = faceCount,
					VertexCount = vertexCount,
					VertexOrderLen = vertexOrderLength,
					VertexOrderOffset = vertexOrderOffset,
					TransformMatrix = transformMatrix,
					BoundsMin = boundsMin,
					BoundsMax = boundsMax,
					Materials = materials.ToArray()
				} );
			}

			ResolveHierarchy( reader, meshCnt, meshPtr, nodeCnt, nodePtr );
			ReadNodeIds( reader );

			// Process mesh data
			for ( int meshIdx = 0; meshIdx < Meshes.Count; meshIdx++ )
			{
				Mesh mesh = Meshes[meshIdx];
				uint meshPosEnd = (meshIdx + 1 < Meshes.Count) ? Meshes[meshIdx + 1].VertexOffset : Meshes[0].MaterialOffset;
				uint cnt = (meshPosEnd - mesh.VertexOffset) / (3 * 4 * 4);

				reader.BaseStream.Seek( mesh.UvOffset, SeekOrigin.Begin );
				List<Vector2> uvs = new List<Vector2>();
				uint uvCnt = mesh.VertexOrderLen;
				if ( uvCnt % 4 != 0 )
				{
					uvCnt += (uint)(4 - (uvCnt % 4));
				}

				while ( uvCnt > 0 )
				{
					int elem = (int)Math.Min( uvCnt, 4 );
					Vector2[] points = new Vector2[elem];

					for ( int i = 0; i < elem; i++ )
						points[i].X = reader.ReadSingle();
					for ( int i = 0; i < elem; i++ )
						points[i].Y = reader.ReadSingle();

					uvs.AddRange( points );
					uvCnt -= (uint)elem;
				}

				mesh.TexCoords = uvs.ToArray();

				reader.BaseStream.Seek( mesh.VertexOffset, SeekOrigin.Begin );

				List<Vector3> vertices = new List<Vector3>();
				uint c = mesh.VertexCount;
				if ( c % 4 != 0 )
				{
					c += (uint)(4 - (c % 4));
				}

				while ( c > 0 )
				{
					int elem = (int)Math.Min( c, 4 );
					Vector3[] points = new Vector3[elem];

					for ( int i = 0; i < elem; i++ )
						points[i].X = reader.ReadSingle();
					for ( int i = 0; i < elem; i++ )
						points[i].Y = reader.ReadSingle();
					for ( int i = 0; i < elem; i++ )
						points[i].Z = reader.ReadSingle();

					vertices.AddRange( points );
					c -= (uint)elem;
				}

				// Read vertex order
				reader.BaseStream.Seek( mesh.VertexOrderOffset, SeekOrigin.Begin );
				ushort[] vertexOrder = new ushort[mesh.VertexOrderLen];
				for ( int i = 0; i < mesh.VertexOrderLen; i++ )
				{
					vertexOrder[i] = reader.ReadUInt16();
				}

				// Re-order vertices
				Vertex[] reorderedVertices = new Vertex[vertexOrder.Length];
				for ( int i = 0; i < vertexOrder.Length; i++ )
				{
					int textureIndex = 0;
					for ( int j = 0; j < mesh.Materials.Length; ++j )
					{
						if ( i >= mesh.Materials[j].StartIndex && i <= mesh.Materials[j].EndIndex )
						{
							textureIndex = j;
							break;
						}
					}

					var vertex = new Vertex()
					{
						Position = vertices[vertexOrder[i]],
						TextureIndex = (uint)textureIndex
					};

					reorderedVertices[i] = vertex;
				}

				mesh.Vertices = reorderedVertices;
				mesh.VertexOrder = vertexOrder;

				// Parse face data
				reader.BaseStream.Seek( mesh.FaceOffset, SeekOrigin.Begin );
				List<uint> indices = new List<uint>();

				for ( int i = 0; i < mesh.FaceCount; i++ )
				{
					reader.ReadUInt16(); // Skip _ptr
					ushort _a = reader.ReadUInt16();
					ushort _b = reader.ReadUInt16();
					ushort _c = reader.ReadUInt16();

					// Reverse winding order
					indices.Add( (uint)_a );
					indices.Add( (uint)_b );
					indices.Add( (uint)_c );
				}

				mesh.Indices = indices.ToArray();

				CalculateNormals( mesh );
			}
		}
	}

	/// <summary>
	/// Resolves the model's node hierarchy and bakes each mesh's <see cref="Mesh.WorldTransform"/>.
	///
	/// A model is a tree of nodes, not a flat list of meshes. The ushort at 0x42 is the total
	/// node count and the ushort at 0x44 the mesh count; the meshes are the first meshCnt nodes,
	/// in the 160-byte records at 0x70, and the remainder are transform-only nodes in 88-byte
	/// records at 0x74. The engine indexes them exactly that way:
	///
	///     node &lt; meshCount ? meshTable + node * 0xA0 : nodeTable + (node - meshCount) * 0x58
	///
	/// Both record kinds start with the same header: a flags word at +0x00 (bit 0x200 marks a
	/// transform-only node - the engine skips material processing for those), then three links
	/// as file offsets - parent at +0x04, next sibling at +0x08, first child at +0x0C - and the
	/// node's own transform at +0x10.
	///
	/// A node's transform is relative to its parent, so ignoring the tree leaves children piled
	/// at the model origin. Jun_isle is the clear case: its three trees hang off dummy nodes
	/// named l_tree1 and l_tree2 plus the Island mesh itself, and two of them store their trunk
	/// and leaves at a local (0, 0, 0) and (0, 3.66, 0) - meaningless until the parent is
	/// applied, which is what put them under the Dino.
	/// </summary>
	private void ResolveHierarchy( BinaryReader reader, ushort meshCount, uint meshPtr,
		ushort nodeCount, uint nodePtr )
	{
		if ( nodeCount < meshCount )
			nodeCount = meshCount;

		var stream = reader.BaseStream;

		// Node index -> file offset of its record, using the engine's own indexing rule.
		long RecordAt( int node ) => node < meshCount
			? meshPtr + (160L * node)
			: nodePtr + (88L * (node - meshCount));

		// The links are stored as file offsets; turn one back into a node index.
		int NodeAt( uint pointer )
		{
			if ( pointer == 0 )
				return -1;

			if ( pointer >= meshPtr && pointer < meshPtr + (160L * meshCount) )
			{
				var offset = pointer - meshPtr;
				return offset % 160 == 0 ? (int)(offset / 160) : -1;
			}

			var extra = nodeCount - meshCount;
			if ( nodePtr != 0 && extra > 0 && pointer >= nodePtr && pointer < nodePtr + (88L * extra) )
			{
				var offset = pointer - nodePtr;
				return offset % 88 == 0 ? meshCount + (int)(offset / 88) : -1;
			}

			return -1;
		}

		var parents = new int[nodeCount];
		var local = new Matrix4x4[nodeCount];

		for ( int node = 0; node < nodeCount; ++node )
		{
			var record = RecordAt( node );

			if ( record < 0 || record + 0x50 > stream.Length )
			{
				parents[node] = -1;
				local[node] = Matrix4x4.Identity;
				continue;
			}

			stream.Seek( record + 0x04, SeekOrigin.Begin );
			parents[node] = NodeAt( reader.ReadUInt32() );

			stream.Seek( record + 0x10, SeekOrigin.Begin );
			local[node] = new Matrix4x4(
				reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(),
				reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(),
				reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(),
				reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle() );
		}

		var world = new Matrix4x4[nodeCount];
		var resolved = new bool[nodeCount];

		Matrix4x4 World( int node, int guard )
		{
			if ( resolved[node] )
				return world[node];

			// A malformed parent chain must not spin forever - bail out to the local transform.
			var parent = parents[node];
			if ( guard <= 0 || parent < 0 || parent >= nodeCount || parent == node )
			{
				world[node] = local[node];
			}
			else
			{
				world[node] = local[node] * World( parent, guard - 1 );
			}

			resolved[node] = true;
			return world[node];
		}

		for ( int i = 0; i < Meshes.Count && i < nodeCount; ++i )
		{
			Meshes[i].ParentIndex = parents[i];
			Meshes[i].WorldTransform = World( i, nodeCount );
		}

		Nodes = new List<Node>( nodeCount );

		for ( int node = 0; node < nodeCount; ++node )
		{
			var record = RecordAt( node );
			uint flags = 0;

			if ( record >= 0 && record + 4 <= stream.Length )
			{
				stream.Seek( record, SeekOrigin.Begin );
				flags = reader.ReadUInt32();
			}

			// The name pointer is the last word of the smaller record kind, so a node record is
				// only whole once it reaches 0x58. Tested apart from the flags above so that a
				// truncated file still gives up what it can rather than nothing.
				var name = string.Empty;

				if ( record >= 0 && record + 0x58 <= stream.Length )
				{
					stream.Seek( record + 0x54, SeekOrigin.Begin );
					name = NameAt( reader, reader.ReadUInt32() );
				}

				Nodes.Add( new Node
				{
					LocalTransform = local[node],

					// Already worked out above for the meshes, and memoised, so asking for the rest
					// costs one walk up each remaining chain rather than a second pass over the tree.
					WorldTransform = World( node, nodeCount ),
					ParentIndex = parents[node],
					Flags = flags,
					Name = name
				} );
		}
	}

	/// <summary>
	/// The NUL-terminated name a node record points at, or empty when it points nowhere in the file.
	/// </summary>
	private static string NameAt( BinaryReader reader, uint offset )
	{
		var stream = reader.BaseStream;

		if ( offset == 0 || offset >= stream.Length )
			return string.Empty;

		stream.Seek( offset, SeekOrigin.Begin );

		var name = new StringBuilder();

		// The longest name in the game is under twenty characters. The cap is against a file whose
		// pointer lands somewhere with no terminator ahead of it, which would otherwise read to the
		// end of the file.
		while ( name.Length < 64 && stream.Position < stream.Length )
		{
			var character = reader.ReadChar();

			if ( character == '\0' )
				break;

			name.Append( character );
		}

		return name.ToString();
	}

	/// <summary>
	/// Attaches each node's lookup id, from the table the engine searches when it puts a costume
	/// on a character.
	///
	/// The ushort at 0x48 is a record count and the uint at 0x7C the table's offset; each record
	/// is 20 bytes, a flag word, the id, then 12 bytes that are zero in most records but not in 376
	/// of the game's 2452, and are not understood. Record r belongs to node (ushort at 0x46) + r.
	/// That pairing is the engine's own: its lookup (0x0044b220) walks the table for a record whose
	/// id matches and whose flag word shares a bit with a mask it is given, returns the record
	/// index, and the costume code adds 0x46 to it to find the node it shows or hides. 346 of the
	/// game's 839 models carry a table, and in all but one it fits inside the model's node count.
	///
	/// The table is a general way of naming nodes rather than a costume list - its flag words vary
	/// widely - but costume lookups ask for flag 0x400, and every record on the advisor has it.
	///
	/// On the advisor the ids are costume pieces - his antennae are 19 and 20, which most costumes
	/// hide (0x00598ca0: all but 3, 8, 12 and 14), and his right hand is 21, which costume 14, his
	/// spatula, hides.
	/// </summary>
	private void ReadNodeIds( BinaryReader reader )
	{
		var stream = reader.BaseStream;

		stream.Seek( 0x46, SeekOrigin.Begin );
		var firstNode = reader.ReadUInt16();
		var count = reader.ReadUInt16();

		stream.Seek( 0x7C, SeekOrigin.Begin );
		var table = reader.ReadUInt32();

		if ( count == 0 || table == 0 || table + (20L * count) > stream.Length )
			return;

		for ( int r = 0; r < count; ++r )
		{
			var node = firstNode + r;

			if ( node >= Nodes.Count )
				break;

			stream.Seek( table + (20L * r), SeekOrigin.Begin );
			Nodes[node].IdFlags = reader.ReadUInt32();
			Nodes[node].Id = (int)reader.ReadUInt32();
		}
	}

	private void CalculateNormals( Mesh mesh )
	{
		Vector3[] normals = new Vector3[mesh.Vertices.Length];

		// Initialize all normals to zero
		for ( int i = 0; i < normals.Length; i++ )
		{
			normals[i] = Vector3.Zero;
		}

		// Calculate face normals and accumulate them for each vertex
		for ( int i = 0; i < mesh.Indices.Length; i += 3 )
		{
			uint i1 = mesh.Indices[i];
			uint i2 = mesh.Indices[i + 1];
			uint i3 = mesh.Indices[i + 2];

			Vector3 v1 = mesh.Vertices[i1].Position;
			Vector3 v2 = mesh.Vertices[i2].Position;
			Vector3 v3 = mesh.Vertices[i3].Position;

			Vector3 edge1 = v2 - v1;
			Vector3 edge2 = v3 - v1;

			Vector3 faceNormal = Vector3.Cross( edge1, edge2 );
			faceNormal = faceNormal.Normal; // Ensure face normal is unit length

			// Accumulate the face normal to all three vertices
			normals[i1] += faceNormal;
			normals[i2] += faceNormal;
			normals[i3] += faceNormal;
		}

		// Normalize all vertex normals to average them
		for ( int i = 0; i < normals.Length; i++ )
		{
			if ( normals[i] != Vector3.Zero )
				normals[i] = normals[i].Normal;
		}

		mesh.Normals = normals;
	}
}
