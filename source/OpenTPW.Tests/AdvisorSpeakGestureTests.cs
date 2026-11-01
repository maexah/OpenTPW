using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// Which clips <c>Advisor.Speak</c> plays for a line (<see cref="Advisor.PlanClips"/>, the choice it makes before it
/// touches the audio or the figure): a line given a gesture row plays that row's clips and flags and never rolls the
/// random chain; a line given none, or a row that adds up to nought, rolls it.
/// </summary>
[TestClass]
public class AdvisorSpeakGestureTests
{
	private static readonly Dictionary<int, int> Jungle = new()
	{
		[14] = 0, [15] = 666, [16] = 28666, [17] = 633, [18] = 2966, [19] = 1300, [20] = 2000, [11] = 3333, [5] = 5000, [12] = 5000,
		[1] = 1666, [6] = 7500, [4] = 4166, [9] = 10833, [2] = 2000, [3] = 3000, [7] = 4000, [8] = 4500, [10] = 6000
	};

	private static int Length( int clip ) => Jungle.GetValueOrDefault( clip );

	private static int NoRoll( int _ ) => throw new AssertFailedException( "the random chain was rolled for a line that has a row" );

	/// <summary>A line given row 5 plays 14, 5, 11, 15 with the row's length, and the dice are never thrown.</summary>
	[TestMethod]
	public void ALineWithARowPlaysTheRowsClipsNotTheRandomChain()
	{
		var plan = Advisor.PlanClips( 5, sampleMilliseconds: 20000, Length, NoRoll );

		CollectionAssert.AreEqual( new[] { 14, 5, 11, 15 }, plan.Clips );
		CollectionAssert.AreEqual( new[] { 0, 0, 0, 0 }, plan.Flags );
		Assert.AreEqual( 5000 + 3333 + 666, plan.Milliseconds );
		Assert.IsTrue( plan.Row!.Value.LeadIn );
	}

	/// <summary>Row 13's clips carry their flags through, and its cue and missing lead-in come with it.</summary>
	[TestMethod]
	public void ARowsFlagsAndCueComeThrough()
	{
		var plan = Advisor.PlanClips( 13, sampleMilliseconds: 20000, Length, NoRoll );

		CollectionAssert.AreEqual( new[] { 17, 17, 17, 17, 18, 19, 20 }, plan.Clips );
		CollectionAssert.AreEqual( new[] { 0x20000, 0x20000, 0x20000, 0x20000, 0x40000, 0x90000, 0x110000 }, plan.Flags );
		Assert.AreEqual( (7798, false), (plan.Row!.Value.CueAt, plan.Row!.Value.LeadIn) );
	}

	/// <summary>No row, and a row of nought length (row 2), both roll the random chain: 14 first, 15 last.</summary>
	[TestMethod]
	public void NoRowOrAnEmptyRowRollsTheRandomChain()
	{
		foreach ( var row in new[] { -1, 2 } )
		{
			var rolls = 0;
			var plan = Advisor.PlanClips( row, sampleMilliseconds: 1000, Length, _ => { rolls++; return 0; } );

			Assert.IsTrue( rolls > 0, $"row {row} should have rolled" );
			Assert.AreEqual( 14, plan.Clips.First() );
			Assert.AreEqual( 15, plan.Clips.Last() );
			Assert.IsTrue( plan.Flags.All( f => f == 0 ) );
		}
	}
}
