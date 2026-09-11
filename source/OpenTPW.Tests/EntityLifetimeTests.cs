using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenTPW.Tests;

/// <summary>
/// Deleting an entity: it ends once, it leaves the list only between passes, and a walk over the list that it
/// is deleted during carries on without it.
/// </summary>
[TestClass]
public class EntityLifetimeTests
{
	private sealed class Probe : Entity
	{
		public int Deletes;
		public int Updates;
		public Action? DuringUpdate;

		protected override void OnDelete() => Deletes++;

		protected override void OnUpdate()
		{
			Updates++;
			DuringUpdate?.Invoke();
		}
	}

	[TestMethod]
	public void DeletingDuringAWalkLeavesTheWalkAlone()
	{
		var first = new Probe();
		var second = new Probe();
		first.DuringUpdate = second.Delete;

		try
		{
			Entity.All.ForEach( entity => entity.Update() );

			Assert.AreEqual( 1, second.Deletes );
			Assert.AreEqual( 0, second.Updates, "deleted before its turn in the walk" );
			Assert.IsTrue( Entity.All.Contains( second ), "still listed until the walk is over" );

			Entity.ApplyDeletions();

			Assert.IsFalse( Entity.All.Contains( second ) );
			Assert.IsTrue( Entity.All.Contains( first ) );
		}
		finally
		{
			first.Delete();
			second.Delete();
			Entity.ApplyDeletions();
		}
	}

	[TestMethod]
	public void DeletingTwiceEndsItOnce()
	{
		var probe = new Probe();

		probe.Delete();
		probe.Delete();
		Entity.ApplyDeletions();
		probe.Delete();
		Entity.ApplyDeletions();

		Assert.AreEqual( 1, probe.Deletes );
		Assert.IsFalse( Entity.All.Contains( probe ) );
	}
}
