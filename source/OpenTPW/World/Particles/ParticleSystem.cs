using OpenTPW.UI;

namespace OpenTPW;

/// <summary>
/// The original's particle system: effects out of <c>data\Particle\Tp2.plb</c> (see
/// <see cref="ParticleLibraryFile"/>), each run by an emitter that throws out particles, with
/// effectors pulling on them.
///
/// <para>
/// It follows the game's own code closely, because the effects are tuned to it. Everything is whole
/// numbers, as there. Particles_Init (0x0051faa0) sets up 120 emitters, 20 effectors and 2048
/// particles, and the random numbers come from the C library's generator, seeded with 1. The state
/// machine steps it (0x00520130) every 31 milliseconds, in the lobby as in a park, catching up at
/// most half a second at a time - so here it ticks on the same beat off <see cref="Time.Delta"/>,
/// and a paused clock stops it. Nothing moves between ticks: the original draws the last tick.
/// </para>
/// <para>
/// A tick steps every emitter, newest first: counts down its life, and either ends it or emits;
/// then it steps the effectors, then every particle. Effects are started by number
/// (<see cref="Spawn"/>, 0x00521e60) at a position sixteen times the size of particle space, and
/// come back as a handle - a slot and a generation, so a handle to an emitter that has since ended
/// and been reused does nothing. Ending one (<see cref="Kill"/>, 0x0051feb0) only stops it
/// emitting: what it already threw out lives out its time, unless the effect says otherwise.
/// </para>
/// <para>
/// <b>On the screen.</b> An effect flagged for the screen is laid over the interface rather than the
/// world - see <see cref="ScreenParticles"/> for how particle space maps onto the screen. The
/// original only ran at 4:3; on a wider window an effect is pinned the way a control at its
/// position would be (<see cref="VirtualScreen"/>), and an effect started by another inherits its
/// pin, so what an effect decorates and the effect stay together.
/// </para>
/// <para>
/// Not built: drawing effects in the world, and the ground an emitter or effector bounces off, which
/// is flat at zero - what the system's default callback (0x005d60c0) answers.
/// </para>
/// </summary>
internal sealed class ParticleSystem
{
	public const int EmitterCount = 120;
	public const int EffectorCount = 20;
	public const int ParticleCount = 2048;

	private const float TickSeconds = 0.031f;
	private const float LongestCatchUp = 0.5f;

	/// <summary>sin() * 256 over 512 steps, as 0x0051f320 builds it.</summary>
	internal static readonly int[] Sine = Enumerable.Range( 0, 512 ).Select( step => (int)(Math.Sin( step * Math.PI / 256.0 ) * 256.0) ).ToArray();

	public static ParticleSystem? Current { get; private set; }

	public ParticleLibraryFile Library { get; }

	/// <summary>
	/// The particle density setting, in 1024ths: effects that do not say otherwise emit at their rates
	/// and counts scaled by it (0x00521d60). The detail files set it - <c>GameOptions.PARTICLEDENSITY</c>
	/// is 500 in low.sam, 1000 in med.sam and 1500 in high.sam.
	/// </summary>
	public int Density { get; }

	internal readonly Emitter[] Emitters = new Emitter[EmitterCount];
	internal readonly Effector[] Effectors = new Effector[EffectorCount];
	internal readonly Particle[] Particles = new Particle[ParticleCount];

	private int _firstEmitter = -1;
	private int _freeEmitter;
	private int _firstEffector = -1;
	private int _freeEffector;
	private int _freeParticle;

	private int _seed = 1;
	private int _generation;
	private int _ticks;
	private float _owed;

	public ParticleSystem( string libraryPath, int density )
	{
		Library = new ParticleLibraryFile( libraryPath );
		Density = density == 0 ? 1 : density;

		for ( int i = 0; i < EmitterCount; ++i )
			Emitters[i] = new Emitter { Next = i + 1 < EmitterCount ? i + 1 : -1 };

		for ( int i = 0; i < EffectorCount; ++i )
			Effectors[i] = new Effector { Next = i + 1 < EffectorCount ? i + 1 : -1 };

		for ( int i = 0; i < ParticleCount; ++i )
			Particles[i] = new Particle { Next = i + 1 < ParticleCount ? i + 1 : -1, Previous = -1 };

		Current = this;
	}

