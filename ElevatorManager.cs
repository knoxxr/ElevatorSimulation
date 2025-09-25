using System.Threading;

namespace knoxxr.Evelvator.Core
{
    public class ElevatorManager
    {
        public Dictionary<int, Elevator> Elevators;
        public Dictionary<int, Floor> Floors;
        private Timer Scheduler;
        public ElevatorManager()
        {
            Scheduler = new Timer(TimerCallback, null, 0, 500);
        }
        public void Initialize(Dictionary<int, Floor> floors, int totalElevator)
        {
            Floors = floors;
            InitFoorReqEvent();

            for (int eleNo = 1; eleNo <= totalElevator; eleNo++)
            {
                Elevator newElevator = new Elevator();
                Elevators.Add(eleNo, newElevator);
            }
        }
        private void InitFoorReqEvent()
        {
            foreach (KeyValuePair<int, Floor> floor in Floors)
            {
                floor.Value.EventReqUp += Floor_OnReqUp;
                floor.Value.EventReqDown += Floor_OnReqDown;
                floor.Value.EventCancelUp += Floor_OnCalcenUp;
                floor.Value.EventCancelDown += Floor_OnCancelDown;
            }
        }
        public void Floor_OnReqUp(object sender, EventArgs e)
        {
            int reqFloor = ((FloorEventArgs)e).ReqFloor;
            GetNearestElevator(reqFloor).Move(reqFloor);
        }
        public void Floor_OnReqDown(object sender, EventArgs e)
        {
            int reqFloor = ((FloorEventArgs)e).ReqFloor;
            GetNearestElevator(reqFloor).Move(reqFloor);
        }
        public void Floor_OnCalcenUp(object sender, EventArgs e)
        {
            int reqFloor = ((FloorEventArgs)e).ReqFloor;
            GetNearestElevator(reqFloor).Stop();
        }
        public void Floor_OnCancelDown(object sender, EventArgs e)
        {
            int reqFloor = ((FloorEventArgs)e).ReqFloor;
            GetNearestElevator(reqFloor).Stop();
        }
        public Elevator GetNearestElevator(int reqFloor)
        {
            return null;
        }
        private static void TimerCallback(object state)
        {

        }
    }
}