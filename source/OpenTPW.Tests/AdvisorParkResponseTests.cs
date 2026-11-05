using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenTPW.Tests;

/// <summary>
/// A park response through <see cref="Advisor.PlayParkResponse"/>, not through <see cref="Advisor.ParkResponseDuration"/> alone
/// (Q1029, from Q211): the response is refused while he is busy, held or cannot speak, and otherwise answers its clips' time,
/// clip 15's length and a second (Advisor_SayResponse, <c>0x00599050</c>..<c>0x005994ac</c>). The advisor is the model-less
/// one of <see cref="AdvisorLineSeamTests"/>, with the clip lengths Lost Kingdom's table holds; the speech bank is the game's.
/// </summary>
[TestClass]
public class AdvisorParkResponseTests
{
	/// <summary>Lost Kingdom's clip lengths, as <see cref="AdvisorLineTests"/> has them.</summary>
	private static readonly Dictionary<int, int> Jungle = new()
	{
		[14] = 0, [15] = 666, [16] = 28666, [17] = 633, [18] = 2966, [19] = 1300, [20] = 2000, [11] = 3333, [5] = 5000
	};

	private readonly List<Advisor> _advisors = [];

	[TestInitialize]
	public void MountTheGame()
	{
		Log ??= new();
		FileSystem = GameData.Required();
	}

	[TestCleanup]
	public void LeaveNothingBehind()
	{
		foreach ( var advisor in _advisors )
			Entity.All.Remove( advisor );

		_advisors.Clear();
		typeof( Audio ).GetProperty( nameof( Audio.Ready ) )!.SetValue( null, false );
		Audio.Duck( 1f, 0f );
	}

	private Advisor Bare()
	{
		var advisor = Advisor.WithoutModels( clip => Jungle.GetValueOrDefault( clip ) );
		_advisors.Add( advisor );
		return advisor;
	}

	private static void AudioRolling() => typeof( Audio ).GetProperty( nameof( Audio.Ready ) )!.SetValue( null, true );

	/// <summary>Row 13's clips take 8,798 ms and clip 15, the end clip, 666: the response takes both and a second.</summary>
	[TestMethod]
	public void AResponseAnswersItsClipsTheEndClipAndASecond()
	{
		AudioRolling();
		var advisor = Bare();

		var milliseconds = advisor.PlayParkResponse( response: 435, sample: 294, lip: 293, gestureRow: 13 );

		Assert.AreEqual( 8798 + 666 + 1000, milliseconds );
		Assert.AreEqual( "global/Speech/lips/sp_293.lip", advisor.LipFileOfTheLine, "the line was begun" );
	}

	/// <summary>While a line is waited on he is busy, and a second response is refused: it does not begin, and the first goes on.</summary>
	[TestMethod]
	public void AResponseWhileHeIsBusyIsRefusedAndTheFirstGoesOn()
	{
		AudioRolling();
		var advisor = Bare();

		Assert.AreNotEqual( 0, advisor.PlayParkResponse( response: 435, sample: 294, lip: 293, gestureRow: 13 ) );

		var second = advisor.PlayParkResponse( response: 447, sample: 446, gestureRow: 13 );

		Assert.AreEqual( 0, second, "refused: nothing is answered" );
		Assert.AreEqual( "global/Speech/lips/sp_293.lip", advisor.LipFileOfTheLine, "and the first line is still the one being said" );
	}

	/// <summary>Held where he is (a menu open), or with no audio to say it by, he says nothing and begins nothing.</summary>
	[TestMethod]
	public void AResponseWhileHeIsHeldOrHasNoAudioBeginsNothing()
	{
		var deaf = Bare();

		Assert.AreEqual( 0, deaf.PlayParkResponse( response: 435, sample: 294, lip: 293, gestureRow: 13 ), "no audio device in a test run" );
		Assert.IsNull( deaf.LipFileOfTheLine );

		AudioRolling();
		var held = Bare();
		held.Paused = true;

		Assert.AreEqual( 0, held.PlayParkResponse( response: 435, sample: 294, lip: 293, gestureRow: 13 ) );
		Assert.IsNull( held.LipFileOfTheLine );
	}
}
