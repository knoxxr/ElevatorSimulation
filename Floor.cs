using System.Reflection.Metadata;

namespace knoxxr.Evelvator.Core
{
    public class Floor
    {
        public event EventHandler<FloorEventArgs> EventReqUp;
        public event EventHandler<FloorEventArgs> EventReqDown;
        public event EventHandler<FloorEventArgs> EventCancelUp;
        public event EventHandler<FloorEventArgs> EventCancelDown;
        public static readonly int Height = 3200; // 층당 높이 (mm)
        public int Position; // 층 위치 (mm)
        public int FloorNo;

        public Button BtnUp = new Button();
        public Button BtnDown = new Button();

        public Floor(int floorNo)
        {
            FloorNo = floorNo;
            Position = (floorNo -1) * Height;
        }
        public void ReqUpSide()
        {
            BtnUp.Press();
            OnReqUp(new FloorEventArgs(this));
            Console.WriteLine($"Floor {FloorNo} UP button pressed.");
        }

        public void CancelUpSide()
        {
            BtnUp.Cancel();
            OnCancelUp(new FloorEventArgs(this));
            Console.WriteLine($"Floor {FloorNo} UP button canceled.");
        }

        public void ReqDownSide()
        {
            BtnDown.Press();
            OnReqDown(new FloorEventArgs(this));
            Console.WriteLine($"Floor {FloorNo} DOWN button pressed.");
        }

        public void CancelDownSide()
        {
            BtnDown.Cancel();
            OnCancelDown(new FloorEventArgs(this));
            Console.WriteLine($"Floor {FloorNo} DOWN button canceled.");
        }

        public void OnReqUp(FloorEventArgs e)
        {
            FloorEventArgs args = new FloorEventArgs(this);
            EventReqUp?.Invoke(this, args);
            Console.WriteLine($"Floor {FloorNo} UP button pressed.");
        }

        public void OnReqDown(FloorEventArgs e)
        {
            FloorEventArgs args = new FloorEventArgs(this);
            EventReqDown?.Invoke(this, args);
            Console.WriteLine($"Floor {FloorNo} DOWN button pressed.");
        }

        public void OnCancelUp(FloorEventArgs e)
        {
            FloorEventArgs args = new FloorEventArgs(this);
            EventCancelUp?.Invoke(this, args);
            Console.WriteLine($"Floor {FloorNo} UP button canceled.");
        }

        public void OnCancelDown(FloorEventArgs e)
        {
            FloorEventArgs args = new FloorEventArgs(this);
            EventCancelDown?.Invoke(this, args);
            Console.WriteLine($"Floor {FloorNo} DOWN button canceled.");
        }
    }

    public class FloorEventArgs : EventArgs
    {
        public Floor ReqFloor { get; }

        public FloorEventArgs(Floor reqFloor)
        {
            ReqFloor = reqFloor;
        }
    }
}