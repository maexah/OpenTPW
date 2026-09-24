using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace OpenTPW.Tests;

/// <summary>
/// Leaving a park lets go of the park itself, so that it is not kept in memory through the lobby: the rides as
/// they are deleted (<see cref="ParkRides"/>' <c>OnDelete</c>), and the running state and the hiring pool last of
/// all (<see cref="Level.ForgetRunningPark"/>).
///
/// <para>
/// <b>The question is asked of the garbage collector</b>, as <see cref="ParkCamcorderForgetTests"/> asks it, and
/// not of the fields: <see cref="ParkRides"/> holds no save of its own, and reaches one only through the level
/// every entity keeps (<see cref="Entity.Level"/>), so a cleared field would prove nothing about what can go. The
/// saves are stand-ins, <c>new ParkWorld( [] )</c>, so these need no installation.
/// </para>
/// <para>
/// <b>The calls from <see cref="Level.Unload"/> are not reached here</b>, because no test can build a level; the
/// game run is what shows a left park collected in the lobby.
/// </para>
/// </summary>
[TestClass]
public class ParkForgetTests
{
	private static readonly PropertyInfo StateCurrent = typeof( ParkState ).GetProperty( nameof( ParkState.Current ) )!;
	private static readonly PropertyInfo PoolCurrent = typeof( ParkStaffPool ).GetProperty( nameof( ParkStaffPool.Current ) )!;

	private Level? _levelBefore;
	private ParkState? _stateBefore;
	private ParkStaffPool? _poolBefore;

	/// <summary>The pool says how many candidates it rolled, so the log has to exist.</summary>
	[TestInitialize]
	public void Remember()
	{
		Log = new();
		(_levelBefore, _stateBefore, _poolBefore) = (Level.Current, ParkState.Current, ParkStaffPool.Current);
	}

	[TestCleanup]
	public void PutBack()
	{
		Level.Current = _levelBefore!;
		StateCurrent.SetValue( null, _stateBefore );
		PoolCurrent.SetValue( null, _poolBefore );
	}

	/// <summary>
	/// <b>The running state and the hiring pool are collected once the park has ended</b>, and the save with the
	/// state.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> <see cref="Level.ForgetRunningPark"/> leaving out either of its two calls keeps what
	/// that one holds alive.
	/// </remarks>
	[TestMethod]
	public void EndingTheParkLetsGoOfItsRunningStateAndItsPool()
	{
		var (save, state, pool) = PlayIn();

		Collect();
		Assert.IsTrue( state.IsAlive && save.IsAlive, "the control: the park being played holds its state, and the save" );
		Assert.IsTrue( pool.IsAlive, "and its hiring pool" );

		Level.ForgetRunningPark();

		Collect();
		Assert.IsFalse( save.IsAlive, "the save is held by nothing of the running park's" );
		Assert.IsFalse( state.IsAlive, "nor is the running state" );
		Assert.IsFalse( pool.IsAlive, "nor the hiring pool" );
	}

	/// <summary>
	/// <b>Deleted rides are held by nothing</b> - not by <see cref="ParkRides.Current"/>, not by
	/// <see cref="Entity.All"/> - and so no longer keep the level they were made in, or its save, in memory.
	/// </summary>
	/// <remarks><b>Mutation:</b> without its <c>OnDelete</c>, <see cref="ParkRides.Current"/> keeps the level alive.</remarks>
	[TestMethod]
	public void DeletingTheRidesLetsGoOfTheLevelTheyWereMadeIn()
	{
		var (save, rides) = MakeRidesInALevel();

		Collect();
		Assert.IsTrue( save.IsAlive, "the control: the rides hold the level they were made in, and its save" );

		Delete( rides );

		Collect();
		Assert.IsFalse( rides.IsAlive, "the rides are held by nothing" );
		Assert.IsFalse( save.IsAlive, "and nor is the level's save" );
	}

	/// <summary>
	/// Deleting rides that are no longer the current ones leaves the current ones where they are - the guard every
	/// park entity's <c>Current</c> has.
	/// </summary>
	/// <remarks><b>Mutation:</b> clearing <see cref="ParkRides.Current"/> whoever is deleted.</remarks>
	[TestMethod]
	public void DeletingEarlierRidesLeavesTheLaterOnesCurrent()
	{
		var earlier = new ParkRides( "jungle", null, null );
		var later = new ParkRides( "jungle", null, null );

		try
		{
			earlier.Delete();
			Entity.ApplyDeletions();

			Assert.AreSame( later, ParkRides.Current, "the later rides are still the park's" );
		}
		finally
		{
			later.Delete();
			Entity.ApplyDeletions();
		}

		Assert.IsNull( ParkRides.Current, "and deleting those lets them go" );
	}

	/// <summary>
	/// A park being played - a save, the running state seeded from it and a hiring pool - known only by weak
	/// references. Not inlined, and so its own frame: a debug build keeps a method's locals alive until it returns.
	/// </summary>
	[MethodImpl( MethodImplOptions.NoInlining )]
	private static (WeakReference Save, WeakReference State, WeakReference Pool) PlayIn()
	{
		var save = new ParkWorld( [] );
		var state = new ParkState( save );
		var pool = new ParkStaffPool( null );

		Assert.AreSame( state, ParkState.Current, "building the state makes it the park's" );
		Assert.AreSame( pool, ParkStaffPool.Current, "and building the pool makes it the park's" );

		return (new WeakReference( save ), new WeakReference( state ), new WeakReference( pool ));
	}

	/// <summary>
	/// Rides made while a level holding a save is <see cref="Level.Current"/>, which is how each entity comes by its
	/// level, known only by weak references. The level is put back before this returns, so the rides are what holds
	/// it.
	/// </summary>
	[MethodImpl( MethodImplOptions.NoInlining )]
	private (WeakReference Save, WeakReference Rides) MakeRidesInALevel()
	{
		var save = new ParkWorld( [] );
		var level = (Level)RuntimeHelpers.GetUninitializedObject( typeof( Level ) );

		typeof( Level ).GetProperty( nameof( Level.Park ) )!.SetValue( level, save );
		Level.Current = level;

		var rides = new ParkRides( "jungle", null, null );

		Level.Current = _levelBefore!;

		Assert.AreSame( level, rides.Level, "the rides keep the level they were made in" );
		Assert.AreSame( rides, ParkRides.Current, "and are the park's" );

		return (new WeakReference( save ), new WeakReference( rides ));
	}

	/// <summary>Deletes the rides, in a frame of their own for the reason <see cref="PlayIn"/> gives.</summary>
	[MethodImpl( MethodImplOptions.NoInlining )]
	private static void Delete( WeakReference rides )
	{
		((Entity)rides.Target!).Delete();
		Entity.ApplyDeletions();
	}

	private static void Collect()
	{
		GC.Collect();
		GC.WaitForPendingFinalizers();
		GC.Collect();
	}
}
