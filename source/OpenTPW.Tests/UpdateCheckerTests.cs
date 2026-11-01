using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenTPW.UI;

namespace OpenTPW.Tests;

/// <summary>
/// The update check (Q941): which release a reply from GitHub makes the offer, when a build asks at all, what a failure does,
/// the link, the off switch, and the two lines that show the offer. No test reaches GitHub: a reply is given by a handler of the
/// test's own.
/// </summary>
[TestClass]
public class UpdateCheckerTests
{
	private const string Installed = "weekly-2026-11-01";

	private const string Rid = "linux-x64";

	/// <summary>One release of a reply, holding the zips of the platforms given.</summary>
	private static string Release( string tag, bool draft = false, params string[] rids )
	{
		var assets = string.Join( ",", rids.Select( rid => $$"""{"name":"{{UpdateChecker.AssetName( tag, rid )}}"}""" ) );
		return $$"""{"tag_name":"{{tag}}","draft":{{(draft ? "true" : "false")}},"prerelease":true,"assets":[{{assets}}]}""";
	}

	private static string Reply( params string[] releases ) => "[" + string.Join( ",", releases ) + "]";

	[TestMethod]
	public void TheSameBuildIsUpToDate()
	{
		var outcome = UpdateChecker.Pick( Reply( Release( Installed, false, Rid ) ), Installed, Rid );

		Assert.IsNull( outcome.Offer );
		Assert.IsFalse( outcome.Failed, "a reply with nothing newer is an answer, not a failure" );
	}

	[TestMethod]
	public void ANewerBuildIsOffered()
	{
		var outcome = UpdateChecker.Pick( Reply( Release( "weekly-2026-11-08", false, Rid ) ), Installed, Rid );

		Assert.AreEqual( "weekly-2026-11-08", outcome.Offer?.Tag );
	}

	[TestMethod]
	public void AnOlderBuildIsNotOffered()
	{
		Assert.IsNull( UpdateChecker.Pick( Reply( Release( "weekly-2026-10-25", false, Rid ) ), Installed, Rid ).Offer );
	}

	/// <summary>The newest wins wherever it stands in the list, and an older one before it does not hide it.</summary>
	[TestMethod]
	public void OfSeveralTheNewestWins()
	{
		var reply = Reply(
			Release( "weekly-2026-11-08", false, Rid ),
			Release( "weekly-2026-12-06", false, Rid ),
			Release( "weekly-2026-11-15", false, Rid ),
			Release( "weekly-2026-10-25", false, Rid ) );

		Assert.AreEqual( "weekly-2026-12-06", UpdateChecker.Pick( reply, Installed, Rid ).Offer?.Tag );
	}

	/// <summary>Dated by the calendar, not by the text: a tag in the next year is newer though it sorts the same way, and one a month on beats ten days on.</summary>
	[TestMethod]
	public void NewerIsByDate()
	{
		Assert.AreEqual( "weekly-2027-01-03", UpdateChecker.Pick( Reply( Release( "weekly-2027-01-03", false, Rid ) ), "weekly-2026-12-27", Rid ).Offer?.Tag );
		Assert.IsNull( UpdateChecker.Pick( Reply( Release( "weekly-2026-12-27", false, Rid ) ), "weekly-2027-01-03", Rid ).Offer );
	}

	[TestMethod]
	public void ADraftIsNotOffered()
	{
		Assert.IsNull( UpdateChecker.Pick( Reply( Release( "weekly-2026-11-08", true, Rid ) ), Installed, Rid ).Offer );
		Assert.AreEqual( "weekly-2026-11-08", UpdateChecker.Pick( Reply( Release( "weekly-2026-11-15", true, Rid ), Release( "weekly-2026-11-08", false, Rid ) ), Installed, Rid ).Offer?.Tag,
			"a newer draft does not hide the published one under it" );
	}

