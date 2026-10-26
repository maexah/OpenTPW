namespace OpenTPW.UI;

/// <summary>
/// One research panel's pump, the <c>res_fluid</c> mesh, whose 64 parts are 32 of the fluid running (<c>fluid</c> to
/// <c>fluid32</c>) and 32 of it draining and filling (<c>empty01</c> to <c>empty32</c>). The panel keeps it in its user
/// slots: the position in 16.16 (slot 0), the speed (2) and the state (3); slot 1, a clock stamp written on each pass and
/// never read, is not kept here (<c>docs/exe/hud.md</c>, "The pump").
///
/// <para>
/// <c>ResearchPump_SetRate</c> (<c>0x004a95b0</c>) and <c>ResearchPump_Tick</c> (<c>0x004a9fc0</c>, message <c>0x1e</c>,
/// once a UI pass): running it loops frames <c>0x00</c> to <c>0x1f</c>; starting it plays <c>0x30</c> to <c>0x3f</c> and then
/// runs; stopping it plays <c>0x20</c> to <c>0x2f</c> and rests at <c>0x30</c>. Each pass adds twice the speed.
/// </para>
///
/// <para>
/// <b>The run is drawn as one smooth scroll, where the original steps it</b> (a deviation, on by default at Alexah's
/// word, 2026-10-21; <c>docs/exe/hud.md</c>, "The pump"). The run's 32 meshes are <c>fluid</c> moved across by -1/31 of
/// its wrapped texture each, so <c>fluid32</c> is <c>fluid</c> again: the original shows a picture every two or three
/// passes and one picture for five passes at each turn. Here a running pump draws <c>fluid</c> moved by
/// <see cref="Scroll"/> over 31, which moves as the position does, turns at 31 with its fraction kept, and moves a
/// little every drawn frame by the speed it has then (<see cref="Advance"/>), so a change of rate bends it and does not
/// step it (<see cref="Shift"/>). So a loop takes 31 frames of travel where the original's takes 32 and the fraction
/// lost at its turn: at rate 2, 2.35 s against 2.43. The drain and the fill are stepped as the original's: their meshes
/// are two quads whose edge moves, not a scroll. Under <c>graphics faithful</c> the run is stepped too (<see cref="Smooth"/>).
/// </para>
/// </summary>
internal sealed class ResearchPump
{
	internal enum PumpState
	{
		Stopped = 0,
		Running = 1,
		Stopping = 2,
		Starting = 3
	}

	/// <summary>The frame a pump rests on, which the opening gives it (<c>FUN_0065d3a3( 0x30 )</c>).</summary>
	internal const int RestFrame = 0x30;

	/// <summary>Where stopping starts (<c>0x20</c>), and the last frame of the run, of stopping and of starting.</summary>
	internal const int StoppingFrame = 0x20;

	private const int LastRunning = 0x1f;
	private const int LastStopping = 0x2f;
	private const int LastStarting = 0x3f;

	/// <summary>Where the position's whole frames are: its high word.</summary>
	private const int Whole = 16;

	/// <summary>The run's distinct pictures: <c>fluid32</c>, the 32nd, is <c>fluid</c> moved by one whole repeat of its texture.</summary>
	internal const int RunPictures = 31;

	/// <summary>The position, frames in 16.16 (slot 0). The opening writes <c>0x30</c> itself, not <c>0x30</c> frames.</summary>
	internal int Position { get; private set; } = RestFrame;

	/// <summary>The speed, half the frames a pass in 16.16 (slot 2).</summary>
	internal int Speed { get; private set; }

	internal PumpState State { get; private set; } = PumpState.Stopped;

	/// <summary>The part of <c>res_fluid</c> shown.</summary>
	internal int Frame { get; private set; } = RestFrame;

	/// <summary>How many passes have moved it.</summary>
	internal int Passes { get; private set; }

	/// <summary>
	/// Where the drawn run stands, frames in 16.16 below 31 (<see cref="RunPictures"/>): nought where the run starts, twice
	/// the speed more each running pass (<see cref="Advance"/> adds it as the passes elapse), turned at 31 with its fraction kept.
	/// </summary>
	internal float Scroll { get; private set; }

