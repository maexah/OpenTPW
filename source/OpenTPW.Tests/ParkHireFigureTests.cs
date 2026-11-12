using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// The hire screen's candidate figure (Q419, <c>FUN_0049ba00</c>): its script at word 1832, the rule that makes it again,
/// the bank and picture it shows, where it stands and how often it steps.
/// </summary>
[TestClass]
public class ParkHireFigureTests
{
	/// <summary>The portrait <c>0x2484</c>'s rectangle, the stream's.</summary>
	private static readonly UI.UiRect Portrait = new( 484, 179, 748, 443 );

	private static ParkStaffPool.Candidate Candidate( int id, int kind, int costume )
		=> new( Id: id, Kind: kind, Name: $"c{id}", Grade: 2, Costume: costume, Wage: 100 );

	/// <summary>
	/// <b>The figure's script</b> (<c>0x0074f758</c>, word 1832): the draw flags <c>0x51000</c>, set 1, facing 5, then
	/// frames nought to seven a turn, and round again; its words laid out by the operand counts.
	/// </summary>
	/// <remarks><b>Mutations:</b> the facing written 3; the set 0; the jump to the standing script (90); the flags a person's.</remarks>
	[TestMethod]
	public void TheFigureScriptWalksFacingFiveOnSetOneRoundAndRound()
	{
		int[] starts = [1832, 1835, 1837, 1840, 1842, 1844, 1846, 1848, 1850, 1852, 1854, 1856];

		for ( var word = 1832; word <= 1859; ++word )
			Assert.AreEqual( starts.Contains( word ), SpriteScript.HasInstructionAt( word ), $"word {word}" );

		var sprite = new SpriteScript( SpriteScript.None, SpriteScript.None, spriteNumber: 0, frame: 0 );
		sprite.StartAt( SpriteScript.StaffFigureEntry );

		var frames = new List<int>();

		for ( var turn = 1; turn <= 10; ++turn )
		{
			Assert.IsTrue( sprite.Step( turn * 1000 ), $"turn {turn} runs" );
			Assert.AreEqual( 0x51000, sprite.DrawFlags );
			Assert.AreEqual( 1, sprite.Set );
			Assert.AreEqual( 5, sprite.Facing );
			Assert.IsTrue( sprite.Shown );
			frames.Add( sprite.Frame );
		}

		CollectionAssert.AreEqual( new[] { 0, 1, 2, 3, 4, 5, 6, 7, 0, 1 }, frames );
		Assert.AreEqual( SpriteScript.StaffFigureEntry, sprite.Script );
	}

	/// <summary>
	/// <b>The point it is placed on</b>: for the portrait (484, 179)-(748, 443), x = (484 + 132 − 1016) / 1024 = −0.390625 and
	/// y = (768 − 443 + 66) / 768, on the virtual screen (624, 377). Where the picture sits on that point, and its size, are not
	/// decoded (Q1513), so nothing is drawn.
	/// </summary>
	/// <remarks><b>Mutations:</b> the centre taken without the 8 (<c>0x3f8</c> as 1024); the foot at the bottom (no quarter).</remarks>
	[TestMethod]
	public void TheFiguresPointIsTheOriginals()
	{
		var (x, y) = UI.ParkHireFigure.Place( Portrait );

		Assert.AreEqual( -400f / 1024f, x, 1e-6f );
		Assert.AreEqual( 391f / 768f, y, 1e-6f );

		var (vx, vy) = UI.ParkHireFigure.OnVirtualScreen( Portrait );

		Assert.AreEqual( 624f, vx, 1e-3f );
		Assert.AreEqual( 377f, vy, 1e-3f );
	}

