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
        private readonly int _minIntervalMs = 5000;
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
                try
                {
                    int interval = _random.Next(_minIntervalMs, DensityFactor + 1);

                    Floor floor = _building.Floors[GetRandomFloor()];
                    if (floor == null)
                    {
                        Console.WriteLine("오류: 유효하지 않은 층 선택됨.");
                        continue;
                    }

                    if (_building == null)
                    {
                        Console.WriteLine("오류: 유효하지 않은 층 선택됨.");
                        continue;
                    }
                    Person newPerson = new Person(floor, _building);
                    if (newPerson == null)
                    {
                        Console.WriteLine("오류: 유효하지 않은 층 선택됨.");
                        continue;
                    }

                    if (People == null)
                    {
                        Console.WriteLine("오류: 유효하지 않은 층 선택됨.");
                        continue;
                    }

                    newPerson.EventRequestCompleted += OnEventRequestCompleted;
                    People.Add(newPerson.Id, newPerson);

                    Console.WriteLine($"[생성됨] {newPerson}");
                    await Task.Delay(interval);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"오류 발생: {ex.Message}");
                }
               
            }
        }

        private void OnEventRequestCompleted(object sender, EventArgs e)
        {
            // sender(s)는 이벤트를 발생시킨 객체이므로 Person 타입으로 형 변환합니다.
            Person person = (Person)sender;
            person.EventRequestCompleted -= OnEventRequestCompleted;
            // People 컬렉션에서 해당 Person 객체를 제거합니다.
            People.Remove(person.Id);

            // 완료 메시지를 출력합니다.
            Console.WriteLine($"[완료] {person}");
        }

        private int GetRandomFloor()
        {
            int floor;

            try
            {
                floor = _random.Next(Building.TotalUndergroundFloor, Building.TotalGroundFloor + 1);
                if (_random.NextDouble() < GroundFloorRatio)
                {
                    floor = 1; // 지상 1층 선택 확률 증가
                }               
            }
            catch (Exception ex)
            {
                Console.WriteLine($"오류 발생: {ex.Message}");
                floor = 1; // 기본값 설정
            }
            return floor;
        }

        public void Initialize()
        {
        }
    }
}