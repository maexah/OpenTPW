using System.Buffers.Binary;
using System.Text;

namespace OpenTPW;

/// <summary>
/// A particle library, <c>data\Particle\Tp2.plb</c>: every particle effect the game can start, and
/// every effector - an attractor or repeller that pulls on particles.
///
/// <para>
/// The layout comes from the game's loader (0x0051f370). A count and a record size, then that many
/// effect records; a count and a record size again, then the effector records; then two more
/// numbers, 33 and 1024 in the only library there is, which the loader keeps (clamping the first to
/// 10-500) but nothing found reads. The game refuses records of any other size.
/// </para>
/// <para>
/// Effects are started by number, and <c>data\Particle\par_lib.h</c>, shipped beside the library,
/// names each one - <c>P_EFFECT_KeySparkle</c> is 97 - and each effector (<c>E_EFFECT_*</c>).
/// OpenTPW has that list as <c>ParLib</c>.
/// </para>
/// </summary>
public sealed class ParticleLibraryFile : BaseFormat
{
	public ParticleEffectTemplate[] Effects { get; private set; } = [];
	public ParticleEffectorTemplate[] Effectors { get; private set; } = [];

	public ParticleLibraryFile( string path )
	{
		ReadFromFile( path );
	}

	public ParticleLibraryFile( Stream stream )
	{
		ReadFromStream( stream );
	}

	protected override void ReadFromStream( Stream stream )
	{
		using var reader = new BinaryReader( stream );

		Effects = ReadRecords( reader, ParticleEffectTemplate.RecordSize, record => new ParticleEffectTemplate( record ) );
		Effectors = ReadRecords( reader, ParticleEffectorTemplate.RecordSize, record => new ParticleEffectorTemplate( record ) );
	}

	private static T[] ReadRecords<T>( BinaryReader reader, int recordSize, Func<byte[], T> read )
	{
		var count = reader.ReadInt32();
		var size = reader.ReadInt32();

		if ( size != recordSize )
			throw new InvalidDataException( $"Particle library records are {size} bytes where {recordSize} were expected" );

		var records = new T[count];

		for ( int i = 0; i < count; ++i )
			records[i] = read( reader.ReadBytes( size ) );

		return records;
	}

	internal static int Int( byte[] record, int offset ) => BinaryPrimitives.ReadInt32LittleEndian( record.AsSpan( offset ) );
	internal static short Short( byte[] record, int offset ) => BinaryPrimitives.ReadInt16LittleEndian( record.AsSpan( offset ) );
	internal static ParticleVector Vector( byte[] record, int offset ) => new( Int( record, offset ), Int( record, offset + 4 ), Int( record, offset + 8 ) );

	internal static string Name( byte[] record, int offset, int length )
	{
		var bytes = record.AsSpan( offset, length );
		var end = bytes.IndexOf( (byte)0 );
		return Encoding.ASCII.GetString( end < 0 ? bytes : bytes[..end] );
	}
}

/// <summary>Three whole numbers in particle space - see <see cref="ParticleEffectTemplate"/>.</summary>
public readonly record struct ParticleVector( int X, int Y, int Z )
{
	public bool IsZero => X == 0 && Y == 0 && Z == 0;
}

/// <summary>
/// One effect of a <see cref="ParticleLibraryFile"/>: an emitter's settings, 320 bytes.
///
/// <para>
/// Starting an effect copies its record whole into an emitter slot (0x00521e60), and the running
/// emitter is read at the same offsets, which is how each field below was identified: by what the
/// emitter code (0x00520130, 0x00520560, 0x005214a0) does with that offset. Everything is a whole
/// number. Distances are in particle space, a sixteenth of the units effects are started at;
/// times are in ticks of the particle system, which steps every 31 milliseconds.
/// </para>
/// <para>
/// Offsets the game only uses while an effect runs - its slot's links, its particle count, the
/// generation in its handle - are not listed. Nor is the bounding box at the end, which is filled
/// in as the particles move.
/// </para>
/// </summary>
public sealed class ParticleEffectTemplate
{
	public const int RecordSize = 320;

