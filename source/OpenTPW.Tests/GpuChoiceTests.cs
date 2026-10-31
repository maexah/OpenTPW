using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
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
	/// <b>The Options row's Secondary is the card after the best one</b> (Q983): Primary the discrete card, Secondary the
	/// built-in one; with three, the second best; with one there is no other; a named card wins over the row; an unmet
	/// setting falls back to the row's pick and still says it was not met.
	/// </summary>
	/// <remarks><b>Mutation:</b> Secondary taking the worst card or the loader's second device, ignored with one card, or
	/// winning over a named card, fails a step.</remarks>
	[TestMethod]
	public void SecondaryIsTheCardAfterThePreferredOne()
	{
		var all = new[] { Amd, Nvidia, Software };

		Assert.AreEqual( 1, GpuChoice.Pick( all, null, secondary: false ).Index, "Primary: the discrete card" );
		Assert.AreEqual( 0, GpuChoice.Pick( all, null, secondary: true ).Index, "Secondary: the built-in one, not the software renderer" );
		Assert.AreEqual( 0, GpuChoice.Pick( new[] { Amd, Software }, "" ).Index, "two cards, Primary is the built-in one" );
		Assert.AreEqual( 2, GpuChoice.Pick( new[] { Amd, Software }, "auto", secondary: true ).Index, "two cards: the other one" );
		Assert.AreEqual( 0, GpuChoice.Pick( new[] { Amd }, null, secondary: true ).Index, "one card: nothing after it" );
		Assert.AreEqual( 1, GpuChoice.Pick( all, "gtx", secondary: true ).Index, "a named card wins over the row" );
		Assert.AreEqual( 2, GpuChoice.Pick( all, "#2", secondary: true ).Index );

		var (index, why, met) = GpuChoice.Pick( all, "voodoo", secondary: true );
		Assert.AreEqual( 0, index, "an unmet setting takes the row's pick" );
		Assert.IsFalse( met );
		StringAssert.Contains( why, "matches no device name" );
		StringAssert.Contains( why, "Secondary" );

		CollectionAssert.AreEqual( new[] { 1, 0, 2 }, GpuChoice.Ranked( all ).Select( d => d.Index ).ToList() );
		Assert.IsTrue( GpuChoice.Names( "gtx" ) );
		Assert.IsTrue( GpuChoice.Names( "#1" ) );
		Assert.IsFalse( GpuChoice.Names( " AUTO " ) );
		Assert.IsFalse( GpuChoice.Names( null ) );
		Assert.IsFalse( GpuChoice.Names( "  " ) );
	}

	/// <summary>
	/// <b>The Video Card row</b> (Q983, 0x004a361c): a click takes Secondary back to Primary always and Primary to Secondary only
	/// with more than one card; the row reads the saved choice where it can be had and otherwise the card drawn on.
	/// </summary>
	/// <remarks><b>Mutation:</b> a click that ignores the count, one that cannot go back, a row that follows the choice
	/// where the card drawn has the other role, or that ignores a named card, a <c>Kept</c> that does not clear with one card, fails a step.</remarks>
	[TestMethod]
	public void TheVideoCardRowClicksAndReadsAsTheOriginalsDoes()
	{
		Assert.IsTrue( GpuChoice.Clicked( false, 2 ), "Primary to Secondary where there is another card" );
		Assert.IsTrue( GpuChoice.Clicked( false, 4 ) );
		Assert.IsFalse( GpuChoice.Clicked( false, 1 ), "with one card there is no Secondary" );
		Assert.IsFalse( GpuChoice.Clicked( false, 0 ), "with none listed neither" );
		Assert.IsFalse( GpuChoice.Clicked( true, 2 ), "Secondary back to Primary" );
		Assert.IsFalse( GpuChoice.Clicked( true, 1 ), "back to Primary even with one card, as a Secondary saved elsewhere is cleared" );

		// Shows( secondary now, exact, drawsOnPrimary, asked at the start )
		Assert.IsTrue( GpuChoice.Shows( true, false, false, true ), "Secondary asked and drawn" );
		Assert.IsFalse( GpuChoice.Shows( false, false, true, false ), "Primary asked and drawn" );
		Assert.IsTrue( GpuChoice.Shows( true, false, true, false ), "a click just made reads as made, drawn on from the next start" );
		Assert.IsFalse( GpuChoice.Shows( true, false, true, true ), "Secondary asked but Primary drawn (the card could not be used) reads Primary" );
		Assert.IsTrue( GpuChoice.Shows( false, false, false, false ), "Primary asked but Secondary drawn reads Secondary" );
		Assert.IsFalse( GpuChoice.Shows( false, false, true, true ), "Secondary asked, Primary drawn, clicked to Primary: still the card drawn" );
		Assert.IsTrue( GpuChoice.Shows( true, false, false, false ), "Primary asked, Secondary drawn, clicked to Secondary: still the card drawn" );
		Assert.IsTrue( GpuChoice.Shows( false, true, false, false ), "a named card that is not the preferred one reads Secondary" );
		Assert.IsFalse( GpuChoice.Shows( true, true, true, true ), "a named card that is the preferred one reads Primary" );
		Assert.IsFalse( GpuChoice.Shows( true, true, true, false ), "a click does not move a named card" );

		Assert.IsTrue( GpuChoice.Kept( true, 2 ), "a saved Secondary stays where there is another card" );
		Assert.IsFalse( GpuChoice.Kept( true, 1 ), "one card: cleared (0x0044e334)" );
		Assert.IsFalse( GpuChoice.Kept( true, 0 ), "none listed: cleared" );
		Assert.IsFalse( GpuChoice.Kept( false, 2 ), "Primary stays Primary" );
	}

	/// <summary>
	/// <b>A choice that cannot be used is taken back</b> (Q980): the loader variables the choice left set are removed and
	/// the start goes on. A variable the choice did not set is not touched.
	/// </summary>
	/// <remarks><b>Mutation:</b> a Withdraw that removes nothing, or one that removes a variable of the person's, fails a step.</remarks>
	[TestMethod]
	public void AChoiceThatCannotBeUsedIsTakenBack()
	{
		const string theirs = "OPENTPW_TEST_THEIRS";
		const string ours = "OPENTPW_TEST_OURS";

		try
		{
			Log = new();
			Environment.SetEnvironmentVariable( theirs, "keep" );
			GpuSelector.Set( ours, "x" );
			GpuSelector.Track( ours );
			Assert.IsTrue( GpuSelector.IsSet );
			Assert.AreEqual( "x", GpuSelector.Get( ours ), "set where the loader looks" );

			GpuSelector.Withdraw( new InvalidOperationException( "cannot present" ) );

			Assert.IsFalse( GpuSelector.IsSet );
			Assert.IsNull( GpuSelector.Get( ours ), "taken back where the loader looks" );
			Assert.AreEqual( "keep", Environment.GetEnvironmentVariable( theirs ) );
		}
		finally
		{
			Environment.SetEnvironmentVariable( theirs, null );
			GpuSelector.Set( ours, null );
		}
	}

	/// <summary>
	/// <b>The tick asks RESTART GAME for a changed video card</b> (Q985, 0x004237d4): a card different from the one the screen
	/// opened with, either way round; nothing else the screen keeps is asked for.
	/// </summary>
	/// <remarks><b>Mutation:</b> a rule that asks only for Secondary, that never asks, or that asks for any change, fails a step.</remarks>
	[TestMethod]
	public void TheTickAsksToRestartOnlyForAChangedVideoCard()
	{
		var before = new GameOptions { SecondaryVideoCard = false };
		var now = before.Copy();
		Assert.IsFalse( UI.OptionsScreen.NeedsRestart( before, now ), "nothing changed" );

		now.SecondaryVideoCard = true;
		Assert.IsTrue( UI.OptionsScreen.NeedsRestart( before, now ), "Primary to Secondary" );
		Assert.IsTrue( UI.OptionsScreen.NeedsRestart( now, before ), "Secondary back to Primary is a change from how the screen opened" );

		var other = before.Copy();
		other.GraphicsQuality = (other.GraphicsQuality + 1) % 3;
		other.Advisor = !other.Advisor;
		other.DisplayMode = DisplayMode.BorderlessFullScreen;
		Assert.IsFalse( UI.OptionsScreen.NeedsRestart( before, other ), "other options are kept without asking" );
	}

	/// <summary>
	/// <b>RESTART GAME is not asked where the options were not written</b> (Q986): the card was not kept, and the box would hold the
	/// screen modal over OPTIONS NOT SAVED, which the tick would then leave the game with untold.
	/// </summary>
	/// <remarks><b>Mutation:</b> a rule that ignores the lost write, or one that never asks, fails a step.</remarks>
	[TestMethod]
	public void RestartGameIsNotAskedWhereTheOptionsWereNotWritten()
	{
		var before = new GameOptions { SecondaryVideoCard = false };
		var now = before.Copy();
		now.SecondaryVideoCard = true;

		Assert.IsTrue( UI.OptionsScreen.AsksToRestart( before, now, optionsLost: false ), "a changed card, written: asked" );
		Assert.IsFalse( UI.OptionsScreen.AsksToRestart( before, now, optionsLost: true ), "a changed card, not written: told as OPTIONS NOT SAVED instead" );
		Assert.IsFalse( UI.OptionsScreen.AsksToRestart( before, before.Copy(), optionsLost: false ), "no change: not asked" );
	}

	/// <summary>
	/// <b>The cross puts back the display the tick put into effect</b> (Q986): after RESTART GAME's cross the screen is still open with
	/// the window already changed, so the screen's cross must change it back; where the tick changed no display there is nothing to undo.
	/// </summary>
	/// <remarks><b>Mutation:</b> a rule that compares only the mode, only the size, or never undoes, fails a step.</remarks>
	[TestMethod]
	public void TheCrossUndoesOnlyADisplayTheTickPutIntoEffect()
	{
		var before = new GameOptions { DisplayMode = DisplayMode.Windowed, FullScreenSize = new VideoMode( 1280, 720, 60 ) };

		Assert.IsFalse( UI.OptionsScreen.DisplayToUndo( null, before ), "the tick applied nothing" );
		Assert.IsFalse( UI.OptionsScreen.DisplayToUndo( (before.DisplayMode, before.FullScreenSize), before ), "applied the display the screen opened with" );
		Assert.IsTrue( UI.OptionsScreen.DisplayToUndo( (DisplayMode.BorderlessFullScreen, before.FullScreenSize), before ), "another mode" );
		Assert.IsTrue( UI.OptionsScreen.DisplayToUndo( (before.DisplayMode, new VideoMode( 1920, 1080, 60 )), before ), "another size" );
	}

	/// <summary>
	/// <b>The fallback itself</b> (Q980, Q985): a device that cannot be made while the choice's settings are in force is made again
	/// once after they are taken back; with none in force the failure is the caller's, and a second failure is too.
	/// </summary>
	/// <remarks><b>Mutation:</b> a <c>Make</c> that does not catch, one that catches with nothing set, one that does not take the
	/// settings back, or one that tries a third time, fails a step.</remarks>
	[TestMethod]
	public void ADeviceThatCannotBeMadeIsMadeAgainWithTheSettingsTakenBack()
	{
		const string ours = "OPENTPW_TEST_MAKE";

		try
		{
			Log = new();
			GpuSelector.Set( ours, "x" );
			GpuSelector.Track( ours );

			var calls = 0;
			var seen = new List<string?>();
			var made = GpuSelector.Make( () =>
			{
				seen.Add( GpuSelector.Get( ours ) );
				return ++calls == 1 ? throw new InvalidOperationException( "cannot present" ) : "device";
			} );

			Assert.AreEqual( "device", made );
			Assert.AreEqual( 2, calls );
			CollectionAssert.AreEqual( new string?[] { "x", null }, seen, "the second try runs with the setting gone" );
			Assert.IsFalse( GpuSelector.IsSet );

			calls = 0;
			Assert.ThrowsException<InvalidOperationException>( () => GpuSelector.Make<string>( () => { ++calls; throw new InvalidOperationException( "no setting" ); } ) );
			Assert.AreEqual( 1, calls, "with nothing set the failure is not retried" );

			GpuSelector.Set( ours, "x" );
			GpuSelector.Track( ours );
			calls = 0;
			Assert.ThrowsException<InvalidOperationException>( () => GpuSelector.Make<string>( () => { ++calls; throw new InvalidOperationException( "still" ); } ) );
			Assert.AreEqual( 2, calls, "the retry is once" );
		}
		finally
		{
			GpuSelector.Set( ours, null );
		}
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

		var amd = GpuChoice.Levers( Amd );
		Assert.AreEqual( ("MESA_VK_DEVICE_SELECT", "1002:1638"), amd[0] );
		foreach ( var part in new[] { "radeon_icd", "amd_icd", "amd-vulkan", "amdvlk" } )
			StringAssert.Contains( amd[1].Value, $"*{part}*", "AMD driver row" );

		var intel = GpuChoice.Levers( new GpuInfo( 0, "Intel(R) UHD Graphics", 0x8086, 0x9a49, GpuKind.Integrated ) );
		Assert.AreEqual( "8086:9a49", intel[0].Value );
		foreach ( var part in new[] { "intel_icd", "igvk" } )
			StringAssert.Contains( intel[1].Value, $"*{part}*", "Intel driver row" );

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
			SaveFolder.SaveDisplay( tell: true );
			var text = File.ReadAllText( file );
			StringAssert.Contains( text, "\ngpu NVIDIA GeForce GTX 1650\n" );
			StringAssert.Contains( text, "# gpu auto:" );

			GameOptions.Current = new();
			SaveFolder.LoadDisplay();
			Assert.AreEqual( "NVIDIA GeForce GTX 1650", GameOptions.Current.Gpu, "written, read back" );

			GameOptions.Current = new() { Gpu = "  " };
			SaveFolder.SaveDisplay( tell: true );
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
