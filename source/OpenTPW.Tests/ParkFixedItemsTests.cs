using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Globalization;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// A park's fixed items - the gate and the traffic lights - and where they stand. These read real
/// game files and are skipped where there is no installation: see <see cref="GameData"/>.
///
/// <para>
/// What makes these worth a test rather than a comment is that the positions are invisible in two
/// different ways. They are not in the save, which holds the sentinel 128/128 for all three fixed
/// items; and they are not in the models' bounding boxes either, which are node-local and describe
/// only how big each mesh is. They live in the node transforms, and the check that they are right is
/// that they agree with cells a completely separate file declares - <c>Standard.sam</c>.
/// </para>
/// </summary>
[TestClass]
public class ParkFixedItemsTests
{
	private BaseFileSystem data = null!;

	[TestInitialize]
	public void MountTheGame() => data = GameData.Required();

	/// <summary>
	/// Read through the file system this test mounted rather than the global one, which belongs to a
	/// running game - the same reason the other park tests do it this way.
	/// </summary>
	private T Read<T>( string path, Func<Stream, T> make )
	{
		using var stream = new MemoryStream( data.ReadAllBytes( path ) );
		return make( stream );
	}

	private ModelFile Model( string path ) => Read( path, stream => new ModelFile( stream ) );

	private SettingsFile Balance() => Read( "levels/jungle/Standard.sam", stream => new SettingsFile( stream ) );

	/// <summary>By name, so the test says which mesh it means - the names were authored by hand and
	/// a few carry a trailing space or terminator.</summary>
	private static ModelFile.Mesh MeshNamed( ModelFile model, string name )
	{
		var mesh = model.Meshes.FirstOrDefault(
			candidate => string.Equals( candidate.Name.TrimEnd( '\0' ).Trim(), name, StringComparison.OrdinalIgnoreCase ) );

		Assert.IsNotNull( mesh,
			$"no mesh called '{name}' among: {string.Join( ", ", model.Meshes.Select( m => $"'{m.Name.TrimEnd( '\0' ).Trim()}'" ) )}" );

		return mesh;
	}

	private static int Cell( SettingsFile balance, string key )
	{
		var value = balance[key];

		Assert.IsNotNull( value, $"{key} is missing from the balance file" );
		Assert.IsTrue( int.TryParse( value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var cell ),
			$"{key} is '{value}', which is not a cell" );

		return cell;
	}

	/// <summary>
	/// The gate spans the entrance rather than sitting on one cell of it. Its two door meshes stand at
	/// the outer edges of the two cells Standard.sam calls EntranceA and EntranceB, so the left door is
	/// at the near edge of the first and the right door at the far edge of the second - which is the
	/// check that the model's node transforms really are in world coordinates, since nothing in the
	/// model knows what Standard.sam says.
	/// </summary>
	[TestMethod]
	public void TheGateStandsOverTheTwoEntranceCells()
	{
		var balance = Balance();
		var model = Model( "levels/jungle/features/gates/gates.MD2" );

		var entranceA = Cell( balance, "FixedItemInfo.EntranceAPosX" );
		var entranceB = Cell( balance, "FixedItemInfo.EntranceBPosX" );

		Assert.AreEqual( entranceA + 1, entranceB, "the two entrance cells should be side by side" );

		// A cell is 10 units, so a cell boundary is at ten times the cell number.
		Assert.AreEqual( entranceA * 10f, MeshNamed( model, "door01" ).WorldTransform.Translation.X, 1f,
			"the left door stands at the near edge of EntranceA" );

		Assert.AreEqual( (entranceB + 1) * 10f, MeshNamed( model, "door02" ).WorldTransform.Translation.X, 1f,
			"the right door stands at the far edge of EntranceB" );
	}

	/// <summary>
	/// One traffic light on each corner of the two crossings, which is four lights on the four cells
	/// Standard.sam names. Each light sits at a cell's outer corner rather than its centre, so the cell
	/// is the floor of the world position - and all four together must cover exactly the declared set.
	/// </summary>
	[TestMethod]
	public void TheTrafficLightsStandOnTheFourCrossingCells()
	{
		var balance = Balance();
		var model = Model( "levels/jungle/features/lights/lights.MD2" );

		var declared = new[] { "CrossingParkSideA", "CrossingParkSideB", "CrossingBSSideA", "CrossingBSSideB" }
			.Select( item => (X: Cell( balance, $"FixedItemInfo.{item}PosX" ), Y: Cell( balance, $"FixedItemInfo.{item}PosY" )) )
			.OrderBy( cell => cell.X ).ThenBy( cell => cell.Y )
			.ToArray();

		var lights = new[] { "tl01", "tl02", "tl03", "tl04" }
			.Select( name => MeshNamed( model, name ).WorldTransform.Translation )
			.Select( position => (X: (int)MathF.Floor( position.X / 10f ), Y: (int)MathF.Floor( position.Z / 10f )) )
			.OrderBy( cell => cell.X ).ThenBy( cell => cell.Y )
			.ToArray();

		CollectionAssert.AreEqual( declared, lights,
			$"the lights stand on {string.Join( ", ", lights )} and the crossings are {string.Join( ", ", declared )}" );
	}

