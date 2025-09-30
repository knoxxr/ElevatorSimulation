using knoxxr.Evelvator.Core;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

namespace knoxxr.Evelvator.Sim
{
    public class Person
    {
        public event EventHandler<DirectionEventArgs> EventReqUp;
        public event EventHandler<DirectionEventArgs> EventReqDown;
        public event EventHandler<DirectionEventArgs> EventCancelUp;
        public event EventHandler<DirectionEventArgs> EventCancelDown;
        public event EventHandler<ButtonEventArgs> EventReqButton;
        public event EventHandler<ButtonEventArgs> EventCancelButton;
        public event EventHandler<PersonEventArgs> EventWaitingElevator;
        public event EventHandler<PersonEventArgs> EventGetInElevator;
        public event EventHandler<PersonEventArgs> EventGeOffElevator;
        public event EventHandler<PersonEventArgs> EventRequestCompleted;
        private BackgroundWorker _worker;
        private int _finalResult;
        public PersonLocation _location = PersonLocation.Floor;
        private static int nextId = 1;
        private const int ButtonPressDelayMs = 1000; // 버튼 누르는 딜레이 (밀리초)
        public readonly int Id = Interlocked.Increment(ref nextId);
        public Floor _curFloor;
        public Floor _targetFloor;
        public PersonState _state = PersonState.Waiting;
        private ElevatorManager _elevatorManager;
        private Elevator _currentElevator = null;
        private PersonRequest _curRequest;
        private Building _building;
        public Person(Floor curFloor, Building building)
        {
            _building = building;
            ChangeCurrentFloor(curFloor);
            Initialize();
            Console.WriteLine($"Person created at Floor {_curFloor.FloorNo}.");
        }

        public void Initialize()
        {
            _elevatorManager = _curFloor._eleMgr;
            InitiaizeElevatorEvent();
            ChangePersonState(PersonState.Waiting);

            _worker = new BackgroundWorker();
            // 1. 작업 수행 메서드 지정 (백그라운드 스레드에서 실행)
            _worker.DoWork += Worker_DoWork;
            // 2. 진행 상황 보고 허용 설정
            _worker.WorkerReportsProgress = true;
            // 3. 작업 완료 메서드 지정 (UI 스레드에서 실행)
            _worker.RunWorkerCompleted += Worker_RunWorkerCompleted;

            if (!_worker.IsBusy)
            {
                Console.WriteLine("--- BackgroundWorker 작업 시작 ---");
                // 작업을 시작합니다. 인수로 10을 전달합니다.
                _worker.RunWorkerAsync();
            }
        }

        private void InitiaizeElevatorEvent()
        {
            foreach (var ele in _elevatorManager.Elevators.Values)
            {
                ele.EventArrivedFloor += Elevator_OnArrivedFloor;
                ele.EventDoorOpened += Elevator_OnDoorOpened;
                ele.EventDoorClosed += Elevator_OnDoorClosed;
                ele.EventChangeCurrentFloor += Elevator_OnArrivedFloor;
            }
        }

        private void Elevator_OnChangeCurrentFloor(object sender, EventArgs e)
        {
            if (_state == PersonState.InElevator && _currentElevator != null)
            {
                ChangeCurrentFloor(_currentElevator._currentFloor);
            }
        }

        private void Elevator_OnArrivedFloor(object sender, EventArgs e)
        {
            Elevator ele = ((ElevatorEventArgs)e).Elevator;
            if (ele._currentFloor.FloorNo == _curFloor.FloorNo && (_state == PersonState.Calling))
            {
                Console.WriteLine($"[Person {Id} at Floor {_curFloor.FloorNo}] 엘리베이터 {ele.Id}이(가) 도착했습니다.");
                ChangePersonState(PersonState.CheckArrivedElevatorAtCurrentFloor);
            }
            else if (ele._currentFloor.FloorNo == _targetFloor.FloorNo && _state == PersonState.InElevator)
            {
                Console.WriteLine($"[Person {Id} at Floor {_curFloor.FloorNo}] 엘리베이터 {ele.Id}이(가) 목적지에 도착했습니다.");
                ChangePersonState(PersonState.CheckDestinationReached);
            }
        }

