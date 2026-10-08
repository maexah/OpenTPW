using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;

namespace OpenTPW.Tests;

/// <summary>
/// The staff's sounds (<c>docs/exe/ride-operation.md</c>, "Drawn on the way"): an idle, walking or researching
/// turn opens with a draw and plays its kind's <c>cat_staff</c> effect on one in sixteen.
/// </summary>
[TestClass]
public class ParkStaffSoundTests
{
	private BaseFileSystem data = null!;

	[TestInitialize]
	public void MountTheGame() => FileSystem = data = GameData.Required();

	private ParkWorld Park()
	{
		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		return new ParkWorld( new SaveReader( stream ).ReadFile() );
	}

	private static ParkBalance Balance() => new( "jungle", easyMode: true );

	private const int Handyman = 25;
	private const int Mechanic = 26;
	private const int Entertainer = 27;
	private const int Guard = 28;
	private const int Researcher = 30;

	/// <summary>Answers its values in order, the last for ever, and counts how often it was asked.</summary>
	private sealed class Draws( params int[] values ) : Random
	{
		public int Asked { get; private set; }

		public override int Next() => values[Math.Min( Asked++, values.Length - 1 )];
	}

	/// <summary>One member on the path at (48,22), every edge shut so no walk is found, in the state and stamp given.</summary>
	private (Staff Member, PeepWalk Walk, ParkState State) Standing( int thing, StaffActivity activity, int stamp = 0 )
	{
		var world = Park();
		var state = new ParkState( world );
		var saved = world.People.Single( person => person.ThingId == thing );
		var member = new Staff( saved.ThingId, saved.Model, saved.Staff!.Value with
		{
			State = (int)activity, TimeStartedIdling = stamp,
			PatrolBottomLeft = MapStep.CellId( 0, 0 ), PatrolTopRight = MapStep.CellId( 127, 127 )
		}, saved.Navigator );

		member.Happiness = 50f;
		member.Tiredness = 80f;
		member.Navigator.Position = new FixedVector( PeepNavigator.WaypointCentre( 48 ), PeepNavigator.WaypointCentre( 22 ) );
		member.Navigator.Target = member.Navigator.Position;

		return (member, new PeepWalk( member.Navigator, ( _, _, _ ) => true ), state);
	}

	private static (StaffBehaviour Behaviour, List<(int Thing, int Effect)> Sounded) Listening( Random random, ParkState state )
	{
		var sounded = new List<(int Thing, int Effect)>();

		return (new StaffBehaviour( Balance(), random, state )
		{
			Sound = ( member, effect ) => sounded.Add( (member.ThingId, effect) )
		}, sounded);
	}

	/// <summary>
	/// Each kind's idle and walking effect, on a first draw whose low four bits are nought and on no other
	/// (<c>0x004d74f2</c>, <c>0x004d73e6</c> the handyman's; the other four kinds' the same).
	/// </summary>
	[DataTestMethod]
	[DataRow( Handyman, StaffActivity.Idle, 0xa1 )]
	[DataRow( Handyman, StaffActivity.Walking, 0xa0 )]
	[DataRow( Mechanic, StaffActivity.Idle, 0xa3 )]
	[DataRow( Mechanic, StaffActivity.Walking, 0xa2 )]
	[DataRow( Entertainer, StaffActivity.Idle, 0xa5 )]
	[DataRow( Entertainer, StaffActivity.Walking, 0xa4 )]
	[DataRow( Guard, StaffActivity.Idle, 0xa7 )]
	[DataRow( Guard, StaffActivity.Walking, 0xa6 )]
	[DataRow( Researcher, StaffActivity.Idle, 0xa9 )]
	[DataRow( Researcher, StaffActivity.Walking, 0xa8 )]
	public void AnIdleOrWalkingTurnSoundsItsKindsEffectOnOneDrawInSixteen( int thing, StaffActivity activity, int effect )
	{
		foreach ( var draw in new[] { 0, 16, 0x7ffffff0, 1, 8, 15, 17, 0x7fffffff } )
		{
			// The idle stamp is the clock, so an idle member's spell has not run out: the turn is the draw alone.
			var (member, walk, state) = Standing( thing, activity, stamp: 1000 );
			var random = new Draws( draw, 1 );
			var (behaviour, sounded) = Listening( random, state );

			behaviour.Step( member, walk, playing: null, tick: 1000 );

			if ( (draw & 0xf) == 0 )
				CollectionAssert.AreEqual( new[] { (thing, effect) }, sounded, $"draw {draw}" );
			else
				Assert.AreEqual( 0, sounded.Count, $"draw {draw}" );

			Assert.IsTrue( random.Asked >= 1, "the draw is taken whether or not anything plays" );
			Assert.AreEqual( (1, sounded.Count), member.Sounds, $"draw {draw}" );
		}
	}

