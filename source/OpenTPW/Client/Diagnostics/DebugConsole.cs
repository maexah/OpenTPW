using System.Collections.Concurrent;
using System.Globalization;

namespace OpenTPW;

/// <summary>
/// Optional, self-contained command channel on stdin, for driving the game without a human at
/// the keyboard - reproducing a camera angle exactly, forcing an effect that is otherwise random,
/// or reading back frame timings.
///
/// Disabled unless OPENTPW_DEBUG_CONSOLE=1 is set, and costs a boolean test and an integer test per frame when off (Poll's and StallFrame's).
/// To remove entirely: delete this file, the two call sites in Level.Update() (Poll at its top, StallFrame at its end), Time.Paused and Time.StepFrames, and
/// every member whose summary says it exists for the debug console alone (grep "debug console", "DebugConsole" and
/// "from the console"), plus Advisor.Say, and ParkGuestSprites' debug dash: the white square Load adds to the atlas
/// and the second quad a person Build reserves. ParkPeople.Current, LobbyAudio.Current and Game.RequestLobbyReload
/// are shared with the game and stay.
///
/// Engine and content: neither. It drives and reads both, and nothing else depends on it.
///
/// Usage:
///
///     mkfifo /tmp/tpw.in
///     ( sleep 99999 &gt; /tmp/tpw.in ) &amp;     # hold the write end open, or the first echo ends the reader
///     OPENTPW_LOG_DIR=&lt;run folder&gt; OPENTPW_DEBUG_CONSOLE=1 dotnet OpenTPW.dll --game &lt;folder&gt; &lt; /tmp/tpw.in
///     echo "island 2" &gt; /tmp/tpw.in
///
/// OPENTPW_LOG_DIR keeps the run's console.log out of the game folder, so a run does not rotate the player's own.
///
/// Every command answers on stdout with a line beginning "[dbg]", so a caller can wait for the
/// reply rather than guessing how long something took.
/// </summary>
public static class DebugConsole
{
	private static readonly bool Enabled =
		Environment.GetEnvironmentVariable( "OPENTPW_DEBUG_CONSOLE" ) == "1";

	private static readonly ConcurrentQueue<string> _pending = new();
	private static bool _started;

	// A rolling window rather than an average since launch, so `stats` reports what the game is
	// doing now instead of being dragged down by the loading frames.
	private const int Window = 240;
	private static readonly double[] _frames = new double[Window];
	private static int _frameIndex;
	private static int _frameCount;

	/// <summary>Called once per frame. Does nothing at all unless the environment asked for it.</summary>
	public static void Poll()
	{
		if ( !Enabled )
			return;

		// A paused clock reports a zero delta, which would otherwise fill the window with zeroes
		// and have `stats` claim an infinite frame rate.
		if ( !Time.Paused )
			Record( Time.Delta );

		Notice( Level.Current );

		if ( !_started )
			Start();

		while ( _pending.TryDequeue( out var line ) )
			Execute( line );
	}

	/// <summary>The length of one axis row of a matrix.</summary>
	private static float AxisLength( float x, float y, float z ) => MathF.Sqrt( (x * x) + (y * y) + (z * z) );

	private static void Record( float delta )
	{
		_frames[_frameIndex] = delta * 1000.0;
		_frameIndex = (_frameIndex + 1) % Window;
		_frameCount = Math.Min( _frameCount + 1, Window );
	}

	/// <summary>
	/// Every park save seen up, in the order they came, with the hiring pool its level built, each held WEAKLY so
	/// that watching one cannot be what keeps it alive - see <see cref="Parks"/>.
	/// </summary>
	private static readonly List<(int Number, string Theme, WeakReference<IParkInitialState> Save,
		WeakReference<ParkStaffPool>? Pool)> _parks = [];

	/// <summary>
	/// Every interface seen on show, in the order they came, with the scene it was built for, each held WEAKLY for
	/// the reason <see cref="_parks"/> is - see <see cref="Huds"/>.
	/// </summary>
	private static readonly List<(int Number, string Scene, WeakReference<UI.RootPanel> Hud)> _huds = [];

	private static void Notice( Level? level )
	{
		NoticeHud( level );

		if ( level?.Park is not { } save )
			return;

		foreach ( var seen in _parks )
		{
			if ( seen.Save.TryGetTarget( out var known ) && ReferenceEquals( known, save ) )
				return;
		}

		_parks.Add( (_parks.Count + 1, level.ThemeName, new WeakReference<IParkInitialState>( save ),
			level.StaffPool is { } pool ? new WeakReference<ParkStaffPool>( pool ) : null) );
	}

	/// <summary>Notes the level's interface, once; every level builds its own, so only the last one noted can be it.</summary>
	private static void NoticeHud( Level? level )
	{
		if ( level?.Hud is not { } hud )
			return;

		if ( _huds.Count > 0 && _huds[^1].Hud.TryGetTarget( out var last ) && ReferenceEquals( last, hud ) )
			return;

		_huds.Add( (_huds.Count + 1, level.Kind == Level.Scene.Lobby ? "lobby" : level.ThemeName,
			new WeakReference<UI.RootPanel>( hud )) );
	}

	/// <summary>
	/// The voices the mixer holds, in the order they were started: name, bus, placed or flat, and whether each
	/// loops, is held, or has been told to stop. A placed voice with a range reads its place, the range, how far
	/// the listener is from it and what that distance last made of it.
	/// </summary>
	private static string Voices()
	{
		lock ( Audio.Lock )
		{
			return $"voices {Audio.Voices.Count}: " + string.Join( ", ", Audio.Voices.Select( voice =>
				$"{voice.Name} {voice.Bus} {(voice.IsPlaced ? "placed" : "flat")}" + Ranged( voice ) +
				(voice.Loop ? " looped" : "") + (voice.IsHeld ? " held" : "") + (voice.Playing ? "" : " stopped") ) );
		}
	}

	private static string Ranged( Voice voice )
	{
		if ( voice.Place is not { } place || voice.Range is not { } range )
			return "";

		var distance = (place - Camera.Ears).Length;

		return $" at ({place.X:F0},{place.Y:F0},{place.Z:F0}) volume {voice.Volume:F4} rate {voice.Rate:F4} range {range:F0} distance {distance:F2} "
			+ $"law {AudioListener.RangeGain( distance, range ):F4} gain {voice.DistanceGain:F4}";
	}

	/// <summary>
	/// Each park save seen, alive or collected after a full blocking collection, and for one still alive
	/// the static roots known to reach it: the level on show, <see cref="ParkState.Current"/>,
	/// <see cref="ParkRides.Current"/> through the level its entity was made in, and the camcorder's edge
	/// test. Then the same of the hiring pool that park's level built, which reaches no save and so is asked
	/// after separately: held by the level on show, by <see cref="ParkRides.Current"/> through the level its
	/// entity was made in, or by <see cref="ParkStaffPool.Current"/>, "pool". The collector's answer is the
	/// measurement; the named roots only say why, and one alive with none of them named is held by something
	/// this does not know about.
	/// </summary>
	private static string Parks()
	{
		GC.Collect();
		GC.WaitForPendingFinalizers();
		GC.Collect();

		var heap = GC.GetTotalMemory( forceFullCollection: false ) / (1024.0 * 1024.0);
		var alive = 0;
		var each = new List<string>();

		foreach ( var (number, theme, weak, weakPool) in _parks )
		{
			if ( !weak.TryGetTarget( out var save ) )
			{
				each.Add( $"#{number} {theme} collected" + PoolOf( weakPool ) );
				continue;
			}

			++alive;

			var by = new List<string>();

			if ( ReferenceEquals( Level.Current?.Park, save ) )
				by.Add( "level" );

			if ( ReferenceEquals( ParkState.Current?.Park, save ) )
				by.Add( "state" );

			if ( ReferenceEquals( ParkRides.Current?.Level?.Park, save ) )
				by.Add( "rides" );

			if ( ReferenceEquals( ParkCamcorderCameraMode.EdgeTestPark, save ) )
				by.Add( "camcorder" );

			each.Add( $"#{number} {theme} alive, held by {(by.Count > 0 ? string.Join( " ", by ) : "nothing named")}"
				+ PoolOf( weakPool ) );
		}

		return $"parks seen {_parks.Count} alive {alive} heap={heap:F1}MB"
			+ string.Concat( each.Select( line => $" | {line}" ) );
	}

	/// <summary>
	/// Each interface seen on show, alive or collected after a full blocking collection, and for one still alive
	/// whether it is the level on show's, "level", and how many panels it has left. A level is the one holder of its
	/// interface (<see cref="Level.Hud"/>) and empties it as it ends (<see cref="Level.Unload"/>), so one alive and
	/// not the level's is held by something this does not know about, with nothing in it.
	/// </summary>
	private static string Huds()
	{
		GC.Collect();
		GC.WaitForPendingFinalizers();
		GC.Collect();

		var alive = 0;
		var each = new List<string>();

		foreach ( var (number, scene, weak) in _huds )
		{
			if ( !weak.TryGetTarget( out var hud ) )
			{
				each.Add( $"#{number} {scene} collected" );
				continue;
			}

			++alive;

			var by = ReferenceEquals( Level.Current?.Hud, hud ) ? "level" : "nothing named";
			each.Add( $"#{number} {scene} alive, held by {by}, {hud.Children.Count} panels" );
		}

		return $"huds seen {_huds.Count} alive {alive}" + string.Concat( each.Select( line => $" | {line}" ) );
	}

	/// <summary>The hiring pool's half of a <see cref="Parks"/> line, after the collection that call has made.</summary>
	private static string PoolOf( WeakReference<ParkStaffPool>? weak )
	{
		if ( weak == null )
			return "";

		if ( !weak.TryGetTarget( out var pool ) )
			return "; its staff pool collected";

		var by = new List<string>();

		if ( ReferenceEquals( Level.Current?.StaffPool, pool ) )
			by.Add( "level" );

		if ( ReferenceEquals( ParkRides.Current?.Level?.StaffPool, pool ) )
			by.Add( "rides" );

		if ( ReferenceEquals( ParkStaffPool.Current, pool ) )
			by.Add( "pool" );

		return $"; its staff pool alive, held by {(by.Count > 0 ? string.Join( " ", by ) : "nothing named")}";
	}

	private static void Start()
	{
		_started = true;

		var thread = new Thread( () =>
		{
			// Blocks here for the life of the process; ends on EOF, which is what closing the
			// writing end of the pipe does.
			while ( Console.ReadLine() is { } line )
			{
				if ( !string.IsNullOrWhiteSpace( line ) )
					_pending.Enqueue( line.Trim() );
			}
		} )
		{ IsBackground = true, Name = "OpenTPW debug console" };

		thread.Start();
		Reply( "ready" );
	}