	/// <summary>Runs however many 31ms ticks have come due.</summary>
	public void Update()
	{
		_owed = MathF.Min( _owed + Time.Delta, LongestCatchUp );

		while ( _owed >= TickSeconds )
		{
			_owed -= TickSeconds;
			Tick();
		}
	}

	/// <summary>
	/// Starts effect <paramref name="effect"/> at a position in the units effects are started at, and
	/// returns its handle, or 0 if there is no such effect or no free emitter. An on-screen effect is
	/// pinned by <paramref name="anchor"/>, or by where it starts when that is not given.
	/// </summary>
	public int Spawn( int effect, int x, int y, int z, Anchor? anchor = null )
	{
		if ( effect < 0 || effect >= Library.Effects.Length )
			return 0;

		var slot = TakeEmitter();
		if ( slot < 0 )
			return 0;

		var template = Library.Effects[effect];
		var emitter = Emitters[slot];

		emitter.Template = template;
		emitter.Effect = effect;
		emitter.Active = true;
		emitter.Hidden = false;
		emitter.Moves = true;
		emitter.Lifetime = emitter.FirstLifetime = template.Lifetime;
		emitter.X = x >> 4;
		emitter.Y = y >> 4;
		emitter.Z = z >> 4;
		emitter.VX = template.EmitterVelocity.X + Signed( template.EmitterVelocityRandom );
		emitter.VY = template.EmitterVelocity.Y + Signed( template.EmitterVelocityRandom );
		emitter.VZ = template.EmitterVelocity.Z + Signed( template.EmitterVelocityRandom );
		emitter.EndSpawn = template.EndSpawn;
		emitter.Countdown = 0;
		emitter.OrbitAngle = template.OrbitAngle;
		emitter.RingRadius = 0;
		emitter.Count = 0;
		emitter.FirstParticle = -1;
		emitter.Jitters = !template.Jitter.IsZero;
		emitter.Linked = 0;
		emitter.Anchor = anchor ?? AnchorAt( x );

		for ( int i = 0; i < 4; ++i )
			emitter.Rates[i] = template.IgnoresDensity ? template.Rates[i] : ScaleRate( template.Rates[i] );

		emitter.MaxParticles = template.MaxParticles;
		var burst = template.Burst;

		if ( !template.IgnoresDensity )
		{
			if ( emitter.MaxParticles > 0 )
				emitter.MaxParticles = Math.Max( (short)((Density * emitter.MaxParticles) >> 10), (short)1 );

			if ( burst > 0 )
				burst = Math.Max( (Density * burst) >> 10, 1 );
		}

		_generation = (_generation % 0xffff) + 1;
		emitter.Generation = _generation;

		if ( template.EmissionMode == 0 )
		{
			for ( int i = 0; i < burst; ++i )
				Emit( slot );
		}

		if ( template.Linked >= 0 && template.Linked != effect )
			emitter.Linked = SpawnLinked( emitter, x, y, z );

		return (emitter.Generation << 16) | slot;
	}

	/// <summary>Stops an effect emitting (0x0051feb0). Its particles live out their time.</summary>
	public void Kill( int handle )
	{
		if ( TryEmitter( handle, out var emitter ) )
			emitter.Lifetime = -2;
	}

	/// <summary>Stops an effect being drawn, or lets it be drawn again (0x0051ff10).</summary>
	public void Hide( int handle, bool hidden )
	{
		if ( TryEmitter( handle, out var emitter ) )
			emitter.Hidden = hidden;
	}

	/// <summary>Moves an effect's emitter (0x0051fe30), in the units effects are started at.</summary>
	public void Move( int handle, int x, int y, int z )
	{
		if ( !TryEmitter( handle, out var emitter ) )
			return;

		emitter.X = x >> 4;
		emitter.Y = y >> 4;
		emitter.Z = z >> 4;
	}

