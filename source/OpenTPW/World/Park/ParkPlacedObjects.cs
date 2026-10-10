namespace OpenTPW;

/// <summary>
/// What a park's <c>scape.omp</c> places in the land as the level loads (<c>FUN_00550e00</c>, called once a load at
/// <c>0x00407f82</c>; <c>docs/exe/park.md</c>, "The land's own particles"): the Lost Kingdom's waterfall's spray is
/// the one particle effect any shipped park places.
///
/// <para>
/// <b>Engine and content.</b> Which effect and where is the file's (<see cref="PlacedObjectsFile"/>); this starts
/// each in the park's particle system, where <see cref="WorldParticles"/> draws it. Nothing keeps the handle, as
/// the loader keeps none: the effect runs until the system ends with its park.
/// </para>
/// <para>
/// A type 1 record is a sound with a range of its own (<c>0x005510ba</c>; <c>docs/exe/park.md</c>, "The land's own
/// sounds"): the Lost Kingdom's waterfall and its river. One in the global <c>cat_ambient</c> is handed to the
/// park's sound to start; one in the level's own category, which is not loaded, and every type 3 are counted.
/// </para>
/// </summary>
internal static class ParkPlacedObjects
{
	/// <summary>The file in a level's folder (<c>0x00747ee8</c>).</summary>
	internal static string PathFor( string theme ) => $"levels/{theme.ToLowerInvariant()}/scape.omp";

	/// <summary>
	/// Starts what <paramref name="theme"/>'s file places, and answers how many particle effects it started. A level
	/// with no such file places nothing.
	/// </summary>
	internal static int Start( string theme, ParticleSystem? system )
	{
		var path = PathFor( theme );

		if ( !FileSystem.FileExists( path ) )
			return 0;

		using var stream = FileSystem.OpenRead( path );

		return Start( new PlacedObjectsFile( stream ), system );
	}

	/// <summary>
	/// Starts each type 2 record's effect at its place, <c>Particles_Spawn( word 1, word 3, word 4, word 5 )</c>
	/// (<c>0x00551006</c>), and answers how many started. A start the system refuses is counted.
	/// </summary>
	internal static int Start( PlacedObjectsFile file, ParticleSystem? system )
	{
		var started = 0;

		foreach ( var record in file.Records )
		{
			switch ( record.Type )
			{
				case PlacedObjectsFile.ParticleType:
					if ( system is null )
						break;

					var (x, height, z) = record.Place;

					if ( system.Spawn( record.Effect, x, height, z ) == 0 )
					{
						Unimplemented.Report( "PARK_PARTICLE_NOT_STARTED" );
						break;
					}

					++started;
					Log.Info( $"Particles: the land's own effect {record.Effect} started at ({x >> 10},{height >> 10},{z >> 10})" );
					break;

				case 3:
					Unimplemented.Report( "SCAPE_OMP_SOUND_TYPE_3" );
					break;
			}
		}

		return started;
	}

	/// <summary>One type 1 record's sound: its effect in the global <c>cat_ambient</c>, where it stands and how far it is heard.</summary>
	internal readonly record struct Sound( int Effect, Vector3 Place, float Range );

	/// <summary>
	/// Starts the sounds <paramref name="theme"/>'s file places, through <paramref name="start"/>, and answers how
	/// many it took. A level with no such file places none.
	/// </summary>
	internal static int StartSounds( string theme, Func<Sound, bool> start )
	{
		var path = PathFor( theme );

		if ( !FileSystem.FileExists( path ) )
			return 0;

		using var stream = FileSystem.OpenRead( path );

		return StartSounds( new PlacedObjectsFile( stream ), start );
	}

	/// <summary>
	/// Hands each type 1 record of the global category to <paramref name="start"/>
	/// (<c>FUN_0051c130( 0, category, word 2, place, range )</c>) and answers how many it took. One it refuses, and
	/// one in the level's own category, is counted.
	/// </summary>
	internal static int StartSounds( PlacedObjectsFile file, Func<Sound, bool> start )
	{
		var started = 0;

		foreach ( var record in file.Records )
		{
			if ( record.Type != PlacedObjectsFile.RangedSoundType )
				continue;

			var (x, height, z) = record.SoundPlace;

			// The original's height is this world's third axis.
			if ( record.Category == 0 && start( new Sound( record.Sound, new Vector3( x, z, height ), record.Range ) ) )
				++started;
			else
				Unimplemented.Report( "SCAPE_OMP_SOUND_TYPE_1" );
		}

		return started;
	}
}
