using knoxxr.Evelvator.Core;
using knoxxr.Evelvator.Sim;

namespace knoxxr.Evelvator.Core
{
    public class Building
    {
        protected int TotalGroundFloor = 100;
        protected int TotalUndergroundFloor = 10;
        protected int TotalElevator = 2;
        public Dictionary<int, Floor> Floors = new Dictionary<int, Floor>();
        public ElevatorManager eleMgr = new ElevatorManager();
        public Sim_ReqGenerator simMgr = new Sim_ReqGenerator();

        public Building()
        {
            Initialize();
        }

        public void Initialize()
        {
            InitBuilding();
            InitElevatorManager();
        }

        public void InitBuilding()
        {
            for (int floor = 1; floor <= TotalGroundFloor; floor++)
            {
                Floor newFloor = new Floor(floor);
                Floors.Add(floor, newFloor);
            }

            for (int floor = 1; floor <= TotalUndergroundFloor; floor++)
            {
                Floor newFloor = new Floor(floor * -1);
                Floors.Add(floor, newFloor);
            }
        }

        public void InitElevatorManager()
        {
            eleMgr.Initialize(Floors, TotalElevator);
        }
    }
}