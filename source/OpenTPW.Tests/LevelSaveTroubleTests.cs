using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace OpenTPW.Tests;

/// <summary>
/// <see cref="Level"/>'s hand-over of a waiting save-folder notice to the front window (Q982, Q984, Q922), driven the way a frame
/// drives it: the notice is given at the start, the level's frame asks to open its box, and it is counted told in
/// opentpw.cfg only once the box is open. The rest of the notices' tests call the pieces; this one goes through the level.
/// </summary>
[TestClass]
[DoNotParallelize]
public class LevelSaveTroubleTests
{
	private string root = null!;
	private string game = null!;
	private BaseFileSystem? oldSaves;
	private GameOptions oldOptions = null!;
	private Level? oldLevel;
	private bool oldHidden;

	[TestInitialize]
	public void MakeFolder()
	{
		root = Directory.CreateTempSubdirectory( "opentpw-level-trouble-" ).FullName;
		game = Path.Join( root, "game", "save" );
		Directory.CreateDirectory( game );

		oldSaves = SaveFileSystemOrNull;
		oldOptions = GameOptions.Current;
		oldLevel = Level.Current;
		oldHidden = UI.RootPanel.Hidden;
		SaveFileSystem = new BaseFileSystem( game );
		GameOptions.Current = new();
		UI.RootPanel.Hidden = false;
		Drain();
	}

	[TestCleanup]
	public void Restore()
	{
		Drain();
		UI.RootPanel.Hidden = oldHidden;
		Level.Current = oldLevel!;
		SaveFileSystemOrNull = oldSaves;
		GameOptions.Current = oldOptions;
		oldOptions.ApplySound();
		Directory.Delete( root, true );
	}

	private static void Drain()
	{
		SaveTrouble.Asked();

		while ( SaveTrouble.Pending )
			SaveTrouble.Take();

		SaveTrouble.NewVisit();
	}

	/// <summary>A bare level with the given window stack, as the park tests make one; its frame's notice step is then run by hand.</summary>
	private static Level ALevel( UI.WindowStack stack )
	{
		var level = (Level)RuntimeHelpers.GetUninitializedObject( typeof( Level ) );
		typeof( Level ).GetField( "_windows", BindingFlags.Instance | BindingFlags.NonPublic )!.SetValue( level, stack );
		Level.Current = level;

		return level;
	}

	/// <summary>The level's step of a frame that hands a waiting notice to the front window.</summary>
	private static void TheFrame( Level level )
		=> typeof( Level ).GetMethod( "TellSaveTrouble", BindingFlags.Instance | BindingFlags.NonPublic )!.Invoke( level, [] );

	private static string TextOf( UI.UiControl control )
		=> control.Text ?? control.Children.Select( TextOf ).FirstOrDefault( text => text != null ) ?? "";

	private string Cfg() => File.Exists( Path.Join( game, "opentpw.cfg" ) ) ? File.ReadAllText( Path.Join( game, "opentpw.cfg" ) ) : "";

	/// <summary>
	/// <b>The start's notice is counted told once the frame has opened its box, and not before</b>: held while F2 hides the
	/// interface and while another box is up (nothing is opened, nothing is written), then a frame with the front window free opens
	/// the box with the notice's words and writes <c>told kept</c>, and the next start has no box.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> the <c>SaveHome.Told</c> call dropped from <c>Level.TellSaveTrouble</c> (opentpw.cfg is never written: the
	/// box comes at every start); the call moved before the box is opened or made on every frame whatever the window (a hidden
	/// or covered frame counts it told unseen); the hidden test dropped (the box opens under F2); the modal test dropped (a second box
	/// over the first).
	/// </remarks>
	[TestMethod]
	public void AStartsNoticeIsCountedToldOnlyOnceTheFramesBoxIsOpen()
	{
		var kept = new SaveHome.Pick( game, false, SaveHome.KeptNotice(), SaveHome.KeptName );
		var stack = new UI.WindowStack();
		var level = ALevel( stack );

		SaveHome.LoadDisplayAndTell( kept );
		Assert.IsTrue( SaveTrouble.Pending, "the start gave the notice" );

		// F2 has hidden the interface: the frame leaves the notice waiting, and nothing is counted.
		UI.RootPanel.Hidden = true;
		TheFrame( level );
		Assert.AreEqual( 0, stack.Windows.Count, "no box under F2" );
		Assert.IsTrue( SaveTrouble.Pending, "the notice waits for the interface" );
		Assert.IsFalse( Cfg().Contains( "told" ), "an unopened notice is not counted told" );
		UI.RootPanel.Hidden = false;

		// Another question is up: the notice does not land over it.
		stack.Open( new UI.MessageBox( stack, "Another question?", null ) );
		Assert.IsTrue( stack.ModalUp );
		TheFrame( level );
		Assert.AreEqual( 1, stack.Windows.OfType<UI.MessageBox>().Count(), "no second box over the first" );
		Assert.IsTrue( SaveTrouble.Pending, "the notice waits for the question to be answered" );
		Assert.IsFalse( Cfg().Contains( "told" ), "still unopened, still not counted" );

		// The question is answered: the next frame opens the notice's box and counts it told.
		stack.Close( stack.Windows.OfType<UI.MessageBox>().Single() );
		Assert.IsFalse( stack.ModalUp );
		TheFrame( level );

		var box = stack.Windows.OfType<UI.MessageBox>().Single();
		StringAssert.Contains( TextOf( box.Root ), SaveHome.KeptNotice().Split( '\n' )[0], "the box carries the notice's words" );
		Assert.IsFalse( SaveTrouble.Pending, "nothing is left waiting" );
		StringAssert.Contains( Cfg(), "\ntold kept\n", "counted told, and opentpw.cfg written, as the box opened" );

		// The start after that has no box.
		GameOptions.Current = new();
		SaveHome.LoadDisplayAndTell( kept );
		Assert.IsFalse( SaveTrouble.Pending, "told: no box at the next start" );
	}

	/// <summary>
	/// <b>A text that carries no notice name opens its box and writes nothing</b>: a failed write's or a kept copy's box (no name) is not a
	/// told notice, so opentpw.cfg gains no <c>told</c> line from it.
	/// </summary>
	/// <remarks><b>Mutation:</b> <c>SaveHome.Told</c> called with a made-up name when the notice has none.</remarks>
	[TestMethod]
	public void ABoxWithNoNoticeNameWritesNoToldLine()
	{
		var stack = new UI.WindowStack();
		var level = ALevel( stack );

		SaveTrouble.Tell( "SOMETHING\n\nA sentence.", null );
		TheFrame( level );

		Assert.AreEqual( 1, stack.Windows.OfType<UI.MessageBox>().Count(), "the box opened" );
		Assert.IsFalse( Cfg().Contains( "told" ), "and no notice was counted told" );
	}
}
