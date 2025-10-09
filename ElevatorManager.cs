using System.Threading;
using knoxxr.Evelvator.Core;
using knoxxr.Evelvator.Sim;

namespace knoxxr.Evelvator.Core
{
    public class ElevatorManager
    {
        public Dictionary<int, Elevator> _elevators = new Dictionary<int, Elevator>();
        public Dictionary<int, Person> _people = new Dictionary<int, Person>();
        public List<PersonRequest> _requests = new List<PersonRequest>();
        public Dictionary<int, Floor>? _floors = null;
        public Building? _building = null;
        private ElevatorAPI? _api = null;

        public ElevatorManager(Building building)
        {
            _building = building;
        }
        public void Initialize(Dictionary<int, Floor> floors, int totalElevator)
        {
            InitFloorReqEvent(floors);
            InitElevators(totalElevator);
            InitAPI();
            InitScheuler();
            //Console.WriteLine($"ElevatorManager initialized with {totalElevator} elevators.");
        }

        private void InitScheuler()
        {
            Thread SchedulerThread = new Thread(ScheduleElevator);
            SchedulerThread.IsBackground = false;
            SchedulerThread.Start();
        }

        private void InitElevators(int totalElevator)
        {
            for (int eleNo = 1; eleNo <= totalElevator; eleNo++)
            {
                Elevator newElevator = new Elevator(eleNo, _building);
                _elevators.Add(newElevator.Id, newElevator);
            }
        }
        private void InitAPI()
        {
            _api = new ElevatorAPI(this);
            _api.Start();
        }
        private void InitFloorReqEvent(Dictionary<int, Floor> floors)
        {
            _floors = floors;
            //foreach (KeyValuePair<int, Floor> floor in Floors)
            //{
            //   floor.Value.EventReqUp += Floor_OnReqUp;
            //  floor.Value.EventReqDown += Floor_OnReqDown;
            // floor.Value.EventCancelUp += Floor_OnCalcenUp;
            //floor.Value.EventCancelDown += Floor_OnCancelDown;
            //}
        }
        public void RequestElevator(PersonRequest request)
        {
            _requests.Add(request);
            //Console.WriteLine($"Person {request.ReqPerson.Id} requested elevator to floor {request.TargetFloor.FloorNo}.");
        }
        public Elevator GetNearestElevator(Floor reqFloor)
        {
            return null;
        }
        private void ScheduleElevator()
        {
            while (true)
            {
                if (_requests.Count > 0)
                {
                    var req = _requests[0];

                    switch (req.ReqLocation)
                    {
                        case PersonLocation.Floor:
                            Elevator bestElevator = SearchBestElevator(req.ReqFloor, req.ReqDirection);
                            if (bestElevator != null)
                            {
                                bestElevator.ExecuteCallMission(req);
                            }
                            else
                            {
                                //Console.WriteLine($"[스케줄러] 요청된 {req.ReqFloor.FloorNo}층에 가장 적합한 엘리베이터가 없습니다.");
                            }
                            //Console.WriteLine($"[스케줄러] {req.ReqFloor.FloorNo}층에서 {req.ReqDirection.ToString()} 요청 감지됨.");
                            break;
                        case PersonLocation.Elevator:
                            //Console.WriteLine($"[스케줄러] 엘리베이터 내부에서 {req.TargetFloor.FloorNo}층 버튼 요청 감지됨.");
                            req.ReqElevator.ExecuteCallMission(req);
                            break;
                    }

                    _requests.RemoveAt(0);
                }
                Thread.Sleep(500); // Simulate time delay
            }
        }

        protected Elevator SearchBestElevator(Floor reqFloor, Direction dir)
        {
            foreach (var elevator in _elevators.Values)
            {
                if (elevator.IsAvailable(reqFloor, dir))
                {
                    return elevator;
                }
            }
            return null;
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
        public Person ReqPerson;
        public RequestType ReqType;
        public Floor ReqFloor = null;
        public Floor TargetFloor = null;
        public Direction Dir = Direction.None;

        public ElevatorRequest(Person reqPerson, RequestType requestType, Floor reqFloor, Floor targetFloor, Direction dir)
        {
            ReqPerson = reqPerson;
            ReqType = requestType;
            TargetFloor = targetFloor;
            ReqFloor = reqFloor;
            Dir = dir;
        }
    }
}