	/// <summary>+0x118, 16 bytes. Space for longer names, so what follows the NUL is often left over from another.</summary>
	public string Name { get; }

	/// <summary>+0x04. Given to each particle, so an effector can pull on only one group.</summary>
	public int Group { get; }

	/// <summary>+0x05. Drawn flat on the screen rather than in the world.</summary>
	public bool OnScreen { get; }

	/// <summary>+0x0E. 0 emits at <see cref="Rates"/>; 1 emits rings that widen every tick.</summary>
	public int EmissionMode { get; }

	/// <summary>+0x10. The effect (or effector - <see cref="EndSpawnIsEffector"/>) started where the emitter ends, -1 for none.</summary>
	public int EndSpawn { get; }

	/// <summary>+0x12. Up to this much is added to each of <see cref="EmitterVelocity"/>'s parts when the effect starts.</summary>
	public int EmitterVelocityRandom { get; }

	/// <summary>+0x20. How many ticks the emitter runs.</summary>
	public int Lifetime { get; }

	/// <summary>+0x24. How much of its velocity the emitter itself loses a tick, in 1024ths.</summary>
	public int EmitterDrag { get; }

	/// <summary>+0x26. The emitter's lifetime never counts down, so it runs until something stops it.</summary>
	public bool Endless { get; }

	/// <summary>+0x28. Taken off the emitter's upward velocity every tick.</summary>
	public int EmitterGravity { get; }

	/// <summary>+0x2C. The emitter's own velocity.</summary>
	public ParticleVector EmitterVelocity { get; }

	/// <summary>+0x38. Every particle's starting velocity, before <see cref="VelocityRandom"/>.</summary>
	public ParticleVector Velocity { get; }

	/// <summary>+0x44. How far from the emitter particles appear on each axis - see <see cref="SpawnInBox"/>.</summary>
	public ParticleVector Area { get; }

	/// <summary>+0x50. Up to this much either way is added to each part of a particle's velocity. A ring's growth a tick.</summary>
	public int VelocityRandom { get; }

	/// <summary>+0x52. The depth an on-screen particle is drawn at.</summary>
	public int Depth { get; }

	/// <summary>+0x54. Where round <see cref="Orbit"/> the emitter starts, of 4096.</summary>
	public int OrbitAngle { get; }

	/// <summary>+0x58. 0 takes a particle's colour from its age; bit 1 picks one of <see cref="Colours"/> at random every tick; anything else picks one at birth.</summary>
	public int ColourMode { get; }

	/// <summary>+0x5C. When not 0, particles fly off in a random direction at up to this speed instead of taking <see cref="Velocity"/>.</summary>
	public int RadialSpeed { get; }

	/// <summary>+0x60. Particles emitted at once when the effect starts.</summary>
	public int Burst { get; }

	/// <summary>+0x64. The most particles the emitter has alive at once.</summary>
	public int MaxParticles { get; }

	/// <summary>
	/// +0x68, a signed byte for each quarter of the emitter's life, first quarter first: that many
	/// particles a tick, or when negative one particle every that many ticks.
	/// </summary>
	public sbyte[] Rates { get; }

	/// <summary>+0x6C. Particles start with the emitter's velocity added to their own.</summary>
	public bool InheritVelocity { get; }

	/// <summary>+0x6D. Particles start at a random rotation rather than upright.</summary>
	public bool RandomRotation { get; }

	/// <summary>+0x70. How the sprites are drawn: bit 0x4 adds them to the screen, and 0x2000 lets their alpha scale what they add.</summary>
	public int DrawFlags { get; }

	/// <summary>+0x74. A particle's size when it is born.</summary>
	public int StartSize { get; }

	/// <summary>+0x76. The emitter bounces off the ground as it moves.</summary>
	public bool EmitterCollides { get; }