	[TestMethod]
	public void ADryRunReleaseIsNotOffered()
	{
		Assert.IsNull( UpdateChecker.Pick( Reply( Release( "weekly-test-3", false, Rid ) ), Installed, Rid ).Offer );
		Assert.IsNull( UpdateChecker.Pick( Reply( Release( "v2.0", false, Rid ) ), Installed, Rid ).Offer, "any other tag" );
	}

	[TestMethod]
	public void ATagWithABadDateIsNotOffered()
	{
		foreach ( var tag in new[] { "weekly-2026-13-01", "weekly-2026-02-30", "weekly-2026-00-10", "weekly-2026-1-8", "weekly-2026-11-08-x", "xweekly-2026-11-08", "weekly-٢٠٢٦-١١-٠٨" } )
			Assert.IsNull( UpdateChecker.Pick( Reply( Release( tag, false, Rid ) ), Installed, Rid ).Offer, tag );
	}

	[TestMethod]
	public void AReleaseWithoutThisPlatformsZipIsNotOffered()
	{
		var reply = Reply( Release( "weekly-2026-11-08", false, "win-x64", "osx-arm64" ), Release( "weekly-2026-11-15", false ) );

		Assert.IsNull( UpdateChecker.Pick( reply, Installed, Rid ).Offer );
		Assert.AreEqual( "weekly-2026-11-08", UpdateChecker.Pick( reply, Installed, "win-x64" ).Offer?.Tag, "the same reply offers it to the platform it holds" );
	}

	/// <summary>The link is made from the tag that was checked; the reply's own address, whatever it says, is never read.</summary>
	[TestMethod]
	public void TheLinkIsBuiltFromTheCheckedTag()
	{
		var hostile = """[{"tag_name":"weekly-2026-11-08","draft":false,"html_url":"https://evil.example/x","url":"https://evil.example/y","assets":[{"name":"OpenTPW-weekly-2026-11-08-linux-x64.zip","browser_download_url":"https://evil.example/z"}]}]""";
		var offer = UpdateChecker.Pick( hostile, Installed, Rid ).Offer;

		Assert.AreEqual( "https://github.com/maexah/OpenTPW/releases/tag/weekly-2026-11-08", offer?.Url );
	}

	[TestMethod]
	public void AnEmptyOrMalformedReplyIsAFailureThatOffersNothing()
	{
		Assert.IsNull( UpdateChecker.Pick( "[]", Installed, Rid ).Offer, "no releases" );
		Assert.IsFalse( UpdateChecker.Pick( "[]", Installed, Rid ).Failed );

		foreach ( var bad in new[] { "", "not json", "{", "{\"message\":\"Not Found\"}", "null", "42", "[1,\"a\",null,{\"tag_name\":7},{\"tag_name\":\"weekly-2026-11-08\",\"draft\":false,\"assets\":5}]" } )
			Assert.IsNull( UpdateChecker.Pick( bad, Installed, Rid ).Offer, bad );

		Assert.IsTrue( UpdateChecker.Pick( "not json", Installed, Rid ).Failed );
		Assert.IsTrue( UpdateChecker.Pick( "{\"message\":\"Not Found\"}", Installed, Rid ).Failed );
	}

	[TestMethod]
	public void OnlyAWeeklyBuildAsks()
	{
		Assert.IsTrue( UpdateChecker.Asks( true, Installed, out _ ) );

		foreach ( var build in new[] { "local", "local - 01850bd3", "weekly-test-4", "unknown", "", "weekly-2026-13-40" } )
		{
			Assert.IsFalse( UpdateChecker.Asks( true, build, out var why ), build );
			StringAssert.Contains( why, "not a weekly one" );
		}

		Assert.IsFalse( UpdateChecker.Asks( false, Installed, out var off ), "the setting off" );
		StringAssert.Contains( off, "updatecheck off" );
	}

