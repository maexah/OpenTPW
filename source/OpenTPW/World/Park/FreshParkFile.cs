using System.Buffers.Binary;
using System.Text;

namespace OpenTPW;

/// <summary>
/// A park made fresh as the original writes it at tick nought: the inflated body of its <c>restart.INTS</c>, module by
/// module, from <see cref="FreshPark"/> and the theme's data. <c>Level.WritePark</c> reads it as the file the park was
/// loaded from (<see cref="ParkWorld"/>) and the patching writers bring it up to the running park, so a thing bought, a
/// person made and a cell changed take the paths a loaded park's do. <c>docs/exe/saves.md</c>, "A park made fresh: its
/// file at tick nought"; FileFormats <c>saves.md</c>, "A park made fresh".
/// </summary>
/// <remarks>
/// <b>Deviations</b>, said where each is written: the words of a module that the original leaves as whatever memory held
/// (the weather's location, the lab past its used projects, the advisor's records' unused bytes) are nought, the value the
/// clean runs hold.
/// </remarks>
public static class FreshParkFile
{
	/// <summary>The bytes before the compressed block: the version, then the legal text of a file read from <paramref name="shipped"/>.</summary>
	/// <param name="shipped">The shipped park's first <c>0x60D</c> bytes, whose text after the version is every file's (FileFormats <c>saves.md</c>, "A park made fresh").</param>
	public static byte[] Preamble( byte[] shipped )
	{
		var preamble = (byte[])shipped.Clone();
		BinaryPrimitives.WriteInt32LittleEndian( preamble, 500 );

		return preamble;
	}

	/// <summary>The inflated body of the fresh park's file, each module followed by its tag as the game stores it.</summary>
	public static byte[] Body( FreshPark park, ParkItemCatalogue catalogue, ParkBalance balance, BaseFileSystem files )
	{
		var body = new Sink( 1_600_000 );

		// What the original holds that is not built here (docs/exe/saves.md, "A park made fresh: its file at tick nought"): the
		// session's music voice, which is nought here (Sound); and, off Lost Kingdom, the flyers' four values, the land's own
		// voices and its emitter.
		Unimplemented.Report( "FRESH_PARK_FILE_MUSIC_VOICE" );

		if ( !park.Theme.Equals( "jungle", StringComparison.OrdinalIgnoreCase ) )
			Unimplemented.Report( "FRESH_PARK_FILE_OTHER_THEME" );

		ActionRecording( body );
		World( body, park, catalogue, balance, files );
		body.Tag( "DLRW" );
		Sprites( body );
		body.Tag( "CSPS" );
		Particles( body, park, catalogue, files );
		body.Tag( "TRAP" );
		MessageCentre( body );
		body.Tag( "SSEM" );
		Clock( body );
		body.Tag( "KOLC" );
		body.Int32( 0 );
		body.Tag( "TNAV" );
		GameSystem( body );
		body.Tag( "SYSG" );
		RideSystem( body, park );
		body.Tag( "SYSR" );
		TrackRides( body );
		body.Tag( "KART" );
		Flyers( body );
		body.Tag( "RYLF" );
		RideScripts( body );
		body.Tag( "ESSR" );
		Camera( body );
		body.Tag( "EMAK" );
		Coasters( body );
		body.Tag( "SAOC" );
		Advisor( body, park, balance );
		body.Tag( "SVDA" );
		Sound( body, park );
		body.Tag( "NUOS" );
		body.Zeros( 2 );
		body.Tag( "STHC" );
		body.Int32( 0 );
		body.Tag( "CSDA" );
		body.Int32( 0 );

		return body.ToArray();
	}

	/// <summary>The action recording: not a loaded published park, and 135 bytes of the recorder's fresh buffer (not decoded).</summary>
	private static void ActionRecording( Sink body )
	{
		body.Int32( 0 );
		body.Int32( 135 );
		body.Zeros( 135 );
		body.Poke8( body.Length - 135, 0xe7 );
		body.Poke8( body.Length - 135 + 130, 0x19 );
	}

	private static void World( Sink body, FreshPark park, ParkItemCatalogue catalogue, ParkBalance balance, BaseFileSystem files )
	{
		// The header, 64 bytes (FileFormats saves.md, "The world block"): the twelve things' handles are those of
		// "A fresh Full Simulation world" (docs/exe/park-engine.md).
		body.Int32( 2 );
		body.UInt16( 0 );
		body.UInt16( 0 );
		body.UInt16( 0 );
		body.UInt16( ThingOf( park, 16 ) );
		body.UInt16( 0 );
		body.Int32( park.GameTick );
		body.UInt16( ThingOf( park, 10 ) );
		body.UInt16( ThingOf( park, 13 ) );
		body.Int32( park.ParkClosed );
		body.Int32( park.NumberOfVisitorsToDate );
		body.UInt16( park.ParkGates );
		body.UInt16( park.TrafficLights );
		body.Int32( park.RandomSeed );
		body.UInt16( ThingOf( park, 14 ) );
		body.UInt16( ThingOf( park, 9 ) );
		body.UInt16( ThingOf( park, 12 ) );
		body.UInt16( ThingOf( park, 17 ) );
		body.UInt16( ThingOf( park, 15 ) );
		body.Int32( park.WorldState );
		for ( var head = 0; head < 5; ++head )
			body.UInt16( 0 );
		// The object list is empty until the two objects are made; the writer puts its head in.
		body.UInt16( 0 );

		ObjectControls( body, park, catalogue );
		StaffPoolAndClock( body, park );
		Cells( body, park );
		Things( body, park, balance, files );
	}

