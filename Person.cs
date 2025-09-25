using knoxxr.Evelvator.Core;

namespace knoxxr.Evelvator.Sim
{
    public class Person
    {
        public event EventHandler<DirectionEventArgs> EventReqUp;
        public event EventHandler<DirectionEventArgs> EventReqDown;
        public event EventHandler<DirectionEventArgs> EventCancelUp;
        public event EventHandler<DirectionEventArgs> EventCancelDown;
        public event EventHandler<ButtonEventArgs> EventReqButton;
        public event EventHandler<ButtonEventArgs> EventCancelButton;
        public event EventHandler<PersonEventArgs> EventWaitingElevator;
        public event EventHandler<PersonEventArgs> EventGetInElevator;
        public event EventHandler<PersonEventArgs> EventGeOffElevator;
        public Floor CurFloor;
        public int TargetFloor;
        public PersonState State = PersonState.Waiting;
        public Person(Floor curFloor)
        {
            CurFloor = curFloor;
        }
        public void CallUp()
        {
            if (CurFloor != null)
            {
                CurFloor.ReqUpSide();
                OnReqlUp();
            }
        }
        public void CallDown()
        {
            if (CurFloor != null)
            {
                CurFloor.ReqDownSide();
                OnReqlDown();
            }
        }
        public void CancelUp()
        {
            if (CurFloor != null)
            {
                CurFloor.CancelUpSide();
                OnCancellUp();
            }
        }
        public void CancelDown()
        {
            if (CurFloor != null)
            {
                CurFloor.CancelDownSide();
                OnCancellDown();
            }
        }
        public void WaitingElevator()
        {
            State = PersonState.Waiting;
            OnWaitingElevator();
        }
        public void GetInElevator()
        {
            State = PersonState.InElevator;
            OnGetInElevator();
        }
        public void GetOffElevator()
        {
            State = PersonState.Arrived;
            OnGeOffElevator();
        }
        public void PressButton(int targetFloor)
        {
            OnReqButton(targetFloor);
        }
        public void CancelButton(int targetFloor)
        {
            OnCancelButton(targetFloor);
        }
        public void OnWaitingElevator()
        {
            PersonEventArgs args = new PersonEventArgs(this);
            EventWaitingElevator?.Invoke(this, args);
        }
        public void OnGetInElevator()
        {
            PersonEventArgs args = new PersonEventArgs(this);
            EventGetInElevator?.Invoke(this, args);
        }
        public void OnGeOffElevator()
        {
            PersonEventArgs args = new PersonEventArgs(this);
            EventGeOffElevator?.Invoke(this, args);
        }
        public void OnReqlUp()
        {
            DirectionEventArgs args = new DirectionEventArgs(Direction.Up);
            EventReqUp?.Invoke(this, args);
        }

        public void OnReqlDown()
        {
            DirectionEventArgs args = new DirectionEventArgs(Direction.Down);
            EventReqDown?.Invoke(this, args);
        }

        public void OnCancellUp()
        {
            DirectionEventArgs args = new DirectionEventArgs(Direction.Up);
            EventCancelUp?.Invoke(this, args);
        }

        public void OnCancellDown()
        {
            DirectionEventArgs args = new DirectionEventArgs(Direction.Down);
            EventCancelDown?.Invoke(this, args);
        }

        public void OnReqButton(int targetFloor)
        {
            ButtonEventArgs args = new ButtonEventArgs(targetFloor);
            EventReqButton?.Invoke(this, args);
        }
        public void OnCancelButton(int targetFloor)
        {
            ButtonEventArgs args = new ButtonEventArgs(targetFloor);
            EventReqButton?.Invoke(this, args);
        }
    }

    public enum PersonState
    {
        Waiting,
        InElevator,
        Arrived
    }

    public enum Direction
    {
        Up,
        Down,
        None
    }
    public class DirectionEventArgs : EventArgs
    {
        public Direction Direction { get; }

        public DirectionEventArgs(Direction reqDirection)
        {
            Direction = reqDirection;
        }
    }

    public class ButtonEventArgs : EventArgs
    {
        public int ReqFloor { get; }

        public ButtonEventArgs(int targetFloor)
        {
            ReqFloor = targetFloor;
        }
    }
    public class PersonEventArgs : EventArgs
    {
        public Person Person { get; }

        public PersonEventArgs(Person person)
        {
            Person = person;
        }
    }
}
