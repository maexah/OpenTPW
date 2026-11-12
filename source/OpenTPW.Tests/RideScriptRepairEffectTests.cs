using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace OpenTPW.Tests;

/// <summary>
/// <c>REPAIREFFECT</c> (93, <c>0x005562a6</c>) and <c>BUMP 17</c> (<c>0x005549ed</c>), Q448: docs/exe/park.md,
/// "`REPAIREFFECT` (93)" and "The pieces' clips". The repair's sounds are chosen by <see cref="RideScript.RepairSounds"/>;
/// <c>BUMP 17</c>'s walks go through <see cref="RideScript.PlayTrackPieces"/>, which these tests record.
/// </summary>
[TestClass]
public class RideScriptRepairEffectTests
{
	private static int Word( Opcode opcode ) => unchecked( (int)(0x80000000u | (uint)opcode) );

	private static int Lit( int value ) => value;

	private static int Var( int index ) => unchecked( (int)(0x40000000u | (uint)index) );

	private static RideScriptFile Build( int variableCount, params int[] body )
	{
		using var memory = new MemoryStream();
		using var writer = new BinaryWriter( memory );

		writer.Write( Encoding.ASCII.GetBytes( "RSSE" ) );
		writer.Write( 0x00010F51 );
		writer.Write( variableCount );
		writer.Write( 8 );              // stack size
		writer.Write( 50 );             // time slice
		writer.Write( 0 );              // limbo records
		writer.Write( 0 );              // bounce records
		writer.Write( 0 );              // walk records
		writer.Write( Encoding.ASCII.GetBytes( "Pad Pad Pad Pad " ) );
		writer.Write( body.Length );

		foreach ( var word in body )
			writer.Write( word );

		writer.Write( 0 );              // string blob length

		for ( int i = 0; i < variableCount; ++i )
		{
			var name = Encoding.ASCII.GetBytes( $"VAR_{i}\0" );

			writer.Write( name.Length );
			writer.Write( name );
		}

		writer.Flush();

		return new RideScriptFile( new MemoryStream( memory.ToArray() ) );
	}

	private static RideScript Script( int variableCount, params int[] body )
	{
		var file = Build( variableCount, body );

		Assert.IsTrue( file.IsValid, "the hand-built script did not read - the builder is wrong, not the machine" );

		return new RideScript( file );
	}

	private Func<int, bool, int?>? _playTrackPieces;

	private Func<int, int, int, Vector3, ParkAudio.ScriptSoundStart?>? _startRepairSound;

	private Func<int, int, Vector3, int, bool?>? _playRepairScream;

	private readonly List<(int Thing, bool Running)> _walks = [];

	private readonly List<(int Script, int Type, int Effect, Vector3 At)> _started = [];

	private readonly List<(int Band, int Level, Vector3 At, int Speed)> _screams = [];

	[TestInitialize]
	public void RecordTheWalksAndTheSounds()
	{
		_playTrackPieces = RideScript.PlayTrackPieces;
		RideScript.PlayTrackPieces = ( thing, running ) =>
		{
			_walks.Add( (thing, running) );
			return 8;
		};

		_startRepairSound = RideScript.StartRepairSound;
		RideScript.StartRepairSound = ( script, type, effect, at ) =>
		{
			_started.Add( (script, type, effect, at) );
			return ParkAudio.ScriptSoundStart.Started;
		};

		_playRepairScream = RideScript.PlayRepairScream;
		RideScript.PlayRepairScream = ( band, level, at, speed ) =>
		{
			_screams.Add( (band, level, at, speed) );
			return true;
		};
	}

	[TestCleanup]
	public void PutTheWalksAndTheSoundsBack()
	{
		RideScript.PlayTrackPieces = _playTrackPieces!;
		RideScript.StartRepairSound = _startRepairSound!;
		RideScript.PlayRepairScream = _playRepairScream!;
	}

	/// <summary>
	/// A non-zero value shows the repair: the global rides' 111 and 211, whatever else it is, and picks no scream. Made to
	/// fail by the other fork's polarity (sounds on nought) or by testing for 1 rather than non-zero.
	/// </summary>
	[TestMethod]
	public void AnyNonZeroValuePlaysTheGlobalRidesTwo()
	{
		foreach ( var value in new[] { 1, -1, 7 } )
		{
			var (globalRides, picks, scream) = RideScript.RepairSounds( value, slots: 12, onRide: 3 );

			CollectionAssert.AreEqual( new[] { 111, 211 }, globalRides, $"value {value}" );
			Assert.IsFalse( picks, $"value {value} picked a scream" );
			Assert.AreEqual( 0, scream );
		}
	}