	/// <summary>The id of the one thing of a model among the twelve.</summary>
	private static int ThingOf( FreshPark park, int model ) => park.Things.First( thing => thing.Model == model ).ThingId;

	private static void ObjectControls( Sink body, FreshPark park, ParkItemCatalogue catalogue )
	{
		var start = body.Length;

		foreach ( var control in park.ObjectControlRecords )
		{
			var at = body.Length;
			body.Zeros( 32 );
			body.Poke16( at, control.ItemId );
			body.Poke32( at + 4, control.Price );
			// The item's FatigueEffect and RipOffOK, which the control manager copies from the item as it makes the record
			// (ControlManager_FillFromItems, 0x004d3e00; docs/exe/saves.md, "The rest of a park made fresh's file").
			var known = catalogue.TryGet( control.ItemId, out var item );
			body.Poke32( at + 8, known ? item.FatigueEffect : 0 );
			body.Poke32( at + 0xc, known ? item.RipOffOK : 0 );
			body.Poke8( at + 0x10, control.Researched ? 1 : 0 );
			body.Poke32( at + 0x14, control.TierResearched );
			body.Poke32( at + 0x18, control.Standing );
			body.Poke32( at + 0x1c, (int)control.FirstBuilt );
		}

		body.Zeros( 150 * 32 - (body.Length - start) );
		body.Int32( park.ObjectControlRecords.Count );

		// mPreviousSearchKey: the loader zeroes it after reading it.
		body.UInt16( 0 );
	}

	private static void StaffPoolAndClock( Sink body, FreshPark park )
	{
		// The 32 records of the pool, all empty: the pool's own writer fills them (ParkStaffPool.Written).
		body.Zeros( 32 * 20 );

		// The five kinds' counts and stop flags (nought, nought) and the pool's mark.
		body.Zeros( 5 * 5 + 4 );

		// mOpeningStaffPoolGenerated.
		body.Byte( 1 );

		// The park clock: its start, the session's FILETIME (the computer's local time as the clock is made), the month
		// and day last updated (a clock before its first update: 0 and -1, which are not the date of tick 0) and the rate.
		body.Int64( ParkWorld.StaffState.FileTimeOf( GameCalendar.Epoch ) );
		body.Int64( ParkWorld.StaffState.FileTimeOf( DateTime.Now ) );
		body.Int32( 0 );
		body.Int32( -1 );
		body.Int32( 15000 );

		// The arrival timer: rate, mark, the capacity a park starts with, the load in progress, offloading, gates open.
		body.Int32( park.Arrival.ArrivalRate );
		body.Int32( park.Arrival.TimeSig );
		body.Int32( park.Arrival.TargetVehicleCapacity );
		body.Int32( park.Arrival.PeopleOnBus );
		body.Byte( park.Arrival.Offloading ? 1 : 0 );
		body.Byte( park.Arrival.GatesOpen ? 1 : 0 );
	}

	private static void Cells( Sink body, FreshPark park )
	{
		// The boundary's anchors by cell: the piece's slot plus one is the model slot it takes in the ride system
		// module, the third slot on, in the walk's order.
		var anchors = new Dictionary<int, (int Handle, ParkBoundaryHoarding.Anchor Anchor)>();

		for ( var at = 0; at < park.Boundary.Count; ++at )
		{
			var anchor = park.Boundary[at];
			anchors[anchor.Y * ParkWorld.MapSize + anchor.X] = (FirstBoundarySlot + at + 1, anchor);
		}

		var start = body.Length;
		body.Zeros( ParkWorld.MapSize * ParkWorld.MapSize * ParkWorld.FreshCellSize );

		for ( var index = 0; index < park.Cells.Count; ++index )
		{
			var cell = park.Cells[index];
			var extras = new ParkWorld.TrackExtras( 0, 0 );

			// Cell 0's chain is the managers' alone; the two objects head it as they are made.
			if ( index == 0 )
				cell = cell with { Occupant = (ushort)(park.FirstObject - 2) };

			if ( anchors.TryGetValue( index, out var found ) )
			{
				extras = new( found.Handle, found.Anchor.Mask );
			}

			body.Cell( start + index * ParkWorld.FreshCellSize, cell, extras );
		}
	}

