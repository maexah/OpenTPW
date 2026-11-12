namespace OpenTPW;

/// <summary>
/// The running park's research lab and which items it has researched, to which upgrade tier - the researched flag and
/// tier of each of <c>CControlManager</c>'s records (<c>+0x10</c>, <c>+0x14</c>), which the buy list asks before it
/// lists an item (<c>0x004ab023</c>; <c>docs/exe/hud.md</c>, "What the buy list actually filters on").
///
/// <para>
/// <b>At a park load the save's word is the flag</b>: world setup seeds every catalogued item researched when its
/// <c>Upgrades[0].CostOfResearch</c> is nought (<c>0x004d3e92</c>), the save's <c>mObjectControls</c> is laid over
/// all of it (<c>0x005181e7</c>), and an item the save has no record of is seeded again (<c>FUN_00415140</c>). So an
/// item the save holds keeps the save's flag, whatever its file's cost.
/// </para>
///
/// <para>
/// <b>The lab at work</b> (<c>docs/exe/park-engine.md</c>, "The lab at work"): a researcher's hand-out is
/// <see cref="SpendPoints"/>, a project reaching its cost marks its item researched, and the lab appends the item's
/// next upgrade, raises the kind's highest group and picks the kind's next project. A finished project is told to the
/// advisor and the park analyser (<see cref="Researched"/>), a group's raise to the advisor (<see cref="GroupOpened"/>)
/// and the last project's finish to the level (<see cref="AllDone"/>); what it sends the challenge manager is counted
/// where the original sends it.
/// </para>
/// </summary>
public sealed class ParkResearch
{
	private readonly Dictionary<int, (bool Researched, int Tier)> _items = [];

	/// <summary>How many items came from the save's records, and how many were seeded from their files.</summary>
	public int FromSave { get; }

	/// <inheritdoc cref="FromSave"/>
	public int Seeded { get; }

	private readonly List<ParkWorld.ResearchProject> _projects;

	private readonly ParkWorld.ResearchKind[] _kinds;

	private readonly Dictionary<int, ParkItemCatalogue.Item> _catalogue = [];

	private readonly int[] _groupOpensAt;

	/// <summary>The lab's <c>mWorkLoad</c> (<c>+0x1460</c>): the file's, then the research screen's (<see cref="SetWorkLoad"/>).</summary>
	public int WorkLoad { get; private set; }

	/// <summary>
	/// The lab's <c>mMostExpensiveProject</c> (<c>+0x1464</c>), the index of the project a bar is measured against
	/// (<see cref="PercentLeftOfDearest"/>): the file's, or a fresh world's <see cref="MostExpensive"/>. Nothing after
	/// the fill moves it, not an appended upgrade either.
	/// </summary>
	public int MostExpensiveProject { get; }

	/// <summary>Each item's control record's <c>+0x04</c>, what buying one costs as the record was made.</summary>
	private readonly Dictionary<int, int> _prices = [];

	/// <summary>How many hand-outs the lab has had since the park was entered, for the log.</summary>
	public int HandOuts { get; private set; }

	/// <summary>
	/// Whether the player has unlocked an item with a golden-ticket cost (<c>FUN_004d4b70</c>), which a project of
	/// kind 4 asks of the item it upgrades. Null where nobody is playing: nothing is unlocked.
	/// </summary>
	public Func<int, bool>? Unlocked { get; set; }

	/// <summary>
	/// Message <c>0x11</c>'s listeners before the lab's own handler, a project finished: the item, its project's kind
	/// and the tick. They hear it in the order of their thing ids, as every listener set is walked (the shipped park's:
	/// the advisor 3, the park analyser 5, the lab 6, the challenge manager 10); <see cref="ParkState.HearResearched"/> is
	/// the advisor's and the analyser's.
	/// </summary>
	public Action<int, int, int>? Researched { get; set; }

	/// <summary>
	/// A kind's highest group raised with work of the kind left (<c>Lab_RaiseHighestGroup</c>, <c>0x00503c8e</c>): the
	/// kind, 0 to 3, and the tick; <see cref="ParkState.HearGroupOpened"/> posts it to the advisor.
	/// </summary>
	public Action<int, int>? GroupOpened { get; set; }

	/// <summary>
	/// Every project in the table done, after the finish that did it (<c>Lab_OnMessage</c>'s end test, <c>0x00503e15</c>):
	/// the tick. The level marks the park all researched and raises what follows (<see cref="MarkAllResearched"/>).
	/// </summary>
	public Action<int>? AllDone { get; set; }