	/// <summary>Starts effector <paramref name="effector"/> (0x00522360) and returns its handle, or 0.</summary>
	public int SpawnEffector( int effector, int x, int y, int z, Anchor anchor )
	{
		if ( effector < 0 || effector >= Library.Effectors.Length || _freeEffector < 0 )
			return 0;

		var slot = _freeEffector;
		var instance = Effectors[slot];
		_freeEffector = instance.Next;

		Link( ref _firstEffector, slot, instance, Effectors );

		var template = Library.Effectors[effector];
		instance.Template = template;
		instance.Moves = true;
		instance.X = x >> 4;
		instance.Y = y >> 4;
		instance.Z = z >> 4;
		instance.VX = template.Velocity.X;
		instance.VY = template.Velocity.Y;
		instance.VZ = template.Velocity.Z;
		instance.Radius = template.Radius;
		instance.Lifetime = template.Lifetime;
		instance.Anchor = anchor;

		_generation = (_generation % 0xffff) + 1;
		instance.Generation = _generation;

		return (instance.Generation << 16) | slot;
	}

	internal bool TryEmitter( int handle, out Emitter emitter )
	{
		emitter = Emitters[(handle & 0xffff) % EmitterCount];
		return handle != 0 && (handle & 0xffff) < EmitterCount && emitter.Generation != 0 && emitter.Generation == handle >> 16;
	}

	private bool TryEffector( int handle, out Effector effector )
	{
		effector = Effectors[(handle & 0xffff) % EffectorCount];
		return handle != 0 && (handle & 0xffff) < EffectorCount && effector.Generation != 0 && effector.Generation == handle >> 16;
	}

	/// <summary>
	/// The effect or effector an effect starts along with it. One that follows is started offset by
	/// its own velocity times sixteen, and does not move by itself after that, only with this one.
	/// </summary>
	private int SpawnLinked( Emitter emitter, int x, int y, int z )
	{
		var template = emitter.Template;

		if ( template.LinkedIsEffector )
		{
			if ( template.LinkedFollows && template.Linked < Library.Effectors.Length )
			{
				var linked = Library.Effectors[template.Linked].Velocity;
				(x, y, z) = (x + (linked.X * 16), y + (linked.Y * 16), z + (linked.Z * 16));
			}

			var handle = SpawnEffector( template.Linked, x, y, z, emitter.Anchor );

			if ( template.LinkedFollows && TryEffector( handle, out var effector ) )
				effector.Moves = false;

			return handle;
		}
		else
		{
			if ( template.LinkedFollows && template.Linked < Library.Effects.Length )
			{
				var linked = Library.Effects[template.Linked].EmitterVelocity;
				(x, y, z) = (x + (linked.X * 16), y + (linked.Y * 16), z + (linked.Z * 16));
			}

			var handle = Spawn( template.Linked, x, y, z, emitter.Anchor );

			if ( template.LinkedFollows && TryEmitter( handle, out var follower ) )
				follower.Moves = false;

			return handle;
		}
	}

	/// <summary>One step of the whole system - 0x00520130.</summary>
	private void Tick()
	{
		// The first step after the system starts does nothing.
		if ( ++_ticks < 2 )
			return;

		for ( int slot = _firstEmitter; slot >= 0; )
		{
			var emitter = Emitters[slot];
			var next = emitter.Next;
			var template = emitter.Template;

			if ( !template.Endless )
				emitter.Lifetime--;

			if ( emitter.Lifetime < 0 )
				EndEmitter( slot, emitter );
			else
				RunEmitter( slot, emitter );

			slot = next;
		}

		StepEffectors();
		StepParticles();
	}

	/// <summary>
	/// An emitter past its life: it starts its end effect the tick it runs out (but not when it was
	/// killed), ends a linked effect that ends with it, and frees its slot once its last particle dies.
	/// </summary>
	private void EndEmitter( int slot, Emitter emitter )
	{
		var template = emitter.Template;

		if ( emitter.EndSpawn >= 0 && emitter.Lifetime == -1 )
		{
			if ( template.EndSpawnIsEffector )
				SpawnEffector( emitter.EndSpawn, emitter.X << 4, emitter.Y << 4, emitter.Z << 4, emitter.Anchor );
			else
				Spawn( emitter.EndSpawn, emitter.X << 4, emitter.Y << 4, emitter.Z << 4, emitter.Anchor );

			emitter.EndSpawn = -1;
		}

		if ( template.LinkedEndsWithEmitter && emitter.Linked != 0 )
		{
			if ( !template.LinkedIsEffector && TryEmitter( emitter.Linked, out var linked ) )
				linked.Lifetime = -1;
			else if ( template.LinkedIsEffector && TryEffector( emitter.Linked, out var effector ) )
				effector.Lifetime = -1;
		}

		if ( emitter.Count == 0 )
			FreeEmitter( slot, emitter );
	}