	/// <summary>The model slot of the boundary's first piece: the gates' and the lights' take the first two.</summary>
	private const int FirstBoundarySlot = 2;

	private static void Things( Sink body, FreshPark park, ParkBalance balance, BaseFileSystem files )
	{
		// The head of the list, then the managers from the newest to the oldest, as the original's list stands once the two
		// objects are made; the objects (the gates, ids 11, and the lights, 12) are written as a thing bought is
		// (ParkFileWriter).
		var managers = park.Things.Where( thing => thing.ThingId <= park.FirstObject - 2 ).OrderByDescending( thing => thing.ThingId ).ToList();
		body.Int32( managers[0].ThingId );

		foreach ( var thing in managers )
		{
			var at = body.Length;
			body.Zeros( ManagerSize( thing.Model ) );

			// The list head: the next thing's id, or nought at the end.
			body.Poke32( at, thing.ThingId - 1 );
			body.Poke32( at + 4, thing.Model );
			body.Poke16( at + 8, 128 );
			body.Poke16( at + 10, 128 );
			body.Poke16( at + 12, thing.ThingId - 1 );

			// The newest manager has nobody above it until the objects are made.
			body.Poke16( at + 14, thing.ThingId == managers[0].ThingId ? 0 : thing.ThingId + 1 );

			if ( thing.Model == 11 )
				AdvisorThing( body, at );

			if ( thing.Model == 13 )
				Analyser( body, at );

			if ( thing.Model == 19 )
				ChallengeManager( body, at, balance, files );
		}
	}

	/// <summary>
	/// The park analyser as its constructors leave it (FileFormats <c>saves.md</c>, "The park analyser"): the grid of 1,024 blocks
	/// with nine kept answers each, four small rings, six kept answers, sixteen rings of months, the stays' ring, the loads'
	/// ring, twenty rings of opinions and the last 76 bytes with the six stamps at the calendar's start.
	/// </summary>
	private static void Analyser( Sink body, int at )
	{
		// Four ints and a count of cells to a block.
		body.Poke32( at + 16, 4 );
		body.Poke32( at + 20, 32 );
		body.Poke32( at + 24, 32 );
		body.Poke32( at + 28, 128 );
		body.Poke32( at + 32, 128 );

		at += 36;

		for ( var block = 0; block < 1024; ++block )
		{
			body.Poke32( at, block % 32 * 4 );
			body.Poke32( at + 4, block / 32 * 4 );
			body.Poke32( at + 8, 4 );
			body.Poke32( at + 12, 4 );

			// Nine answers never worked out: -999 and 0, the first two as floats.
			for ( var answer = 0; answer < 9; ++answer )
				body.Poke32( at + 16 + answer * 5, answer < 2 ? BitConverter.SingleToInt32Bits( -999f ) : -999 );

			at += 61;
		}

		// Four small rings: current entry -1, three entries, nothing pushed.
		for ( var ring = 0; ring < 4; ++ring )
		{
			body.Poke32( at, -1 );
			body.Poke32( at + 4, 3 );
			at += 17;
		}

		// Six kept answers.
		for ( var answer = 0; answer < 6; ++answer )
		{
			body.Poke32( at, -999 );
			at += 5;
		}

		// Sixteen rings of 144 dwords, the stays' ring of 50 and the loads' ring of 5.
		for ( var ring = 0; ring < 16; ++ring )
			at = DwordRing( body, at, 144 );

		at = DwordRing( body, at, 50 );
		at = DwordRing( body, at, 5 );

		// Twenty rings of 50 bytes.
		for ( var ring = 0; ring < 20; ++ring )
		{
			body.Poke32( at, -1 );
			body.Poke32( at + 4, 50 );
			at += 60;
		}

		// mMisbehavingKids and the two after it, mLifetimeVisitors, then the six stamps at the calendar's start.
		at += 16;

		for ( var stamp = 0; stamp < 6; ++stamp )
		{
			body.Poke64( at, ParkWorld.StaffState.FileTimeOf( GameCalendar.Epoch ) );
			at += 8;
		}
	}

	/// <summary>
	/// The advisor's record as his constructor leaves it (FileFormats <c>saves.md</c>, "The advisor"): the first of the eight slots
	/// taken by message 0 at a score of 100000 with the line to take next in turn, and every one of the 351 messages never
	/// said, the line -1. <b>Written differently from the original, said here and not counted:</b> slot 0's <c>mValid</c> is written 0 by
	/// <c>ParkWorld.PutAdvisorSlots</c> where the original's file holds 1, because this game posts no message 0 (Q543), so the original loading
	/// a file from here has no welcome in its slot list (docs/exe/saves.md, "A park made fresh").
	/// </summary>
	private static void AdvisorThing( Sink body, int at )
	{
		body.Poke32( at + 24 + 10, 100000 );
		body.Poke8( at + 24 + 14, 1 );
		body.Poke32( at + 24 + 15, -1 );

		for ( var message = 0; message < 351; ++message )
			body.Poke32( at + 204 + 16 * message + 4, -1 );
	}

