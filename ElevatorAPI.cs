using System;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.IO;

namespace knoxxr.Evelvator.Core
{
    // 데이터 모델 정의
    public class Post
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Content { get; set; }
    }

    // =======================================================
    // 3. 메인 시뮬레이션 및 TCP 통신 클래스
    // =======================================================
    public class ElevatorAPI
    {
        private readonly HttpListener listener = new HttpListener();
        private static List<Post> posts = new List<Post>
    {
        new Post { Id = 1, Title = "콘솔 백엔드 첫 글", Content = "HttpListener 테스트입니다." },
        new Post { Id = 2, Title = "두 번째 글", Content = "C# 콘솔 앱에서 제공하는 API." }
    };
        private static int nextId = 3;

        // HttpListener는 관리자 권한이 필요할 수 있습니다.
        private const string ApiPrefix = "http://localhost:5000/api/posts/";
        private ElevatorManager eleMgr;
        public ElevatorAPI(ElevatorManager elevatorManager)
        {
            eleMgr = elevatorManager;
            // 리스너가 수신할 URL을 지정합니다.
            listener.Prefixes.Add(ApiPrefix);
        }

        public async Task Start()
        {
            listener.Start();
            Console.WriteLine($"서버 시작됨. {ApiPrefix}에서 수신 중...");

            while (listener.IsListening)
            {
                // 요청을 비동기적으로 기다립니다.
                HttpListenerContext context = await listener.GetContextAsync();
                _ = Task.Run(() => HandleRequest(context)); // 새 Task에서 요청 처리
            }
        }

        private async Task HandleRequest(HttpListenerContext context)
        {
            HttpListenerRequest request = context.Request;
            HttpListenerResponse response = context.Response;

            // CORS 허용 (실제 환경에서는 특정 출처만 허용해야 합니다.)
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
                string urlPath = request.Url.AbsolutePath;

                if (urlPath == "/api/posts")
                {
                    if (request.HttpMethod == "GET")
                    {
                        await HandleGetPosts(response);
                    }
                    else if (request.HttpMethod == "POST")
                    {
                        await HandleCreatePost(request, response);
                    }
                    else
                    {
                        response.StatusCode = (int)HttpStatusCode.MethodNotAllowed; // 405
                    }
                }
                else
                {
                    response.StatusCode = (int)HttpStatusCode.NotFound; // 404
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

        // GET 요청 처리 로직
        private async Task HandleGetPosts(HttpListenerResponse response)
        {
            response.ContentType = "application/json";

            // C# 객체를 JSON 문자열로 직렬화
            string jsonResponse = JsonSerializer.Serialize(posts);
            byte[] buffer = Encoding.UTF8.GetBytes(jsonResponse);

            response.ContentLength64 = buffer.Length;
            response.StatusCode = (int)HttpStatusCode.OK; // 200

            await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
        }

        // POST 요청 처리 로직
        private async Task HandleCreatePost(HttpListenerRequest request, HttpListenerResponse response)
        {
            // 요청 본문(Body) 읽기
            using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
            string requestBody = await reader.ReadToEndAsync();

            // JSON 문자열을 C# 객체로 역직렬화
            Post newPost = JsonSerializer.Deserialize<Post>(requestBody);

            newPost.Id = nextId++;
            posts.Add(newPost);

            response.ContentType = "application/json";
            string jsonResponse = JsonSerializer.Serialize(newPost);
            byte[] buffer = Encoding.UTF8.GetBytes(jsonResponse);

            response.ContentLength64 = buffer.Length;
            response.StatusCode = (int)HttpStatusCode.Created; // 201

            await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
        }

        public void Stop()
        {
            listener.Stop();
            listener.Close();
        }
    }
}