	[TestMethod]
	public void ThePlatformsAreTheZipsNames()
	{
		Assert.AreEqual( "win-x64", UpdateChecker.PlatformRid( true, false, false, Architecture.X64 ) );
		Assert.AreEqual( "linux-x64", UpdateChecker.PlatformRid( false, true, false, Architecture.X64 ) );
		Assert.AreEqual( "osx-arm64", UpdateChecker.PlatformRid( false, false, true, Architecture.Arm64 ) );
		Assert.AreEqual( "osx-x64", UpdateChecker.PlatformRid( false, false, true, Architecture.X64 ) );
		Assert.IsNull( UpdateChecker.PlatformRid( false, true, false, Architecture.Arm64 ), "no zip for Linux on ARM" );
		Assert.IsNull( UpdateChecker.PlatformRid( true, false, false, Architecture.Arm64 ), "no zip for Windows on ARM" );
	}

	/// <summary>A handler of the test's own: it answers with a status and a body, or throws, or waits, and counts the requests it was given.</summary>
	private sealed class Fake : HttpMessageHandler
	{
		public int Requests;

		public HttpRequestMessage? Seen;

		public HttpStatusCode Status = HttpStatusCode.OK;

		public string Body = "[]";

		public Exception? Throws;

		public bool Hangs;

		protected override async Task<HttpResponseMessage> SendAsync( HttpRequestMessage request, CancellationToken cancel )
		{
			++Requests;
			Seen = request;

			if ( Hangs )
				await Task.Delay( Timeout.Infinite, cancel );

			if ( Throws != null )
				throw Throws;

			return new HttpResponseMessage( Status ) { Content = new StringContent( Body, Encoding.UTF8, "application/json" ) };
		}
	}

	[TestMethod]
	public async Task TheRequestIsTheReleasesListWithAUserAgentAndNoToken()
	{
		var fake = new Fake { Body = Reply( Release( "weekly-2026-11-08", false, Rid ) ) };
		var outcome = await UpdateChecker.CheckAsync( fake, Installed, Rid, TimeSpan.FromSeconds( 5 ) );

		Assert.AreEqual( 1, fake.Requests );
		Assert.AreEqual( "weekly-2026-11-08", outcome.Offer?.Tag );
		Assert.AreEqual( "https://api.github.com/repos/maexah/OpenTPW/releases?per_page=30", fake.Seen!.RequestUri!.ToString() );
		StringAssert.DoesNotMatch( fake.Seen.RequestUri.AbsolutePath, new System.Text.RegularExpressions.Regex( "latest" ), "not /releases/latest: weekly builds are pre-releases" );
		Assert.IsTrue( fake.Seen.Headers.UserAgent.Any(), "GitHub refuses a request with no User-Agent" );
		Assert.IsNull( fake.Seen.Headers.Authorization, "no token" );
	}

	[TestMethod]
	public async Task ARateLimitIsAFailureWhoseNoteSaysSo()
	{
		var fake = new Fake { Status = HttpStatusCode.Forbidden, Body = """{"message":"API rate limit exceeded"}""" };
		var outcome = await UpdateChecker.CheckAsync( fake, Installed, Rid, TimeSpan.FromSeconds( 5 ) );

		Assert.IsNull( outcome.Offer );
		Assert.IsTrue( outcome.Failed );
		StringAssert.Contains( outcome.Note, "403" );
		StringAssert.Contains( outcome.Note, "rate limit" );
		Assert.IsTrue( (await UpdateChecker.CheckAsync( new Fake { Status = HttpStatusCode.NotFound }, Installed, Rid, TimeSpan.FromSeconds( 5 ) )).Failed, "a 404 too" );
	}

