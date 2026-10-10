using System.Buffers.Binary;

namespace OpenTPW;

/// <summary>
/// The particles module of a park save (<c>TRAP</c>): the particle system's live image, with every emitter a
/// script's <c>ADDOBJ</c> or <c>EVENT</c> had alive, and the effect library it starts them from. FileFormats
/// <c>saves.md</c>, "The particles module"; the writer is <c>FUN_0051f680</c> and the loader <c>FUN_0051f7a0</c>,
/// which starts every emitter in use with no particles.
///
/// <para>
/// <b>An emitter is its template's 320 bytes as <c>Particles_Spawn</c> leaves them</b> (<c>0x00521e60</c>;
/// <c>docs/exe/saves.md</c>, "OpenTPW's writer, an emitter started"), in a slot taken off the free chain and put
/// at the head of the used one. Its handle is its count over its slot, and nought names none.
/// </para>
/// </summary>
public sealed class ParkParticles
{
	/// <summary>The live image's size: a header, 120 emitters, 20 effectors and the chains' heads.</summary>
	public const int LiveSize = 0x9e68;

	/// <summary>The effect library's size: 105 effects and 20 effectors.</summary>
	public const int TemplatesSize = 0x8b60;

	public const int EmitterSize = 0x140;

	public const int EmitterSlots = 120;

	public const int TemplateSlots = 105;

	/// <summary>What a killed emitter's life is set to (<c>Particles_Kill</c>, <c>0x0051feb0</c>): the tick frees it once it holds no particle.</summary>
	public const int KilledLife = -2;

	/// <summary>The handle <c>Particles_Spawn</c> answers with no slot free or no such effect.</summary>
	public const int NoSlot = -1;

	private const string Magic = "LCTP";

	// The live image's header.
	private const int OnScreenOnlyAt = 0x08;
	private const int SeedAt = 0x10;
	private const int NextCountAt = 0x18;
	private const int DensityAt = 0x20;
	private const int EmittersAt = 0x2c;

	// The chains' heads, after the effectors.
	private const int UsedHeadAt = 0x9e4c;
	private const int FreeHeadAt = 0x9e50;

	// An emitter.
	private const int InUseAt = 0x00;
	private const int HoldsParticlesAt = 0x05;
	private const int FirstParticleAt = 0x06;
	private const int ParticlesAt = 0x08;
	private const int CountAt = 0x0a;
	private const int TemplateAt = 0x0c;
	private const int ModeAt = 0x0e;
	private const int RandomRangeAt = 0x12;
	private const int XAt = 0x14;
	private const int HeightAt = 0x18;
	private const int ZAt = 0x1c;
	private const int LifeAt = 0x20;
	private const int VelocityAt = 0x2c;
	private const int DirectedAt = 0x38;
	private const int BurstAt = 0x60;
	private const int MostAt = 0x64;
	private const int RatesAt = 0x68;
	private const int ForcedAt = 0x6f;
	private const int ForceAt = 0x84;
	private const int FollowsAt = 0xab;
	private const int SpeedAt = 0xb0;
	private const int LinkedAt = 0xb4;
	private const int LinkedFollowsAt = 0xb8;
	private const int LinkedIsEffectorAt = 0xba;
	private const int OwnRatesAt = 0xc0;
	private const int OnScreenAt = 0xc1;
	private const int RingAt = 0xcc;
	private const int NextAt = 0xd0;
	private const int PreviousAt = 0xd2;
	private const int FullLifeAt = 0xd4;
	private const int BoundsAt = 0x128;

	private readonly byte[] _live = new byte[LiveSize];

	private readonly byte[] _templates = new byte[TemplatesSize];

	/// <summary>Why the module did not read, or null.</summary>
	public string? Problem { get; }

	/// <summary>Where the live image lies in the body; -1 where the module did not read.</summary>
	public int LiveAt { get; } = -1;

	/// <param name="body">The inflated body of the file.</param>
	/// <param name="at">Where the module's magic should be: the byte after the sprite table's trailer.</param>
	public ParkParticles( byte[] body, int at )
	{
		if ( at < 0 || at + 12 + LiveSize + 4 + TemplatesSize > body.Length || System.Text.Encoding.ASCII.GetString( body, at, 4 ) != Magic )
		{
			Problem = "no particles module after the sprites";
			return;
		}

		if ( BinaryPrimitives.ReadInt32LittleEndian( body.AsSpan( at + 4 ) ) != 1 )
		{
			Problem = "saved with particles off";
			return;
		}

		if ( BinaryPrimitives.ReadInt32LittleEndian( body.AsSpan( at + 8 ) ) != LiveSize
			|| BinaryPrimitives.ReadInt32LittleEndian( body.AsSpan( at + 12 + LiveSize ) ) != TemplatesSize )
		{
			Problem = "the two images are another size";
			return;
		}

		LiveAt = at + 12;
		Buffer.BlockCopy( body, LiveAt, _live, 0, LiveSize );
		Buffer.BlockCopy( body, LiveAt + LiveSize + 4, _templates, 0, TemplatesSize );
	}

