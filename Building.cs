using knoxxr.Evelvator.Core;
using knoxxr.Evelvator.Sim;

namespace knoxxr.Evelvator.Core
{
    public class Building
    {
        public static int TotalGroundFloor = 100;
        public static int TotalUndergroundFloor = 10;
        protected int TotalElevator = 2;
        public Dictionary<int, Floor> Floors = new Dictionary<int, Floor>();
        public ElevatorManager eleMgr = new ElevatorManager();
        public Sim_ReqGenerator simMgr = null;

        public Building()
        {
            Initialize();
        }

        public void Initialize()
        {
            InitBuilding();
            InitElevatorManager();
            simMgr = new Sim_ReqGenerator(this);
        }

        protected void InitSimManager()
        {
        }
        protected void InitBuilding()
        {
            for (int floorNo = 1; floorNo <= TotalGroundFloor; floorNo++)
            {
                Floor newFloor = new Floor(floorNo, eleMgr.Elevators);
                Floors.Add(newFloor.FloorNo, newFloor);
            }

            for (int floorNo = -TotalUndergroundFloor; floorNo <= -1; floorNo++)
            {
                Floor newFloor = new Floor(floorNo);
                Floors.Add(newFloor.FloorNo, newFloor);
            }

            Console.WriteLine($"Building initialized with {TotalGroundFloor} ground floors and {TotalUndergroundFloor} underground floors.");
        }

        protected void InitElevatorManager()
        {
            eleMgr.Initialize(Floors, TotalElevator);
        }
    }
}