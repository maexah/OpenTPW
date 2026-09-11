using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenTPW.Tests;

/// <summary>
/// Particle handles at every generation. A handle is the generation shifted up sixteen bits with the slot
/// below it, which makes it a negative int from generation 0x8000 on; it must still find its own emitter,
/// and only that.
/// </summary>
[TestClass]
public class ParticleHandleTests
{
	private static readonly int[] Generations = { 1, 0x7fff, 0x8000, 0xfffe, 0xffff };
	private static readonly int[] Slots = { 0, 1, ParticleSystem.EmitterCount - 1 };

	[TestMethod]
	public void AHandleFindsItsOwnSlotAndGeneration()
	{
		foreach ( var generation in Generations )
		{
			foreach ( var slot in Slots )
			{
				var handle = ParticleSystem.Handle( generation, slot );

				Assert.AreEqual( slot, handle & 0xffff, $"slot, generation {generation:x}" );
				Assert.IsTrue( ParticleSystem.IsCurrent( handle, generation ), $"generation {generation:x}, slot {slot}" );
			}
		}
	}

	[TestMethod]
	public void AHandleMissesAnyOtherGenerationAndAFreeSlot()
	{
		foreach ( var generation in Generations )
		{
			var handle = ParticleSystem.Handle( generation, 7 );

			Assert.IsFalse( ParticleSystem.IsCurrent( handle, generation == 1 ? 2 : generation - 1 ), $"generation {generation:x}" );
			Assert.IsFalse( ParticleSystem.IsCurrent( handle, 0 ), $"free slot, generation {generation:x}" );
		}
	}
}