	private void RunEmitter( int slot, Emitter emitter )
	{
		var template = emitter.Template;

		if ( template.LinkedFollows && emitter.Linked != 0 )
		{
			if ( !template.LinkedIsEffector && TryEmitter( emitter.Linked, out var linked ) )
				(linked.X, linked.Y, linked.Z) = (emitter.X, emitter.Y, emitter.Z);
			else if ( template.LinkedIsEffector && TryEffector( emitter.Linked, out var effector ) )
				(effector.X, effector.Y, effector.Z) = (emitter.X, emitter.Y, emitter.Z);
		}

		if ( template.OrbitSpeed != 0 )
			emitter.OrbitAngle = (emitter.OrbitAngle + template.OrbitSpeed) & 0xfff;

		if ( emitter.Moves && (emitter.VX != 0 || emitter.VY != 0 || emitter.VZ != 0 || template.EmitterGravity != 0) )
			MoveEmitter( emitter );

		if ( template.EmissionMode == 0 )
		{
			var rate = Rate( emitter );

			if ( emitter.Count < emitter.MaxParticles )
			{
				if ( rate < 0 )
				{
					if ( --emitter.Countdown < 1 )
					{
						Emit( slot );
						emitter.Countdown = -rate;
					}
				}
				else
				{
					for ( int i = 0; i < rate; ++i )
						Emit( slot );
				}
			}
		}
		else if ( template.EmissionMode == 1 )
		{
			EmitRing( slot );
		}
	}

	/// <summary>The rate for the quarter of its life the emitter is in (0x00520470).</summary>
	private static int Rate( Emitter emitter )
	{
		var quarter = emitter.FirstLifetime != 0 ? (emitter.Lifetime << 2) / emitter.FirstLifetime : 4;

		return quarter switch
		{
			0 => emitter.Rates[3],
			1 => emitter.Rates[2],
			2 => emitter.Rates[1],
			_ => emitter.Rates[0]
		};
	}

	/// <summary>0x00521d60: a rate scaled by the density, kept to a signed byte and never to nothing.</summary>
	private int ScaleRate( int rate )
	{
		if ( rate == 0 )
			return 0;

		var scaled = Math.Min( (Density * rate) >> 10, 127 );
		return scaled == 0 ? 1 : (sbyte)scaled;
	}

	/// <summary>An emitter under its own velocity - 0x00520e00.</summary>
	private static void MoveEmitter( Emitter emitter )
	{
		var template = emitter.Template;

		emitter.VX -= emitter.VX * template.EmitterDrag / 1024;
		emitter.VY -= (emitter.VY * template.EmitterDrag / 1024) + template.EmitterGravity;
		emitter.VZ -= emitter.VZ * template.EmitterDrag / 1024;

		emitter.X += emitter.VX;
		emitter.Y += emitter.VY;
		emitter.Z += emitter.VZ;

		if ( !template.EmitterCollides || emitter.Y >= 0 )
			return;

		if ( emitter.Y < emitter.VY )
		{
			emitter.X -= emitter.VX;
			emitter.Y -= emitter.VY;
			emitter.VX = -(emitter.VX / 2);
			emitter.VY /= 2;
			emitter.VZ = -(emitter.VZ / 2);
		}
		else
		{
			emitter.Y = 0;
			emitter.VY = -(emitter.VY / 2);
			emitter.VX /= 2;
			emitter.VZ /= 2;
		}
	}