	private static void Execute( string line )
	{
		var parts = line.Split( ' ', StringSplitOptions.RemoveEmptyEntries );
		var command = parts[0].ToLowerInvariant();

		float Argument( int index, float fallback = 0f )
			=> parts.Length > index
				&& float.TryParse( parts[index], NumberStyles.Float, CultureInfo.InvariantCulture, out var value )
					? value
					: fallback;

		switch ( command )
		{
			case "island":
				LobbyCameraMode.DebugSelect( (int)Argument( 1 ) );
				Reply( $"island {LobbyCameraMode.CurrentIsland?.Index} '{LobbyCameraMode.CurrentIsland?.ParkName}'" );
				break;

			case "orbit":
				LobbyCameraMode.DebugOrbit = Argument( 1 );
				Reply( $"orbit {LobbyCameraMode.DebugOrbit:F3}" );
				break;

			case "freeze":
				LobbyCameraMode.Paused = true;
				Reply( "frozen" );
				break;

			// `freeze` stops the camera orbiting but leaves the world running, which is enough to
			// hold a composition and no use at all for comparing two builds: the ocean keeps
			// scrolling, the sky keeps drifting and the Dino keeps moving, so no two frames match.
			// `pause` stops the clock itself, and then a screenshot is repeatable.
			case "pause":
				Time.Paused = true;
				Reply( "paused" );
				break;

			case "resume":
				Time.Paused = false;
				Reply( "resumed" );
				break;

			// Runs the world on for a counted number of fixed-size frames and stops again, so a
			// caller can put the lobby at exactly the same point every run: pause, `attract off`, pick an
			// island, step, shoot (with nobody playing, `island` pins nothing until the attract camera is off -
			// docs/VERIFYING.md rule 98). Poll `state` for stepping=0 to know it has finished.
			case "step":
				Time.Paused = true;
				Time.StepFrames = parts.Length > 1 ? (int)Argument( 1 ) : 1;
				Reply( $"stepping {Time.StepFrames}" );
				break;

			// Stops this frame for that many milliseconds of real time once the park has had its buttons (StallFrame),
			// as a slow draw does: what the window system is sent meanwhile arrives in the next frame together (the quick
			// right click's tick, Level.RightTick).
			case "stall":
				_stall = parts.Length > 1 ? (int)Argument( 1 ) : 500;
				Reply( $"stall: {_stall} ms asked at wall {Time.WallMilliseconds}" );
				break;

			case "unfreeze":
				LobbyCameraMode.Paused = false;
				Reply( "running" );
				break;

			// Which of the lobby's two cameras runs. With nobody playing it FLIES - and that flight is
			// seeded afresh every time a lobby is built, so `island`, `orbit` and `settle` are all
			// ignored and two lobbies in one run stand in different places. `attract off` forces the
			// orbiting camera instead, whose position comes from the island and the orbit angle alone,
			// which is the only way to photograph the lobby before and after something from one spot.
			case "attract":
				LobbyCameraMode.DebugHoldOrbit = parts.Length > 1 && parts[1].ToLowerInvariant() == "off";
				Reply( $"attract {(LobbyCameraMode.DebugHoldOrbit ? "off - orbiting the selected island" : "on - the camera flies itself")}" );
				break;

			// Where the flying camera is AIMING. `attract` above is a SETTER and must never be polled
			// (VERIFYING rule 88), and `state` carries cam= but nothing about the aim.
			// This is a pure getter, so polling it at frame rate cannot perturb what it measures.
			case "aim":
				Reply( Level.Current?.Kind == Level.Scene.Lobby
					? LobbyCameraMode.AttractState()
					: "aim: only in the lobby" );
				break;

			case "settle":
				// Drops the camera straight onto where it is headed, so a screenshot taken next
				// frame is the same every run instead of depending on how long the ease had.
				LobbyCameraMode.DebugSettle();
				Reply( "settled" );
				break;

			case "strike":
				LobbyWeather.Current?.DebugStrike();
				Reply( "strike" );
				break;

			case "rain":
				if ( LobbyWeather.Current != null )
					LobbyWeather.Current.DebugRain = parts.Length > 1 ? Argument( 1 ) : null;

				Reply( $"rain {LobbyWeather.Current?.DebugRain?.ToString( "F2" ) ?? "script"}" );
				break;

			// A park's weather is a different thing entirely from the lobby's - see ParkWeather. Its
			// quality is the one lever worth having, because everything else follows from it: 1 is the
			// worst weather it can ask for (a lower number is raised to 1) and 100 is a clear day.
			case "weather":
				if ( ParkWeather.Current != null && parts.Length > 1 )
					ParkWeather.Current.DebugQuality( (int)Argument( 1 ) );

				Reply( ParkWeather.Current == null
					? "weather none - not in a park"
					: $"weather quality={ParkWeather.Current.Quality} drops={ParkWeather.Current.Drops} "
						+ $"storm={ParkWeather.Current.Lightning} rain: {ParkAudio.Current?.RainCensus() ?? "no park audio"}; "
						+ $"as saved: {ParkWeather.Describe( ParkWeather.Current.Written( ParkState.Current?.Park?.Save?.WeatherRecord ) )}" );
				break;

			// Waiting for one is up to forty seconds of real time, which is no way to check a bolt.
			// It reports where it put it, because a strike leaves nothing else behind to measure.
			//
			// "bolt x y" aims it. A park is twelve hundred units across and a bolt lands anywhere on
			// that, as the original's does, while the camera sees a few hundred - so an unaimed bolt
			// is usually out of frame and proves nothing about whether one can be drawn at all.
			case "bolt":
				var aim = parts.Length > 2
					? new Vector3( Argument( 1 ), Argument( 2 ), 0f )
					: (Vector3?)null;

				ParkWeather.Current?.DebugStrike( aim );

				Reply( ParkWeather.Current == null
					? "bolt none - not in a park"
					: $"bolt at {ParkWeather.Current.LastStrike} from {ParkWeather.Current.LastStrikeGround} "
						+ $"strikes={ParkWeather.Current.DebugStrikes}" );
				break;

			case "near":
				if ( parts.Length > 1 && parts[1] == "reset" )
				{
					LobbyFlyer.DebugClosestApproach = float.MaxValue;
					LobbyFlyer.DebugClosestSolid = float.MaxValue;
				}

				Reply( Near() );
				break;

			case "stats":
				Reply( Stats() );
				break;

			// What a scene leaves behind, by name rather than by count: `stats` can say how many
			// assets survive a lobby-park-lobby cycle and cannot say which. Bare `assets` summarises; `assets list` prints one
			// line each, so a harness can diff the sets between scene builds. The diff has to count
			// duplicates, not just compare paths: total above distinct means a path was registered
			// more than once, which is what a missed cache lookup looks like.
			case "assets":
				Reply( AssetSummary() );

				if ( parts.Length > 1 && parts[1] == "list" )
				{
					foreach ( var asset in Asset.All )
					{
						// Most of what a scene leaves behind has no path at all, and a listing of
						// empty strings cannot be told apart. Two things do tell them apart: a
						// texture's size - a 1x1 sky tint, a 16x16 ramp, a 128x128 sign panel and a
						// text label are all different shapes - and, for a model or a material, the
						// shader it draws with, which says what built it.
						var what = asset switch
						{
							Texture texture when string.IsNullOrEmpty( asset.Path )
								=> $"{texture.Width}x{texture.Height}",
							Model model when string.IsNullOrEmpty( asset.Path )
								=> $"shader {model.Material.Shader.Path}",
							Material material when string.IsNullOrEmpty( asset.Path )
								=> $"shader {material.Shader.Path}",
							_ => asset.Path,
						};

						Reply( $"asset {asset.GetType().Name} {what}" );
					}
				}

				break;

			// Every park save the game has had up, and whether it is still in memory after a full
			// collection - the question a left park's save staying alive is, asked of the collector
			// rather than inferred from which statics look as if they hold it. See Parks.
			case "parks":
				Reply( Parks() );
				break;

			// Every interface the game has had on show, and whether it is still in memory after a full collection -
			// the same question of a level's HUD, which nothing but its level should hold. See Huds.
			case "huds":
				Reply( Huds() );
				break;

			// The sea's texture, and the sampler it is ACTUALLY drawn with. Water asks for
			// TextureFlags.Wrap, and what comes back may have been served out of the texture cache,
			// which carries the sampler and the size across (Texture.TryAdoptCached) - so `requested`
			// and `sampler` disagreeing would be a fault, said by the game rather than inferred from a
			// photograph. `adopted` says which of the two roads the texture came down.
			case "water":
				if ( Entity.All.OfType<Water>().FirstOrDefault() is not { } waterEntity )
				{
					Reply( "water: no sea in this scene" );
					break;
				}

				if ( waterEntity.Sea is not { } seaTexture )
				{
					Reply( "water: the sea has no texture" );
					break;
				}

				Reply( $"water: path '{seaTexture.Path}' requested {seaTexture.Requested} " +
					$"sampler {seaTexture.SamplerType} size {seaTexture.Width}x{seaTexture.Height} " +
					$"adopted {seaTexture.Adopted}" );
				break;

			case "state":
				Reply( State() );
				break;

			// Where a control of an open window is on screen, in window pixels, by its id in hex - so that a harness
			// can `click` the middle of a button rather than work out the virtual screen's anchoring for itself, and what
			// it letters: a box's text and whether it has the keys, any other control's text, and a lettered control's font, colour and edge. A pure getter.
			case "control":
				if ( parts.Length < 2 || !int.TryParse( parts[1].Replace( "0x", "" ), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var controlId ) )
				{
					Reply( "control: control <id in hex>" );
					break;
				}

				if ( (Level.Current?.Hud?.Children.OfType<UI.WindowStack>().FirstOrDefault()?.Windows
					.Select( window => FindControl( window.Root, controlId ) ).FirstOrDefault( found => found != null )
					?? Level.Current?.Hud?.Children.OfType<UI.VersionLabel>().Select( label => FindControl( label.Control, controlId ) ).FirstOrDefault()) is not { } control )
				{
					Reply( $"control: no open window holds 0x{controlId:x}" );
					break;
				}

				var at = control.Pixels;
				Reply( $"control 0x{controlId:x} at ({at.X:F0},{at.Y:F0}) size ({at.Width:F0},{at.Height:F0}) "
					+ $"middle ({at.X + (at.Width / 2f):F0},{at.Y + (at.Height / 2f):F0})"
					+ (control is UI.UiEdit typedInto ? $" holds '{typedInto.Value}' {(typedInto.HasFocus ? "with" : "without")} the keys"
						: control.Text is { Length: > 0 } lettered ? $" reads '{lettered.Replace( "\n", " / " )}'" : "")
					+ (control.Mesh is { } drawnMesh ? $" mesh {drawnMesh.Name} frame {control.Frame}" : "")
					+ (control.Text is { Length: > 0 } && control is not UI.UiEdit
						? $" lettered font {control.Font} in ({control.TextColour.R}, {control.TextColour.G}, {control.TextColour.B}) "
							+ (control.TextShadow ? "with" : "without") + " the edge"
						: "") );
				break;

			// The level the crowd has the park's music at, and how many times it has been set (ParkAudio.MusicLevel).
			case "music":
				Reply( ParkAudio.Current is { } playing
					? $"music: level {playing.LevelNow} variation {playing.VariationNow + 1} set {playing.LevelSets} times, {ParkPeople.Current?.Peeps.Count ?? 0} guests, tick {GameClock.Ticks}"
					: "music: a park has to be loaded" );
				break;

			// The crowd's voice: its level, the count behind it and what it is playing (ParkAudio.CrowdVoiceLevel).
			case "crowd":
				Reply( ParkAudio.Current is { } crowded
					? $"crowd: level {crowded.CrowdVoiceLevelNow} from {crowded.CrowdVoiceGuests} guests near cell {crowded.CrowdVoiceCell}"
						+ $" ({(crowded.CrowdVoiceCell > 0 ? $"{(crowded.CrowdVoiceCell - 1) % ParkWorld.MapSize},{(crowded.CrowdVoiceCell - 1) / ParkWorld.MapSize}" : "none")}),"
						+ $" {(crowded.CrowdVoiceHeld ? "held" : "not held")}, variation {crowded.CrowdVoiceVariation + 1} volume {crowded.CrowdVoiceSample.Volume} plays {crowded.CrowdVoiceVolume:F4}"
						+ $" pitch {crowded.CrowdVoiceSample.Pitch}, {crowded.CrowdVoicePlays.Samples} samples, started {crowded.CrowdVoicePlays.Starts} times, tick {GameClock.Ticks}"
					: "crowd: a park has to be loaded" );
				break;

			// Writes the running park into the player's folder under the name given, spaces and all, as far as the
			// writer is built (Level.WritePark), as the park menu's Save Game does.
			case "savepark":
				var saveName = line.Trim()[parts[0].Length..].Trim();

				if ( saveName.Length == 0 )
				{
					Reply( "savepark: a name is needed" );
					break;
				}

				Reply( Level.Current?.WritePark( saveName ) is { } written
					? $"savepark: wrote {written}"
					: $"savepark: '{saveName}' was not written - the log says why" );
				break;

			// The park's own clock and what the three-a-frame cap has cost it (ParkPeople.SweepsAFrame).
			case "sweeps":
				Reply( ParkPeople.Current is { } swept
					? $"sweeps: mGameTick {swept.State.GameTick}, {swept.SweepsRun} run, {swept.SweepsDropped} dropped"
					: "sweeps: a park has to be loaded" );
				break;

			// The windows open in the interface, back to front - the front one last. A pure getter, and the lobby's
			// only way to say whether the island panel or the game menu is up.
			case "windows":
				Reply( Level.Current?.Hud?.Children.OfType<UI.WindowStack>().FirstOrDefault() is { } shown
					? $"windows: {(shown.Windows.Count == 0 ? "none" : string.Join( ", ", shown.Windows.Select( window => window is UI.ParkBuyScreen buy ? $"{nameof( UI.ParkBuyScreen )} tab {buy.Tab}" : window is UI.ParkHireScreen hire ? $"{nameof( UI.ParkHireScreen )} tab {hire.Tab}" : window.GetType().Name ) ))}"
						+ (shown.Covered ? " - under the full-screen view" : "")
						+ $"; left on {UI.ParkCategoryScreens.LeftOn( UI.ParkCategoryScreens.Building )},{UI.ParkCategoryScreens.LeftOn( UI.ParkCategoryScreens.Information )},{UI.ParkCategoryScreens.LeftOn( UI.ParkCategoryScreens.Finance )}"
					: "windows: no window stack" );
				break;

			case "volume":
				if ( parts.Length > 1 )
					Audio.MasterVolume = Argument( 1, Audio.MasterVolume );

				Reply( $"volume={Audio.MasterVolume:0.00}" );
				break;

			// One group's level as a multiple of its layers' (Audio.SetBusVolume), so a capture can hold one group
			// alone: `bus music 0`. The options screen sets the same three; a change there replaces this.
			case "bus":
				if ( parts.Length > 2 && Enum.TryParse<AudioBus>( parts[1], ignoreCase: true, out var bus ) )
				{
					Audio.SetBusVolume( bus, Argument( 2 ) );
					Reply( $"bus: {bus} at {Argument( 2 ):0.00}" );
				}
				else
				{
					Reply( "bus: bus <effects|music|speech> <gain>" );
				}

				break;

			case "mute":
				if ( LobbyAudio.Current != null )
					LobbyAudio.Current.Muted = parts.Length < 2 || parts[1] != "0";

				Reply( $"muted={LobbyAudio.Current?.Muted}" );
				break;

			// The cue the front end plays as a key is spent to enter a park, without the key (Q147).
			case "parkcue":
				LobbyAudio.Current?.ParkEntry();
				Reply( LobbyAudio.Current == null ? "no lobby audio" : "park-entry cue played; see the log" );
				break;

			case "sound":
				Reply( LobbyAudio.Current?.State() ?? "no lobby audio" );
				break;

			// Every voice sounding, placed or flat, and whether it is held - what a park's pause does to
			// sound, said by the mixer's own list rather than heard (see Audio.HoldPlaced).
			case "voices":
				Reply( Voices() );
				break;

			// The land's own sounds: where each stands, its range, and what the listener's distance makes of it.
			case "placed":
				Reply( ParkAudio.Current?.PlacedCensus() ?? "no park audio" );
				break;

			// The sounds the park's scripts have started with ADDOBJ and EVENT, each with its place and voice.
			case "scriptsounds":
				Reply( ParkAudio.Current?.ScriptSoundCensus() ?? "no park audio" );
				break;

			// Plays one of the island's ambient samples at a place of the caller's choosing, so a pan
			// can be measured at angles the lobby's own marked place never reaches: the Space antenna
			// sits almost on the camera's look axis, so it never pans more than about a seventh of the
			// way across. Bare `place` uses whatever place the island marks.
			case "place":
				Reply( LobbyAudio.Current?.DebugPlaceSound(
					parts.Length > 3
						? new Vector3( Argument( 1 ), Argument( 2 ), Argument( 3 ) )
						: null ) ?? "no lobby audio" );
				break;

			case "speech":
				// Auditions one of the 641 global speech samples, ducking the rest of the mix
				// exactly as a real line would. This is how Advisor's first-launch sample
				// gets confirmed: the response table (0x00768fb8, docs/exe/advisor-park.md) says which sample a
				// response plays, and listening says what the sample says.
				if ( Advisor.Current == null )
					Reply( "no advisor" );
				else if ( parts.Length > 1 )
					Advisor.Current.Say( (int)Argument( 1, 1 ) );
				else
					Advisor.Current.Hush();

				Reply( Advisor.Current?.State() ?? "no advisor" );
				break;

			case "advisor":
				Reply( Advisor.Current?.State() ?? "no advisor" );
				if ( ParkState.Current?.AdvisorMessages is { } messages ) Reply( messages.Census() );
				break;

			case "outlets":
				// The shops and sideshows as the advisor's rows 238 to 257 judge them, in the object chain's order.
				if ( ParkPeople.Current?.AdvisorFacts().Outlets?.All is not { } judged )
				{
					Reply( "outlets: a park has to be loaded" );
					break;
				}

				Reply( $"outlets {judged.Count} day {GameCalendar.Days} tick {ParkState.Current?.GameTick}" );
				foreach ( var outlet in judged )
					Reply( $"  thing {outlet.Thing} {outlet.Of} age {outlet.Age} customers {outlet.Customers} walk-aways {outlet.WalkAways} "
						+ $"share {(outlet.Customers == 0 ? "-" : outlet.WalkAwayShare.ToString())} satisfaction {outlet.Satisfaction}" );
				break;

			case "sayas":
				// An instrument: the park advisor's next line is said as response N (a row of the response table only).
				if ( ParkState.Current?.AdvisorMessages is not { } sayAs )
					Reply( "no park" );
				else if ( parts.Length < 2 || !int.TryParse( parts[1], out var asResponse ) || ParkAdvisorMessages.ResponseOf( asResponse ) == null )
					Reply( "sayas: give a response of the table" );
				else
				{
					sayAs.SayAs = asResponse;
					Reply( $"sayas: the next line is said as response {asResponse}, gesture row {ParkAdvisorMessages.GestureRowOf( asResponse )}" );
				}
				break;

			case "tags":
				// The tags on the gadget's aerial, the oldest first, and where the gadget has drawn them.
				Reply( ParkState.Current?.Tags.Census( Time.WallMilliseconds ) ?? "no park" );
				Reply( Level.Current?.Hud?.Children.OfType<UI.ParkFrontEnd>().FirstOrDefault()?.Gadget.AerialCensus() ?? "no gadget" );
				Reply( Level.Current?.Hud?.Children.OfType<UI.ParkFrontEnd>().FirstOrDefault()?.Gadget.AerialDrawn() ?? "no gadget" );
				Reply( Level.Current?.Hud?.Children.OfType<UI.ParkFrontEnd>().FirstOrDefault()?.Gadget.BarCensus() ?? "no gadget" );
				break;

			case "locator":
				// The staff and visitor locators: which is open, whom the camera is on and what the strip shows.
				Reply( Level.Current?.Hud?.Children.OfType<UI.ParkFrontEnd>().FirstOrDefault()?.Gadget.Locator.Census() ?? "no gadget" );
				break;

			case "staffwindow":
				// The staff member's window: its member, title, figures, bars, status and buttons, or that none is up.
				Reply( Level.Current?.Hud?.Children.OfType<UI.WindowStack>().FirstOrDefault()?.Windows.OfType<UI.ParkStaffWindow>()
					.FirstOrDefault()?.Census() ?? "staff window: none" );
				break;

			case "shopwindow":
				// The shop's window: its shop, title, figures, bars, settings, door and status, or that none is up.
				Reply( Level.Current?.Hud?.Children.OfType<UI.WindowStack>().FirstOrDefault()?.Windows.OfType<UI.ParkShopWindow>()
					.FirstOrDefault() is { } shopWindow ? $"shop window: {shopWindow.Census()}" : "shop window: none" );
				break;

			case "researchscreen":
				// The research screen: the work load, the slider's limit, knob, dial and gauge, and each kind's bar, effort, output and label.
				Reply( Level.Current?.Hud?.Children.OfType<UI.WindowStack>().FirstOrDefault()?.Windows.OfType<UI.ParkResearchScreen>()
					.FirstOrDefault() is { } researchScreen ? $"research screen: {researchScreen.Census()}" : "research screen: none" );
				break;

			case "visitorwindow":
				// The visitor's window: its guest, title, figures, bar and thought, or that none is up.
				Reply( Level.Current?.Hud?.Children.OfType<UI.WindowStack>().FirstOrDefault()?.Windows.OfType<UI.ParkVisitorWindow>()
					.FirstOrDefault()?.Census() ?? "visitor window: none" );
				break;

			case "greet":
				// Replays the front-end greeting the way the lobby gives it on arrival with no saved
				// players, so its timing and ducking can be captured at a moment of the caller's choosing.
				UI.FrontEndLines.Greet( usedSlots: 0 );
				Reply( Advisor.Current?.State() ?? "no advisor" );
				break;

			case "duck":
				Audio.Duck( parts.Length > 1 ? Argument( 1, 1f ) : 1f, 0.3f );
				Reply( $"duck={Audio.DuckLevel:0.00}" );
				break;

			case "reload":
				// Ends the lobby and builds it again between frames, the way coming back from a park does,
				// so whatever a scene leaves behind as it ends can be seen and measured.
				Game.RequestLobbyReload();
				Reply( "reloading" );
				break;

			// Resizes the window from here rather than from the desktop, so a run can put the game at
			// an exact size, or change its size while it runs, with no window manager in the way.
			case "size":
				// Spelled out, because Window here is this file's own frame-timing constant.
				if ( parts.Length > 2 )
					global::OpenTPW.Window.Current?.Resize( (int)Argument( 1 ), (int)Argument( 2 ) );

				Reply( $"size {Screen.Size.X}x{Screen.Size.Y}" );
				break;

			// Swaps straight to a park, and `lobby` comes back out. The scene swap on its own, with
			// no gate and no front end in front of it, so that what a park loads and draws can be
			// looked at without anything deciding when to load it. `enter` below is the path a
			// player actually takes.
			case "park":
				Game.RequestParkLoad( parts.Length > 1 ? parts[1].ToLowerInvariant() : "jungle" );
				Reply( $"entering {(parts.Length > 1 ? parts[1].ToLowerInvariant() : "jungle")}" );
				break;

			// The front end's own park entry, which `park` above deliberately skips: the camera leaves for the
			// island on show and the park is asked for when it arrives, so that the flight can be watched
			// without having to drive the interface. The panel's Enter this park is the player's route.
			case "enter":
				if ( LobbyCameraMode.CurrentIsland is not { } entering )
				{
					Reply( "no island on show" );
					break;
				}

				Reply( $"leaving for {entering.ParkName}: the camera swings onto its gate, opens it and flies in" );

				// The call IslandPanel.EnterPark ends with, without the panel: the camera swings onto the gate,
				// opens it, flies in, and asks for the park when it arrives. Escape cancels it as it cancels theirs.
				LobbyCameraMode.LeaveForPark( () => Game.RequestParkLoad( entering.ThemeName ) );
				break;

			case "lobby":
				Game.RequestLobbyReload();
				Reply( "returning to the lobby" );
				break;

			// What a label over each person's head would have said. This engine has no world-to-screen
			// projection and every font it owns draws in the interface's own virtual screen, so a
			// printed census is both cheaper and easier to read than text pinned over a 15-pixel figure.
			case "guests":
				if ( ParkGuestSprites.Current is not { } guests )
				{
					Reply( "guests: none - a park has to be loaded" );
					break;
				}

				var census = guests.Census().ToArray();
				Reply( $"guests {census.Length}" );

				foreach ( var person in census )
					Reply( "  " + person );

				var balloons = guests.BalloonCensus().ToArray();
				Reply( $"balloons {balloons.Length}" );

				foreach ( var balloon in balloons )
					Reply( "  " + balloon );

				break;

			// What each guest wants, as opposed to what they look like - the other half of a person, and
			// the one that changes while the park runs.
			case "peeps":
				if ( ParkPeople.Current is not { } people )
				{
					Reply( "peeps: none - a park has to be loaded" );
					break;
				}

				var wants = people.Census().ToArray();
				Reply( $"peeps {wants.Length}" );

				foreach ( var peep in wants )
					Reply( "  " + peep );

				break;

			// What each ride's script is doing and who it is carrying. Riders are drawn on a node of the
			// ride's own model, so when one appears at the front of the queue instead this is the census
			// that says which link of that chain is broken.
			case "rides":
				if ( ParkPeople.Current is not { } operators )
				{
					Reply( "rides: none - a park has to be loaded" );
					break;
				}

				// The scheduler's own count beside the census, because the census walks what is standing and
				// so stops listing a sold thing whether or not its script was taken down with it.
				var running = operators.RideCensus().ToArray();
				var scripts = ParkRides.Current?.Scheduler.Scripts.ToArray() ?? [];
				Reply( $"rides {running.Length} distinct scream samples heard "
					+ $"{ParkAudio.Current?.ScreamSamplesHeard ?? 0} screams let go {ParkAudio.Current?.ScreamsLetGo ?? 0} "
					+ $"scripts {ParkRides.Current?.Scheduler.Count ?? 0} "
					+ $"tick {ParkRides.Current?.Scheduler.Tick ?? 0} "
					+ $"now {ParkRides.NowMilliseconds} last tick at {ParkRides.LastTickMilliseconds} "
					+ $"bound {ParkRides.Current?.Bound ?? 0} "
					+ $"critical longest {scripts.Select( script => script.LongestCritical ).DefaultIfEmpty().Max()} "
					+ $"cap {RideScript.CriticalStepCap} reached {scripts.Count( script => script.ReachedCriticalCap )} "
					+ $"lastunit {scripts.Sum( script => script.LastUnitLocks )}" );

				foreach ( var ride in running )
					Reply( "  " + ride );

				break;

			// Writes one of a thing's script variables by name, as the engine writes VAR_BREAKSTAT, VAR_WORN and the rest
			// from outside the script - an instrument for an arm nothing built here reaches yet (breakdowns, wear).
			// With no value it reads the variable instead.
			case "scriptvar":
			{
				var varValue = 0;

				if ( parts.Length < 3 || !int.TryParse( parts[1], out var varThing )
					|| (parts.Length > 3 && !int.TryParse( parts[3], out varValue ))
					|| ParkRides.Current is not { } varRides || varRides.ScriptFor( varThing ) is not (var varId and not 0)
					|| varRides.Scheduler.Find( varId ) is not { } varScript )
				{
					Reply( "scriptvar: scriptvar <thing> <VAR_NAME> [value], for a thing running a script" );
					break;
				}

				if ( parts.Length == 3 )
				{
					Reply( varScript.IndexOf( parts[2] ) >= 0
						? $"scriptvar: thing {varThing} {parts[2]} is {varScript[parts[2]]}"
						: $"scriptvar: thing {varThing}'s script declares no {parts[2]}" );
					break;
				}

				Reply( varScript.Set( parts[2], varValue )
					? $"scriptvar: thing {varThing} {parts[2]} = {varValue}"
					: $"scriptvar: thing {varThing}'s script declares no {parts[2]}" );
				break;
			}

			// The bumper rides and their cars - ParkBumperCars: each ride's state, wear, duration, seated count and lists,
			// then each live car's place, heading, timer, clip and riders, and how many boats are drawn.
			case "bumpers":
			{
				if ( ParkState.Current?.TrackRides.Cars is not { } bumpers )
				{
					Reply( "bumpers: none - a park has to be loaded" );
					break;
				}

				var report = new List<string>();

				foreach ( var placed in ParkState.Current.Objects )
				{
					if ( bumpers.RideOf( placed.TrackRide ) is not { } ride )
						continue;

					report.Add( $"  ride 0x{placed.TrackRide:x} thing {placed.ThingId} state {ride.State} wear {ride.Wear} "
						+ $"duration {ride.Duration} cars {ride.Cars} seated {ride.Seated} boarding [{string.Join( ",", ride.Boarding )}] "
						+ $"leaving [{string.Join( ",", ride.Leaving )}] centre ({ride.CentreX},{ride.CentreZ}) "
						+ $"performance {ride.Performance} thrust {ride.Thrust} friction {ride.Friction} turn {ride.TurnRate} "
						+ $"restitution {ride.Restitution}" );

					foreach ( var car in bumpers.CarsOf( placed.TrackRide ) )
					{
						var off = ParkBumperCars.Distance( car.X - ride.CentreX, car.Z - ride.CentreZ );
						var target = (car.Flags & ParkBumperCars.CarFlags.Chasing) != 0 ? $"chase {car.Chased}" : $"buoy {car.Buoy}";

						report.Add( $"    car {car.Index} at ({car.X},{car.Z}) from centre {off} velocity ({car.VelocityX},{car.VelocityZ}) "
							+ $"speed {car.Speed} steering {car.Steering} steer ({car.SteerX},{car.SteerZ}) {target} patience {car.Patience} "
							+ $"heading {car.Heading} turned {car.Turned} timer {car.Timer} anim {car.Animation} "
							+ $"height {ParkBumperBoats.Current?.HeightOf( car.Index ):0.0000} "
							+ ( ParkBumperBoats.Current?.AnimationsOf( car.Index )?.Channel( 0 ) is { IsIdle: false } clip
								? $"clip start {clip.StartAnimTime} time {clip.AnimTime} frame {clip.AnimFrame:0.00} " : "clip none " )
							+ (car.Smoking ? car.Smoke != ParkBumperCars.NoSmoke ? $"smoke 0x{car.Smoke:x} " : "smoke started " : "smoke none ")
							+ $"flags 0x{(int)car.Flags:x} riders [{string.Join( ",", car.Riders.Select( rider => $"{rider.Peep}@{rider.Seat}" ) )}] "
							+ ( car.Voice is ParkCarSounds.Held held
								? $"sound effect {held.Effect} '{held.Voice.Name}' param {held.Values[1]} volume {held.Volume} playing at {held.Voice.Volume:0.0000} pitch {held.Pitch} rate {held.Voice.Rate:0.000} "
									+ $"at ({held.Voice.Place?.X:0.0},{held.Voice.Place?.Y:0.0}) playing {held.Voice.Playing}"
								: "sound none" ) );
					}
				}

				Reply( $"bumpers {report.Count} track tick {bumpers.Ticks} boats drawn {ParkBumperBoats.Current?.Standing ?? 0} "
					+ $"clips resumed {ParkBumperBoats.Current?.Resumed ?? 0} loaded at {ParkRides.Current?.LoadedAt} now {ParkRides.NowMilliseconds} "
					+ $"sounds started {bumpers.SoundsStarted}" );

				foreach ( var entry in report )
					Reply( entry );

				break;
			}

			// Sets every guest's happiness - an instrument as `thirst` is, so that a dock of happiness can be
			// seen: see ParkPeople.SetHappiness.
			case "happy":
				if ( ParkPeople.Current is not { } moods )
				{
					Reply( "happy: none - a park has to be loaded" );
					break;
				}

				Reply( $"happy: {moods.SetHappiness( Argument( 1, 50 ) )} guests are now happiness {Argument( 1, 50 )}" );

				break;

			// Where the guests' happiness goes: `happiness on` starts Peep.HappinessLedger from nought, `happiness` prints
			// it by state, most lost first, `happiness off` stops it.
			case "happiness":
				if ( parts.Length > 1 && parts[1] is "on" or "off" )
				{
					Peep.HappinessLedger = parts[1] == "on" ? new() : null;
					Reply( $"happiness: ledger {parts[1]}" );
					break;
				}

				if ( Peep.HappinessLedger is not { } moodLedger )
				{
					Reply( "happiness: ledger off - `happiness on` starts it" );
					break;
				}

				Reply( $"happiness: lost {-moodLedger.Values.Where( v => v < 0 ).Sum():0} gained {moodLedger.Values.Where( v => v > 0 ).Sum():0}" );

				foreach ( var (state, change) in moodLedger.OrderBy( kv => kv.Value ) )
					Reply( $"  {state} {change:+0.0;-0.0}" );

				break;

			// Sets every guest's cash - an instrument as `happy` is, so that a guest can come to a door short of
			// its price: see ParkPeople.SetCash.
			case "cash":
				if ( ParkPeople.Current is not { } purses )
				{
					Reply( "cash: none - a park has to be loaded" );
					break;
				}

				Reply( $"cash: {purses.SetCash( (int)Argument( 1, 10 ) )} guests now carry {(int)Argument( 1, 10 )}" );

				break;

			// Makes every guest thirsty. An INSTRUMENT rather than a behaviour, and it is here for the
			// reason `load` is: a park left alone almost never holds a guest who is thirsty AND still
			// deciding, which is the one condition a drinks shop is chosen under. Only a quarter of
			// guests grow thirsty on their own, and by the time they have, their day is usually over.
			// It moves a meter the game itself moves and nothing else - no choosing, no placing, no till.
			case "thirst":
				if ( ParkPeople.Current is not { } drinkers )
				{
					Reply( "thirst: none - a park has to be loaded" );
					break;
				}

				// A level below the top holds off the maxed-need drain on happiness (Peep.Tick) until the thirst
				// drifts back up, so a dock of it can be read exactly: `thirst 90`.
				var level = Math.Clamp( Argument( 1, Peep.Most ), Peep.Least, Peep.Most );

				Reply( $"thirst: {drinkers.MakeThirsty( level )} guests are now thirst {level}" );

				break;

			// Sets the life of every balloon held - an instrument as `thirst` is, so that a balloon's running out
			// can be watched without waiting its two minutes: see ParkPeople.SetBalloonLife.
			case "balloon":
				if ( ParkPeople.Current is not { } holders )
				{
					Reply( "balloon: none - a park has to be loaded" );
					break;
				}

				Reply( $"balloon: {holders.SetBalloonLife( (int)Argument( 1, 1 ) )} balloons now have life {(int)Argument( 1, 1 )}" );

				break;

			// Sets every guest's toilet need - an instrument as `thirst` is, so that a queuer can pass 80 and leave
			// for a toilet: see ParkPeople.SetToilet. A second argument names one guest.
			case "toilet":
				if ( ParkPeople.Current is not { } bladders )
				{
					Reply( "toilet: none - a park has to be loaded" );
					break;
				}

				var need = Math.Clamp( Argument( 1, Peep.HurryAboveToilet ), Peep.Least, Peep.Most );

				Reply( $"toilet: {bladders.SetToilet( need, (int)Argument( 2, 0 ) )} guests are now toilet {need}" );

				break;

			// Sets a level the deciding turn's counted arms test - `need vomit|litter|prankery <n> [guest]` - an
			// instrument as `toilet` is: see ParkPeople.SetNeed.
			case "need":
				if ( ParkPeople.Current is not { } needy )
				{
					Reply( "need: none - a park has to be loaded" );
					break;
				}

				var needName = parts.Length > 1 ? parts[1] : "";
				var needSet = needy.SetNeed( needName, (int)Argument( 2, 100 ), (int)Argument( 3, 0 ) );

				Reply( needSet < 0
					? "need: vomit, litter, hunger or prankery, then a level and optionally a guest"
					: $"need: {needSet} guests are now {needName} {(int)Argument( 2, 100 )}" );

				break;

			// What every thing a guest may be sent to has taken, and whether it can be offered at all. The
			// two censuses above this one cannot answer that: `rides` says what a script is doing
			// and `peeps` says what a guest carries, while whether a shop is REACHABLE turns on a walk over
			// the map that neither makes. It prints every visitable thing, refused ones included.
			case "spend":
				if ( ParkPeople.Current is not { } spending )
				{
					Reply( "spend: none - a park has to be loaded" );
					break;
				}

				var tills = spending.SpendCensus().ToArray();
				Reply( $"spend {tills.Length}" );

				foreach ( var till in tills )
					Reply( "  " + till );

				break;

			// Which routes the park's fixed items loaded, out of the path table at model file 0xac.
			// The bus is the one worth reading: 45 points, open Bezier, first point near
			// (207.4, 0.0, -247.8). Read it in a live park - a test proves the file parses, not that
			// the park loaded it.
			case "paths":
				if ( ParkFixedItems.Current is not { } fixedItems )
				{
					Reply( "paths: none - a park has to be loaded" );
					break;
				}

				var routes = fixedItems.PathCensus().ToArray();
				Reply( $"paths {routes.Length}" );

				foreach ( var route in routes )
					Reply( "  " + route );

				break;

			// Where each vehicle's script actually IS. The paths census says where a vehicle is drawn,
			// and that is the same line whether its script is running or parked for ever waiting on a
			// trigger - only the program counter separates them. See ParkPeople.VehicleCensus.
			case "vehicles":
				if ( ParkPeople.Current is not { } fleet )
				{
					Reply( "vehicles: none - a park has to be loaded" );
					break;
				}

				var crewed = fleet.VehicleCensus().ToArray();
				Reply( $"vehicles {crewed.Length}" );

				foreach ( var vehicle in crewed )
					Reply( "  " + vehicle );

				break;

			// The arrival timer: the park's own clock, the mark its wait counts from, and the load held or the tick
			// the next is due on. A pure getter.
			case "arrivals":
				Reply( ParkPeople.Current is { } timer ? timer.ArrivalCensus() : "arrivals: none - a park has to be loaded" );
				break;

			// Puts one new guest at a cell, the first bus stop unless one is named. An arrival's own cell is the
			// second stop (ParkPeople.ArrivalCell) - the original makes one per thing tick there while a vehicle
			// unloads. Driven by hand here so a guest can be made on demand, at any cell.
			case "arrive":
				if ( ParkPeople.Current is not { } arrivals )
				{
					Reply( "arrive: none - a park has to be loaded" );
					break;
				}

				// BusStopA, which Standard.sam puts at (42,5): the stop guests leave from.
				var arrivedAt = arrivals.Admit( (int)Argument( 1, 42 ), (int)Argument( 2, 5 ) );

				Reply( arrivedAt == 0
					? "arrive: the park would not take one"
					: $"arrive: guest {arrivedAt}" );

				break;

			// A guest made inside the park, already through the gate and deciding, of a chosen kind (0-7) or a
			// drawn one - see ParkPeople.AdmitInside. An instrument, as `send` is.
			case "admit":
				if ( ParkPeople.Current is not { } admitting )
				{
					Reply( "admit: none - a park has to be loaded" );
					break;
				}

				if ( parts.Length < 3 )
				{
					Reply( "admit <cellX> <cellY> [kind 0-7]" );
					break;
				}

				int? kind = parts.Length > 3
					? Math.Clamp( (int)Argument( 3, 0 ), 0, ParkWorld.GuestState.PersonTypes - 1 )
					: null;

				var admitted = admitting.AdmitInside( (int)Argument( 1, 0 ), (int)Argument( 2, 0 ), kind );

				Reply( admitted == 0
					? "admit: the park would not take one"
					: $"admit: guest {admitted} kind {admitting.Peeps.First( peep => peep.ThingId == admitted ).PersonType} "
						+ $"deciding at ({(int)Argument( 1, 0 )},{(int)Argument( 2, 0 )})" );

				break;

			// Sends a guest to a thing as if they had chosen it: the walk to its back of queue and everything after
			// is the guest's own, and only the choice is skipped (PeepBehaviour.SendAsChosen). A run using it proves
			// the thing's side, not the guest's choice.
			case "send":
				if ( ParkPeople.Current is not { } senders )
				{
					Reply( "send: none - a park has to be loaded" );
					break;
				}

				if ( parts.Length < 3 )
				{
					Reply( "send <guest> <thing>" );
					break;
				}

				var sentGuest = (int)Argument( 1, 0 );
				var sentTo = (int)Argument( 2, 0 );

				Reply( senders.SendAsChosen( sentGuest, sentTo ) is { } refused
					? $"send: {refused}"
					: $"send: guest {sentGuest} going to thing {sentTo}" );

				break;

			// Brings a whole load in, of whatever size is asked for, which is the only way to see the
			// second and third vehicles in the stock park: its loads are 13 and fewer, and a crowd under 36
			// takes the bus. The banding is the original's own - under 36 the bus, up to 60 the seaplane,
			// beyond that the ferry.
			case "load":
				if ( ParkPeople.Current is not { } loading )
				{
					Reply( "load: none - a park has to be loaded" );
					break;
				}

				var coming = (int)Argument( 1, 40 );
				var by = loading.ForceArrival( coming );

				Reply( $"load: {coming} arriving, vehicle {by} - they come one a tick" );

				break;

			// Sends one guest home, which is the other half of `arrive`. Driven by hand for the same
			// reason: a guest sets off of their own accord only when miserable, shut out, or caught deciding
			// as their day runs out, and waiting for one is a poor way to find out whether the removal works.
			//
			// With no id it takes the first guest no thing is holding, because a guest a ride or a
			// queue has is refused - see ParkPeople.Depart.
			case "depart":
				if ( ParkPeople.Current is not { } departures )
				{
					Reply( "depart: none - a park has to be loaded" );
					break;
				}

				var asked = (int)Argument( 1, 0 );

				var leaving = asked != 0
					? asked
					: departures.Peeps.FirstOrDefault(
						peep => !PeepBehaviour.HeldByAThing( peep ) )?.ThingId ?? 0;

				Reply( leaving != 0 && departures.Depart( leaving )
					? $"depart: guest {leaving} went home"
					: "depart: nobody could go" );

				break;

			// The ids the next things made will take, the free list from its head, and the ids deleted this sweep
			// that its end will put back on it, newest first - see ParkState.NextThingId.
			case "thingids":
				if ( ParkState.Current is not { } numbered )
				{
					Reply( "thingids: none - a park has to be loaded" );
					break;
				}

				Reply( $"thingids: next {string.Join( ' ', numbered.FreeThingIds( (int)Argument( 1, 8 ) ) )}; "
					+ $"deleted {(numbered.DeletedThingIds.Count == 0 ? "none" : string.Join( ' ', numbered.DeletedThingIds ))}" );

				break;

			// An instrument: takes ids off the free list, held by no thing, until N are left free (nought fills the table),
			// so a full thing table can be met in a park far smaller than 0x27ff things - see ParkState.NextThingId. A save
			// made after it is not a park file to keep.
			case "thingfill":
				if ( ParkState.Current is not { } filled )
				{
					Reply( "thingfill: none - a park has to be loaded" );
					break;
				}

				var keepFree = Math.Max( 0, (int)Argument( 1, 0 ) );
				var freeIds = filled.FreeThingIds( ParkState.LastThingId + 1 ).Count();
				var takenIds = 0;

				for ( ; freeIds - takenIds > keepFree; ++takenIds )
					filled.NextThingId();

				Reply( $"thingfill: {takenIds} ids taken and held by nothing, {freeIds - takenIds} left free" );

				break;

			// What this session has reached and not built. Each gap announces itself once on the console
			// when it is first reached and is counted after that, so this is how to ask what a whole run
			// hit without scrolling back through it - see Unimplemented.
			case "unimplemented":
				var gaps = Unimplemented.Summary;

				if ( gaps.Count == 0 )
				{
					Reply( "unimplemented: nothing has been reached that is not built" );
					break;
				}

				Reply( $"unimplemented {gaps.Count}" );

				foreach ( var gap in gaps )
					Reply( $"  {gap.Times,5}x {gap.What}" );

				break;

			// The research lab as it runs: its hand-outs, each kind's current project and highest group, and each project
			// with its points.
			case "research":
				if ( Level.Current?.Research is not { } lab )
				{
					Reply( "research: none - a park has to be loaded" );
					break;
				}

				Reply( $"research hand-outs {lab.HandOuts}, {lab.Projects.Count( project => !(project.Done < project.Cost) )} of "
					+ $"{lab.Projects.Count} projects done, work load {lab.WorkLoad}" );

				for ( var labKind = 0; labKind < lab.Kinds.Count; ++labKind )
					Reply( $"  kind {labKind}: effort {lab.Kinds[labKind].Effort}, current {lab.Kinds[labKind].CurrentProject}, highest group {lab.Kinds[labKind].HighestGroup}" );

				if ( Level.Current.ParkState is { } researchState )
				{
					for ( var tab = 0; tab < ParkWorld.ResearchedRings; ++tab )
						Reply( $"  recently researched, tab {tab}: {researchState.RecentlyResearched.Census( tab )}" );
				}

				for ( var slot = 0; slot < lab.Projects.Count; ++slot )
				{
					var project = lab.Projects[slot];
					Reply( $"  project {slot}: item {project.ItemId} kind {project.Category} group {project.Group} tier {project.UpgradeLevel} "
						+ $"{project.Done:0.###} of {project.Cost:0.###}, item researched {(lab.IsResearched( project.ItemId ) ? 1 : 0)}" );
				}

				break;

			// The staff as the simulation holds them: `peeps` is guests only, and `guests` shows staff only as sprites.
			case "staff":
				if ( ParkPeople.Current is not { } employer )
				{
					Reply( "staff: none - a park has to be loaded" );
					break;
				}

				var working = employer.StaffCensus().ToArray();
				Reply( $"staff {working.Length}" );

				foreach ( var member in working )
					Reply( "  " + member );

				break;

			// A dash on the ground under each person, pointing the way they face and coloured by kind.
			// Toggles, or takes 0/1, as `mute` does.
			case "facing":
				ParkGuestSprites.DebugFacing = parts.Length > 1
					? Argument( 1 ) != 0f
					: !ParkGuestSprites.DebugFacing;

				Reply( $"facing {(ParkGuestSprites.DebugFacing ? "on" : "off")}" );
				break;

			// The people's pictures, their balloons and bubbles drawn or not (ParkGuestSprites.DebugHidden), the pick's
			// record kept either way: a paused frame with and one without is the people's own pixels. Toggles, or 0/1.
			case "sprites":
				ParkGuestSprites.DebugHidden = parts.Length > 1
					? Argument( 1 ) == 0f
					: !ParkGuestSprites.DebugHidden;

				Reply( $"sprites {(ParkGuestSprites.DebugHidden ? "hidden" : "drawn")}" );
				break;

			// The people's shadows: `shadows` the census (the tier, the light, the colour, how many the last frame drew),
			// `shadows 0`/`shadows 1` leaves them undrawn or draws them, `shadows THING` that person's four corners in the
			// world and the window (0 and 1 the far end).
			case "shadows":
				{
					var sprites = ParkGuestSprites.Current;
					var thing = parts.Length > 1 ? (int)Argument( 1 ) : -1;

					if ( thing is 0 or 1 && parts.Length > 1 )
						ParkGuestSprites.DebugShadowsHidden = thing == 0;

					var travels = Level.Current?.ParkLight?.Travels;
					var darkness = travels is { } t ? ParkPersonShadows.Darkness( t ) : null;

					Reply( $"shadows: tier {ParkPersonShadows.Tier( Level.DetailFile )} ({Level.DetailFile} SPRITESHADOW) "
						+ $"{(ParkGuestSprites.DebugShadowsHidden ? "hidden" : "drawn")} {sprites?.ShadowsDrawn.Count ?? 0} "
						+ $"light {travels?.ToString() ?? "none"} darkness {darkness?.ToString() ?? "none"} "
						+ $"colour 0x{(darkness is { } d ? ParkPersonShadows.Colour( d ) : 0u):x8} "
						+ $"lift {(ParkCamcorderCameraMode.Active ? ParkPersonShadows.FirstPersonLift : ParkPersonShadows.Lift)}" );

					foreach ( var (who, corners) in sprites?.ShadowsDrawn ?? [] )
					{
						if ( who != thing )
							continue;

						var said = $"shadow: thing {who}";

						foreach ( var corner in corners )
						{
							var seen = System.Numerics.Vector4.Transform( new System.Numerics.Vector4( corner.X, corner.Y, corner.Z, 1f ), Camera.ViewMatrix * Camera.ProjMatrix );
							said += $" ({corner.X:F2},{corner.Y:F2},{corner.Z:F2})=px({(seen.X / seen.W + 1f) * Screen.Width / 2f:F1},{(1f - seen.Y / seen.W) * Screen.Height / 2f:F1})";
						}

						Reply( said );
					}

					break;
				}

			// First person's fog: the band, whether it reaches this frame's draws, the colour and what the shaders get.
			// `fog S E` holds another band, in the original's clip z as memhold.py holds [0x008bd490]/[0x008bd494] there;
			// `fog file` puts the detail file's back.
			case "fog":
				{
					if ( Level.Current is not { Kind: Level.Scene.Park } fogged )
					{
						Reply( "fog: only in a park" );
						break;
					}

					if ( parts.Length > 2 )
						fogged.FirstPersonFog = new FirstPersonFog( Argument( 1 ), Argument( 2 ) );
					else if ( parts.Length > 1 && parts[1] == "file" )
						fogged.FirstPersonFog = FirstPersonFog.Read( Level.DetailFile );

					var colour = Level.FogColour;
					var uniform = Level.LinearFog;
					Reply( $"fog: first person {(ParkCamcorderCameraMode.Active ? "on" : "off")}, band {fogged.FirstPersonFog?.ToString() ?? "none"} "
						+ $"({Level.DetailFile} FIRSTPERSONVIEWDISTANCE), colour ({colour.X * 255f:0},{colour.Y * 255f:0},{colour.Z * 255f:0}), "
						+ $"shaders ({uniform.X:0.0},{uniform.Y:0.0},{uniform.Z:0})" );
					break;
				}

			// The park map's census: its view (centre, zoom, the picture's source and where it lands) and the help bar's
			// place; "map zoom Z" sets the kept zoom for a repeatable picture.
			case "map":
				if ( parts.Length > 2 && parts[1] == "zoom" )
					UI.ParkMapView.KeptZoom = Math.Clamp( Argument( 2 ), UI.ParkMapView.MinZoom, UI.ParkMapView.MaxZoom );

				Reply( UI.ParkMapScreen.Current is { } map
					? $"{map.View} helpbar top {map.HelpBarTop?.ToString() ?? "none"} option {(GameOptions.Current.RightButtonScroll ? "right button" : "pushscroll")}"
					: $"map: not open, kept zoom {UI.ParkMapView.KeptZoom:0.0}" );
				break;

			// Where the park camera is, in world units and in grid cells - the two coordinate systems
			// a park is described in, and the pair most worth seeing side by side while placing things.
			case "camera":
				if ( parts.Length > 2 )
					ParkOrbitCameraMode.PointOfInterest = new Vector3( Argument( 1 ), Argument( 2 ), 0f );

				if ( parts.Length > 3 )
					ParkOrbitCameraMode.Zoom = Argument( 3 );

				// A yaw in degrees, so a photograph can be taken from the far side of something.
				if ( parts.Length > 4 )
					ParkOrbitCameraMode.Yaw = Argument( 4 ) * MathF.PI / 180f;

				Reply( ParkOrbitCameraMode.State() );
				break;

			// The End feature's census: the thing, its two scripts, the camera's eye and look, the cover and the music dip.
			case "endfeature":
				{
					if ( ParkEndFeature.Current is not { } end )
					{
						Reply( $"endfeature: none standing; camera eye ({Camera.Position.X:F2}, {Camera.Position.Y:F2}, {Camera.Position.Z:F2}) ride view "
							+ $"{(ParkRideViewCameraMode.Active ? "on" : "off")}; music dip {ParkRides.Current?.Scheduler.MusicDip}" );
						break;
					}

					var scheduler = ParkRides.Current?.Scheduler;
					var own = scheduler?.Find( end.Script.Id );
					var child = own is { ChildId: not 0 } ? scheduler?.Find( own.ChildId ) : null;
					var look = Camera.Rotation.Forward;

					Reply( $"endfeature: thing {end.ThingId}, script {end.Script.Id} {end.Script.Name} {(own?.Running == true ? "running" : "ended")}"
						+ $" VAR_STATUS {(own is null ? "-" : own["VAR_STATUS"].ToString())}, child {(child is null ? "none" : $"{child.Id} {child.Name}")}"
						+ $", made {Time.WallMilliseconds - end.MadeAt} ms ago; camera eye ({Camera.Position.X:F2}, {Camera.Position.Y:F2}, "
						+ $"{Camera.Position.Z:F2}) look ({look.X:F3}, {look.Y:F3}, {look.Z:F3}) ride view {(ParkRideViewCameraMode.Active ? "on" : "off")}"
						+ $"; cover {(Level.Current?.FullScreenView == true ? "on" : "off")}; music dip {scheduler?.MusicDip}" );

					// Where each plane is drawn, and whether it is shown: what the clips' position tracks move.
					if ( ParkObjects.Current?.ModelFor( end.ThingId ) is { } model )
					{
						var planes = model.Source.Meshes
							.Select( ( mesh, index ) => (Name: mesh.Name.TrimEnd( '\0' ), Index: index) )
							.Where( mesh => mesh.Name.StartsWith( "_f_endplane", StringComparison.OrdinalIgnoreCase ) )
							.OrderBy( mesh => mesh.Name, StringComparer.Ordinal )
							.Select( mesh => model.Entities[mesh.Index] is var drawn
								? $"{mesh.Name[^2..]} ({drawn.Position.X:F1}, {drawn.Position.Y:F1}, {drawn.Position.Z:F1}){(drawn.Opacity > 0f ? "" : " hidden")}"
								: "" );

						Reply( $"endfeature planes: {string.Join( "; ", planes )}" );
					}

					// Where each particle the two scripts started stands now, in the park's axes: what the sweep carries.
					if ( ParticleSystem.Current is { } trailSystem )
					{
						var trails = new[] { own, child }.Where( script => script?.Effects is not null )
							.SelectMany( script => script!.Effects!.Records.Where( record => record.IsParticle && record.Emitter != 0 )
								.Select( record => trailSystem.TryEmitter( record.Emitter, out var emitter )
									? $"{script.Id}:{record.Effect}@{record.Node} ({emitter.X / 64f:F1}, {emitter.Z / 64f:F1}, {emitter.Y / 64f:F1})"
										+ (emitter.Lifetime < 0 ? " killed" : "")
									: $"{script.Id}:{record.Effect}@{record.Node} gone" ) );

						Reply( $"endfeature trails: carried {(own?.EmittersCarried ?? 0) + (child?.EmittersCarried ?? 0)}; {string.Join( "; ", trails )}" );
					}

					break;
				}

			// Down to eye level and back, which is the only way to see a park's sky: from the orbit
			// camera the horizon sits above the top of the frame. `camcorder` toggles; two arguments
			// stand the viewer at a world position first.
			case "camcorder":
				// A park only. The camera it swaps in flies park coordinates, so letting this run in
				// the lobby leaves the islands out of frame with no key to get back - the console can
				// reach it whether or not a park is up, so the guard belongs here.
				if ( Level.Current?.Kind != Level.Scene.Park )
				{
					Reply( "camcorder: only in a park" );
					break;
				}

				if ( parts.Length > 2 )
				{
					if ( !ParkCamcorderCameraMode.Active )
						ParkCamcorderCameraMode.Enter();

					// StandAt rather than Stand: this puts the viewer down somewhere else rather than
					// walking them there, so the eye takes the ground as-is. Assigning Stand would leave it
					// easing up from the old height, which never finishes while the clock is stopped -
					// and a stopped clock is how frames are captured.
					ParkCamcorderCameraMode.StandAt( new Vector3( Argument( 1 ), Argument( 2 ), 0f ) );

					// An optional heading, in radians, because a capture cannot otherwise be AIMED.
					// Steer() turns the view from where the POINTER is - not from how far it moved - and
					// the console cannot move the pointer, so without this a screenshot faces wherever the
					// mouse was last left and then drifts for as long as the clock runs.
					if ( parts.Length > 3 )
						ParkCamcorderCameraMode.Yaw = Argument( 3 );
				}
				else if ( ParkCamcorderCameraMode.Active )
				{
					ParkCamcorderCameraMode.Leave();
				}
				else
				{
					ParkCamcorderCameraMode.Enter();
				}

				Reply( ParkCamcorderCameraMode.Active
					? ParkCamcorderCameraMode.State()
					: $"orbit - {ParkOrbitCameraMode.State()}" );
				break;

			// Walking on the ground, driven by hand, because `camcorder x y` TELEPORTS - it calls
			// StandAt - and so crosses no cell edge at all, and what happens BETWEEN two cells is the question.
			//
			// It reaches ParkCamcorderCameraMode.Step, the same body Walk() runs with the real keys, so
			// only the reading of Input is skipped. Frames rather than seconds, because the step is
			// per-frame: 60 frames is a second of walking at the ordinary speed.
			case "walk":
				if ( !ParkCamcorderCameraMode.Active )
				{
					Reply( "walk: camcorder mode is not active - use `camcorder` first" );
					break;
				}

				if ( parts.Length < 3 )
				{
					Reply( "walk <forward> <right> [frames]" );
					break;
				}

				ParkCamcorderCameraMode.DebugWalk( Argument( 1 ), Argument( 2 ),
					parts.Length > 3 ? (int)Argument( 3 ) : 60 );

				Reply( ParkCamcorderCameraMode.State() );
				break;

			// What the pointer is over. The instrument for every verb that starts by pointing at the
			// ground, and the only way to see whether the ray lands where the cursor is drawn - which
			// a screenshot alone cannot settle, because a cell is ten units across and a pitched
			// camera makes "further away" and "higher up the frame" look the same.
			// A person's pick circle as the last frame drew them (SpritePick.Circle): its middle and radius in window
			// pixels, so a harness can aim at it and at its edge, and the pixel their origin is drawn at. With no thing,
			// every person drawn.
			case "circle":
				{
					var who = parts.Length > 1 ? (int)Argument( 1 ) : 0;
					var listed = 0;
					var frame = SpritePick.Frame( Screen.Width, Screen.Height );

					foreach ( var picture in ParkGuestSprites.Current?.Drawn ?? [] )
					{
						if ( (who != 0 && picture.Thing != who)
							|| SpritePick.Circle( picture, Camera.ViewMatrix * Camera.ProjMatrix, frame ) is not { } circle )
							continue;

						++listed;
						var place = System.Numerics.Vector4.Transform( new System.Numerics.Vector4( picture.Origin.X, picture.Origin.Y, picture.Origin.Z, 1f ), Camera.ViewMatrix * Camera.ProjMatrix );
						Reply( $"circle: thing {picture.Thing} middle ({(circle.X / frame.X + 1f) * Screen.Width / 2f:F1},{(circle.Y / frame.Y + 1f) * Screen.Height / 2f:F1}) " +
							$"origin pixel ({(place.X / place.W + 1f) * Screen.Width / 2f:F1},{(1f - place.Y / place.W) * Screen.Height / 2f:F1}) " +
							$"radius {MathF.Sqrt( circle.Reach ) / frame.X * Screen.Width / 2f:F1} x {MathF.Sqrt( circle.Reach ) / frame.Y * Screen.Height / 2f:F1} r2 {circle.Reach:F4} " +
							$"depth {circle.Depth:F2} size {picture.Width}x{picture.Height} origin {picture.OriginX},{picture.OriginY} mirrored {picture.Mirrored} " +
							$"of {Screen.Width:F0}x{Screen.Height:F0} " +
							$"tested {(ParkGuestSprites.Current?.Pulled == true ? ParkGuestSprites.PulledDepth( circle.Depth ) : circle.Depth):F2}" );
					}

					if ( listed == 0 )
						Reply( who != 0 ? $"circle: thing {who} not drawn" : "circle: nobody drawn" );

					break;
				}

			case "pick":
				if ( Level.Current?.Kind != Level.Scene.Park )
				{
					Reply( "pick: only in a park" );
					break;
				}

				// With two arguments it asks about a point of the window instead of the pointer. That is
				// not a convenience: the console cannot move the pointer - a warp
				// with no real motion behind it reaches the window system and never reaches the game -
				// so a test that could only ask about the cursor would be measuring X as much as the
				// arithmetic. `arrive`, `load` and `thirst` exist for the same reason.
				Reply( parts.Length > 2
					? ParkPicking.PickAt( Argument( 1 ), Argument( 2 ) )
					: ParkPicking.State() );
				break;

			// The picker handed a ray rather than a window point: from an eye through a point, both in world units, so
			// the original's eye and its pick point P (its x, height, z as our x, z, y) can be fed straight in.
			case "pickray":
				if ( Level.Current?.Kind != Level.Scene.Park || parts.Length < 7 )
				{
					Reply( "pickray: pickray <eye x> <eye y> <eye z> <through x> <through y> <through z>, in a park" );
					break;
				}

				Reply( ParkPicking.PickRay( new Vector3( Argument( 1 ), Argument( 2 ), Argument( 3 ) ),
					new Vector3( Argument( 4 ), Argument( 5 ), Argument( 6 ) ) ) );
				break;

			// A click at a point of the window, and it takes coordinates for exactly the reason `pick`
			// does: a warp of the pointer reaches X and never reaches SDL, so a harness cannot select
			// a row by warping the cursor onto it. Only SDL is skipped - the hit test, the row
			// arithmetic and both the press and the release handlers are the real ones.
			case "click":
				if ( parts.Length < 3 )
				{
					Reply( "click: click <x> <y>, in window pixels" );
					break;
				}

				if ( Level.Current?.Hud?.Children.OfType<UI.WindowStack>().FirstOrDefault() is not { } clickStack )
				{
					Reply( "click: no window stack" );
					break;
				}

				if ( clickStack.ClickAt( Argument( 1 ), Argument( 2 ) ) )
				{
					Reply( $"click: the interface took ({Argument( 1 ):F0},{Argument( 2 ):F0})" );
					break;
				}

				// The interface did not want it, so the world gets it - the same order a real frame
				// takes, where Level.WorldClick runs after Hud.Update.
				if ( Level.Current is not { Kind: Level.Scene.Park } clickPark )
				{
					Reply( clickStack.ViewTook
						? $"click: the view took ({Argument( 1 ):F0},{Argument( 2 ):F0})"
						: $"click: nothing took ({Argument( 1 ):F0},{Argument( 2 ):F0})" );
					break;
				}

				if ( Level.LeftPressTaken( false ) )
				{
					Reply( $"click: first person took ({Argument( 1 ):F0},{Argument( 2 ):F0})" );
					break;
				}

				if ( Level.KeptFromThePark( clickStack.ParkScreenUp ) )
				{
					Reply( $"click: kept from the park beside an open screen ({Argument( 1 ):F0},{Argument( 2 ):F0})" );
					break;
				}

				if ( !ParkPicking.TryCellAt( Argument( 1 ), Argument( 2 ), out var clickX, out var clickY ) )
				{
					Reply( "click: no cell yet: the ray has never met the ground" );
					break;
				}

				var (clickPerson, clickObject) = ParkPicking.ThingsAt( Argument( 1 ), Argument( 2 ) );
				Reply( clickPark.ClickWorldAt( clickX, clickY, clickPerson, clickObject ) );
				break;

			// Buying, selling and moving something, driven by hand. A player buys through the buy screen
			// and sells and moves through an object's window, buying and moving by way of the hand; these
			// call ParkBuilding in one step each, so a verb can be proved in a running park without the
			// screens in front of it.
			case "buy":
				Reply( parts.Length > 3
					? ParkBuilding.Buy( (int)Argument( 1 ), (int)Argument( 2 ), (int)Argument( 3 ),
						parts.Length > 4 ? (int)Argument( 4 ) : 0 )
					: "buy <catalogueId> <cellX> <cellY> [angle]" );
				break;

			// Hiring, firing, and carrying somebody from one place to another, each in one step. `hire`
			// runs the place-staff click's body, ParkStaffPool.Hire, without the hire screen or the hand;
			// `hirescreen`, `click` and `worldclick` drive the real path. The staff window that would fire
			// and carry is not built.
			case "hire":
				if ( parts.Length < 4 )
				{
					Reply( "hire <candidateId> <cellX> <cellY>" );
					break;
				}

				if ( Level.Current?.StaffPool is not { } hiring || ParkPeople.Current is not { } staffing )
				{
					Reply( "hire: a park has to be loaded" );
					break;
				}

				var wanted = (int)Argument( 1 );

				if ( hiring.Find( wanted ) is not { } candidate )
				{
					Reply( $"hire: nobody in the pool is candidate #{wanted}" );
					break;
				}

				// The place-staff click's own body, which takes them out of the pool only once they are
				// standing in the park.
				var hired = hiring.Hire( staffing, candidate, (int)Argument( 2 ), (int)Argument( 3 ) );

				if ( hired == 0 )
				{
					Reply( $"hire: '{candidate.Name}' could not be put there, and is still waiting" );
					break;
				}

				Reply( $"hire: '{candidate.Name}' is thing {hired}, {candidate.Wage} a month" );
				break;

			case "fire":
				Reply( parts.Length > 1 && ParkPeople.Current is { } employer2
					? employer2.Fire( (int)Argument( 1 ) )
						? $"fire: thing {(int)Argument( 1 )} dismissed"
						: $"fire: nobody in the park is thing {(int)Argument( 1 )}"
					: "fire <thingId>" );
				break;

			case "pickup":
				Reply( parts.Length > 1 && ParkPeople.Current is { } holder
					? holder.PickUp( (int)Argument( 1 ) )
						? $"pickup: holding thing {(int)Argument( 1 )}"
						: $"pickup: nobody in the park is thing {(int)Argument( 1 )}"
					: "pickup <thingId>" );
				break;

			// The staff HQ's strike records: the force and {level, flag, stamp} a kind, handymen, mechanics,
			// entertainers, guards, researchers, with the clock and what the month's look would ask.
			case "strikes":
				Reply( ParkPeople.Current is { } striking
					? $"strikes: tick {striking.State.GameTick} {striking.State.CalendarNow:d/M/yyyy} "
						+ $"months {ParkStrikes.MonthsSinceTheFirstSweep( striking.State.GameTick )} "
						+ $"closed {(striking.State.ParkIsClosed ? 1 : 0)} guests {striking.GateGuestCensus} "
						+ striking.Strikes.Describe()
					: "strikes: a park has to be loaded" );
				break;

			// An instrument: sets or clears the staff HQ's mForceStrike, one word for the whole park, which only
			// a save sets in the original.
			case "forcestrike":
				if ( parts.Length > 1 && ParkPeople.Current is { } forced )
				{
					forced.Strikes.Force = (int)Argument( 1 ) != 0;
					Reply( $"forcestrike: force {(forced.Strikes.Force ? 1 : 0)}" );
				}
				else
					Reply( "forcestrike <0|1>" );

				break;

			// An instrument: writes the park's clock, mGameTick, which the calendar reads after the next frame's
			// sweeps. A write into another month is a month's change.
			case "clock":
				if ( parts.Length > 1 && Level.Current?.ParkState is { } clocked )
				{
					clocked.SetGameTick( (int)Argument( 1 ) );
					Reply( $"clock: mGameTick {clocked.GameTick}, {clocked.CalendarNow:d/M/yyyy}" );
				}
				else
					Reply( "clock <mGameTick>" );

				break;

			// An instrument, as `happy` is for guests: sets how rested one member of staff is, 0 to 100, so the
			// tired arms of their decide can be reached without waiting a member out.
			case "staffrest":
				Reply( parts.Length > 2 && ParkPeople.Current is { } tiring
					? tiring.SetStaffRest( (int)Argument( 1 ), Argument( 2 ) )
						? $"staffrest: thing {(int)Argument( 1 )} rest {Argument( 2 )}"
						: $"staffrest: nobody on the staff is thing {(int)Argument( 1 )}"
					: "staffrest <thingId> <0-100>" );
				break;

			case "putstaff":
				Reply( parts.Length > 2 && ParkPeople.Current is { } dropper
					? dropper.DropStaff( (int)Argument( 1 ), (int)Argument( 2 ) )
						? $"putstaff: put down at ({(int)Argument( 1 )},{(int)Argument( 2 )})"
						: "putstaff: nobody is being carried, or that cell is off the map"
					: "putstaff <cellX> <cellY>" );
				break;

			// Who the park may hire. A population of its own - the five staff read from the save are
			// the ones already employed, and these are the ones waiting.
			case "candidates":
				if ( Level.Current?.StaffPool is not { } pool )
				{
					Reply( "candidates: none - a park has to be loaded" );
					break;
				}

				var waiting = pool.Census().ToArray();
				Reply( $"candidates {waiting.Length}" );

				foreach ( var person in waiting )
					Reply( "  " + person );

				break;

			// The hand: what the buy screen puts an item into, and what a click on the ground would
			// take out of it. Carrying costs nothing - the money is taken when the thing goes up.
			// `carry <item> <thing>` takes a track add-on for the ride that thing is, as the ride window's Buy Upgrades row does.
			case "carry":
				Reply( parts.Length > 2 ? ParkBuilding.CarryFor( (int)Argument( 1 ), (int)Argument( 2 ) )
					: parts.Length > 1 ? ParkBuilding.Carry( (int)Argument( 1 ) )
					: ParkBuilding.HandState() );
				break;

			// The idle mode installed over whatever is current - the body a quick right click, Escape and leaving
			// the park all reach, whatever the RMB cancel switch says.
			case "drop":
				Reply( ParkHand.LetGo() is { } letGo
					? $"drop: {letGo}"
					: "drop: nothing is being carried" );
				break;

			// Everything the hand holds, and the build tool - see ParkHand.
			case "hand":
				Reply( ParkHand.Census() );
				break;

			// A quick right click - the same body the button reaches, which the RMB cancel switch gates.
			case "rightclick":
				Reply( Level.QuickRightClick()
					?? $"rightclick: nothing to put away (tool mode {ParkBuildMode.Current}, RMB cancel " +
						$"{(GameOptions.Current.RmbCancel ? "on" : "off")})" );
				break;

			// The Options switch "RMB cancel", in memory only. The options screen's tick would also write
			// Config.tcf and opentpw.cfg, which a harness must not touch.
			case "rmbcancel":
				if ( parts.Length > 1 )
					GameOptions.Current.RmbCancel = parts[1] == "on";

				Reply( $"rmbcancel: {(GameOptions.Current.RmbCancel ? "on" : "off")}" );
				break;

			// The Options switch "Tutorial", in memory only, as rmbcancel's: the advisor's gate reads it at each post
			// of a tutorial message.
			// The game type the park advisor's poll takes: `advisorgame full` holds it to Full Simulation's and
			// `advisorgame instant` to Instant Action's, whatever the player's is; `advisorgame own` lets go. An
			// instrument, the twin of holding the original's 0x00fb3b7c: no screen of the game does it.
			case "advisorgame":
				if ( ParkState.Current?.AdvisorMessages is not { } polling )
				{
					Reply( "advisorgame: a park has to be loaded" );
					break;
				}

				if ( parts.Length > 1 )
					polling.InstantActionHeld = parts[1] switch { "full" => false, "instant" => true, _ => null };

				Reply( $"advisorgame: {(polling.InstantAction ? "instant" : "full")} held={(polling.InstantActionHeld is { } gameHeld ? (gameHeld ? "instant" : "full") : "none")}" );
				break;

			case "tutorial":
				if ( parts.Length > 1 )
					GameOptions.Current.Tutorial = parts[1] == "on";

				Reply( $"tutorial: {(GameOptions.Current.Tutorial ? "on" : "off")}" );
				break;

			// Where the pointer is and the control under it, so a real press can be predicted before it is made. In a park,
			// also the cursor, the help bar's posted row and what it shows, and whose a right press there would be: the interface's, or
			// the park's, which arms the quick click.
			case "pointer":
				var pointerStack = Level.Current?.Hud?.Children.OfType<UI.WindowStack>().FirstOrDefault();

				if ( Level.Current is { Kind: Level.Scene.Park } pointed )
				{
					Reply( $"pointer: cursor {pointed.ParkCursor}, help row {pointerStack?.HelpRow ?? -1} \"{pointerStack?.HelpBarText}\", at "
						+ $"({Input.Mouse.Position.X:F0},{Input.Mouse.Position.Y:F0}) over "
						+ (pointerStack?.Hovered is { } under ? $"control 0x{under.Id:x}" : "no control")
						+ $"; a right press here is {(Level.RightPressTaken( pointerStack is { Covered: true } || pointerStack?.TakesRightPress( Input.Mouse.Position.X, Input.Mouse.Position.Y ) == true ) ? "the interface's" : "the park's")}" );
					break;
				}

				Reply( $"pointer: at ({Input.Mouse.Position.X:F0},{Input.Mouse.Position.Y:F0}), over "
					+ (pointerStack?.Hovered is { } over ? $"control 0x{over.Id:x}"
						: pointerStack?.ModalUp == true ? "no control, and a modal window takes the press" : "the view") );
				break;

			// The help bar as it was last drawn: its text's lines, the width they were wrapped at in the font's pixels and
			// where the block stands on the strip.
			case "helpbar":
				var barStack = Level.Current?.Hud?.Children.OfType<UI.WindowStack>().FirstOrDefault();

				if ( barStack is not { } stack || stack.HelpBarText.Length == 0 )
				{
					Reply( "helpbar: hidden" );
					break;
				}

				Reply( $"helpbar: wrap {stack.HelpBar.WrapWidth}, down {stack.HelpBar.Down}, lines {stack.HelpBar.Lines.Count}: "
					+ string.Join( " | ", stack.HelpBar.Lines.Select( line => $"\"{line}\"" ) ) );
				break;

			// Backspace, as the key reaches it - the same body. It acts on the pointer's cell, so pair it
			// with `hover` for the idle delete.
			case "backspace":
				Reply( Level.Current?.BackspaceKey() ?? "backspace: a park has to be loaded" );
				break;

			// `put`, not `place` - the lobby already has a `place`, which auditions an ambient sample
			// at a position. Two cases with one label does not compile, but the quieter version of the same mistake is a command that works in one scene and
			// silently means something else in the other.
			case "put":
				Reply( parts.Length > 2
					? ParkBuilding.PlaceCarried( (int)Argument( 1 ), (int)Argument( 2 ),
						parts.Length > 3 ? (int)Argument( 3 ) : null )
					: "put <cellX> <cellY> [angle]" );
				break;

			case "sell":
				Reply( parts.Length > 1
					? ParkBuilding.Sell( (int)Argument( 1 ) )
					: "sell <thingId>" );
				break;

			// The window's move, then one click at the cell. A cell that refuses leaves the thing in the hand.
			case "move":
				Reply( parts.Length > 3
					? ParkBuilding.Move( (int)Argument( 1 ), (int)Argument( 2 ), (int)Argument( 3 ),
						parts.Length > 4 ? (int)Argument( 4 ) : null )
					: "move <thingId> <cellX> <cellY> [angle]" );
				break;

			// One-cell path commits use the player tool's verdict and stamp. The pointer tool separately
			// anchors its first click and lays an axis-aligned run on the second (Level.WorldClick).
			case "path":
				Reply( parts.Length > 2
					? ParkPathBuilding.Lay( (int)Argument( 1 ), (int)Argument( 2 ) )
					: "path <cellX> <cellY>" );
				break;

			case "delpath":
				Reply( parts.Length > 2
					? ParkPathBuilding.Lift( (int)Argument( 1 ), (int)Argument( 2 ) )
					: "delpath <cellX> <cellY>" );
				break;

			// A queue cell has to say which object it serves and which way it was entered from: the
			// flow byte it stores is the OPPOSITE of that step, and the walk that measures the queue
			// accepts a cell only when its flow points back the way the walk came. So the `from` pair
			// is not decoration - written the other way round, every queue measures nought long.
			case "queue":
				Reply( parts.Length > 5
					? ParkPathBuilding.LayQueue( (int)Argument( 1 ), (int)Argument( 2 ),
						(int)Argument( 3 ), (int)Argument( 4 ), (int)Argument( 5 ) )
					: "queue <cellX> <cellY> <servesThingId> <fromX> <fromY>" );
				break;

			case "delqueue":
				Reply( parts.Length > 2
					? ParkPathBuilding.LiftQueue( (int)Argument( 1 ), (int)Argument( 2 ) )
					: "delqueue <cellX> <cellY>" );
				break;

			// What the renderer actually holds, as opposed to what the simulation believes.
			//
			// Correlating screenshots against console replies by hand gives a different answer each
			// time: the console can say the cells are laid and the money spent while the picture shows
			// neither. A capture cannot arbitrate between the two halves of the program; only the
			// program can. So this reports the DRAW side - how many
			// cells the path mesh actually contains, how many queue pieces are standing, whether the
			// surfaces exist at all - beside the simulation side, in one reply, from one frame.
			//
			// If these two ever disagree, the defect is in the game and not in the harness. If they
			// agree and a photograph still disagrees with both, the harness is at fault and this says
			// so without anybody having to guess.
			case "drawn":
				if ( Level.Current is not { Kind: Level.Scene.Park } drawnPark )
				{
					Reply( "drawn: only in a park" );
					break;
				}

				var drawnState = drawnPark.ParkState;
				var sameState = ReferenceEquals( drawnState, ParkState.Current );

				var overlay = 0;

				if ( drawnState is not null )
					overlay = drawnState.ChangedCells;

				Reply( $"drawn: paths {ParkPaths.Current?.Drawn.ToString() ?? "<no ParkPaths>"} cells (laid over the ground's tiles " +
					$"{ParkPaths.Current?.LaidOver.ToString() ?? "?"}), " +
					$"queues {ParkQueues.Current?.Placed.ToString() ?? "<no ParkQueues>"} pieces, " +
					$"ground {(ParkGround.Current is null ? "<none>" : "built")} | " +
					$"overlay {overlay} changed cells, balance {drawnState?.Balance.ToString() ?? "?"} | " +
					$"Level.ParkState is ParkState.Current: {sameState}" );
				break;

			// A world click AT A CELL. `click` takes window pixels and needs the pointer to be over the
			// map, which is right for a real frame and useless for reaching one named cell - so this
			// reaches the very method a real frame reaches, Level.ClickWorldAt, which its own remarks
			// say was split out of WorldClick for exactly this purpose.
			//
			// THE CELL'S OBJECT IS RESOLVED THE WAY A FRAME RESOLVES IT - ParkPicking.ThingOn with people
			// false, as Level.WorldClick reaches it through ParkPicking.ObjectUnderCursor. The person is
			// the head of the cell's list where that is a person, since a harness has no cursor to latch
			// a picture with; a frame takes the person by their picture instead (SpritePick). Reading the
			// cell's own occupant for the object would make an instrument that cannot see the thing it is
			// pointed at: a placed thing sits on ONE cell's occupancy list and owns the rest of its
			// footprint through mParentID, so it would answer on the anchor alone, 1 of 12 cells of a ride.
			case "worldclick":
				if ( parts.Length < 3 )
				{
					Reply( "worldclick <cellX> <cellY>" );
					break;
				}

				if ( Level.Current is not { Kind: Level.Scene.Park } clickedPark
					|| clickedPark.ParkState is not { } clickedState )
				{
					Reply( "worldclick: only in a park" );
					break;
				}

				var clickedX = (int)Argument( 1 );
				var clickedY = (int)Argument( 2 );

				var clickedHead = ParkState.OnMap( clickedX, clickedY ) ? clickedState.CellAt( clickedX, clickedY ).Occupant : 0;
				var clickedPerson = clickedHead != 0 && ParkPeople.Current is { } clickedPeople
					&& (clickedPeople.Guests.ContainsKey( clickedHead ) || clickedPeople.IsStaff( clickedHead ))
					? clickedHead
					: 0;
				var clickedObject = ParkState.OnMap( clickedX, clickedY )
					? ParkPicking.ThingOn( clickedState, clickedX, clickedY, people: false )
					: 0;

				Reply( clickedPark.ClickWorldAt( clickedX, clickedY, clickedPerson, clickedObject ) );
				break;

			// Arms a build mode, so that clicking the park lays a RUN rather than one cell. The mouse
			// half is Level.WorldClick; this exists for the reason `click` and `put` do - the console
			// cannot press a button, and without it the anchor-and-commit path is unreachable from the
			// console. `tool` alone reports what is armed.
			case "tool":
				if ( parts.Length < 2 )
				{
					Reply( $"tool: mode {ParkBuildMode.Current}" +
						(ParkBuildMode.Anchored
							? $", anchored at ({ParkBuildMode.Anchor.X},{ParkBuildMode.Anchor.Y})"
							: ", not anchored") +
						$", {ParkBuildMode.Pending.Count} pending, {ParkBuildMode.Clicks} clicks" +
						$", latch {(ParkBuildMode.RefusalLatched ? 1 : 0)}" +
						" - `tool path`, `tool queue <thingId>`, `tool off`" );
					break;
				}

				Reply( parts[1].ToLowerInvariant() switch
				{
					"path" => ParkBuildMode.Arm( ParkBuildMode.Path ),
					"queue" => ParkBuildMode.Arm( ParkBuildMode.Queue,
						parts.Length > 2 ? (int)Argument( 2 ) : 0 ),
					_ => ParkBuildMode.Arm( ParkBuildMode.None )
				} );
				break;

			// Holds the pointer's CELL over a cell of the map, so what follows the pointer - the queue tool's
			// squares and its cursor - can be driven and photographed. `hover off` hands it back to the mouse.
			case "hover":
				if ( parts.Length > 2 && !ParkState.OnMap( (int)Argument( 1 ), (int)Argument( 2 ) ) )
				{
					Reply( "hover: that cell is off the map" );
					break;
				}

				// Only the CELL is held: the thing under the pointer and the world point still follow the
				// mouse, so a real click while a cell is held pairs this cell with whatever is under it.
				if ( parts.Length > 2 )
				{
					ParkPicking.Pinned = ((int)Argument( 1 ), (int)Argument( 2 ));
					Reply( $"hover: the pointer's cell is held at ({ParkPicking.Pinned.Value.X},{ParkPicking.Pinned.Value.Y})" );
				}
				else
				{
					ParkPicking.Pinned = null;
					Reply( "hover: following the mouse again" );
				}

				break;

			// The armed tool's preview as the SIMULATION computes it for a cell, beside what the marker mesh
			// last drew for the pointer - the two halves, in one reply, for the reason `drawn` gives.
			case "strip":
				if ( parts.Length < 3 )
				{
					Reply( $"strip <cellX> <cellY> - drawn for the pointer: {ParkBuildMarkers.Current?.Census() ?? "<no markers>"}" );
					break;
				}

				// A square marked $ refused for money (c_cash); the run total is what the run owes after its last square, the second charges in it.
				var stripSquares = ParkPathBuilding.Strip( (int)Argument( 1 ), (int)Argument( 2 ), out var stripLatched );

				Reply( "strip: " + string.Join( " ", stripSquares
					.Select( square => $"({square.X},{square.Y}):{square.Marker}" + (square.Unaffordable ? "$" : "")
						+ (square.Why is { } why ? $"[{why}]" : "") ) )
					+ $" | run total {(stripSquares.Count > 0 ? stripSquares[^1].Owed : 0)}, latch {(stripLatched ? 1 : 0)}" );
				break;

			// The harbour's moving sea: its corner, phase, folder and texture words; `sea y <h>` moves its corner's
			// height, as the original's was held under Line01 to read the lake alone, and `sea phase <p>` puts its wave
			// anywhere, which a paused park then holds.
			case "sea":
				if ( ParkSea.Current is not { } sea )
				{
					Reply( "sea: none - a park has to be loaded" );
					break;
				}

				if ( parts.Length >= 3 && parts[1] == "y" )
					sea.SetHeight( Argument( 2 ) );

				if ( parts.Length >= 3 && parts[1] == "phase" )
					sea.SetPhase( Argument( 2 ) );

				Reply( $"sea: corner {sea.CornerText} phase {sea.Phase:0.000} from {sea.Folder} words {sea.Census()}" );
				break;

			// The gates' flags: how many, their phase (the sea's), the angle they stand at and the weather's wind it eases to,
			// and the folder; `flags wind <degrees>` holds the weather's wind there and stands the flags at it at once, as the
			// original's frames were taken with its mWindDir held, and `flags wind free` lets it go.
			case "flags":
				if ( ParkFlags.Current is not { } flags )
				{
					Reply( "flags: none - a park has to be loaded" );
					break;
				}

				if ( parts.Length >= 3 && parts[1] == "wind" )
				{
					ParkWeather.Current?.DebugWind( parts[2] == "free" ? null : Argument( 2 ) );
					if ( parts[2] != "free" )
						flags.DebugSnap();
				}

				Reply( $"flags: {ParkFlags.Poles.Length} of {ParkFlags.FacesPerFlag} faces, phase {flags.Phase:0.000} "
					+ $"at {flags.Wind:0.###} wind {ParkWeather.Current?.WindDirection.ToString( "0.###" ) ?? "none"} "
					+ $"now {Time.Now:0.000} from {flags.Folder}" );
				break;

			// WHY a guest is not going anywhere, which `peeps` cannot answer. MajorDest is written only after a route
			// succeeds, so a guest whose best cannot be reached looks identical to one who chose nothing. This asks
			// the chooser alone, no route; each route a real choice asked is in the log ("the chooser routes to").
			case "why":
				if ( ParkPeople.Current is not { } undecided )
				{
					Reply( "why: none - a park has to be loaded" );
					break;
				}

				var reasons = undecided.WhyCensus().ToArray();
				Reply( $"why {reasons.Length}" );

				foreach ( var reason in reasons )
					Reply( "  " + reason );

				break;

			// What a cell actually holds, which is the measuring instrument for everything above: the
			// type, the neighbour mask the link pass built, and the tile the mask chose.
			case "cell":
				if ( parts.Length < 3 )
				{
					Reply( "cell <cellX> <cellY>" );
					break;
				}

				if ( Level.Current?.Park is not { } cellPark )
				{
					Reply( "cell: a park has to be loaded" );
					break;
				}

				var probeX = (int)Argument( 1 );
				var probeY = (int)Argument( 2 );
				var probed = ParkState.CellFor( cellPark, probeX, probeY );

				// PARENT AND OCCUPANT ARE PRINTED because nothing else prints them, and a cell's owner is
				// what decides whose queue a cell belongs to and which thing a click on a footprint
				// finds. Diagnosing either without them means reasoning about a field no instrument can
				// show.
				Reply( $"cell ({probeX},{probeY}) type {probed.Type} neighbours 0x{probed.Neighbours:x2} " +
					$"direction 0x{probed.Direction:x2} flags 0x{probed.Flags:x4} " +
					$"tile set {probed.TileSet} index {probed.TileIndex} angle {probed.TileAngle} " +
					$"parent {probed.ParentId}" +
					(probed.ParentId != 0
						? $" = ({MapStep.CellAt( probed.ParentId ).X},{MapStep.CellAt( probed.ParentId ).Y})"
						: "") +
					$" occupant {ParkState.Current?.CellAt( probeX, probeY ).Occupant ?? 0}" +
					$" overlap {probed.OverlapCounter} track type {probed.TrackType} links 0x{probed.TrackNeighbours:x2}" +
					$" way 0x{probed.TrackDirection:x2} track flags 0x{probed.TrackFlags:x} track parent {probed.TrackParentId} track over {probed.TrackOverlapCounter} piece {probed.TrackTileSet}/{probed.TrackTileIndex}/{probed.TrackTileAngle}" +
					(ParkState.Current is { } effectState && ParkState.OnMap( probeX, probeY )
						? $" effects {string.Join( ",", effectState.EffectsAt( probeX, probeY ).ToArray() )}"
						: "") );
				break;

			// The live emitters of the park file this park was loaded from, the newest first: each one's slot, count,
			// handle, effect, place in the park's units and life, and the script record that names it.
			case "emitters":
				if ( Level.Current?.Park?.Save is not { Particles.Problem: null } emittersOf )
				{
					Reply( "emitters: a park loaded from a file has to be running" );
					break;
				}

				var inUse = emittersOf.Particles.Used;

				Reply( $"emitters: {inUse.Count} in use in the file" );

				foreach ( var emitter in inUse )
				{
					var namedBy = emittersOf.ScriptStates.Order.Select( emittersOf.ScriptStates.For )
						.FirstOrDefault( script => script?.Effects?.Any( record => record.Type <= RideEffects.LastParticleType && record.Handle == emitter.Handle ) == true );

					Reply( $"emitters: slot {emitter.Slot} count {emitter.Count} handle 0x{emitter.Handle:x} effect {emitter.Template} "
						+ $"at ({emitter.X / 64},{emitter.Height / 64},{emitter.Z / 64}) aim {emittersOf.Particles.Aim( emitter.Slot )} life {emitter.Life}, "
						+ (namedBy is { } owner ? $"named by script {owner.Handle} of thing {owner.Thing}"
							: emittersOf.TrackRides.Cars.FirstOrDefault( car => car.Word( 0x2c ) == emitter.Handle ) is { } smoker
								? $"named by a car of ride 0x{smoker.Handle:x}" : "named by no record") );
				}

				break;

			// The running particle system's census: each emitter of the world's with its effect, where it stands in
			// park units, its life and how many particles it holds, then how many the last frame drew. `emitters`
			// is the loaded FILE's. `particles <slot>` prints every particle of that emitter instead: its place less
			// the emitter's in park units, its velocity, size, frame, colour and the ticks it has left of its life.
			case "particles":
				if ( ParticleSystem.Current is not { } particleSystem )
				{
					Reply( "particles: no particle system is running" );
					break;
				}

				// `particles screen`: the on-screen effects instead, each with the window it was started for and whether
				// that window is drawn now; ScreenParticles draws an owned effect only while its window is open.
				if ( parts.Length > 1 && parts[1] == "screen" )
				{
					var stackNow = Level.Current?.Hud?.Children.OfType<UI.WindowStack>().FirstOrDefault();
					var onScreen = 0;

					for ( var slot = 0; slot < ParticleSystem.EmitterCount; ++slot )
					{
						var emitter = particleSystem.Emitters[slot];

						if ( !emitter.Active || !emitter.Template.OnScreen )
							continue;

						++onScreen;

						// "open" only where ScreenParticles would draw it: the window up and showing, the stack not covered, the
						// emitter shown and holding a particle.
						var ownedBy = emitter.Owner is not { } window ? "no window (drawn over all)"
							: stackNow is null ? $"{window.GetType().Name}, no window stack"
							: $"{window.GetType().Name}, " + (!stackNow.Windows.Contains( window ) ? "closed (not drawn)"
								: window.Hidden ? "hidden (not drawn)" : window.PutAway ? "put away (not drawn)"
								: emitter.Hidden ? "emitter hidden (not drawn)" : emitter.Count == 0 ? "no particles (not drawn)"
								: stackNow.Covered ? "stack covered (not drawn)" : "open");

						Reply( $"particles: screen slot {slot} effect {emitter.Effect} life {emitter.Lifetime} particles {emitter.Count} owner {ownedBy}" );
					}

					Reply( $"particles: {onScreen} on-screen emitters" );
					break;
				}

				if ( parts.Length > 1 )
				{
					var cloudSlot = (int)Argument( 1 );

					if ( cloudSlot < 0 || cloudSlot >= ParticleSystem.EmitterCount || !particleSystem.Emitters[cloudSlot].Active )
					{
						Reply( $"particles: no emitter is running in slot {cloudSlot}" );
						break;
					}

					var cloud = particleSystem.Emitters[cloudSlot];

					Reply( $"particles: slot {cloudSlot} effect {cloud.Effect} holds {cloud.Count}" );

					for ( var index = cloud.FirstParticle; index >= 0; index = particleSystem.Particles[index].Next )
					{
						var one = particleSystem.Particles[index];

						Reply( $"particles: particle {index} off ({(one.X - cloud.X) / 64f:0.00},{(one.Y - cloud.Y) / 64f:0.00},{(one.Z - cloud.Z) / 64f:0.00}) "
							+ $"vel ({one.VX},{one.VY},{one.VZ}) size {one.Size} frame {one.Frame} colour 0x{one.Colour:x8} life {one.Life} of {one.Lifetime}" );
					}

					break;
				}

				var ofTheWorld = 0;
				var alive = 0;

				for ( var slot = 0; slot < ParticleSystem.EmitterCount; ++slot )
				{
					var emitter = particleSystem.Emitters[slot];

					if ( !emitter.Active || emitter.Template.OnScreen )
						continue;

					++ofTheWorld;
					alive += emitter.Count;

					// How far over the emitter its highest particle stands, in park units.
					var highest = 0;

					for ( var index = emitter.FirstParticle; index >= 0; index = particleSystem.Particles[index].Next )
						highest = Math.Max( highest, (particleSystem.Particles[index].Y - emitter.Y) / 64 );

					Reply( $"particles: slot {slot} effect {emitter.Effect} at ({emitter.X / 64},{emitter.Y / 64},{emitter.Z / 64}) "
						+ $"aim {(emitter.Aim is { } aimed ? $"({aimed.X},{aimed.Y},{aimed.Z})" : "none")} life {emitter.Lifetime} particles {emitter.Count} highest {highest} "
						+ $"flags 0x{emitter.Template.DrawFlags:x} set {emitter.Template.SpriteSet} frames {emitter.Template.Frames} "
						+ $"rates ({string.Join( ", ", emitter.Rates )}) most {emitter.MaxParticles}" );
				}

				Reply( $"particles: {ofTheWorld} emitters of the world, {alive} particles alive, {WorldParticles.Current?.Drawn ?? 0} drawn last frame, density {particleSystem.Density}" );

				break;

			// The region effects' census: how many cells hold any, and each of the five words summed over the map
			// (happiness, illness, hunger, security, attraction). `cell` prints one cell's.
			case "effects":
				if ( ParkState.Current is not { } effectsOf )
				{
					Reply( "effects: a park has to be loaded" );
					break;
				}

				var holding = 0;
				var sums = new int[ParkWorld.EffectWords];

				for ( var i = 0; i < effectsOf.Effects.Count; i += ParkWorld.EffectWords )
				{
					var any = false;

					for ( var word = 0; word < ParkWorld.EffectWords; ++word )
					{
						sums[word] += effectsOf.Effects[i + word];
						any |= effectsOf.Effects[i + word] != 0;
					}

					if ( any )
						++holding;
				}

				Reply( $"effects: {holding} cells hold one; sums happiness {sums[0]} illness {sums[1]} hunger {sums[2]} security {sums[3]} attraction {sums[4]}" );
				break;

			// Opens the park's own game menu - the thing that actually HOLDS THE WORLD. A window with
			// Pauses set is the only thing that stops GameClock, and no other command here opens one by
			// name: buyscreen, hirescreen and openthing all leave the clock running, and
			// ParkObjectWindow sets Pauses = false outright.
			//
			// `pause` is a DIFFERENT pause and must not be used for this. That one sets Time.Paused so a
			// screenshot repeats; this one is the game's own, which a park's menu asks for and the lobby's
			// never does. Escape through XTEST reaches the same road, and this exists so the audio half
			// can be driven without X at all.
			//
			// The reply reads the WINDOW STACK, not GameClock.Paused, and the difference is not
			// cosmetic: Level.Update calls DebugConsole.Poll() BEFORE GameClock.Update, so the clock
			// still carries last frame's answer at the moment this line is written and would report
			// "running" on the very frame it pauses. PausedByWindow() is true the instant the window opens.
			case "menu":
				if ( Level.Current is not { Kind: Level.Scene.Park } menuPark )
				{
					Reply( "menu: only in a park - the lobby's menu does not pause, which is the original's own rule" );
					break;
				}

				menuPark.Hud?.Children.OfType<UI.ParkFrontEnd>().FirstOrDefault()?.DebugOpenMenu();
				Reply( $"menu: toggled, pausing={menuPark.PausedByWindow()}" );
				break;

			// Opens the purchase menu. A screen is verifiable by eye and capture rather than by test,
			// and the console cannot move the pointer - so this opens it the way
			// the gadget's own button does, leaving the capture to photograph.
			case "buyscreen":
				if ( Level.Current is not { Kind: Level.Scene.Park } park )
				{
					Reply( "buyscreen: only in a park" );
					break;
				}

				park.OpenBuyScreen();
				Reply( "buyscreen: opened" );
				break;

			// What the buy screen's panel shows: the row shown and the row waiting, and the footprint picture's cells
			// and its block on the window; and its help bar poster's item, the item it last posted and that one's state.
			case "footprint":
				Reply( Level.Current?.Hud?.Children.OfType<UI.WindowStack>().FirstOrDefault()?.Windows
					.OfType<UI.ParkBuyScreen>().FirstOrDefault() is { } buying
						? $"footprint: {buying.PreviewCensus()}; {buying.HelpCensus()}"
						: "footprint: the buy screen is not open" );
				break;

			// What the buy screen's corner shows of the park's money, which its own timer writes once a second.
			case "buymoney":
				Reply( Level.Current?.Hud?.Children.OfType<UI.WindowStack>().FirstOrDefault()?.Windows
					.OfType<UI.ParkBuyScreen>().FirstOrDefault() is { } paying
						? $"buymoney: {paying.MoneyCensus()}, balance {Level.Current?.ParkState?.Balance}"
						: "buymoney: the buy screen is not open" );
				break;

			// The other half of the same gadget button - the two screens are siblings and reach each
			// other directly, which is what FUN_004a0940( 1 ) does from a remembered-tab global.
			case "hirescreen":
				if ( Level.Current is not { Kind: Level.Scene.Park } hirePark )
				{
					Reply( "hirescreen: only in a park" );
					break;
				}

				hirePark.OpenHireScreen();
				Reply( "hirescreen: opened" );
				break;

			// The per-object management window, by thing id rather than by pointing - the same reason
			// `pick` and `click` take coordinates. `objects` lists what is standing in the park.
			case "openthing":
				if ( Level.Current is not { Kind: Level.Scene.Park } thingPark )
				{
					Reply( "openthing: only in a park" );
					break;
				}

				if ( parts.Length < 2 )
				{
					Reply( "openthing: openthing <thingId> - `objects` lists them" );
					break;
				}

				thingPark.OpenObjectWindow( (int)Argument( 1 ) );
				Reply( $"openthing: asked for thing {(int)Argument( 1 )}" );
				break;

			// What each object keeps of its days: its mTotalCosts and mTotalTakings (ParkState.CostsFor, TakingsFor), then from ParkObjectRings its two
			// lifetime counts and each ring's today, entry, wrap and last finished days, newest first. `rings <thingId>` shows one; the header gives the
			// calendar's day count, which steps once each day's change.
			case "rings":
				if ( Level.Current?.ParkState is not { } days )
				{
					Reply( "rings: a park has to be loaded" );
					break;
				}

				var ledgers = days.Objects
					.Where( thing => parts.Length < 2 || thing.ThingId == (int)Argument( 1 ) )
					.Select( thing => $"thing {thing.ThingId,2} total costs {days.CostsFor( thing.ThingId )} "
						+ $"total takings {days.TakingsFor( thing.ThingId )} "
						+ days.RingsFor( thing.ThingId ).Census() )
					.ToArray();

				Reply( $"rings {ledgers.Length} day {GameCalendar.Days}" );

				foreach ( var ledger in ledgers )
					Reply( "  " + ledger );

				break;

			// Everything this theme offers. The UI type is the useful column: it is what sorts the four
			// buy tabs AND what picks which of the nine object windows a click opens, so a test that
			// needs "a ride, any ride" can find one without knowing the park by heart.
			case "catalogue":
				if ( Level.Current?.Catalogue is not { } theCatalogue )
				{
					Reply( "catalogue: a park has to be loaded" );
					break;
				}

				foreach ( var entry in theCatalogue.All.OrderBy( one => one.Id ) )
					Reply( $"item {entry.Id,3} '{entry.Name}' type {entry.UiType} price {entry.BuildPrice}" );

				break;

			// The park gate's script as the door left it: the command and status, and the two censuses the close waits on.
			case "gate":
				Reply( ParkPeople.Current?.GateDescription() ?? "gate: no park" );
				break;

			// What the park is worth. It exists so that a test can prove money moved by EXACTLY one
			// amount: the clock is stopped under `pause`, so between two of these with no `step`
			// between them no tick runs, nobody pays at the gate, and nothing but the command under
			// test can have moved the balance; measured across a stepped frame, the park's own takings
			// would be counted in.
			// The FEE and the GATE are here beside the balance because the entry-price screen moves both,
			// and a screenshot can show that a number changed without saying what it changed to. With
			// them printed, "the plus button charged one more" is a measurement rather than a picture.
			// Then the bank's other four, which the original's memory shows beside a drink's +10: the year's profit,
			// the balance the last withdrawal left, the tick it entered the red and whether withdrawals are on.
			// Thing 1's training budgets and whether one was ever set, each kind's skill as the Staff Training
			// Budgets screen's bars read it, and the screen's own census where it is open.
			case "budgets":
				Reply( ParkPeople.Current is { } trained
					? $"budgets {string.Join( ",", trained.TrainingBudgets )} evertrained {(trained.HaveEverTrained ? 1 : 0)} "
						+ $"skill {string.Join( ",", Enumerable.Range( 0, ParkStaffPool.Kinds ).Select( kind => trained.TrainingOf( kind ).GradeShare ) )}"
						+ (Level.Current?.Hud?.Children.OfType<UI.WindowStack>().FirstOrDefault()?.Windows.OfType<UI.ParkStaffCostsScreen>().FirstOrDefault() is { } costs
							? $"; screen: {costs.Census()}" : "; screen: not open")
					: "budgets: a park has to be loaded" );
				break;

			// The bank's seven fields and every loan slot, as the writer writes them (ParkState.Economy).
			case "loans":
				if ( Level.Current?.ParkState is not { } bank )
				{
					Reply( "loans: a park has to be loaded" );
					break;
				}

				Reply( $"loans: {bank.DescribeEconomy()}" );

				foreach ( var (loan, slot) in bank.Loans.Select( ( loan, slot ) => (loan, slot) ) )
					Reply( $"  loan {slot} available {loan.Available} amount {loan.AmountAvailable} apr {loan.AprPercent} "
						+ $"months {loan.RepaymentMonths} monthly {loan.MonthlyRepayment} bought {loan.Bought} "
						+ $"repaid {loan.MonthsRepaid} lender {loan.LenderNameIndex}" );

				break;

			case "tickets":
				Reply( Players.Roster.Current is not { } ticketHolder
					? "tickets: nobody is playing"
					: $"tickets {ticketHolder.Tickets} besides secret {ticketHolder.File.CountTicketsBesidesSecret( SaveFolder.IsTheme )} keys {ticketHolder.Keys} " +
						$"spent {ticketHolder.File.TicketsSpent} left {ticketHolder.TicketsUnspent} rides [{string.Join( ",", ticketHolder.File.RideIds )}] " +
						$"records [{string.Join( ",", ticketHolder.File.RecordTickets )}] secret [{string.Join( ",", ticketHolder.File.BonusTickets )}] " +
						string.Join( " ", ticketHolder.File.Parks.Select( p => $"{p.Name} [{string.Join( ",", p.Tickets )}] holds [{string.Join( ",", p.HoldsRecord.Select( ( h, i ) => h != 0 ? p.RecordValues[i].ToString() : "-" ) )}]" ) ) +
						(ParkGoldenTickets.Last is { } last ? $"; last test tick {last.Tick}, {last.Measures}, won {last.Won.Count}" : "; no test yet") );
				break;

			// The money's census, or with a word and a number an INSTRUMENT that writes one figure: `money balance N` the bank's
			// balance, `money cashin N` and `money costs N` the analyser's month so far, as the original's are written by
			// thingpoke.py (docs/TOOLING.md, "A month closed in profit or in loss in the original, on demand").
			case "money":
				if ( parts.Length > 2 && Level.Current?.ParkState is { } purseWritten )
				{
					// Parsed whole: a float would write 123456789 as 123456792.
					var figure = int.TryParse( parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var moneyFigure ) ? moneyFigure : 0;

					if ( parts[1] == "balance" )
						purseWritten.SetBalance( figure );
					else if ( parts[1] == "cashin" )
						purseWritten.MonthCashIn.Add( unchecked(figure - purseWritten.MonthCashIn.Total) );
					else if ( parts[1] == "costs" )
						purseWritten.MonthCosts.Add( unchecked(figure - purseWritten.MonthCosts.Total) );

					Reply( parts[1] is "balance" or "cashin" or "costs" ? $"money: {parts[1]} written {figure}" : "money: money balance|cashin|costs <n>" );
					break;
				}

				Reply( Level.Current?.ParkState is { } purse
					? $"money balance {purse.Balance} takings {purse.Takings} " +
						$"fee {purse.AdmissionFee} gate {(purse.ParkIsClosed ? "shut" : "open")} " +
						$"profit {purse.ProfitThisYear} last {purse.LastBalance} red {purse.TurnEnteredRed} " +
						$"withdrawals {purse.WithdrawalsEnabled} " +
						$"month balance {purse.MonthBalance.Newest} slot {purse.MonthBalance.Current} " +
						$"park value {ParkValue.Of( purse, Level.Current.Catalogue )} ring {purse.MonthParkValue.Newest} slot {purse.MonthParkValue.Current} " +
						$"lifetime visitors {purse.LifetimeVisitors} ring {purse.MonthVisitors.Newest} slot {purse.MonthVisitors.Current} most paid {purse.MostPaidForTicket} " +
						$"misbehaving kids {purse.MisbehavingKids.Month} got away {purse.MisbehavingKids.GotAwayThisMonth} ever {purse.MisbehavingKids.Ever} " +
						$"guests inside ring {purse.MonthGuestsInside.Newest} slot {purse.MonthGuestsInside.Current} rides {purse.RidesStanding( Level.Current.Catalogue )} ring {purse.MonthRides.Newest} slot {purse.MonthRides.Current} " +
						$"arrivals {purse.MonthArrivals.Total}/{purse.MonthArrivals.Newest} slot {purse.MonthArrivals.Current} " +
						$"loads {purse.ArrivalLoads.Current}/{(purse.ArrivalLoads.Wrapped ? 1 : 0)} newest {purse.ArrivalLoads.Newest} " +
						$"happiness {purse.MonthHappiness.NewestSingle} slot {purse.MonthHappiness.Current} now {ParkPeople.Current?.GuestsInsideHappiness} " +
						$"mean stay {purse.MonthMeanStay.Newest} slot {purse.MonthMeanStay.Current} leavers {purse.LeaverStays.Current}/{(purse.LeaverStays.Wrapped ? 1 : 0)} newest {purse.LeaverStays.Newest} now {purse.LeaverStays.MeanOfNewest( ParkMonthRing.Stays )} " +
						$"park score {purse.MonthParkScore.Newest} slot {purse.MonthParkScore.Current} now {ParkScore.Of( purse, Level.Current.Catalogue, ParkPeople.Current?.GateGuestCensus ?? 0, ParkPeople.Current?.ScoreStaff ?? default )} " +
						$"cashin {purse.MonthCashIn.Total}/{purse.MonthCashIn.Newest} " +
						$"staff {purse.MonthStaffCosts.Total}/{purse.MonthStaffCosts.Newest} " +
						$"costs {purse.MonthCosts.Total}/{purse.MonthCosts.Newest} slot {purse.MonthCosts.Current} " +
						$"training {purse.MonthTraining.Total}/{purse.MonthTraining.Newest} " +
						$"gate {purse.MonthGateTakings.Total}/{purse.MonthGateTakings.Newest} " +
						$"shops {purse.MonthShopTakings.Total}/{purse.MonthShopTakings.Newest} " +
						$"sideshows {purse.MonthSideshowTakings.Total}/{purse.MonthSideshowTakings.Newest} " +
						$"trend {(purse.MonthInLoss ? "red" : "green")}"
					: "money: a park has to be loaded" );
				break;

			// The open ride or shop window's preview, the latest opened: what it shows, its fit and where its turn and clip
			// stand. `preview <radians>` holds the turn there for a repeatable frame; `preview go` lets it turn.
			case "preview":
				var previewWindow = Level.Current?.OpenPreviewWindows().LastOrDefault();
				(Action<float?> Hold, Func<string?> Census)? previewOf = previewWindow switch
				{
					UI.ParkObjectWindow ride => (ride.HoldPreview, () => ride.PreviewCensus),
					UI.ParkShopWindow shop => (shop.HoldPreview, () => shop.PreviewCensus),
					_ => null
				};

				if ( previewOf is not { } preview )
				{
					Reply( "preview: no object window is open" );
					break;
				}

				if ( parts.Length > 1 )
					preview.Hold( parts[1] == "go" ? null : (float)Argument( 1 ) );

				Reply( $"preview: {preview.Census() ?? "none built"}" );
				break;

			// Every bound hoarding: its panels, texture, progress and flags.
			case "hoardings":
				foreach ( var hoardingLine in ParkObjects.Current?.HoardingCensus() ?? [] ) Reply( hoardingLine );
				break;

			// The karts' and water rides' track pieces standing: each anchor, model, row and turn; then each track ride's
			// +0x2c and sections.
			case "track":
				if ( ParkTrackPieces.Current is not { } track )
				{
					Reply( "track: none - a park has to be loaded" );
					break;
				}

				Reply( $"track pieces {track.Pieces.Count}" );

				foreach ( var trackLine in track.Census() ) Reply( $"  {trackLine}" );

				// Each track ride's mIsTrackRideValid, its buoys and its sections with their objects, as original/trackride.py prints them.
				foreach ( var trackRide in ParkState.Current?.Objects.Where( o => o.TrackRide != 0 ) ?? [] )
				{
					var sections = ParkState.Current!.TrackRides.SectionsOf( trackRide.TrackRide );

					Reply( $"track ride 0x{trackRide.TrackRide:x} thing {trackRide.ThingId} +0x2c {trackRide.IsTrackRideValid} "
						+ $"can load {trackRide.CanLoad} sections {sections.Count}" );

					var buoys = ParkState.Current!.TrackRides.BuoysOf( trackRide.TrackRide );

					Reply( $"    buoys {buoys.Count}" );

					for ( var index = 0; index < buoys.Count; ++index )
						Reply( $"    buoy {index} flags 0x{buoys[index].Flags:x} ({buoys[index].X},{buoys[index].Z}) "
							+ $"object {(buoys[index].Object is { } tied ? $"0x{tied.Flags:x} ({tied.X},{tied.Z})" : "no")}" );

					for ( var index = 0; index < sections.Count; ++index )
					{
						var objects = string.Join( " ", ParkState.Current!.TrackRides.ObjectsOf( trackRide.TrackRide, index )
							.Select( o => $"[0x{o.Flags:x} ({o.X},{o.Z}) {o.RadiusX}/{o.RadiusZ}]" ) );

						Reply( $"    section {index} type {sections[index].Type & 0xffff} flags 0x{sections[index].Type >> 16:x} "
							+ $"cells ({sections[index].X / 0xc00},{sections[index].Y / 0xc00}) at ({sections[index].X},{sections[index].Y}) objects {objects}" );
					}
				}

				break;

			// The wall between the park and the land not bought; `boundary X Y` adds the pieces made on or beside a cell.
			case "boundary":
				if ( ParkBoundaryHoarding.Current is not { } boundary )
				{
					Reply( "boundary: none - a park has to be loaded" );
					break;
				}

				(int X, int Y)? near = parts.Length >= 3 && int.TryParse( parts[1], out var nearX ) && int.TryParse( parts[2], out var nearY )
					? (nearX, nearY) : null;

				foreach ( var boundaryLine in boundary.Census( near ) ) Reply( boundaryLine );
				break;

			// `meshes THING`: which of a standing thing's drawn meshes are put away, by index and name, and what its
			// channel 0 plays and the start that bound it.
			case "meshes":
				if ( parts.Length < 2 || !int.TryParse( parts[1], out var meshesThing ) || ParkObjects.Current?.ModelFor( meshesThing ) is not { } meshesModel )
				{
					Reply( "meshes: a standing thing's id is wanted" );
					break;
				}

				var putAway = meshesModel.Source.Meshes
					.Select( ( mesh, index ) => (Name: mesh.Name.TrimEnd( '\0' ), Index: index) )
					.Where( mesh => meshesModel.Entities[mesh.Index].Opacity <= 0f )
					.Select( mesh => $"{mesh.Index} {mesh.Name}" );
				var meshesPlayers = ParkRides.Current?.Scheduler.Scripts.FirstOrDefault( script => script.ThingId == meshesThing )?.Animations;
				var meshesChannel = meshesPlayers?.Channel( 0 );

				Reply( $"meshes {meshesThing}: {meshesModel.Entities.Length} meshes, hidden [{string.Join( ", ", putAway )}]; channel 0 "
					+ (meshesChannel is null ? "none" : $"role {meshesChannel.AnimID} entry {meshesChannel.SubAnim} frame {meshesChannel.AnimFrame:F1} "
						+ $"flags 0x{meshesChannel.Flags:x}, bound {meshesPlayers!.BoundOn( 0 )}") );

				// `meshes THING axes`: each node's own axis lengths as the node tree last posed them, for a scale track's
				// keys against the original's (modelnodes.py --axes reads its nodes' +0x10 rows).
				if ( parts.Length > 2 && parts[2] == "axes" )
				{
					for ( var node = 0; node < meshesModel.Source.Nodes.Count; ++node )
					{
						var name = node < meshesModel.Source.Meshes.Count ? meshesModel.Source.Meshes[node].Name.TrimEnd( '\0' ).Trim() : $"node {node}";
						var local = meshesModel.PosedLocal( node ) ?? meshesModel.Source.Nodes[node].LocalTransform;

						Reply( $"  node {node} {name} axes ({AxisLength( local.M11, local.M12, local.M13 ):F4}, "
							+ $"{AxisLength( local.M21, local.M22, local.M23 ):F4}, {AxisLength( local.M31, local.M32, local.M33 ):F4})"
							+ (meshesModel.PosedLocal( node ) is null ? " rest" : "") );
					}
				}

				break;

			// What is standing in the park NOW, which is the save's list plus what has been built and
			// minus what has been sold - the census that says whether buying actually changed anything.
			case "objects":
				var standing = ParkBuilding.Census().ToArray();

				if ( standing.Length == 0 )
				{
					Reply( "objects: none - a park has to be loaded" );
					break;
				}

				Reply( $"objects {standing.Length}" );

				foreach ( var entry in standing )
					Reply( "  " + entry );

				break;

			// Where each standing object's floor plate meets the ground, and how far the rim is from the terrain corners beside it;
			// the last line counts the objects and queue cells over a step (the rim the original conforms and this game does not).
			case "rim":
				var rims = ParkBuilding.RimCensus().ToArray();

				if ( rims.Length == 0 )
				{
					Reply( "rim: none - a park has to be loaded" );
					break;
				}

				foreach ( var rim in rims )
					Reply( rim );

				break;

			// One of the gadget's CATEGORY screens, by the original's own screen number: 3 park status,
			// 4 all staff, 5 all items, 6 all visitors, 7 finances, 8 loans, 9 staff costs, 10 entry
			// price. It exists for the reason `buyscreen` does - a screen is verifiable by eye and by
			// capture rather than by test - and `screen info` / `screen money` open whichever screen
			// each category was last left on, which is exactly what the gadget's own button does.
			case "screen":
				if ( Level.Current is not { Kind: Level.Scene.Park } screenPark )
				{
					Reply( "screen: only in a park" );
					break;
				}

				if ( parts.Length < 2 )
				{
					Reply( "screen: `screen info`, `screen money`, or `screen <3-10>`" );
					break;
				}

				switch ( parts[1].ToLowerInvariant() )
				{
					case "info":
						screenPark.OpenCategoryScreen( UI.ParkCategoryScreens.Information );
						Reply( "screen: opened the Information category" );
						break;

					case "money":
						screenPark.OpenCategoryScreen( UI.ParkCategoryScreens.Finance );
						Reply( "screen: opened the Finance category" );
						break;

					default:
						var askedFor = (int)Argument( 1 );

						// 7 and up are the Finance category's; 3 to 6 are the Information category's.
						screenPark.OpenCategoryScreen(
							askedFor >= 7 ? UI.ParkCategoryScreens.Finance : UI.ParkCategoryScreens.Information,
							askedFor );

						Reply( $"screen: asked for screen {askedFor}" );
						break;
				}

				break;

			// `strings <table> <row>...`: a string table's rows as the game reads them, through the table's own folder's
			// character table, so a language folder the game does not show can still be read in it.
			case "strings":
				if ( parts.Length < 3 )
				{
					Reply( "strings: give a table under data and at least one row, as `strings Language/american/THEMENAMES.str 0`" );
					break;
				}

				try
				{
					var table = new StringFile( parts[1] );

					foreach ( var row in parts.Skip( 2 ) )
					{
						if ( int.TryParse( row, out var index ) && index >= 0 && index < table.Entries.Length )
							Reply( $"strings: {parts[1]} row {index} of {table.Entries.Length}: \"{table[index]}\"" );
						else
							Reply( $"strings: {parts[1]} has no row '{row}' (rows 0 to {table.Entries.Length - 1})" );
					}
				}
				catch ( Exception e )
				{
					Reply( $"strings: {parts[1]} could not be read: {e.Message}" );
				}

				break;

			case "quit":
				Reply( "quitting" );
				Environment.Exit( 0 );
				break;

			default:
				// Only the lobby-era commands; the `case` labels above are the full list.
				Reply( $"unknown command '{command}' - island/orbit/freeze/unfreeze/pause/resume/step/settle/strike/rain/weather/bolt/near/stats/assets/state/volume/mute/sound/place/speech/advisor/greet/duck/reload/size/park/lobby/camera/camcorder/quit" );
				break;
		}
	}

