using System.Collections.Generic;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenTPW.Tests;

/// <summary>
/// The world's three passes: every entity's solid half, then every see-through half, then every sprite, so a
/// sprite that writes no depth is never painted over by a see-through surface made after it.
/// </summary>
[TestClass]
public class EntityRenderOrderTests
{
	private sealed class Probe( string name, List<string> drawn ) : Entity
	{
		protected override void OnRender() => drawn.Add( $"{name} solid" );
		protected override void OnRenderTranslucent() => drawn.Add( $"{name} see-through" );
		protected override void OnRenderSprites() => drawn.Add( $"{name} sprites" );
	}

	[TestMethod]
	public void AnEarlierEntitysSpritesAreDrawnAfterALaterEntitysSeeThroughHalf()
	{
		var all = Entity.All;
		var drawn = new List<string>();

		try
		{
			// A list of its own: what other tests left in the real one has nothing to draw with here.
			Entity.All = new List<Entity>();

			_ = new Probe( "first", drawn );
			_ = new Probe( "second", drawn );

			Entity.RenderWorld();

			CollectionAssert.AreEqual( new[]
			{
				"first solid", "second solid",
				"first see-through", "second see-through",
				"first sprites", "second sprites"
			}, drawn );
		}
		finally
		{
			Entity.All = all;
		}
	}

	[TestMethod]
	public void TheWorldsParticlesAreDrawnInTheSpritePass()
	{
		const BindingFlags own = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

		Assert.IsNotNull( typeof( WorldParticles ).GetMethod( "OnRenderSprites", own ), "the particles draw in the sprite pass" );
		Assert.IsNull( typeof( WorldParticles ).GetMethod( "OnRenderTranslucent", own ), "and nowhere in the see-through pass" );
	}
}
