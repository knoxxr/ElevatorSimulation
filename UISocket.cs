using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Linq; // MinBy/OrderBy 사용을 위해 필요

namespace knoxxr.Evelvator.Core
{
    // =======================================================
    // 0. 데이터 모델 (외부 클래스에 정의되었다고 가정)
    // =======================================================
    /* * 주의: 이 모델들은 프로젝트의 다른 곳에 정의되어 있어야 합니다.
    * public class Elevator { ... } 
    * public class ElevatorManager { ... }
    * public class ElevatorStateModel { ... } // 전송용 상태 모델
    */

    // 임시 데이터 모델 정의 (컴파일 오류 방지용)
    public class ElevatorStateModel
    {
        public int id { get; set; }
        public double position_m { get; set; }
        public int floor_no { get; set; }
        public string status { get; set; }
    }
    public class FloorRequest
    {
        public int floor { get; set; }
        public string direction { get; set; }
    }

    // =======================================================
    // 3. 메인 시뮬레이션 및 TCP 통신 클래스
    // =======================================================
    public class UISocket
    {
        // C# --> Flask (상태 전송 클라이언트)
        private const string FlaskHost = "127.0.0.1";
        private const int FlaskPort = 65432; // Flask가 이 포트에서 수신(서버) 역할을 합니다.

        // Flask --> C# (요청 수신 서버)
        private const int ListenerPort = 65433; // C#이 이 포트에서 리스닝(서버) 역할을 합니다.

        private const int UpdateIntervalMs = 1000; // 1초마다 상태 전송 요청
        private const int TotalElevators = 2; // (사용되지 않음: _eleManager에서 가져옴)

        // private readonly List<Elevator> _elevators = new List<Elevator>(); // _eleManager로 대체
        private TcpClient _client;
        private StreamWriter _writer;
        private ElevatorManager _eleManager;

        public UISocket(ElevatorManager elevatorManager)
        {
            _eleManager = elevatorManager;

            // Console.WriteLine($"[C# 시뮬레이터] {TotalElevators}대의 엘리베이터 초기화 완료.");

            // Task.Delay(Timeout.Infinite, cts.Token); 부분은 Main 함수에서 처리해야 합니다.
        }

        /// <summary>
        /// Flask와의 통신을 시작합니다. 상태 전송(Client) 및 요청 수신(Server)을 병렬로 실행합니다.
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

        // ----------------------------------------------------------------
        // C# -> Flask: 상태 전송 (클라이언트 역할 - Port 65432)
        // ----------------------------------------------------------------
        private async Task ConnectAndSendLoopAsync()
        {
            while (true)
            {
                try
                {
                    _client = new TcpClient();
                    await _client.ConnectAsync(FlaskHost, FlaskPort);

                    Console.WriteLine($"[TCP 클라이언트] Flask 서버 ({FlaskHost}:{FlaskPort}) 연결 성공. 상태 전송 시작.");

                    _writer = new StreamWriter(_client.GetStream(), Encoding.UTF8) { AutoFlush = true };

                    while (_client.Connected)
                    {
                        // 1초마다 상태를 JSON으로 직렬화 및 전송
                        string jsonPayload = GetCurrentStatesJson();
                        await _writer.WriteLineAsync(jsonPayload);

                        await Task.Delay(UpdateIntervalMs); // 1초 대기
                    }
                }
                catch (SocketException)
                {
                    Console.WriteLine("[TCP 클라이언트] Flask 상태 수신 서버 연결 실패. 5초 후 재시도...");
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
        /// 모든 엘리베이터의 상태를 Flask 서버가 예상하는 JSON 배열 형식으로 직렬화합니다.
        /// </summary>
        private string GetCurrentStatesJson()
        {
            // 실제 ElevatorManager의 데이터를 사용하도록 수정
            List<ElevatorStateModel> states = new List<ElevatorStateModel>();
            foreach (var elevator in _eleManager.Elevators)
            {
                // 실제 ToState() 메서드를 호출해야 함. 여기서는 Mock 객체를 사용하여 구조를 맞춥니다.
                states.Add(new ElevatorStateModel
                {
                    id = 0,
                    position_m = 0.0,
                    floor_no = 1,
                    status = "Idle"
                });
            }

            // JsonSerializer 설정: JSON 키는 소문자(snake_case)로 유지
            var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            return JsonSerializer.Serialize(states); // _eleManager가 아닌 상태 목록을 직렬화
        }

        // ----------------------------------------------------------------
        // Flask -> C#: 요청 수신 (서버 역할 - Port 65433)
        // ----------------------------------------------------------------

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
                    string jsonRequest = await reader.ReadLineAsync();
                    if (string.IsNullOrEmpty(jsonRequest)) return;

                    // JSON 역직렬화
                    var requestData = JsonSerializer.Deserialize<FloorRequest>(jsonRequest);

                    if (requestData != null)
                    {
                        // 요청 처리 로직: 가장 한가한 엘리베이터에 경로 추가
                        // MinBy는 Linq에 포함되어 있습니다.
                        //Elevator assignedElevator = _eleManager.Elevators.OrderBy(e => e.Path.Count).FirstOrDefault();

                        //if (assignedElevator != null)
                        {
                            // 엘리베이터 관리자에게 요청을 전달하여 처리하도록 합니다.
                            //_eleManager.AddRequestToElevator(requestData.floor, requestData.direction, assignedElevator);
                            //Console.WriteLine($"[요청 수신] Floor {requestData.floor}, Dir {requestData.direction}. Assigned to Elevator {assignedElevator.GetHashCode()}.");
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
    }
}