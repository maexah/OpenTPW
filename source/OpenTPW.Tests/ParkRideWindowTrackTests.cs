using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenTPW.UI;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;

namespace OpenTPW.Tests;

/// <summary>
/// The ride window's <c>b_track</c> (<c>0x3e2a</c>) is shown only for a ride that runs on a track: <c>FUN_004ad4e0</c> sets its
/// visibility to the item's <c>Bumper.WhichTrackType</c> not nought (<c>0x004ad648</c>..<c>0x004ad667</c>;
/// <c>docs/exe/park-engine.md</c>, "The ride window's verbs, shown and hidden").
/// </summary>
[TestClass]
public class ParkRideWindowTrackTests
{
	/// <summary>Nought, no track, hides it; each of the three track types shows it.</summary>
	/// <remarks><b>Mutations:</b> always shown; shown for the coaster alone (<c>== 3</c>); for the karts alone (<c>== 1</c>); above 1.</remarks>
	[TestMethod]
	[DataRow( 0, false )]
	[DataRow( ItemDescriptionFile.CarTrack, true )]
	[DataRow( ItemDescriptionFile.WaterTrack, true )]
	[DataRow( ItemDescriptionFile.CoasterTrack, true )]
	public void OnlyARideOnATrackShowsTheTrackButton( int trackType, bool shown )
		=> Assert.AreEqual( shown, ParkObjectWindow.ShowsTheTrack( trackType ) );
}

/// <summary>
/// The same rule through the window itself, over the shipped park and the jungle's items; skipped without the game
/// (<see cref="GameData"/>).
/// </summary>
[TestClass]
public class ParkRideWindowTrackWindowTests
{
	private Level? _level;
	private Point2 _screen;
	private ParkState _state = null!;
	private ParkItemCatalogue _catalogue = null!;
	private WindowStack _stack = null!;

	[TestInitialize]
	public void LoadThePark()
	{
		_level = Level.Current;
		_screen = Screen.Size;

		Screen.Size = new Point2( 2048, 1536 );
		Log ??= new();
		FileSystem = GameData.Required();

		using var stream = new MemoryStream( FileSystem.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		var world = new ParkWorld( new SaveReader( stream ).ReadFile() );
		_catalogue = new ParkItemCatalogue( "jungle", FileSystem );
		_state = new ParkState( world );

		var level = (Level)RuntimeHelpers.GetUninitializedObject( typeof( Level ) );
		typeof( Level ).GetProperty( nameof( Level.Park ) )!.SetValue( level, world );
		typeof( Level ).GetProperty( nameof( Level.ParkState ) )!.SetValue( level, _state );
		typeof( Level ).GetProperty( nameof( Level.Catalogue ) )!.SetValue( level, _catalogue );
		Level.Current = level;

		_stack = new WindowStack();
	}

	[TestCleanup]
	public void PutItAllBack()
	{
		Entity.ApplyDeletions();
		Level.Current = _level!;
		Screen.Size = _screen;
	}

	/// <summary>
	/// <b>A flat ride's window leaves the track's place empty, and a kart, water or coaster ride's shows it.</b> The stock
	/// park's thing 13 is a flat ride; a Dino Karts (1150), a Splish Splash (1160) and the jungle's first coaster are added
	/// beside it, and the one window is shown each in turn, as its cycle arrows would, ending on the flat ride again. A
	/// hidden button takes no click: the pointer at its middle finds the window under it instead.
	/// </summary>
	/// <remarks><b>Mutations:</b> the button never hidden; never shown again once hidden (set only when hiding).</remarks>
	[TestMethod]
	public void TheWindowHidesTheTrackButtonForARideWithNoTrack()
	{
		Assert.IsTrue( _state.TryObject( 13, out var flat ), "the stock park's thing 13" );
		Assert.IsTrue( _catalogue.TryGet( flat.CatalogueId, out var flatItem ), "and its item" );
		Assert.AreEqual( 0, flatItem.TrackType, $"thing 13 ({flatItem.Name}) runs on no track" );

		var coaster = Enumerable.Range( 1000, 700 )
			.Select( id => _catalogue.TryGet( id, out var item ) ? item : default )
			.First( item => item.Id != 0 && item.TrackType == ItemDescriptionFile.CoasterTrack && item.ResearchCategory != ParkResearch.Upgrade );

		var tracked = new[] { 1150, 1160, coaster.Id }.Select( id =>
		{
			var thing = new ParkWorld.CatalogueObject( _state.NextThingId(), id, 60 << 8, 60 << 8, 0 );
			_state.AddObject( thing );
			return (thing.ThingId, Item: id);
		} ).ToArray();

		var window = new ParkObjectWindow( _stack, 13 );
		_stack.Open( window );

		var button = Find( window.Root, 0x3e2a );
		var middle = (X: (button.Rect.Left + button.Rect.Right) / 2f, Y: (button.Rect.Top + button.Rect.Bottom) / 2f);

		Assert.IsFalse( button.Visible, "a flat ride's window hides b_track" );
		Assert.AreNotSame( button, window.Root.HitTest( middle.X, middle.Y ), "and a click there does not reach it" );

		foreach ( var id in new[] { 0x3e2b, 0x3e2c, 0x3e34, 0x3e37, 0x3e36, 0x3e35, 0x3e38 } )
			Assert.IsTrue( Find( window.Root, id ).Visible, $"verb 0x{id:x} stays shown" );

		foreach ( var (thingId, item) in tracked )
		{
			window.Show( thingId );
			Assert.IsTrue( button.Visible, $"item {item}'s window shows b_track" );
			Assert.AreSame( button, window.Root.HitTest( middle.X, middle.Y ), $"item {item}: and takes the click" );
		}

		window.Show( 13 );
		Assert.IsFalse( button.Visible, "back on the flat ride, it is hidden again" );
	}

	private static UiControl Find( UiControl root, int id )
		=> FindOrNull( root, id ) ?? throw new AssertFailedException( $"no control 0x{id:x}" );

	private static UiControl? FindOrNull( UiControl root, int id )
		=> root.Id == id ? root : root.Children.Select( child => FindOrNull( child, id ) ).FirstOrDefault( found => found != null );
}
