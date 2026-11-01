using System.Net;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace OpenTPW;

/// <summary>A weekly build newer than the one running: its tag, and the release page to send the player to.</summary>
internal sealed record UpdateOffer( string Tag, string Url );

/// <summary>
/// The quiet look for a newer weekly build (Q941). <b>OpenTPW's own, a deviation</b>: the original has no such thing, and the
/// words it puts on screen are new English.
///
/// <para>
/// Once a launch, off the render thread, a running <c>weekly-YYYY-MM-DD</c> build asks GitHub for the repository's releases
/// (<see cref="ReleasesUrl"/>; not <c>/releases/latest</c>, which skips pre-releases, and the weekly builds are pre-releases).
/// <see cref="Pick"/> takes the newest published weekly tag dated after the running build that holds this platform's
/// zip; <see cref="Available"/> then holds it for the lobby's notice and the Escape menu (<see cref="UI.GameMenu"/>) to read.
/// A <c>local</c> or <c>weekly-test-N</c> build asks nothing, so a test launch never reaches GitHub, and neither does a game
/// whose <c>opentpw.cfg</c> says <c>updatecheck off</c> (<see cref="GameOptions.UpdateCheck"/>).
/// </para>
/// <para>
/// No token, no cache file (one call a launch is inside GitHub's 60 an hour), no repeat while the game runs. Any failure - no
/// network, GitHub down, a timeout, a rate limit, bad JSON, no release - is logged once and nothing else happens. The link
/// is built from the checked tag (<see cref="ReleasePage"/>), never taken from the reply. Nothing is downloaded or installed.
/// </para>
/// </summary>
internal static class UpdateChecker
{
	public const string Repository = "maexah/OpenTPW";

	/// <summary>The releases, newest first. A published pre-release is in the list; a draft is not shown without a token.</summary>
	public const string ReleasesUrl = "https://api.github.com/repos/" + Repository + "/releases?per_page=30";

	/// <summary>How long the whole call may take before it is given up.</summary>
	public static readonly TimeSpan Timeout = TimeSpan.FromSeconds( 10 );

	/// <summary>The longest reply read: the thirty newest releases are a few hundred kilobytes at most.</summary>
	internal const int MaxReplyBytes = 4 * 1024 * 1024;

	private static readonly Regex WeeklyTag = new( "^weekly-([0-9]{4})-([0-9]{2})-([0-9]{2})$", RegexOptions.CultureInvariant );

	private static UpdateOffer? _available;

	private static int _started;

	/// <summary>The newer build the check found, or null: none yet, none newer, or the check failed. Written once, off the main thread.</summary>
	public static UpdateOffer? Available => Volatile.Read( ref _available );

	/// <summary>How a check ended: the offer it found, or null with the reason for the log.</summary>
	internal readonly record struct Outcome( UpdateOffer? Offer, string Note, bool Failed = false );

	/// <summary>The release page of a tag, built here from the tag that was checked.</summary>
	internal static string ReleasePage( string tag ) => $"https://github.com/{Repository}/releases/tag/{tag}";

	/// <summary>The zip a weekly release holds for a platform (<c>tools/weekly/release.py</c> names it).</summary>
	internal static string AssetName( string tag, string rid ) => $"OpenTPW-{tag}-{rid}.zip";

	/// <summary>The date of a <c>weekly-YYYY-MM-DD</c> build name; false for any other name or a date that is not one.</summary>
	internal static bool TryWeeklyDate( string? name, out DateOnly date )
	{
		date = default;

		if ( name == null || WeeklyTag.Match( name ) is not { Success: true } match )
			return false;

		return DateOnly.TryParseExact( name["weekly-".Length..], "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture,
			System.Globalization.DateTimeStyles.None, out date );
	}

	/// <summary>
	/// The platform's name in the zips' names: <c>win-x64</c>, <c>linux-x64</c>, <c>osx-arm64</c> or <c>osx-x64</c>; null for any
	/// other (no zip is built for it, so there is nothing to offer).
	/// </summary>
	internal static string? PlatformRid( bool windows, bool linux, bool macOs, Architecture architecture ) => architecture switch
	{
		Architecture.X64 when windows => "win-x64",
		Architecture.X64 when linux => "linux-x64",
		Architecture.X64 when macOs => "osx-x64",
		Architecture.Arm64 when macOs => "osx-arm64",
		_ => null
	};

	/// <summary>This machine's <see cref="PlatformRid"/>.</summary>
	internal static string? RunningRid()
		=> PlatformRid( OperatingSystem.IsWindows(), OperatingSystem.IsLinux(), OperatingSystem.IsMacOS(), RuntimeInformation.ProcessArchitecture );

	/// <summary>
	/// Whether a build asks at all: the setting is on and the build is a weekly one. A <c>local</c> build, a dry run and a
	/// build with no name make no request.
	/// </summary>
	internal static bool Asks( bool enabled, string build, out string why )
	{
		why = string.Empty;

		if ( !enabled )
		{
			why = "opentpw.cfg says updatecheck off";
			return false;
		}

		if ( !TryWeeklyDate( build, out _ ) )
		{
			why = $"this is the build '{build}', not a weekly one";
			return false;
		}

		return true;
	}

