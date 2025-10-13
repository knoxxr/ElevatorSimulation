using knoxxr.Evelvator.Sim;
using System.ComponentModel;
using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Reflection;
using log4net;
using log4net.Config;
using System.Diagnostics;

namespace knoxxr.Evelvator.Core
{
    public class Elevator
    {
        private BackgroundWorker _worker;

        public event EventHandler<ElevatorEventArgs> EventChangeCurrentFloor;
        public event EventHandler<ElevatorEventArgs> EventArrivedFloor;
        public event EventHandler<ElevatorEventArgs> EventDoorOpened;
        public event EventHandler<ElevatorEventArgs> EventDoorClosed;
        private readonly object _pathLock = new object();
        public int _Id;
        public int Id { get { return _Id; } }

        public Floor _currentFloor;
        public Floor CurrentFloor
        {
            get { return _currentFloor; }
        }
        public int MaximumOccupancy = 15;
        public int Height = 2700; // 엘리베이터 높이 (mm)
        protected ElevatorState _state = ElevatorState.Idle;
        public ElevatorState State
        { get { return _state; } }

        // 가정: Elevator 클래스 내부에 정의됩니다.
        // 문이 열리는 데 걸리는 시간 (ms)
        public int DoorOperationTimeMs = 2000;
        public int DoorOpenWaitTimeMs = 3000;

        // === 설정 변수 ===
        private const double MaxSpeed = 20000.0; // 최대 속도 (mm/s)
        private const double Acceleration = 5000.0; // 가속도 (mm/s^2)
        private const double DecelerationDistance = 1000.0; // 감속 시작 거리 (m)
        // === 상태 변수 (현재 위치 저장) ===
        public double CurrentPosition { get; private set; } = 0.0; // 현재 위치 (m)
        private double currentVelocity = 0.0; // 현재 속도 (m/s)

        // === 상태/제어 변수 ===
        private bool isMoving = false;
        private CancellationTokenSource cts;
        public List<Person> _people = new List<Person>();
        protected List<Button> Buttons = new List<Button>();
        public Direction CurrentDirection = Direction.None;

        public Building _building;

        private List<Floor> MovingPath = new List<Floor>();

