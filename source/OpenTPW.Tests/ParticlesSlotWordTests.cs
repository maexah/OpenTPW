using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>Q284: the console's `particles &lt;slot&gt;` says so when the slot is not a number, and does not read it as slot 0.</summary>
[TestClass]
public class ParticlesSlotWordTests
{
	[TestMethod]
	public void AWordIsNotSlotZero()
	{
		Log ??= new();
		FileSystem = GameData.Required();
		var system = new ParticleSystem( "Particle/Tp2.plb", 1000 );

		try
		{
			// Predicted: slot 0 is live, `0` shows its cloud, and `abc` / `1.5` / `` answer that they are not slot numbers.
			Assert.AreEqual( 0, system.Spawn( 1, 0, 0, 0 ) & 0xffff, "the first emitter is slot 0" );
			Assert.IsTrue( system.Emitters[0].Active );

			StringAssert.StartsWith( DebugConsole.ParticleCloudReplies( system, "0" )[0], "particles: slot 0 effect 1 holds" );
			CollectionAssert.AreEqual( new[] { "particles: abc is not a slot number" }, DebugConsole.ParticleCloudReplies( system, "abc" ) );
			CollectionAssert.AreEqual( new[] { "particles: 1.5 is not a slot number" }, DebugConsole.ParticleCloudReplies( system, "1.5" ) );
			CollectionAssert.AreEqual( new[] { "particles: no emitter is running in slot 7" }, DebugConsole.ParticleCloudReplies( system, "7" ) );
			CollectionAssert.AreEqual( new[] { "particles: no emitter is running in slot 999" }, DebugConsole.ParticleCloudReplies( system, "999" ) );
		}
		finally
		{
			system.Shutdown();
		}
	}
}