	/// <summary>
	/// Nought hides it and calls the <c>SINGLESCREAM</c> picker with (the sixth variable, 1, 1): grid step nought, so 75,
	/// 79, 83 or 87 by the number aboard, and nothing with none. Made to fail by passing the script's speed word (50 makes
	/// step 1: 76, 80, ...), by the negative branch (105...), or by reading the band from anywhere but the number aboard.
	/// </summary>
	[TestMethod]
	public void NoughtPicksAScreamByTheNumberAboard()
	{
		var expected = new Dictionary<int, int> { [0] = 0, [1] = 75, [2] = 79, [3] = 79, [4] = 83, [7] = 83, [8] = 87, [20] = 87 };

		foreach ( var (aboard, effect) in expected )
		{
			var (globalRides, picks, scream) = RideScript.RepairSounds( 0, slots: 12, onRide: aboard );

			Assert.AreEqual( 0, globalRides.Length, $"{aboard} aboard played the show's sounds" );
			Assert.IsTrue( picks, $"{aboard} aboard did not reach the picker" );
			Assert.AreEqual( effect, scream, $"{aboard} aboard" );
		}
	}

	/// <summary>
	/// The picker is reached only when the script declares more than five variables (<c>+0x8c</c> &gt; 5): five picks
	/// nothing, six picks. Made to fail by testing five or more, or by not testing at all.
	/// </summary>
	[TestMethod]
	public void NoughtPicksNothingForAScriptOfFiveVariables()
	{
		Assert.IsFalse( RideScript.RepairSounds( 0, slots: 5, onRide: 2 ).Picks, "five variables reached the picker" );
		Assert.IsTrue( RideScript.RepairSounds( 0, slots: 6, onRide: 2 ).Picks, "six variables did not" );
	}

	/// <summary>
	/// In the machine the instruction is no longer the dispatch's counted default. With no park: nought on a script of five
	/// variables does nothing and counts nothing; a non-zero value counts the place it has no thing to sound from, and the
	/// particle too only where the script has a thing (the spawn's gate, <c>+0xac</c>). Made to fail by dropping the case
	/// (each counts 1 at the default), or by gating the particle wrongly.
	/// </summary>
	[TestMethod]
	public void TheInstructionCountsOnlyWhatIsNotBuilt()
	{
		Assert.IsNull( ParkObjects.Current, "a park is standing, so this proves nothing about the counts with none" );

		var hidden = Script( 5, Word( Opcode.REPAIREFFECT ), Lit( 0 ), Word( Opcode.END ) );
		hidden.Turn( 0f );
		Assert.AreEqual( 0, hidden.NotImplemented, "nought on five variables" );

		var noThing = Script( 12, Word( Opcode.REPAIREFFECT ), Lit( 1 ), Word( Opcode.END ) );
		noThing.Turn( 0f );
		Assert.AreEqual( 1, noThing.NotImplemented, "one with no thing: its place alone" );

		var onAThing = Script( 12, Word( Opcode.REPAIREFFECT ), Lit( 1 ), Word( Opcode.END ) );
		onAThing.ThingId = 9;
		onAThing.Turn( 0f );
		Assert.AreEqual( 2, onAThing.NotImplemented, "one on a thing: the particle and its place" );
	}

	/// <summary>
	/// <c>BUMP 17</c> walks only when its operand differs from the last, which starts at -1: 65535 (a literal's -1) first
	/// walks nothing, then 0, 0, 1, 1, 0 walk running, at rest, running. Made to fail by the other polarity, by walking on
	/// every call, by starting the field at nought, or by sending 17 to the bumper record test (no record here: it counted).
	/// </summary>
	[TestMethod]
	public void Bump17WalksOnlyWhenItsOperandChanges()
	{
		var script = Script( 12,
			Word( Opcode.BUMP ), Lit( 17 ), Lit( 65535 ),
			Word( Opcode.BUMP ), Lit( 17 ), Lit( 0 ),
			Word( Opcode.BUMP ), Lit( 17 ), Lit( 0 ),
			Word( Opcode.BUMP ), Lit( 17 ), Lit( 1 ),
			Word( Opcode.BUMP ), Lit( 17 ), Lit( 1 ),
			Word( Opcode.BUMP ), Lit( 17 ), Lit( 0 ),
			Word( Opcode.END ) );
		script.ThingId = 33;

		script.Turn( 0f );

		CollectionAssert.AreEqual( new[] { (33, true), (33, false), (33, true) }, _walks );
		Assert.AreEqual( 0, script.NotImplemented, "BUMP 17 was counted" );
	}