	/// <summary>
	/// The challenge manager (FileFormats <c>saves.md</c>, "The challenge manager"): the theme's <c>ChallengesInThisLevel</c>
	/// in list order, each the entry of <c>Challenges.sam</c> it indexes, then no challenge current and the system off.
	/// </summary>
	private static void ChallengeManager( Sink body, int at, ParkBalance balance, BaseFileSystem files )
	{
		using var stream = files.OpenRead( "Challenges.sam" );
		var challenges = new SettingsFile( stream );

		int Value( int entry, string field ) => int.TryParse( challenges[$"Challenges[{entry}].{field}"], out var value ) ? value : 0;

		var slot = 0;

		for ( var at20 = 0; at20 < 20; ++at20 )
		{
			var entry = balance.Int( $"ChallengesInThisLevel[{at20}].ChallengeType" );

			if ( entry < 1 )
				continue;

			var to = at + 16 + slot * 45;
			body.Poke32( to, Value( entry, "Type" ) );
			body.Poke32( to + 4, Value( entry, "TargetTime" ) );
			body.Poke32( to + 8, Value( entry, "TargetVal" ) );
			body.Poke16( to + 12, Value( entry, "TargetObj" ) );
			body.Poke16( to + 14, Value( entry, "TargetObj2" ) );
			body.Poke32( to + 16, Value( entry, "TargetStaffType" ) );
			body.Poke32( to + 20, Value( entry, "Prize" ) );
			body.Poke32( to + 24, Value( entry, "FollowupType" ) );
			body.Poke8( to + 28, Value( entry, "CheckAtEndOnly" ) );
			body.Poke8( to + 42, Value( entry, "Independent" ) );
			++slot;
		}

		// mCurrentChallenge.
		body.Poke32( at + 916, -1 );
	}

	private static int DwordRing( Sink body, int at, int entries )
	{
		body.Poke32( at, -1 );
		body.Poke32( at + 4, entries );

		return at + 4 + 4 + 1 + 4 + entries * 4;
	}

	/// <summary>A singleton manager's record, by model (FileFormats <c>saves.md</c>, "Thing records").</summary>
	private static int ManagerSize( int model ) => model switch
	{
		9 => 103,
		10 => 18,
		11 => 5846,
		12 => 16,
		13 => 73544,
		14 => 4190,
		15 => 99,
		16 => 300,
		17 => 16,
		19 => 937,
		_ => throw new ArgumentOutOfRangeException( nameof( model ), $"model {model} is no manager" )
	};

	/// <summary>The jungle's own emitter, the land's WaterFall (effect 20), in the particle image's place units (FileFormats <c>saves.md</c>, "A park made fresh").</summary>
	private const int WaterFall = 20;

	private static readonly (int X, int Height, int Z) WaterFallAt = (34068, 312, 33668);

	/// <summary>The jungle's generator seed in the particles' live header; the other themes' is 1 (FileFormats <c>saves.md</c>).</summary>
	private const int JungleParticleSeed = 0x18be873a;

