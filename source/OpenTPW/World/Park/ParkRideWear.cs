namespace OpenTPW;

/// <summary>
/// What a turn of running takes off a ride - <c>FUN_004df450</c>, the sum <c>FUN_004df670</c> lowers the State of
/// repair and the remaining life by (<c>docs/exe/ride-operation.md</c>, "A ride's wear").
/// </summary>
public static class ParkRideWear
{
	/// <summary>One tier of an item's <c>Upgrades</c>: its wear rate and its two red lines.</summary>
	public readonly record struct Tier( int Rate, int RedLineSpeed, int RedLineCapacity );

	/// <summary>The clock's bits that are nought on a wearing turn - every 64th sweep (<c>0x004df719</c>).</summary>
	public const int EveryMask = 0x3f;

	/// <summary>The clock's bits that are nought on a turn that wears and asks for a breakdown (<c>0x004e0c2c</c>).</summary>
	public const int TurnMask = 7;

	/// <summary>The share of a wear the remaining life loses - 0.02 (<c>0x00700548</c>).</summary>
	public const float LifeShare = 0.02f;

	/// <summary>What a breakdown asked for takes off the remaining life - 5 (<c>0x007005c8</c>).</summary>
	public const float BreakdownCostsLife = 5f;

	/// <summary>The State of repair's byte below which a ride's script is told it is worn - 20 (<c>0x0070059c</c>).</summary>
	public const int WornBelow = 20;

	/// <summary>The State of repair's byte below which the advisor is told, on the turn it falls there - 10 (<c>0x007005a0</c>).</summary>
	public const int AdvisorBelow = 10;

	private const float Scale = 0.9f, Floor = 0.1f;

	/// <summary>
	/// The wear of one turn, the speed being the script's word (<c>FUN_0055a330</c>, <c>0x004df758</c>; <see cref="RideScript.Speed"/>). The speed over the item's most and the riders, held to the item's most, over it are
	/// each scaled to 0.1..1; one at or past its red line is moved half way to 1 (the capacity's red line is held
	/// against a tenth of the riders, as the listing has it, <c>0x004df5c2</c>); their sum times the tier's rate,
	/// halved. An item whose most is nought divides by it as the original's floats do.
	/// </summary>
	public static float Of( int speed, int riders, int maxSpeed, int maxCapacity, Tier tier )
	{
		riders = riders < 0 ? 0 : Math.Min( riders, maxCapacity );

		// Each ratio is stored as a float before its floor is added, and each term once more after its red line.
		var bySpeed = (float)((float)speed / (double)maxSpeed * Scale) + (double)Floor;

		if ( tier.RedLineSpeed <= speed )
			bySpeed = (bySpeed + 1.0) * 0.5;

		var byRiders = (float)(riders / (double)maxCapacity * Scale) + (double)Floor;

		if ( tier.RedLineCapacity <= riders * (double)Floor )
			byRiders = (byRiders + 1.0) * 0.5;

		return (float)(((double)(float)byRiders + (float)bySpeed) * tier.Rate * 0.5);
	}

	/// <summary>A value less a loss, held to 0..100 as the original's two compares hold it: not a number is nought.</summary>
	public static float Lowered( float value, double loss )
	{
		var now = -loss + value;

		return now > 100.0 ? 100f : now >= 0.0 ? (float)now : 0f;
	}
}
