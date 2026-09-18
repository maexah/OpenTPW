using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Linq;
using System.Text;

namespace OpenTPW.Tests;

/// <summary>
/// The <c>_CH</c> animation instructions: <c>TRIGANIM_CH</c>, <c>LOOPANIM_CH</c> and <c>GETANIM_CH</c>,
/// which name the animation player they act on instead of always taking the first.
///
/// <para>
/// <b>These were the last live gap in the ride VM.</b> Across the 308 shipped scripts the family is
/// <c>TRIGANIM_CH</c> 63 uses in 6 scripts, <c>GETANIM_CH</c> 15 in 5 and <c>LOOPANIM_CH</c> 1 - and
/// <c>WAITANIM_CH</c>, <c>FLUSHANIM_CH</c>, <c>TRIGWAITANIM_CH</c> and plain <c>GETANIM</c> are used by
/// nothing at all, so they stay unbuilt and keep announcing themselves.
/// </para>
/// <para>
/// <b>Each one is its plain sibling plus one channel word, and each differs from that sibling in exactly
/// one further way</b> - which is what these assert, because copying the sibling and adding a parameter
/// would look right and be wrong three times over. See <see cref="RideScriptAnimationTests"/> for the
/// plain forms.
/// </para>
/// <para>
/// These read real game files, because a channel needs real clips: <see cref="RideAnimations.None"/>
/// carries none, and a trigger naming a clip that does not exist <i>stops</i> the channel rather than
/// playing on it. They are skipped where there is no installation - see <see cref="GameData"/>.
/// </para>
/// </summary>
[TestClass]
public class RideScriptChannelTests
{
	/// <summary>Lost Kingdom's Jungle Spray: three lanes, three players, and role 5 entries 0 to 5.</summary>
	private const string JunsprayDirectory = "levels/jungle/sideshow/junspray";

	private const string JunsprayStem = "Junspray";

	/// <summary>What <c>Junspray.sam</c> declares in <c>UsageInfo.NumSimultAnims</c>.</summary>
	private const int JunsprayChannels = 3;

	private BaseFileSystem _data = null!;

	[TestInitialize]
	public void MountTheGame() => _data = GameData.Required();

	private RideAnimations Junspray( int channels = JunsprayChannels )
		=> RideAnimations.Load( JunsprayDirectory, JunsprayStem, _data, channels );

	/// <summary>The instruction word for an opcode: top byte 0x80, the opcode in the low bits.</summary>
	private static int Word( Opcode opcode ) => unchecked( (int)(0x80000000u | (uint)opcode) );

	/// <summary>A variable operand - tag 0x40.</summary>
	private static int Var( int index ) => unchecked( (int)(0x40000000u | (uint)index) );

	/// <summary>A literal - tag 0x00, sign-extended from its low sixteen bits.</summary>
	private static int Lit( int value ) => value;

	/// <summary>
	/// A whole .RSE file, the same builder <see cref="RideScriptAnimationTests"/> keeps, written out again
	/// rather than shared so neither file has to move for the other.
	/// </summary>
	private static RideScriptFile Build( int variableCount, params int[] body )
	{
		using var memory = new MemoryStream();
		using var writer = new BinaryWriter( memory );

		writer.Write( Encoding.ASCII.GetBytes( "RSSE" ) );
		writer.Write( 0x00010F51 );
		writer.Write( variableCount );
		writer.Write( 8 );          // stack size
		writer.Write( 50 );         // time slice
		writer.Write( 0 );          // limbo records
		writer.Write( 0 );          // bounce records
		writer.Write( 0 );          // walk records
		writer.Write( Encoding.ASCII.GetBytes( "Pad Pad Pad Pad " ) );
		writer.Write( body.Length );

		foreach ( var word in body )
			writer.Write( word );

		writer.Write( 0 );          // string blob length

		for ( var i = 0; i < variableCount; ++i )
		{
			var name = Encoding.ASCII.GetBytes( $"VAR_{i}\0" );

			writer.Write( name.Length );
			writer.Write( name );
		}

		writer.Flush();

		return new RideScriptFile( new MemoryStream( memory.ToArray() ) );
	}

	private RideScript Running( RideAnimations animations, params int[] body )
		=> new( Build( 2, body ) ) { Animations = animations };