	/// <summary>
	/// The particle module: the live system with the land's one emitter started (<see cref="ParkParticles.Edit.Start"/>), then
	/// the effect library's records with an item's own <c>.emt</c> files in the first unnamed slots, and no particles.
	/// </summary>
	private static void Particles( Sink body, FreshPark park, ParkItemCatalogue catalogue, BaseFileSystem files )
	{
		var library = ParticleSystem.Current?.Library;
		var density = ParticleSystem.Current?.Density ?? 1000;

		if ( library == null )
		{
			library = new ParticleLibraryFile( "Particle/Tp2.plb" );
			catalogue.RegisterParticleEffects( library );
		}

		var jungle = park.Theme.Equals( "jungle", StringComparison.OrdinalIgnoreCase );
		var at = body.Length;

		body.Ascii( "LCTP" );
		body.Int32( 1 );
		body.Int32( ParkParticles.LiveSize );

		var live = body.Length;
		body.Zeros( ParkParticles.LiveSize );

		// The live header: the particle count, the generator's seed, the next emitter's count and the next effector's, the
		// density, and two words (the live system's, a data address 0x803b68 and a function's 0x5d60c0, which a load keeps its own over).
		body.Poke32( live + 0x0c, 2048 );
		body.Poke32( live + 0x10, jungle ? JungleParticleSeed : 1 );
		body.Poke32( live + 0x14, 1 );
		body.Poke16( live + 0x18, 1 );
		body.Poke16( live + 0x1a, 1 );
		body.Poke32( live + 0x1c, 0x803b68 );
		body.Poke32( live + 0x20, density );
		body.Poke32( live + 0x24, 0x5d60c0 );

		// Every emitter and effector on its free chain, head first.
		for ( var slot = 0; slot < ParkParticles.EmitterSlots; ++slot )
		{
			var emitter = live + 0x2c + slot * ParkParticles.EmitterSize;
			body.Poke16( emitter + 0xd0, slot + 1 < ParkParticles.EmitterSlots ? slot + 1 : 0xffff );
			body.Poke16( emitter + 0xd2, slot - 1 );
		}

		const int Effectors = 20;
		var effectors = live + 0x2c + ParkParticles.EmitterSlots * ParkParticles.EmitterSize;

		for ( var slot = 0; slot < Effectors; ++slot )
		{
			body.Poke16( effectors + slot * 0x68 + 0x60, slot + 1 < Effectors ? slot + 1 : 0xffff );
			body.Poke16( effectors + slot * 0x68 + 0x62, slot - 1 );
		}

		// The chains' heads: no emitter and no effector in use, then the first of each free, and no free particle.
		body.Poke16( live + 0x9e4c, 0xffff );
		body.Poke16( live + 0x9e4e, 0xffff );

		body.Int32( ParkParticles.TemplatesSize );

		foreach ( var record in library.EffectRecords )
			body.Bytes( record );

		foreach ( var record in library.EffectorRecords )
			body.Bytes( record );

		body.Int32( 0 );

		if ( !jungle )
			return;

		// The land's WaterFall, started on the image as a script's effect is.
		var module = body.ToArray();
		var particles = new ParkParticles( module, at );
		var edit = particles.Begin();
		edit.Start( new ParkParticles.Spawn( WaterFall, WaterFallAt.X << 4, WaterFallAt.Height << 4, WaterFallAt.Z << 4 ) );
		particles.Put( module, edit );
		body.Overwrite( live, module.AsSpan( live, ParkParticles.LiveSize ) );

		// The generator as the original's start of this emitter left it; its draws here are not the original's.
		body.Poke32( live + 0x10, JungleParticleSeed );
	}

	/// <summary>
	/// The ride system: the first two slots empty, where the gates' and the lights' models are made as any bought thing's, then the
	/// boundary's 107 pieces in the order the boundary was laid (FileFormats <c>saves.md</c>, "A boundary piece's record").
	/// </summary>
	private static void RideSystem( Sink body, FreshPark park )
	{
		var at = body.Length;
		body.Int32( 0 );
		body.Int32( park.Boundary.Count );
		body.Int32( FirstBoundarySlot );
		body.Int32( 0 );

		for ( var slot = 0; slot < FirstBoundarySlot; ++slot )
			body.Byte( 0 );

		foreach ( var anchor in park.Boundary )
		{
			var piece = ParkBoundaryHoarding.PieceOf( anchor );
			body.Bytes( ParkThingStates.BoundaryPieceRecord( ParkBoundaryHoarding.FirstItem + piece.Row, piece.CellX, piece.CellY, piece.Angle ) );
		}

		// The length counts what follows its own dword.
		body.Poke32( at, body.Length - at - 4 );
	}

	/// <summary>
	/// The ride scripts: the subsystem set up, no script (the gates' and the lights' are made as any bought thing's, which counts
	/// them into the header's count), the next handle 1, and the size of a script's struct.
	/// </summary>
	private static void RideScripts( Sink body )
	{
		body.Ascii( "RSSE" );
		body.Int32( 20 );
		body.Int32( 1 );
		body.Int32( 0 );
		body.Int32( 1 );
		body.Int32( 0 );
		body.Int32( 0 );

		// Five dwords the loader reads and discards.
		body.Ascii( "PAD_PAD_PAD_PAD_PAD_" );
		body.Int32( 0 );
		body.Int32( 244 );
	}