	[TestMethod]
	public async Task ANetworkFailureIsAFailureThatNamesNoHostOrAddress()
	{
		var inner = new System.Net.Sockets.SocketException( 11001 );
		var fake = new Fake { Throws = new HttpRequestException( "No such host is known. (api.github.com:443) via proxy http://user:secret@10.0.0.9:3128", inner ) };
		var outcome = await UpdateChecker.CheckAsync( fake, Installed, Rid, TimeSpan.FromSeconds( 5 ) );

		Assert.IsNull( outcome.Offer );
		Assert.IsTrue( outcome.Failed );
		StringAssert.Contains( outcome.Note, nameof( HttpRequestException ) );
		Assert.IsFalse( outcome.Note.Contains( "secret" ) || outcome.Note.Contains( "10.0.0.9" ) || outcome.Note.Contains( "github" ), outcome.Note );
	}

	[TestMethod]
	public async Task ATimeoutIsAFailureAndDoesNotHang()
	{
		var fake = new Fake { Hangs = true };
		var clock = System.Diagnostics.Stopwatch.StartNew();
		var outcome = await UpdateChecker.CheckAsync( fake, Installed, Rid, TimeSpan.FromMilliseconds( 200 ) );

		Assert.IsTrue( outcome.Failed );
		StringAssert.Contains( outcome.Note, "no answer" );
		Assert.IsTrue( clock.Elapsed < TimeSpan.FromSeconds( 5 ), "the call gave up at its own limit" );
	}

	[TestMethod]
	public async Task TheLimitIsTenSeconds()
	{
		Assert.AreEqual( TimeSpan.FromSeconds( 10 ), UpdateChecker.Timeout );
		await Task.CompletedTask;
	}

	/// <summary>A local build, a dry run and an unnamed build make no request at all: the handler is never asked for, let alone sent to.</summary>
	[TestMethod]
	public async Task ALocalOrTestBuildMakesNoRequest()
	{
		Log = new();

		foreach ( var build in new[] { "local", "weekly-test-1", "unknown" } )
		{
			UpdateChecker.ResetForTests();
			var made = 0;

			await UpdateChecker.StartAsync( true, build, () =>
			{
				++made;
				return new Fake();
			} );

			Assert.AreEqual( 0, made, $"{build}: no handler was made" );
			Assert.IsNull( UpdateChecker.Available, build );
		}

		UpdateChecker.ResetForTests();
	}

	[TestMethod]
	public async Task TheOffSwitchMakesNoRequest()
	{
		Log = new();
		UpdateChecker.ResetForTests();
		var made = 0;

		await UpdateChecker.StartAsync( false, Installed, () =>
		{
			++made;
			return new Fake { Body = Reply( Release( "weekly-2026-11-08", false, Rid ) ) };
		} );

		Assert.AreEqual( 0, made );
		Assert.IsNull( UpdateChecker.Available );
		UpdateChecker.ResetForTests();
	}

	/// <summary>A weekly build with the setting on asks once, whatever the reply, publishes an offer only for a newer build, and a second start does nothing.</summary>
	[TestMethod]
	public async Task AWeeklyBuildAsksOnceAndPublishesTheOffer()
	{
		Log = new();
		UpdateChecker.ResetForTests();

		var rid = UpdateChecker.RunningRid();

		if ( rid is null )
			Assert.Inconclusive( "no zip is built for this platform, so no request is made" );

		var fake = new Fake { Body = Reply( Release( "weekly-2026-11-08", false, rid! ) ) };

		await UpdateChecker.StartAsync( true, Installed, () => fake );
		await UpdateChecker.StartAsync( true, Installed, () => fake );

		Assert.AreEqual( 1, fake.Requests, "once a launch" );
		Assert.AreEqual( "weekly-2026-11-08", UpdateChecker.Available?.Tag );

		UpdateChecker.ResetForTests();
		Assert.IsNull( UpdateChecker.Available );
	}