        public Elevator(int id, Building building)
        {
            _Id = id;
            _building = building;
            ChangeCurrentFloor(building._floors[1]);

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

        public async Task ExecuteCallMission(PersonRequest req)
        {
            AddMovePath(req);

            ChangeElevatorState(ElevatorState.Idle);
        }

        private void Worker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        { }
        private void Worker_DoWork(object sender, DoWorkEventArgs e)
        {
            // 이동 루프 설정
            const int updateIntervalMs = 50;
            double deltaTime = updateIntervalMs / 1000.0; // 0.05초

            try
            {
                while (true)
                {
                    int pathCount;
                    // 🚨 락: Count를 읽을 때도 락을 사용합니다.
                    lock (_pathLock)
                    {
                        pathCount = MovingPath.Count;
                    }

                    if (_state != ElevatorState.Idle) continue;

                    // MovingPath에 목표가 있거나 엘리베이터가 움직이고 있을 때 루프를 계속합니다.
                    while (pathCount > 0 || Math.Abs(currentVelocity) > 0.001)
                    {
                        double targetPosition;
                        int targetFloorNo = -1;
                        bool isStoppingForPath = false;

                        // ----------------------------------------------------
                        // 1. 현재 목표 위치 설정 (MovingPath 비었을 때 정지 층으로 설정)
                        // ----------------------------------------------------

                        // 🚨 락: MovingPath에 접근하는 동안 락을 유지합니다.
                        lock (_pathLock)
                        {
                            if (MovingPath.Count > 0)
                            {
                                // 경로의 첫 번째 층을 목표로 설정
                                targetPosition = MovingPath[0].Position;
                                targetFloorNo = MovingPath[0].FloorNo;
                                isStoppingForPath = true; // 경로상의 목표
                                ChangeElevatorState(ElevatorState.Moving);
                            }
                            else // 경로가 비었을 경우 (외부에서 제거되었거나 도착 후 제거된 경우)
                            {
                                targetPosition = GetNearestFloorPosition();
                                // MovingPath.Count가 0이므로, 정지 목표로 처리합니다.
                                Logger.Info($"[ID {_Id}] **경로 소진**! 가장 가까운 층 ({targetPosition:F2}m)으로 감속합니다.");
                            }
                            // lock 블록 종료 (이동 루프 계산에 필요한 데이터 획득)
                        }

                        // ----------------------------------------------------
                        // 2. 물리 계산 로직 (CurrentPosition, currentVelocity만 사용)
                        // ----------------------------------------------------

                        // (기존 로직 그대로 유지)
                        double remainingDistance = targetPosition - CurrentPosition;
                        double absRemainingDistance = Math.Abs(remainingDistance);
                        int direction = Math.Sign(remainingDistance);

                        if (direction > 0)
                            ChangeDirectionState(Direction.Up);
                        else if (direction < 0)
                            ChangeDirectionState(Direction.Down);
                        else
                            ChangeDirectionState(Direction.None);

                        double distanceToStop = (currentVelocity * currentVelocity) / (2 * Acceleration);
                        bool shouldDecelerate = absRemainingDistance <= Math.Max(distanceToStop, DecelerationDistance);

                        double targetAcceleration;
                        if (absRemainingDistance < 0.001)
                            targetAcceleration = -currentVelocity / deltaTime;
                        else if (shouldDecelerate)
                            targetAcceleration = -direction * Acceleration;
                        else
                            targetAcceleration = direction * Acceleration;

                        double nextVelocity = currentVelocity + targetAcceleration * deltaTime;

                        if (!shouldDecelerate)
                            nextVelocity = Math.Min(Math.Abs(nextVelocity), MaxSpeed) * direction;

                        if (direction != 0 && Math.Sign(nextVelocity) != direction && absRemainingDistance < 0.01)
                            nextVelocity = 0;

                        double averageVelocity = (currentVelocity + nextVelocity) / 2.0;
                        double distanceMoved = averageVelocity * deltaTime;

                        if (absRemainingDistance > 0 && Math.Abs(distanceMoved) > absRemainingDistance)
                        {
                            distanceMoved = remainingDistance;
                            nextVelocity = 0;
                        }

                        CurrentPosition += distanceMoved;
                        currentVelocity = nextVelocity;

                        try
                        {
                            CalulateCurrentFloorNumber();
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[ID {_Id}] CalulateCurrentFloorNumber 중 오류: {ex.Message}");
                        }

                        Logger.Info($"[ID {_Id}] / [이동] 위치: {CurrentPosition:F2}m, 속도: {currentVelocity:F2}m/s, 목표: {targetPosition:F2}m, 목표층 : {targetFloorNo}, 현재 층수 : {_currentFloor.FloorNo}, 방향 : {CurrentDirection.ToString()}");

                        // 4. 시뮬레이션 지연 (BackgroundWorker에서는 Thread.Sleep 사용)
                        Thread.Sleep(updateIntervalMs);

                        // ----------------------------------------------------
                        // 5. 도착 확인 및 경로 업데이트
                        // ----------------------------------------------------
                        // 목표 위치에 도달했고, 속도가 0에 가까우며, 이것이 경로상의 목표일 때
                        if (isStoppingForPath && Math.Abs(CurrentPosition - targetPosition) < 0.001 && Math.Abs(currentVelocity) < 0.001)
                        {
                            try
                            {
                                CalulateCurrentFloorNumber();
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"[ID {_Id}] CalulateCurrentFloorNumber 중 오류: {ex.Message}");
                            }
                            // 최종 위치와 속도를 정돈
                            CurrentPosition = targetPosition;
                            currentVelocity = 0;
                            ChangeDirectionState(Direction.None);

                            // 🚨 락: 리스트에서 항목을 제거할 때 락을 사용합니다.
                            lock (_pathLock)
                            {
                                // 제거하기 전에 MovingPath[0]이 여전히 targetFloorNo인지 확인하는 것이 안전합니다.
                                // 하지만 여기서는 간단히 제거합니다.
                                try
                                {
                                    MovingPath.RemoveAt(0);
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"[ID {_Id}] 경로 제거 중 오류: {ex.Message}");
                                }

                                Logger.Info($"[ID {_Id}] / [경로 도착] {targetFloorNo}층 도착! 다음 목표 확인.");

                                try
                                {
                                    // 문 열림/닫힘 비동기 작업 시작
                                    Task.Run(async () =>
                                {
                                    await OpenDoorAsync();
                                    await OpenDoorWaitAsync();
                                    OnArrived(new ElevatorEventArgs(this));
                                    await CloseDoorAsync();
                                }).Wait(); // Wait()로 동기화하여 다음 루프 전에 완료되도록 함
                                }
                                catch (Exception ex)
                                {
                                    Logger.Error($"[ID {_Id}] 문 제어 중 오류: {ex.Message}");
                                }
                            }

                            ChangeElevatorState(ElevatorState.Idle);
                        }

                        // 🚨 락: 다음 루프 조건을 위해 Count를 다시 읽습니다.
                        lock (_pathLock)
                        {
                            pathCount = MovingPath.Count;
                        }
                    } // while 루프 종료

                    // 최종 정지 후 상태 정리 및 다음 체크를 위한 대기
                    ChangeDirectionState(Direction.None);
                    Thread.Sleep(updateIntervalMs);
                }
            }
            catch (Exception ex) // TaskCanceledException 대신 일반 Exception 처리
            {
                Logger.Error($"[ID {_Id}] 작업자 오류 발생: {ex.Message}");
            }
            finally
            {
                // 작업 완료 후 상태를 Idle로 변경
                ChangeElevatorState(ElevatorState.Idle);
            }
        }