        private void Elevator_OnDoorOpened(object sender, EventArgs e)
        {
            Elevator ele = ((ElevatorEventArgs)e).Elevator;
            if (ele._currentFloor.FloorNo == _curFloor.FloorNo && (_state == PersonState.Calling || _state == PersonState.Waiting))
            {
                Console.WriteLine($"[Person {Id} at Floor {_curFloor.FloorNo}] 엘리베이터 {ele.Id}의 문이 열렸습니다.");
                ChangePersonState(PersonState.GetIn);
            }
            else if (ele._currentFloor.FloorNo == _targetFloor.FloorNo && _state == PersonState.InElevator)
            {
                Console.WriteLine($"[Person {Id} at Floor {_curFloor.FloorNo}] 엘리베이터 {ele.Id}의 문이 열렸습니다.");
                ChangePersonState(PersonState.GetOut);
            }
        }

        private void Elevator_OnDoorClosed(object sender, EventArgs e)
        {
            Elevator ele = ((ElevatorEventArgs)e).Elevator;
            if (ele._currentFloor.FloorNo == _curFloor.FloorNo && (_state == PersonState.Calling || _state == PersonState.Waiting))
            {
                Console.WriteLine($"[Person {Id} at Floor {_curFloor.FloorNo}] 엘리베이터 {ele.Id}의 문이 닫혔습니다.");
                ChangePersonState(PersonState.Waiting);
            }
            else if (ele._currentFloor.FloorNo == _targetFloor.FloorNo && _state == PersonState.InElevator)
            {
                Console.WriteLine($"[Person {Id} at Floor {_curFloor.FloorNo}] 엘리베이터 {ele.Id}의 문이 닫혔습니다.");
                ChangePersonState(PersonState.InElevator);
            }
        }

        public void ChangeLocation(PersonLocation newLocation)
        {
            _location = newLocation;
            Console.WriteLine($"Person {Id} location changed to {_location}.");
        }

        protected Elevator CheckArrivedElevatorAtCurrentFloor()
        {
            foreach (var ele in _elevatorManager.Elevators.Values)
            {
                if (ele._currentFloor != null
                && ele._currentFloor.FloorNo == _curFloor.FloorNo
                && (ele._state == ElevatorState.DoorOpened
                || ele._state == ElevatorState.DoorWaiting)
                && ele.IsMaximumOccupancy() == false)
                {
                    return ele;
                }
            }
            return null;
        }

        private void Worker_DoWork(object sender, DoWorkEventArgs e)
        {
            while (true)
            {
                //if (_curRequest == null) continue;

                switch (_state)
                {
                    case PersonState.Waiting:
                        ChangePersonState(PersonState.Calling);
                        break;
                    case PersonState.Calling:
                        //WaitingElevator();
                        CrateFloorRequest();
                        ChangePersonState(PersonState.CheckArrivedElevatorAtCurrentFloor);
                        break;
                    case PersonState.CheckArrivedElevatorAtCurrentFloor:
                        Elevator selectedEvlevator = CheckArrivedElevatorAtCurrentFloor();
                        if (selectedEvlevator != null)
                        {
                            GetInElevator(selectedEvlevator);
                            ChangePersonState(PersonState.GetIn);
                            ChangeLocation(PersonLocation.Elevator);
                            _currentElevator = selectedEvlevator;
                        }
                        break;
                    case PersonState.GetIn:
                        ChangePersonState(PersonState.InElevator);
                        break;
                    case PersonState.InElevator:
                        PressButton(_curRequest);
                        ChangePersonState(PersonState.CheckDestinationReached);
                        break;
                    case PersonState.CheckDestinationReached:
                        if (CheckArrivedonTargetFloor())
                            ChangePersonState(PersonState.CheckDestinationReached);
                        break;
                    case PersonState.GetOut:
                        GetOutElevator();
                        _currentElevator = null;
                        ChangePersonState(PersonState.Completed);
                        ChangeLocation(PersonLocation.Floor);
                        Console.WriteLine($"Person {Id} has completed their journey.");
                        break;
                    case PersonState.Completed:
                        // 완료 상태에서는 아무 작업도 하지 않음
                        Thread.Sleep(1000); // 1초 대기                        
                        break;
                    default:
                        Thread.Sleep(1000); // 기타 상태에서는 1초 대기
                        break;
                }
            }
        }
        private void Worker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        { }

