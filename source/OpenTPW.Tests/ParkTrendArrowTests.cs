using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace OpenTPW.Tests;

/// <summary>
/// The money's trend arrow, the gadget's <c>0x31</c>, and the currency icon's sign, <c>0x32</c>
/// (<c>docs/exe/hud.md</c>, "The money's trend arrow"): which arrow the rings call for and when they are read, and where
/// the arrow stands after the balance's lettering. The gadget's tests read its meshes and fonts from the shipped game and
/// are skipped without it - see <see cref="GameData"/>.
/// </summary>
[TestClass]
public class ParkTrendArrowTests
{
	[TestInitialize]
	public void HaveALog() => Log ??= new();

	/// <summary>
	/// <b>The arrow is red while the newest CLOSED month's cash in is below its costs</b> (<c>0x004a0f8a</c>, signed): a park
	/// with no month closed reads the same stand-in for both and is green; the month so far counts for nothing until it
	/// closes; cash in one below the costs is red, equal is green; the reading stands until the rings are read again.
	/// </summary>
	/// <remarks><b>Mutations:</b> the month so far read in place of the newest; at or below for red; the reading taken
	/// whenever asked rather than when read.</remarks>
	[TestMethod]
	public void TheArrowIsRedWhileTheNewestClosedMonthIsALoss()
	{
		var state = new ParkState( parkIsClosed: false, visitorsToDate: 0 );
		Assert.IsFalse( state.MonthInLoss, "no month closed: green" );

		state.MonthCosts.Add( 13175 );
		state.MonthCashIn.Add( 1500 );
		state.ReadTheTrend();
		Assert.IsFalse( state.MonthInLoss, "the month so far is not read" );

		state.MonthCosts.Close();
		state.MonthCashIn.Close();
		Assert.IsFalse( state.MonthInLoss, "closed but not read again" );

		state.ReadTheTrend();
		Assert.IsTrue( state.MonthInLoss, "1500 in against 13175 of costs" );

		state.MonthCosts.Add( 700 );
		state.MonthCashIn.Add( 700 );
		state.MonthCosts.Close();
		state.MonthCashIn.Close();
		state.ReadTheTrend();
		Assert.IsFalse( state.MonthInLoss, "equal is green" );

		state.MonthCosts.Add( 700 );
		state.MonthCashIn.Add( 699 );
		state.MonthCosts.Close();
		state.MonthCashIn.Close();
		state.ReadTheTrend();
		Assert.IsTrue( state.MonthInLoss, "one below is red" );
	}

	/// <summary>
	/// <b>The rings are read once a day, after the day's change and before the month's</b> (<c>0x004f83ac</c>): a February
	/// that took 20,000 against February's costs of 500, after a January closed in loss, leaves the arrow red on 1 March,
	/// when February closes after the reading, and green on 2 March. Driven through <see cref="Level.TurnTheCalendar"/>
	/// tick by tick from the epoch.
	/// </summary>
	/// <remarks><b>Mutations:</b> the reading after the month's change; no reading at the day's change.</remarks>
	[TestMethod]
	public void TheFirstOfAMonthStillShowsTheMonthBeforeLast()
	{
		GameCalendar.Rebase();

		try
		{
			var state = new ParkState( parkIsClosed: false, visitorsToDate: 0 );
			var shown = new Dictionary<string, bool>();

			for ( var tick = 1; GameCalendar.Now < new System.DateTime( 2000, 3, 3 ); ++tick )
			{
				// January's money, then February's, each put in the month before it closes.
				if ( GameCalendar.Now.Date == new System.DateTime( 2000, 1, 20 ) && state.MonthCosts.Total == 0 )
				{
					state.MonthCosts.Add( 900 );
					state.MonthCashIn.Add( 100 );
				}

				if ( GameCalendar.Now.Date == new System.DateTime( 2000, 2, 20 ) && state.MonthCashIn.Total == 0 )
				{
					state.MonthCosts.Add( 500 );
					state.MonthCashIn.Add( 20000 );
				}

				state.SetGameTick( tick );
				Level.TurnTheCalendar( state, null, null, _ => { } );

				if ( GameCalendar.DayRolled )
					shown[GameCalendar.Now.ToString( "MM-dd" )] = state.MonthInLoss;
			}

			Assert.IsFalse( shown["01-31"], "no month closed in January" );
			Assert.IsFalse( shown["02-01"], "January closes after the 1st's reading" );
			Assert.IsTrue( shown["02-02"], "January's loss from the 2nd" );
			Assert.IsTrue( shown["03-01"], "still January's on 1 March" );
			Assert.IsFalse( shown["03-02"], "February's profit from the 2nd" );
		}
		finally
		{
			GameCalendar.Rebase();
		}
	}

