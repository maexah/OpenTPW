namespace OpenTPW;

/// <summary>
/// The region effects: what a thing, a member of staff or litter adds to the cells round it, and takes off again
/// as it goes (<c>FUN_004d8480</c>; <c>docs/exe/ride-operation.md</c>, "The region effects"). The eight effects
/// are the balance file's <c>RegionFX[0..7]</c>; which one a caller stamps is the executable's own choice.
/// </summary>
public sealed class ParkRegionEffects
{
	/// <summary>An entertainer's, moved with them cell by cell.</summary>
	public const int Entertainer = 0;

	/// <summary>A toilet's while it is clean.</summary>
	public const int CleanToilet = 1;

	/// <summary>A guard's, moved with them cell by cell.</summary>
	public const int Guard = 3;

	/// <summary>A thing's that provides security.</summary>
	public const int Security = 4;

	/// <summary>A toilet's while it is dirty.</summary>
	public const int DirtyToilet = 6;

	/// <summary>Fireworks'.</summary>
	public const int Fireworks = 7;

	/// <summary>One effect: the five words it adds at its own cell, in a cell's order, and how many cells it reaches each way.</summary>
	public readonly record struct Effect( short Happiness, short Illness, short Hunger, short Security, short Attraction, int Radius )
	{
		internal short Word( int index ) => index switch
		{
			0 => Happiness,
			1 => Illness,
			2 => Hunger,
			3 => Security,
			_ => Attraction
		};
	}

	/// <summary><c>data/levels/Standard.sam</c>'s eight, for a park with no balance to hand.</summary>
	public static ParkRegionEffects Standard { get; } = new(
	[
		new( 2, 0, 0, 0, 0, 3 ),
		new( 0, 1, -1, 0, 0, 3 ),
		new( -1, 1, -1, 0, 0, 1 ),
		new( 0, 0, 0, 10, 0, 3 ),
		new( 0, 0, 0, 20, 0, 5 ),
		new( -3, 3, -1, 0, 0, 3 ),
		new( 0, 2, -2, 0, 0, 3 ),
		new( 2, 0, 0, 0, 10, 6 )
	] );

	private readonly Effect[] _effects;

	public ParkRegionEffects( IReadOnlyList<Effect> effects )
	{
		_effects = [.. effects];
	}

	/// <summary>The balance's <c>RegionFX</c> rows, each key <see cref="Standard"/>'s where the balance leaves it out.</summary>
	public static ParkRegionEffects From( ParkBalance? balance )
	{
		if ( balance == null )
			return Standard;

		var effects = new Effect[Standard._effects.Length];

		for ( var i = 0; i < effects.Length; ++i )
		{
			var standard = Standard._effects[i];

			effects[i] = new Effect(
				(short)balance.Int( $"RegionFX[{i}].Happiness", standard.Happiness ),
				(short)balance.Int( $"RegionFX[{i}].Illness", standard.Illness ),
				(short)balance.Int( $"RegionFX[{i}].Hunger", standard.Hunger ),
				(short)balance.Int( $"RegionFX[{i}].Security", standard.Security ),
				(short)balance.Int( $"RegionFX[{i}].Attraction", standard.Attraction ),
				balance.Int( $"RegionFX[{i}].Radius", standard.Radius ) );
		}

		return new ParkRegionEffects( effects );
	}

	public Effect this[int effect] => _effects[effect];

	/// <summary>
	/// Adds an effect to the cells round (<paramref name="x"/>, <paramref name="y"/>), or takes it off: over the
	/// square of its radius held to the map, each word divided by the cell's distance along the grid plus one and
	/// cut towards nought, so what a stamp adds an unstamp takes off exactly.
	/// </summary>
	/// <param name="grid"><see cref="ParkWorld.EffectWords"/> words a cell in the map's order.</param>
	public void Stamp( short[] grid, int effect, int x, int y, bool off = false )
	{
		var stamped = _effects[effect];

		for ( var cellX = Math.Max( 0, x - stamped.Radius ); cellX <= Math.Min( ParkWorld.MapSize - 1, x + stamped.Radius ); ++cellX )
		{
			for ( var cellY = Math.Max( 0, y - stamped.Radius ); cellY <= Math.Min( ParkWorld.MapSize - 1, y + stamped.Radius ); ++cellY )
			{
				var distance = Math.Abs( cellX - x ) + Math.Abs( cellY - y ) + 1;
				var at = ((cellY * ParkWorld.MapSize) + cellX) * ParkWorld.EffectWords;

				for ( var word = 0; word < ParkWorld.EffectWords; ++word )
					grid[at + word] += (short)((off ? -stamped.Word( word ) : stamped.Word( word )) / distance);
			}
		}
	}

	/// <summary>
	/// The effect a member of staff carries with them by their thing model, or null: an entertainer's and a guard's,
	/// the two kinds whose constructor stamps one (<c>0x004d42f1</c>, <c>0x004d5e3f</c>).
	/// </summary>
	public static int? OfStaff( int model ) => model switch
	{
		EntertainerModel => Entertainer,
		GuardModel => Guard,
		_ => null
	};

	private const int EntertainerModel = 6;
	private const int GuardModel = 7;

	/// <summary>
	/// The effects a standing object holds on the cells round it, by its flags as the object's constructor and
	/// destructor read them (<c>FUN_004db090</c>, <c>FUN_004dd0a0</c>): a toilet's clean or dirty one, security's
	/// and fireworks'.
	/// </summary>
	public static IEnumerable<int> Of( ParkWorld.CatalogueObject thing )
	{
		if ( thing.IsToilet )
			yield return ParkState.IsDirty( thing ) ? DirtyToilet : CleanToilet;

		if ( (thing.Flags & ParkWorld.CatalogueObject.ProvidesSecurityFlag) != 0 )
			yield return Security;

		if ( (thing.Flags & ParkWorld.CatalogueObject.IsFireworksFlag) != 0 )
			yield return Fireworks;
	}
}
