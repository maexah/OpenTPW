using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenTPW.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace OpenTPW.Tests;

/// <summary>
/// <b>The all-items screen's pictures</b> (Q1043). The layout stream <c>0x007508e0</c> gives the control that holds the tab strip
/// (<c>0x12c4b8</c>, 239, 179 to 1835, 902) the mesh <c>0xbea849ac</c>, <c>list_all</c>, the tab group <c>0x12c4b9</c> none, and the
/// close button <c>0xfffffffe</c> the mesh <c>0x14a7e48b</c>, <c>b_okay</c>, the green tick (<c>docs/exe/hud.md</c>, "The all-items
/// screen's pictures"). A mesh is named in a stream by <c>h = (c ^ h) * 47</c> over its model's first node name, so each name the
/// screen asks for is hashed here and compared with the executable's number. Skipped without the game (<see cref="GameData"/>).
/// </summary>
[TestClass]
public class ParkItemsPicturesTests
{
	/// <summary>The stream's mesh hashes, read from the executable (<c>0x007509e2</c>, <c>0x00750a2c</c>).</summary>
	private const uint BackdropHash = 0xbea849ac;
	private const uint CloseHash = 0x14a7e48b;

	private Level? _level;

	[TestInitialize]
	public void LoadThePark()
	{
		_level = Level.Current;

		Screen.Size = new Point2( 2048, 1536 );
		Log ??= new();
		FileSystem = GameData.Required();
		Input.Mouse = new();
		Unimplemented.Forget();
	}

	[TestCleanup]
	public void PutItAllBack()
	{
		Level.Current = _level!;
		Unimplemented.Forget();
	}

	private static uint Hash( string meshName )
	{
		var node = new ModelFile( $"ui/{meshName}.md2" ).Nodes[0].Name.TrimEnd( '\0' );
		uint h = 0;

		foreach ( var c in node )
			h = (c ^ h) * 47;

		return h;
	}

	/// <summary>
	/// <b>The names the screen asks for are the stream's meshes</b>: <c>list_all</c> hashes to <c>0xbea849ac</c> and <c>b_okay</c> to
	/// <c>0x14a7e48b</c>, and the red cross <c>b_exit</c> and the frame <c>!frame</c> do not.
	/// </summary>
	/// <remarks><b>Mutations:</b> <c>b_exit</c> for the close button's name; <c>!frame</c> for the backdrop's.</remarks>
	[TestMethod]
	public void TheScreensMeshNamesHashToTheStreamsNumbers()
	{
		Assert.AreEqual( BackdropHash, Hash( ParkItemsScreen.BackdropMesh ), "the backdrop" );
		Assert.AreEqual( CloseHash, Hash( ParkItemsScreen.CloseMesh ), "the close button" );

		Assert.AreNotEqual( CloseHash, Hash( "b_exit" ), "the red cross is another picture, or the test is hollow" );
		Assert.AreNotEqual( BackdropHash, Hash( "!frame" ), "the frame is another picture, or the test is hollow" );
	}

	private static IEnumerable<UiControl> All( UiControl control )
	{
		yield return control;

		foreach ( var child in control.Children )
		{
			foreach ( var found in All( child ) )
				yield return found;
		}
	}

	/// <summary>
	/// <b>The screen puts those pictures where the stream does</b>: the big control, the tab group with none, the close button the
	/// tick. What a control is made to wear is read from the names the screen asked for as it was built (and, through <see cref="UiMesh.MeshAsked"/>, every name anything in it asked <see cref="UiMesh.Get"/> for)
	/// (a graphics device is needed to load the picture itself).
	/// </summary>
	/// <remarks><b>Mutations:</b> the backdrop's name on the tab group instead (also put back as the old <c>Mesh = UiMesh.Get( "list_all" )</c> line, not through the seam); <c>b_exit</c> on the close button; <c>!frame</c> on the big control.</remarks>
	[TestMethod]
	public void EachControlWearsTheMeshTheStreamGivesIt()
	{
		var asked = new List<(int Id, string Name)>();
		var everyName = new List<string>();
		ParkItemsScreen.PictureAsked = ( id, name ) => asked.Add( (id, name) );
		UiMesh.MeshAsked = name => everyName.Add( name );

		try
		{
			_ = new ParkItemsScreen( new WindowStack(), 0 );
		}
		finally
		{
			ParkItemsScreen.PictureAsked = null;
			UiMesh.MeshAsked = null;
		}

		Assert.AreEqual( ParkItemsScreen.BackdropMesh, asked.Single( x => x.Id == 0x12c4b8 ).Name, "the big control" );
		Assert.AreEqual( ParkItemsScreen.CloseMesh, asked.Single( x => x.Id == -2 ).Name, "the close button" );
		Assert.IsFalse( asked.Any( x => x.Id == 0x12c4b9 ), "the tab group has no mesh in the stream" );
		Assert.AreEqual( 1, everyName.Count( x => x == ParkItemsScreen.BackdropMesh ), "list_all is asked for once, by the big control, from anywhere in the screen" );
	}
}
