using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenTPW.Tests;

/// <summary>
/// VSync and the frame limit (Q19, <see cref="FramePacing"/>): the first launch's limit, the range, when the limiter paces, the
/// pace it holds on a clock of the test's own, the measured line, and opentpw.cfg's two lines. None needs the game or a window.
/// </summary>
[TestClass]
public class FramePacingTests
{
	/// <summary>A clock that moves only when read (a twentieth of a millisecond each time) or slept on, and a frame's work moved by hand.</summary>
	private sealed class FakeClock
	{
		public TimeSpan Now;
		public int Sleeps;

		public TimeSpan Read()
		{
			Now += TimeSpan.FromTicks( 500 );
			return Now;
		}

		public void Sleep( int milliseconds )
		{
			++Sleeps;
			Now += TimeSpan.FromMilliseconds( milliseconds );
		}
	}

	/// <summary>Runs <paramref name="frames"/> frames of <paramref name="work"/> each on <paramref name="clock"/>, and gives back when each began.</summary>
	private static List<TimeSpan> Run( FramePacing pacing, FakeClock clock, GameOptions options, int frames, Func<int, TimeSpan> work )
	{
		var starts = new List<TimeSpan>();
		var last = clock.Now;

		for ( var i = 0; i < frames; ++i )
		{
			pacing.Wait( options );
			var now = clock.Read();
			pacing.Count( options, now - last );
			last = now;
			starts.Add( now );
			clock.Now += work( i );
		}

		return starts;
	}

	/// <remarks><b>Mutation:</b> a first limit of a fixed 60 fails at 144; the refresh rate taken under 30 fails at 24.</remarks>
	[TestMethod]
	public void AFirstLaunchTakesTheMonitorsRefreshRate()
	{
		Assert.AreEqual( 144, FramePacing.FirstLimit( 144 ) );
		Assert.AreEqual( 75, FramePacing.FirstLimit( 75 ) );
		Assert.AreEqual( 30, FramePacing.FirstLimit( 24 ), "no lower than the range" );
		Assert.AreEqual( FramePacing.RefreshUnknown, FramePacing.FirstLimit( 0 ), "SDL says 0 where it does not know" );
		Assert.AreEqual( FramePacing.RefreshUnknown, FramePacing.FirstLimit( null ) );
		Assert.AreEqual( FramePacing.NotChosen, new GameOptions().FrameLimit, "nothing chosen until the start" );
		Assert.IsTrue( new GameOptions().VSync, "on by default, as the game always presented" );
	}

	/// <remarks><b>Mutation:</b> no floor lets 10 through; a floor on Unlimited makes it 30.</remarks>
	[TestMethod]
	public void TheLimitRunsFromThirtyToUnlimited()
	{
		Assert.AreEqual( 30, FramePacing.Clamp( 10 ) );
		Assert.AreEqual( 30, FramePacing.Clamp( 30 ) );
		Assert.AreEqual( 45, FramePacing.Clamp( 45 ) );
		Assert.AreEqual( 500, FramePacing.Clamp( 500 ) );
		Assert.AreEqual( FramePacing.Unlimited, FramePacing.Clamp( 0 ) );
		Assert.AreEqual( FramePacing.Unlimited, FramePacing.Clamp( -1 ) );
	}

	/// <remarks><b>Mutation:</b> the limiter always pacing fails the VSync rows; VSync ignored fails them too; "at or above" made "above" fails 60 at 60.</remarks>
	[TestMethod]
	public void WithVSyncOnALimitAtOrAboveTheRefreshRateIsLeftToVSync()
	{
		Assert.IsFalse( FramePacing.Limits( vsync: true, limit: 60, refreshRate: 60 ) );
		Assert.IsFalse( FramePacing.Limits( vsync: true, limit: 144, refreshRate: 60 ) );
		Assert.IsTrue( FramePacing.Limits( vsync: true, limit: 30, refreshRate: 60 ) );
		Assert.IsTrue( FramePacing.Limits( vsync: false, limit: 144, refreshRate: 60 ) );
		Assert.IsTrue( FramePacing.Limits( vsync: true, limit: 60, refreshRate: 0 ), "a refresh rate not known leaves the cap to the limiter" );
		Assert.IsFalse( FramePacing.Limits( vsync: false, limit: FramePacing.Unlimited, refreshRate: 60 ) );
	}