	[TestMethod]
	public async Task AFailedCheckLeavesNoOfferAndLogsOneWarning()
	{
		UpdateChecker.ResetForTests();

		if ( UpdateChecker.RunningRid() is null )
			Assert.Inconclusive( "no zip is built for this platform, so no request is made" );

		var warned = new List<string>();
		Logger.LogDelegate hear = ( severity, text ) =>
		{
			if ( severity == Logger.Level.Warning && text.StartsWith( "Update check", StringComparison.Ordinal ) )
				warned.Add( text );
		};

		Log = new();
		Logger.OnLog += hear;

		try
		{
			await UpdateChecker.StartAsync( true, Installed, () => new Fake { Throws = new HttpRequestException( "offline" ) } );
		}
		finally
		{
			Logger.OnLog -= hear;
			UpdateChecker.ResetForTests();
		}

		Assert.AreEqual( 1, warned.Count, string.Join( " | ", warned ) );
		StringAssert.Contains( warned[0], "the game goes on" );
	}

	/// <summary>The click reaches the opener with the link from the tag; a failure to open is logged and is all that happens.</summary>
	[TestMethod]
	public void AClickOpensTheReleasePageAndAFailureOnlyLogs()
	{
		Log = new();
		var was = UpdateChecker.Launch;
		var opened = new List<string>();

		try
		{
			UpdateChecker.ResetForTests();
			UpdateChecker.OpenReleasePage();
			Assert.AreEqual( 0, opened.Count );

			UpdateChecker.Launch = url =>
			{
				opened.Add( url );
				return null;
			};

			UpdateChecker.Answer( Reply( Release( "weekly-2026-11-08", false, UpdateChecker.RunningRid() ?? Rid ) ), Installed, UpdateChecker.RunningRid() ?? Rid );
			UpdateChecker.OpenReleasePage();
			CollectionAssert.AreEqual( new[] { "https://github.com/maexah/OpenTPW/releases/tag/weekly-2026-11-08" }, opened );

			UpdateChecker.Launch = _ => "no browser";
			UpdateChecker.OpenReleasePage();

			UpdateChecker.Launch = _ => throw new InvalidOperationException( "no display" );
			UpdateChecker.OpenReleasePage();
		}
		finally
		{
			UpdateChecker.Launch = was;
			UpdateChecker.ResetForTests();
		}
	}

	// ---- opentpw.cfg ----

	[TestMethod]
	public void TheOffSwitchIsKeptInOpenTpwCfg()
	{
		var folder = Path.Combine( Path.GetTempPath(), $"opentpw-update-{System.Guid.NewGuid():N}" );
		Directory.CreateDirectory( folder );
		var was = GameOptions.Current;
		var wasSaves = SaveFileSystemOrNull;

		try
		{
			Log = new();
			SaveFileSystem = new BaseFileSystem( folder );
			var file = Path.Combine( folder, "opentpw.cfg" );

			Assert.IsTrue( new GameOptions().UpdateCheck, "on by default" );

			GameOptions.Current = new();
			File.WriteAllText( file, "display windowed\nupdatecheck off\n" );
			SaveFolder.LoadDisplay();
			Assert.IsFalse( GameOptions.Current.UpdateCheck, "off" );

			File.WriteAllText( file, "updatecheck OFF\n" );
			SaveFolder.LoadDisplay();
			Assert.IsFalse( GameOptions.Current.UpdateCheck, "any case" );

			File.WriteAllText( file, "updatecheck on\n" );
			SaveFolder.LoadDisplay();
			Assert.IsTrue( GameOptions.Current.UpdateCheck, "on" );

			GameOptions.Current = new() { UpdateCheck = false };
			SaveFolder.SaveDisplay( tell: true );
			StringAssert.Contains( File.ReadAllText( file ), "\nupdatecheck off\n" );
			GameOptions.Current = new();
			SaveFolder.LoadDisplay();
			Assert.IsFalse( GameOptions.Current.UpdateCheck, "written off, read back" );

			GameOptions.Current = new();
			SaveFolder.SaveDisplay( tell: true );
			StringAssert.Contains( File.ReadAllText( file ), "\nupdatecheck on\n" );
			Assert.IsFalse( new GameOptions { UpdateCheck = false }.Copy().UpdateCheck, "the Options screen's copy keeps it" );
		}
		finally
		{
			GameOptions.Current = was;
			SaveFileSystemOrNull = wasSaves;
			Directory.Delete( folder, true );
		}
	}