	/// <summary>The draw is the turn's first: a later draw of nought sounds nothing, and an idle spell not run out draws all the same.</summary>
	[TestMethod]
	public void TheSoundsDrawIsTheTurnsFirstAndIsTakenByAnIdleMemberWhoDoesNotDecide()
	{
		var (member, walk, state) = Standing( Researcher, StaffActivity.Walking );
		var random = new Draws( 1, 0 );
		var (behaviour, sounded) = Listening( random, state );

		behaviour.Step( member, walk, playing: null, tick: 1000 );

		Assert.IsTrue( random.Asked >= 2, "the walk's end went on to the choice's draw" );
		Assert.AreEqual( 0, sounded.Count, "the choice's draw of nought is not the sound's" );

		var (idle, idleWalk, idleState) = Standing( Guard, StaffActivity.Idle, stamp: 1000 );
		var idleRandom = new Draws( 0 );
		var (idleBehaviour, idleSounded) = Listening( idleRandom, idleState );

		idleBehaviour.Step( idle, idleWalk, playing: null, tick: 1001 );

		Assert.AreEqual( StaffActivity.Idle, idle.Activity );
		Assert.AreEqual( 1000, idle.TimeStartedIdling, "the spell has not run out and nothing was decided" );
		Assert.AreEqual( 1, idleRandom.Asked );
		CollectionAssert.AreEqual( new[] { (Guard, 0xa7) }, idleSounded );
	}

	/// <summary>
	/// A researching turn that does not end draws for effect <c>0x8a</c> (<c>0x00502a61</c>); the turn that ends
	/// takes no such draw.
	/// </summary>
	[TestMethod]
	public void AResearchingTurnThatDoesNotEndDrawsForItsOwnEffect()
	{
		var (member, walk, state) = Standing( Researcher, StaffActivity.Researching );

		member.TimeStartedResearching = 1000;

		var work = new StaffBehaviour( Balance() ).ResearcherWorkDurationAt( member.PayGrade );
		var random = new Draws( 16 );
		var (behaviour, sounded) = Listening( random, state );

		behaviour.Step( member, walk, playing: null, tick: 1000 + work );

		Assert.AreEqual( 1, random.Asked );
		CollectionAssert.AreEqual( new[] { (Researcher, StaffBehaviour.ResearchingSound) }, sounded );
		Assert.AreEqual( 0x8a, StaffBehaviour.ResearchingSound );

		var quiet = new Draws( 5 );
		var (quietBehaviour, quietSounded) = Listening( quiet, state );

		quietBehaviour.Step( member, walk, playing: null, tick: 1000 + work );

		Assert.AreEqual( 1, quiet.Asked );
		Assert.AreEqual( 0, quietSounded.Count );

		// Past the stamp and the duration the spell ends: with every edge shut nowhere is found, the stamp is
		// written again, and no draw for a sound is taken.
		var ending = new Draws( 0 );
		var (endingBehaviour, endingSounded) = Listening( ending, state );

		endingBehaviour.Step( member, walk, playing: null, tick: 1001 + work );

		Assert.AreEqual( 1001 + work, member.TimeStartedResearching );
		Assert.AreEqual( 0, endingSounded.Count, "the ending turn sounds nothing" );
	}

