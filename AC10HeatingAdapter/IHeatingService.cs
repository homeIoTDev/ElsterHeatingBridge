using System;

namespace AC10Service;

public interface IHeatingService
{
    public bool RequestElsterValue(ushort senderCanId, ushort receiverCanId, ushort elster_idx);
    public void ProcessCanFrame(CanFrame frame);
}
