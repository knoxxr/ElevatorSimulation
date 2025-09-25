namespace knoxxr.Evelvator.Core
{
    public class Floor
    {
        public event EventHandler<FloorEventArgs> EventReqUp;
        public event EventHandler<FloorEventArgs> EventReqDown;
        public event EventHandler<FloorEventArgs> EventCancelUp;
        public event EventHandler<FloorEventArgs> EventCancelDown;
        public int Height = 3;
        public readonly int myFloor;

        public Floor(int floor)
        {
            myFloor = floor;
        }
        public void ReqUpSide(int floor)
        {

        }

        public void CancelUpSide()
        {

        }

        public void ReqDownSide(int floor)
        {

        }

        public void CancelDownSide(int floor)
        {

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