	/// <summary>
	/// <b>The stock park loads red</b>: its file's closed month took 1,500 against 13,175 of costs, and the load reads the
	/// rings (<c>FUN_004815e0</c>), so the gadget built on it shows the second part, <c>red_down</c>.
	/// </summary>
	/// <remarks><b>Mutations:</b> no reading at the load.</remarks>
	[TestMethod]
	public void TheStockParkLoadsRed()
	{
		var world = Stock();

		WithAGadget( world, new ParkState( world ), gadget => Assert.AreEqual( 1, Find( gadget.Root, 0x31 ).Frame ) );
		WithAGadget( world, new ParkState( parkIsClosed: false, visitorsToDate: 0 ), gadget => Assert.AreEqual( 0, Find( gadget.Root, 0x31 ).Frame ) );
	}

	/// <summary>
	/// <b>The arrow's left edge is the lettering's end, its top and size kept, and the icon carries the sign</b>
	/// (<c>Gadget_ShowMoney</c> <c>0x004a0ab0</c>): on the interface's own 2048 x 1536 the set is the 768 one (CASHBIG,
	/// two units a pixel), so "5" ends at 258 + 2 x its advance and "123456789" 2 x the nine advances on; -500 is lettered
	/// "500" with <c>dollarsignneg</c>, part 1 of <c>i_dollar</c>, and the arrow after "500". The arrow is 0x2f's child
	/// (<c>0x004a0f05</c>), not the root's.
	/// </summary>
	/// <remarks><b>Mutations:</b> the arrow left where the stream puts it; placed by the font's pixels, not the screen's;
	/// its width not kept; the balance lettered with its sign; the icon's part not set, or set at nought; the arrow added to
	/// the root, not to 0x2f.</remarks>
	[TestMethod]
	public void TheArrowFollowsTheLetteringAndTheIconCarriesTheSign()
	{
		var world = Stock();

		foreach ( var (balance, text, end, icon) in new[] { (5, "5", 258 + 2 * Five, 0), (123456789, "123456789", 258 + 2 * NineDigits, 0),
			(0, "0", 258 + 2 * Nought, 0), (-500, "500", 258 + 2 * FiveHundred, 1) } )
		{
			WithAGadget( world, new ParkState( parkIsClosed: false, visitorsToDate: 0, balance: balance ), gadget =>
			{
				Assert.AreEqual( text, Find( gadget.Root, 0x2f ).Text, $"{balance}'s lettering" );
				CollectionAssert.Contains( Find( gadget.Root, 0x2f ).Children.ToList(), Find( gadget.Root, 0x31 ), "0x31 is a child of 0x2f" );
				Assert.AreEqual( new UI.UiRect( end, 109, end + 103, 211 ), Find( gadget.Root, 0x31 ).Rect, $"{balance}'s arrow" );
				Assert.AreEqual( icon, Find( gadget.Root, 0x32 ).Frame, $"{balance}'s icon" );
			} );
		}
	}

	/// <summary>
	/// <b>The set the lettering was measured in is the screen's, not a constant</b>: at 800 x 600 the set is the 600 one, and
	/// CASHBIG's advances are laid at 1536 / 600 = 2.56 units a pixel, not the 768 set's 2, so "5" ends at 258 + 69 = 327 and
	/// the nine digits at 258 + 629 = 887. A 768 held fixed puts them at 312 and 750, the true distance 1.28 times the wrong
	/// one's for "5".
	/// </summary>
	/// <remarks><b>Mutations:</b> <c>SetScreenHeight</c> replaced by 768 in <c>ShowMoney</c>.</remarks>
	[TestMethod]
	public void TheArrowIsPlacedForTheSetTheScreenCallsFor()
	{
		var world = Stock();

		foreach ( var (balance, end) in new[] { (5, 258 + 27 * 1536 / 600), (123456789, 258 + 246 * 1536 / 600) } )
		{
			WithAGadget( world, new ParkState( parkIsClosed: false, visitorsToDate: 0, balance: balance ), gadget =>
			{
				Assert.AreEqual( 600, UI.UiFonts.SetScreenHeight );
				Assert.AreEqual( new UI.UiRect( end, 109, end + 103, 211 ), Find( gadget.Root, 0x31 ).Rect, $"{balance}'s arrow at 800 x 600" );
			}, new Point2( 800, 600 ) );
		}
	}

