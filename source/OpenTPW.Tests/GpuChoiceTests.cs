using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// Which graphics card the game draws with (Q980): <see cref="GpuChoice"/>'s pick, the loader settings that put it first,
/// and the <c>gpu</c> line of opentpw.cfg. What the machine's loader really does with a setting is
/// <see cref="GpuSelector"/>'s, seen in a running game's log, not here.
/// </summary>
[TestClass]
public class GpuChoiceTests
{
	// The PC Q980 was found on, in the loader's order: the built-in GPU first, the GTX 1650 second.
	private static readonly GpuInfo Amd = new( 0, "AMD Radeon(TM) Graphics", 0x1002, 0x1638, GpuKind.Integrated );
	private static readonly GpuInfo Nvidia = new( 1, "NVIDIA GeForce GTX 1650", 0x10DE, 0x1f91, GpuKind.Discrete );
	private static readonly GpuInfo Software = new( 2, "llvmpipe (LLVM 20.1.2, 256 bits)", 0x10005, 0, GpuKind.Cpu );

	/// <summary>
	/// <b>Auto takes a discrete card over the built-in one the loader lists first</b>, a built-in one over a software
	/// renderer, and the loader's own order between two of a kind.
	/// </summary>
	/// <remarks><b>Mutation:</b> taking the first device, the last, the built-in before the discrete, or the later of two
	/// discrete cards, fails a step.</remarks>
	[TestMethod]
	public void AutoPrefersTheDiscreteCard()
	{
		foreach ( var setting in new string?[] { null, "", "  ", "auto", "AUTO" } )
			Assert.AreEqual( 1, GpuChoice.Pick( new[] { Amd, Nvidia }, setting ).Index, $"\"{setting}\"" );

		Assert.AreEqual( 0, GpuChoice.Pick( new[] { Nvidia with { Index = 0 }, Amd with { Index = 1 } }, null ).Index, "already first" );
		Assert.AreEqual( 1, GpuChoice.Pick( new[] { Software with { Index = 0 }, Amd with { Index = 1 } }, null ).Index, "built-in over software" );
		Assert.AreEqual( 0, GpuChoice.Pick( new[] { Software with { Index = 0 } }, null ).Index, "software alone" );

		var second = Nvidia with { Index = 1, Name = "NVIDIA GeForce RTX 2060" };
		var first = Nvidia with { Index = 0 };
		Assert.AreEqual( 0, GpuChoice.Pick( new[] { first, second, Amd with { Index = 2 } }, null ).Index, "two discrete cards: the loader's first" );

		var picked = GpuChoice.Pick( new[] { Amd, Nvidia }, null );
		Assert.AreEqual( "a discrete card is preferred", picked.Why );
		Assert.IsTrue( picked.Met, "auto asks for nothing it can miss" );
		Assert.AreEqual( "no discrete card, so the first integrated one", GpuChoice.Pick( new[] { Amd, Software with { Index = 1 } }, null ).Why );
	}

	/// <summary>
	/// <b>A name asks for the first device that holds it, ignoring case; <c>#N</c> asks for a place in the logged list</b>;
	/// a setting nothing matches falls back to the automatic pick and says why.
	/// </summary>
	/// <remarks><b>Mutation:</b> a case-sensitive match, <c>#N</c> counted from one, a bad setting taking the first device,
	/// or no reason naming the setting, fails a step.</remarks>
	[TestMethod]
	public void ASettingChoosesByNameOrPlace()
	{
		var all = new[] { Amd, Nvidia, Software };

		Assert.AreEqual( 0, GpuChoice.Pick( all, "radeon" ).Index, "a name, case folded" );
		Assert.AreEqual( 1, GpuChoice.Pick( all, "gtx 1650" ).Index );
		Assert.AreEqual( 1, GpuChoice.Pick( all, "  1650  " ).Index, "trimmed" );
		Assert.AreEqual( 2, GpuChoice.Pick( all, "LLVMPIPE" ).Index );
		Assert.AreEqual( 0, GpuChoice.Pick( all, "e" ).Index, "the first of several that hold it" );

		Assert.AreEqual( 0, GpuChoice.Pick( all, "#0" ).Index );
		Assert.AreEqual( 2, GpuChoice.Pick( all, "#2" ).Index );
		StringAssert.Contains( GpuChoice.Pick( all, "#2" ).Why, "\"gpu #2\"" );
		Assert.IsTrue( GpuChoice.Pick( all, "#2" ).Met );
		Assert.IsTrue( GpuChoice.Pick( all, "radeon" ).Met );

		// Nothing to match: the automatic pick (the discrete card), and the reason says the setting was not met.
		foreach ( var (ask, said) in new[] { ("#3", "names no device (there are 3)"), ("#-1", "names no device"), ("#x", "matches no device name"), ("voodoo", "matches no device name") } )
		{
			var (index, why, met) = GpuChoice.Pick( all, ask );
			Assert.AreEqual( 1, index, ask );
			Assert.IsFalse( met, ask );
			StringAssert.Contains( why, $"\"gpu {ask}\"" );
			StringAssert.Contains( why, said, ask );
			StringAssert.EndsWith( why, "a discrete card is preferred", ask );
		}

		Assert.ThrowsException<System.ArgumentException>( () => GpuChoice.Pick( new List<GpuInfo>(), null ) );
	}

