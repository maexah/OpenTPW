namespace OpenTPW;

/// <summary>The read-only seed for a running park: either a save file or a newly initialized world.</summary>
public interface IParkInitialState
{
	/// <summary>Serialized modules exist only when this seed came from a file.</summary>
	ParkWorld? Save { get; }
	IReadOnlyList<ParkThingIdentity> Things { get; }
	IReadOnlyList<ParkWorld.CatalogueObject> Objects { get; }
	ParkWorld.EconomyState? Economy { get; }
	ParkWorld.StaffHqState? StaffHq { get; }
	IReadOnlyList<ParkWorld.Person> People { get; }
	IReadOnlyList<ParkWorld.Sprite> Sprites { get; }
	IReadOnlyList<ParkWorld.MapCell> Cells { get; }
	ParkWorld.MapCell CellAt( int x, int y );
	int ParkGates { get; }
	int TrafficLights { get; }
	int FirstObject { get; }
	int RandomSeed { get; }
	int Weather { get; }
	int ParkClosed { get; }
	int GameTick { get; }
	int BankAccount { get; }
	int NumberOfVisitorsToDate { get; }
	int WorldState { get; }
	int ThingCount { get; }
	int ArrivalVehicleForSmallCrowd { get; }
	int ArrivalVehicleForMediumCrowd { get; }
	int ArrivalVehicleForLargeCrowd { get; }
	int CurrentArrivalVehicle { get; }
	ParkWorld.ArrivalBlock Arrival { get; }
	IReadOnlyList<ParkWorld.ObjectControl> ObjectControlRecords { get; }
}

/// <summary>A reserved world-table slot, including managers that have no drawable object.</summary>
public readonly record struct ParkThingIdentity( int ThingId, int Model );
