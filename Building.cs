using knoxxr.Evelvator.Core;
using knoxxr.Evelvator.Sim;

namespace knoxxr.Evelvator.Core
{
    public class Building
    {
        public static int TotalGroundFloor = 10;
        public static int TotalUndergroundFloor = 3;
        protected int TotalElevator = 2;
        public Dictionary<int, Floor> _floors = new Dictionary<int, Floor>();
        public ElevatorManager _eleMgr = null;
        public Sim_ReqGenerator _simMgr = null;

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
            foreach (var floor in _floors.Values)
            {
                floor.Initialize(_eleMgr);
            }
        }

        protected void InitFloors()
        {
            for (int floorNo = 1; floorNo <= TotalGroundFloor; floorNo++)
            {
                Floor newFloor = new Floor(floorNo);
                _floors.Add(newFloor.FloorNo, newFloor);
            }

            for (int floorNo = -TotalUndergroundFloor; floorNo <= -1; floorNo++)
            {
                Floor newFloor = new Floor(floorNo);
                _floors.Add(newFloor.FloorNo, newFloor);
            }

            Logger.Info($"Floors initialized with {TotalGroundFloor} ground floors and {TotalUndergroundFloor} underground floors.");
        }

        protected void InitElevatorManager()
        {
            _eleMgr = new ElevatorManager(this);

            _eleMgr.Initialize(_floors, TotalElevator);
        }
    }
}