	/// <summary>The other states take no draw for a sound and play none.</summary>
	[DataTestMethod]
	[DataRow( Handyman, StaffActivity.Resting )]
	[DataRow( Handyman, StaffActivity.Cleaning )]
	[DataRow( Entertainer, StaffActivity.Performing )]
	[DataRow( Guard, StaffActivity.Waiting )]
	[DataRow( Guard, StaffActivity.Held )]
	[DataRow( Guard, StaffActivity.OnStrike )]
	public void NoOtherStateDrawsForASound( int thing, StaffActivity activity )
	{
		var (member, walk, state) = Standing( thing, activity, stamp: 1000 );

		member.TimeStartedCleaning = member.TimeStartedEntertaining = 1000;

		var random = new Draws( 0 );
		var (behaviour, sounded) = Listening( random, state );

		behaviour.Step( member, walk, playing: null, tick: 1001 );

		Assert.AreEqual( 0, random.Asked );
		Assert.AreEqual( 0, sounded.Count );
		Assert.AreEqual( (0, 0), member.Sounds );
	}

	/// <summary>A kind with no effect of its own sounds nothing, and the idle effect of each kind is the walking one's next id.</summary>
	[TestMethod]
	public void TheFiveKindsEffectsAreTenIdsFromA0()
	{
		CollectionAssert.AreEqual( new[] { 0xa3, 0xa1, 0xa5, 0xa7, 0xa9 },
			new[] { 4, 5, 6, 7, 8 }.Select( StaffBehaviour.IdleSoundOf ).ToArray(), "mechanic, handyman, entertainer, guard, researcher" );
		Assert.AreEqual( 0, StaffBehaviour.IdleSoundOf( 1 ) );
		Assert.AreEqual( 0xf, StaffBehaviour.SoundDrawMask );
	}

	/// <summary>Every sample name of one staff effect, as the category file lists them.</summary>
	private static (HashSet<string> Names, SoundCategoryFile.Variation Header) SamplesOf( int effect )
	{
		var file = new SoundCategoryFile( "global/sound", "staff" );
		var banks = file.Banks.Select( bank => SoundBank.Load( "global", bank ) ).ToList();
		var lists = file.ReadSamples( banks.Select( bank => (IReadOnlyList<TimeSpan>)(bank?.Durations ?? []) ).ToList() );
		var index = file.IndexOf( effect );

		return (lists[index].Single().Select( sample => banks[sample.Bank]![sample.Index]!.Name ).ToHashSet(),
			file.ReadVariations()[index].Single());
	}

	/// <summary>
	/// What the shipped <c>cat_staff</c> holds for the fourteen effects the staff's code names: one variation each,
	/// the kinds' at volume 24 (the researcher's 16) and a pitch of -10 to 10, each with the blank sample among its
	/// voices; the performance's end one sample at 21; the two chase effects and the researching one as listed.
	/// </summary>
	[TestMethod]
	public void TheShippedCategoryHoldsTheFourteenEffects()
	{
		foreach ( var effect in Enumerable.Range( 0xa0, 10 ) )
		{
			var (names, header) = SamplesOf( effect );
			var volume = effect >= 0xa8 ? 16 : 24;

			Assert.AreEqual( (volume, volume), header.Volume, $"effect 0x{effect:x}" );
			Assert.AreEqual( (-10, 10), header.Pitch, $"effect 0x{effect:x}" );
			Assert.AreEqual( 0, header.GapByParameter | header.SecondMask, "nothing controls its volume or pitch" );
			Assert.IsTrue( names.Contains( "blank44.mp2" ), $"effect 0x{effect:x} has the blank sample" );
			Assert.IsTrue( names.Count is >= 3 and <= 6, $"effect 0x{effect:x} has {names.Count}" );
		}

		CollectionAssert.AreEquivalent( new[] { "blank44.mp2", "gd_st01.mp2", "gd_st02.mp2" }, SamplesOf( 0xa7 ).Names.ToArray() );
		CollectionAssert.AreEquivalent( new[] { "TADA.mp2" }, SamplesOf( 0x87 ).Names.ToArray() );
		Assert.AreEqual( (21, 21), SamplesOf( 0x87 ).Header.Volume );
		Assert.AreEqual( (0, 0), SamplesOf( 0x87 ).Header.Pitch );
		CollectionAssert.AreEquivalent( new[] { "Oi.mp2" }, SamplesOf( 0x88 ).Names.ToArray() );
		CollectionAssert.AreEquivalent( new[] { "blank44.mp2" }, SamplesOf( 0x89 ).Names.ToArray() );
		CollectionAssert.AreEquivalent( new[] { "blank44.mp2" }, SamplesOf( 0x8a ).Names.ToArray() );
	}