	/// <summary>
	/// How near things have come to the camera, which is otherwise very hard to catch: a flyer
	/// passing close is a fraction of a second and a bolt landing on the camera is a one in
	/// sixty-four roll.
	///
	/// "closest" says close passes are actually happening, so a clean "solid" number means the
	/// fade caught them rather than that nothing came near. Both are in multiples of the flyer's
	/// own radius, which is what the fade band is written in.
	/// </summary>
	private static string Near()
	{
		static string Radii( float value ) => value == float.MaxValue ? "-" : value.ToString( "F2" );

		var bolt = LobbyWeather.Current?.DebugBolt;

		var boltDistance = bolt == null || bolt.DebugAxisDistance == float.MaxValue
			? "-"
			: bolt.DebugAxisDistance.ToString( "F1" );

		return $"near flyers closest={Radii( LobbyFlyer.DebugClosestApproach )}r "
			+ $"closestSolid={Radii( LobbyFlyer.DebugClosestSolid )}r "
			+ $"bolt axisDist={boltDistance} opacity={bolt?.DebugOpacity ?? 0f:F2}";
	}

	private static string Stats()
	{
		if ( _frameCount == 0 )
			return "stats no frames yet";

		var window = _frames.Take( _frameCount ).ToArray();
		var sorted = window.Order().ToArray();

		var mean = window.Average();
		var p99 = sorted[Math.Min( sorted.Length - 1, (int)(sorted.Length * 0.99) )];

		var entities = Entity.All.Count;
		var models = Asset.All.OfType<Model>().Count();
		var windows = Level.Current?.Hud?.Children.OfType<UI.WindowStack>().FirstOrDefault()?.Windows.Count ?? 0;

		return $"stats fps={1000.0 / mean:F1} mean={mean:F2}ms p99={p99:F2}ms worst={sorted[^1]:F2}ms "
			+ $"frames={_frameCount} entities={entities} models={models} assets={Asset.All.Count} "
			+ $"duck={Audio.DuckLevel:0.00} windows={windows}";
	}