	// ---- the label ----

	/// <summary>A local build's label names the commit, eight characters of it; no commit known is plain local; a weekly build's is unchanged.</summary>
	[TestMethod]
	public void ALocalBuildsLabelNamesItsCommit()
	{
		Assert.AreEqual( "local - 01850bd3", LaunchReport.BuildLabel( "local+01850bd3e5f40d5c0e1d3e7ad2b81c4a1f6b0e11" ) );
		Assert.AreEqual( "local - 01850bd", LaunchReport.BuildLabel( "local+01850bd" ), "a shorter commit is shown whole" );
		Assert.AreEqual( "local", LaunchReport.BuildLabel( "local" ), "no git: plain local" );
		Assert.AreEqual( "local", LaunchReport.BuildLabel( "local+" ) );
		Assert.AreEqual( "local", LaunchReport.BuildLabel( "local+not a commit" ), "text that is no commit is not lettered" );
		Assert.AreEqual( "weekly-2026-11-01", LaunchReport.BuildLabel( "weekly-2026-11-01+52ac9c2c35e5eed437f16fadd2a758853d9fbbb1" ) );
		Assert.AreEqual( "weekly-test-2", LaunchReport.BuildLabel( "weekly-test-2+52ac9c2c35e5eed437f16fadd2a758853d9fbbb1" ) );
		Assert.AreEqual( "unknown", LaunchReport.BuildLabel( null ) );
	}

	/// <summary>The check decides from the build's name, never the label's text: a local build's label still asks nothing.</summary>
	[TestMethod]
	public void TheCheckReadsTheNameNotTheLabel()
	{
		var informational = "local+01850bd3e5f40d5c0e1d3e7ad2b81c4a1f6b0e11";

		Assert.AreEqual( "local - 01850bd3", LaunchReport.BuildLabel( informational ) );
		Assert.IsFalse( UpdateChecker.Asks( true, LaunchReport.BuildName( informational ), out _ ) );
		Assert.IsFalse( UpdateChecker.Asks( true, LaunchReport.BuildLabel( informational ), out _ ), "and the label's own text is no weekly name either" );
	}

	// ---- the lines that show the offer ----

	/// <summary>
	/// The line says only "Update available" (the tag is in the log and the link), in the label's font, in the menu's resting teal, and
	/// goes grey and then white under the pointer as a menu choice does: 33 on the first tick, 32 more each, white at the eighth, and
	/// straight back to teal when the pointer goes.
	/// </summary>
	[TestMethod]
	public void TheNoticeIsAMenuChoiceInTheLabelsFont()
	{
		var control = UpdateNotice.Make();

		Assert.AreEqual( "Update available", control.Text );
		Assert.AreEqual( VersionLabel.LabelFont, control.Font );
		Assert.IsTrue( control.TakesMouse, "it takes the pointer, which the label does not" );
		Assert.AreEqual( new UiColour( 0, 175, 190 ), control.TextColour, "resting: the Escape menu's teal" );
		Assert.AreEqual( UiColour.White, new VersionLabel( "weekly-2026-01-01" ).Control.TextColour, "the build label stays plain white" );

		control.Entered!();
		control.Tick();
		Assert.AreEqual( new UiColour( 33, 33, 33 ), control.TextColour, "the first tick under the pointer" );
		control.Tick();
		Assert.AreEqual( new UiColour( 65, 65, 65 ), control.TextColour );

		for ( var tick = 0; tick < 10; ++tick )
			control.Tick();

		Assert.AreEqual( UiColour.White, control.TextColour, "white under the pointer" );

		control.Exited!();
		control.Tick();
		Assert.AreEqual( new UiColour( 0, 175, 190 ), control.TextColour, "straight back to teal" );
	}