	/// <summary>+0x78. How many ticks a particle lives, plus up to a quarter as many again.</summary>
	public int ParticleLifetime { get; }

	/// <summary>+0x7C. How much of its velocity a particle loses a tick, in 1024ths.</summary>
	public int Drag { get; }

	/// <summary>+0x80. Taken off a particle's upward velocity every tick.</summary>
	public int Gravity { get; }

	/// <summary>+0x84. Up to this much either way is added to a particle's velocity every tick.</summary>
	public ParticleVector Jitter { get; }

	/// <summary>+0x94. Which sprites the particles wear: the file (the number over 16) and the set in it (the rest) - see <see cref="SpriteBankFile"/>.</summary>
	public int SpriteSet { get; }

	/// <summary>+0x96. How many frames of the sprite set a particle plays through over its life. 0 draws plain squares.</summary>
	public int Frames { get; }

	/// <summary>+0x98. The effect started wherever a particle dies, -1 for none.</summary>
	public int DeathSpawn { get; }

	/// <summary>+0x9C. Effectors that pull on every group leave these particles alone.</summary>
	public bool IgnoresEffectors { get; }

	/// <summary>+0xA0. Particles appear only above the emitter within <see cref="Area"/>, not below it too.</summary>
	public bool AreaAboveOnly { get; }

	/// <summary>+0xA2. How spread out the particles are drawn around the emitter, in 1000ths.</summary>
	public int Scale { get; }

	/// <summary>+0xA6. A particle's size when it dies; it grows or shrinks steadily towards it.</summary>
	public int EndSize { get; }

	/// <summary>+0xA8. Particles appear on the edge of <see cref="Area"/> rather than anywhere inside it.</summary>
	public bool OnAreaEdge { get; }

	/// <summary>+0xAA. When the emitter ends, its particles end with it.</summary>
	public bool ParticlesEndWithEmitter { get; }

	/// <summary>+0xAC. Turns <see cref="Area"/> about the upright axis, of 4096.</summary>
	public int AreaRotation { get; }

	/// <summary>+0xB0. What a velocity handed to the running emitter is scaled by, in 1024ths.</summary>
	public int VelocityScale { get; }

	/// <summary>+0xB4. An effect (or effector - <see cref="LinkedIsEffector"/>) started along with this one, -1 for none.</summary>
	public int Linked { get; }

	/// <summary>+0xB8. The linked effect is kept at this emitter's position.</summary>
	public bool LinkedFollows { get; }

	/// <summary>+0xBA. <see cref="Linked"/> is an effector.</summary>
	public bool LinkedIsEffector { get; }

	/// <summary>+0xBB. <see cref="EndSpawn"/> is an effector.</summary>
	public bool EndSpawnIsEffector { get; }

	/// <summary>+0xBC. The linked effect ends when this one does.</summary>
	public bool LinkedEndsWithEmitter { get; }

	/// <summary>+0xBE and +0xC6. A particle turns by something between these a tick, of 65536.</summary>
	public int SpinMin { get; }

	/// <inheritdoc cref="SpinMin"/>
	public int SpinMax { get; }

	/// <summary>+0xC0. The particle density setting leaves the rates and counts alone.</summary>
	public bool IgnoresDensity { get; }

	/// <summary>+0xC1. Refused while the system is told to hold back such effects (0x0080ced8), which nothing found does.</summary>
	public bool Suppressible { get; }

	/// <summary>+0xC2. <see cref="Area"/> is a box; otherwise it is an upright ellipse.</summary>
	public bool SpawnInBox { get; }

	/// <summary>+0xC4. Particles appear this far out from the emitter, round a circle it turns by <see cref="OrbitSpeed"/> a tick.</summary>
	public int Orbit { get; }

	/// <summary>+0xC8. How far round <see cref="Orbit"/> the emitter moves a tick, of 4096.</summary>
	public int OrbitSpeed { get; }

