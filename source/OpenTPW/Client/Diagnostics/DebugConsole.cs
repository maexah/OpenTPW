using System.Collections.Concurrent;
using System.Globalization;

namespace OpenTPW;

/// <summary>
/// Optional, self-contained command channel on stdin, for driving the game without a human at
/// the keyboard - reproducing a camera angle exactly, forcing an effect that is otherwise random,
/// or reading back frame timings.
///
/// Disabled unless OPENTPW_DEBUG_CONSOLE=1 is set, and costs one boolean test per frame when off.
/// To remove entirely: delete this file, the one call site in Level.Update(), Time.Paused and Time.StepFrames, and
/// the members marked as being for it - LobbyCameraMode's DebugOrbit, DebugSelect, DebugSettle and DebugHoldOrbit; LobbyWeather's
/// Current, DebugRain, DebugBolt and DebugStrike; LobbyAudio's Muted, State and DebugPlaceSound; LobbyFlyer's DebugClosestApproach
/// and DebugClosestSolid; Lightning's DebugAxisDistance and DebugOpacity; the advisor's Say and State;
/// Game's RequestLobbyReload; Water's Sea; ParkGuestSprites' Current, DebugFacing, Census and WriteGroundDash
/// (with the white square Load appends to the atlas for it, and the second quad a person Build reserves);
/// ParkFrontEnd's DebugOpenMenu; and ParkPeople's Current and Census.
///
/// Engine and content: neither. It drives and reads both, and nothing else depends on it.
///
/// Usage:
///
///     mkfifo /tmp/tpw.in
///     OPENTPW_DEBUG_CONSOLE=1 dotnet OpenTPW.dll &lt; /tmp/tpw.in
///     echo "island 2" &gt; /tmp/tpw.in
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

		if ( !_started )
			Start();

		while ( _pending.TryDequeue( out var line ) )
			Execute( line );
	}

	private static void Record( float delta )
	{
		_frames[_frameIndex] = delta * 1000.0;
		_frameIndex = (_frameIndex + 1) % Window;
		_frameCount = Math.Min( _frameCount + 1, Window );
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
			// caller can put the lobby at exactly the same point every run: pause, pick an island,
			// step, shoot. Poll `state` for stepping=0 to know it has finished.
			case "step":
				Time.Paused = true;
				Time.StepFrames = parts.Length > 1 ? (int)Argument( 1 ) : 1;
				Reply( $"stepping {Time.StepFrames}" );
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

			// Where the flying camera is AIMING, which nothing could report. `attract` above is a SETTER
			// and must never be polled (VERIFYING rule 88), and `state` carries cam= but nothing about
			// the aim - so "the view swings when it changes island" was not a measurable claim at all.
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
			// quality is the one lever worth having, because everything else follows from it: 0 is the
			// heaviest storm the game can make and 100 is a clear day.
			case "weather":
				if ( ParkWeather.Current != null && parts.Length > 1 )
					ParkWeather.Current.DebugQuality( (int)Argument( 1 ) );

				Reply( ParkWeather.Current == null
					? "weather none - not in a park"
					: $"weather quality={ParkWeather.Current.Quality} drops={ParkWeather.Current.Drops} "
						+ $"storm={ParkWeather.Current.Lightning}" );
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

			// What a scene leaves behind, by name rather than by count. `stats` can say that 33
			// assets survived a lobby-park-lobby cycle and cannot say which, which is why the
			// residue has stayed unidentified. Bare `assets` summarises; `assets list` prints one
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

			// The sea's texture, and the sampler it is ACTUALLY drawn with. Water asks for
			// TextureFlags.Wrap, but what comes back may have been served out of the texture cache,
			// and a cache hit returns before the sampler is assigned - so `requested` and `sampler`
			// disagreeing is the fault itself, said by the game rather than inferred from a
			// photograph. `adopted` says which of the two roads the texture came down, and `size`
			// is nought for a hit because the size is assigned on the same line the sampler is.
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

			case "volume":
				if ( parts.Length > 1 )
					Audio.MasterVolume = Argument( 1, Audio.MasterVolume );

				Reply( $"volume={Audio.MasterVolume:0.00}" );
				break;

			case "mute":
				if ( LobbyAudio.Current != null )
					LobbyAudio.Current.Muted = parts.Length < 2 || parts[1] != "0";

				Reply( $"muted={LobbyAudio.Current?.Muted}" );
				break;

			case "sound":
				Reply( LobbyAudio.Current?.State() ?? "no lobby audio" );
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
				// gets confirmed - there is no way to read it out of the executable.
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

			// The front end's own park entry, which `park` above deliberately skips: the island on
			// show has its gate swung open, and the park is asked for once the doors have finished.
			// The same two calls IslandPanel.EnterPark makes, so that the swing can be watched
			// without having to drive the interface.
			case "enter":
				if ( LobbyCameraMode.CurrentIsland is not { } entering )
				{
					Reply( "no island on show" );
					break;
				}

				// Replied before the gate is opened, because a gate with no clip to play hands the
				// park load straight on and the scene is gone by the next line.
				Reply( $"opening {entering.ParkName}'s gate" );

				// The same two calls IslandPanel.EnterPark makes, in the same order: the gate swings
					// (ours) while the camera swings onto it and flies in (the original's), and the park
					// is asked for when the CAMERA arrives, not when the gate finishes.
					entering.Gate.Open();
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

				var running = operators.RideCensus().ToArray();
				Reply( $"rides {running.Length} distinct scream samples heard "
					+ $"{ParkAudio.Current?.ScreamSamplesHeard ?? 0}" );

				foreach ( var ride in running )
					Reply( "  " + ride );

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

				Reply( $"thirst: {drinkers.MakeThirsty( Peep.Most )} guests are now as thirsty "
					+ "as the meter allows" );

				break;

			// What every thing a guest may be sent to has taken, and whether it can be offered at all. The
			// two censuses either side of this one cannot answer that: `rides` says what a script is doing
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
			// The bus is the one worth reading: 45 points, closed Bezier, first point near
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

			// Puts one new guest at the bus stop, which is what an arrival does - the original makes one
			// per thing tick while a vehicle unloads. Driven by hand here because nothing yet runs the
			// timer, and because the question worth answering first is whether a guest who was never in
			// the save can walk and be seen at all.
			case "arrive":
				if ( ParkPeople.Current is not { } arrivals )
				{
					Reply( "arrive: none - a park has to be loaded" );
					break;
				}

				// BusStopA, which Standard.sam puts at (42,5).
				var arrivedAt = arrivals.Admit( (int)Argument( 1, 42 ), (int)Argument( 2, 5 ) );

				Reply( arrivedAt == 0
					? "arrive: the park would not take one"
					: $"arrive: guest {arrivedAt}" );

				break;

			// Brings a whole load in, of whatever size is asked for, which is the only way to see the
			// second and third vehicles at all: the headcount is floored at Arrival.MinPeople and a
			// crowd of one always takes the bus. The banding is the original's own - under 36 the bus,
			// up to 60 the seaplane, beyond that the ferry.
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
			// reason: a guest's own day takes about two minutes of park time to run down, and watching
			// that is a poor way to find out whether the removal works.
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
						peep => !PeepBehaviour.HeldByAThing( peep.State ) )?.ThingId ?? 0;

				Reply( leaving != 0 && departures.Depart( leaving )
					? $"depart: guest {leaving} went home"
					: "depart: nobody could go" );

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

			// The staff, who are a separate list from the guests and so appear in neither census above.
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
					// walking them there, so the eye takes the ground as-is. Assigning Stand left it
					// easing up from the old height, which never finishes while the clock is stopped -
					// and a stopped clock is how frames are captured.
					ParkCamcorderCameraMode.StandAt( new Vector3( Argument( 1 ), Argument( 2 ), 0f ) );

					// An optional heading, in radians, because a capture cannot otherwise be AIMED.
					// Steer() turns the view from where the POINTER is - not from how far it moved - and
					// a harness cannot move the pointer, so without this a screenshot faces wherever the
					// mouse was last left and then drifts for as long as the clock runs. Same reason item
					// 4 of the cleanup plan added `attract off`: the shot the work had to be confirmed by
					// could not be framed with what already existed.
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
			// StandAt - and so crosses no cell edge at all. The whole of cleanup item 2 is about what
			// happens BETWEEN two cells, so nothing that already existed could show it.
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
			case "pick":
				if ( Level.Current?.Kind != Level.Scene.Park )
				{
					Reply( "pick: only in a park" );
					break;
				}

				// With two arguments it asks about a point of the window instead of the pointer. That is
				// not a convenience: the pointer is not something a harness can move reliably - a warp
				// with no real motion behind it reaches the window system and never reaches the game -
				// so a test that could only ask about the cursor would be measuring X as much as the
				// arithmetic. `arrive`, `load` and `thirst` exist for the same reason.
				Reply( parts.Length > 2
					? ParkPicking.PickAt( Argument( 1 ), Argument( 2 ) )
					: ParkPicking.State() );
				break;

			// A click at a point of the window, and it takes coordinates for exactly the reason `pick`
			// does: synthetic pointer motion reaches X and never reaches SDL, so a harness cannot select
			// a row by moving the cursor onto it. Only SDL is skipped - the hit test, the row
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
					Reply( $"click: nothing took ({Argument( 1 ):F0},{Argument( 2 ):F0})" );
					break;
				}

				if ( !ParkPicking.TryCellAt( Argument( 1 ), Argument( 2 ), out var clickX, out var clickY ) )
				{
					Reply( "click: that point is not over the map" );
					break;
				}

				Reply( clickPark.ClickWorldAt( clickX, clickY,
					ParkPicking.ThingAt( Argument( 1 ), Argument( 2 ) ) ) );
				break;

			// Buying, selling and moving something, driven by hand. The screens that will do this for a
			// player do not exist yet, and these exist for the same reason `arrive` did before the
			// arrival manager: the verb has to be provable in a running park before anything is wrapped
			// around it.
			case "buy":
				Reply( parts.Length > 3
					? ParkBuilding.Buy( (int)Argument( 1 ), (int)Argument( 2 ), (int)Argument( 3 ),
						parts.Length > 4 ? (int)Argument( 4 ) : 0 )
					: "buy <catalogueId> <cellX> <cellY> [angle]" );
				break;

			// Hiring, firing, and carrying somebody from one place to another. The hire screen is not
			// built yet, so these drive the verbs directly - the same reason `arrive` existed before
			// the arrival manager.
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
				var candidate = hiring.Candidates.FirstOrDefault( person => person.Id == wanted );

				if ( candidate.Id != wanted )
				{
					Reply( $"hire: nobody in the pool is candidate #{wanted}" );
					break;
				}

				// Taken out of the pool only once they are actually standing in the park - a failed
				// hire must not quietly destroy the candidate.
				var hired = staffing.Hire( candidate, (int)Argument( 2 ), (int)Argument( 3 ) );

				if ( hired == 0 )
				{
					Reply( $"hire: '{candidate.Name}' could not be put there, and is still waiting" );
					break;
				}

				hiring.Take( wanted, out _ );

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
			case "carry":
				Reply( parts.Length > 1
					? ParkBuilding.Carry( (int)Argument( 1 ) )
					: ParkBuilding.HandState() );
				break;

			// Whichever hand is full - an item, or a candidate taken off the hire screen. THE SAME BODY
			// THE RIGHT MOUSE BUTTON RUNS, rather than a copy: the button's edge cannot be driven from
			// here, so this reaches Level.CancelCarried and the tested path is the real one.
			case "drop":
				Reply( Level.Current?.CancelCarried()
					?? "drop: nothing is being carried" );
				break;

			// A quick right click, as far as a build tool is concerned - the same body the button reaches.
			case "rightclick":
				Reply( Level.Current?.QuickRightClick()
					?? $"rightclick: nothing to put away (tool mode {ParkBuildMode.Current}, RMB cancel " +
						$"{(GameOptions.Current.RmbCancel ? "on" : "off")})" );
				break;

			// `put`, not `place` - the lobby already has a `place`, which auditions an ambient sample
			// at a position. Two cases with one label does not compile, which is how this was caught,
			// but the quieter version of the same mistake is a command that works in one scene and
			// silently means something else in the other.
			case "put":
				Reply( parts.Length > 2
					? ParkBuilding.PlaceCarried( (int)Argument( 1 ), (int)Argument( 2 ),
						parts.Length > 3 ? (int)Argument( 3 ) : 0 )
					: "put <cellX> <cellY> [angle]" );
				break;

			case "sell":
				Reply( parts.Length > 1
					? ParkBuilding.Sell( (int)Argument( 1 ) )
					: "sell <thingId>" );
				break;

			case "move":
				Reply( parts.Length > 3
					? ParkBuilding.Move( (int)Argument( 1 ), (int)Argument( 2 ), (int)Argument( 3 ),
						parts.Length > 4 ? (int)Argument( 4 ) : 0 )
					: "move <thingId> <cellX> <cellY> [angle]" );
				break;

			// Laying and lifting path one cell at a time. The original has no drag - both drag slots of
			// its build mode are bare RET stubs - so a run really is a sequence of single commits, and
			// driving them one at a time from here is the same shape the game uses rather than a
			// shortcut around it. The pointer half is Level.WorldClick; this exists for the same reason
			// `put` does, because a harness cannot move the mouse.
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

			// >>> WHAT THE RENDERER ACTUALLY HOLDS, as opposed to what the simulation believes. <<<
			//
			// This exists because a whole session was lost correlating screenshots against console
			// replies BY HAND and getting a different answer each time - the console said the cells
			// were laid and the money spent, the picture showed neither, and seven explanations for
			// that were each plausible and each wrong. A capture cannot arbitrate between the two
			// halves of the program; only the program can. So this reports the DRAW side - how many
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

				Reply( $"drawn: paths {ParkPaths.Current?.Drawn.ToString() ?? "<no ParkPaths>"} cells, " +
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
			// THE THING UNDER THE CELL IS RESOLVED THE WAY A FRAME RESOLVES IT - ParkPicking.ThingOn,
			// exactly as Level.WorldClick reaches it through ParkPicking.ThingUnderCursor. Only the ray
			// is skipped, because a harness has no cursor to cast from. Reading the cell's own occupant
			// here instead made an instrument that could not see the thing it was pointed at: a placed
			// thing sits on ONE cell's occupancy list and owns the rest of its footprint through
			// mParentID, so this answered on the anchor alone, and a measurement taken through it read
			// 1 of 12 cells of a ride whatever the picking code did.
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

				var clickedThing = ParkState.OnMap( clickedX, clickedY )
					? ParkPicking.ThingOn( clickedState, clickedX, clickedY )
					: 0;

				Reply( clickedPark.ClickWorldAt( clickedX, clickedY, clickedThing ) );
				break;

			// Arms a build mode, so that clicking the park lays a RUN rather than one cell. The mouse
			// half is Level.WorldClick; this exists for the reason `click` and `put` do - a harness
			// cannot press a button, and without it the anchor-and-commit path is unreachable by any
			// test. `tool` alone reports what is armed.
			case "tool":
				if ( parts.Length < 2 )
				{
					Reply( $"tool: mode {ParkBuildMode.Current}" +
						(ParkBuildMode.Anchored
							? $", anchored at ({ParkBuildMode.Anchor.X},{ParkBuildMode.Anchor.Y})"
							: ", not anchored") +
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

			// The queue tool's preview as the SIMULATION computes it for a cell, beside what the marker mesh
			// last drew for the pointer - the two halves, in one reply, for the reason `drawn` gives.
			case "strip":
				if ( parts.Length < 3 )
				{
					Reply( $"strip <cellX> <cellY> - drawn for the pointer: {ParkBuildMarkers.Current?.Census() ?? "<no markers>"}" );
					break;
				}

				Reply( "strip: " + string.Join( " ", ParkPathBuilding.QueueStrip( (int)Argument( 1 ), (int)Argument( 2 ) )
					.Select( square => $"({square.X},{square.Y}):{square.Marker}" + (square.Why is { } why ? $"[{why}]" : "") ) ) );
				break;

			// WHY a guest is not going anywhere, which `peeps` cannot answer and four driven runs were
			// wasted guessing at. MajorDest is written only after a route succeeds, so a guest who
			// chose somewhere unreachable looks identical to one who chose nothing - and those two
			// want opposite fixes. This asks the chooser and the router separately.
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
				// show, which is how a queue cell that named no owner went unnoticed.
				Reply( $"cell ({probeX},{probeY}) type {probed.Type} neighbours 0x{probed.Neighbours:x2} " +
					$"direction 0x{probed.Direction:x2} flags 0x{probed.Flags:x4} " +
					$"tile set {probed.TileSet} index {probed.TileIndex} angle {probed.TileAngle} " +
					$"parent {probed.ParentId}" +
					(probed.ParentId != 0
						? $" = ({MapStep.CellAt( probed.ParentId ).X},{MapStep.CellAt( probed.ParentId ).Y})"
						: "") +
					$" occupant {ParkState.Current?.CellAt( probeX, probeY ).Occupant ?? 0}" );
				break;

			// Opens the park's own game menu - the thing that actually HOLDS THE WORLD. A window with
			// Pauses set is the only thing that stops GameClock, and until this was added nothing here
			// could produce one: buyscreen, hirescreen and openthing all leave the clock running, and
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
			// and the pointer is not something a harness can move reliably - so this opens it the way
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

			// What the park is worth. It exists so that a test can prove money moved by EXACTLY one
			// amount: the clock is stopped under `pause`, so between two of these with no `step`
			// between them no tick runs, nobody pays at the gate, and nothing but the command under
			// test can have moved the balance. Measuring across a stepped frame instead is how a
			// refund check came back 25 out - the park had earned it.
			// The FEE and the GATE are here beside the balance because the entry-price screen moves both,
			// and a screenshot can show that a number changed without saying what it changed to. With
			// them printed, "the plus button charged one more" is a measurement rather than a picture.
			case "money":
				Reply( Level.Current?.ParkState is { } purse
					? $"money balance {purse.Balance} takings {purse.Takings} " +
						$"fee {purse.AdmissionFee} gate {(purse.ParkIsClosed ? "shut" : "open")}"
					: "money: a park has to be loaded" );
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

			case "quit":
				Reply( "quitting" );
				Environment.Exit( 0 );
				break;

			default:
				// Kept in the order the cases appear, so a command added without a line here shows
				// up as an obvious gap. weather, bolt and camcorder were missing from this list
				// before assets was added to it.
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
			+ $"{LobbyCameraMode.LeaveDescription()} "
			+ $"clock={(Time.Paused ? "paused" : "running")} stepping={Time.StepFrames} "
			+ $"game={(GameClock.Paused ? "paused" : "running")} ticks={GameClock.Ticks} "
			+ $"day={GameCalendar.Days} season={GameCalendar.Season} date={GameCalendar.Now:yyyy-MM-dd} "
			+ $"quality={ParkWeather.Current?.Quality} drops={ParkWeather.Current?.Drops} "
			+ $"storm={ParkWeather.Current?.Lightning} snow={ParkWeather.Current?.Snowing} "
			+ $"strikes={ParkWeather.Current?.DebugStrikes} forecast={ParkWeather.Current?.Forecast} "
			+ $"flash={ParkWeather.Current?.DebugBolt.Flash:F3} "
			+ $"boltOpacity={ParkWeather.Current?.DebugBolt.DebugOpacity:F3} "
			+ $"boltDist={ParkWeather.Current?.DebugBolt.DebugAxisDistance:F0} "
			// Compact, because Vector3's own formatting carries spaces and a harness splitting the
			// reply on whitespace gets a single bracket - which is exactly what the first bolt probe
			// reported back.
			+ $"cam={Camera.Position.X:F0},{Camera.Position.Y:F0},{Camera.Position.Z:F0} "
			+ $"rainy={script?.Rainy} lightning={script?.Lightning} "
			+ $"strikes/s={script?.StrikesPerSecond:F3} flyers={script?.FlyingMeshes.Count}";
	}

	private static void Reply( string message )
	{
		Console.Out.WriteLine( $"[dbg] {message}" );
		Console.Out.Flush();
	}
}