	/// <summary>Clicking it reaches the opener with the link, and the stack hits it where no window is.</summary>
	[TestMethod]
	public void TheNoticeIsHitWhereNoWindowIsAndNotUnderAModalWindow()
	{
		FileSystem = GameData.Required();
		var stack = new WindowStack();
		var control = UpdateNotice.Make();
		control.Rect = new UiRect( 1500, 1444, 1800, 1472 );
		stack.Notice = () => control;

		var hit = typeof( WindowStack ).GetMethod( "HitTest", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic )!;
		var inside = UiPixel( control );

		Assert.AreSame( control, hit.Invoke( stack, [inside.X, inside.Y] ), "no window: the notice is hit" );

		stack.Open( new ModalProbe( stack ) );
		Assert.IsNull( hit.Invoke( stack, [inside.X, inside.Y] ), "a modal window shuts out what lies behind it" );
	}

	/// <summary>
	/// The Escape menu letters the build whatever the check found, and the update line only when it found a newer build, which a click
	/// takes to the opener; neither is one of the menu's choices. A check that lands after the menu opened shows at the next menu.
	/// </summary>
	[TestMethod]
	public void TheEscapeMenuLettersTheBuildAndTheOfferWhenThereIsOne()
	{
		FileSystem = GameData.Required();
		Log = new();
		var was = UpdateChecker.Launch;
		var opened = new List<string>();

		try
		{
			UpdateChecker.ResetForTests();
			UpdateChecker.Launch = url =>
			{
				opened.Add( url );
				return null;
			};

			var items = new[] { new GameMenu.Item( UIStrings.Options, 11, _ => { } ) };
			var plain = new GameMenu( new WindowStack(), items, 5 );
			var plainControls = plain.Root.Children;

			Assert.AreEqual( 1, plainControls.Count( c => c.Id == VersionLabel.ControlId ), "the build is lettered" );
			Assert.AreEqual( LaunchReport.RunningBuildLabel, plainControls.Single( c => c.Id == VersionLabel.ControlId ).Text );
			Assert.AreEqual( 0, plainControls.Count( c => c.Id == UpdateNotice.ControlId ), "no offer, no update line" );

			var rid = UpdateChecker.RunningRid() ?? Rid;
			UpdateChecker.Answer( Reply( Release( "weekly-2026-11-08", false, rid ) ), Installed, rid );

			Assert.AreEqual( 0, plain.Root.Children.Count( c => c.Id == UpdateNotice.ControlId ), "a menu already open does not change under the player" );

			var offered = new GameMenu( new WindowStack(), items, 5 );
			var notice = offered.Root.Children.Single( c => c.Id == UpdateNotice.ControlId );

			var build = offered.Root.Children.Single( c => c.Id == VersionLabel.ControlId );

			Assert.AreEqual( "Update available", notice.Text );
			Assert.AreEqual( new UiColour( 0, 175, 190 ), notice.TextColour, "teal, as a choice rests" );
			Assert.AreEqual( UiColour.White, build.TextColour, "the build stays white" );
			Assert.IsTrue( notice.TakesMouse, "the menu's dimmer takes the pointer, so the line is wired in the menu" );
			Assert.AreEqual( build.Rect.Right, notice.Rect.Right, "right-aligned with the build" );
			Assert.AreEqual( build.Rect.Top, notice.Rect.Bottom, "directly above it" );

			notice.LeftClicked!();
			CollectionAssert.AreEqual( new[] { "https://github.com/maexah/OpenTPW/releases/tag/weekly-2026-11-08" }, opened );

			// Under the pointer it goes white on the menu's own clock, as a choice does.
			var delta = Time.Delta;
			Time.Delta = 1f / 60f;

			try
			{
				notice.Entered!();

				for ( var frame = 0; frame < 120; ++frame )
					offered.Update();
			}
			finally
			{
				Time.Delta = delta;
			}

			Assert.AreEqual( UiColour.White, notice.TextColour, "white under the pointer" );

			// Neither is a choice: the menu's own list holds the one it was given.
			var choices = (System.Collections.IList)typeof( GameMenu ).GetField( "_choices", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic )!.GetValue( offered )!;
			Assert.AreEqual( 1, choices.Count );
		}
		finally
		{
			UpdateChecker.Launch = was;
			UpdateChecker.ResetForTests();
		}
	}