	/// <summary>
	/// <b>The channel count is the item's own, out of its <c>.sam</c>.</b> It is
	/// <c>UsageInfo.NumSimultAnims</c>, which the thing loader hands the model loader as the size of the
	/// player array - and an item that says nothing gets one, because the engine substitutes 1 for nought.
	/// </summary>
	[TestMethod]
	public void AnItemsFileSaysHowManyPlayersItWants()
	{
		Assert.AreEqual( 1, new ItemDescriptionFile( "Info.Id 1\n" ).NumSimultAnims,
			"an item that does not mention it gets the one the engine floors a nought to" );

		Assert.AreEqual( 4, new ItemDescriptionFile( "Info.Id 1\nUsageInfo.NumSimultAnims 4\n" ).NumSimultAnims );

		// The trailing prose is the file's own: every shipped line carries an explanation after the value.
		Assert.AreEqual( 3, new ItemDescriptionFile(
			"UsageInfo.NumSimultAnims\t\t3\t\tHow many different anims can this ride run?\n" ).NumSimultAnims,
			"the value is read and the comment after it ignored" );
	}

	/// <summary>
	/// The shipped items agree with the shipped scripts: every script naming a channel reaches exactly the
	/// last one its item allows. <b>That is what identifies the field</b> - the Totem moves both numbers
	/// together, so neither is a constant that happens to be three.
	/// </summary>
	[TestMethod]
	public void TheDeclaredCountMatchesTheChannelsTheScriptsActuallyName()
	{
		foreach ( var (directory, stem, declared) in new[]
		{
			("levels/jungle/sideshow/junspray", "Junspray", 3),
			("levels/jungle/sideshow/hyenas", "Hyenas", 3),
			("levels/jungle/rides/totem", "Totem", 4),
			("levels/space/features/gates", "gates", 2)
		} )
		{
			using var stream = _data.OpenRead( $"{directory}/{stem}.sam" );

			Assert.AreEqual( declared, new ItemDescriptionFile( stream ).NumSimultAnims,
				$"{stem} declares a different number of players than it used to" );
		}
	}

	/// <summary>
	/// The array is sized by that number, and floored at one exactly as <c>FUN_004629d0</c> floors it.
	/// <see cref="RideAnimations.Channel"/> refuses anything past the end, which is the one place this
	/// deliberately does not reproduce the original - the engine would index past its allocation.
	/// </summary>
	[TestMethod]
	public void ThePlayerArrayIsSizedByTheDeclaredCount()
	{
		var three = Junspray();

		Assert.AreEqual( JunsprayChannels, three.ChannelCount );
		Assert.IsNotNull( three.Channel( 2 ), "the Jungle Spray's third lane has to have a player" );
		Assert.IsNull( three.Channel( 3 ), "and asking past the end is refused rather than read" );
		Assert.IsNull( three.Channel( -1 ) );

		Assert.AreEqual( 1, RideAnimations.None( "nothing", 0 ).ChannelCount,
			"a nought count is the engine's own 1" );
	}

	/// <summary>
	/// <c>TRIGANIM_CH</c> plays on the channel its fourth operand names and leaves the others alone.
	///
	/// <para>
	/// This is the Jungle Spray's own instruction shape: its three lanes trigger role 5 entries 0, 1 and 2
	/// onto channels 0, 1 and 2 respectively, so the entry and the channel both track the lane.
	/// </para>
	/// </summary>
	[TestMethod]
	public void ATriggerPlaysOnTheChannelItNames()
	{
		var animations = Junspray();

		var script = Running( animations,
			Word( Opcode.TRIGANIM_CH ), Lit( 5 ), Lit( 2 ), Lit( 0 ), Lit( 2 ),
			Word( Opcode.END ) );

		script.Turn( 0f );

		Assert.AreEqual( 5, animations.Channel( 2 )!.AnimID, "channel 2 should be running role 5" );
		Assert.AreEqual( 2, animations.Channel( 2 )!.SubAnim, "entry 2" );

		Assert.IsTrue( animations.Channel( 0 )!.IsIdle, "channel 0 was not asked for and must be untouched" );
		Assert.IsTrue( animations.Channel( 1 )!.IsIdle, "nor channel 1" );
	}

