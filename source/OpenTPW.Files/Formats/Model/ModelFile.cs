using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text;

namespace OpenTPW;

/// <summary>
/// There are (at least) two structurally different .md2 layouts in the game's data, both
/// sharing the same 4-byte magic number (0x1CD15D46) and the same "0xDD, 0xCB" constants at
/// offset 0x04/0x08 (likely a fixed format/tool version stamp, always identical).
///
/// Verified by parsing all 2401 .md2 files found across the actual game data (via a
/// throwaway harness reusing this project's own WadArchive/BaseFileSystem/ModelFile code,
/// not by inspecting a handful of files by hand):
///
///   - "Variant A" - what this parser below implements. ~1122 files (47%). Has real
///     absolute-offset table pointers at 0x50 (textureListOffset), 0x54 (frameListOffset),
///     0x70 (meshPtr), and embedded ASCII texture-name strings. 1119/1122 of these parse
///     correctly after the FrameOffset==0 sentinel fix below.
///
///   - "Variant B" - NOT implemented by this parser; ~1279 files (53%) hit this. These
///     files have ZERO at all three of those pointer offsets (0x50/0x54/0x70), and contain
///     no embedded texture-name strings anywhere in the file. frameCount (0x36) and meshCnt
///     (0x44) read as plausible small numbers for both variants - only the pointer fields
///     and everything they'd normally lead to differ.
///
/// Confirmed structure for Variant B (verified against many files, cross-referencing
/// header count fields against manually-identified data by pattern, e.g. finding N
/// plausible float triples where N matches a header field - not guessed):
///
///   - Fixed 184-byte (0xB8) header per mesh, structurally similar in spirit to Variant A's
///     but NOT at the same offsets - the 0x36/0x44 count fields read correctly, but nothing
///     resembling Variant A's 0x50/0x54/0x70 pointers exists.
///   - Vertex position array (3x float per vertex) starts immediately at offset 0xB8. Count
///     matches the header field at 0x48 (NOT 0x38, which is a different, still-unidentified
///     count - the 0x38 field looked like "vertex count" in one sample by coincidence but
///     didn't generalize).
///   - uint32 at offset 0x98 == fileSize - 72, exactly, on every file tested. The final 72
///     bytes of the file are a per-mesh footer containing back-pointers (as file-absolute
///     byte offsets) into: the mesh sub-header, a table of ascending integers followed by a
///     table of per-entry float triples clustered near 1.0 (semantics unconfirmed - possibly
///     per-material vertex ranges + blend/weight values, unverified), and a trivial ascending
///     vertex-order index list (1..vertexCount, 0-terminated).
///   - NOT located, despite real effort: where UV coordinates and face/triangle index data
///     live. Every alignment/grouping tried on the remaining bytes produced inconsistent
///     results (e.g. a value of 16256 = the upper 16 bits of the float 1.0f, suggesting a
///     2-byte misalignment somewhere nearby that wasn't run to ground). Do not guess at this
///     - implementing face/UV parsing without being able to actually verify it (there's no
///     spec, no reference tool, and wrong-but-plausible geometry would be worse than a clear
///     "unsupported" failure) risks silently-wrong meshes rendering in-game.
///   - Texture/material assignment for Variant B does not appear to live in the model file at
///     all (no embedded names, unlike Variant A). Checked the owning ride's .sam config (e.g.
///     /levels/jungle/rides/tourride/tourride.sam for Bird*.md2) - it only references the
///     model filename, not per-mesh textures. Sibling "textures" folders next to these models
///     (e.g. JF_Rbody.wct/JF_Rhead4.wct/JF_Rwing.wct next to Bird*.md2) strongly suggest the
///     binding is driven by the .RSE ride-script format instead (itself only partially
///     implemented in this project) - a separate investigation from MD2 parsing itself.
///
/// Good sample files for continuing this (small, meshCnt=1, easy to reason about by hand):
///   /levels/jungle/rides/wateride/wr_ringM.md2 (256 bytes, 1 mesh, minimal)
///   /levels/jungle/rides/tourride/BirdC.MD2 (832 bytes, 1 mesh, 11 verts - the file most of
///     the above was derived from)
/// </summary>
public partial class ModelFile : BaseFormat
{
	public List<Mesh> Meshes { get; private set; }

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
		public Matrix4x4 TransformMatrix { get; set; }
		public MaterialData[] Materials { get; set; }

		public Vector3[] Normals { get; set; }
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
		public uint Flags;
	}

	protected override void ReadFromStream( Stream stream )
	{
		using ( BinaryReader reader = new BinaryReader( stream, Encoding.ASCII, true ) )
		{
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

			reader.BaseStream.Seek( 0x44, SeekOrigin.Begin );
			ushort meshCnt = reader.ReadUInt16();

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
				reader.BaseStream.Seek( 32, SeekOrigin.Current ); // Skip _idk2 to _37
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
					Materials = materials.ToArray()
				} );
			}

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
