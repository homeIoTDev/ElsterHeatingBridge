using System;

namespace HeatingDaemon;

public interface ICanBusService
{
    public bool SendCanFrame(CanFrame frame);
    public bool IsCanBusOpen{ get; } 
}
