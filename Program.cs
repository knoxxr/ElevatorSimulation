using System.Threading;
using System.Timers;
using knoxxr.Evelvator.Core;
using knoxxr.Evelvator.Sim;
class Program
{
    static void Main(string[] args)
    {
        Logger.Info("프로그램 시작");
        Building _building = new Building();
        Sim_ReqGenerator _ReqGenerator = new Sim_ReqGenerator(_building);
        Logger.Info("프로그램 종료");
    }
}
