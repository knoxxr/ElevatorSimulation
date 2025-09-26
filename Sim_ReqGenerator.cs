using System.Threading.Tasks;
using knoxxr.Evelvator.Core;

namespace knoxxr.Evelvator.Sim
{
    public class Sim_ReqGenerator
    {
        // 밀집도 조절 변수: 값이 높을수록 생성 간격이 깁니다.
        // (예: 1000ms ~ 3000ms 사이의 랜덤 간격 생성)
        public int DensityFactor { get; set; } = 100000;
        public double GroundFloorRatio { get; set; } = 0.5; // 50% 비율
        private readonly int _minIntervalMs = 10000;
        private readonly Random _random = new Random();

        private Building Building;

        protected List<Person> Persons = new List<Person>();
        public Sim_ReqGenerator(Building building)
        {
            Building = building;

            Task taskA = Task.Run(() => RunTaskMethod());
        }

        public async Task RunTaskMethod()
        {
            while (true)
            {
                int interval = _random.Next(_minIntervalMs, DensityFactor + 1);

                Floor floor = Building.Floors[GetRandomFloor()];
                Person newPerson = new Person(floor);
                Persons.Add(newPerson);

                Console.WriteLine($"[생성됨] {newPerson}");
                await Task.Delay(interval);
            }
        }

        private int GetRandomFloor()
        {
            int floor;
            floor = _random.Next(Building.TotalUndergroundFloor, Building.TotalGroundFloor + 1);
            return floor;
        }

        public void Initialize()
        {
        }
    }
}