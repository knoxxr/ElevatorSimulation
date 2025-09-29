using knoxxr.Evelvator.Sim;
using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace knoxxr.Evelvator.Core
{
    public class Elevator
    {
        public event EventHandler<ElevatorEventArgs> EventArrivedFloor;
        public event EventHandler<ElevatorEventArgs> EventDoorOpened;
        public event EventHandler<ElevatorEventArgs> EventDoorClosed;


        public int Id;
        public Floor CurrentFloor;
        public int MaximumOccupancy = 15;
        public int Height = 2700; // 엘리베이터 높이 (mm)
        public ElevatorState State = ElevatorState.Idle;
        // 가정: Elevator 클래스 내부에 정의됩니다.
        // 문이 열리는 데 걸리는 시간 (ms)
        private const int DoorOperationTimeMs = 2000;

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

        public Elevator(int id)
        {
            Id = id;
        }

        public async void ExecuteCallMission(Floor targetFloor)
        {
            await MoveAsync(targetFloor);
            await OpenDoorAsync();

            ChangeElevatorState(ElevatorState.Idle);
        }

        public async Task MoveAsync(Floor targetFloor)
        {
            if (targetFloor == null) return;
            if (State == ElevatorState.Moving) return;

            Console.WriteLine($"[ID {Id}] / [요청] {targetFloor.FloorNo}층으로 이동 요청됨.");

            cts = new CancellationTokenSource();
            //await MoveElevatorAsync(targetFloor.Position, targetFloor.FloorNo, cts.Token);

            if (State == ElevatorState.Moving) return;
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

                    int curFloor = GetCurrentFloorNumber();

                    Console.WriteLine($"[ID {Id}] / [이동] 위치: {CurrentPosition:F2}m, 속도: {currentVelocity:F2}m/s, 목표: {targetPosition:F2}m, 목표층 : {targetFloor.FloorNo}, 현재 층수 : {curFloor}, 방향 : {CurrentDirection.ToString()}");
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

        /// <summary>
        /// 엘리베이터를 지정된 목표 위치로 비동기 이동시킵니다.
        /// </summary>
        /// <param name="destination">목표 위치 (m)</param>
        /// <param name="cancellationToken">이동 취소를 위한 토큰</param>
        /// <returns>이동 완료 Task</returns>
        protected async Task MoveElevatorAsync(double destination, int targetFloorNo, CancellationToken cancellationToken)
        {
            if (State == ElevatorState.Moving) return;
            ChangeElevatorState(ElevatorState.Moving);

            // 이동 루프는 50ms마다 갱신 (시뮬레이션 시간 간격)
            const int updateIntervalMs = 50;
            double deltaTime = updateIntervalMs / 1000.0; // 0.05초

            try
            {
                double targetPosition = destination;

                while (Math.Abs(CurrentPosition - targetPosition) > 0.001 || Math.Abs(currentVelocity) > 0.001)
                {
                    // 1. 취소 요청 확인 및 목표 위치 변경 (가장 가까운 층으로)
                    if (cancellationToken.IsCancellationRequested)
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

                    int curFloor = GetCurrentFloorNumber();

                    Console.WriteLine($"[ID {Id}] / [이동] 위치: {CurrentPosition:F2}m, 속도: {currentVelocity:F2}m/s, 목표: {targetPosition:F2}m, 목표층 : {targetFloorNo}, 현재 층수 : {curFloor}, 방향 : {CurrentDirection.ToString()}");
                    // 5. 시뮬레이션 지연 (비동기 대기)

                    await Task.Delay(updateIntervalMs, cancellationToken);
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
            Console.WriteLine($"[ID {Id}] 문 열림 완료 (현재 층: {GetCurrentFloorNumber()}).");
            ChangeElevatorState(ElevatorState.DoorOpened);
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
        }

        protected void ChangeDirectionState(Direction dir)
        {
            CurrentDirection = dir;
        }

        protected void ChangeElevatorState(ElevatorState state)
        {
            State = state;
            Console.WriteLine($"[ID {Id}] 엘리베이터 상태 변경: {State}");
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

        protected int GetCurrentFloorNumber()
        {
            // CurrentPosition / FloorHeight 계산 후 반올림하여 층 번호(1층부터 시작)를 얻습니다.
            return (int)Math.Round(CurrentPosition / Floor.Height) + 1;
        }

        public bool IsAvailable(Floor reqFloor, Direction dir)
        {
            if (State == ElevatorState.Idle)
            {
                return true;
            }
            return false;
        }

        public void OnArrived(ElevatorEventArgs e)
        {
            ElevatorEventArgs args = new ElevatorEventArgs(this);
            EventArrivedFloor?.Invoke(this, args);
            Console.WriteLine($"[ID {Id}] 엘리베이터가 {e.Elevator.CurrentFloor}층에 도착했습니다.");
        }

        public void OnDoorOpened(ElevatorEventArgs e)
        {
            ElevatorEventArgs args = new ElevatorEventArgs(this);
            EventArrivedFloor?.Invoke(this, args);
            Console.WriteLine($"[ID {Id}] 엘리베이터가 {e.Elevator.CurrentFloor}층에서 문이 열렸습니다.");
        }

        public void OnDoorClosed(ElevatorEventArgs e)
        {
            ElevatorEventArgs args = new ElevatorEventArgs(this);
            EventArrivedFloor?.Invoke(this, args);
            Console.WriteLine($"[ID {Id}] 엘리베이터가 {e.Elevator.CurrentFloor}층에서 문이 닫혔습니다.");
        }
    }

    public enum ElevatorState
    {
        Idle,
        Moving,
        DoorOpenningStarting,
        DoorOpening,
        DoorOpened,
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