	/// <summary>
	/// What <see cref="Asset.All"/> holds right now, by kind.
	/// </summary>
	/// <remarks>
	/// <c>distinct</c> counts each kind-and-path once, so a total above it says some path is
	/// registered more than once - which is what a lookup that missed the cache leaves behind, and
	/// the first thing worth knowing about a scene's residue.
	/// </remarks>
	private static string AssetSummary()
	{
		var counts = Asset.All
			.GroupBy( asset => asset.GetType().Name )
			.OrderBy( group => group.Key )
			.Select( group => $"{group.Key.ToLowerInvariant()}={group.Count()}" );

		var distinct = Asset.All
			.Select( asset => $"{asset.GetType().Name}|{asset.Path}" )
			.Distinct()
			.Count();

		return $"assets total={Asset.All.Count} distinct={distinct} {string.Join( " ", counts )}";
	}

	private static string State()
	{
		var island = LobbyCameraMode.CurrentIsland;
		var script = island?.Script;

		return $"state size={Screen.Size.X}x{Screen.Size.Y} island={island?.Index} name='{island?.ParkName}' "
			+ $"orbit={LobbyCameraMode.DebugOrbit:F3} paused={LobbyCameraMode.Paused} "
			// How far through swinging onto the gate and flying into it the camera is. A pure getter,
			// and the only way to see the park-entry move at all: it finishes by loading the park, so
			// by the time anything else could be asked the lobby is gone.
			+ $"{LobbyCameraMode.LeaveDescription()} gate={island?.Gate.Describe()} "
			+ $"clock={(Time.Paused ? "paused" : "running")} stepping={Time.StepFrames} "
			+ $"game={(GameClock.Paused ? "paused" : "running")} ticks={GameClock.Ticks} "
			+ $"day={GameCalendar.Days} season={GameCalendar.Season} date={GameCalendar.Now:yyyy-MM-dd} "
			+ $"quality={ParkWeather.Current?.Quality} drops={ParkWeather.Current?.Drops} "
			+ $"storm={ParkWeather.Current?.Lightning} snow={ParkWeather.Current?.Snowing} "
			+ $"strikes={ParkWeather.Current?.DebugStrikes} forecast={ParkWeather.Current?.Forecast} "
			+ $"flash={ParkWeather.Current?.DebugBolt?.Flash:F3} "
			+ $"boltOpacity={ParkWeather.Current?.DebugBolt?.DebugOpacity:F3} "
			+ $"boltDist={ParkWeather.Current?.DebugBolt?.DebugAxisDistance:F0} "
			// Compact, because Vector3's own formatting carries spaces and a harness splitting the
			// reply on whitespace gets a single bracket.
			+ $"cam={Camera.Position.X:F0},{Camera.Position.Y:F0},{Camera.Position.Z:F0} "
			+ $"rainy={script?.Rainy} lightning={script?.Lightning} "
			+ $"strikes/s={script?.StrikesPerSecond:F3} flyers={script?.FlyingMeshes.Count}";
	}

	/// <summary>The control with <paramref name="id"/> in the tree under <paramref name="control"/>, or null - for `control`.</summary>
	private static UI.UiControl? FindControl( UI.UiControl? control, int id )
	{
		if ( control == null )
			return null;

		if ( control.Id == id )
			return control;

		foreach ( var child in control.Children )
		{
			if ( FindControl( child, id ) is { } found )
				return found;
		}

		return null;
	}

	private static int _stall;

	/// <summary>Runs the stop `stall` asked for, if any: called at the end of the level's update.</summary>
	internal static void StallFrame()
	{
		if ( _stall <= 0 )
			return;

		Reply( $"stall: {_stall} ms from wall {Time.WallMilliseconds}" );
		System.Threading.Thread.Sleep( _stall );
		_stall = 0;
	}

	private static void Reply( string message )
	{
		Console.Out.WriteLine( $"[dbg] {message}" );
		Console.Out.Flush();
	}
}