	/// <summary>
	/// <c>BUMP 17</c>'s field is a word (<c>0x00554a11</c>) compared sign-extended with the whole operand (<c>0x005549fd</c>):
	/// 40000 in a variable walks both times (it is stored as -25536), and 65535 in a variable then the literal 65535 (-1)
	/// walks once (stored as -1, which the literal matches). Made to fail by storing the whole int (40000 then walks once,
	/// and the -1 walks again).
	/// </summary>
	[TestMethod]
	public void Bump17KeepsItsOperandAsAWord()
	{
		var wide = Script( 12,
			Word( Opcode.MULT ), Var( 0 ), Lit( 200 ), Lit( 200 ),
			Word( Opcode.BUMP ), Lit( 17 ), Var( 0 ),
			Word( Opcode.BUMP ), Lit( 17 ), Var( 0 ),
			Word( Opcode.END ) );
		wide.ThingId = 33;
		wide.Turn( 0f );

		CollectionAssert.AreEqual( new[] { (33, false), (33, false) }, _walks, "40000 twice" );

		_walks.Clear();

		var minusOne = Script( 12,
			Word( Opcode.MULT ), Var( 1 ), Lit( 255 ), Lit( 257 ),
			Word( Opcode.BUMP ), Lit( 17 ), Var( 1 ),
			Word( Opcode.BUMP ), Lit( 17 ), Lit( 65535 ),
			Word( Opcode.END ) );
		minusOne.ThingId = 33;
		minusOne.Turn( 0f );

		CollectionAssert.AreEqual( new[] { (33, false) }, _walks, "65535, then the literal -1" );
	}