	/// <summary>
	/// The advisor: <c>ADV_</c>, 360 bytes of <c>Advisor_ResetState</c> (<c>0x005989c0</c>: zeroed, <c>+0x110</c> 5 and
	/// <c>+0x12c</c> -1, the nodes and clip lengths of his models, the costume) and the length of a buffer, nought
	/// (<c>docs/exe/saves.md</c>, the <c>ADVS</c> row).
	/// </summary>
	private static void Advisor( Sink body, FreshPark park, ParkBalance balance )
	{
		body.Ascii( "ADV_" );
		body.Int32( 0x168 );

		var at = body.Length;
		body.Zeros( 0x168 );
		body.Poke32( at + 0x110, 5 );
		body.Poke32( at + 0x12c, -1 );

		// The models' words: slot 0 is the lobby's fly, slot 1 the level's own. A load copies these bytes over the original's
		// advisor, which runs its lines' lengths and mouths from them.
		var theme = park.Theme.ToLowerInvariant();
		var slots = new[]
		{
			AdvisorModelWords( "global/advisor/Advisor.md2" ),
			AdvisorModelWords( $"levels/{theme}/advisor/Advisor_{theme[..Math.Min( theme.Length, 3 )].ToUpperInvariant()}.md2" )
		};

		for ( var slot = 0; slot < slots.Length; ++slot )
		{
			body.Poke32( at + 0x38 + slot * 4, slots[slot].Head );

			for ( var mouth = 0; mouth < slots[slot].Mouths.Length; ++mouth )
				body.Poke32( at + 0x40 + slot * 20 + mouth * 4, slots[slot].Mouths[mouth] );
		}

		// The twenty clip lengths are one model's, slot 1's where there is one, and clip 14's (entry 13) is nought.
		var lengths = slots[1].Found ? slots[1].Clips : slots[0].Clips;

		for ( var clip = 0; clip < lengths.Length; ++clip )
			body.Poke32( at + 0x68 + clip * 4, clip == 13 ? 0 : lengths[clip] );

		// The theme's costume, four pairs copied from the balance (FUN_004080e0): the add-ons from +0x144, the sub-offs from +0x154.
		var costume = AdvisorProps.CostumeOf( key => balance.Int( key ) );

		for ( var pair = 0; pair < costume.Length; ++pair )
		{
			body.Poke32( at + 0x144 + pair * 4, costume[pair].AddOn );
			body.Poke32( at + 0x154 + pair * 4, costume[pair].SubOff );
		}

		body.Int32( 0 );
	}

	/// <summary>
	/// What <c>Advisor_ResetState</c> takes from one advisor model: the node named <c>bug head</c> and the five mouths (nought where
	/// there is no such node, <c>FUN_0044b2e0</c>), and the twenty clips' lengths (<see cref="AdvisorModel.MillisecondsOf"/>, nought
	/// where the clip is not shipped).
	/// </summary>
	internal static (bool Found, int Head, int[] Mouths, int[] Clips) AdvisorModelWords( string path )
	{
		var mouths = new int[5];
		var clips = new int[20];

		try
		{
			var model = new ModelFile( path );
			var head = NodeNamed( model, "bug head" );
			string[] names = ["mouth - normal", "mouth - aah", "mouth - eee", "mouth - ooh", "mouth - sss"];

			for ( var mouth = 0; mouth < mouths.Length; ++mouth )
				mouths[mouth] = NodeNamed( model, names[mouth] );

			var stem = path[..path.LastIndexOf( '.' )];

			for ( var clip = 1; clip <= clips.Length; ++clip )
			{
				if ( AnimationFile.TryLoad( $"{stem}M{clip}.md2", out var animation ) )
					clips[clip - 1] = AdvisorModel.MillisecondsOf( animation! );
			}

			return (true, head, mouths, clips);
		}
		catch ( Exception e ) // a missing file reads as no model, as the original's lookup does
		{
			Log.Warning( $"FreshParkFile: advisor model {path} would not read - {e.Message}" );
			return (false, 0, mouths, clips);
		}
	}

	/// <summary>A node's index by name, ignoring case; nought where there is none, as the engine answers.</summary>
	private static int NodeNamed( ModelFile model, string name )
	{
		for ( var node = 0; node < model.Nodes.Count; ++node )
		{
			if ( string.Equals( model.Nodes[node].Name.TrimEnd( '\0' ).Trim(), name, StringComparison.OrdinalIgnoreCase ) )
				return node;
		}

		return 0;
	}

