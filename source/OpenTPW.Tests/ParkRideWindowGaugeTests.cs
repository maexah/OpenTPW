using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenTPW.UI;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace OpenTPW.Tests;

/// <summary>
/// The ride window's Excitement (<c>0x3e16</c>) and Reliability (<c>0x3e18</c>) gauges, filled from the window's own
/// sliders (<c>docs/exe/park-engine.md</c>, "The object window's stats panel"). The stock park's Belly Bounce, thing 13:
/// speed 60, capacity 5, duration 30, its tier's starting speed and duration, Instant Action's wear rate of 3.
/// </summary>
[TestClass]
public class ParkRideWindowGaugeTests
{
	private const int BellyBounce = 13;

	private Level? before;
	private Point2 size;
	private ParkState state = null!;
	private ParkObjectWindow window = null!;
	private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

	[TestInitialize]
	public void OpenWindow()
	{
		before = Level.Current;
		size = Screen.Size;
		Screen.Size = new( 2048, 1536 );
		Log ??= new();
		FileSystem = GameData.Required();
		using var stream = new MemoryStream( FileSystem.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		var world = new ParkWorld( new SaveReader( stream ).ReadFile() );
		state = new( world );
		var catalogue = new ParkItemCatalogue( "jungle", FileSystem, instantAction: true );
		var level = (Level)RuntimeHelpers.GetUninitializedObject( typeof( Level ) );
		typeof( Level ).GetProperty( nameof( Level.Park ) )!.SetValue( level, world );
		typeof( Level ).GetProperty( nameof( Level.ParkState ) )!.SetValue( level, state );
		typeof( Level ).GetProperty( nameof( Level.Catalogue ) )!.SetValue( level, catalogue );
		Level.Current = level;
		var stack = (WindowStack)RuntimeHelpers.GetUninitializedObject( typeof( WindowStack ) );
		typeof( WindowStack ).GetField( "_windows", Private )!.SetValue( stack, new List<UiWindow>() );
		window = new( stack, BellyBounce );
	}

	[TestCleanup]
	public void PutBack()
	{
		Level.Current = before!;
		Screen.Size = size;
	}

	private static UiControl? Find( UiControl root, int id )
		=> root.Id == id ? root : root.Children.Select( c => Find( c, id ) ).FirstOrDefault( c => c != null );

	private ParkObjectWindow.UiStatBar Gauge( int id ) => (ParkObjectWindow.UiStatBar)Find( window.Root, id )!;

	private UiSlider[] Sliders => (UiSlider[])typeof( ParkObjectWindow ).GetField( "_sliders", Private )!.GetValue( window )!;

	private void Fill() => typeof( ParkObjectWindow ).GetMethod( "FillStats", Private )!.Invoke( window, null );

	private ParkWorld.CatalogueObject Bounce => state.Objects.Single( thing => thing.ThingId == BellyBounce );

	/// <summary>
	/// At its own settings the Bounce's excitement is its level, 40, and its reliability 82: each handed to its gauge
	/// as <c>((v &amp; 0xff) &lt;&lt; 10) / 100</c>.
	/// </summary>
	/// <remarks><b>Mutation:</b> the gauges left unfilled, or the reliability truncated as one (81, 829 units).</remarks>
	[TestMethod]
	public void TheBouncesGaugesShowItsExcitementAndReliabilityAsItOpens()
	{
		Assert.AreEqual( (5, 30, 60), (Bounce.OperatingCapacity, Bounce.OperatingDuration, Bounce.OperatingSpeed), "the stock park" );

		Assert.IsTrue( Gauge( 0x3e16 ).Known, "excitement filled" );
		Assert.AreEqual( 409, Gauge( 0x3e16 ).Units, "excitement 40" );
		Assert.IsTrue( Gauge( 0x3e18 ).Known, "reliability filled" );
		Assert.AreEqual( 839, Gauge( 0x3e18 ).Units, "reliability 82" );
	}

	/// <summary>
	/// The sliders moved and not committed - speed 45, capacity 2, duration 45: excitement is 40 × 0.75 × 1.25, 37, and
	/// reliability 90, at the next fill, while the thing keeps its own settings.
	/// </summary>
	/// <remarks><b>Mutation:</b> either gauge worked out from the thing's settings (409, 839 units).</remarks>
	[TestMethod]
	public void TheGaugesFollowTheSlidersBeforeAnythingIsCommitted()
	{
		Sliders[0].SetValue( 45 );
		Sliders[1].SetValue( 2 );
		Sliders[2].SetValue( 45 );

		Fill();

		Assert.AreEqual( 378, Gauge( 0x3e16 ).Units, "excitement 37" );
		Assert.AreEqual( 921, Gauge( 0x3e18 ).Units, "reliability 90" );
		Assert.AreEqual( (5, 30, 60), (Bounce.OperatingCapacity, Bounce.OperatingDuration, Bounce.OperatingSpeed), "nothing committed" );
	}
}