	/// <summary>
	/// Whether the run is drawn as one scroll, OpenTPW's improvement; false under <see cref="GameOptions.Faithful"/>, when every
	/// part is drawn as the original's, picture by picture, and <see cref="Scroll"/> is kept but not drawn.
	/// </summary>
	internal bool Smooth { get; init; } = !GameOptions.Current.Faithful;

	/// <summary>
	/// The part drawn: while running smoothly, the run's first, <c>fluid</c> (moved by <see cref="Shift"/>); else
	/// <see cref="Frame"/>.
	/// </summary>
	internal int DrawnPart => Smooth && State == PumpState.Running ? 0 : Frame;

	/// <summary>
	/// <c>ResearchPump_SetRate</c>: a rate of nought turns a running or starting pump to stopping at <c>0x20</c>; any other
	/// starts a stopped or stopping one at <c>0x30</c> and sets the speed to <c>(rate + 20) × 0x10000 / 100</c>, which a rate
	/// of nought leaves as it was.
	/// </summary>
	internal void SetRate( int rate )
	{
		if ( rate == 0 )
		{
			if ( State is PumpState.Starting or PumpState.Running )
			{
				State = PumpState.Stopping;
				Frame = StoppingFrame;
				Position = StoppingFrame << Whole;
			}

			return;
		}

		if ( State is PumpState.Stopped or PumpState.Stopping )
		{
			State = PumpState.Starting;
			Position = RestFrame << Whole;
			Frame = RestFrame;
		}

		Speed = (rate + 20) * 0x10000 / 100;
	}

	/// <summary>
	/// One whole UI pass: the drawn run moved by it (<see cref="Advance"/>) and the pass itself (<see cref="Step"/>).
	/// </summary>
	internal void Pass()
	{
		Advance( 1f );
		Step();
	}

	/// <summary>
	/// The drawn run moved on by <paramref name="passes"/> of a pass at the speed it has now, twice the speed a pass, while
	/// running; nothing otherwise. The screen calls it for the part of the passes each drawn frame spans, so the line
	/// the scroll draws bends where the speed changes and does not step.
	/// </summary>
	internal void Advance( float passes )
	{
		if ( State == PumpState.Running && passes > 0f )
			Scroll = (Scroll + (passes * Speed * 2f)) % (RunPictures << Whole);
	}

	/// <summary>
	/// <c>ResearchPump_Tick</c> on one UI pass, the pass's move of the drawn run taken by <see cref="Advance"/>: nothing while stopped; otherwise twice the speed added and the frame its
	/// high word, a run past <c>0x1f</c> back to nought, a stop past <c>0x2f</c> to rest at <c>0x30</c>, a start past
	/// <c>0x3f</c> into the run at nought. The position is set to the new frame exactly at each of those turns.
	/// </summary>
	internal void Step()
	{
		if ( State == PumpState.Stopped )
			return;

		if ( State != PumpState.Running )
			Scroll = 0;

		var position = Position + (Speed * 2);
		var frame = position >> Whole;

		switch ( State )
		{
			case PumpState.Running when frame > LastRunning:
				frame = 0;
				position = 0;
				break;

			case PumpState.Stopping when frame > LastStopping:
				State = PumpState.Stopped;
				frame = RestFrame;
				position = RestFrame << Whole;
				break;

			case PumpState.Starting when frame > LastStarting:
				State = PumpState.Running;
				frame = 0;
				position = 0;
				break;
		}

		Frame = frame;
		Position = position;
		++Passes;
	}

	/// <summary>
	/// How far <see cref="DrawnPart"/>'s texture is moved, in repeats of it: while running smoothly, <see cref="Scroll"/> over
	/// 31, the way each of the run's meshes is moved (-1/31); else nought.
	/// </summary>
	internal float Shift()
		=> Smooth && State == PumpState.Running ? -(Scroll / (1 << Whole)) / RunPictures : 0f;

	public override string ToString() => $"{State.ToString().ToLowerInvariant()} frame 0x{Frame:x2} speed {Speed}"
		+ (State == PumpState.Running ? Smooth ? $" scroll {Scroll / (float)(1 << Whole):0.000}" : " stepped" : "");
}