	/// <summary>
	/// The sound module (FileFormats <c>saves.md</c>, "The advisor, sound and advisor scoring modules"): the sound manager's state
	/// with its volumes, and its voice list.
	/// </summary>
	private static void Sound( Sink body, FreshPark park )
	{
		body.Int32( 1 );
		body.Int32( 1024 );
		body.Int32( 0x54 );

		var state = body.Length;
		body.Zeros( 0x54 );

		// The music's voice handle is nought, a deviation from the original's (its session's own voice, FRESH_PARK_FILE_MUSIC_VOICE),
		// and so is the crowd's (no voice is in the list); the four volumes, a word that is 1 in
		// every file, and the three on-flags.
		body.Poke32( state + 0x08, 1 );
		body.Poke32( state + 0x18, 100 );
		body.Poke32( state + 0x1c, 75 );
		body.Poke32( state + 0x20, 60 );
		body.Poke32( state + 0x24, 75 );
		body.Poke32( state + 0x28, 1 );
		body.Poke32( state + 0x38, 1 );
		body.Poke32( state + 0x3c, 1 );
		body.Poke32( state + 0x40, 1 );

		// Three floats the loader reads and drops.
		body.Single( 0f );
		body.Single( 0f );
		body.Single( -50f );

		// The voice list: its length (which counts itself), the count of voices, then the category record.
		var list = body.Length;
		body.Int32( 0 );
		body.Int32( 0 );

		var theme = park.Theme.ToLowerInvariant();
		string[] categories =
		[
			"", @"\data\global\sound\cat_ambient", @"\data\global\sound\cat_rides", @"\data\global\sound\cat_ui",
			@"\data\global\sound\cat_kids", @"\data\global\sound\cat_staff", @"\data\global\speech\cat_speech",
			$@"\data\levels\{theme}\sound\cat_ambient", $@"\data\levels\{theme}\sound\cat_rides",
			$@"\data\levels\{theme}\speech\cat_speech", $@"\data\levels\{theme}\music\cat_music",
			"", "", "", "", ""
		];

		var record = body.Length;
		body.Int32( 0 );
		body.Int32( categories.Length );
		body.Zeros( categories.Length * 4 );

		for ( var slot = 0; slot < categories.Length; ++slot )
		{
			var name = categories[slot];

			// The first slot is named by a nought; the unused ones have no name at all.
			if ( name.Length == 0 )
			{
				if ( slot == 0 )
					body.Byte( 1 );

				body.Byte( 0 );
				continue;
			}

			body.Byte( name.Length + 1 );
			body.Ascii( name );
			body.Byte( 0 );
		}

		// The category record's length counts what follows its own dword and the slot count's.
		body.Poke32( record, body.Length - record - 8 );

		// The voice pool's blocks of 100 slots.
		body.Int32( 1 );

		// The land's own voices, as the original puts them back on entering a park (measured; FileFormats saves.md, "What the
		// shipped park holds"). Only Lost Kingdom's are known: another theme's file holds none here, a deviation.
		var voices = theme == "jungle" ? LostKingdomVoices : [];

		foreach ( var voice in voices )
		{
			body.Int32( voice.Handle );
			body.Int32( voice.Type );
			body.Int32( VoiceMark );
			body.Single( voice.X );
			body.Single( 0f );
			body.Single( voice.Z );
			body.Single( 0f );
			body.Single( voice.Range );
			body.Int32( voice.Category );
			body.Int32( voice.Effect );
			body.Int32( voice.Variation );
			body.Int32( voice.Sample );
			body.Int32( VoiceMark );

			// The controllers of a voice that follows the listener in a rectangle: four key bytes and four value bytes
			// (nought here), then the rectangle's z1, x1, x2 and z2.
			body.Int32( voice.Rectangle != null ? 1 : 0 );

			if ( voice.Rectangle is { } corners )
			{
				body.Zeros( 8 );
				body.Int32( corners.Z1 );
				body.Int32( corners.X1 );
				body.Int32( corners.X2 );
				body.Int32( corners.Z2 );
			}

			body.Int32( VoiceMark );
			body.Byte( voice.Rectangle != null ? 1 : 0 );
			body.Int32( voice.Handle );
			body.Int32( 0 );
		}

		body.Poke32( list + 4, voices.Count );
		body.Poke32( list, body.Length - list );
	}

	private const int VoiceMark = 0x0012d591;

	private readonly record struct LandVoice( int Handle, int Type, float X, float Z, float Range, int Category, int Effect,
		int Variation, int Sample, (int X1, int Z1, int X2, int Z2)? Rectangle = null );

	/// <summary>Lost Kingdom's five: the global ambient's two plain voices that follow the listener in a rectangle, the level's ambient, and two one-shots.</summary>
	private static readonly List<LandVoice> LostKingdomVoices =
	[
		new( 0x360061, 5, 353f, 31f, 0f, 1, 4, 0, 1, (353, 31, 450, -66) ),
		new( 0x370060, 5, 372f, 21f, 0f, 1, 6, 1, 0, (372, 21, 567, -174) ),
		new( 0x38005e, 5, 21f, 39f, 19f, 7, 4, 0, 0 ),
		new( 0x390063, 3, 530f, 513f, 97f, 1, 5, 0, 0 ),
		new( 0x3a005f, 3, 511f, 664f, 97f, 1, 3, 0, 0 )
	];

	/// <summary>The sprite table: its magic, a record's size, 100 slots and none live.</summary>
	private static void Sprites( Sink body )
	{
		body.Tag( "TPCS" );
		body.Int32( 0x118 );
		body.Int32( 100 );
		body.Zeros( 100 * 4 );
	}

	/// <summary>
	/// The message centre: for each of the 29 message types the things that hear it, ascending (FileFormats <c>saves.md</c>,
	/// "The message centre module"). The sets of the twelve things, as their constructors register them.
	/// </summary>
	private static readonly int[][] Listeners =
	[
		[], [7], [7], [], [], [3], [], [2, 4, 9], [3, 9], [], [9], [10, 11, 12], [1, 4, 5, 8], [8], [1, 3], [9], [], [3, 5, 6, 10],
		[9], [3, 4, 5, 8, 9], [4], [], [], [3], [4], [3, 4], [10], [9], [9]
	];

	private static void MessageCentre( Sink body )
	{
		body.Int32( Listeners.Length );

		foreach ( var set in Listeners )
		{
			body.Int32( set.Length );

			foreach ( var thing in set )
				body.UInt16( thing );
		}
	}

