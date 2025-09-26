using knoxxr.Evelvator.Core;
using System.ComponentModel;
using System.Threading;

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

        private BackgroundWorker _worker;
        private int _finalResult;

        public int Id;
        public Floor _curFloor;
        public int _targetFloor;
        public PersonState _state = PersonState.Waiting;
        public Person(Floor curFloor)
        {
            ChangeCurrentFloor(curFloor);
            Initialize();
            CrateRequest();
            Console.WriteLine($"Person created at Floor {_curFloor.FloorNo}.");
        }

        public void Initialize()
        {
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
        private void Worker_DoWork(object sender, DoWorkEventArgs e)
        {
            switch (_state)
            {
                case PersonState.Waiting:
                    CrateRequest();
                    Thread.Sleep(5000); // 대기 상태에서 5초 대기
                    break;
                case PersonState.Calling:
                    WaitingElevator();
                    Thread.Sleep(3000); // 호출 상태에서 3초 대기
                    break;
                case PersonState.CheckArrivedElevatorAtCurrentFloor:
                    // 엘리베이터 도착 확인 로직 필요
                    Thread.Sleep(2000); // 도착 확인 상태에서 2초 대기
                    break;
                case PersonState.GetIn:
                    GetInElevator();
                    Thread.Sleep(4000); // 엘리베이터 탑승 상태에서 4초 대기
                    break;
                case PersonState.InElevator:
                    WaitingforTargetFloor();
                    Thread.Sleep(7000); // 엘리베이터 탑승 상태에서 7초 대기
                    break;
                case PersonState.CheckDestinationReached:
                    GetOffElevator();
                    Thread.Sleep(4000); // 목적지 도착 확인 상태에서 4초 대기
                    break;
                case PersonState.GetOut:
                    ChangePersonState(PersonState.Completed);
                    Console.WriteLine($"Person {Id} has completed their journey.");
                    Thread.Sleep(2000); // 하차 상태에서 2초 대기
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
        private void Worker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        { }

        protected void CrateRequest()
        {
            _targetFloor = MakeTargetFloor(_curFloor.FloorNo);

            if (_targetFloor > _curFloor.FloorNo)
            {
                CallUp();
            }
            else if (_targetFloor < _curFloor.FloorNo)
            {
                CallDown();
            }
            ChangePersonState(PersonState.Calling);

        }

        protected void WaitingforTargetFloor()
        {
        }

        protected int MakeTargetFloor(int excludeFloor)
        {
            Random rand = new Random();
            int floor;
            do
            {
                floor = rand.Next(Building.TotalUndergroundFloor, Building.TotalGroundFloor + 1);
            } while (floor == excludeFloor); // 출발지와 목적지가 같지 않도록

            return floor;
        }

        private void ChangePersonState(PersonState newState)
        {
            _state = newState;
        }
        public override string ToString()
        {
            return $"Person {Id} at Floor {_curFloor.FloorNo}, Target Floor {_targetFloor}, State {_state}";
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
            _state = PersonState.Waiting;
            OnWaitingElevator();
        }
        public void GetInElevator()
        {
            _state = PersonState.InElevator;
            OnGetInElevator();
        }
        public void GetOffElevator()
        {
            _state = PersonState.CheckDestinationReached;
            OnGeOffElevator();
        }
        public void PressButton(int targetFloor)
        {
            OnReqButton(targetFloor);
        }
        public void CancelButton(int targetFloor)
        {
            OnCancelButton(targetFloor);
        }
        public void OnWaitingElevator()
        {
            PersonEventArgs args = new PersonEventArgs(this);
            EventWaitingElevator?.Invoke(this, args);
        }
        public void OnGetInElevator()
        {
            PersonEventArgs args = new PersonEventArgs(this);
            EventGetInElevator?.Invoke(this, args);
        }
        public void OnGeOffElevator()
        {
            PersonEventArgs args = new PersonEventArgs(this);
            EventGeOffElevator?.Invoke(this, args);
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

        public void OnReqButton(int targetFloor)
        {
            ButtonEventArgs args = new ButtonEventArgs(targetFloor);
            EventReqButton?.Invoke(this, args);

            Console.WriteLine($"Person {Id} pressed button for Floor {targetFloor} inside the elevator.");
        }
        public void OnCancelButton(int targetFloor)
        {
            ButtonEventArgs args = new ButtonEventArgs(targetFloor);
            EventCancelButton?.Invoke(this, args);

            Console.WriteLine($"Person {Id} canceled button for Floor {targetFloor} inside the elevator.");
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
        public int ReqFloor { get; }

        public ButtonEventArgs(int targetFloor)
        {
            ReqFloor = targetFloor;
        }
    }
    public class PersonEventArgs : EventArgs
    {
        public Person Person { get; }

        public PersonEventArgs(Person person)
        {
            Person = person;
        }
    }
}