	/// <summary>
	/// At a limit of 30 with VSync off, frames of 5 ms of work begin a thirtieth of a second apart: the thirty-first a second after
	/// the first, within the clock's own step.
	/// </summary>
	/// <remarks><b>Mutation:</b> no wait gives 5 ms frames; the next due counted from the frame's start rather than when it was due
	/// lets each frame run late by the clock's steps (over 1 ms in 30 frames); a wait that stops a step early runs short.</remarks>
	[TestMethod]
	public void ALimitOfThirtyBeginsAFrameEveryThirtiethOfASecond()
	{
		var clock = new FakeClock();
		var pacing = new FramePacing( 60, clock.Read, clock.Sleep );
		var options = new GameOptions { VSync = false, FrameLimit = 30 };

		var starts = Run( pacing, clock, options, 31, _ => TimeSpan.FromMilliseconds( 5 ) );
		var second = starts[30] - starts[0];

		Assert.IsTrue( Math.Abs( second.TotalMilliseconds - 1000 ) < 0.2, $"30 frames took {second.TotalMilliseconds:F3} ms" );
		Assert.IsTrue( clock.Sleeps > 0, "the wait sleeps while more than two milliseconds are left" );

		for ( var i = 1; i < starts.Count; ++i )
			Assert.IsTrue( (starts[i] - starts[i - 1]).TotalMilliseconds >= 33.3, $"frame {i} began early" );
	}

	/// <summary>A frame that runs past a whole period is not made up for by rushing the next ones: the next is due a period after it.</summary>
	/// <remarks><b>Mutation:</b> the due counted on from the missed one lets the frames after a 100 ms stall run with no wait.</remarks>
	[TestMethod]
	public void AStallIsNotCaughtUpByRushedFrames()
	{
		var clock = new FakeClock();
		var pacing = new FramePacing( 60, clock.Read, clock.Sleep );
		var options = new GameOptions { VSync = false, FrameLimit = 30 };

		var starts = Run( pacing, clock, options, 6, i => TimeSpan.FromMilliseconds( i == 2 ? 100 : 5 ) );

		Assert.IsTrue( (starts[3] - starts[2]).TotalMilliseconds >= 100, "the stall itself" );
		Assert.IsTrue( (starts[4] - starts[3]).TotalMilliseconds >= 33.3, $"the frame after it waited, {(starts[4] - starts[3]).TotalMilliseconds:F3} ms" );
	}

	/// <remarks><b>Mutation:</b> the limiter pacing beside VSync sleeps here.</remarks>
	[TestMethod]
	public void VSyncAtTheRefreshRateLeavesTheLimiterIdleAndUnlimitedNeverWaits()
	{
		var clock = new FakeClock();
		var pacing = new FramePacing( 60, clock.Read, clock.Sleep );

		Run( pacing, clock, new GameOptions { VSync = true, FrameLimit = 60 }, 20, _ => TimeSpan.FromMilliseconds( 1 ) );
		Run( pacing, clock, new GameOptions { VSync = false, FrameLimit = FramePacing.Unlimited }, 20, _ => TimeSpan.FromMilliseconds( 1 ) );

		Assert.AreEqual( 0, clock.Sleeps );
	}

	/// <summary>
	/// The measured line is logged once five seconds have been measured at a setting, with the setting and the average frame, and
	/// is measured again from a change.
	/// </summary>
	/// <remarks><b>Mutation:</b> a line never logged, logged before five seconds, or not measured again after a change fails.</remarks>
	[TestMethod]
	public void TheMeasuredFrameTimeIsLoggedForEachSetting()
	{
		Log = new();
		var clock = new FakeClock();
		var pacing = new FramePacing( 60, clock.Read, clock.Sleep );
		var thirty = new GameOptions { VSync = false, FrameLimit = 30 };

		Run( pacing, clock, thirty, 140, _ => TimeSpan.FromMilliseconds( 5 ) );
		Assert.IsNull( pacing.Measured, "not before five seconds" );

		Run( pacing, clock, thirty, 20, _ => TimeSpan.FromMilliseconds( 5 ) );
		Assert.IsNotNull( pacing.Measured );
		StringAssert.Contains( pacing.Measured, "vsync off, limit 30, refresh 60 (the limiter paces)" );
		StringAssert.Contains( pacing.Measured, "33.3" );

		var fifty = new GameOptions { VSync = false, FrameLimit = 50 };
		Run( pacing, clock, fifty, 10, _ => TimeSpan.FromMilliseconds( 5 ) );
		Assert.IsNull( pacing.Measured, "a new setting is measured again" );

		Run( pacing, clock, fifty, 260, _ => TimeSpan.FromMilliseconds( 5 ) );
		StringAssert.Contains( pacing.Measured, "limit 50" );
		StringAssert.Contains( pacing.Measured, " 20.0" );
	}

