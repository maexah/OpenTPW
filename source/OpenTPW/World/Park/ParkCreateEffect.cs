namespace OpenTPW;

/// <summary>
/// What building a thing gives off: its item's <c>Info.CreateParticleEffect</c>, started by the engine's script loader as a
/// bought or moved thing's script is made (<c>FUN_005587f0</c>, <c>0x00558e5d</c>..<c>0x00558fac</c>; <c>docs/exe/park.md</c>,
/// "What building a thing gives off"), over the box its model covers.
/// </summary>
internal static class ParkCreateEffect
{
	/// <summary>
	/// A thing's box as <c>FUN_00466b70</c> fills it, in world units: across and along from the cells its model covers
	/// (<c>+0xc0</c> to <c>+0xc4</c> plus one) times the cell's size, and up from its base height plus its <c>.hmp</c>'s
	/// box, low then high (<c>+0x1c</c>, <c>+0x28</c>). The destroy effect is spread over the same box (Q38).
	/// </summary>
	internal readonly record struct Box( float Left, float Low, float Top, float Right, float High, float Bottom )
	{
		/// <summary>The box over <paramref name="cells"/> (inclusive), its heights <paramref name="heights"/>' above <paramref name="baseHeight"/>.</summary>
		public static Box Of( (int Left, int Top, int Right, int Bottom) cells, float baseHeight, ItemHeightMapFile heights,
			float cellX, float cellY )
			=> new( cells.Left * cellX, baseHeight + heights.BoxMin.Y, cells.Top * cellY,
				(cells.Right + 1) * cellX, baseHeight + heights.BoxMax.Y, (cells.Bottom + 1) * cellY );
	}

	/// <summary>The ground to stand the effect on in place of the park's, which a test sets: <see cref="ParkGround"/> is a model and needs a window.</summary>
	internal static HeightfieldFile? Ground { get; set; }

	/// <summary>
	/// Starts <paramref name="item"/>'s create effect for a thing just built at <paramref name="placed"/>, and answers its
	/// handle, or nought where none was started: no effect, no particle system, no ground, no <c>.hmp</c> (the original's
	/// box needs one; every shipped item has it), or a start the system refuses (counted).
	/// </summary>
	internal static int Start( ParkItemCatalogue.Item item, ParkWorld.CatalogueObject placed )
	{
		if ( item.CreateParticleEffect == 0 || ParticleSystem.Current is not { } system
			|| (Ground ?? ParkGround.Current?.Heightfield) is not { } field || ParkItemHeights.For( item ) is not { } heights )
			return 0;

		var baseHeight = ParkItemHeights.BaseOf( item, placed, field ).Under;
		var box = Box.Of( ParkObjects.SoundBoxOf( item, placed ), baseHeight, heights, field.CellSizeX, field.CellSizeY );

		return Start( system, item.CreateParticleEffect, box, baseHeight );
	}

	/// <summary>
	/// <c>Particles_Spawn</c> at the middle of <paramref name="box"/> across and along, at <paramref name="baseHeight"/>, each
	/// times 1024 (<c>0x00558eab</c>..<c>0x00558f25</c>); then <c>Particles_SetArea</c> with the box's width times 512, its
	/// height times 256 and its depth times 512 (<c>0x00558f3d</c>..<c>0x00558fac</c>): half the box across and along and a
	/// quarter of its height, in place of the template's area.
	/// </summary>
	internal static int Start( ParticleSystem system, int effect, Box box, float baseHeight )
	{
		var x = (int)((box.Left + box.Right) * 0.5f * 1024f);
		var y = (int)(baseHeight * 1024f);
		var z = (int)((box.Top + box.Bottom) * 0.5f * 1024f);

		var handle = system.Spawn( effect, x, y, z );

		if ( handle == 0 )
		{
			Unimplemented.Report( "PARK_PARTICLE_NOT_STARTED" );
			return 0;
		}

		var (areaX, areaY, areaZ) = ((int)((box.Right - box.Left) * 512f), (int)((box.High - box.Low) * 256f),
			(int)((box.Bottom - box.Top) * 512f));

		system.SetArea( handle, areaX, areaY, areaZ );

		Log.Info( $"Particles: create effect {effect} started at ({x},{y},{z}) over 1024 for the box " +
			$"({box.Left:F1},{box.Low:F2},{box.Top:F1})..({box.Right:F1},{box.High:F2},{box.Bottom:F1}), area ({areaX},{areaY},{areaZ}) over 16, handle 0x{handle:x}" );

		return handle;
	}
}
