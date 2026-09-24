namespace OpenTPW;

public static partial class Input
{
	public struct MouseInfo
	{
		public Vector2 Delta;
		public Vector2 Position;

		public bool Left;
		public bool Right;

		/// <summary>
		/// Whether the left button went down this frame: a press the window system sent, as the original acts on
		/// WM_LBUTTONDOWN (0x10005). A button already held when the frame began, or when the interface was last
		/// looking, is not one.
		/// </summary>
		public bool LeftWentDown;

		public float Wheel;

		public override string ToString()
		{
			var str = "";

			foreach ( var field in typeof( MouseInfo ).GetFields() )
				str += $"{field.Name}: {field.GetValue( this )}\n";

			return str;
		}
	}
}