	/// <summary>
	/// A project finished and its kind's next picked: the kind. The research screen, while open, letters and fills that
	/// kind's panel again (<c>Research_OnKindFinished</c>, <c>0x004aaec0</c>, on the lab's message <c>0x12</c>), and with
	/// nothing left to research stops its pump.
	/// </summary>
	public Action<int>? KindFinished { get; set; }

	/// <param name="records">The save's records, <see cref="ParkWorld.ObjectControlRecords"/>.</param>
	/// <param name="projects">The save's lab's projects, <see cref="ParkWorld.ResearchProjects"/>; none where no file gave any.</param>
	/// <param name="kinds">The save's lab's kinds' records, <see cref="ParkWorld.ResearchKinds"/>; none where no file gave any.</param>
	/// <param name="workLoad">The lab's <c>mWorkLoad</c>, <see cref="ParkWorld.ResearchWorkLoad"/>.</param>
	/// <param name="groupOpensAt">
	/// The share done, in whole per cent, at which each highest group opens the next: <see cref="GroupOpensAt"/>.
	/// Missing entries are nought, as the executable's image holds them.
	/// </param>
	/// <param name="mostExpensiveProject">The lab's <c>mMostExpensiveProject</c>, <see cref="ParkWorld.ResearchMostExpensiveProject"/>.</param>
	public ParkResearch( IEnumerable<ParkWorld.ObjectControl> records, ParkItemCatalogue catalogue,
		IReadOnlyList<ParkWorld.ResearchProject>? projects = null, IReadOnlyList<ParkWorld.ResearchKind>? kinds = null,
		int workLoad = 0, IReadOnlyList<int>? groupOpensAt = null, int mostExpensiveProject = -1 )
	{
		_projects = [.. projects ?? []];
		_kinds = [.. kinds ?? []];
		WorkLoad = workLoad;
		MostExpensiveProject = mostExpensiveProject;
		_groupOpensAt = new int[MostGroup];

		for ( var highest = 0; highest < _groupOpensAt.Length && highest < (groupOpensAt?.Count ?? 0); ++highest )
			_groupOpensAt[highest] = groupOpensAt![highest];

		foreach ( var record in records )
		{
			if ( _items.TryAdd( record.ItemId, (record.Researched, record.TierResearched) ) )
			{
				_prices[record.ItemId] = record.Price;
				++FromSave;
			}
		}

		foreach ( var item in catalogue.All )
		{
			_catalogue.TryAdd( item.Id, item );

			// FUN_004d3e00's record for an item the save has none of: the item's own price.
			_prices.TryAdd( item.Id, item.BuildPrice );

			if ( _items.TryAdd( item.Id, (item.ResearchCost == 0, 0) ) )
				++Seeded;
		}
	}

	/// <summary>How many items are researched and where their flags came from, for the log.</summary>
	public string Summary => $"Research: {_items.Values.Count( entry => entry.Researched )} of {_items.Count} items researched, "
		+ $"{FromSave} from the save's records and {Seeded} seeded from their files; the lab {_projects.Count} projects, "
		+ $"{_projects.Count( IsDone )} done, work load {WorkLoad}, the kinds' current projects "
		+ $"[{string.Join( ",", _kinds.Select( kind => kind.CurrentProject ) )}] and highest groups "
		+ $"[{string.Join( ",", _kinds.Select( kind => kind.HighestGroup ) )}]";

	/// <summary>Whether the item is researched - the flag the buy list lists by. An item nobody knows is not.</summary>
	public bool IsResearched( int itemId ) => _items.TryGetValue( itemId, out var entry ) && entry.Researched;

	/// <summary>The lab's projects as they stand, in the table's order.</summary>
	public IReadOnlyList<ParkWorld.ResearchProject> Projects => _projects;

	/// <summary>The lab's five kinds as they stand.</summary>
	public IReadOnlyList<ParkWorld.ResearchKind> Kinds => _kinds;

	/// <summary>
	/// How many of the lab's projects of <paramref name="kind"/>, a project's <c>mCat</c>, are not done: the
	/// points put to them are under their cost (<c>FUN_00503b30</c>).
	/// </summary>
	public int Unfinished( int kind ) => _projects.Count( project => project.Category == kind && !IsDone( project ) );