	/// <summary>
	/// <b>Made again only when the role or costume changes</b> (<c>[0x007523e8]</c>, <c>[0x007523ec]</c>): no candidate leaves
	/// the figure as it is; another candidate of one role and costume keeps it, its walk going on; another costume or
	/// another role makes it again; the destroy frees it and sets both to -1, so the same candidate makes it again.
	/// </summary>
	/// <remarks><b>Mutations:</b> made on every pick; made again on a costume alone ignored; the pair not cleared by the free;
	/// no candidate clearing the figure.</remarks>
	[TestMethod]
	public void TheFigureIsMadeAgainOnlyWhenTheRoleOrCostumeChanges()
	{
		var figure = new UI.ParkHireFigure( null, "jungle", null ) { Rect = Portrait };

		Assert.IsFalse( figure.Show( null, 0 ) );
		Assert.AreEqual( UI.ParkHireFigure.None, figure.Role );
		Assert.AreEqual( 0, figure.Made );

		Assert.IsTrue( figure.Show( Candidate( 1, kind: 1, costume: 0 ), 0 ) );
		var first = figure.Sprite;

		first!.Step( 100 );
		first.Step( 200 );

		Assert.IsFalse( figure.Show( Candidate( 2, kind: 1, costume: 0 ), 0 ), "another mechanic in the same costume" );
		Assert.AreSame( first, figure.Sprite );
		Assert.AreEqual( 1, figure.Sprite!.Frame, "the walk goes on" );

		Assert.IsFalse( figure.Show( null, 0 ), "no candidate" );
		Assert.AreSame( first, figure.Sprite );

		Assert.IsTrue( figure.Show( Candidate( 3, kind: 1, costume: 1 ), 0 ), "another costume" );
		Assert.AreEqual( (1, 1), (figure.Role, figure.Costume) );

		Assert.IsTrue( figure.Show( Candidate( 4, kind: 3, costume: 1 ), 0 ), "another role" );
		Assert.AreEqual( (3, 1, 7), (figure.Role, figure.Costume, figure.Kind) );
		Assert.AreEqual( 3, figure.Made );

		figure.Free();

		Assert.AreEqual( (UI.ParkHireFigure.None, UI.ParkHireFigure.None), (figure.Role, figure.Costume) );
		Assert.IsNull( figure.Sprite );
		Assert.IsTrue( figure.Show( Candidate( 4, kind: 3, costume: 1 ), 0 ), "made again after the destroy" );
	}

	/// <summary>
	/// <b>It steps with the park's sprites</b>: on every second 31 ms tick, the interval the constructor writes (62) held
	/// strictly, so a new frame each fourth tick, as a person on the default interval; and its first is due one interval after the
	/// make (<c>0x004759df</c>), so a figure made at tick 0 shows its first frame at tick 4, not 2, and one made at tick 5 at tick 8.
	/// </summary>
	/// <remarks><b>Mutations:</b> stepped on every call; on the tick itself, not the newest sprite tick; at 62 ms a tick; no first
	/// due time (the old rule: a turn on the first step).</remarks>
	[TestMethod]
	public void TheFigureStepsOnTheParksSpriteTicks()
	{
		var figure = new UI.ParkHireFigure( null, "jungle", null ) { Rect = Portrait };

		figure.Show( Candidate( 1, kind: 0, costume: 0 ), 0 );

		var seen = new List<(int Tick, int Frame)>();
		var last = 0;

		for ( var tick = 1; tick <= 14; ++tick )
		{
			figure.Step( tick );

			if ( figure.Sprite!.Turns != last )
			{
				last = figure.Sprite.Turns;
				seen.Add( (tick, figure.Sprite.Frame) );
			}
		}

		CollectionAssert.AreEqual( new[] { (4, 0), (8, 1), (12, 2) }, seen.ToArray() );

		var later = new UI.ParkHireFigure( null, "jungle", null ) { Rect = Portrait };
		later.Show( Candidate( 2, kind: 0, costume: 0 ), 5 );

		var first = Enumerable.Range( 6, 10 ).First( tick => { later.Step( tick ); return later.Sprite!.Turns > 0; } );
		Assert.AreEqual( 8, first, "made at tick 5, due one interval on" );
	}