        public void Stop()
        {
            Logger.Info($"[ID {_Id}] / [요청] 이동 취소 요청됨.");
            CancelMovement();
        }

        protected int TransFloorToPosition(int floor)
        {
            return floor * Floor.Height;
        }

        protected int TransPositionToFloor(double position)
        {
            return (int)Math.Round(position / Floor.Height);
        }

        protected double GetNearestFloorPosition()
        {
            // 현재 위치를 층 높이로 나눈 후 반올림하여 층 수를 구함
            int nearestFloor = (int)Math.Round(CurrentPosition / Floor.Height);
            // 층 수에 층 높이를 곱하여 정확한 층 위치(m)를 구함
            return nearestFloor * Floor.Height;
        }


        public async Task OpenDoorAsync()
        {
            // 문이 이미 열려 있는지 확인하는 로직이 있다면 여기에 추가

            Logger.Info($"[ID {_Id}] 문 열림 시작...");
            ChangeElevatorState(ElevatorState.DoorOpenningStarting);
            await Task.Delay(100);

            // 문 열림 시뮬레이션
            ChangeElevatorState(ElevatorState.DoorOpening);
            await Task.Delay(DoorOperationTimeMs);

            // 상태 업데이트 (예: DoorState = DoorState.Open)
            Logger.Info($"[ID {_Id}] 문 열림 완료 (현재 층: {_currentFloor.FloorNo}).");
            ChangeElevatorState(ElevatorState.DoorOpened);
            OnDoorOpened(new ElevatorEventArgs(this));
        }

        public async Task OpenDoorWaitAsync()
        {
            // 문이 이미 열려 있는지 확인하는 로직이 있다면 여기에 추가

            Logger.Info($"[ID {_Id}] 문 열린후 대기 시작...");
            ChangeElevatorState(ElevatorState.DoorWaitingFinish);
            await Task.Delay(100);

            // 문 열림 시뮬레이션
            ChangeElevatorState(ElevatorState.DoorWaiting);
            await Task.Delay(DoorOpenWaitTimeMs);

            // 상태 업데이트 (예: DoorState = DoorState.Open)
            Logger.Info($"[ID {_Id}] 문 열림후 대기 완료 (현재 층: {_currentFloor.FloorNo}).");
            ChangeElevatorState(ElevatorState.DoorWaitingFinish);
        }


        /// <summary>
        /// 엘리베이터 문을 비동기적으로 닫고, 닫힐 때까지 대기합니다.
        /// </summary>
        public async Task CloseDoorAsync()
        {
            // 문이 이미 닫혀 있는지 확인하는 로직이 있다면 여기에 추가

            Logger.Info($"[ID {_Id}] 문 닫힘 시작...");
            ChangeElevatorState(ElevatorState.DoorClosingStarted);

            // 문 닫힘 시뮬레이션
            ChangeElevatorState(ElevatorState.DoorClosing);
            await Task.Delay(DoorOperationTimeMs);

            // 상태 업데이트 (예: DoorState = DoorState.Closed)
            Logger.Info($"[ID {_Id}] 문 닫힘 완료.");
            ChangeElevatorState(ElevatorState.DoorClosed);
            OnDoorClosed(new ElevatorEventArgs(this));
        }