	/// <summary>Throws out one particle - 0x00520560.</summary>
	private void Emit( int slot )
	{
		var index = TakeParticle( slot );
		if ( index < 0 )
			return;

		var emitter = Emitters[slot];
		var template = emitter.Template;
		ref var particle = ref Particles[index];

		emitter.Count++;

		particle.Newborn = true;
		particle.Emitter = slot;
		particle.Size = template.StartSize;
		particle.Group = template.Group;
		particle.Frame = 0;

		var quarter = template.ParticleLifetime >> 2;
		var life = template.ParticleLifetime + (quarter == 0 ? 0 : Next() % quarter);
		particle.Life = life;
		particle.Lifetime = life == 0 ? 1 : life;

		particle.Rotation = template.RandomRotation ? Next() & 0x7fff : 0;
		particle.Colour = template.ColourMode == 0 ? template.Colours[15] : template.Colours[Next() & 0xf];

		int x, y, z;
		var area = template.Area;

		if ( area.IsZero )
		{
			(x, y, z) = (emitter.X, emitter.Y, emitter.Z);
		}
		else
		{
			if ( template.SpawnInBox )
			{
				x = Signed( area.X );
				y = AreaHeight( template );
				z = Signed( area.Z );
			}
			else
			{
				// An upright ellipse: a random direction, and anywhere out to the edge, or on it.
				var angle = (Next() & 0xfff) >> 3;

				if ( template.OnAreaEdge )
				{
					x = Cos( angle ) * area.X * 4 >> 10;
					y = AreaHeight( template );
					z = -(Sin( angle ) * area.Z * 4 >> 10);
				}
				else
				{
					x = Cos( angle ) * Positive( area.X ) * 4 >> 10;
					y = AreaHeight( template );
					z = -(Sin( angle ) * Positive( area.Z ) * 4 >> 10);
				}
			}

			if ( template.AreaRotation != 0 )
			{
				var turn = template.AreaRotation / 8;
				var cos = Cos( turn ) * 4;
				var sin = Sin( turn ) * 4;
				(x, z) = ((cos * x - sin * z) >> 10, (cos * z + sin * x) >> 10);
			}

			x += emitter.X;
			y += emitter.Y;
			z += emitter.Z;
		}

		if ( template.Orbit != 0 )
		{
			var turn = emitter.OrbitAngle / 8;
			x += Sin( turn ) * template.Orbit * 4 >> 10;
			z += Cos( turn ) * template.Orbit * 4 >> 10;
		}

		(particle.X, particle.Y, particle.Z) = (x, y, z);

		if ( template.RadialSpeed == 0 )
		{
			particle.VX = template.Velocity.X + Signed( template.VelocityRandom );
			particle.VY = template.Velocity.Y + Signed( template.VelocityRandom );
			particle.VZ = template.Velocity.Z + Signed( template.VelocityRandom );
		}
		else
		{
			var direction = (Next() & 0xfff) >> 3;
			var speed = Positive( template.RadialSpeed );

			particle.VX = Cos( direction ) * speed * 4 >> 10;
			particle.VY = Signed( template.RadialSpeed );
			particle.VZ = Sin( direction ) * speed * 4 >> 10;
		}

		if ( template.InheritVelocity )
		{
			particle.VX += emitter.VX;
			particle.VY += emitter.VY;
			particle.VZ += emitter.VZ;
		}
	}

	private int AreaHeight( ParticleEffectTemplate template )
		=> template.AreaAboveOnly ? Positive( template.Area.Y ) : Signed( template.Area.Y );

	/// <summary>
	/// One tick of a ring effect - 0x005224f0. The ring widens by <see cref="ParticleEffectTemplate.VelocityRandom"/>
	/// a tick, and the wider it is the more particles go round it.
	/// </summary>
	private void EmitRing( int slot )
	{
		var emitter = Emitters[slot];
		var template = emitter.Template;
		var radius = emitter.RingRadius + template.VelocityRandom;

		for ( int i = 0; emitter.Active && Rate( emitter ) * ((radius >> 7) + 32) > i; ++i )
		{
			var index = TakeParticle( slot );
			if ( index < 0 )
				continue;

			ref var particle = ref Particles[index];
			emitter.Count++;

			particle.Emitter = slot;
			particle.Newborn = true;
			particle.Size = template.StartSize;
			particle.Group = template.Group;
			particle.Colour = template.Colours[15];
			particle.Frame = 0;
			particle.Rotation = 0;

			var half = template.ParticleLifetime >> 1;
			var life = template.ParticleLifetime + (half == 0 ? 0 : Next() % half);
			particle.Life = life;
			particle.Lifetime = life == 0 ? 1 : life;

			var angle = (Next() & 0xfff) >> 3;

			particle.X = (Cos( angle ) * radius * 4 >> 10) + emitter.X;
			particle.Y = emitter.Y;
			particle.Z = emitter.Z - (Sin( angle ) * radius * 4 >> 10);

			particle.VX = Cos( angle ) * template.VelocityRandom * 4 >> 10;
			particle.VY = template.Velocity.Y + 20;
			particle.VZ = -(Sin( angle ) * template.VelocityRandom * 4 >> 10);
		}

		emitter.RingRadius = radius;
	}

