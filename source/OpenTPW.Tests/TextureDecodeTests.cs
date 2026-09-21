using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// Bullfrog's .wct decoder, over the art the game actually ships. These read real game files and are
/// skipped where there is no installation - see <see cref="GameData"/>.
///
/// <para>
/// Written because the decode's two scratch buffers were resized from <c>size*size*size</c> floats to
/// the indices the decode really reaches, and nothing anywhere pinned that. The failure mode is not a
/// quiet one: a buffer an element short throws out of the list indexer, so decoding every texture a
/// model names is enough to catch it. That resize is the whole of the risk the load-time work
/// carried, and this is the only cover it has.
/// </para>
///
/// <para>
/// <b>This is regression cover, not the proof.</b> What proves the decode right is the park on the
/// screen; all four themes were loaded and looked at. A green run here only says nothing threw.
/// </para>
///
/// <para>
/// <b>Mutation-checked, including one mutation that is EXPECTED TO SURVIVE.</b> Undersizing either
/// buffer on a path shipped art reaches fails this test: <c>DequantizationBuffer</c> at
/// <c>size*size/2</c> fails, and <c>RowDecodeBuffer</c> at <c>size*size/2</c> fails. But dropping the
/// row buffer's extra <c>size*size/2</c> - the headroom the alpha path needs - <b>passes</b>, and that
/// is correct rather than a hole: <c>wOffset</c> is <c>size*size</c> only when a texture is alpha
/// <i>and</i> half-scale, and <b>of the 656 .wct files in the four themes' terrain and shared
/// directories, 0 are half-scale</b> (211 carry alpha, all of them full-scale). That headroom is dead
/// by CONTENT, not by code, so no test written against shipped data can pin it. It stays because it
/// is correct for a texture the decoder supports and the game happens never to ship.
/// </para>
/// </summary>
[TestClass]
public class TextureDecodeTests
{
	private BaseFileSystem data = null!;

	[TestInitialize]
	public void MountTheGame() => data = GameData.Required();

	/// <summary>
	/// Every distinct texture a model's materials name, as paths beside the model.
	///
	/// Taken from the model rather than written down here, so the set cannot drift away from what is
	/// really loaded - this is the same <c>{directory}/{name}.wct</c> rule
	/// <see cref="LobbyModel.LoadTexture"/> follows, names untrimmed exactly as it leaves them.
	/// </summary>
	private List<string> TexturesNamedBy( string modelPath, string textureDirectory )
	{
		var model = new ModelFile( new MemoryStream( data.ReadAllBytes( modelPath ) ) );

		return model.Meshes
			.SelectMany( mesh => mesh.Materials )
			.Select( material => material.Name )
			.Where( name => !string.IsNullOrWhiteSpace( name ) )
			.Distinct()
			.Select( name => $"{textureDirectory}/{name}.wct" )
			.Where( Exists )
			.ToList();
	}

	/// <summary>
	/// Whether the file system can offer this path at all.
	///
	/// <b>Asked with <c>GetSize</c> rather than <c>FileExists</c>, because that one does not look
	/// inside archives</b> and every one of these lives in a .wad - so <c>FileExists</c> answers no to
	/// all of them and the set comes back empty. This is the same helper, for the same reason, as
	/// <see cref="LobbyModel"/>'s own <c>Exists</c>.
	/// </summary>
	private bool Exists( string path )
	{
		try
		{
			return data.GetSize( path ) > 0;
		}
		catch ( System.Exception )
		{
			return false;
		}
	}

	/// <summary>
	/// Every texture the jungle's terrain names decodes, to the size its own header declares.
	///
	/// <para>
	/// <c>base.MD2</c> is the right model to do this with: at 272 meshes it is the largest in the
	/// game, it names more distinct textures than anything else, and decoding its textures once per
	/// mesh that used them is what made a park load take twenty seconds. Nothing is asserted about
	/// the PIXELS - what this pins is that every shipped texture survives the decode at all, and that
	/// the decode agrees with the header about how big it is.
	/// </para>
	/// </summary>
	[TestMethod]
	public void EveryTextureTheJungleTerrainNamesDecodes()
	{
		var wanted = TexturesNamedBy( "levels/jungle/terrain/base.MD2", "levels/jungle/terrain/textures" );

		Assert.IsTrue( wanted.Count >= 40,
			$"the jungle terrain should name dozens of textures; found {wanted.Count}" );

		foreach ( var path in wanted )
		{
			var texture = new TextureFile( new MemoryStream( data.ReadAllBytes( path ) ) );

			Assert.IsTrue( texture.IsValid, $"{path} did not decode" );

			Assert.IsTrue( texture.Data.Width > 0 && texture.Data.Height > 0,
				$"{path} decoded to {texture.Data.Width}x{texture.Data.Height}" );

			// Four bytes a texel, every one of them written. A buffer sized short of what the decode
			// indexes throws long before it reaches this line.
			Assert.AreEqual( texture.Data.Width * texture.Data.Height * 4, texture.Data.Data.Length,
				$"{path} decoded to the wrong number of bytes" );
		}
	}
}