        protected void CrateFloorRequest()
        {
            _targetFloor = _building.Floors[MakeTargetFloor(_curFloor)];
            Direction dir = (_targetFloor.FloorNo > _curFloor.FloorNo) ? Direction.Up : Direction.Down;

            PersonRequest newreq = new PersonRequest()
            {
                ReqPerson = this,
                ReqFloor = _curFloor,
                TargetFloor = _targetFloor,
                ReqDirection = dir,
                ReqLocation = PersonLocation.Floor
            };

            _curRequest = newreq;
            Console.WriteLine($"Person {Id} is creating floor request: {newreq}");
            _elevatorManager.RequestElevator(newreq);
        }

        protected void CrateElevatorRequest()
        {
            PersonRequest newreq = new PersonRequest()
            {
                ReqPerson = this,
                ReqFloor = _curFloor,
                TargetFloor = _curRequest.TargetFloor,
                ReqDirection = _curRequest.ReqDirection,
                ReqElevator = _currentElevator,
                ReqLocation = PersonLocation.Elevator
            };

            _curRequest = newreq;
            Console.WriteLine($"Person {Id} is creating elevator request: {newreq}");
            _elevatorManager.RequestElevator(newreq);
        }

        protected bool CheckArrivedonTargetFloor()
        {
            if (_curFloor.FloorNo == _curRequest.TargetFloor.FloorNo)
            {
                return true;
            }
            return false;
        }

        protected int MakeTargetFloor(Floor excludeFloor)
        {
            Random rand = new Random();
            int floor;
            do
            {
                floor = rand.Next(Building.TotalUndergroundFloor, Building.TotalGroundFloor + 1);
            } while (floor == excludeFloor.FloorNo); // 출발지와 목적지가 같지 않도록

            return floor;
        }

        private void ChangePersonState(PersonState newState)
        {
            _state = newState;
            Console.WriteLine($"Person {Id} state changed to {_state}.");
        }
        public override string ToString()
        {
            return $"Person {Id} at Floor: {_curFloor.FloorNo}, Target Floor: {_targetFloor.FloorNo}, State: {_state}";
        }
        public void CallUp()
        {
            if (_curFloor != null)
            {
                _curFloor.ReqUpSide();
                OnReqlUp();
            }
        }
        public void CallDown()
        {
            if (_curFloor != null)
            {
                _curFloor.ReqDownSide();
                OnReqlDown();
            }
        }

        public void ChangeCurrentFloor(Floor newFloor)
        {
            _curFloor = newFloor;
        }
        public void CancelUp()
        {
            if (_curFloor != null)
            {
                _curFloor.CancelUpSide();
                OnCancellUp();
            }
        }
        public void CancelDown()
        {
            if (_curFloor != null)
            {
                _curFloor.CancelDownSide();
                OnCancellDown();
            }
        }
        public void WaitingElevator()
        {
            ChangePersonState(PersonState.Waiting);
            OnWaitingElevator();
        }
        public async Task GetInElevator(Elevator selectedElevator)
        {
            selectedElevator.AddPerson(this);
            ChangePersonState(PersonState.InElevator);
            OnGetInElevator(selectedElevator);
        }
        public void GetOutElevator()
        {
            ChangePersonState(PersonState.GetOut);
            _currentElevator.RemovePerson(this);
            OnGeOffElevator();
        }
        public async Task PressButton(PersonRequest req)
        {
            await Task.Delay(ButtonPressDelayMs); // Simulate delay before pressing the button
            CrateElevatorRequest();
            //selectedElevator.ReqButton(targetFloor);
            OnReqButton(req.TargetFloor);
        }
        public void CancelButton(Floor targetFloor)
        {
            OnCancelButton(targetFloor);
        }
        public void OnWaitingElevator()
        {
            PersonEventArgs args = new PersonEventArgs(this);
            EventWaitingElevator?.Invoke(this, args);
        }
        public void OnGetInElevator(Elevator curElevator)
        {
            PersonEventArgs args = new PersonEventArgs(this, curElevator);

            EventGetInElevator?.Invoke(this, args);
        }
        public void OnGeOffElevator()
        {
            PersonEventArgs args = new PersonEventArgs(this, null);
            EventGeOffElevator?.Invoke(this, args);
        }
        public void OnRequestCompleted()
        {
            PersonEventArgs args = new PersonEventArgs(this, null);
            EventRequestCompleted?.Invoke(this, args);
        }
        public void OnReqlUp()
        {
            DirectionEventArgs args = new DirectionEventArgs(Direction.Up);
            EventReqUp?.Invoke(this, args);

            Console.WriteLine($"Person {Id} requested UP at Floor {_curFloor.FloorNo}.");
        }

