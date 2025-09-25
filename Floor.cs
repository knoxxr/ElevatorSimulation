using System.Reflection.Metadata;

namespace knoxxr.Evelvator.Core
{
    public class Floor
    {
        public event EventHandler<FloorEventArgs> EventReqUp;
        public event EventHandler<FloorEventArgs> EventReqDown;
        public event EventHandler<FloorEventArgs> EventCancelUp;
        public event EventHandler<FloorEventArgs> EventCancelDown;
        public static readonly int Height = 3000; // 층당 높이 (mm)
        public readonly int myFloor;

        public Button BtnUp = new Button();
        public Button BtnDown = new Button();

        public Floor(int floor)
        {
            myFloor = floor;
        }
        public void ReqUpSide()
        {
            BtnUp.Press();
            OnReqUp(new FloorEventArgs(myFloor));
        }

        public void CancelUpSide()
        {
            BtnUp.Cancel();
            OnCancelUp(new FloorEventArgs(myFloor));
        }

        public void ReqDownSide()
        {
            BtnDown.Press();
            OnReqDown(new FloorEventArgs(myFloor));
        }

        public void CancelDownSide()
        {
            BtnDown.Cancel();
            OnCancelDown(new FloorEventArgs(myFloor));
        }

        public void OnReqUp(FloorEventArgs e)
        {
            FloorEventArgs args = new FloorEventArgs(myFloor);
            EventReqUp?.Invoke(this, args);
        }

        public void OnReqDown(FloorEventArgs e)
        {
            FloorEventArgs args = new FloorEventArgs(myFloor);
            EventReqDown?.Invoke(this, args);
        }

        public void OnCancelUp(FloorEventArgs e)
        {
            FloorEventArgs args = new FloorEventArgs(myFloor);
            EventCancelUp?.Invoke(this, args);
        }

        public void OnCancelDown(FloorEventArgs e)
        {
            FloorEventArgs args = new FloorEventArgs(myFloor);
            EventCancelDown?.Invoke(this, args);
        }
    }

    public class FloorEventArgs : EventArgs
    {
        public int ReqFloor { get; }

        public FloorEventArgs(int reqFloor)
        {
            ReqFloor = reqFloor;
        }
    }
}