	/// <summary>How many of the lab's projects of every kind are not done: <c>FUN_00503b30</c> asked for kind 5, which it takes as any.</summary>
	public int UnfinishedOfAnyKind => _projects.Count( project => !IsDone( project ) );

	/// <summary>A project is done once its points are at or over its cost; a done project stays in the table.</summary>
	private static bool IsDone( ParkWorld.ResearchProject project ) => !(project.Done < project.Cost);

	/// <summary>
	/// How much of the lab's work a kind of research is getting from the researchers on the staff
	/// (<c>FUN_00503980</c>; <c>docs/exe/advisor-park.md</c>, "Every sideshow built, and more to research"): the
	/// kind's output times the share, in whole per cent, that <paramref name="ability"/>, the staff's researchers'
	/// <c>ResearchAbility</c> added up, is of <paramref name="most"/>, the most researchers a park may employ
	/// times the top grade's ability, over a hundred; each division a whole number's. Nought with no such most.
	/// The outputs are the file's or the fresh world's, and the research screen's sliders work them again (<see cref="SetEffort"/>).
	/// </summary>
	public int Rate( int kind, int ability, int most )
		=> most == 0 || kind < 0 || kind >= _kinds.Length ? 0
			: (int)((uint)_kinds[kind].Output * ((uint)(ability * 100) / (uint)most) / 100);

	/// <summary>
	/// The highest group a kind's raise stops below (<c>0x00503c22</c>): the loop leaves at a highest group over 6.
	/// </summary>
	public const int MostGroup = 7;

	/// <summary>How many projects the lab's table holds (<c>FUN_00502dc0</c>).</summary>
	public const int ProjectSlots = 256;

	/// <summary>
	/// The share done at which each highest group opens the next, indexed by the highest group: the dword at
	/// <c>0x00785540 + 4 * highest</c>, which is <c>ResearchTech[highest + 1].PercentageForThisTech</c> (slot 317 is
	/// <c>ResearchTech[0]</c>, <c>0x0078553c</c>). <c>Standard.sam</c> sets the first five; the rest are nought in the
	/// executable's image.
	/// </summary>
	public static int[] GroupOpensAt( ParkBalance? balance )
		=> [.. Enumerable.Range( 1, MostGroup ).Select( tech => balance?.Int( $"ResearchTech[{tech}].PercentageForThisTech" ) ?? 0 )];

	/// <summary>A project finished by a hand-out, for the log and the tests.</summary>
	/// <param name="Project">Its index in the table.</param>
	/// <param name="Raised">The kind's highest group after the raise, or -1 where it was not raised.</param>
	/// <param name="Next">The kind's current project after the pick.</param>
	/// <param name="Appended">The index of the upgrade project appended for the item, or -1.</param>
	/// <param name="AllDone">Whether every project in the table is done after it.</param>
	public readonly record struct Finished( int Project, int ItemId, int Kind, int Tier, float Points, int Raised,
		int Next, int Appended, bool AllDone );

	/// <summary>
	/// One researcher's hand-out to the lab (<c>Lab_SpendPoints</c>, <c>0x00503430</c>): the efforts of the kinds whose
	/// current project is set and not done are added up, once; with none, nothing is spent. Then each kind, 0 to 4,
	/// with an effort and a current project, gets effort / that sum × <see cref="WorkLoad"/> × <paramref name="ability"/>
	/// × 0.01 (the float at <c>0x007007e4</c>), worked at the sweep's 53-bit precision and stored as a float
	/// (<c>0x005034d0</c>), put to its project (<see cref="AddPoints"/>).
	/// </summary>
	/// <returns>The projects this hand-out finished, in the order they finished.</returns>
	public IReadOnlyList<Finished> SpendPoints( int ability, int tick )
	{
		++HandOuts;
		var sum = 0;

		foreach ( var kind in _kinds )
		{
			if ( kind.CurrentProject >= 0 && kind.CurrentProject < _projects.Count && !IsDone( _projects[kind.CurrentProject] ) )
				sum += kind.Effort;
		}

		if ( sum == 0 || _kinds.Length == 0 )
			return [];

		var finished = new List<Finished>();

		for ( var kind = 0; kind < _kinds.Length; ++kind )
		{
			var (effort, current) = (_kinds[kind].Effort, _kinds[kind].CurrentProject);

			if ( effort == 0 || current < 0 || current >= _projects.Count )
				continue;

			// The effort is loaded as an unsigned dword (FILD qword with a nought high half), the rest as signed.
			var share = (float)((double)(uint)effort / sum * WorkLoad * ability * (double)ShareScale);

			if ( AddPoints( current, share, tick ) is { } done )
				finished.Add( done );
		}

		return finished;
	}