	/// <summary>
	/// +0xD8, sixteen colours as 0xAARRGGBB. A particle's age picks one: the last on the day it is
	/// born, the first as it dies.
	/// </summary>
	public uint[] Colours { get; }

	public ParticleEffectTemplate( byte[] record )
	{
		Name = ParticleLibraryFile.Name( record, 0x118, 16 );
		Group = record[0x04];
		OnScreen = record[0x05] != 0;
		EmissionMode = ParticleLibraryFile.Short( record, 0x0e );
		EndSpawn = ParticleLibraryFile.Short( record, 0x10 );
		EmitterVelocityRandom = ParticleLibraryFile.Short( record, 0x12 );
		Lifetime = ParticleLibraryFile.Int( record, 0x20 );
		EmitterDrag = ParticleLibraryFile.Short( record, 0x24 );
		Endless = ParticleLibraryFile.Short( record, 0x26 ) != 0;
		EmitterGravity = ParticleLibraryFile.Short( record, 0x28 );
		EmitterVelocity = ParticleLibraryFile.Vector( record, 0x2c );
		Velocity = ParticleLibraryFile.Vector( record, 0x38 );
		Area = ParticleLibraryFile.Vector( record, 0x44 );
		VelocityRandom = ParticleLibraryFile.Short( record, 0x50 );
		Depth = ParticleLibraryFile.Short( record, 0x52 );
		OrbitAngle = ParticleLibraryFile.Int( record, 0x54 );
		ColourMode = ParticleLibraryFile.Int( record, 0x58 );
		RadialSpeed = ParticleLibraryFile.Int( record, 0x5c );
		Burst = ParticleLibraryFile.Int( record, 0x60 );
		MaxParticles = ParticleLibraryFile.Short( record, 0x64 );
		Rates = [(sbyte)record[0x68], (sbyte)record[0x69], (sbyte)record[0x6a], (sbyte)record[0x6b]];
		InheritVelocity = record[0x6c] != 0;
		RandomRotation = record[0x6d] != 0;
		DrawFlags = ParticleLibraryFile.Int( record, 0x70 );
		StartSize = ParticleLibraryFile.Short( record, 0x74 );
		EmitterCollides = ParticleLibraryFile.Short( record, 0x76 ) != 0;
		ParticleLifetime = ParticleLibraryFile.Int( record, 0x78 );
		Drag = ParticleLibraryFile.Int( record, 0x7c );
		Gravity = ParticleLibraryFile.Int( record, 0x80 );
		Jitter = ParticleLibraryFile.Vector( record, 0x84 );
		SpriteSet = ParticleLibraryFile.Short( record, 0x94 );
		Frames = ParticleLibraryFile.Short( record, 0x96 );
		DeathSpawn = ParticleLibraryFile.Int( record, 0x98 );
		IgnoresEffectors = ParticleLibraryFile.Int( record, 0x9c ) != 0;
		AreaAboveOnly = ParticleLibraryFile.Short( record, 0xa0 ) != 0;
		Scale = ParticleLibraryFile.Short( record, 0xa2 );
		EndSize = ParticleLibraryFile.Short( record, 0xa6 );
		OnAreaEdge = ParticleLibraryFile.Short( record, 0xa8 ) != 0;
		ParticlesEndWithEmitter = record[0xaa] != 0;
		AreaRotation = ParticleLibraryFile.Int( record, 0xac );
		VelocityScale = ParticleLibraryFile.Int( record, 0xb0 );
		Linked = ParticleLibraryFile.Int( record, 0xb4 );
		LinkedFollows = ParticleLibraryFile.Short( record, 0xb8 ) != 0;
		LinkedIsEffector = record[0xba] != 0;
		EndSpawnIsEffector = record[0xbb] != 0;
		LinkedEndsWithEmitter = ParticleLibraryFile.Short( record, 0xbc ) != 0;
		SpinMin = ParticleLibraryFile.Short( record, 0xbe );
		IgnoresDensity = record[0xc0] != 0;
		Suppressible = record[0xc1] != 0;
		SpawnInBox = record[0xc2] != 0;
		Orbit = ParticleLibraryFile.Short( record, 0xc4 );
		SpinMax = ParticleLibraryFile.Short( record, 0xc6 );
		OrbitSpeed = ParticleLibraryFile.Int( record, 0xc8 );

		Colours = new uint[16];
		for ( int i = 0; i < Colours.Length; ++i )
			Colours[i] = (uint)ParticleLibraryFile.Int( record, 0xd8 + (i * 4) );
	}
}