	/// <summary>One emitter slot as the file holds it: the place in the park's units times 64.</summary>
	public readonly record struct Emitter( int Slot, bool InUse, int Count, int Template, int X, int Height, int Z, int Life )
	{
		/// <summary>The handle a script's record names it by.</summary>
		public int Handle => (Count << 16) | Slot;
	}

	/// <summary>
	/// An emitter to start: the effect's slot in the library and the place, each of the three as
	/// <c>Particles_Spawn</c> takes it, the park's units times 1024 (<c>0x00557481</c>).
	///
	/// <para>
	/// <see cref="Direction"/> makes it <c>Particles_SpawnFull</c>'s (<c>0x00521930</c>), a script's type 2: the
	/// way its node points, each of the three times 1024 and cut to a whole number, x, the height, z.
	/// </para>
	/// </summary>
	public readonly record struct Spawn( int Template, int X, int Height, int Z, (int X, int Height, int Z)? Direction = null );

	/// <summary>The file's emitter in <paramref name="slot"/>.</summary>
	public Emitter At( int slot ) => Read( _live, slot );

	/// <summary>The emitters in use, from the head of the used chain: the newest first.</summary>
	public IReadOnlyList<Emitter> Used => UsedIn( _live );

	/// <summary>The 320 bytes of the file's emitter in <paramref name="slot"/>.</summary>
	public ReadOnlySpan<byte> Bytes( int slot ) => _live.AsSpan( EmittersAt + (slot * EmitterSize), EmitterSize );

	/// <summary>
	/// The own velocity of the file's emitter in <paramref name="slot"/>, the three words at <c>+0x38</c>: its
	/// effect's, or where a script aims it (type 2) its node's direction times the effect's speed.
	/// </summary>
	public (int X, int Height, int Z) Aim( int slot )
	{
		var at = EmittersAt + (slot * EmitterSize) + DirectedAt;

		return (Int32( _live, at ), Int32( _live, at + 4 ), Int32( _live, at + 8 ));
	}

	/// <summary>The 320 bytes of the effect in <paramref name="slot"/> of the file's library.</summary>
	public ReadOnlySpan<byte> Template( int slot ) => _templates.AsSpan( slot * EmitterSize, EmitterSize );

	/// <summary>Whether <paramref name="handle"/> names an emitter of the file's (<c>FUN_005222e0</c>).</summary>
	public bool Names( int handle ) => Names( _live, handle );

	/// <summary>A copy of the file's live image to start and kill emitters in.</summary>
	public Edit Begin() => new( (byte[])_live.Clone(), _templates );

	/// <summary>Writes <paramref name="edit"/>'s live image over the file's, where it lies.</summary>
	public void Put( byte[] body, Edit edit )
	{
		if ( LiveAt < 0 )
			throw new InvalidOperationException( $"the park file's particles module was not read: {Problem}" );

		Buffer.BlockCopy( edit.Live, 0, body, LiveAt, LiveSize );
	}

	/// <summary>A live image being written: the file's, with emitters started and killed in it.</summary>
	public sealed class Edit
	{
		private readonly byte[] _templates;

		internal byte[] Live { get; }

		internal Edit( byte[] live, byte[] templates )
		{
			Live = live;
			_templates = templates;
		}

		/// <summary>The emitter in <paramref name="slot"/> as it stands.</summary>
		public Emitter At( int slot ) => Read( Live, slot );

		/// <inheritdoc cref="ParkParticles.Used"/>
		public IReadOnlyList<Emitter> Used => UsedIn( Live );

		/// <summary>The 320 bytes of the emitter in <paramref name="slot"/> as it stands.</summary>
		public ReadOnlySpan<byte> Bytes( int slot ) => Live.AsSpan( EmittersAt + (slot * EmitterSize), EmitterSize );

		/// <summary>
		/// Whether <see cref="Start"/> starts <paramref name="template"/> as the engine does: an effect that links
		/// an effector, itself or down its chain of linked emitters, is not started here.
		/// </summary>
		public bool Starts( int template )
		{
			for ( var hops = 0; template >= 0 && template < TemplateSlots && hops < TemplateSlots; ++hops )
			{
				var at = template * EmitterSize;
				var linked = Int32( _templates, at + LinkedAt );

				if ( linked < 0 || linked == template )
					return true;

				if ( _templates[at + LinkedIsEffectorAt] != 0 )
					return false;

				template = linked;
			}

			return true;
		}

