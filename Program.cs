using System.Threading;
using System.Timers;
using knoxxr.Evelvator.Core;
using knoxxr.Evelvator.Sim;
class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("Hello, World!");

        Building building = new Building();

        //building.eleMgr.Elevators[1].Move(building.Floors[10]);
        //building.eleMgr.Elevators[2].Move(building.Floors[50]);

        var worker = new Worker();
        worker.Start();

        Console.WriteLine("Worker 시작. Enter를 누르면 종료됩니다.");

        // **이 부분이 중요:** Enter가 눌릴 때까지 메인 스레드를 차단하고 포그라운드로 유지
        Console.ReadLine();

        worker.Stop();
        Console.WriteLine("프로그램 종료.");
    }
}
public class Worker
{
    private System.Threading.Timer _timer;
    // 프로그램 종료를 제어할 이벤트
    public readonly ManualResetEventSlim ExitEvent = new ManualResetEventSlim(false);

    public void Start()
    {
        // 1초마다 DoWork 콜백 실행 (백그라운드 스레드)
        _timer = new System.Threading.Timer(DoWork, null, 0, 1000);
    }

    private void DoWork(object state)
    {
        // 이 루프는 백그라운드 스레드에서 돌고 있음
        //Console.WriteLine($"타이머 콜백 실행 중: {DateTime.Now}");        
    }

    public void Stop()
    {
        _timer?.Dispose();
        // 종료 이벤트 발생
        ExitEvent.Set();
    }
}