	/// <summary>The float a hand-out's share is scaled by, <c>[0x007007e4]</c>.</summary>
	private const float ShareScale = 0.01f;

	/// <summary>
	/// Puts <paramref name="share"/> to a project not yet done (<c>LabProject_AddPoints</c>, <c>0x00504630</c>). The sum
	/// is compared with the cost before it is stored as a float (<c>fcom</c> <c>0x0050466f</c>, <c>fstp</c>
	/// <c>0x00504672</c>), so a sum just under the cost that rounds onto it is kept at the cost and does not finish.
	/// Reaching the cost marks the item's control record researched at the project's tier and sends message
	/// <c>0x11</c> to the four listeners at once: the advisor's and the park analyser's are <see cref="Researched"/>, the
	/// lab's own handler is <see cref="LabOnFinished"/>, and the challenge manager's is counted.
	/// </summary>
	private Finished? AddPoints( int index, float share, int tick )
	{
		var project = _projects[index];

		if ( IsDone( project ) )
			return null;

		var sum = (double)project.Done + share;
		_projects[index] = project with { Done = (float)sum };

		if ( sum < project.Cost )
			return null;

		_items[project.ItemId] = (true, project.UpgradeLevel);

		// The advisor and the park analyser, then the lab, then the challenge manager.
		Researched?.Invoke( project.ItemId, project.Category, tick );

		var (appended, raised, next, allDone) = LabOnFinished( project, tick );

		// A ride researched is challenge type 0x10 (ChallengeManager_OnMessage 0x004d2860): no challenge is built.
		if ( project.Category == ItemDescriptionFile.Ride )
			Unimplemented.Report( "RESEARCH_FINISHED_TO_THE_CHALLENGES" );
		var result = new Finished( index, project.ItemId, project.Category, project.UpgradeLevel, (float)sum, raised, next,
			appended, allDone );

		Log.Info( $"Research: hand-out {HandOuts} on mGameTick {tick} finishes project {index}, item {project.ItemId} "
			+ $"(kind {project.Category}, tier {project.UpgradeLevel}), at {(float)sum:0.###} of {project.Cost:0.###}; "
			+ (raised >= 0 ? $"the kind's highest group raised to {raised}; " : "")
			+ (appended >= 0 ? $"its next upgrade appended as project {appended}; " : "")
			+ $"the kind's next project {next}; {_projects.Count( IsDone )} of {_projects.Count} done" );

		return result;
	}

	/// <summary>
	/// The lab's handler of message <c>0x11</c> (<c>Lab_OnMessage</c>, <c>0x00503d40</c>): the item's next upgrade
	/// appended (<see cref="MakeNextUpgrade"/>), the kind's highest group raised (<see cref="RaiseHighestGroup"/>),
	/// message <c>0x12</c>'s pick of the kind's next project (<c>Research_OnKindFinished</c>, <c>0x004aaec0</c>:
	/// unless the kind's current project is set and open, <see cref="PickNextProject"/>), then the end test.
	/// </summary>
	private (int Appended, int Raised, int Next, bool AllDone) LabOnFinished( ParkWorld.ResearchProject project, int tick )
	{
		var appended = -1;

		if ( _catalogue.TryGetValue( project.ItemId, out var item ) && MakeNextUpgrade( item ) is { } upgrade )
		{
			// The original writes the table's next slot unchecked (0x00503da2); a full table takes no more.
			if ( _projects.Count < ProjectSlots )
			{
				appended = _projects.Count;
				_projects.Add( upgrade );
			}
			else
				Log.Warning( $"Research: the lab's {ProjectSlots} projects are full; item {item.Id}'s upgrade is not appended" );
		}

		var kind = project.Category;

		if ( kind < 0 || kind >= _kinds.Length )
		{
			Log.Warning( $"Research: project of item {project.ItemId} is of kind {kind}, which the lab keeps no record of" );
			return (appended, -1, -1, _projects.All( IsDone ));
		}

		var raised = RaiseHighestGroup( kind, tick ) ? _kinds[kind].HighestGroup : -1;

		if ( !KindHasOpenProject( kind ) )
			PickNextProject( kind );

		KindFinished?.Invoke( kind );

		var allDone = _projects.All( IsDone );

		if ( allDone )
			AllDone?.Invoke( tick );

		return (appended, raised, _kinds[kind].CurrentProject, allDone);
	}

