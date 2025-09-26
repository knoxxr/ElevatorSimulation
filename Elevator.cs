using knoxxr.Evelvator.Sim;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace knoxxr.Evelvator.Core
{
    public class Elevator
    {
        public int Id;
        public Floor CurrentFloor;
        public int MaximumOccupancy = 15;
        public int Height = 2700; // 엘리베이터 높이 (mm)
        public ElevatorState State = ElevatorState.Idle;

        // === 설정 변수 ===
        private const double MaxSpeed = 5000.0; // 최대 속도 (mm/s)
        private const double Acceleration = 1000.0; // 가속도 (mm/s^2)
        private const double DecelerationDistance = 2000.0; // 감속 시작 거리 (m)
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
        public void Move(Floor targetFloor)
        {
            if (targetFloor == null) return;
            if (State == ElevatorState.Moving) return;

            Console.WriteLine($"[ID {Id}] / [요청] {targetFloor.FloorNo}층으로 이동 요청됨.");
            StartMove(targetFloor.Position);
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
        protected async Task MoveElevatorAsync(double destination, CancellationToken cancellationToken)
        {
            if (State == ElevatorState.Moving) return;
            State = ElevatorState.Moving;

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

                    Console.WriteLine($"[ID {Id}] / [이동] 위치: {CurrentPosition:F2}m, 속도: {currentVelocity:F2}m/s, 목표: {targetPosition:F2}m, 현재 층수 : {curFloor}, 방향 : {CurrentDirection.ToString()}");
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
                State = ElevatorState.Idle;
            }
        }

        protected void ChangeDirectionState(Direction dir)
        {
            CurrentDirection = dir;
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
        protected async Task StartMove(double destination)
        {
            cts = new CancellationTokenSource();
            await MoveElevatorAsync(destination, cts.Token);
        }

        protected int GetCurrentFloorNumber()
        {
            // CurrentPosition / FloorHeight 계산 후 반올림하여 층 번호(1층부터 시작)를 얻습니다.
            return (int)Math.Round(CurrentPosition / Floor.Height) + 1;
        }
    }

    public enum ElevatorState
    {
        Idle,
        Moving,
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
}