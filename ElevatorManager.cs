using System.Threading;
using knoxxr.Evelvator.Core;
using knoxxr.Evelvator.Sim;

namespace knoxxr.Evelvator.Core
{
    public class ElevatorManager
    {
        public Dictionary<int, Elevator> Elevators = new Dictionary<int, Elevator>();
        public Dictionary<int, Floor> Floors;
        public Dictionary<int, Person> People = new Dictionary<int, Person>();

        public List<PersonRequest> Requests = new List<PersonRequest>();

        public Building _building = null;

        private UISocket uISocket;

        public ElevatorManager(Building building)
        {
            this._building = building;
        }
        public void Initialize(Dictionary<int, Floor> floors, int totalElevator)
        {
            Thread SchedulerThread = new Thread(ScanElevatorEvent);
            SchedulerThread.IsBackground = true; // 백그라운드 스레드로 설정
            SchedulerThread.Start();

            Floors = floors;
            InitFloorReqEvent();

            for (int eleNo = 1; eleNo <= totalElevator; eleNo++)
            {
                Elevator newElevator = new Elevator(eleNo, _building);
                Elevators.Add(newElevator.Id, newElevator);
            }

            uISocket = new UISocket(this);
            uISocket.StartSimulationAsync();

            Console.WriteLine($"ElevatorManager initialized with {totalElevator} elevators.");

           //InitUISocket().GetAwaiter().GetResult();
        }
        
        private async Task InitUISocket()
        {
           /*  // 시스템에서 JSON 직렬화를 위한 설정 확인 메시지
            Console.WriteLine(".NET 엘리베이터 시뮬레이터 서버 시작 (Flask 통신).");

            // Ctrl+C 입력 시 프로그램 종료를 위해 대기
            var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (sender, eventArgs) =>
            {
                eventArgs.Cancel = true;
                cts.Cancel();
                Console.WriteLine("프로그램 종료 요청...");
            };

            var simulator = new ElevatorSimulationManager();

            // 시뮬레이션 및 TCP 통신 시작 (이제 내부적으로 클라이언트/서버 역할 모두 수행)
            await simulator.StartSimulationAsync();

            // 프로그램이 종료될 때까지 대기
            await Task.Delay(Timeout.Infinite, cts.Token); */
        }
        private void InitFloorReqEvent()
        {
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
            Requests.Add(request);
            Console.WriteLine($"Person {request.ReqPerson.Id} requested elevator to floor {request.TargetFloor.FloorNo}.");
        }
        /*
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
        */
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
                                Console.WriteLine($"[스케줄러] 요청된 {req.ReqFloor.FloorNo}층에 가장 적합한 엘리베이터가 없습니다.");
                            }
                            Console.WriteLine($"[스케줄러] {req.ReqFloor.FloorNo}층에서 {req.ReqDirection.ToString()} 요청 감지됨.");
                            break;
                        case PersonLocation.Elevator:
                            Console.WriteLine($"[스케줄러] 엘리베이터 내부에서 {req.TargetFloor.FloorNo}층 버튼 요청 감지됨.");
                            req.ReqElevator.ExecuteCallMission(req);
                            break;
                    }

                    Requests.RemoveAt(0);
                }

                // Check each elevator's state and move them accordingly
                //Console.WriteLine("Scheduler tick: Checking elevator states...");
                Thread.Sleep(1000); // Simulate time delay
            }
        }
        
        protected Elevator SearchBestElevator(Floor reqFloor, Direction dir)
        {
            foreach (var elevator in Elevators.Values)
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