	/// <summary>
	/// The newest release in a GitHub releases reply that is newer than <paramref name="installed"/> and holds the zip for
	/// <paramref name="rid"/>. A draft, a tag that is not <c>weekly-YYYY-MM-DD</c> (a dry run's <c>weekly-test-N</c> among them),
	/// a tag dated on or before the installed build, and a release without the platform's zip are all passed over.
	/// </summary>
	internal static Outcome Pick( string json, string installed, string rid )
	{
		if ( !TryWeeklyDate( installed, out var installedDate ) )
			return new( null, $"the installed build '{installed}' is not a weekly one" );

		try
		{
			using var document = JsonDocument.Parse( json );

			if ( document.RootElement.ValueKind != JsonValueKind.Array )
				return new( null, "the reply is not a list of releases", Failed: true );

			string? bestTag = null;
			var bestDate = installedDate;

			foreach ( var release in document.RootElement.EnumerateArray() )
			{
				if ( release.ValueKind != JsonValueKind.Object )
					continue;

				if ( release.TryGetProperty( "draft", out var draft ) && draft.ValueKind != JsonValueKind.False )
					continue;

				if ( !release.TryGetProperty( "tag_name", out var tagElement ) || tagElement.ValueKind != JsonValueKind.String )
					continue;

				var tag = tagElement.GetString();

				if ( !TryWeeklyDate( tag, out var date ) || date <= bestDate || !HoldsAsset( release, AssetName( tag!, rid ) ) )
					continue;

				bestTag = tag;
				bestDate = date;
			}

			return bestTag is null
				? new( null, $"no weekly build for {rid} is newer than {installed}" )
				: new( new UpdateOffer( bestTag, ReleasePage( bestTag ) ), $"{bestTag} is newer than {installed}" );
		}
		catch ( JsonException )
		{
			return new( null, "the reply is not readable JSON", Failed: true );
		}
	}

	private static bool HoldsAsset( JsonElement release, string asset )
	{
		if ( !release.TryGetProperty( "assets", out var assets ) || assets.ValueKind != JsonValueKind.Array )
			return false;

		foreach ( var each in assets.EnumerateArray() )
		{
			if ( each.ValueKind == JsonValueKind.Object
				&& each.TryGetProperty( "name", out var name )
				&& name.ValueKind == JsonValueKind.String
				&& name.GetString() == asset )
				return true;
		}

		return false;
	}

	/// <summary>
	/// Asks GitHub through <paramref name="handler"/> and picks. Never throws once the request is made (on Linux <see cref="OpenSslPresent"/> is asked before): a request that fails, times out or is refused
	/// comes back as a failed <see cref="Outcome"/> whose note names the kind of failure and nothing about the machine.
	/// </summary>
	internal static async Task<Outcome> CheckAsync( HttpMessageHandler handler, string installed, string rid, TimeSpan timeout )
	{
		try
		{
			using var client = new HttpClient( handler, disposeHandler: false )
			{
				Timeout = timeout,
				MaxResponseContentBufferSize = MaxReplyBytes
			};

			using var request = new HttpRequestMessage( HttpMethod.Get, ReleasesUrl );
			request.Headers.UserAgent.Add( new ProductInfoHeaderValue( "OpenTPW-update-check", "1" ) );
			request.Headers.Accept.Add( new MediaTypeWithQualityHeaderValue( "application/vnd.github+json" ) );

			using var response = await client.SendAsync( request ).ConfigureAwait( false );

			if ( response.StatusCode != HttpStatusCode.OK )
			{
				var limited = response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests ? ", a rate limit perhaps" : "";
				return new( null, $"GitHub answered {(int)response.StatusCode}{limited}", Failed: true );
			}

			var json = await response.Content.ReadAsStringAsync().ConfigureAwait( false );

			return Pick( json, installed, rid );
		}
		catch ( TaskCanceledException )
		{
			return new( null, $"no answer within {timeout.TotalSeconds:0} seconds", Failed: true );
		}
		catch ( Exception e ) when ( e is HttpRequestException or InvalidOperationException or IOException or NotSupportedException or OperationCanceledException )
		{
			// The kind of failure only: a message can carry a proxy's address or a host name.
			return new( null, $"the request failed ({e.GetType().Name}{(e.InnerException is { } inner ? $", {inner.GetType().Name}" : "")})", Failed: true );
		}
	}

	/// <summary>
	/// The start of the game's check: once, on another thread, and only for a weekly build whose setting is on. Returns at once.
	/// A second call does nothing, so the game asks GitHub once however it is reached.
	/// </summary>
	public static void Start( bool enabled, string build ) => _ = StartAsync( enabled, build, NewHandler );

	/// <summary>The game's handler: a connection that cannot be made gives up at the same limit as the whole call.</summary>
	internal static SocketsHttpHandler NewHandler() => new() { ConnectTimeout = Timeout };

