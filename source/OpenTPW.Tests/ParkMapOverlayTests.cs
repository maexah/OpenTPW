using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeoVeldrid;
using OpenTPW.UI;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;

namespace OpenTPW.Tests;

/// <summary>
/// The map's six layer switches and two pickers on the original's three words (<see cref="ParkMapOverlay"/>,
/// <c>docs/exe/hud.md</c>, "The map's overlay"): each starts at 1, a switch XORs its own bit, a pick takes its value or 0,
/// the words outlive the screen, and each button shows its "show" row (op 0x11) unless it is down, when it shows its "hide"
/// row (op 0x12, <see cref="UiButton.HelpWhenOn"/>).
/// </summary>
/// <remarks>
/// <b>Mutations</b> (each put back by hand and seen to fail): a word starting at 0; <c>|=</c> for the switch's XOR; a pick
/// that keeps its value when its press lifts it; the switches' second row left off; the opener not putting the pickers down.
/// </remarks>
[TestClass]
public class ParkMapOverlayTests
{
	private (int Layers, int Staff, int Metric) _words;
	private Point2 _screen;
	private Vector3 _poi;

	[TestInitialize]
	public void KeepTheStatics()
	{
		Log ??= new();
		_words = (ParkMapOverlay.Layers, ParkMapOverlay.Staff, ParkMapOverlay.Metric);
		_screen = Screen.Size;
		_poi = ParkOrbitCameraMode.PointOfInterest;
		Screen.Size = new Point2( 2048, 1536 );
		Input.Mouse = new();
		Unimplemented.Forget();
		(ParkMapOverlay.Layers, ParkMapOverlay.Staff, ParkMapOverlay.Metric) = (1, 1, 1);
	}

	[TestCleanup]
	public void PutThemBack()
	{
		(ParkMapOverlay.Layers, ParkMapOverlay.Staff, ParkMapOverlay.Metric) = _words;
		Screen.Size = _screen;
		ParkOrbitCameraMode.PointOfInterest = _poi;
		Input.Mouse = new();
		Input.ForgetHeldKeys();
		Unimplemented.Forget();
		UiSounds.LastClick = null;
	}

	/// <summary>
	/// <b>A fresh process opens on the rides layer, the handyman and happiness</b> (the constructor <c>0x005f0690</c> sets
	/// each word to 1, <c>0x005f06c3</c>..<c>0x005f06cd</c>). Read from a copy of the assembly loaded on its own, so no other
	/// test's map has touched it; then the opener puts down the rides switch alone, the handyman and happiness.
	/// </summary>
	[TestMethod]
	public void AFreshProcessOpensOnRidesTheHandymanAndHappiness()
	{
		var context = new AssemblyLoadContext( "fresh map words", isCollectible: true );

		try
		{
			var fresh = context.LoadFromAssemblyPath( typeof( ParkMapOverlay ).Assembly.Location ).GetType( typeof( ParkMapOverlay ).FullName! )!;
			int Word( string name ) => (int)fresh.GetProperty( name, BindingFlags.Static | BindingFlags.NonPublic )!.GetValue( null )!;

			Assert.AreEqual( (1, 1, 1), (Word( nameof( ParkMapOverlay.Layers ) ), Word( nameof( ParkMapOverlay.Staff ) ), Word( nameof( ParkMapOverlay.Metric ) )) );
		}
		finally
		{
			context.Unload();
		}

		var (_, map) = Open();
		Assert.AreEqual( "987 98e 998", Down( map ) );
		Assert.AreEqual( (0x98e, 0x998), (Group( map, 0x98b ).Selected, Group( map, 0x996 ).Selected) );
	}

