using System;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.IO;

namespace knoxxr.Evelvator.Core
{
    // =======================================================
    // 1. 데이터 모델 정의 (API 응답 및 요청 본문에 사용)
    // =======================================================
    public class ElevatorStatus
    {
        public int ElevatorId { get; set; }
        public int CurrentFloor { get; set; }
        public string Direction { get; set; } // "UP", "DOWN", "STOP"
        public List<int> DestinationFloors { get; set; } // 엘리베이터 내부 요청 층
    }

    public class CallRequest
    {
        public int Floor { get; set; }
        public string Direction { get; set; } // "UP", "DOWN" (탑승 요청 방향)
        public DateTime RequestTime { get; set; } // 요청 시간 (시뮬레이션 용)
    }

    public class ElevatorConfigResponse
    {
        public int BuildingFloors { get; set; }
        public int ElevatorCount { get; set; }
    }

    // =======================================================
    // 2. 메인 시뮬레이션 및 HTTP 통신 클래스
    // =======================================================
    public class ElevatorAPI
    {
        private readonly HttpListener listener = new HttpListener();
        
        // 시뮬레이션 상태 (임시로 API 클래스 내부에 정의)
        private static readonly object StateLock = new object();
        private int buildingFloors = 15;
        private int elevatorCount = 3;
        private List<CallRequest> pendingCalls = new List<CallRequest>();
        private List<ElevatorStatus> currentElevatorStates = new List<ElevatorStatus>();

        // HttpListener는 관리자 권한이 필요할 수 있습니다.
        // API 기본 주소 (65432 포트 사용)
        private const string ApiPrefix = "http://localhost:65432/api/";
        private ElevatorManager eleMgr; // 외부 엘리베이터 관리 클래스 (로직 처리 담당)
        
        public ElevatorAPI(ElevatorManager elevatorManager)
        {
            eleMgr = elevatorManager;
            
            // 리스너가 수신할 URL을 지정합니다. 모든 /api/ 요청을 받습니다.
            listener.Prefixes.Add(ApiPrefix);

            // 초기 엘리베이터 상태 Mock 설정
            lock (StateLock)
            {
                for (int i = 1; i <= elevatorCount; i++)
                {
                    currentElevatorStates.Add(new ElevatorStatus
                    {
                        ElevatorId = i,
                        CurrentFloor = 1,
                        Direction = "STOP",
                        DestinationFloors = new List<int>()
                    });
                }
            }
        }

        public async Task Start()
        {
            listener.Start();
            Logger.Info($"서버 시작됨. {ApiPrefix}에서 수신 중...");

            // 클라이언트 요청이 들어올 때까지 무한히 대기합니다.
            while (listener.IsListening)
            {
                // 요청을 비동기적으로 기다립니다. (접속 대기)
                HttpListenerContext context = await listener.GetContextAsync();
                _ = Task.Run(() => HandleRequest(context)); // 새 Task에서 요청 처리
            }
        }

