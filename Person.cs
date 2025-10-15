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
        public PersonLocation Location
        {
            get { return _location; }
        }
        private static int nextId = 1;
        private const int ButtonPressDelayMs = 1000; // 버튼 누르는 딜레이 (밀리초)
        public readonly int _Id = Interlocked.Increment(ref nextId);
        public int Id
        {
            get { return _Id; } 
        }
        public Floor _curFloor;
        public Floor CurrentFloor
        {
            get { return _curFloor; }
        }
        protected Floor _targetFloor;
        public Floor TargetFloor
        {
            get { return _targetFloor; }
        }
        protected PersonState _state = PersonState.Waiting;
        public PersonState State
        { get { return _state; } }
        private ElevatorManager _elevatorManager;
        private Elevator _currentElevator = null;
        public Elevator CurrentElevator
        {
            get { return _currentElevator; }
        }
        public PersonRequest _curRequest;
        private Building _building;
        public int ElapsedSec
        {
            get 
            {
                if (_curRequest == null)
                    return 0;
                else
                    return _curRequest.ElapsedSec; 
            }
        }
        public Person(Floor curFloor, Building building)
        {
            _building = building;
            ChangeCurrentFloor(curFloor);
            Initialize();
            Logger.Info($"Person created at Floor {_curFloor.FloorNo}.");
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
                Logger.Info("--- BackgroundWorker 작업 시작 ---");
                // 작업을 시작합니다. 인수로 10을 전달합니다.
                _worker.RunWorkerAsync();
            }
        }

        private void InitiaizeElevatorEvent()
        {
            foreach (var ele in _elevatorManager._elevators.Values)
            {
                ele.EventArrivedFloor += Elevator_OnArrivedFloor;
                ele.EventDoorOpened += Elevator_OnDoorOpened;
                ele.EventDoorClosed += Elevator_OnDoorClosed;
                ele.EventChangeCurrentFloor += Elevator_OnChangeCurrentFloor;
            }
        }

        private void Elevator_OnChangeCurrentFloor(object sender, EventArgs e)
        {
            Elevator ele = ((ElevatorEventArgs)e).Elevator;
            if (_location == PersonLocation.Elevator && _currentElevator._Id == ele._Id)
            {
                ChangeCurrentFloor(ele._currentFloor);
                Logger.Info($"[Person {_Id} in Elevator {_currentElevator._Id}] 현재 층이 {_curFloor.FloorNo}로 변경되었습니다.");
            }
        }

        private void Elevator_OnArrivedFloor(object sender, EventArgs e)
        {
            Elevator ele = ((ElevatorEventArgs)e).Elevator;
            if (ele._currentFloor.FloorNo == _curFloor.FloorNo && (_state == PersonState.Calling))
            {
                Logger.Info($"[Person {_Id} at Floor {_curFloor.FloorNo}] 엘리베이터 {ele._Id}이(가) 도착했습니다.");
                ChangePersonState(PersonState.CheckArrivedElevatorAtCurrentFloor);
            }
            else if (_state == PersonState.InElevator && _targetFloor != null && ele._currentFloor.FloorNo == _targetFloor.FloorNo)
            {
                Logger.Info($"[Person {_Id} at Floor {_curFloor.FloorNo}] 엘리베이터 {ele._Id}이(가) 목적지에 도착했습니다.");
                ChangePersonState(PersonState.CheckDestinationReached);
            }
        }

        private void Elevator_OnDoorOpened(object sender, EventArgs e)
        {
            Elevator ele = ((ElevatorEventArgs)e).Elevator;
            if (ele._currentFloor.FloorNo == _curFloor.FloorNo && (_state == PersonState.Calling || _state == PersonState.Waiting))
            {
                Logger.Info($"[Person {_Id} at Floor {_curFloor.FloorNo}] 엘리베이터 {ele._Id}의 문이 열렸습니다.");
                ChangePersonState(PersonState.GetIn);
            }
            else if (ele._currentFloor.FloorNo == _targetFloor.FloorNo && _state == PersonState.InElevator)
            {
                Logger.Info($"[Person {_Id} at Floor {_curFloor.FloorNo}] 엘리베이터 {ele._Id}의 문이 열렸습니다.");
                ChangePersonState(PersonState.GetOut);
            }
        }

        private void Elevator_OnDoorClosed(object sender, EventArgs e)
        {
            Elevator ele = ((ElevatorEventArgs)e).Elevator;
            if (ele._currentFloor.FloorNo == _curFloor.FloorNo && (_state == PersonState.Calling || _state == PersonState.Waiting))
            {
                Logger.Info($"[Person {_Id} at Floor {_curFloor.FloorNo}] 엘리베이터 {ele._Id}의 문이 닫혔습니다.");
                ChangePersonState(PersonState.Waiting);
            }
            else if (ele._currentFloor.FloorNo == _targetFloor.FloorNo && _state == PersonState.InElevator)
            {
                Logger.Info($"[Person {_Id} at Floor {_curFloor.FloorNo}] 엘리베이터 {ele._Id}의 문이 닫혔습니다.");
                ChangePersonState(PersonState.InElevator);
            }
        }

        public void ChangeLocation(PersonLocation newLocation)
        {
            _location = newLocation;
            Logger.Info($"Person {_Id} location changed to {_location}.");
        }

        protected Elevator CheckArrivedElevatorAtCurrentFloor()
        {
            foreach (var ele in _elevatorManager._elevators.Values)
            {
                if (ele._currentFloor != null && ele.IsOpen
                && ele._currentFloor.FloorNo == _curFloor.FloorNo
                && (ele.State == ElevatorState.DoorOpened
                || ele.State == ElevatorState.DoorWaiting)
                && ele.IsMaximumOccupancy() == false
                )
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
                            ChangeCurrentElevator(selectedEvlevator);
                        }
                        break;
                    case PersonState.GetIn:
                        ChangePersonState(PersonState.InElevator);
                        break;
                    case PersonState.InElevator:
                        lock (_requestLock)
                        {
                            PressButton(_curRequest);
                        }
                        ChangePersonState(PersonState.CheckDestinationReached);
                        break;
                    case PersonState.CheckDestinationReached:
                        if (CheckArrivedonTargetFloor())
                            ChangePersonState(PersonState.GetOut);
                        break;
                    case PersonState.GetOut:
                        GetOutElevator();
                        ChangeCurrentElevator(null);
                        ChangePersonState(PersonState.Completed);
                        ChangeLocation(PersonLocation.Floor);
                        OnRequestCompleted();
                        RecordEndTime();
                        Logger.Info($"Person {_Id} has completed their journey.");
                        break;
                    case PersonState.Completed:
                        // 완료 상태에서는 아무 작업도 하지 않음
                        Thread.Sleep(1000); // 1초 대기                        
                        break;
                    default:
                        Thread.Sleep(1000); // 기타 상태에서는 1초 대기
                        break;
                }

                UpdateReqElapseTime();
                Thread.Sleep(500); // 각 상태에서의 작업 사이에 약간의 대기 시간 추가
            }
        }
        private void Worker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        { }

        private void RecordEndTime()
        {
            if(_curRequest!=null)
            {
                _curRequest.EndTime = DateTime.Now;
            }
        }

        private void UpdateReqElapseTime()
        {
            if(_curRequest != null && _curRequest.ReqFloor != null && _curRequest.TargetFloor != null)
            {
                _curRequest.ElapsedSec= (int)(DateTime.Now - _curRequest.StartTime).TotalSeconds;
            }
        }

        // Person 클래스 내 멤버 변수 추가
        private readonly object _requestLock = new object();
        protected void CrateFloorRequest()
        {
            int target = MakeTargetFloor(_curFloor);
            Logger.Info($"Person {_Id} Current Floor {_curFloor.FloorNo}");
            Logger.Info($"Person {_Id} target Floor {target}");

            try
            {
                _targetFloor = _building._floors[target];

                if(_targetFloor == null)
                {

                }
                //_targetFloor = _building._floors[MakeTargetFloor(_curFloor)];
                Direction dir = (_targetFloor.FloorNo > _curFloor.FloorNo) ? Direction.Up : Direction.Down;
                PersonRequest newreq = new PersonRequest()
                {
                    ReqPerson = this,
                    ReqFloor = _curFloor,
                    TargetFloor = _targetFloor,
                    ReqDirection = dir,
                    ReqLocation = PersonLocation.Floor
                };
                // 💡 동기화 블록 추가: 이 부분은 한 번에 하나의 스레드만 실행할 수 있습니다.
                lock (_requestLock)
                {
                    _curRequest = newreq;
                }
                Logger.Info($"Person {_Id} is creating request on floor : {newreq}");
                _elevatorManager.RequestElevator(newreq);
            }
            catch (Exception ex)
            {

            }
        }

        protected void CrateElevatorRequest()
        {
            // 💡 _curRequest 읽기/쓰기 접근을 동일한 lock 객체로 보호
            lock (_requestLock)
            {
                // 널 체크 추가: _curRequest가 null인 경우를 대비하여 방어 코드 삽입
                if (_curRequest == null)
                {
                    Logger.Warn($"Person {_Id} attempted to create elevator request but _curRequest is null.");
                    return;
                }
                _curRequest.ReqElevator = _currentElevator;
                _curRequest.ReqLocation = PersonLocation.Elevator;

                Logger.Info($"Person {_Id} is renewal request on elevator : {_curRequest}");
                _elevatorManager.RequestElevator(_curRequest);
            }
        }

        protected bool CheckArrivedonTargetFloor()
        {
            if (_curFloor.FloorNo == _curRequest.TargetFloor.FloorNo && _currentElevator.IsOpen)
            {
                Logger.Info($"Person {_Id} has arrived at the target floor {_curFloor.FloorNo}.");
                return true;
            }

            Logger.Info($"Person {_Id} has not yet arrived at the target floor {_curRequest.TargetFloor.FloorNo}. Current floor: {_curFloor.FloorNo}.");
            return false;
        }

        protected int MakeTargetFloor(Floor excludeFloor)
        {
            Random rand = new Random();
            int floor;
            do
            {
                floor = rand.Next(Building.TotalUndergroundFloor, Building.TotalGroundFloor);
            } while (floor == excludeFloor.FloorNo); // 출발지와 목적지가 같지 않도록

            return floor;
        }

        private void ChangePersonState(PersonState newState)
        {
            _state = newState;
            Logger.Info($"Person {_Id} state changed to {_state}.");
        }
        public override string ToString()
        {
            string result;

            try
            {
                result = $"Person {_Id} at Floor: {_curFloor.FloorNo}, Target Floor: {_targetFloor?.FloorNo}, State: {_state}";
                result += $", Current Elevator: {_currentElevator?._Id}";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"오류 발생: {ex.Message}");
                return "Error generating string representation.";
            }
            return result;
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
            ChangeLocation(PersonLocation.Floor);
            OnGeOffElevator();
            Thread.Sleep(1000);
            _currentElevator.RemovePerson(this);
        }
        protected void ChangeCurrentElevator(Elevator newElevator)
        {
            _currentElevator = newElevator;
        }
        public void PressButton(PersonRequest req)
        {
            Thread.Sleep(ButtonPressDelayMs); // Simulate delay before pressing the button

            lock (_requestLock)
            {
                CrateElevatorRequest();
                //selectedElevator.ReqButton(targetFloor);
                OnReqButton(req.TargetFloor);
            }
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

            Logger.Info($"Person {_Id} requested UP at Floor {_curFloor.FloorNo}.");
        }

        public void OnReqlDown()
        {
            DirectionEventArgs args = new DirectionEventArgs(Direction.Down);
            EventReqDown?.Invoke(this, args);

            Logger.Info($"Person {_Id} requested DOWN at Floor {_curFloor.FloorNo}.");
        }

        public void OnCancellUp()
        {
            DirectionEventArgs args = new DirectionEventArgs(Direction.Up);
            EventCancelUp?.Invoke(this, args);

            Logger.Info($"Person {_Id} canceled UP request at Floor {_curFloor.FloorNo}.");
        }

        public void OnCancellDown()
        {
            DirectionEventArgs args = new DirectionEventArgs(Direction.Down);
            EventCancelDown?.Invoke(this, args);

            Logger.Info($"Person {_Id} canceled DOWN request at Floor {_curFloor.FloorNo}.");
        }

        public void OnReqButton(Floor targetFloor)
        {
            ButtonEventArgs args = new ButtonEventArgs(targetFloor);
            EventReqButton?.Invoke(this, args);

            Logger.Info($"Person {_Id} pressed button for Floor {targetFloor.FloorNo} inside the elevator.");
        }
        public void OnCancelButton(Floor targetFloor)
        {
            ButtonEventArgs args = new ButtonEventArgs(targetFloor);
            EventCancelButton?.Invoke(this, args);

            Logger.Info($"Person {_Id} canceled button for Floor {targetFloor.FloorNo} inside the elevator.");
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
        public DateTime StartTime;
        public DateTime EndTime;
        public int ElapsedSec = 0;

        public PersonRequest()
        {
            StartTime = DateTime.Now;
        }

        public override string ToString()
        {
            return $"PersonRequest: Person {ReqPerson._Id}, From Floor {ReqFloor.FloorNo} to Floor {TargetFloor.FloorNo}, Direction {ReqDirection}, Location {ReqLocation}";
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
