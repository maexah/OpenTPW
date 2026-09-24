namespace OpenTPW;

/// <summary>
/// What the player is holding - the original's one current interaction mode (<c>DAT_007b05d8</c>). Here it is
/// four pieces of state, each kept by its owner: the armed build tool (<see cref="ParkBuildMode"/>), an item
/// bought or moved (<see cref="ParkBuilding.Carrying"/>), a candidate off the hire screen
/// (<see cref="ParkStaffPool.Carrying"/>) and a worker picked up (<see cref="ParkPeople.CarriedStaff"/>).
/// </summary>
/// <remarks>
/// <b>At most one is held at a time.</b> The original's setter <c>FUN_0046c350</c> runs the outgoing mode's
/// uninstall before it installs the next, so whatever installs a mode of its own - a buy, a hire, a move, the
/// queue button, a worker's pickup, the camcorder, the Delete key's Clear Land - calls <see cref="LetGo"/> first.
/// Every way out that is not a drop - a quick right click with RMB cancel on, Escape, leaving the park, and a sale
/// made with no tool in hand - installs the idle mode, which is <see cref="LetGo"/> as well. See
/// <c>docs/exe/park-engine.md</c>, "The hand's ways out".
/// </remarks>
internal static class ParkHand
{
	/// <summary>Whether no item, candidate or worker is held. A build tool may still be armed.</summary>
	public static bool Empty
		=> ParkBuilding.Carrying == 0 && ParkStaffPool.Carrying == 0 && ParkPeople.Current is not { CarriedStaff: not 0 };

	/// <summary>
	/// The idle mode installed over whatever is current, which runs the outgoing mode's uninstall and ends the
	/// tool (<c>FUN_0052f200( 0, 1 )</c>): an item is let go of with nothing built and nothing refunded, so a
	/// moved thing stays sold; a candidate goes back to the pool; a worker is put down in the cell they stand in;
	/// and a build tool is put away. Answers what it let go of, or null when nothing was held or armed.
	/// </summary>
	public static string? LetGo()
	{
		var what = new List<string>();

		if ( ParkBuilding.Carrying != 0 )
			what.Add( ParkBuilding.Drop() );

		if ( ParkStaffPool.Carrying != 0 )
			what.Add( ParkStaffPool.Drop() );

		if ( ParkPeople.Current is { CarriedStaff: not 0 } people )
			what.Add( people.PutBack() );

		if ( ParkBuildMode.Current != ParkBuildMode.None )
		{
			ParkBuildMode.Disarm();
			what.Add( "the build tool is put away" );
		}

		return what.Count > 0 ? string.Join( "; ", what ) : null;
	}

	/// <summary>Everything the hand holds, for the debug console.</summary>
	public static string Census()
		=> $"hand: item {ParkBuilding.Carrying} turned {ParkBuilding.CarryingAngle}, candidate {ParkStaffPool.Carrying}, " +
			$"worker {ParkPeople.Current?.CarriedStaff ?? 0}, tool {ParkBuildMode.Current}";
}
