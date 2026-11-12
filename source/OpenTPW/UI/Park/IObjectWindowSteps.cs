namespace OpenTPW.UI;

/// <summary>
/// An object window that steps to the next or the previous thing of its class, as its arrows do and as the cursor Up
/// and Down keys do through the base proc <c>FUN_0048d200</c> (<see cref="ParkFrontEnd.ObjectWindowKeys"/>;
/// <c>docs/exe/hud.md</c>, "The shop, sideshow and ride windows"). Each window's own arrows reach the same step.
/// </summary>
internal interface IObjectWindowSteps
{
	/// <summary>
	/// <c>FUN_0048cbe0</c> with <paramref name="newer"/> (the next towards the newest, round from the oldest), else
	/// <c>FUN_0048caf0</c> (the next towards the oldest, round from the newest); each applies the window's buffered
	/// settings first where it has any.
	/// </summary>
	void Step( bool newer );
}