	/// <summary>
	/// <b>The log's words and the loader settings for a device</b>: the list line, and Mesa's select by the hex
	/// vendor and device ids (four digits, as Mesa reads them), then the driver filter for the vendors it knows.
	/// </summary>
	/// <remarks><b>Mutation:</b> ids in decimal or padded wrong, the software renderer's vendor not named, or the driver
	/// filter given for an unknown vendor, fails a step.</remarks>
	[TestMethod]
	public void TheLoaderIsAskedByIdThenByDriver()
	{
		Assert.AreEqual( "[1] NVIDIA GeForce GTX 1650 (NVIDIA, discrete)", GpuChoice.Describe( Nvidia ) );
		Assert.AreEqual( "[0] AMD Radeon(TM) Graphics (AMD, integrated)", GpuChoice.Describe( Amd ) );
		Assert.AreEqual( "[2] llvmpipe (LLVM 20.1.2, 256 bits) (Mesa software renderer, software)", GpuChoice.Describe( Software ) );
		Assert.AreEqual( "vendor 0x1234", GpuChoice.VendorName( 0x1234 ) );

		var levers = GpuChoice.Levers( Nvidia );
		Assert.AreEqual( ("MESA_VK_DEVICE_SELECT", "10de:1f91"), levers[0] );
		Assert.AreEqual( "VK_LOADER_DRIVERS_SELECT", levers[1].Name );
		StringAssert.Contains( levers[1].Value, "nvidia_icd" );
		StringAssert.Contains( levers[1].Value, "nv-vk" );
		Assert.AreEqual( 2, levers.Count );

		Assert.AreEqual( "10005:0000", GpuChoice.Levers( Software )[0].Value );
		Assert.AreEqual( "*lvp_icd*", GpuChoice.Levers( Software )[1].Value );
		Assert.AreEqual( "0000:0009", GpuChoice.Levers( new GpuInfo( 0, "x", 0, 9, GpuKind.Other ) )[0].Value, "padded to four digits" );
		Assert.AreEqual( 1, GpuChoice.Levers( new GpuInfo( 0, "x", 0x1234, 9, GpuKind.Other ) ).Count, "no driver names for a vendor not known" );
	}

	/// <summary>
	/// <b>opentpw.cfg's <c>gpu</c> line</b>: <c>auto</c> by default, the rest of the line as written (a name with spaces
	/// whole), written back under its comment and read back the same; a line with nothing after it sets nothing.
	/// </summary>
	/// <remarks><b>Mutation:</b> only the first word read, the line not written, or an empty setting written, fails a step.</remarks>
	[TestMethod]
	public void TheCardIsKeptInOpenTpwCfg()
	{
		var folder = Path.Combine( Path.GetTempPath(), $"opentpw-gpu-{System.Guid.NewGuid():N}" );
		Directory.CreateDirectory( folder );
		var was = GameOptions.Current;
		var wasSaves = SaveFileSystemOrNull;

		try
		{
			Log = new();
			SaveFileSystem = new BaseFileSystem( folder );
			var file = Path.Combine( folder, "opentpw.cfg" );

			Assert.AreEqual( "auto", new GameOptions().Gpu, "the default" );

			GameOptions.Current = new();
			File.WriteAllText( file, "display windowed\ngpu GTX 1650\n" );
			SaveFolder.LoadDisplay();
			Assert.AreEqual( "GTX 1650", GameOptions.Current.Gpu, "the rest of the line" );

			GameOptions.Current = new();
			File.WriteAllText( file, "gpu\ngpu #1\n" );
			SaveFolder.LoadDisplay();
			Assert.AreEqual( "#1", GameOptions.Current.Gpu, "a line with nothing after it is passed over" );

			GameOptions.Current = new() { Gpu = "NVIDIA GeForce GTX 1650" };
			SaveFolder.SaveDisplay();
			var text = File.ReadAllText( file );
			StringAssert.Contains( text, "\ngpu NVIDIA GeForce GTX 1650\n" );
			StringAssert.Contains( text, "# gpu auto:" );

			GameOptions.Current = new();
			SaveFolder.LoadDisplay();
			Assert.AreEqual( "NVIDIA GeForce GTX 1650", GameOptions.Current.Gpu, "written, read back" );

			GameOptions.Current = new() { Gpu = "  " };
			SaveFolder.SaveDisplay();
			StringAssert.Contains( File.ReadAllText( file ), "\ngpu auto\n", "an empty setting is written as auto" );
			Assert.AreEqual( 1, File.ReadAllLines( file ).Count( l => l.StartsWith( "gpu ", System.StringComparison.Ordinal ) ), "one gpu line" );
		}
		finally
		{
			GameOptions.Current = was;
			SaveFileSystemOrNull = wasSaves;
			Directory.Delete( folder, true );
		}
	}
}