        protected void ChangeDirectionState(Direction dir)
        {
            CurrentDirection = dir;
        }

        protected void ChangeElevatorState(ElevatorState state)
        {
            if (_state == state) return;
            _state = state;
            Logger.Info($"[ID {_Id}] 엘리베이터 상태 변경: {_state}");
        }

        /// <summary>
        /// 현재 진행 중인 이동을 취소하고 가장 가까운 층으로 멈추도록 요청합니다.
        /// </summary>
        protected void CancelMovement()
        {
            cts?.Cancel();
        }

        /// <summary>
        /// 새로운 이동을 시작하고 취소 토큰을 준비합니다.
        /// </summary>
        protected async Task StartMove(double destination, int targetFloorNo)
        {

        }

        protected Floor CalulateCurrentFloorNumber()
        {
            Floor newFloor = _building._floors[(int)Math.Round(CurrentPosition / Floor.Height) + 1];
            if (newFloor == null)
            {
                return _currentFloor;
            }
            ChangeCurrentFloor(newFloor);
            return newFloor;
        }

        protected void ChangeCurrentFloor(Floor floor)
        {
            if (floor == null) return;
            if (_currentFloor == null || _currentFloor.FloorNo != floor.FloorNo)
            {
                _currentFloor = floor;
                OnChangeCurrentFloor(new ElevatorEventArgs(this));
            }
        }

        public bool IsAvailable(Floor reqFloor, Direction dir)
        {
            if (_state == ElevatorState.Idle)
            {
                return true;
            }
            return false;
        }

        public void OnChangeCurrentFloor(ElevatorEventArgs e)
        {
            ElevatorEventArgs args = new ElevatorEventArgs(this);
            EventChangeCurrentFloor?.Invoke(this, args);
            Logger.Info($"[ID {_Id}] 엘리베이터가 {e.Elevator._currentFloor.FloorNo}층으로 이동 중입니다.");
        }
        public void OnArrived(ElevatorEventArgs e)
        {
            ElevatorEventArgs args = new ElevatorEventArgs(this);
            EventArrivedFloor?.Invoke(this, args);
            Logger.Info($"[ID {_Id}] 엘리베이터가 {e.Elevator._currentFloor.FloorNo}층에 도착했습니다.");
        }

        public void OnDoorOpened(ElevatorEventArgs e)
        {
            ElevatorEventArgs args = new ElevatorEventArgs(this);
            EventArrivedFloor?.Invoke(this, args);
            Logger.Info($"[ID {_Id}] 엘리베이터가 {e.Elevator._currentFloor.FloorNo}층에서 문이 열렸습니다.");
        }

        public void OnDoorClosed(ElevatorEventArgs e)
        {
            ElevatorEventArgs args = new ElevatorEventArgs(this);
            EventArrivedFloor?.Invoke(this, args);
            Logger.Info($"[ID {_Id}] 엘리베이터가 {e.Elevator._currentFloor.FloorNo}층에서 문이 닫혔습니다.");
        }

        public void AddPerson(Person person)
        {
            if (IsMaximumOccupancy() == false)
            {
                _people.Add(person);
                Logger.Info($"[ID {_Id}] 엘리베이터에 {person._Id} 탑승. 현재 탑승 인원: {_people.Count}");
            }
            else
            {
                Logger.Info($"[ID {_Id}] 엘리베이터가 만원입니다. {person._Id} 탑승 불가.");
            }
        }

        public void RemovePerson(Person person)
        {
            if (_people.Contains(person))
            {
                _people.Remove(person);
                Logger.Info($"[ID {_Id}] 엘리베이터에서 {person} 하차. 현재 탑승 인원: {_people.Count}");
            }
            else
            {
                Logger.Info($"[ID {_Id}] 엘리베이터에 {person} 이(가) 없습니다.");
            }
        }

