using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using knoxxr.Evelvator.Core;

namespace knoxxr.Evelvator.Sim
{
    public class Sim_ReqGenerator
    {
        // 밀집도 조절 변수: 값이 높을수록 생성 간격이 깁니다.
        // (예: 1000ms ~ 3000ms 사이의 랜덤 간격 생성)
        public int DensityFactor { get; set; } = 15000;
        public double GroundFloorRatio { get; set; } = 0.5; // 50% 비율
        private readonly int _minIntervalMs = 2000;
        private readonly Random _random = new Random();
        private Building _building;

        protected Dictionary<int, Person> _people = new Dictionary<int, Person>();
        public Sim_ReqGenerator(Building building)
        {
            _building = building;
            InitScheuler();
            InitPainting();
        }
        private void InitScheuler()
        {
            Thread SchedulerThread = new Thread(ScheduleElevator);
            SchedulerThread.IsBackground = false;
            SchedulerThread.Start();
        }
        private void InitPainting()
        {
            Thread DrawingThread = new Thread(Drawing);
            DrawingThread.IsBackground = true;
            DrawingThread.Start();
        }

         public void Drawing()
        {
            while (true)
            {
                try
                {
                    //DrawConsole();
                    Thread.Sleep(500);
                }
                catch (Exception ex)
                {
                    Logger.Error($"오류 발생: {ex.Message}");
                }
            }
        }

        public void ScheduleElevator()
        {
            while (true)
            {
                try
                {
                    int interval = _random.Next(_minIntervalMs, DensityFactor + 1);

                    Floor floor = _building._floors[GetRandomFloor()];
                    if (floor == null)
                    {
                        Logger.Info("오류: 유효하지 않은 층 선택됨.");
                        continue;
                    }
                    Person newPerson = new Person(floor, _building);

                    newPerson.EventRequestCompleted += OnEventRequestCompleted;
                    _people.Add(newPerson.Id, newPerson);

                    Logger.Info($"[생성됨] {newPerson}");
                    Thread.Sleep(interval);
                }
                catch (Exception ex)
                {
                    Logger.Error($"오류 발생: {ex.Message}");
                }
            }
        }

        private void OnEventRequestCompleted(object sender, EventArgs e)
        {
            // sender(s)는 이벤트를 발생시킨 객체이므로 Person 타입으로 형 변환합니다.
            Person person = (Person)sender;
            person.EventRequestCompleted -= OnEventRequestCompleted;
            // People 컬렉션에서 해당 Person 객체를 제거합니다.
            _people.Remove(person.Id);

            // 완료 메시지를 출력합니다.
            Logger.Info($"[완료] {person}");
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
                Logger.Error($"오류 발생: {ex.Message}");
                floor = 1; // 기본값 설정
            }
            return floor;
        }

        public void Initialize()
        {
        }
        
        private void DrawConsole()
        {
            Console.Clear();
            Dictionary<int, string> textArray = new Dictionary<int, string>();

            foreach (Elevator elevator in _building._eleMgr._elevators.Values)
            {
                string eleText = String.Format($" <E{elevator.Id}>: ");
                Console.Write(eleText);
                foreach (Person person in elevator._people)
                {
                    Console.Write(string.Format($"(P{person.Id}): "));
                }
            }
            Console.WriteLine();
            foreach (Person person in _people.Values)
            {
                string personText = String.Format($" <P{person.Id}>: ");
                Console.Write(personText);

                if(person._curRequest != null)
                    Console.Write(string.Format($"{person._curRequest.ReqFloor.FloorNo}F->{person._curRequest.TargetFloor.FloorNo}F"));
            }
            Console.WriteLine();

            foreach (Floor floor in _building._floors.Values)
            {
                string floorText = string.Format($"[{floor.FloorNo,3}] ■■■■■■■■■■■■■■■■■■■■  ");
                textArray.Add(floor.FloorNo, floorText);
            }

            Dictionary<int, string> orderedText = textArray.OrderByDescending(kv => kv.Key).ToDictionary(kv => kv.Key, kv => kv.Value);

            foreach (Elevator elevator in _building._eleMgr._elevators.Values)
            {

                string editText = ReplaceNthChar(orderedText[elevator._currentFloor.FloorNo], 11 + elevator.Id, '□');
                orderedText[elevator._currentFloor.FloorNo] = editText;
            }

            foreach (Person person in _people.Values)
            {
                if (person._location == PersonLocation.Floor)
                {
                    orderedText[person._curFloor.FloorNo]+= string.Format($"(P{person.Id})");

                }
            }

            foreach (string text in orderedText.Values)
            {
                Console.Write(text);

            }

            Console.WriteLine();

        }

        public static string ReplaceNthChar(string originalString, int n, char newChar)
        {
            // C# 문자열 인덱스는 0부터 시작하므로, 'n'번째 글자는 인덱스 (n - 1)입니다.
            int indexToChange = n - 1;

            // 문자열이 충분히 길지 않으면 원본을 그대로 반환
            if (indexToChange < 0 || indexToChange >= originalString.Length)
            {
                return originalString;
            }

            // 앞부분 (인덱스 0부터 indexToChange 직전까지)
            string prefix = originalString.Substring(0, indexToChange);

            // 뒷부분 (indexToChange + 1부터 끝까지)
            string suffix = originalString.Substring(indexToChange + 1);

            // 세 부분을 결합하여 새로운 문자열 반환
            return prefix + newChar + suffix;
        }
    }
}