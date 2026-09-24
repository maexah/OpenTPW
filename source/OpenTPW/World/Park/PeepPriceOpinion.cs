namespace OpenTPW;

/// <summary>
/// What a guest at a thing's door thinks its price is worth - <c>FUN_004fde50</c>, asked once, when a guest
/// called forward arrives to board (<c>0x00500715</c>). Too expensive turns them out of the queue: see
/// <c>docs/exe/ride-operation.md</c>, "At the door".
/// </summary>
/// <remarks>
/// <b>The arithmetic is the original's, truncation for truncation.</b> Each meter is truncated to a byte,
/// each term is divided by a hundred on its own, the divisions the original makes signed stay signed and
/// the three it makes unsigned (<c>MUL</c> and <c>SHR</c>, <c>0x004fdf6d</c>, <c>0x004fdfe7</c>,
/// <c>0x004fe005</c>) are unsigned here, and the products wrap at 32 bits as its <c>IMUL</c>s do.
/// <para>
/// <b><c>RipOffOK</c> is read from the item here.</b> The original reads the copy in the item's control record
/// (<c>+0xc</c>), which a loaded park takes from its save; all 50 records in Lost Kingdom's save hold their
/// item's value, so the two agree.
/// </para>
/// <para>
/// At Lost Kingdom's prices only the cash test can refuse: the Drinks Shop is worth 42 to 128 against 30,
/// the Jungle Spray 241 to 482 against 20.
/// </para>
/// </remarks>
public static class PeepPriceOpinion
{
	/// <summary>The one kind of thing whose prize enters what it is worth - <c>+0x4ac</c> 2.</summary>
	private const int Sideshow = ParkRideOperation.SideshowUiType;

	/// <summary>The kind whose goods decide whether a price sample is pushed - <c>+0x4ac</c> 1.</summary>
	private const int Shop = 1;

	/// <summary>
	/// Whether this guest thinks the thing too expensive to board: its price above what it is worth to them,
	/// or above their cash, both compared unsigned (<c>0x004fe15f</c>, <c>0x004fe167</c>). A thing with no
	/// price is never too expensive and pushes no sample (<c>0x004fde6a</c>).
	/// </summary>
	/// <remarks>
	/// <b>The analyser's price samples are counted, not pushed.</b> The original hands one to
	/// <c>FUN_00519510</c> and <c>FUN_004c74b0</c> for a sideshow, and for a shop one per ingredient
	/// (1 to 4) and one per appearance effect (1 or 2); nothing here keeps a park analyser.
	/// </remarks>
	public static bool TooExpensive( Peep peep, int price, ParkItemCatalogue.Item item )
	{
		ArgumentNullException.ThrowIfNull( peep );

		if ( price == 0 )
			return false;

		var worth = Worth( peep, item );

		for ( var samples = SamplesPushed( item ); samples > 0; --samples )
			Unimplemented.Report( "DOOR_PRICE_ANALYSER_SAMPLE" );

		return (uint)price > worth || (uint)peep.Cash < (uint)price;
	}

	/// <summary>
	/// What a thing is worth to this guest - its cost of goods, raised by what it does for their needs, by
	/// the prize a sideshow may pay, by <c>UsageInfo.RipOffOK</c> and by how happy they are.
	/// </summary>
	public static uint Worth( Peep peep, ParkItemCatalogue.Item item )
	{
		ArgumentNullException.ThrowIfNull( peep );

		unchecked
		{
			var happy = Meter( peep.Happiness );

			var mood = item.HappinessEffect * (100 - happy) / 100
				+ item.ThirstEffect * Meter( peep.Thirst ) / 100
				+ item.HungerEffect * Meter( peep.Hunger ) / 100
				- item.VomitEffect * Meter( peep.Vomit ) / 100
				+ 100;

			var valued = (uint)(mood * (item.CostOfGoods * 115 / 100)) / 100;

			// The prize and the chance are read from the item. The original reads the object's +0x188 and +0x190,
			// built from the item at placement but saved and loaded with the object and settable in its window;
			// Lost Kingdom's save holds the items' own (the Drinks Shop 20 and 100, the Jungle Spray 50 and 25).
			var prize = item.UiType == Sideshow ? item.CostOfGoods : 0;
			var won = prize * (item.ChanceOfWinning & 0xff) / 100;

			var premium = (uint)((won + (int)valued) * (item.RipOffOK + 100)) / 100;

			return premium * (uint)(happy + 100) / 100;
		}
	}

	/// <summary>How many price samples the original pushes at this thing's door - see <see cref="TooExpensive"/>.</summary>
	public static int SamplesPushed( ParkItemCatalogue.Item item ) => item.UiType switch
	{
		Sideshow => 1,
		Shop => (item.SpecialIngredient is >= 1 and <= 4 ? 1 : 0) + (item.AppearanceEffect is 1 or 2 ? 1 : 0),
		_ => 0
	};

	/// <summary>A meter as the opinion reads it: truncated towards nought (<c>__ftol</c>), then its low byte.</summary>
	private static int Meter( float value ) => (int)value & 0xff;
}
