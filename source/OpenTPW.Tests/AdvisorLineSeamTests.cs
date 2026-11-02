using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;

namespace OpenTPW.Tests;

/// <summary>
/// The advisor's wiring between a line and the rest: the glints' channel emptied where the original empties it, the lip
/// file a line is given, the nodes a clip's start shows and hides, and the lip a park response is handed. Each is a unit
/// test of a seam: an <see cref="Advisor"/> built without its model or audio (<see cref="Advisor.WithoutModels"/>) that the
/// line is given to directly (<see cref="Advisor.BeginLine"/>) and clocked by hand (<see cref="Advisor.Step"/>). None needs
/// the game.
/// </summary>
[TestClass]
public class AdvisorLineSeamTests
{
	private readonly List<Advisor> _advisors = [];

	[TestInitialize]
	public void MakeTheLog() => Log ??= new();

	[TestCleanup]
	public void LeaveNothingBehind()
	{
		foreach ( var advisor in _advisors )
			Entity.All.Remove( advisor );

		_advisors.Clear();
		Audio.Duck( 1f, 0f );
	}

	private Advisor Bare()
	{
		var advisor = Advisor.WithoutModels();
		_advisors.Add( advisor );
		return advisor;
	}

	private static void StandGlints( Advisor advisor )
		=> advisor.Glints.SetCount( 2, (int)ParLib.P_EFFECT_GoldenTicket2, UI.Anchor.Right, down: UI.VerticalAnchor.Bottom );

	/// <summary>
	/// <b>The glints' channel is emptied the update he is no longer busy, and only then</b> (Advisor_Update, 0x0059993e): a
	/// line waiting on its sample keeps what the clips set, the next update after it ends does not.
	/// </summary>
	/// <remarks><b>Mutations:</b> the call in <c>Step</c> removed; the call made while he is still busy.</remarks>
	[TestMethod]
	public void TheGlintsAreEmptiedWhenTheLineIsOverAndNotBefore()
	{
		var advisor = Bare();
		advisor.BeginLine( 5, cue: null, sampleMilliseconds: 1000 );

		StandGlints( advisor );
		advisor.Step( 0.016f );
		Assert.AreEqual( 2, advisor.Glints.Count, "the sample is still waited on, so he is busy and the glints stand" );

		var idle = Bare();
		StandGlints( idle );
		idle.Step( 0.016f );
		Assert.AreEqual( 0, idle.Glints.Count, "nothing to say: the update empties the channel" );
	}

	/// <summary>
	/// <b>A line empties the glints as it begins</b> (Advisor_SayResponse, 0x005993b3): what the line before it left standing
	/// goes, whether or not that line ran to its end.
	/// </summary>
	/// <remarks><b>Mutation:</b> the call in <c>BeginLine</c> removed.</remarks>
	[TestMethod]
	public void ABeginningLineEmptiesTheGlints()
	{
		var advisor = Bare();
		StandGlints( advisor );

		advisor.BeginLine( 5, cue: null, sampleMilliseconds: 1000 );

		Assert.AreEqual( 0, advisor.Glints.Count );
	}

	/// <summary>
	/// <b>Cutting him off and quietening him each empty the glints</b> (Advisor_StopSpeaking, 0x00599543; Advisor_StopQuietly,
	/// 0x0059972a), with nothing sounding and nothing playing.
	/// </summary>
	/// <remarks><b>Mutation:</b> the call in <c>Dismiss</c> removed.</remarks>
	[TestMethod]
	public void CuttingHimOffAndQuietingHimEmptyTheGlints()
	{
		var advisor = Bare();

		StandGlints( advisor );
		advisor.StopSpeaking();
		Assert.AreEqual( 0, advisor.Glints.Count, "cut off" );

		StandGlints( advisor );
		advisor.StopQuietly();
		Assert.AreEqual( 0, advisor.Glints.Count, "quietened" );

		StandGlints( advisor );
		advisor.Hush();
		Assert.AreEqual( 0, advisor.Glints.Count, "hushed" );
	}

	/// <summary>
	/// <b>A line moves his mouth by its response's lip file where the response names one, and by the sample's otherwise</b>: the
	/// sample 294 said as response 435 is given lip 293; a sample that names none is given its own, three digits wide.
	/// </summary>
	/// <remarks><b>Mutations:</b> the sample's own always; the lip dropped on the way from <c>BeginLine</c>.</remarks>
	[TestMethod]
	public void ALineIsGivenTheLipFileItsResponseNames()
	{
		var advisor = Bare();

		advisor.BeginLine( 294, cue: null, sampleMilliseconds: 1000, lip: 293 );
		Assert.AreEqual( "global/Speech/lips/sp_293.lip", advisor.LipFileOfTheLine );

		advisor.BeginLine( 88, cue: null, sampleMilliseconds: 1000 );
		Assert.AreEqual( "global/Speech/lips/sp_088.lip", advisor.LipFileOfTheLine );

		Assert.AreEqual( "global/Speech/lips/sp_640.lip", Advisor.LipPath( 640, 0 ) );
	}

	/// <summary>
	/// <b>The sweep hands the speaker the lip a response names, not the sample's own</b> (<see cref="ParkPeople.SpeakResponse"/>):
	/// response 435 said with sample 294 is handed lip 293, and 447 with 446 is handed 446.
	/// </summary>
	/// <remarks><b>Mutation:</b> the sample handed where the lip goes.</remarks>
	[TestMethod]
	public void TheSweepHandsTheSpeakerTheLipTheResponseNames()
	{
		var handed = new List<(int Sample, int Lip)>();

		ParkPeople.SpeakResponse( ( response, sample, first, second, slot, lip, row ) =>
		{
			handed.Add( (sample, lip) );
			return 0;
		}, 435, 294 );

		Assert.AreEqual( (294, 293), handed[0], "a row names another lip" );
		Assert.AreNotEqual( handed[0].Sample, handed[0].Lip );
	}

	/// <summary>
	/// <b>A clip's start shows the nodes the clip before hid, then the nodes its tracks name, and last hides its own hide list</b>
	/// (<see cref="AdvisorModel.Turnover"/>), so a node in both its tracks and its hide list ends hidden.
	/// </summary>
	/// <remarks><b>Mutations:</b> the hide list applied before the tracks' nodes are shown; the outgoing clip's nodes not shown.</remarks>
	[TestMethod]
	public void AClipsStartShowsThenHidesInOrder()
	{
		var calls = new List<string>();
		var visible = new Dictionary<int, bool>();

		AdvisorModel.Turnover( outgoingHidden: [3, 4], incomingTouched: [5, 4], incomingHidden: [4, 6], ( node, show ) =>
		{
			calls.Add( $"{node}{(show ? "+" : "-")}" );
			visible[node] = show;
		} );

		CollectionAssert.AreEqual( new[] { "3+", "4+", "5+", "4+", "4-", "6-" }, calls );
		Assert.IsTrue( visible[3] && visible[5], "shown by the start" );
		Assert.IsFalse( visible[4] || visible[6], "hidden by the incoming clip's own list, last" );
	}
}
