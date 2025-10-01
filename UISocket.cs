using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace knoxxr.Evelvator.Core
{
    // =======================================================
    // 3. 메인 시뮬레이션 및 TCP 통신 클래스
    // =======================================================
    // =======================================================
    // 3. 메인 시뮬레이션 및 TCP 통신 클래스
    // =======================================================
    public class UISocket
    {
        // C# --> Flask (상태 전송)
        private const string FlaskHost = "127.0.0.1";
        private const int FlaskPort = 65432;

        // Flask --> C# (요청 수신)
        private const int ListenerPort = 65433; // 새로운 포트 사용 (백엔드 서버 역할)

        private const int UpdateIntervalMs = 100;
        private const int TotalElevators = 2;

        private readonly List<Elevator> _elevators = new List<Elevator>();
        private TcpClient _client;
        private StreamWriter _writer;

        private ElevatorManager _eleManager;

        public UISocket(ElevatorManager elevatorManager)
        {
            _eleManager = elevatorManager;
            // 엘리베이터 초기화
            //StartSimulationAsync();

            Console.WriteLine($"[C# 시뮬레이터] {TotalElevators}대의 엘리베이터 초기화 완료.");

            var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (sender, eventArgs) =>
            {
                eventArgs.Cancel = true;
                cts.Cancel();
                Console.WriteLine("프로그램 종료 요청...");
            };
            Task.Delay(Timeout.Infinite, cts.Token);
        }

        /// <summary>
        /// Flask로부터 요청을 수신할 서버를 시작하고, 시뮬레이션을 시작합니다.
        /// </summary>
        public async Task StartSimulationAsync()
        {
            // 1. Flask로부터 요청을 수신할 서버(리스너) 시작 (Task 분리)
            var listenerTask = Task.Run(StartRequestListener);

            // 2. Flask로 상태를 전송할 클라이언트(sender) 시작
            var senderTask = ConnectAndSendLoopAsync();

            // 두 작업이 모두 완료될 때까지 대기
            await Task.WhenAll(listenerTask, senderTask);
        }

        /// <summary>
        /// Flask 서버에 연결하고 실시간 상태를 전송합니다. (클라이언트 역할)
        /// </summary>
        private async Task ConnectAndSendLoopAsync()
        {
            while (true)
            {
                try
                {
                    _client = new TcpClient();
                    await _client.ConnectAsync(FlaskHost, FlaskPort);

                    Console.WriteLine($"[TCP 클라이언트] Flask 서버 ({FlaskHost}:{FlaskPort}) 연결 성공.");

                    _writer = new StreamWriter(_client.GetStream(), Encoding.UTF8) { AutoFlush = true };

                    while (_client.Connected)
                    {
                        double deltaTime = UpdateIntervalMs / 1000.0;

                        // 1. 시뮬레이션 업데이트
                        foreach (var elevator in _elevators)
                        {
                            //elevator.UpdateMockMovement(deltaTime);
                        }

                        // 2. 현재 상태를 JSON으로 직렬화 및 전송
                        string jsonPayload = GetCurrentStatesJson();
                        await _writer.WriteLineAsync(jsonPayload);

                        await Task.Delay(UpdateIntervalMs);
                    }
                }
                catch (SocketException)
                {
                    Console.WriteLine("[TCP 클라이언트] Flask 서버 연결 실패. 5초 후 재시도...");
                    await Task.Delay(5000);
                }
                catch (IOException)
                {
                    Console.WriteLine("[TCP 클라이언트] Flask 서버 연결 끊김. 5초 후 재연결 시도...");
                    await Task.Delay(5000);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[치명적 오류 - 클라이언트] {ex.Message}");
                    await Task.Delay(5000);
                }
                finally
                {
                    _writer?.Dispose();
                    _client?.Close();
                }
            }
        }

        /// <summary>
        /// Flask로부터 요청을 수신하기 위해 리스너를 시작합니다. (서버 역할)
        /// </summary>
        private async Task StartRequestListener()
        {
            TcpListener listener = null;
            try
            {
                listener = new TcpListener(System.Net.IPAddress.Parse(FlaskHost), ListenerPort);
                listener.Start();
                Console.WriteLine($"[TCP 서버] Flask 요청 수신 대기 중... ({FlaskHost}:{ListenerPort})");

                while (true)
                {
                    TcpClient flaskClient = await listener.AcceptTcpClientAsync();
                    // Task를 분리하여 여러 요청을 동시에 처리할 수 있게 합니다.
                    _ = HandleFlaskRequestAsync(flaskClient);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TCP 서버 오류] 요청 리스너 실패: {ex.Message}");
            }
            finally
            {
                listener?.Stop();
            }
        }

        /// <summary>
        /// Flask로부터 수신된 요청을 처리합니다.
        /// </summary>
        private async Task HandleFlaskRequestAsync(TcpClient client)
        {
            try
            {
                using (var stream = client.GetStream())
                using (var reader = new StreamReader(stream, Encoding.UTF8))
                {
                    // Flask는 보통 요청 시 하나의 JSON 객체를 보낸다고 가정
                    string jsonRequest = await reader.ReadLineAsync();
                    if (string.IsNullOrEmpty(jsonRequest)) return;

                    // JSON 역직렬화
                    //var requestData = JsonSerializer.Deserialize<FloorRequest>(jsonRequest);

                    //if (requestData != null)
                    {
                        // 요청 처리 로직: 가장 한가한 엘리베이터에 경로 추가
                        // 실제 엘리베이터 로직에서는 스케줄링 알고리즘이 들어갑니다.
                        //Elevator assignedElevator = _elevators.MinBy(e => e.Path.Count);

                        //if (assignedElevator != null)
                        {
                            //assignedElevator.AddDestination(requestData.floor);
                            //Console.WriteLine($"[요청 수신] Floor {requestData.floor}, Dir {requestData.direction}. Assigned to Elevator {assignedElevator.Id}.");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[요청 처리 오류] {ex.Message}");
            }
            finally
            {
                client.Close();
            }
        }

        /// <summary>
        /// 모든 엘리베이터의 상태를 Flask 서버가 예상하는 JSON 배열 형식으로 직렬화합니다.
        /// </summary>
        private string GetCurrentStatesJson()
        {
            List<Elevator> states = new List<Elevator>();
            foreach (var elevator in _elevators)
            {
                //states.Add(elevator.ToState());
            }

            // JsonSerializer 설정: JSON 키는 소문자(snake_case)로 유지
            var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            return JsonSerializer.Serialize(_eleManager);
        }
    }
}