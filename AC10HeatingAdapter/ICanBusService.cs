using System;

namespace AC10Service;

public interface ICanBusService
{
    public bool SendCanFrame(CanFrame frame);
}