        public bool IsMaximumOccupancy()
        {
            return _people.Count >= MaximumOccupancy;
        }

        public void ReqButton(int floorNo)
        {
            if (floorNo < Building.TotalUndergroundFloor || floorNo > Building.TotalGroundFloor)
            {
                Logger.Info($"[ID {_Id}] {floorNo}층은 유효한 층이 아닙니다.");
                return;
            }
            // 버튼이 이미 눌려 있는지 확인
            if (Buttons.Exists(b => b == Buttons[floorNo - 1] && b.Pressed))
            {
                Logger.Info($"[ID {_Id}] {floorNo}층 버튼이 이미 눌려 있습니다. 중복 요청 무시됨.");
                return;
            }

            Buttons[floorNo - 1].Press();
            Logger.Info($"[ID {_Id}] {floorNo}층 버튼이 눌렸습니다.");

            // 버튼이 눌렸을 때의 추가 로직 (예: 이동 요청 추가 등)을 여기에 구현
        }

        public void CancelButton(int floorNo)
        {
            if (floorNo < Building.TotalUndergroundFloor || floorNo > Building.TotalGroundFloor)
            {
                Logger.Info($"[ID {_Id}] {floorNo}층은 유효한 층이 아닙니다.");
                return;
            }
            // 버튼이 눌려 있지 않은지 확인
            if (Buttons.Exists(b => b == Buttons[floorNo - 1] && !b.Pressed))
            {
                Logger.Info($"[ID {_Id}] {floorNo}층 버튼이 이미 취소되어 있습니다. 중복 요청 무시됨.");
                return;
            }

            Buttons[floorNo - 1].Cancel();
            Logger.Info($"[ID {_Id}] {floorNo}층 버튼이 취소되었습니다.");

            // 버튼이 취소되었을 때의 추가 로직 (예: 이동 요청 제거 등)을 여기에 구현
        }

        public void AddMovePath(PersonRequest request)
        {
            // request가 null이거나, ReqLocation이 정의되지 않은 경우 등을 처리할 수 있습니다.
            //if (request is null) return;
            switch (request.ReqLocation)
            {
                case PersonLocation.Floor:
                    AddMovePath(request.ReqFloor, request.ReqDirection);
                    break;
                case PersonLocation.Elevator:
                    AddMovePath(request.TargetFloor, request.ReqDirection);
                    break;
            }
        }
        public void AddMovePath(Floor floor, Direction dir)
        {
            if (floor == null) return;
            lock (_pathLock)
            {
                if (MovingPath.Exists(f => f.FloorNo == floor.FloorNo))
                {
                    Logger.Info($"[ID {_Id}] {floor.FloorNo}층이 이미 경로에 포함되어 있습니다. 중복 추가 무시됨.");
                    return;
                }

                MovingPath.Add(floor);

                switch (dir)
                {
                    case Direction.Up:
                        MovingPath.Sort((f1, f2) => f1.FloorNo.CompareTo(f2.FloorNo));
                        break;
                    case Direction.Down:
                        MovingPath.Sort((f1, f2) => f2.FloorNo.CompareTo(f1.FloorNo));
                        break;
                    case Direction.None:
                        break;
                }
                Logger.Info($"[ID {_Id}] {floor.FloorNo}층이 이동 경로에 추가되었습니다.");
            }
        }
    }

    public enum ElevatorState
    {
        Idle,
        Moving,
        DoorOpenningStarting,
        DoorOpening,
        DoorOpened,
        DoorWaitingStart,
        DoorWaiting,
        DoorWaitingFinish,
        DoorClosingStarted,
        DoorClosing,
        DoorClosed,
        Faulted,
    }

    public enum Direction
    {
        Up,
        Down,
        None
    }
    public class Button()
    {
        public bool Enable = true;
        public bool Pressed = false;
        public void Press()
        {
            if (Enable && !Pressed)
                Pressed = true;
        }

        public void Cancel()
        {
            if (Enable && Pressed)
                Pressed = false;
        }
    }
    
    public class ElevatorEventArgs : EventArgs
    {
        public Elevator Elevator { get; }

        public ElevatorEventArgs(Elevator elevator)
        {
            Elevator = elevator;
        }
    }
}