	/// <summary>
	/// An item's <c>.hmp</c> gives the footprint it occupies, and its size alone is enough to read it:
	/// a 48-byte header and 27 bytes per cell. Every one of the jungle's items fits that, and the ones
	/// whose names state their own size are the check that the arithmetic means what it appears to -
	/// 4x4rock really does come out at sixteen cells.
	///
	/// <para>
	/// The gate is the one that matters here: eighteen cells, which is the 6x3 its Gates.sam declares
	/// with EngineFootprintWidthOverride and EngineFootprintHeightOverride, agreed on by a file that
	/// carries no such text.
	/// </para>
	/// </summary>
	[TestMethod]
	public void AnItemsFootprintFileGivesTheCellsItOccupies()
	{
		var expected = new (string Item, int Cells)[]
		{
			("features/1x1east", 1),
			("features/2x1log", 2),
			("features/2x2rck", 4),
			("features/4x4rock", 16),
			("features/5x5rck", 25),
			("features/gates", 6 * 3),
			("features/lights", 1)
		};

		foreach ( var (item, cells) in expected )
		{
			var name = item[(item.LastIndexOf( '/' ) + 1)..];
			var size = data.ReadAllBytes( $"levels/jungle/{item}/{name}.hmp" ).Length;

			Assert.AreEqual( 0, (size - 48) % 27,
				$"{name}.hmp is {size} bytes, which is not a 48-byte header and whole cells of 27" );

			Assert.AreEqual( cells, (size - 48) / 27, $"{name} occupies" );
		}
	}

	/// <summary>The park this theme ships, walked the same way every other park test walks it.</summary>
	private ParkWorld World()
	{
		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );

		return new ParkWorld( new SaveReader( stream ).ReadFile() );
	}

	/// <summary>Whether the file system can offer this at all, treating "not there" and "will not open" alike.</summary>
	private bool Has( string path )
	{
		try
		{
			using var stream = data.OpenRead( path );

			return stream != null;
		}
		catch ( Exception )
		{
			return false;
		}
	}

	/// <summary>
	/// One fixed item's script, holding its own archive's animation roles exactly as <see cref="ParkRides"/>
	/// hands a placed thing its own. No model and no graphics device are needed for this: a role is pure
	/// data, which is what makes the two tests below possible at all.
	/// </summary>
	private RideScript Bound( string stem ) => BoundIn( "jungle", stem );

	/// <summary>The same for a named theme, because all four ship their own gate script and they differ.</summary>
	private RideScript BoundIn( string theme, string stem )
	{
		var directory = $"levels/{theme}/features/{stem}";

		using var stream = new MemoryStream( data.ReadAllBytes( $"{directory}/{stem}.RSE" ) );

		var file = new RideScriptFile( stream );

		Assert.IsTrue( file.IsValid, $"{stem}.RSE did not read" );

		return new RideScript( file )
		{
			Ride = new RideState(),
			Effects = new RideEffects(),
			Animations = RideAnimations.Load( directory, stem, data )
		};
	}

	/// <summary>
	/// The gate and the traffic lights are things in their own right - the save's header names which thing
	/// each one is - and both ship a script for that thing to run.
	///
	/// <para>
	/// This is the whole basis for binding them. They were passed over for a long time because they carry no
	/// position, which made them look like data the park had no use for; what they actually are is catalogue
	/// objects whose places are baked into their models. The header handles are thing ids rather than
	/// catalogue numbers, and the two are easy to confuse because both are small integers.
	/// </para>
	/// </summary>
	[TestMethod]
	public void TheGateAndTheLightsAreThingsOfTheirOwnWithScriptsToRun()
	{
		var world = World();

		foreach ( var (handle, stem, catalogueId) in new[]
		{
			(world.ParkGates, "gates", 1601),
			(world.TrafficLights, "lights", 1603)
		} )
		{
			Assert.AreNotEqual( 0, handle, $"the save's header does not name the {stem} at all" );

			var named = world.Objects.Where( thing => thing.ThingId == handle ).ToArray();

			Assert.AreEqual( 1, named.Length, $"the header's {stem} handle should name exactly one thing" );
			Assert.AreEqual( catalogueId, named[0].CatalogueId, $"the catalogue number of '{stem}'" );

			Assert.IsFalse( named[0].IsPlaced,
				$"'{stem}' carries a position, so it is not the fixed item this test thinks it is" );

			Assert.IsTrue( Has( $"levels/jungle/features/{stem}/{stem}.RSE" ), $"'{stem}' should ship a script" );
		}
	}

	/// <summary>
	/// The park gate holds still, and holds still <b>because its own script says so</b> rather than because
	/// nothing is driving it.
	///
	/// <para>
	/// <c>Gates.RSE</c> opens on a dispatch loop that reads <c>VAR_COMMAND</c>, and every variable starts at
	/// nought - so it cycles five instructions for ever and reaches neither the open branch's
	/// <c>WAITANIM</c> nor the close branch's <c>TRIGANIM</c>. In the original the only thing that ever
	/// writes that variable is opening or closing the park, which this program has no concept of yet.
	/// </para>
	/// <para>
	/// The anti-vacuity check matters more than the assertion it guards: an idle channel would also be what
	/// a gate with no clips at all looked like, so the clips its script names are asserted to exist first.
	/// </para>
	/// </summary>
	[TestMethod]
	public void TheGateStandsStillUntilSomethingCommandsIt()
	{
		var script = Bound( "gates" );

		// The three clips its own script names - entry 1 to open, 0 to shut, 2 for the sequence behind
		// command 2 - so "it played nothing" cannot be "it had nothing to play".
		for ( var entry = 0; entry < 3; ++entry )
			Assert.IsNotNull( script.Animations!.Clip( 5, entry ), $"the gate ships no role 5 entry {entry}" );

		for ( var turn = 0; turn < 200; ++turn )
			script.Turn( turn * 31f );

		Assert.IsTrue( script.Running, "the gate's script stopped, which no shipped script should do" );

		var channel = script.Animations!.Channel( 0 );

		Assert.IsTrue( channel == null || channel.IsIdle,
			$"the gate played role {channel?.AnimID} entry {channel?.SubAnim} with nothing having commanded it" );

		// And it is cycling its dispatch loop rather than having wandered off: with both variables at nought
		// every word it can be at lies at or before the close branch's own test.
		Assert.IsTrue( script.Position <= 34, $"the gate's script settled at word {script.Position}" );
	}

	/// <summary>
	/// And it <b>moves</b> when it is commanded, which is the other half of the test above and the half
	/// that was missing while a park's gates could not be opened at all.
	///
	/// <para>
	/// <b>The index is asserted, not assumed.</b> <c>FUN_00519ef0</c> writes the gate's variable <b>0</b> -
	/// read off the disassembly, because the decompile renders those call sites with the wrong argument
	/// lists - and this pins that the script's own name tail agrees, so the command is reached by name
	/// rather than by a number nobody checked. A name the script does not declare would give -1 and write
	/// nothing, which on screen is indistinguishable from a gate nobody commanded.
	/// </para>
	/// <para>
	/// One is open and <b>two</b> is shut, which is the value a decompile-only reading gets wrong.
	/// </para>
	/// </summary>
	[TestMethod]
	public void TheGateMovesOnceItIsCommandedToOpen()
	{
		var script = Bound( "gates" );

		Assert.AreEqual( 0, script.IndexOf( "VAR_COMMAND" ),
			"the gate's script should declare VAR_COMMAND as its first variable" );

		Assert.IsTrue( script.Set( "VAR_COMMAND", 1 ), "the open command should land in a declared variable" );

		for ( var turn = 0; turn < 200; ++turn )
			script.Turn( turn * 31f );

		var channel = script.Animations!.Channel( 0 );

		Assert.IsNotNull( channel, "a commanded gate should have started an animation" );
		Assert.IsFalse( channel!.IsIdle,
			$"the gate was told to open and its channel is still idle at word {script.Position}" );
	}

	/// <summary>
	/// The traffic lights are the opposite case, and the pair is why "fixed items hold their built pose" is
	/// not a rule: <c>lights.RSE</c> starts an unconditional <c>LOOPANIM</c> as its second instruction, so a
	/// bound crossing begins animating with nothing having asked it to.
	/// </summary>
	[TestMethod]
	public void TheTrafficLightsStartTheirOwnClipWithNothingAskingThem()
	{
		var script = Bound( "lights" );

		for ( var turn = 0; turn < 10; ++turn )
			script.Turn( turn * 31f );

		var channel = script.Animations!.Channel( 0 );

		Assert.IsNotNull( channel, "the lights started no animation at all" );
		Assert.IsFalse( channel!.IsIdle, "the lights' channel is idle, so its LOOPANIM never took" );
		Assert.AreEqual( 5, channel.AnimID, "the role the lights loop" );
		Assert.AreEqual( 0, channel.SubAnim, "the entry they loop until a crossing changes it" );
	}

	/// <summary>
	/// <b>And it will still not look like anything, which is measured rather than assumed.</b> Both clips the
	/// crossing loops declare a real ten-frame span and carry not one track of any kind - no rotation, no
	/// position, no morph, no UV, no visibility.
	///
	/// <para>
	/// This is worth pinning because of how it would otherwise be read. A correctly bound crossing spins a
	/// channel for ever while nothing on screen moves, which looks exactly like posing being broken; it is
	/// not. Whatever changes the lamps in the original is not in these clips - the archive ships two lamp
	/// textures and four of the meshes carry a flag this program does not read, which is where to look, and
	/// this test should be revisited rather than deleted if that is ever chased.
	/// </para>
	/// <para>
	/// The declared span is asserted first so that "no tracks" cannot pass as "the file did not read": these
	/// are real clips of real length, and they are the limiting case of the 114 the strict loader rejects.
	/// </para>
	/// </summary>
	[TestMethod]
	public void TheTrafficLightsLoopTwoClipsThatCannotMoveAnything()
	{
		var animations = RideAnimations.Load( "levels/jungle/features/lights", "lights", data );

		for ( var entry = 0; entry < 2; ++entry )
		{
			var clip = animations.Clip( 5, entry );

			Assert.IsNotNull( clip, $"the lights ship no role 5 entry {entry}" );

			Assert.AreEqual( 10, clip!.DeclaredLastFrame - clip.DeclaredFirstFrame,
				$"role 5 entry {entry} declares a span of" );

			Assert.AreEqual( 0,
				clip.RotationTracks.Count + clip.PositionTracks.Count + clip.MorphTracks.Count
					+ clip.UvTracks.Count + clip.VisibilityTracks.Count,
				$"role 5 entry {entry} carries a track after all, so the lights could move" );
		}
	}

	/// <summary>
	/// Every theme's gate holds still, and <b>space's holds still for a different reason than the other
	/// three</b> - which is why "the gate idles" is three themes out of four rather than a rule about gates.
	///
	/// <para>
	/// All four themes ship their own <c>Gates.RSE</c> and all four differ. Jungle, fantasy and hallow open on
	/// <c>TEST VAR_COMMAND</c> and cycle their dispatch loop, reaching no animation at all. Space opens with an
	/// unconditional <c>LOOPANIM_CH</c> at word 2, before any test - and what keeps its gate still here is that
	/// this interpreter does not implement that opcode, so it is counted rather than obeyed.
	/// </para>
	/// <para>
	/// <b>That distinction is the whole point of the test.</b> Implementing <c>LOOPANIM_CH</c> would set one
	/// park's gate moving with nothing having commanded it, and without this the only sign would be a gate
	/// swinging in a theme nobody happened to be looking at. The unimplemented count is therefore asserted in
	/// both directions, so this says which reason applies to which theme rather than merely that nothing moved.
	/// </para>
	/// </summary>
	[TestMethod]
	public void EveryThemesGateHoldsStillAndSpacesForADifferentReason()
	{
		foreach ( var theme in new[] { "jungle", "fantasy", "hallow", "space" } )
		{
			var script = BoundIn( theme, "gates" );

			for ( var turn = 0; turn < 200; ++turn )
				script.Turn( turn * 31f );

			Assert.IsTrue( script.Running, $"{theme}'s gate script stopped, which no shipped script should do" );

			var channel = script.Animations!.Channel( 0 );

			Assert.IsTrue( channel == null || channel.IsIdle,
				$"{theme}'s gate played role {channel?.AnimID} entry {channel?.SubAnim} with nothing commanding it" );

			if ( theme == "space" )
			{
				Assert.IsTrue( script.NotImplemented > 0,
					"space's gate opens with LOOPANIM_CH, so this should be counting an instruction it cannot obey" );
			}
			else
			{
				Assert.AreEqual( 0, script.NotImplemented,
					$"{theme}'s gate idles entirely on instructions this interpreter does implement" );
			}
		}
	}
}