	/// <summary>
	/// The player's half of the end of research (<c>0x00503e58</c>..<c>0x00503f77</c>): the current park's record in the
	/// player's progress is marked all researched (<c>PlayerProgress_MarkParkAllResearched</c>, <c>0x005b0ca0</c>: its
	/// <c>mAllResearchCompleted</c>, which <c>gms.dat</c> keeps), and the answer is whether advisor row 167 and the End
	/// feature follow: in Instant Action always; in Full Simulation only when every park record the player holds is
	/// marked (<c>PlayerProgress_AllParksAllResearched</c>, <c>0x005b0c40</c>; none held is every one). With nobody
	/// playing nothing is marked, and the park is Instant Action's.
	/// </summary>
	public static bool MarkAllResearched( PlayerFile? progress, string theme, bool instantAction )
	{
		if ( progress is not null )
			progress.Park( theme ).AllResearchCompleted = true;

		return instantAction || progress is null || progress.Parks.All( park => park.AllResearchCompleted );
	}

	/// <summary>
	/// The item's next upgrade project (<c>LabProject_MakeNextUpgrade</c>, <c>0x00504580</c>): for an item whose own
	/// <c>Research.Category</c> is not 4, the tiers after its control record's, while under 3. A tier whose
	/// <c>Upgrades[tier].CostOfResearch</c> is above nought is the project, kind 4, group 0, at that cost; a tier that
	/// costs nothing is written to the record as researched and the next looked at.
	/// </summary>
	private ParkWorld.ResearchProject? MakeNextUpgrade( ParkItemCatalogue.Item item )
	{
		if ( item.ResearchCategory == Upgrade || !_items.TryGetValue( item.Id, out var record ) )
			return null;

		for ( var tier = record.Tier + 1; tier < Tiers; ++tier )
		{
			var cost = tier == 1 ? item.UpgradeResearchCost1 : item.UpgradeResearchCost2;

			if ( cost > 0 )
				return new ParkWorld.ResearchProject( item.Id, Upgrade, 0, tier, cost, 0 );

			_items[item.Id] = _items[item.Id] with { Tier = tier };
		}

		return null;
	}

	/// <summary>How many upgrade tiers an item has, 0 to 2.</summary>
	private const int Tiers = 3;

	/// <summary>
	/// <c>Lab_RaiseHighestGroup</c> (<c>0x00503b80</c>): while the kind's projects at or under its highest group are
	/// done, in whole per cent, at least <see cref="GroupOpensAt"/> of the highest, and the highest is under 7, the
	/// highest goes up one. A raise with any of the kind's projects not done tells <see cref="GroupOpened"/> for kinds 0
	/// to 3 (advisor message <c>0x85</c> + kind, rows 133 to 136).
	/// </summary>
	/// <returns>Whether the highest group was raised.</returns>
	private bool RaiseHighestGroup( int kind, int tick )
	{
		var highest = RaisedGroup( _projects, kind, _kinds[kind].HighestGroup, _groupOpensAt );
		var raised = highest != _kinds[kind].HighestGroup;
		_kinds[kind] = _kinds[kind] with { HighestGroup = highest };

		if ( raised && kind <= ItemDescriptionFile.Feature && _projects.Any( project => project.Category == kind && !IsDone( project ) ) )
			GroupOpened?.Invoke( kind, tick );

		return raised;
	}

	/// <summary>The raise's loop alone: the kind's highest group after it.</summary>
	private static int RaisedGroup( IReadOnlyList<ParkWorld.ResearchProject> projects, int kind, int highest,
		IReadOnlyList<int> groupOpensAt )
	{
		while ( true )
		{
			var under = projects.Where( project => project.Category == kind && project.Group <= highest ).ToArray();
			var share = under.Length == 0 ? 0u : (uint)(under.Count( IsDone ) * 100) / (uint)under.Length;

			if ( highest >= MostGroup || share < (uint)groupOpensAt[highest] )
				return highest;

			++highest;
		}
	}

	/// <summary>Whether the kind's current project is set and not done (<c>Lab_KindHasOpenProject</c>, <c>0x00504040</c>).</summary>
	private bool KindHasOpenProject( int kind )
		=> _kinds[kind].CurrentProject is var current && current >= 0 && current < _projects.Count && !IsDone( _projects[current] );