	/// <summary>
	/// The channel operand is <b>resolved</b> for a trigger - a variable holding 2 names channel 2. This is
	/// the tag test at <c>0x00553158</c>, and it is exactly what <c>LOOPANIM_CH</c> does not do.
	/// </summary>
	[TestMethod]
	public void ATriggersChannelMayComeFromAVariable()
	{
		var animations = Junspray();

		var script = Running( animations,
			Word( Opcode.COPY ), Var( 0 ), Lit( 1 ),
			Word( Opcode.TRIGANIM_CH ), Lit( 5 ), Lit( 1 ), Lit( 0 ), Var( 0 ),
			Word( Opcode.END ) );

		script.Turn( 0f );

		Assert.AreEqual( 5, animations.Channel( 1 )!.AnimID, "the variable named channel 1" );
		Assert.IsTrue( animations.Channel( 0 )!.IsIdle, "and channel 0 is not where it went" );
	}

	/// <summary>
	/// <b><c>TRIGANIM_CH</c> does not stamp the looping key, and plain <c>TRIGANIM</c> does.</b> The two
	/// handlers are identical until the very end, where <c>TRIGANIM</c> jumps to a shared tail
	/// (<c>0x00552fe5</c>) that writes the deadline <i>and</i> the key at <c>+0xa8</c>, while
	/// <c>TRIGANIM_CH</c> ends inline having written only the deadline.
	///
	/// <para>
	/// <b>The difference is observable through the next <c>LOOPANIM</c>.</b> Its key for role 0 entry 0 is
	/// nought, which is also what the field starts at - so after a <c>_CH</c> trigger the loop reads as
	/// "already looping" and takes the engine's early exit, queueing nothing, whereas after a plain trigger
	/// the field holds <c>0xFFFF</c>, the keys differ, and the loop really is asked for. Copying
	/// <c>TriggerAnimation</c> and adding a channel would fail this and nothing else.
	/// </para>
	/// </summary>
	[TestMethod]
	public void OnlyThePlainTriggerStampsTheLoopingKey()
	{
		var afterPlain = Junspray();

		Running( afterPlain,
			Word( Opcode.TRIGANIM ), Lit( 0 ), Lit( 0 ), Lit( 0 ),
			Word( Opcode.LOOPANIM ), Lit( 0 ), Lit( 0 ),
			Word( Opcode.END ) ).Turn( 0f );

		Assert.IsTrue( afterPlain.Channel( 0 )!.HasQueued,
			"a plain trigger leaves the key at OneShot, so the loop that follows differs from it and is obeyed" );

		var afterChannel = Junspray();

		Running( afterChannel,
			Word( Opcode.TRIGANIM_CH ), Lit( 0 ), Lit( 0 ), Lit( 0 ), Lit( 0 ),
			Word( Opcode.LOOPANIM ), Lit( 0 ), Lit( 0 ),
			Word( Opcode.END ) ).Turn( 0f );

		Assert.IsFalse( afterChannel.Channel( 0 )!.HasQueued,
			"a _CH trigger leaves the key alone, so the loop matches it and takes the engine's early exit" );
	}

	/// <summary>
	/// <c>LOOPANIM_CH</c> loops role and entry on the channel named, with the loop flag set and no deadline
	/// left for <c>WAIT4ANIM</c> to wait on - a loop never finishes.
	/// </summary>
	[TestMethod]
	public void ALoopRunsOnTheChannelItNames()
	{
		var animations = Junspray();

		var script = Running( animations,
			Word( Opcode.LOOPANIM_CH ), Lit( 5 ), Lit( 1 ), Lit( 1 ),
			Word( Opcode.END ) );

		script.Turn( 0f );

		var channel = animations.Channel( 1 )!;

		Assert.AreEqual( 5, channel.AnimID );
		Assert.AreEqual( 1, channel.SubAnim );
		Assert.AreNotEqual( 0, channel.Flags & AnimTimeControl.LoopFlag, "it has to actually loop" );

		Assert.IsTrue( animations.Channel( 0 )!.IsIdle, "and not on channel 0" );
	}

