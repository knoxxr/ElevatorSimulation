using System.Threading;

namespace knoxxr.Evelvator.Core
{
    public class ElevatorManager
    {
        public Dictionary<int, Elevator> Elevators = new Dictionary<int, Elevator>();
        public Dictionary<int, Floor> Floors;

        public List<ElevatorRequest> Requests = new List<ElevatorRequest>();

        public ElevatorManager()
        {
        }
        public void Initialize(Dictionary<int, Floor> floors, int totalElevator)
        {
            Thread SchedulerThread = new Thread(ScanElevatorEvent);
            SchedulerThread.IsBackground = true; // 백그라운드 스레드로 설정
            SchedulerThread.Start();

            Floors = floors;
            InitFoorReqEvent();

            for (int eleNo = 1; eleNo <= totalElevator; eleNo++)
            {
                Elevator newElevator = new Elevator(eleNo);
                Elevators.Add(newElevator.Id, newElevator);
            }

            Console.WriteLine($"ElevatorManager initialized with {totalElevator} elevators.");
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
            Floor reqFloor = ((FloorEventArgs)e).ReqFloor;
            Requests.Add(new ElevatorRequest(RequestType.FloorRequest, reqFloor, null, Direction.Up));
            //GetNearestElevator(reqFloor).Move(reqFloor);
            Console.WriteLine($"Floor {reqFloor} requested UP. Nearest elevator is moving to the floor.");
        }
        public void Floor_OnReqDown(object sender, EventArgs e)
        {
            Floor reqFloor = ((FloorEventArgs)e).ReqFloor;
            Requests.Add(new ElevatorRequest(RequestType.FloorRequest, reqFloor, null, Direction.Down));
            //GetNearestElevator(reqFloor).Move(reqFloor);
            Console.WriteLine($"Floor {reqFloor} requested DOWN. Nearest elevator is moving to the floor.");
        }
        public void Floor_OnCalcenUp(object sender, EventArgs e)
        {
            Floor reqFloor = ((FloorEventArgs)e).ReqFloor;
            Requests.Add(new ElevatorRequest(RequestType.FloorCancel, reqFloor, null, Direction.Up));
            //GetNearestElevator(reqFloor).Stop();

            Console.WriteLine($"Floor {reqFloor} canceled UP request. Nearest elevator is stopping.");
        }
        public void Floor_OnCancelDown(object sender, EventArgs e)
        {
            Floor reqFloor = ((FloorEventArgs)e).ReqFloor;
            Requests.Add(new ElevatorRequest(RequestType.FloorCancel, reqFloor, null, Direction.Down));
            //GetNearestElevator(reqFloor).Stop();

            Console.WriteLine($"Floor {reqFloor} canceled DOWN request. Nearest elevator is stopping.");
        }
        public Elevator GetNearestElevator(Floor reqFloor)
        {
            return null;
        }
        private void ScanElevatorEvent()
        {
            while (true)
            {
                if (Requests.Count > 0)
                {
                    var req = Requests[0];

                    switch (req.ReqType)
                    {
                        case RequestType.FloorRequest:

                            Console.WriteLine($"[스케줄러] {req.ReqFloor.FloorNo}층에서 {req.Dir.ToString()} 요청 감지됨.");
                            break;
                        case RequestType.FloorCancel:
                            Console.WriteLine($"[스케줄러] {req.ReqFloor.FloorNo}층에서 {req.Dir.ToString()} 취소 요청 감지됨.");
                            break;
                        case RequestType.ButtonRequest:
                            Console.WriteLine($"[스케줄러] 엘리베이터 내부에서 {req.TargetFloor.FloorNo}층 버튼 요청 감지됨.");
                            break;
                        case RequestType.ButtonCancel:
                            Console.WriteLine($"[스케줄러] 엘리베이터 내부에서 {req.TargetFloor.FloorNo}층 버튼 취소 요청 감지됨.");
                            break;
                    }
                    
                    Requests.RemoveAt(0);
                }

                // Check each elevator's state and move them accordingly
                Console.WriteLine("Scheduler tick: Checking elevator states...");
                Thread.Sleep(1000); // Simulate time delay
            }
        }
        

    }

    public enum RequestType
    {
        FloorRequest,
        FloorCancel,
        ButtonRequest,
        ButtonCancel
    }

    public struct ElevatorRequest
    {
        public RequestType ReqType;
        public Floor ReqFloor = null;
        public Floor TargetFloor = null;
        public Direction Dir = Direction.None;

        public ElevatorRequest(RequestType requestType, Floor reqFloor, Floor targetFloor, Direction dir)
        {
            ReqType = requestType;
            TargetFloor = targetFloor;
            ReqFloor = reqFloor;
            Dir = dir;
        }
    }
}