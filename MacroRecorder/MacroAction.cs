namespace MacroRecorderPro.Models
{
    public enum ActionType
    {
        Keyboard = 0,
        Mouse = 1
    }

    public enum MouseButton
    {
        None = 0,
        Move = 0,
        Left = 1,
        Right = 2,
        Middle = 3,
        Wheel = 4
    }

    public class MacroAction
    {
        public ActionType Type { get; set; }
        public long TimeTicks { get; set; }
        public int Key { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public MouseButton Button { get; set; }
        public bool Down { get; set; }
        public int WheelDelta { get; set; }

        public MacroAction Clone()
        {
            return new MacroAction
            {
                Type = this.Type,
                TimeTicks = this.TimeTicks,
                Key = this.Key,
                X = this.X,
                Y = this.Y,
                Button = this.Button,
                Down = this.Down,
                WheelDelta = this.WheelDelta
            };
        }
    }
}