		/// <summary>
		/// Kills the emitter <paramref name="handle"/> names, as <c>KILLOBJ</c>, <c>FADEOBJ</c> and a script's end
		/// do (<c>FUN_0051ff70( handle, -2 )</c>): the first tick after a load frees it. False where it names none.
		/// </summary>
		public bool Kill( int handle )
		{
			if ( !Names( Live, handle ) )
				return false;

			Put32( Live, EmittersAt + ((handle & 0xffff) * EmitterSize) + LifeAt, KilledLife );
			return true;
		}

		/// <summary>
		/// Starts an emitter as <c>Particles_Spawn</c> does and answers its handle: nought for an effect of the
		/// screen's while the system keeps to those, <see cref="NoSlot"/> with no slot free or no such effect.
		///
		/// <para>
		/// No particle is emitted: a file holds none, and the loader empties every emitter. An effect that bursts
		/// as it starts draws the world's particle generator for each particle in the engine and not here, so the
		/// seed written is behind the engine's by those draws.
		/// </para>
		/// </summary>
		public int Start( Spawn spawn )
		{
			var template = spawn.Template;

			if ( template < 0 || template >= TemplateSlots )
				return NoSlot;

			var from = template * EmitterSize;

			if ( Int32( Live, OnScreenOnlyAt ) != 0 && _templates[from + OnScreenAt] != 0 )
				return 0;

			var slot = Int16( Live, FreeHeadAt );

			if ( slot < 0 )
				return NoSlot;

			var at = EmittersAt + (slot * EmitterSize);

			// Off the free chain, and on at the head of the used one.
			var usedBefore = Int16( Live, UsedHeadAt );
			var free = Int16( Live, at + NextAt );

			Put16( Live, UsedHeadAt, slot );
			Put16( Live, FreeHeadAt, free );

			if ( free >= 0 )
				Put16( Live, EmittersAt + (free * EmitterSize) + PreviousAt, -1 );

			if ( usedBefore >= 0 )
				Put16( Live, EmittersAt + (usedBefore * EmitterSize) + PreviousAt, slot );

			var previous = Int16( Live, at + PreviousAt );

			Buffer.BlockCopy( _templates, from, Live, at, EmitterSize );
			Put16( Live, at + NextAt, usedBefore );
			Put16( Live, at + PreviousAt, previous );

			if ( Live[at + OwnRatesAt] == 0 )
				ScaleByDensity( at );

			Put32( Live, at + FullLifeAt, Int32( Live, at + LifeAt ) );
			Put32( Live, at + XAt, spawn.X >> 4 );
			Put32( Live, at + HeightAt, spawn.Height >> 4 );
			Put32( Live, at + ZAt, spawn.Z >> 4 );

			// The box its particles have reached: empty, to be grown, for an emitter that keeps one.
			var keeps = Live[at + HoldsParticlesAt] == 0;

			for ( var word = 0; word < 6; ++word )
				Put32( Live, at + BoundsAt + (word * 4), !keeps ? 0 : word < 3 ? int.MaxValue : int.MinValue );

			Put16( Live, at + ParticlesAt, 0 );
			Put16( Live, at + TemplateAt, template );
			Put16( Live, at + InUseAt, 1 );

			// Three draws of the system's own generator, one an axis, each under the effect's range. A directed
			// start makes none.
			var range = spawn.Direction is null ? Int16( Live, at + RandomRangeAt ) : (short)0;

			for ( var axis = 0; axis < 3; ++axis )
			{
				if ( range == 0 )
					continue;

				var seed = unchecked((Int32( Live, SeedAt ) * 0x343fd) + 0x269ec3);

				Put32( Live, SeedAt, seed );
				Put32( Live, at + VelocityAt + (axis * 4), Int32( Live, at + VelocityAt + (axis * 4) ) + ((seed >> 16) % range) );
			}

			var count = Int16( Live, NextCountAt );

			Put16( Live, at + CountAt, count );
			Put16( Live, NextCountAt, unchecked((short)(count + 1)) );
			Put16( Live, at + FirstParticleAt, -1 );
			Live[at + FollowsAt] = 0;

			// A directed start's own velocity: the direction times the effect's speed, over 1024 toward nought, as
			// the sweep writes it again every tick (Particles_SetVelocity, 0x0051fd90).
			if ( spawn.Direction is { } direction )
			{
				var speed = Int32( Live, at + SpeedAt );

				Put32( Live, at + DirectedAt, direction.X * speed / 1024 );
				Put32( Live, at + DirectedAt + 4, direction.Height * speed / 1024 );
				Put32( Live, at + DirectedAt + 8, direction.Z * speed / 1024 );
			}

			Live[at + ForcedAt] = (byte)(Int32( Live, at + ForceAt ) != 0 || Int32( Live, at + ForceAt + 4 ) != 0
				|| Int32( Live, at + ForceAt + 8 ) != 0 ? 1 : 0);

			if ( Int16( Live, at + ModeAt ) == 1 )
				Put32( Live, at + RingAt, 0 );

			var handle = ((ushort)count << 16) | (ushort)slot;
			var linked = Int32( Live, at + LinkedAt );

			// The effect it links is started with it, where it follows by the linked effect's own velocity.
			if ( linked >= 0 && Live[at + LinkedIsEffectorAt] == 0 )
			{
				var follows = Int16( Live, at + LinkedFollowsAt ) != 0;
				var its = spawn;

				if ( follows && linked < TemplateSlots )
				{
					var other = linked * EmitterSize;

					its = new Spawn( linked, spawn.X + (Int32( _templates, other + VelocityAt ) * 16),
						spawn.Height + (Int32( _templates, other + VelocityAt + 4 ) * 16),
						spawn.Z + (Int32( _templates, other + VelocityAt + 8 ) * 16) );
				}

				var started = Int32( _templates, from + LinkedAt ) == template ? -1 : Start( its with { Template = linked } );

				Put32( Live, at + LinkedAt, started );

				if ( started >= 0 && follows )
					Live[EmittersAt + ((started & 0xffff) * EmitterSize) + FollowsAt] = 1;
			}

			return handle;
		}