	/// <summary>
	/// <b>Each switch XORs its own bit of the layer word</b> (<c>Map_Proc</c> <c>0x005f1130</c>, message <c>0x100</c>): rides 1,
	/// shops 2, sideshows 4, miscellaneous 8, tracks <c>0x10</c>, visitors <c>0x20</c>. Pressed in the stream's order from
	/// rides alone, the word climbs 3, 7, 0xf, 0x1f, 0x3f and the rides switch then takes its own bit away, 0x3e; each
	/// switch is down while its bit is set, and every press counts the unpainted overlay.
	/// </summary>
	[TestMethod]
	public void EachSwitchXorsItsOwnBit()
	{
		var (stack, map) = Open();
		Assert.AreEqual( 1, Times( "MAP_OVERLAY" ), "the opening" );

		foreach ( var (id, layers) in new[] { (0x993, 0x03), (0x994, 0x07), (0x982, 0x0f), (0x989, 0x1f), (0x983, 0x3f), (0x987, 0x3e) } )
		{
			Click( stack, map, id );
			Assert.AreEqual( layers, ParkMapOverlay.Layers, $"after 0x{id:x}" );
		}

		Assert.AreEqual( "994 993 982 989 983 98e 998", Down( map ), "sideshows stands before shops on the screen" );
		Assert.AreEqual( 7, Times( "MAP_OVERLAY" ), "and each press" );
		Assert.AreEqual( (1, 1), (ParkMapOverlay.Staff, ParkMapOverlay.Metric), "no switch touches the pickers' words" );
	}

	/// <summary>
	/// <b>A pick takes its value, and a press on the chosen one leaves none</b> (the group posts <c>0x101</c> with the button
	/// and 1 as it goes down, 0 as it comes up; the word is the value or 0). The staff values are 1 handyman, 2 mechanic,
	/// 3 guard, 4 entertainer, so the guard, below the entertainer on the screen, is 3; the metrics 1 happiness, 2
	/// excitement, 3 satisfaction, so satisfaction, the left one, is 3. Each <c>0x101</c> repaints (<c>Map_Proc</c>'s arms,
	/// <c>0x005f1406</c>..<c>0x005f141b</c>), so a pick that moves the choice counts the unpainted overlay twice, (old, 0) and
	/// (id, 1), and one from none or a lift once: 2, 2, 2, 1, 1 and 2, 2, 1, 1 after the opening's one, 15.
	/// </summary>
	[TestMethod]
	public void APickTakesItsValueAndAPressOnTheChosenOneLeavesNone()
	{
		var (stack, map) = Open();
		var staff = Group( map, 0x98b );
		var metric = Group( map, 0x996 );

		foreach ( var (id, word, chosen) in new[] { (0x98c, 3, 0x98c), (0x98d, 4, 0x98d), (0x98f, 2, 0x98f), (0x98f, 0, -1), (0x98e, 1, 0x98e) } )
		{
			Click( stack, map, id );
			Assert.AreEqual( (word, chosen), (ParkMapOverlay.Staff, staff.Selected), $"after 0x{id:x}" );
			Assert.AreEqual( chosen, staff.Children.OfType<UiButton>().SingleOrDefault( option => option.IsDown )?.Id ?? -1, $"down after 0x{id:x}" );
		}

		foreach ( var (id, word, chosen) in new[] { (0x997, 2, 0x997), (0x999, 3, 0x999), (0x999, 0, -1), (0x998, 1, 0x998) } )
		{
			Click( stack, map, id );
			Assert.AreEqual( (word, chosen), (ParkMapOverlay.Metric, metric.Selected), $"after 0x{id:x}" );
			Assert.AreEqual( chosen, metric.Children.OfType<UiButton>().SingleOrDefault( option => option.IsDown )?.Id ?? -1, $"down after 0x{id:x}" );
		}

		Assert.AreEqual( 1, ParkMapOverlay.Layers, "no pick touches the layer word" );
		Assert.AreEqual( 15, Times( "MAP_OVERLAY" ), "a repaint for each 0x101" );
	}

	/// <summary>
	/// <b>Every one of the map's 13 buttons clicks <c>BUTTON01</c> (31), not <c>Select3</c> (189)</b>: the click hook picks the
	/// sound by the receiver's flag <c>0x10</c> (<c>0x004857cb</c>), and the switches tell the root <c>0x980</c> and the picks
	/// their groups <c>0x98b</c> and <c>0x996</c>, all three flags <c>0x1</c> (stream <c>0x00774da0</c>; <c>docs/exe/hud.md</c>,
	/// "Which window the click sound is for"). The tabs' groups, flags <c>0x11</c>, play 189 (<c>ParkListScreenRewriteTests</c>).
	/// </summary>
	/// <remarks><b>Mutations:</b> the button's own <c>Toggles</c> read for the sound (every one 189); any group's option taken for
	/// <c>Select3</c> (the picks 189).</remarks>
	[TestMethod]
	public void EveryButtonOfTheMapClicksButton01()
	{
		var (stack, map) = Open();
		var clicked = new List<string>();

		foreach ( var id in new[] { 0x987, 0x994, 0x993, 0x982, 0x989, 0x983, 0x98e, 0x98f, 0x98d, 0x98c, 0x999, 0x998, 0x997 } )
		{
			UiSounds.LastClick = null;
			Click( stack, map, id );
			clicked.Add( $"{id:x} {UiSounds.LastClick?.ToString() ?? "none"}" );
		}

		CollectionAssert.AreEqual( clicked.Select( line => line[..3] + " 31" ).ToList(), clicked, string.Join( "; ", clicked ) );
	}

