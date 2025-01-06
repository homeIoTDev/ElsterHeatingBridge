using System;

namespace HeatingDaemon;

public interface ICanBusService
{
    public bool SendCanFrame(CanFrame frame);
}