        public void OnReqlDown()
        {
            DirectionEventArgs args = new DirectionEventArgs(Direction.Down);
            EventReqDown?.Invoke(this, args);

            Console.WriteLine($"Person {Id} requested DOWN at Floor {_curFloor.FloorNo}.");
        }

        public void OnCancellUp()
        {
            DirectionEventArgs args = new DirectionEventArgs(Direction.Up);
            EventCancelUp?.Invoke(this, args);

            Console.WriteLine($"Person {Id} canceled UP request at Floor {_curFloor.FloorNo}.");
        }

        public void OnCancellDown()
        {
            DirectionEventArgs args = new DirectionEventArgs(Direction.Down);
            EventCancelDown?.Invoke(this, args);

            Console.WriteLine($"Person {Id} canceled DOWN request at Floor {_curFloor.FloorNo}.");
        }

        public void OnReqButton(Floor targetFloor)
        {
            ButtonEventArgs args = new ButtonEventArgs(targetFloor);
            EventReqButton?.Invoke(this, args);

            Console.WriteLine($"Person {Id} pressed button for Floor {targetFloor.FloorNo} inside the elevator.");
        }
        public void OnCancelButton(Floor targetFloor)
        {
            ButtonEventArgs args = new ButtonEventArgs(targetFloor);
            EventCancelButton?.Invoke(this, args);

            Console.WriteLine($"Person {Id} canceled button for Floor {targetFloor.FloorNo} inside the elevator.");
        }
    }

    public enum PersonState
    {
        Waiting,
        Calling,
        CheckArrivedElevatorAtCurrentFloor,
        GetIn,
        InElevator,
        CheckDestinationReached,
        GetOut,
        Completed
    }

    public enum PersonLocation
    {
        Floor,
        Elevator
    }

    public class PersonRequest
    {
        public Person ReqPerson;
        public Floor ReqFloor;
        public Floor TargetFloor;
        public Direction ReqDirection;
        public Elevator ReqElevator;
        public PersonLocation ReqLocation;

        public override string ToString()
        {
            return $"PersonRequest: Person {ReqPerson.Id}, From Floor {ReqFloor.FloorNo} to Floor {TargetFloor.FloorNo}, Direction {ReqDirection}, Location {ReqLocation}";
        }
    }
    
    public class DirectionEventArgs : EventArgs
    {
        public Direction Direction { get; }

        public DirectionEventArgs(Direction reqDirection)
        {
            Direction = reqDirection;
        }
    }

    public class ButtonEventArgs : EventArgs
    {
        public Floor ReqFloor { get; }

        public ButtonEventArgs(Floor targetFloor)
        {
            ReqFloor = targetFloor;
        }
    }
    public class PersonEventArgs : EventArgs
    {
        public Person Person { get; }
        public Elevator CurElevator { get; }

        public PersonEventArgs(Person person, Elevator curElevator = null)
        {
            Person = person;
            CurElevator = curElevator;
        }
    }
}