	/// <summary>
	/// <b><c>LOOPANIM_CH</c> takes its channel RAW, alone in the family.</b> It fetches the operand word and
	/// pushes it with no <c>0x40000000</c> tag test (<c>0x00553435</c> to <c>0x00553470</c>), where both the
	/// trigger and the query resolve theirs. A variable therefore arrives as its tagged word, which names no
	/// channel at all - the original would index that far past its allocation, and this refuses.
	///
	/// <para>
	/// No shipped script does it: the single <c>LOOPANIM_CH</c> in the whole game names a literal nought.
	/// The test exists because resolving it would look like tidying up an inconsistency.
	/// </para>
	/// </summary>
	[TestMethod]
	public void ALoopsChannelIsNotResolvedFromAVariable()
	{
		var animations = Junspray();

		var script = Running( animations,
			Word( Opcode.COPY ), Var( 0 ), Lit( 1 ),
			Word( Opcode.LOOPANIM_CH ), Lit( 5 ), Lit( 1 ), Var( 0 ),
			Word( Opcode.END ) );

		script.Turn( 0f );

		Assert.IsTrue( animations.Channel( 1 )!.IsIdle,
			"the variable held 1, but a raw tagged word names no channel, so nothing should play on channel 1" );

		Assert.IsTrue( animations.Channel( 0 )!.IsIdle, "and nothing fell back to channel 0 either" );
	}

	/// <summary>
	/// <b><c>GETANIM_CH</c> answers the role a channel is playing, and <c>-1</c> once the clip is over.</b>
	/// The engine reads the player through <c>FUN_00473fb0</c> and overrides the role with <c>-1</c> when the
	/// flag word carries <c>0x4</c>, which is the bit <see cref="AnimTimeControl.HoldAtEnd"/> sets.
	///
	/// <para>
	/// <b>This is the Jungle Spray's exit gate.</b> Its script branches away on positive and on zero, so
	/// <c>-1</c> is the only answer that reaches <c>WALKOFF</c>. An idle channel answers
	/// <see cref="RideAnimations.NoRole"/>, which is 12 and positive, so an empty lane correctly keeps its
	/// rider rather than releasing one it never had.
	/// </para>
	/// </summary>
	[TestMethod]
	public void AskingAChannelAnswersItsRoleAndMinusOneOnceItHasFinished()
	{
		var animations = Junspray();

		var idle = Running( animations,
			Word( Opcode.GETANIM_CH ), Var( 0 ), Lit( 0 ),
			Word( Opcode.END ) );

		idle.Turn( 0f );

		Assert.AreEqual( RideAnimations.NoRole, idle["VAR_0"],
			"an idle channel answers the sentinel, which is positive and so keeps a rider aboard" );

		var playing = Running( animations,
			Word( Opcode.TRIGANIM_CH ), Lit( 5 ), Lit( 0 ), Lit( 0 ), Lit( 0 ),
			Word( Opcode.GETANIM_CH ), Var( 0 ), Lit( 0 ),
			Word( Opcode.END ) );

		playing.Turn( 0f );

		Assert.AreEqual( 5, playing["VAR_0"], "while it runs, the channel answers its role" );

		// What ParkObjects.Sweep does once a frame, and the only thing that can move a channel to its end.
		animations.Advance( 10_000_000 );

		var finished = Running( animations,
			Word( Opcode.GETANIM_CH ), Var( 0 ), Lit( 0 ),
			Word( Opcode.END ) );

		finished.Turn( 0f );

		Assert.AreEqual( -1, finished["VAR_0"],
			"once the clip is held at its end the answer is -1, which is what lets a rider off" );
	}

	/// <summary>
	/// With no model at all the answer is nought. The engine reads a stack slot it never wrote here, which is
	/// genuinely undefined; nought is chosen because it is the safe one - it is the "not finished" branch, so
	/// a script running without a model holds its riders rather than flinging them off.
	/// </summary>
	[TestMethod]
	public void AskingWithNoModelAnswersNought()
	{
		var script = new RideScript( Build( 2,
			Word( Opcode.GETANIM_CH ), Var( 0 ), Lit( 0 ),
			Word( Opcode.END ) ) );

		script.Turn( 0f );

		Assert.AreEqual( 0, script["VAR_0"] );
	}

