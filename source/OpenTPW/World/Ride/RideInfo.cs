namespace OpenTPW;

/// <summary>
/// The fields of a ride's <c>.sam</c> record, under the original's own names for them. <b>Nothing reads
/// this.</b> It has no constructor and no reader anywhere in the tree, and belongs to the unbuilt ride
/// runtime described in <see cref="Ride"/> - kept as a record of the field set, not as working code.
/// </summary>
struct RideInfo
{
	public int Id { get; set; }
	public string Name { get; set; }
	public string Shape { get; set; }
	public string Hoarding { get; set; }
	public int EngineMapOffsetOverrideX { get; set; }
	public int EngineMapOffsetOverrideY { get; set; }
	public int EngineFootprintWidthOverride { get; set; }
	public int EngineFootprintHeightOverride { get; set; }
	public int MapOffsetX { get; set; }
	public int MapOffsetY { get; set; }
	public string DontApplyOffset { get; set; }
	public bool IsChoosable { get; set; }
	public bool HasQueue { get; set; }
	public string CanBreakDown { get; set; }
	public string RunsContinuously { get; set; }
	public int WhichUIType { get; set; }
	public string DontDeformBase { get; set; }
	public int OverwritePriority { get; set; }
	public int AttractionValue { get; set; }
	public int NewAttractionDecayTime { get; set; }
	public int CreateParticleEffect { get; set; }
	public int DestroyParticleEffect { get; set; }
	public int RepairParticleEffect { get; set; }
	public int UpgradeParticleEffect { get; set; }
	public string DurationUnit { get; set; }
	public bool UsesCars { get; set; }
	public int PreviewAnimType { get; set; }
	public int PreviewAnimNum { get; set; }
	public bool FirstPersonSprites { get; set; }
	public bool DoHeadProcessing { get; set; }
	public bool TurnOffAnimatingTextures { get; set; }
	public bool TurnOffScrollingTextures { get; set; }
	public int RideTypeStringIndex { get; set; }
	public int DownloadedRideNumber { get; set; }
	public bool IsPreviewRide { get; set; }
}