/// <summary>
/// One effector of a <see cref="ParticleLibraryFile"/>, 104 bytes: a point that pulls particles
/// towards it, or pushes them away, for as long as it lasts. Read the same way as the effects, from
/// what the effector code (0x00522360 starts one, 0x00520f10 steps them) does with each offset.
/// </summary>
public sealed class ParticleEffectorTemplate
{
	public const int RecordSize = 104;

	/// <summary>+0x50, 16 bytes.</summary>
	public string Name { get; }

	/// <summary>+0x04. Pulls only on particles of this <see cref="ParticleEffectTemplate.Group"/>, or on every group for -1.</summary>
	public int Group { get; }

	/// <summary>+0x0B. Bounces off the ground as it moves.</summary>
	public bool Collides { get; }

	/// <summary>+0x18. The effector's velocity.</summary>
	public ParticleVector Velocity { get; }

	/// <summary>+0x24. How much of its velocity it loses a tick, in 1024ths.</summary>
	public int Drag { get; }

	/// <summary>+0x28. Taken off its upward velocity every tick.</summary>
	public int Gravity { get; }

	/// <summary>+0x2C. How close a particle must come for <see cref="Mode"/> 2 to end it. 0 is taken as 1.</summary>
	public int Radius { get; }

	/// <summary>+0x30. 2 ends particles that come within <see cref="Radius"/>; 0 and 1 only pull.</summary>
	public int Mode { get; }

	/// <summary>+0x34. The point pulled towards moves by up to this much either way, afresh every tick.</summary>
	public ParticleVector Scatter { get; }

	/// <summary>+0x40. How hard it pulls: positive draws particles in, negative drives them off.</summary>
	public int Strength { get; }

	/// <summary>+0x45. Works on on-screen effects.</summary>
	public bool OnScreen { get; }

	/// <summary>+0x46. The effect (or effector - <see cref="EndSpawnIsEffector"/>) started where it ends, -1 for none.</summary>
	public int EndSpawn { get; }

	/// <summary>+0x49. <see cref="EndSpawn"/> is an effector.</summary>
	public bool EndSpawnIsEffector { get; }

	/// <summary>+0x4A. Its lifetime never counts down.</summary>
	public bool Endless { get; }

	/// <summary>+0x4C. How many ticks it lasts.</summary>
	public int Lifetime { get; }

	public ParticleEffectorTemplate( byte[] record )
	{
		Name = ParticleLibraryFile.Name( record, 0x50, 16 );
		Group = ParticleLibraryFile.Int( record, 0x04 );
		Collides = record[0x0b] != 0;
		Velocity = ParticleLibraryFile.Vector( record, 0x18 );
		Drag = ParticleLibraryFile.Int( record, 0x24 );
		Gravity = ParticleLibraryFile.Int( record, 0x28 );
		Radius = ParticleLibraryFile.Int( record, 0x2c );
		Mode = ParticleLibraryFile.Int( record, 0x30 );
		Scatter = ParticleLibraryFile.Vector( record, 0x34 );
		Strength = ParticleLibraryFile.Int( record, 0x40 );
		OnScreen = record[0x45] != 0;
		EndSpawn = ParticleLibraryFile.Short( record, 0x46 );
		EndSpawnIsEffector = record[0x49] != 0;
		Endless = ParticleLibraryFile.Short( record, 0x4a ) != 0;
		Lifetime = ParticleLibraryFile.Int( record, 0x4c );
	}
}
