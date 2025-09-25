using System.Threading;

namespace knoxxr.Evelvator.Sim
{
    public class Sim_ReqGenerator
    {
        private Timer Scheduler;

        protected List<Sim_Person> Persons = new List<Sim_Person>();
        public Sim_ReqGenerator()
        {
            Scheduler = new Timer(TimerCallback, null, 0, 500);
        }

        private static void TimerCallback(object state)
        {

        }

        public void Initialize()
        {
        }
    }
}