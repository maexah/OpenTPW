using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using NumericsVector3 = System.Numerics.Vector3;

namespace OpenTPW.Tests;

/// <summary>
/// A research panel's pump (<c>ResearchPump_SetRate</c> <c>0x004a95b0</c>, <c>ResearchPump_Tick</c> <c>0x004a9fc0</c>;
/// <c>docs/exe/hud.md</c>, "The pump"), and the slider thumb's press and release that start and fade the effort's sound.
/// </summary>
[TestClass]
public class ResearchPumpTests
{
	/// <summary>
	/// <b>A rate starts the pump, which fills and then runs</b>: a rate of 2 is a speed of <c>22 × 0x10000 / 100</c> = 14417,
	/// so 28834 a pass; from <c>0x30</c> it passes <c>0x3f</c> on the 37th pass (36 reach 0x3f's 0x3fd6d0) and runs from
	/// nought, and the run passes <c>0x1f</c> on its 73rd. A rate of nought drains it from <c>0x20</c>, and after 37 passes
	/// it rests at <c>0x30</c>. A new rate while it runs changes only its speed; nought on a stopped pump changes nothing.
	/// </summary>
	/// <remarks><b>Mutations:</b> one speed a pass rather than two; the run's wrap at 0x20 kept as a frame; a running pump
	/// restarted by a new rate; the stop resting at 0x2f; a stopped pump moved.</remarks>
	[TestMethod]
	public void ARateStartsThePumpWhichFillsThenRuns()
	{
		var pump = new UI.ResearchPump();

		pump.Pass();
		Assert.AreEqual( (UI.ResearchPump.PumpState.Stopped, 0x30, 0), (pump.State, pump.Frame, pump.Passes), "a stopped pump does not move" );

		pump.SetRate( 2 );
		Assert.AreEqual( (UI.ResearchPump.PumpState.Starting, 0x30, 0x300000, 14417), (pump.State, pump.Frame, pump.Position, pump.Speed) );

		for ( var pass = 0; pass < 36; ++pass )
			pump.Pass();

		Assert.AreEqual( (UI.ResearchPump.PumpState.Starting, 0x3f, 0x300000 + (36 * 28834)), (pump.State, pump.Frame, pump.Position) );

		pump.Pass();
		Assert.AreEqual( (UI.ResearchPump.PumpState.Running, 0, 0), (pump.State, pump.Frame, pump.Position) );

		for ( var pass = 0; pass < 72; ++pass )
			pump.Pass();

		Assert.AreEqual( (0x1f, 72 * 28834), (pump.Frame, pump.Position) );
		pump.Pass();
		Assert.AreEqual( (UI.ResearchPump.PumpState.Running, 0, 0), (pump.State, pump.Frame, pump.Position), "the run comes round" );

		pump.Pass();
		pump.SetRate( 5 );
		Assert.AreEqual( (UI.ResearchPump.PumpState.Running, 0, 28834, 16384), (pump.State, pump.Frame, pump.Position, pump.Speed), "only the speed" );

		pump.SetRate( 0 );
		Assert.AreEqual( (UI.ResearchPump.PumpState.Stopping, 0x20, 0x200000, 16384), (pump.State, pump.Frame, pump.Position, pump.Speed) );

		for ( var pass = 0; pass < 31; ++pass )
			pump.Pass();

		Assert.AreEqual( (UI.ResearchPump.PumpState.Stopping, 0x2f), (pump.State, pump.Frame), "31 passes of 32768 reach 0x2f" );
		pump.Pass();
		Assert.AreEqual( (UI.ResearchPump.PumpState.Stopped, 0x30, 0x300000), (pump.State, pump.Frame, pump.Position) );

		var passes = pump.Passes;
		pump.SetRate( 0 );
		pump.Pass();
		Assert.AreEqual( (UI.ResearchPump.PumpState.Stopped, 0x30, 16384, passes), (pump.State, pump.Frame, pump.Speed, pump.Passes) );
	}