	/// <summary>
	/// <c>Lab_PickNextProject</c> (<c>0x005034f0</c>): the first project of the kind, not done and at or under its
	/// highest group, after the current one; else the first such before it; else none, the current set to -1 where it
	/// is done. A project of kind 4 is taken only while the item it upgrades (<c>AddOn.UpgradesId</c>, <c>+0x284</c>,
	/// or the item itself) is researched and, where that item has a golden-ticket cost, unlocked.
	/// </summary>
	private void PickNextProject( int kind )
	{
		var current = _kinds[kind].CurrentProject;
		var before = -1;

		for ( var i = 0; i < _projects.Count; ++i )
		{
			var project = _projects[i];

			if ( project.Category != kind || project.Group > _kinds[kind].HighestGroup || IsDone( project ) || !Eligible( project ) )
				continue;

			if ( i < current )
			{
				if ( before < 0 )
					before = i;
			}
			else if ( i > current )
			{
				_kinds[kind] = _kinds[kind] with { CurrentProject = i };
				return;
			}
		}

		if ( before >= 0 )
			_kinds[kind] = _kinds[kind] with { CurrentProject = before };
		else if ( current >= 0 && current < _projects.Count && IsDone( _projects[current] ) )
			_kinds[kind] = _kinds[kind] with { CurrentProject = -1 };
	}

	private bool Eligible( ParkWorld.ResearchProject project )
	{
		if ( project.Category != Upgrade )
			return true;

		var upgrades = _catalogue.TryGetValue( project.ItemId, out var item ) && item.AddOnUpgradesId != 0
			? item.AddOnUpgradesId : project.ItemId;

		if ( !IsResearched( upgrades ) )
			return false;

		return !(_catalogue.TryGetValue( upgrades, out var upgraded ) && upgraded.GoldenTicketCost > 0
			&& Unlocked?.Invoke( upgrades ) != true);
	}

	/// <summary>
	/// The lab and the control records' research as they stand, for the park file (<see cref="ParkWorld.PutLab"/>).
	/// </summary>
	public ParkWorld.WrittenLab Written
		=> new( [.. _projects], [.. _kinds], _items.ToDictionary( entry => entry.Key, entry => (entry.Value.Researched, entry.Value.Tier) ),
			WorkLoad, MostExpensiveProject );

	/// <summary>
	/// A fresh world's five kinds' records (<c>Lab_SetupKinds</c>, <c>FUN_00503330</c>): each kind's effort is its
	/// <c>ResearchCategories[kind].Effort</c> held to 100, and its output that effort's share of the five in whole
	/// per cent, times <paramref name="workLoad"/>, <c>Research.StartingWorkLoad</c>, over a hundred; nought
	/// where the efforts add up to nothing. Each kind starts at highest group 1 (<c>FUN_00502d20</c>); its current
	/// project is the first of the kind not done at or under that group, and the group is then raised as a finished
	/// project raises it (<see cref="GroupOpensAt"/>; a raise there would post the advisor's row, which is not counted).
	/// </summary>
	public static IReadOnlyList<ParkWorld.ResearchKind> FreshKinds( IEnumerable<int> efforts, int workLoad,
		IReadOnlyList<ParkWorld.ResearchProject>? projects = null, IReadOnlyList<int>? groupOpensAt = null )
	{
		var held = efforts.Select( effort => (uint)effort > 99 ? 100u : (uint)effort ).ToArray();
		var sum = (uint)held.Sum( effort => effort );
		projects ??= [];
		var opens = new int[MostGroup];

		for ( var highest = 0; highest < opens.Length && highest < (groupOpensAt?.Count ?? 0); ++highest )
			opens[highest] = groupOpensAt![highest];

		return held.Select( ( effort, kind ) =>
		{
			var current = Enumerable.Range( 0, projects.Count ).FirstOrDefault( i => projects[i].Category == kind
				&& projects[i].Group <= FreshHighestGroup && !IsDone( projects[i] ), -1 );

			return new ParkWorld.ResearchKind( (int)effort, Output( effort, sum, workLoad ), current,
				RaisedGroup( projects, kind, FreshHighestGroup, opens ) );
		} ).ToArray();
	}