	/// <summary>The lobby's panel makes the line only while there is an offer, directly above the label and right-aligned with it.</summary>
	[TestMethod]
	public void TheLobbysNoticeComesAndGoesWithTheOffer()
	{
		FileSystem = GameData.Required();
		Log = new();

		try
		{
			UpdateChecker.ResetForTests();
			var panel = new UpdateNotice.LobbyPanel( "weekly-2026-01-01" );

			panel.Update();
			Assert.IsNull( panel.Control, "no offer, no line" );

			var rid = UpdateChecker.RunningRid() ?? Rid;
			UpdateChecker.Answer( Reply( Release( "weekly-2026-11-08", false, rid ) ), Installed, rid );
			panel.Update();

			var label = VersionLabel.LabelBox( "weekly-2026-01-01" )!.Value;
			Assert.AreEqual( "Update available", panel.Control?.Text );
			Assert.AreEqual( label.Right, panel.Control!.Rect.Right, "right-aligned with the label" );
			Assert.AreEqual( label.Top, panel.Control.Rect.Bottom, "directly above it" );

			UpdateChecker.Clear();
			panel.Update();
			Assert.IsNull( panel.Control, "the line goes when the offer does" );
		}
		finally
		{
			UpdateChecker.ResetForTests();
		}
	}

	/// <summary>
	/// The lobby's line is driven by the panel's own clock, 30 ticks a second of <see cref="Time.Delta"/>: a second with the pointer on it
	/// and it is white; the pointer gone and it is teal on the next frame.
	/// </summary>
	[TestMethod]
	public void TheLobbysNoticeTurnsWhiteUnderThePointerOnTheClock()
	{
		FileSystem = GameData.Required();
		Log = new();
		var was = Time.Delta;

		try
		{
			UpdateChecker.ResetForTests();
			var rid = UpdateChecker.RunningRid() ?? Rid;
			UpdateChecker.Answer( Reply( Release( "weekly-2026-11-08", false, rid ) ), Installed, rid );

			var panel = new UpdateNotice.LobbyPanel( "weekly-2026-01-01" );
			Time.Delta = 1f / 60f;
			panel.Update();

			Assert.AreEqual( new UiColour( 0, 175, 190 ), panel.Control!.TextColour, "teal at rest, however long the frames run" );

			panel.Control.Entered!();

			for ( var frame = 0; frame < 120; ++frame )
				panel.Update();

			Assert.AreEqual( UiColour.White, panel.Control.TextColour, "two seconds under the pointer" );

			panel.Control.Exited!();
			panel.Update();
			Assert.AreEqual( new UiColour( 0, 175, 190 ), panel.Control.TextColour, "teal once it goes" );
		}
		finally
		{
			Time.Delta = was;
			UpdateChecker.ResetForTests();
		}
	}

	private static (float X, float Y) UiPixel( UiControl control )
	{
		var area = control.HitArea;
		return (area.X + (area.Width / 2f), area.Y + (area.Height / 2f));
	}

	private sealed class ModalProbe : UiWindow
	{
		public ModalProbe( WindowStack stack ) : base( stack )
		{
			Modal = true;
			Root = new UiControl { Rect = new UiRect( 0, 0, 10, 10 ) };
		}
	}
}