	/// <summary>
	/// Every effector - 0x00520f10. Each pulls every particle of its group towards a point scattered
	/// afresh round it, harder the closer the particle is, and one past its life starts its end
	/// effect and goes.
	/// </summary>
	private void StepEffectors()
	{
		for ( int slot = _firstEffector; slot >= 0; )
		{
			var effector = Effectors[slot];
			var next = effector.Next;
			var template = effector.Template;

			if ( !template.Endless )
				effector.Lifetime--;

			if ( effector.Radius == 0 )
				effector.Radius = 1;

			if ( effector.Moves && (effector.VX != 0 || effector.VY != 0 || effector.VZ != 0 || template.Gravity != 0) )
				MoveEffector( effector );

			if ( effector.Lifetime < 0 )
			{
				if ( template.EndSpawnIsEffector )
					SpawnEffector( template.EndSpawn, effector.X << 4, effector.Y << 4, effector.Z << 4, effector.Anchor );
				else
					Spawn( template.EndSpawn, effector.X << 4, effector.Y << 4, effector.Z << 4, effector.Anchor );

				Unlink( ref _firstEffector, slot, effector, Effectors );
				effector.Generation = 0;
				effector.Next = _freeEffector;
				_freeEffector = slot;

				slot = next;
				continue;
			}

			var targetX = effector.X + Signed( template.Scatter.X );
			var targetY = effector.Y + Signed( template.Scatter.Y );
			var targetZ = effector.Z + Signed( template.Scatter.Z );
			var killDistance = effector.Radius * effector.Radius >> 8;

			for ( int e = _firstEmitter; e >= 0; e = Emitters[e].Next )
			{
				var emitter = Emitters[e];

				if ( template.Group != -1 && template.Group != (sbyte)emitter.Template.Group )
					continue;

				for ( int p = emitter.FirstParticle; p >= 0; p = Particles[p].Next )
				{
					ref var particle = ref Particles[p];

					// One that pulls on every group spares effects that ask to be left alone; one
					// that pulls on a group does not.
					if ( template.Group < 0 ? emitter.Template.IgnoresEffectors : particle.Group != template.Group )
						continue;

					var dx = (targetX - particle.X) / 4;
					var dy = (targetY - particle.Y) / 4;
					var dz = (targetZ - particle.Z) / 4;
					var distance = ((dx * dx + dy * dy + dz * dz) >> 8) + 10;

					particle.VX += template.Strength * dx / distance;
					particle.VY += template.Strength * dy / distance;
					particle.VZ += template.Strength * dz / distance;

					if ( template.Mode == 2 && distance < killDistance )
						particle.Life = 0;
				}
			}

			slot = next;
		}
	}

	private static void MoveEffector( Effector effector )
	{
		var template = effector.Template;

		effector.VX -= effector.VX * template.Drag / 1024;
		effector.VY -= (effector.VY * template.Drag / 1024) + template.Gravity;
		effector.VZ -= effector.VZ * template.Drag / 1024;

		effector.X += effector.VX;
		effector.Y += effector.VY;
		effector.Z += effector.VZ;

		if ( !template.Collides || effector.Y >= 0 )
			return;

		if ( effector.Y < effector.VY )
		{
			effector.X -= effector.VX;
			effector.Y -= effector.VY;
			effector.VX = -(effector.VX / 4);
			effector.VY /= 4;
			effector.VZ = -(effector.VZ / 4);
		}
		else
		{
			effector.Y = 0;
			effector.VY = -(effector.VY / 2);
			effector.VX /= 2;
			effector.VZ /= 2;
		}
	}