	/// <summary>A kind's output: its effort's share of the efforts' sum in whole per cent, times the work load, over a hundred, all unsigned; nought where the sum is.</summary>
	private static int Output( uint effort, uint sum, int workLoad )
		=> sum == 0 ? 0 : (int)(effort * 100 / sum * (uint)workLoad / 100);

	/// <summary>
	/// A fresh world's <c>mMostExpensiveProject</c> (the fill, <c>0x00503225</c> and <c>0x005032f4</c>): the first
	/// project strictly dearer than every one before it, -1 for none.
	/// </summary>
	public static int MostExpensive( IReadOnlyList<ParkWorld.ResearchProject> projects )
	{
		var dearest = -1;

		for ( var i = 0; i < projects.Count; ++i )
		{
			if ( dearest < 0 || projects[dearest].Cost < projects[i].Cost )
				dearest = i;
		}

		return dearest;
	}

	/// <summary>
	/// <c>Lab_SetEffort</c> (<c>0x00503870</c>), the research screen's effort slider: the kind's effort, held to 100,
	/// and every kind's output worked out again by the work load standing. Answers the effort kept.
	/// </summary>
	public int SetEffort( int kind, int effort )
	{
		if ( kind < 0 || kind >= _kinds.Length )
			return 0;

		_kinds[kind] = _kinds[kind] with { Effort = (uint)effort > 99 ? 100 : effort };
		RecomputeOutputs();
		return _kinds[kind].Effort;
	}

	/// <summary>
	/// <c>Lab_SetWorkLoad</c> (<c>0x00503900</c>), the research screen's work-load slider: <c>mWorkLoad</c>, held to
	/// 100, and every kind's output worked out again by it.
	/// </summary>
	public void SetWorkLoad( int workLoad )
	{
		WorkLoad = (uint)workLoad > 99 ? 100 : workLoad;
		RecomputeOutputs();
	}

	private void RecomputeOutputs()
	{
		var sum = 0u;

		foreach ( var kind in _kinds )
			sum += (uint)kind.Effort;

		for ( var kind = 0; kind < _kinds.Length; ++kind )
			_kinds[kind] = _kinds[kind] with { Output = Output( (uint)_kinds[kind].Effort, sum, WorkLoad ) };
	}

	/// <summary>The catalogue's item of that id (<c>FUN_00412e90</c>), or null where it holds none.</summary>
	public ParkItemCatalogue.Item? Item( int itemId ) => _catalogue.TryGetValue( itemId, out var item ) ? item : null;

	/// <summary>The item of the kind's current project, done or not, or nought with none (<c>Lab_KindCurrentItem</c>, <c>0x005039f0</c>).</summary>
	public int CurrentItem( int kind ) => CurrentProjectOf( kind )?.ItemId ?? 0;

	/// <summary>The kind's current project, done or not; null with none.</summary>
	private ParkWorld.ResearchProject? CurrentProjectOf( int kind )
		=> kind >= 0 && kind < _kinds.Length && _kinds[kind].CurrentProject is var current && current >= 0 && current < _projects.Count
			? _projects[current] : null;

	/// <summary>
	/// <c>Lab_PercentLeftOfDearest</c> (<c>0x00503ac0</c>): the points left in the kind's current project as a per cent
	/// of the dearest project's cost, <c>(mCost - mResearchDone) × 100 / dearest</c> in the FPU's doubles and cut
	/// toward nought (<c>__ftol</c>). Nought with no current project, a cost of 0.1 or less, or a dearest cost of 1.0
	/// or less.
	/// </summary>
	public int PercentLeftOfDearest( int kind )
	{
		// A dearest index outside the table, which no file or fresh world holds, reads as no dearest project.
		if ( CurrentProjectOf( kind ) is not { } project || MostExpensiveProject < 0 || MostExpensiveProject >= _projects.Count )
			return 0;

		var dearest = _projects[MostExpensiveProject].Cost;

		if ( !(project.Cost > 0.1f) || !(dearest > 1.0f) )
			return 0;

		return (int)(((double)project.Cost - project.Done) * 100.0 / dearest);
	}

	/// <summary>
	/// <c>Lab_KindEstimatedPrice</c> (<c>0x00503a20</c>), what the research screen's bar letters: for kinds 0 to 3 the
	/// current project's item's control record's price, and for kind 4 the <c>CostOfUpgrade</c> of the project's own
	/// item at the project's tier. Nought with no current project.
	/// </summary>
	public int EstimatedPrice( int kind )
	{
		if ( CurrentProjectOf( kind ) is not { } project )
			return 0;

		if ( kind == Upgrade )
			return _catalogue.TryGetValue( project.ItemId, out var item ) ? item.UpgradeCostAt( project.UpgradeLevel ) : 0;

		return _prices.GetValueOrDefault( project.ItemId );
	}