        private async Task HandleRequest(HttpListenerContext context)
        {
            HttpListenerRequest request = context.Request;
            HttpListenerResponse response = context.Response;

            // CORS 설정
            response.AppendHeader("Access-Control-Allow-Origin", "*");
            response.AppendHeader("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
            response.AppendHeader("Access-Control-Allow-Headers", "Content-Type");

            // OPTIONS 요청 처리 (Preflight 요청)
            if (request.HttpMethod == "OPTIONS")
            {
                response.StatusCode = (int)HttpStatusCode.NoContent; // 204
                response.Close();
                return;
            }

            try
            {
                string urlPath = request.Url.AbsolutePath.TrimEnd('/'); // 마지막 슬래시 제거
                Logger.Info($"-> [{request.HttpMethod}] {urlPath} 요청 수신");

                switch (urlPath)
                {
                    case "/api/config":
                        if (request.HttpMethod == "GET") await HandleGetConfig(response);
                        else if (request.HttpMethod == "POST") await HandleUpdateConfig(request, response);
                        else response.StatusCode = (int)HttpStatusCode.MethodNotAllowed; // 405
                        break;
                    
                    case "/api/status":
                        if (request.HttpMethod == "GET") await HandleGetStatus(response);
                        else response.StatusCode = (int)HttpStatusCode.MethodNotAllowed; // 405
                        break;
                    
                    case "/api/request":
                        if (request.HttpMethod == "POST") await HandleAddUserRequest(request, response);
                        else response.StatusCode = (int)HttpStatusCode.MethodNotAllowed; // 405
                        break;

                    default:
                        response.StatusCode = (int)HttpStatusCode.NotFound; // 404
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"요청 처리 중 오류 발생: {ex.Message}");
                response.StatusCode = (int)HttpStatusCode.InternalServerError; // 500
            }
            finally
            {
                response.Close(); // 응답을 완료하고 연결을 닫습니다.
            }
        }

        // =======================================================
        // 3. GET: 초기 빌딩 설정 정보 응답 (빌딩 층수, 엘리베이터 댓수)
        // =======================================================
        private async Task HandleGetConfig(HttpListenerResponse response)
        {
            response.ContentType = "application/json";
            
            var config = new ElevatorConfigResponse 
            { 
                BuildingFloors = buildingFloors, 
                ElevatorCount = elevatorCount 
            };
            
            string jsonResponse = JsonSerializer.Serialize(config);
            await WriteResponse(response, jsonResponse, HttpStatusCode.OK);
        }

        // =======================================================
        // 4. GET: 현재 엘리베이터 상태 및 대기 요청 정보 응답
        // =======================================================
        private async Task HandleGetStatus(HttpListenerResponse response)
        {
            response.ContentType = "application/json";

            var statusData = new
            {
                PendingCalls = pendingCalls, // 현재 앨리베이터를 사용하려는 사람들의 요청 정보
                ElevatorStates = currentElevatorStates // 엘리베이터의 현재 위치, 요청 처리 정보
            };

            string jsonResponse = JsonSerializer.Serialize(statusData);
            await WriteResponse(response, jsonResponse, HttpStatusCode.OK);
        }

        // =======================================================
        // 5. POST: 빌딩 설정 정보 변경 (층수 또는 댓수)
        // =======================================================
        private async Task HandleUpdateConfig(HttpListenerRequest request, HttpListenerResponse response)
        {
            var configData = await DeserializeRequest<ElevatorConfigResponse>(request);

            if (configData == null)
            {
                await WriteResponse(response, "Invalid config data.", HttpStatusCode.BadRequest); // 400
                return;
            }

            lock (StateLock)
            {
                if (configData.BuildingFloors > 0)
                {
                    buildingFloors = configData.BuildingFloors;
                    Logger.Info($"[Config] 층수 변경됨: {buildingFloors}층");
                }
                if (configData.ElevatorCount >= 0)
                {
                    elevatorCount = configData.ElevatorCount;
                    // TODO: 엘리베이터 댓수 변경 시 eleMgr에 반영하는 로직 추가 필요
                   // Console.WriteLine($"[Config] 엘리베이터 댓수 변경됨: {elevatorCount}대");
                }
            }
            
            await WriteResponse(response, JsonSerializer.Serialize(configData), HttpStatusCode.OK);
        }

        // =======================================================
        // 6. POST: 사용자 엘리베이터 요청 추가
        // =======================================================
        private async Task HandleAddUserRequest(HttpListenerRequest request, HttpListenerResponse response)
        {
            var userCall = await DeserializeRequest<CallRequest>(request);

            if (userCall == null || userCall.Floor <= 0 || userCall.Floor > buildingFloors)
            {
                await WriteResponse(response, "Invalid floor or request data.", HttpStatusCode.BadRequest); // 400
                return;
            }

            lock (StateLock)
            {
                userCall.RequestTime = DateTime.UtcNow;
                pendingCalls.Add(userCall);
                // TODO: eleMgr에 요청을 전달하여 엘리베이터 배차 로직 시작
                Logger.Info($"[Request] 새로운 요청 추가됨: {userCall.Floor}층, {userCall.Direction}");
            }

            string jsonResponse = JsonSerializer.Serialize(userCall);
            await WriteResponse(response, jsonResponse, HttpStatusCode.Created); // 201
        }

        // =======================================================
        // 7. 헬퍼 메서드
        // =======================================================
        private async Task<T> DeserializeRequest<T>(HttpListenerRequest request) where T : class
        {
            try
            {
                using var reader = new StreamReader(request.InputStream, request.ContentEncoding ?? Encoding.UTF8);
                string requestBody = await reader.ReadToEndAsync();
                return JsonSerializer.Deserialize<T>(requestBody);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"역직렬화 오류: {ex.Message}");
                return null;
            }
        }

        private async Task WriteResponse(HttpListenerResponse response, string jsonContent, HttpStatusCode statusCode)
        {
            response.ContentType = "application/json";
            byte[] buffer = Encoding.UTF8.GetBytes(jsonContent);

            response.ContentLength64 = buffer.Length;
            response.StatusCode = (int)statusCode;

            await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
        }

        public void Stop()
        {
            listener.Stop();
            listener.Close();
        }
    }
}
