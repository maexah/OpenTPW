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
}
