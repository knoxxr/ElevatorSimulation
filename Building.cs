using knoxxr.Evelvator.Core;
using knoxxr.Evelvator.Sim;

namespace knoxxr.Evelvator.Core
{
    public class Building
    {
        public static int TotalGroundFloor = 30;
        public static int TotalUndergroundFloor = 3;
        protected int TotalElevator = 2;
        public Dictionary<int, Floor> Floors = new Dictionary<int, Floor>();
        public ElevatorManager eleMgr = null;
        public Sim_ReqGenerator simMgr = null;

        public Building()
        {
            Initialize();
        }

        public void Initialize()
        {
            InitFloors();
            InitElevatorManager();
            LinkFloorandElevator();
        }

        protected void LinkFloorandElevator()
        {
            foreach (var floor in Floors.Values)
            {
                floor.Initialize(eleMgr);
            }
        }

        protected void InitFloors()
        {
            for (int floorNo = 1; floorNo <= TotalGroundFloor; floorNo++)
            {
                Floor newFloor = new Floor(floorNo);
                Floors.Add(newFloor.FloorNo, newFloor);
            }

            for (int floorNo = -TotalUndergroundFloor; floorNo <= -1; floorNo++)
            {
                Floor newFloor = new Floor(floorNo);
                Floors.Add(newFloor.FloorNo, newFloor);
            }

            Console.WriteLine($"Floors initialized with {TotalGroundFloor} ground floors and {TotalUndergroundFloor} underground floors.");
        }

        protected void InitElevatorManager()
        {
            eleMgr = new ElevatorManager(this);

            eleMgr.Initialize(Floors, TotalElevator);
        }
    }
}