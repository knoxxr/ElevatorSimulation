using knoxxr.Evelvator.Sim;

namespace knoxxr.Evelvator.Core
{
    public class Elevator
    {
        public Floor CurrentFloor;
        public int MaximumOccupancy = 15;
        public List<Person> People = new List<Person>();
        protected List<Button> Buttons = new List<Button>();
        public Elevator()
        {
        }
        public void Move(int targetFloor)
        {

        }

        public void Stop()
        {

        }
    }

    public class Button()
    {
        public void Press()
        {

        }

        public void Cancel()
        {

        }
    }
}