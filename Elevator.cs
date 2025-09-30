using knoxxr.Evelvator.Sim;
using System.ComponentModel;
using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

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
        public int Id;
        public Floor _currentFloor;
        public int MaximumOccupancy = 15;
        public int Height = 2700; // 엘리베이터 높이 (mm)
        public ElevatorState _state = ElevatorState.Idle;
        // 가정: Elevator 클래스 내부에 정의됩니다.
        // 문이 열리는 데 걸리는 시간 (ms)
        private const int DoorOperationTimeMs = 2000;
        private const int DoorOpenWaitTimeMs = 3000;

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
        public List<Person> People = new List<Person>();
        protected List<Button> Buttons = new List<Button>();
        public Direction CurrentDirection = Direction.None;

        public Building _building;

        public List<Floor> MovingPath = new List<Floor>();

        public Elevator(int id, Building building)
        {
            Id = id;
            _building = building;
            ChangeCurrentFloor(building.Floors[1]);

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
                                Console.WriteLine($"[ID {Id}] **경로 소진**! 가장 가까운 층 ({targetPosition:F2}m)으로 감속합니다.");
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

                        CalulateCurrentFloorNumber();

                        Console.WriteLine($"[ID {Id}] / [이동] 위치: {CurrentPosition:F2}m, 속도: {currentVelocity:F2}m/s, 목표: {targetPosition:F2}m, 목표층 : {targetFloorNo}, 현재 층수 : {_currentFloor.FloorNo}, 방향 : {CurrentDirection.ToString()}");

                        // 4. 시뮬레이션 지연 (BackgroundWorker에서는 Thread.Sleep 사용)
                        Thread.Sleep(updateIntervalMs);

                        // ----------------------------------------------------
                        // 5. 도착 확인 및 경로 업데이트
                        // ----------------------------------------------------
                        // 목표 위치에 도달했고, 속도가 0에 가까우며, 이것이 경로상의 목표일 때
                        if (isStoppingForPath && Math.Abs(CurrentPosition - targetPosition) < 0.001 && Math.Abs(currentVelocity) < 0.001)
                        {
                            // 최종 위치와 속도를 정돈
                            CurrentPosition = targetPosition;
                            currentVelocity = 0;
                            ChangeDirectionState(Direction.None);

                            // 🚨 락: 리스트에서 항목을 제거할 때 락을 사용합니다.
                            lock (_pathLock)
                            {
                                // 제거하기 전에 MovingPath[0]이 여전히 targetFloorNo인지 확인하는 것이 안전합니다.
                                // 하지만 여기서는 간단히 제거합니다.
                                MovingPath.RemoveAt(0);
                            }

                            Console.WriteLine($"[ID {Id}] / [경로 도착] {targetFloorNo}층 도착! 다음 목표 확인.");

                            // 문 열림/닫힘 비동기 작업 시작
                            Task.Run(async () =>
                            {
                                await OpenDoorAsync();
                                await OpenDoorWaitAsync();
                                await CloseDoorAsync();
                            }).Wait(); // Wait()로 동기화하여 다음 루프 전에 완료되도록 함
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
                Console.WriteLine($"[ID {Id}] 작업자 오류 발생: {ex.Message}");
            }
            finally
            {
                // 작업 완료 후 상태를 Idle로 변경
                ChangeElevatorState(ElevatorState.Idle);
            }
        }

        public async Task MoveAsync(Floor targetFloor)
        {
            if (targetFloor == null) return;
            if (_state == ElevatorState.Moving) return;

            // CancellationTokenSource 생성
            cts = new CancellationTokenSource();

            // 상태 변경
            ChangeElevatorState(ElevatorState.Moving);

            // 이동 루프 설정
            const int updateIntervalMs = 50;
            double deltaTime = updateIntervalMs / 1000.0; // 0.05초

            try
            {
                // MovingPath에 목표가 있거나 엘리베이터가 움직이고 있을 때 루프를 계속합니다.
                while (MovingPath.Count > 0 || Math.Abs(currentVelocity) > 0.001)
                {
                    // ----------------------------------------------------
                    // 1. 현재 목표 위치 설정 (MovingPath 비었을 때 정지 층으로 설정)
                    // ----------------------------------------------------

                    double targetPosition;
                    int targetFloorNo = -1;
                    bool isStoppingForPath = false;

                    if (MovingPath.Count > 0)
                    {
                        // 경로의 첫 번째 층을 목표로 설정
                        targetPosition = MovingPath[0].Position;
                        targetFloorNo = MovingPath[0].FloorNo;
                        isStoppingForPath = true; // 경로상의 목표
                    }
                    else // 🚨 경로가 비었을 경우 (MovingPath.Count == 0)
                    {
                        targetPosition = GetNearestFloorPosition();
                        // targetFloorNo는 GetFloorNumberByPosition 함수를 통해 얻는다고 가정
                        // isStoppingForPath = false; // 경로 목표가 아니므로 false 유지

                        Console.WriteLine($"[ID {Id}] **경로 소진**! 가장 가까운 층 ({targetPosition:F2}m)으로 감속합니다.");
                    }

                    // 1-1. 취소 요청 확인 및 목표 위치 변경 (기존 로직)
                    if (cts.IsCancellationRequested)
                    {
                        // 취소 시, 목표를 가장 가까운 층으로 변경하여 즉시 정지 유도
                        if (targetPosition != GetNearestFloorPosition())
                        {
                            targetPosition = GetNearestFloorPosition();
                            // targetFloorNo 갱신 로직 필요
                        }
                        isStoppingForPath = false; // 취소 정지는 경로 이행이 아님
                    }

                    // ----------------------------------------------------
                    // 2. 남은 거리 계산 및 방향 설정 (기존 로직)
                    // ----------------------------------------------------
                    double remainingDistance = targetPosition - CurrentPosition;
                    double absRemainingDistance = Math.Abs(remainingDistance);
                    int direction = Math.Sign(remainingDistance);

                    // ... (방향 설정 로직) ...
                    if (direction > 0)
                        ChangeDirectionState(Direction.Up);
                    else if (direction < 0)
                        ChangeDirectionState(Direction.Down);
                    else
                        ChangeDirectionState(Direction.None);

                    // 3. 가속/감속 상태 결정 (기존 로직)
                    double distanceToStop = (currentVelocity * currentVelocity) / (2 * Acceleration);
                    bool shouldDecelerate = absRemainingDistance <= Math.Max(distanceToStop, DecelerationDistance);

                    double targetAcceleration;
                    if (absRemainingDistance < 0.001)
                        targetAcceleration = -currentVelocity / deltaTime;
                    else if (shouldDecelerate)
                        targetAcceleration = -direction * Acceleration;
                    else
                        targetAcceleration = direction * Acceleration;

                    // 4. 속도 및 위치 업데이트 (기존 로직)
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

                    CalulateCurrentFloorNumber();

                    // Console.WriteLine
                    Console.WriteLine($"[ID {Id}] / [이동] 위치: {CurrentPosition:F2}m, 속도: {currentVelocity:F2}m/s, 목표: {targetPosition:F2}m, 목표층 : {targetFloorNo}, 현재 층수 : {_currentFloor.FloorNo}, 방향 : {CurrentDirection.ToString()}");

                    // 5. 시뮬레이션 지연 (비동기 대기)
                    await Task.Delay(updateIntervalMs, cts.Token);

                    // ----------------------------------------------------
                    // 6. 도착 확인 및 경로 업데이트
                    // ----------------------------------------------------
                    // 목표 위치에 도달했고, 속도가 0에 가까우며, 이것이 경로상의 목표일 때
                    if (isStoppingForPath && Math.Abs(CurrentPosition - targetPosition) < 0.001 && Math.Abs(currentVelocity) < 0.001)
                    {
                        // 최종 위치와 속도를 정돈
                        CurrentPosition = targetPosition;
                        currentVelocity = 0;
                        ChangeDirectionState(Direction.None);

                        // 도착 처리
                        Console.WriteLine($"[ID {Id}] / [경로 도착] {targetFloorNo}층 도착! 다음 목표 확인.");

                        // 도착한 층을 경로에서 제거 (다음 루프에서 새 목표를 읽게 됨)
                        MovingPath.RemoveAt(0);

                        await OpenDoorAsync();
                        await OpenDoorWaitAsync();
                        await CloseDoorAsync();

                        // Door handling can be called here: await OpenDoorAsync(); await Task.Delay(3000); await CloseDoorAsync();
                    }
                } // while 루프 종료

                // 최종 정지 후 상태 정리
                ChangeDirectionState(Direction.None);
            }
            catch (TaskCanceledException)
            {
                // 취소 처리 로직
            }
            finally
            {
                // 작업 완료 후 상태를 Idle로 변경
                ChangeElevatorState(ElevatorState.Idle);
            }
        }
        public async Task MoveAsync2(Floor targetFloor)
        {
            if (targetFloor == null) return;
            if (_state == ElevatorState.Moving) return;

            Console.WriteLine($"[ID {Id}] / [요청] {targetFloor.FloorNo}층으로 이동 요청됨.");

            cts = new CancellationTokenSource();
            //await MoveElevatorAsync(targetFloor.Position, targetFloor.FloorNo, cts.Token);

            if (_state == ElevatorState.Moving) return;
            ChangeElevatorState(ElevatorState.Moving);

            // 이동 루프는 50ms마다 갱신 (시뮬레이션 시간 간격)
            const int updateIntervalMs = 50;
            double deltaTime = updateIntervalMs / 1000.0; // 0.05초

            try
            {
                double targetPosition = targetFloor.Position;

                while (Math.Abs(CurrentPosition - targetPosition) > 0.001 || Math.Abs(currentVelocity) > 0.001)
                {
                    // 1. 취소 요청 확인 및 목표 위치 변경 (가장 가까운 층으로)
                    if (cts.IsCancellationRequested)
                    {
                        // 취소 요청 시, 목표를 가장 가까운 층으로 변경하고 루프를 계속함
                        if (targetPosition != GetNearestFloorPosition())
                        {
                            targetPosition = GetNearestFloorPosition();
                            //Console.WriteLine($"[취소] 요청됨. 목표 위치를 가장 가까운 층 ({targetPosition:F2}m)으로 변경.");
                        }
                        // 이미 가장 가까운 층으로 목표가 설정된 경우, 다음 로직에서 정지할 것임
                    }

                    // 2. 남은 거리 계산
                    double remainingDistance = targetPosition - CurrentPosition;
                    double absRemainingDistance = Math.Abs(remainingDistance);
                    int direction = Math.Sign(remainingDistance);

                    if (direction > 0)
                        ChangeDirectionState(Direction.Up);
                    else if (direction < 0)
                        ChangeDirectionState(Direction.Down);
                    else
                        ChangeDirectionState(Direction.None);
                    // 3. 가속/감속 상태 결정
                    double distanceToStop = (currentVelocity * currentVelocity) / (2 * Acceleration);
                    bool shouldDecelerate = absRemainingDistance <= Math.Max(distanceToStop, DecelerationDistance);

                    double targetAcceleration;

                    if (absRemainingDistance < 0.001)
                    {
                        targetAcceleration = -currentVelocity / deltaTime;
                    }
                    else if (shouldDecelerate)
                    {
                        targetAcceleration = -direction * Acceleration;
                    }
                    else
                    {
                        targetAcceleration = direction * Acceleration;
                    }

                    // 4. 속도 및 위치 업데이트 (이전 코드의 로직과 동일)
                    double nextVelocity = currentVelocity + targetAcceleration * deltaTime;

                    if (!shouldDecelerate)
                    {
                        nextVelocity = Math.Min(Math.Abs(nextVelocity), MaxSpeed) * direction;
                    }

                    if (direction != 0 && Math.Sign(nextVelocity) != direction && absRemainingDistance < 0.01)
                    {
                        nextVelocity = 0;
                    }

                    double averageVelocity = (currentVelocity + nextVelocity) / 2.0;
                    double distanceMoved = averageVelocity * deltaTime;

                    if (absRemainingDistance > 0 && Math.Abs(distanceMoved) > absRemainingDistance)
                    {
                        distanceMoved = remainingDistance;
                        nextVelocity = 0;
                    }

                    CurrentPosition += distanceMoved;
                    currentVelocity = nextVelocity;

                    CalulateCurrentFloorNumber();

                    Console.WriteLine($"[ID {Id}] / [이동] 위치: {CurrentPosition:F2}m, 속도: {currentVelocity:F2}m/s, 목표: {targetPosition:F2}m, 목표층 : {targetFloor.FloorNo}, 현재 층수 : {_currentFloor.FloorNo}, 방향 : {CurrentDirection.ToString()}");
                    // 5. 시뮬레이션 지연 (비동기 대기)

                    await Task.Delay(updateIntervalMs, cts.Token);
                }

                ChangeDirectionState(Direction.None);

                // 최종 정리
                CurrentPosition = targetPosition;
                currentVelocity = 0;
            }
            catch (TaskCanceledException)
            {
                // 가장 가까운 층으로 이동하는 도중 Task가 명시적으로 취소되면 (예: 외부에서 cts.Cancel() 호출)
                // 이 블록이 실행되지만, 우리는 루프 내에서 취소 요청을 확인하고 있으므로 이 예외는 무시하거나 로깅할 수 있습니다.
                //Console.WriteLine("이동 Task가 외부에서 취소되었습니다.");
            }
            finally
            {
                ChangeElevatorState(ElevatorState.Idle);
            }
        }

        public void Stop()
        {
            Console.WriteLine($"[ID {Id}] / [요청] 이동 취소 요청됨.");
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

            Console.WriteLine($"[ID {Id}] 문 열림 시작...");
            ChangeElevatorState(ElevatorState.DoorOpenningStarting);
            await Task.Delay(100);

            // 문 열림 시뮬레이션
            ChangeElevatorState(ElevatorState.DoorOpening);
            await Task.Delay(DoorOperationTimeMs);

            // 상태 업데이트 (예: DoorState = DoorState.Open)
            Console.WriteLine($"[ID {Id}] 문 열림 완료 (현재 층: {CalulateCurrentFloorNumber().FloorNo}).");
            ChangeElevatorState(ElevatorState.DoorOpened);
            OnDoorOpened(new ElevatorEventArgs(this));
        }

        public async Task OpenDoorWaitAsync()
        {
            // 문이 이미 열려 있는지 확인하는 로직이 있다면 여기에 추가

            Console.WriteLine($"[ID {Id}] 문 열린후 대기 시작...");
            ChangeElevatorState(ElevatorState.DoorWaitingFinish);
            await Task.Delay(100);

            // 문 열림 시뮬레이션
            ChangeElevatorState(ElevatorState.DoorWaiting);
            await Task.Delay(DoorOpenWaitTimeMs);

            // 상태 업데이트 (예: DoorState = DoorState.Open)
            Console.WriteLine($"[ID {Id}] 문 열림후 대기 완료 (현재 층: {CalulateCurrentFloorNumber().FloorNo}).");
            ChangeElevatorState(ElevatorState.DoorWaitingFinish);
        }


        /// <summary>
        /// 엘리베이터 문을 비동기적으로 닫고, 닫힐 때까지 대기합니다.
        /// </summary>
        public async Task CloseDoorAsync()
        {
            // 문이 이미 닫혀 있는지 확인하는 로직이 있다면 여기에 추가

            Console.WriteLine($"[ID {Id}] 문 닫힘 시작...");
            ChangeElevatorState(ElevatorState.DoorClosingStarted);

            // 문 닫힘 시뮬레이션
            ChangeElevatorState(ElevatorState.DoorClosing);
            await Task.Delay(DoorOperationTimeMs);

            // 상태 업데이트 (예: DoorState = DoorState.Closed)
            Console.WriteLine($"[ID {Id}] 문 닫힘 완료.");
            ChangeElevatorState(ElevatorState.DoorClosed);
            OnDoorClosed(new ElevatorEventArgs(this));
        }

        protected void ChangeDirectionState(Direction dir)
        {
            CurrentDirection = dir;
        }

        protected void ChangeElevatorState(ElevatorState state)
        {
            if(_state == state) return;
            _state = state;
            Console.WriteLine($"[ID {Id}] 엘리베이터 상태 변경: {_state}");
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
            Floor newFloor = _building.Floors[(int)Math.Round(CurrentPosition / Floor.Height) + 1];
            if (newFloor == null)
            {
                return _currentFloor;
            }

            if (_currentFloor.FloorNo != newFloor.FloorNo)
            {
                ChangeCurrentFloor(newFloor);
            }

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
            Console.WriteLine($"[ID {Id}] 엘리베이터가 {e.Elevator._currentFloor.FloorNo}층으로 이동 중입니다.");
        }
        public void OnArrived(ElevatorEventArgs e)
        {
            ElevatorEventArgs args = new ElevatorEventArgs(this);
            EventArrivedFloor?.Invoke(this, args);
            Console.WriteLine($"[ID {Id}] 엘리베이터가 {e.Elevator._currentFloor.FloorNo}층에 도착했습니다.");
        }

        public void OnDoorOpened(ElevatorEventArgs e)
        {
            ElevatorEventArgs args = new ElevatorEventArgs(this);
            EventArrivedFloor?.Invoke(this, args);
            Console.WriteLine($"[ID {Id}] 엘리베이터가 {e.Elevator._currentFloor.FloorNo}층에서 문이 열렸습니다.");
        }

        public void OnDoorClosed(ElevatorEventArgs e)
        {
            ElevatorEventArgs args = new ElevatorEventArgs(this);
            EventArrivedFloor?.Invoke(this, args);
            Console.WriteLine($"[ID {Id}] 엘리베이터가 {e.Elevator._currentFloor.FloorNo}층에서 문이 닫혔습니다.");
        }

        public void AddPerson(Person person)
        {
            if (IsMaximumOccupancy() == false)
            {
                People.Add(person);
                Console.WriteLine($"[ID {Id}] 엘리베이터에 {person.Id} 탑승. 현재 탑승 인원: {People.Count}");
            }
            else
            {
                Console.WriteLine($"[ID {Id}] 엘리베이터가 만원입니다. {person.Id} 탑승 불가.");
            }
        }

        public void RemovePerson(Person person)
        {
            if (People.Contains(person))
            {
                People.Remove(person);
                Console.WriteLine($"[ID {Id}] 엘리베이터에서 {person} 하차. 현재 탑승 인원: {People.Count}");
            }
            else
            {
                Console.WriteLine($"[ID {Id}] 엘리베이터에 {person} 이(가) 없습니다.");
            }
        }

        public bool IsMaximumOccupancy()
        {
            return People.Count >= MaximumOccupancy;
        }

        public void ReqButton(int floorNo)
        {
            if (floorNo < Building.TotalUndergroundFloor || floorNo > Building.TotalGroundFloor)
            {
                Console.WriteLine($"[ID {Id}] {floorNo}층은 유효한 층이 아닙니다.");
                return;
            }
            // 버튼이 이미 눌려 있는지 확인
            if (Buttons.Exists(b => b == Buttons[floorNo - 1] && b.Pressed))
            {
                Console.WriteLine($"[ID {Id}] {floorNo}층 버튼이 이미 눌려 있습니다. 중복 요청 무시됨.");
                return;
            }

            Buttons[floorNo - 1].Press();
            Console.WriteLine($"[ID {Id}] {floorNo}층 버튼이 눌렸습니다.");

            // 버튼이 눌렸을 때의 추가 로직 (예: 이동 요청 추가 등)을 여기에 구현
        }

        public void CancelButton(int floorNo)
        {
            if (floorNo < Building.TotalUndergroundFloor || floorNo > Building.TotalGroundFloor)
            {
                Console.WriteLine($"[ID {Id}] {floorNo}층은 유효한 층이 아닙니다.");
                return;
            }
            // 버튼이 눌려 있지 않은지 확인
            if (Buttons.Exists(b => b == Buttons[floorNo - 1] && !b.Pressed))
            {
                Console.WriteLine($"[ID {Id}] {floorNo}층 버튼이 이미 취소되어 있습니다. 중복 요청 무시됨.");
                return;
            }

            Buttons[floorNo - 1].Cancel();
            Console.WriteLine($"[ID {Id}] {floorNo}층 버튼이 취소되었습니다.");

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
                    Console.WriteLine($"[ID {Id}] {floor.FloorNo}층이 이미 경로에 포함되어 있습니다. 중복 추가 무시됨.");
                    return;
                }

                MovingPath.Add(floor);

                switch(dir)
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
                Console.WriteLine($"[ID {Id}] {floor.FloorNo}층이 이동 경로에 추가되었습니다.");
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