	/// <summary>
	/// Every particle - 0x005214a0. It ages; one past its life starts its death effect and goes, and
	/// the rest take their colour, frame and size from their age, turn, and move - except in the tick
	/// they were born.
	/// </summary>
	private void StepParticles()
	{
		for ( int slot = _firstEmitter; slot >= 0; slot = Emitters[slot].Next )
		{
			var emitter = Emitters[slot];
			var template = emitter.Template;

			for ( int index = emitter.FirstParticle; index >= 0; )
			{
				ref var particle = ref Particles[index];
				var next = particle.Next;

				if ( --particle.Life < 0 )
				{
					emitter.Count--;

					if ( template.DeathSpawn >= 0 )
						Spawn( template.DeathSpawn, particle.X << 4, particle.Y << 4, particle.Z << 4, emitter.Anchor );

					FreeParticle( index, emitter );
					index = next;
					continue;
				}

				if ( emitter.Lifetime < 0 && template.ParticlesEndWithEmitter )
					particle.Life = -1;

				if ( template.ColourMode == 0 )
					particle.Colour = template.Colours[template.ParticleLifetime < 1 ? 0 : Math.Clamp( particle.Life * 15 / particle.Lifetime, 0, 15 )];
				else if ( (template.ColourMode & 1) != 0 )
					particle.Colour = template.Colours[Positive( 16 )];

				if ( template.Frames == 0 )
				{
					particle.Frame = 0;
				}
				else
				{
					var frames = template.Frames;

					if ( particle.Life >= 0 )
						frames -= (template.Frames - 1) * particle.Life / particle.Lifetime;

					particle.Frame = frames - 1 < template.Frames ? frames - 1 : 0;
				}

				particle.Size = template.ParticleLifetime < 1
					? template.StartSize
					: (short)(((template.StartSize - template.EndSize) * particle.Life / particle.Lifetime) + template.EndSize);

				var spin = template.SpinMin == template.SpinMax ? template.SpinMin : Positive( template.SpinMax - template.SpinMin ) + template.SpinMin;
				particle.Rotation = (particle.Rotation + spin) & 0xffff;

				if ( emitter.Jitters )
				{
					particle.VX += Signed( template.Jitter.X );
					particle.VY += Signed( template.Jitter.Y );
					particle.VZ += Signed( template.Jitter.Z );
				}

				if ( template.Drag != 0 )
				{
					particle.VX -= particle.VX * template.Drag / 1024;
					particle.VY -= particle.VY * template.Drag / 1024;
					particle.VZ -= particle.VZ * template.Drag / 1024;
				}

				particle.VY -= template.Gravity;

				if ( particle.Newborn )
				{
					particle.Newborn = false;
				}
				else
				{
					particle.X += particle.VX;
					particle.Y += particle.VY;
					particle.Z += particle.VZ;
				}

				index = next;
			}
		}
	}

	private int TakeEmitter()
	{
		if ( _freeEmitter < 0 )
			return -1;

		var slot = _freeEmitter;
		var emitter = Emitters[slot];
		_freeEmitter = emitter.Next;

		Link( ref _firstEmitter, slot, emitter, Emitters );
		return slot;
	}

	private void FreeEmitter( int slot, Emitter emitter )
	{
		Unlink( ref _firstEmitter, slot, emitter, Emitters );

		emitter.Active = false;
		emitter.Generation = 0;
		emitter.Next = _freeEmitter;
		_freeEmitter = slot;
	}

	/// <summary>A free particle, put at the front of its emitter's list - 0x00520d60.</summary>
	private int TakeParticle( int slot )
	{
		if ( _freeParticle < 0 )
			return -1;

		var index = _freeParticle;
		var emitter = Emitters[slot];
		ref var particle = ref Particles[index];

		_freeParticle = particle.Next;

		particle.Next = emitter.FirstParticle;
		particle.Previous = -1;

		if ( emitter.FirstParticle >= 0 )
			Particles[emitter.FirstParticle].Previous = index;

		emitter.FirstParticle = index;
		return index;
	}