	/// <summary>
	/// <see cref="Start"/> with the handler given and the work to wait for: the task ends when the check has, at once where no
	/// request is made. The handler is made only when one is.
	/// </summary>
	internal static Task StartAsync( bool enabled, string build, Func<HttpMessageHandler> handlerFactory )
	{
		if ( Interlocked.Exchange( ref _started, 1 ) != 0 )
			return Task.CompletedTask;

		if ( !Asks( enabled, build, out var why ) )
		{
			Log.Info( $"Update check: not made - {why}" );
			return Task.CompletedTask;
		}

		if ( RunningRid() is not { } rid )
		{
			Log.Info( $"Update check: not made - no zip is built for {RuntimeInformation.OSDescription.Split( ' ' )[0]} on {RuntimeInformation.ProcessArchitecture}" );
			return Task.CompletedTask;
		}

		if ( OperatingSystem.IsLinux() && !OpenSslPresent( OpenSslProbe, out var missing ) )
		{
			Log.Info( $"Update check: not made - {missing}" );
			return Task.CompletedTask;
		}

		Log.Info( $"Update check: asking GitHub for a weekly build newer than {build} for {rid}" );

		return Task.Run( async () =>
		{
			try
			{
				using var handler = handlerFactory();

				Publish( await CheckAsync( handler, build, rid, Timeout ).ConfigureAwait( false ) );
			}
			catch ( Exception e )
			{
				Log.Warning( $"Update check: stopped ({e.GetType().Name}); the game goes on" );
			}
		} );
	}

	/// <summary>
	/// The names of the OpenSSL library the runtime's crypto shim (<c>libSystem.Security.Cryptography.Native.OpenSsl.so</c>, .NET 10)
	/// looks for, newest first.
	/// </summary>
	internal static readonly string[] OpenSslNames = [ "libssl.so.3", "libssl.so.1.1", "libssl.so.1.0.2", "libssl.so.1.0.0", "libssl.so.10" ];

	/// <summary>Whether a library of that name loads; a seam for tests. The default is <see cref="NativeLibrary.TryLoad(string, out IntPtr)"/>.</summary>
	internal static Func<string, bool> OpenSslProbe { get; set; } = name =>
	{
		if ( !NativeLibrary.TryLoad( name, out var handle ) )
			return false;

		NativeLibrary.Free( handle );
		return true;
	};

	/// <summary>
	/// On Linux the first HTTPS call loads the runtime's OpenSSL shim, which prints "No usable version of libssl was found" and
	/// aborts the process where no libssl loads, and no catch stops that. So a check asks first, and without one makes no request.
	/// </summary>
	internal static bool OpenSslPresent( Func<string, bool> loads, out string why )
	{
		foreach ( var name in OpenSslNames )
		{
			if ( loads( name ) )
			{
				why = string.Empty;
				return true;
			}
		}

		why = $"no libssl was found ({string.Join( ", ", OpenSslNames )}), and without one the runtime stops the game at the first HTTPS call";
		return false;
	}

	/// <summary>Lets a test start again; the game starts the check once and nothing resets it.</summary>
	internal static void ResetForTests()
	{
		Volatile.Write( ref _started, 0 );
		Clear();
	}

	/// <summary>Records an outcome for the lobby and the menu to read, and says so once.</summary>
	private static void Publish( Outcome outcome )
	{
		Volatile.Write( ref _available, outcome.Offer );

		if ( outcome.Failed )
			Log.Warning( $"Update check: {outcome.Note}; the game goes on" );
		else
			Log.Info( $"Update check: {outcome.Note}" );
	}

	/// <summary>
	/// The debug console's way to a fake answer (<c>update fake</c>): <paramref name="json"/> goes through the same
	/// <see cref="Pick"/> a reply from GitHub does, as if it had arrived for the running build.
	/// </summary>
	internal static Outcome Answer( string json, string installed, string rid )
	{
		var outcome = Pick( json, installed, rid );
		Publish( outcome );

		return outcome;
	}

	/// <summary>Forgets the offer (the debug console's <c>update clear</c>).</summary>
	internal static void Clear() => Volatile.Write( ref _available, null );

	/// <summary>
	/// What opens a link: SDL's own call, which hands it to the system's browser. A seam for tests. Returns null where it
	/// worked, else what went wrong.
	/// </summary>
	internal static Func<string, string?> Launch { get; set; } = OpenWithSdl;

	/// <summary>
	/// Opens the offered release's page, once per click. A failure is logged and nothing else happens: no dialog, no retry.
	/// </summary>
	public static void OpenReleasePage()
	{
		if ( Available is not { } offer )
			return;

		Log.Info( $"Update check: opening {offer.Url}" );

		try
		{
			if ( Launch( offer.Url ) is { } trouble )
				Log.Warning( $"Update check: the browser could not be opened ({trouble}); the page is {offer.Url}" );
		}
		catch ( Exception e )
		{
			Log.Warning( $"Update check: the browser could not be opened ({e.GetType().Name}); the page is {offer.Url}" );
		}
	}

	private static string? OpenWithSdl( string url )
	{
		var sdl = NeoVeldrid.Sdl2.Sdl2Window.SdlInstance;

		return sdl.OpenURL( url ) == 0 ? null : sdl.GetErrorS();
	}
}