	/// <summary>
	/// <see cref="ParkAudio.StaffSound"/> starts one placed voice of the effect's own samples on the effects bus, at
	/// the variation's volume over a hundred of <see cref="ParkAudio.CrowdVoiceGain"/> and a rate from its pitch, and
	/// counts the voiced ones apart from the blank.
	/// </summary>
	[TestMethod]
	public void AStaffSoundIsAPlacedVoiceAtItsVariationsVolumeAndPitch()
	{
		var ready = typeof( Audio ).GetProperty( nameof( Audio.Ready ) )!;

		Assert.IsFalse( Audio.Ready, "a test run has no audio device" );

		List<Voice> before;

		lock ( Audio.Lock )
			before = [.. Audio.Voices];

		ParkAudio? park = null;

		try
		{
			ready.SetValue( null, true );
			park = new ParkAudio( "jungle" );

			var at = new Vector3( 480f, 220f, 3f );
			var names = SamplesOf( 0xa9 ).Names;
			var heard = new HashSet<string>();
			var rates = new HashSet<double>();

			for ( var i = 0; i < 400; ++i )
			{
				Assert.IsTrue( park.StaffSound( 0xa9, at ) );

				Voice voice;

				lock ( Audio.Lock )
				{
					voice = Audio.Voices[^1];
					Audio.Voices.Remove( voice );
				}

				Assert.IsTrue( names.Contains( voice.Name ), voice.Name );
				Assert.IsTrue( voice.IsPlaced );
				Assert.AreEqual( AudioBus.Effects, voice.Bus );
				Assert.IsFalse( voice.Loop );
				Assert.AreEqual( 0.16f * ParkAudio.CrowdVoiceGain, voice.Volume, 0.0001f );
				Assert.IsTrue( voice.Rate is >= 0.92 and <= 1.08, $"rate {voice.Rate}" );
				heard.Add( voice.Name );
				rates.Add( voice.Rate );
			}

			Assert.AreEqual( 400, park.StaffSounds.Started );
			Assert.AreEqual( names.Count, heard.Count, "four hundred draws reach every sample" );
			Assert.IsTrue( park.StaffSounds.Voiced is > 100 and < 220, $"about two in five are voices: {park.StaffSounds.Voiced}" );
			Assert.IsTrue( rates.Count > 10, "the pitch is drawn afresh" );
			Assert.IsTrue( rates.Min() < 0.95 && rates.Max() > 1.05, "across the variation's -10 to 10" );

			Assert.IsTrue( park.StaffSound( 0x87, at ) );

			lock ( Audio.Lock )
			{
				Assert.AreEqual( "TADA.mp2", Audio.Voices[^1].Name );
				Assert.AreEqual( 0.21f * ParkAudio.CrowdVoiceGain, Audio.Voices[^1].Volume, 0.0001f );
				Assert.AreEqual( 1.0, Audio.Voices[^1].Rate );
			}

			Assert.IsFalse( park.StaffSound( 0x70, at ), "an effect the category does not hold" );
			Assert.AreEqual( (401, park.StaffSounds.Voiced), park.StaffSounds );
		}
		finally
		{
			park?.Delete();
			Entity.ApplyDeletions();

			lock ( Audio.Lock )
				Audio.Voices.RemoveAll( voice => !before.Contains( voice ) );

			ready.SetValue( null, false );
		}
	}
}
