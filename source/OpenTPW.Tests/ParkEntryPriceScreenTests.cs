using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenTPW.UI;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// The entry price screen's loans button (<c>0x4f3ac</c>, <c>FUN_00498d80</c>), disabled in Instant Action, game type 2
/// (<c>0x00499008</c>..<c>0x00499030</c>; <c>docs/exe/hud.md</c>, "The entry price screen's controls"). These read the shipped
/// game's meshes and are skipped where there is no installation - see <see cref="GameData"/>.
/// </summary>
[TestClass]
public class ParkEntryPriceScreenTests
{
	/// <summary>The loans button's middle on the interface's 2048x1536: (1568,411)-(1670,513).</summary>
	private const float LoansX = 1619;
	private const float LoansY = 462;

	private Point2 _screen;

	[TestInitialize]
	public void MountTheGame()
	{
		_screen = Screen.Size;
		Screen.Size = new Point2( 2048, 1536 );

		Log ??= new();
		FileSystem = GameData.Required();
		Input.Mouse = new();
		Input.ForgetHeldKeys();
		Unimplemented.Forget();
		ParkCategoryScreens.Seed();
	}

	[TestCleanup]
	public void PutItAllBack()
	{
		Playing( null );
		ParkCategoryScreens.Seed();
		Input.Mouse = new();
		Input.ForgetHeldKeys();
		Unimplemented.Forget();
		Screen.Size = _screen;
	}

	/// <summary>
	/// <b>In Instant Action the loans button is disabled</b>: the census says so and its click is nobody's - the screen
	/// stays up and no loans screen is counted. With nobody playing the game type reads as Instant Action
	/// (<see cref="Level.InstantAction"/>), so that is the same.
	/// </summary>
	/// <remarks><b>Mutations:</b> the button left enabled (the click closes the screen and counts <c>LOANS_SCREEN</c>).</remarks>
	[TestMethod]
	[DataRow( true )]
	[DataRow( null )]
	public void InInstantActionTheLoansButtonTakesNoClick( bool? instantAction )
	{
		Playing( instantAction );
		var stack = new WindowStack();
		var screen = new ParkEntryPriceScreen( stack );

		stack.Open( screen );

		StringAssert.EndsWith( screen.Census(), "loans off" );

		stack.ClickAt( LoansX, LoansY );

		Assert.AreEqual( (1, 0, 10), (stack.Windows.Count, Times( "LOANS_SCREEN" ), ParkCategoryScreens.LeftOn( ParkCategoryScreens.Finance )),
			"the screen stays up, no loans screen, the category still on the entry price" );
		Assert.AreSame( screen, stack.Windows.Single() );
	}

	/// <summary>
	/// <b>In Full Simulation the loans button is enabled</b>: its click closes the screen and goes to the loans, which are
	/// not built and are counted, and the category is left on screen 8.
	/// </summary>
	/// <remarks><b>Mutations:</b> the button disabled in every game type; disabled on the opposite test.</remarks>
	[TestMethod]
	public void InFullSimulationTheLoansButtonGoesToTheLoans()
	{
		Playing( false );
		var stack = new WindowStack();
		var screen = new ParkEntryPriceScreen( stack );

		stack.Open( screen );

		StringAssert.EndsWith( screen.Census(), "loans on" );

		stack.ClickAt( LoansX, LoansY );

		Assert.AreEqual( (0, 1, 8), (stack.Windows.Count, Times( "LOANS_SCREEN" ), ParkCategoryScreens.LeftOn( ParkCategoryScreens.Finance )) );
	}

	/// <summary>Sets who is playing: a player of that game type, or nobody for null.</summary>
	private static void Playing( bool? instantAction )
		=> typeof( Players ).GetProperty( nameof( Players.Current ) )!.SetValue( Players.Roster,
			instantAction is { } type ? new Player( 0, "Test", new PlayerFile { InstantAction = type } ) : null );

	private static int Times( string what ) => Unimplemented.Summary.FirstOrDefault( entry => entry.What == what ).Times;
}
