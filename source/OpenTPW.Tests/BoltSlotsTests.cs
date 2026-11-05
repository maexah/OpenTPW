using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenTPW.Tests;

/// <summary>
/// The bolts alive at once: sixteen slots, each counting down its own life and cut when it reaches nought
/// (FUN_00580320, FUN_0057ee30, FUN_0057ff60; <c>docs/exe/weather.md</c>, "The bolt").
/// </summary>
[TestClass]
public class BoltSlotsTests
{
	private static readonly Vector3 Ground = new( 100f, 100f, 0f );
	private static readonly Vector3 Top = new( 100f, 100f, 300f );

	/// <summary>
	/// <b>Sixteen bolts fit and a seventeenth is dropped</b>, as the original drops a draw made with all its slots taken
	/// (0x0058032c).
	/// </summary>
	/// <remarks><b>Mutations:</b> a capacity of 8 or 17; the seventeenth replacing the first; the seventeenth replacing the last.</remarks>
	[TestMethod]
	public void SixteenSlotsAndTheSeventeenthIsDropped()
	{
		var slots = new BoltSlots();

		for ( var i = 0; i < 16; ++i )
			Assert.IsTrue( slots.Add( Ground, Top + new Vector3( i, 0f, 0f ), 2f ), $"bolt {i}" );

		Assert.IsFalse( slots.Add( Ground, Top + new Vector3( 99f, 0f, 0f ), 2f ) );
		Assert.AreEqual( 16, slots.Count );
		Assert.AreEqual( 0f, slots[0].Top.X - 100f, "the first is still the first" );
		Assert.AreEqual( 15f, slots[15].Top.X - 100f, "the last is still the sixteenth" );
	}

	/// <summary>
	/// <b>A bolt is up for exactly its lifetime:</b> 2 s as four 0.5 s frames leaves it with 1500, 1000, 500 ms and then
	/// gone (the cut is at a life left of nought, 0x0057ee81, not at a fade's end), and nothing it was struck with
	/// changes its grey.
	/// </summary>
	/// <remarks><b>Mutations:</b> the cut at a life left under 0.1 or one frame early; the life left kept as a fraction of the lifetime.</remarks>
	[TestMethod]
	public void ABoltIsCutAtNoughtNotFaded()
	{
		var slots = new BoltSlots();
		slots.Add( Ground, Top, 2f );

		foreach ( var left in new[] { 1500f, 1000f, 500f } )
		{
			slots.Advance( 0.5f );

			Assert.AreEqual( 1, slots.Count );
			Assert.AreEqual( left, slots[0].LifeMs, 0.01f );
		}

		slots.Advance( 0.4375f );

		Assert.AreEqual( 1, slots.Count, "62.5 ms left is still up" );
		Assert.AreEqual( 62.5f, slots[0].LifeMs, 0.01f );

		slots.Advance( 0.0625f );

		Assert.AreEqual( 0, slots.Count, "a life left of exactly nought is cut, and not drawn that frame" );
	}

	/// <summary>
	/// <b>A frame ages a bolt by at most 500 ms</b> (0x0057ff83): a three-second stall costs a two-second bolt 500, not all of it.
	/// </summary>
	/// <remarks><b>Mutations:</b> no clamp; a clamp of 100 or 1000.</remarks>
	[TestMethod]
	public void AFrameAgesABoltByHalfASecondAtMost()
	{
		var slots = new BoltSlots();
		slots.Add( Ground, Top, 2f );

		slots.Advance( 3f );

		Assert.AreEqual( 1500f, slots[0].LifeMs, 0.01f );
	}

	/// <summary>
	/// <b>A second strike runs beside the first, each on its own life</b>, where the old pool replaced the running one: a
	/// 1 s bolt, 0.6 s on a 2 s one, 0.5 s on, leaves the second with 900 ms and the first cut.
	/// </summary>
	/// <remarks><b>Mutations:</b> Add replacing the running bolt; every bolt taking the newest's life; the cut taking the wrong record.</remarks>
	[TestMethod]
	public void StrikesOverlapEachOnItsOwnLife()
	{
		var slots = new BoltSlots();
		slots.Add( Ground, Top, 1f );
		slots.Advance( 0.5f );
		slots.Advance( 0.1f );
		slots.Add( Ground + new Vector3( 50f, 0f, 0f ), Top, 2f );

		Assert.AreEqual( 2, slots.Count );
		Assert.AreEqual( 400f, slots[0].LifeMs, 0.01f );
		Assert.AreEqual( 2000f, slots[1].LifeMs, 0.01f );

		slots.Advance( 0.5f );

		Assert.AreEqual( 1, slots.Count, "the first ran out" );
		Assert.AreEqual( 1500f, slots[0].LifeMs, 0.01f, "and the second is left in its slot" );
		Assert.AreEqual( 150f, slots[0].Ground.X, "it is the second one" );
	}

	/// <summary>
	/// <b>A slot's grey is neutral, not tinted, and a held frame keeps it:</b> a bolt drawn with a rand of 0 is the envelope's
	/// grey for its life in all three channels, halved by a fade of 0.5; drawn again held it keeps that grey whatever the next
	/// rand is, and drawn again running it takes the new one.
	/// </summary>
	/// <remarks><b>Mutations:</b> the old tint (0.85, 0.88, 1.0); the fade ignored; a draw that always redraws; one that never redraws.</remarks>
	[TestMethod]
	public void ASlotsGreyIsNeutralAndAHeldFrameKeepsIt()
	{
		var slots = new BoltSlots();
		slots.Add( Ground, Top, 2f );
		slots.Advance( 0.0625f );

		// 1937.5 ms left: 437 into a tooth, a base of 254 and a rand of 0, so 254 in all three channels.
		Assert.AreEqual( 1937.5f, slots[0].LifeMs, 0.01f );

		var colour = slots.Draw( 0, 0, redraw: true, fade: 1f );

		Assert.AreEqual( 254f / 255f, colour.X, 1e-5f );
		Assert.AreEqual( colour.X, colour.Y, 0f, "green is red's" );
		Assert.AreEqual( colour.X, colour.Z, 0f, "blue is red's: no tint" );
		Assert.AreEqual( colour.X * 0.5f, slots.Draw( 0, 0, redraw: false, fade: 0.5f ).X, 1e-5f, "the fade scales it" );

		Assert.AreEqual( colour, slots.Draw( 0, 63, redraw: false, fade: 1f ), "held: the next rand is not taken" );
		Assert.AreNotEqual( colour, slots.Draw( 0, 63, redraw: true, fade: 1f ), "running: it is" );
	}

	/// <summary>A bolt struck with no lifetime draws nothing and takes no slot.</summary>
	/// <remarks><b>Mutations:</b> a lifetime of nought taking a slot.</remarks>
	[TestMethod]
	public void ANoughtLifetimeTakesNoSlot()
	{
		var slots = new BoltSlots();

		Assert.IsFalse( slots.Add( Ground, Top, 0f ) );
		Assert.IsFalse( slots.Add( Ground, Top, -1f ) );
		Assert.AreEqual( 0, slots.Count );
	}
}