	/// <summary>The clock module: the game clock's reading and the second stopwatch's (nought at the start; the clock's writer fills them).</summary>
	private static void Clock( Sink body )
	{
		body.Int32( 0 );
		body.Int32( 0 );
	}

	/// <summary>The game system: nine dwords (the clock's writer fills the readings).</summary>
	private static void GameSystem( Sink body )
	{
		body.Zeros( 36 );
		body.Poke32( body.Length - 36 + 20, 2 );
	}

	/// <summary>The track rides: the stamp alone (<c>15:25:02</c>, <c>Mar 17 2000</c>, the 2.0 build's).</summary>
	private static void TrackRides( Sink body )
	{
		body.Int32( 1 );
		body.Int32( 12 );
		body.Int32( 44 );
		body.Int32( 2 );
		body.Int32( 0x20 );
		body.Int32( 0x20 );
		body.Ascii( "15:25:02" );
		body.Ascii( "Mar 17 2000" );
		body.Byte( 0 );
	}

	/// <summary>The flying rides: set up with no flyer, the four values of Lost Kingdom.</summary>
	private static void Flyers( Sink body )
	{
		body.Ascii( "FLY_" );
		body.Int32( 1 );
		body.Int32( 400 );
		body.Zeros( 400 );
		body.Int32( 190000 );
		body.Int32( 70000 );
		body.Int32( 140000 );
		body.Int32( 30000 );
		body.Ascii( "PAD_PAD_PAD_PAD_PAD_" );
		body.Int32( 4652 );
	}

	/// <summary>The camera: zoom 110, rotation 0, flags 0, the point (475, 0, 175), nothing saved (the camera's writer fills these).</summary>
	private static void Camera( Sink body )
	{
		body.Single( 110f );
		body.Int32( 0 );
		body.Int32( 0 );
		body.Single( 475f );
		body.Single( 0f );
		body.Single( 175f );
		body.Zeros( 16 );
	}

	/// <summary>The coasters: none (<c>0 0 0 1</c>).</summary>
	private static void Coasters( Sink body )
	{
		body.Zeros( 12 );
		body.Int32( 1 );
	}

	/// <summary>An append-only buffer with the poke and put helpers the body is written with.</summary>
	private sealed class Sink( int capacity )
	{
		private byte[] _bytes = new byte[capacity];

		public int Length { get; private set; }

		public byte[] ToArray() => _bytes[..Length];

		private void Reserve( int count )
		{
			if ( Length + count > _bytes.Length )
				Array.Resize( ref _bytes, Math.Max( _bytes.Length * 2, Length + count ) );
		}

		public void Zeros( int count )
		{
			Reserve( count );
			Length += count;
		}

		public void Byte( int value )
		{
			Reserve( 1 );
			_bytes[Length++] = (byte)value;
		}

		public void UInt16( int value )
		{
			Reserve( 2 );
			BinaryPrimitives.WriteUInt16LittleEndian( _bytes.AsSpan( Length ), (ushort)value );
			Length += 2;
		}

		public void Int32( int value )
		{
			Reserve( 4 );
			BinaryPrimitives.WriteInt32LittleEndian( _bytes.AsSpan( Length ), value );
			Length += 4;
		}

		public void Int64( long value )
		{
			Reserve( 8 );
			BinaryPrimitives.WriteInt64LittleEndian( _bytes.AsSpan( Length ), value );
			Length += 8;
		}

		public void Single( float value ) => Int32( BitConverter.SingleToInt32Bits( value ) );

		public void Ascii( string text )
		{
			Reserve( text.Length );
			Encoding.ASCII.GetBytes( text, _bytes.AsSpan( Length ) );
			Length += text.Length;
		}

		public void Tag( string tag ) => Ascii( tag );

		public void Bytes( ReadOnlySpan<byte> bytes )
		{
			Reserve( bytes.Length );
			bytes.CopyTo( _bytes.AsSpan( Length ) );
			Length += bytes.Length;
		}

		public void Overwrite( int at, ReadOnlySpan<byte> bytes ) => bytes.CopyTo( _bytes.AsSpan( at ) );

		public void Poke8( int at, int value ) => _bytes[at] = (byte)value;

		public void Poke16( int at, int value ) => BinaryPrimitives.WriteUInt16LittleEndian( _bytes.AsSpan( at ), (ushort)value );

		public void Poke64( int at, long value ) => BinaryPrimitives.WriteInt64LittleEndian( _bytes.AsSpan( at ), value );

		public void Poke32( int at, int value ) => BinaryPrimitives.WriteInt32LittleEndian( _bytes.AsSpan( at ), value );

		public void Cell( int at, ParkWorld.MapCell cell, ParkWorld.TrackExtras track )
			=> ParkWorld.PutFreshCell( _bytes, at, cell, track );
	}
}
