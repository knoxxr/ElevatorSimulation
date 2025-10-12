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
        protected int _floorNo;
        public int FloorNo
        {
            get { return _floorNo; }
        }


        public Dictionary<int, Elevator> _elevators;

        public ElevatorManager _eleMgr = null;

        public Button BtnUp = new Button();
        public Button BtnDown = new Button();

        public Floor(int floorNo)
        {
            _floorNo = floorNo;
            Position = (floorNo - 1) * Height;
        }

        public void Initialize(ElevatorManager elevatorManager)
        {
            _eleMgr = elevatorManager;
            InitiaizeElevatorEvent(_eleMgr._elevators);
        }
        protected void InitiaizeElevatorEvent(Dictionary<int, Elevator> elevators)
        {
            foreach (var ele in _eleMgr._elevators.Values)
            {
                ele.EventArrivedFloor += Elevator_OnArrivedFloor;
                ele.EventDoorOpened += Elevator_OnDoorOpened;
                ele.EventDoorClosed += Elevator_OnDoorClosed;
            }
        }
        private void Elevator_OnArrivedFloor(object sender, EventArgs e)
        {
            Elevator ele = ((ElevatorEventArgs)e).Elevator;
            if (ele._currentFloor == this)
            {
                Logger.Info($"[Floor {_floorNo}] 엘리베이터 {ele._Id}이(가) 도착했습니다.");
            }
        }
        private void Elevator_OnDoorOpened(object sender, EventArgs e)
        {
            Elevator ele = ((ElevatorEventArgs)e).Elevator;
            if (ele._currentFloor == this)
            {
                Logger.Info($"[Floor {_floorNo}] 엘리베이터 {ele._Id}의 문이 열렸습니다.");
            }
        }
        private void Elevator_OnDoorClosed(object sender, EventArgs e)
        {
            Elevator ele = ((ElevatorEventArgs)e).Elevator;
            if (ele._currentFloor == this)
            {
                Logger.Info($"[Floor {_floorNo}] 엘리베이터 {ele._Id}의 문이 닫혔습니다.");
            }
        }

        public void ReqUpSide()
        {
            BtnUp.Press();
            OnReqUp(new FloorEventArgs(this));
            Logger.Info($"Floor {_floorNo} UP button pressed.");
        }

        public void CancelUpSide()
        {
            BtnUp.Cancel();
            OnCancelUp(new FloorEventArgs(this));
           // Console.WriteLine($"Floor {FloorNo} UP button canceled.");
        }

        public void ReqDownSide()
        {
            BtnDown.Press();
            OnReqDown(new FloorEventArgs(this));
            Logger.Info($"Floor {_floorNo} DOWN button pressed.");
        }

        public void CancelDownSide()
        {
            BtnDown.Cancel();
            OnCancelDown(new FloorEventArgs(this));
            Logger.Info($"Floor {_floorNo} DOWN button canceled.");
        }

        public void OnReqUp(FloorEventArgs e)
        {
            FloorEventArgs args = new FloorEventArgs(this);
            EventReqUp?.Invoke(this, args);
            Logger.Info($"Floor {_floorNo} UP button pressed.");
        }

        public void OnReqDown(FloorEventArgs e)
        {
            FloorEventArgs args = new FloorEventArgs(this);
            EventReqDown?.Invoke(this, args);
            Logger.Info($"Floor {_floorNo} DOWN button pressed.");
        }

        public void OnCancelUp(FloorEventArgs e)
        {
            FloorEventArgs args = new FloorEventArgs(this);
            EventCancelUp?.Invoke(this, args);
            Logger.Info($"Floor {_floorNo} UP button canceled.");
        }

        public void OnCancelDown(FloorEventArgs e)
        {
            FloorEventArgs args = new FloorEventArgs(this);
            EventCancelDown?.Invoke(this, args);
            Logger.Info($"Floor {_floorNo} DOWN button canceled.");
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