	/// <summary><b>The magnitude is lettered as the original takes it</b>, <c>(x ^ s) - s</c>, which leaves the least int itself.</summary>
	/// <remarks><b>Mutations:</b> <c>Math.Abs</c>, which throws there.</remarks>
	[TestMethod]
	public void TheLeastIntIsLetteredAsItIs()
	{
		Assert.AreEqual( 500, UI.ParkGadget.Unsigned( -500 ) );
		Assert.AreEqual( 0, UI.ParkGadget.Unsigned( 0 ) );
		Assert.AreEqual( int.MinValue, UI.ParkGadget.Unsigned( int.MinValue ) );
	}

	/// <summary>
	/// <b>A lettering's end is cut toward nought in the screen's units</b>: 100 font pixels of the 480 set (3.2 units a
	/// pixel) end 320 on, 7 of the 600 set (2.56) 17 on, not 18.
	/// </summary>
	[TestMethod]
	public void ALetteringsEndIsCutTowardNought()
	{
		Assert.AreEqual( 578, UI.ParkGadget.LetteringEnd( 258, 100, 480 ) );
		Assert.AreEqual( 275, UI.ParkGadget.LetteringEnd( 258, 7, 600 ) );
	}

	// The pen's advances in CASHBIG.bf4, the balance's font in the 768 set, in its pixels: "5", the nine digits 1 to 9, "0", "500".
	private const int Five = 27;
	private const int NineDigits = 246;
	private const int Nought = 29;
	private const int FiveHundred = 85;

	private static ParkWorld Stock()
	{
		Log ??= new();
		FileSystem = GameData.Required();

		using var stream = new System.IO.MemoryStream( FileSystem.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		var reader = new SaveReader( stream );

		return new ParkWorld( reader.ReadFile(), reader.Preamble );
	}

	/// <summary>A gadget built on its own level of <paramref name="world"/> and <paramref name="state"/>, on the interface's own 2048 x 1536 unless <paramref name="size"/> says.</summary>
	private static void WithAGadget( ParkWorld world, ParkState state, System.Action<UI.ParkGadget> check, Point2? size = null )
	{
		var (screen, level) = (Screen.Size, Level.Current);

		try
		{
			Screen.Size = size ?? new Point2( 2048, 1536 );
			Level.Current = RunningLevel( world, state );
			check( new UI.ParkGadget( AStack() ) );
		}
		finally
		{
			Screen.Size = screen;
			Level.Current = level;
		}
	}

	/// <summary>A level of its own for the gadget to read: the park's file and its running state.</summary>
	private static Level RunningLevel( ParkWorld world, ParkState state )
	{
		var level = (Level)RuntimeHelpers.GetUninitializedObject( typeof( Level ) );

		typeof( Level ).GetProperty( nameof( Level.Kind ) )!.SetValue( level, Level.Scene.Park );
		typeof( Level ).GetProperty( nameof( Level.ThemeName ) )!.SetValue( level, "jungle" );
		typeof( Level ).GetProperty( nameof( Level.Park ) )!.SetValue( level, world );
		typeof( Level ).GetProperty( nameof( Level.ParkState ) )!.SetValue( level, state );

		return level;
	}

	private static UI.UiControl Find( UI.UiControl control, int id )
		=> control.Id == id ? control : control.Children.Select( child => Find( child, id ) ).FirstOrDefault( found => found != null )!;

	/// <summary>A window stack with no windows, made without its constructor.</summary>
	private static UI.WindowStack AStack()
	{
		var stack = (UI.WindowStack)RuntimeHelpers.GetUninitializedObject( typeof( UI.WindowStack ) );

		typeof( UI.WindowStack ).GetField( "_windows", BindingFlags.Instance | BindingFlags.NonPublic )!
			.SetValue( stack, new List<UI.UiWindow>() );

		return stack;
	}
}
