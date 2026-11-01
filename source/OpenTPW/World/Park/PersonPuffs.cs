namespace OpenTPW;

/// <summary>
/// The puff of smoke that stands where a person is when they go (<c>FUN_004fa470</c>, <c>docs/exe/park-engine.md</c>,
/// "The three words Q822 left unread", 1): particle <see cref="Effect"/> started in the world at the person's x and z
/// and at the ground's height there.
///
/// <para>
/// <b>The place</b> is the sprite's own x and z (ours is the navigator's position; see <c>ParkPeople.Fire</c>), moved by two globals (<c>0x00785584</c>, <c>0x00785588</c>) that are
/// nought in the original, at <c>FUN_004527f0</c>'s height there (<see cref="HeightfieldFile.ScapeHeight"/>), not the
/// sprite's <c>+0x8c</c>; the three are multiplied by 1024.0 and truncated, as <c>Particles_Spawn</c> takes them. So
/// a puff stands at the person's feet, whatever a seat or a hand holds them above.
/// </para>
/// <para>
/// <b>The sites built:</b> a leaver taken off the bus stop's cell (<c>FUN_00500bd0</c>, <c>0x00500c35</c>) and a member
/// of staff dismissed (<c>FUN_00505790</c>, <c>0x005059c3</c>, before the thing is deleted); a guest made off the park's
/// counting cells (<c>FUN_004faec0</c>, <c>0x004fb26e</c>); and a guest coming off and going onto a ride that
/// teleports them (<c>FUN_00501db0</c>'s cases <c>0xf</c> and <c>0x10</c>).
/// </para>
/// </summary>
internal static class PersonPuffs
{
	/// <summary>The effect every site built here starts (<c>push 0x13</c>).</summary>
	internal const int Effect = 0x13;

	/// <summary>
	/// Where a puff for a person at <paramref name="at"/> starts on <paramref name="field"/>, in the units
	/// <c>Particles_Spawn</c> takes: x, the height, z.
	/// </summary>
	internal static (int X, int Height, int Z) PlaceOn( HeightfieldFile field, FixedVector at )
	{
		var x = at.X / (float)FixedVector.One * field.CellSizeX;
		var z = at.Y / (float)FixedVector.One * field.CellSizeY;
		var height = field.ScapeHeight( x, z );

		return ((int)(x * 1024f), (int)(height * 1024f), (int)(z * 1024f));
	}

	/// <summary>
	/// Starts the puff for a person at <paramref name="at"/> in the park's particle system, and answers its handle,
	/// or nought where there is no system, no ground or no free emitter (a start the system refuses is counted).
	/// </summary>
	internal static int Start( FixedVector at )
		=> Start( ParticleSystem.Current, Ground ?? ParkGround.Current?.Heightfield, at );

	/// <summary>The ground to stand a puff on in place of the park's, which a test sets: <see cref="ParkGround"/> is a model and needs a window.</summary>
	internal static HeightfieldFile? Ground { get; set; }

	/// <inheritdoc cref="Start(FixedVector)"/>
	internal static int Start( ParticleSystem? system, HeightfieldFile? field, FixedVector at )
	{
		if ( system is null || field is null )
			return 0;

		var (x, height, z) = PlaceOn( field, at );
		var started = system.Spawn( Effect, x, height, z );

		if ( started == 0 )
			Unimplemented.Report( "PARK_PARTICLE_NOT_STARTED" );

		Log.Info( $"Person puff: particle 0x{Effect:x} at ({x},{height},{z}) over 1024 for a person standing at "
			+ $"({at.X / (float)FixedVector.One:F3},{at.Y / (float)FixedVector.One:F3}) in cells, handle 0x{started:x}" );

		return started;
	}
}
