using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using OpenTPW.UI;

namespace OpenTPW.Tests;

/// <summary>A list that builds its cells again lets go of the text textures of the old ones (the week-end audit of 2026-10-31, L-F1).</summary>
[TestClass]
public class UiListReleaseTests
{
	private static readonly FieldInfo Text = typeof( UiControl ).GetField( "_text", BindingFlags.NonPublic | BindingFlags.Instance )!;
	private static readonly FieldInfo Held = typeof( UiText ).GetField( "_texture", BindingFlags.NonPublic | BindingFlags.Instance )!;
	private static readonly FieldInfo Deletions = typeof( Renderer ).GetField( "_deletionQueue", BindingFlags.NonPublic | BindingFlags.Instance )!;

	private Renderer? oldRender;

	[TestInitialize]
	public void StandInRenderer()
	{
		// Texture.Delete queues the card's release on the renderer, which nothing here runs.
		oldRender = Global.Render;
		var render = (Renderer)RuntimeHelpers.GetUninitializedObject( typeof( Renderer ) );
		Deletions.SetValue( render, new System.Collections.Generic.List<Action>() );
		Global.Render = render;
	}

	[TestCleanup]
	public void PutRendererBack() => Global.Render = oldRender!;

	/// <remarks><b>Mutation:</b> <c>ReleaseText</c> taken out of <c>UiList.Build</c>: the old cells' textures stay in <c>Asset.All</c>, which holds them for the life of the process.</remarks>
	[TestMethod]
	public void BuildingAgainLetsGoOfTheOldCellsTexts()
	{
		var list = new UiList
		{
			RowArea = new UiRect( 0, 0, 100, 100 ),
			Columns = [(0, 50), (50, 100)],
			RowHeight = 44
		};

		list.Build();

		var stood = new System.Collections.Generic.List<Texture>();

		foreach ( var cell in list.Children )
		{
			var texture = (Texture)RuntimeHelpers.GetUninitializedObject( typeof( Texture ) );
			Held.SetValue( Text.GetValue( cell ), texture );
			Asset.All.Add( texture );
			stood.Add( texture );
		}

		Assert.AreEqual( 4, stood.Count, "two rows of two cells" );

		list.Build();

		foreach ( var texture in stood )
			Assert.IsFalse( Asset.All.Contains( texture ), "a texture of a cell that is gone is still held" );

		Assert.AreEqual( 4, list.Children.Count, "the new cells stand" );
	}
}