		/// <summary>
		/// <c>Particles_ScaleByDensity</c> (<c>0x00521d60</c>): the four rates, the most particles and the burst,
		/// each times the particle density over 1024, and never scaled down to nought.
		/// </summary>
		private void ScaleByDensity( int at )
		{
			var density = Int32( Live, DensityAt );

			for ( var rate = 0; rate < 4; ++rate )
			{
				var value = (sbyte)Live[at + RatesAt + rate];

				if ( value == 0 )
					continue;

				var scaled = (density * value) >> 10;

				Live[at + RatesAt + rate] = unchecked((byte)(sbyte)(scaled > 0x7f ? 0x7f : scaled == 0 ? 1 : scaled));
			}

			var most = Int16( Live, at + MostAt );

			if ( most > 0 )
			{
				var scaled = unchecked((short)((density * most) >> 10));

				Put16( Live, at + MostAt, scaled == 0 ? 1 : scaled );
			}

			var burst = Int32( Live, at + BurstAt );

			if ( burst > 0 )
			{
				var scaled = (density * burst) >> 10;

				Put32( Live, at + BurstAt, scaled == 0 ? 1 : scaled );
			}
		}
	}

	private static Emitter Read( byte[] live, int slot )
	{
		if ( slot < 0 || slot >= EmitterSlots )
			return new Emitter( slot, false, 0, -1, 0, 0, 0, 0 );

		var at = EmittersAt + (slot * EmitterSize);

		return new Emitter( slot, Int16( live, at + InUseAt ) != 0, (ushort)Int16( live, at + CountAt ), Int16( live, at + TemplateAt ),
			Int32( live, at + XAt ), Int32( live, at + HeightAt ), Int32( live, at + ZAt ), Int32( live, at + LifeAt ) );
	}

	private static List<Emitter> UsedIn( byte[] live )
	{
		var used = new List<Emitter>();

		for ( int slot = Int16( live, UsedHeadAt ), hops = 0; slot >= 0 && slot < EmitterSlots && hops < EmitterSlots; ++hops )
		{
			used.Add( Read( live, slot ) );
			slot = Int16( live, EmittersAt + (slot * EmitterSize) + NextAt );
		}

		return used;
	}

	private static bool Names( byte[] live, int handle )
	{
		var slot = handle & 0xffff;

		return handle != 0 && slot < EmitterSlots
			&& Int16( live, EmittersAt + (slot * EmitterSize) + CountAt ) == unchecked((short)(handle >> 16));
	}

	private static short Int16( byte[] bytes, int at ) => BinaryPrimitives.ReadInt16LittleEndian( bytes.AsSpan( at ) );

	private static int Int32( byte[] bytes, int at ) => BinaryPrimitives.ReadInt32LittleEndian( bytes.AsSpan( at ) );

	private static void Put16( byte[] bytes, int at, int value ) => BinaryPrimitives.WriteInt16LittleEndian( bytes.AsSpan( at ), unchecked((short)value) );

	private static void Put32( byte[] bytes, int at, int value ) => BinaryPrimitives.WriteInt32LittleEndian( bytes.AsSpan( at ), value );
}