	/// <summary>
	/// <b>The words outlive the screen</b> (the map's static, <c>0x00f86db8</c>): left at tracks and visitors, the guard and
	/// no metric, the next opening puts down those two switches and the guard, and no metric.
	/// </summary>
	[TestMethod]
	public void TheWordsAreKeptAcrossOpenings()
	{
		var (stack, map) = Open();

		foreach ( var id in new[] { 0x987, 0x989, 0x983, 0x98c, 0x998 } )
			Click( stack, map, id );

		Assert.AreEqual( "layers 0x30 staff 3 metric 0", ParkMapOverlay.Census );
		stack.Close( map );

		map = new ParkMapScreen( stack );
		stack.Open( map );
		Assert.AreEqual( "989 983 98c", Down( map ) );
		Assert.AreEqual( (0x98c, -1), (Group( map, 0x98b ).Selected, Group( map, 0x996 ).Selected) );
	}

	/// <summary>
	/// <b>A switch shows its "hide" row while it is down</b> (the button class's help getter <c>0x00669410</c>, the rule in
	/// <see cref="UiButton.ShownHelp"/>): the rides switch, down, 268 "hide rides", held down too (the pointer coming on posts
	/// before the held bit is set); let go up, the release posts 267 under the pointer, and down again 268. A pick the same: the
	/// chosen handyman 280, the mechanic 281. A button given no second row keeps its one.
	/// </summary>
	[TestMethod]
	public void ASwitchShowsItsHideRowWhileItIsDown()
	{
		var (stack, map) = Open();
		var rides = (UiButton)Find( map, 0x987 );
		Assert.AreEqual( 268, rides.ShownHelp, "down" );

		rides.Pressed = true;
		Assert.AreEqual( 268, rides.ShownHelp, "held down" );
		rides.Pressed = false;

		Click( stack, map, 0x987 );
		Assert.AreEqual( (267, 267), (rides.ShownHelp, stack.HelpRow), "let go up: the release posts the show row" );

		Click( stack, map, 0x987 );
		Assert.AreEqual( (268, 268), (rides.ShownHelp, stack.HelpRow), "down again" );

		Assert.AreEqual( (280, 281), (Find( map, 0x98e ).ShownHelp, Find( map, 0x98f ).ShownHelp) );
		Assert.AreEqual( 5, new UiButton { HelpText = 5, IsDown = true }.ShownHelp, "one row" );
	}

	private static (WindowStack Stack, ParkMapScreen Map) Open()
	{
		var stack = ParkEscapeOnReleaseTests.APark();
		var map = new ParkMapScreen( stack );
		stack.Open( map );
		return (stack, map);
	}

	private static void Click( WindowStack stack, ParkMapScreen map, int id )
	{
		var rect = Find( map, id ).Rect;
		Assert.IsTrue( stack.ClickAt( (rect.Left + rect.Right) / 2, (rect.Top + rect.Bottom) / 2 ), $"0x{id:x} clicked" );
	}

	/// <summary>The ids of the switches and options that are down, in the screen's order.</summary>
	private static string Down( ParkMapScreen map )
		=> string.Join( " ", All( map.Root ).OfType<UiButton>().Where( button => button.Toggles && button.IsDown ).Select( button => $"{button.Id:x}" ) );

	private static UiRadioGroup Group( ParkMapScreen map, int id ) => (UiRadioGroup)Find( map, id );

	private static UiControl Find( ParkMapScreen map, int id ) => All( map.Root ).Single( control => control.Id == id );

	private static System.Collections.Generic.IEnumerable<UiControl> All( UiControl control )
		=> control.Children.SelectMany( All ).Prepend( control );

	private static int Times( string what ) => Unimplemented.Summary.FirstOrDefault( gap => gap.What == what ).Times;
}
