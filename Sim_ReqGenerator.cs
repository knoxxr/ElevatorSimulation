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

        private Building _building;

        protected Dictionary<int, Person> People = new Dictionary<int, Person>();
        public Sim_ReqGenerator(Building building)
        {
            _building = building;

            Task taskA = Task.Run(() => RunTaskMethod());
        }

        public async Task RunTaskMethod()
        {
            while (true)
            {
                int interval = _random.Next(_minIntervalMs, DensityFactor + 1);

                Floor floor = _building.Floors[GetRandomFloor()];
                Person newPerson = new Person(floor, _building);
                newPerson.EventRequestCompleted += (s, e) =>
                {
                    Person person = (Person)s;
                    People.Remove(person.Id);
                    Console.WriteLine($"[완료] {person}");
                };
                People.Add(newPerson.Id, newPerson);

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