	/// <summary>
	/// <b>The picture</b>, from the game's banks: each role's kind by the table at <c>0x0049bb90</c> (5, 6, 4, 7, 8), its
	/// costume's bank in the original's order, set 1, and facing 5 drawn as direction 3 reflected, frame by frame. A
	/// costume past the kind's loaded banks is brought within them (the deviation <c>ParkPeople.StaffPicture</c> makes).
	/// </summary>
	/// <remarks><b>Mutations:</b> the role taken as the sprite kind; facing 5 not folded (clamped to 4); not reflected; the
	/// costume not brought within the loaded banks.</remarks>
	[TestMethod]
	public void TheFigureShowsTheCandidatesWalkFacingFiveReflected()
	{
		var data = GameData.Required();
		var banks = ParkSpriteBanks.Read( data, "jungle", numKids: 0 );

		var folders = new[] { "Handymen", "Mechanics", "Entertainers", "Guards", "Researchers" };

		for ( var role = 0; role < 5; ++role )
		{
			var figure = new UI.ParkHireFigure( data, "jungle", banks ) { Rect = Portrait };

			// Costume 1: on low detail the mechanics' and every other capped kind's one bank, so a costume past it is reduced.
			figure.Show( Candidate( role, kind: role, costume: 1 ), 0 );

			var kind = ParkStaffPool.SpriteKindFor( role );
			var folder = ParkGuestSprites.FolderFor( kind, "jungle" )!;
			var files = ParkGuestSprites.BanksIn( data, folder, kind );
			var expected = banks.Reduce( kind, 1 );

			StringAssert.Contains( folder, folders[role] );
			Assert.AreEqual( new[] { 5, 6, 4, 7, 8 }[role], figure.Kind, $"role {role}" );
			Assert.AreEqual( expected, figure.Bank, $"role {role}" );
			Assert.AreEqual( Path.GetFileNameWithoutExtension( files[expected] ), figure.BankName );

			var file = new SpriteBankFile( new MemoryStream( data.ReadAllBytes( files[expected] ) ) );
			var set = file.Sets[1];

			Assert.AreEqual( 5, set.Directions, $"{figure.BankName}'s walk is stored five ways round" );
			Assert.IsNull( figure.Showing(), "nothing before the script's first turn" );

			for ( var turn = 1; turn <= 8; ++turn )
			{
				figure.Sprite!.Step( turn * 1000 );

				var shown = figure.Showing()!.Value;

				Assert.AreEqual( set.First + (turn - 1) + (set.FramesPerDirection * 3), shown.Index, $"{figure.BankName} frame {turn - 1}" );
				Assert.IsTrue( shown.Mirrored );
			}
		}
	}

	/// <summary>
	/// <b>The hire screen makes the figure for the row it picks</b>, steps it on the game clock, and frees it as it closes: opened
	/// on the mechanics' tab, the first row's candidate is the figure's, half a second of frames turns its script and shows a
	/// picture, and after the close the pair is -1.
	/// </summary>
	/// <remarks><b>Mutations:</b> the pick not handing the candidate to the figure; the screen's update not stepping it; the close not freeing it.</remarks>
	[TestMethod]
	public void TheHireScreenMakesTheFigureForThePickedRowAndFreesItOnClose()
	{
		var levelBefore = Level.Current;
		var screenBefore = Screen.Size;
		var dataBefore = FileSystemOrNull;

		FileSystem = GameData.Required();
		Screen.Size = new Point2( 2048, 1536 );

		var pool = new ParkStaffPool( new ParkBalance( "jungle", easyMode: true ), gameTick: 0 );
		var level = (Level)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject( typeof( Level ) );
		typeof( Level ).GetProperty( nameof( Level.StaffPool ) )!.SetValue( level, pool );
		Level.Current = level;

		try
		{
			for ( var tick = 1; tick <= 400 && !pool.OfKind( 1 ).Any(); ++tick )
				pool.Sweep( tick, _ => 0 );

			var stack = new UI.WindowStack();
			var screen = new UI.ParkHireScreen( stack, openOn: 1 );
			var figure = (UI.ParkHireFigure)typeof( UI.ParkHireScreen )
				.GetField( "_figure", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic )!.GetValue( screen )!;

			stack.Open( screen );
			screen.Update();

			var list = (UI.UiList)typeof( UI.ParkHireScreen )
				.GetField( "_list", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic )!.GetValue( screen )!;
			var picked = pool.Candidates.Single( person => person.Id == list.Selected );

			Assert.AreEqual( (picked.Kind, picked.Costume & 0xff), (figure.Role, figure.Costume) );
			Assert.AreEqual( 1, figure.Made );
			Assert.AreEqual( 6, figure.Kind );
			Assert.IsTrue( figure.Bank >= 0, "a mechanics' bank read" );
			Assert.AreEqual( 0, figure.Sprite!.Turns, "no turn before the clock moves" );

			// The screen steps the figure on the game clock: a tenth of a second a frame, three ticks, for half a second.
			for ( var frame = 0; frame < 5; ++frame )
			{
				Time.Update( 0.1f );
				GameClock.Update( paused: false, GameClock.ParkCatchUp );
				screen.Update();
			}

			Assert.IsTrue( figure.Sprite!.Turns > 0, "the screen's frames step it" );
			Assert.IsNotNull( figure.Showing(), "and it shows a picture" );

			stack.Close( screen );

			Assert.AreEqual( (UI.ParkHireFigure.None, UI.ParkHireFigure.None), (figure.Role, figure.Costume) );
			Assert.IsNull( figure.Sprite );
		}
		finally
		{
			Level.Current = levelBefore!;
			Screen.Size = screenBefore;
			FileSystemOrNull = dataBefore;
		}
	}
}