	private void FreeParticle( int index, Emitter emitter )
	{
		ref var particle = ref Particles[index];

		if ( emitter.FirstParticle == index )
			emitter.FirstParticle = particle.Next;

		if ( particle.Previous >= 0 )
			Particles[particle.Previous].Next = particle.Next;

		if ( particle.Next >= 0 )
			Particles[particle.Next].Previous = particle.Previous;

		particle.Next = _freeParticle;
		particle.Previous = -1;
		_freeParticle = index;
	}

	private static void Link<T>( ref int first, int slot, T item, T[] items ) where T : class, ILinked
	{
		item.Next = first;
		item.Previous = -1;

		if ( first >= 0 )
			items[first].Previous = slot;

		first = slot;
	}

	private static void Unlink<T>( ref int first, int slot, T item, T[] items ) where T : class, ILinked
	{
		if ( first == slot )
			first = item.Next;

		if ( item.Previous >= 0 )
			items[item.Previous].Next = item.Next;

		if ( item.Next >= 0 )
			items[item.Next].Previous = item.Previous;

		item.Previous = -1;
	}

	/// <summary>The C library's rand() step, without the mask it usually has - most calls here leave the sign in.</summary>
	private int Next()
	{
		_seed = unchecked((_seed * 0x343fd) + 0x269ec3);
		return _seed >> 16;
	}

	private int Signed( int range ) => range == 0 ? 0 : Next() % range;

	private int Positive( int range ) => range == 0 ? 0 : (Next() & 0x7fff) % range;

	private static int Sin( int angle ) => Sine[angle & 0x1ff];

	private static int Cos( int angle ) => Sine[(angle + 0x80) & 0x1ff];

	/// <summary>Which edge an effect started at <paramref name="x"/> is pinned to, as a control there would be.</summary>
	private static Anchor AnchorAt( int x ) => VirtualScreen.AnchorAt( ScreenParticles.ScreenX( x >> 4 ) );
}

internal interface ILinked
{
	int Next { get; set; }
	int Previous { get; set; }
}

/// <summary>A running effect - see <see cref="ParticleEffectTemplate"/> for what the template's fields do.</summary>
internal sealed class Emitter : ILinked
{
	public ParticleEffectTemplate Template = null!;
	public int Effect;

	/// <summary>0 while the slot is free; part of the handle otherwise.</summary>
	public int Generation;

	public bool Active;
	public bool Hidden;

	/// <summary>False while it is kept at the position of the effect it was started with.</summary>
	public bool Moves = true;

	public int X, Y, Z;
	public int VX, VY, VZ;
	public int Lifetime;
	public int FirstLifetime;
	public int EndSpawn;
	public readonly int[] Rates = new int[4];
	public int MaxParticles;
	public int Countdown;
	public int OrbitAngle;
	public int RingRadius;
	public bool Jitters;
	public int Linked;
	public int Count;
	public int FirstParticle = -1;
	public Anchor Anchor;

	public int Next { get; set; } = -1;
	public int Previous { get; set; } = -1;
}

internal sealed class Effector : ILinked
{
	public ParticleEffectorTemplate Template = null!;
	public int Generation;
	public bool Moves = true;
	public int X, Y, Z;
	public int VX, VY, VZ;
	public int Radius;
	public int Lifetime;
	public Anchor Anchor;

	public int Next { get; set; } = -1;
	public int Previous { get; set; } = -1;
}

/// <summary>One particle, as the 52 bytes of the original's (0x0080cef8) hold it.</summary>
internal struct Particle
{
	public int X, Y, Z;
	public int VX, VY, VZ;

	/// <summary>0xAARRGGBB.</summary>
	public uint Colour;

	/// <summary>The ticks it was born with, and the ticks it has left.</summary>
	public int Lifetime, Life;

	/// <summary>It was born this tick, so it does not move yet.</summary>
	public bool Newborn;

	public int Emitter;
	public int Size;
	public int Group;
	public int Frame;

	/// <summary>Of 65536.</summary>
	public int Rotation;

	public int Next, Previous;
}