	/// <summary>
	/// <b>The count has to survive the journey from the item's own file to the placed thing</b>, and nothing
	/// asserted that until this test existed.
	///
	/// <para>
	/// <b>Found by mutation, not by reading.</b> Replacing <c>description.NumSimultAnims</c> with a literal 1
	/// in <see cref="ParkItemCatalogue"/> left all 789 tests green. Every other test here reads
	/// <see cref="ItemDescriptionFile"/> directly, and the sideshow's round trip loads
	/// <see cref="RideAnimations"/> directly with a count of its own - so both step straight over the single
	/// wire that carries the number into a real park. Coverage of the parts is not coverage of the join.
	/// </para>
	/// <para>
	/// It asserts in <b>both</b> directions on purpose: some item must report more than one player and some
	/// must report exactly one, so neither a constant 1 nor a constant 3 could pass. The per-item comparison
	/// then checks the whole wire against each file rather than against a number written here.
	/// </para>
	/// </summary>
	[TestMethod]
	public void TheCatalogueCarriesTheDeclaredCountToThePlacedThing()
	{
		var catalogue = new ParkItemCatalogue( "jungle", _data );

		Assert.IsTrue( catalogue.TryGet( 1303, out var sideshow ), "the jungle catalogue has no item 1303" );
		Assert.AreEqual( "Jungle Spray", sideshow.Name, "1303 is not the item this test thinks it is" );

		// Read back through the catalogue's OWN path before asserting, so a failure says whether the file
		// or the wire is at fault rather than only that the number is wrong.
		int Declared( ParkItemCatalogue.Item item, bool withCategory = false )
		{
			using var stream = _data.OpenRead( $"{item.Directory}/{item.Stem}.sam" );

			if ( !withCategory )
				return new ItemDescriptionFile( stream ).NumSimultAnims;

			// Exactly what ParkItemCatalogue does: the item's file read against its folder's.
			using var folder = _data.OpenRead( "levels/jungle/sideshow/SideShow.sam" );

			return new ItemDescriptionFile( stream, new ItemDescriptionFile( folder ) ).NumSimultAnims;
		}

		// Stored against declared for a spread of items, so a failure says whether this is the Jungle Spray
		// alone or every item, and prints the directory and stem the catalogue actually kept.
		var report = new StringBuilder();

		foreach ( var id in new[] { 1100, 1101, 1200, 1203, 1300, 1303, 1601, 1603 } )
		{
			if ( !catalogue.TryGet( id, out var each ) )
				continue;

			report.Append( $"[{id} '{each.Name}' dir='{each.Directory}' stem='{each.Stem}' "
				+ $"stored={each.AnimationChannels} declared={Declared( each )}] " );
		}

		Assert.AreEqual( JunsprayChannels, sideshow.AnimationChannels,
			$"the Jungle Spray's three players did not reach the catalogue. Its own file at "
				+ $"'{sideshow.Directory}/{sideshow.Stem}.sam' reads {Declared( sideshow )} alone and "
				+ $"{Declared( sideshow, withCategory: true )} when read against its folder's description, "
				+ $"as the catalogue reads it. Across the theme: {report}" );

		var items = new[] { 1100, 1101, 1203, 1303, 1601, 1603 }
			.Select( id => catalogue.TryGet( id, out var item ) ? item : default )
			.Where( item => item.Id != 0 )
			.ToArray();

		Assert.IsTrue( items.Any( item => item.AnimationChannels > 1 ),
			"nothing reported more than one player, so the count is being thrown away" );

		Assert.IsTrue( items.Any( item => item.AnimationChannels == 1 ),
			"nothing reported exactly one, so the count is not being read per item" );

		foreach ( var item in items )
		{
			using var stream = _data.OpenRead( $"{item.Directory}/{item.Stem}.sam" );

			Assert.AreEqual( new ItemDescriptionFile( stream ).NumSimultAnims, item.AnimationChannels,
				$"'{item.Name}' reports a different count through the catalogue than its own file declares" );
		}
	}

	/// <summary>
	/// The rest of the family is still unbuilt, and deliberately: nothing in the 308 shipped scripts uses
	/// <c>WAITANIM_CH</c>, <c>FLUSHANIM_CH</c>, <c>TRIGWAITANIM_CH</c> or plain <c>GETANIM</c>. Reaching one
	/// has to keep saying so on the console rather than passing silently.
	/// </summary>
	[TestMethod]
	public void TheUnusedHalfOfTheFamilyStillAnnouncesItself()
	{
		Unimplemented.Forget();

		var script = new RideScript( Build( 2,
			Word( Opcode.WAITANIM_CH ), Lit( 0 ), Lit( 0 ), Lit( 0 ),
			Word( Opcode.END ) ) );

		script.Turn( 0f );

		Assert.IsTrue( script.NotImplemented > 0, "WAITANIM_CH is not built and must be counted" );
		Assert.IsTrue( Unimplemented.Any, "and must have said so" );
	}
}