	/// <summary>
	/// <b>A running pump is drawn as one scroll of <c>fluid</c>, turned at 31 with its fraction kept</b>: at rate 2 (28834 a
	/// pass) the run starts at nought; 70 passes in it stands at 2018380 (frame 30.80), drawn moved by -30.80/31 of a repeat
	/// where the original shows <c>0x1e</c>; the 71st turns it to 15598 (0.238) where the original shows <c>fluid32</c>, and
	/// the 73rd, where the original's run comes round to nought and loses its fraction, to 73266 (1.118), and the 74th a pass
	/// on from there. Half a pass owed is drawn as half a pass further on. Stopping draws the drain's own parts, unmoved, and
	/// a run started again starts at nought.
	/// </summary>
	/// <remarks><b>Mutations:</b> the turn at 32; the fraction dropped at the turn; the owed part of a pass not drawn; the
	/// shift's sign; the logic's frame drawn while running; the scroll not set to nought at a new run; the logic's position
	/// drawn, turned at 31; a repeat taken as 32 frames.</remarks>
	[TestMethod]
	public void ARunningPumpIsDrawnAsOneScroll()
	{
		var pump = new UI.ResearchPump();
		pump.SetRate( 2 );

		for ( var pass = 0; pass < 37; ++pass )
			pump.Pass();

		Assert.AreEqual( (UI.ResearchPump.PumpState.Running, 0, 0, 0), (pump.State, pump.Frame, pump.Scroll, pump.DrawnPart) );
		Assert.AreEqual( -(0.5f * 28834 / 65536) / 31, pump.Shift( 0.5f ), 1e-6f, "half a pass owed" );

		for ( var pass = 0; pass < 70; ++pass )
			pump.Pass();

		Assert.AreEqual( (0x1e, 2018380, 0), (pump.Frame, pump.Scroll, pump.DrawnPart) );
		Assert.AreEqual( -(2018380f / 65536) / 31, pump.Shift( 0f ), 1e-6f );

		pump.Pass();
		Assert.AreEqual( (0x1f, 15598), (pump.Frame, pump.Scroll), "turned at 31, the fraction kept" );
		Assert.AreEqual( -(15598f / 65536) / 31, pump.Shift( 0f ), 1e-6f );

		pump.Pass();
		pump.Pass();
		Assert.AreEqual( (0, 0, 73266), (pump.Frame, pump.Position, pump.Scroll), "the original's run comes round; the drawn one goes on" );
		StringAssert.EndsWith( pump.ToString(), " scroll 1.118" );
		pump.Pass();
		Assert.AreEqual( (0, 28834, 73266 + 28834), (pump.Frame, pump.Position, pump.Scroll), "a pass on from both" );

		pump.SetRate( 0 );
		Assert.AreEqual( (0x20, 0f), (pump.DrawnPart, pump.Shift( 0.5f )) );

		pump.SetRate( 2 );

		for ( var pass = 0; pass < 37; ++pass )
			pump.Pass();

		Assert.AreEqual( (UI.ResearchPump.PumpState.Running, 0), (pump.State, pump.Scroll), "a new run starts at nought" );
	}

	/// <summary>
	/// <b>What the scroll stands on, in <c>ui/res_fluid.md2</c></b>: the run's 32 meshes, <c>fluid</c> to <c>fluid32</c>, are
	/// one quad each of the same corners (the file lays them side by side) with the same <c>v</c>, mesh <c>k</c>'s <c>u</c> <c>fluid</c>'s less <c>k/31</c>, so
	/// <c>fluid</c> moved by <see cref="UI.ResearchPump.Shift"/> at a whole frame is that frame's mesh, and <c>fluid32</c> is
	/// <c>fluid</c> one repeat on. The drain's and the fill's are not a scroll: <c>empty02</c> to <c>empty15</c> and
	/// <c>empty18</c> to <c>empty31</c> are two quads each.
	/// </summary>
	[TestMethod]
	public void TheRunsMeshesAreFluidMovedAThirtyFirstEach()
	{
		FileSystem = GameData.Required();
		var meshes = new ModelFile( "ui/res_fluid.md2" ).Meshes;
		Assert.AreEqual( 64, meshes.Count );

		var fluid = meshes[0];
		Assert.AreEqual( ("fluid", "fluid02", "fluid32", "empty01"), (Name( meshes[0] ), Name( meshes[1] ), Name( meshes[31] ), Name( meshes[32] )) );

		for ( var k = 0; k < 32; ++k )
		{
			var mesh = meshes[k];
			Assert.AreEqual( 4, mesh.Vertices.Length, Name( mesh ) );

			for ( var i = 0; i < 4; ++i )
			{
				var at = Placed( mesh, i ) - Corner( mesh );
				var was = Placed( fluid, i ) - Corner( fluid );
				Assert.IsTrue( NumericsVector3.Distance( at, was ) < 1e-3f, $"{Name( mesh )} corner {i}" );
				Assert.AreEqual( fluid.TexCoords[i].Y, mesh.TexCoords[i].Y, 1e-5f, $"{Name( mesh )} v {i}" );
				Assert.AreEqual( fluid.TexCoords[i].X - (k / (float)UI.ResearchPump.RunPictures), mesh.TexCoords[i].X, 2e-5f, $"{Name( mesh )} u {i}" );
			}
		}

		CollectionAssert.AreEqual( Enumerable.Repeat( 8, 28 ).ToArray(),
			Enumerable.Range( 33, 14 ).Concat( Enumerable.Range( 49, 14 ) ).Select( k => meshes[k].Vertices.Length ).ToArray() );
	}