	/// <summary>
	/// What the machine plays, recorded at its seams on the shipped Belly Bounce at (51,23), its model stood at its anchor's
	/// corner (510, 230, 7): a 1 starts the global rides' 111 then 211 as type 5 under the script's id, and a 0 plays the
	/// picker with (the sixth variable, 1, 1); all three at the model's placed origin (<c>FUN_00556a80</c>), not the middle
	/// of its cells (525, 250, 7) where a scream sounds.
	/// </summary>
	/// <remarks><b>Mutations:</b> the place from <c>TrySoundPlace</c>, a type other than 5, the band from the fifth variable,
	/// or the script's speed for the last 1 each fails here.</remarks>
	[TestMethod]
	public void TheRepairSoundsWhereTheModelStands()
	{
		var data = GameData.Required();
		Log ??= new();
		FileSystem = data;

		ParkWorld world;

		using ( var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) ) )
			world = new ParkWorld( new SaveReader( stream ).ReadFile() );

		var catalogue = new ParkItemCatalogue( "jungle", data );
		var placed = world.Objects.Single( thing => (thing.CellX, thing.CellY) == (51, 23) );

		Assert.IsTrue( catalogue.TryGet( placed.CatalogueId, out var item ) );

		// A model with only the position the sounds read: the GPU-side parts are not built.
		var model = (LobbyModel)RuntimeHelpers.GetUninitializedObject( typeof( LobbyModel ) );
		var corner = new Vector3( 510f, 230f, 7f );

		typeof( LobbyModel ).GetField( "_placedOrigin", BindingFlags.NonPublic | BindingFlags.Instance )!.SetValue( model, corner );

		var objects = new ParkObjects( "jungle", null, null );

		try
		{
			objects.StandPlaced( placed, item, model, RideAnimations.None(), null, [], 0, [], 0, () => default );

			Assert.IsTrue( objects.TrySoundPlace( placed.ThingId, out var middle ) );
			Assert.AreEqual( new Vector3( 525f, 250f, 7f ), middle, "the scream's place, which the repair does not use" );

			var script = Script( 12,
				Word( Opcode.COPY ), Var( 4 ), Lit( 7 ),
				Word( Opcode.COPY ), Var( 5 ), Lit( 3 ),
				Word( Opcode.REPAIREFFECT ), Lit( 1 ),
				Word( Opcode.REPAIREFFECT ), Lit( 0 ),
				Word( Opcode.END ) );
			script.Id = 16;
			script.ThingId = placed.ThingId;
			script.Turn( 0f );

			CollectionAssert.AreEqual( new[] { (16, 5, 111, corner), (16, 5, 211, corner) }, _started );
			CollectionAssert.AreEqual( new[] { (3, 1, corner, 1) }, _screams );
		}
		finally
		{
			objects.Delete();
		}
	}

	/// <summary>
	/// With no thing to walk it still stores the operand, and counts each walk it could not make. Made to fail by not
	/// counting, or by storing only after a walk (the second 0 would then count again).
	/// </summary>
	[TestMethod]
	public void Bump17WithNoRideCountsEachWalk()
	{
		RideScript.PlayTrackPieces = ( _, _ ) => null;

		var script = Script( 12,
			Word( Opcode.BUMP ), Lit( 17 ), Lit( 0 ),
			Word( Opcode.BUMP ), Lit( 17 ), Lit( 0 ),
			Word( Opcode.BUMP ), Lit( 17 ), Lit( 1 ),
			Word( Opcode.END ) );

		script.Turn( 0f );

		Assert.AreEqual( 2, script.NotImplemented );
	}

	private BaseFileSystem? _data;

	private string[] Entries( string path, bool directories )
	{
		try
		{
			return directories ? _data!.GetDirectories( path ) : _data!.GetFiles( path );
		}
		catch ( Exception )
		{
			return [];
		}
	}

	/// <summary>Every ride script the game ships, found by walking, as <c>RideScriptBounceTests</c> finds them.</summary>
	private IEnumerable<(string Path, RideScriptFile File)> EveryScript()
	{
		foreach ( var theme in Entries( "levels", directories: true ) )
		{
			var themeName = Path.GetFileName( theme );

			foreach ( var folder in new[] { "features", "rides", "shops", "sideshow", "upgrades" } )
			{
				foreach ( var item in Entries( $"levels/{themeName}/{folder}", directories: true ) )
				{
					var stem = Path.GetFileName( item );

					foreach ( var entry in Entries( $"levels/{themeName}/{folder}/{stem}", directories: false ) )
					{
						var name = Path.GetFileName( entry );

						if ( !name.EndsWith( ".rse", StringComparison.OrdinalIgnoreCase ) )
							continue;

						var path = $"levels/{themeName}/{folder}/{stem}/{name}";
						using var stream = new MemoryStream( _data!.ReadAllBytes( path ) );

						yield return (path, new RideScriptFile( stream ));
					}
				}
			}
		}
	}

	/// <summary>
	/// Over every shipped script: <c>REPAIREFFECT</c> is 70 x 1 and 70 x 0, and every script that runs it declares more
	/// than five variables with <c>VAR_ONRIDE</c> sixth, so the slot the handler reads by its place is the number aboard.
	/// Jungle's <c>Wateride.RSE</c> runs <c>BUMP 17</c> four times, 0, 1, 1, 0, and no other jungle script runs it.
	/// </summary>
	[TestMethod]
	public void TheCorpusReadsTheNumberAboardAndJunglesWaterRideAloneRunsBump17()
	{
		_data = GameData.Required();

		var values = new Dictionary<int, int>();
		var scripts = 0;
		var jungleBump17 = new List<(string Path, int Value)>();

		foreach ( var (path, file) in EveryScript() )
		{
			Assert.IsTrue( file.IsValid, path );

			var repairs = file.Instructions.Where( i => i.Opcode == Opcode.REPAIREFFECT ).ToList();

			if ( repairs.Count > 0 )
			{
				++scripts;
				Assert.IsTrue( file.VariableCount > RideScript.RepairOnRideSlot, $"{path} declares {file.VariableCount}" );
				Assert.AreEqual( "VAR_ONRIDE", file.VariableNames[RideScript.RepairOnRideSlot], path );
			}

			foreach ( var repair in repairs )
			{
				Assert.AreEqual( RideOperandKind.Literal, repair.Operands[0].Kind, path );
				values[repair.Operands[0].Value] = values.GetValueOrDefault( repair.Operands[0].Value ) + 1;
			}

			if ( !path.StartsWith( "levels/jungle/", StringComparison.OrdinalIgnoreCase ) )
				continue;

			foreach ( var bump in file.Instructions.Where( i => i.Opcode == Opcode.BUMP && i.Operands[0] is { Kind: RideOperandKind.Literal, Value: 17 } ) )
				jungleBump17.Add( (Path.GetFileName( path ), bump.Operands[1].Value) );
		}

		Console.WriteLine( $"REPAIREFFECT: {scripts} scripts, values {string.Join( ", ", values.OrderBy( v => v.Key ).Select( v => $"{v.Key} x{v.Value}" ) )}" );
		Console.WriteLine( $"jungle BUMP 17: {string.Join( ", ", jungleBump17 )}" );

		Assert.AreEqual( 2, values.Count, "a value other than 0 and 1" );
		Assert.AreEqual( 70, values[1] );
		Assert.AreEqual( 70, values[0] );
		CollectionAssert.AreEqual( new[] { 0, 1, 1, 0 }, jungleBump17.Select( b => b.Value ).ToArray() );
		Assert.IsTrue( jungleBump17.All( b => b.Path.Equals( "Wateride.RSE", StringComparison.OrdinalIgnoreCase ) ), string.Join( ", ", jungleBump17 ) );
	}
}