	/// <summary>
	/// opentpw.cfg's <c>vsync</c> and <c>framelimit</c> lines: read, a number under 30 read as 30, a word it does not know passed
	/// over (the start then takes the monitor's rate), written back, and no <c>framelimit</c> line written before one is chosen.
	/// </summary>
	/// <remarks><b>Mutation:</b> a line not read, read as the other value, unclamped, or written in another form fails.</remarks>
	[TestMethod]
	public void TheTwoSettingsAreKeptInOpenTpwCfg()
	{
		var folder = Path.Combine( Path.GetTempPath(), $"opentpw-frames-{Guid.NewGuid():N}" );
		Directory.CreateDirectory( folder );
		var was = GameOptions.Current;
		var wasSaves = SaveFileSystemOrNull;

		try
		{
			Log = new();
			SaveFileSystem = new BaseFileSystem( folder );
			var file = Path.Combine( folder, "opentpw.cfg" );

			GameOptions.Current = new();
			File.WriteAllText( file, "vsync off\nframelimit 45\n" );
			SaveFolder.LoadDisplay();
			Assert.IsFalse( GameOptions.Current.VSync );
			Assert.AreEqual( 45, GameOptions.Current.FrameLimit );

			GameOptions.Current = new();
			File.WriteAllText( file, "vsync ON\nframelimit 10\n" );
			SaveFolder.LoadDisplay();
			Assert.IsTrue( GameOptions.Current.VSync );
			Assert.AreEqual( 30, GameOptions.Current.FrameLimit, "under the range" );

			GameOptions.Current = new();
			File.WriteAllText( file, "framelimit Unlimited\n" );
			SaveFolder.LoadDisplay();
			Assert.AreEqual( FramePacing.Unlimited, GameOptions.Current.FrameLimit );

			GameOptions.Current = new();
			File.WriteAllText( file, "framelimit fast\n" );
			SaveFolder.LoadDisplay();
			Assert.AreEqual( FramePacing.NotChosen, GameOptions.Current.FrameLimit, "passed over" );

			GameOptions.Current = new();
			SaveFolder.SaveDisplay( tell: true );
			var written = File.ReadAllText( file );
			StringAssert.Contains( written, "\nvsync on\n" );
			Assert.IsFalse( written.Contains( "\nframelimit " ), "nothing chosen, nothing written" );

			GameOptions.Current = new() { VSync = false, FrameLimit = 144 };
			SaveFolder.SaveDisplay( tell: true );
			written = File.ReadAllText( file );
			StringAssert.Contains( written, "\nvsync off\n" );
			StringAssert.Contains( written, "\nframelimit 144\n" );

			GameOptions.Current = new();
			SaveFolder.LoadDisplay();
			Assert.IsFalse( GameOptions.Current.VSync, "written off, read back" );
			Assert.AreEqual( 144, GameOptions.Current.FrameLimit, "written, read back" );

			GameOptions.Current = new() { FrameLimit = FramePacing.Unlimited };
			SaveFolder.SaveDisplay( tell: true );
			StringAssert.Contains( File.ReadAllText( file ), "\nframelimit unlimited\n" );

			var copy = new GameOptions { VSync = false, FrameLimit = 90 }.Copy();
			Assert.IsFalse( copy.VSync, "the Options screen's copy keeps them" );
			Assert.AreEqual( 90, copy.FrameLimit );
		}
		finally
		{
			GameOptions.Current = was;
			SaveFileSystemOrNull = wasSaves;
			Directory.Delete( folder, true );
		}
	}
}