	private static string Name( ModelFile.Mesh mesh ) => mesh.Name.TrimEnd( '\0' ).Trim();

	/// <summary>A vertex where its node puts it, and the least corner of a mesh's so: a control fits a part's bounds to its rect.</summary>
	private static NumericsVector3 Placed( ModelFile.Mesh mesh, int i )
		=> NumericsVector3.Transform( new NumericsVector3( mesh.Vertices[i].Position.X, mesh.Vertices[i].Position.Y, mesh.Vertices[i].Position.Z ), mesh.WorldTransform );

	private static NumericsVector3 Corner( ModelFile.Mesh mesh )
		=> Enumerable.Range( 0, mesh.Vertices.Length ).Select( i => Placed( mesh, i ) ).Aggregate( NumericsVector3.Min );

	/// <summary>
	/// <b>A rate given to a stopping pump starts it again from <c>0x30</c></b>, and nought given to a starting one drains it.
	/// </summary>
	[TestMethod]
	public void AStoppingPumpStartsAgain()
	{
		var pump = new UI.ResearchPump();

		pump.SetRate( 80 );
		pump.Pass();
		pump.SetRate( 0 );
		Assert.AreEqual( (UI.ResearchPump.PumpState.Stopping, 0x20), (pump.State, pump.Frame), "a starting pump drains" );

		pump.Pass();
		pump.SetRate( 1 );
		Assert.AreEqual( (UI.ResearchPump.PumpState.Starting, 0x30, 0x300000, 13762), (pump.State, pump.Frame, pump.Position, pump.Speed) );
	}

	/// <summary>
	/// <b>A slider's thumb says when it is taken hold of and let go</b> (messages <c>0x801</c> and <c>0x802</c>), the release
	/// after the thumb is put where its value belongs: dragged to 95, the value 52, and put back at 93 before it is told.
	/// A click on the track says neither.
	/// </summary>
	[TestMethod]
	public void ASlidersThumbSaysItsPressAndRelease()
	{
		var screen = Screen.Size;
		Screen.Size = new Point2( 2048, 1536 );

		try
		{
			ThumbSays();
		}
		finally
		{
			Screen.Size = screen;
		}
	}

	private static void ThumbSays()
	{
		var said = new List<string>();
		var slider = new UI.UiSlider { Rect = new UI.UiRect( 0, 0, 200, 40 ), HitRect = new UI.UiRect( 0, 0, 200, 40 ), Track = new UI.UiRect( 0, 0, 200, 40 ), Maximum = 100 };
		var thumb = slider.AddThumb( new UI.UiSliderThumb { Rect = new UI.UiRect( 0, 0, 20, 40 ) } );

		slider.ThumbPressed = () => said.Add( $"pressed {slider.Value}" );
		slider.ThumbReleased = () => said.Add( $"released {thumb.Rect.Left}" );
		slider.SetValue( 50 );

		slider.LeftClickedAt( 190, 10 );
		CollectionAssert.AreEqual( Array.Empty<string>(), said, "a page click" );

		thumb.PointerPressed( 100, 10 );
		thumb.PointerDragged( 104, 10 );
		Assert.AreEqual( (95, 52), (thumb.Rect.Left, slider.Value) );
		thumb.PointerReleased();
		CollectionAssert.AreEqual( new[] { "pressed 51", "released 93" }, said );
	}
}
