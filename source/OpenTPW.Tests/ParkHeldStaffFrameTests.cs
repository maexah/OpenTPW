using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace OpenTPW.Tests;

/// <summary>
/// A member of staff in the hand is not drawn in the park (<c>FUN_00505c50</c> frees their sprite), shown by the frame itself:
/// <see cref="ParkGuestSprites.WriteTheFrame"/> writes the quads of the people there and none of the one held. The census
/// predicate <see cref="ParkGuestSprites.InTheHand"/> alone (<c>ParkStaffWindowTests.PickUpPutsTheMemberInTheHand</c>) cannot
/// see the draw loop's skip taken out (Q211, Q432). Skipped without the game (<see cref="GameData"/>).
/// </summary>
/// <remarks><b>Mutations:</b> the <c>InTheHand</c> skip out of the draw loop.</remarks>
[TestClass]
public class ParkHeldStaffFrameTests
{
	[TestCleanup]
	public void PutAway()
	{
		TestRun.DeleteEvery<ParkGuestSprites>();
		TestRun.DeleteEvery<ParkPeople>();
	}

	/// <summary>
	/// <b>The frame writes no quad for the member in the hand</b>: three sprites stood 5, 50 and 20 along the line of sight, the
	/// nearest the park's own mechanic's, each a picture and the facing dash, are six quads while they stand and four once
	/// they are picked up, the 50 and the 20 alone.
	/// </summary>
	[TestMethod]
	public void AFrameWritesNoQuadForTheMemberInTheHand()
	{
		var data = FileSystem = GameData.Required();
		Log ??= new();
		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		var shipped = new ParkWorld( new SaveReader( stream ).ReadFile() );
		var bankFile = ParkGuestSprites.BanksIn( data, ParkGuestSprites.FolderFor( 0, "jungle" )!, 0 )[0];
		var bank = new SpriteBankFile( new MemoryStream( data.ReadAllBytes( bankFile ) ) );
		var pictures = Enumerable.Repeat( new ParkGuestSprites.Region( 0.25f, 0.1f, 0.5f, 0.3f, 38, 50, 22, 42 ), 1024 ).ToArray();

		var all = BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
		var cameraField = typeof( Camera ).GetField( "CameraMode", all )!;
		var cameraBefore = cameraField.GetValue( null );
		var statics = new[] { typeof( ParkBumperBoats ), typeof( ParkGround ), typeof( ParkObjects ) }
			.Select( type => type.GetProperty( "Current", all )! ).ToArray();
		var staticsBefore = statics.Select( property => property.GetValue( null ) ).ToArray();
		var dashBefore = ParkGuestSprites.DebugFacing;

		try
		{
			foreach ( var property in statics )
				property.SetValue( null, null );

			var people = new ParkPeople( shipped );
			var member = people.Staff.First( staff => staff.Model == ParkPeople.HandymanModel );

			Camera.SetCameraMode<CameraMode>();
			var mode = (CameraMode)cameraField.GetValue( null )!;
			mode.Position = new OpenTPW.Vector3( 100f, -40f, 7f );
			var forward = Camera.Rotation.Forward;

			var pool = new ParkGuestSprites( "jungle", null );
			pool.Resize( 6 );

			var loaded = typeof( ParkGuestSprites ).GetNestedType( "Loaded", all )!;
			((IDictionary)typeof( ParkGuestSprites ).GetField( "_banks", all )!.GetValue( pool )!)[(0, 0)] = Activator.CreateInstance( loaded, bank, pictures );
			typeof( ParkGuestSprites ).GetField( "_plain", all )!.SetValue( pool, pictures[0] );

			var sprites = (List<(ParkWorld.Person Person, ParkWorld.Sprite Sprite)>)typeof( ParkGuestSprites ).GetField( "_people", all )!.GetValue( pool )!;

			foreach ( var (thing, depth) in new[] { (member.ThingId, 5f), (900001, 50f), (900002, 20f) } )
			{
				var at = mode.Position + (forward * depth);
				sprites.Add( (new ParkWorld.Person( thing, 0, 0, 0, thing, 0, default!, null ),
					new ParkWorld.Sprite( thing, 0, 0, 0, at.X, at.Z, at.Y, 0, 0, 255, 0, 0, 0 )) );
			}

			ParkGuestSprites.DebugFacing = true;

			Assert.AreEqual( 6, pool.WriteTheFrame().Used, "the control: all three stand, a picture and a dash each" );

			Assert.IsTrue( people.PickUp( member.ThingId ) );
			Assert.IsTrue( ParkGuestSprites.InTheHand( people, member.ThingId ) );

			var (used, _) = pool.WriteTheFrame();
			var (_, depths) = pool.Written;

			Assert.AreEqual( 4, used, "the member in the hand has no quad" );
			CollectionAssert.AreEqual( new[] { 50f, 50f, 20f, 20f }, depths.Take( used ).Select( d => MathF.Round( d, 2 ) ).ToArray(), "the other two, furthest first" );
		}
		finally
		{
			ParkGuestSprites.DebugFacing = dashBefore;
			cameraField.SetValue( null, cameraBefore );

			for ( var i = 0; i < statics.Length; ++i )
				statics[i].SetValue( null, staticsBefore[i] );
		}
	}
}