	/// <summary>The research screen's right arrow: <see cref="PickNextProject"/>.</summary>
	public void PickNext( int kind )
	{
		if ( kind >= 0 && kind < _kinds.Length )
			PickNextProject( kind );
	}

	/// <summary>
	/// The research screen's left arrow, <c>Lab_PickPreviousProject</c> (<c>0x005036a0</c>): from the table's end down,
	/// the nearest project of the kind open, at or under its highest group and taken by the same test as forward's
	/// (<see cref="Eligible"/>) before the current one; else the last such after it; else none, the current set to -1
	/// where it is done.
	/// </summary>
	public void PickPrevious( int kind )
	{
		if ( kind < 0 || kind >= _kinds.Length )
			return;

		var current = _kinds[kind].CurrentProject;
		var after = -1;

		for ( var i = _projects.Count - 1; i >= 0; --i )
		{
			var project = _projects[i];

			if ( project.Category != kind || project.Group > _kinds[kind].HighestGroup || IsDone( project ) || !Eligible( project ) )
				continue;

			if ( i > current )
			{
				if ( after < 0 )
					after = i;
			}
			else if ( i < current )
			{
				_kinds[kind] = _kinds[kind] with { CurrentProject = i };
				return;
			}
		}

		if ( after >= 0 )
			_kinds[kind] = _kinds[kind] with { CurrentProject = after };
		else if ( current >= 0 && current < _projects.Count && IsDone( _projects[current] ) )
			_kinds[kind] = _kinds[kind] with { CurrentProject = -1 };
	}

	/// <summary>The highest group each kind of a fresh world starts at (<c>FUN_00502d20</c>).</summary>
	public const int FreshHighestGroup = 1;

	/// <summary>The kind of research an upgrade's project is, whatever its item's own (<c>0x005045fc</c>).</summary>
	public const int Upgrade = 4;

	/// <summary>
	/// The lab of a world just made, and the upgrade tier each item's control record is left at: world setup's fill
	/// (<c>FUN_005031a0</c>; <c>docs/exe/park-engine.md</c>, "A fresh world's research lab"). Every item a buy list
	/// can show (<c>Info.WhichUIType</c> under 4), in the catalogue's order, gives at most one project: its own
	/// research where that costs anything, and otherwise the first later tier whose research costs anything, as a
	/// project of kind 4. A tier costing nothing is passed over and counted researched (<c>FUN_00504580</c>), so an
	/// item with no upgrade is left at tier 2; an item of kind 4 has no upgrade and is left at 0.
	/// </summary>
	public static (IReadOnlyList<ParkWorld.ResearchProject> Projects, IReadOnlyDictionary<int, int> Tiers) FreshLab(
		IEnumerable<ParkItemCatalogue.Item> items )
	{
		var projects = new List<ParkWorld.ResearchProject>();
		var tiers = new Dictionary<int, int>();

		foreach ( var item in items.Where( item => item.UiType < Upgrade ) )
		{
			if ( item.ResearchCost != 0 )
			{
				projects.Add( new( item.Id, item.ResearchCategory, item.ResearchGroup, 0, item.ResearchCost, 0 ) );
				continue;
			}

			if ( item.ResearchCategory == Upgrade )
				continue;

			int[] later = [item.UpgradeResearchCost1, item.UpgradeResearchCost2];
			var next = Array.FindIndex( later, cost => cost > 0 );
			tiers[item.Id] = next < 0 ? later.Length : next;

			if ( next >= 0 )
				projects.Add( new( item.Id, Upgrade, 0, next + 1, later[next], 0 ) );
		}

		return (projects, tiers);
	}

	/// <summary>The upgrade tier researched, 0 to 2 (<c>+0x14</c>), which a finished item's next upgrade starts after.</summary>
	public int TierResearched( int itemId ) => _items.TryGetValue( itemId, out var entry ) ? entry.Tier : 0;

	/// <summary>The item's control record's price (<c>+0x04</c>): the save's, or the item's own build price for one it lacks. Nought for an item nobody knows.</summary>
	public int Price( int itemId ) => _prices.GetValueOrDefault( itemId );
}
